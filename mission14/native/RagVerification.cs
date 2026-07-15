using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Networking;
#endif
using Debug = UnityEngine.Debug;

namespace TexChatbot
{
    // N-h / O-b verification driver: checks the Unity port against the Python golden
    // (Export/parity_fixtures.json) on-device.
    //   N-h: C# token ids == Python token ids (XLM-R remapping, no prefix).
    //   O-b: cosine(C# query embedding, Python query embedding) >= 0.999.
    // Optionally runs one RAG query (N-g sanity: retrieve -> route).
    // Attach to an empty GameObject; place assets in StreamingAssets; Play.
    public sealed class RagVerification : MonoBehaviour
    {
        [Header("Assets (StreamingAssets file names)")]
        [SerializeField] private string _spmFile = "kure_v1_spm.model";
        [SerializeField] private string _onnxFile = "kure_v1_fp16.onnx";     // + .onnx.data alongside
        [SerializeField] private string _onnxDataFile = "kure_v1_fp16.onnx.data";
        [SerializeField] private string _npyFile = "index_embeddings.npy";
        [SerializeField] private string _chunksFile = "index_chunks.json";
        [SerializeField] private string _manifestFile = "index_manifest.json";
        [SerializeField] private string _fixturesFile = "parity_fixtures.json";

        [Header("Thresholds")]
        [SerializeField] private float _cosineThreshold = 0.999f;

        [Header("N-g RAG sanity (선택, GGUF·LLM 필요)")]
        [SerializeField] private bool _runRagSample = false;
        [SerializeField] private string _ggufFile = "gemma-4-E2B-it-Q4_K_M.gguf";
        [SerializeField] private string _ragQuery = "월세 세액공제를 받으려면 어떤 요건을 충족해야 하나요?";

        private IEnumerator Start() => RunVerification();

        private IEnumerator RunVerification()
        {
            var report = new StringBuilder();
            report.AppendLine("=== N-h / O-b. Unity 포팅 패리티 검증 ===");

            // 0) 자산 확보 (대용량 onnx.data/gguf는 persistentDataPath로 스트리밍 복사)
            string spm = null, onnx = null, npy = null, chunks = null, manifest = null, fixtures = null;
            yield return Ensure(_spmFile, p => spm = p, alwaysRefresh: true);
            yield return Ensure(_onnxDataFile, _ => { });  // onnx 옆에 동반 (대용량, copy-once/adb push)
            yield return Ensure(_onnxFile, p => onnx = p);
            yield return Ensure(_npyFile, p => npy = p, alwaysRefresh: true);
            yield return Ensure(_chunksFile, p => chunks = p, alwaysRefresh: true);
            yield return Ensure(_manifestFile, p => manifest = p, alwaysRefresh: true);
            yield return Ensure(_fixturesFile, p => fixtures = p, alwaysRefresh: true);

            if (!AllExist(report, spm, onnx, npy, chunks, fixtures))
            {
                Debug.LogError(report.ToString());
                yield break;
            }

            // 1) 골든 로드 (파일 구조: {manifest, fixtures:[...]})
            JArray fx = (JArray)JObject.Parse(File.ReadAllText(fixtures))["fixtures"];

            // 2) 컴포넌트 초기화 (ONNX 세션 로드는 워커 스레드)
            KureTokenizer tokenizer = null;
            OnnxEmbedder embedder = null;
            IndexStore index = null;
            Exception initErr = null;
            var initTask = Task.Run(() =>
            {
                try
                {
                    tokenizer = new KureTokenizer(spm);
                    embedder = new OnnxEmbedder(onnx, tokenizer);
                    index = IndexStore.Load(npy, chunks, manifest);
                }
                catch (Exception e) { initErr = e; }
            });
            while (!initTask.IsCompleted) yield return null;
            if (initErr != null)
            {
                report.AppendLine($"[FAIL] 초기화: {initErr.Message}");
                Debug.LogError(report.ToString());
                yield break;
            }
            report.AppendLine($"[PASS] 로드: 인덱스 {index.Count}청크 / dim {index.Dim} / 매니페스트 일치");

            // 3) N-h 토큰 ID 패리티 + O-b 임베딩 코사인 패리티 (워커 스레드)
            int total = fx.Count, tokMatch = 0, cosPass = 0, top5Match = 0;
            float minCos = 1f;
            var diffs = new StringBuilder();
            Exception runErr = null;
            var runTask = Task.Run(() =>
            {
                try
                {
                    var retriever = new CosineRetriever(index);
                    for (int i = 0; i < fx.Count; i++)
                    {
                        string text = fx[i]["text"].ToString();
                        long[] goldIds = ToLongArray(fx[i]["token_ids"]);
                        float[] goldEmb = ToFloatArray(fx[i]["embedding"]);
                        string[] goldTop5 = ToStringArray(fx[i]["top5_chunk_ids"]);

                        long[] ids = tokenizer.Encode(text, addSpecialTokens: true);
                        bool idOk = SequenceEqual(ids, goldIds);
                        if (idOk) tokMatch++;
                        else if (diffs.Length < 1200)
                            diffs.AppendLine($"  [{i}] tok diff: C#={Join(ids)} | py={Join(goldIds)}");

                        float[] emb = embedder.Embed(text);
                        float cos = Cosine(emb, goldEmb);
                        if (cos < minCos) minCos = cos;
                        if (cos >= _cosineThreshold) cosPass++;

                        // O-b: C# brute-force top-5 chunk_id == Python golden top-5 (ordered).
                        if (goldTop5 != null)
                        {
                            string[] csTop5 = retriever.Search(emb, 5).Select(h => h.ChunkId).ToArray();
                            bool top5Ok = SequenceEqual(csTop5, goldTop5);
                            if (top5Ok) top5Match++;
                            else if (diffs.Length < 2000)
                                diffs.AppendLine($"  [{i}] top5 diff: C#=[{string.Join(",", csTop5)}] | py=[{string.Join(",", goldTop5)}]");
                        }
                    }
                }
                catch (Exception e) { runErr = e; }
            });
            while (!runTask.IsCompleted) yield return null;
            if (runErr != null)
            {
                report.AppendLine($"[FAIL] 패리티 실행: {runErr.Message}");
                Debug.LogError(report.ToString());
                embedder.Dispose();
                yield break;
            }

            bool tokAllPass = tokMatch == total;
            bool cosAllPass = cosPass == total;
            bool top5AllPass = top5Match == total;
            report.AppendLine($"[{P(tokAllPass)}] N-h 토큰 ID 일치 : {tokMatch}/{total}");
            report.AppendLine($"[{P(cosAllPass)}] O-b 임베딩 코사인 >= {_cosineThreshold} : {cosPass}/{total} (min cos = {minCos:F5})");
            report.AppendLine($"[{P(top5AllPass)}] O-b top-5 chunk_id 일치 : {top5Match}/{total}");
            if (diffs.Length > 0) report.Append(diffs);

            // 4) N-g RAG 상태기계 sanity (선택)
            if (_runRagSample)
                yield return RunRagSample(report, tokenizer, embedder, index);

            embedder.Dispose();
            bool allPass = tokAllPass && cosAllPass && top5AllPass;
            if (allPass) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        private IEnumerator RunRagSample(StringBuilder report, KureTokenizer tok,
            OnnxEmbedder embedder, IndexStore index)
        {
            string gguf = null;
            yield return Ensure(_ggufFile, p => gguf = p);
            if (string.IsNullOrEmpty(gguf) || !File.Exists(gguf))
            {
                report.AppendLine("[SKIP] N-g RAG sanity: GGUF 없음");
                yield break;
            }

            var retriever = new CosineRetriever(index);
            LlamaBridge.LlamaModel llm = null;
            RagState st = null;
            Exception err = null;
            var th = new System.Threading.Thread(() =>
            {
                try
                {
                    llm = new LlamaBridge.LlamaModel(gguf);
                    var rag = new RagPipeline(embedder, retriever, llm);
                    st = rag.Run(_ragQuery);
                }
                catch (Exception e) { err = e; }
            }) { IsBackground = true, Name = "rag-sample" };
            th.Start();
            while (th.IsAlive) yield return null;

            if (err != null) { report.AppendLine($"[FAIL] N-g RAG: {err.Message}"); }
            else
            {
                report.AppendLine($"--- N-g RAG sanity ---");
                report.AppendLine($"  query     : {_ragQuery}");
                report.AppendLine($"  top_score : {st.TopScore:F4} -> route={(st.RoutedToGenerate ? "generate" : "out_of_scope")}");
                report.AppendLine($"  답변      : {Trim(st.Answer, 220)}");
            }
            llm?.Dispose();
        }

        // ---- helpers ----
        private static string P(bool ok) => ok ? "PASS" : "FAIL";

        private static bool AllExist(StringBuilder rep, params string[] paths)
        {
            bool ok = true;
            foreach (string p in paths)
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) { rep.AppendLine($"[FAIL] 누락: {p}"); ok = false; }
            return ok;
        }

        private static long[] ToLongArray(JToken t)
        {
            var a = (JArray)t; var r = new long[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = a[i].Value<long>();
            return r;
        }

        private static float[] ToFloatArray(JToken t)
        {
            var a = (JArray)t; var r = new float[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = a[i].Value<float>();
            return r;
        }

        private static string[] ToStringArray(JToken t)
        {
            if (t == null) return null;
            var a = (JArray)t; var r = new string[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = a[i].ToString();
            return r;
        }

        private static bool SequenceEqual(long[] a, long[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool SequenceEqual(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static float Cosine(float[] a, float[] b)
        {
            // Both sides are L2-normalized -> dot product. Re-normalize defensively.
            double dot = 0, na = 0, nb = 0;
            for (int i = 0; i < a.Length; i++) { dot += (double)a[i] * b[i]; na += (double)a[i] * a[i]; nb += (double)b[i] * b[i]; }
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-12));
        }

        private static string Join(long[] a)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < a.Length; i++) { if (i > 0) sb.Append(','); sb.Append(a[i]); }
            return sb.Append(']').ToString();
        }

        private static string Trim(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) + "..." : s;

        // On Android, copy StreamingAssets -> persistentDataPath. alwaysRefresh=true forces
        // re-copy each run for small config/fixtures (so updated assets never go stale after
        // an APK update); large models (onnx.data, gguf) are copy-once / adb-pushed.
        private IEnumerator Ensure(string fileName, Action<string> onResolved, bool alwaysRefresh = false)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string dst = Path.Combine(Application.persistentDataPath, fileName);
            if (alwaysRefresh && File.Exists(dst)) File.Delete(dst);
            if (!File.Exists(dst))
            {
                string src = Path.Combine(Application.streamingAssetsPath, fileName);
                using (var req = UnityWebRequest.Get(src))
                {
                    req.downloadHandler = new DownloadHandlerFile(dst) { removeFileOnAbort = true };
                    Debug.Log($"자산 복사(최초 1회): {fileName}");
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"자산 복사 실패 {fileName}: {req.error}");
                        onResolved(null);
                        yield break;
                    }
                }
            }
            onResolved(dst);
#else
            onResolved(Path.Combine(Application.streamingAssetsPath, fileName));
            yield break;
#endif
        }
    }
}
