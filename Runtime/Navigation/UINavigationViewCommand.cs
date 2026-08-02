using System;
using NKStudio.UITKNavigation.Identity;
using UnityEngine;

namespace NKStudio.UITKNavigation.Navigation
{
    internal enum UIViewTransitionMode
    {
        Animated,
        Instant
    }

    internal enum UIToggleOutputCondition
    {
        On,
        Off,
        Any
    }

    /// <summary>
    /// Identifies the view visibility event that can trigger a navigation transition.
    /// </summary>
    public enum UIViewOutputCondition
    {
        /// <summary>Triggers when a matching view starts to show.</summary>
        Show,
        /// <summary>Triggers when a matching view starts to hide.</summary>
        Hide
    }

    [Serializable]
    internal struct UINavigationViewCommand
    {
        [SerializeField]
        private UIKey view;

        [SerializeField]
        private UIViewTransitionMode mode;

        public UINavigationViewCommand(
            UIKey view,
            UIViewTransitionMode mode = UIViewTransitionMode.Animated)
        {
            this.view = view;
            this.mode = mode;
        }

        public UIKey View => view;
        public UIViewTransitionMode Mode => mode;
        public bool IsValid => view.IsValid;
        public bool IsInstant => mode == UIViewTransitionMode.Instant;
    }
}
