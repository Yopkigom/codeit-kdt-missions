using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TexChatbot
{
    // MVP View: scene-wired, no logic. Raises OnSubmit and exposes display methods the
    // presenter calls. Bubbles are instantiated from prefabs into the scroll content.
    public sealed class ChatView : MonoBehaviour
    {
        [Header("입력")]
        [SerializeField] private TMP_InputField _input;
        [SerializeField] private Button _sendButton;

        [Header("메시지 목록")]
        [SerializeField] private ScrollRect _scroll;
        [SerializeField] private RectTransform _content;     // VerticalLayoutGroup + ContentSizeFitter
        [SerializeField] private ChatBubble _userBubblePrefab;
        [SerializeField] private ChatBubble _botBubblePrefab;

        [Header("상태 / 진행률")]
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private GameObject _busyIndicator;  // 검색/생성 중 표시 (스피너 등, Status GO)
        [SerializeField] private GameObject _progressRoot;   // 최초 모델 복사 진행 표시
        [SerializeField] private Slider _progressBar;

        public event Action<string> OnSubmit;

        private void Awake()
        {
            if (_sendButton != null) _sendButton.onClick.AddListener(Submit);
            // 엔터 전송 (모바일 IME에서도 동작)
            if (_input != null) _input.onSubmit.AddListener(_ => Submit());
        }

        private void OnDestroy()
        {
            if (_sendButton != null) _sendButton.onClick.RemoveListener(Submit);
        }

        private void Submit()
        {
            string q = _input != null ? _input.text?.Trim() : null;
            if (!string.IsNullOrEmpty(q)) OnSubmit?.Invoke(q);
        }

        public void ClearInput() { if (_input != null) _input.text = string.Empty; }

        public void SetInteractable(bool on)
        {
            if (_input != null) _input.interactable = on;
            if (_sendButton != null) _sendButton.interactable = on;
        }

        public void SetStatus(string s) { if (_statusText != null) _statusText.text = s ?? string.Empty; }

        // Toggles the busy indicator (Status GO) shown while a turn is in progress.
        public void SetBusy(bool on) { if (_busyIndicator != null) _busyIndicator.SetActive(on); }

        public ChatBubble AddUserMessage(string text)
        {
            ChatBubble b = Spawn(_userBubblePrefab, "UserBubble");
            if (b == null) return null;
            b.SetText(text);
            ScrollToBottom();
            return b;
        }

        public ChatBubble AddBotBubble()
        {
            ChatBubble b = Spawn(_botBubblePrefab, "BotBubble");
            if (b != null) b.SetText(string.Empty);
            return b;
        }

        // Instantiates a bubble into the scroll content and guarantees it is active
        // (a prefab whose root is disabled would otherwise spawn invisible).
        private ChatBubble Spawn(ChatBubble prefab, string label)
        {
            if (prefab == null)
            {
                Debug.LogError($"ChatView: {label} 프리팹이 할당되지 않음");
                return null;
            }
            ChatBubble b = Instantiate(prefab, _content);
            if (!b.gameObject.activeSelf) b.gameObject.SetActive(true);
            return b;
        }

        public void ScrollToBottom()
        {
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }

        public void ShowProgress(bool on, float value01 = 0f)
        {
            if (_progressRoot != null) _progressRoot.SetActive(on);
            if (_progressBar != null) _progressBar.value = value01;
        }
    }
}
