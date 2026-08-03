using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Represents UI Animation Binding data.
    /// </summary>
    internal readonly struct UIAnimationBinding
    {
        /// <summary>
        /// Gets the element.
        /// </summary>
        public readonly VisualElement Element;

        /// <summary>
        /// Gets the animation.
        /// </summary>
        public readonly UIAnimation Animation;

        /// <summary>
        /// Initializes a new instance of <see cref="UIAnimationBinding"/>.
        /// </summary>
        public UIAnimationBinding(VisualElement element, UIAnimation animation)
        {
            Element = element;
            Animation = animation;
        }

        /// <summary>
        /// Gets a value indicating whether valid.
        /// </summary>
        public bool IsValid => Element != null && Animation != null;

        /// <summary>
        /// Gets the total duration.
        /// </summary>
        public float TotalDuration => Animation != null ? Animation.TotalDuration : 0f;

        /// <summary>
        /// Performs the prepare operation.
        /// </summary>
        public void Prepare()
        {
            Animation?.Prepare(Element);
        }

        /// <summary>
        /// Applies a t.
        /// </summary>
        public void ApplyAt(float time)
        {
            if (Animation != null)
                Animation.ApplyAt(Element, time);
        }

        /// <summary>
        /// Clears s ty le s.
        /// </summary>
        public void ClearStyles(bool forceClearOpacity)
        {
            bool keepOpacity = !forceClearOpacity
                               && Animation != null
                               && Animation.Fade.Enabled
                               && !Animation.Fade.RestIsOpaque;

            UIAnimation.ClearStyles(Element, keepOpacity);
        }
    }
}
