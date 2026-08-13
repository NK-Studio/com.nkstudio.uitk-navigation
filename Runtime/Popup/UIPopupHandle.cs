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
        private enum CompletionState
        {
            Pending,
            Completed,
            Canceled,
            Failed
        }

        // Created on the first Completion access so event-driven callers never pay for the task.
        private TaskCompletionSource<UIPopupResult> _completion;
        private CompletionState _state = CompletionState.Pending;
        private UIPopupResult _result;
        private CancellationToken _cancellationToken;
        private Exception _exception;
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
        public Task<UIPopupResult> Completion => EnsureCompletion().Task;
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
            if (_state != CompletionState.Pending)
                return;

            _state = CompletionState.Completed;
            _result = result;
            _completion?.TrySetResult(result);
        }

        internal void Cancel(CancellationToken cancellationToken)
        {
            Unbind();
            if (_state != CompletionState.Pending)
                return;

            _state = CompletionState.Canceled;
            _cancellationToken = cancellationToken.IsCancellationRequested
                ? cancellationToken
                : new CancellationToken(true);
            _completion?.TrySetCanceled(_cancellationToken);
        }

        internal void Fail(Exception exception)
        {
            Unbind();
            if (_state != CompletionState.Pending)
                return;

            _state = CompletionState.Failed;
            _exception = exception;
            _completion?.TrySetException(exception);
        }

        private void Unbind()
        {
            IsOpen = false;
            View = null;
            _close = null;
        }

        /// <summary>
        /// Returns the completion source, replaying an already recorded outcome onto a
        /// source that is created only when a caller actually awaits the popup.
        /// </summary>
        private TaskCompletionSource<UIPopupResult> EnsureCompletion()
        {
            if (_completion != null)
                return _completion;

            _completion = new TaskCompletionSource<UIPopupResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            switch (_state)
            {
                case CompletionState.Completed:
                    _completion.TrySetResult(_result);
                    break;
                case CompletionState.Canceled:
                    _completion.TrySetCanceled(_cancellationToken);
                    break;
                case CompletionState.Failed:
                    _completion.TrySetException(_exception);
                    break;
            }

            return _completion;
        }
    }
}
