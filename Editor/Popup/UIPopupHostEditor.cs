using NKStudio.UITKNavigation.Popup;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Popup
{
    [CustomEditor(typeof(UIPopupHost))]
    [CanEditMultipleObjects]
    internal sealed class UIPopupHostEditor : UnityEditor.Editor
    {
        private const string TreeAssetPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/UXML/UIPopupHostEditor.uxml";

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
