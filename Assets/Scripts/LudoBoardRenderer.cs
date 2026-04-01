using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoBoardRenderer : MonoBehaviour
    {
        private RectTransform _layerRect;
        private RectTransform _backgroundRoot;
        private RectTransform _patternLayer;
        private RectTransform _boardRoot;
        private RectTransform _boardShadow;
        private RectTransform _boardImageRect;
        private RectTransform _markerLayer;
        private Image _backdropImage;
        private Image _boardShadowImage;
        private Image _boardImage;
        private readonly Image[] _patternImages = new Image[4];
        private readonly Image[] _glowImages = new Image[2];
        private readonly Dictionary<LudoTokenColor, Text> _ninthBoxMarkers = new Dictionary<LudoTokenColor, Text>(4);
        private readonly List<RectTransform> _mirroredRects = new List<RectTransform>(4);
        private readonly Vector2[,] _cellCenters = new Vector2[LudoBoardGeometry.BoardSize, LudoBoardGeometry.BoardSize];
        private readonly List<Vector2> _pathPositions = new List<Vector2>(LudoBoardGeometry.CommonPathLength);
        private Vector2 _lastLayerSize;
        private Vector2 _boardDisplaySize;
        private float _gridInsetX;
        private float _gridInsetY;
        private float _cellWidth;
        private float _cellHeight;
        private float _topPadding;
        private float _bottomPadding;
        private float _leftPadding;
        private float _rightPadding;
        private int _layoutVersion;

        public RectTransform BoardRoot
        {
            get { return _boardRoot; }
        }

        public float TokenSize
        {
            get { return Mathf.Min(_cellWidth, _cellHeight) * 2.0f; }
        }

        public float HomeTokenSize
        {
            get { return Mathf.Min(_cellWidth, _cellHeight) * 2.0f; }
        }

        public int LayoutVersion
        {
            get { return _layoutVersion; }
        }

        public IReadOnlyList<Vector2> PathPositions
        {
            get { return _pathPositions; }
        }

        public void SetBoardVisible(bool visible)
        {
            if (_boardRoot != null)
            {
                _boardRoot.gameObject.SetActive(visible);
            }
        }

        public void Initialize(RectTransform layerRect)
        {
            _layerRect = layerRect;
            BuildIfNeeded();
            RefreshLayout();
        }

        public void SetViewportPadding(float top, float bottom, float left = 0f, float right = 0f)
        {
            _topPadding = Mathf.Max(0f, top);
            _bottomPadding = Mathf.Max(0f, bottom);
            _leftPadding = Mathf.Max(0f, left);
            _rightPadding = Mathf.Max(0f, right);

            if (_layerRect != null)
            {
                RefreshLayout();
            }
        }

        public void RegisterMirroredRect(RectTransform rectTransform)
        {
            if (rectTransform == null || _mirroredRects.Contains(rectTransform))
            {
                return;
            }

            _mirroredRects.Add(rectTransform);
            SyncMirroredRect(rectTransform);
        }

        public Vector2 GetCellCenterLocal(Vector2Int boardCoordinate)
        {
            int x = Mathf.Clamp(boardCoordinate.x, 0, LudoBoardGeometry.BoardSize - 1);
            int y = Mathf.Clamp(boardCoordinate.y, 0, LudoBoardGeometry.BoardSize - 1);
            return _cellCenters[x, y];
        }

        public Vector2 GetBoardPoint(Vector2 boardCoordinate)
        {
            return GetBoardPoint(boardCoordinate.x, boardCoordinate.y);
        }

        public Vector2 GetBoardPoint(float boardX, float boardY)
        {
            if (_boardRoot == null)
            {
                return Vector2.zero;
            }

            float clampedX = Mathf.Clamp(boardX, -0.5f, LudoBoardGeometry.BoardSize - 0.5f);
            float clampedY = Mathf.Clamp(boardY, -0.5f, LudoBoardGeometry.BoardSize - 0.5f);
            Vector2 boardOffset = _boardRoot.anchoredPosition;
            float left = (-_boardDisplaySize.x * 0.5f) + _gridInsetX;
            float bottom = (-_boardDisplaySize.y * 0.5f) + _gridInsetY;
            return boardOffset + new Vector2(left + ((clampedX + 0.5f) * _cellWidth), bottom + ((clampedY + 0.5f) * _cellHeight));
        }

        private void Update()
        {
            if (_layerRect == null)
            {
                return;
            }

            Vector2 currentSize = _layerRect.rect.size;
            if ((currentSize - _lastLayerSize).sqrMagnitude > 0.5f)
            {
                RefreshLayout();
            }
        }

        private void BuildIfNeeded()
        {
            if (_boardRoot != null)
            {
                return;
            }

            _backgroundRoot = LudoUtility.CreateUIObject("BackgroundRoot", _layerRect);
            LudoUtility.Stretch(_backgroundRoot);

            _backdropImage = LudoUtility.CreateImage("Backdrop", _backgroundRoot, LudoSpriteFactory.BackdropGradient, new Color(0.18f, 0.41f, 0.93f, 1f));
            LudoUtility.Stretch(_backdropImage.rectTransform);
            _backdropImage.raycastTarget = false;

            _patternLayer = LudoUtility.CreateUIObject("PatternLayer", _backgroundRoot);
            LudoUtility.Stretch(_patternLayer);

            for (int i = 0; i < _patternImages.Length; i++)
            {
                Image pattern = LudoUtility.CreateImage("Pattern" + i, _patternLayer, null, new Color(0.84f, 0.93f, 1f, i < 2 ? 0.14f : 0.09f));
                pattern.preserveAspect = true;
                pattern.raycastTarget = false;
                _patternImages[i] = pattern;
            }

            for (int i = 0; i < _glowImages.Length; i++)
            {
                Image glow = LudoUtility.CreateImage("Glow" + i, _backgroundRoot, LudoSpriteFactory.SoftDisc, i == 0 ? new Color(0.30f, 0.63f, 1f, 0.20f) : new Color(0.14f, 0.30f, 0.76f, 0.28f));
                glow.raycastTarget = false;
                _glowImages[i] = glow;
            }

            _boardRoot = LudoUtility.CreateUIObject("BoardRoot", _layerRect);
            _boardRoot.anchorMin = _boardRoot.anchorMax = _boardRoot.pivot = new Vector2(0.5f, 0.5f);

            _boardShadow = LudoUtility.CreateUIObject("BoardShadow", _boardRoot);
            _boardShadow.anchorMin = _boardShadow.anchorMax = _boardShadow.pivot = new Vector2(0.5f, 0.5f);
            _boardShadowImage = LudoUtility.GetOrAddComponent<Image>(_boardShadow.gameObject);
            _boardShadowImage.color = new Color(0f, 0f, 0f, 0.16f);
            _boardShadowImage.preserveAspect = true;
            _boardShadowImage.raycastTarget = false;

            _boardImageRect = LudoUtility.CreateUIObject("BoardImage", _boardRoot);
            _boardImageRect.anchorMin = _boardImageRect.anchorMax = _boardImageRect.pivot = new Vector2(0.5f, 0.5f);
            _boardImage = LudoUtility.GetOrAddComponent<Image>(_boardImageRect.gameObject);
            _boardImage.color = Color.white;
            _boardImage.preserveAspect = true;
            _boardImage.raycastTarget = false;

            _markerLayer = LudoUtility.CreateUIObject("MarkerLayer", _boardRoot);
            _markerLayer.anchorMin = _markerLayer.anchorMax = _markerLayer.pivot = new Vector2(0.5f, 0.5f);

            ApplyBoardSprite();
            BuildNinthBoxMarkers();
        }

        private void ApplyBoardSprite()
        {
            Sprite boardSprite = LudoArtLibrary.GetBoardSprite();
            if (_boardImage != null)
            {
                _boardImage.sprite = boardSprite;
                _boardImage.enabled = boardSprite != null;
            }

            if (_boardShadowImage != null)
            {
                _boardShadowImage.sprite = boardSprite;
                _boardShadowImage.enabled = boardSprite != null;
            }

            for (int i = 0; i < _patternImages.Length; i++)
            {
                if (_patternImages[i] == null)
                {
                    continue;
                }

                _patternImages[i].sprite = boardSprite;
                _patternImages[i].enabled = boardSprite != null;
            }
        }

        private void RefreshLayout()
        {
            if (_layerRect == null || _boardRoot == null)
            {
                return;
            }

            _lastLayerSize = _layerRect.rect.size;
            if (_lastLayerSize.x <= 1f || _lastLayerSize.y <= 1f)
            {
                return;
            }

            UpdateBoardDisplayRect();
            _boardRoot.sizeDelta = _boardDisplaySize;
            _boardRoot.anchoredPosition = new Vector2((_leftPadding - _rightPadding) * 0.5f, (_bottomPadding - _topPadding) * 0.5f);

            if (_boardShadow != null)
            {
                _boardShadow.sizeDelta = _boardDisplaySize;
                _boardShadow.anchoredPosition = new Vector2(_boardDisplaySize.x * 0.012f, -_boardDisplaySize.y * 0.016f);
            }

            RefreshBackdropLayout();

            CacheCellCenters();
            UpdateNinthBoxMarkers();

            for (int i = 0; i < _mirroredRects.Count; i++)
            {
                SyncMirroredRect(_mirroredRects[i]);
            }

            _layoutVersion++;
        }

        private void RefreshBackdropLayout()
        {
            if (_layerRect == null)
            {
                return;
            }

            Vector2 size = _lastLayerSize;
            if (_backgroundRoot != null)
            {
                _backgroundRoot.sizeDelta = size;
                _backgroundRoot.anchoredPosition = Vector2.zero;
            }

            if (_glowImages[0] != null)
            {
                _glowImages[0].rectTransform.anchorMin = _glowImages[0].rectTransform.anchorMax = _glowImages[0].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _glowImages[0].rectTransform.sizeDelta = new Vector2(size.x * 1.05f, size.y * 0.92f);
                _glowImages[0].rectTransform.anchoredPosition = new Vector2(-size.x * 0.06f, size.y * 0.09f);
            }

            if (_glowImages[1] != null)
            {
                _glowImages[1].rectTransform.anchorMin = _glowImages[1].rectTransform.anchorMax = _glowImages[1].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _glowImages[1].rectTransform.sizeDelta = new Vector2(size.x * 1.12f, size.y * 1.02f);
                _glowImages[1].rectTransform.anchoredPosition = new Vector2(size.x * 0.14f, -size.y * 0.14f);
            }

            ConfigurePattern(0, new Vector2(-size.x * 0.34f, size.y * 0.34f), new Vector2(size.x * 0.88f, size.y * 0.88f), 28f);
            ConfigurePattern(1, new Vector2(size.x * 0.34f, -size.y * 0.34f), new Vector2(size.x * 0.88f, size.y * 0.88f), 28f);
            ConfigurePattern(2, new Vector2(size.x * 0.35f, size.y * 0.36f), new Vector2(size.x * 0.76f, size.y * 0.76f), 28f);
            ConfigurePattern(3, new Vector2(-size.x * 0.36f, -size.y * 0.33f), new Vector2(size.x * 0.76f, size.y * 0.76f), 28f);
        }

        private void ConfigurePattern(int index, Vector2 anchoredPosition, Vector2 size, float rotation)
        {
            if (index < 0 || index >= _patternImages.Length || _patternImages[index] == null)
            {
                return;
            }

            RectTransform rectTransform = _patternImages[index].rectTransform;
            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.localEulerAngles = new Vector3(0f, 0f, rotation);
        }

        private void CacheCellCenters()
        {
            Vector2 boardOffset = _boardRoot != null ? _boardRoot.anchoredPosition : Vector2.zero;
            float left = (-_boardDisplaySize.x * 0.5f) + _gridInsetX + (_cellWidth * 0.5f);
            float bottom = (-_boardDisplaySize.y * 0.5f) + _gridInsetY + (_cellHeight * 0.5f);

            for (int y = 0; y < LudoBoardGeometry.BoardSize; y++)
            {
                for (int x = 0; x < LudoBoardGeometry.BoardSize; x++)
                {
                    _cellCenters[x, y] = boardOffset + new Vector2(left + (x * _cellWidth), bottom + (y * _cellHeight));
                }
            }

            _pathPositions.Clear();
            for (int i = 0; i < LudoBoardGeometry.CommonPath.Count; i++)
            {
                _pathPositions.Add(GetCellCenterLocal(LudoBoardGeometry.CommonPath[i]));
            }
        }

        private void SyncMirroredRect(RectTransform rectTransform)
        {
            if (rectTransform == null || _boardRoot == null)
            {
                return;
            }

            rectTransform.anchorMin = rectTransform.anchorMax = rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = _boardRoot.sizeDelta;
            rectTransform.anchoredPosition = _boardRoot.anchoredPosition;
            rectTransform.localScale = Vector3.one;
        }

        private void UpdateBoardDisplayRect()
        {
            Sprite boardSprite = _boardImage != null ? _boardImage.sprite : null;
            float spriteAspect = 1f;
            if (boardSprite != null && boardSprite.rect.height > 0.01f)
            {
                spriteAspect = boardSprite.rect.width / boardSprite.rect.height;
            }

            float availableWidth = Mathf.Max(160f, _lastLayerSize.x - _leftPadding - _rightPadding);
            float availableHeight = Mathf.Max(160f, _lastLayerSize.y - _topPadding - _bottomPadding);
            float width = availableWidth;
            float height = width / spriteAspect;
            if (height > availableHeight)
            {
                height = availableHeight;
                width = height * spriteAspect;
            }

            _boardDisplaySize = new Vector2(width, height);
            if (_boardImageRect != null)
            {
                _boardImageRect.sizeDelta = _boardDisplaySize;
                _boardImageRect.anchoredPosition = Vector2.zero;
            }

            _gridInsetX = 0f;
            _gridInsetY = 0f;
            _cellWidth = _boardDisplaySize.x / LudoBoardGeometry.BoardSize;
            _cellHeight = _boardDisplaySize.y / LudoBoardGeometry.BoardSize;
        }

        private void BuildNinthBoxMarkers()
        {
            CreateNinthBoxMarker(LudoTokenColor.Red);
            CreateNinthBoxMarker(LudoTokenColor.Green);
            CreateNinthBoxMarker(LudoTokenColor.Blue);
            CreateNinthBoxMarker(LudoTokenColor.Yellow);
        }

        private void CreateNinthBoxMarker(LudoTokenColor tokenColor)
        {
            if (_markerLayer == null || _ninthBoxMarkers.ContainsKey(tokenColor))
            {
                return;
            }

            Text marker = LudoUtility.CreateText(
                tokenColor + "NinthBoxMarker",
                _markerLayer,
                "★",
                24,
                FontStyle.Bold,
                TextAnchor.MiddleCenter,
                new Color(0.22f, 0.18f, 0.14f, 0.60f));
            marker.raycastTarget = false;
            Outline outline = LudoUtility.GetOrAddComponent<Outline>(marker.gameObject);
            outline.effectColor = new Color(1f, 1f, 1f, 0.22f);
            outline.effectDistance = new Vector2(0.8f, -0.8f);
            outline.useGraphicAlpha = true;
            _ninthBoxMarkers[tokenColor] = marker;
        }

        private void UpdateNinthBoxMarkers()
        {
            if (_markerLayer == null)
            {
                return;
            }

            _markerLayer.sizeDelta = _boardDisplaySize;
            _markerLayer.anchoredPosition = Vector2.zero;

            foreach (KeyValuePair<LudoTokenColor, Text> pair in _ninthBoxMarkers)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                RectTransform markerRect = pair.Value.rectTransform;
                Vector2 boardSpacePosition = GetCellCenterLocal(LudoBoardGeometry.GetNinthBoxCoordinate(pair.Key)) - _boardRoot.anchoredPosition;
                markerRect.anchorMin = markerRect.anchorMax = markerRect.pivot = new Vector2(0.5f, 0.5f);
                markerRect.anchoredPosition = boardSpacePosition;
                markerRect.sizeDelta = new Vector2(_cellWidth * 0.85f, _cellHeight * 0.85f);
                pair.Value.fontSize = Mathf.RoundToInt(Mathf.Min(_cellWidth, _cellHeight) * 0.50f);
            }
        }
    }
}
