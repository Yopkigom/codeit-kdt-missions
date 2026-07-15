using TMPro;
using UnityEngine;

namespace TexChatbot
{
    // Prefab component for one chat message. Streaming text + optional source chips
    // (in-scope: page citations) / fallback badge (out-of-scope: general knowledge).
    public sealed class ChatBubble : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;
        [Header("출처 표시 (봇 버블에만)")]
        [SerializeField] private Transform _chipRoot;        // 페이지 칩이 붙는 컨테이너
        [SerializeField] private GameObject _pageChipPrefab; // 자식에 TMP_Text 포함
        [SerializeField] private GameObject _fallbackBadge;  // "문서 외 일반 지식" 배지

        public void SetText(string s) { if (_text != null) _text.text = s ?? string.Empty; }

        public void Append(string piece) { if (_text != null) _text.text += piece; }

        // Renders the source: page chips for RAG answers, a badge for fallback answers.
        public void ShowSource(AnswerSource source, int[] pages)
        {
            if (_fallbackBadge != null)
                _fallbackBadge.SetActive(source == AnswerSource.OutOfScopeFallback);

            if (source != AnswerSource.InScopeRag || _pageChipPrefab == null || _chipRoot == null || pages == null)
                return;

            foreach (int p in pages)
            {
                GameObject chip = Instantiate(_pageChipPrefab, _chipRoot);
                var label = chip.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = $"페이지 {p}";
            }
        }
    }
}
