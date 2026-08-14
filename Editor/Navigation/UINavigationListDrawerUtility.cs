using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Builds the generic card list used by every graph node inspector section.
    /// </summary>
    internal static class UINavigationListDrawerUtility
    {
        internal static VisualElement CreateList(
            string title,
            SerializedProperty array,
            float rowHeight,
            Func<SerializedProperty, int, Action, VisualElement> createRow,
            Action<SerializedProperty> initialize,
            string emptyMessage = "None",
            Action<Button, Action<int>> configureAddButton = null)
        {
            var root = new VisualElement();
            root.AddToClassList("uinavigation-list-root");
            UINavigationInspectorStyles.Attach(root);

            var header = new VisualElement();
            header.AddToClassList("uinavigation-list-header");
            var titleLabel = new Label(title);
            titleLabel.AddToClassList("uinavigation-list-title");
            header.Add(titleLabel);
            var count = new Label();
            count.AddToClassList("uinavigation-list-count");
            header.Add(count);
            var add = new Button
            {
                text = title == "Outputs" ? "+ Add Output" : "+",
                tooltip = $"{title} - Add item"
            };
            add.AddToClassList("uinavigation-list-add");
            add.style.width = title == "Outputs" ? 92f : 25f;
            header.Add(add);
            root.Add(header);

            var empty = new Label(emptyMessage);
            empty.AddToClassList("uinavigation-list-empty");
            root.Add(empty);

            var list = new ListView
            {
                selectionType = SelectionType.None,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                showBorder = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None
            };
            list.AddToClassList("uinavigation-list");
            list.style.flexGrow = 0f;
            list.style.flexShrink = 0f;
            root.Add(list);

            void UpdateListHeightFromLayout()
            {
                int length = Math.Max(0, array.arraySize);
                if (length == 0)
                    return;

                List<VisualElement> renderedItems = list
                    .Query<VisualElement>(
                        className: "unity-collection-element__item")
                    .ToList();
                float measuredHeight = 0f;
                int measuredCount = 0;
                foreach (VisualElement item in renderedItems)
                {
                    if (item.resolvedStyle.display == DisplayStyle.None ||
                        item.layout.height <= 0f ||
                        float.IsNaN(item.layout.height))
                    {
                        continue;
                    }

                    measuredHeight += item.layout.height;
                    measuredCount++;
                }

                if (measuredCount != length)
                    return;

                float targetHeight = Mathf.Ceil(measuredHeight + 2f);
                if (Mathf.Abs(list.resolvedStyle.height - targetHeight) > 0.5f)
                    list.style.height = targetHeight;
            }

            list.RegisterCallback<GeometryChangedEvent>(_ =>
                list.schedule.Execute(UpdateListHeightFromLayout));

            void Refresh()
            {
                int length = Math.Max(0, array.arraySize);
                count.text = length.ToString();
                empty.style.display = length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                list.style.display = length == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                float initialRowHeight = rowHeight + 16f;
                list.style.height = Math.Max(
                    initialRowHeight,
                    length * initialRowHeight + 4f);
                list.itemsSource = ValueEnumerable.Range(0, length).ToList();
                list.Rebuild();
                list.schedule.Execute(UpdateListHeightFromLayout);
            }

            list.makeItem = () =>
            {
                var item = new VisualElement();
                item.AddToClassList("uinavigation-list-item-content");
                return item;
            };
            list.bindItem = (container, index) =>
            {
                container.Clear();
                if (index < 0 || index >= array.arraySize)
                    return;

                int capturedIndex = index;
                container.Add(createRow(
                    array.GetArrayElementAtIndex(index),
                    index,
                    () =>
                    {
                        if (capturedIndex < 0 || capturedIndex >= array.arraySize)
                            return;
                        array.DeleteArrayElementAtIndex(capturedIndex);
                        array.serializedObject.ApplyModifiedProperties();
                        Refresh();
                    }));
            };

            list.itemIndexChanged += (from, to) =>
            {
                if (from < 0 || from >= array.arraySize ||
                    to < 0 || to >= array.arraySize)
                    return;
                array.MoveArrayElement(from, to);
                array.serializedObject.ApplyModifiedProperties();
                Refresh();
            };

            void AddItem(int variant)
            {
                int index = array.arraySize;
                array.InsertArrayElementAtIndex(index);
                SerializedProperty element = array.GetArrayElementAtIndex(index);
                initialize(element);
                if (variant >= 0)
                {
                    SerializedProperty trigger = element.FindPropertyRelative("trigger");
                    if (trigger != null)
                        trigger.intValue = variant;
                }
                array.serializedObject.ApplyModifiedProperties();
                Refresh();
            }

            if (configureAddButton != null)
                configureAddButton(add, AddItem);
            else
                add.clicked += () => AddItem(-1);

            Refresh();
            return root;
        }
        internal static Button CreateRemoveButton(
            Action remove,
            string tooltip = "Remove item",
            string text = "x")
        {
            var button = new Button(remove)
            {
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList("uinavigation-remove-button");
            return button;
        }
    }
}
