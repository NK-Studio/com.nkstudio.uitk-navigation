using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomPropertyDrawer(typeof(UINavigationPortalCondition))]
    internal sealed class UINavigationPortalConditionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty kind = property.FindPropertyRelative("kind");
            SerializedProperty key = property.FindPropertyRelative("key");
            SerializedProperty signalAddressKind =
                property.FindPropertyRelative("signalAddressKind");
            SerializedProperty customSignal =
                property.FindPropertyRelative("customSignal");
            SerializedProperty toggleValue = property.FindPropertyRelative("toggleValue");

            var root = new VisualElement();
            root.AddToClassList("uinavigation-phase");
            root.AddToClassList("uinavigation-portal-phase");
            UINavigationInspectorStyles.Attach(root);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("uinavigation-phase__header");

            var iconBox = new VisualElement();
            iconBox.AddToClassList("uinavigation-phase__icon-box");

            var icon = new VisualElement();
            icon.AddToClassList("uinavigation-phase__icon");
            icon.tooltip = "Portal Condition";
            icon.style.backgroundImage = new StyleBackground(
                AssetDatabase.LoadAssetAtPath<VectorImage>(
                    "Packages/com.nkstudio.uitk-navigation/Editor/Assets/NodeOutputIcon.svg"));
            icon.style.height = 16;
            iconBox.Add(icon);
            titleRow.Add(iconBox);

            var title = new Label("Portal Condition");
            title.AddToClassList("uinavigation-phase__title");
            titleRow.Add(title);
            root.Add(titleRow);

            var body = new VisualElement();
            body.AddToClassList("uinavigation-phase__body");

            var kindField = new EnumField(
                (UINavigationPortalConditionKind)kind.intValue);
            kindField.label = "Type";
            body.Add(kindField);

            var addressKindField = new EnumField(
                "Signal Address",
                (UINavigationSignalAddressKind)signalAddressKind.intValue);
            body.Add(addressKindField);

            var customSignalField = new TextField("Custom Signal")
            {
                value = customSignal.stringValue,
                isDelayed = true
            };
            body.Add(customSignalField);

            UIKeyPickerField keyField = UIKeyPickerField.Create(
                "Key",
                property,
                key.FindPropertyRelative("category"),
                key.FindPropertyRelative("key"),
                () => GetKind(
                    (UINavigationPortalConditionKind)kind.intValue),
                graphInspectorLayout: true);
            body.Add(keyField);

            var toggleField = new Toggle("Expected Value")
            {
                value = toggleValue.boolValue
            };
            body.Add(toggleField);

            UINavigationInspectorLayout.MatchInputBoundsToNodeOption(
                root,
                kindField);
            UINavigationInspectorLayout.MatchInputBoundsToNodeOption(
                root,
                addressKindField);
            UINavigationInspectorLayout.MatchInputBoundsToNodeOption(
                root,
                customSignalField);
            UINavigationInspectorLayout.MatchInputBoundsToNodeOption(
                root,
                toggleField);

            void Refresh()
            {
                bool signal =
                    (UINavigationPortalConditionKind)kind.intValue ==
                    UINavigationPortalConditionKind.Signal;
                bool custom = signal &&
                    (UINavigationSignalAddressKind)signalAddressKind.intValue ==
                    UINavigationSignalAddressKind.Custom;
                addressKindField.style.display =
                    signal ? DisplayStyle.Flex : DisplayStyle.None;
                customSignalField.style.display =
                    custom ? DisplayStyle.Flex : DisplayStyle.None;
                keyField.style.display =
                    custom ? DisplayStyle.None : DisplayStyle.Flex;
                toggleField.style.display =
                    (UINavigationPortalConditionKind)kind.intValue ==
                    UINavigationPortalConditionKind.Toggle
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                keyField.Refresh();
            }

            kindField.RegisterValueChangedCallback(evt =>
            {
                kind.intValue =
                    (int)(UINavigationPortalConditionKind)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            addressKindField.RegisterValueChangedCallback(evt =>
            {
                signalAddressKind.intValue =
                    (int)(UINavigationSignalAddressKind)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            customSignalField.RegisterValueChangedCallback(evt =>
            {
                customSignal.stringValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            toggleField.RegisterValueChangedCallback(evt =>
            {
                toggleValue.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            Refresh();

            root.Add(body);
            return root;
        }

        private static UIKeyCatalogKind GetKind(UINavigationPortalConditionKind kind)
        {
            return kind == UINavigationPortalConditionKind.Toggle
                ? UIKeyCatalogKind.Toggle
                : UIKeyCatalogKind.Signal;
        }
    }
}
