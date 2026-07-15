using System;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Networking;
#endif
using Debug = UnityEngine.Debug;

namespace LlamaBridge
{
    // E-b-2 검증 드라이버: llama.cpp 직접 통합(P/Invoke)이 Unity에서 동작하는지 자동 점검.
    // 빈 GameObject에 부착 -> StreamingAssets에 GGUF 배치 -> Play.
    // 네이티브 호출은 블로킹이므로 워커 스레드에서 수행하고, 토큰은 큐로 받아 메인 스레드에서 소비한다.
    public sealed class LlamaVerification : MonoBehaviour
    {
        [Header("Model")]
        [Tooltip("StreamingAssets 아래 파일명")]
        [SerializeField] private string _modelFileName = "gemma-4-E2B-it-Q4_K_M.gguf";
        [SerializeField] private int _nCtx = 8192;
        [Tooltip("S25(Snapdragon 8 Elite)는 prime 코어 2개가 decode에 가장 빠름 (E-c-2: 6→16.3, 2→23.1 tok/s)")]
        [SerializeField] private int _nThreads = 2;
        [Tooltip("0 = CPU 전용(Android 기준). Editor에서 GPU offload 실험 시 증가")]
        [SerializeField] private int _nGpuLayers = 0;

        [Header("Generation (프로토타입 패리티 파라미터)")]
        [SerializeField] private string _systemPrompt = "당신은 한국어로 간결히 답하는 도우미입니다.";
        [SerializeField, TextArea] private string _userPrompt = "연말정산이 무엇인지 한 문장으로 설명해줘.";
        [SerializeField] private int _maxTokens = 256;
        [SerializeField] private float _temperature = 0.2f;
        [SerializeField] private float _topP = 0.9f;
        [SerializeField] private float _repeatPenalty = 1.1f;
        [SerializeField] private uint _seed = 42;

        [Header("E-c-2. 256토큰 측정 (벤치)")]
        [Tooltip("기능 검증 후 ignore_eos로 정확히 N토큰을 강제 생성해 순수 decode wall-clock 측정")]
        [SerializeField] private bool _run256Benchmark = true;
        [SerializeField] private int _benchmarkTokens = 256;

        [Header("Output (선택)")]
        [Tooltip("실시간 토큰을 표시할 UI 텍스트(없으면 Console에만 출력)")]
        [SerializeField] private UnityEngine.UI.Text _outputText;

        private readonly ConcurrentQueue<string> _tokenQueue = new ConcurrentQueue<string>();
        private readonly StringBuilder _answer = new StringBuilder();
        private int _tokenChunks;

        public string Answer => _answer.ToString();

        private IEnumerator Start()
        {
            yield return RunVerification();
        }

        private IEnumerator RunVerification()
        {
            var report = new StringBuilder();
            report.AppendLine("=== E-b-2. llama.cpp Editor 검증 ===");

            // 1) 모델 경로 확보 (Android는 StreamingAssets -> persistentDataPath 복사)
            string modelPath = null;
            yield return EnsureModelAvailable(_modelFileName, p => modelPath = p);
            report.AppendLine($"model: {modelPath}");
            if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            {
                report.AppendLine("[FAIL] 모델 파일을 찾을 수 없음 (StreamingAssets 배치 확인)");
                Debug.LogError(report.ToString());
                yield break;
            }

            // 2) 로드 (워커 스레드)
            LlamaModel model = null;
            var loadTask = Task.Run(() =>
            {
                model = new LlamaModel(modelPath, _nCtx, _nThreads, nGpuLayers: _nGpuLayers);
            });
            while (!loadTask.IsCompleted) yield return null;
            bool loadOk = loadTask.Exception == null && model != null;
            report.AppendLine($"[{(loadOk ? "PASS" : "FAIL")}] lb_load 성공");
            if (!loadOk)
            {
                report.AppendLine($"  error: {loadTask.Exception?.GetBaseException().Message}");
                Debug.LogError(report.ToString());
                yield break;
            }

            // 3) 생성 (워커 스레드, 토큰은 큐로 스트리밍)
            _answer.Clear();
            _tokenChunks = 0;
            if (_outputText != null) _outputText.text = string.Empty;

            LbTimings timings = default;
            var genTask = Task.Run(() =>
            {
                timings = model.Chat(_systemPrompt, _userPrompt,
                    piece => _tokenQueue.Enqueue(piece),
                    _maxTokens, _temperature, _topP, _repeatPenalty, _seed);
            });

            // 메인 스레드: 매 프레임 큐 비우며 UI/Console 갱신
            while (!genTask.IsCompleted || !_tokenQueue.IsEmpty)
            {
                DrainTokens();
                yield return null;
            }
            DrainTokens();

            if (genTask.Exception != null)
            {
                report.AppendLine($"[FAIL] lb_generate 예외: {genTask.Exception.GetBaseException().Message}");
                Debug.LogError(report.ToString());
                model.Dispose();
                yield break;
            }

            // 4) 체크리스트 판정
            string answer = _answer.ToString();
            bool genOk     = timings.NDecoded > 0 && answer.Length > 0;
            bool streamOk  = _tokenChunks > 1;
            bool koreanOk  = ContainsHangul(answer);
            bool paramOk   = timings.NDecoded <= _maxTokens;

            report.AppendLine($"[{P(genOk)}] 단일 프롬프트 추론 응답 생성 (decoded={timings.NDecoded})");
            report.AppendLine($"[{P(streamOk)}] 토큰 스트리밍 콜백 동작 (chunks={_tokenChunks})");
            report.AppendLine($"[{P(koreanOk)}] 한국어 프롬프트 정상 응답");
            report.AppendLine($"[{P(paramOk)}] 생성 파라미터 반영 (max={_maxTokens}, temp={_temperature}, top_p={_topP}, rep={_repeatPenalty}, seed={_seed})");
            report.AppendLine($"--- 측정(참고용, {PlatformLabel()}) ---");
            report.AppendLine($"  prompt tokens : {timings.NPrompt}");
            report.AppendLine($"  prefill       : {timings.PrefillMs:F1} ms");
            report.AppendLine($"  TTFT          : {timings.TtftMs:F1} ms");
            report.AppendLine($"  decode        : {timings.DecodeMs:F1} ms ({DecodeTps(timings):F1} tok/s)");
            report.AppendLine($"  wall-clock    : {(timings.PrefillMs + timings.DecodeMs):F1} ms (prefill+decode)");
            report.AppendLine("--- 응답 ---");
            report.AppendLine(answer);

            bool allPass = loadOk && genOk && streamOk && koreanOk && paramOk;
            if (allPass) Debug.Log(report.ToString());
            else         Debug.LogWarning(report.ToString());

            // E-c-2: 순수 256토큰 decode wall-clock (EOS 무시, 정확히 N토큰 강제)
            if (_run256Benchmark)
            {
                // 새 직렬화 필드가 0으로 역직렬화되는 Unity 함정 가드.
                int benchTokens = _benchmarkTokens > 0 ? _benchmarkTokens : 256;
                LbTimings bt = default;
                Exception benchErr = null;

                // 스레드풀(Task.Run) 대신 전용 백그라운드 스레드 사용:
                // 첫 생성 직후 풀 스레드 고갈로 두 번째 Task가 시작되지 않는 문제를 회피.
                // 완료 판정은 Thread.IsAlive로 폴링한다.
                var th = new System.Threading.Thread(() =>
                {
                    try
                    {
                        bt = model.Chat(_systemPrompt, _userPrompt, _ => { },
                            benchTokens, _temperature, _topP, _repeatPenalty, _seed, ignoreEos: true);
                    }
                    catch (Exception e) { benchErr = e; }
                })
                { IsBackground = true, Name = "llama-bench" };

                Debug.Log($"E-c-2 {benchTokens}토큰 측정 시작…");
                th.Start();
                while (th.IsAlive) yield return null;

                var bench = new StringBuilder();
                bench.AppendLine($"=== E-c-2. {benchTokens}토큰 측정 (ignore_eos, {PlatformLabel()}) ===");
                if (benchErr != null)
                {
                    bench.AppendLine($"[FAIL] {benchErr.Message}");
                    Debug.LogError(bench.ToString());
                }
                else
                {
                    double wall = bt.PrefillMs + bt.DecodeMs;
                    bool budgetPass = wall <= 15000.0;
                    bench.AppendLine($"  prompt tokens : {bt.NPrompt}");
                    bench.AppendLine($"  decode tokens : {bt.NDecoded}");
                    bench.AppendLine($"  prefill       : {bt.PrefillMs:F1} ms");
                    bench.AppendLine($"  decode        : {bt.DecodeMs:F1} ms ({DecodeTps(bt):F1} tok/s)");
                    bench.AppendLine($"  wall-clock    : {wall:F1} ms ({wall / 1000.0:F2} s, prefill+decode)");
                    bench.AppendLine($"  합격선(<=15s) : {(budgetPass ? "PASS" : "FAIL")}");
                    if (budgetPass) Debug.Log(bench.ToString());
                    else            Debug.LogWarning(bench.ToString());
                }
            }

            model.Dispose();
        }

        private void DrainTokens()
        {
            bool changed = false;
            while (_tokenQueue.TryDequeue(out string piece))
            {
                _answer.Append(piece);
                _tokenChunks++;
                changed = true;
            }
            if (changed && _outputText != null) _outputText.text = _answer.ToString();
        }

        private static string P(bool ok) => ok ? "PASS" : "FAIL";

        private static string PlatformLabel()
        {
#if UNITY_EDITOR
            return "Editor/PC, CPU";
#elif UNITY_ANDROID
            return "Android 기기";
#else
            return Application.platform.ToString();
#endif
        }

        private static double DecodeTps(LbTimings t) =>
            t.DecodeMs > 0.0 ? t.NDecoded / (t.DecodeMs / 1000.0) : 0.0;

        private static bool ContainsHangul(string s)
        {
            foreach (char c in s)
            {
                if ((c >= 0xAC00 && c <= 0xD7A3) ||  // 완성형 음절
                    (c >= 0x1100 && c <= 0x11FF) ||  // 자모
                    (c >= 0x3130 && c <= 0x318F))    // 호환 자모
                    return true;
            }
            return false;
        }

        // StreamingAssets는 Editor/Standalone에서 실제 경로지만, Android에서는 APK(jar) 내부라
        // UnityWebRequest로 읽어 persistentDataPath로 복사해야 한다(최초 1회).
        private IEnumerator EnsureModelAvailable(string fileName, Action<string> onResolved)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string dst = Path.Combine(Application.persistentDataPath, fileName);
            if (!File.Exists(dst))
            {
                string src = Path.Combine(Application.streamingAssetsPath, fileName);
                using (var req = UnityWebRequest.Get(src))
                {
                    // The GGUF (~1.6GB) is far too large to hold in a single byte[]
                    // (downloadHandler.data would be null/OOM). Stream straight to disk.
                    req.downloadHandler = new DownloadHandlerFile(dst) { removeFileOnAbort = true };
                    Debug.Log($"모델 복사 시작(최초 1회): {src} -> {dst}");
                    yield return req.SendWebRequest();
                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogError($"모델 복사 실패: {req.error}");
                        onResolved(null);
                        yield break;
                    }
                    Debug.Log("모델 복사 완료.");
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
