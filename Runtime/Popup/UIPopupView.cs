using System;
using NKStudio.UITKNavigation.Animation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Defines how the topmost popup responds to a Back request.
    /// </summary>
    public enum UIPopupBackBehavior
    {
        /// <summary>Closes the popup and consumes the Back request.</summary>
        Close,
        /// <summary>Consumes the Back request while leaving the popup open.</summary>
        Block,
        /// <summary>Allows the Back request to continue to navigation below the popup.</summary>
        PassThrough
    }

    /// <summary>
    /// Provides UI Popup View functionality.
    /// </summary>
    [UxmlElement]
    public partial class UIPopupView : VisualElement
    {
        private const int PopupBackPriority = 1000;

        private UIViewVisibility _visibility;
        private UIPopupBackdrop _backdrop;
        private EventCallback<ClickEvent> _backdropClick;
        private Func<UIPopupCloseReason, string, bool> _close;
        private Action<string> _action;

        /// <summary>
        /// Gets or sets how this popup responds when it is topmost and Back is requested.
        /// </summary>
        [UxmlAttribute("back-behavior")]
        public UIPopupBackBehavior BackBehavior { get; set; } = UIPopupBackBehavior.Close;

        /// <summary>
        /// Gets or sets whether selecting this popup's backdrop closes the popup.
        /// </summary>
        [UxmlAttribute("close-on-backdrop")]
        public bool CloseOnBackdrop { get; set; }

        [SerializeField]
        [UITransitionInspector]
        [UxmlObjectReference("backdrop-transitions")]
        private UITransitionSet backdropTransitions = new UITransitionSet();

        [SerializeField]
        [UITransitionInspector]
        [UxmlObjectReference("content-transitions")]
        private UITransitionSet contentTransitions = new UITransitionSet();

        internal UITransitionSet BackdropTransitions
        {
            get => backdropTransitions;
            set => backdropTransitions = value;
        }

        internal UITransitionSet ContentTransitions
        {
            get => contentTransitions;
            set => contentTransitions = value;
        }

        internal bool IsVisible => _visibility != null && _visibility.IsVisible;
        internal UIViewVisibility Visibility => _visibility;

        /// <summary>
        /// Initializes a full-panel popup root element.
        /// </summary>
        public UIPopupView()
        {
            AddToClassList("ui-popup-view");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            focusable = true;
            pickingMode = PickingMode.Position;
        }

        internal void InitializeRuntime(UIPopupBackdrop backdrop, UIPopupContent content, Func<UIPopupCloseReason, string, bool> close, Action<string> action)
        {
            DisposeRuntime();

            _backdrop = backdrop;
            _close = close;
            _action = action;
            _visibility = new UIViewVisibility(this, false);
            
            if (backdrop != null)
                _visibility.AddLayer(backdrop, backdropTransitions?.BuildShow(), backdropTransitions?.BuildHide());

            if (content != null)
                _visibility.AddLayer(content, contentTransitions?.BuildShow(), contentTransitions?.BuildHide());
            
            _visibility.BackHandler = () => _close?.Invoke(UIPopupCloseReason.Back, string.Empty) ?? false;

            if (_backdrop != null)
            {
                _backdropClick = OnBackdropClicked;
                _backdrop.RegisterCallback(_backdropClick);
            }
        }

        internal void SetTopmost(bool topmost)
        {
            if (_visibility == null)
                return;

            _visibility.BackPriority = topmost ? PopupBackPriority : 0;
            _visibility.HideOnBackButton = topmost && BackBehavior == UIPopupBackBehavior.Close;
            _visibility.BlockBackButton = topmost && BackBehavior == UIPopupBackBehavior.Block;
        }

        internal void ShowRuntime()
        {
            _visibility.InstantHide();
            _visibility.Show();
            schedule.Execute(FocusFirstContent);
        }

        internal void FocusRuntime() => FocusFirstContent();

        internal void HideRuntime() => _visibility?.Hide();
        
        internal void InstantHideRuntime() => _visibility?.InstantHide();

        internal void RequestAction(string actionId, bool closePopup)
        {
            string normalized = actionId?.Trim() ?? string.Empty;
            _action?.Invoke(normalized);
            if (closePopup)
                _close?.Invoke(UIPopupCloseReason.Action, normalized);
        }

        internal void DisposeRuntime()
        {
            if (_backdrop != null && _backdropClick != null)
                _backdrop.UnregisterCallback(_backdropClick);

            _backdrop = null;
            _backdropClick = null;
            _close = null;
            _action = null;
            _visibility?.Dispose();
            _visibility = null;
        }

        private void OnBackdropClicked(ClickEvent evt)
        {
            if (!CloseOnBackdrop || !ReferenceEquals(evt.target, _backdrop))
                return;

            if (_close?.Invoke(UIPopupCloseReason.Backdrop, string.Empty) == true)
                evt.StopPropagation();
        }

        private void FocusFirstContent()
        {
            VisualElement target = FindFirstFocusable(this);
            (target ?? this).Focus();
        }

        private static VisualElement FindFirstFocusable(VisualElement root)
        {
            for (int i = 0; i < root.hierarchy.childCount; i++)
            {
                VisualElement child = root.hierarchy[i];
                if (child.focusable && child.enabledInHierarchy)
                    return child;

                VisualElement nested = FindFirstFocusable(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }
    }
}
