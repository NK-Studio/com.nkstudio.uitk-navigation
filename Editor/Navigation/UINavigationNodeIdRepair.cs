using System;
using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides UI Navigation Node Id Repair functionality.
    /// </summary>
    internal static class UINavigationNodeIdRepair
    {
        private static readonly HashSet<UINavigationAuthoringGraph> Pending = new();

        internal static void ScheduleEnsureUniqueNodeIds(UINavigationAuthoringGraph graph)
        {
            if (graph == null || Pending.Contains(graph) || !HasInvalidNodeIds(graph))
                return;

            Pending.Add(graph);
            EditorApplication.delayCall += () =>
            {
                Pending.Remove(graph);
                EnsureUniqueNodeIdsNow(graph);
            };
        }

        private static bool HasInvalidNodeIds(UINavigationAuthoringGraph graph)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationUINodeBase node in GetUINodes(graph))
            {
                string id = node.GetNodeId();
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                    return true;

                if (node is UINavigationRandomNode randomNode && HasDuplicateOutputIds(randomNode))
                    return true;
            }

            return false;
        }

        private static bool HasDuplicateOutputIds(UINavigationRandomNode randomNode)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationRandomOutputDefinition output in randomNode.GetOutputs())
            {
                if (output == null)
                    continue;

                if (!seen.Add(output.GetPortName()))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Attempts to repair output ids.
        /// </summary>
        private static bool TryRepairOutputIds(UINavigationRandomNode randomNode)
        {
            UINavigationRandomOutputDefinition[] outputs = randomNode.GetOutputs();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var repaired = new UINavigationRandomOutputDefinition[outputs.Length];
            bool changed = false;

            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationRandomOutputDefinition output = outputs[index];
                if (output == null)
                {
                    repaired[index] = new UINavigationRandomOutputDefinition(100f);
                    changed = true;
                    continue;
                }

                string id = output.GetPortName();
                if (seen.Add(id))
                {
                    repaired[index] = output;
                    continue;
                }

                string replacement = Guid.NewGuid().ToString("N");
                seen.Add(replacement);
                repaired[index] = new UINavigationRandomOutputDefinition(replacement, output.Weight);
                changed = true;
            }

            return changed &&
                   UINavigationNodeOptionWriter.TrySetValue(
                       randomNode.GetNodeOptionByName(UINavigationRandomNode.RandomOutputsOption),
                       new UINavigationRandomOutputCollection(repaired));
        }

        /// <summary>
        /// Performs the ensure unique node ids now operation.
        /// </summary>
        internal static bool EnsureUniqueNodeIdsNow(UINavigationAuthoringGraph graph)
        {
            if (graph == null)
                return false;

            try
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                bool changed = false;
                foreach (UINavigationUINodeBase node in GetUINodes(graph))
                {
                    if (node is UINavigationRandomNode randomNode && TryRepairOutputIds(randomNode))
                        changed = true;

                    string id = node.GetNodeId();
                    if (!string.IsNullOrEmpty(id) && seen.Add(id))
                        continue;

                    string replacement = Guid.NewGuid().ToString("N");
                    if (!UINavigationNodeOptionWriter.TrySetValue(
                            node.GetNodeOptionByName(UINavigationUINodeBase.NodeIdOption),
                            new UINavigationNodeIdentity(replacement)))
                        continue;

                    seen.Add(replacement);
                    changed = true;
                }

                if (changed)
                    GraphDatabase.SaveGraph(graph);

                return changed;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }

        private static IEnumerable<UINavigationUINodeBase> GetUINodes(
            UINavigationAuthoringGraph graph)
        {
            return graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationUINodeBase)
                .Select(node => (UINavigationUINodeBase)node)
                .ToArray();
        }
    }
}
