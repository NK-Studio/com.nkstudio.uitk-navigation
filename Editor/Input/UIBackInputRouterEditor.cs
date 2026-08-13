using NKStudio.UITKNavigation.Input;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Input
{
    [CustomEditor(typeof(UIBackInputRouter))]
    [CanEditMultipleObjects]
    internal sealed class UIBackInputRouterEditor : UnityEditor.Editor
    {
        private const string TreeAssetPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/UXML/UIBackInputRouterEditor.uxml";

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            VisualTreeAsset tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TreeAssetPath);
            if (tree == null)
                return root;

            tree.CloneTree(root);
            root.Bind(serializedObject);
            return root;
        }
    }
}
