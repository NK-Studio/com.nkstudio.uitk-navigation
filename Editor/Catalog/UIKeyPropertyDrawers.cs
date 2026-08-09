using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
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

    [CustomPropertyDrawer(typeof(UIKey))]
    internal sealed class UIKeyPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty category = property.FindPropertyRelative("category");
            SerializedProperty key = property.FindPropertyRelative("key");
            if (category == null || key == null)
                return new Label($"UIKey 직렬화 필드를 찾지 못했습니다: {property.propertyPath}");

            UIKeyPickerField field = CreateField(property.displayName, property, category, key);
            field.TrackPropertyValue(category, _ => field.Refresh());
            field.TrackPropertyValue(key, _ => field.Refresh());
            return field;
        }

        internal static UIKeyPickerField CreateField(
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
    }

    [CustomPropertyDrawer(typeof(UIKeySelectorAttribute))]
    internal sealed class UIKeySelectorPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var selector = (UIKeySelectorAttribute)attribute;
            SerializedProperty key = FindSibling(property, selector.KeyPropertyName);
            if (key == null)
            {
                return new HelpBox(
                    $"Companion Key 속성 '{selector.KeyPropertyName}'을 찾지 못했습니다.",
                    HelpBoxMessageType.Error);
            }

            UIKeyPickerField field = UIKeyPropertyDrawer.CreateField(
                property.displayName.Replace(" Category", string.Empty),
                property,
                property,
                key,
                () => selector.Kind);
            field.TrackPropertyValue(property, _ => field.Refresh());
            field.TrackPropertyValue(key, _ => field.Refresh());
            return field;
        }

        private static SerializedProperty FindSibling(
            SerializedProperty property,
            string siblingName)
        {
            string parentPath = string.Empty;
            int separator = property.propertyPath.LastIndexOf('.');
            if (separator >= 0)
                parentPath = property.propertyPath.Substring(0, separator + 1);

            SerializedProperty sibling = property.serializedObject.FindProperty(parentPath + siblingName);
            if (sibling != null)
                return sibling;

            if (string.IsNullOrEmpty(siblingName))
                return null;

            string camelCase = char.ToLowerInvariant(siblingName[0]) + siblingName.Substring(1);
            return property.serializedObject.FindProperty(parentPath + camelCase);
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
            UINavigationListDrawerUtility.AttachGraphInspectorStyles(root);

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
            body.Add(UINavigationListDrawerUtility.CreateUIKeyList(
                "Show Views",
                "Views that will be shown when the node is activated",
                property.FindPropertyRelative("showCommands")));
            body.Add(UINavigationListDrawerUtility.CreateUIKeyList(
                "Hide Views",
                "Views that will be hidden when the node is activated",
                property.FindPropertyRelative("hideCommands")));
            root.Add(body);
            return root;
        }
    }
    [CustomPropertyDrawer(typeof(UINavigationOutputCollection))]
    internal sealed class UINavigationOutputCollectionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.AddToClassList("uinavigation-phase");
            root.AddToClassList("uinavigation-output-phase");
            UINavigationListDrawerUtility.AttachGraphInspectorStyles(root);

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
            body.Add(UINavigationListDrawerUtility.CreateOutputList(
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

    [CustomPropertyDrawer(typeof(UINavigationLoadSceneSettings))]
    internal sealed class UINavigationLoadSceneSettingsDrawer : PropertyDrawer
    {
        private static readonly Color Panel = new(0.145f, 0.155f, 0.175f, 0.96f);
        private static readonly Color Muted = new(0.56f, 0.59f, 0.65f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty referenceKind =
                property.FindPropertyRelative("referenceKind");
            SerializedProperty sceneName = property.FindPropertyRelative("sceneName");
            SerializedProperty buildIndex = property.FindPropertyRelative("buildIndex");
            SerializedProperty loadMode = property.FindPropertyRelative("loadMode");
            SerializedProperty allowActivation =
                property.FindPropertyRelative("allowSceneActivation");
            SerializedProperty activationDelay =
                property.FindPropertyRelative("sceneActivationDelay");

            var root = new VisualElement();
            root.style.marginTop = 6f;
            root.style.marginBottom = 7f;

            root.Add(CreateSectionTitle("SCENE REFERENCE"));
            var referencePanel = CreatePanel();
            var referenceRow = CreateRow();

            var referenceField = new EnumField(
                (UINavigationSceneReferenceKind)referenceKind.enumValueIndex);
            VisualElement referenceColumn = CreateColumn(
                "GET SCENE BY",
                referenceField,
                145f);
            referenceColumn.style.flexGrow = 0f;
            referenceRow.Add(referenceColumn);

            var nameField = new TextField { value = sceneName.stringValue, isDelayed = true };
            nameField.tooltip = "Build Settings에 등록된 Scene 이름 또는 경로입니다.";
            VisualElement nameColumn = CreateColumn("SCENE NAME", nameField);
            nameColumn.style.flexGrow = 1f;
            referenceRow.Add(nameColumn);

            var indexField = new IntegerField
            {
                value = Mathf.Max(0, buildIndex.intValue),
                isDelayed = true
            };
            indexField.tooltip = "Build Settings의 Scene 인덱스입니다.";
            VisualElement indexColumn = CreateColumn("BUILD INDEX", indexField);
            indexColumn.style.flexGrow = 1f;
            referenceRow.Add(indexColumn);

            var modeField = new EnumField(
                (LoadSceneMode)loadMode.enumValueIndex);
            modeField.tooltip =
                "Single은 현재 Scene을 교체하고, Additive는 현재 Scene 위에 추가로 로드합니다.";
            VisualElement modeColumn = CreateColumn(
                "LOAD SCENE MODE",
                modeField,
                145f);
            modeColumn.style.flexGrow = 0f;
            modeColumn.style.marginRight = 0f;
            referenceRow.Add(modeColumn);
            referencePanel.Add(referenceRow);
            root.Add(referencePanel);

            root.Add(CreateSectionTitle("ACTIVATION"));
            var activationPanel = CreatePanel();
            var activationRow = CreateRow();

            var allowField = new Toggle("Allow Scene Activation")
            {
                value = allowActivation.boolValue
            };
            allowField.tooltip =
                "로드가 90%에 도달했을 때 Scene을 자동으로 활성화합니다.";
            allowField.style.flexGrow = 1f;
            allowField.style.unityFontStyleAndWeight = FontStyle.Bold;
            activationRow.Add(allowField);

            var delayField = new FloatField
            {
                value = Mathf.Max(0f, activationDelay.floatValue),
                isDelayed = true
            };
            delayField.tooltip =
                "비동기 로드가 90%에 도달한 뒤 활성화하기 전까지 기다릴 Unscaled Time입니다.";
            VisualElement delayColumn = CreateColumn(
                "ACTIVATION DELAY (SEC)",
                delayField,
                185f);
            delayColumn.style.flexGrow = 0f;
            activationRow.Add(delayColumn);
            activationPanel.Add(activationRow);
            root.Add(activationPanel);

            var pendingHint = new Label("90%에서 대기 · 수동 활성화 필요");
            pendingHint.tooltip =
                "UINavigatorBehaviour.ActivatePendingScene()으로 활성화를 계속합니다.";
            pendingHint.style.fontSize = 10f;
            pendingHint.style.color = Muted;
            pendingHint.style.marginTop = 4f;
            pendingHint.style.marginLeft = 4f;
            activationPanel.Add(pendingHint);

            void Refresh()
            {
                bool useIndex =
                    (UINavigationSceneReferenceKind)referenceKind.enumValueIndex ==
                    UINavigationSceneReferenceKind.BuildIndex;
                nameColumn.style.display =
                    useIndex ? DisplayStyle.None : DisplayStyle.Flex;
                indexColumn.style.display =
                    useIndex ? DisplayStyle.Flex : DisplayStyle.None;
                delayColumn.SetEnabled(allowActivation.boolValue);
                pendingHint.style.display =
                    allowActivation.boolValue
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
            }

            referenceField.RegisterValueChangedCallback(evt =>
            {
                referenceKind.enumValueIndex =
                    (int)(UINavigationSceneReferenceKind)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            nameField.RegisterValueChangedCallback(evt =>
            {
                sceneName.stringValue = evt.newValue?.Trim() ?? string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            });
            indexField.RegisterValueChangedCallback(evt =>
            {
                int value = Mathf.Max(0, evt.newValue);
                buildIndex.intValue = value;
                property.serializedObject.ApplyModifiedProperties();
                if (value != evt.newValue)
                    indexField.SetValueWithoutNotify(value);
            });
            modeField.RegisterValueChangedCallback(evt =>
            {
                loadMode.enumValueIndex = (int)(LoadSceneMode)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            allowField.RegisterValueChangedCallback(evt =>
            {
                allowActivation.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            delayField.RegisterValueChangedCallback(evt =>
            {
                float value = Mathf.Max(0f, evt.newValue);
                activationDelay.floatValue = value;
                property.serializedObject.ApplyModifiedProperties();
                if (!Mathf.Approximately(value, evt.newValue))
                    delayField.SetValueWithoutNotify(value);
            });
            Refresh();
            return root;
        }

        private static Label CreateSectionTitle(string text)
        {
            var title = new Label(text);
            title.style.fontSize = 10f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Muted;
            title.style.marginTop = 6f;
            title.style.marginBottom = 3f;
            title.style.marginLeft = 3f;
            return title;
        }

        private static VisualElement CreatePanel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = Panel;
            panel.style.paddingTop = 7f;
            panel.style.paddingBottom = 7f;
            panel.style.paddingLeft = 8f;
            panel.style.paddingRight = 8f;
            panel.style.borderTopLeftRadius = 4f;
            panel.style.borderTopRightRadius = 4f;
            panel.style.borderBottomLeftRadius = 4f;
            panel.style.borderBottomRightRadius = 4f;
            return panel;
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            return row;
        }

        private static VisualElement CreateColumn(
            string label,
            VisualElement field,
            float width = 0f)
        {
            var column = new VisualElement();
            column.style.marginRight = 7f;
            if (width > 0f)
                column.style.width = width;

            var caption = new Label(label);
            caption.style.fontSize = 9f;
            caption.style.color = Muted;
            caption.style.marginBottom = 2f;
            column.Add(caption);
            column.Add(field);
            return column;
        }
    }

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
            UINavigationListDrawerUtility.AttachGraphInspectorStyles(root);

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
            UIKeyPickerField databaseField = UIKeyPropertyDrawer.CreateField(
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

            MatchInputBoundsToNodeOption(root, kindField);
            MatchInputBoundsToNodeOption(root, customField);

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

        internal static void MatchInputBoundsToNodeOption(
            VisualElement owner,
            VisualElement field)
        {
            VisualElement input = field.Q<VisualElement>(
                className: "unity-base-field__input");
            VisualElement referenceInput = null;

            void ResolveReferenceInput()
            {
                VisualElement panelRoot = field.panel?.visualTree;
                if (panelRoot == null)
                {
                    referenceInput = null;
                    return;
                }

                Label referenceLabel = panelRoot
                    .Query<Label>(className: "ge-model-property-field__label")
                    .Where(candidate =>
                        candidate != null &&
                        !owner.Contains(candidate) &&
                        candidate.parent?.Q<VisualElement>(
                            className: "unity-base-field__input") != null)
                    .Build()
                    .First();
                referenceInput = referenceLabel?.parent?.Q<VisualElement>(
                    className: "unity-base-field__input");
            }

            void Apply()
            {
                if (input == null ||
                    input.worldBound.width <= 0f)
                {
                    return;
                }

                if (referenceInput == null ||
                    referenceInput.panel != field.panel)
                {
                    ResolveReferenceInput();
                }

                if (referenceInput == null ||
                    referenceInput.worldBound.width <= 0f)
                {
                    return;
                }

                float leftOffset =
                    referenceInput.worldBound.xMin - input.worldBound.xMin;
                float rightOffset =
                    input.worldBound.xMax - referenceInput.worldBound.xMax;
                if (Mathf.Abs(leftOffset) <= 0.25f &&
                    Mathf.Abs(rightOffset) <= 0.25f)
                {
                    return;
                }

                input.style.marginLeft =
                    input.resolvedStyle.marginLeft + leftOffset;
                input.style.marginRight =
                    input.resolvedStyle.marginRight + rightOffset;
            }

            field.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ResolveReferenceInput();
                Apply();
            });
            field.RegisterCallback<DetachFromPanelEvent>(_ =>
                referenceInput = null);
            field.RegisterCallback<GeometryChangedEvent>(_ => Apply());
        }
    }

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
            UINavigationListDrawerUtility.AttachGraphInspectorStyles(root);

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

            UIKeyPickerField keyField = UIKeyPropertyDrawer.CreateField(
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

            UINavigationSignalAddressDrawer.MatchInputBoundsToNodeOption(
                root,
                kindField);
            UINavigationSignalAddressDrawer.MatchInputBoundsToNodeOption(
                root,
                addressKindField);
            UINavigationSignalAddressDrawer.MatchInputBoundsToNodeOption(
                root,
                customSignalField);
            UINavigationSignalAddressDrawer.MatchInputBoundsToNodeOption(
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

    internal static class UINavigationListDrawerUtility
    {
        private const float KeyRowHeight = 30f;
        private const float OutputRowHeight = 96f;

        internal static VisualElement CreateUIKeyList(
            string title,
            string description,
            SerializedProperty array)
        {
            VisualElement section = CreateList(
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
                    var picker = UIKeyPropertyDrawer.CreateField(
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

                    var removeButton = CreateRemoveButton(
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
        internal static VisualElement CreateOutputList(SerializedProperty array)
        {
            VisualElement section = CreateList(
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

                    VisualElement address = CreateOutputAddressField(
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

                    var removeButton = CreateRemoveButton(
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
                ConfigureOutputAddButton);
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
        private static VisualElement CreateOutputAddressField(
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
            categoryColumn.Add(CreateOutputMiniLabel("Category"));
            var categoryDropdown = new DropdownField();
            categoryColumn.Add(categoryDropdown);
            root.Add(categoryColumn);

            var nameColumn = new VisualElement();
            nameColumn.style.flexGrow = 1f;
            nameColumn.Add(CreateOutputMiniLabel("Name"));
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
        private static Label CreateOutputMiniLabel(string text)
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
        internal static void AttachGraphInspectorStyles(VisualElement root)
        {
            if (root == null)
                return;

            StyleSheet styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.nkstudio.uitk-navigation/Editor/Styles/UINavigationGraphInspector.uss");
            if (styles != null && !root.styleSheets.Contains(styles))
                root.styleSheets.Add(styles);
        }

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
            AttachGraphInspectorStyles(root);

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
        private static VisualElement CreateRowContainer()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1f;
            return row;
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

        private static UIKeyCatalogKind GetCatalogKind(UINavigationTriggerKind trigger)
        {
            return trigger switch
            {
                UINavigationTriggerKind.Toggle => UIKeyCatalogKind.Toggle,
                UINavigationTriggerKind.UIView => UIKeyCatalogKind.View,
                _ => UIKeyCatalogKind.Signal
            };
        }

        private static void ConfigureOutputAddButton(
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
