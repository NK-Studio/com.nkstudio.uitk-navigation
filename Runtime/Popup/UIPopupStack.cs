using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Properties;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Provides UI Popup Stack functionality.
    /// </summary>
    internal sealed class UIPopupStack
    {
        private sealed class Instance
        {
            private readonly UIPopupStack _owner;
            private Action _cancelOnUi;

            internal Instance(UIPopupStack owner, UIPopupHandle handle)
            {
                _owner = owner;
                Handle = handle;
                CloseCallback = RequestClose;
                HideFinishedCallback = HandleHideFinished;
            }

            internal readonly UIPopupHandle Handle;
            internal TemplateContainer Container;
            internal UIPopupView View;
            internal VisualElement PreviousFocus;
            internal CancellationTokenRegistration Cancellation;
            internal CancellationToken CancellationToken;
            internal UIPopupResult Result;
            internal bool Closing;
            internal bool Cancelled;

            /// <summary>The single close delegate shared by the view and the handle.</summary>
            internal readonly Func<UIPopupCloseReason, string, bool> CloseCallback;

            /// <summary>The Hide completion delegate kept alongside this instance.</summary>
            internal readonly Action HideFinishedCallback;

            internal void RequestCancel() => _owner.PostToUi(_cancelOnUi ??= CancelOnUi);

            private bool RequestClose(UIPopupCloseReason reason, string actionId) => _owner.Close(this, reason, actionId, false);

            private void HandleHideFinished() => _owner.FinalizeInstance(this, Cancelled, CancellationToken);

            private void CancelOnUi() => _owner.Cancel(this, CancellationToken);
        }

        private readonly VisualElement _parent;
        private readonly VisualElement _layer;
        private readonly List<Instance> _instances = new();
        private readonly SynchronizationContext _uiContext;
        private readonly int _uiThreadId;
        private bool _disposed;

        /// <summary>Raised after a popup has been pushed and its Show transition has started.</summary>
        internal Action<UIPopupHandle> Opened;

        /// <summary>Raised after a popup has been fully removed and its completion task resolved.</summary>
        internal Action<UIPopupHandle, UIPopupResult> Closed;

        /// <summary>Raised instead of <see cref="Closed"/> when a popup ended through cancellation.</summary>
        internal Action<UIPopupHandle> Canceled;

        internal UIPopupStack(VisualElement parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _uiContext = SynchronizationContext.Current;
            _uiThreadId = Thread.CurrentThread.ManagedThreadId;
            _layer = new VisualElement
            {
                name = "ui-popup-layer",
                pickingMode = PickingMode.Ignore
            };
            _layer.AddToClassList("ui-popup-layer");
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0f;
            _layer.style.right = 0f;
            _layer.style.top = 0f;
            _layer.style.bottom = 0f;
            _parent.Add(_layer);
            _layer.BringToFront();
        }

        internal int Count => _instances.Count;
        internal UIPopupHandle Top => _instances.Count == 0
            ? null
            : _instances[_instances.Count - 1].Handle;

        internal UIPopupHandle Show(VisualTreeAsset template, object dataSource = null, Action<UIPopupView> configure = null, CancellationToken cancellationToken = default) =>
            ShowWithHandle(template, dataSource, configure == null ? null : (view, _) => configure(view), cancellationToken);

        internal UIPopupHandle ShowWithHandle(VisualTreeAsset template, object dataSource, Action<UIPopupView, UIPopupHandle> configure, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (template == null)
                throw new ArgumentNullException(nameof(template));

            var handle = new UIPopupHandle(dataSource);
            if (cancellationToken.IsCancellationRequested)
            {
                handle.Cancel(cancellationToken);
                return handle;
            }

            TemplateContainer container = template.Instantiate();
            ConfigureContainer(container);
            UIPopupView view = RequireSingleView(container);
            RequireContentAndBackdrop(view, out UIPopupContent content, out UIPopupBackdrop backdrop);

            view.dataSource = dataSource;
            view.dataSourceType = dataSource?.GetType();
            try
            {
                configure?.Invoke(view, handle);
            }
            catch
            {
                view.dataSource = null;
                view.dataSourceType = null;
                throw;
            }

            var instance = new Instance(this, handle)
            {
                Container = container,
                View = view,
                PreviousFocus = _layer.panel?.focusController?.focusedElement as VisualElement,
                CancellationToken = cancellationToken
            };

            try
            {
                view.InitializeRuntime(backdrop, content, instance.CloseCallback, handle.RaiseAction);
                handle.Bind(view, instance.CloseCallback);

                view.Visibility.HideFinished += instance.HideFinishedCallback;
                _layer.Add(container);

                // A binding can be created while the cloned tree is still detached from a panel.
                // Re-notify after attachment and perform the initial ToTarget copy immediately.
                // The engine binding remains installed for later source-driven updates.
                object configuredDataSource = view.dataSource;
                view.dataSource = null;
                view.dataSource = configuredDataSource;
                RefreshBindings(view);

                _instances.Add(instance);
                _layer.pickingMode = PickingMode.Position;
                _layer.BringToFront();
                RefreshBackPolicies();

                if (cancellationToken.CanBeCanceled)
                    instance.Cancellation = cancellationToken.Register(instance.RequestCancel);

                if (handle.IsOpen)
                    view.ShowRuntime();
            }
            catch (Exception exception)
            {
                RollbackShow(instance, exception);
                throw;
            }

            Opened?.Invoke(handle);
            return handle;
        }

        internal bool CloseTop(UIPopupCloseReason reason = UIPopupCloseReason.Programmatic)
        {
            if (_instances.Count == 0)
                return false;

            return Close(_instances[_instances.Count - 1], reason, string.Empty, false);
        }

        internal void CloseAll(UIPopupCloseReason reason = UIPopupCloseReason.Programmatic, bool instant = false)
        {
            Instance[] snapshot = _instances.ToArray();
            for (int i = snapshot.Length - 1; i >= 0; i--)
                Close(snapshot[i], reason, string.Empty, instant);
        }

        internal void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            CloseAll(UIPopupCloseReason.HostDetached, true);
            _layer.RemoveFromHierarchy();
        }

        private bool Close(Instance instance, UIPopupCloseReason reason, string actionId, bool instant)
        {
            if (instance == null || instance.Closing || !_instances.Contains(instance))
                return false;

            instance.Closing = true;
            instance.Result = new UIPopupResult(actionId, reason);
            if (instant)
            {
                instance.View.InstantHideRuntime();
                FinalizeInstance(instance, false, default);
            }
            else
            {
                instance.View.HideRuntime();
            }

            return true;
        }

        private void Cancel(Instance instance, CancellationToken cancellationToken)
        {
            if (instance == null || instance.Closing || !_instances.Contains(instance))
                return;

            instance.Closing = true;
            instance.Cancelled = true;
            instance.CancellationToken = cancellationToken;
            instance.View.InstantHideRuntime();
            FinalizeInstance(instance, true, cancellationToken);
        }

        private void FinalizeInstance(Instance instance, bool cancelled, CancellationToken cancellationToken)
        {
            int index = _instances.IndexOf(instance);
            if (index < 0)
                return;

            bool wasTop = index == _instances.Count - 1;
            _instances.RemoveAt(index);
            if (!cancelled)
                instance.Cancellation.Dispose();
            instance.View.dataSource = null;
            instance.View.dataSourceType = null;
            instance.View.DisposeRuntime();
            instance.Container.RemoveFromHierarchy();

            if (cancelled)
                instance.Handle.Cancel(cancellationToken);
            else
                instance.Handle.Complete(instance.Result);

            _layer.pickingMode = _instances.Count > 0
                ? PickingMode.Position
                : PickingMode.Ignore;
            RefreshBackPolicies();

            if (wasTop)
            {
                if (instance.PreviousFocus?.panel != null && instance.PreviousFocus.enabledInHierarchy)
                    instance.PreviousFocus.Focus();
                else if (_instances.Count > 0)
                    _instances[_instances.Count - 1].View.schedule.Execute(_instances[_instances.Count - 1].View.FocusRuntime);
            }

            // Raised last so a handler that opens a replacement popup is not overridden
            // by the focus restore above.
            if (cancelled)
                Canceled?.Invoke(instance.Handle);
            else
                Closed?.Invoke(instance.Handle, instance.Result);
        }

        private void RollbackShow(Instance instance, Exception exception)
        {
            bool removed = _instances.Remove(instance);
            instance.Cancellation.Dispose();
            instance.View.dataSource = null;
            instance.View.dataSourceType = null;
            instance.View.DisposeRuntime();
            instance.Container.RemoveFromHierarchy();
            instance.Handle.Fail(exception);

            if (!removed)
                return;

            _layer.pickingMode = _instances.Count > 0
                ? PickingMode.Position
                : PickingMode.Ignore;
            RefreshBackPolicies();
        }

        private void RefreshBackPolicies()
        {
            for (int i = 0; i < _instances.Count; i++)
                _instances[i].View.SetTopmost(i == _instances.Count - 1 && !_instances[i].Closing);
        }

        private void PostToUi(Action action)
        {
            if (_uiContext == null || Thread.CurrentThread.ManagedThreadId == _uiThreadId)
                action();
            else
                _uiContext.Post(static state => ((Action)state)(), action);
        }

        private static void ConfigureContainer(TemplateContainer container)
        {
            container.style.position = Position.Absolute;
            container.style.left = 0f;
            container.style.right = 0f;
            container.style.top = 0f;
            container.style.bottom = 0f;
        }

        private static void RefreshBindings(VisualElement element)
        {
            List<BindingInfo> bindings = ListPool<BindingInfo>.Get();
            try
            {
                RefreshBindings(element, bindings);
            }
            finally
            {
                ListPool<BindingInfo>.Release(bindings);
            }
        }

        private static void RefreshBindings(VisualElement element, List<BindingInfo> bindings)
        {
            bindings.Clear();
            element.GetBindingInfos(bindings);
            for (int i = 0; i < bindings.Count; i++)
                ApplyInitialToTarget(element, bindings[i]);

            for (int i = 0; i < element.hierarchy.childCount; i++)
                RefreshBindings(element.hierarchy[i], bindings);
        }

        private static void ApplyInitialToTarget(VisualElement targetElement, BindingInfo bindingInfo)
        {
            if (bindingInfo.binding is not DataBinding dataBinding ||
                dataBinding.bindingMode == BindingMode.ToSource)
                return;

            DataSourceContext context;
            try
            {
                context = targetElement.GetDataSourceContext(bindingInfo.bindingId);
            }
            catch (ArgumentException)
            {
                // Some custom panels activate binding requests on their first update.
                // In that case the engine performs the initial copy normally.
                return;
            }

            object source = dataBinding.dataSource ?? context.dataSource;
            if (source == null)
                return;

            PropertyPath sourcePath = context.dataSourcePath.IsEmpty
                ? dataBinding.dataSourcePath
                : context.dataSourcePath;
            if (!PropertyContainer.TryGetValue(source, sourcePath, out object value))
                return;

            object boxedTarget = targetElement;
            PropertyPath targetPath = bindingInfo.bindingId;
            dataBinding.sourceToUiConverters.TrySetValue(ref boxedTarget, targetPath, value, out _);
        }

        private static UIPopupView RequireSingleView(VisualElement root)
        {
            UIPopupView view = null;
            int count = 0;
            CollectViews(root, ref view, ref count);
            if (count != 1)
                throw new InvalidOperationException($"[UIPopup] 템플릿에는 UIPopupView가 정확히 하나 필요합니다. 현재 개수: {count}");

            return view;
        }

        // Both markers are collected in one descent so a popup template is walked once
        // instead of once per marker type.
        private static void RequireContentAndBackdrop(UIPopupView view, out UIPopupContent content, out UIPopupBackdrop backdrop)
        {
            content = null;
            backdrop = null;
            int contentCount = 0;
            int backdropCount = 0;
            CollectMarkers(view, ref content, ref contentCount, ref backdrop, ref backdropCount);

            if (contentCount != 1)
                throw new InvalidOperationException($"[UIPopup] UIPopupView에는 UIPopupContent가 정확히 하나 필요합니다. 현재 개수: {contentCount}");

            if (backdropCount > 1)
                throw new InvalidOperationException($"[UIPopup] UIPopupView에는 UIPopupBackdrop을 하나만 둘 수 있습니다. 현재 개수: {backdropCount}");
        }

        private static void CollectViews(VisualElement element, ref UIPopupView view, ref int count)
        {
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                VisualElement child = element.hierarchy[i];
                if (child is UIPopupView match)
                {
                    view ??= match;
                    count++;
                }

                CollectViews(child, ref view, ref count);
            }
        }

        private static void CollectMarkers(VisualElement element, ref UIPopupContent content, ref int contentCount, ref UIPopupBackdrop backdrop, ref int backdropCount)
        {
            for (int i = 0; i < element.hierarchy.childCount; i++)
            {
                VisualElement child = element.hierarchy[i];
                switch (child)
                {
                    case UIPopupContent matchedContent:
                        content ??= matchedContent;
                        contentCount++;
                        break;
                    case UIPopupBackdrop matchedBackdrop:
                        backdrop ??= matchedBackdrop;
                        backdropCount++;
                        break;
                }

                CollectMarkers(child, ref content, ref contentCount, ref backdrop, ref backdropCount);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(UIPopupStack));
        }
    }
}
