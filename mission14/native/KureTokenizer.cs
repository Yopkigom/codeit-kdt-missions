using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.ML.Tokenizers;

namespace TexChatbot
{
    // KURE-v1 (= bge-m3 = XLM-RoBERTa) SentencePiece Unigram tokenizer, ported via
    // Microsoft.ML.Tokenizers. The shipped SentencePiece model (kure_v1_spm.model) is
    // the bge-m3/XLM-R model; KURE-v1 reuses it unchanged (verified: token ids match
    // transformers exactly on KO/EN samples, N-h fixtures).
    //
    // XLM-R remaps the raw SentencePiece ids onto the HF vocab and wraps the sequence:
    //   hf_id = (sp_id == 0) ? UnkId : sp_id + FairseqOffset      // SP <unk>(0) -> HF <unk>(3)
    //   sequence = [ClsId] + ids + [SepId]                        // <s> ... </s>
    // SentencePiece's own bos/eos are disabled so the wrapping is controlled here.
    // No prefix: KURE/bge-m3 use the same input for query and passage.
    public sealed class KureTokenizer
    {
        public const int ClsId = 0;   // <s>
        public const int PadId = 1;   // <pad>
        public const int SepId = 2;   // </s>
        public const int UnkId = 3;   // <unk>
        private const int FairseqOffset = 1;

        private readonly SentencePieceTokenizer _sp;

        public KureTokenizer(string spModelPath)
        {
            if (!File.Exists(spModelPath))
                throw new FileNotFoundException($"SentencePiece model not found: {spModelPath}");
            using Stream stream = File.OpenRead(spModelPath);
            // XLM-R wrapping is applied manually -> disable SP's own bos/eos.
            _sp = SentencePieceTokenizer.Create(stream, addBeginOfSentence: false, addEndOfSentence: false);
        }

        // Encodes text to XLM-R input ids (int64, matching the ONNX input dtype).
        public long[] Encode(string text, bool addSpecialTokens = true)
        {
            IReadOnlyList<int> sp = _sp.EncodeToIds(text ?? string.Empty);
            var ids = new List<long>(sp.Count + 2);
            if (addSpecialTokens) ids.Add(ClsId);
            foreach (int s in sp)
                ids.Add(s == 0 ? UnkId : s + FairseqOffset);
            if (addSpecialTokens) ids.Add(SepId);
            return ids.ToArray();
        }
    }
}
