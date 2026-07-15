using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LlamaBridge;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace TexChatbot
{
    // R-c 자동 재평가(이식 손실 점검): 골든 Q&A 50문항을 Unity 파이프라인에 그대로 돌려
    // 답변·검색 top-k·타이밍을 덤프한다. 출력(persistentDataPath/eval_unity_answers.json)을
    // adb pull 한 뒤 노트북 R-c 셀에서 Python(M과 동일 Gemini 하니스)으로 재채점한다.
    // 빈 GameObject에 부착 → 자산 배치(소형 StreamingAssets, 대용량 push) → Play.
    public sealed class GoldenEvalRunner : MonoBehaviour
    {
        [SerializeField] private string _ggufFile = "gemma-4-E2B-it-Q4_K_M.gguf";
        [SerializeField] private string _onnxFile = "kure_v1_fp16.onnx";
        [SerializeField] private string _onnxDataFile = "kure_v1_fp16.onnx.data";
        [SerializeField] private string _spmFile = "kure_v1_spm.model";
        [SerializeField] private string _npyFile = "index_embeddings.npy";
        [SerializeField] private string _chunksFile = "index_chunks.json";
        [SerializeField] private string _manifestFile = "index_manifest.json";
        [SerializeField] private string _goldenFile = "golden_qa.json";    // Export -> StreamingAssets
        [SerializeField] private string _outFile = "eval_unity_answers.json";
        [SerializeField] private int _searchTopK = 10;                     // Recall@10 까지 산출용

        private sealed class EvalRow
        {
            public int qid;
            public string question;
            public string answer;
            public List<string> contexts;
            public float top_score;
            public string route;
            public List<string> top_chunk_ids;   // Unity 검색 top-k (검색 측 재측정용)
            public double prefill_ms, decode_ms, ttft_ms, tok_s;
            public int n_prompt, n_decoded;
        }

        private IEnumerator Start()
        {
            string gguf = null, onnx = null, spm = null, npy = null, chunks = null, manifest = null, golden = null;
            yield return AssetResolver.Ensure(_spmFile, p => spm = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_onnxDataFile, _ => { });
            yield return AssetResolver.Ensure(_onnxFile, p => onnx = p);
            yield return AssetResolver.Ensure(_npyFile, p => npy = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_chunksFile, p => chunks = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_manifestFile, p => manifest = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_goldenFile, p => golden = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_ggufFile, p => gguf = p);

            if (golden == null || !File.Exists(golden))
            {
                Debug.LogError($"[Eval] golden 파일 없음: {_goldenFile} (Export/golden_qa.json을 StreamingAssets에 배치)");
                yield break;
            }
            JArray goldenArr = JArray.Parse(File.ReadAllText(golden));

            OnnxEmbedder embedder = null; LlamaModel llm = null; CosineRetriever retriever = null; RagPipeline pipeline = null;
            Exception initErr = null;
            var initTask = Task.Run(() =>
            {
                try
                {
                    var tok = new KureTokenizer(spm);
                    embedder = new OnnxEmbedder(onnx, tok);
                    IndexStore index = IndexStore.Load(npy, chunks, manifest);
                    retriever = new CosineRetriever(index);
                    llm = new LlamaModel(gguf, nGpuLayers: 99); // Vulkan 빌드면 GPU offload
                    pipeline = new RagPipeline(embedder, retriever, llm);
                }
                catch (Exception e) { initErr = e; }
            });
            while (!initTask.IsCompleted) yield return null;
            if (initErr != null) { Debug.LogError($"[Eval] 초기화 실패: {initErr.Message}"); yield break; }
            Debug.Log($"[Eval] 파이프라인 로드 완료. 골든 {goldenArr.Count}문항 평가 시작 (수 분 소요).");

            var rows = new List<EvalRow>(goldenArr.Count);
            for (int i = 0; i < goldenArr.Count; i++)
            {
                string question = goldenArr[i]["question"]?.ToString() ?? string.Empty;
                int qid = goldenArr[i]["qid"]?.Value<int>() ?? i;

                EvalRow row = null; Exception err = null;
                var th = new Thread(() =>
                {
                    try
                    {
                        float[] q = embedder.Embed(question);
                        List<Hit> topN = retriever.Search(q, _searchTopK);
                        RagState st = pipeline.Run(question);
                        LbTimings tm = st.Timings;
                        double tps = tm.DecodeMs > 0 ? tm.NDecoded / (tm.DecodeMs / 1000.0) : 0.0;
                        row = new EvalRow
                        {
                            qid = qid, question = question, answer = st.Answer, contexts = st.Contexts,
                            top_score = st.TopScore, route = st.RoutedToGenerate ? "generate" : "fallback",
                            top_chunk_ids = topN.Select(h => h.ChunkId).ToList(),
                            prefill_ms = tm.PrefillMs, decode_ms = tm.DecodeMs, ttft_ms = tm.TtftMs,
                            tok_s = tps, n_prompt = tm.NPrompt, n_decoded = tm.NDecoded,
                        };
                    }
                    catch (Exception e) { err = e; }
                }) { IsBackground = true, Name = "eval" };
                th.Start();
                while (th.IsAlive) yield return null;

                if (err != null) { Debug.LogError($"[Eval] qid={qid} 실패: {err.Message}"); continue; }
                rows.Add(row);
                if ((i + 1) % 5 == 0 || i == goldenArr.Count - 1)
                    Debug.Log($"[Eval] {i + 1}/{goldenArr.Count} (qid={qid} route={row.route} top={row.top_score:F3})");
            }

            string outPath = Path.Combine(Application.persistentDataPath, _outFile);
            File.WriteAllText(outPath, JsonConvert.SerializeObject(rows, Formatting.Indented));
            Debug.Log($"[Eval] 완료: {rows.Count}건 -> {outPath}");
            Debug.Log($"[Eval] adb pull: adb pull /sdcard/Android/data/<pkg>/files/{_outFile} Export/");

            embedder?.Dispose();
            llm?.Dispose();
        }
    }
}
