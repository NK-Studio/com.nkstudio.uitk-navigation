using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEngine;

namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Provides UI Navigation Asset functionality.
    /// </summary>
    public sealed class UINavigationAsset : ScriptableObject
    {
        [SerializeField]
        private string startNodeId;

        [SerializeField]
        private UINavigationNode[] nodes = Array.Empty<UINavigationNode>();

        [SerializeField]
        private UINavigationTransition[] portals = Array.Empty<UINavigationTransition>();

        private Dictionary<string, UINavigationNode> _index;

        /// <summary>
        /// Gets the start node id.
        /// </summary>
        public string StartNodeId => startNodeId;

        /// <summary>
        /// Gets the nodes.
        /// </summary>
        internal IReadOnlyList<UINavigationNode> Nodes => nodes;
        /// <summary>
        /// Gets the portals.
        /// </summary>
        internal IReadOnlyList<UINavigationTransition> Portals => portals;

        internal bool TryGetPortal(
            UINavigationTriggerKind triggerKind,
            UIKey key,
            bool toggleValue,
            out UINavigationTransition transition)
        {
            for (int i = 0; i < portals.Length; i++)
            {
                UINavigationTransition candidate = portals[i];
                if (candidate == null ||
                    candidate.TriggerKind != triggerKind ||
                    candidate.Signal != key)
                    continue;

                if (triggerKind == UINavigationTriggerKind.Toggle &&
                    candidate.ToggleValue != toggleValue)
                    continue;

                transition = candidate;
                return true;
            }

            transition = null;
            return false;
        }

        /// <summary>
        /// Attempts to get node.
        /// </summary>
        internal bool TryGetNode(string nodeId, out UINavigationNode node)
        {
            if (string.IsNullOrEmpty(nodeId))
            {
                node = null;
                return false;
            }

            EnsureIndex();
            return _index.TryGetValue(nodeId, out node);
        }

        /// <summary>
        /// Gets the start node.
        /// </summary>
        internal UINavigationNode GetStartNode()
        {
            return TryGetNode(startNodeId, out UINavigationNode node) ? node : null;
        }

        private void OnEnable()
        {
            _index = null;
        }

        private void EnsureIndex()
        {
            if (_index != null)
                return;

            _index = new Dictionary<string, UINavigationNode>(nodes.Length, StringComparer.Ordinal);

            for (int i = 0; i < nodes.Length; i++)
            {
                UINavigationNode node = nodes[i];
                if (node == null || string.IsNullOrEmpty(node.Id))
                    continue;

                _index[node.Id] = node;
            }
        }

        /// <summary>
        /// Sets c on te nt s.
        /// </summary>
        internal void SetContents(
            string start,
            UINavigationNode[] graphNodes,
            UINavigationTransition[] portalTransitions = null)
        {
            startNodeId = start;
            nodes = graphNodes ?? Array.Empty<UINavigationNode>();
            portals = portalTransitions ?? Array.Empty<UINavigationTransition>();
            _index = null;
        }

    }
}
