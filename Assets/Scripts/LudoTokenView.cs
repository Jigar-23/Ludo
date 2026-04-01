using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PremiumLudo
{
    public sealed class LudoTokenView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private static readonly Color OutlineBaseColor = new Color(0.12f, 0.10f, 0.08f, 1f);
        private static readonly Vector2 TokenPivot = new Vector2(0.53f, 0.23f);

        private RectTransform _rectTransform;
        private CanvasGroup _canvasGroup;
        private Image _tokenImage;
        private Shadow _shadowEffect;
        private Outline _outlineEffect;
        private LudoAnimationController _animationController;
        private LudoAnimationHandle _pulseHandle;
        private LudoAnimationHandle _focusHandle;
        private bool _isSelectable;
        private bool _isHovered;
        private Color _baseColor;
        private float _currentSize;

        public event Action<LudoTokenView> Clicked;

        public Vector2 Position
        {
            get { return _rectTransform != null ? _rectTransform.anchoredPosition : Vector2.zero; }
            set
            {
                if (_rectTransform != null)
                {
                    _rectTransform.anchoredPosition = value;
                }
            }
        }

        public Vector2 TargetPosition { get; set; }

        public Vector3 Scale
        {
            get { return _rectTransform != null ? _rectTransform.localScale : Vector3.one; }
            set
            {
                if (_rectTransform != null)
                {
                    _rectTransform.localScale = value;
                }
            }
        }

        public Vector2 Velocity { get; set; }

        public void Build(RectTransform parent, LudoAnimationController animationController, Sprite tokenSprite, Color tintColor, float size)
        {
            _animationController = animationController;
            _baseColor = Color.white;
            _rectTransform = gameObject.GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }

            _rectTransform.SetParent(parent, false);
            _rectTransform.anchorMin = _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _rectTransform.pivot = TokenPivot;
            _rectTransform.localScale = Vector3.one;
            _rectTransform.localRotation = Quaternion.identity;

            _tokenImage = LudoUtility.GetOrAddComponent<Image>(gameObject);
            _tokenImage.sprite = tokenSprite;
            _tokenImage.color = Color.white;
            _tokenImage.preserveAspect = true;
            _tokenImage.useSpriteMesh = false;
            _tokenImage.raycastTarget = false;

            _canvasGroup = LudoUtility.GetOrAddComponent<CanvasGroup>(gameObject);
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _shadowEffect = LudoUtility.GetOrAddComponent<Shadow>(gameObject);
            _shadowEffect.effectColor = new Color(0f, 0f, 0f, 0.16f);
            _shadowEffect.effectDistance = new Vector2(2f, -3f);
            _shadowEffect.useGraphicAlpha = true;

            _outlineEffect = LudoUtility.GetOrAddComponent<Outline>(gameObject);
            _outlineEffect.effectColor = LudoUtility.WithAlpha(OutlineBaseColor, 0.16f);
            _outlineEffect.effectDistance = new Vector2(1.2f, -1.2f);
            _outlineEffect.useGraphicAlpha = true;

            Resize(size);
            ResetPresentation();
            RefreshPulseState();
        }

        public void Resize(float size)
        {
            _currentSize = size;
            if (_rectTransform != null)
            {
                _rectTransform.sizeDelta = new Vector2(size, size);
            }

            RefreshStaticEffects();
        }

        public void SetSelectable(bool isSelectable)
        {
            _isSelectable = isSelectable;
            if (_tokenImage != null)
            {
                _tokenImage.raycastTarget = isSelectable;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = isSelectable;
                _canvasGroup.interactable = isSelectable;
            }

            RefreshPulseState();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
            if (visible)
            {
                ResetPresentation();
            }
        }

        public void ResetPresentation()
        {
            if (_focusHandle != null)
            {
                _focusHandle.Cancel();
                _focusHandle = null;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one;
            }

            if (_tokenImage != null)
            {
                _tokenImage.color = _baseColor;
            }

            RefreshStaticEffects();
        }

        public void SetFocusScale(float scale, float duration)
        {
            if (_rectTransform == null)
            {
                return;
            }

            if (_focusHandle != null)
            {
                _focusHandle.Cancel();
                _focusHandle = null;
            }

            Vector3 targetScale = new Vector3(scale, scale, 1f);
            if (_animationController == null || duration <= 0f)
            {
                _rectTransform.localScale = targetScale;
                return;
            }

            _focusHandle = _animationController.ScheduleScale(_rectTransform, _rectTransform.localScale, targetScale, _animationController.TimeNow, duration, LudoEase.EaseOut);
        }

        public void PlayLandingHighlight()
        {
            if (_animationController == null || _rectTransform == null)
            {
                return;
            }

            float now = _animationController.TimeNow;
            _animationController.SchedulePulse(_rectTransform, Vector3.one, now, 0.22f, 0.08f, 1);
            _animationController.ScheduleCallback(now, () => SetOutlineAlpha(0.34f));
            _animationController.ScheduleCallback(now + 0.22f, RefreshStaticEffects);
        }

        public void PlayStepMovement(IList<Vector2> stepPositions, Action onComplete)
        {
            if (_animationController == null || _rectTransform == null || stepPositions == null || stepPositions.Count == 0)
            {
                if (onComplete != null)
                {
                    onComplete();
                }

                return;
            }

            StopPassiveEffects();
            transform.SetAsLastSibling();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            float currentTime = _animationController.TimeNow;
            Vector2 currentPosition = Position;

            for (int i = 0; i < stepPositions.Count; i++)
            {
                Vector2 targetPosition = stepPositions[i];
                Vector2 movement = targetPosition - currentPosition;
                Vector2 direction = movement.sqrMagnitude > 0.0001f ? movement.normalized : Vector2.up;
                bool horizontal = Mathf.Abs(movement.x) >= Mathf.Abs(movement.y);
                bool isFinalStep = i == stepPositions.Count - 1;
                float anticipationDuration = UnityEngine.Random.Range(0.05f, 0.09f);
                float moveDuration = 0.18f * UnityEngine.Random.Range(0.92f, 1.08f);
                float postStepPause = isFinalStep ? 0.08f : 0.04f;
                float anticipationDistance = UnityEngine.Random.Range(1.0f, 1.8f);
                float arcHeight = UnityEngine.Random.Range(6f, 10f) * Mathf.Max(0.8f, _currentSize / 70f);
                Vector2 anticipationPosition = currentPosition - (direction * anticipationDistance);
                Vector2 normal = new Vector2(-direction.y, direction.x);
                if (normal.sqrMagnitude < 0.001f)
                {
                    normal = Vector2.up;
                }

                normal.Normalize();
                if (UnityEngine.Random.value > 0.5f)
                {
                    normal *= -1f;
                }

                TargetPosition = targetPosition;
                Velocity = direction * (movement.magnitude / Mathf.Max(0.01f, moveDuration));

                Vector2 controlPoint = ((anticipationPosition + targetPosition) * 0.5f) + (normal * arcHeight);
                float moveStart = currentTime + anticipationDuration;
                float stretchStart = moveStart + 0.03f;
                float landingStart = moveStart + (moveDuration * 0.62f);
                float landingSquashDuration = 0.05f;
                float landingReboundDuration = 0.05f;
                float landingSettleDuration = 0.04f;
                float landingRecoverEnd = landingStart + landingSquashDuration + landingReboundDuration + landingSettleDuration;

                Vector3 anticipationScale = Vector3.one * 0.95f;
                Vector3 launchScale = Vector3.one * 1.10f;
                Vector3 stretchScale = horizontal ? new Vector3(1.10f, 0.96f, 1f) : new Vector3(0.96f, 1.10f, 1f);
                Vector3 squashScale = horizontal ? new Vector3(0.94f, 1.06f, 1f) : new Vector3(1.06f, 0.94f, 1f);
                Vector3 settleScale = horizontal ? new Vector3(1.02f, 0.99f, 1f) : new Vector3(0.99f, 1.02f, 1f);

                _animationController.ScheduleMove(_rectTransform, currentPosition, anticipationPosition, currentTime, anticipationDuration, LudoEase.EaseOutQuad);
                _animationController.ScheduleScale(_rectTransform, Vector3.one, anticipationScale, currentTime, anticipationDuration, LudoEase.EaseOutQuad);

                _animationController.ScheduleScale(_rectTransform, anticipationScale, launchScale, moveStart, 0.05f, LudoEase.EaseOut);
                _animationController.ScheduleScale(_rectTransform, launchScale, stretchScale, stretchStart, moveDuration * 0.34f, LudoEase.EaseOutQuad);
                _animationController.ScheduleArcMove(_rectTransform, anticipationPosition, controlPoint, targetPosition, moveStart, moveDuration, LudoEase.EaseInOut);
                _animationController.ScheduleScale(_rectTransform, stretchScale, squashScale, landingStart, landingSquashDuration, LudoEase.EaseOut);
                _animationController.ScheduleScale(_rectTransform, squashScale, settleScale, landingStart + landingSquashDuration, landingReboundDuration, LudoEase.EaseOutBack);
                _animationController.ScheduleScale(_rectTransform, settleScale, Vector3.one, landingStart + landingSquashDuration + landingReboundDuration, landingSettleDuration, LudoEase.EaseOut);

                currentTime = landingRecoverEnd + postStepPause;
                currentPosition = targetPosition;
            }

            float finishTime = currentTime;
            _animationController.ScheduleCallback(finishTime, () =>
            {
                Position = currentPosition;
                TargetPosition = currentPosition;
                Velocity = Vector2.zero;
                if (_rectTransform != null)
                {
                    _rectTransform.localScale = Vector3.one;
                }

                RefreshStaticEffects();

                if (onComplete != null)
                {
                    onComplete();
                }

                RefreshPulseState();
            });
        }

        public void PlayReturnHomeMovement(IList<Vector2> stepPositions, Action onComplete)
        {
            if (_animationController == null || _rectTransform == null || stepPositions == null || stepPositions.Count == 0)
            {
                if (onComplete != null)
                {
                    onComplete();
                }

                return;
            }

            StopPassiveEffects();
            transform.SetAsLastSibling();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            float currentTime = _animationController.TimeNow;
            Vector2 currentPosition = Position;

            for (int i = 0; i < stepPositions.Count; i++)
            {
                Vector2 targetPosition = stepPositions[i];
                Vector2 movement = targetPosition - currentPosition;
                Vector2 direction = movement.sqrMagnitude > 0.0001f ? movement.normalized : Vector2.up;
                bool horizontal = Mathf.Abs(movement.x) >= Mathf.Abs(movement.y);
                bool isFinalStep = i == stepPositions.Count - 1;
                float moveDuration = 0.05f * UnityEngine.Random.Range(0.92f, 1.08f);
                float postStepPause = isFinalStep ? 0.05f : 0.012f;
                float arcHeight = UnityEngine.Random.Range(2f, 4f) * Mathf.Max(0.8f, _currentSize / 70f);
                Vector2 normal = new Vector2(-direction.y, direction.x);
                if (normal.sqrMagnitude < 0.001f)
                {
                    normal = Vector2.up;
                }

                normal.Normalize();
                if (UnityEngine.Random.value > 0.5f)
                {
                    normal *= -1f;
                }

                TargetPosition = targetPosition;
                Velocity = direction * (movement.magnitude / Mathf.Max(0.01f, moveDuration));

                Vector2 controlPoint = ((currentPosition + targetPosition) * 0.5f) + (normal * arcHeight);
                Vector3 stretchScale = horizontal ? new Vector3(1.05f, 0.98f, 1f) : new Vector3(0.98f, 1.05f, 1f);
                Vector3 settleScale = horizontal ? new Vector3(0.98f, 1.02f, 1f) : new Vector3(1.02f, 0.98f, 1f);

                _animationController.ScheduleArcMove(_rectTransform, currentPosition, controlPoint, targetPosition, currentTime, moveDuration, LudoEase.EaseInOut);
                _animationController.ScheduleScale(_rectTransform, Vector3.one, stretchScale, currentTime, moveDuration * 0.48f, LudoEase.EaseOut);
                _animationController.ScheduleScale(_rectTransform, stretchScale, settleScale, currentTime + (moveDuration * 0.48f), moveDuration * 0.22f, LudoEase.EaseOut);
                _animationController.ScheduleScale(_rectTransform, settleScale, Vector3.one, currentTime + (moveDuration * 0.70f), moveDuration * 0.30f, LudoEase.EaseOut);

                currentTime += moveDuration + postStepPause;
                currentPosition = targetPosition;
            }

            float finishTime = currentTime;
            _animationController.ScheduleCallback(finishTime, () =>
            {
                Position = currentPosition;
                TargetPosition = currentPosition;
                Velocity = Vector2.zero;
                if (_rectTransform != null)
                {
                    _rectTransform.localScale = Vector3.one;
                }

                RefreshStaticEffects();

                if (onComplete != null)
                {
                    onComplete();
                }

                RefreshPulseState();
            });
        }

        public void PlayCaptureReaction(Action onComplete)
        {
            if (_animationController == null || _rectTransform == null)
            {
                if (onComplete != null)
                {
                    onComplete();
                }

                return;
            }

            StopPassiveEffects();
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
            }

            float now = _animationController.TimeNow;
            float endTime = now + 0.22f;
            Vector3 burstScale = Vector3.one * 1.08f;
            Vector3 squeezeScale = Vector3.one * 0.93f;

            SetOutlineAlpha(0.38f);
            _animationController.ScheduleShake(_rectTransform, now + 0.01f, 0.16f, 8f, 9);
            _animationController.ScheduleScale(_rectTransform, Vector3.one, burstScale, now, 0.07f, LudoEase.EaseOut);
            _animationController.ScheduleScale(_rectTransform, burstScale, squeezeScale, now + 0.07f, 0.07f, LudoEase.EaseOut);
            _animationController.ScheduleScale(_rectTransform, squeezeScale, Vector3.one, now + 0.14f, 0.08f, LudoEase.EaseOutBack);
            _animationController.ScheduleCallback(endTime, RefreshStaticEffects);

            _animationController.ScheduleCallback(endTime, () =>
            {
                if (onComplete != null)
                {
                    onComplete();
                }
            });
        }

        public void PlayCaptureBurst()
        {
            if (_animationController == null || _rectTransform == null)
            {
                return;
            }

            float now = _animationController.TimeNow;
            Vector3 burstScale = Vector3.one * 1.18f;
            SetOutlineAlpha(0.34f);
            _animationController.ScheduleScale(_rectTransform, Vector3.one, burstScale, now, 0.06f, LudoEase.EaseOut);
            _animationController.ScheduleScale(_rectTransform, burstScale, Vector3.one, now + 0.06f, 0.12f, LudoEase.EaseOutBack, RefreshStaticEffects);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isSelectable && Clicked != null)
            {
                Clicked(this);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            RefreshPulseState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            RefreshPulseState();
        }

        private void StopPassiveEffects()
        {
            if (_pulseHandle != null)
            {
                _pulseHandle.Cancel();
                _pulseHandle = null;
            }

            if (_rectTransform != null)
            {
                _rectTransform.localScale = Vector3.one;
            }
        }

        private void RefreshPulseState()
        {
            if (_animationController == null || _rectTransform == null)
            {
                return;
            }

            StopPassiveEffects();
            RefreshStaticEffects();

            float amplitude = _isSelectable ? 0.030f : (_isHovered ? 0.022f : 0.012f);
            float duration = _isSelectable ? 1.12f : (_isHovered ? 1.28f : 2.80f);
            _pulseHandle = _animationController.ScheduleLoopPulse(_rectTransform, Vector3.one, amplitude, duration);
        }

        private void RefreshStaticEffects()
        {
            if (_tokenImage != null)
            {
                _tokenImage.color = _baseColor;
            }

            if (_shadowEffect != null)
            {
                _shadowEffect.effectColor = new Color(0f, 0f, 0f, _isSelectable ? 0.18f : 0.14f);
                _shadowEffect.effectDistance = _isHovered ? new Vector2(2.5f, -3.5f) : new Vector2(2f, -3f);
            }

            SetOutlineAlpha(_isSelectable ? 0.30f : (_isHovered ? 0.24f : 0.16f));
        }

        private void SetOutlineAlpha(float alpha)
        {
            if (_outlineEffect != null)
            {
                _outlineEffect.effectColor = LudoUtility.WithAlpha(OutlineBaseColor, alpha);
            }
        }
    }
}
