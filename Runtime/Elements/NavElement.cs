using System.Collections.Generic;
using Unity.Scripting.LifecycleManagement;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Elements
{
    /// <summary>
    /// Provides Nav Element functionality.
    /// </summary>
    [UxmlElement]
    public partial class NavElement : VisualElement, IUIVisibleView
    {
        /// <summary>
        /// Gets the runtime visible states.
        /// </summary>
        [AutoStaticsCleanup]
        private static Dictionary<UIKey, bool> _runtimeVisibleStates;

        private static Dictionary<UIKey, bool> RuntimeVisibleStates =>
            _runtimeVisibleStates ??= new Dictionary<UIKey, bool>();

        /// <summary>
        /// Gets the followers.
        /// </summary>
        private readonly List<UIViewVisibility> _followers = new List<UIViewVisibility>();

        private UIViewVisibility _visibility;
        private IVisualElementScheduledItem _autoHideTimer;
        private bool _startupApplied;
        private UIKey _registeredId;
        private string _viewCategory = "Default";
        private string _viewKey = string.Empty;

        /// <summary>
        /// Gets or sets the catalog category used to register this view.
        /// </summary>
        [UIKeySelector(nameof(ViewKey), UIKeyCatalogKind.View)]
        [UxmlAttribute("view-category")]
        public string ViewCategory
        {
            get => _viewCategory;
            set
            {
                _viewCategory = value;
                RefreshRegistration();
            }
        }

        /// <summary>
        /// Gets or sets the key used to register this view within its category.
        /// </summary>
        [UnityEngine.HideInInspector]
        [UxmlAttribute("view-key")]
        public string ViewKey
        {
            get => _viewKey;
            set
            {
                _viewKey = value;
                RefreshRegistration();
            }
        }

        /// <summary>
        /// Gets or sets whether a Back request hides this view when it has the highest Back priority.
        /// </summary>
        [UxmlAttribute("hide-on-back")]
        public bool HideOnBack { get; set; }

        /// <summary>
        /// Gets or sets whether this visible view consumes Back requests without hiding itself.
        /// </summary>
        [UxmlAttribute("block-back")]
        public bool BlockBack { get; set; }

        /// <summary>
        /// Gets or sets the follow parent.
        /// </summary>
        [UxmlAttribute("follow-parent")]
        public bool FollowParent { get; set; } = true;

        #region Startup Settings (Custom Inspector)

        /// <summary>
        /// Performs the ui view startup settings operation.
        /// </summary>
        [SerializeField]
        [UIViewStartupInspector]
        [UxmlObjectReference("startup")]
        private UIViewStartupSettings startup = new UIViewStartupSettings();

        internal UIViewStartupSettings Startup
        {
            get => startup;
            set => startup = value;
        }

        /// <summary>
        /// Gets the starts hidden.
        /// </summary>
        public bool StartsHidden => startup != null && startup.StartsHidden;

        #endregion

        #region Transition Settings (DoozyUI Custom Inspector)

        /// <summary>
        /// Performs the ui transition set operation.
        /// </summary>
        [SerializeField]
        [UITransitionInspector]
        [UxmlObjectReference("transitions")]
        private UITransitionSet transitions = new UITransitionSet();

        internal UITransitionSet Transitions
        {
            get => transitions;
            set => transitions = value;
        }

        #endregion

        /// <summary>
        /// Gets the normalized identifier under which this view is registered.
        /// </summary>
        public UIKey Id => new UIKey(ViewCategory, ViewKey);
        /// <summary>
        /// Gets whether this view is visible or currently animating into view.
        /// </summary>
        public bool IsVisible => _visibility != null && _visibility.IsVisible;

        /// <summary>
        /// Performs the ensure visibility operation.
        /// </summary>
        public UIViewVisibility Visibility => EnsureVisibility();

        /// <summary>
        /// Initializes a new navigation view element and its panel lifecycle callbacks.
        /// </summary>
        public NavElement()
        {
            AddToClassList("nav-element");
            style.display = DisplayStyle.Flex;
            style.position = Position.Absolute;
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);
        }

        /// <summary>
        /// Shows the view using its configured Show transition.
        /// </summary>
        public void Show()
        {
            EnsureVisibility().Show();
        }

        /// <summary>
        /// Hides the view using its configured Hide transition.
        /// </summary>
        public void Hide()
        {
            EnsureVisibility().Hide();
        }

        /// <summary>
        /// Shows the view immediately without playing a transition.
        /// </summary>
        public void InstantShow()
        {
            EnsureVisibility().InstantShow();
        }

        /// <summary>
        /// Hides the view immediately without playing a transition.
        /// </summary>
        public void InstantHide()
        {
            EnsureVisibility().InstantHide();
        }

        /// <summary>
        /// Applies s ta rt po si ti on.
        /// </summary>
        public void ApplyStartPosition()
        {
            if (startup is not { UseCustomStartPosition: true })
                return;

            Vector3 position = startup.CustomStartPosition;
            style.translate = new StyleTranslate(new Translate(
                new Length(position.x, LengthUnit.Pixel),
                new Length(position.y, LengthUnit.Pixel),
                position.z));

            InvalidateAnimationStartValues();
        }

        /// <summary>
        /// Performs the invalidate animation start values operation.
        /// </summary>
        public void InvalidateAnimationStartValues()
        {
            if (_visibility == null)
                return;

            _visibility.ShowAnimation?.InvalidateStartValues();
            _visibility.HideAnimation?.InvalidateStartValues();
        }

        /// <summary>
        /// Performs the restore start state operation.
        /// </summary>
        public void RestoreStartState()
        {
            if (StartsHidden) InstantHide(); else InstantShow();
            ApplyStartPosition();
        }

        private void OnAttached(AttachToPanelEvent _)
        {
            UIViewVisibility visibility = EnsureVisibility();
            visibility.HideOnBackButton = HideOnBack;
            visibility.BlockBackButton = BlockBack;
            visibility.HideMode = startup?.HideMode ?? UIViewHideMode.Display;
            ApplyTransitions(visibility);

            if (!Application.isPlaying || _startupApplied)
            {
                SyncStateToDisplay(visibility);
            }
            else if (Id.IsValid && RuntimeVisibleStates.TryGetValue(Id, out bool wasVisible))
            {
                _startupApplied = true;
                visibility.InitializeVisible(wasVisible);
                ApplyStartPosition();
            }
            else
            {
                _startupApplied = true;
                ApplyStartBehaviour(visibility);
            }

            RefreshRegistration();
        }

        /// <summary>
        /// Performs the sync state to display operation.
        /// </summary>
        private void SyncStateToDisplay(UIViewVisibility visibility)
        {
            visibility.InitializeVisible(resolvedStyle.display != DisplayStyle.None);
        }

        private void ApplyStartBehaviour(UIViewVisibility visibility)
        {
            NavElement controller = FindControllingParent();
            if (controller != null)
            {
                visibility.InitializeVisible(controller.Visibility.IsVisible);
                ApplyStartPosition();
                return;
            }

            UIViewStartBehaviour onStart = startup?.OnStart ?? UIViewStartBehaviour.Disabled;

            switch (onStart)
            {
                case UIViewStartBehaviour.InstantHide:
                case UIViewStartBehaviour.AnimationShow:
                    visibility.InitializeVisible(false);
                    break;
                case UIViewStartBehaviour.InstantShow:
                case UIViewStartBehaviour.AnimationHide:
                    visibility.InitializeVisible(true);
                    break;
                default:
                    SyncStateToDisplay(visibility);
                    break;
            }

            ApplyStartPosition();

            if (onStart == UIViewStartBehaviour.AnimationShow)
                ScheduleStartAnimation(visibility, true);
            else if (onStart == UIViewStartBehaviour.AnimationHide)
                ScheduleStartAnimation(visibility, false);
        }

        private void ScheduleStartAnimation(UIViewVisibility visibility, bool show)
        {
            schedule.Execute(() =>
            {
                if (panel == null)
                    return;
                if (show) visibility.Show(); else visibility.Hide();
            });
        }

        private void OnDetached(DetachFromPanelEvent _)
        {
            CancelAutoHide();

            if (Application.isPlaying && Id.IsValid)
                RuntimeVisibleStates[Id] = IsVisible;

            UIViewRegistry.Unregister(_registeredId, this);
            _registeredId = default;

            _visibility?.Dispose(); // 추�?
            _visibility = null;
            _followers.Clear();
        }

        /// <summary>
        /// Resets r un ti me vi si bl es ta te s.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeVisibleStates()
        {
            RuntimeVisibleStates.Clear();
        }

        private UIViewVisibility EnsureVisibility()
        {
            if (_visibility != null)
                return _visibility;

            _visibility = new UIViewVisibility(this);
            _visibility.DependentsProvider = CollectFollowers;
            _visibility.ShowStarted += OnShowStarted;
            _visibility.ShowFinished += OnShowFinished;
            _visibility.HideStarted += OnHideStarted;
            return _visibility;
        }

        #region Hierarchy Propagation

        /// <summary>
        /// Performs the collect followers operation.
        /// </summary>
        private IReadOnlyList<UIViewVisibility> CollectFollowers()
        {
            _followers.Clear();
            CollectFollowers(this, _followers);
            return _followers;
        }

        private static void CollectFollowers(VisualElement parent, List<UIViewVisibility> result)
        {
            int count = parent.hierarchy.childCount;
            for (int i = 0; i < count; i++)
            {
                VisualElement child = parent.hierarchy[i];

                if (child is NavElement nav)
                {
                    if (nav.FollowParent)
                        result.Add(nav.Visibility);

                    continue;
                }

                CollectFollowers(child, result);
            }
        }

        /// <summary>
        /// Performs the collect follower elements operation.
        /// </summary>
        internal static void CollectFollowerElements(VisualElement parent, List<NavElement> result)
        {
            int count = parent.hierarchy.childCount;
            for (int i = 0; i < count; i++)
            {
                VisualElement child = parent.hierarchy[i];

                if (child is NavElement nav)
                {
                    if (!nav.FollowParent)
                        continue;

                    result.Add(nav);
                    CollectFollowerElements(nav, result);
                    continue;
                }

                CollectFollowerElements(child, result);
            }
        }

        /// <summary>
        /// Finds c on tr ol li ng pa re nt.
        /// </summary>
        private NavElement FindControllingParent()
        {
            if (!FollowParent)
                return null;

            VisualElement current = hierarchy.parent;
            while (current != null)
            {
                if (current is NavElement nav)
                    return nav;

                current = current.hierarchy.parent;
            }

            return null;
        }

        #endregion

        private void OnShowStarted()
        {
            UINavigationEvents.RaiseViewTransitionStarted(
                Id,
                UIViewOutputCondition.Show);
        }

        private void OnShowFinished()
        {
            ScheduleAutoHide();
        }

        private void OnHideStarted()
        {
            CancelAutoHide();
            UINavigationEvents.RaiseViewTransitionStarted(
                Id,
                UIViewOutputCondition.Hide);
        }

        #region Auto Hide

        /// <summary>
        /// Performs the schedule auto hide operation.
        /// </summary>
        private void ScheduleAutoHide()
        {
            CancelAutoHide();

            if (!Application.isPlaying || startup is not { AutoHideAfterShow: true })
                return;

            long delayMs = (long)(Mathf.Max(0f, startup.AutoHideDelay) * 1000f);
            _autoHideTimer = schedule.Execute(() =>
            {
                _autoHideTimer = null;
                if (panel != null && IsVisible)
                    Hide();
            }).StartingIn(delayMs);
        }

        private void CancelAutoHide()
        {
            if (_autoHideTimer == null)
                return;

            _autoHideTimer.Pause();
            _autoHideTimer = null;
        }

        #endregion

        private void ApplyTransitions(UIViewVisibility visibility)
        {
            visibility.ShowAnimation = transitions?.BuildShow();
            visibility.HideAnimation = transitions?.BuildHide();
        }

        private void RefreshRegistration()
        {
            if (panel == null)
                return;

            UIKey next = panel.contextType == ContextType.Player ? Id : default;
            if (_registeredId == next)
                return;

            UIViewRegistry.Unregister(_registeredId, this);
            _registeredId = default;

            if (!next.IsValid)
                return;

            UIViewRegistry.Register(next, this);
            _registeredId = next;
        }
    }
}
