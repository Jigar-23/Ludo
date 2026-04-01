using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PremiumLudo
{
    public enum LudoEase
    {
        Linear = 0,
        EaseInOut = 1,
        EaseOut = 2,
        EaseOutBack = 3,
        EaseOutQuad = 4,
    }

    public sealed class LudoAnimationHandle
    {
        internal bool Cancelled;

        public void Cancel()
        {
            Cancelled = true;
        }
    }

    public sealed class LudoAnimationController : MonoBehaviour
    {
        private interface IAnimationJob
        {
            LudoAnimationHandle Handle { get; }
            bool Tick(float timeNow);
        }

        private sealed class CallbackJob : IAnimationJob
        {
            private readonly float _startTime;
            private readonly Action _callback;
            private bool _fired;

            public CallbackJob(float startTime, Action callback, LudoAnimationHandle handle)
            {
                _startTime = startTime;
                _callback = callback;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled)
                {
                    return false;
                }

                if (_fired || timeNow < _startTime)
                {
                    return !_fired;
                }

                _fired = true;
                if (_callback != null)
                {
                    _callback();
                }

                return false;
            }
        }

        private sealed class FloatJob : IAnimationJob
        {
            private readonly UnityEngine.Object _guard;
            private readonly float _startTime;
            private readonly float _duration;
            private readonly float _from;
            private readonly float _to;
            private readonly LudoEase _ease;
            private readonly Action<float> _setter;
            private readonly Action _onComplete;
            private bool _complete;

            public FloatJob(UnityEngine.Object guard, float startTime, float duration, float from, float to, LudoEase ease, Action<float> setter, Action onComplete, LudoAnimationHandle handle)
            {
                _guard = guard;
                _startTime = startTime;
                _duration = Mathf.Max(0.0001f, duration);
                _from = from;
                _to = to;
                _ease = ease;
                _setter = setter;
                _onComplete = onComplete;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _guard == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = Mathf.Clamp01((timeNow - _startTime) / _duration);
                float eased = EvaluateEase(_ease, normalized);
                _setter(Mathf.LerpUnclamped(_from, _to, eased));

                if (_complete || normalized < 1f)
                {
                    return true;
                }

                _complete = true;
                if (_onComplete != null)
                {
                    _onComplete();
                }

                return false;
            }
        }

        private sealed class Vector2Job : IAnimationJob
        {
            private readonly UnityEngine.Object _guard;
            private readonly float _startTime;
            private readonly float _duration;
            private readonly Vector2 _from;
            private readonly Vector2 _to;
            private readonly LudoEase _ease;
            private readonly Action<Vector2> _setter;
            private readonly Action _onComplete;
            private bool _complete;

            public Vector2Job(UnityEngine.Object guard, float startTime, float duration, Vector2 from, Vector2 to, LudoEase ease, Action<Vector2> setter, Action onComplete, LudoAnimationHandle handle)
            {
                _guard = guard;
                _startTime = startTime;
                _duration = Mathf.Max(0.0001f, duration);
                _from = from;
                _to = to;
                _ease = ease;
                _setter = setter;
                _onComplete = onComplete;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _guard == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = Mathf.Clamp01((timeNow - _startTime) / _duration);
                float eased = EvaluateEase(_ease, normalized);
                _setter(Vector2.LerpUnclamped(_from, _to, eased));

                if (_complete || normalized < 1f)
                {
                    return true;
                }

                _complete = true;
                if (_onComplete != null)
                {
                    _onComplete();
                }

                return false;
            }
        }

        private sealed class ArcMoveJob : IAnimationJob
        {
            private readonly RectTransform _target;
            private readonly float _startTime;
            private readonly float _duration;
            private readonly Vector2 _from;
            private readonly Vector2 _control;
            private readonly Vector2 _to;
            private readonly LudoEase _ease;
            private readonly Action _onComplete;
            private bool _complete;

            public ArcMoveJob(RectTransform target, float startTime, float duration, Vector2 from, Vector2 control, Vector2 to, LudoEase ease, Action onComplete, LudoAnimationHandle handle)
            {
                _target = target;
                _startTime = startTime;
                _duration = Mathf.Max(0.0001f, duration);
                _from = from;
                _control = control;
                _to = to;
                _ease = ease;
                _onComplete = onComplete;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _target == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = Mathf.Clamp01((timeNow - _startTime) / _duration);
                float eased = EvaluateEase(_ease, normalized);
                _target.anchoredPosition = EvaluateQuadraticBezier(_from, _control, _to, eased);

                if (_complete || normalized < 1f)
                {
                    return true;
                }

                _complete = true;
                if (_onComplete != null)
                {
                    _onComplete();
                }

                return false;
            }
        }

        private sealed class Vector3Job : IAnimationJob
        {
            private readonly UnityEngine.Object _guard;
            private readonly float _startTime;
            private readonly float _duration;
            private readonly Vector3 _from;
            private readonly Vector3 _to;
            private readonly LudoEase _ease;
            private readonly Action<Vector3> _setter;
            private readonly Action _onComplete;
            private bool _complete;

            public Vector3Job(UnityEngine.Object guard, float startTime, float duration, Vector3 from, Vector3 to, LudoEase ease, Action<Vector3> setter, Action onComplete, LudoAnimationHandle handle)
            {
                _guard = guard;
                _startTime = startTime;
                _duration = Mathf.Max(0.0001f, duration);
                _from = from;
                _to = to;
                _ease = ease;
                _setter = setter;
                _onComplete = onComplete;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _guard == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = Mathf.Clamp01((timeNow - _startTime) / _duration);
                float eased = EvaluateEase(_ease, normalized);
                _setter(Vector3.LerpUnclamped(_from, _to, eased));

                if (_complete || normalized < 1f)
                {
                    return true;
                }

                _complete = true;
                if (_onComplete != null)
                {
                    _onComplete();
                }

                return false;
            }
        }

        private sealed class ColorJob : IAnimationJob
        {
            private readonly Graphic _target;
            private readonly float _startTime;
            private readonly float _duration;
            private readonly Color _from;
            private readonly Color _to;
            private readonly LudoEase _ease;
            private readonly Action _onComplete;
            private bool _complete;

            public ColorJob(Graphic target, float startTime, float duration, Color from, Color to, LudoEase ease, Action onComplete, LudoAnimationHandle handle)
            {
                _target = target;
                _startTime = startTime;
                _duration = Mathf.Max(0.0001f, duration);
                _from = from;
                _to = to;
                _ease = ease;
                _onComplete = onComplete;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _target == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = Mathf.Clamp01((timeNow - _startTime) / _duration);
                float eased = EvaluateEase(_ease, normalized);
                _target.color = Color.LerpUnclamped(_from, _to, eased);

                if (_complete || normalized < 1f)
                {
                    return true;
                }

                _complete = true;
                if (_onComplete != null)
                {
                    _onComplete();
                }

                return false;
            }
        }

        private sealed class ShakeJob : IAnimationJob
        {
            private readonly RectTransform _target;
            private readonly float _startTime;
            private readonly float _duration;
            private readonly float _amplitude;
            private readonly int _vibrato;
            private readonly Vector2 _basePosition;
            private readonly Action _onComplete;

            public ShakeJob(RectTransform target, float startTime, float duration, float amplitude, int vibrato, Vector2 basePosition, Action onComplete, LudoAnimationHandle handle)
            {
                _target = target;
                _startTime = startTime;
                _duration = Mathf.Max(0.0001f, duration);
                _amplitude = amplitude;
                _vibrato = Mathf.Max(2, vibrato);
                _basePosition = basePosition;
                _onComplete = onComplete;
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _target == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = Mathf.Clamp01((timeNow - _startTime) / _duration);
                float fade = 1f - normalized;
                float radians = normalized * _vibrato * Mathf.PI * 2f;
                Vector2 offset = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians * 0.73f) * 0.65f) * (_amplitude * fade);
                _target.anchoredPosition = _basePosition + offset;

                if (normalized < 1f)
                {
                    return true;
                }

                _target.anchoredPosition = _basePosition;
                if (_onComplete != null)
                {
                    _onComplete();
                }

                return false;
            }
        }

        private sealed class LoopScaleJob : IAnimationJob
        {
            private readonly Transform _target;
            private readonly float _startTime;
            private readonly Vector3 _baseScale;
            private readonly float _amplitude;
            private readonly float _cycleDuration;

            public LoopScaleJob(Transform target, float startTime, Vector3 baseScale, float amplitude, float cycleDuration, LudoAnimationHandle handle)
            {
                _target = target;
                _startTime = startTime;
                _baseScale = baseScale;
                _amplitude = amplitude;
                _cycleDuration = Mathf.Max(0.05f, cycleDuration);
                Handle = handle;
            }

            public LudoAnimationHandle Handle { get; private set; }

            public bool Tick(float timeNow)
            {
                if (Handle.Cancelled || _target == null)
                {
                    return false;
                }

                if (timeNow < _startTime)
                {
                    return true;
                }

                float normalized = (timeNow - _startTime) / _cycleDuration;
                float wave = Mathf.Sin(normalized * Mathf.PI * 2f) * _amplitude;
                _target.localScale = _baseScale * (1f + wave);
                return true;
            }
        }

        private readonly List<IAnimationJob> _jobs = new List<IAnimationJob>(128);
        private float _timeNow;

        public float TimeNow
        {
            get { return _timeNow; }
        }

        private void Update()
        {
            _timeNow += Time.unscaledDeltaTime;
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                if (!_jobs[i].Tick(_timeNow))
                {
                    _jobs.RemoveAt(i);
                }
            }
        }

        public AnimationSequence CreateSequence()
        {
            return new AnimationSequence(this, _timeNow);
        }

        public LudoAnimationHandle ScheduleCallback(float startTime, Action callback)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            _jobs.Add(new CallbackJob(startTime, callback, handle));
            return handle;
        }

        public LudoAnimationHandle Delay(float duration, Action callback)
        {
            return ScheduleCallback(_timeNow + Mathf.Max(0f, duration), callback);
        }

        public LudoAnimationHandle ScheduleMove(RectTransform target, Vector2 from, Vector2 to, float startTime, float duration, LudoEase ease, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new Vector2Job(target, startTime, duration, from, to, ease, value => target.anchoredPosition = value, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleArcMove(RectTransform target, Vector2 from, Vector2 control, Vector2 to, float startTime, float duration, LudoEase ease, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new ArcMoveJob(target, startTime, duration, from, control, to, ease, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleScale(Transform target, Vector3 from, Vector3 to, float startTime, float duration, LudoEase ease, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new Vector3Job(target, startTime, duration, from, to, ease, value => target.localScale = value, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleRotationZ(Transform target, float from, float to, float startTime, float duration, LudoEase ease, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new FloatJob(target, startTime, duration, from, to, ease, value =>
            {
                Vector3 current = target.localEulerAngles;
                current.z = value;
                target.localEulerAngles = current;
            }, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleGraphicColor(Graphic target, Color from, Color to, float startTime, float duration, LudoEase ease, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new ColorJob(target, startTime, duration, from, to, ease, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleCanvasGroupAlpha(CanvasGroup target, float from, float to, float startTime, float duration, LudoEase ease, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new FloatJob(target, startTime, duration, from, to, ease, value => target.alpha = value, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleShake(RectTransform target, float startTime, float duration, float amplitude, int vibrato, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new ShakeJob(target, startTime, duration, amplitude, vibrato, target.anchoredPosition, onComplete, handle));
            return handle;
        }

        public LudoAnimationHandle SchedulePulse(Transform target, Vector3 baseScale, float startTime, float duration, float amplitude, int cycles, Action onComplete = null)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            cycles = Mathf.Max(1, cycles);
            _jobs.Add(new FloatJob(target, startTime, duration, 0f, 1f, LudoEase.Linear, value =>
            {
                float wave = Mathf.Sin(value * cycles * Mathf.PI);
                target.localScale = baseScale * (1f + (wave * amplitude));
            }, () =>
            {
                if (target != null)
                {
                    target.localScale = baseScale;
                }

                if (onComplete != null)
                {
                    onComplete();
                }
            }, handle));
            return handle;
        }

        public LudoAnimationHandle ScheduleLoopPulse(Transform target, Vector3 baseScale, float amplitude, float cycleDuration)
        {
            LudoAnimationHandle handle = new LudoAnimationHandle();
            if (target == null)
            {
                handle.Cancel();
                return handle;
            }

            _jobs.Add(new LoopScaleJob(target, _timeNow, baseScale, amplitude, cycleDuration, handle));
            return handle;
        }

        private static float EvaluateEase(LudoEase ease, float value)
        {
            switch (ease)
            {
                case LudoEase.EaseInOut:
                    return value * value * (3f - (2f * value));
                case LudoEase.EaseOut:
                    return 1f - Mathf.Pow(1f - value, 3f);
                case LudoEase.EaseOutBack:
                    float c1 = 1.70158f;
                    float c3 = c1 + 1f;
                    float t = value - 1f;
                    return 1f + (c3 * t * t * t) + (c1 * t * t);
                case LudoEase.EaseOutQuad:
                    return 1f - ((1f - value) * (1f - value));
                default:
                    return value;
            }
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 from, Vector2 control, Vector2 to, float t)
        {
            float oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * from) + (2f * oneMinusT * t * control) + (t * t * to);
        }

        public sealed class AnimationSequence
        {
            private readonly LudoAnimationController _controller;
            private float _cursor;

            internal AnimationSequence(LudoAnimationController controller, float startTime)
            {
                _controller = controller;
                _cursor = startTime;
            }

            public float Cursor
            {
                get { return _cursor; }
            }

            public AnimationSequence Delay(float duration)
            {
                _cursor += Mathf.Max(0f, duration);
                return this;
            }

            public AnimationSequence Callback(Action callback)
            {
                _controller.ScheduleCallback(_cursor, callback);
                return this;
            }

            public AnimationSequence Move(RectTransform target, Vector2 from, Vector2 to, float duration, LudoEase ease, Action onComplete = null)
            {
                _controller.ScheduleMove(target, from, to, _cursor, duration, ease, onComplete);
                _cursor += duration;
                return this;
            }

            public AnimationSequence Scale(Transform target, Vector3 from, Vector3 to, float duration, LudoEase ease, Action onComplete = null)
            {
                _controller.ScheduleScale(target, from, to, _cursor, duration, ease, onComplete);
                _cursor += duration;
                return this;
            }

            public AnimationSequence RotateZ(Transform target, float from, float to, float duration, LudoEase ease, Action onComplete = null)
            {
                _controller.ScheduleRotationZ(target, from, to, _cursor, duration, ease, onComplete);
                _cursor += duration;
                return this;
            }
        }
    }
}
