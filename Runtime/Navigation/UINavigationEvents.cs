using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;

namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Provides UI Navigation Events functionality.
    /// </summary>
    public static class UINavigationEvents
    {
        /// <summary>
        /// Occurs when the back requested event is raised.
        /// </summary>
        public static event Action BackRequested;

        /// <summary>
        /// Occurs when the forward requested event is raised.
        /// </summary>
        public static event Action ForwardRequested;

        /// <summary>
        /// Occurs when the resync requested event is raised.
        /// </summary>
        public static event Action ResyncRequested;

        /// <summary>
        /// Occurs when the toggle requested event is raised.
        /// </summary>
        public static event Action<UIKey, bool> ToggleRequested;

        internal static event Action<UIKey, UIViewOutputCondition> ViewTransitionStarted;

        /// <summary>
        /// Occurs when the go to node requested event is raised.
        /// </summary>
        public static event Action<string> GoToNodeRequested;

        /// <summary>
        /// Occurs when the node changing event is raised.
        /// </summary>
        internal static event Action<UINavigationChange> NodeChanging;

        /// <summary>
        /// Occurs when the node changed event is raised.
        /// </summary>
        internal static event Action<UINavigationChange> NodeChanged;

        /// <summary>
        /// Occurs when the back unhandled event is raised.
        /// </summary>
        public static event Action BackUnhandled;

        /// <summary>
        /// Performs the request back operation.
        /// </summary>
        public static void RequestBack()
        {
            BackRequested?.Invoke();
        }

        /// <summary>
        /// Performs the request forward operation.
        /// </summary>
        public static void RequestForward()
        {
            ForwardRequested?.Invoke();
        }

        /// <summary>
        /// Performs the request resync operation.
        /// </summary>
        public static void RequestResync()
        {
            ResyncRequested?.Invoke();
        }

        /// <summary>
        /// Performs the request toggle operation.
        /// </summary>
        public static void RequestToggle(UIKey toggle, bool value)
        {
            if (toggle.IsValid)
                ToggleRequested?.Invoke(toggle, value);
        }

        /// <summary>
        /// Requests a toggle navigation transition using category and key components.
        /// </summary>
        /// <param name="category">The toggle category.</param>
        /// <param name="key">The toggle key.</param>
        /// <param name="value">The current toggle value.</param>
        public static void RequestToggle(string category, string key, bool value)
        {
            RequestToggle(new UIKey(category, key), value);
        }

        /// <summary>
        /// Performs the request go to node operation.
        /// </summary>
        public static void RequestGoToNode(string nodeId)
        {
            GoToNodeRequested?.Invoke(nodeId);
        }

        internal static void RaiseViewTransitionStarted(
            UIKey view,
            UIViewOutputCondition condition)
        {
            if (view.IsValid)
                ViewTransitionStarted?.Invoke(view, condition);
        }

        internal static void ApplyViewShow(IReadOnlyList<UINavigationViewCommand> commands)
        {
            if (commands == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                UINavigationViewCommand command = commands[i];
                if (command.IsValid)
                    UIViewRegistry.Show(command.View, command.IsInstant);
            }
        }

        internal static void ApplyViewHide(IReadOnlyList<UINavigationViewCommand> commands)
        {
            if (commands == null)
                return;

            for (int i = 0; i < commands.Count; i++)
            {
                UINavigationViewCommand command = commands[i];
                if (command.IsValid)
                    UIViewRegistry.Hide(command.View, command.IsInstant);
            }
        }

        internal static void ApplyViewResync(IReadOnlyList<UIKey> ids)
        {
            UIViewRegistry.ResyncTo(ids);
        }

        internal static void RaiseNodeChanging(UINavigationChange change)
        {
            NodeChanging?.Invoke(change);
        }

        internal static void RaiseNodeChanged(UINavigationChange change)
        {
            NodeChanged?.Invoke(change);
        }

        internal static void RaiseBackUnhandled()
        {
            BackUnhandled?.Invoke();
        }
    }
}
