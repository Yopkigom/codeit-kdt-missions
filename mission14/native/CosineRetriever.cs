using System;
using System.Collections.Generic;

namespace TexChatbot
{
    public sealed class Hit
    {
        public int Rank;
        public string ChunkId;
        public float Score;
        public int Page;
        public string Type;
        public string Text;
    }

    // Brute-force top-k retrieval by inner product (== cosine for L2-normalized vectors).
    // Mirrors the prototype J-b path (the device's reference search), not the VectorDB.
    public sealed class CosineRetriever
    {
        private readonly IndexStore _index;

        public CosineRetriever(IndexStore index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
        }

        public List<Hit> Search(float[] query, int topK = 5)
        {
            if (query.Length != _index.Dim)
                throw new ArgumentException($"query dim {query.Length} != index dim {_index.Dim}");

            int n = _index.Count;
            float[][] emb = _index.Embeddings;
            var scores = new float[n];
            for (int i = 0; i < n; i++)
            {
                float dot = 0f;
                float[] row = emb[i];
                for (int d = 0; d < query.Length; d++) dot += row[d] * query[d];
                scores[i] = dot;
            }

            List<int> top = ArgTopK(scores, Math.Min(topK, n));
            var hits = new List<Hit>(top.Count);
            for (int r = 0; r < top.Count; r++)
            {
                int j = top[r];
                Chunk c = _index.Chunks[j];
                hits.Add(new Hit
                {
                    Rank = r,
                    ChunkId = c.Id,
                    Score = scores[j],
                    Page = c.Page,
                    Type = c.Type,
                    Text = c.Text,
                });
            }
            return hits;
        }

        // Partial selection of the k highest scores (k is small, n ~1.3k -> O(n*k) is fine).
        private static List<int> ArgTopK(float[] scores, int k)
        {
            var idx = new List<int>(k);
            var used = new bool[scores.Length];
            for (int r = 0; r < k; r++)
            {
                int best = -1;
                float bestScore = float.NegativeInfinity;
                for (int i = 0; i < scores.Length; i++)
                {
                    if (used[i]) continue;
                    if (scores[i] > bestScore) { bestScore = scores[i]; best = i; }
                }
                if (best < 0) break;
                used[best] = true;
                idx.Add(best);
            }
            return idx;
        }
    }
}
