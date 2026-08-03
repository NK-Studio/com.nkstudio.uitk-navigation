using System;
using System.Collections.Generic;
using LitMotion;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI View Visibility functionality.
    /// </summary>
    public sealed partial class UIViewVisibility : IDisposable
    {
        /// <summary>
        /// Defines the max layout warmup frames value.
        /// </summary>
        private const int MaxLayoutWarmupFrames = 4;

        /// <summary>
        /// Gets the visible list.
        /// </summary>
        [AutoStaticsCleanup]
        private static List<UIViewVisibility> _visibleList;

        private static List<UIViewVisibility> VisibleList =>
            _visibleList ??= new List<UIViewVisibility>();

        private readonly VisualElement _gate;
        private readonly UIAnimator _animator = new UIAnimator();
        private readonly PickingMode _restPickingMode;
        private StyleEnum<Visibility> _restVisibility;
        private readonly EventCallback<DetachFromPanelEvent> _detachCallback;
        private readonly List<Layer> _layers = new List<Layer>();
        private readonly List<UIAnimationBinding> _bindings = new List<UIAnimationBinding>();

        /// <summary>
        /// Gets the dependents.
        /// </summary>
        private readonly List<UIViewVisibility> _dependents = new List<UIViewVisibility>();

        private IVisualElementScheduledItem _pendingShow;
        private int _showGeneration;
        private int _settleGeneration;
        private Action _detachSettleHandlers;
        private bool _disposed;
        private UIViewHideMode _hideMode = UIViewHideMode.Display;
        private bool _hiddenByVisibility;

        /// <summary>
        /// Initializes a new instance of <see cref="UIViewVisibility"/>.
        /// </summary>
        public UIViewVisibility(VisualElement gate)
            : this(gate, true)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="UIViewVisibility"/>.
        /// </summary>
        public UIViewVisibility(VisualElement gate, bool animateGate)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
            _restPickingMode = gate.pickingMode;
            _restVisibility = gate.style.visibility;

            if (animateGate)
                _layers.Add(new Layer(gate, null));

            _detachCallback = _ => OnDetachedFromPanel();
            _gate.RegisterCallback(_detachCallback);
        }

        /// <summary>
        /// Gets the visible views.
        /// </summary>
        public static IReadOnlyList<UIViewVisibility> VisibleViews => VisibleList;

        /// <summary>
        /// Gets the gate.
        /// </summary>
        public VisualElement Gate => _gate;

        /// <summary>
        /// Gets the preset.
        /// </summary>
        public UIAnimationPreset Preset
        {
            get => _layers.Count > 0 && _layers[0].Element == _gate ? _layers[0].Preset : null;
            set
            {
                if (_layers.Count > 0 && _layers[0].Element == _gate)
                {
                    _layers[0] = _layers[0].WithPreset(value);
                    RefreshUsageHints();
                }
            }
        }

        /// <summary>
        /// Gets the show animation.
        /// </summary>
        internal UIAnimation ShowAnimation
        {
            get => _layers.Count > 0 && _layers[0].Element == _gate ? _layers[0].ShowAnimation : null;
            set
            {
                if (_layers.Count > 0 && _layers[0].Element == _gate)
                {
                    _layers[0] = _layers[0].WithShowAnimation(value);
                    RefreshUsageHints();
                }
            }
        }

        /// <summary>
        /// Gets the hide animation.
        /// </summary>
        internal UIAnimation HideAnimation
        {
            get => _layers.Count > 0 && _layers[0].Element == _gate ? _layers[0].HideAnimation : null;
            set
            {
                if (_layers.Count > 0 && _layers[0].Element == _gate)
                {
                    _layers[0] = _layers[0].WithHideAnimation(value);
                    RefreshUsageHints();
                }
            }
        }

        /// <summary>
        /// Gets the scheduler.
        /// </summary>
        public IMotionScheduler Scheduler
        {
            get => _animator.Scheduler;
            set => _animator.Scheduler = value;
        }

        /// <summary>
        /// Gets or sets the state.
        /// </summary>
        internal VisibilityState State { get; private set; } = VisibilityState.NotVisible;

        /// <summary>
        /// Gets the visibility progress.
        /// </summary>
        public float VisibilityProgress => State switch
        {
            VisibilityState.Visible => 1f,
            VisibilityState.Showing => _animator.Progress,
            VisibilityState.Hiding => _animator.InverseProgress,
            _ => 0f
        };

        /// <summary>
        /// Gets the inverse visibility.
        /// </summary>
        public float InverseVisibility => 1f - VisibilityProgress;

        /// <summary>
        /// Gets a value indicating whether visible.
        /// </summary>
        public bool IsVisible => State is VisibilityState.Visible or VisibilityState.Showing;

        /// <summary>
        /// Gets or sets the hide on back button.
        /// </summary>
        public bool HideOnBackButton { get; set; }

        /// <summary>
        /// Gets or sets the block back button.
        /// </summary>
        public bool BlockBackButton { get; set; }

        /// <summary>
        /// Gets or sets the back priority.
        /// </summary>
        internal int BackPriority { get; set; }

        /// <summary>
        /// Gets the hide mode.
        /// </summary>
        internal UIViewHideMode HideMode
        {
            get => _hideMode;
            set
            {
                if (_hideMode == value)
                    return;

                _hideMode = value;
                if (!_disposed && State == VisibilityState.NotVisible)
                    CloseGate();
            }
        }

        /// <summary>
        /// Gets or sets the dependents provider.
        /// </summary>
        public Func<IReadOnlyList<UIViewVisibility>> DependentsProvider { get; set; }

        /// <summary>
        /// Gets or sets the back handler.
        /// </summary>
        public Func<bool> BackHandler { get; set; }

        /// <summary>
        /// Occurs when the show started event is raised.
        /// </summary>
        public event Action ShowStarted;

        /// <summary>
        /// Occurs when the show finished event is raised.
        /// </summary>
        public event Action ShowFinished;

        /// <summary>
        /// Occurs when the hide started event is raised.
        /// </summary>
        public event Action HideStarted;

        /// <summary>
        /// Occurs when the hide finished event is raised.
        /// </summary>
        public event Action HideFinished;

        /// <summary>
        /// Occurs when the state changed event is raised.
        /// </summary>
        internal event Action<VisibilityState> StateChanged;

        /// <summary>
        /// Occurs when the disposing event is raised.
        /// </summary>
        private event Action Disposing;

        /// <summary>
        /// Adds l ay er.
        /// </summary>
        public void AddLayer(VisualElement element, UIAnimationPreset preset)
        {
            if (element == null)
                return;

            _layers.Add(new Layer(element, preset));
            RefreshUsageHints();
        }

        /// <summary>
        /// Adds l ay er.
        /// </summary>
        internal void AddLayer(VisualElement element, UIAnimation showAnimation, UIAnimation hideAnimation)
        {
            if (element == null)
                return;

            _layers.Add(new Layer(element, null, showAnimation, hideAnimation));
            RefreshUsageHints();
        }

        /// <summary>
        /// Shows member.
        /// </summary>
        public void Show()
        {
            if (_disposed || State is VisibilityState.Visible or VisibilityState.Showing)
                return;

            float startProgress = State == VisibilityState.Hiding ? _animator.InverseProgress : 0f;

            OpenGate();
            AddToVisibleList();
            SetState(VisibilityState.Showing);
            ShowStarted?.Invoke();

            CaptureDependents();
            for (int i = 0; i < _dependents.Count; i++)
                _dependents[i].Show();

            if (!BuildBindings(UIAnimationType.Show))
            {
                FinishShow();
                return;
            }

            if (NeedsLayoutWarmup())
            {
                ScheduleShow(startProgress);
                return;
            }

            HideGateVisibility();
            _gate.pickingMode = PickingMode.Ignore;
            _animator.Play(_bindings, startProgress, FinishShow);
            RestoreGateVisibility();
            _gate.pickingMode = _restPickingMode;
        }

        /// <summary>
        /// Hides member.
        /// </summary>
        public void Hide()
        {
            if (_disposed || State is VisibilityState.NotVisible or VisibilityState.Hiding)
                return;

            bool wasPending = _pendingShow != null;
            CancelPendingShow();
            float startProgress = !wasPending && State == VisibilityState.Showing ? _animator.InverseProgress : 0f;

            SetState(VisibilityState.Hiding);
            HideStarted?.Invoke();

            CaptureDependents();
            for (int i = 0; i < _dependents.Count; i++)
                _dependents[i].Hide();

            if (!BuildBindings(UIAnimationType.Hide))
            {
                FinishHide();
                return;
            }

            _animator.Play(_bindings, startProgress, FinishHide);
        }

        /// <summary>
        /// Performs the instant show operation.
        /// </summary>
        public void InstantShow()
        {
            ApplyInstant(true, true);
        }

        /// <summary>
        /// Performs the instant hide operation.
        /// </summary>
        public void InstantHide()
        {
            ApplyInstant(false, true);
        }

        /// <summary>
        /// Initializes visible.
        /// </summary>
        public void InitializeVisible(bool visible)
        {
            ApplyInstant(visible, false);
        }

        private void ApplyInstant(bool visible, bool clearStyles)
        {
            if (_disposed)
                return;

            CancelPendingShow();
            _animator.Stop();

            if (clearStyles)
                ClearAllLayerStyles();

            CaptureDependents();
            for (int i = 0; i < _dependents.Count; i++)
                _dependents[i].ApplyInstant(visible, clearStyles);

            if (visible)
            {
                OpenGate();
                AddToVisibleList();
            }
            else
            {
                CloseGate();
                VisibleList.Remove(this);
            }

            VisibilityState next = visible ? VisibilityState.Visible : VisibilityState.NotVisible;
            bool changed = State != next;
            SetState(next);

            if (!changed)
                return;

            if (visible)
                ShowFinished?.Invoke();
            else
                HideFinished?.Invoke();
        }

        /// <summary>
        /// Sets v is ib le.
        /// </summary>
        public void SetVisible(bool visible, bool instant = false)
        {
            if (instant)
            {
                if (visible)
                    InstantShow();
                else
                    InstantHide();
                return;
            }

            if (visible)
                Show();
            else
                Hide();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed)
                return;

            CancelPendingShow();
            CancelDependentWait();
            _disposed = true;
            VisibleList.Remove(this);
            _gate.UnregisterCallback(_detachCallback);
            _animator.Dispose();
            _bindings.Clear();
            ClearUsageHints();
            _layers.Clear();
            _dependents.Clear();
            BackHandler = null;
            DependentsProvider = null;

            Disposing?.Invoke();
            Disposing = null;
        }

        private bool NeedsLayoutWarmup()
        {
            for (int i = 0; i < _bindings.Count; i++)
            {
                UIAnimationBinding binding = _bindings[i];
                if (binding.Element?.panel != null
                    && binding.Animation?.Move.Enabled == true
                    && !UIMoveAnimation.HasValidLayout(binding.Element))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Performs the schedule show operation.
        /// </summary>
        private void ScheduleShow(float startProgress)
        {
            CancelPendingShow();
            int generation = ++_showGeneration;
            HideGateVisibility();
            _gate.pickingMode = PickingMode.Ignore;

            int waitedFrames = 0;
            IVisualElementScheduledItem item = null;
            item = _gate.schedule.Execute(() =>
            {
                if (_disposed || generation != _showGeneration || State != VisibilityState.Showing)
                {
                    item.Pause();
                    return;
                }

                if (NeedsLayoutWarmup() && ++waitedFrames < MaxLayoutWarmupFrames)
                    return;

                item.Pause();
                _pendingShow = null;
                _animator.Play(_bindings, startProgress, FinishShow);
                RestoreGateVisibility();
                _gate.pickingMode = _restPickingMode;
            }).Every(0);

            _pendingShow = item;
        }

        private void CancelPendingShow()
        {
            if (_pendingShow == null)
                return;

            _pendingShow.Pause();
            _pendingShow = null;
            _showGeneration++;
            RestoreGateVisibility();
            _gate.pickingMode = _restPickingMode;
        }
        private bool BuildBindings(UIAnimationType type)
        {
            _bindings.Clear();

            for (int i = 0; i < _layers.Count; i++)
            {
                Layer layer = _layers[i];
                if (layer.Element == null || !layer.HasAnimation)
                    continue;

                UIAnimation animation = layer.GetAnimation(type);
                if (animation != null && animation.HasEnabledChannel)
                    _bindings.Add(new UIAnimationBinding(layer.Element, animation));
            }

            return _bindings.Count > 0;
        }

        /// <summary>
        /// Refreshes u sa ge hi nt s.
        /// </summary>
        private void RefreshUsageHints()
        {
            if (_disposed)
                return;

            ApplyUsageHints(false);
        }

        /// <summary>
        /// Clears u sa ge hi nt s.
        /// </summary>
        private void ClearUsageHints()
        {
            ApplyUsageHints(true);
        }

        /// <summary>
        /// Applies u sa ge hi nt s.
        /// </summary>
        private void ApplyUsageHints(bool clear)
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                Layer layer = _layers[i];
                VisualElement element = layer.Element;
                if (element == null)
                    continue;

                UsageHints required = UsageHints.None;
                if (!clear)
                {
                    for (int j = 0; j < _layers.Count; j++)
                    {
                        if (ReferenceEquals(_layers[j].Element, element))
                            required |= _layers[j].RequiredHints;
                    }
                }

                UsageHints next = (element.usageHints & ~layer.AppliedHints) | required;

                if (element.usageHints != next)
                    element.usageHints = next;

                _layers[i] = layer.WithAppliedHints(required);
            }
        }

        private void FinishShow()
        {
            for (int i = 0; i < _bindings.Count; i++)
                _bindings[i].ClearStyles(false);

            WaitForDependents(true, () =>
            {
                SetState(VisibilityState.Visible);
                ShowFinished?.Invoke();
            });
        }

        private void FinishHide()
        {
            WaitForDependents(false, () =>
            {
                ClearAllLayerStyles();
                CloseGate();
                VisibleList.Remove(this);
                SetState(VisibilityState.NotVisible);
                HideFinished?.Invoke();
            });
        }

        /// <summary>
        /// Performs the capture dependents operation.
        /// </summary>
        private void CaptureDependents()
        {
            CancelDependentWait();
            _dependents.Clear();

            IReadOnlyList<UIViewVisibility> source = DependentsProvider?.Invoke();
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                UIViewVisibility dependent = source[i];
                if (dependent != null && !ReferenceEquals(dependent, this) && !dependent._disposed)
                    _dependents.Add(dependent);
            }
        }

        /// <summary>
        /// Waits for f or de pe nd en ts.
        /// </summary>
        private void WaitForDependents(bool targetVisible, Action onSettled)
        {
            CancelDependentWait();

            if (AreDependentsSettled(targetVisible))
            {
                onSettled();
                return;
            }

            int generation = ++_settleGeneration;
            List<UIViewVisibility> watched = new List<UIViewVisibility>(_dependents);

            Action reevaluate = null;
            Action<VisibilityState> onStateChanged = _ => reevaluate?.Invoke();

            void Detach()
            {
                for (int i = 0; i < watched.Count; i++)
                {
                    watched[i].StateChanged -= onStateChanged;
                    watched[i].Disposing -= reevaluate;
                }

                _detachSettleHandlers = null;
            }

            reevaluate = () =>
            {
                if (_disposed || generation != _settleGeneration)
                    return;

                if (!AreDependentsSettled(targetVisible))
                    return;

                Detach();
                onSettled();
            };

            _detachSettleHandlers = Detach;

            for (int i = 0; i < watched.Count; i++)
            {
                watched[i].StateChanged += onStateChanged;
                watched[i].Disposing += reevaluate;
            }
        }

        private bool AreDependentsSettled(bool targetVisible)
        {
            VisibilityState target = targetVisible ? VisibilityState.Visible : VisibilityState.NotVisible;

            for (int i = 0; i < _dependents.Count; i++)
            {
                UIViewVisibility dependent = _dependents[i];

                if (dependent._disposed)
                    continue;

                if (dependent.State != target)
                    return false;
            }

            return true;
        }

        private void CancelDependentWait()
        {
            _settleGeneration++;
            _detachSettleHandlers?.Invoke();
            _detachSettleHandlers = null;
        }

        private void ClearAllLayerStyles()
        {
            for (int i = 0; i < _layers.Count; i++)
                UIAnimation.ClearStyles(_layers[i].Element, false);
        }

        private void OpenGate()
        {
            _gate.style.display = DisplayStyle.Flex;
            RestoreGateVisibility();
            _gate.pickingMode = _restPickingMode;
        }

        private void CloseGate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                OpenGate();
                return;
            }
#endif
            
            if (_hideMode == UIViewHideMode.Visibility)
            {
                _gate.style.display = DisplayStyle.Flex;
                HideGateVisibility();
            }
            else
            {
                _gate.style.display = DisplayStyle.None;
                RestoreGateVisibility();
            }

            _gate.pickingMode = PickingMode.Ignore;
        }

        /// <summary>
        /// Hides g at ev is ib il it y.
        /// </summary>
        private void HideGateVisibility()
        {
            if (_hiddenByVisibility)
                return;

            _restVisibility = _gate.style.visibility;
            _hiddenByVisibility = true;
            _gate.style.visibility = Visibility.Hidden;
        }

        /// <summary>
        /// Performs the restore gate visibility operation.
        /// </summary>
        private void RestoreGateVisibility()
        {
            if (!_hiddenByVisibility)
                return;

            _hiddenByVisibility = false;
            _gate.style.visibility = _restVisibility;
        }

        private void AddToVisibleList()
        {
            VisibleList.Remove(this);
            VisibleList.Add(this);
        }

        private void SetState(VisibilityState state)
        {
            if (State == state)
                return;

            State = state;
            StateChanged?.Invoke(state);
        }

        private void OnDetachedFromPanel()
        {
            CancelPendingShow();
            CancelDependentWait();
            _animator.Stop();
            VisibleList.Remove(this);

            if (State == VisibilityState.Showing)
                SetState(VisibilityState.Visible);
            else if (State == VisibilityState.Hiding)
                SetState(VisibilityState.NotVisible);
        }

        private readonly struct Layer
        {
            public readonly VisualElement Element;
            public readonly UIAnimationPreset Preset;
            internal readonly UIAnimation ShowAnimation;
            internal readonly UIAnimation HideAnimation;

            /// <summary>
            /// Gets the applied hints.
            /// </summary>
            public readonly UsageHints AppliedHints;

            public Layer(VisualElement element, UIAnimationPreset preset)
                : this(element, preset, null, null, UsageHints.None)
            {
            }

            public Layer(
                VisualElement element,
                UIAnimationPreset preset,
                UIAnimation showAnimation,
                UIAnimation hideAnimation)
                : this(element, preset, showAnimation, hideAnimation, UsageHints.None)
            {
            }

            private Layer(
                VisualElement element,
                UIAnimationPreset preset,
                UIAnimation showAnimation,
                UIAnimation hideAnimation,
                UsageHints appliedHints)
            {
                Element = element;
                Preset = preset;
                ShowAnimation = showAnimation;
                HideAnimation = hideAnimation;
                AppliedHints = appliedHints;
            }

            /// <summary>
            /// Gets a value indicating whether s animation.
            /// </summary>
            public bool HasAnimation => Preset != null || ShowAnimation != null || HideAnimation != null;

            /// <summary>
            /// Gets the required hints.
            /// </summary>
            public UsageHints RequiredHints
            {
                get
                {
                    if (Element == null)
                        return UsageHints.None;

                    UsageHints hints = GetAnimation(UIAnimationType.Show)?.RequiredUsageHints ?? UsageHints.None;
                    hints |= GetAnimation(UIAnimationType.Hide)?.RequiredUsageHints ?? UsageHints.None;
                    return hints;
                }
            }

            /// <summary>
            /// Gets the animation.
            /// </summary>
            internal UIAnimation GetAnimation(UIAnimationType type)
            {
                if (type == UIAnimationType.Show)
                    return ShowAnimation ?? Preset?.GetShow();
                return HideAnimation ?? Preset?.GetHide();
            }


            public Layer WithPreset(UIAnimationPreset preset)
            {
                return new Layer(Element, preset, ShowAnimation, HideAnimation, AppliedHints);
            }

            internal Layer WithShowAnimation(UIAnimation showAnimation)
            {
                return new Layer(Element, Preset, showAnimation, HideAnimation, AppliedHints);
            }

            internal Layer WithHideAnimation(UIAnimation hideAnimation)
            {
                return new Layer(Element, Preset, ShowAnimation, hideAnimation, AppliedHints);
            }

            public Layer WithAppliedHints(UsageHints appliedHints)
            {
                return new Layer(Element, Preset, ShowAnimation, HideAnimation, appliedHints);
            }
        }
    }
}
