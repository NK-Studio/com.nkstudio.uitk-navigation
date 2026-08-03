using System;
using System.IO;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomEditor(typeof(UINavigatorBehaviour))]
    [CanEditMultipleObjects]
    internal sealed class UINavigatorBehaviourEditor : UnityEditor.Editor
    {
        private SerializedProperty _navigationAsset;
        private SerializedProperty _maxHistoryDepth;

        private void OnEnable()
        {
            _navigationAsset = serializedObject.FindProperty("navigationAsset");
            _maxHistoryDepth = serializedObject.FindProperty("maxHistoryDepth");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((UINavigatorBehaviour)target), typeof(MonoScript), false);

            EditorGUILayout.PropertyField(_navigationAsset, new GUIContent(
                "Navigation Graph",
                "Create > UI Navigation > UI Navigation Graph로 생성한 그래프 에셋입니다."));

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(targets.Length != 1))
            {
                if (GUILayout.Button("Create New"))
                    CreateAndAssignGraph();
            }

            UINavigationAsset asset = _navigationAsset.objectReferenceValue as UINavigationAsset;
            using (new EditorGUI.DisabledScope(
                       asset == null ||
                       _navigationAsset.hasMultipleDifferentValues ||
                       !UINavigationAssetEditor.HasAuthoringGraph(asset)))
            {
                if (GUILayout.Button("Open Graph"))
                    AssetDatabase.OpenAsset(asset);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(_maxHistoryDepth);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            DrawValidation(asset);
        }

        private void CreateAndAssignGraph()
        {
            string folder = UINavigationGraphAssetUtility.GetSelectedFolder();
            string path = EditorUtility.SaveFilePanelInProject(
                "Create UI Navigation Graph",
                "UI Navigation Graph",
                UINavigationAuthoringGraph.Extension,
                "새 Navigation Graph의 위치를 선택하세요.",
                folder);

            if (string.IsNullOrEmpty(path))
                return;

            UINavigationAsset asset = UINavigationGraphAssetUtility.CreateAtPath(path, false);
            if (asset == null)
                return;

            serializedObject.Update();
            _navigationAsset.objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            AssetDatabase.OpenAsset(asset);
        }

        private static void DrawValidation(UINavigationAsset asset)
        {
            var errors = UINavigationGraphValidator.Validate(asset);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Navigation Graph가 유효합니다.", MessageType.Info);
                return;
            }

            foreach (string error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Warning);
        }
    }

    [CustomEditor(typeof(UINavigationAsset))]
    internal sealed class UINavigationAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var asset = (UINavigationAsset)target;

            EditorGUILayout.LabelField("UI Navigation Graph", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Nodes", asset.Nodes.Count.ToString());
            EditorGUILayout.LabelField("Start Node", asset.GetStartNode()?.DisplayName ?? "None");
            EditorGUILayout.Space(6f);

            bool hasAuthoringGraph = HasAuthoringGraph(asset);
            if (hasAuthoringGraph)
            {
                if (GUILayout.Button("Open Graph Editor", GUILayout.Height(28f)))
                    AssetDatabase.OpenAsset(asset);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "이 파일은 편집 원본이 없는 런타임 .asset입니다. 편집 가능한 그래프는 .uinavgraph로 생성하세요.",
                    MessageType.Info);
            }

            EditorGUILayout.Space(6f);
            var errors = UINavigationGraphValidator.Validate(asset);
            if (errors.Count == 0)
            {
                EditorGUILayout.HelpBox("그래프가 유효합니다.", MessageType.Info);
                return;
            }

            foreach (string error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Warning);
        }

        internal static bool HasAuthoringGraph(UINavigationAsset asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return string.Equals(
                Path.GetExtension(path),
                "." + UINavigationAuthoringGraph.Extension,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
