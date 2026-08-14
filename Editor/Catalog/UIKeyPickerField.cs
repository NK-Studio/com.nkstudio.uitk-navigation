using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Elements;
using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    internal static class UIKeyPopupPositionUtility
    {
        internal static Rect GetScreenRect(VisualElement anchor)
        {
            return anchor.worldBound;
        }
    }

    internal sealed class UIKeyPickerField : SimpleBaseField
    {
        private readonly Func<UIKey> _getter;
        private readonly Action<UIKey> _setter;
        private readonly Func<UIKeyCatalogKind> _kindGetter;
        private const string EditNameIconPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/Assets/EditNameIcon.svg";
        private const string GraphPropertyFieldUssClassName =
            "ge-model-property-field";
        private const string GraphPropertyFieldLabelUssClassName =
            "ge-model-property-field__label";
        private const string GraphPropertyFieldInputUssClassName =
            "ge-model-property-field__input";

        private readonly SerializedProperty _owner;
        private readonly Button _pickerButton;
        private readonly Button _customButton;
        private readonly Button _renameButton;
        private readonly Button _clearButton;
        private readonly Label _warning;
        private readonly Button _addButton;
        private readonly VisualElement _customRow;
        private readonly TextField _categoryField;
        private readonly TextField _keyField;
        private bool _refreshing;

        internal UIKeyPickerField(
            string label,
            Func<UIKey> getter,
            Action<UIKey> setter,
            Func<UIKeyCatalogKind> kindGetter = null,
            SerializedProperty owner = null,
            bool graphInspectorLayout = false)
        {
            UIKeyProjectService.EnsureCatalogIsSeparated();

            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _kindGetter = kindGetter ?? (() => UIKeyCatalogKind.Signal);
            _owner = owner;

            // The picker is the field itself: the base class draws the aligned label and hands
            // over the input container that the rows below fill. An empty label draws no label
            // at all, and being one element means one row margin from the theme, not two.
            Label = label ?? string.Empty;

            VisualElement input = contentContainer;

            // The base class zeroes the label margin; the theme value is restored so the input
            // starts exactly where a built-in field's input does.
            LabelElement.style.marginRight = StyleKeyword.Null;

            if (graphInspectorLayout)
            {
                AddToClassList(GraphPropertyFieldUssClassName);
                AddToClassList(PropertyField.ussClassName);

                LabelElement.AddToClassList(GraphPropertyFieldLabelUssClassName);
                LabelElement.AddToClassList(PropertyField.labelUssClassName);
                input.AddToClassList(GraphPropertyFieldInputUssClassName);
                input.AddToClassList(PropertyField.inputUssClassName);
            }

            // The rows below carry their own buttons and borders, so the field container
            // stays borderless instead of framing them like a text field.
            input.style.flexDirection = FlexDirection.Column;
            input.style.alignItems = Align.Stretch;
            // A flex basis of auto would size the container after its content — a long address
            // plus the buttons — so the row would grow past the space it was given instead of
            // shrinking the picker button.
            input.style.flexBasis = 0f;
            input.style.minWidth = 0f;
            input.style.paddingTop = 0f;
            input.style.paddingBottom = 0f;
            input.style.borderTopWidth = 0f;
            input.style.borderBottomWidth = 0f;

            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.minWidth = 0f;
            input.Add(mainRow);

            _pickerButton = new Button(ShowPicker);
            _pickerButton.style.flexGrow = 1f;
            _pickerButton.style.flexShrink = 1f;
            _pickerButton.style.minWidth = 0f;
            // The button is the only part of the row that shrinks, so a long address is cut with
            // an ellipsis instead of being drawn over the buttons next to it.
            _pickerButton.style.overflow = Overflow.Hidden;
            _pickerButton.style.whiteSpace = WhiteSpace.NoWrap;
            _pickerButton.style.textOverflow = TextOverflow.Ellipsis;
            // The theme gives buttons a 3px side margin; the first one starts at the input edge.
            _pickerButton.style.marginLeft = 0f;
            _pickerButton.style.unityTextAlign = TextAnchor.MiddleLeft;
            _pickerButton.tooltip = "등록된 UI Navigation Category/Key를 검색합니다.";
            mainRow.Add(_pickerButton);

            _customButton = CreateCompactButton("✎", "직접 문자열을 입력합니다.", ToggleCustom);
            mainRow.Add(_customButton);

            _renameButton = CreateIconButton(
                EditNameIconPath,
                "요소의 name을 주소 기반 이름으로 바꿉니다.",
                ApplyElementName);
            mainRow.Add(_renameButton);

            _clearButton = CreateCompactButton("×", "주소를 비웁니다.", () => SetValue(default));
            // The theme gives buttons a 3px side margin; this one ends at the input edge.
            _clearButton.style.marginRight = 0f;
            mainRow.Add(_clearButton);

            _warning = new Label("⚠");
            _warning.style.marginLeft = 4f;
            _warning.style.flexShrink = 0f;
            _warning.style.color = new Color(1f, 0.65f, 0.15f);
            mainRow.Add(_warning);

            _addButton = new Button(AddCurrentToCatalog) { text = "Add" };
            _addButton.tooltip = "현재 문자열 주소를 Key Catalog에 등록합니다.";
            _addButton.style.marginLeft = 2f;
            _addButton.style.minWidth = 44f;
            _addButton.style.flexShrink = 0f;
            mainRow.Add(_addButton);

            // Theme buttons and labels bring their own vertical margin. Left alone it stacks on
            // top of the row margin and makes the picker taller than the plain fields next to it.
            foreach (VisualElement child in mainRow.Children())
            {
                child.style.marginTop = 0f;
                child.style.marginBottom = 0f;
            }

            _customRow = new VisualElement();
            _customRow.style.flexDirection = FlexDirection.Row;
            _customRow.style.marginTop = 2f;
            input.Add(_customRow);

            _categoryField = new TextField { tooltip = "Category" };
            _categoryField.style.flexGrow = 1f;
            _categoryField.RegisterValueChangedCallback(_ => ApplyCustomValue());
            _customRow.Add(_categoryField);

            var slash = new Label("/");
            slash.style.marginLeft = 4f;
            slash.style.marginRight = 4f;
            slash.style.unityTextAlign = TextAnchor.MiddleCenter;
            _customRow.Add(slash);

            _keyField = new TextField { tooltip = "Key" };
            _keyField.style.flexGrow = 1f;
            _keyField.RegisterValueChangedCallback(_ => ApplyCustomValue());
            _customRow.Add(_keyField);

            _customRow.style.display = DisplayStyle.None;
            UIKeyCatalog.Changed += Refresh;
            RegisterCallback<DetachFromPanelEvent>(_ => UIKeyCatalog.Changed -= Refresh);
            Refresh();
        }

        /// <summary>
        /// Creates a picker bound to the serialized category and key strings of <paramref name="owner"/>.
        /// </summary>
        internal static UIKeyPickerField Create(
            string label,
            SerializedProperty owner,
            SerializedProperty category,
            SerializedProperty key,
            Func<UIKeyCatalogKind> kindGetter = null,
            bool graphInspectorLayout = false)
        {
            return new UIKeyPickerField(
                label,
                () => new UIKey(category.stringValue, key.stringValue),
                value =>
                {
                    category.stringValue = value.Category;
                    key.stringValue = value.Key;
                    owner.serializedObject.ApplyModifiedProperties();
                },
                kindGetter,
                owner,
                graphInspectorLayout);
        }

        internal void Refresh()
        {
            if (_refreshing)
                return;

            _refreshing = true;
            UIKey value = _getter();
            _pickerButton.text = value.IsValid ? $"{value.Category} / {value.Key}" : "(None)";
            _categoryField.SetValueWithoutNotify(value.Category);
            _keyField.SetValueWithoutNotify(value.Key);

            bool missing = value.IsValid &&
                           !UIKeyCatalog.instance.Contains(value, _kindGetter());
            _warning.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;
            _warning.tooltip = missing
                ? $"'{value}'은(는) Key Catalog에 등록되지 않았습니다."
                : string.Empty;
            _addButton.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;
            _clearButton.SetEnabled(value.IsValid);

            bool canRename = _owner != null && UxmlAuthoringUtility.CanWriteAttribute(_owner, "name");
            _renameButton.style.display = canRename ? DisplayStyle.Flex : DisplayStyle.None;
            _renameButton.SetEnabled(value.IsValid);

            _refreshing = false;
        }

        /// <summary>
        /// Applies e le me nt na me.
        /// </summary>
        private void ApplyElementName()
        {
            UIKey value = _getter();
            if (!value.IsValid || _owner == null)
                return;

            string category = UxmlAuthoringUtility.ToKebabCase(value.Category);
            string key = UxmlAuthoringUtility.ToKebabCase(value.Key);
            string name = string.IsNullOrEmpty(category) ? key : $"{category}-{key}";
            if (string.IsNullOrEmpty(name))
                return;

            Undo.RecordObjects(_owner.serializedObject.targetObjects, "Apply Element Name");
            if (!UxmlAuthoringUtility.SetStringAttribute(_owner, "name", name))
                return;

            foreach (UnityEngine.Object target in _owner.serializedObject.targetObjects)
                EditorUtility.SetDirty(target);
        }

        private static Button CreateIconButton(
            string iconPath,
            string tooltip,
            Action clicked)
        {
            var button = new Button(clicked) { tooltip = tooltip };
            button.style.width = 24f;
            button.style.flexShrink = 0f;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 2f;
            button.style.paddingRight = 2f;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;

            var icon = new VisualElement();
            icon.style.width = 13f;
            icon.style.height = 13f;
            icon.style.flexShrink = 0f;
            icon.style.unityBackgroundImageTintColor = EditorGUIUtility.isProSkin
                ? new Color(0.83f, 0.83f, 0.83f)
                : new Color(0.25f, 0.25f, 0.25f);

            var vector = AssetDatabase.LoadAssetAtPath<VectorImage>(iconPath);
            if (vector != null)
                icon.style.backgroundImage = new StyleBackground(vector);

            button.Add(icon);
            return button;
        }

        private static Button CreateCompactButton(
            string text,
            string tooltip,
            Action clicked)
        {
            var button = new Button(clicked)
            {
                text = text,
                tooltip = tooltip
            };
            button.style.width = 24f;
            button.style.flexShrink = 0f;
            button.style.marginLeft = 2f;
            button.style.paddingLeft = 2f;
            button.style.paddingRight = 2f;
            return button;
        }

        private void ShowPicker()
        {
            var dropdown = new UIKeyAdvancedDropdown(
                new AdvancedDropdownState(),
                UIKeyCatalog.instance.GetKeys(_kindGetter()),
                SetValue);
            dropdown.Show(UIKeyPopupPositionUtility.GetScreenRect(_pickerButton));
        }

        private void ToggleCustom()
        {
            bool show = _customRow.style.display == DisplayStyle.None;
            _customRow.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
                _categoryField.Focus();
        }

        private void ApplyCustomValue()
        {
            if (_refreshing)
                return;

            SetValue(new UIKey(_categoryField.value, _keyField.value));
        }

        private void AddCurrentToCatalog()
        {
            UIKey value = _getter();
            if (UIKeyCatalog.instance.Add(value, _kindGetter()))
                Refresh();
        }

        private void SetValue(UIKey value)
        {
            _setter(value);
            Refresh();
        }

        private sealed class UIKeyAdvancedDropdown : AdvancedDropdown
        {
            private readonly List<UIKey> _keys;
            private readonly Action<UIKey> _selected;

            internal UIKeyAdvancedDropdown(
                AdvancedDropdownState state,
                IEnumerable<UIKey> keys,
                Action<UIKey> selected)
                : base(state)
            {
                _keys = keys?
                    .AsValueEnumerable()
                    .Where(key => key.IsValid)
                    .Distinct()
                    .OrderBy(key => key.Category, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(key => key.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<UIKey>();
                _selected = selected;
                minimumSize = new Vector2(280f, 320f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("UI Navigation Keys");
                root.AddChild(new KeyItem("(None)", default));

                foreach (System.Linq.IGrouping<string, UIKey> category in _keys.AsValueEnumerable().GroupBy(
                             key => key.Category,
                             StringComparer.Ordinal))
                {
                    var categoryItem = new AdvancedDropdownItem(category.Key);
                    foreach (UIKey key in category)
                        categoryItem.AddChild(new KeyItem(key.Key, key));
                    root.AddChild(categoryItem);
                }

                if (_keys.Count == 0)
                    root.AddChild(new AdvancedDropdownItem("No registered keys"));

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is KeyItem keyItem)
                    _selected?.Invoke(keyItem.Value);
            }

            private sealed class KeyItem : AdvancedDropdownItem
            {
                internal KeyItem(string name, UIKey value)
                    : base(name)
                {
                    Value = value;
                }

                internal UIKey Value { get; }
            }
        }
    }
}
