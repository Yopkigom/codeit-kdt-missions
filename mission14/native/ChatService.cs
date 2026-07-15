using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace TexChatbot
{
    // Where the answer came from (drives the UI source chip / fallback badge).
    public enum AnswerSource { InScopeRag, OutOfScopeFallback }

    // Coarse UI status for the current turn.
    public enum ChatStage { Idle, Retrieving, Generating, Fallback, Done, Error }

    public sealed class ChatResult
    {
        public string Query;
        public string Answer;
        public AnswerSource Source;
        public float TopScore;
        public int[] Pages;        // distinct source pages used as context (InScope only)
    }

    public enum ChatEventType { Stage, Token, Done, Error }

    // One unit of progress, produced on the worker thread and consumed on the main thread.
    public struct ChatEvent
    {
        public ChatEventType Type;
        public ChatStage Stage;    // Stage
        public string Text;        // Token piece / Error message
        public ChatResult Result;  // Done
    }

    // MVP Model: wraps the RAG state machine (N-g) with async execution + token streaming
    // + source metadata. Native generation blocks, so a turn runs on a background thread;
    // events are queued and pumped on Unity's main thread by the presenter.
    public sealed class ChatService
    {
        private readonly RagPipeline _pipeline;
        private readonly ConcurrentQueue<ChatEvent> _events = new ConcurrentQueue<ChatEvent>();
        private volatile bool _busy;
        private Thread _worker;

        public bool IsBusy => _busy;

        public ChatService(RagPipeline pipeline)
        {
            _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        }

        // Non-blocking. Returns false if a turn is already running or the query is empty.
        public bool Submit(string query)
        {
            if (_busy || string.IsNullOrWhiteSpace(query)) return false;
            _busy = true;
            _worker = new Thread(() => Work(query)) { IsBackground = true, Name = "rag-chat" };
            _worker.Start();
            return true;
        }

        // Drain on the main thread (presenter calls this each frame).
        public bool TryDequeue(out ChatEvent ev) => _events.TryDequeue(out ev);

        // Teardown: ask the decode loop to stop and wait briefly for the worker, so the
        // native call is not torn down mid-flight on Play-mode exit (prefill is uninterruptible).
        public void Cancel() => _pipeline.RequestCancel();
        public void WaitForIdle(int timeoutMs = 2000) => _worker?.Join(timeoutMs);

        private void Work(string query)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool firstToken = true;
            Debug.Log($"[Chat] submit: \"{query}\"");
            try
            {
                RagState st = _pipeline.Run(
                    query,
                    onToken: piece =>
                    {
                        if (firstToken)
                        {
                            firstToken = false;
                            Debug.Log($"[Chat] 첫 토큰(TTFT) @ {sw.ElapsedMilliseconds}ms");
                        }
                        _events.Enqueue(new ChatEvent { Type = ChatEventType.Token, Text = piece });
                    },
                    onStage: s =>
                    {
                        Debug.Log($"[Chat] stage={Map(s)} @ {sw.ElapsedMilliseconds}ms");
                        _events.Enqueue(new ChatEvent { Type = ChatEventType.Stage, Stage = Map(s) });
                    });

                var result = new ChatResult
                {
                    Query = query,
                    Answer = st.Answer,
                    Source = st.RoutedToGenerate ? AnswerSource.InScopeRag : AnswerSource.OutOfScopeFallback,
                    TopScore = st.TopScore,
                    Pages = DistinctContextPages(st),
                };
                Debug.Log($"[Chat] 완료 @ {sw.ElapsedMilliseconds}ms route={(st.RoutedToGenerate ? "generate" : "fallback")} " +
                          $"top_score={st.TopScore:F3} answerLen={result.Answer?.Length ?? 0}");

                // R-b 처리속도: 실제 RAG 프롬프트 기준 prefill/decode/TTFT + 256토큰 wall-clock 투영.
                if (st.RoutedToGenerate)
                {
                    var t = st.Timings;
                    double tps = t.DecodeMs > 0 ? t.NDecoded / (t.DecodeMs / 1000.0) : 0.0;
                    double wall = t.PrefillMs + t.DecodeMs;
                    double proj256 = t.PrefillMs + (tps > 0 ? 256.0 / tps * 1000.0 : 0.0);
                    Debug.Log($"[R-b] n_prompt={t.NPrompt} prefill={t.PrefillMs:F0}ms TTFT={t.TtftMs:F0}ms " +
                              $"decode={t.DecodeMs:F0}ms({tps:F1} tok/s) n_decoded={t.NDecoded} " +
                              $"wall={wall / 1000.0:F2}s proj256_wall={proj256 / 1000.0:F2}s");
                }

                _events.Enqueue(new ChatEvent { Type = ChatEventType.Done, Result = result });
            }
            catch (Exception e)
            {
                Debug.LogError($"[Chat] 예외 @ {sw.ElapsedMilliseconds}ms: {e}");
                _events.Enqueue(new ChatEvent { Type = ChatEventType.Error, Stage = ChatStage.Error, Text = e.Message });
            }
            finally
            {
                _busy = false;
            }
        }

        private static ChatStage Map(RagStage s)
        {
            switch (s)
            {
                case RagStage.Retrieving: return ChatStage.Retrieving;
                case RagStage.Generating: return ChatStage.Generating;
                case RagStage.Fallback:   return ChatStage.Fallback;
                default:                  return ChatStage.Idle;
            }
        }

        // Pages of the chunks that actually made it into the context (== first Contexts.Count hits).
        private static int[] DistinctContextPages(RagState st)
        {
            if (!st.RoutedToGenerate) return Array.Empty<int>();
            int n = st.Contexts.Count > 0 ? st.Contexts.Count : st.Hits.Count;
            var pages = new List<int>();
            foreach (Hit h in st.Hits.Take(n))
                if (!pages.Contains(h.Page)) pages.Add(h.Page);
            return pages.ToArray();
        }
    }
}
