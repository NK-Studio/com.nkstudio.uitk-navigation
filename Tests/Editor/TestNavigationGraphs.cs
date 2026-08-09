using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides Test Navigation Graph Builder functionality.
    /// </summary>
    internal sealed class TestNavigationGraphBuilder
    {
        private readonly List<UINavigationNode> _nodes = new List<UINavigationNode>();
        private readonly List<Object> _created = new List<Object>();
        private readonly Dictionary<string, List<UINavigationTransition>> _transitions =
            new Dictionary<string, List<UINavigationTransition>>();
        private readonly Dictionary<string, ViewLists> _viewLists = new Dictionary<string, ViewLists>();
        private readonly List<UINavigationTransition> _portals = new List<UINavigationTransition>();

        private string _startNodeId;

        /// <summary>
        /// Adds n od e.
        /// </summary>
        public TestNavigationGraphBuilder AddNode(string id, bool clearHistory = false, bool useBack = false)
        {
            _nodes.Add(new UINavigationNode(id, id, clearHistory, useBack));
            _transitions[id] = new List<UINavigationTransition>();
            _viewLists[id] = new ViewLists();
            _startNodeId ??= id;
            return this;
        }

        /// <summary>
        /// Adds t ra ns it io n.
        /// </summary>
        public TestNavigationGraphBuilder AddTransition(
            string fromNodeId,
            string signalKey,
            string targetNodeId,
            UINavigationTransitionKind kind)
        {
            return AddTransition(
                fromNodeId,
                signalKey,
                targetNodeId,
                kind,
                System.Array.Empty<UINavigationAction>());
        }

        public TestNavigationGraphBuilder AddTransition(
            string fromNodeId,
            string signalKey,
            string targetNodeId,
            UINavigationTransitionKind kind,
            params UINavigationAction[] actions)
        {
            _transitions[fromNodeId].Add(
                new UINavigationTransition(
                    new UIKey("Test", signalKey),
                    targetNodeId,
                    kind,
                    actions));
            return this;
        }

        public TestNavigationGraphBuilder AddTransition(
            string fromNodeId,
            UIKey signal,
            string targetNodeId,
            UINavigationTransitionKind kind)
        {
            _transitions[fromNodeId].Add(
                new UINavigationTransition(signal, targetNodeId, kind));
            return this;
        }

        public TestNavigationGraphBuilder AddOutput(
            string fromNodeId,
            UINavigationTriggerKind trigger,
            string key,
            float delaySeconds,
            bool toggleValue,
            string targetNodeId,
            UINavigationTransitionKind kind = UINavigationTransitionKind.Push)
        {
            return AddOutput(
                fromNodeId,
                trigger,
                key,
                delaySeconds,
                toggleValue
                    ? UIToggleOutputCondition.On
                    : UIToggleOutputCondition.Off,
                UIViewOutputCondition.Show,
                targetNodeId,
                kind);
        }

        public TestNavigationGraphBuilder AddOutput(
            string fromNodeId,
            UINavigationTriggerKind trigger,
            string key,
            float delaySeconds,
            UIToggleOutputCondition toggleCondition,
            UIViewOutputCondition viewCondition,
            string targetNodeId,
            UINavigationTransitionKind kind = UINavigationTransitionKind.Push)
        {
            _transitions[fromNodeId].Add(
                new UINavigationTransition(
                    trigger,
                    new UIKey("Test", key),
                    delaySeconds,
                    toggleCondition,
                    viewCondition,
                    100f,
                    targetNodeId,
                    kind,
                    System.Array.Empty<UINavigationAction>()));
            return this;
        }

        public TestNavigationGraphBuilder AddPortal(
            UINavigationTriggerKind trigger,
            string key,
            bool toggleValue,
            string targetNodeId,
            UINavigationTransitionKind kind = UINavigationTransitionKind.Push)
        {
            _portals.Add(new UINavigationTransition(
                trigger,
                new UIKey("Test", key),
                0f,
                toggleValue,
                targetNodeId,
                kind,
                System.Array.Empty<UINavigationAction>()));
            return this;
        }

        public TestNavigationGraphBuilder AddCustomTransition(
            string key,
            string targetNodeId,
            params string[] sourceNodeIds)
        {
            foreach (string sourceNodeId in sourceNodeIds)
            {
                _transitions[sourceNodeId].Add(new UINavigationTransition(
                    UINavigationTriggerKind.Signal,
                    default,
                    key,
                    0f,
                    UIToggleOutputCondition.On,
                    UIViewOutputCondition.Show,
                    100f,
                    targetNodeId,
                    UINavigationTransitionKind.Push,
                    System.Array.Empty<UINavigationAction>()));
            }

            return this;
        }

        /// <summary>
        /// Adds v ie w.
        /// </summary>
        public TestNavigationGraphBuilder AddView(string nodeId, ViewSlot slot, UIKey id, UIViewTransitionMode mode = UIViewTransitionMode.Animated)
        {
            ViewLists lists = _viewLists[nodeId];
            var command = new UINavigationViewCommand(id, mode);

            switch (slot)
            {
                case ViewSlot.ShowOnEnter: lists.ShowOnEnter.Add(command); break;
                case ViewSlot.HideOnEnter: lists.HideOnEnter.Add(command); break;
                case ViewSlot.ShowOnExit: lists.ShowOnExit.Add(command); break;
                case ViewSlot.HideOnExit: lists.HideOnExit.Add(command); break;
            }

            return this;
        }

        /// <summary>
        /// Creates v ie wi d.
        /// </summary>
        public UIKey CreateViewId(string name)
        {
            return new UIKey("Test", name);
        }

        /// <summary>
        /// Builds member.
        /// </summary>
        public UINavigationAsset Build()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                UINavigationNode node = _nodes[i];
                ViewLists lists = _viewLists[node.Id];

                node.SetContents(
                    lists.ShowOnEnter.ToArray(),
                    lists.HideOnEnter.ToArray(),
                    lists.ShowOnExit.ToArray(),
                    lists.HideOnExit.ToArray(),
                    _transitions[node.Id].ToArray());
            }

            UINavigationAsset asset = ScriptableObject.CreateInstance<UINavigationAsset>();
            asset.SetContents(
                _startNodeId,
                _nodes.ToArray(),
                _portals.ToArray());
            _created.Add(asset);
            return asset;
        }

        /// <summary>
        /// Releases the resources used by this instance.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        /// <summary>
        /// Defines the available View Slot values.
        /// </summary>
        public enum ViewSlot
        {
            ShowOnEnter,
            HideOnEnter,
            ShowOnExit,
            HideOnExit
        }

        private sealed class ViewLists
        {
            public readonly List<UINavigationViewCommand> ShowOnEnter = new();
            public readonly List<UINavigationViewCommand> HideOnEnter = new();
            public readonly List<UINavigationViewCommand> ShowOnExit = new();
            public readonly List<UINavigationViewCommand> HideOnExit = new();
        }
    }
}
