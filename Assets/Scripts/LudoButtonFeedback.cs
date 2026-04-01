using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform _rectTransform;
        private Graphic _accentGraphic;
        private LudoAnimationController _animationController;
        private LudoAnimationHandle _scaleHandle;
        private LudoAnimationHandle _accentHandle;
        private Vector3 _baseScale = Vector3.one;
        private Color _baseAccentColor = Color.clear;
        private float _hoverScale = 1.015f;
        private float _pressedScale = 0.975f;
        private float _hoverAccentAlpha = 0.12f;
        private float _pressedAccentAlpha = 0.18f;
        private bool _isPointerInside;
        private bool _isPressed;

        public void Configure(Graphic accentGraphic, float hoverScale = 1.015f, float pressedScale = 0.975f, float hoverAccentAlpha = 0.12f, float pressedAccentAlpha = 0.18f)
        {
            _rectTransform = transform as RectTransform;
            _accentGraphic = accentGraphic;
            _hoverScale = hoverScale;
            _pressedScale = pressedScale;
            _hoverAccentAlpha = hoverAccentAlpha;
            _pressedAccentAlpha = pressedAccentAlpha;
            _baseScale = _rectTransform != null ? _rectTransform.localScale : Vector3.one;
            _baseAccentColor = _accentGraphic != null ? _accentGraphic.color : Color.clear;

            if (_animationController == null)
            {
                _animationController = FindAnyObjectByType<LudoAnimationController>();
            }
        }

        public void SyncBaseState()
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            _baseScale = _rectTransform != null ? _rectTransform.localScale : Vector3.one;
            _baseAccentColor = _accentGraphic != null ? _accentGraphic.color : Color.clear;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            ApplyState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            _isPressed = false;
            ApplyState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            ApplyState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            ApplyState();
        }

        private void OnDisable()
        {
            _isPointerInside = false;
            _isPressed = false;

            if (_scaleHandle != null)
            {
                _scaleHandle.Cancel();
                _scaleHandle = null;
            }

            if (_accentHandle != null)
            {
                _accentHandle.Cancel();
                _accentHandle = null;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = _baseScale;
            }

            if (_accentGraphic != null)
            {
                _accentGraphic.color = _baseAccentColor;
            }
        }

        private void ApplyState()
        {
            if (_rectTransform == null)
            {
                _rectTransform = transform as RectTransform;
            }

            float targetScaleFactor = _isPressed ? _pressedScale : (_isPointerInside ? _hoverScale : 1f);
            float targetAccentAlpha = _isPressed ? _pressedAccentAlpha : (_isPointerInside ? _hoverAccentAlpha : _baseAccentColor.a);
            Vector3 targetScale = _baseScale * targetScaleFactor;

            if (_animationController == null)
            {
                if (_rectTransform != null)
                {
                    _rectTransform.localScale = targetScale;
                }

                if (_accentGraphic != null)
                {
                    Color accent = _accentGraphic.color;
                    accent.a = targetAccentAlpha;
                    _accentGraphic.color = accent;
                }

                return;
            }

            float now = _animationController.TimeNow;

            if (_scaleHandle != null)
            {
                _scaleHandle.Cancel();
            }

            if (_accentHandle != null)
            {
                _accentHandle.Cancel();
            }

            if (_rectTransform != null)
            {
                _scaleHandle = _animationController.ScheduleScale(_rectTransform, _rectTransform.localScale, targetScale, now, 0.12f, LudoEase.EaseOut);
            }

            if (_accentGraphic != null)
            {
                Color targetAccent = _accentGraphic.color;
                targetAccent.a = targetAccentAlpha;
                _accentHandle = _animationController.ScheduleGraphicColor(_accentGraphic, _accentGraphic.color, targetAccent, now, 0.12f, LudoEase.EaseOut);
            }
        }
    }
}
