using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Builds the output port list of a UI node.
    /// </summary>
    internal static class UINavigationOutputList
    {
        private const float OutputRowHeight = 96f;

        internal static VisualElement Create(SerializedProperty array)
        {
            VisualElement section = UINavigationListDrawerUtility.CreateList(
                string.Empty,
                array,
                OutputRowHeight,
                (element, index, remove) =>
                {
                    SerializedProperty trigger = element.FindPropertyRelative("trigger");
                    SerializedProperty key = element.FindPropertyRelative("key");
                    SerializedProperty delay = element.FindPropertyRelative("delaySeconds");
                    SerializedProperty toggle = element.FindPropertyRelative("toggleCondition");
                    SerializedProperty viewCondition = element.FindPropertyRelative("viewCondition");

                    var triggerKinds = new[]
                    {
                        UINavigationTriggerKind.TimeDelay,
                        UINavigationTriggerKind.Signal,
                        UINavigationTriggerKind.Toggle,
                        UINavigationTriggerKind.UIView
                    };
                    var triggerNames = new List<string>
                    {
                        "TimeDelay",
                        "Signal",
                        "UIToggle",
                        "UI View"
                    };

                    var card = new VisualElement();
                    card.AddToClassList("uinavigation-command-card");
                    card.AddToClassList("uinavigation-output-card");

                    var fields = new VisualElement();
                    fields.AddToClassList("uinavigation-command-card__fields");
                    var header = new VisualElement();
                    header.AddToClassList("uinavigation-output-card__header");
                    fields.Add(header);

                    var kindIcon = new Label();
                    kindIcon.AddToClassList("uinavigation-output-card__kind");
                    header.Add(kindIcon);

                    int triggerIndex = Array.IndexOf(
                        triggerKinds,
                        UINavigationTriggerKindUtility.Normalize(
                            (UINavigationTriggerKind)trigger.intValue));
                    var triggerField = new DropdownField(
                        triggerNames,
                        Mathf.Max(0, triggerIndex));
                    triggerField.style.flexGrow = 1f;
                    triggerField.style.minWidth = 135f;
                    header.Add(triggerField);

                    var delayField = new FloatField
                    {
                        value = Mathf.Max(0f, delay.floatValue),
                        isDelayed = true
                    };
                    delayField.AddToClassList("uinavigation-output-card__condition");
                    delayField.style.flexGrow = 1f;
                    delayField.style.minWidth = 100f;
                    header.Add(delayField);

                    var toggleField = new EnumField(
                        (UIToggleOutputCondition)toggle.enumValueIndex);
                    toggleField.AddToClassList("uinavigation-output-card__condition");
                    toggleField.style.width = 72f;
                    header.Add(toggleField);

                    var viewField = new EnumField(
                        (UIViewOutputCondition)viewCondition.enumValueIndex);
                    viewField.AddToClassList("uinavigation-output-card__condition");
                    viewField.style.width = 78f;
                    header.Add(viewField);

                    VisualElement address = UINavigationOutputAddressField.Create(
                        viewCondition,
                        key.FindPropertyRelative("category"),
                        key.FindPropertyRelative("key"),
                        () => GetCatalogKind(
                            UINavigationTriggerKindUtility.Normalize(
                                (UINavigationTriggerKind)trigger.intValue)),
                        out Action refreshAddress);
                    address.AddToClassList("uinavigation-output-card__address");
                    fields.Add(address);
                    card.Add(fields);

                    var divider = new VisualElement();
                    divider.AddToClassList("uinavigation-command-card__divider");
                    card.Add(divider);

                    var removeButton = UINavigationListDrawerUtility.CreateRemoveButton(
                        remove,
                        "Remove output",
                        "-");
                    removeButton.AddToClassList("uinavigation-command-card__remove");
                    card.Add(removeButton);

                    void RefreshTrigger()
                    {
                        var kind = UINavigationTriggerKindUtility.Normalize(
                            (UINavigationTriggerKind)trigger.intValue);
                        kindIcon.text = kind switch
                        {
                            UINavigationTriggerKind.TimeDelay => "T",
                            UINavigationTriggerKind.Signal => "S",
                            UINavigationTriggerKind.Toggle => "T",
                            UINavigationTriggerKind.UIView => "V",
                            _ => string.Empty
                        };
                        kindIcon.tooltip = kind.ToString();
                        delayField.style.display =
                            kind == UINavigationTriggerKind.TimeDelay
                                ? DisplayStyle.Flex
                                : DisplayStyle.None;
                        address.style.display =
                            kind == UINavigationTriggerKind.TimeDelay
                                ? DisplayStyle.None
                                : DisplayStyle.Flex;
                        toggleField.style.display =
                            kind == UINavigationTriggerKind.Toggle
                                ? DisplayStyle.Flex
                                : DisplayStyle.None;
                        viewField.style.display =
                            kind == UINavigationTriggerKind.UIView
                                ? DisplayStyle.Flex
                                : DisplayStyle.None;
                        refreshAddress();
                    }

                    triggerField.RegisterValueChangedCallback(_ =>
                    {
                        int selected = Mathf.Clamp(
                            triggerField.index,
                            0,
                            triggerKinds.Length - 1);
                        trigger.intValue = (int)triggerKinds[selected];
                        trigger.serializedObject.ApplyModifiedProperties();
                        RefreshTrigger();
                    });
                    toggleField.RegisterValueChangedCallback(evt =>
                    {
                        toggle.enumValueIndex = (int)(UIToggleOutputCondition)evt.newValue;
                        toggle.serializedObject.ApplyModifiedProperties();
                    });
                    viewField.RegisterValueChangedCallback(evt =>
                    {
                        viewCondition.enumValueIndex = (int)(UIViewOutputCondition)evt.newValue;
                        viewCondition.serializedObject.ApplyModifiedProperties();
                    });
                    delayField.RegisterValueChangedCallback(evt =>
                    {
                        float clamped = Mathf.Max(0f, evt.newValue);
                        delay.floatValue = clamped;
                        delay.serializedObject.ApplyModifiedProperties();
                        if (!Mathf.Approximately(clamped, evt.newValue))
                            delayField.SetValueWithoutNotify(clamped);
                    });
                    RefreshTrigger();
                    return card;
                },
                element =>
                {
                    element.FindPropertyRelative("outputId").stringValue =
                        Guid.NewGuid().ToString("N");
                    element.FindPropertyRelative("trigger").intValue =
                        (int)UINavigationTriggerKind.Signal;
                    SerializedProperty key = element.FindPropertyRelative("key");
                    key.FindPropertyRelative("category").stringValue = "Default";
                    key.FindPropertyRelative("key").stringValue = "Next";
                    element.FindPropertyRelative("delaySeconds").floatValue = 1f;
                    element.FindPropertyRelative("toggleCondition").enumValueIndex =
                        (int)UIToggleOutputCondition.On;
                    element.FindPropertyRelative("viewCondition").enumValueIndex =
                        (int)UIViewOutputCondition.Show;
                    element.FindPropertyRelative("upgraded").boolValue = true;
                },
                "No outputs. Use + Add Output to create one.",
                ConfigureAddButton);
            section.AddToClassList("uinavigation-output-section");

            VisualElement header = section.Q<VisualElement>(
                className: "uinavigation-list-header");
            Button addButton = section.Q<Button>(
                className: "uinavigation-list-add");
            if (header != null)
                header.style.display = DisplayStyle.None;
            if (addButton != null)
            {
                addButton.RemoveFromHierarchy();
                addButton.text = "+ Add Output";
                addButton.tooltip = "Add output";
                addButton.AddToClassList("uinavigation-output-add");
                addButton.style.width = Length.Percent(100f);
                section.Add(addButton);
            }
            return section;
        }

        private static UIKeyCatalogKind GetCatalogKind(UINavigationTriggerKind trigger)
        {
            return trigger switch
            {
                UINavigationTriggerKind.Toggle => UIKeyCatalogKind.Toggle,
                UINavigationTriggerKind.UIView => UIKeyCatalogKind.View,
                _ => UIKeyCatalogKind.Signal
            };
        }

        private static void ConfigureAddButton(
            Button button,
            Action<int> addItem)
        {
            button.clicked += () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(
                    new GUIContent("TimeDelay"),
                    false,
                    () => addItem((int)UINavigationTriggerKind.TimeDelay));
                menu.AddItem(
                    new GUIContent("Signal"),
                    false,
                    () => addItem((int)UINavigationTriggerKind.Signal));
                menu.AddItem(
                    new GUIContent("UIToggle"),
                    false,
                    () => addItem((int)UINavigationTriggerKind.Toggle));
                menu.AddItem(
                    new GUIContent("UI View"),
                    false,
                    () => addItem((int)UINavigationTriggerKind.UIView));
                menu.DropDown(
                    UIKeyPopupPositionUtility.GetScreenRect(button));
            };
        }
    }
}
