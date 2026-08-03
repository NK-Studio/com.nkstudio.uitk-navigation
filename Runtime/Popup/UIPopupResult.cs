namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Identifies why a popup was closed.
    /// </summary>
    public enum UIPopupCloseReason
    {
        /// <summary>The popup closed after one of its actions was invoked.</summary>
        Action,
        /// <summary>The popup closed in response to a Back request.</summary>
        Back,
        /// <summary>The popup closed because its backdrop was selected.</summary>
        Backdrop,
        /// <summary>The popup was closed explicitly by application code.</summary>
        Programmatic,
        /// <summary>The popup closed because its host UI root was detached or reloaded.</summary>
        HostDetached
    }

    /// <summary>
    /// Describes the action and close reason produced when a popup completes.
    /// </summary>
    public readonly struct UIPopupResult
    {
        internal UIPopupResult(string actionId, UIPopupCloseReason reason)
        {
            ActionId = actionId ?? string.Empty;
            Reason = reason;
        }

        /// <summary>
        /// Gets the normalized identifier of the action that completed the popup, or an empty string.
        /// </summary>
        public string ActionId { get; }
        /// <summary>
        /// Gets the reason the popup was closed.
        /// </summary>
        public UIPopupCloseReason Reason { get; }
    }
}
