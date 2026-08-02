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
            if (_stack == null && panelRenderer != null)
            {
                panelRenderer.enabled = false;
                panelRenderer.enabled = true;
            }
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement root, int version)
        {
            _stack?.Dispose();
            _stack = null;
            if (root == null)
                return;

            _stack = new UIPopupStack(root);
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