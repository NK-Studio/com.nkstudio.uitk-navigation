using System;
using System.Collections;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    internal sealed class UIKeyCatalogWindow : EditorWindow
    {
        private const string StyleSheetPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/Catalog/UIKeyCatalogWindow.uss";

        private readonly List<UIKeyCatalog.CategoryEntry> _visibleCategories = new();
        private readonly List<UIKeyUsage> _visibleUsages = new();
        private readonly Dictionary<UIKeyCatalogKind, ToolbarToggle> _kindToggles = new();
        private IReadOnlyList<UIKeyUsage> _usages = Array.Empty<UIKeyUsage>();

        private ListView _categoryList;
        private DropdownField _categoryDropdown;
        private Label _categoryFieldLabel;
        private VisualElement _categorySelectRow;
        private VisualElement _categoryCreateRow;
        private TextField _newCategoryField;
        private TextField _newKeyField;
        private Label _categoryTitle;
        private Label _categoryMeta;
        private Label _itemCount;
        private Label _catalogSummary;
        private Label _categoriesLabel;
        private Label _kindSubtitle;
        private VisualElement _keyRows;
        private ListView _usageList;
        private Foldout _usageFoldout;
        private UIKeyCatalog.CategoryEntry _selectedCategory;
        private string _selectedKey;
        private string _search = string.Empty;
        private bool _refreshing;
        private bool _newCategoryMode;
        private UIKeyCatalogKind _catalogKind = UIKeyCatalogKind.View;

        [MenuItem("Tools/UI Navigation/Key Catalog", priority = 100)]
        internal static void Open()
        {
            var window = GetWindow<UIKeyCatalogWindow>();
            window.titleContent = new GUIContent("UI Navigation Database");
            window.minSize = new Vector2(760f, 500f);
            window.Show();
        }

        public void CreateGUI()
        {
            UIKeyProjectService.EnsureCatalogIsSeparated();

            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("key-catalog");

            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            var split = new TwoPaneSplitView(
                0,
                238f,
                TwoPaneSplitViewOrientation.Horizontal);
            split.AddToClassList("key-catalog__split");
            rootVisualElement.Add(split);

            split.Add(BuildSidebar());
            split.Add(BuildContent());

            UIKeyCatalog.Changed += OnCatalogChanged;
            rootVisualElement.RegisterCallback<DetachFromPanelEvent>(_ =>
                UIKeyCatalog.Changed -= OnCatalogChanged);

            RefreshUsages();
        }

        private void AddKindToggle(
            VisualElement toolbar,
            UIKeyCatalogKind kind,
            string text)
        {
            var toggle = new ToolbarToggle { text = text };
            toggle.AddToClassList("key-catalog__kind-tab");
            toggle.SetValueWithoutNotify(kind == _catalogKind);
            toggle.EnableInClassList(
                "key-catalog__kind-tab--selected",
                kind == _catalogKind);
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    SetCatalogKind(kind);
                else if (_catalogKind == kind)
                    toggle.SetValueWithoutNotify(true);
            });
            _kindToggles[kind] = toggle;
            toolbar.Add(toggle);
        }

        private void SetCatalogKind(UIKeyCatalogKind kind)
        {
            if (_catalogKind == kind && _categoryList != null)
                return;

            _catalogKind = kind;
            foreach ((UIKeyCatalogKind key, ToolbarToggle toggle) in _kindToggles)
            {
                toggle.SetValueWithoutNotify(key == kind);
                toggle.EnableInClassList(
                    "key-catalog__kind-tab--selected",
                    key == kind);
            }

            _selectedCategory = null;
            _selectedKey = null;
            _newCategoryMode = false;
            if (_categoryFieldLabel != null)
                _categoryFieldLabel.text = $"{kind.ToString().ToUpperInvariant()} CATEGORY";
            if (_categoriesLabel != null)
                _categoriesLabel.text =
                    $"{kind.ToString().ToUpperInvariant()} CATEGORIES";
            if (_kindSubtitle != null)
                _kindSubtitle.text = $"{kind} Category / Key Catalog";
            RefreshCategories();
        }

        private VisualElement BuildSidebar()
        {
            var sidebar = new VisualElement();
            sidebar.AddToClassList("key-catalog__sidebar");

            var sidebarTitle = new Label("KEY CATALOG");
            sidebarTitle.AddToClassList("key-catalog__eyebrow");
            sidebar.Add(sidebarTitle);

            var kindToolbar = new Toolbar();
            kindToolbar.AddToClassList("key-catalog__kind-tabs");
            AddKindToggle(kindToolbar, UIKeyCatalogKind.View, "View");
            AddKindToggle(kindToolbar, UIKeyCatalogKind.Button, "Button");
            AddKindToggle(kindToolbar, UIKeyCatalogKind.Signal, "Signal");
            sidebar.Add(kindToolbar);

            var search = new ToolbarSearchField();
            search.AddToClassList("key-catalog__search");
            search.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue?.Trim() ?? string.Empty;
                RefreshCategories();
            });
            sidebar.Add(search);

            var tools = new VisualElement();
            tools.AddToClassList("key-catalog__tools");
            tools.Add(CreateUtilityButton(
                "d_Search Icon",
                "Scan Project",
                "UXML과 Navigation Graph에서 사용 중인 주소를 찾아 등록합니다.",
                ScanProject));
            tools.Add(CreateUtilityButton(
                "Refresh",
                "Refresh Usage",
                "등록된 주소의 프로젝트 사용처를 다시 계산합니다.",
                RefreshUsages));
            sidebar.Add(tools);

            _categoriesLabel = new Label(
                $"{_catalogKind.ToString().ToUpperInvariant()} CATEGORIES");
            _categoriesLabel.AddToClassList("key-catalog__section-label");
            sidebar.Add(_categoriesLabel);

            _categoryList = new ListView
            {
                itemsSource = _visibleCategories,
                selectionType = SelectionType.Single,
                fixedItemHeight = 30f,
                makeItem = MakeCategoryRow,
                bindItem = BindCategoryRow
            };
            _categoryList.AddToClassList("key-catalog__categories");
            _categoryList.selectionChanged += OnCategorySelected;
            sidebar.Add(_categoryList);

            _catalogSummary = new Label();
            _catalogSummary.AddToClassList("key-catalog__sidebar-footer");
            sidebar.Add(_catalogSummary);
            return sidebar;
        }

        private VisualElement BuildContent()
        {
            var content = new VisualElement();
            content.AddToClassList("key-catalog__content");

            var hero = new VisualElement();
            hero.AddToClassList("key-catalog__hero");
            var title = new Label("UI Navigation Database");
            title.AddToClassList("key-catalog__title");
            hero.Add(title);
            _kindSubtitle = new Label($"{_catalogKind} Category / Key Catalog");
            _kindSubtitle.AddToClassList("key-catalog__subtitle");
            hero.Add(_kindSubtitle);
            content.Add(hero);

            content.Add(BuildCreateBar());

            var categoryHeader = new VisualElement();
            categoryHeader.AddToClassList("key-catalog__category-header");

            var headingBlock = new VisualElement();
            headingBlock.AddToClassList("key-catalog__category-heading");
            _categoryTitle = new Label("No Category");
            _categoryTitle.AddToClassList("key-catalog__category-title");
            headingBlock.Add(_categoryTitle);
            _categoryMeta = new Label();
            _categoryMeta.AddToClassList("key-catalog__category-meta");
            headingBlock.Add(_categoryMeta);
            categoryHeader.Add(headingBlock);

            var categoryActions = new VisualElement();
            categoryActions.AddToClassList("key-catalog__row-actions");
            categoryActions.Add(CreateIconButton(
                "✎",
                "선택한 Category 이름을 변경합니다.",
                BeginCategoryRename,
                "key-catalog__icon-button"));
            categoryActions.Add(CreateIconButton(
                "−",
                "선택한 Category를 삭제합니다.",
                DeleteCategory,
                "key-catalog__remove-button"));
            categoryHeader.Add(categoryActions);
            content.Add(categoryHeader);

            var keyListCard = new VisualElement();
            keyListCard.AddToClassList("key-catalog__list-card");
            var keyScroll = new ScrollView(ScrollViewMode.Vertical);
            keyScroll.AddToClassList("key-catalog__key-scroll");
            _keyRows = new VisualElement();
            _keyRows.AddToClassList("key-catalog__key-rows");
            keyScroll.Add(_keyRows);
            keyListCard.Add(keyScroll);

            _itemCount = new Label("0 items");
            _itemCount.AddToClassList("key-catalog__item-count");
            keyListCard.Add(_itemCount);
            content.Add(keyListCard);

            _usageFoldout = new Foldout { text = "Selected Key Usages", value = false };
            _usageFoldout.AddToClassList("key-catalog__usages");
            _usageList = new ListView
            {
                itemsSource = _visibleUsages,
                selectionType = SelectionType.None,
                fixedItemHeight = 22f,
                makeItem = () => new Label(),
                bindItem = BindUsageRow
            };
            _usageList.AddToClassList("key-catalog__usage-list");
            _usageFoldout.Add(_usageList);
            content.Add(_usageFoldout);
            return content;
        }

        private VisualElement BuildCreateBar()
        {
            var createBar = new VisualElement();
            createBar.AddToClassList("key-catalog__create-bar");

            var categoryBlock = new VisualElement();
            categoryBlock.AddToClassList("key-catalog__create-category");

            _categoryFieldLabel = new Label(
                $"{_catalogKind.ToString().ToUpperInvariant()} CATEGORY");
            _categoryFieldLabel.AddToClassList("key-catalog__field-label");
            categoryBlock.Add(_categoryFieldLabel);

            _categorySelectRow = new VisualElement();
            _categorySelectRow.AddToClassList("key-catalog__input-row");
            _categoryDropdown = new DropdownField();
            _categoryDropdown.AddToClassList("key-catalog__category-dropdown");
            _categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_refreshing || _newCategoryMode)
                    return;

                SelectCategory(evt.newValue);
            });
            _categorySelectRow.Add(_categoryDropdown);

            var newCategoryButton = new Button(() => SetNewCategoryMode(true))
            {
                text = "✎  New Category",
                tooltip = "새 Category 입력 모드로 전환합니다."
            };
            newCategoryButton.AddToClassList("key-catalog__new-category-button");
            _categorySelectRow.Add(newCategoryButton);
            categoryBlock.Add(_categorySelectRow);

            _categoryCreateRow = new VisualElement();
            _categoryCreateRow.AddToClassList("key-catalog__input-row");
            _newCategoryField = new TextField();
            _newCategoryField.tooltip = "새 Category 이름";
            _newCategoryField.AddToClassList("key-catalog__new-category-field");
            _newCategoryField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return &&
                    evt.keyCode != KeyCode.KeypadEnter)
                {
                    return;
                }

                CreateCategory();
                evt.StopPropagation();
            });
            _categoryCreateRow.Add(_newCategoryField);

            var createCategoryButton = new Button(CreateCategory)
            {
                text = "✎  New Category",
                tooltip = "입력한 이름으로 새 Category를 생성합니다."
            };
            createCategoryButton.AddToClassList("key-catalog__create-category-button");
            _categoryCreateRow.Add(createCategoryButton);

            var cancelCategoryButton = CreateIconButton(
                "×",
                "Category 생성을 취소합니다.",
                () => SetNewCategoryMode(false),
                "key-catalog__cancel-category-button");
            _categoryCreateRow.Add(cancelCategoryButton);
            categoryBlock.Add(_categoryCreateRow);
            createBar.Add(categoryBlock);

            var keyBlock = new VisualElement();
            keyBlock.AddToClassList("key-catalog__create-key");
            var keyLabel = new Label("NEW KEY NAME");
            keyLabel.AddToClassList("key-catalog__field-label");
            keyBlock.Add(keyLabel);

            var keyInputRow = new VisualElement();
            keyInputRow.AddToClassList("key-catalog__input-row");
            _newKeyField = new TextField();
            _newKeyField.AddToClassList("key-catalog__new-key-field");
            _newKeyField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return &&
                    evt.keyCode != KeyCode.KeypadEnter)
                {
                    return;
                }

                AddKey();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
            keyInputRow.Add(_newKeyField);
            keyInputRow.Add(CreateIconButton(
                "+",
                "Category/Key를 카탈로그에 추가합니다.",
                AddKey,
                "key-catalog__add-button"));
            keyBlock.Add(keyInputRow);
            createBar.Add(keyBlock);
            return createBar;
        }

        private static Button CreateUtilityButton(
            string iconName,
            string text,
            string tooltip,
            Action clicked)
        {
            var button = new Button(clicked)
            {
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList("key-catalog__utility-button");

            Texture icon = EditorGUIUtility.IconContent(iconName).image;
            if (icon != null)
            {
                button.style.backgroundImage = new StyleBackground(icon as Texture2D);
                button.style.unityBackgroundImageTintColor =
                    new Color(0.72f, 0.76f, 0.82f);
            }

            return button;
        }

        private static Button CreateIconButton(
            string text,
            string tooltip,
            Action clicked,
            string className)
        {
            var button = new Button(clicked)
            {
                text = text,
                tooltip = tooltip
            };
            button.AddToClassList(className);
            return button;
        }

        private VisualElement MakeCategoryRow()
        {
            var row = new VisualElement();
            row.AddToClassList("key-catalog__category-row");
            row.AddManipulator(new ContextualMenuManipulator(evt =>
            {
                if (row.userData is not UIKeyCatalog.CategoryEntry category)
                    return;

                evt.menu.AppendAction(
                    "Delete",
                    _ => DeleteCategory(category),
                    DropdownMenuAction.AlwaysEnabled);
            }));

            var name = new Label { name = "category-name" };
            name.AddToClassList("key-catalog__category-row-name");
            row.Add(name);
            var count = new Label { name = "category-count" };
            count.AddToClassList("key-catalog__category-row-count");
            row.Add(count);
            return row;
        }

        private void BindCategoryRow(VisualElement element, int index)
        {
            UIKeyCatalog.CategoryEntry category = _visibleCategories[index];
            element.userData = category;
            element.Q<Label>("category-name").text = category.Name;
            element.Q<Label>("category-count").text = category.Keys.Count.ToString();
            element.tooltip =
                $"{category.Keys.Count} keys · " +
                $"{UIKeyProjectService.CountCategoryUsages(category.Name, _usages, _catalogKind)} uses";
        }

        private void BindUsageRow(VisualElement element, int index)
        {
            UIKeyUsage usage = _visibleUsages[index];
            ((Label)element).text =
                $"{usage.Kind}  ·  {usage.AssetPath}" +
                (string.IsNullOrEmpty(usage.Context) ? string.Empty : $"  ·  {usage.Context}");
            element.tooltip = usage.Value.ToString();
        }

        private void OnCategorySelected(IEnumerable selection)
        {
            if (_refreshing)
                return;

            _selectedCategory = selection
                .AsValueEnumerable()
                .Select(item => (UIKeyCatalog.CategoryEntry)item)
                .FirstOrDefault();
            _selectedKey = null;
            SyncCategoryDropdown();
            RefreshKeyRows();
        }

        private void SelectCategory(string categoryName)
        {
            UIKeyCatalog.CategoryEntry category =
                UIKeyCatalog.instance.FindCategory(categoryName, _catalogKind);
            if (category == null)
                return;

            _selectedCategory = category;
            _selectedKey = null;
            int index = _visibleCategories.IndexOf(category);
            if (index >= 0)
                _categoryList.SetSelection(index);
            RefreshKeyRows();
        }

        private void SetNewCategoryMode(bool enabled)
        {
            bool hasCategories = UIKeyCatalog.instance.GetCategories(_catalogKind).Count > 0;
            _newCategoryMode = enabled || !hasCategories;
            _categoryFieldLabel.text = _newCategoryMode ? "NEW CATEGORY" : "CATEGORY";
            _categorySelectRow.style.display =
                _newCategoryMode ? DisplayStyle.None : DisplayStyle.Flex;
            _categoryCreateRow.style.display =
                _newCategoryMode ? DisplayStyle.Flex : DisplayStyle.None;
            _categoryCreateRow
                .Q<Button>(className: "key-catalog__cancel-category-button")
                .style.display = hasCategories ? DisplayStyle.Flex : DisplayStyle.None;

            if (enabled)
                _newCategoryField.Focus();
        }

        private void CreateCategory()
        {
            string categoryName = _newCategoryField.value?.Trim();
            if (string.IsNullOrEmpty(categoryName))
            {
                ShowNotification(new GUIContent("새 Category 이름을 입력하세요."));
                return;
            }

            if (!UIKeyCatalog.instance.AddCategory(categoryName, _catalogKind))
            {
                ShowNotification(new GUIContent("이미 존재하는 Category입니다."));
                return;
            }

            _selectedCategory = UIKeyCatalog.instance.FindCategory(categoryName, _catalogKind);
            _selectedKey = null;
            _newCategoryField.SetValueWithoutNotify(string.Empty);
            _newCategoryMode = false;
            RefreshCategories();
            SelectCategory(categoryName);
            SetNewCategoryMode(false);
        }

        private void AddKey()
        {
            var value = new UIKey(_selectedCategory?.Name, _newKeyField.value);
            if (!value.IsValid)
            {
                ShowNotification(new GUIContent("먼저 Category를 선택하고 Key 이름을 입력하세요."));
                return;
            }

            if (!UIKeyCatalog.instance.Add(value, _catalogKind))
            {
                ShowNotification(new GUIContent("이미 존재하는 Key입니다."));
                return;
            }

            _selectedCategory = UIKeyCatalog.instance.FindCategory(value.Category, _catalogKind);
            _selectedKey = value.Key;
            _newKeyField.SetValueWithoutNotify(string.Empty);
            RefreshCategories();
            SelectCategory(value.Category);
        }

        private void BeginCategoryRename()
        {
            if (_selectedCategory == null)
                return;

            string oldName = _selectedCategory.Name;
            _categoryTitle.style.display = DisplayStyle.None;

            VisualElement heading = _categoryTitle.parent;
            var editRow = new VisualElement();
            editRow.name = "category-edit-row";
            editRow.AddToClassList("key-catalog__inline-edit");
            var field = new TextField { value = oldName };
            field.AddToClassList("key-catalog__inline-field");
            editRow.Add(field);

            bool finished = false;
            void Apply()
            {
                if (finished)
                    return;

                if (RenameCategory(oldName, field.value))
                {
                    finished = true;
                    EndCategoryRename(editRow);
                }
                else
                {
                    field.schedule.Execute(field.Focus);
                }
            }

            void Cancel()
            {
                if (finished)
                    return;

                finished = true;
                EndCategoryRename(editRow);
            }

            editRow.Add(CreateIconButton(
                "✓",
                "변경 내용을 적용합니다.",
                Apply,
                "key-catalog__confirm-button"));
            editRow.Add(CreateIconButton(
                "×",
                "이름 변경을 취소합니다.",
                Cancel,
                "key-catalog__icon-button"));
            RegisterInlineEditEvents(field, Apply, Cancel, () => finished);
            heading.Insert(0, editRow);
            field.Focus();
        }

        private void EndCategoryRename(VisualElement editRow)
        {
            editRow?.RemoveFromHierarchy();
            _categoryTitle.style.display = DisplayStyle.Flex;
        }

        private bool RenameCategory(string oldName, string newName)
        {
            newName = newName?.Trim();
            if (string.IsNullOrEmpty(newName) ||
                UIKeyCatalog.instance.FindCategory(newName, _catalogKind) != null &&
                !string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                ShowNotification(new GUIContent("새 Category 이름을 확인하세요."));
                return false;
            }

            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return true;

            UIKeyUsage[] affected = _usages
                .AsValueEnumerable()
                .Where(usage =>
                    usage.CatalogKind == _catalogKind &&
                    string.Equals(
                        usage.Value.Category,
                        oldName,
                        StringComparison.Ordinal))
                .ToArray();
            if (!ConfirmRename($"{oldName} → {newName}", affected))
                return true;

            if (!UIKeyProjectService.RenameCategory(
                    oldName,
                    newName,
                    _catalogKind,
                    out string error))
            {
                EditorUtility.DisplayDialog("Rename 실패", error, "확인");
                return true;
            }

            UIKeyCatalog.instance.RenameCategoryLocal(oldName, newName, _catalogKind);
            _selectedCategory = UIKeyCatalog.instance.FindCategory(newName, _catalogKind);
            RefreshUsages();
            SelectCategory(newName);
            return true;
        }

        private void DeleteCategory()
        {
            if (_selectedCategory == null)
                return;

            DeleteCategory(_selectedCategory);
        }

        private void DeleteCategory(UIKeyCatalog.CategoryEntry category)
        {
            if (category == null)
                return;

            string categoryName = category.Name;
            int uses = UIKeyProjectService.CountCategoryUsages(
                categoryName,
                _usages,
                _catalogKind);
            string message = uses > 0
                ? $"{categoryName}에는 {uses}개의 사용처가 있습니다.\n" +
                  "참조 문자열은 유지되고 미등록 경고로 표시됩니다."
                : $"{categoryName} Category를 삭제할까요?";
            if (!EditorUtility.DisplayDialog("Category 삭제", message, "삭제", "취소"))
                return;

            UIKeyCatalog.instance.RemoveCategory(categoryName, _catalogKind);
            _selectedCategory = null;
            _selectedKey = null;
            RefreshCategories();
        }

        private void BuildKeyRow(string key)
        {
            var value = new UIKey(_selectedCategory.Name, key);
            var row = new VisualElement();
            row.AddToClassList("key-catalog__key-row");

            var editButton = CreateIconButton(
                "✎",
                $"{value} 이름을 변경합니다.",
                () => BeginKeyRename(row, value),
                "key-catalog__row-edit-button");
            row.Add(editButton);

            var nameButton = new Button
            {
                text = key,
                tooltip = $"{value} 사용처를 표시합니다. 더블클릭하면 이름을 변경합니다."
            };
            nameButton.AddToClassList("key-catalog__key-name");
            nameButton.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount >= 2)
                {
                    BeginKeyRename(row, value);
                    evt.StopImmediatePropagation();
                    return;
                }

                SelectKey(value);
            });
            row.Add(nameButton);

            int useCount = UIKeyProjectService.CountUsages(value, _usages, _catalogKind);
            var usage = new Label($"{useCount} uses");
            usage.AddToClassList("key-catalog__usage-badge");
            usage.tooltip = BuildUsageTooltip(value);
            row.Add(usage);

            row.Add(CreateIconButton(
                "⧉",
                $"{value}를 클립보드에 복사합니다.",
                () =>
                {
                    EditorGUIUtility.systemCopyBuffer = value.ToString();
                    ShowNotification(new GUIContent($"{value} 복사됨"));
                },
                "key-catalog__copy-button"));
            row.Add(CreateIconButton(
                "−",
                $"{value}를 삭제합니다.",
                () => DeleteKey(value),
                "key-catalog__remove-button"));
            _keyRows.Add(row);
        }

        private void BeginKeyRename(VisualElement row, UIKey oldValue)
        {
            row.Clear();
            row.AddToClassList("key-catalog__inline-edit");
            var field = new TextField { value = oldValue.Key };
            field.AddToClassList("key-catalog__inline-field");
            row.Add(field);

            bool finished = false;
            void Apply()
            {
                if (finished)
                    return;

                if (RenameKey(oldValue, field.value))
                {
                    finished = true;
                }
                else
                {
                    field.schedule.Execute(field.Focus);
                }
            }

            void Cancel()
            {
                if (finished)
                    return;

                finished = true;
                RefreshKeyRows();
            }

            row.Add(CreateIconButton(
                "✓",
                "변경 내용을 적용합니다.",
                Apply,
                "key-catalog__confirm-button"));
            row.Add(CreateIconButton(
                "×",
                "이름 변경을 취소합니다.",
                Cancel,
                "key-catalog__icon-button"));
            RegisterInlineEditEvents(field, Apply, Cancel, () => finished);
            field.Focus();
        }

        private bool RenameKey(UIKey oldValue, string newKey)
        {
            newKey = newKey?.Trim();
            if (string.IsNullOrEmpty(newKey) ||
                _selectedCategory.Contains(newKey) &&
                !string.Equals(oldValue.Key, newKey, StringComparison.Ordinal))
            {
                ShowNotification(new GUIContent("새 Key 이름을 확인하세요."));
                return false;
            }

            if (string.Equals(oldValue.Key, newKey, StringComparison.Ordinal))
            {
                RefreshKeyRows();
                return true;
            }

            UIKeyUsage[] affected = _usages.AsValueEnumerable().Where(usage =>
                usage.CatalogKind == _catalogKind &&
                usage.Value == oldValue).ToArray();
            if (!ConfirmRename(
                    $"{oldValue} → {oldValue.Category}/{newKey}",
                    affected))
            {
                RefreshKeyRows();
                return true;
            }

            if (!UIKeyProjectService.RenameKey(
                    oldValue,
                    newKey,
                    _catalogKind,
                    out string error))
            {
                EditorUtility.DisplayDialog("Rename 실패", error, "확인");
                RefreshKeyRows();
                return true;
            }

            UIKeyCatalog.instance.RenameKeyLocal(oldValue, newKey, _catalogKind);
            _selectedKey = newKey;
            RefreshUsages();
            return true;
        }

        private static void RegisterInlineEditEvents(
            TextField field,
            Action apply,
            Action cancel,
            Func<bool> isFinished)
        {
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return ||
                    evt.keyCode == KeyCode.KeypadEnter)
                {
                    apply();
                    evt.StopImmediatePropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    cancel();
                    evt.StopImmediatePropagation();
                }
            });

            field.RegisterCallback<FocusOutEvent>(_ =>
            {
                field.schedule.Execute(() =>
                {
                    if (!isFinished())
                        apply();
                });
            });
        }

        private void DeleteKey(UIKey value)
        {
            int uses = UIKeyProjectService.CountUsages(value, _usages, _catalogKind);
            string message = uses > 0
                ? $"{value}에는 {uses}개의 사용처가 있습니다.\n" +
                  "참조 문자열은 유지되고 미등록 경고로 표시됩니다."
                : $"{value}를 삭제할까요?";
            if (!EditorUtility.DisplayDialog("Key 삭제", message, "삭제", "취소"))
                return;

            UIKeyCatalog.instance.Remove(value, _catalogKind);
            if (_selectedKey == value.Key)
                _selectedKey = null;
            RefreshKeyRows();
        }

        private void SelectKey(UIKey value)
        {
            _selectedKey = value.Key;
            RefreshUsageList();
            _usageFoldout.value = true;
        }

        private string BuildUsageTooltip(UIKey value)
        {
            string[] paths = _usages
                .AsValueEnumerable()
                .Where(usage =>
                    usage.CatalogKind == _catalogKind &&
                    usage.Value == value)
                .Select(usage => usage.AssetPath)
                .Distinct(StringComparer.Ordinal)
                .Take(8)
                .ToArray();
            return paths.Length == 0
                ? "프로젝트 사용처 없음"
                : string.Join("\n", paths);
        }

        private void ScanProject()
        {
            _usages = UIKeyProjectService.ScanProject();
            int added = 0;
            foreach (UIKeyCatalogKind kind in Enum.GetValues(typeof(UIKeyCatalogKind)))
            {
                added += UIKeyCatalog.instance.AddRange(
                    _usages
                        .AsValueEnumerable()
                        .Where(usage => usage.CatalogKind == kind)
                        .Select(usage => usage.Value)
                        .ToArray(),
                    kind);
            }
            EditorUtility.DisplayDialog(
                "UI Navigation Scan",
                $"{_usages.Count}개의 사용처를 찾았고 {added}개의 Key를 등록했습니다.",
                "확인");
            RefreshCategories();
        }

        private void RefreshUsages()
        {
            _usages = UIKeyProjectService.ScanProject();
            RefreshCategories();
        }

        private void OnCatalogChanged()
        {
            RefreshCategories();
        }

        private void RefreshCategories()
        {
            if (_categoryList == null)
                return;

            _refreshing = true;
            string selectedName = _selectedCategory?.Name;
            _visibleCategories.Clear();
            foreach (UIKeyCatalog.CategoryEntry category in
                     UIKeyCatalog.instance.GetCategories(_catalogKind))
            {
                bool categoryMatches = MatchesSearch(category.Name);
                bool keyMatches = category.Keys.AsValueEnumerable().Any(MatchesSearch);
                if (categoryMatches || keyMatches)
                    _visibleCategories.Add(category);
            }

            _selectedCategory = _visibleCategories.AsValueEnumerable().FirstOrDefault(category =>
                                    string.Equals(
                                        category.Name,
                                        selectedName,
                                        StringComparison.Ordinal))
                                ?? _visibleCategories.AsValueEnumerable().FirstOrDefault();

            _categoryList.RefreshItems();
            if (_selectedCategory != null)
            {
                int index = _visibleCategories.IndexOf(_selectedCategory);
                if (index >= 0)
                    _categoryList.SetSelectionWithoutNotify(new[] { index });
            }
            else
            {
                _categoryList.ClearSelection();
            }

            SyncCategoryDropdown();
            _catalogSummary.text =
                $"{UIKeyCatalog.instance.GetCategories(_catalogKind).Count} categories  ·  " +
                $"{UIKeyCatalog.instance.GetKeys(_catalogKind).AsValueEnumerable().Count()} keys";
            _refreshing = false;
            SetNewCategoryMode(
                _newCategoryMode ||
                UIKeyCatalog.instance.GetCategories(_catalogKind).Count == 0);
            RefreshKeyRows();
        }

        private void SyncCategoryDropdown()
        {
            if (_categoryDropdown == null)
                return;

            _categoryDropdown.choices = UIKeyCatalog.instance.GetCategories(_catalogKind)
                .AsValueEnumerable()
                .Select(category => category.Name)
                .ToList();
            _categoryDropdown.SetValueWithoutNotify(
                _selectedCategory?.Name ?? string.Empty);
        }

        private void RefreshKeyRows()
        {
            if (_keyRows == null)
                return;

            _keyRows.Clear();
            if (_selectedCategory == null)
            {
                _categoryTitle.text = "No Category";
                _categoryMeta.text = "Create a category and add the first key.";
                _itemCount.text = "0 items";
                RefreshUsageList();
                return;
            }

            _categoryTitle.text = _selectedCategory.Name;
            int categoryUses = UIKeyProjectService.CountCategoryUsages(
                _selectedCategory.Name,
                _usages,
                _catalogKind);
            _categoryMeta.text =
                $"{_selectedCategory.Keys.Count} keys  ·  {categoryUses} project uses";

            IEnumerable<string> keys = _selectedCategory.Keys;
            if (!string.IsNullOrEmpty(_search))
                keys = keys.AsValueEnumerable().Where(MatchesSearch).ToArray();

            string[] visibleKeys = keys.AsValueEnumerable().ToArray();
            foreach (string key in visibleKeys)
                BuildKeyRow(key);

            if (visibleKeys.Length == 0)
            {
                var empty = new Label(
                    string.IsNullOrEmpty(_search)
                        ? "아직 등록된 Key가 없습니다."
                        : "검색 결과가 없습니다.");
                empty.AddToClassList("key-catalog__empty");
                _keyRows.Add(empty);
            }

            _itemCount.text = $"{visibleKeys.Length} items";
            RefreshUsageList();
        }

        private void RefreshUsageList()
        {
            if (_usageList == null)
                return;

            _visibleUsages.Clear();
            if (_selectedCategory != null && !string.IsNullOrEmpty(_selectedKey))
            {
                var value = new UIKey(_selectedCategory.Name, _selectedKey);
                _visibleUsages.AddRange(_usages.AsValueEnumerable().Where(usage =>
                    usage.CatalogKind == _catalogKind &&
                    usage.Value == value).ToArray());
                _usageFoldout.text = $"{value} Usages ({_visibleUsages.Count})";
            }
            else
            {
                _usageFoldout.text = "Selected Key Usages";
            }

            _usageList.RefreshItems();
        }

        private bool MatchesSearch(string value)
        {
            return string.IsNullOrEmpty(_search) ||
                   (!string.IsNullOrEmpty(value) &&
                    value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ConfirmRename(
            string title,
            IReadOnlyCollection<UIKeyUsage> usages)
        {
            string[] paths = usages
                .AsValueEnumerable()
                .Select(usage => usage.AssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string preview = paths.Length == 0
                ? "사용처가 없습니다."
                : string.Join("\n", paths.AsValueEnumerable().Take(8).ToArray());
            if (paths.Length > 8)
                preview += $"\n... 외 {paths.Length - 8}개 파일";

            return EditorUtility.DisplayDialog(
                "UI Navigation Rename",
                $"{title}\n\n{usages.Count}개 참조 / {paths.Length}개 파일\n\n{preview}",
                "참조까지 변경",
                "취소");
        }
    }
}
