using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoGameController : MonoBehaviour
    {
        private const float TurnHandoffDelay = 0.84f;
        private const float NetworkTurnHandoffDelay = 0.06f;
        private const float HumanAutoMoveDelay = 0.18f;
        private const float OnlineHumanAutoMoveDelay = 0.04f;
        private const float AITokenChoiceDelay = 0.35f;
        private const float RemoteRollAnimationDuration = 0.22f;
        private const float TokenCompletionVolume = 0.95f;

        private static readonly LudoTokenColor[] s_AllColors =
        {
            LudoTokenColor.Red,
            LudoTokenColor.Green,
            LudoTokenColor.Yellow,
            LudoTokenColor.Blue,
        };

        private LudoAnimationController _animationController;
        private LudoBoardRenderer _boardRenderer;
        private RectTransform _tokenLayer;
        private RectTransform _effectsLayer;
        private RectTransform _uiLayer;
        private RectTransform _tokenViewport;
        private RectTransform _effectViewport;
        private RectTransform _playerLabelRoot;
        private RectTransform _statusRoot;
        private Text _statusText;
        private LudoDiceView _diceView;
        private AudioSource _audioSource;
        private AudioClip _tokenCompletionSound;
        private LudoAnimationHandle _statusPulseHandle;

        private readonly Dictionary<LudoTokenColor, List<LudoTokenState>> _tokenStates = new Dictionary<LudoTokenColor, List<LudoTokenState>>(4);
        private readonly Dictionary<LudoTokenColor, List<LudoTokenView>> _tokenViews = new Dictionary<LudoTokenColor, List<LudoTokenView>>(4);
        private readonly Dictionary<LudoTokenColor, Text> _playerLabels = new Dictionary<LudoTokenColor, Text>(4);
        private readonly Dictionary<LudoTokenColor, LudoAnimationHandle> _playerLabelPulseHandles = new Dictionary<LudoTokenColor, LudoAnimationHandle>(4);
        private readonly Dictionary<LudoTokenColor, LudoParticipantConfig> _participants = new Dictionary<LudoTokenColor, LudoParticipantConfig>(4);
        private readonly Dictionary<LudoTokenView, LudoTokenState> _tokenStateLookup = new Dictionary<LudoTokenView, LudoTokenState>(32);
        private readonly List<LudoTokenState> _currentSelectableTokens = new List<LudoTokenState>(LudoBoardGeometry.TokensPerColor);
        private readonly List<LudoTokenColor> _turnOrder = new List<LudoTokenColor>(4);

        private LudoSessionConfig _sessionConfig;
        private LudoTokenColor _currentTurnColor = LudoTokenColor.Red;
        private LudoTurnPhase _phase = LudoTurnPhase.Booting;
        private int _currentTurnIndex;
        private int _pendingRoll;
        private bool _bootstrapped;
        private bool _sessionActive;
        private Vector2 _lastUiSize;
        private int _lastBoardLayoutVersion = -1;

        public event Action<LudoTurnActionMessage> OnlineTurnActionCommitted;

        public bool SessionActive
        {
            get { return _sessionActive; }
        }

        public void Initialize(LudoAnimationController animationController, LudoBoardRenderer boardRenderer, RectTransform tokenLayer, RectTransform effectsLayer, RectTransform uiLayer)
        {
            if (_bootstrapped)
            {
                return;
            }

            _animationController = animationController;
            _boardRenderer = boardRenderer;
            _tokenLayer = tokenLayer;
            _effectsLayer = effectsLayer;
            _uiLayer = uiLayer;
            _tokenViewport = _tokenLayer;
            _effectViewport = _effectsLayer;

            BuildScene();
            SetPresentationVisible(false);
            _lastBoardLayoutVersion = _boardRenderer.LayoutVersion;
            _bootstrapped = true;
        }

        public void StartSession(LudoSessionConfig sessionConfig)
        {
            if (!_bootstrapped || sessionConfig == null || sessionConfig.Participants.Count < 2)
            {
                return;
            }

            if (_sessionActive)
            {
                EndSession();
            }

            _sessionConfig = sessionConfig.Clone();
            PopulateParticipants();
            InitializeSessionTokens();
            _sessionActive = true;
            RefreshPlayerLabels();
            ClearSelectableTokens();
            SetPresentationVisible(true);
            RefreshDiceBoardFrame();
            _currentTurnIndex = 0;
            _pendingRoll = 0;
            SetTurn(_turnOrder[0], BuildTurnPrompt(_turnOrder[0]));
        }

        public void EndSession()
        {
            _sessionActive = false;
            _sessionConfig = null;
            _participants.Clear();
            _turnOrder.Clear();
            _phase = LudoTurnPhase.Booting;
            _pendingRoll = 0;
            ClearSelectableTokens();
            ClearTurnIndicatorAnimations();
            HideAllTokenViews();
            SetPresentationVisible(false);
        }

        public void ApplyRemoteTurnAction(LudoTurnActionMessage action)
        {
            if (!_sessionActive || _sessionConfig == null || !_sessionConfig.UsesNetwork || action == null || _diceView == null)
            {
                return;
            }

            LudoTokenColor actionColor;
            if (!TryParseColor(action.Color, out actionColor))
            {
                return;
            }

            LudoParticipantConfig participant = GetParticipant(actionColor);
            if (participant == null || participant.Control != LudoParticipantControl.Remote || _currentTurnColor != actionColor)
            {
                return;
            }

            _phase = LudoTurnPhase.Resolving;
            _diceView.SetDockForTokenColor(actionColor, true);
            _diceView.SetInteractable(false);
            _diceView.PlayRollAnimation(action.Roll, RemoteRollAnimationDuration, () =>
            {
                if (action.NoMove || action.TokenIndex < 0)
                {
                    SetStatus(GetDisplayName(actionColor) + " rolled " + action.Roll + " and could not move.");
                    AdvanceTurn(false, false, false);
                    return;
                }

                HandleRollResult(actionColor, action.Roll, true, action.TokenIndex);
            }, null);
        }

        public void ApplyOnlineSnapshot(LudoRoomSnapshot snapshot, bool immediate)
        {
            if (!_sessionActive || _sessionConfig == null || !_sessionConfig.UsesNetwork || snapshot == null)
            {
                return;
            }

            if (snapshot.TokenStates != null)
            {
                for (int tokenStateIndex = 0; tokenStateIndex < snapshot.TokenStates.Length; tokenStateIndex++)
                {
                    LudoTokenProgressState progressState = snapshot.TokenStates[tokenStateIndex];
                    if (progressState == null)
                    {
                        continue;
                    }

                    LudoTokenColor color;
                    if (!TryParseColor(progressState.Color, out color))
                    {
                        continue;
                    }

                    List<LudoTokenState> tokenStates = GetOrCreateTokenStates(color);
                    for (int tokenIndex = 0; tokenIndex < tokenStates.Count; tokenIndex++)
                    {
                        tokenStates[tokenIndex].Progress = progressState.Progress != null && tokenIndex < progressState.Progress.Length
                            ? progressState.Progress[tokenIndex]
                            : -1;
                    }
                }
            }

            if (snapshot.Seats != null)
            {
                for (int seatIndex = 0; seatIndex < snapshot.Seats.Length; seatIndex++)
                {
                    LudoOnlineSeatState seat = snapshot.Seats[seatIndex];
                    if (seat == null)
                    {
                        continue;
                    }

                    LudoTokenColor color;
                    if (!TryParseColor(seat.Color, out color))
                    {
                        continue;
                    }

                    LudoParticipantConfig participant = GetParticipant(color);
                    if (participant != null && !string.IsNullOrWhiteSpace(seat.DisplayName))
                    {
                        participant.DisplayName = seat.DisplayName;
                    }
                }
            }

            RefreshPlayerLabels();
            SyncAllTokens(immediate);

            LudoTokenColor winnerColor;
            if (TryParseColor(snapshot.WinnerColor, out winnerColor) && _participants.ContainsKey(winnerColor))
            {
                HandleGameOver(winnerColor, GetFirstTokenView(winnerColor));
                return;
            }

            LudoTokenColor nextTurnColor;
            if (TryParseColor(snapshot.CurrentTurnColor, out nextTurnColor) && _turnOrder.Contains(nextTurnColor))
            {
                _currentTurnIndex = _turnOrder.IndexOf(nextTurnColor);
                SetTurn(nextTurnColor, BuildTurnPrompt(nextTurnColor));
            }
        }

        private void Update()
        {
            if (!_bootstrapped || _uiLayer == null || _boardRenderer == null)
            {
                return;
            }

            Vector2 uiSize = _uiLayer.rect.size;
            if ((uiSize - _lastUiSize).sqrMagnitude > 0.5f)
            {
                _lastUiSize = uiSize;
                RefreshStatusLayout();
                UpdateBoardViewportPadding();
                RefreshPlayerLabels();
            }

            if (_boardRenderer.LayoutVersion != _lastBoardLayoutVersion)
            {
                _lastBoardLayoutVersion = _boardRenderer.LayoutVersion;
                if (_sessionActive)
                {
                    SyncAllTokens(true);
                }

                RefreshDiceBoardFrame();
                RefreshPlayerLabels();
            }
        }

        private void BuildScene()
        {
            BuildAudio();
            BuildDice();
            BuildStatus();
            BuildPlayerLabels();
            BuildAllTokenViews();
            UpdateBoardViewportPadding();
            RefreshDiceBoardFrame();
            RefreshPlayerLabels();
        }

        private void BuildAudio()
        {
            _audioSource = LudoUtility.GetOrAddComponent<AudioSource>(gameObject);
            if (_audioSource != null)
            {
                _audioSource.playOnAwake = false;
                _audioSource.loop = false;
                _audioSource.spatialBlend = 0f;
                _audioSource.volume = 1f;
            }

            _tokenCompletionSound = Resources.Load<AudioClip>("token_completing");
        }

        private void BuildDice()
        {
            GameObject diceObject = new GameObject("DiceView", typeof(RectTransform));
            _diceView = diceObject.AddComponent<LudoDiceView>();
            _diceView.Build(_uiLayer, _animationController, OnDicePressed);
            _diceView.SetDockForTokenColor(LudoTokenColor.Red, true);
        }

        private void BuildStatus()
        {
            _statusRoot = LudoUtility.CreateUIObject("StatusRoot", _uiLayer);
            _statusRoot.anchorMin = _statusRoot.anchorMax = _statusRoot.pivot = new Vector2(0.5f, 1f);

            _statusText = LudoUtility.CreateText("Status", _statusRoot, string.Empty, 26, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.97f, 0.98f, 1f, 0.96f));
            _statusText.rectTransform.anchorMin = _statusText.rectTransform.anchorMax = _statusText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _statusText.raycastTarget = false;
            Outline outline = LudoUtility.GetOrAddComponent<Outline>(_statusText.gameObject);
            outline.effectColor = new Color(0.04f, 0.11f, 0.28f, 0.72f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            RefreshStatusLayout();
        }

        private void BuildPlayerLabels()
        {
            if (_tokenViewport == null || _playerLabelRoot != null)
            {
                return;
            }

            _playerLabelRoot = LudoUtility.CreateUIObject("PlayerLabels", _tokenViewport);
            LudoUtility.Stretch(_playerLabelRoot);
            _playerLabelRoot.SetSiblingIndex(0);

            CreatePlayerLabel(LudoTokenColor.Green, 180f);
            CreatePlayerLabel(LudoTokenColor.Yellow, -90f);
            CreatePlayerLabel(LudoTokenColor.Blue, 0f);
            CreatePlayerLabel(LudoTokenColor.Red, 90f);
        }

        private void CreatePlayerLabel(LudoTokenColor color, float rotationZ)
        {
            Text label = LudoUtility.CreateText("PlayerLabel" + color, _playerLabelRoot, LudoBoardGeometry.GetDefaultPlayerLabel(color), 24, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.92f, 0.95f, 1f, 0.86f));
            label.raycastTarget = false;
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.localEulerAngles = new Vector3(0f, 0f, rotationZ);
            Outline outline = LudoUtility.GetOrAddComponent<Outline>(label.gameObject);
            outline.effectColor = new Color(0.05f, 0.12f, 0.34f, 0.56f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
            Shadow shadow = LudoUtility.GetOrAddComponent<Shadow>(label.gameObject);
            shadow.effectColor = new Color(0f, 0f, 0f, 0.16f);
            shadow.effectDistance = new Vector2(0f, -2f);
            shadow.useGraphicAlpha = true;
            _playerLabels[color] = label;
        }

        private void BuildAllTokenViews()
        {
            for (int i = 0; i < s_AllColors.Length; i++)
            {
                LudoTokenColor color = s_AllColors[i];
                List<LudoTokenView> tokenViews = GetOrCreateTokenViews(color);
                float tokenSize = Mathf.Max(48f, _boardRenderer != null ? _boardRenderer.HomeTokenSize : 96f);

                for (int tokenIndex = 0; tokenIndex < LudoBoardGeometry.TokensPerColor; tokenIndex++)
                {
                    GameObject tokenObject = new GameObject(color + "Token" + (tokenIndex + 1), typeof(RectTransform));
                    LudoTokenView tokenView = tokenObject.AddComponent<LudoTokenView>();
                    tokenView.Build(_tokenViewport, _animationController, LudoArtLibrary.GetTokenSprite(color), Color.white, tokenSize);
                    tokenView.Clicked += OnTokenClicked;
                    tokenView.SetVisible(false);
                    tokenViews.Add(tokenView);
                }
            }
        }

        private void PopulateParticipants()
        {
            _participants.Clear();
            _turnOrder.Clear();

            for (int i = 0; i < _sessionConfig.Participants.Count; i++)
            {
                LudoParticipantConfig participant = _sessionConfig.Participants[i];
                if (participant == null)
                {
                    continue;
                }

                _participants[participant.Color] = participant;
            }

            _turnOrder.AddRange(_sessionConfig.BuildTurnOrder());
            if (_turnOrder.Count == 0)
            {
                _turnOrder.Add(LudoTokenColor.Red);
            }
        }

        private void InitializeSessionTokens()
        {
            _tokenStateLookup.Clear();
            for (int i = 0; i < s_AllColors.Length; i++)
            {
                LudoTokenColor color = s_AllColors[i];
                bool isActive = _participants.ContainsKey(color);
                List<LudoTokenState> tokenStates = GetOrCreateTokenStates(color);
                List<LudoTokenView> tokenViews = GetOrCreateTokenViews(color);
                tokenStates.Clear();

                for (int tokenIndex = 0; tokenIndex < tokenViews.Count; tokenIndex++)
                {
                    LudoTokenView tokenView = tokenViews[tokenIndex];
                    if (tokenView == null)
                    {
                        continue;
                    }

                    tokenView.SetVisible(isActive);
                    tokenView.SetSelectable(false);
                    if (!isActive)
                    {
                        continue;
                    }

                    LudoTokenState tokenState = new LudoTokenState
                    {
                        Owner = color,
                        TokenIndex = tokenIndex,
                        Progress = -1,
                    };
                    tokenStates.Add(tokenState);
                    _tokenStateLookup[tokenView] = tokenState;
                }
            }

            SyncAllTokens(true);
        }

        private void SetTurn(LudoTokenColor color, string status)
        {
            _currentTurnColor = color;
            _pendingRoll = 0;
            ClearSelectableTokens();
            SetStatus(status);

            if (_diceView != null)
            {
                _diceView.SetDockForTokenColor(color, true);
            }

            LudoParticipantConfig participant = GetParticipant(color);
            if (participant == null)
            {
                return;
            }

            switch (participant.Control)
            {
                case LudoParticipantControl.AI:
                    _phase = LudoTurnPhase.AwaitingAI;
                    if (_diceView != null)
                    {
                        _diceView.SetInteractable(false);
                    }
                    ScheduleAITurn();
                    break;
                case LudoParticipantControl.Remote:
                    _phase = LudoTurnPhase.AwaitingRemote;
                    if (_diceView != null)
                    {
                        _diceView.SetInteractable(false);
                    }
                    SetStatus("Waiting for " + GetDisplayName(color) + "...");
                    break;
                default:
                    _phase = LudoTurnPhase.AwaitingHumanRoll;
                    if (_diceView != null)
                    {
                        _diceView.SetInteractable(true);
                    }
                    break;
            }

            RefreshTurnIndicators();
        }

        private void ScheduleAITurn()
        {
            if (_animationController == null || _phase == LudoTurnPhase.GameOver)
            {
                return;
            }

            float delay = UnityEngine.Random.Range(0.8f, 1.2f);
            _animationController.Delay(delay, ExecuteAITurn);
        }

        private void ExecuteAITurn()
        {
            if (!_sessionActive || _phase == LudoTurnPhase.GameOver)
            {
                return;
            }

            int roll = RollDiceAI(_currentTurnColor);
            _phase = LudoTurnPhase.Resolving;
            SetStatus(GetDisplayName(_currentTurnColor) + " is rolling...");

            if (_diceView != null)
            {
                _diceView.SetInteractable(false);
                _diceView.PlayRollAnimation(roll, UnityEngine.Random.Range(0.55f, 0.90f), () => HandleRollResult(_currentTurnColor, roll, false, -1), null);
            }
            else
            {
                HandleRollResult(_currentTurnColor, roll, false, -1);
            }
        }

        private void OnDicePressed()
        {
            if (!_sessionActive || _phase != LudoTurnPhase.AwaitingHumanRoll)
            {
                return;
            }

            LudoParticipantConfig participant = GetParticipant(_currentTurnColor);
            if (participant == null || participant.Control != LudoParticipantControl.HumanLocal)
            {
                return;
            }

            int roll = UnityEngine.Random.Range(1, 7);
            _phase = LudoTurnPhase.Resolving;
            if (_diceView != null)
            {
                _diceView.SetInteractable(false);
                _diceView.PlayRollAnimation(roll, UnityEngine.Random.Range(0.55f, 0.85f), () => HandleRollResult(_currentTurnColor, roll, false, -1), null);
            }
            else
            {
                HandleRollResult(_currentTurnColor, roll, false, -1);
            }
        }

        private void HandleRollResult(LudoTokenColor color, int roll, bool fromRemote, int forcedTokenIndex)
        {
            if (!_sessionActive)
            {
                return;
            }

            _pendingRoll = roll;
            List<LudoTokenState> movableTokens = GetMovableTokens(color, roll);
            if (movableTokens.Count == 0)
            {
                SetStatus(GetDisplayName(color) + " cannot move " + roll + ".");
                if (!fromRemote && ShouldBroadcastTurnAction(color))
                {
                    EmitTurnAction(color, roll, -1, true);
                }

                AdvanceTurn(false, false, false);
                return;
            }

            if (fromRemote)
            {
                LudoTokenState remoteToken = FindTokenState(color, forcedTokenIndex);
                if (remoteToken == null || !movableTokens.Contains(remoteToken))
                {
                    remoteToken = movableTokens[0];
                }

                ExecuteMove(remoteToken, roll, false);
                return;
            }

            LudoParticipantConfig participant = GetParticipant(color);
            if (participant == null)
            {
                return;
            }

            if (participant.Control == LudoParticipantControl.AI)
            {
                LudoTokenState aiToken = ChooseAIToken(color, roll, movableTokens);
                SetStatus(GetDisplayName(color) + " rolled " + roll + ".");
                _animationController.Delay(AITokenChoiceDelay, () => ExecuteMove(aiToken, roll, false));
                return;
            }

            _phase = LudoTurnPhase.AwaitingHumanMove;
            if (movableTokens.Count == 1)
            {
                LudoTokenState tokenState = movableTokens[0];
                SetStatus(GetDisplayName(color) + " rolled " + roll + ". Moving the only available token.");
                float autoMoveDelay = ShouldBroadcastTurnAction(color) ? OnlineHumanAutoMoveDelay : HumanAutoMoveDelay;
                _animationController.Delay(autoMoveDelay, () =>
                {
                    if (_phase != LudoTurnPhase.AwaitingHumanMove)
                    {
                        return;
                    }

                    ClearSelectableTokens();
                    ExecuteMove(tokenState, roll, ShouldBroadcastTurnAction(color));
                });
                return;
            }

            SetSelectableTokens(movableTokens);
            SetStatus(GetDisplayName(color) + " rolled " + roll + ". Choose a token.");
        }

        private void OnTokenClicked(LudoTokenView tokenView)
        {
            if (!_sessionActive || _phase != LudoTurnPhase.AwaitingHumanMove || tokenView == null)
            {
                return;
            }

            LudoTokenState tokenState;
            if (!_tokenStateLookup.TryGetValue(tokenView, out tokenState) || tokenState == null || tokenState.Owner != _currentTurnColor)
            {
                return;
            }

            if (!_currentSelectableTokens.Contains(tokenState))
            {
                return;
            }

            ClearSelectableTokens();
            ExecuteMove(tokenState, _pendingRoll, ShouldBroadcastTurnAction(_currentTurnColor));
        }

        private void ExecuteMove(LudoTokenState tokenState, int roll, bool broadcastTurnAction)
        {
            if (!_sessionActive || tokenState == null || _phase == LudoTurnPhase.GameOver)
            {
                return;
            }

            LudoTokenView tokenView = GetTokenView(tokenState);
            if (tokenView == null)
            {
                return;
            }

            _phase = LudoTurnPhase.Resolving;
            tokenView.Resize(_boardRenderer.TokenSize);

            List<Vector2> stepPositions = BuildStepPath(tokenState, roll, out int finalProgress);
            tokenState.Progress = finalProgress;

            if (broadcastTurnAction)
            {
                EmitTurnAction(tokenState.Owner, roll, tokenState.TokenIndex, false);
            }

            SetMovementFocus(tokenState, true);
            tokenView.PlayStepMovement(stepPositions, () =>
            {
                if (tokenState.HasFinished)
                {
                    PlayTokenCompletionSound();
                }

                bool playerFinished = HasAllTokensFinished(tokenState.Owner);
                if (playerFinished)
                {
                    SetMovementFocus(null, false);
                    HandleGameOver(tokenState.Owner, tokenView);
                    return;
                }

                ResolveCapture(tokenState, captured =>
                {
                    SetMovementFocus(null, false);
                    if (tokenState.HasFinished)
                    {
                        SetStatus(GetDisplayName(tokenState.Owner) + " brought a token home.");
                    }
                    else
                    {
                        SetStatus(captured
                            ? GetDisplayName(tokenState.Owner) + " captured a token."
                            : GetDisplayName(tokenState.Owner) + " moved " + roll + (roll == 1 ? " step." : " steps."));
                    }

                    AdvanceTurn(roll == 6, captured, true);
                });
            });
        }

        private void PlayTokenCompletionSound()
        {
            if (_audioSource == null)
            {
                _audioSource = LudoUtility.GetOrAddComponent<AudioSource>(gameObject);
                if (_audioSource != null)
                {
                    _audioSource.playOnAwake = false;
                    _audioSource.loop = false;
                    _audioSource.spatialBlend = 0f;
                    _audioSource.volume = 1f;
                }
            }

            if (_tokenCompletionSound == null)
            {
                _tokenCompletionSound = Resources.Load<AudioClip>("token_completing");
            }

            if (_audioSource != null && _tokenCompletionSound != null)
            {
                _audioSource.PlayOneShot(_tokenCompletionSound, TokenCompletionVolume);
            }
        }

        private void AdvanceTurn(bool rolledSix, bool captured, bool moveWasPlayed)
        {
            if (_phase == LudoTurnPhase.GameOver || _turnOrder.Count == 0)
            {
                return;
            }

            bool extraTurn = rolledSix || captured;
            if (extraTurn)
            {
                SetTurn(_currentTurnColor, GetDisplayName(_currentTurnColor) + " earned a bonus turn.");
                return;
            }

            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            LudoTokenColor nextTurn = _turnOrder[_currentTurnIndex];
            string status = BuildTurnPrompt(nextTurn);
            float handoffDelay = 0f;
            if (!moveWasPlayed)
            {
                handoffDelay = _sessionConfig != null && _sessionConfig.UsesNetwork
                    ? NetworkTurnHandoffDelay
                    : TurnHandoffDelay;
            }

            if (_animationController != null && handoffDelay > 0f)
            {
                _animationController.Delay(handoffDelay, () =>
                {
                    if (_phase == LudoTurnPhase.GameOver)
                    {
                        return;
                    }

                    SetTurn(nextTurn, status);
                });
            }
            else
            {
                SetTurn(nextTurn, status);
            }
        }

        private void HandleGameOver(LudoTokenColor winner, LudoTokenView winnerView)
        {
            _phase = LudoTurnPhase.GameOver;
            if (_diceView != null)
            {
                _diceView.SetInteractable(false);
            }

            SetStatus(GetDisplayName(winner) + " wins the game.");
            RefreshTurnIndicators();
            if (winnerView != null)
            {
                winnerView.PlayLandingHighlight();
            }
        }

        private void ResolveCapture(LudoTokenState attacker, Action<bool> onResolved)
        {
            if (attacker == null || !attacker.IsOnCommonPath)
            {
                if (onResolved != null)
                {
                    onResolved(false);
                }

                return;
            }

            LudoTokenState defender = FindCapturedDefender(attacker);
            if (defender == null)
            {
                if (onResolved != null)
                {
                    onResolved(false);
                }

                return;
            }

            List<Vector2> returnPath = BuildCaptureReturnPath(defender);
            defender.Progress = -1;
            LudoTokenView defenderView = GetTokenView(defender);
            if (defenderView != null)
            {
                defenderView.PlayCaptureReaction(() =>
                {
                    defenderView.PlayReturnHomeMovement(returnPath, () =>
                    {
                        SyncTokenView(defender, defenderView, true);
                        if (_animationController != null)
                        {
                            _animationController.Delay(0.10f, () =>
                            {
                                if (onResolved != null)
                                {
                                    onResolved(true);
                                }
                            });
                        }
                        else if (onResolved != null)
                        {
                            onResolved(true);
                        }
                    });
                });
            }
            else if (onResolved != null)
            {
                onResolved(true);
            }

            LudoTokenView attackerView = GetTokenView(attacker);
            if (attackerView != null)
            {
                attackerView.PlayCaptureBurst();
            }
        }

        private List<Vector2> BuildStepPath(LudoTokenState tokenState, int roll, out int finalProgress)
        {
            List<Vector2> steps = new List<Vector2>(Mathf.Max(1, roll));
            if (tokenState == null)
            {
                finalProgress = 0;
                return steps;
            }

            if (tokenState.IsHome)
            {
                finalProgress = 0;
                steps.Add(_boardRenderer.GetCellCenterLocal(LudoBoardGeometry.GetRouteCoordinate(tokenState.Owner, 0)));
                return steps;
            }

            finalProgress = tokenState.Progress + roll;
            for (int progress = tokenState.Progress + 1; progress <= finalProgress; progress++)
            {
                steps.Add(_boardRenderer.GetCellCenterLocal(LudoBoardGeometry.GetRouteCoordinate(tokenState.Owner, progress)));
            }

            return steps;
        }

        private List<Vector2> BuildCaptureReturnPath(LudoTokenState tokenState)
        {
            List<Vector2> steps = new List<Vector2>();
            if (tokenState == null || _boardRenderer == null)
            {
                return steps;
            }

            int startProgress = Mathf.Clamp(tokenState.Progress, 0, LudoBoardGeometry.CommonPathLength - 1);
            for (int progress = startProgress - 1; progress >= 0; progress--)
            {
                steps.Add(_boardRenderer.GetCellCenterLocal(LudoBoardGeometry.GetRouteCoordinate(tokenState.Owner, progress)));
            }

            steps.Add(_boardRenderer.GetBoardPoint(LudoBoardGeometry.GetHomeCircleCoordinate(tokenState.Owner, tokenState.TokenIndex)));
            return steps;
        }

        private bool CanMove(LudoTokenState tokenState, int roll)
        {
            if (tokenState == null || tokenState.HasFinished)
            {
                return false;
            }

            if (tokenState.IsHome)
            {
                return roll == 6;
            }

            return tokenState.Progress + roll <= LudoBoardGeometry.FinalProgress;
        }

        private List<LudoTokenState> GetMovableTokens(LudoTokenColor color, int roll)
        {
            List<LudoTokenState> movableTokens = new List<LudoTokenState>(LudoBoardGeometry.TokensPerColor);
            List<LudoTokenState> tokenStates;
            if (!_tokenStates.TryGetValue(color, out tokenStates))
            {
                return movableTokens;
            }

            for (int i = 0; i < tokenStates.Count; i++)
            {
                if (CanMove(tokenStates[i], roll))
                {
                    movableTokens.Add(tokenStates[i]);
                }
            }

            return movableTokens;
        }

        private int RollDiceAI(LudoTokenColor color)
        {
            List<int> captureRolls = GetCaptureRolls(color);
            bool ignoreBias = UnityEngine.Random.value < 0.125f;
            int[] weights = new int[6];

            for (int i = 0; i < weights.Length; i++)
            {
                int roll = i + 1;
                weights[i] = 1;

                if (!ignoreBias && captureRolls.Contains(roll))
                {
                    weights[i] = 3;
                }

                if (HasHomeToken(color) && roll == 6)
                {
                    weights[i] += 1;
                }

                if (GetMovableTokens(color, roll).Count == 0)
                {
                    weights[i] = Mathf.Max(1, weights[i] - 1);
                }
            }

            int totalWeight = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                totalWeight += weights[i];
            }

            int random = UnityEngine.Random.Range(0, totalWeight);
            int cumulative = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                cumulative += weights[i];
                if (random < cumulative)
                {
                    return i + 1;
                }
            }

            return UnityEngine.Random.Range(1, 7);
        }

        private List<int> GetCaptureRolls(LudoTokenColor color)
        {
            List<int> captureRolls = new List<int>(2);
            for (int roll = 1; roll <= 6; roll++)
            {
                List<LudoTokenState> movableTokens = GetMovableTokens(color, roll);
                for (int i = 0; i < movableTokens.Count; i++)
                {
                    LudoTokenState ignoredDefender;
                    if (WouldCapture(movableTokens[i], roll, out ignoredDefender))
                    {
                        captureRolls.Add(roll);
                        break;
                    }
                }
            }

            return captureRolls;
        }

        private LudoTokenState ChooseAIToken(LudoTokenColor color, int roll, List<LudoTokenState> movableTokens)
        {
            if (movableTokens == null || movableTokens.Count == 0)
            {
                return null;
            }

            LudoTokenState bestToken = movableTokens[0];
            float bestScore = float.MinValue;
            for (int i = 0; i < movableTokens.Count; i++)
            {
                float score = ScoreMove(movableTokens[i], roll) + UnityEngine.Random.Range(0f, 0.15f);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestToken = movableTokens[i];
                }
            }

            return bestToken;
        }

        private float ScoreMove(LudoTokenState tokenState, int roll)
        {
            if (tokenState == null)
            {
                return float.MinValue;
            }

            float score = tokenState.IsHome ? 0f : tokenState.Progress;
            LudoTokenState defender;
            if (WouldCapture(tokenState, roll, out defender))
            {
                score += 100f;
            }

            int landingProgress = tokenState.IsHome ? 0 : tokenState.Progress + roll;
            Vector2Int landingCoordinate = LudoBoardGeometry.GetRouteCoordinate(tokenState.Owner, landingProgress);
            if (LudoBoardGeometry.IsSafeCell(landingCoordinate))
            {
                score += 35f;
            }

            if (tokenState.IsHome && roll == 6)
            {
                score += 25f;
            }

            score += landingProgress * 0.5f;
            return score;
        }

        private bool WouldCapture(LudoTokenState tokenState, int roll, out LudoTokenState defender)
        {
            defender = null;
            if (!CanMove(tokenState, roll))
            {
                return false;
            }

            int landingProgress = tokenState.IsHome ? 0 : tokenState.Progress + roll;
            if (landingProgress >= LudoBoardGeometry.CommonPathLength)
            {
                return false;
            }

            Vector2Int landingCoordinate = LudoBoardGeometry.GetRouteCoordinate(tokenState.Owner, landingProgress);
            if (LudoBoardGeometry.IsSafeCell(landingCoordinate))
            {
                return false;
            }

            for (int i = 0; i < _turnOrder.Count; i++)
            {
                LudoTokenColor otherColor = _turnOrder[i];
                if (otherColor == tokenState.Owner)
                {
                    continue;
                }

                List<LudoTokenState> opponentTokens;
                if (!_tokenStates.TryGetValue(otherColor, out opponentTokens))
                {
                    continue;
                }

                for (int tokenIndex = 0; tokenIndex < opponentTokens.Count; tokenIndex++)
                {
                    if (!opponentTokens[tokenIndex].IsOnCommonPath)
                    {
                        continue;
                    }

                    if (opponentTokens[tokenIndex].GetBoardCoordinate() == landingCoordinate)
                    {
                        defender = opponentTokens[tokenIndex];
                        return true;
                    }
                }
            }

            return false;
        }

        private LudoTokenState FindCapturedDefender(LudoTokenState attacker)
        {
            if (attacker == null || !attacker.IsOnCommonPath)
            {
                return null;
            }

            Vector2Int attackerCoordinate = attacker.GetBoardCoordinate();
            if (LudoBoardGeometry.IsSafeCell(attackerCoordinate))
            {
                return null;
            }

            for (int i = 0; i < _turnOrder.Count; i++)
            {
                LudoTokenColor otherColor = _turnOrder[i];
                if (otherColor == attacker.Owner)
                {
                    continue;
                }

                List<LudoTokenState> opponentTokens;
                if (!_tokenStates.TryGetValue(otherColor, out opponentTokens))
                {
                    continue;
                }

                for (int tokenIndex = 0; tokenIndex < opponentTokens.Count; tokenIndex++)
                {
                    if (!opponentTokens[tokenIndex].IsOnCommonPath)
                    {
                        continue;
                    }

                    if (opponentTokens[tokenIndex].GetBoardCoordinate() == attackerCoordinate)
                    {
                        return opponentTokens[tokenIndex];
                    }
                }
            }

            return null;
        }

        private void SyncAllTokens(bool immediate)
        {
            for (int i = 0; i < s_AllColors.Length; i++)
            {
                SyncTokensForColor(s_AllColors[i], immediate);
            }
        }

        private void SyncTokensForColor(LudoTokenColor color, bool immediate)
        {
            List<LudoTokenState> tokenStates;
            List<LudoTokenView> tokenViews;
            if (!_tokenStates.TryGetValue(color, out tokenStates) || !_tokenViews.TryGetValue(color, out tokenViews))
            {
                return;
            }

            int count = Mathf.Min(tokenStates.Count, tokenViews.Count);
            for (int i = 0; i < count; i++)
            {
                SyncTokenView(tokenStates[i], tokenViews[i], immediate);
            }
        }

        private void SyncTokenView(LudoTokenState tokenState, LudoTokenView tokenView, bool immediate)
        {
            if (tokenState == null || tokenView == null || _boardRenderer == null)
            {
                return;
            }

            tokenView.SetVisible(true);
            tokenView.Resize(tokenState.IsHome ? _boardRenderer.HomeTokenSize : _boardRenderer.TokenSize);

            Vector2 position;
            if (tokenState.HasFinished)
            {
                position = _boardRenderer.GetCellCenterLocal(LudoBoardGeometry.GoalCoordinate);
            }
            else if (tokenState.IsHome)
            {
                position = _boardRenderer.GetBoardPoint(LudoBoardGeometry.GetHomeCircleCoordinate(tokenState.Owner, tokenState.TokenIndex));
            }
            else
            {
                position = _boardRenderer.GetCellCenterLocal(tokenState.GetBoardCoordinate());
            }

            tokenView.Position = position;
            tokenView.TargetPosition = position;
            tokenView.ResetPresentation();

            if (!immediate)
            {
                tokenView.PlayLandingHighlight();
            }
        }

        private void SetSelectableTokens(List<LudoTokenState> selectableTokens)
        {
            ClearSelectableTokens();
            if (selectableTokens == null)
            {
                return;
            }

            for (int i = 0; i < selectableTokens.Count; i++)
            {
                LudoTokenView tokenView = GetTokenView(selectableTokens[i]);
                if (tokenView != null)
                {
                    tokenView.SetSelectable(true);
                    _currentSelectableTokens.Add(selectableTokens[i]);
                }
            }
        }

        private void ClearSelectableTokens()
        {
            for (int colorIndex = 0; colorIndex < s_AllColors.Length; colorIndex++)
            {
                List<LudoTokenView> tokenViews;
                if (!_tokenViews.TryGetValue(s_AllColors[colorIndex], out tokenViews))
                {
                    continue;
                }

                for (int i = 0; i < tokenViews.Count; i++)
                {
                    if (tokenViews[i] != null && tokenViews[i].gameObject.activeSelf)
                    {
                        tokenViews[i].SetSelectable(false);
                    }
                }
            }

            _currentSelectableTokens.Clear();
        }

        private void SetMovementFocus(LudoTokenState activeToken, bool isFocused)
        {
            for (int colorIndex = 0; colorIndex < _turnOrder.Count; colorIndex++)
            {
                LudoTokenColor color = _turnOrder[colorIndex];
                List<LudoTokenState> states;
                List<LudoTokenView> views;
                if (!_tokenStates.TryGetValue(color, out states) || !_tokenViews.TryGetValue(color, out views))
                {
                    continue;
                }

                int count = Mathf.Min(states.Count, views.Count);
                for (int i = 0; i < count; i++)
                {
                    if (views[i] == null)
                    {
                        continue;
                    }

                    float targetScale = 1f;
                    float duration = isFocused ? 0.12f : 0.16f;
                    if (isFocused && activeToken != null)
                    {
                        targetScale = states[i] == activeToken ? 1.06f : 0.95f;
                    }

                    views[i].SetFocusScale(targetScale, duration);
                }
            }
        }

        private void UpdateBoardViewportPadding()
        {
            if (_boardRenderer == null || _uiLayer == null)
            {
                return;
            }

            float topReserved = 0f;
            float bottomReserved = 0f;
            float leftReserved = Mathf.Max(12f, _uiLayer.rect.width * 0.03f);
            float rightReserved = leftReserved;
            if (_diceView != null)
            {
                topReserved = Mathf.Max(topReserved, _diceView.ReservedTopPadding + 12f);
                bottomReserved = Mathf.Max(bottomReserved, _diceView.ReservedBottomPadding + 12f);
                leftReserved = Mathf.Max(leftReserved, _diceView.ReservedLeftPadding + 12f);
                rightReserved = Mathf.Max(rightReserved, _diceView.ReservedRightPadding + 12f);
            }

            _boardRenderer.SetViewportPadding(topReserved, bottomReserved, leftReserved, rightReserved);
        }

        private void RefreshDiceBoardFrame()
        {
            if (_diceView == null || _boardRenderer == null)
            {
                return;
            }

            _diceView.SetBoardFrame(_boardRenderer.BoardRoot);
        }

        private void RefreshPlayerLabels()
        {
            if (_playerLabelRoot == null || _boardRenderer == null || _boardRenderer.BoardRoot == null)
            {
                return;
            }

            RectTransform boardRoot = _boardRenderer.BoardRoot;
            Vector2 boardSize = boardRoot.rect.size;
            float cellSize = Mathf.Min(boardSize.x, boardSize.y) / LudoBoardGeometry.BoardSize;
            Vector2 horizontalLabelSize = new Vector2(cellSize * 3.55f, cellSize * 0.72f);
            Vector2 verticalLabelSize = new Vector2(cellSize * 3.85f, cellSize * 0.72f);

            PositionPlayerLabel(LudoTokenColor.Green, _boardRenderer.GetBoardPoint(11.45f, 13.82f), horizontalLabelSize);
            PositionPlayerLabel(LudoTokenColor.Yellow, _boardRenderer.GetBoardPoint(13.88f, 2.15f), verticalLabelSize);
            PositionPlayerLabel(LudoTokenColor.Blue, _boardRenderer.GetBoardPoint(2.55f, 0.18f), horizontalLabelSize);
            PositionPlayerLabel(LudoTokenColor.Red, _boardRenderer.GetBoardPoint(0.18f, 11.18f), verticalLabelSize);

            for (int i = 0; i < s_AllColors.Length; i++)
            {
                Text label;
                if (!_playerLabels.TryGetValue(s_AllColors[i], out label) || label == null)
                {
                    continue;
                }

                bool isActive = _participants.ContainsKey(s_AllColors[i]);
                label.gameObject.SetActive(_sessionActive && isActive);
                if (!isActive)
                {
                    continue;
                }

                label.text = GetDisplayName(s_AllColors[i]);
                label.fontSize = Mathf.RoundToInt(Mathf.Clamp(cellSize * 0.42f, 14f, 20f));
            }

            RefreshTurnIndicators();
        }

        private void PositionPlayerLabel(LudoTokenColor color, Vector2 anchoredPosition, Vector2 size)
        {
            Text label;
            if (!_playerLabels.TryGetValue(color, out label) || label == null)
            {
                return;
            }

            RectTransform rectTransform = label.rectTransform;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;
        }

        private void RefreshStatusLayout()
        {
            if (_statusRoot == null || _statusText == null || _uiLayer == null)
            {
                return;
            }

            _statusRoot.anchoredPosition = new Vector2(0f, -Mathf.Max(24f, _uiLayer.rect.height * 0.028f));
            _statusRoot.sizeDelta = new Vector2(Mathf.Clamp(_uiLayer.rect.width * 0.70f, 280f, 760f), 44f);
            _statusText.rectTransform.sizeDelta = _statusRoot.sizeDelta;
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
            {
                _statusText.text = message ?? string.Empty;
            }

            if (_animationController != null && _statusRoot != null)
            {
                if (_statusPulseHandle != null)
                {
                    _statusPulseHandle.Cancel();
                }

                _statusRoot.localScale = Vector3.one;
                _statusPulseHandle = _animationController.SchedulePulse(_statusRoot, Vector3.one, _animationController.TimeNow, 0.28f, 0.02f, 1);
            }
        }

        private void RefreshTurnIndicators()
        {
            for (int i = 0; i < s_AllColors.Length; i++)
            {
                LudoTokenColor color = s_AllColors[i];
                Text label;
                if (!_playerLabels.TryGetValue(color, out label) || label == null)
                {
                    continue;
                }

                LudoAnimationHandle handle;
                if (_playerLabelPulseHandles.TryGetValue(color, out handle) && handle != null)
                {
                    handle.Cancel();
                    _playerLabelPulseHandles[color] = null;
                }

                bool isActive = _sessionActive && _participants.ContainsKey(color);
                bool isCurrentTurn = isActive && _currentTurnColor == color && _phase != LudoTurnPhase.Booting && _phase != LudoTurnPhase.GameOver;

                label.rectTransform.localScale = isCurrentTurn ? new Vector3(1.04f, 1.04f, 1f) : Vector3.one;
                label.color = isCurrentTurn
                    ? new Color(1f, 0.98f, 0.90f, 0.98f)
                    : new Color(0.92f, 0.95f, 1f, isActive ? 0.86f : 0.42f);

                Outline outline = label.GetComponent<Outline>();
                if (outline != null)
                {
                    outline.effectColor = isCurrentTurn
                        ? LudoUtility.WithAlpha(LudoTheme.GetTokenTint(color), 0.58f)
                        : new Color(0.05f, 0.12f, 0.34f, 0.56f);
                }

                Shadow shadow = label.GetComponent<Shadow>();
                if (shadow != null)
                {
                    shadow.effectColor = isCurrentTurn
                        ? new Color(0f, 0f, 0f, 0.24f)
                        : new Color(0f, 0f, 0f, 0.16f);
                }

                if (isCurrentTurn && _animationController != null)
                {
                    _playerLabelPulseHandles[color] = _animationController.ScheduleLoopPulse(label.rectTransform, label.rectTransform.localScale, 0.028f, 1.2f);
                }
            }
        }

        private void ClearTurnIndicatorAnimations()
        {
            if (_statusPulseHandle != null)
            {
                _statusPulseHandle.Cancel();
                _statusPulseHandle = null;
            }

            for (int i = 0; i < s_AllColors.Length; i++)
            {
                LudoAnimationHandle handle;
                if (_playerLabelPulseHandles.TryGetValue(s_AllColors[i], out handle) && handle != null)
                {
                    handle.Cancel();
                    _playerLabelPulseHandles[s_AllColors[i]] = null;
                }

                Text label;
                if (_playerLabels.TryGetValue(s_AllColors[i], out label) && label != null)
                {
                    label.rectTransform.localScale = Vector3.one;
                }
            }
        }

        private void SetPresentationVisible(bool visible)
        {
            if (_boardRenderer != null)
            {
                _boardRenderer.SetBoardVisible(visible);
            }

            if (_tokenLayer != null)
            {
                _tokenLayer.gameObject.SetActive(visible);
            }

            if (_effectsLayer != null)
            {
                _effectsLayer.gameObject.SetActive(visible);
            }

            if (_diceView != null)
            {
                _diceView.gameObject.SetActive(visible);
            }

            if (_statusRoot != null)
            {
                _statusRoot.gameObject.SetActive(visible);
            }

            if (_playerLabelRoot != null)
            {
                _playerLabelRoot.gameObject.SetActive(visible);
            }
        }

        private void HideAllTokenViews()
        {
            for (int colorIndex = 0; colorIndex < s_AllColors.Length; colorIndex++)
            {
                List<LudoTokenView> tokenViews;
                if (!_tokenViews.TryGetValue(s_AllColors[colorIndex], out tokenViews))
                {
                    continue;
                }

                for (int i = 0; i < tokenViews.Count; i++)
                {
                    if (tokenViews[i] != null)
                    {
                        tokenViews[i].SetVisible(false);
                    }
                }
            }
        }

        private bool HasAllTokensFinished(LudoTokenColor color)
        {
            List<LudoTokenState> tokenStates;
            if (!_tokenStates.TryGetValue(color, out tokenStates))
            {
                return false;
            }

            for (int i = 0; i < tokenStates.Count; i++)
            {
                if (!tokenStates[i].HasFinished)
                {
                    return false;
                }
            }

            return tokenStates.Count > 0;
        }

        private bool HasHomeToken(LudoTokenColor color)
        {
            List<LudoTokenState> tokenStates;
            if (!_tokenStates.TryGetValue(color, out tokenStates))
            {
                return false;
            }

            for (int i = 0; i < tokenStates.Count; i++)
            {
                if (tokenStates[i].IsHome)
                {
                    return true;
                }
            }

            return false;
        }

        private LudoTokenView GetTokenView(LudoTokenState tokenState)
        {
            if (tokenState == null)
            {
                return null;
            }

            List<LudoTokenView> tokenViews;
            if (!_tokenViews.TryGetValue(tokenState.Owner, out tokenViews))
            {
                return null;
            }

            if (tokenState.TokenIndex < 0 || tokenState.TokenIndex >= tokenViews.Count)
            {
                return null;
            }

            return tokenViews[tokenState.TokenIndex];
        }

        private LudoTokenView GetFirstTokenView(LudoTokenColor color)
        {
            List<LudoTokenView> tokenViews;
            if (!_tokenViews.TryGetValue(color, out tokenViews) || tokenViews == null || tokenViews.Count == 0)
            {
                return null;
            }

            return tokenViews[0];
        }

        private LudoTokenState FindTokenState(LudoTokenColor color, int tokenIndex)
        {
            List<LudoTokenState> tokenStates;
            if (!_tokenStates.TryGetValue(color, out tokenStates))
            {
                return null;
            }

            if (tokenIndex < 0 || tokenIndex >= tokenStates.Count)
            {
                return null;
            }

            return tokenStates[tokenIndex];
        }

        private LudoParticipantConfig GetParticipant(LudoTokenColor color)
        {
            LudoParticipantConfig participant;
            return _participants.TryGetValue(color, out participant) ? participant : null;
        }

        private string GetDisplayName(LudoTokenColor color)
        {
            LudoParticipantConfig participant = GetParticipant(color);
            if (participant != null && !string.IsNullOrEmpty(participant.DisplayName))
            {
                return participant.DisplayName;
            }

            return LudoBoardGeometry.GetDefaultPlayerLabel(color);
        }

        private string BuildTurnPrompt(LudoTokenColor color)
        {
            LudoParticipantConfig participant = GetParticipant(color);
            if (participant == null)
            {
                return string.Empty;
            }

            switch (participant.Control)
            {
                case LudoParticipantControl.AI:
                    return GetDisplayName(color) + " is thinking...";
                case LudoParticipantControl.Remote:
                    return "Waiting for " + GetDisplayName(color) + "...";
                default:
                    return GetDisplayName(color) + ", roll the dice.";
            }
        }

        private bool ShouldBroadcastTurnAction(LudoTokenColor color)
        {
            if (_sessionConfig == null || !_sessionConfig.UsesNetwork)
            {
                return false;
            }

            LudoParticipantConfig participant = GetParticipant(color);
            return participant != null && participant.Control == LudoParticipantControl.HumanLocal && participant.IsLocal;
        }

        private void EmitTurnAction(LudoTokenColor color, int roll, int tokenIndex, bool noMove)
        {
            Action<LudoTurnActionMessage> handler = OnlineTurnActionCommitted;
            if (handler == null)
            {
                return;
            }

            handler(new LudoTurnActionMessage
            {
                Color = color.ToString(),
                Roll = roll,
                TokenIndex = tokenIndex,
                NoMove = noMove,
            });
        }

        private List<LudoTokenState> GetOrCreateTokenStates(LudoTokenColor color)
        {
            List<LudoTokenState> tokenStates;
            if (!_tokenStates.TryGetValue(color, out tokenStates))
            {
                tokenStates = new List<LudoTokenState>(LudoBoardGeometry.TokensPerColor);
                _tokenStates[color] = tokenStates;
            }

            return tokenStates;
        }

        private List<LudoTokenView> GetOrCreateTokenViews(LudoTokenColor color)
        {
            List<LudoTokenView> tokenViews;
            if (!_tokenViews.TryGetValue(color, out tokenViews))
            {
                tokenViews = new List<LudoTokenView>(LudoBoardGeometry.TokensPerColor);
                _tokenViews[color] = tokenViews;
            }

            return tokenViews;
        }

        private static bool TryParseColor(string value, out LudoTokenColor color)
        {
            return Enum.TryParse(value, true, out color);
        }
    }
}
