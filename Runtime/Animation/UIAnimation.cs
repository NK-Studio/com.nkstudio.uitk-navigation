using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Animation functionality.
    /// </summary>
    [Serializable]
    internal sealed class UIAnimation
    {
        [SerializeField]
        private UIAnimationType type = UIAnimationType.Show;

        [SerializeField]
        private UIMoveAnimation move = new UIMoveAnimation();

        [SerializeField]
        private UIRotateAnimation rotate = new UIRotateAnimation();

        [SerializeField]
        private UIScaleAnimation scale = new UIScaleAnimation();

        [SerializeField]
        private UIFadeAnimation fade = new UIFadeAnimation();

        /// <summary>
        /// Gets the type.
        /// </summary>
        public UIAnimationType Type
        {
            get => type;
            set => type = value;
        }

        /// <summary>
        /// Gets the move.
        /// </summary>
        public UIMoveAnimation Move => move;

        /// <summary>
        /// Gets the rotate.
        /// </summary>
        public UIRotateAnimation Rotate => rotate;

        /// <summary>
        /// Gets the scale.
        /// </summary>
        public UIScaleAnimation Scale => scale;

        /// <summary>
        /// Gets the fade.
        /// </summary>
        public UIFadeAnimation Fade => fade;

        /// <summary>
        /// Performs the invalidate start values operation.
        /// </summary>
        public void InvalidateStartValues()
        {
            move.InvalidateStartValue();
        }

        /// <summary>
        /// Gets a value indicating whether s enabled channel.
        /// </summary>
        public bool HasEnabledChannel =>
            move.Enabled || rotate.Enabled || scale.Enabled || fade.Enabled;

        /// <summary>
        /// Gets the required usage hints.
        /// </summary>
        public UsageHints RequiredUsageHints
        {
            get
            {
                UsageHints hints = UsageHints.None;

                if (move.Enabled || rotate.Enabled || scale.Enabled)
                    hints |= UsageHints.DynamicTransform;

                if (fade.Enabled)
                    hints |= UsageHints.DynamicColor;

                return hints;
            }
        }

        /// <summary>True when any enabled channel repeats forever.</summary>
        public bool IsInfinite =>
            (move.Enabled && move.IsInfinite)
            || (rotate.Enabled && rotate.IsInfinite)
            || (scale.Enabled && scale.IsInfinite)
            || (fade.Enabled && fade.IsInfinite);

        /// <summary>
        /// Gets the total duration.
        /// </summary>
        public float TotalDuration
        {
            get
            {
                float total = 0f;
                if (move.Enabled)
                    total = Mathf.Max(total, move.TotalDuration);
                if (rotate.Enabled)
                    total = Mathf.Max(total, rotate.TotalDuration);
                if (scale.Enabled)
                    total = Mathf.Max(total, scale.TotalDuration);
                if (fade.Enabled)
                    total = Mathf.Max(total, fade.TotalDuration);
                return total;
            }
        }

        /// <summary>
        /// Performs the prepare operation.
        /// </summary>
        public void Prepare(VisualElement target)
        {
            if (target == null)
                return;

            if (rotate.Enabled)
                rotate.Prepare(target, type);
            if (scale.Enabled)
                scale.Prepare(target, type);
            if (fade.Enabled)
                fade.Prepare(target, type);
            if (move.Enabled)
            {
                Vector2 directionScale = scale.Enabled ? scale.DetachedScale : Vector2.one;
                float directionAngle = rotate.Enabled ? rotate.DetachedAngle : 0f;
                move.Prepare(target, type, directionScale, directionAngle);
            }
        }
        /// <summary>
        /// Applies a t.
        /// </summary>
        public void ApplyAt(VisualElement target, float time)
        {
            if (target == null)
                return;

            if (move.Enabled)
                move.ApplyAt(target, type, time);
            if (rotate.Enabled)
                rotate.ApplyAt(target, type, time);
            if (scale.Enabled)
                scale.ApplyAt(target, type, time);
            if (fade.Enabled)
                fade.ApplyAt(target, type, time);
        }

        /// <summary>
        /// Clears s ty le s.
        /// </summary>
        public static void ClearStyles(VisualElement target, bool keepOpacity)
        {
            if (target == null)
                return;

            target.style.translate = StyleKeyword.Null;
            target.style.rotate = StyleKeyword.Null;
            target.style.scale = StyleKeyword.Null;

            if (!keepOpacity)
                target.style.opacity = StyleKeyword.Null;
        }

        /// <summary>
        /// Performs the clone operation.
        /// </summary>
        public UIAnimation Clone()
        {
            return new UIAnimation
            {
                type = type,
                move = (UIMoveAnimation)move.Clone(),
                rotate = (UIRotateAnimation)rotate.Clone(),
                scale = (UIScaleAnimation)scale.Clone(),
                fade = (UIFadeAnimation)fade.Clone()
            };
        }
    }
}
