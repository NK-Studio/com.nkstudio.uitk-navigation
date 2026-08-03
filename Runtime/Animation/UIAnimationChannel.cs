using System;
using LitMotion;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Animation Channel functionality.
    /// </summary>
    [Serializable]
    internal abstract class UIAnimationChannel
    {
        [SerializeField]
        private bool enabled;

        [SerializeField]
        private UIAnimationEaseType easeType = UIAnimationEaseType.Ease;

        [SerializeField]
        private Ease ease = Ease.InOutSine;

        [SerializeField]
        private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField, Min(0f)]
        private float startDelay;

        [SerializeField, Min(0f)]
        private float duration = 0.3f;

        [SerializeField]
        private UIAnimationPlayMode playMode = UIAnimationPlayMode.Normal;

        [SerializeField]
        private int loops;

        /// <summary>
        /// Gets the enabled.
        /// </summary>
        public bool Enabled
        {
            get => enabled;
            set => enabled = value;
        }

        /// <summary>
        /// Gets the ease type.
        /// </summary>
        public UIAnimationEaseType EaseType
        {
            get => easeType;
            set => easeType = value;
        }

        /// <summary>
        /// Gets the ease.
        /// </summary>
        public Ease Ease
        {
            get => ease;
            set => ease = value;
        }

        /// <summary>
        /// Gets the curve.
        /// </summary>
        public AnimationCurve Curve
        {
            get => curve;
            set => curve = value;
        }

        /// <summary>
        /// Gets the start delay.
        /// </summary>
        public float StartDelay
        {
            get => startDelay;
            set => startDelay = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Gets the duration.
        /// </summary>
        public float Duration
        {
            get => duration;
            set => duration = Mathf.Max(0f, value);
        }

        /// <summary>How this channel travels between its From and To values.</summary>
        public UIAnimationPlayMode PlayMode
        {
            get => playMode;
            set => playMode = value;
        }

        /// <summary>Additional cycles. Zero plays once; -1 repeats forever.</summary>
        public int Loops
        {
            get => loops;
            set => loops = Mathf.Max(-1, value);
        }

        /// <summary>Infinite loops always use Ping Pong to avoid an endpoint cut.</summary>
        public UIAnimationPlayMode EffectivePlayMode =>
            loops < 0 ? UIAnimationPlayMode.PingPong : playMode;

        public bool IsInfinite => loops < 0;

        /// <summary>Total finite duration. Infinite channels report one repeat period.</summary>
        public float TotalDuration =>
            startDelay + duration * (loops < 0 ? 1 : loops + 1);
        /// <summary>
        /// Performs the evaluate at operation.
        /// </summary>
        public float EvaluateAt(float time)
        {
            if (time < startDelay)
                return 0f;

            UIAnimationPlayMode mode = EffectivePlayMode;
            if (duration <= 0f)
                return mode == UIAnimationPlayMode.Normal ? 1f : 0f;

            float elapsed = Mathf.Max(0f, time - startDelay);
            int cycleCount = loops < 0 ? 1 : loops + 1;
            float finiteDuration = duration * cycleCount;
            if (loops >= 0 && elapsed >= finiteDuration)
                return mode == UIAnimationPlayMode.Normal ? 1f : 0f;

            float local = Mathf.Repeat(elapsed, duration) / duration;
            switch (mode)
            {
                case UIAnimationPlayMode.PingPong:
                    return local <= 0.5f
                        ? EvaluateEase(local * 2f)
                        : EvaluateEase((1f - local) * 2f);

                case UIAnimationPlayMode.Spring:
                    return Mathf.Sin(local * Mathf.PI * 5f) * (1f - local);

                case UIAnimationPlayMode.Shake:
                    return EvaluateShake(local);

                default:
                    return EvaluateEase(local);
            }
        }

        private float EvaluateEase(float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (easeType == UIAnimationEaseType.AnimationCurve)
                return curve != null && curve.length > 0 ? curve.Evaluate(progress) : progress;

            return EaseUtility.Evaluate(progress, ease);
        }

        private static float EvaluateShake(float progress)
        {
            const int segments = 10;
            if (progress <= 0f || progress >= 1f)
                return 0f;

            float scaled = progress * segments;
            int index = Mathf.FloorToInt(scaled);
            float t = Mathf.SmoothStep(0f, 1f, scaled - index);
            float from = index == 0 ? 0f : ShakeValue(index);
            float to = index >= segments - 1 ? 0f : ShakeValue(index + 1);
            return Mathf.LerpUnclamped(from, to, t) * (1f - progress * 0.35f);
        }

        private static float ShakeValue(int index)
        {
            uint value = (uint)index * 747796405u + 2891336453u;
            value = (value >> ((int)(value >> 28) + 4)) ^ value;
            value *= 277803737u;
            value = (value >> 22) ^ value;
            return (value / (float)uint.MaxValue) * 2f - 1f;
        }
        /// <summary>
        /// Performs the prepare operation.
        /// </summary>
        public virtual void Prepare(VisualElement target, UIAnimationType type)
        {
        }

        /// <summary>
        /// Applies a t.
        /// </summary>
        public abstract void ApplyAt(VisualElement target, UIAnimationType type, float time);

        /// <summary>
        /// Resets s ty le.
        /// </summary>
        public abstract void ResetStyle(VisualElement target);

        /// <summary>
        /// Performs the clone operation.
        /// </summary>
        public abstract UIAnimationChannel Clone();

        /// <summary>
        /// Performs the copy shared to operation.
        /// </summary>
        protected void CopySharedTo(UIAnimationChannel other)
        {
            other.enabled = enabled;
            other.easeType = easeType;
            other.ease = ease;
            other.curve = curve != null ? new AnimationCurve(curve.keys) : null;
            other.startDelay = startDelay;
            other.duration = duration;
            other.playMode = playMode;
            other.loops = loops;
        }
    }
}
