using System;
using System.Collections.Generic;
using System.IO;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Public, code-first authoring API for creating editable UI Navigation graphs.
    /// This API intentionally hides Graph Toolkit implementation types so editor tools,
    /// generators, and AI-authored scripts can create graphs without reflection.
    /// </summary>
    public sealed class UINavigationGraphBuilder
    {
        private readonly string _assetPath;
        private readonly bool _overwrite;
        private readonly List<ScreenNode> _screens = new();
        private readonly List<PortalNode> _portals = new();
        private readonly List<Connection> _connections = new();
        private StartNode _start;

        private UINavigationGraphBuilder(string assetPath, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("Graph asset path must not be empty.", nameof(assetPath));

            string normalized = assetPath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) &&
                !normalized.StartsWith("Packages/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Graph asset path must be project-relative and start with 'Assets/' or 'Packages/'.",
                    nameof(assetPath));
            }

            _assetPath = Path.ChangeExtension(normalized, UINavigationAuthoringGraph.Extension);
            _overwrite = overwrite;
        }

        /// <summary>
        /// Starts a new graph description. No files are changed until <see cref="Save"/> is called.
        /// </summary>
        public static UINavigationGraphBuilder Create(string assetPath, bool overwrite = false)
        {
            return new UINavigationGraphBuilder(assetPath, overwrite);
        }

        /// <summary>Adds the single Start node at the requested canvas position.</summary>
        public StartNode AddStart(Vector2 position)
        {
            if (_start != null)
                throw new InvalidOperationException("A UI Navigation graph can contain only one Start node.");

            _start = new StartNode(this, position);
            return _start;
        }

        /// <summary>Adds an editable Screen node at the requested canvas position.</summary>
        public ScreenNode AddScreen(string id, string displayName, Vector2 position)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Screen node ID must not be empty.", nameof(id));

            string normalizedId = id.Trim();
            if (_screens.Exists(item => string.Equals(item.Id, normalizedId, StringComparison.Ordinal)))
                throw new InvalidOperationException($"A Screen node with ID '{normalizedId}' already exists.");

            var node = new ScreenNode(
                this,
                normalizedId,
                string.IsNullOrWhiteSpace(displayName) ? normalizedId : displayName.Trim(),
                position);
            _screens.Add(node);
            return node;
        }

        /// <summary>Adds a Portal node at the requested canvas position.</summary>
        public PortalNode AddPortal(UIKey key, Vector2 position, string displayName = null)
        {
            if (!key.IsValid)
                throw new ArgumentException("UI key must contain both a category and key.", nameof(key));

            var node = new PortalNode(this, key, position, displayName);
            _portals.Add(node);
            return node;
        }

        /// <summary>Connects Start to a Screen's Enter port.</summary>
        public UINavigationGraphBuilder Connect(StartNode source, ScreenNode target)
        {
            RequireOwned(source, nameof(source));
            RequireOwned(target, nameof(target));
            _connections.Add(new Connection(source, null, target));
            return this;
        }

        /// <summary>Connects a Portal output to a Screen's Enter port.</summary>
        public UINavigationGraphBuilder Connect(PortalNode source, ScreenNode target)
        {
            RequireOwned(source, nameof(source));
            RequireOwned(target, nameof(target));
            _connections.Add(new Connection(source, null, target));
            return this;
        }

        /// <summary>Connects a Screen output to another Screen's Enter port.</summary>
        public UINavigationGraphBuilder Connect(OutputPort source, ScreenNode target)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            RequireOwned(source.Owner, nameof(source));
            RequireOwned(target, nameof(target));
            _connections.Add(new Connection(source.Owner, source.Definition, target));
            return this;
        }

        /// <summary>
        /// Creates or replaces the .uinavgraph file, imports its runtime asset, and optionally opens it.
        /// </summary>
        public UINavigationAsset Save(bool openGraph = false)
        {
            ValidateDescription();
            EnsureParentFolder(_assetPath);

            bool assetExists = AssetDatabase.LoadMainAssetAtPath(_assetPath) != null;
            if (assetExists && !_overwrite)
                throw new InvalidOperationException($"An asset already exists at '{_assetPath}'.");

            UINavigationAuthoringGraph graph = assetExists
                ? GraphDatabase.LoadGraph<UINavigationAuthoringGraph>(_assetPath)
                : GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(_assetPath);
            if (graph == null)
            {
                throw new InvalidOperationException(
                    assetExists
                        ? $"The existing asset is not a UI Navigation graph: '{_assetPath}'."
                        : $"Could not create UI Navigation graph at '{_assetPath}'.");
            }

            if (assetExists)
            {
                var existingNodes = new List<INode>();
                foreach (INode node in graph.GetNodes())
                    existingNodes.Add(node);
                foreach (INode node in existingNodes)
                    graph.RemoveNode(node);
            }

            var nodeMap = new Dictionary<NodeHandle, Node>();
            var startNode = new UINavigationStartNode { Position = _start.Position };
            graph.AddNode(startNode);
            nodeMap.Add(_start, startNode);

            foreach (ScreenNode description in _screens)
            {
                var node = new UINavigationUINode
                {
                    Position = description.Position,
                    InitialNodeId = description.Id,
                    InitialDisplayName = description.DisplayName,
                    InitialDescription = description.Description,
                    InitialClearHistory = description.ClearHistory,
                    InitialUseBack = description.UseBack,
                    InitialShowOnEnter = description.ShowOnEnter.ToArray(),
                    InitialHideOnEnter = description.HideOnEnter.ToArray(),
                    InitialShowOnExit = description.ShowOnExit.ToArray(),
                    InitialHideOnExit = description.HideOnExit.ToArray(),
                    InitialOutputs = description.Outputs.ToArray()
                };
                graph.AddNode(node);
                nodeMap.Add(description, node);
            }

            foreach (PortalNode description in _portals)
            {
                var condition = new UINavigationPortalCondition();
                condition.SetKey(description.Key);

                var node = new UINavigationPortalNode
                {
                    Position = description.Position,
                    InitialDisplayName = description.DisplayName,
                    InitialCondition = condition,
                    InitialHistory = description.History
                };
                graph.AddNode(node);
                nodeMap.Add(description, node);
            }

            foreach (Connection connection in _connections)
            {
                Node source = nodeMap[connection.Source];
                Node target = nodeMap[connection.Target];
                string outputName = connection.Output == null
                    ? (connection.Source is PortalNode ? UINavigationPortalNode.NextPort : UINavigationStartNode.StartPort)
                    : connection.Output.GetPortName();
                IPort output = source.GetOutputPortByName(outputName);
                IPort input = target.GetInputPortByName(UINavigationUINodeBase.EnterPort);
                if (output == null || input == null || !graph.Connect(output, input))
                {
                    throw new InvalidOperationException(
                        $"Could not connect '{connection.Source.Label}' to '{connection.Target.Label}'.");
                }
            }

            GraphDatabase.SaveGraph(graph);
            RegisterCatalogKeys();
            AssetDatabase.ImportAsset(
                _assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            UINavigationAsset asset = AssetDatabase.LoadAssetAtPath<UINavigationAsset>(_assetPath);
            if (asset == null)
                throw new InvalidOperationException($"Graph was saved but its runtime asset could not be loaded: '{_assetPath}'.");

            if (openGraph)
                AssetDatabase.OpenAsset(asset);

            return asset;
        }

        private void ValidateDescription()
        {
            if (_start == null)
                throw new InvalidOperationException("Add a Start node before saving the graph.");
            if (_screens.Count == 0)
                throw new InvalidOperationException("Add at least one Screen node before saving the graph.");

            int startConnections = _connections.FindAll(item => item.Source == _start).Count;
            if (startConnections != 1)
                throw new InvalidOperationException("The Start node must be connected to exactly one Screen.");

            foreach (ScreenNode screen in _screens)
            {
                foreach (UINavigationOutputDefinition output in screen.Outputs)
                {
                    int count = _connections.FindAll(item => item.Output == output).Count;
                    if (count != 1)
                    {
                        throw new InvalidOperationException(
                            $"Every output on Screen '{screen.Id}' must be connected exactly once.");
                    }
                }
            }

            foreach (PortalNode portal in _portals)
            {
                int count = _connections.FindAll(item => item.Source == portal).Count;
                if (count != 1)
                {
                    throw new InvalidOperationException(
                        $"Portal '{portal.Label}' must be connected to exactly one Screen.");
                }
            }
        }

        private void RegisterCatalogKeys()
        {
            var views = new HashSet<UIKey>();
            var buttons = new HashSet<UIKey>();
            var signals = new HashSet<UIKey>();

            foreach (ScreenNode screen in _screens)
            {
                views.UnionWith(screen.ShowOnEnter);
                views.UnionWith(screen.HideOnEnter);
                views.UnionWith(screen.ShowOnExit);
                views.UnionWith(screen.HideOnExit);

                foreach (UINavigationOutputDefinition output in screen.Outputs)
                {
                    if (!output.Key.IsValid)
                        continue;

                    switch (output.Trigger)
                    {
                        case UINavigationTriggerKind.UIButton:
                        case UINavigationTriggerKind.Toggle:
                            buttons.Add(output.Key);
                            break;
                        case UINavigationTriggerKind.Signal:
                            signals.Add(output.Key);
                            break;
                        case UINavigationTriggerKind.UIView:
                            views.Add(output.Key);
                            break;
                    }
                }
            }

            foreach (PortalNode portal in _portals)
            {
                if (portal.Key.IsValid)
                    buttons.Add(portal.Key);
            }

            UIKeyCatalog.instance.AddRange(views, UIKeyCatalogKind.View);
            UIKeyCatalog.instance.AddRange(buttons, UIKeyCatalogKind.Button);
            UIKeyCatalog.instance.AddRange(signals, UIKeyCatalogKind.Signal);
        }

        private void RequireOwned(NodeHandle node, string parameterName)
        {
            if (node == null)
                throw new ArgumentNullException(parameterName);
            if (node.Builder != this)
                throw new ArgumentException("The node belongs to a different graph builder.", parameterName);
        }

        private static void EnsureParentFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        /// <summary>Opaque base handle for a node owned by this builder.</summary>
        public abstract class NodeHandle
        {
            internal NodeHandle(UINavigationGraphBuilder builder, Vector2 position, string label)
            {
                Builder = builder;
                Position = position;
                Label = label;
            }

            internal UINavigationGraphBuilder Builder { get; }
            internal Vector2 Position { get; }
            internal string Label { get; }
        }

        /// <summary>Handle returned by <see cref="AddStart"/>.</summary>
        public sealed class StartNode : NodeHandle
        {
            internal StartNode(UINavigationGraphBuilder builder, Vector2 position)
                : base(builder, position, "Start")
            {
            }
        }

        /// <summary>Configurable Portal node handle.</summary>
        public sealed class PortalNode : NodeHandle
        {
            internal PortalNode(
                UINavigationGraphBuilder builder,
                UIKey key,
                Vector2 position,
                string displayName)
                : base(builder, position, string.IsNullOrWhiteSpace(displayName) ? $"Portal ({key.Key})" : displayName.Trim())
            {
                Key = key;
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? $"Portal ({key.Key})" : displayName.Trim();
            }

            public UIKey Key { get; }
            public string DisplayName { get; }
            public UINavigationTransitionKind History { get; private set; } = UINavigationTransitionKind.Push;

            public PortalNode WithHistory(UINavigationTransitionKind kind)
            {
                History = kind;
                return this;
            }
        }

        /// <summary>Configurable Screen node handle.</summary>
        public sealed class ScreenNode : NodeHandle
        {
            internal readonly List<UIKey> ShowOnEnter = new();
            internal readonly List<UIKey> HideOnEnter = new();
            internal readonly List<UIKey> ShowOnExit = new();
            internal readonly List<UIKey> HideOnExit = new();
            internal readonly List<UINavigationOutputDefinition> Outputs = new();

            internal ScreenNode(
                UINavigationGraphBuilder builder,
                string id,
                string displayName,
                Vector2 position)
                : base(builder, position, displayName)
            {
                Id = id;
                DisplayName = displayName;
            }

            public string Id { get; }
            public string DisplayName { get; }
            public string Description { get; private set; } = string.Empty;
            public bool ClearHistory { get; private set; }
            public bool UseBack { get; private set; }

            public ScreenNode WithDescription(string value)
            {
                Description = value ?? string.Empty;
                return this;
            }

            public ScreenNode WithHistory(bool clearOnEnter = false, bool useBack = false)
            {
                ClearHistory = clearOnEnter;
                UseBack = useBack;
                return this;
            }

            public ScreenNode ShowViewsOnEnter(params UIKey[] views) => AddViews(ShowOnEnter, views);
            public ScreenNode HideViewsOnEnter(params UIKey[] views) => AddViews(HideOnEnter, views);
            public ScreenNode ShowViewsOnExit(params UIKey[] views) => AddViews(ShowOnExit, views);
            public ScreenNode HideViewsOnExit(params UIKey[] views) => AddViews(HideOnExit, views);

            public OutputPort AddButtonOutput(UIKey key) =>
                AddOutput(new UINavigationOutputDefinition(
                    UINavigationTriggerKind.UIButton,
                    RequireKey(key),
                    0f,
                    UINavigationTransitionKind.Push));

            public OutputPort AddSignalOutput(UIKey key) =>
                AddOutput(new UINavigationOutputDefinition(
                    UINavigationTriggerKind.Signal,
                    RequireKey(key),
                    0f,
                    UINavigationTransitionKind.Push));

            public OutputPort AddDelayOutput(float seconds) =>
                AddOutput(new UINavigationOutputDefinition(
                    UINavigationTriggerKind.TimeDelay,
                    default,
                    Mathf.Max(0f, seconds),
                    UINavigationTransitionKind.Push));

            public OutputPort AddToggleOutput(UIKey key, ToggleCondition condition) =>
                AddOutput(new UINavigationOutputDefinition(
                    UINavigationTriggerKind.Toggle,
                    RequireKey(key),
                    0f,
                    condition switch
                    {
                        ToggleCondition.Off => UIToggleOutputCondition.Off,
                        ToggleCondition.Any => UIToggleOutputCondition.Any,
                        _ => UIToggleOutputCondition.On
                    },
                    UIViewOutputCondition.Show));

            public OutputPort AddViewOutput(UIKey key, UIViewOutputCondition condition) =>
                AddOutput(new UINavigationOutputDefinition(
                    UINavigationTriggerKind.UIView,
                    RequireKey(key),
                    0f,
                    UIToggleOutputCondition.On,
                    condition));

            private OutputPort AddOutput(UINavigationOutputDefinition definition)
            {
                Outputs.Add(definition);
                return new OutputPort(this, definition);
            }

            private ScreenNode AddViews(List<UIKey> target, UIKey[] views)
            {
                if (views == null)
                    return this;

                foreach (UIKey view in views)
                {
                    UIKey valid = RequireKey(view);
                    if (!target.Contains(valid))
                        target.Add(valid);
                }

                return this;
            }

            private static UIKey RequireKey(UIKey value)
            {
                if (!value.IsValid)
                    throw new ArgumentException("UI key must contain both a category and key.", nameof(value));
                return value;
            }
        }

        /// <summary>Opaque handle for a dynamic Screen output port.</summary>
        public sealed class OutputPort
        {
            internal OutputPort(ScreenNode owner, UINavigationOutputDefinition definition)
            {
                Owner = owner;
                Definition = definition;
            }

            internal ScreenNode Owner { get; }
            internal UINavigationOutputDefinition Definition { get; }
        }

        /// <summary>Value condition used by a Toggle output.</summary>
        public enum ToggleCondition
        {
            On,
            Off,
            Any
        }

        private sealed class Connection
        {
            internal Connection(NodeHandle source, UINavigationOutputDefinition output, ScreenNode target)
            {
                Source = source;
                Output = output;
                Target = target;
            }

            internal NodeHandle Source { get; }
            internal UINavigationOutputDefinition Output { get; }
            internal ScreenNode Target { get; }
        }
    }
}
