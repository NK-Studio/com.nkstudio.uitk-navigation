using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEngine;

namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Provides UI Navigation Service functionality.
    /// </summary>
    public sealed class UINavigationService
    {
        private readonly Stack<string> _back = new Stack<string>();
        private readonly Stack<string> _forward = new Stack<string>();
        private readonly Queue<Request> _pending = new Queue<Request>();

        private readonly Dictionary<UIKey, UINavigationViewCommand> _showCommands = new();
        private readonly Dictionary<UIKey, UINavigationViewCommand> _hideCommands = new();
        private readonly List<UINavigationViewCommand> _showCommandBuffer = new();
        private readonly List<UINavigationViewCommand> _hideCommandBuffer = new();

        private bool _dispatching;
        private int _destinationDepth;

        private UINavigationTransition _activeTransition;
        private float[] _delayRemaining = Array.Empty<float>();
        private bool[] _delayConsumed = Array.Empty<bool>();
        private IReadOnlyList<UINavigationTransition> _delayTransitions;
        private int _delayStateCount;
        private bool _hasAutomaticTransitions;

        /// <summary>
        /// Initializes a new instance of <see cref="UINavigationService"/>.
        /// </summary>
        public UINavigationService(UINavigationAsset asset)
        {
            Asset = asset;
        }

        /// <summary>
        /// Gets the asset.
        /// </summary>
        public UINavigationAsset Asset { get; }

        /// <summary>
        /// Gets or sets the active node.
        /// </summary>
        internal UINavigationNode ActiveNode { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Gets or sets the max history depth.
        /// </summary>
        public int MaxHistoryDepth { get; set; } = 32;

        /// <summary>
        /// Gets the back stack.
        /// </summary>
        public IReadOnlyCollection<string> BackStack => _back;

        /// <summary>
        /// Gets the forward stack.
        /// </summary>
        public IReadOnlyCollection<string> ForwardStack => _forward;

        /// <summary>
        /// Occurs when the node changing event is raised.
        /// </summary>
        internal event Action<UINavigationChange> NodeChanging;

        internal event Action<IReadOnlyList<UINavigationViewCommand>> HideCommandsRequested;

        internal event Action<IReadOnlyList<UINavigationViewCommand>> ShowCommandsRequested;

        internal event Action<IReadOnlyList<UIKey>> ResyncViewsRequested;

        /// <summary>
        /// Occurs when the action requested event is raised.
        /// </summary>
        internal event Action<UINavigationAction> ActionRequested;

        /// <summary>
        /// Occurs when the node changed event is raised.
        /// </summary>
        internal event Action<UINavigationChange> NodeChanged;

        /// <summary>
        /// Initializes this instance.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized)
                return;

            IsInitialized = true;
            Asset?.Prepare();

            UINavigationNode start = Asset != null ? Asset.GetStartNode() : null;
            if (start == null)
            {
                if (Asset != null)
                    Debug.LogWarning("[UINavigation] 시작 노드를 찾을 수 없어 초기화를 건너뜁니다.", Asset);
                DrainPending();
                return;
            }

            ResyncViewsRequested?.Invoke(Array.Empty<UIKey>());

            _dispatching = true;

            try
            {
                ApplyTransition(start, UINavigationTransitionKind.Replace);
            }
            finally
            {
                _dispatching = false;
            }

            DrainPending();
        }

        /// <summary>
        /// Performs the go to operation.
        /// </summary>
        public bool GoTo(string nodeId, UINavigationTransitionKind kind = UINavigationTransitionKind.Push)
        {
            return Dispatch(new Request(RequestKind.GoTo, nodeId, default, kind));
        }

        /// <summary>
        /// Performs the back operation.
        /// </summary>
        public bool Back()
        {
            return Dispatch(new Request(RequestKind.Back, null, default, UINavigationTransitionKind.Back));
        }

        /// <summary>
        /// Performs the forward operation.
        /// </summary>
        public bool Forward()
        {
            return Dispatch(new Request(RequestKind.Forward, null, default, UINavigationTransitionKind.Push));
        }

        /// <summary>
        /// Performs the trigger operation.
        /// </summary>
        public bool Trigger(UIKey signal)
        {
            return Dispatch(new Request(RequestKind.Signal, null, signal, UINavigationTransitionKind.Push));
        }

        /// <summary>
        /// Sends a graph-local custom destination signal.
        /// </summary>
        public bool Trigger(string destinationKey)
        {
            if (string.IsNullOrWhiteSpace(destinationKey))
                return false;

            return Dispatch(new Request(
                RequestKind.CustomSignal,
                destinationKey,
                default,
                UINavigationTransitionKind.Push));
        }

        /// <summary>
        /// Performs the trigger toggle operation.
        /// </summary>
        public bool TriggerToggle(UIKey toggle, bool value)
        {
            return Dispatch(new Request(
                RequestKind.Toggle,
                null,
                toggle,
                UINavigationTransitionKind.Push,
                value));
        }

        /// <summary>
        /// Performs the trigger view operation.
        /// </summary>
        public bool TriggerView(
            UIKey view,
            UIViewOutputCondition condition)
        {
            return Dispatch(new Request(
                RequestKind.UIView,
                null,
                view,
                UINavigationTransitionKind.Push,
                viewCondition: condition));
        }

        internal bool Tick(float unscaledDeltaTime)
        {
            if (!IsInitialized || ActiveNode == null || _dispatching)
                return false;

            float delta = Mathf.Max(0f, unscaledDeltaTime);
            IReadOnlyList<UINavigationTransition> transitions = ActiveNode.Transitions;
            EnsureDelayState(transitions);
            if (!_hasAutomaticTransitions)
                return false;

            for (int i = 0; i < transitions.Count; i++)
            {
                UINavigationTransition transition = transitions[i];
                if (transition == null ||
                    transition.TriggerKind != UINavigationTriggerKind.TimeDelay ||
                    _delayConsumed[i])
                {
                    continue;
                }

                _delayRemaining[i] -= delta;
            }

            for (int i = 0; i < transitions.Count; i++)
            {
                UINavigationTransition transition = transitions[i];
                if (transition == null ||
                    transition.TriggerKind != UINavigationTriggerKind.TimeDelay ||
                    _delayConsumed[i])
                {
                    continue;
                }

                if (_delayRemaining[i] > 0f)
                    continue;

                _delayConsumed[i] = true;
                return ExecuteTransition(transition);
            }

            // --- Random Node Logic ---
            float totalWeight = 0f;
            bool hasRandom = false;
            for (int i = 0; i < transitions.Count; i++)
            {
                UINavigationTransition transition = transitions[i];
                if (transition != null && transition.TriggerKind == UINavigationTriggerKind.Random && !_delayConsumed[i])
                {
                    hasRandom = true;
                    totalWeight += transition.Weight;
                }
            }

            if (hasRandom)
            {
                float randomValue = UnityEngine.Random.value * totalWeight;
                float currentWeight = 0f;
                for (int i = 0; i < transitions.Count; i++)
                {
                    UINavigationTransition transition = transitions[i];
                    if (transition != null && transition.TriggerKind == UINavigationTriggerKind.Random && !_delayConsumed[i])
                    {
                        currentWeight += transition.Weight;
                        if (randomValue <= currentWeight || Mathf.Approximately(totalWeight, 0f))
                        {
                            _delayConsumed[i] = true;
                            return ExecuteTransition(transition);
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Performs the resync operation.
        /// </summary>
        public void Resync()
        {
            UINavigationNode node = ActiveNode;
            ResyncViewsRequested?.Invoke(node?.ShowOnEnter ?? Array.Empty<UIKey>());
        }

        private bool Dispatch(Request request)
        {
            if (_dispatching || !IsInitialized)
            {
                _pending.Enqueue(request);
                return true;
            }

            _dispatching = true;
            bool result;

            try
            {
                result = Execute(request);
            }
            finally
            {
                _dispatching = false;
            }

            DrainPending();
            return result;
        }

        private void DrainPending()
        {
            if (_dispatching || !IsInitialized)
                return;

            _dispatching = true;

            try
            {
                while (_pending.Count > 0)
                    Execute(_pending.Dequeue());
            }
            finally
            {
                _dispatching = false;
            }
        }

        private bool Execute(Request request)
        {
            switch (request.Kind)
            {
                case RequestKind.GoTo:
                    return ExecuteGoTo(request.NodeId, request.TransitionKind);

                case RequestKind.Back:
                    return ExecuteBack();

                case RequestKind.Forward:
                    return ExecuteForward();

                case RequestKind.Signal:
                    return ExecuteSignal(request.Signal);

                case RequestKind.CustomSignal:
                    return ExecuteCustomSignal(request.NodeId);

                case RequestKind.Toggle:
                    return ExecuteToggle(request.Signal, request.ToggleValue);

                case RequestKind.UIView:
                    return ExecuteView(request.Signal, request.ViewCondition);

                default:
                    return false;
            }
        }

        private bool ExecuteGoTo(string nodeId, UINavigationTransitionKind kind)
        {
            if (kind == UINavigationTransitionKind.Back)
                return ExecuteBack();

            if (Asset == null || !Asset.TryGetNode(nodeId, out UINavigationNode target))
                return false;

            if (target.IsDestination)
                return ExecuteDestinationRoute(target);

            return ApplyTransition(target, kind);
        }

        private bool ExecuteSignal(UIKey signal)
        {
            if (Asset != null &&
                Asset.TryGetPortal(
                    UINavigationTriggerKind.Signal,
                    signal,
                    false,
                    out UINavigationTransition portal))
                return ExecuteTransition(portal);

            if (ActiveNode != null &&
                ActiveNode.TryGetTransition(
                    UINavigationTriggerKind.Signal,
                    signal,
                    out UINavigationTransition transition))
                return ExecuteTransition(transition);

            if (signal.Key.Equals("Back", StringComparison.OrdinalIgnoreCase))
                return ExecuteBack();

            return false;
        }

        private bool ExecuteCustomSignal(string signal)
        {
            if (Asset != null &&
                Asset.TryGetPortal(signal, out UINavigationTransition portal))
            {
                return ExecuteTransition(portal);
            }

            if (ActiveNode == null ||
                !ActiveNode.TryGetTransition(signal, out UINavigationTransition transition))
            {
                return false;
            }

            return ExecuteTransition(transition);
        }

        private bool ExecuteDestinationRoute(UINavigationNode destination)
        {
            if (Asset == null || destination == null || _destinationDepth >= 16)
                return false;

            UINavigationTransition portal;
            bool found =
                destination.DestinationAddressKind == UINavigationSignalAddressKind.Custom
                    ? Asset.TryGetPortal(
                        destination.DestinationCustomSignal,
                        out portal)
                    : Asset.TryGetPortal(
                        UINavigationTriggerKind.Signal,
                        destination.DestinationSignal,
                        false,
                        out portal);
            if (!found)
                return false;

            _destinationDepth++;
            try
            {
                bool handled = ExecuteTransition(portal);
                if (handled && destination.ClearHistory)
                {
                    _back.Clear();
                    _forward.Clear();
                }

                return handled;
            }
            finally
            {
                _destinationDepth--;
            }
        }

        private bool ExecuteToggle(UIKey toggle, bool value)
        {
            if (Asset != null &&
                Asset.TryGetPortal(
                    UINavigationTriggerKind.Toggle,
                    toggle,
                    value,
                    out UINavigationTransition portal))
                return ExecuteTransition(portal);

            if (ActiveNode == null ||
                !ActiveNode.TryGetToggleTransition(toggle, value, out UINavigationTransition transition))
            {
                return false;
            }

            return ExecuteTransition(transition);
        }

        private bool ExecuteView(
            UIKey view,
            UIViewOutputCondition condition)
        {
            if (ActiveNode == null ||
                !ActiveNode.TryGetViewTransition(view, condition, out UINavigationTransition transition))
            {
                return false;
            }

            return ExecuteTransition(transition);
        }

        private bool ExecuteTransition(UINavigationTransition transition)
        {
            UINavigationTransition previousTransition = _activeTransition;
            _activeTransition = transition;

            try
            {
                IReadOnlyList<UINavigationAction> actions = transition.Actions;
                for (int i = 0; i < actions.Count; i++)
                {
                    if (actions[i] != null)
                        ActionRequested?.Invoke(actions[i]);
                }

                if (transition.Kind == UINavigationTransitionKind.Back)
                    return ExecuteBack() || actions.Count > 0;

                return string.IsNullOrEmpty(transition.TargetNodeId)
                    ? actions.Count > 0
                    : ExecuteGoTo(transition.TargetNodeId, transition.Kind);
            }
            finally
            {
                _activeTransition = previousTransition;
            }
        }

        private bool ExecuteBack()
        {
            if (_back.Count == 0)
                return false;

            string previousId = _back.Peek();
            if (Asset == null || !Asset.TryGetNode(previousId, out UINavigationNode previous))
            {
                _back.Pop();
                return false;
            }

            return ApplyTransition(previous, UINavigationTransitionKind.Back);
        }

        private bool ExecuteForward()
        {
            if (_forward.Count == 0)
                return false;

            string nextId = _forward.Peek();
            if (Asset == null || !Asset.TryGetNode(nextId, out UINavigationNode next))
            {
                _forward.Pop();
                return false;
            }

            return ApplyTransition(next, UINavigationTransitionKind.Push, fromForward: true);
        }

        private bool ApplyTransition(
            UINavigationNode next,
            UINavigationTransitionKind kind,
            bool fromForward = false)
        {
            UINavigationNode previous = ActiveNode;

            if (next == null || ReferenceEquals(next, previous))
                return false;

            IReadOnlyList<UINavigationViewCommand> show = BuildShowCommands(previous, next);
            IReadOnlyList<UINavigationViewCommand> hide = BuildHideCommands(previous, next);

            UINavigationChange change = new UINavigationChange(previous, next, kind)
            {
                Transition = _activeTransition
            };
            NodeChanging?.Invoke(change);

            if (change.Cancel)
                return false;

            UpdateHistory(previous, kind, fromForward);
            ActiveNode = next;
            ResetDelayState(next);

            if (next.ClearHistory)
            {
                _back.Clear();
                _forward.Clear();
            }

            TrimHistory();

            HideCommandsRequested?.Invoke(hide);
            ShowCommandsRequested?.Invoke(show);
            NodeChanged?.Invoke(change);
            return true;
        }

        private void UpdateHistory(UINavigationNode previous, UINavigationTransitionKind kind, bool fromForward)
        {
            switch (kind)
            {
                case UINavigationTransitionKind.Push:
                    if (fromForward)
                        _forward.Pop();

                    if (previous != null)
                        _back.Push(previous.Id);

                    if (!fromForward)
                        _forward.Clear();
                    break;

                case UINavigationTransitionKind.Back:
                    _back.Pop();

                    if (previous != null)
                        _forward.Push(previous.Id);
                    break;

                case UINavigationTransitionKind.Replace:
                    break;
            }
        }

        private void TrimHistory()
        {
            if (MaxHistoryDepth <= 0 || _back.Count <= MaxHistoryDepth)
                return;

            string[] recent = _back.ToArray();
            _back.Clear();

            for (int i = MaxHistoryDepth - 1; i >= 0; i--)
                _back.Push(recent[i]);
        }

        private IReadOnlyList<UINavigationViewCommand> BuildShowCommands(
            UINavigationNode previous,
            UINavigationNode next)
        {
            _showCommands.Clear();
            AddAll(_showCommands, previous?.ShowOnExitCommands);
            AddAll(_showCommands, next.ShowOnEnterCommands);
            CopyValues(_showCommands, _showCommandBuffer);
            return _showCommandBuffer;
        }

        private IReadOnlyList<UINavigationViewCommand> BuildHideCommands(
            UINavigationNode previous,
            UINavigationNode next)
        {
            _hideCommands.Clear();
            AddAll(_hideCommands, previous?.HideOnExitCommands);
            AddAll(_hideCommands, next.HideOnEnterCommands);

            foreach (UIKey key in _showCommands.Keys)
                _hideCommands.Remove(key);

            CopyValues(_hideCommands, _hideCommandBuffer);
            return _hideCommandBuffer;
        }

        private static void AddAll(
            IDictionary<UIKey, UINavigationViewCommand> destination,
            IReadOnlyList<UINavigationViewCommand> source)
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                UINavigationViewCommand command = source[i];
                if (command.IsValid)
                    destination[command.View] = command;
            }
        }

        private static void CopyValues(
            Dictionary<UIKey, UINavigationViewCommand> source,
            List<UINavigationViewCommand> destination)
        {
            destination.Clear();
            foreach (UINavigationViewCommand command in source.Values)
                destination.Add(command);
        }

        private enum RequestKind
        {
            GoTo,
            Back,
            Forward,
            Signal,
            CustomSignal,
            Toggle,
            UIView
        }

        private readonly struct Request
        {
            public readonly RequestKind Kind;
            public readonly string NodeId;
            public readonly UIKey Signal;
            public readonly UINavigationTransitionKind TransitionKind;
            public readonly bool ToggleValue;
            public readonly UIViewOutputCondition ViewCondition;

            public Request(
                RequestKind kind,
                string nodeId,
                UIKey signal,
                UINavigationTransitionKind transitionKind,
                bool toggleValue = false,
                UIViewOutputCondition viewCondition = UIViewOutputCondition.Show)
            {
                Kind = kind;
                NodeId = nodeId;
                Signal = signal;
                TransitionKind = transitionKind;
                ToggleValue = toggleValue;
                ViewCondition = viewCondition;
            }
        }

        private void ResetDelayState(UINavigationNode node)
        {
            IReadOnlyList<UINavigationTransition> transitions = node?.Transitions;
            int count = transitions?.Count ?? 0;
            _delayTransitions = transitions;
            _delayStateCount = count;
            _hasAutomaticTransitions = false;

            for (int i = 0; i < count; i++)
            {
                UINavigationTriggerKind trigger = transitions[i]?.TriggerKind ?? default;
                if (trigger is UINavigationTriggerKind.TimeDelay or UINavigationTriggerKind.Random)
                {
                    _hasAutomaticTransitions = true;
                    break;
                }
            }

            if (!_hasAutomaticTransitions)
                return;

            if (_delayRemaining.Length < count)
            {
                int capacity = Mathf.NextPowerOfTwo(count);
                _delayRemaining = new float[capacity];
                _delayConsumed = new bool[capacity];
            }

            for (int i = 0; i < count; i++)
            {
                UINavigationTransition transition = transitions[i];
                _delayConsumed[i] = false;
                _delayRemaining[i] = transition != null &&
                                     transition.TriggerKind == UINavigationTriggerKind.TimeDelay
                    ? Mathf.Max(0f, transition.DelaySeconds)
                    : float.PositiveInfinity;
            }
        }

        private void EnsureDelayState(IReadOnlyList<UINavigationTransition> transitions)
        {
            if (!ReferenceEquals(_delayTransitions, transitions) ||
                _delayStateCount != (transitions?.Count ?? 0))
            {
                ResetDelayState(ActiveNode);
            }
        }
    }
}
