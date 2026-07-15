using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TexChatbot
{
    // Query embedder: KURE-v1 fp16 ONNX (kure_v1_fp16.onnx + kure_v1_fp16.onnx.data) via
    // ONNX Runtime. Reproduces the Python/index build path exactly:
    //   tokens -> last_hidden_state -> CLS (seq position 0) -> L2 normalize.
    // The model's "tanh" pooler output is intentionally ignored: the deployment index
    // was built from CLS, so the query must use CLS too (vector-space match).
    public sealed class OnnxEmbedder : IDisposable
    {
        public const int Dim = 1024;

        private readonly InferenceSession _session;
        private readonly KureTokenizer _tokenizer;

        public OnnxEmbedder(string onnxPath, KureTokenizer tokenizer)
        {
            if (!File.Exists(onnxPath))
                throw new FileNotFoundException($"ONNX model not found: {onnxPath}");
            // External weights (.onnx.data) must sit next to onnxPath; ORT resolves them by name.
            var opts = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
            _session = new InferenceSession(onnxPath, opts);
            _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
        }

        // Returns an L2-normalized CLS embedding (float[1024]).
        public float[] Embed(string text)
        {
            long[] ids = _tokenizer.Encode(text, addSpecialTokens: true);
            int n = ids.Length;
            var mask = new long[n];
            for (int i = 0; i < n; i++) mask[i] = 1L;

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(ids, new[] { 1, n })),
                NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(mask, new[] { 1, n })),
            };

            using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);
            Tensor<Float16> lhs = GetTensor(results, "last_hidden_state"); // [1, n, 1024], fp16

            var vec = new float[Dim];
            for (int d = 0; d < Dim; d++)            // CLS = sequence position 0
                vec[d] = (float)lhs[0, 0, d];
            L2Normalize(vec);
            return vec;
        }

        private static Tensor<Float16> GetTensor(
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results, string name)
        {
            foreach (DisposableNamedOnnxValue v in results)
                if (v.Name == name) return v.AsTensor<Float16>();
            throw new InvalidOperationException($"ONNX output '{name}' not found.");
        }

        private static void L2Normalize(float[] v)
        {
            double sum = 0.0;
            for (int i = 0; i < v.Length; i++) sum += (double)v[i] * v[i];
            float inv = (float)(1.0 / (Math.Sqrt(sum) + 1e-12));
            for (int i = 0; i < v.Length; i++) v[i] *= inv;
        }

        public void Dispose() => _session?.Dispose();
    }
}
