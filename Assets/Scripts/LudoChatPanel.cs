using System;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoChatPanel : MonoBehaviour
    {
        private sealed class MessageView
        {
            public RectTransform Root;
            public Image Fill;
            public Outline Outline;
            public Text SenderText;
            public Text BodyText;
            public LayoutElement BodyLayout;
        }

        private const float Margin = 24f;

        private RectTransform _parentRect;
        private RectTransform _rootRect;
        private RectTransform _windowRect;
        private RectTransform _toggleRect;
        private RectTransform _contentRect;
        private ScrollRect _scrollRect;
        private InputField _inputField;
        private Text _titleText;
        private Text _toggleText;
        private Button _sendButton;
        private Button _toggleButton;
        private Button _minimizeButton;
        private Action<string> _sendRequested;
        private Vector2 _lastParentSize;
        private bool _visible;
        private bool _minimized = true;
        private int _activeMessageCount;
        private readonly System.Collections.Generic.List<MessageView> _messagePool = new System.Collections.Generic.List<MessageView>(16);

        public void Build(RectTransform parent, Action<string> sendRequested)
        {
            _parentRect = parent;
            _sendRequested = sendRequested;
            BuildIfNeeded();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (_rootRect != null)
            {
                _rootRect.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                _minimized = true;
            }

            RefreshVisibility();
        }

        public void SetMinimized(bool minimized)
        {
            _minimized = minimized;
            RefreshVisibility();
        }

        public void ClearMessages()
        {
            if (_contentRect == null)
            {
                return;
            }

            _activeMessageCount = 0;
            for (int i = 0; i < _messagePool.Count; i++)
            {
                if (_messagePool[i] != null && _messagePool[i].Root != null)
                {
                    _messagePool[i].Root.gameObject.SetActive(false);
                }
            }
        }

        public void AppendMessage(string sender, string message, Color accentColor, bool emphasize)
        {
            if (_contentRect == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            MessageView bubble = GetOrCreateMessageView();
            bubble.Root.gameObject.SetActive(true);
            bubble.Root.SetSiblingIndex(_activeMessageCount);
            bubble.Fill.color = emphasize ? new Color(0.20f, 0.42f, 0.92f, 0.94f) : new Color(1f, 1f, 1f, 0.93f);
            bubble.Outline.effectColor = LudoUtility.WithAlpha(accentColor, emphasize ? 0.42f : 0.18f);
            bubble.SenderText.text = string.IsNullOrWhiteSpace(sender) ? "Player" : sender;
            bubble.SenderText.color = emphasize ? Color.white : accentColor;
            bubble.BodyText.text = message.Trim();
            bubble.BodyText.color = emphasize ? new Color(0.98f, 0.99f, 1f, 1f) : new Color(0.21f, 0.24f, 0.30f, 1f);
            bubble.BodyLayout.preferredHeight = Mathf.Max(26f, bubble.BodyText.preferredHeight);
            _activeMessageCount += 1;

            Canvas.ForceUpdateCanvases();
            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }

        private MessageView GetOrCreateMessageView()
        {
            if (_activeMessageCount < _messagePool.Count && _messagePool[_activeMessageCount] != null)
            {
                return _messagePool[_activeMessageCount];
            }

            RectTransform bubble = LudoUtility.CreateUIObject("Message", _contentRect);
            Image bubbleFill = LudoUtility.GetOrAddComponent<Image>(bubble.gameObject);
            LudoUtility.ApplySprite(bubbleFill, LudoSpriteFactory.RoundedMask);
            bubbleFill.raycastTarget = false;

            Outline outline = LudoUtility.GetOrAddComponent<Outline>(bubble.gameObject);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            VerticalLayoutGroup bubbleLayout = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(bubble.gameObject);
            bubbleLayout.padding = new RectOffset(16, 16, 12, 12);
            bubbleLayout.spacing = 6f;
            bubbleLayout.childAlignment = TextAnchor.UpperLeft;
            bubbleLayout.childControlWidth = true;
            bubbleLayout.childControlHeight = false;
            bubbleLayout.childForceExpandHeight = false;
            bubbleLayout.childForceExpandWidth = true;

            ContentSizeFitter bubbleFitter = LudoUtility.GetOrAddComponent<ContentSizeFitter>(bubble.gameObject);
            bubbleFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bubbleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement layoutElement = LudoUtility.GetOrAddComponent<LayoutElement>(bubble.gameObject);
            layoutElement.minHeight = 58f;
            layoutElement.preferredWidth = 0f;
            layoutElement.flexibleWidth = 1f;

            Text senderText = LudoUtility.CreateText("Sender", bubble, "Player", 18, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            senderText.raycastTarget = false;
            senderText.horizontalOverflow = HorizontalWrapMode.Wrap;
            senderText.verticalOverflow = VerticalWrapMode.Overflow;

            Text messageText = LudoUtility.CreateText("Body", bubble, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft, Color.white);
            messageText.raycastTarget = false;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Overflow;

            LayoutElement messageLayout = LudoUtility.GetOrAddComponent<LayoutElement>(messageText.gameObject);
            messageLayout.preferredHeight = 26f;

            MessageView view = new MessageView
            {
                Root = bubble,
                Fill = bubbleFill,
                Outline = outline,
                SenderText = senderText,
                BodyText = messageText,
                BodyLayout = messageLayout,
            };
            _messagePool.Add(view);
            return view;
        }

        private void OnRectTransformDimensionsChange()
        {
            RefreshLayoutIfNeeded();
        }

        private void BuildIfNeeded()
        {
            if (_rootRect != null)
            {
                return;
            }

            _rootRect = GetComponent<RectTransform>();
            if (_rootRect == null)
            {
                _rootRect = gameObject.AddComponent<RectTransform>();
            }

            _rootRect.SetParent(_parentRect, false);
            LudoUtility.Stretch(_rootRect);

            _windowRect = LudoUtility.CreateUIObject("Window", _rootRect);
            _windowRect.anchorMin = _windowRect.anchorMax = _windowRect.pivot = new Vector2(1f, 0f);

            Image windowFill = LudoUtility.GetOrAddComponent<Image>(_windowRect.gameObject);
            LudoUtility.ApplySprite(windowFill, LudoSpriteFactory.RoundedMask);
            windowFill.color = new Color(0.98f, 0.98f, 1f, 0.98f);
            windowFill.raycastTarget = true;

            Image windowInnerShadow = LudoUtility.CreateImage("InnerShadow", _windowRect, LudoSpriteFactory.RoundedInnerShadow, new Color(0.08f, 0.12f, 0.26f, 0.16f));
            LudoUtility.Stretch(windowInnerShadow.rectTransform);
            windowInnerShadow.raycastTarget = false;

            VerticalLayoutGroup windowLayout = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(_windowRect.gameObject);
            windowLayout.padding = new RectOffset(18, 18, 18, 18);
            windowLayout.spacing = 12f;
            windowLayout.childAlignment = TextAnchor.UpperLeft;
            windowLayout.childControlWidth = true;
            windowLayout.childControlHeight = false;
            windowLayout.childForceExpandWidth = true;
            windowLayout.childForceExpandHeight = false;

            RectTransform header = LudoUtility.CreateUIObject("Header", _windowRect);
            LayoutElement headerLayout = LudoUtility.GetOrAddComponent<LayoutElement>(header.gameObject);
            headerLayout.preferredHeight = 44f;
            HorizontalLayoutGroup headerGroup = LudoUtility.GetOrAddComponent<HorizontalLayoutGroup>(header.gameObject);
            headerGroup.spacing = 8f;
            headerGroup.childAlignment = TextAnchor.MiddleLeft;
            headerGroup.childControlWidth = false;
            headerGroup.childControlHeight = true;
            headerGroup.childForceExpandWidth = false;
            headerGroup.childForceExpandHeight = false;

            _titleText = LudoUtility.CreateText("Title", header, "Match Chat", 22, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.12f, 0.18f, 0.34f, 1f));
            LayoutElement titleLayout = LudoUtility.GetOrAddComponent<LayoutElement>(_titleText.gameObject);
            titleLayout.flexibleWidth = 1f;
            titleLayout.preferredHeight = 32f;

            _minimizeButton = CreateTinyHeaderButton(header, "−", () => SetMinimized(true));

            RectTransform scrollRoot = LudoUtility.CreateUIObject("ScrollRoot", _windowRect);
            LayoutElement scrollLayout = LudoUtility.GetOrAddComponent<LayoutElement>(scrollRoot.gameObject);
            scrollLayout.flexibleHeight = 1f;
            scrollLayout.preferredHeight = 220f;

            Image scrollFill = LudoUtility.GetOrAddComponent<Image>(scrollRoot.gameObject);
            LudoUtility.ApplySprite(scrollFill, LudoSpriteFactory.RoundedMask);
            scrollFill.color = new Color(0.90f, 0.93f, 1f, 0.48f);
            scrollFill.raycastTarget = true;

            RectTransform viewport = LudoUtility.CreateUIObject("Viewport", scrollRoot);
            LudoUtility.Stretch(viewport, 8f, 8f, 8f, 8f);
            Image viewportFill = LudoUtility.GetOrAddComponent<Image>(viewport.gameObject);
            viewportFill.color = new Color(1f, 1f, 1f, 0.02f);
            viewportFill.raycastTarget = true;
            Mask mask = LudoUtility.GetOrAddComponent<Mask>(viewport.gameObject);
            mask.showMaskGraphic = false;

            _contentRect = LudoUtility.CreateUIObject("Content", viewport);
            _contentRect.anchorMin = new Vector2(0f, 1f);
            _contentRect.anchorMax = new Vector2(1f, 1f);
            _contentRect.pivot = new Vector2(0.5f, 1f);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup contentLayout = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(_contentRect.gameObject);
            contentLayout.padding = new RectOffset(2, 2, 2, 2);
            contentLayout.spacing = 8f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            ContentSizeFitter contentFitter = LudoUtility.GetOrAddComponent<ContentSizeFitter>(_contentRect.gameObject);
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect = LudoUtility.GetOrAddComponent<ScrollRect>(scrollRoot.gameObject);
            _scrollRect.viewport = viewport;
            _scrollRect.content = _contentRect;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 24f;

            RectTransform composer = LudoUtility.CreateUIObject("Composer", _windowRect);
            LayoutElement composerLayout = LudoUtility.GetOrAddComponent<LayoutElement>(composer.gameObject);
            composerLayout.preferredHeight = 60f;
            HorizontalLayoutGroup composerGroup = LudoUtility.GetOrAddComponent<HorizontalLayoutGroup>(composer.gameObject);
            composerGroup.spacing = 10f;
            composerGroup.childAlignment = TextAnchor.MiddleCenter;
            composerGroup.childControlWidth = false;
            composerGroup.childControlHeight = true;
            composerGroup.childForceExpandHeight = false;
            composerGroup.childForceExpandWidth = false;

            _inputField = CreateInputField(composer, "Say something...");
            LayoutElement inputLayout = LudoUtility.GetOrAddComponent<LayoutElement>(_inputField.gameObject);
            inputLayout.flexibleWidth = 1f;
            inputLayout.preferredHeight = 52f;

            _sendButton = CreatePillButton(composer, "Send", OnSendPressed);
            LayoutElement sendLayout = LudoUtility.GetOrAddComponent<LayoutElement>(_sendButton.gameObject);
            sendLayout.preferredWidth = 96f;
            sendLayout.preferredHeight = 52f;

            _toggleRect = LudoUtility.CreateUIObject("Toggle", _rootRect);
            _toggleRect.anchorMin = _toggleRect.anchorMax = _toggleRect.pivot = new Vector2(1f, 0f);
            Image toggleFill = LudoUtility.GetOrAddComponent<Image>(_toggleRect.gameObject);
            LudoUtility.ApplySprite(toggleFill, LudoSpriteFactory.RoundedMask);
            toggleFill.color = new Color(0.21f, 0.30f, 0.54f, 0.98f);
            toggleFill.raycastTarget = true;
            Image toggleAccent = LudoUtility.CreateImage("Accent", _toggleRect, LudoSpriteFactory.RoundedGloss, new Color(1f, 1f, 1f, 0.14f));
            LudoUtility.Stretch(toggleAccent.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
            toggleAccent.raycastTarget = false;
            Outline toggleOutline = LudoUtility.GetOrAddComponent<Outline>(_toggleRect.gameObject);
            toggleOutline.effectColor = new Color(1f, 0.84f, 0.36f, 0.36f);
            toggleOutline.effectDistance = new Vector2(1f, -1f);
            toggleOutline.useGraphicAlpha = true;

            _toggleButton = LudoUtility.GetOrAddComponent<Button>(_toggleRect.gameObject);
            _toggleButton.onClick.AddListener(() => SetMinimized(false));
            LudoButtonFeedback toggleFeedback = LudoUtility.GetOrAddComponent<LudoButtonFeedback>(_toggleButton.gameObject);
            toggleFeedback.Configure(toggleAccent, 1.01f, 0.982f, 0.18f, 0.22f);
            _toggleText = LudoUtility.CreateText("Label", _toggleRect, "Chat", 22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            LudoUtility.Stretch(_toggleText.rectTransform);
            _toggleText.raycastTarget = false;

            RefreshLayout();
            RefreshVisibility();
        }

        private void RefreshLayoutIfNeeded()
        {
            if (_parentRect == null || _rootRect == null)
            {
                return;
            }

            Vector2 parentSize = _parentRect.rect.size;
            if ((parentSize - _lastParentSize).sqrMagnitude > 0.5f)
            {
                RefreshLayout();
            }
        }

        private void RefreshVisibility()
        {
            bool showWindow = _visible && !_minimized;
            bool showToggle = _visible && _minimized;

            if (_windowRect != null)
            {
                _windowRect.gameObject.SetActive(showWindow);
            }

            if (_toggleRect != null)
            {
                _toggleRect.gameObject.SetActive(showToggle);
            }
        }

        private void RefreshLayout()
        {
            if (_parentRect == null)
            {
                return;
            }

            _lastParentSize = _parentRect.rect.size;
            float width = Mathf.Clamp(_lastParentSize.x * 0.34f, 320f, 420f);
            float height = Mathf.Clamp(_lastParentSize.y * 0.36f, 300f, 420f);
            float toggleWidth = 110f;
            float toggleHeight = 56f;

            if (_windowRect != null)
            {
                _windowRect.sizeDelta = new Vector2(width, height);
                _windowRect.anchoredPosition = new Vector2(-Margin, Margin);
            }

            if (_toggleRect != null)
            {
                _toggleRect.sizeDelta = new Vector2(toggleWidth, toggleHeight);
                _toggleRect.anchoredPosition = new Vector2(-Margin, Margin);
            }
        }

        private void OnSendPressed()
        {
            if (_inputField == null)
            {
                return;
            }

            string text = _inputField.text;
            _inputField.text = string.Empty;
            if (_sendRequested != null && !string.IsNullOrWhiteSpace(text))
            {
                _sendRequested(text.Trim());
            }
        }

        private static Button CreateTinyHeaderButton(Transform parent, string label, Action onClick)
        {
            Button button = CreatePillButton(parent, label, onClick);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(button.gameObject);
            layout.preferredWidth = 36f;
            layout.preferredHeight = 36f;
            Text text = button.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 22;
            }

            return button;
        }

        private static Button CreatePillButton(Transform parent, string label, Action onClick)
        {
            RectTransform root = LudoUtility.CreateUIObject(label + "Button", parent);
            Image fill = LudoUtility.GetOrAddComponent<Image>(root.gameObject);
            LudoUtility.ApplySprite(fill, LudoSpriteFactory.RoundedMask);
            fill.color = new Color(0.21f, 0.30f, 0.54f, 1f);
            fill.raycastTarget = true;

            Image accent = LudoUtility.CreateImage("Accent", root, LudoSpriteFactory.RoundedGloss, new Color(1f, 1f, 1f, 0.16f));
            LudoUtility.Stretch(accent.rectTransform);
            accent.raycastTarget = false;

            Button button = LudoUtility.GetOrAddComponent<Button>(root.gameObject);
            button.onClick.AddListener(() =>
            {
                if (onClick != null)
                {
                    onClick();
                }
            });

            Text text = LudoUtility.CreateText("Label", root, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            LudoUtility.Stretch(text.rectTransform);
            text.raycastTarget = false;

            LudoButtonFeedback feedback = LudoUtility.GetOrAddComponent<LudoButtonFeedback>(button.gameObject);
            feedback.Configure(accent, 1.01f, 0.982f, 0.18f, 0.24f);

            return button;
        }

        private static InputField CreateInputField(Transform parent, string placeholderText)
        {
            RectTransform root = LudoUtility.CreateUIObject("InputField", parent);
            Image fill = LudoUtility.GetOrAddComponent<Image>(root.gameObject);
            LudoUtility.ApplySprite(fill, LudoSpriteFactory.RoundedMask);
            fill.color = new Color(1f, 1f, 1f, 0.97f);
            fill.raycastTarget = true;

            Outline outline = LudoUtility.GetOrAddComponent<Outline>(root.gameObject);
            outline.effectColor = new Color(0.18f, 0.32f, 0.62f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;

            Text text = LudoUtility.CreateText("Text", root, string.Empty, 19, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.18f, 0.22f, 0.30f, 1f));
            LudoUtility.Stretch(text.rectTransform, 18f, 18f, 14f, 14f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Text placeholder = LudoUtility.CreateText("Placeholder", root, placeholderText, 19, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.34f, 0.40f, 0.52f, 0.65f));
            LudoUtility.Stretch(placeholder.rectTransform, 18f, 18f, 14f, 14f);
            placeholder.raycastTarget = false;

            InputField inputField = LudoUtility.GetOrAddComponent<InputField>(root.gameObject);
            inputField.targetGraphic = fill;
            inputField.textComponent = text;
            inputField.placeholder = placeholder;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 64;
            return inputField;
        }
    }
}
