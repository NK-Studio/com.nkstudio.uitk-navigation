using System;
using NKStudio.UITKNavigation.Identity;

namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Sends navigation signals to the active navigator.
    /// </summary>
    public static class Signal
    {
        /// <summary>
        /// Sends a navigation signal using category and name components.
        /// </summary>
        /// <param name="category">The signal category.</param>
        /// <param name="name">The signal name.</param>
        /// <returns><see langword="true"/> when the signal was handled or queued; otherwise, <see langword="false"/>.</returns>
        public static bool Send(string category, string name)
        {
            return Send(new UIKey(category, name));
        }

        /// <summary>
        /// Sends a graph-local custom destination signal.
        /// </summary>
        public static bool Send(string destinationKey)
        {
            if (string.IsNullOrWhiteSpace(destinationKey))
                return false;

            UINavigationService service = UINavigatorBehaviour.Active?.Service;
            return service != null && service.Trigger(destinationKey);
        }

        /// <summary>
        /// Sends a navigation signal to the active navigator.
        /// </summary>
        /// <param name="signal">The signal to send.</param>
        /// <returns><see langword="true"/> when the signal was handled or queued; otherwise, <see langword="false"/>.</returns>
        public static bool Send(UIKey signal)
        {
            if (!signal.IsValid)
                return false;

            UINavigationService service = UINavigatorBehaviour.Active?.Service;
            return service != null && service.Trigger(signal);
        }
    }
}
