using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomPropertyDrawer(typeof(UINavigationOutputCollection))]
    internal sealed class UINavigationOutputCollectionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList("uinavigation-phase");
            root.AddToClassList("uinavigation-output-phase");
            UINavigationInspectorStyles.Attach(root);

            var titleRow = new VisualElement();
            titleRow.AddToClassList("uinavigation-phase__header");

            var iconBox = new VisualElement();
            iconBox.AddToClassList("uinavigation-phase__icon-box");

            var icon = new VisualElement();
            icon.AddToClassList("uinavigation-phase__icon");
            icon.tooltip = "Outputs";
            icon.style.backgroundImage = new StyleBackground(
                AssetDatabase.LoadAssetAtPath<VectorImage>(
                    "Packages/com.nkstudio.uitk-navigation/Editor/Assets/NodeOutputIcon.svg"));
            icon.style.height = 16;
            iconBox.Add(icon);
            titleRow.Add(iconBox);

            var title = new Label("Outputs");
            title.AddToClassList("uinavigation-phase__title");
            titleRow.Add(title);
            root.Add(titleRow);

            var body = new VisualElement();
            body.AddToClassList("uinavigation-phase__body");
            body.Add(UINavigationOutputList.Create(
                property.FindPropertyRelative("items")));
            root.Add(body);
            return root;
        }
    }

    [CustomPropertyDrawer(typeof(UINavigationOutputDefinition))]
    internal sealed class UINavigationOutputDefinitionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty trigger = property.FindPropertyRelative("trigger");
            SerializedProperty key = property.FindPropertyRelative("key");
            SerializedProperty signalAddressKind =
                property.FindPropertyRelative("signalAddressKind");
            SerializedProperty customSignal =
                property.FindPropertyRelative("customSignal");
            SerializedProperty delay = property.FindPropertyRelative("delaySeconds");
            SerializedProperty toggle = property.FindPropertyRelative("toggleCondition");
            SerializedProperty view = property.FindPropertyRelative("viewCondition");

            var root = new VisualElement();
            var triggerField = new PropertyField(trigger, "Trigger");
            var signalAddressKindField =
                new PropertyField(signalAddressKind, "Signal Address");
            var keyField = new PropertyField(key, "Address");
            var customSignalField = new PropertyField(customSignal, "Custom Signal");
            var delayField = new PropertyField(delay, "Seconds");
            var toggleField = new PropertyField(toggle, "State");
            var viewField = new PropertyField(view, "State");
            root.Add(triggerField);
            root.Add(toggleField);
            root.Add(viewField);
            root.Add(signalAddressKindField);
            root.Add(customSignalField);
            root.Add(keyField);
            root.Add(delayField);

            void Refresh()
            {
                var kind = UINavigationTriggerKindUtility.Normalize(
                    (UINavigationTriggerKind)trigger.intValue);
                bool signal = kind == UINavigationTriggerKind.Signal;
                bool custom = signal &&
                    (UINavigationSignalAddressKind)signalAddressKind.intValue ==
                    UINavigationSignalAddressKind.Custom;
                signalAddressKindField.style.display =
                    signal ? DisplayStyle.Flex : DisplayStyle.None;
                customSignalField.style.display =
                    custom ? DisplayStyle.Flex : DisplayStyle.None;
                keyField.style.display =
                    kind == UINavigationTriggerKind.TimeDelay || custom
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
                delayField.style.display =
                    kind == UINavigationTriggerKind.TimeDelay
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                toggleField.style.display =
                    kind == UINavigationTriggerKind.Toggle
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
                viewField.style.display =
                    kind == UINavigationTriggerKind.UIView
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            }

            triggerField.TrackPropertyValue(trigger, _ => Refresh());
            signalAddressKindField.TrackPropertyValue(signalAddressKind, _ => Refresh());
            Refresh();
            return root;
        }
    }
}
