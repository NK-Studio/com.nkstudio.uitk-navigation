using System;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Transition Set functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UITransitionSet
    {
        [UxmlObjectReference("show")]
        public UITransitionSettings Show { get; set; } = new UITransitionSettings();

        [UxmlObjectReference("hide")]
        public UITransitionSettings Hide { get; set; } = new UITransitionSettings();

        /// <summary>
        /// Builds s ho w.
        /// </summary>
        internal UIAnimation BuildShow() => Show?.BuildAnimation(UIAnimationType.Show);

        /// <summary>
        /// Builds h id e.
        /// </summary>
        internal UIAnimation BuildHide() => Hide?.BuildAnimation(UIAnimationType.Hide);
    }
}
