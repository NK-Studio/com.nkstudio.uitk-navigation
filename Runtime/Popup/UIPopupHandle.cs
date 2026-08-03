using System;
using System.Threading;
using System.Threading.Tasks;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Provides UI Popup Handle functionality.
    /// </summary>
    public sealed class UIPopupHandle
    {
        private readonly TaskCompletionSource<UIPopupResult> _completion =
            new TaskCompletionSource<UIPopupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<UIPopupCloseReason, string, bool> _close;

        internal UIPopupHandle(object dataSource)
        {
            DataSource = dataSource;
        }

        internal UIPopupView View { get; private set; }
        internal object DataSource { get; }
        /// <summary>
        /// Gets whether the popup is currently attached to its host stack.
        /// </summary>
        public bool IsOpen { get; private set; }
        /// <summary>
        /// Gets the task that completes when the popup closes, is canceled, or fails.
        /// </summary>
        public Task<UIPopupResult> Completion => _completion.Task;
        /// <summary>
        /// Occurs when an action button in the popup is invoked, before optional popup closure.
        /// </summary>
        public event Action<string> ActionInvoked;

        /// <summary>
        /// Requests that this popup close with the supplied result information.
        /// </summary>
        /// <param name="reason">The reason reported by <see cref="Completion"/>.</param>
        /// <param name="actionId">The optional action identifier reported by the result.</param>
        /// <returns><see langword="true"/> when the popup was open and accepted the close request.</returns>
        public bool Close(
            UIPopupCloseReason reason = UIPopupCloseReason.Programmatic,
            string actionId = "")
        {
            return _close?.Invoke(reason, actionId) ?? false;
        }

        internal void Bind(
            UIPopupView view,
            Func<UIPopupCloseReason, string, bool> close)
        {
            View = view;
            _close = close;
            IsOpen = true;
        }

        internal void RaiseAction(string actionId)
        {
            ActionInvoked?.Invoke(actionId ?? string.Empty);
        }

        internal void Complete(UIPopupResult result)
        {
            Unbind();
            _completion.TrySetResult(result);
        }

        internal void Cancel(CancellationToken cancellationToken)
        {
            Unbind();
            _completion.TrySetCanceled(cancellationToken);
        }

        private void Unbind()
        {
            IsOpen = false;
            View = null;
            _close = null;
        }

        internal void Fail(Exception exception)
        {
            Unbind();
            _completion.TrySetException(exception);
        }
    }
}
