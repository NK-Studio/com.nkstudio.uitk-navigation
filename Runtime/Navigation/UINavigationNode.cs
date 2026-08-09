using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEngine;

namespace NKStudio.UITKNavigation.Navigation
{
    [Serializable]
    internal sealed class UINavigationNode
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private string description;

        [SerializeField]
        private bool clearHistory;

        [SerializeField]
        private bool useBack;

        [SerializeField]
        private UIKey[] showOnEnter = Array.Empty<UIKey>();

        [SerializeField]
        private UIKey[] hideOnEnter = Array.Empty<UIKey>();

        [SerializeField]
        private UIKey[] showOnExit = Array.Empty<UIKey>();

        [SerializeField]
        private UIKey[] hideOnExit = Array.Empty<UIKey>();

        [SerializeField]
        private UINavigationViewCommand[] showOnEnterCommands =
            Array.Empty<UINavigationViewCommand>();

        [SerializeField]
        private UINavigationViewCommand[] hideOnEnterCommands =
            Array.Empty<UINavigationViewCommand>();

        [SerializeField]
        private UINavigationViewCommand[] showOnExitCommands =
            Array.Empty<UINavigationViewCommand>();

        [SerializeField]
        private UINavigationViewCommand[] hideOnExitCommands =
            Array.Empty<UINavigationViewCommand>();

        [SerializeField]
        private UINavigationTransition[] transitions = Array.Empty<UINavigationTransition>();

        [SerializeField]
        private bool destination;

        [SerializeField]
        private UINavigationSignalAddressKind destinationAddressKind;

        [SerializeField]
        private UIKey destinationSignal;

        [SerializeField]
        private string destinationCustomSignal;

        public UINavigationNode(
            string id,
            string displayName,
            bool clearHistory,
            bool useBack = false,
            string description = "")
        {
            this.id = id;
            this.displayName = displayName;
            this.clearHistory = clearHistory;
            this.useBack = useBack;
            this.description = description;
        }

        public string Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public bool ClearHistory => clearHistory;
        public bool UseBack => useBack;
        public IReadOnlyList<UIKey> ShowOnEnter => showOnEnter;
        public IReadOnlyList<UIKey> HideOnEnter => hideOnEnter;
        public IReadOnlyList<UIKey> ShowOnExit => showOnExit;
        public IReadOnlyList<UIKey> HideOnExit => hideOnExit;
        public IReadOnlyList<UINavigationViewCommand> ShowOnEnterCommands => showOnEnterCommands;
        public IReadOnlyList<UINavigationViewCommand> HideOnEnterCommands => hideOnEnterCommands;
        public IReadOnlyList<UINavigationViewCommand> ShowOnExitCommands => showOnExitCommands;
        public IReadOnlyList<UINavigationViewCommand> HideOnExitCommands => hideOnExitCommands;
        public IReadOnlyList<UINavigationTransition> Transitions => transitions;
        internal bool IsDestination => destination;
        internal UINavigationSignalAddressKind DestinationAddressKind =>
            destinationAddressKind;
        internal UIKey DestinationSignal => destinationSignal;
        internal string DestinationCustomSignal =>
            destinationCustomSignal ?? string.Empty;

        public bool TryGetTransition(
            UINavigationTriggerKind triggerKind,
            UIKey signal,
            out UINavigationTransition transition)
        {
            if (signal.IsValid)
            {
                for (int i = 0; i < transitions.Length; i++)
                {
                    if (transitions[i] != null &&
                        transitions[i].TriggerKind == triggerKind &&
                        transitions[i].Signal == signal)
                    {
                        transition = transitions[i];
                        return true;
                    }
                }
            }

            transition = null;
            return false;
        }

        public bool TryGetTransition(UIKey signal, out UINavigationTransition transition)
        {
            return TryGetTransition(
                UINavigationTriggerKind.Signal,
                signal,
                out transition);
        }

        public bool TryGetTransition(
            string customSignal,
            out UINavigationTransition transition)
        {
            if (!string.IsNullOrEmpty(customSignal))
            {
                for (int i = 0; i < transitions.Length; i++)
                {
                    UINavigationTransition candidate = transitions[i];
                    if (candidate != null &&
                        candidate.TriggerKind == UINavigationTriggerKind.Signal &&
                        string.Equals(
                            candidate.CustomSignal,
                            customSignal,
                            StringComparison.Ordinal))
                    {
                        transition = candidate;
                        return true;
                    }
                }
            }

            transition = null;
            return false;
        }

        public bool TryGetToggleTransition(
            UIKey toggle,
            bool value,
            out UINavigationTransition transition)
        {
            UINavigationTransition any = null;

            if (toggle.IsValid)
            {
                for (int i = 0; i < transitions.Length; i++)
                {
                    UINavigationTransition candidate = transitions[i];
                    if (candidate == null ||
                        candidate.TriggerKind != UINavigationTriggerKind.Toggle ||
                        candidate.Signal != toggle)
                    {
                        continue;
                    }

                    if (candidate.ToggleCondition == UIToggleOutputCondition.Any)
                    {
                        any ??= candidate;
                        continue;
                    }

                    if (candidate.MatchesToggle(value))
                    {
                        transition = candidate;
                        return true;
                    }
                }
            }

            transition = any;
            return transition != null;
        }

        public bool TryGetViewTransition(
            UIKey view,
            UIViewOutputCondition condition,
            out UINavigationTransition transition)
        {
            if (view.IsValid)
            {
                for (int i = 0; i < transitions.Length; i++)
                {
                    UINavigationTransition candidate = transitions[i];
                    if (candidate != null &&
                        candidate.TriggerKind == UINavigationTriggerKind.UIView &&
                        candidate.Signal == view &&
                        candidate.ViewCondition == condition)
                    {
                        transition = candidate;
                        return true;
                    }
                }
            }

            transition = null;
            return false;
        }

        internal void SetContents(
            UIKey[] enterShow,
            UIKey[] enterHide,
            UIKey[] exitShow,
            UIKey[] exitHide,
            UINavigationTransition[] nodeTransitions)
        {
            SetContents(
                ToCommands(enterShow),
                ToCommands(enterHide),
                ToCommands(exitShow),
                ToCommands(exitHide),
                nodeTransitions);
        }

        internal void SetContents(
            UINavigationViewCommand[] enterShow,
            UINavigationViewCommand[] enterHide,
            UINavigationViewCommand[] exitShow,
            UINavigationViewCommand[] exitHide,
            UINavigationTransition[] nodeTransitions)
        {
            showOnEnterCommands = enterShow ?? Array.Empty<UINavigationViewCommand>();
            hideOnEnterCommands = enterHide ?? Array.Empty<UINavigationViewCommand>();
            showOnExitCommands = exitShow ?? Array.Empty<UINavigationViewCommand>();
            hideOnExitCommands = exitHide ?? Array.Empty<UINavigationViewCommand>();
            showOnEnter = ToKeys(showOnEnterCommands);
            hideOnEnter = ToKeys(hideOnEnterCommands);
            showOnExit = ToKeys(showOnExitCommands);
            hideOnExit = ToKeys(hideOnExitCommands);
            transitions = nodeTransitions ?? Array.Empty<UINavigationTransition>();
        }

        internal void SetDestination(
            UINavigationSignalAddressKind addressKind,
            UIKey databaseSignal,
            string customSignal)
        {
            destination = true;
            destinationAddressKind = addressKind;
            destinationSignal = databaseSignal;
            destinationCustomSignal = customSignal ?? string.Empty;
        }

        private static UINavigationViewCommand[] ToCommands(UIKey[] keys)
        {
            if (keys == null || keys.Length == 0)
                return Array.Empty<UINavigationViewCommand>();

            var result = new UINavigationViewCommand[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                result[i] = new UINavigationViewCommand(keys[i]);
            return result;
        }

        private static UIKey[] ToKeys(UINavigationViewCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
                return Array.Empty<UIKey>();

            var result = new UIKey[commands.Length];
            for (int i = 0; i < commands.Length; i++)
                result[i] = commands[i].View;
            return result;
        }
    }
}
