using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Builds the catalog-bound Category / Name dropdown pair used by output rows.
    /// </summary>
    internal static class UINavigationOutputAddressField
    {
        internal static VisualElement Create(
            SerializedProperty owner,
            SerializedProperty category,
            SerializedProperty key,
            Func<UIKeyCatalogKind> kindGetter,
            out Action refresh)
        {
            UIKeyProjectService.EnsureCatalogIsSeparated();
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.FlexEnd;

            var categoryColumn = new VisualElement();
            categoryColumn.style.flexGrow = 1f;
            categoryColumn.style.marginRight = 7f;
            categoryColumn.Add(CreateMiniLabel("Category"));
            var categoryDropdown = new DropdownField();
            categoryColumn.Add(categoryDropdown);
            root.Add(categoryColumn);

            var nameColumn = new VisualElement();
            nameColumn.style.flexGrow = 1f;
            nameColumn.Add(CreateMiniLabel("Name"));
            var nameDropdown = new DropdownField();
            nameColumn.Add(nameDropdown);
            root.Add(nameColumn);

            bool refreshing = false;

            void Apply(string categoryValue, string keyValue)
            {
                category.stringValue = categoryValue?.Trim() ?? string.Empty;
                key.stringValue = keyValue?.Trim() ?? string.Empty;
                owner.serializedObject.ApplyModifiedProperties();
            }

            void Refresh()
            {
                if (refreshing)
                    return;

                refreshing = true;
                UIKeyCatalogKind kind = kindGetter();
                List<UIKeyCatalog.CategoryEntry> entries =
                    UIKeyCatalog.instance.GetCategories(kind)
                        .AsValueEnumerable()
                        .Where(entry => entry != null)
                        .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                var categories = new List<string> { "None" };
                categories.AddRange(entries.AsValueEnumerable().Select(entry => entry.Name).ToArray());
                categoryDropdown.choices = categories;

                string categoryValue = category.stringValue ?? string.Empty;
                int categoryIndex = categories.FindIndex(value =>
                    string.Equals(value, categoryValue, StringComparison.Ordinal));
                categoryDropdown.SetValueWithoutNotify(
                    categoryIndex >= 0 ? categories[categoryIndex] : "None");

                UIKeyCatalog.CategoryEntry selected = entries.AsValueEnumerable().FirstOrDefault(entry =>
                    string.Equals(entry.Name, categoryValue, StringComparison.Ordinal));
                var names = new List<string> { "None" };
                if (selected != null)
                    names.AddRange(selected.Keys);
                nameDropdown.choices = names;

                string keyValue = key.stringValue ?? string.Empty;
                int keyIndex = names.FindIndex(value =>
                    string.Equals(value, keyValue, StringComparison.Ordinal));
                nameDropdown.SetValueWithoutNotify(
                    keyIndex >= 0 ? names[keyIndex] : "None");
                refreshing = false;
            }

            categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                if (refreshing)
                    return;
                string value = evt.newValue == "None" ? string.Empty : evt.newValue;
                Apply(value, string.Empty);
                Refresh();
            });
            nameDropdown.RegisterValueChangedCallback(evt =>
            {
                if (refreshing)
                    return;
                string value = evt.newValue == "None" ? string.Empty : evt.newValue;
                Apply(category.stringValue, value);
                Refresh();
            });

            refresh = Refresh;
            UIKeyCatalog.Changed += Refresh;
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
                UIKeyCatalog.Changed -= Refresh);
            Refresh();
            return root;
        }
        private static Label CreateMiniLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 9f;
            label.style.marginLeft = 2f;
            label.style.marginBottom = 1f;
            label.style.color = EditorGUIUtility.isProSkin
                ? new Color(0.52f, 0.52f, 0.52f)
                : new Color(0.38f, 0.38f, 0.38f);
            return label;
        }
    }
}
