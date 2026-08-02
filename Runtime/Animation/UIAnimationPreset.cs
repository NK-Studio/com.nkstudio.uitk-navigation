using UnityEngine;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Animation Preset functionality.
    /// </summary>
    [CreateAssetMenu(menuName = "UITK Navigation/UI Animation Preset", fileName = "UIAnimationPreset")]
    public sealed class UIAnimationPreset : ScriptableObject
    {
        [SerializeField]
        private UIAnimation showAnimation = new UIAnimation();

        [SerializeField]
        private UIAnimation hideAnimation = new UIAnimation();

        private void OnValidate()
        {
            showAnimation.Type = UIAnimationType.Show;
            hideAnimation.Type = UIAnimationType.Hide;
        }

        /// <summary>
        /// Gets the show.
        /// </summary>
        internal UIAnimation GetShow()
        {
            UIAnimation clone = showAnimation.Clone();
            clone.Type = UIAnimationType.Show;
            return clone;
        }

        /// <summary>
        /// Gets the hide.
        /// </summary>
        internal UIAnimation GetHide()
        {
            UIAnimation clone = hideAnimation.Clone();
            clone.Type = UIAnimationType.Hide;
            return clone;
        }

        /// <summary>
        /// Sets a ni ma ti on s.
        /// </summary>
        internal void SetAnimations(UIAnimation show, UIAnimation hide)
        {
            if (show != null)
            {
                showAnimation = show;
                showAnimation.Type = UIAnimationType.Show;
            }

            if (hide != null)
            {
                hideAnimation = hide;
                hideAnimation.Type = UIAnimationType.Hide;
            }
        }
    }
}
