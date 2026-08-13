using System;
using System.IO;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomEditor(typeof(UINavigatorBehaviour))]
    [CanEditMultipleObjects]
    internal sealed class UINavigatorBehaviourEditor : UnityEditor.Editor
    {
        private const string TreeAssetPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/UXML/UINavigatorBehaviourEditor.uxml";

        private SerializedProperty _navigationAsset;
        private Button _createButton;
        private Button _openButton;
        private VisualElement _validation;

        private void OnEnable()
        {
            _navigationAsset = serializedObject.FindProperty("navigationAsset");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TreeAssetPath);
            if (tree == null)
                return root;

            tree.CloneTree(root);
            _createButton = root.Q<Button>("create-graph-button");
            _openButton = root.Q<Button>("open-graph-button");
            _validation = root.Q<VisualElement>("validation");
            _createButton.clicked += CreateAndAssignGraph;
            _openButton.clicked += OpenGraph;

            root.Bind(serializedObject);
            root.TrackPropertyValue(_navigationAsset, _ => RefreshGraphState());
            root.schedule.Execute(RefreshGraphState);
            return root;
        }

        private void CreateAndAssignGraph()
        {
            string folder = UINavigationGraphAssetUtility.GetSelectedFolder();
            string path = EditorUtility.SaveFilePanelInProject("Create UI Navigation Graph", "UI Navigation Graph", UINavigationAuthoringGraph.Extension, "새 Navigation Graph의 위치를 선택하세요.", folder);

            if (string.IsNullOrEmpty(path))
                return;

            UINavigationAsset asset = UINavigationGraphAssetUtility.CreateAtPath(path, false);
            if (asset == null)
                return;

            serializedObject.Update();
            _navigationAsset.objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            RefreshGraphState();
            AssetDatabase.OpenAsset(asset);
        }

        private void OpenGraph()
        {
            if (_navigationAsset.hasMultipleDifferentValues)
                return;

            if (_navigationAsset.objectReferenceValue is UINavigationAsset asset && UINavigationAssetEditor.HasAuthoringGraph(asset))
                AssetDatabase.OpenAsset(asset);
        }

        private void RefreshGraphState()
        {
            if (_validation == null)
                return;

            serializedObject.UpdateIfRequiredOrScript();
            bool mixed = _navigationAsset.hasMultipleDifferentValues;
            UINavigationAsset asset = mixed ? null : _navigationAsset.objectReferenceValue as UINavigationAsset;
            _createButton?.SetEnabled(targets.Length == 1);
            _openButton?.SetEnabled(!mixed && asset != null && UINavigationAssetEditor.HasAuthoringGraph(asset));

            _validation.Clear();
            if (mixed)
            {
                _validation.Add(new HelpBox("선택한 오브젝트에 서로 다른 Navigation Graph가 설정되어 있습니다.", HelpBoxMessageType.Info));
                return;
            }

            var errors = UINavigationGraphValidator.Validate(asset);
            if (errors.Count == 0)
            {
                _validation.Add(new HelpBox("Navigation Graph가 유효합니다.", HelpBoxMessageType.Info));
                return;
            }

            foreach (string error in errors)
                _validation.Add(new HelpBox(error, HelpBoxMessageType.Warning));
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
