using System;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Builds the Show / Hide view command list of a UI node phase.
    /// </summary>
    internal static class UINavigationViewCommandList
    {
        internal static VisualElement Create(
            string title,
            string description,
            SerializedProperty array)
        {
            VisualElement section = UINavigationListDrawerUtility.CreateList(
                title,
                array,
                90f,
                (element, index, remove) =>
                {
                    SerializedProperty viewKey = element.FindPropertyRelative("view");
                    SerializedProperty category = viewKey.FindPropertyRelative("category");
                    SerializedProperty key = viewKey.FindPropertyRelative("key");
                    SerializedProperty mode = element.FindPropertyRelative("mode");

                    var card = new VisualElement();
                    card.AddToClassList("uinavigation-command-card");

                    var fields = new VisualElement();
                    fields.AddToClassList("uinavigation-command-card__fields");
                    var picker = UIKeyPickerField.Create(
                        string.Empty,
                        viewKey,
                        category,
                        key,
                        () => UIKeyCatalogKind.View);
                    picker.style.flexGrow = 1f;
                    fields.Add(picker);

                    var modeField = new EnumField(
                        (UIViewTransitionMode)mode.enumValueIndex);
                    modeField.style.marginTop = 4f;
                    modeField.style.flexGrow = 1f;
                    modeField.RegisterValueChangedCallback(evt =>
                    {
                        mode.enumValueIndex = (int)(UIViewTransitionMode)evt.newValue;
                        mode.serializedObject.ApplyModifiedProperties();
                    });
                    fields.Add(modeField);
                    card.Add(fields);

                    var divider = new VisualElement();
                    divider.AddToClassList("uinavigation-command-card__divider");
                    card.Add(divider);

                    var removeButton = UINavigationListDrawerUtility.CreateRemoveButton(
                        remove,
                        "Remove view command",
                        "-");
                    removeButton.AddToClassList("uinavigation-command-card__remove");
                    card.Add(removeButton);
                    return card;
                },
                element =>
                {
                    SerializedProperty viewKey = element.FindPropertyRelative("view");
                    viewKey.FindPropertyRelative("category").stringValue = string.Empty;
                    viewKey.FindPropertyRelative("key").stringValue = string.Empty;
                    element.FindPropertyRelative("mode").enumValueIndex =
                        (int)UIViewTransitionMode.Animated;
                },
                "No view commands.");

            section.AddToClassList("uinavigation-view-section");
            var descriptionLabel = new Label(description);
            descriptionLabel.AddToClassList("uinavigation-list-description");
            section.Insert(1, descriptionLabel);
            return section;
        }
    }
}
