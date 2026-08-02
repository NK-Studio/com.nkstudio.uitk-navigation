namespace NKStudio.UITKNavigation.Identity
{
    /// <summary>
    /// Defines the contract for IUI Visible View.
    /// </summary>
    public interface IUIVisibleView
    {
        /// <summary>
        /// Gets a value indicating whether visible.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Shows member.
        /// </summary>
        void Show();

        /// <summary>
        /// Hides member.
        /// </summary>
        void Hide();

        /// <summary>
        /// Performs the instant show operation.
        /// </summary>
        void InstantShow();

        /// <summary>
        /// Performs the instant hide operation.
        /// </summary>
        void InstantHide();
    }
}
