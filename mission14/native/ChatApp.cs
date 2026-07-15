using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using LlamaBridge;
using UnityEngine;

namespace TexChatbot
{
    // Composition root: resolves assets, builds the RAG pipeline + ChatService off the main
    // thread, then wires the scene's ChatView through ChatPresenter. Attach to a GameObject
    // and assign the scene ChatView. Heavy I/O (model load) is async so the UI stays responsive.
    public sealed class ChatApp : MonoBehaviour
    {
        [SerializeField] private ChatView _view;

        [Header("자산 파일명 (StreamingAssets / persistentDataPath)")]
        [SerializeField] private string _ggufFile = "gemma-4-E2B-it-Q4_K_M.gguf";
        [SerializeField] private string _onnxFile = "kure_v1_fp16.onnx";
        [SerializeField] private string _onnxDataFile = "kure_v1_fp16.onnx.data";
        [SerializeField] private string _spmFile = "kure_v1_spm.model";
        [SerializeField] private string _npyFile = "index_embeddings.npy";
        [SerializeField] private string _chunksFile = "index_chunks.json";
        [SerializeField] private string _manifestFile = "index_manifest.json";
        [Tooltip("범위 밖 질의 Gemini Fallback API 키 파일(선택). StreamingAssets 또는 persistentDataPath. 없으면 안내 메시지.")]
        [SerializeField] private string _geminiKeyFile = "gemini.key";

        private ChatService _service;
        private ChatPresenter _presenter;
        private OnnxEmbedder _embedder;
        private LlamaModel _llm;
        private GeminiFallback _gemini;

        // Load progress, written off-thread and pushed to the UI from Update() (main thread).
        private volatile bool _loading;
        private volatile float _loadProgress;
        private volatile string _loadStage = string.Empty;

        private IEnumerator Start()
        {
            if (_view == null) { Debug.LogError("ChatApp: ChatView 미할당"); yield break; }
            _view.SetInteractable(false);
            _loading = true;
            var loadSw = System.Diagnostics.Stopwatch.StartNew();   // R-b 콜드스타트 로드시간
            Report(0.05f, "자산 준비 중…");

            string gguf = null, onnx = null, spm = null, npy = null, chunks = null, manifest = null;
            yield return AssetResolver.Ensure(_spmFile, p => spm = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_onnxDataFile, _ => { });           // onnx 동반 (대용량)
            yield return AssetResolver.Ensure(_onnxFile, p => onnx = p);
            yield return AssetResolver.Ensure(_npyFile, p => npy = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_chunksFile, p => chunks = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_manifestFile, p => manifest = p, alwaysRefresh: true);
            yield return AssetResolver.Ensure(_ggufFile, p => gguf = p);
            // 선택: Gemini Fallback 키 (없으면 fallback은 안내 메시지로 동작)
            string geminiKeyPath = null;
            yield return AssetResolver.Ensure(_geminiKeyFile, p => geminiKeyPath = p);

            // 무거운 로드(ONNX 세션 + GGUF)는 워커 스레드에서. 단계별 진행률을 보고.
            Exception err = null;
            var task = Task.Run(() =>
            {
                try
                {
                    Report(0.15f, "토크나이저 로드 중…");
                    var tokenizer = new KureTokenizer(spm);
                    Report(0.25f, "임베딩 모델(ONNX) 로드 중…");
                    _embedder = new OnnxEmbedder(onnx, tokenizer);
                    Report(0.60f, "인덱스 로드 중…");
                    IndexStore index = IndexStore.Load(npy, chunks, manifest);
                    var retriever = new CosineRetriever(index);
                    Report(0.70f, "LLM(GGUF) 로드 중…");
                    _llm = new LlamaModel(gguf, nGpuLayers: 99); // Vulkan 빌드면 전 레이어 GPU offload, CPU 빌드면 무시
                    Report(0.98f, "마무리 중…");

                    // 범위 밖 질의용 Gemini Fallback (W). 키 파일이 있으면 연결, 없으면 기본 안내 메시지.
                    Func<RagState, string> fallback = null;
                    string geminiKey = (geminiKeyPath != null && File.Exists(geminiKeyPath))
                        ? File.ReadAllText(geminiKeyPath).Trim() : null;
                    if (!string.IsNullOrEmpty(geminiKey))
                    {
                        _gemini = new GeminiFallback(geminiKey);
                        fallback = _gemini.Answer;
                    }

                    var pipeline = new RagPipeline(_embedder, retriever, _llm, fallback);
                    _service = new ChatService(pipeline);
                    Report(1.00f, "준비 완료");
                }
                catch (Exception e) { err = e; }
            });
            while (!task.IsCompleted) yield return null;

            _loading = false;
            _view.ShowProgress(false);
            if (err != null)
            {
                _view.SetStatus($"로딩 실패: {err.Message}");
                Debug.LogError($"ChatApp 로딩 실패: {err}");
                yield break;
            }

            _presenter = new ChatPresenter(_view, _service);
            _view.SetStatus(string.Empty);
            _view.SetInteractable(true);
            Debug.Log($"[R-b] model load (cold) = {loadSw.ElapsedMilliseconds} ms");
        }

        private void Report(float progress, string stage)
        {
            _loadProgress = progress;
            _loadStage = stage;
        }

        private void Update()
        {
            // During load, mirror the latest progress/stage to the UI on the main thread.
            if (_loading)
            {
                _view.ShowProgress(true, _loadProgress);
                _view.SetStatus(_loadStage);
            }
            _presenter?.Tick();
        }

        private void OnDestroy()
        {
            _presenter?.Dispose();
            // Stop a running turn and wait for the worker before unloading the native model.
            // If generation is still in flight (prefill cannot be interrupted), skip native
            // disposal rather than free it under an active lb_generate call (Editor crash).
            // The leak is reclaimed on domain reload / process exit.
            if (_service != null)
            {
                _service.Cancel();
                _service.WaitForIdle(3000);
                if (_service.IsBusy)
                {
                    Debug.LogWarning("ChatApp: 생성이 진행 중이라 네이티브 해제를 건너뜀(prefill은 중단 불가). 정지 전 생성 완료를 권장.");
                    return;
                }
            }
            _embedder?.Dispose();
            _llm?.Dispose();
            _gemini?.Dispose();
        }
    }
}
