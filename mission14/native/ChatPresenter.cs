namespace TexChatbot
{
    // MVP Presenter: translates ChatService events into ChatView updates. Pure C# (only
    // calls the view), pumped each frame via Tick() on the main thread by ChatApp.
    public sealed class ChatPresenter
    {
        private readonly ChatView _view;
        private readonly ChatService _service;
        private ChatBubble _current;   // bot bubble currently being streamed

        public ChatPresenter(ChatView view, ChatService service)
        {
            _view = view;
            _service = service;
            _view.OnSubmit += HandleSubmit;
        }

        public void Dispose()
        {
            if (_view != null) _view.OnSubmit -= HandleSubmit;
        }

        private void HandleSubmit(string query)
        {
            if (_service.IsBusy) return;
            _view.AddUserMessage(query);
            _view.ClearInput();
            _view.SetInteractable(false);
            _current = null;              // bot bubble is created lazily on the first token
            _view.SetStatus("검색 중…");   // status carries progress until the answer arrives
            _view.SetBusy(true);          // show the "생성 중" indicator (Status GO)
            _service.Submit(query);
        }

        // Main-thread pump: drains queued events into the UI.
        public void Tick()
        {
            while (_service.TryDequeue(out ChatEvent ev))
            {
                switch (ev.Type)
                {
                    case ChatEventType.Stage:
                        _view.SetStatus(StatusOf(ev.Stage));
                        break;

                    case ChatEventType.Token:
                        if (_current == null) _current = _view.AddBotBubble(); // 첫 토큰에 버블 생성
                        _current?.Append(ev.Text);   // TTFT: partial tokens shown immediately
                        _view.ScrollToBottom();
                        break;

                    case ChatEventType.Done:
                        // Fallback path streams no tokens -> create the bubble here.
                        if (_current == null) _current = _view.AddBotBubble();
                        _current?.SetText(ev.Result.Answer);
                        _current?.ShowSource(ev.Result.Source, ev.Result.Pages);
                        _view.SetStatus(string.Empty);
                        _view.SetBusy(false);
                        _view.SetInteractable(true);
                        _view.ScrollToBottom();
                        _current = null;
                        break;

                    case ChatEventType.Error:
                        if (_current == null) _current = _view.AddBotBubble();
                        _current?.SetText($"오류가 발생했습니다: {ev.Text}");
                        _view.SetStatus(string.Empty);
                        _view.SetBusy(false);
                        _view.SetInteractable(true);
                        _current = null;
                        break;
                }
            }
        }

        private static string StatusOf(ChatStage s)
        {
            switch (s)
            {
                case ChatStage.Retrieving: return "검색 중…";
                case ChatStage.Generating: return "생성 중…";
                case ChatStage.Fallback:   return "문서 외 — 일반 지식으로 답변 중…";
                default:                   return string.Empty;
            }
        }
    }
}
