using System;
using System.IO;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    internal static class UINavigationGraphAssetUtility
    {
        private const string DefaultAssetName = "UI Navigation Graph.uinavgraph";
        private const string SampleAssetPath = "Assets/Sample.uinavgraph";

        [MenuItem("Tools/UI Navigation/Rebuild Sample Graph")]
        private static void RebuildSampleGraph()
        {
            UINavigationAuthoringGraph graph = File.Exists(SampleAssetPath)
                ? GraphDatabase.LoadGraph<UINavigationAuthoringGraph>(SampleAssetPath)
                : GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(SampleAssetPath);
            if (graph == null)
            {
                Debug.LogError($"UI Navigation 샘플 그래프를 다시 만들지 못했습니다: {SampleAssetPath}");
                return;
            }

            foreach (INode node in graph.GetNodes().AsValueEnumerable().ToArray())
                graph.RemoveNode(node);

            UIKey main = new UIKey("Demo", "Main");
            UIKey shop = new UIKey("Demo", "Shop");
            UIKey popup = new UIKey("Demo", "Popup");

            var start = new UINavigationStartNode { Position = new Vector2(40f, 220f) };
            var mainToShop = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Demo", "Shop"),
                0f,
                UINavigationTransitionKind.Push);
            var mainToPopup = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Demo", "Popup"),
                0f,
                UINavigationTransitionKind.Push);
            var shopToPopup = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Demo", "Popup"),
                0f,
                UINavigationTransitionKind.Push);

            var mainScreen = CreateUI(
                "main", "Main", new Vector2(300f, 100f), false,
                new[] { main }, new[] { shop, popup }, new[] { main },
                new[] { mainToShop, mainToPopup });
            var shopScreen = CreateUI(
                "shop", "Shop", new Vector2(820f, 40f), true,
                new[] { shop }, new[] { main, popup }, new[] { shop },
                new[] { shopToPopup });
            var popupScreen = CreateUI(
                "popup", "Popup", new Vector2(820f, 380f), true,
                new[] { popup }, new[] { main, shop }, new[] { popup },
                Array.Empty<UINavigationOutputDefinition>());

            graph.AddNode(start);
            graph.AddNode(mainScreen);
            graph.AddNode(shopScreen);
            graph.AddNode(popupScreen);

            Connect(graph, start, UINavigationStartNode.StartPort, mainScreen, UINavigationUINodeBase.EnterPort);
            Connect(graph, mainScreen, mainToShop.GetPortName(), shopScreen, UINavigationUINodeBase.EnterPort);
            Connect(graph, mainScreen, mainToPopup.GetPortName(), popupScreen, UINavigationUINodeBase.EnterPort);
            Connect(graph, shopScreen, shopToPopup.GetPortName(), popupScreen, UINavigationUINodeBase.EnterPort);

            GraphDatabase.SaveGraph(graph);
            UIKeyCatalog.instance.AddRange(
                new[] { main, shop, popup },
                UIKeyCatalogKind.View);
            UIKeyCatalog.instance.AddRange(
                new[] { shop, popup },
                UIKeyCatalogKind.Signal);
            AssetDatabase.ImportAsset(
                SampleAssetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        private static UINavigationUINode CreateUI(
            string id,
            string title,
            Vector2 position,
            bool useBack,
            UIKey[] showOnEnter,
            UIKey[] hideOnEnter,
            UIKey[] hideOnExit,
            UINavigationOutputDefinition[] outputs)
        {
            return new UINavigationUINode
            {
                Position = position,
                InitialNodeId = id,
                InitialDisplayName = title,
                InitialUseBack = useBack,
                InitialShowOnEnter = showOnEnter,
                InitialHideOnEnter = hideOnEnter,
                InitialHideOnExit = hideOnExit,
                InitialOutputs = outputs
            };
        }

        private static void Connect(Graph graph, Node from, string output, Node to, string input)
        {
            if (!graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName(input)))
                Debug.LogError($"UI Navigation 샘플 연결을 만들지 못했습니다: {from} → {to}");
        }

        [MenuItem("Assets/Create/UI Navigation/UI Navigation Graph", priority = 220)]
        private static void CreateFromProjectWindow()
        {
            var action = ScriptableObject.CreateInstance<CreateGraphAssetAction>();
            Texture2D icon = EditorGUIUtility.IconContent("ScriptableObject Icon").image as Texture2D;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                EntityId.None,
                action,
                DefaultAssetName,
                icon,
                null);
        }

        internal static UINavigationAsset CreateAtPath(string path, bool openWindow)
        {
            path = Path.ChangeExtension(path, UINavigationAuthoringGraph.Extension);
            path = AssetDatabase.GenerateUniqueAssetPath(path);

            UINavigationAuthoringGraph graph =
                GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(path);
            if (graph == null)
            {
                Debug.LogError($"UI Navigation Graph을 생성하지 못했습니다: {path}");
                return null;
            }

            var startNode = new UINavigationStartNode
            {
                Position = new Vector2(80f, 160f)
            };
            var homeScreen = new UINavigationUINode
            {
                Position = new Vector2(360f, 160f),
                InitialNodeId = "home",
                InitialDisplayName = "Home"
            };
            graph.AddNode(startNode);
            graph.AddNode(homeScreen);
            bool connected = graph.Connect(
                startNode.GetOutputPortByName(UINavigationStartNode.StartPort),
                homeScreen.GetInputPortByName(UINavigationUINodeBase.EnterPort));

            if (!graph.GetNodes().AsValueEnumerable().Any(node => node is UINavigationStartNode) ||
                !graph.GetNodes().AsValueEnumerable().Any(node => node is UINavigationUINode) ||
                !connected)
            {
                Debug.LogError($"기본 Start → Home Screen 구성을 생성하지 못했습니다: {path}");
                return null;
            }

            GraphDatabase.SaveGraph(graph);

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            UINavigationAsset asset = AssetDatabase.LoadAssetAtPath<UINavigationAsset>(path);
            if (asset == null)
            {
                Debug.LogError($"UI Navigation Graph을 생성했지만 임포트된 에셋을 찾지 못했습니다: {path}");
                return null;
            }

            ProjectWindowUtil.ShowCreatedAsset(asset);

            if (openWindow)
                AssetDatabase.OpenAsset(asset);

            return asset;
        }

        internal static string GetSelectedFolder()
        {
            string path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(path))
                return "Assets";

            if (AssetDatabase.IsValidFolder(path))
                return path;

            string directory = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
        }

        private sealed class CreateGraphAssetAction : AssetCreationEndAction
        {
            public override void Action(EntityId entityId, string pathName, string resourceFile)
            {
                CreateAtPath(pathName, true);
            }
        }
    }
}
