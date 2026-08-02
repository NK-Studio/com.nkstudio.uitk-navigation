#if UNITY_6000_6_OR_NEWER
using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using Unity.GraphToolkit.Editor.GraphVisualization;
using UnityEditor;
using UnityEngine;
using ZLinq;
using VisualizationContext = Unity.GraphToolkit.Editor.GraphVisualization.Context;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides UI Navigation Graph Visualizer functionality.
    /// </summary>
    [InitializeOnLoad]
    internal static class UINavigationGraphVisualizer
    {
        private const string EnabledPrefKey = "NKStudio.UITKNavigation.FollowPlayMode";

        private const float ActiveNodeAnimationSpeed = 1f;

        private const double SyncInterval = 0.25d;

        private const double ContextLoadTimeout = 0.75d;

        private static readonly Dictionary<string, UINavigationUINodeBase> NodesById = new();

        private static UINavigationAsset _boundAsset;
        private static UINavigationAuthoringGraph _graph;
        private static VisualizationContext _context;

        private static Hash128 _animatedNode;
        private static string _animatedNodeId;
        private static bool _hasAnimatedNode;
        private static EditorWindow _boundGraphWindow;
        private static double _contextCreatedTime;
        private static double _nextSyncTime;

        static UINavigationGraphVisualizer()
        {
            UINavigationEvents.NodeChanged += OnNodeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += Sync;
        }

        /// <summary>
        /// Gets the enabled.
        /// </summary>
        internal static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set
            {
                if (Enabled == value)
                    return;

                EditorPrefs.SetBool(EnabledPrefKey, value);
                if (!value)
                    ReleaseBinding();
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.EnteredEditMode)
                ReleaseBinding();
        }

        private static void OnNodeChanged(UINavigationChange change)
        {
            if (!Enabled || change?.Next == null)
                return;

            try
            {
                UINavigationService service = UINavigatorBehaviour.Active?.Service;
                UINavigationAsset asset = service?.Asset;
                if (!TryGetAvailableGraphWindow(asset, out EditorWindow graphWindow))
                {
                    ReleaseBinding();
                    return;
                }

                if (!TryEnsureBinding(asset, graphWindow) || !_context.IsGraphLoaded)
                    return;

                StopNodeMotion();
                PlayNodeMotion(change.Next.Id);
            }
            catch (ArgumentException)
            {
                ReleaseBinding();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseBinding();
            }
        }

        /// <summary>
        /// Performs the sync operation.
        /// </summary>
        private static void Sync()
        {
            if (!Enabled || !EditorApplication.isPlaying)
                return;

            if (EditorApplication.timeSinceStartup < _nextSyncTime)
                return;

            _nextSyncTime = EditorApplication.timeSinceStartup + SyncInterval;

            try
            {
                UINavigationService service = UINavigatorBehaviour.Active?.Service;
                UINavigationAsset asset = service?.Asset;
                if (!TryGetAvailableGraphWindow(asset, out EditorWindow graphWindow))
                {
                    ReleaseBinding();
                    return;
                }

                string activeId = service?.ActiveNode?.Id;
                if (string.IsNullOrEmpty(activeId))
                {
                    StopNodeMotion();
                    return;
                }

                if (!TryEnsureBinding(asset, graphWindow))
                    return;

                if (!_context.IsGraphLoaded)
                {
                    InvalidateNodeMotionCache();
                    return;
                }

                if (_hasAnimatedNode && _animatedNodeId == activeId)
                    return;

                StopNodeMotion();
                PlayNodeMotion(activeId);
            }
            catch (ArgumentException)
            {
                ReleaseBinding();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ReleaseBinding();
            }
        }

        private static void PlayNodeMotion(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) ||
                !NodesById.TryGetValue(nodeId, out UINavigationUINodeBase node))
            {
                return;
            }

            NodeReference reference = _context.GetNodeReference(node.ID);
            _context.Motion.Play(reference, ActiveNodeAnimationSpeed);

            _animatedNode = node.ID;
            _animatedNodeId = nodeId;
            _hasAnimatedNode = true;
        }

        private static void StopNodeMotion()
        {
            if (!_hasAnimatedNode)
                return;

            if (CanTouchGraph())
            {
                NodeReference node = _context.GetNodeReference(_animatedNode);
                node.ClearCustomization();
            }

            InvalidateNodeMotionCache();
        }

        private static void InvalidateNodeMotionCache()
        {
            _animatedNode = default;
            _hasAnimatedNode = false;
            _animatedNodeId = null;
        }

        private static bool IsGraphWindowForAsset(EditorWindow window, UINavigationAsset asset)
        {
            if (window == null || asset == null)
                return false;

            string windowType = window.GetType().FullName;
            string title = window.titleContent?.text;
            return string.Equals(
                       windowType,
                       "Unity.GraphToolkit.Editor.Implementation.GraphViewEditorWindowImp",
                       StringComparison.Ordinal) &&
                   !string.IsNullOrEmpty(title) &&
                   title.IndexOf(asset.name, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Attempts to get available graph window.
        /// </summary>
        private static bool TryGetAvailableGraphWindow(
            UINavigationAsset asset,
            out EditorWindow graphWindow)
        {
            graphWindow = null;
            if (asset == null)
                return false;

            EditorWindow[] windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow candidate = windows[i];
                if (IsGraphWindowForAsset(candidate, asset) &&
                    candidate.rootVisualElement?.panel != null)
                {
                    graphWindow = candidate;
                    break;
                }
            }

            if (graphWindow == null)
                return false;

            for (int i = 0; i < windows.Length; i++)
            {
                EditorWindow candidate = windows[i];
                if (candidate != null && candidate != graphWindow && candidate.maximized)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether touch graph.
        /// </summary>
        private static bool CanTouchGraph()
        {
            return _context != null && _context.IsValid && _context.IsGraphLoaded;
        }

        private static bool TryEnsureBinding(UINavigationAsset asset, EditorWindow graphWindow)
        {
            if (asset == null || graphWindow == null)
            {
                ReleaseBinding();
                return false;
            }

            if (_boundAsset == asset &&
                ReferenceEquals(_boundGraphWindow, graphWindow) &&
                _context != null &&
                _context.IsValid)
            {
                if (_context.IsGraphLoaded ||
                    EditorApplication.timeSinceStartup - _contextCreatedTime < ContextLoadTimeout)
                {
                    return true;
                }

            }

            ReleaseBinding();

            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path))
                return false;

            var graph = GraphDatabase.LoadGraph<UINavigationAuthoringGraph>(path);
            if (graph == null)
                return false;

            VisualizationContext context = Registry.CreateVisualizationContext(graph.ID);
            if (context == null || !context.IsValid)
            {
                context?.Dispose();
                return false;
            }

            NodesById.Clear();
            foreach (UINavigationUINodeBase node in graph.GetNodes()
                         .AsValueEnumerable()
                         .Where(n => n is UINavigationUINodeBase)
                         .Select(n => (UINavigationUINodeBase)n))
            {
                string id = node.GetNodeId();
                if (!string.IsNullOrEmpty(id))
                    NodesById[id] = node;
            }

            _graph = graph;
            _context = context;
            _boundGraphWindow = graphWindow;
            _contextCreatedTime = EditorApplication.timeSinceStartup;
            _boundAsset = asset;
            return true;
        }

        private static void ReleaseBinding()
        {
            if (_context != null)
            {
                try
                {
                    if (_context.IsValid && _context.IsGraphLoaded)
                        _context.ClearAllVisualization();
                }
                catch (Exception)
                {
                    // ignored
                }

                try
                {
                    _context.Dispose();
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            _context = null;
            _boundGraphWindow = null;
            _contextCreatedTime = 0d;
            _graph = null;
            _boundAsset = null;
            InvalidateNodeMotionCache();
            NodesById.Clear();
        }
    }
}
#endif
