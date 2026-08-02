using System.Collections.Generic;
using Unity.GraphToolkit.Editor;
#if UNITY_6000_6_OR_NEWER
using System;
using Unity.GraphToolkit.Editor.GraphVisualization;
using UnityEditor;
using UnityEngine;
using VisualizationContext = Unity.GraphToolkit.Editor.GraphVisualization.Context;
#endif

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides UI Navigation Random Port Preview functionality.
    /// </summary>
    internal static class UINavigationRandomPortPreview
    {
#if UNITY_6000_6_OR_NEWER
        private const double SyncInterval = 0.1d;

        private static readonly List<UINavigationAuthoringGraph> Graphs = new();
        private static readonly List<UINavigationAuthoringGraph> Dropped = new();
        private static readonly List<(IPort Port, string Text)> Shares = new();

        private static double _nextSyncTime;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.update -= Sync;
            EditorApplication.update += Sync;
        }
#endif

        /// <summary>
        /// Registers member.
        /// </summary>
        internal static void Register(UINavigationAuthoringGraph graph)
        {
#if UNITY_6000_6_OR_NEWER
            if (graph == null || Graphs.Contains(graph))
                return;

            Graphs.Add(graph);
#endif
        }

#if UNITY_6000_6_OR_NEWER
        private static void Sync()
        {
            if (Graphs.Count == 0 || EditorApplication.timeSinceStartup < _nextSyncTime)
                return;

            _nextSyncTime = EditorApplication.timeSinceStartup + SyncInterval;

            foreach (UINavigationAuthoringGraph graph in Graphs)
            {
                try
                {
                    if (!Apply(graph))
                        Dropped.Add(graph);
                }
                catch (Exception exception)
                {
                    Dropped.Add(graph);
                    Debug.LogException(exception);
                }
            }

            if (Dropped.Count == 0)
                return;

            foreach (UINavigationAuthoringGraph graph in Dropped)
                Graphs.Remove(graph);

            Dropped.Clear();
        }

        /// <summary>
        /// Applies member.
        /// </summary>
        private static bool Apply(UINavigationAuthoringGraph graph)
        {
            if (graph == null || string.IsNullOrEmpty(GraphDatabase.GetGraphAssetPath(graph)))
                return false;

            VisualizationContext context = Registry.GetActiveContext(graph.ID)
                                           ?? Registry.CreateVisualizationContext(graph.ID);
            if (context == null || !context.IsValid)
                return true;

            if (!context.IsGraphLoaded || !context.PortPreviewEnabled)
                return true;

            foreach (INode node in graph.GetNodes())
            {
                if (node is not UINavigationRandomNode random)
                    continue;

                random.CollectPortShares(Shares);
                foreach ((IPort port, string text) in Shares)
                {
                    PortReference reference = context.GetPortReference(port.ID);

                    if (reference.TryGetPreview(out string current) &&
                        string.Equals(current, text, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    reference.SetPreview(text);
                }
            }

            return true;
        }
#endif
    }
}
