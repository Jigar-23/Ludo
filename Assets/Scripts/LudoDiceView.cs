using System;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoDiceView : MonoBehaviour
    {
        private enum DiceDock
        {
            Top = 0,
            Right = 1,
            Bottom = 2,
            Left = 3,
        }

        private const float SoundVolume = 0.9f;
        private static readonly Color PipColor = new Color(0.14f, 0.14f, 0.15f, 1f);
        private static readonly Color PipRollColor = new Color(0.14f, 0.14f, 0.15f, 0.42f);
        private static readonly Color PipGlossColor = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color PipRollGlossColor = new Color(1f, 1f, 1f, 0.13f);
        private static readonly Color DieShadowColor = new Color(0.16f, 0.11f, 0.08f, 0.14f);
        private static readonly Color SlotShadowColor = new Color(0.14f, 0.10f, 0.08f, 0.08f);
        private static readonly Color SlotFillColor = new Color(0.99f, 0.975f, 0.948f, 0.88f);
        private static readonly Color SlotActiveFillColor = new Color(1f, 0.988f, 0.968f, 0.96f);
        private static readonly Color SlotBorderColor = new Color(0.87f, 0.73f, 0.56f, 0.74f);
        private static readonly Color SlotFaceColor = new Color(1f, 0.995f, 0.982f, 0.98f);
        private static readonly Color SlotFaceIdleColor = new Color(0.985f, 0.972f, 0.952f, 0.92f);
        private static readonly Vector2[] PipPositions =
        {
            new Vector2(0f, 0f),
            new Vector2(-1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-1f, -1f),
            new Vector2(1f, -1f),
        };

        private RectTransform _uiLayer;
        private RectTransform _rectTransform;
        private readonly RectTransform[] _slotRects = new RectTransform[4];
        private readonly Image[] _slotFillImages = new Image[4];
        private readonly Image[] _slotAccentImages = new Image[4];
        private readonly RectTransform[] _slotFaceDockRects = new RectTransform[4];
        private readonly Image[] _slotFaceDockImages = new Image[4];
        private readonly RectTransform[] _slotTokenBadgeRects = new RectTransform[4];
        private readonly Image[] _slotTokenBadgeImages = new Image[4];
        private readonly Image[] _slotTokenIconImages = new Image[4];
        private readonly Vector2[] _slotCenters = new Vector2[4];
        private readonly Vector2[] _slotFaceCenters = new Vector2[4];
        private RectTransform _shadowRect;
        private RectTransform _buttonVisual;
        private RectTransform _faceRoot;
        private Image _readyGlow;
        private Button _button;
        private AudioSource _audioSource;
        private AudioClip _diceSound;
        private LudoAnimationController _animationController;
        private LudoAnimationHandle _readyGlowHandle;
        private LudoAnimationHandle _readyScaleHandle;
        private Action _rollRequested;
        private Vector2 _lastParentSize;
        private Vector2 _rootBasePosition;
        private Vector2 _buttonBasePosition;
        private Vector2 _shadowBasePosition;
        private Vector2 _readyGlowBasePosition;
        private readonly Image[] _pipImages = new Image[7];
        private readonly Image[] _pipGlossImages = new Image[7];
        private DiceDock _currentDock = DiceDock.Top;
        private int _currentFaceValue = 1;
        private bool _isRolling;
        private bool _hasBoardFrame;
        private float _reservedTopPadding;
        private float _reservedBottomPadding;
        private float _reservedLeftPadding;
        private float _reservedRightPadding;
        private Vector2 _boardCenter;
        private Vector2 _boardSize;

        public float ReservedHeight
        {
            get { return _reservedBottomPadding; }
        }

        public float ReservedTopPadding
        {
            get { return _reservedTopPadding; }
        }

        public float ReservedBottomPadding
        {
            get { return _reservedBottomPadding; }
        }

        public float ReservedLeftPadding
        {
            get { return _reservedLeftPadding; }
        }

        public float ReservedRightPadding
        {
            get { return _reservedRightPadding; }
        }

        public void Build(RectTransform uiLayer, LudoAnimationController animationController, Action rollRequested)
        {
            _uiLayer = uiLayer;
            _animationController = animationController;
            _rollRequested = rollRequested;
            BuildIfNeeded();
            RefreshLayout(true);
            SetFaceValue(_currentFaceValue);
            SetDockForTokenColor(LudoTokenColor.Red, true);
            RefreshReadyState(false);
        }

        public void SetInteractable(bool interactable)
        {
            if (_button != null)
            {
                _button.interactable = interactable;
            }

            if (!_isRolling)
            {
                ResetToBasePose();
            }

            RefreshReadyState(interactable);
        }

        public void SetDockForTokenColor(LudoTokenColor tokenColor, bool immediate)
        {
            SetDock(GetDockForTokenColor(tokenColor), immediate);
        }

        public void SetBoardFrame(RectTransform boardRoot)
        {
            if (boardRoot == null)
            {
                _hasBoardFrame = false;
                _boardCenter = Vector2.zero;
                _boardSize = Vector2.zero;
                RefreshLayout(false);
                return;
            }

            _hasBoardFrame = true;
            _boardCenter = boardRoot.anchoredPosition;
            _boardSize = boardRoot.rect.size;
            RefreshLayout(false);
        }

        public void SetCaption(string caption)
        {
        }

        public void SetValueText(string content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            int value;
            if (int.TryParse(content, out value))
            {
                SetFaceValue(value);
                return;
            }

            if (string.Equals(content, "?", StringComparison.Ordinal))
            {
                SetFaceValue(UnityEngine.Random.Range(1, 7));
            }
        }

        public void PlayRollAnimation(int result, float revealDelay, Action onRevealed, Action onComplete)
        {
            PlayDiceSound();
            RefreshLayout(false);
            _isRolling = true;

            if (_animationController == null || _rectTransform == null || _buttonVisual == null || _faceRoot == null)
            {
                SetFaceValue(result);
                ResetToBasePose();
                if (onRevealed != null)
                {
                    onRevealed();
                }

                if (onComplete != null)
                {
                    onComplete();
                }

                return;
            }

            CancelReadyAnimation();
            if (_readyGlow != null)
            {
                _readyGlow.rectTransform.localScale = Vector3.one;
                _readyGlow.color = new Color(1f, 0.88f, 0.56f, 0.14f);
            }

            _buttonVisual.localScale = Vector3.one;
            _buttonVisual.localEulerAngles = Vector3.zero;
            _rectTransform.anchoredPosition = _rootBasePosition;
            _buttonVisual.anchoredPosition = _buttonBasePosition;
            _shadowRect.anchoredPosition = _shadowBasePosition;
            if (_readyGlow != null)
            {
                _readyGlow.rectTransform.anchoredPosition = _readyGlowBasePosition;
            }
            _faceRoot.localScale = Vector3.one;

            float start = _animationController.TimeNow;
            float spinStart = start + 0.04f;
            float revealTime = start + revealDelay;
            float rollingEnd = revealTime;
            float rotationDuration = Mathf.Max(0.22f, rollingEnd - spinStart);
            float segmentA = rotationDuration * 0.26f;
            float segmentB = rotationDuration * 0.26f;
            float segmentC = rotationDuration * 0.24f;
            float segmentD = Mathf.Max(0.06f, rotationDuration - segmentA - segmentB - segmentC);

            SetRollingFaceVisual();

            _animationController.ScheduleScale(_buttonVisual, Vector3.one, new Vector3(0.92f, 0.92f, 1f), start, 0.06f, LudoEase.EaseOut);
            _animationController.ScheduleScale(_buttonVisual, new Vector3(0.92f, 0.92f, 1f), new Vector3(1.06f, 1.06f, 1f), start + 0.06f, 0.10f, LudoEase.EaseOutBack);
            _animationController.ScheduleScale(_buttonVisual, new Vector3(1.06f, 1.06f, 1f), Vector3.one, start + 0.16f, 0.12f, LudoEase.EaseOutBack);

            _animationController.ScheduleRotationZ(_buttonVisual, 0f, 220f, spinStart, segmentA, LudoEase.Linear);
            _animationController.ScheduleRotationZ(_buttonVisual, 220f, 430f, spinStart + segmentA, segmentB, LudoEase.Linear);
            _animationController.ScheduleRotationZ(_buttonVisual, 430f, 600f, spinStart + segmentA + segmentB, segmentC, LudoEase.EaseOutQuad);
            _animationController.ScheduleRotationZ(_buttonVisual, 600f, 720f, spinStart + segmentA + segmentB + segmentC, segmentD, LudoEase.EaseOut);

            _animationController.ScheduleShake(_buttonVisual, spinStart, rotationDuration, 7f, 15);
            _animationController.ScheduleScale(_buttonVisual, Vector3.one, new Vector3(1.03f, 1.03f, 1f), Mathf.Max(start + 0.12f, revealTime - 0.08f), 0.06f, LudoEase.EaseOut);

            if (_readyGlow != null)
            {
                Color activeGlow = new Color(1f, 0.86f, 0.52f, 0.26f);
                _animationController.ScheduleGraphicColor(_readyGlow, _readyGlow.color, activeGlow, start, 0.10f, LudoEase.EaseOut);
            }

            _animationController.Delay(revealDelay, () =>
            {
                _isRolling = false;
                float revealMoment = _animationController.TimeNow;
                SetFaceValue(result);
                if (_readyGlow != null)
                {
                    Color burstGlow = new Color(1f, 0.93f, 0.72f, 0.42f);
                    _animationController.ScheduleGraphicColor(_readyGlow, _readyGlow.color, burstGlow, revealMoment, 0.06f, LudoEase.EaseOut);
                    _animationController.ScheduleGraphicColor(_readyGlow, burstGlow, new Color(1f, 0.88f, 0.56f, 0.16f), revealMoment + 0.06f, 0.20f, LudoEase.EaseOut);
                    _animationController.SchedulePulse(_readyGlow.rectTransform, Vector3.one, revealMoment, 0.34f, 0.055f, 1);
                }

                _animationController.ScheduleScale(_buttonVisual, _buttonVisual.localScale, new Vector3(1.02f, 1.02f, 1f), revealMoment, 0.06f, LudoEase.EaseOut);
                _animationController.ScheduleScale(_buttonVisual, new Vector3(1.02f, 1.02f, 1f), Vector3.one, revealMoment + 0.06f, 0.12f, LudoEase.EaseOut);
                _animationController.ScheduleScale(_faceRoot, Vector3.one, new Vector3(1.04f, 1.04f, 1f), revealMoment, 0.08f, LudoEase.EaseOut);
                _animationController.ScheduleScale(_faceRoot, new Vector3(1.04f, 1.04f, 1f), Vector3.one, revealMoment + 0.08f, 0.12f, LudoEase.EaseOut);

                if (onRevealed != null)
                {
                    onRevealed();
                }
            });

            _animationController.Delay(revealDelay + 0.28f, ResetToBasePose);
            _animationController.Delay(revealDelay + 0.30f, onComplete);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_uiLayer == null || _rectTransform == null)
            {
                return;
            }

            Vector2 currentSize = _uiLayer.rect.size;
            if ((currentSize - _lastParentSize).sqrMagnitude > 0.5f)
            {
                RefreshLayout(false);
            }
        }

        private void BuildIfNeeded()
        {
            if (_rectTransform != null)
            {
                return;
            }

            _rectTransform = gameObject.GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }

            _rectTransform.SetParent(_uiLayer, false);
            LudoUtility.Stretch(_rectTransform);

            _audioSource = LudoUtility.GetOrAddComponent<AudioSource>(gameObject);
            if (_audioSource != null)
            {
                _audioSource.enabled = true;
                _audioSource.playOnAwake = false;
                _audioSource.loop = false;
                _audioSource.mute = false;
                _audioSource.ignoreListenerPause = true;
                _audioSource.spatialBlend = 0f;
                _audioSource.volume = SoundVolume;
            }

            _diceSound = Resources.Load<AudioClip>("dice_sound");

            if (_diceSound != null && _diceSound.loadState == AudioDataLoadState.Unloaded)
            {
                _diceSound.LoadAudioData();
            }

            BuildSlots();

            _readyGlow = LudoUtility.CreateImage("ReadyGlow", _rectTransform, LudoSpriteFactory.SoftDisc, new Color(1f, 0.88f, 0.56f, 0.16f));
            _readyGlow.rectTransform.anchorMin = _readyGlow.rectTransform.anchorMax = _readyGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            _readyGlow.raycastTarget = false;

            _shadowRect = LudoUtility.CreateUIObject("Shadow", _rectTransform);
            _shadowRect.anchorMin = _shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            _shadowRect.pivot = new Vector2(0.5f, 0.5f);
            Image shadow = LudoUtility.GetOrAddComponent<Image>(_shadowRect.gameObject);
            LudoUtility.ApplySprite(shadow, LudoSpriteFactory.RoundedMask);
            shadow.color = DieShadowColor;
            shadow.raycastTarget = false;

            _buttonVisual = LudoUtility.CreateUIObject("ButtonVisual", _rectTransform);
            _buttonVisual.anchorMin = _buttonVisual.anchorMax = new Vector2(0.5f, 0.5f);
            _buttonVisual.pivot = new Vector2(0.5f, 0.5f);

            Image fill = LudoUtility.GetOrAddComponent<Image>(_buttonVisual.gameObject);
            LudoUtility.ApplySprite(fill, LudoSpriteFactory.RoundedMask);
            fill.color = new Color(0.99f, 0.99f, 0.98f, 1f);
            fill.raycastTarget = true;

            Image innerShadow = LudoUtility.CreateImage("InnerShadow", _buttonVisual, LudoSpriteFactory.RoundedInnerShadow, new Color(0.14f, 0.10f, 0.08f, 0.16f));
            LudoUtility.Stretch(innerShadow.rectTransform);
            innerShadow.raycastTarget = false;

            Image gloss = LudoUtility.CreateImage("Gloss", _buttonVisual, LudoSpriteFactory.RoundedGloss, new Color(1f, 1f, 1f, 0.24f));
            LudoUtility.Stretch(gloss.rectTransform);
            gloss.raycastTarget = false;

            _faceRoot = LudoUtility.CreateUIObject("FaceRoot", _buttonVisual);
            LudoUtility.Stretch(_faceRoot, 18f, 18f, 18f, 18f);

            _button = LudoUtility.GetOrAddComponent<Button>(_buttonVisual.gameObject);
            ColorBlock colors = _button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.98f, 0.98f, 0.98f, 1f);
            colors.pressedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.86f, 0.86f, 0.86f, 0.78f);
            _button.colors = colors;
            _button.onClick.AddListener(OnRollPressed);

            BuildPips();
        }

        private void BuildSlots()
        {
            CreateSlot(DiceDock.Top, "TopSlot");
            CreateSlot(DiceDock.Right, "RightSlot");
            CreateSlot(DiceDock.Bottom, "BottomSlot");
            CreateSlot(DiceDock.Left, "LeftSlot");
        }

        private void CreateSlot(DiceDock dock, string name)
        {
            int index = (int)dock;
            RectTransform slotRect = LudoUtility.CreateUIObject(name, _rectTransform);
            slotRect.anchorMin = slotRect.anchorMax = slotRect.pivot = new Vector2(0.5f, 0.5f);
            _slotRects[index] = slotRect;

            Image shadow = LudoUtility.CreateImage("Shadow", slotRect, LudoSpriteFactory.RoundedMask, SlotShadowColor);
            LudoUtility.Stretch(shadow.rectTransform, -6f, -6f, -8f, -2f);
            shadow.raycastTarget = false;

            Image fill = LudoUtility.CreateImage("Fill", slotRect, LudoSpriteFactory.RoundedMask, SlotFillColor);
            LudoUtility.Stretch(fill.rectTransform);
            fill.raycastTarget = false;
            _slotFillImages[index] = fill;

            Image border = LudoUtility.CreateImage("Border", slotRect, LudoSpriteFactory.RoundedMask, SlotBorderColor);
            LudoUtility.Stretch(border.rectTransform, 2.5f, 2.5f, 2.5f, 2.5f);
            border.raycastTarget = false;

            Image panel = LudoUtility.CreateImage("Panel", slotRect, LudoSpriteFactory.RoundedMask, new Color(0.988f, 0.979f, 0.962f, 0.97f));
            LudoUtility.Stretch(panel.rectTransform, 5.5f, 5.5f, 5.5f, 5.5f);
            panel.raycastTarget = false;

            Image innerShadow = LudoUtility.CreateImage("InnerShadow", slotRect, LudoSpriteFactory.RoundedInnerShadow, new Color(0.14f, 0.10f, 0.08f, 0.10f));
            LudoUtility.Stretch(innerShadow.rectTransform);
            innerShadow.raycastTarget = false;

            Image accent = LudoUtility.CreateImage("Accent", slotRect, LudoSpriteFactory.RoundedGloss, LudoUtility.WithAlpha(GetDockAccentColor(dock), 0.10f));
            LudoUtility.Stretch(accent.rectTransform, 2f, 2f, 2f, 2f);
            accent.raycastTarget = false;
            _slotAccentImages[index] = accent;

            RectTransform faceDock = LudoUtility.CreateUIObject("FaceDock", slotRect);
            faceDock.anchorMin = faceDock.anchorMax = faceDock.pivot = new Vector2(0.5f, 0.5f);
            _slotFaceDockRects[index] = faceDock;

            Image faceFill = LudoUtility.CreateImage("Fill", faceDock, LudoSpriteFactory.RoundedMask, SlotFaceIdleColor);
            LudoUtility.Stretch(faceFill.rectTransform);
            faceFill.raycastTarget = false;
            _slotFaceDockImages[index] = faceFill;

            Image faceShadow = LudoUtility.CreateImage("InnerShadow", faceDock, LudoSpriteFactory.RoundedInnerShadow, new Color(0.20f, 0.12f, 0.10f, 0.12f));
            LudoUtility.Stretch(faceShadow.rectTransform);
            faceShadow.raycastTarget = false;

            RectTransform tokenBadge = LudoUtility.CreateUIObject("TokenBadge", slotRect);
            tokenBadge.anchorMin = tokenBadge.anchorMax = tokenBadge.pivot = new Vector2(0.5f, 0.5f);
            _slotTokenBadgeRects[index] = tokenBadge;

            Image badgeGlow = LudoUtility.CreateImage("Glow", tokenBadge, LudoSpriteFactory.SoftDisc, LudoUtility.WithAlpha(GetDockAccentColor(dock), 0.22f));
            LudoUtility.Stretch(badgeGlow.rectTransform, -4f, -4f, -4f, -4f);
            badgeGlow.raycastTarget = false;

            Image badgeFill = LudoUtility.CreateImage("Fill", tokenBadge, LudoSpriteFactory.CircleGradient, Color.Lerp(GetDockAccentColor(dock), Color.white, 0.08f));
            LudoUtility.Stretch(badgeFill.rectTransform, 4f, 4f, 4f, 4f);
            badgeFill.raycastTarget = false;
            _slotTokenBadgeImages[index] = badgeFill;

            Image badgeRim = LudoUtility.CreateImage("Rim", tokenBadge, LudoSpriteFactory.CircleInnerShadow, new Color(0.10f, 0.08f, 0.06f, 0.20f));
            LudoUtility.Stretch(badgeRim.rectTransform, 4f, 4f, 4f, 4f);
            badgeRim.raycastTarget = false;

            Image tokenIcon = LudoUtility.CreateImage("TokenIcon", tokenBadge, LudoArtLibrary.GetTokenSprite(GetDockTokenColor(dock)), Color.white);
            tokenIcon.preserveAspect = true;
            tokenIcon.useSpriteMesh = false;
            tokenIcon.raycastTarget = false;
            _slotTokenIconImages[index] = tokenIcon;
        }

        private void BuildPips()
        {
            for (int i = 0; i < _pipImages.Length; i++)
            {
                Image pip = LudoUtility.CreateImage("Pip" + i, _faceRoot, LudoSpriteFactory.CircleGradient, PipColor);
                pip.rectTransform.anchorMin = pip.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                pip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                pip.raycastTarget = false;
                _pipImages[i] = pip;

                Image pipGloss = LudoUtility.CreateImage("Gloss", pip.rectTransform, LudoSpriteFactory.CircleGloss, new Color(1f, 1f, 1f, 0.18f));
                LudoUtility.Stretch(pipGloss.rectTransform);
                pipGloss.raycastTarget = false;
                _pipGlossImages[i] = pipGloss;
            }
        }

        private void RefreshLayout(bool force)
        {
            if (_uiLayer == null || _rectTransform == null || _buttonVisual == null)
            {
                return;
            }

            _lastParentSize = _uiLayer.rect.size;
            float square = Mathf.Clamp(Mathf.Min(_lastParentSize.x, _lastParentSize.y) * 0.090f, 82f, 104f);
            float trayWidth = square * 1.88f;
            float trayHeight = square * 1.02f;
            float horizontalInset = Mathf.Max(18f, _lastParentSize.x * 0.022f);
            float verticalInset = Mathf.Max(18f, _lastParentSize.y * 0.022f);
            float boardTouchGap = Mathf.Max(0f, square * 0.005f);

            _reservedTopPadding = trayHeight + Mathf.Max(8f, verticalInset * 0.45f);
            _reservedBottomPadding = trayHeight + Mathf.Max(8f, verticalInset * 0.45f);
            _reservedLeftPadding = trayWidth + Mathf.Max(8f, horizontalInset * 0.55f);
            _reservedRightPadding = trayWidth + Mathf.Max(8f, horizontalInset * 0.55f);

            if (_hasBoardFrame && _boardSize.x > 1f && _boardSize.y > 1f)
            {
                float halfBoardWidth = _boardSize.x * 0.5f;
                float halfBoardHeight = _boardSize.y * 0.5f;
                float cornerX = halfBoardWidth + (trayWidth * 0.5f) + boardTouchGap;
                float cornerY = halfBoardHeight + (trayHeight * 0.5f) + boardTouchGap;
                _slotCenters[(int)DiceDock.Top] = _boardCenter + new Vector2(-cornerX, cornerY);
                _slotCenters[(int)DiceDock.Right] = _boardCenter + new Vector2(cornerX, cornerY);
                _slotCenters[(int)DiceDock.Bottom] = _boardCenter + new Vector2(cornerX, -cornerY);
                _slotCenters[(int)DiceDock.Left] = _boardCenter + new Vector2(-cornerX, -cornerY);
            }
            else
            {
                float fallbackX = (_lastParentSize.x * 0.5f) - horizontalInset - (trayWidth * 0.5f);
                float fallbackY = (_lastParentSize.y * 0.5f) - verticalInset - (trayHeight * 0.5f);
                _slotCenters[(int)DiceDock.Top] = new Vector2(-fallbackX, fallbackY);
                _slotCenters[(int)DiceDock.Right] = new Vector2(fallbackX, fallbackY);
                _slotCenters[(int)DiceDock.Bottom] = new Vector2(fallbackX, -fallbackY);
                _slotCenters[(int)DiceDock.Left] = new Vector2(-fallbackX, -fallbackY);
            }

            for (int i = 0; i < _slotRects.Length; i++)
            {
                if (_slotRects[i] == null)
                {
                    continue;
                }

                _slotRects[i].sizeDelta = new Vector2(trayWidth, trayHeight);
                _slotRects[i].anchoredPosition = _slotCenters[i];

                bool tokenOnLeft = i == (int)DiceDock.Top || i == (int)DiceDock.Left;
                float badgeSize = trayHeight * 0.78f;
                float dieSize = square;
                float sideInset = trayHeight * 0.14f;
                float badgeX = tokenOnLeft
                    ? (-trayWidth * 0.5f) + sideInset + (badgeSize * 0.5f)
                    : (trayWidth * 0.5f) - sideInset - (badgeSize * 0.5f);
                float dieX = tokenOnLeft
                    ? (trayWidth * 0.5f) - sideInset - (dieSize * 0.5f)
                    : (-trayWidth * 0.5f) + sideInset + (dieSize * 0.5f);

                _slotFaceCenters[i] = _slotCenters[i] + new Vector2(dieX, 0f);

                if (_slotFaceDockRects[i] != null)
                {
                    _slotFaceDockRects[i].sizeDelta = new Vector2(dieSize, dieSize);
                    _slotFaceDockRects[i].anchoredPosition = new Vector2(dieX, 0f);
                }

                if (_slotTokenBadgeRects[i] != null)
                {
                    _slotTokenBadgeRects[i].sizeDelta = new Vector2(badgeSize, badgeSize);
                    _slotTokenBadgeRects[i].anchoredPosition = new Vector2(badgeX, 0f);
                }

                if (_slotTokenIconImages[i] != null)
                {
                    RectTransform iconRect = _slotTokenIconImages[i].rectTransform;
                    iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                    iconRect.sizeDelta = new Vector2(badgeSize * 0.58f, badgeSize * 0.58f);
                    iconRect.anchoredPosition = new Vector2(0f, -badgeSize * 0.02f);
                }
            }

            _rootBasePosition = Vector2.zero;
            _rectTransform.anchoredPosition = _rootBasePosition;
            _buttonBasePosition = _slotFaceCenters[(int)_currentDock];
            _shadowBasePosition = _buttonBasePosition + new Vector2(square * 0.03f, -(square * 0.03f));
            _readyGlowBasePosition = _buttonBasePosition + new Vector2(0f, square * 0.04f);

            if (_buttonVisual != null)
            {
                _buttonVisual.sizeDelta = new Vector2(square, square);
                _buttonVisual.anchoredPosition = _buttonBasePosition;
                if (force || !_isRolling)
                {
                    _buttonVisual.localScale = Vector3.one;
                    _buttonVisual.localEulerAngles = Vector3.zero;
                }
            }

            if (_shadowRect != null)
            {
                _shadowRect.sizeDelta = new Vector2(square * 1.02f, square * 1.02f);
                _shadowRect.anchoredPosition = _shadowBasePosition;
            }

            if (_readyGlow != null)
            {
                _readyGlow.rectTransform.sizeDelta = new Vector2(square * 1.55f, square * 1.35f);
                _readyGlow.rectTransform.anchoredPosition = _readyGlowBasePosition;
            }

            UpdatePipLayout(square);
            UpdateSlotVisuals(_button != null && _button.interactable);
        }

        private void RefreshReadyState(bool interactable)
        {
            if (_readyGlow == null || _animationController == null)
            {
                UpdateSlotVisuals(interactable);
                return;
            }

            CancelReadyAnimation();
            _readyGlow.rectTransform.localScale = Vector3.one;

            if (interactable)
            {
                _readyGlow.color = LudoTheme.ReadyGlow;
                _readyGlowHandle = _animationController.ScheduleLoopPulse(_readyGlow.rectTransform, Vector3.one, 0.09f, 1.30f);
                _readyScaleHandle = _animationController.ScheduleLoopPulse(_buttonVisual, Vector3.one, 0.016f, 1.48f);
            }
            else
            {
                _readyGlow.color = new Color(1f, 0.88f, 0.56f, 0.14f);
                if (_buttonVisual != null)
                {
                    _buttonVisual.localScale = Vector3.one;
                }
            }

            UpdateSlotVisuals(interactable);
        }

        private void CancelReadyAnimation()
        {
            if (_readyGlowHandle != null)
            {
                _readyGlowHandle.Cancel();
                _readyGlowHandle = null;
            }

            if (_readyScaleHandle != null)
            {
                _readyScaleHandle.Cancel();
                _readyScaleHandle = null;
            }
        }

        private void SetFaceValue(int value)
        {
            _currentFaceValue = Mathf.Clamp(value, 1, 6);
            bool[] visiblePips = new bool[_pipImages.Length];

            switch (_currentFaceValue)
            {
                case 1:
                    visiblePips[0] = true;
                    break;
                case 2:
                    visiblePips[2] = true;
                    visiblePips[5] = true;
                    break;
                case 3:
                    visiblePips[1] = true;
                    visiblePips[0] = true;
                    visiblePips[6] = true;
                    break;
                case 4:
                    visiblePips[1] = true;
                    visiblePips[2] = true;
                    visiblePips[5] = true;
                    visiblePips[6] = true;
                    break;
                case 5:
                    visiblePips[1] = true;
                    visiblePips[2] = true;
                    visiblePips[0] = true;
                    visiblePips[5] = true;
                    visiblePips[6] = true;
                    break;
                case 6:
                    visiblePips[1] = true;
                    visiblePips[2] = true;
                    visiblePips[3] = true;
                    visiblePips[4] = true;
                    visiblePips[5] = true;
                    visiblePips[6] = true;
                    break;
            }

            for (int i = 0; i < _pipImages.Length; i++)
            {
                if (_pipImages[i] != null)
                {
                    _pipImages[i].color = PipColor;
                    _pipImages[i].gameObject.SetActive(visiblePips[i]);
                }

                if (_pipGlossImages[i] != null)
                {
                    _pipGlossImages[i].color = PipGlossColor;
                }
            }
        }

        private void UpdatePipLayout(float squareSize)
        {
            if (_faceRoot == null)
            {
                return;
            }

            float pipArea = squareSize * 0.58f;
            float offset = pipArea * 0.40f;
            float pipSize = Mathf.Max(18f, squareSize * 0.13f);

            for (int i = 0; i < _pipImages.Length; i++)
            {
                if (_pipImages[i] == null)
                {
                    continue;
                }

                Vector2 pipOffset = new Vector2(PipPositions[i].x * offset, PipPositions[i].y * offset);
                _pipImages[i].rectTransform.anchoredPosition = pipOffset;
                _pipImages[i].rectTransform.sizeDelta = new Vector2(pipSize, pipSize);
            }
        }

        private void SetRollingFaceVisual()
        {
            for (int i = 0; i < _pipImages.Length; i++)
            {
                if (_pipImages[i] != null)
                {
                    _pipImages[i].color = PipRollColor;
                    _pipImages[i].gameObject.SetActive(true);
                }

                if (_pipGlossImages[i] != null)
                {
                    _pipGlossImages[i].color = PipRollGlossColor;
                }
            }
        }

        private void ResetToBasePose()
        {
            _isRolling = false;

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _rootBasePosition;
            }

            if (_buttonVisual != null)
            {
                _buttonVisual.anchoredPosition = _buttonBasePosition;
                _buttonVisual.localScale = Vector3.one;
                Vector3 rotation = _buttonVisual.localEulerAngles;
                rotation.z = 0f;
                _buttonVisual.localEulerAngles = rotation;
            }

            if (_shadowRect != null)
            {
                _shadowRect.anchoredPosition = _shadowBasePosition;
            }

            if (_readyGlow != null)
            {
                _readyGlow.rectTransform.anchoredPosition = _readyGlowBasePosition;
            }

            if (_faceRoot != null)
            {
                _faceRoot.localScale = Vector3.one;
            }
        }

        private void PlayDiceSound()
        {
            if (_diceSound == null)
            {
                _diceSound = Resources.Load<AudioClip>("dice_sound");
                if (_diceSound != null && _diceSound.loadState == AudioDataLoadState.Unloaded)
                {
                    _diceSound.LoadAudioData();
                }

                if (_diceSound == null)
                {
                    return;
                }
            }

            if (_diceSound.loadState == AudioDataLoadState.Unloaded)
            {
                _diceSound.LoadAudioData();
            }

            AudioListener listener = FindAnyObjectByType<AudioListener>();
            if (listener != null)
            {
                AudioSource.PlayClipAtPoint(_diceSound, listener.transform.position, SoundVolume);
                return;
            }

            if (_audioSource == null)
            {
                return;
            }

            _audioSource.Stop();
            _audioSource.clip = _diceSound;
            _audioSource.pitch = UnityEngine.Random.Range(0.98f, 1.03f);
            _audioSource.volume = SoundVolume;
            _audioSource.time = 0f;
            _audioSource.Play();
        }

        private void OnRollPressed()
        {
            if (_rollRequested != null)
            {
                _rollRequested();
            }
        }

        private void SetDock(DiceDock dock, bool immediate)
        {
            _currentDock = dock;
            RefreshLayout(false);

            if (immediate || _animationController == null || _buttonVisual == null)
            {
                ResetToBasePose();
            }
            else
            {
                _buttonVisual.anchoredPosition = _buttonBasePosition;
                if (_shadowRect != null)
                {
                    _shadowRect.anchoredPosition = _shadowBasePosition;
                }

                if (_readyGlow != null)
                {
                    _readyGlow.rectTransform.anchoredPosition = _readyGlowBasePosition;
                }
            }
        }

        private void UpdateSlotVisuals(bool interactable)
        {
            for (int i = 0; i < _slotRects.Length; i++)
            {
                bool isActive = i == (int)_currentDock;
                if (_slotFillImages[i] != null)
                {
                    _slotFillImages[i].color = isActive ? SlotActiveFillColor : SlotFillColor;
                }

                if (_slotAccentImages[i] != null)
                {
                    DiceDock dock = (DiceDock)i;
                    float alpha = isActive ? (interactable ? 0.24f : 0.18f) : 0.08f;
                    _slotAccentImages[i].color = LudoUtility.WithAlpha(GetDockAccentColor(dock), alpha);
                }

                if (_slotFaceDockImages[i] != null)
                {
                    _slotFaceDockImages[i].color = isActive ? SlotFaceColor : SlotFaceIdleColor;
                }

                if (_slotTokenBadgeImages[i] != null)
                {
                    float tintBoost = isActive ? 0.12f : 0.04f;
                    _slotTokenBadgeImages[i].color = Color.Lerp(GetDockAccentColor((DiceDock)i), Color.white, tintBoost);
                }
            }
        }

        private static DiceDock GetDockForTokenColor(LudoTokenColor tokenColor)
        {
            switch (tokenColor)
            {
                case LudoTokenColor.Red:
                    return DiceDock.Top;
                case LudoTokenColor.Green:
                    return DiceDock.Right;
                case LudoTokenColor.Yellow:
                    return DiceDock.Bottom;
                default:
                    return DiceDock.Left;
            }
        }

        private static Color GetDockAccentColor(DiceDock dock)
        {
            switch (dock)
            {
                case DiceDock.Top:
                    return LudoTheme.GetTokenTint(LudoTokenColor.Red);
                case DiceDock.Right:
                    return LudoTheme.GetTokenTint(LudoTokenColor.Green);
                case DiceDock.Bottom:
                    return LudoTheme.GetTokenTint(LudoTokenColor.Yellow);
                default:
                    return LudoTheme.GetTokenTint(LudoTokenColor.Blue);
            }
        }

        private static LudoTokenColor GetDockTokenColor(DiceDock dock)
        {
            switch (dock)
            {
                case DiceDock.Top:
                    return LudoTokenColor.Red;
                case DiceDock.Right:
                    return LudoTokenColor.Green;
                case DiceDock.Bottom:
                    return LudoTokenColor.Yellow;
                default:
                    return LudoTokenColor.Blue;
            }
        }
    }
}
