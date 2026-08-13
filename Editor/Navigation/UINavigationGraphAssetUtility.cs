using System.IO;
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
