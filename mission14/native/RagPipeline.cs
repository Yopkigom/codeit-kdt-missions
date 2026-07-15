using System;
using System.Collections.Generic;
using System.Text;
using LlamaBridge;

namespace TexChatbot
{
    // Pipeline stage, surfaced to the UI (P) for status display. Does not change routing.
    public enum RagStage { Retrieving, Generating, Fallback }

    // C# port of the prototype RAG state machine (L-d LangGraph), kept 1:1 so behavior
    // matches: retrieve -> route_by_score -> generate / out_of_scope(fallback).
    // Routing signal is the top cosine score; the out-of-scope branch is a replaceable
    // hook (U/W swap in the LLM-API fallback without changing the state machine).
    public sealed class RagPipeline
    {
        // Prototype constants (L-d). Threshold is provisional, calibrated in S, verified in V.
        public const float RetrievalScoreThreshold = 0.5f;
        // R-d 개선(A): 컨텍스트 압축으로 prefill·decode 단축. top-k 5->3, 청크 캡 800->400자.
        // n_prompt ~2077 -> ~700토큰. 검색 자체는 영향 없음(생성 컨텍스트만 축소). 품질은 R-c 재측정.
        public const int TopK = 3;
        public const int MaxContextChars = 400;   // per-chunk cap (avoids large-table flooding)
        public const int MaxContextTokens = 6000; // total budget within n_ctx=8192

        public const string SystemPrompt =
            "당신은 2024년 연말정산 신고안내 문서를 근거로 답하는 도우미입니다. " +
            "아래 제공된 문서 맥락만 근거로 한국어로 정확히 답하세요. " +
            "맥락에 근거가 없으면 모른다고 답하세요. " +
            "답변 마지막에 근거 페이지를 (페이지 N) 형식으로 표기하세요.";

        public const string OutOfScopeMessage =
            "제공된 연말정산 문서에서 관련 근거를 찾지 못했습니다. (범위 밖 질의 - 2차 Fallback에서 처리 예정)";

        private readonly OnnxEmbedder _embedder;
        private readonly CosineRetriever _retriever;
        private readonly LlamaModel _llm;
        private readonly Func<RagState, string> _fallback;

        // fallback: replaces the out-of-scope node (default = guidance message, matching L-d).
        public RagPipeline(OnnxEmbedder embedder, CosineRetriever retriever, LlamaModel llm,
                           Func<RagState, string> fallback = null)
        {
            _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));
            _retriever = retriever ?? throw new ArgumentNullException(nameof(retriever));
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
            _fallback = fallback ?? (_ => OutOfScopeMessage);
        }

        // Runs the full graph. onToken (optional) streams generated pieces for TTFT/UI;
        // onStage (optional) reports the current stage for status display.
        // Blocking (native generate); callers run it on a worker thread (see ChatService).
        public RagState Run(string query, Action<string> onToken = null, Action<RagStage> onStage = null)
        {
            var state = new RagState { Query = query };

            // retrieve
            onStage?.Invoke(RagStage.Retrieving);
            float[] q = _embedder.Embed(query);
            state.Hits = _retriever.Search(q, TopK);
            state.TopScore = state.Hits.Count > 0 ? state.Hits[0].Score : 0f;

            // route_by_score
            if (state.TopScore >= RetrievalScoreThreshold)
            {
                onStage?.Invoke(RagStage.Generating);
                Generate(state, onToken);
            }
            else
            {
                onStage?.Invoke(RagStage.Fallback);
                state.Answer = _fallback(state); // out_of_scope
            }

            return state;
        }

        // Cooperative cancel for clean teardown (stops the decode loop on the next token).
        public void RequestCancel() => _llm.RequestCancel();

        private void Generate(RagState state, Action<string> onToken)
        {
            state.Contexts = BuildContextBlock(state.Hits);
            string contextBlock = string.Join("\n\n", state.Contexts);
            string user = $"문서 맥락:\n{contextBlock}\n\n질문: {state.Query}";

            int promptTokens = _llm.CountTokens(SystemPrompt) + _llm.CountTokens(user);
            UnityEngine.Debug.Log(
                $"[RAG] generate 시작: top_score={state.TopScore:F3} contexts={state.Contexts.Count} " +
                $"~promptTokens={promptTokens} userChars={user.Length} (Editor CPU에선 prefill이 수십 초일 수 있음)");

            var sb = new StringBuilder();
            state.Timings = _llm.Chat(SystemPrompt, user,
                piece => { sb.Append(piece); onToken?.Invoke(piece); },
                maxTokens: 256, temp: 0.2f, topP: 0.9f, repeatPenalty: 1.1f, seed: 42);
            state.Answer = sb.ToString();
        }

        // Mirrors build_context_block: per-chunk char cap + total Gemma-token budget.
        private List<string> BuildContextBlock(List<Hit> hits)
        {
            var parts = new List<string>();
            int total = 0;
            foreach (Hit h in hits)
            {
                string text = h.Text.Length > MaxContextChars
                    ? h.Text.Substring(0, MaxContextChars) : h.Text;
                string piece = $"[페이지 {h.Page}] {text}";
                int n = _llm.CountTokens(piece);
                if (parts.Count > 0 && total + n > MaxContextTokens) break;
                parts.Add(piece);
                total += n;
            }
            return parts;
        }
    }

    // 1:1 with the prototype RAGState (query / hits / top_score / contexts / answer).
    public sealed class RagState
    {
        public string Query;
        public List<Hit> Hits = new List<Hit>();
        public float TopScore;
        public List<string> Contexts = new List<string>();
        public string Answer = string.Empty;
        public LbTimings Timings;   // generate 경로에서만 채워짐 (R-b 속도 측정)

        public bool RoutedToGenerate => TopScore >= RagPipeline.RetrievalScoreThreshold;
    }
}
