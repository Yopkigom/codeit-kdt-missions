using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TexChatbot
{
    public sealed class Chunk
    {
        public string Id;
        public int Page;
        public string Type;
        public string Text;
    }

    // Loads the deployment index: embeddings (.npy float32 [N,1024], L2-normalized) +
    // chunks (json) + manifest. The embedding artifact must be byte-identical to the
    // device build (fp16 ONNX, CLS+L2, no prefix) to keep the vector space aligned.
    public sealed class IndexStore
    {
        public float[][] Embeddings { get; private set; } // [N][1024]
        public Chunk[] Chunks { get; private set; }
        public int Dim { get; private set; }
        public int Count => Chunks.Length;

        public static IndexStore Load(string npyPath, string chunksJsonPath, string manifestJsonPath = null)
        {
            var store = new IndexStore();
            store.Embeddings = LoadNpyFloat2D(npyPath, out int dim);
            store.Dim = dim;
            store.Chunks = LoadChunks(chunksJsonPath);

            if (store.Embeddings.Length != store.Chunks.Length)
                throw new InvalidDataException(
                    $"index/chunks size mismatch: {store.Embeddings.Length} vs {store.Chunks.Length}");

            if (!string.IsNullOrEmpty(manifestJsonPath) && File.Exists(manifestJsonPath))
                VerifyManifest(manifestJsonPath, store);
            return store;
        }

        // Compares the index manifest against the values the C# pipeline assumes.
        // Mismatch is surfaced (throws) because a divergent index breaks parity silently.
        private static void VerifyManifest(string manifestPath, IndexStore store)
        {
            JObject m = JObject.Parse(File.ReadAllText(manifestPath));
            void Expect(string key, object expected)
            {
                JToken tok = m[key];
                if (tok == null) return; // tolerate absent keys (older manifests)
                if (!string.Equals(tok.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(
                        $"index manifest mismatch: {key}={tok} (expected {expected})");
            }
            Expect("precision", "fp16");
            Expect("embedding_dim", store.Dim);
            Expect("pooling", "CLS");
            Expect("l2_normalized", true);
            Expect("use_prefix", false);
            int nChunks = m["n_chunks"]?.Value<int>() ?? store.Count;
            if (nChunks != store.Count)
                throw new InvalidDataException($"manifest n_chunks={nChunks} != loaded {store.Count}");
        }

        private static Chunk[] LoadChunks(string path)
        {
            JArray arr = JArray.Parse(File.ReadAllText(path));
            var chunks = new Chunk[arr.Count];
            for (int i = 0; i < arr.Count; i++)
            {
                JToken c = arr[i];
                int.TryParse(c["page"]?.ToString(), out int page);
                chunks[i] = new Chunk
                {
                    Id = c["id"]?.ToString() ?? i.ToString(),
                    Page = page,
                    Type = c["type"]?.ToString() ?? "text",
                    Text = c["text"]?.ToString() ?? string.Empty,
                };
            }
            return chunks;
        }

        // Minimal numpy v1/v2 reader for C-order float32 little-endian 2D arrays.
        private static float[][] LoadNpyFloat2D(string path, out int dim)
        {
            using FileStream fs = File.OpenRead(path);
            using var br = new BinaryReader(fs);

            byte[] magic = br.ReadBytes(6); // \x93NUMPY
            if (magic.Length < 6 || magic[0] != 0x93 || magic[1] != (byte)'N')
                throw new InvalidDataException("not a .npy file");

            byte major = br.ReadByte();
            br.ReadByte(); // minor
            int headerLen = major >= 2 ? br.ReadInt32() : br.ReadUInt16();
            string header = Encoding.ASCII.GetString(br.ReadBytes(headerLen));

            if (!header.Contains("<f4"))
                throw new InvalidDataException($"expected float32 little-endian npy, header: {header}");
            if (header.Contains("'fortran_order': True"))
                throw new InvalidDataException("fortran-ordered npy not supported");

            ParseShape(header, out int n, out int d);
            dim = d;

            int rowBytes = d * sizeof(float);
            var rows = new float[n][];
            for (int i = 0; i < n; i++)
            {
                byte[] raw = br.ReadBytes(rowBytes);
                if (raw.Length != rowBytes)
                    throw new EndOfStreamException($"npy truncated at row {i}");
                var row = new float[d];
                Buffer.BlockCopy(raw, 0, row, 0, rowBytes); // ARM/x86 are little-endian
                rows[i] = row;
            }
            return rows;
        }

        private static void ParseShape(string header, out int n, out int d)
        {
            int lp = header.IndexOf('(');
            int rp = header.IndexOf(')', lp + 1);
            string inner = header.Substring(lp + 1, rp - lp - 1); // "1345, 1024,"
            string[] parts = inner.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new InvalidDataException($"expected a 2D npy shape, got '{inner}'");
            n = int.Parse(parts[0].Trim());
            d = int.Parse(parts[1].Trim());
        }
    }
}
