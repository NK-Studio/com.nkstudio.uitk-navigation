using NKStudio.UITKNavigation.Editor.Catalog;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomPropertyDrawer(typeof(UINavigationNodeIdentity))]
    internal sealed class UINavigationNodeIdentityDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var hidden = new VisualElement();
            hidden.style.display = DisplayStyle.None;
            return hidden;
        }
    }

    [CustomPropertyDrawer(typeof(UINavigationUIPhase))]
    internal sealed class UINavigationUIPhaseDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            bool isExit = property.FindPropertyRelative("isExit")?.boolValue == true;

            var root = new VisualElement();
            root.AddToClassList("uinavigation-phase");
            UINavigationInspectorStyles.Attach(root);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("uinavigation-phase__header");

            var iconBox = new VisualElement();
            iconBox.AddToClassList("uinavigation-phase__icon-box");

            var icon = new VisualElement();
            icon.AddToClassList("uinavigation-phase__icon");
            icon.style.backgroundImage = new StyleBackground(
                AssetDatabase.LoadAssetAtPath<VectorImage>(
                    "Packages/com.nkstudio.uitk-navigation/Editor/Assets/NodePhaseIcon.svg"));
            icon.style.scale = new Scale(new Vector3(isExit ? -1f : 1f, 1f, 1f));
            iconBox.Add(icon);
            titleRow.Add(iconBox);

            var title = new Label(isExit ? "On Exit Node" : "On Enter Node");
            title.AddToClassList("uinavigation-phase__title");
            titleRow.Add(title);
            root.Add(titleRow);

            var body = new VisualElement();
            body.AddToClassList("uinavigation-phase__body");
            body.Add(UINavigationViewCommandList.Create(
                "Show Views",
                "Views that will be shown when the node is activated",
                property.FindPropertyRelative("showCommands")));
            body.Add(UINavigationViewCommandList.Create(
                "Hide Views",
                "Views that will be hidden when the node is activated",
                property.FindPropertyRelative("hideCommands")));
            root.Add(body);
            return root;
        }
    }
}
