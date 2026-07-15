using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace TexChatbot
{
    // LLM-API fallback (W / A-b 20~23): replaces RagPipeline's out-of-scope node with a
    // Gemini call for out-of-domain queries. Ported 1:1 from the prototype U-c
    // (gemini_fallback_answer): same system prompt, same model, 429 retry. Routing
    // (top_score < 0.5 -> fallback) is unchanged, so parity holds.
    //
    // The fallback hook runs on ChatService's worker thread (RagPipeline.Run is blocking),
    // so this uses HttpClient (blocking on the worker) rather than UnityWebRequest, which
    // would require the Unity main loop. Plug in via:
    //   new RagPipeline(embedder, retriever, llm, fallback: gemini.Answer)
    public sealed class GeminiFallback : IDisposable
    {
        // Matches the prototype FALLBACK_SYSTEM.
        private const string SystemPrompt =
            "당신은 일반 지식 도우미입니다. 사용자 질문에 한국어로 간결히 답하세요. " +
            "이 답변은 연말정산 문서 근거가 아니므로 마지막에 \"(문서 외 일반 지식 답변)\"을 붙이세요.";

        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxRetries;

        public GeminiFallback(string apiKey, string model = "gemini-flash-lite-latest", int maxRetries = 3)
        {
            _apiKey = apiKey;
            _model = model;
            _maxRetries = maxRetries;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        // RagPipeline fallback hook signature: Func<RagState, string>.
        public string Answer(RagState state)
        {
            if (string.IsNullOrEmpty(_apiKey))
                return "제공된 연말정산 문서 범위 밖 질의입니다. (API 키 미설정으로 Fallback 비활성)";

            string url = string.Format(Endpoint, _model, _apiKey);
            string prompt = SystemPrompt + "\n\n질문: " + state.Query;
            string body = new JObject
            {
                ["contents"] = new JArray
                {
                    new JObject { ["parts"] = new JArray { new JObject { ["text"] = prompt } } }
                }
            }.ToString();

            for (int attempt = 0; attempt < _maxRetries; attempt++)
            {
                try
                {
                    using var content = new StringContent(body, Encoding.UTF8, "application/json");
                    HttpResponseMessage resp = _http.PostAsync(url, content).GetAwaiter().GetResult();
                    string text = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if ((int)resp.StatusCode == 429)   // rate limited
                    {
                        Thread.Sleep(20000);
                        continue;
                    }
                    if (!resp.IsSuccessStatusCode)
                        return $"(Fallback 실패: HTTP {(int)resp.StatusCode})";

                    return ExtractText(text);
                }
                catch (Exception e)
                {
                    if (attempt == _maxRetries - 1)
                        return $"(Fallback 실패: {e.Message})";
                }
            }
            return "(Fallback 실패: API 한도 초과)";
        }

        // candidates[0].content.parts[*].text 연결.
        private static string ExtractText(string json)
        {
            JObject root = JObject.Parse(json);
            JToken parts = root["candidates"]?[0]?["content"]?["parts"];
            if (parts == null) return "(Fallback 실패: 응답 파싱 불가)";
            var sb = new StringBuilder();
            foreach (JToken p in parts)
                sb.Append(p["text"]?.ToString());
            return sb.ToString().Trim();
        }

        public void Dispose() => _http?.Dispose();
    }
}
