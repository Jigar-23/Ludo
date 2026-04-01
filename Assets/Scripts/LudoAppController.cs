using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoAppController : MonoBehaviour
    {
        private enum AppScreen
        {
            Home = 0,
            Setup = 1,
            Lobby = 2,
            Gameplay = 3,
        }

        private sealed class ButtonView
        {
            public RectTransform Root;
            public Image Fill;
            public Image Accent;
            public Text Label;
            public Button Button;
        }

        private sealed class ColorChipView
        {
            public ButtonView ButtonView;
            public RectTransform Root;
            public Image Fill;
            public Image Icon;
            public Text Title;
            public Text Subtitle;
        }

        private sealed class LobbySeatView
        {
            public RectTransform Root;
            public Image Fill;
            public Text Title;
            public Text Subtitle;
        }

        private static readonly LudoTokenColor[] s_MenuColorOrder =
        {
            LudoTokenColor.Red,
            LudoTokenColor.Green,
            LudoTokenColor.Blue,
            LudoTokenColor.Yellow,
        };

        private static readonly LudoTokenColor[] s_DefaultSelectionOrder =
        {
            LudoTokenColor.Blue,
            LudoTokenColor.Red,
            LudoTokenColor.Green,
            LudoTokenColor.Yellow,
        };

        private static readonly Color AppPanelColor = new Color(1f, 0.985f, 0.965f, 0.96f);
        private static readonly Color AppPanelChromeColor = new Color(1f, 1f, 1f, 0.05f);
        private static readonly Color AppPanelInnerShadowColor = new Color(0.17f, 0.12f, 0.10f, 0.08f);
        private static readonly Color AppPanelShadowColor = new Color(0.12f, 0.10f, 0.08f, 0.11f);
        private static readonly Color AppSectionFillColor = new Color(1f, 1f, 1f, 0.84f);
        private static readonly Color AppSectionShadowColor = new Color(0.13f, 0.11f, 0.08f, 0.05f);
        private static readonly Color AppButtonFillColor = new Color(1f, 0.992f, 0.978f, 0.98f);
        private static readonly Color AppButtonShadowColor = new Color(0.13f, 0.11f, 0.08f, 0.08f);
        private static readonly Color AppButtonLabelColor = new Color(0.22f, 0.19f, 0.17f, 1f);
        private static readonly Color AppPrimaryButtonFillColor = new Color(0.22f, 0.30f, 0.54f, 0.98f);
        private static readonly Color AppPrimaryButtonSubtitleColor = new Color(0.95f, 0.96f, 0.98f, 0.92f);
        private static readonly Color AppCallToActionFillColor = new Color(0.96f, 0.78f, 0.48f, 0.98f);
        private static readonly Color AppCallToActionTextColor = new Color(0.22f, 0.16f, 0.12f, 1f);
        private static readonly Color AppSelectedFillColor = new Color(1f, 0.985f, 0.955f, 0.99f);
        private static readonly Color AppUnselectedFillColor = new Color(1f, 0.985f, 0.965f, 0.90f);
        private static readonly Color AppMutedTextColor = new Color(0.46f, 0.39f, 0.34f, 0.92f);
        private static readonly Color AppAccentTextColor = new Color(0.62f, 0.47f, 0.24f, 1f);

        private LudoAnimationController _animationController;
        private LudoBoardRenderer _boardRenderer;
        private LudoGameController _gameController;
        private LudoOnlineService _onlineService;
        private RectTransform _uiLayer;
        private RectTransform _appRoot;
        private RectTransform _homeScreen;
        private RectTransform _setupScreen;
        private RectTransform _lobbyScreen;
        private RectTransform _gameplayOverlay;
        private RectTransform _homePanel;
        private RectTransform _setupPanel;
        private RectTransform _lobbyPanel;
        private CanvasGroup _homeCanvasGroup;
        private CanvasGroup _setupCanvasGroup;
        private CanvasGroup _lobbyCanvasGroup;
        private CanvasGroup _gameplayCanvasGroup;
        private RectTransform _setupScrollContent;
        private ScrollRect _setupScrollRect;
        private GridLayoutGroup _colorGrid;
        private HorizontalLayoutGroup _playerCountGroup;
        private HorizontalLayoutGroup _localColorGroup;
        private HorizontalLayoutGroup _onlineEntryGroup;
        private RectTransform _playerCountSection;
        private RectTransform _colorSection;
        private RectTransform _localColorSection;
        private RectTransform _onlineEntrySection;
        private RectTransform _roomCodeSection;
        private RectTransform _nameSection;
        private RectTransform _lobbySeatList;
        private ButtonView _homeLocalButton;
        private ButtonView _homeComputerButton;
        private ButtonView _homeOnlineButton;
        private ButtonView _setupBackButton;
        private ButtonView _playButton;
        private ButtonView _onlineCreateButton;
        private ButtonView _onlineJoinButton;
        private ButtonView _lobbyBackButton;
        private ButtonView _lobbyStartButton;
        private readonly Dictionary<int, ButtonView> _playerCountButtons = new Dictionary<int, ButtonView>(3);
        private readonly Dictionary<LudoTokenColor, ColorChipView> _colorChips = new Dictionary<LudoTokenColor, ColorChipView>(4);
        private readonly Dictionary<LudoTokenColor, ButtonView> _localColorButtons = new Dictionary<LudoTokenColor, ButtonView>(4);
        private readonly Dictionary<LudoTokenColor, LobbySeatView> _lobbySeats = new Dictionary<LudoTokenColor, LobbySeatView>(4);
        private Text _setupTitleText;
        private Text _setupSubtitleText;
        private Text _setupHintText;
        private Text _lobbyTitleText;
        private Text _lobbyStatusText;
        private Text _roomBadgeText;
        private Text _connectionText;
        private InputField _playerNameField;
        private InputField _roomCodeField;
        private LudoChatPanel _chatPanel;
        private Vector2 _lastUiSize;
        private bool _bootstrapped;
        private AppScreen _currentScreen = AppScreen.Home;
        private AppScreen _visibleScreen = (AppScreen)(-1);
        private LudoGameMode _setupMode = LudoGameMode.Local;
        private LudoOnlineEntryMode _onlineEntryMode = LudoOnlineEntryMode.CreateAndJoin;
        private int _selectedPlayerCount = 2;
        private readonly List<LudoTokenColor> _selectedColors = new List<LudoTokenColor>(4);
        private LudoTokenColor _localColor = LudoTokenColor.Blue;
        private LudoRoomSnapshot _roomSnapshot;
        private LudoSessionConfig _activeSession;
        private Text _homeTitleText;
        private Text _homeSubtitleText;

        public void Initialize(LudoAnimationController animationController, LudoBoardRenderer boardRenderer, LudoGameController gameController, RectTransform uiLayer)
        {
            if (_bootstrapped)
            {
                return;
            }

            _animationController = animationController;
            _boardRenderer = boardRenderer;
            _gameController = gameController;
            _uiLayer = uiLayer;
                _onlineService = LudoUtility.GetOrAddComponent<LudoOnlineService>(gameObject);
                _onlineService.RoomSnapshotReceived += OnRoomSnapshotReceived;
                _onlineService.MatchStartedReceived += OnOnlineMatchStarted;
                _onlineService.ChatMessageReceived += OnChatMessageReceived;
                _onlineService.TurnActionReceived += OnTurnActionReceived;
                _onlineService.StatusChanged += OnOnlineStatusChanged;
                _onlineService.ErrorReceived += OnOnlineErrorReceived;

            if (_gameController != null)
            {
                _gameController.OnlineTurnActionCommitted += OnLocalOnlineTurnCommitted;
            }

            BuildIfNeeded();
            ApplyDefaultSelection(LudoGameMode.Local);
            ShowHome();
            _bootstrapped = true;
        }

        private void OnDestroy()
        {
            if (_onlineService != null)
            {
                _onlineService.RoomSnapshotReceived -= OnRoomSnapshotReceived;
                _onlineService.MatchStartedReceived -= OnOnlineMatchStarted;
                _onlineService.ChatMessageReceived -= OnChatMessageReceived;
                _onlineService.TurnActionReceived -= OnTurnActionReceived;
                _onlineService.StatusChanged -= OnOnlineStatusChanged;
                _onlineService.ErrorReceived -= OnOnlineErrorReceived;
            }

            if (_gameController != null)
            {
                _gameController.OnlineTurnActionCommitted -= OnLocalOnlineTurnCommitted;
            }
        }

        private void Update()
        {
            if (_uiLayer == null || _appRoot == null)
            {
                return;
            }

            Vector2 uiSize = _uiLayer.rect.size;
            if ((uiSize - _lastUiSize).sqrMagnitude > 0.5f)
            {
                _lastUiSize = uiSize;
                RefreshLayout();
            }
        }

        private void BuildIfNeeded()
        {
            if (_appRoot != null)
            {
                return;
            }

            _appRoot = LudoUtility.CreateUIObject("AppRoot", _uiLayer);
            LudoUtility.Stretch(_appRoot);
            _appRoot.SetAsLastSibling();

            BuildHomeScreen();
            BuildSetupScreen();
            BuildLobbyScreen();
            BuildGameplayOverlay();
            RefreshLayout();
        }

        private void BuildHomeScreen()
        {
            _homeScreen = LudoUtility.CreateUIObject("HomeScreen", _appRoot);
            LudoUtility.Stretch(_homeScreen);
            _homeCanvasGroup = PrepareScreen(_homeScreen);

            _homePanel = CreateFloatingPanel(_homeScreen, "HomePanel");
            _homeTitleText = LudoUtility.CreateText("Title", _homePanel, "LUDO APP", 42, FontStyle.Bold, TextAnchor.MiddleCenter, LudoTheme.TextPrimary);
            ConfigureSection(_homeTitleText.rectTransform, 80f);
            _homeTitleText.raycastTarget = false;

            _homeSubtitleText = LudoUtility.CreateText("Subtitle", _homePanel, "Choose how you want to play.", 22, FontStyle.Normal, TextAnchor.MiddleCenter, LudoTheme.TextMuted);
            ConfigureSection(_homeSubtitleText.rectTransform, 44f);
            _homeSubtitleText.raycastTarget = false;

            _homeLocalButton = CreateLargeActionButton(_homePanel, "Local", "2, 3 or 4 players on one device.", () => OpenSetup(LudoGameMode.Local));
            _homeComputerButton = CreateLargeActionButton(_homePanel, "Computer", "One player against AI-controlled rivals.", () => OpenSetup(LudoGameMode.Computer));
            _homeOnlineButton = CreateLargeActionButton(_homePanel, "Online", "Create a room or join with a code.", () => OpenSetup(LudoGameMode.Online));
        }

        private void BuildSetupScreen()
        {
            _setupScreen = LudoUtility.CreateUIObject("SetupScreen", _appRoot);
            LudoUtility.Stretch(_setupScreen);
            _setupCanvasGroup = PrepareScreen(_setupScreen);

            _setupPanel = CreateFloatingPanel(_setupScreen, "SetupPanel");
            RectTransform setupHeaderRow = CreateHeaderRow(_setupPanel);
            _setupBackButton = CreateInlineButton(setupHeaderRow, "Back", ShowHome);

            _setupTitleText = LudoUtility.CreateText("SetupTitle", _setupPanel, "Local Match", 34, FontStyle.Bold, TextAnchor.MiddleCenter, LudoTheme.TextPrimary);
            ConfigureSection(_setupTitleText.rectTransform, 56f);
            _setupTitleText.raycastTarget = false;

            _setupSubtitleText = LudoUtility.CreateText("SetupSubtitle", _setupPanel, "Pick player count and token colors.", 20, FontStyle.Normal, TextAnchor.MiddleCenter, LudoTheme.TextMuted);
            ConfigureSection(_setupSubtitleText.rectTransform, 36f);
            _setupSubtitleText.raycastTarget = false;

            RectTransform setupScrollRoot = LudoUtility.CreateUIObject("SetupScrollRoot", _setupPanel);
            LayoutElement setupScrollLayout = LudoUtility.GetOrAddComponent<LayoutElement>(setupScrollRoot.gameObject);
            setupScrollLayout.flexibleHeight = 1f;
            setupScrollLayout.minHeight = 260f;
            setupScrollLayout.preferredHeight = 420f;
            Image setupScrollFill = LudoUtility.GetOrAddComponent<Image>(setupScrollRoot.gameObject);
            LudoUtility.ApplySprite(setupScrollFill, LudoSpriteFactory.RoundedMask);
            setupScrollFill.color = new Color(1f, 0.995f, 0.982f, 0.38f);
            setupScrollFill.raycastTarget = true;
            Shadow setupScrollShadow = LudoUtility.GetOrAddComponent<Shadow>(setupScrollRoot.gameObject);
            setupScrollShadow.effectColor = new Color(0.12f, 0.10f, 0.08f, 0.08f);
            setupScrollShadow.effectDistance = new Vector2(0f, -4f);
            setupScrollShadow.useGraphicAlpha = true;

            RectTransform setupViewport = LudoUtility.CreateUIObject("Viewport", setupScrollRoot);
            LudoUtility.Stretch(setupViewport, 4f, 4f, 4f, 4f);
            Image setupViewportFill = LudoUtility.GetOrAddComponent<Image>(setupViewport.gameObject);
            setupViewportFill.color = new Color(1f, 1f, 1f, 0.03f);
            setupViewportFill.raycastTarget = true;
            Mask setupViewportMask = LudoUtility.GetOrAddComponent<Mask>(setupViewport.gameObject);
            setupViewportMask.showMaskGraphic = false;

            _setupScrollContent = LudoUtility.CreateUIObject("Content", setupViewport);
            _setupScrollContent.anchorMin = new Vector2(0f, 1f);
            _setupScrollContent.anchorMax = new Vector2(1f, 1f);
            _setupScrollContent.pivot = new Vector2(0.5f, 1f);
            _setupScrollContent.sizeDelta = new Vector2(0f, 0f);
            _setupScrollContent.anchoredPosition = Vector2.zero;
            VerticalLayoutGroup setupContentLayout = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(_setupScrollContent.gameObject);
            setupContentLayout.spacing = 14f;
            setupContentLayout.childAlignment = TextAnchor.UpperCenter;
            setupContentLayout.childControlHeight = true;
            setupContentLayout.childControlWidth = true;
            setupContentLayout.childForceExpandHeight = false;
            setupContentLayout.childForceExpandWidth = true;
            ContentSizeFitter setupContentFitter = LudoUtility.GetOrAddComponent<ContentSizeFitter>(_setupScrollContent.gameObject);
            setupContentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            setupContentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _setupScrollRect = LudoUtility.GetOrAddComponent<ScrollRect>(setupScrollRoot.gameObject);
            _setupScrollRect.viewport = setupViewport;
            _setupScrollRect.content = _setupScrollContent;
            _setupScrollRect.horizontal = false;
            _setupScrollRect.vertical = true;
            _setupScrollRect.scrollSensitivity = 24f;
            _setupScrollRect.movementType = ScrollRect.MovementType.Clamped;

            _nameSection = CreateSetupSection(_setupScrollContent, "Player Name");
            _playerNameField = CreateTextField(_nameSection, "Your name");
            _playerNameField.text = "Player";

            _onlineEntrySection = CreateSetupSection(_setupScrollContent, "Online Entry");
            _onlineEntryGroup = CreateButtonRow(_onlineEntrySection);
            _onlineCreateButton = CreateSmallChoiceButton(_onlineEntrySection, "Create & Join", () => SetOnlineEntryMode(LudoOnlineEntryMode.CreateAndJoin), _onlineEntryGroup);
            _onlineJoinButton = CreateSmallChoiceButton(_onlineEntrySection, "Join Room", () => SetOnlineEntryMode(LudoOnlineEntryMode.Join), _onlineEntryGroup);

            _roomCodeSection = CreateSetupSection(_setupScrollContent, "Room Code");
            _roomCodeField = CreateTextField(_roomCodeSection, "ABCD12");

            _playerCountSection = CreateSetupSection(_setupScrollContent, "Players");
            _playerCountGroup = CreateButtonRow(_playerCountSection);
            _playerCountButtons[2] = CreateSmallChoiceButton(_playerCountSection, "2", () => SetPlayerCount(2), _playerCountGroup);
            _playerCountButtons[3] = CreateSmallChoiceButton(_playerCountSection, "3", () => SetPlayerCount(3), _playerCountGroup);
            _playerCountButtons[4] = CreateSmallChoiceButton(_playerCountSection, "4", () => SetPlayerCount(4), _playerCountGroup);

            _colorSection = CreateSetupSection(_setupScrollContent, "Token Colors");
            RectTransform colorGridRoot = LudoUtility.CreateUIObject("ColorGrid", _colorSection);
            LayoutElement colorGridLayout = LudoUtility.GetOrAddComponent<LayoutElement>(colorGridRoot.gameObject);
            colorGridLayout.preferredHeight = 280f;
            _colorGrid = LudoUtility.GetOrAddComponent<GridLayoutGroup>(colorGridRoot.gameObject);
            _colorGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
            _colorGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _colorGrid.constraintCount = 2;
            _colorGrid.spacing = new Vector2(12f, 12f);
            _colorGrid.childAlignment = TextAnchor.UpperCenter;

            for (int i = 0; i < s_MenuColorOrder.Length; i++)
            {
                LudoTokenColor color = s_MenuColorOrder[i];
                _colorChips[color] = CreateColorChip(colorGridRoot, color, () => ToggleColorSelection(color));
            }

            _localColorSection = CreateSetupSection(_setupScrollContent, "Your Color");
            _localColorGroup = CreateButtonRow(_localColorSection);
            for (int i = 0; i < s_MenuColorOrder.Length; i++)
            {
                LudoTokenColor color = s_MenuColorOrder[i];
                _localColorButtons[color] = CreateSmallChoiceButton(_localColorSection, LudoBoardGeometry.GetPlayerName(color), () => SetLocalColor(color), _localColorGroup);
            }

            _setupHintText = LudoUtility.CreateText("SetupHint", _setupPanel, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleCenter, AppAccentTextColor);
            ConfigureSection(_setupHintText.rectTransform, 52f);
            _setupHintText.raycastTarget = false;

            _playButton = CreateBottomActionButton(_setupPanel, "Play", OnPlayPressed);
        }

        private void BuildLobbyScreen()
        {
            _lobbyScreen = LudoUtility.CreateUIObject("LobbyScreen", _appRoot);
            LudoUtility.Stretch(_lobbyScreen);
            _lobbyCanvasGroup = PrepareScreen(_lobbyScreen);

            _lobbyPanel = CreateFloatingPanel(_lobbyScreen, "LobbyPanel");
            RectTransform lobbyHeaderRow = CreateHeaderRow(_lobbyPanel);
            _lobbyBackButton = CreateInlineButton(lobbyHeaderRow, "Back", ExitToHome);

            _lobbyTitleText = LudoUtility.CreateText("LobbyTitle", _lobbyPanel, "Room", 34, FontStyle.Bold, TextAnchor.MiddleCenter, LudoTheme.TextPrimary);
            ConfigureSection(_lobbyTitleText.rectTransform, 54f);
            _lobbyTitleText.raycastTarget = false;

            _lobbyStatusText = LudoUtility.CreateText("LobbyStatus", _lobbyPanel, "Waiting for players.", 20, FontStyle.Normal, TextAnchor.MiddleCenter, LudoTheme.TextMuted);
            ConfigureSection(_lobbyStatusText.rectTransform, 44f);
            _lobbyStatusText.raycastTarget = false;

            _lobbySeatList = LudoUtility.CreateUIObject("SeatList", _lobbyPanel);
            LayoutElement seatListLayout = LudoUtility.GetOrAddComponent<LayoutElement>(_lobbySeatList.gameObject);
            seatListLayout.flexibleHeight = 1f;
            seatListLayout.minHeight = 220f;
            seatListLayout.preferredHeight = 320f;
            VerticalLayoutGroup seatListGroup = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(_lobbySeatList.gameObject);
            seatListGroup.spacing = 12f;
            seatListGroup.childAlignment = TextAnchor.UpperLeft;
            seatListGroup.childControlHeight = true;
            seatListGroup.childControlWidth = true;
            seatListGroup.childForceExpandHeight = false;
            seatListGroup.childForceExpandWidth = true;

            for (int i = 0; i < s_MenuColorOrder.Length; i++)
            {
                LudoTokenColor color = s_MenuColorOrder[i];
                _lobbySeats[color] = CreateLobbySeat(_lobbySeatList, color);
            }

            _lobbyStartButton = CreateBottomActionButton(_lobbyPanel, "Start Match", OnStartMatchPressed);
        }

        private void BuildGameplayOverlay()
        {
            _gameplayOverlay = LudoUtility.CreateUIObject("GameplayOverlay", _appRoot);
            LudoUtility.Stretch(_gameplayOverlay);
            _gameplayCanvasGroup = PrepareScreen(_gameplayOverlay);

            RectTransform topBar = LudoUtility.CreateUIObject("TopBar", _gameplayOverlay);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 96f);

            RectTransform roomBadge = LudoUtility.CreateUIObject("RoomBadge", topBar);
            roomBadge.anchorMin = roomBadge.anchorMax = new Vector2(0.5f, 0.5f);
            roomBadge.pivot = new Vector2(0.5f, 0.5f);
            Image roomBadgeFill = LudoUtility.GetOrAddComponent<Image>(roomBadge.gameObject);
            LudoUtility.ApplySprite(roomBadgeFill, LudoSpriteFactory.RoundedMask);
            roomBadgeFill.color = new Color(1f, 0.985f, 0.965f, 0.94f);
            roomBadgeFill.raycastTarget = false;
            Shadow roomBadgeShadow = LudoUtility.GetOrAddComponent<Shadow>(roomBadge.gameObject);
            roomBadgeShadow.effectColor = new Color(0.10f, 0.08f, 0.06f, 0.10f);
            roomBadgeShadow.effectDistance = new Vector2(0f, -3f);
            roomBadgeShadow.useGraphicAlpha = true;
            _roomBadgeText = LudoUtility.CreateText("RoomBadgeText", roomBadge, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleCenter, LudoTheme.TextPrimary);
            LudoUtility.Stretch(_roomBadgeText.rectTransform, 16f, 16f, 10f, 10f);
            _roomBadgeText.raycastTarget = false;

            _connectionText = LudoUtility.CreateText("Connection", topBar, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleRight, LudoTheme.TextMuted);
            _connectionText.rectTransform.anchorMin = _connectionText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _connectionText.rectTransform.pivot = new Vector2(1f, 0.5f);
            _connectionText.rectTransform.anchoredPosition = new Vector2(-22f, -18f);
            _connectionText.rectTransform.sizeDelta = new Vector2(280f, 34f);
            _connectionText.raycastTarget = false;

            GameObject chatObject = new GameObject("ChatPanel", typeof(RectTransform));
            _chatPanel = chatObject.AddComponent<LudoChatPanel>();
            _chatPanel.Build(_gameplayOverlay, SendChatMessage);
        }

        private void RefreshLayout()
        {
            if (_uiLayer == null)
            {
                return;
            }

            float width = _uiLayer.rect.width;
            float height = _uiLayer.rect.height;
            bool compactPhoneLayout = IsCompactPhoneLayout(width, height);

            if (compactPhoneLayout)
            {
                SetPanelSize(_homePanel, Mathf.Clamp(width * 0.96f, 600f, 1080f), Mathf.Clamp(height * 0.78f, 900f, 1480f));
                SetPanelSize(_setupPanel, Mathf.Clamp(width * 0.975f, 660f, 1080f), Mathf.Clamp(height * 0.92f, 1080f, 1640f));
                SetPanelSize(_lobbyPanel, Mathf.Clamp(width * 0.965f, 640f, 1040f), Mathf.Clamp(height * 0.88f, 980f, 1480f));
            }
            else
            {
                SetPanelSize(_homePanel, Mathf.Clamp(width * 0.74f, 380f, 720f), Mathf.Clamp(height * 0.58f, 420f, 640f));
                SetPanelSize(_setupPanel, Mathf.Clamp(width * 0.82f, 420f, 760f), Mathf.Clamp(height * 0.84f, 620f, 1040f));
                SetPanelSize(_lobbyPanel, Mathf.Clamp(width * 0.80f, 400f, 720f), Mathf.Clamp(height * 0.72f, 520f, 820f));
            }

            if (_colorGrid != null)
            {
                float availableWidth = _setupPanel.rect.width - (compactPhoneLayout ? 52f : 76f);
                float cellWidth = Mathf.Max(compactPhoneLayout ? 170f : 140f, (availableWidth - _colorGrid.spacing.x) * 0.5f);
                float cellHeight = Mathf.Clamp(_setupPanel.rect.height * (compactPhoneLayout ? 0.16f : 0.17f), compactPhoneLayout ? 156f : 122f, compactPhoneLayout ? 220f : 168f);
                _colorGrid.cellSize = new Vector2(cellWidth, cellHeight);
            }

            float panelContentWidth = Mathf.Max(compactPhoneLayout ? 320f : 220f, _setupPanel.rect.width - (compactPhoneLayout ? 28f : 48f));
            RefreshChoiceButtonWidths(_onlineEntryGroup, panelContentWidth);
            RefreshChoiceButtonWidths(_playerCountGroup, panelContentWidth);
            RefreshChoiceButtonWidths(_localColorGroup, panelContentWidth);
            RefreshHomeLayout(compactPhoneLayout);
            RefreshSetupTypography(compactPhoneLayout);

            if (_roomBadgeText != null)
            {
                RectTransform badgeRect = _roomBadgeText.rectTransform.parent as RectTransform;
                if (badgeRect != null)
                {
                    badgeRect.sizeDelta = new Vector2(Mathf.Clamp(width * 0.30f, 180f, 320f), 48f);
                    badgeRect.anchoredPosition = new Vector2(0f, -18f);
                }
            }
        }

        private void RefreshHomeLayout(bool compactPhoneLayout)
        {
            if (_homeTitleText != null)
            {
                _homeTitleText.fontSize = compactPhoneLayout ? 68 : 42;
                SetPreferredHeight(_homeTitleText.rectTransform, compactPhoneLayout ? 132f : 80f);
            }

            if (_homeSubtitleText != null)
            {
                _homeSubtitleText.fontSize = compactPhoneLayout ? 30 : 22;
                SetPreferredHeight(_homeSubtitleText.rectTransform, compactPhoneLayout ? 72f : 44f);
            }

            RefreshLargeActionButtonLayout(_homeLocalButton, compactPhoneLayout);
            RefreshLargeActionButtonLayout(_homeComputerButton, compactPhoneLayout);
            RefreshLargeActionButtonLayout(_homeOnlineButton, compactPhoneLayout);
        }

        private void RefreshSetupTypography(bool compactPhoneLayout)
        {
            if (_setupTitleText != null)
            {
                _setupTitleText.fontSize = compactPhoneLayout ? 42 : 34;
            }

            if (_setupSubtitleText != null)
            {
                _setupSubtitleText.fontSize = compactPhoneLayout ? 24 : 20;
            }

            if (_lobbyTitleText != null)
            {
                _lobbyTitleText.fontSize = compactPhoneLayout ? 42 : 34;
            }

            if (_lobbyStatusText != null)
            {
                _lobbyStatusText.fontSize = compactPhoneLayout ? 24 : 20;
            }

            ResizeButton(_playButton, compactPhoneLayout ? 82f : 64f, compactPhoneLayout ? 26 : 22);
            ResizeButton(_lobbyStartButton, compactPhoneLayout ? 82f : 64f, compactPhoneLayout ? 26 : 22);
            ResizeButton(_setupBackButton, compactPhoneLayout ? 58f : 48f, compactPhoneLayout ? 22 : 18);
            ResizeButton(_lobbyBackButton, compactPhoneLayout ? 58f : 48f, compactPhoneLayout ? 22 : 18);
        }

        private static void RefreshLargeActionButtonLayout(ButtonView buttonView, bool compactPhoneLayout)
        {
            if (buttonView == null || buttonView.Root == null)
            {
                return;
            }

            LayoutElement layout = buttonView.Root.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = compactPhoneLayout ? 156f : 92f;
            }

            Transform titleTransform = buttonView.Root.Find("Content/Title");
            if (titleTransform != null && titleTransform.TryGetComponent(out Text titleText))
            {
                titleText.fontSize = compactPhoneLayout ? 38 : 24;
                titleText.rectTransform.offsetMax = compactPhoneLayout ? new Vector2(0f, -34f) : new Vector2(0f, -20f);
                titleText.rectTransform.offsetMin = compactPhoneLayout ? new Vector2(0f, 24f) : new Vector2(0f, 4f);
            }

            Transform subtitleTransform = buttonView.Root.Find("Content/Subtitle");
            if (subtitleTransform != null && subtitleTransform.TryGetComponent(out Text subtitleText))
            {
                subtitleText.fontSize = compactPhoneLayout ? 20 : 15;
                subtitleText.rectTransform.sizeDelta = compactPhoneLayout ? new Vector2(0f, 34f) : new Vector2(0f, 22f);
            }
        }

        private static void ResizeButton(ButtonView buttonView, float height, int fontSize)
        {
            if (buttonView == null || buttonView.Root == null)
            {
                return;
            }

            LayoutElement layout = buttonView.Root.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = height;
            }

            if (buttonView.Label != null)
            {
                buttonView.Label.fontSize = fontSize;
            }
        }

        private static void SetPreferredHeight(RectTransform rectTransform, float preferredHeight)
        {
            if (rectTransform == null)
            {
                return;
            }

            LayoutElement layout = rectTransform.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredHeight = preferredHeight;
            }
        }

        private static bool IsCompactPhoneLayout(float width, float height)
        {
            if (!Application.isMobilePlatform)
            {
                return false;
            }

            float aspect = height / Mathf.Max(1f, width);
            return width <= 760f || aspect >= 1.55f;
        }

        private void OpenSetup(LudoGameMode mode)
        {
            ApplyDefaultSelection(mode);
            _currentScreen = AppScreen.Setup;
            RefreshSetupUi();
            if (_setupScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _setupScrollRect.verticalNormalizedPosition = 1f;
            }
            RefreshScreenVisibility();
        }

        private void ShowHome()
        {
            _currentScreen = AppScreen.Home;
            RefreshScreenVisibility();
        }

        private void ShowLobby()
        {
            _currentScreen = AppScreen.Lobby;
            RefreshLobbyUi();
            RefreshScreenVisibility();
        }

        private void ShowGameplay()
        {
            _currentScreen = AppScreen.Gameplay;
            RefreshGameplayOverlay();
            RefreshScreenVisibility();
        }

        private void ExitToHome()
        {
            if (_gameController != null && _gameController.SessionActive)
            {
                _gameController.EndSession();
            }

            _activeSession = null;
            _roomSnapshot = null;
            if (_onlineService != null)
            {
                _onlineService.LeaveRoom();
            }

            if (_chatPanel != null)
            {
                _chatPanel.ClearMessages();
                _chatPanel.SetVisible(false);
            }

            _setupHintText.text = string.Empty;
            ShowHome();
        }

        private void RefreshScreenVisibility()
        {
            bool screenChanged = _visibleScreen != _currentScreen;
            _visibleScreen = _currentScreen;

            SetScreenVisible(_homeScreen, _homeCanvasGroup, _currentScreen == AppScreen.Home, screenChanged);
            SetScreenVisible(_setupScreen, _setupCanvasGroup, _currentScreen == AppScreen.Setup, screenChanged);
            SetScreenVisible(_lobbyScreen, _lobbyCanvasGroup, _currentScreen == AppScreen.Lobby, screenChanged);

            bool showGameplayOverlay = _currentScreen == AppScreen.Gameplay;
            SetScreenVisible(_gameplayOverlay, _gameplayCanvasGroup, showGameplayOverlay, screenChanged);
            if (_chatPanel != null)
            {
                _chatPanel.SetVisible(showGameplayOverlay && _activeSession != null && _activeSession.UsesNetwork);
            }

            RefreshGameplayOverlay();
        }

        private CanvasGroup PrepareScreen(RectTransform screenRoot)
        {
            if (screenRoot == null)
            {
                return null;
            }

            CanvasGroup canvasGroup = LudoUtility.GetOrAddComponent<CanvasGroup>(screenRoot.gameObject);
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            screenRoot.anchoredPosition = Vector2.zero;
            return canvasGroup;
        }

        private void SetScreenVisible(RectTransform screenRoot, CanvasGroup canvasGroup, bool visible, bool animate)
        {
            if (screenRoot == null)
            {
                return;
            }

            if (!visible)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                    canvasGroup.interactable = false;
                    canvasGroup.blocksRaycasts = false;
                }

                screenRoot.anchoredPosition = Vector2.zero;
                screenRoot.gameObject.SetActive(false);
                return;
            }

            screenRoot.gameObject.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }

            if (!animate)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }

                screenRoot.anchoredPosition = Vector2.zero;
                return;
            }

            if (_animationController == null || canvasGroup == null)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1f;
                }

                screenRoot.anchoredPosition = Vector2.zero;
                return;
            }

            float now = _animationController.TimeNow;
            Vector2 fromPosition = new Vector2(0f, -18f);
            screenRoot.anchoredPosition = fromPosition;
            canvasGroup.alpha = 0f;
            _animationController.ScheduleMove(screenRoot, fromPosition, Vector2.zero, now, 0.22f, LudoEase.EaseOut);
            _animationController.ScheduleCanvasGroupAlpha(canvasGroup, 0f, 1f, now, 0.20f, LudoEase.EaseOut);
        }

        private static void RefreshChoiceButtonWidths(HorizontalLayoutGroup group, float availableWidth)
        {
            if (group == null)
            {
                return;
            }

            int itemCount = 0;
            for (int i = 0; i < group.transform.childCount; i++)
            {
                if (group.transform.GetChild(i).gameObject.activeSelf)
                {
                    itemCount++;
                }
            }

            if (itemCount <= 0)
            {
                return;
            }

            float spacing = group.spacing * Mathf.Max(0, itemCount - 1);
            float targetWidth = Mathf.Max(76f, (availableWidth - spacing) / itemCount);
            for (int i = 0; i < group.transform.childCount; i++)
            {
                LayoutElement layout = group.transform.GetChild(i).GetComponent<LayoutElement>();
                if (layout != null)
                {
                    layout.preferredWidth = targetWidth;
                }
            }
        }

        private void RefreshSetupUi()
        {
            bool isOnline = _setupMode == LudoGameMode.Online;
            bool isCreateOnline = isOnline && _onlineEntryMode == LudoOnlineEntryMode.CreateAndJoin;
            bool isJoinOnline = isOnline && _onlineEntryMode == LudoOnlineEntryMode.Join;
            bool needsLocalColor = _setupMode == LudoGameMode.Computer || isOnline;

            if (_setupTitleText != null)
            {
                _setupTitleText.text = isOnline ? "Online Match" : (_setupMode == LudoGameMode.Computer ? "Vs Computer" : "Local Match");
            }

            if (_setupSubtitleText != null)
            {
                _setupSubtitleText.text = isJoinOnline
                    ? "Enter the room code and choose your preferred color."
                    : "Pick the seats that will be used in this match.";
            }

            if (_nameSection != null)
            {
                _nameSection.gameObject.SetActive(_setupMode != LudoGameMode.Local);
            }

            if (_onlineEntrySection != null)
            {
                _onlineEntrySection.gameObject.SetActive(isOnline);
            }

            if (_roomCodeSection != null)
            {
                _roomCodeSection.gameObject.SetActive(isJoinOnline);
            }

            if (_playerCountSection != null)
            {
                _playerCountSection.gameObject.SetActive(!isJoinOnline);
            }

            if (_colorSection != null)
            {
                _colorSection.gameObject.SetActive(!isJoinOnline);
            }

            if (_localColorSection != null)
            {
                _localColorSection.gameObject.SetActive(needsLocalColor);
            }

            foreach (KeyValuePair<int, ButtonView> entry in _playerCountButtons)
            {
                SetButtonSelected(entry.Value, entry.Key == _selectedPlayerCount, new Color(0.98f, 0.78f, 0.28f, 0.22f));
            }

            SetButtonSelected(_onlineCreateButton, _onlineEntryMode == LudoOnlineEntryMode.CreateAndJoin, new Color(0.98f, 0.78f, 0.28f, 0.22f));
            SetButtonSelected(_onlineJoinButton, _onlineEntryMode == LudoOnlineEntryMode.Join, new Color(0.98f, 0.78f, 0.28f, 0.22f));

            for (int i = 0; i < s_MenuColorOrder.Length; i++)
            {
                LudoTokenColor color = s_MenuColorOrder[i];
                ColorChipView chipView = _colorChips[color];
                bool isSelected = _selectedColors.Contains(color);
                SetButtonSelected(chipView.ButtonView, isSelected, LudoUtility.WithAlpha(LudoTheme.GetTokenTint(color), 0.18f));
                chipView.Subtitle.text = isSelected ? BuildChipSubtitle(color) : "Tap to add";
                chipView.Fill.color = isSelected
                    ? Color.Lerp(AppSelectedFillColor, LudoTheme.GetTokenTint(color), 0.16f)
                    : new Color(1f, 1f, 1f, 0.86f);
                chipView.Title.color = LudoTheme.TextPrimary;
                chipView.Subtitle.color = isSelected ? LudoTheme.TextMuted : AppMutedTextColor;
            }

            for (int i = 0; i < s_MenuColorOrder.Length; i++)
            {
                LudoTokenColor color = s_MenuColorOrder[i];
                ButtonView buttonView = _localColorButtons[color];
                bool available = isJoinOnline || _selectedColors.Contains(color);
                buttonView.Root.gameObject.SetActive(available);
                if (available)
                {
                    SetButtonSelected(buttonView, _localColor == color, LudoUtility.WithAlpha(LudoTheme.GetTokenTint(color), 0.22f));
                }
            }

            if (_playButton != null)
            {
                _playButton.Label.text = isOnline ? (_onlineEntryMode == LudoOnlineEntryMode.CreateAndJoin ? "Create Room" : "Join Room") : "Play";
            }

            if (_setupHintText != null && string.IsNullOrEmpty(_setupHintText.text))
            {
                _setupHintText.text = BuildSetupHint();
            }

            RefreshOnlineBusyState();
        }

        private void RefreshOnlineBusyState()
        {
            bool isBusy = _setupMode == LudoGameMode.Online && _onlineService != null && _onlineService.HasPendingCommand;

            SetButtonInteractable(_playButton, !isBusy);
            SetButtonInteractable(_setupBackButton, !isBusy);
            SetButtonInteractable(_onlineCreateButton, !isBusy);
            SetButtonInteractable(_onlineJoinButton, !isBusy);

            foreach (KeyValuePair<int, ButtonView> entry in _playerCountButtons)
            {
                SetButtonInteractable(entry.Value, !isBusy);
            }

            foreach (KeyValuePair<LudoTokenColor, ColorChipView> entry in _colorChips)
            {
                if (entry.Value != null)
                {
                    SetButtonInteractable(entry.Value.ButtonView, !isBusy);
                }
            }

            foreach (KeyValuePair<LudoTokenColor, ButtonView> entry in _localColorButtons)
            {
                SetButtonInteractable(entry.Value, !isBusy);
            }

            if (_playerNameField != null)
            {
                _playerNameField.interactable = !isBusy;
            }

            if (_roomCodeField != null)
            {
                _roomCodeField.interactable = !isBusy;
            }
        }

        private string BuildChipSubtitle(LudoTokenColor color)
        {
            if (_setupMode == LudoGameMode.Computer || _setupMode == LudoGameMode.Online)
            {
                if (_localColor == color)
                {
                    return _setupMode == LudoGameMode.Online ? "Your seat" : "You";
                }

                return _setupMode == LudoGameMode.Online ? "Remote seat" : "Computer";
            }

            return "Ready";
        }

        private string BuildSetupHint()
        {
            if (_setupMode == LudoGameMode.Online)
            {
                return _onlineEntryMode == LudoOnlineEntryMode.Join
                    ? "Enter your name and the room code to join the match."
                    : "Enter your name, choose the room seats, then share the generated code.";
            }

            return "Pick exactly " + _selectedPlayerCount + " colors for this match.";
        }

        private void RefreshLobbyUi()
        {
            if (_roomSnapshot == null || _lobbySeats == null || _lobbySeats.Count == 0)
            {
                return;
            }

            if (_lobbyTitleText != null)
            {
                _lobbyTitleText.text = "Room " + _roomSnapshot.RoomCode;
            }

            List<LudoTokenColor> activeColors = GetActiveColorsFromSnapshot(_roomSnapshot);
            int connectedCount = 0;
            for (int i = 0; i < s_MenuColorOrder.Length; i++)
            {
                LudoTokenColor color = s_MenuColorOrder[i];
                LobbySeatView seatView;
                if (!_lobbySeats.TryGetValue(color, out seatView) || seatView == null || seatView.Root == null)
                {
                    continue;
                }

                bool isActive = activeColors.Contains(color);
                seatView.Root.gameObject.SetActive(isActive);
                if (!isActive)
                {
                    continue;
                }

                LudoOnlineSeatState seat = FindSeat(_roomSnapshot, color);
                bool connected = seat != null && seat.Connected;
                if (connected)
                {
                    connectedCount++;
                }

                seatView.Fill.color = connected
                    ? Color.Lerp(AppPanelColor, LudoTheme.GetTokenTint(color), 0.22f)
                    : new Color(1f, 0.992f, 0.978f, 0.88f);
                if (seatView.Title != null)
                {
                    seatView.Title.text = LudoBoardGeometry.GetPlayerName(color);
                }

                if (seatView.Subtitle != null)
                {
                    seatView.Subtitle.text = connected
                        ? (string.IsNullOrEmpty(seat.DisplayName) ? "Connected" : seat.DisplayName + (seat.IsHost ? " (Host)" : string.Empty))
                        : "Waiting...";
                }
            }

            bool readyToStart = connectedCount >= 2;
            int remainingPlayers = Mathf.Max(0, 2 - connectedCount);
            if (_lobbyStatusText != null)
            {
                if (_roomSnapshot.Started)
                {
                    _lobbyStatusText.text = "Match starting...";
                }
                else if (_onlineService != null && _onlineService.IsHost)
                {
                    _lobbyStatusText.text = readyToStart
                        ? "Start whenever you want. The match will use the connected players only."
                        : "Waiting for " + remainingPlayers + " more player" + (remainingPlayers == 1 ? "." : "s.");
                }
                else
                {
                    _lobbyStatusText.text = readyToStart ? "Waiting for the host to start." : "Waiting for at least 2 players.";
                }
            }

            if (_lobbyStartButton != null)
            {
                _lobbyStartButton.Root.gameObject.SetActive(_onlineService != null && _onlineService.IsHost);
                if (_lobbyStartButton.Button != null)
                {
                    _lobbyStartButton.Button.interactable = readyToStart && !_roomSnapshot.Started;
                }
            }
        }

        private void RefreshGameplayOverlay()
        {
            bool showOnline = _activeSession != null && _activeSession.UsesNetwork;
            if (_roomBadgeText != null)
            {
                _roomBadgeText.rectTransform.parent.gameObject.SetActive(showOnline);
                _roomBadgeText.text = showOnline ? "Room " + _activeSession.RoomCode : string.Empty;
            }

            if (_connectionText != null)
            {
                _connectionText.gameObject.SetActive(showOnline);
            }
        }

        private void ApplyDefaultSelection(LudoGameMode mode)
        {
            _setupMode = mode;
            _onlineEntryMode = LudoOnlineEntryMode.CreateAndJoin;
            _selectedPlayerCount = 2;
            _selectedColors.Clear();
            _selectedColors.AddRange(GetDefaultColorsForCount(_selectedPlayerCount));
            _localColor = _selectedColors[0];
            _roomSnapshot = null;
            if (_roomCodeField != null)
            {
                _roomCodeField.text = string.Empty;
            }

            if (_playerNameField != null)
            {
                _playerNameField.text = mode == LudoGameMode.Online ? string.Empty : "Player";
            }

            if (_setupHintText != null)
            {
                _setupHintText.text = string.Empty;
            }

            RefreshSetupUi();
        }

        private void SetPlayerCount(int playerCount)
        {
            _selectedPlayerCount = Mathf.Clamp(playerCount, 2, 4);
            _selectedColors.Clear();
            _selectedColors.AddRange(GetDefaultColorsForCount(_selectedPlayerCount));
            if (!_selectedColors.Contains(_localColor))
            {
                _localColor = _selectedColors[0];
            }

            _setupHintText.text = string.Empty;
            RefreshSetupUi();
        }

        private void SetOnlineEntryMode(LudoOnlineEntryMode mode)
        {
            _onlineEntryMode = mode;
            _setupHintText.text = string.Empty;
            RefreshSetupUi();
        }

        private void ToggleColorSelection(LudoTokenColor color)
        {
            bool isSelected = _selectedColors.Contains(color);
            if (isSelected)
            {
                _selectedColors.Remove(color);
                if (_selectedColors.Count > 0 && _localColor == color)
                {
                    _localColor = _selectedColors[0];
                }
            }
            else
            {
                if (_selectedColors.Count >= _selectedPlayerCount)
                {
                    _setupHintText.text = "Choose exactly " + _selectedPlayerCount + " colors.";
                    RefreshSetupUi();
                    return;
                }

                _selectedColors.Add(color);
            }

            if (_selectedColors.Count == 0)
            {
                _selectedColors.Add(color);
                _localColor = color;
            }

            if (!_selectedColors.Contains(_localColor))
            {
                _localColor = _selectedColors[0];
            }

            _setupHintText.text = string.Empty;
            RefreshSetupUi();
        }

        private void SetLocalColor(LudoTokenColor color)
        {
            if (_setupMode != LudoGameMode.Online && !_selectedColors.Contains(color))
            {
                return;
            }

            _localColor = color;
            _setupHintText.text = string.Empty;
            RefreshSetupUi();
        }

        private void OnPlayPressed()
        {
            if (_setupMode == LudoGameMode.Online)
            {
                BeginOnlineFlow();
                return;
            }

            LudoSessionConfig sessionConfig;
            string error;
            if (!TryBuildOfflineSession(out sessionConfig, out error))
            {
                _setupHintText.text = error;
                RefreshSetupUi();
                return;
            }

            BeginGameplay(sessionConfig);
        }

        private void BeginOnlineFlow()
        {
            if (_onlineService == null)
            {
                _setupHintText.text = "Online service is not ready.";
                return;
            }

            if (_gameController != null && _gameController.SessionActive)
            {
                _gameController.EndSession();
            }

            _activeSession = null;
            if (_chatPanel != null)
            {
                _chatPanel.ClearMessages();
                _chatPanel.SetVisible(false);
            }

            if (_onlineService.IsConnected || !string.IsNullOrEmpty(_onlineService.RoomCode))
            {
                _onlineService.LeaveRoom();
            }

            string playerName;
            string nameError;
            if (!TryGetRequiredOnlinePlayerName(out playerName, out nameError))
            {
                _setupHintText.text = nameError;
                return;
            }

            if (_onlineEntryMode == LudoOnlineEntryMode.CreateAndJoin)
            {
                if (_selectedColors.Count != _selectedPlayerCount)
                {
                    _setupHintText.text = "Choose exactly " + _selectedPlayerCount + " colors before creating the room.";
                    return;
                }

                _setupHintText.text = "Creating room...";
                _onlineService.CreateRoomAndJoin(playerName, _selectedPlayerCount, BuildActiveColorsInBoardOrder(), _localColor);
                RefreshSetupUi();
                return;
            }

            if (string.IsNullOrWhiteSpace(_roomCodeField != null ? _roomCodeField.text : string.Empty))
            {
                _setupHintText.text = "Enter the room code first.";
                return;
            }

            _setupHintText.text = "Joining room...";
            _onlineService.JoinRoom(_roomCodeField.text, playerName, _localColor);
            RefreshSetupUi();
        }

        private bool TryBuildOfflineSession(out LudoSessionConfig sessionConfig, out string error)
        {
            sessionConfig = null;
            error = string.Empty;
            List<LudoTokenColor> activeColors = BuildActiveColorsInBoardOrder();
            if (activeColors.Count != _selectedPlayerCount)
            {
                error = "Choose exactly " + _selectedPlayerCount + " colors before playing.";
                return false;
            }

            sessionConfig = new LudoSessionConfig
            {
                Mode = _setupMode,
                LocalPlayerName = GetPlayerNameInput(),
            };

            int playerCounter = 1;
            int aiCounter = 1;
            for (int i = 0; i < activeColors.Count; i++)
            {
                LudoTokenColor color = activeColors[i];
                LudoParticipantConfig participant = new LudoParticipantConfig
                {
                    Color = color,
                };

                if (_setupMode == LudoGameMode.Local)
                {
                    participant.Control = LudoParticipantControl.HumanLocal;
                    participant.IsLocal = true;
                    participant.DisplayName = "Player " + playerCounter++;
                }
                else
                {
                    bool isLocal = color == _localColor;
                    participant.Control = isLocal ? LudoParticipantControl.HumanLocal : LudoParticipantControl.AI;
                    participant.IsLocal = isLocal;
                    participant.DisplayName = isLocal ? sessionConfig.LocalPlayerName : "Computer " + aiCounter++;
                }

                sessionConfig.Participants.Add(participant);
            }

            return true;
        }

        private void BeginGameplay(LudoSessionConfig sessionConfig)
        {
            if (sessionConfig == null || _gameController == null)
            {
                return;
            }

            if (_gameController.SessionActive)
            {
                _gameController.EndSession();
            }

            _activeSession = sessionConfig;
            _gameController.StartSession(sessionConfig);
            if (_chatPanel != null)
            {
                _chatPanel.ClearMessages();
                _chatPanel.SetMinimized(true);
            }

            ShowGameplay();
        }

        private void OnStartMatchPressed()
        {
            if (_onlineService != null)
            {
                _onlineService.StartMatch();
            }
        }

        private void OnRoomSnapshotReceived(LudoRoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            try
            {
                LudoTokenColor assignedColor;
                if (_onlineService != null && TryParseColor(_onlineService.LocalColor, out assignedColor))
                {
                    _localColor = assignedColor;
                }

                _roomSnapshot = snapshot;
                if (_activeSession != null && _activeSession.UsesNetwork && _gameController != null && _gameController.SessionActive)
                {
                    if (snapshot.Started)
                    {
                        _gameController.ApplyOnlineSnapshot(snapshot, true);
                        ShowGameplay();
                        return;
                    }
                }

                ShowLobby();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnOnlineErrorReceived("Failed to open the online lobby.");
            }
        }

        private void OnOnlineMatchStarted(LudoRoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            try
            {
                _roomSnapshot = snapshot;

                if (_activeSession == null || !_activeSession.UsesNetwork || _gameController == null || !_gameController.SessionActive)
                {
                    LudoSessionConfig session = BuildOnlineSession(snapshot);
                    if (session == null)
                    {
                        return;
                    }

                    BeginGameplay(session);
                }

                if (_gameController != null && _gameController.SessionActive)
                {
                    _gameController.ApplyOnlineSnapshot(snapshot, true);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                OnOnlineErrorReceived("Failed to start the online match.");
            }
        }

        private void OnChatMessageReceived(LudoChatMessage message)
        {
            if (_chatPanel == null || message == null)
            {
                return;
            }

            LudoTokenColor color;
            bool hasColor = TryParseColor(message.Color, out color);
            Color accentColor = hasColor ? LudoTheme.GetTokenTint(color) : new Color(0.22f, 0.42f, 0.92f, 1f);
            bool isLocal = _onlineService != null && string.Equals(message.Color, _onlineService.LocalColor, StringComparison.OrdinalIgnoreCase);
            _chatPanel.AppendMessage(message.Sender, message.Message, accentColor, isLocal);
        }

        private void OnTurnActionReceived(LudoTurnActionMessage action)
        {
            if (_gameController == null || action == null || !_gameController.SessionActive || _activeSession == null || !_activeSession.UsesNetwork)
            {
                return;
            }

            if (_onlineService != null && !string.IsNullOrEmpty(action.PlayerId) && string.Equals(action.PlayerId, _onlineService.LocalPlayerId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _gameController.ApplyRemoteTurnAction(action);
        }

        private void OnLocalOnlineTurnCommitted(LudoTurnActionMessage action)
        {
            if (_activeSession == null || !_activeSession.UsesNetwork || _onlineService == null)
            {
                return;
            }

            _onlineService.SendTurnAction(action);
        }

        private void OnOnlineStatusChanged(string status)
        {
            if (_connectionText != null)
            {
                _connectionText.text = status ?? string.Empty;
            }

            if (_currentScreen == AppScreen.Setup && _setupHintText != null && !string.IsNullOrEmpty(status))
            {
                _setupHintText.text = status;
                RefreshSetupUi();
            }

            if (_currentScreen == AppScreen.Lobby && _lobbyStatusText != null && !string.IsNullOrEmpty(status))
            {
                _lobbyStatusText.text = status;
            }
        }

        private void OnOnlineErrorReceived(string error)
        {
            if (_currentScreen == AppScreen.Setup && _setupHintText != null)
            {
                _setupHintText.text = error;
                RefreshSetupUi();
            }

            if (_currentScreen == AppScreen.Lobby && _lobbyStatusText != null)
            {
                _lobbyStatusText.text = error;
            }

            if (_connectionText != null)
            {
                _connectionText.text = error;
            }
        }

        private void SendChatMessage(string message)
        {
            if (_activeSession == null || !_activeSession.UsesNetwork || _onlineService == null)
            {
                return;
            }

            string sender = !string.IsNullOrWhiteSpace(_activeSession.LocalPlayerName)
                ? _activeSession.LocalPlayerName
                : (_onlineService != null ? _onlineService.LocalPlayerName : GetPlayerNameInput());
            _onlineService.SendChat(sender, message, _localColor);
        }

        private LudoSessionConfig BuildOnlineSession(LudoRoomSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            List<LudoTokenColor> activeColors = GetActiveColorsFromSnapshot(snapshot);
            if (activeColors.Count < 2)
            {
                return null;
            }

            LudoSessionConfig sessionConfig = new LudoSessionConfig
            {
                Mode = LudoGameMode.Online,
                LocalPlayerName = _onlineService != null && !string.IsNullOrWhiteSpace(_onlineService.LocalPlayerName)
                    ? _onlineService.LocalPlayerName
                    : GetPlayerNameInput(),
                RoomCode = snapshot.RoomCode,
                IsHost = _onlineService != null && _onlineService.IsHost,
            };

            for (int i = 0; i < activeColors.Count; i++)
            {
                LudoTokenColor color = activeColors[i];
                LudoOnlineSeatState seat = FindSeat(snapshot, color);
                bool isLocal = _onlineService != null && string.Equals(_onlineService.LocalColor, color.ToString(), StringComparison.OrdinalIgnoreCase);
                sessionConfig.Participants.Add(new LudoParticipantConfig
                {
                    Color = color,
                    Control = isLocal ? LudoParticipantControl.HumanLocal : LudoParticipantControl.Remote,
                    IsLocal = isLocal,
                    DisplayName = seat != null && !string.IsNullOrWhiteSpace(seat.DisplayName)
                        ? seat.DisplayName
                        : (isLocal ? sessionConfig.LocalPlayerName : LudoBoardGeometry.GetPlayerName(color)),
                });
            }

            return sessionConfig;
        }

        private List<LudoTokenColor> BuildActiveColorsInBoardOrder()
        {
            List<LudoTokenColor> colors = new List<LudoTokenColor>(4);
            IReadOnlyList<LudoTokenColor> clockwise = LudoBoardGeometry.ClockwiseColors;
            for (int i = 0; i < clockwise.Count; i++)
            {
                if (_selectedColors.Contains(clockwise[i]))
                {
                    colors.Add(clockwise[i]);
                }
            }

            return colors;
        }

        private static List<LudoTokenColor> GetDefaultColorsForCount(int count)
        {
            List<LudoTokenColor> colors = new List<LudoTokenColor>(4);
            int clampedCount = Mathf.Clamp(count, 2, 4);
            for (int i = 0; i < clampedCount; i++)
            {
                colors.Add(s_DefaultSelectionOrder[i]);
            }

            return colors;
        }

        private static List<LudoTokenColor> GetActiveColorsFromSnapshot(LudoRoomSnapshot snapshot)
        {
            List<LudoTokenColor> colors = new List<LudoTokenColor>(4);
            if (snapshot != null && snapshot.ActiveColors != null && snapshot.ActiveColors.Length > 0)
            {
                for (int i = 0; i < snapshot.ActiveColors.Length; i++)
                {
                    LudoTokenColor color;
                    if (TryParseColor(snapshot.ActiveColors[i], out color) && !colors.Contains(color))
                    {
                        colors.Add(color);
                    }
                }
            }

            if (colors.Count == 0 && snapshot != null && snapshot.Seats != null)
            {
                for (int i = 0; i < snapshot.Seats.Length; i++)
                {
                    if (snapshot.Seats[i] == null)
                    {
                        continue;
                    }

                    LudoTokenColor color;
                    if (TryParseColor(snapshot.Seats[i].Color, out color) && !colors.Contains(color))
                    {
                        colors.Add(color);
                    }
                }
            }

            IReadOnlyList<LudoTokenColor> clockwise = LudoBoardGeometry.ClockwiseColors;
            colors.Sort((left, right) => GetClockwiseIndex(clockwise, left).CompareTo(GetClockwiseIndex(clockwise, right)));
            return colors;
        }

        private static int GetClockwiseIndex(IReadOnlyList<LudoTokenColor> colors, LudoTokenColor color)
        {
            if (colors == null)
            {
                return int.MaxValue;
            }

            for (int i = 0; i < colors.Count; i++)
            {
                if (colors[i] == color)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        private static LudoOnlineSeatState FindSeat(LudoRoomSnapshot snapshot, LudoTokenColor color)
        {
            if (snapshot == null || snapshot.Seats == null)
            {
                return null;
            }

            for (int i = 0; i < snapshot.Seats.Length; i++)
            {
                if (snapshot.Seats[i] == null)
                {
                    continue;
                }

                LudoTokenColor seatColor;
                if (TryParseColor(snapshot.Seats[i].Color, out seatColor) && seatColor == color)
                {
                    return snapshot.Seats[i];
                }
            }

            return null;
        }

        private string GetPlayerNameInput()
        {
            if (_playerNameField == null || string.IsNullOrWhiteSpace(_playerNameField.text))
            {
                return "Player";
            }

            return _playerNameField.text.Trim();
        }

        private static bool TryValidateEnteredName(string rawValue, out string playerName, out string error)
        {
            playerName = "Player";
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                error = "Enter your name before going online.";
                return false;
            }

            string trimmed = rawValue.Trim();
            if (trimmed.Length < 2)
            {
                error = "Your name should be at least 2 characters.";
                return false;
            }

            playerName = trimmed.Length > 18 ? trimmed.Substring(0, 18) : trimmed;
            return true;
        }

        private bool TryGetRequiredOnlinePlayerName(out string playerName, out string error)
        {
            string rawValue = _playerNameField != null ? _playerNameField.text : string.Empty;
            if (!TryValidateEnteredName(rawValue, out playerName, out error))
            {
                return false;
            }

            if (_playerNameField != null)
            {
                _playerNameField.text = playerName;
            }

            return true;
        }

        private static bool TryParseColor(string value, out LudoTokenColor color)
        {
            return Enum.TryParse(value, true, out color);
        }

        private static RectTransform CreateFloatingPanel(Transform parent, string name)
        {
            RectTransform panel = LudoUtility.CreateUIObject(name, parent);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(0.5f, 0.5f);
            Image panelFill = LudoUtility.GetOrAddComponent<Image>(panel.gameObject);
            LudoUtility.ApplySprite(panelFill, LudoSpriteFactory.RoundedMask);
            panelFill.color = AppPanelColor;
            panelFill.raycastTarget = true;
            Shadow panelDropShadow = LudoUtility.GetOrAddComponent<Shadow>(panel.gameObject);
            panelDropShadow.effectColor = AppPanelShadowColor;
            panelDropShadow.effectDistance = new Vector2(0f, -6f);
            panelDropShadow.useGraphicAlpha = true;

            Image panelGloss = LudoUtility.CreateImage("Gloss", panel, LudoSpriteFactory.RoundedGloss, AppPanelChromeColor);
            LudoUtility.Stretch(panelGloss.rectTransform);
            panelGloss.raycastTarget = false;
            SetIgnoreLayout(panelGloss.rectTransform);

            Image panelShadow = LudoUtility.CreateImage("InnerShadow", panel, LudoSpriteFactory.RoundedInnerShadow, AppPanelInnerShadowColor);
            LudoUtility.Stretch(panelShadow.rectTransform);
            panelShadow.raycastTarget = false;
            SetIgnoreLayout(panelShadow.rectTransform);

            VerticalLayoutGroup layoutGroup = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(panel.gameObject);
            layoutGroup.padding = new RectOffset(28, 28, 28, 28);
            layoutGroup.spacing = 16f;
            layoutGroup.childAlignment = TextAnchor.UpperCenter;
            layoutGroup.childControlHeight = true;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            return panel;
        }

        private static RectTransform CreateSetupSection(Transform parent, string title)
        {
            RectTransform section = LudoUtility.CreateUIObject(title.Replace(" ", string.Empty) + "Section", parent);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(section.gameObject);
            layout.minHeight = 92f;
            layout.flexibleHeight = 0f;
            Image fill = LudoUtility.GetOrAddComponent<Image>(section.gameObject);
            LudoUtility.ApplySprite(fill, LudoSpriteFactory.RoundedMask);
            fill.color = AppSectionFillColor;
            fill.raycastTarget = false;
            Shadow shadow = LudoUtility.GetOrAddComponent<Shadow>(section.gameObject);
            shadow.effectColor = AppSectionShadowColor;
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.useGraphicAlpha = true;

            VerticalLayoutGroup sectionLayout = LudoUtility.GetOrAddComponent<VerticalLayoutGroup>(section.gameObject);
            sectionLayout.padding = new RectOffset(18, 18, 16, 16);
            sectionLayout.spacing = 10f;
            sectionLayout.childAlignment = TextAnchor.UpperLeft;
            sectionLayout.childControlHeight = true;
            sectionLayout.childControlWidth = true;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.childForceExpandHeight = false;
            ContentSizeFitter sectionFitter = LudoUtility.GetOrAddComponent<ContentSizeFitter>(section.gameObject);
            sectionFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text titleText = LudoUtility.CreateText("Title", section, title, 18, FontStyle.Bold, TextAnchor.MiddleLeft, LudoTheme.TextPrimary);
            ConfigureSection(titleText.rectTransform, 24f);
            titleText.raycastTarget = false;
            return section;
        }

        private static RectTransform CreateHeaderRow(Transform parent)
        {
            RectTransform row = LudoUtility.CreateUIObject("HeaderRow", parent);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(row.gameObject);
            layout.preferredHeight = 48f;
            HorizontalLayoutGroup group = LudoUtility.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            group.spacing = 12f;
            group.childAlignment = TextAnchor.MiddleLeft;
            group.childControlWidth = false;
            group.childControlHeight = true;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            return row;
        }

        private static HorizontalLayoutGroup CreateButtonRow(Transform parent)
        {
            RectTransform row = LudoUtility.CreateUIObject("ButtonRow", parent);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(row.gameObject);
            layout.preferredHeight = 58f;
            HorizontalLayoutGroup group = LudoUtility.GetOrAddComponent<HorizontalLayoutGroup>(row.gameObject);
            group.spacing = 12f;
            group.childAlignment = TextAnchor.MiddleCenter;
            group.childControlWidth = false;
            group.childControlHeight = true;
            group.childForceExpandHeight = false;
            group.childForceExpandWidth = false;
            return group;
        }

        private static ButtonView CreateLargeActionButton(Transform parent, string title, string subtitle, Action onClick)
        {
            ButtonView buttonView = CreateButtonView(parent, title, onClick);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(buttonView.Root.gameObject);
            layout.preferredHeight = 92f;
            buttonView.Fill.color = AppPrimaryButtonFillColor;
            if (buttonView.Accent != null)
            {
                buttonView.Accent.color = new Color(1f, 1f, 1f, 0.06f);
            }

            RectTransform content = LudoUtility.CreateUIObject("Content", buttonView.Root);
            LudoUtility.Stretch(content, 22f, 22f, 16f, 16f);

            Text titleText = LudoUtility.CreateText("Title", content, title, 24, FontStyle.Bold, TextAnchor.UpperLeft, new Color(0.98f, 0.98f, 0.97f, 1f));
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            titleText.rectTransform.offsetMin = new Vector2(0f, 4f);
            titleText.rectTransform.offsetMax = new Vector2(0f, -20f);
            titleText.raycastTarget = false;

            Text subtitleText = LudoUtility.CreateText("Subtitle", content, subtitle, 15, FontStyle.Normal, TextAnchor.LowerLeft, AppPrimaryButtonSubtitleColor);
            subtitleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            subtitleText.rectTransform.anchorMax = new Vector2(1f, 0f);
            subtitleText.rectTransform.pivot = new Vector2(0.5f, 0f);
            subtitleText.rectTransform.sizeDelta = new Vector2(0f, 22f);
            subtitleText.raycastTarget = false;

            buttonView.Label.gameObject.SetActive(false);
            LudoButtonFeedback largeActionFeedback = buttonView.Root.GetComponent<LudoButtonFeedback>();
            if (largeActionFeedback != null)
            {
                largeActionFeedback.SyncBaseState();
            }
            return buttonView;
        }

        private static ButtonView CreateBottomActionButton(Transform parent, string label, Action onClick)
        {
            ButtonView buttonView = CreateButtonView(parent, label, onClick);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(buttonView.Root.gameObject);
            layout.preferredHeight = 64f;
            buttonView.Fill.color = AppCallToActionFillColor;
            if (buttonView.Label != null)
            {
                buttonView.Label.color = AppCallToActionTextColor;
            }
            if (buttonView.Accent != null)
            {
                buttonView.Accent.color = new Color(1f, 1f, 1f, 0.14f);
            }

            LudoButtonFeedback bottomActionFeedback = buttonView.Root.GetComponent<LudoButtonFeedback>();
            if (bottomActionFeedback != null)
            {
                bottomActionFeedback.SyncBaseState();
            }
            return buttonView;
        }

        private static ButtonView CreateInlineButton(Transform parent, string label, Action onClick)
        {
            ButtonView buttonView = CreateButtonView(parent, label, onClick);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(buttonView.Root.gameObject);
            layout.preferredHeight = 48f;
            layout.preferredWidth = 132f;
            return buttonView;
        }

        private static ButtonView CreateHeaderButton(Transform parent, string label, Action onClick)
        {
            ButtonView buttonView = CreateButtonView(parent, label, onClick);
            buttonView.Root.sizeDelta = new Vector2(116f, 48f);
            return buttonView;
        }

        private static ButtonView CreateSmallChoiceButton(Transform parent, string label, Action onClick, HorizontalLayoutGroup group)
        {
            ButtonView buttonView = CreateButtonView(parent, label, onClick);
            buttonView.Root.SetParent(group.transform, false);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(buttonView.Root.gameObject);
            layout.preferredWidth = 132f;
            layout.preferredHeight = 54f;
            return buttonView;
        }

        private static ColorChipView CreateColorChip(Transform parent, LudoTokenColor color, Action onClick)
        {
            ColorChipView chipView = new ColorChipView();
            chipView.ButtonView = CreateButtonView(parent, string.Empty, onClick);
            chipView.Fill = chipView.ButtonView.Fill;
            chipView.Root = chipView.ButtonView.Root;

            RectTransform content = LudoUtility.CreateUIObject("ChipContent", chipView.ButtonView.Root);
            LudoUtility.Stretch(content, 16f, 16f, 16f, 16f);

            chipView.Icon = LudoUtility.CreateImage("Icon", content, LudoArtLibrary.GetTokenSprite(color), Color.white);
            chipView.Icon.rectTransform.anchorMin = chipView.Icon.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            chipView.Icon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            chipView.Icon.rectTransform.sizeDelta = new Vector2(62f, 62f);
            chipView.Icon.rectTransform.anchoredPosition = new Vector2(0f, 16f);
            chipView.Icon.preserveAspect = true;
            chipView.Icon.useSpriteMesh = false;
            chipView.Icon.raycastTarget = false;

            chipView.Title = LudoUtility.CreateText("Title", content, LudoBoardGeometry.GetPlayerName(color), 20, FontStyle.Bold, TextAnchor.MiddleCenter, LudoTheme.TextPrimary);
            chipView.Title.rectTransform.anchorMin = chipView.Title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            chipView.Title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            chipView.Title.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            chipView.Title.rectTransform.sizeDelta = new Vector2(160f, 24f);
            chipView.Title.raycastTarget = false;

            chipView.Subtitle = LudoUtility.CreateText("Subtitle", content, "Tap to add", 16, FontStyle.Normal, TextAnchor.MiddleCenter, AppMutedTextColor);
            chipView.Subtitle.rectTransform.anchorMin = chipView.Subtitle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            chipView.Subtitle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            chipView.Subtitle.rectTransform.anchoredPosition = new Vector2(0f, -46f);
            chipView.Subtitle.rectTransform.sizeDelta = new Vector2(180f, 22f);
            chipView.Subtitle.raycastTarget = false;

            SetButtonSelected(chipView.ButtonView, false, LudoUtility.WithAlpha(LudoTheme.GetTokenTint(color), 0.18f));
            return chipView;
        }

        private static LobbySeatView CreateLobbySeat(Transform parent, LudoTokenColor color)
        {
            LobbySeatView seatView = new LobbySeatView();
            seatView.Root = LudoUtility.CreateUIObject(color + "Seat", parent);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(seatView.Root.gameObject);
            layout.preferredHeight = 72f;

            seatView.Fill = LudoUtility.GetOrAddComponent<Image>(seatView.Root.gameObject);
            LudoUtility.ApplySprite(seatView.Fill, LudoSpriteFactory.RoundedMask);
            seatView.Fill.color = Color.Lerp(AppPanelColor, LudoTheme.GetTokenTint(color), 0.16f);
            seatView.Fill.raycastTarget = false;
            Shadow seatShadow = LudoUtility.GetOrAddComponent<Shadow>(seatView.Root.gameObject);
            seatShadow.effectColor = AppSectionShadowColor;
            seatShadow.effectDistance = new Vector2(0f, -3f);
            seatShadow.useGraphicAlpha = true;

            Image gloss = LudoUtility.CreateImage("Gloss", seatView.Root, LudoSpriteFactory.RoundedGloss, new Color(1f, 1f, 1f, 0.12f));
            LudoUtility.Stretch(gloss.rectTransform);
            gloss.raycastTarget = false;

            seatView.Title = LudoUtility.CreateText("Title", seatView.Root, LudoBoardGeometry.GetPlayerName(color), 20, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.14f, 0.18f, 0.28f, 1f));
            seatView.Title.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            seatView.Title.rectTransform.anchorMax = new Vector2(0.45f, 0.5f);
            seatView.Title.rectTransform.pivot = new Vector2(0f, 0.5f);
            seatView.Title.rectTransform.anchoredPosition = new Vector2(18f, 10f);
            seatView.Title.rectTransform.sizeDelta = new Vector2(0f, 22f);
            seatView.Title.raycastTarget = false;

            seatView.Subtitle = LudoUtility.CreateText("Subtitle", seatView.Root, "Waiting...", 18, FontStyle.Normal, TextAnchor.MiddleRight, new Color(0.20f, 0.24f, 0.34f, 0.92f));
            seatView.Subtitle.rectTransform.anchorMin = new Vector2(0.48f, 0.5f);
            seatView.Subtitle.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            seatView.Subtitle.rectTransform.pivot = new Vector2(1f, 0.5f);
            seatView.Subtitle.rectTransform.anchoredPosition = new Vector2(-18f, 10f);
            seatView.Subtitle.rectTransform.sizeDelta = new Vector2(0f, 22f);
            seatView.Subtitle.raycastTarget = false;
            return seatView;
        }

        private static InputField CreateTextField(Transform parent, string placeholder)
        {
            RectTransform root = LudoUtility.CreateUIObject("InputField", parent);
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(root.gameObject);
            layout.preferredHeight = 56f;

            Image fill = LudoUtility.GetOrAddComponent<Image>(root.gameObject);
            LudoUtility.ApplySprite(fill, LudoSpriteFactory.RoundedMask);
            fill.color = AppPanelColor;
            fill.raycastTarget = true;
            Shadow shadow = LudoUtility.GetOrAddComponent<Shadow>(root.gameObject);
            shadow.effectColor = AppSectionShadowColor;
            shadow.effectDistance = new Vector2(0f, -3f);
            shadow.useGraphicAlpha = true;
            Image innerShadow = LudoUtility.CreateImage("InnerShadow", root, LudoSpriteFactory.RoundedInnerShadow, new Color(0.16f, 0.12f, 0.10f, 0.08f));
            LudoUtility.Stretch(innerShadow.rectTransform);
            innerShadow.raycastTarget = false;

            Text text = LudoUtility.CreateText("Text", root, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleLeft, LudoTheme.TextPrimary);
            LudoUtility.Stretch(text.rectTransform, 18f, 18f, 16f, 16f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;

            Text placeholderText = LudoUtility.CreateText("Placeholder", root, placeholder, 18, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.50f, 0.44f, 0.39f, 0.72f));
            LudoUtility.Stretch(placeholderText.rectTransform, 18f, 18f, 16f, 16f);
            placeholderText.raycastTarget = false;

            InputField inputField = LudoUtility.GetOrAddComponent<InputField>(root.gameObject);
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.targetGraphic = fill;
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.characterLimit = 24;
            return inputField;
        }

        private static ButtonView CreateButtonView(Transform parent, string label, Action onClick)
        {
            ButtonView view = new ButtonView();
            view.Root = LudoUtility.CreateUIObject(string.IsNullOrEmpty(label) ? "Button" : label.Replace(" ", string.Empty) + "Button", parent);
            view.Fill = LudoUtility.GetOrAddComponent<Image>(view.Root.gameObject);
            LudoUtility.ApplySprite(view.Fill, LudoSpriteFactory.RoundedMask);
            view.Fill.color = AppButtonFillColor;
            view.Fill.raycastTarget = true;
            Shadow buttonShadow = LudoUtility.GetOrAddComponent<Shadow>(view.Root.gameObject);
            buttonShadow.effectColor = AppButtonShadowColor;
            buttonShadow.effectDistance = new Vector2(0f, -2f);
            buttonShadow.useGraphicAlpha = true;

            Image innerShadow = LudoUtility.CreateImage("InnerShadow", view.Root, LudoSpriteFactory.RoundedInnerShadow, new Color(0.16f, 0.12f, 0.10f, 0.08f));
            LudoUtility.Stretch(innerShadow.rectTransform);
            innerShadow.raycastTarget = false;

            view.Accent = LudoUtility.CreateImage("Accent", view.Root, LudoSpriteFactory.RoundedGloss, new Color(1f, 1f, 1f, 0.05f));
            LudoUtility.Stretch(view.Accent.rectTransform, 2f, 2f, 2f, 2f);
            view.Accent.raycastTarget = false;

            view.Button = LudoUtility.GetOrAddComponent<Button>(view.Root.gameObject);
            view.Button.targetGraphic = view.Fill;
            ColorBlock colors = view.Button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.98f);
            colors.pressedColor = new Color(0.94f, 0.94f, 0.94f, 0.98f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.65f);
            colors.fadeDuration = 0.08f;
            view.Button.colors = colors;
            view.Button.onClick.AddListener(() =>
            {
                if (onClick != null)
                {
                    onClick();
                }
            });

            view.Label = LudoUtility.CreateText("Label", view.Root, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, AppButtonLabelColor);
            LudoUtility.Stretch(view.Label.rectTransform, 14f, 14f, 12f, 12f);
            view.Label.raycastTarget = false;

            LudoButtonFeedback feedback = LudoUtility.GetOrAddComponent<LudoButtonFeedback>(view.Root.gameObject);
            feedback.Configure(view.Accent, 1.012f, 0.985f, 0.10f, 0.16f);
            return view;
        }

        private static void SetButtonSelected(ButtonView view, bool selected, Color accent)
        {
            if (view == null)
            {
                return;
            }

            if (view.Fill != null)
            {
                Color selectedFill = Color.Lerp(AppSelectedFillColor, new Color(accent.r, accent.g, accent.b, 1f), 0.16f);
                view.Fill.color = selected ? selectedFill : AppUnselectedFillColor;
            }

            if (view.Label != null)
            {
                view.Label.color = selected ? LudoTheme.TextPrimary : AppButtonLabelColor;
            }

            if (view.Accent != null)
            {
                view.Accent.color = selected ? new Color(accent.r, accent.g, accent.b, 0.18f) : new Color(1f, 1f, 1f, 0.08f);
            }

            LudoButtonFeedback feedback = view.Root != null ? view.Root.GetComponent<LudoButtonFeedback>() : null;
            if (feedback != null)
            {
                feedback.SyncBaseState();
            }
        }

        private static void SetButtonInteractable(ButtonView view, bool interactable)
        {
            if (view == null)
            {
                return;
            }

            if (view.Button != null)
            {
                view.Button.interactable = interactable;
            }

            if (view.Root != null)
            {
                CanvasGroup canvasGroup = LudoUtility.GetOrAddComponent<CanvasGroup>(view.Root.gameObject);
                canvasGroup.alpha = interactable ? 1f : 0.58f;
                canvasGroup.interactable = interactable;
                canvasGroup.blocksRaycasts = interactable;
            }
        }

        private static void SetIgnoreLayout(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                return;
            }

            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(rectTransform.gameObject);
            layout.ignoreLayout = true;
        }

        private static void ConfigureSection(RectTransform rectTransform, float preferredHeight)
        {
            LayoutElement layout = LudoUtility.GetOrAddComponent<LayoutElement>(rectTransform.gameObject);
            layout.preferredHeight = preferredHeight;
        }

        private static void SetPanelSize(RectTransform panel, float width, float height)
        {
            if (panel == null)
            {
                return;
            }

            panel.sizeDelta = new Vector2(width, height);
        }
    }
}
