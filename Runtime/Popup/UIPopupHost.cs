using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Owns a panel-local LIFO popup stack and instantiates popup templates on its UI root.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PanelRenderer))]
    public sealed class UIPopupHost : MonoBehaviour
    {
        [SerializeField] private PanelRenderer panelRenderer;

        private UIPopupStack _stack;

        /// <summary>
        /// Raised after a popup has been pushed onto this host and its Show transition has started.
        /// </summary>
        public event Action<UIPopupHandle> Opened;

        /// <summary>
        /// Raised after a popup has been fully removed and its completion task resolved.
        /// Not raised when a popup ends through cancellation; see <see cref="Canceled"/>.
        /// </summary>
        public event Action<UIPopupHandle, UIPopupResult> Closed;

        /// <summary>
        /// Raised when a popup ended through cancellation, which produces no <see cref="UIPopupResult"/>.
        /// </summary>
        public event Action<UIPopupHandle> Canceled;

        /// <summary>
        /// Gets whether the panel UI root is available, so <see cref="Show"/> will not throw.
        /// </summary>
        public bool IsReady => _stack != null;

        private void OnEnable()
        {
            if (panelRenderer == null)
                panelRenderer = GetComponent<PanelRenderer>();

            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            panelRenderer?.UnregisterUIReloadCallback(OnUIReload);
            _stack?.Dispose();
            _stack = null;
        }

        private void Start()
        {
            if (_stack != null || panelRenderer == null)
                return;

            // PanelRenderer invokes the reload callback as soon as its root joins a panel, and a
            // fresh registration replays that call when the root was already attached. Toggling the
            // component would recover the same way but rebuilds the entire panel visual tree.
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            _stack?.Dispose();
            _stack = null;
            if (root == null)
                return;

            // The stack is recreated on every panel reload, so the relays must be rewired here.
            _stack = new UIPopupStack(root)
            {
                Opened = handle => Opened?.Invoke(handle),
                Closed = (handle, result) => Closed?.Invoke(handle, result),
                Canceled = handle => Canceled?.Invoke(handle)
            };
        }

        /// <summary>
        /// Instantiates a popup template, pushes it onto this host, and starts its Show transition.
        /// </summary>
        /// <param name="template">The template containing exactly one <see cref="UIPopupView"/>.</param>
        /// <param name="dataSource">The optional object assigned as the popup root data source.</param>
        /// <param name="configure">An optional callback invoked before the popup is shown.</param>
        /// <param name="cancellationToken">A token that cancels the popup completion task.</param>
        /// <returns>A handle used to observe actions, close the popup, or await completion.</returns>
        /// <exception cref="InvalidOperationException">The panel root has not been initialized.</exception>
        public UIPopupHandle Show(
            VisualTreeAsset template,
            object dataSource = null,
            Action<UIPopupView> configure = null,
            CancellationToken cancellationToken = default)
        {
            if (_stack == null)
                throw new InvalidOperationException(
                    $"[UIPopupHost] '{name}'의 UI 루트가 아직 준비되지 않았습니다.");

            return _stack.Show(template, dataSource, configure, cancellationToken);
        }

        /// <summary>
        /// Instantiates a popup template and pushes it onto this host, exposing its handle to
        /// <paramref name="configure"/> so a caller can wire per-instance state before the popup is shown.
        /// </summary>
        /// <param name="template">The template containing exactly one <see cref="UIPopupView"/>.</param>
        /// <param name="dataSource">The optional object assigned as the popup root data source.</param>
        /// <param name="configure">An optional callback invoked with the view and its handle before the popup is shown.</param>
        /// <param name="cancellationToken">A token that cancels the popup completion task.</param>
        /// <returns>A handle used to observe actions, close the popup, or await completion.</returns>
        /// <exception cref="InvalidOperationException">The panel root has not been initialized.</exception>
        public UIPopupHandle ShowWithHandle(
            VisualTreeAsset template,
            object dataSource = null,
            Action<UIPopupView, UIPopupHandle> configure = null,
            CancellationToken cancellationToken = default)
        {
            if (_stack == null)
                throw new InvalidOperationException(
                    $"[UIPopupHost] '{name}'의 UI 루트가 아직 준비되지 않았습니다.");

            return _stack.ShowWithHandle(template, dataSource, configure, cancellationToken);
        }

        /// <summary>
        /// Shows a popup and returns the task that completes when it closes.
        /// </summary>
        /// <param name="template">The template containing exactly one <see cref="UIPopupView"/>.</param>
        /// <param name="dataSource">The optional object assigned as the popup root data source.</param>
        /// <param name="configure">An optional callback invoked before the popup is shown.</param>
        /// <param name="cancellationToken">A token that cancels the returned task.</param>
        /// <returns>The popup completion result.</returns>
        public Task<UIPopupResult> ShowAsync(
            VisualTreeAsset template,
            object dataSource = null,
            Action<UIPopupView> configure = null,
            CancellationToken cancellationToken = default) =>
            Show(template, dataSource, configure, cancellationToken).Completion;

        /// <summary>
        /// Closes the topmost popup using its configured Hide transition.
        /// </summary>
        /// <param name="reason">The reason reported by the popup result.</param>
        /// <returns><see langword="true"/> when a popup was available to close.</returns>
        public bool CloseTop(
            UIPopupCloseReason reason = UIPopupCloseReason.Programmatic) =>
            _stack != null && _stack.CloseTop(reason);

        /// <summary>
        /// Closes every popup in this host from top to bottom.
        /// </summary>
        /// <param name="reason">The reason reported by each popup result.</param>
        /// <param name="instant">Whether to skip Hide transitions.</param>
        public void CloseAll(
            UIPopupCloseReason reason = UIPopupCloseReason.Programmatic,
            bool instant = false) =>
            _stack?.CloseAll(reason, instant);
    }
}