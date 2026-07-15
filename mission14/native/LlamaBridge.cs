using System;
using System.Runtime.InteropServices;
using System.Text;
#if UNITY_5_3_OR_NEWER
using AOT;
#endif

namespace LlamaBridge
{
    // Mirrors lb_timings in llama_bridge.h.
    [StructLayout(LayoutKind.Sequential)]
    public struct LbTimings
    {
        public double PrefillMs;
        public double DecodeMs;
        public double TtftMs;
        public int NPrompt;
        public int NDecoded;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate bool LbTokenCallback(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string piece, IntPtr userData);

    // Raw P/Invoke surface. Windows: llama_bridge.dll, Android: libllama_bridge.so.
    public static class Native
    {
        private const string Dll = "llama_bridge";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lb_backend_init();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lb_backend_free();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr lb_load(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string modelPath,
            int nCtx, int nThreads, int nThreadsBatch, int nGpuLayers);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void lb_free(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lb_tokenize(
            IntPtr ctx,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string text,
            [MarshalAs(UnmanagedType.I1)] bool addSpecial,
            int[] outTokens, int maxTokens);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lb_format_chat(
            IntPtr ctx,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string system,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string user,
            byte[] outBuf, int outLen);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int lb_generate(
            IntPtr ctx,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string prompt,
            int maxTokens, float temp, float topP, float repeatPenalty, uint seed,
            [MarshalAs(UnmanagedType.I1)] bool ignoreEos,
            LbTokenCallback cb, IntPtr userData, ref LbTimings timings);
    }

    // Thin managed wrapper. NOTE for IL2CPP/Android: the token callback must be a
    // static method tagged with [MonoPInvokeCallback]; instance delegates crash under AOT.
    public sealed class LlamaModel : IDisposable
    {
        private IntPtr _ctx;

        // nThreads=2: decode is memory-bandwidth bound; 2 prime cores beat 6 on Snapdragon 8 Elite.
        // nThreadsBatch=6: prefill is compute-bound; more cores cut long RAG-prompt prefill (R-d/B).
        // nGpuLayers>0: offload layers to GPU on a Vulkan-enabled build (R-d/C); ignored on CPU build.
        public LlamaModel(string modelPath, int nCtx = 8192, int nThreads = 2,
                          int nThreadsBatch = 6, int nGpuLayers = 0)
        {
            Native.lb_backend_init();
            _ctx = Native.lb_load(modelPath, nCtx, nThreads, nThreadsBatch, nGpuLayers);
            if (_ctx == IntPtr.Zero)
                throw new InvalidOperationException($"llama_bridge: failed to load model '{modelPath}'.");
        }

        // Counts Gemma tokens for the prompt/context budget (RAG context assembly, N-g).
        // Mirrors the prototype's llm.tokenize length so the 6000-token cap matches.
        public int CountTokens(string text, bool addSpecial = false)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            // lb_tokenize rejects a null buffer (-1); size from byte length (tokens <= bytes),
            // and grow once if it still reports -needed.
            var buf = new int[Encoding.UTF8.GetByteCount(text) + 8];
            int n = Native.lb_tokenize(_ctx, text, addSpecial, buf, buf.Length);
            if (n == -1) return 0;             // bad args
            if (n < 0)                          // -needed: buffer too small
            {
                buf = new int[-n];
                n = Native.lb_tokenize(_ctx, text, addSpecial, buf, buf.Length);
            }
            return n < 0 ? 0 : n;
        }

        public string FormatChat(string system, string user)
        {
            // lb_format_chat returns the formatted length; if it does not fit, it
            // returns the needed length without writing. Start large, grow once.
            // (Do not probe with a null buffer: the native side rejects null.)
            var buf = new byte[16384];
            int n = Native.lb_format_chat(_ctx, system ?? string.Empty, user, buf, buf.Length);
            if (n < 0) return user;
            if (n >= buf.Length)
            {
                buf = new byte[n + 1];
                n = Native.lb_format_chat(_ctx, system ?? string.Empty, user, buf, buf.Length);
                if (n < 0) return user;
            }
            return Encoding.UTF8.GetString(buf, 0, n);
        }

        // Format (system + user) with the Gemma 4 chat template, then stream the answer.
        // ignoreEos: force exactly maxTokens (decode-throughput benchmark).
        public LbTimings Chat(string system, string user, Action<string> onPiece,
            int maxTokens = 256, float temp = 0.2f, float topP = 0.9f,
            float repeatPenalty = 1.1f, uint seed = 42, bool ignoreEos = false)
        {
            string prompt = FormatChat(system, user);
            return Generate(prompt, onPiece, maxTokens, temp, topP, repeatPenalty, seed, ignoreEos);
        }

        // Streams tokens to onPiece (called on the worker thread). Returns timings.
        public LbTimings Generate(string prompt, Action<string> onPiece,
            int maxTokens = 256, float temp = 0.2f, float topP = 0.9f,
            float repeatPenalty = 1.1f, uint seed = 42, bool ignoreEos = false)
        {
            _sink = onPiece;
            _active = this;
            _cancelRequested = false;
            var timings = new LbTimings();
            try
            {
                int rc = Native.lb_generate(_ctx, prompt, maxTokens, temp, topP, repeatPenalty, seed,
                                            ignoreEos, TokenThunk, IntPtr.Zero, ref timings);
                if (rc != 0)
                    throw new InvalidOperationException($"llama_bridge: lb_generate failed (rc={rc}).");
                return timings;
            }
            finally
            {
                _sink = null;
                _active = null;
            }
        }

        // Cooperative cancel: the next streamed token stops generation (returns false to
        // native). Only interrupts the DECODE loop; an in-flight prefill cannot be aborted.
        // Used on teardown so a stuck generation does not crash on Play-mode exit.
        private volatile bool _cancelRequested;
        public void RequestCancel() => _cancelRequested = true;

        [ThreadStatic] private static Action<string> _sink;
        [ThreadStatic] private static LlamaModel _active;

#if UNITY_5_3_OR_NEWER
        [MonoPInvokeCallback(typeof(LbTokenCallback))]
#endif
        private static bool TokenThunk(string piece, IntPtr user)
        {
            _sink?.Invoke(piece);
            LlamaModel m = _active;
            return m == null || !m._cancelRequested; // false stops generation
        }

        public void Dispose()
        {
            if (_ctx != IntPtr.Zero)
            {
                Native.lb_free(_ctx);
                _ctx = IntPtr.Zero;
            }
            GC.SuppressFinalize(this);
        }

        ~LlamaModel() => Dispose();
    }
}
