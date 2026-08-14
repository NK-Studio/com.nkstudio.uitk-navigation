using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomPropertyDrawer(typeof(UINavigationSignalAddress))]
    internal sealed class UINavigationSignalAddressDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty kind = property.FindPropertyRelative("kind");
            SerializedProperty customSignal =
                property.FindPropertyRelative("customSignal");
            SerializedProperty databaseSignal =
                property.FindPropertyRelative("databaseSignal");

            var root = new VisualElement();
            root.AddToClassList("uinavigation-phase");
            root.AddToClassList("uinavigation-destination-phase");
            UINavigationInspectorStyles.Attach(root);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("uinavigation-phase__header");

            var iconBox = new VisualElement();
            iconBox.AddToClassList("uinavigation-phase__icon-box");

            var icon = new VisualElement();
            icon.AddToClassList("uinavigation-phase__icon");
            icon.tooltip = "Send Signal";
            icon.style.backgroundImage = new StyleBackground(
                AssetDatabase.LoadAssetAtPath<VectorImage>(
                    "Packages/com.nkstudio.uitk-navigation/Editor/Assets/NodeOutputIcon.svg"));
            icon.style.height = 16;
            iconBox.Add(icon);
            titleRow.Add(iconBox);

            var title = new Label("Send Signal");
            title.AddToClassList("uinavigation-phase__title");
            titleRow.Add(title);
            root.Add(titleRow);

            var body = new VisualElement();
            body.AddToClassList("uinavigation-phase__body");

            var kindField = new EnumField(
                "Address Type",
                (UINavigationSignalAddressKind)kind.intValue);
            var customField = new TextField("Custom Signal")
            {
                value = customSignal.stringValue,
                isDelayed = true
            };
            UIKeyPickerField databaseField = UIKeyPickerField.Create(
                "Database Signal",
                databaseSignal,
                databaseSignal.FindPropertyRelative("category"),
                databaseSignal.FindPropertyRelative("key"),
                () => UIKeyCatalogKind.Signal,
                graphInspectorLayout: true);
            body.Add(kindField);
            body.Add(customField);
            body.Add(databaseField);
            root.Add(body);

            UINavigationInspectorLayout.MatchInputBoundsToNodeOption(root, kindField);
            UINavigationInspectorLayout.MatchInputBoundsToNodeOption(root, customField);

            void Refresh()
            {
                bool custom =
                    (UINavigationSignalAddressKind)kind.intValue ==
                    UINavigationSignalAddressKind.Custom;
                customField.style.display =
                    custom ? DisplayStyle.Flex : DisplayStyle.None;
                databaseField.style.display =
                    custom ? DisplayStyle.None : DisplayStyle.Flex;
            }

            kindField.RegisterValueChangedCallback(evt =>
            {
                kind.intValue = (int)(UINavigationSignalAddressKind)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            customField.RegisterValueChangedCallback(evt =>
            {
                customSignal.stringValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            Refresh();
            return root;
        }
    }
}
