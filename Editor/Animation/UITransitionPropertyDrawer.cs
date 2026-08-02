using System;
using System.Collections.Generic;
using System.Reflection;
using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Animation.Presets;
using NKStudio.UITKNavigation.Elements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Provides UI Transition Property Drawer functionality.
    /// </summary>
    [CustomPropertyDrawer(typeof(UITransitionInspectorAttribute))]
    internal sealed class UITransitionPropertyDrawer : PropertyDrawer
    {
        private static readonly Color MoveColor = new(0.388f, 0.600f, 0.133f);
        private static readonly Color RotateColor = new(0.729f, 0.459f, 0.090f);
        private static readonly Color ScaleColor = new(0.831f, 0.325f, 0.494f);
        private static readonly Color FadeColor = new(0.216f, 0.541f, 0.867f);

        private static readonly Color ShowAccent = new(0.32f, 0.55f, 0.40f);
        private static readonly Color HideAccent = new(0.62f, 0.36f, 0.38f);

        private static readonly Color CellOff = new(0.30f, 0.31f, 0.34f);

        private static readonly string[] ChannelNames = { "Move", "Rotate", "Scale", "Fade" };

        private const string AlignedFieldClass = "unity-base-field__aligned";
        internal const string ThemeStyleSheetPath = "Packages/com.nkstudio.uitk-navigation/Editor/Animation/UITransitionPropertyDrawer.uss";
        private const string PlayIconPath = "Packages/com.nkstudio.uitk-navigation/Editor/Assets/PlayIcon.svg";
        private const string ResetIconPath = "Packages/com.nkstudio.uitk-navigation/Editor/Assets/ResetIcon.svg";

        private static ManualMotionDispatcher _previewDispatcher;
        private static IVisualElementScheduledItem _previewPump;
        private static NavElement _previewElement;
        private static UIAnimationType _previewType;
        private static Action _previewHideRestore;

        /// <summary>
        /// Gets the preview targets.
        /// </summary>
        private static readonly List<NavElement> PreviewTargets = new List<NavElement>();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty show = property.FindPropertyRelative("Show");
            SerializedProperty hide = property.FindPropertyRelative("Hide");

            var root = new VisualElement();
            StyleSheet theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeStyleSheetPath);
            if (theme != null)
                root.styleSheets.Add(theme);
            root.AddToClassList("nk-transition-root");
            root.style.marginTop = 2f;
            root.style.marginBottom = 4f;

            root.RegisterCallback<DetachFromPanelEvent>(_ => CancelPreview());

            VisualElement showPanel = show != null ? CreateDirectionPanel(show, UIAnimationType.Show) : null;
            VisualElement hidePanel = hide != null ? CreateDirectionPanel(hide, UIAnimationType.Hide) : null;

            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 6f;

            var showTab = CreateMainTab("Show", show);
            showTab.root.style.marginRight = 5f;
            var hideTab = CreateMainTab("Hide", hide);

            var hint = new Label(string.Empty)
            {
                style = { fontSize = 10f, color = HideAccent, unityTextAlign = TextAnchor.MiddleCenter, marginRight = 5f, alignSelf = Align.Center }
            };
            var preview = CreateIconButton(PlayIconPath, 16f);
            preview.tooltip = "선택한 Element로 현재 탭의 애니메이션을 재생";
            preview.style.width = 26f;
            preview.style.alignSelf = Align.Center;
            var reset = CreateIconButton(ResetIconPath, 16f);
            reset.tooltip = "캔버스를 On Start 설정 기준으로 되돌림";
            reset.style.width = 24f;
            reset.style.marginLeft = 3f;
            reset.style.alignSelf = Align.Center;

            tabRow.Add(showTab.root);
            tabRow.Add(hideTab.root);
            tabRow.Add(hint);
            tabRow.Add(preview);
            tabRow.Add(reset);
            root.Add(tabRow);

            if (showPanel != null) root.Add(showPanel);
            if (hidePanel != null) root.Add(hidePanel);

            bool showSelected = true;

            void UpdateTabs()
            {
                showTab.SetActive(showSelected, ShowAccent);
                hideTab.SetActive(!showSelected, HideAccent);
                if (showPanel != null) showPanel.style.display = showSelected ? DisplayStyle.Flex : DisplayStyle.None;
                if (hidePanel != null) hidePanel.style.display = showSelected ? DisplayStyle.None : DisplayStyle.Flex;
            }

            showTab.button.clicked += () => { showSelected = true; UpdateTabs(); };
            hideTab.button.clicked += () => { showSelected = false; UpdateTabs(); };
            UpdateTabs();

            preview.clicked += () => RunPreview(showSelected ? UIAnimationType.Show : UIAnimationType.Hide, hint);
            reset.clicked += () =>
            {
                CancelPreview();
                if (TryGetInspectedNavElement(out var element))
                {
                    hint.text = string.Empty;
                    element.RestoreStartState();
                }
            };

            void RefreshActionButtons()
            {
                preview.SetEnabled(true);
                reset.SetEnabled(true);
            }

            foreach (SerializedProperty dir in new[] { show, hide })
            {
                if (dir == null)
                    continue;
                foreach (string channel in ChannelNames)
                {
                    SerializedProperty enable = dir.FindPropertyRelative(channel)?.FindPropertyRelative("Enable");
                    if (enable != null)
                    {
                        root.TrackPropertyValue(enable, _ =>
                        {
                            RefreshActionButtons();
                            SyncUsageHints(property);
                        });
                    }
                }
            }
            RefreshActionButtons();

            return root;
        }

        #region usageHints authoring

        /// <summary>
        /// Defines the managed usage hints value.
        /// </summary>
        private const UsageHints ManagedUsageHints = UsageHints.DynamicTransform | UsageHints.DynamicColor;

        /// <summary>
        /// Performs the sync usage hints operation.
        /// </summary>
        private static void SyncUsageHints(SerializedProperty transitionsProperty)
        {
            if (transitionsProperty == null)
                return;

            try
            {
                SerializedProperty hints = FindOwnerProperty(transitionsProperty, "usageHints");
                if (hints == null)
                    return;

                UsageHints required =
                    RequiredUsageHints(transitionsProperty.FindPropertyRelative("Show"))
                    | RequiredUsageHints(transitionsProperty.FindPropertyRelative("Hide"));

                var current = (UsageHints)hints.intValue;
                UsageHints next = (current & ~ManagedUsageHints) | required;
                if (next == current)
                    return;

                hints.intValue = (int)next;

                SerializedProperty flags = FindOwnerProperty(transitionsProperty, "usageHints_UxmlAttributeFlags");
                if (flags != null)
                {
                    flags.intValue = (int)(next == UsageHints.None
                        ? UxmlSerializedData.UxmlAttributeFlags.Ignore
                        : UxmlSerializedData.UxmlAttributeFlags.OverriddenInUxml);
                }

                hints.serializedObject.ApplyModifiedProperties();
            }
            catch
            {
            }
        }

        private static UsageHints RequiredUsageHints(SerializedProperty direction)
        {
            if (direction == null)
                return UsageHints.None;

            UsageHints hints = UsageHints.None;

            if (IsChannelEnabled(direction, "Move")
                || IsChannelEnabled(direction, "Rotate")
                || IsChannelEnabled(direction, "Scale"))
            {
                hints |= UsageHints.DynamicTransform;
            }

            if (IsChannelEnabled(direction, "Fade"))
                hints |= UsageHints.DynamicColor;

            return hints;
        }

        private static bool IsChannelEnabled(SerializedProperty direction, string channel)
        {
            SerializedProperty enable = direction.FindPropertyRelative(channel)?.FindPropertyRelative("Enable");
            return enable != null && enable.boolValue;
        }

        /// <summary>
        /// Finds o wn er pr op er ty.
        /// </summary>
        private static SerializedProperty FindOwnerProperty(SerializedProperty property, string name)
        {
            return UxmlAuthoringUtility.FindOwnerProperty(property, name);
        }

        #endregion

        #region Main tabs (Show / Hide) with channel indicators

        private struct MainTab
        {
            public VisualElement root;
            public Button button;
            private VisualElement _bar;
            public void SetActive(bool active, Color accent)
            {
                button.EnableInClassList("nk-transition-tab--active", active);
                _bar.style.backgroundColor = active ? accent : Color.clear;
            }
            public MainTab(VisualElement r, Button b, VisualElement bar) { root = r; button = b; _bar = bar; }
        }

        private static MainTab CreateMainTab(string title, SerializedProperty dir)
        {
            var button = new Button();
            button.style.flexGrow = 1f;
            button.style.paddingTop = 5f;
            button.style.paddingBottom = 0f;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            Round(button, 4f);

            var label = new Label(title)
            {
                style = { fontSize = 12f, unityFontStyleAndWeight = FontStyle.Bold }
            };
            label.AddToClassList("nk-transition-title");
            button.AddToClassList("nk-transition-tab");
            button.Add(label);

            var dots = new VisualElement();
            dots.style.flexDirection = FlexDirection.Row;
            dots.style.marginTop = 3f;
            dots.Add(ChannelDot(dir, "Move", MoveColor));
            dots.Add(ChannelDot(dir, "Rotate", RotateColor));
            dots.Add(ChannelDot(dir, "Scale", ScaleColor));
            dots.Add(ChannelDot(dir, "Fade", FadeColor));
            button.Add(dots);

            var bar = new VisualElement();
            bar.style.height = 2f;
            bar.style.marginTop = 4f;
            bar.style.width = Length.Percent(70);
            Round(bar, 1f);
            button.Add(bar);

            return new MainTab(button, button, bar);
        }

        private static VisualElement ChannelDot(SerializedProperty dir, string channel, Color onColor)
        {
            var dot = new VisualElement();
            dot.style.width = 5f;
            dot.style.height = 5f;
            dot.style.marginLeft = 1.5f;
            dot.style.marginRight = 1.5f;
            Round(dot, 2.5f);

            SerializedProperty enableProp = dir?.FindPropertyRelative(channel)?.FindPropertyRelative("Enable");
            void Refresh() => dot.style.backgroundColor = enableProp != null && enableProp.boolValue ? onColor : CellOff;
            if (enableProp != null) dot.TrackPropertyValue(enableProp, _ => Refresh());
            Refresh();
            return dot;
        }

        #endregion

        #region Preset selection

        /// <summary>
        /// Creates p re se tb lo ck.
        /// </summary>
        private static VisualElement CreatePresetBlock(
            SerializedProperty dir,
            SerializedProperty categoryProp,
            SerializedProperty variantProp,
            UIAnimationType type)
        {
            var block = new VisualElement { style = { marginBottom = 6f } };

            var categoryField = new PropertyField(categoryProp, "Preset");
            categoryField.AddToClassList(AlignedFieldClass);
            block.Add(categoryField);

            var variantRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 2f }
            };

            var variantField = new DropdownField("Variant") { style = { flexGrow = 1f } };
            variantField.AddToClassList(AlignedFieldClass);

            var applyButton = new Button
            {
                text = "Apply",
                tooltip = $"선택한 프리셋을 현재 {type} 채널 값으로 구워 넣습니다.",
                style = { width = 64f, marginLeft = 6f }
            };

            variantRow.Add(variantField);
            variantRow.Add(applyButton);
            block.Add(variantRow);

            UITransitionPresetCategory CurrentCategory() =>
                Enum.TryParse(GetEnumName(categoryProp), out UITransitionPresetCategory parsed)
                    ? parsed
                    : UITransitionPresetCategory.None;

            void RebuildVariants()
            {
                UITransitionPresetCategory category = CurrentCategory();
                int count = UITransitionPresetLibrary.GetVariantCount(category);

                var names = new List<string>(count);
                for (int i = 1; i <= count; i++)
                    names.Add(UITransitionPresetLibrary.GetVariantName(category, i));

                variantField.choices = names;
                bool hasVariants = count > 0;
                variantRow.style.display = hasVariants ? DisplayStyle.Flex : DisplayStyle.None;
                applyButton.SetEnabled(category != UITransitionPresetCategory.None);

                if (!hasVariants)
                    return;

                int variant = Mathf.Clamp(variantProp.intValue, 1, count);
                if (variant != variantProp.intValue)
                {
                    variantProp.intValue = variant;
                    variantProp.serializedObject.ApplyModifiedProperties();
                }

                variantField.SetValueWithoutNotify(names[variant - 1]);
            }

            variantField.RegisterValueChangedCallback(evt =>
            {
                int index = variantField.choices.IndexOf(evt.newValue);
                if (index < 0)
                    return;

                variantProp.intValue = index + 1;
                variantProp.serializedObject.ApplyModifiedProperties();
            });

            bool resetPending = false;

            void ResetCategoryIfPending()
            {
                if (!resetPending)
                    return;

                resetPending = false;

                if (categoryProp.serializedObject == null || categoryProp.serializedObject.targetObject == null)
                    return;

                categoryProp.intValue = (int)UITransitionPresetCategory.None;
                categoryProp.serializedObject.ApplyModifiedProperties();
                RebuildVariants();
            }

            applyButton.clicked += () =>
            {
                ApplyPreset(dir, categoryProp, variantProp, type);
                resetPending = true;
                RebuildVariants();
            };

            block.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (evt.relatedTarget is VisualElement next && (next == block || block.Contains(next)))
                    return;

                ResetCategoryIfPending();
            });

            block.RegisterCallback<DetachFromPanelEvent>(_ => ResetCategoryIfPending());

            block.TrackPropertyValue(categoryProp, _ => RebuildVariants());
            block.TrackPropertyValue(variantProp, _ => RebuildVariants());
            RebuildVariants();
            return block;
        }

        #endregion

        #region Direction panel (one per Show/Hide)

        private static VisualElement CreateDirectionPanel(SerializedProperty dir, UIAnimationType type)
        {
            var panel = new VisualElement();

            SerializedProperty categoryProp = dir.FindPropertyRelative("PresetCategory");
            SerializedProperty variantProp = dir.FindPropertyRelative("PresetVariant");
            if (categoryProp != null && variantProp != null)
                panel.Add(CreatePresetBlock(dir, categoryProp, variantProp, type));

            var subRow = new VisualElement();
            subRow.style.flexDirection = FlexDirection.Row;
            subRow.style.marginBottom = 6f;

            var moveTab = CreateSubTab("Move", MoveColor);
            var rotateTab = CreateSubTab("Rotate", RotateColor);
            var scaleTab = CreateSubTab("Scale", ScaleColor);
            var fadeTab = CreateSubTab("Fade", FadeColor);
            moveTab.style.marginRight = 4f;
            rotateTab.style.marginRight = 4f;
            scaleTab.style.marginRight = 4f;
            subRow.Add(moveTab);
            subRow.Add(rotateTab);
            subRow.Add(scaleTab);
            subRow.Add(fadeTab);
            panel.Add(subRow);

            VisualElement moveCard = CreateMoveCard(dir, type);
            VisualElement rotateCard = CreateRotateCard(dir);
            VisualElement scaleCard = CreateScaleCard(dir);
            VisualElement fadeCard = CreateFadeCard(dir);
            panel.Add(moveCard);
            panel.Add(rotateCard);
            panel.Add(scaleCard);
            panel.Add(fadeCard);

            var tabs = new[] { moveTab, rotateTab, scaleTab, fadeTab };
            var cards = new[] { moveCard, rotateCard, scaleCard, fadeCard };
            var colors = new[] { MoveColor, RotateColor, ScaleColor, FadeColor };

            void Select(int idx)
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    bool on = i == idx;
                    tabs[i].EnableInClassList("nk-transition-tab--active", on);
                    cards[i].style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                    Label l = tabs[i].Q<Label>();
                    if (l != null) l.EnableInClassList("nk-transition-title--active", on);
                }
            }

            for (int i = 0; i < tabs.Length; i++)
            {
                int captured = i;
                tabs[i].clicked += () => Select(captured);
            }
            Select(0);

            return panel;
        }

        private static Button CreateSubTab(string name, Color dotColor)
        {
            var b = new Button();
            b.style.flexGrow = 1f;
            b.style.height = 24f;
            b.style.flexDirection = FlexDirection.Row;
            b.style.alignItems = Align.Center;
            b.style.justifyContent = Justify.Center;
            Round(b, 4f);

            var dot = new VisualElement { style = { width = 6f, height = 6f, marginRight = 5f, backgroundColor = dotColor } };
            Round(dot, 3f);
            b.Add(dot);
            var label = new Label(name) { style = { fontSize = 12f } };
            label.AddToClassList("nk-transition-title");
            b.AddToClassList("nk-transition-tab");
            b.Add(label);
            return b;
        }

        #endregion

        #region Channel cards

        private static VisualElement CreateMoveCard(SerializedProperty dir, UIAnimationType type)
        {
            SerializedProperty channel = dir.FindPropertyRelative("Move");
            return BuildCard(channel, "Move", MoveColor, body =>
            {
                string fromControl;
                string toControl;
                string fromCustomName;
                string toCustomName;
                VisualElement from;
                VisualElement to;

                if (type == UIAnimationType.Show)
                {
                    fromControl = "FromDirection";
                    toControl = "ToType";
                    fromCustomName = nameof(UIMoveDirection.CustomPosition);
                    toCustomName = nameof(UIReferenceValue.CustomValue);
                    from = MoveDirectionCard(channel, "Move from", fromControl);
                    to = MoveReferenceCard(channel, "Move to", toControl);
                }
                else
                {
                    fromControl = "FromType";
                    toControl = "ToDirection";
                    fromCustomName = nameof(UIReferenceValue.CustomValue);
                    toCustomName = nameof(UIMoveDirection.CustomPosition);
                    from = MoveReferenceCard(channel, "Move from", fromControl);
                    to = MoveDirectionCard(channel, "Move to", toControl);
                }

                body.Add(TwoColumnRow(from, to, 8f));

                VisualElement fromValue = MoveValueCard(
                    channel,
                    "FromCustom",
                    "FromOffset",
                    fromControl,
                    fromCustomName,
                    "From Custom Position",
                    "From Offset");
                VisualElement toValue = MoveValueCard(
                    channel,
                    "ToCustom",
                    "ToOffset",
                    toControl,
                    toCustomName,
                    "To Custom Position",
                    "To Offset");
                VisualElement valueRow = TwoColumnRow(fromValue, toValue, 8f);
                valueRow.style.marginTop = 8f;
                body.Add(valueRow);

                AddTiming(body, channel);
            });
        }

        private static VisualElement MoveDirectionCard(
            SerializedProperty channel,
            string title,
            string directionRelative)
        {
            VisualElement card = CreateFieldCard(title);
            SerializedProperty direction = channel?.FindPropertyRelative(directionRelative);
            var selectorRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart, minHeight = 58f }
            };
            selectorRow.Add(BuildDirectionGrid(direction));
            var directionField = new PropertyField(direction, string.Empty)
            {
                style = { flexGrow = 1f, marginLeft = 8f }
            };
            selectorRow.Add(directionField);
            card.Add(selectorRow);
            return card;
        }

        private static VisualElement MoveReferenceCard(
            SerializedProperty channel,
            string title,
            string typeRelative)
        {
            VisualElement card = CreateFieldCard(title);
            SerializedProperty type = channel?.FindPropertyRelative(typeRelative);
            var selectorArea = new VisualElement { style = { minHeight = 58f } };
            selectorArea.Add(new PropertyField(type, string.Empty));
            card.Add(selectorArea);
            return card;
        }

        private static VisualElement MoveValueCard(
            SerializedProperty channel,
            string customRelative,
            string offsetRelative,
            string controlRelative,
            string customName,
            string customTitle,
            string offsetTitle)
        {
            VisualElement card = CreateFieldCard(offsetTitle);
            Label title = card.Q<Label>("field-card-title");
            Vector3Field customField = BoundVector3Field(
                channel?.FindPropertyRelative(customRelative));
            Vector3Field offsetField = BoundVector3Field(
                channel?.FindPropertyRelative(offsetRelative));
            SerializedProperty control = channel?.FindPropertyRelative(controlRelative);
            card.Add(customField);
            card.Add(offsetField);

            void Refresh()
            {
                bool custom = GetEnumName(control) == customName;
                if (title != null)
                    title.text = custom ? customTitle : offsetTitle;
                customField.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
                offsetField.style.display = custom ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (control != null)
                card.TrackPropertyValue(control, _ => Refresh());
            Refresh();
            return card;
        }
        private static Vector3Field BoundVector3Field(SerializedProperty property)
        {
            var field = new Vector3Field(string.Empty);
            if (property != null)
                field.BindProperty(property);
            field.labelElement.style.display = DisplayStyle.None;
            field.style.flexGrow = 1f;
            return field;
        }

        private static VisualElement TwoColumnRow(VisualElement left, VisualElement right, float gap)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            left.style.flexGrow = 1f;
            left.style.flexBasis = 0f;
            left.style.marginRight = gap;
            right.style.flexGrow = 1f;
            right.style.flexBasis = 0f;
            row.Add(left);
            row.Add(right);
            return row;
        }

        private static VisualElement ThreeColumnRow(
            VisualElement left,
            VisualElement middle,
            VisualElement right,
            float gap)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            VisualElement[] cards = { left, middle, right };
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].style.flexGrow = 1f;
                cards[i].style.flexBasis = 0f;
                if (i < cards.Length - 1)
                    cards[i].style.marginRight = gap;
                row.Add(cards[i]);
            }
            return row;
        }

        private static VisualElement CreateFieldCard(string title)
        {
            var card = new VisualElement
            {
                style =
                {
                    paddingTop = 7f,
                    paddingBottom = 7f,
                    paddingLeft = 8f,
                    paddingRight = 8f,
                    borderTopWidth = 1f,
                    borderBottomWidth = 1f,
                    borderLeftWidth = 1f,
                    borderRightWidth = 1f,
                }
            };
            card.AddToClassList("nk-transition-field-card");
            Round(card, 6f);
            card.Add(new Label(title)
            {
                name = "field-card-title",
                style =
                {
                    fontSize = 11f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 5f
                }
            });
            card.Q<Label>("field-card-title")?.AddToClassList("nk-transition-title");
            return card;
        }

        private static string GetEnumName(SerializedProperty property)
        {
            return property != null
                   && property.enumValueIndex >= 0
                   && property.enumValueIndex < property.enumNames.Length
                ? property.enumNames[property.enumValueIndex]
                : string.Empty;
        }

        /// <summary>
        /// Applies p re se t.
        /// </summary>
        private static void ApplyPreset(
            SerializedProperty direction,
            SerializedProperty categoryProperty,
            SerializedProperty variantProperty,
            UIAnimationType type)
        {
            if (direction == null || categoryProperty == null || variantProperty == null)
                return;

            if (!Enum.TryParse(GetEnumName(categoryProperty), out UITransitionPresetCategory category))
                return;

            SerializedObject serializedObject = direction.serializedObject;
            Undo.RecordObjects(
                serializedObject.targetObjects,
                $"Apply {type} Transition Preset");
            serializedObject.Update();

            if (category == UITransitionPresetCategory.None)
            {
                SetAllChannelEnabled(direction, false);
            }
            else
            {
                UIAnimation animation = UITransitionFactory.BuildPreset(
                    category,
                    variantProperty.intValue,
                    type);
                if (animation == null)
                    return;

                CopyMove(direction.FindPropertyRelative("Move"), animation.Move);
                CopyFade(direction.FindPropertyRelative("Fade"), animation.Fade);
                CopyScale(direction.FindPropertyRelative("Scale"), animation.Scale);
                CopyRotate(direction.FindPropertyRelative("Rotate"), animation.Rotate);
            }

            serializedObject.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in serializedObject.targetObjects)
                EditorUtility.SetDirty(target);
        }
        
        private static void SetAllChannelEnabled(SerializedProperty direction, bool enabled)
        {
            SetChannelEnabled(direction.FindPropertyRelative("Move"), enabled);
            SetChannelEnabled(direction.FindPropertyRelative("Fade"), enabled);
            SetChannelEnabled(direction.FindPropertyRelative("Scale"), enabled);
            SetChannelEnabled(direction.FindPropertyRelative("Rotate"), enabled);
        }

        private static void SetChannelEnabled(SerializedProperty channel, bool enabled)
        {
            SerializedProperty enable = channel?.FindPropertyRelative("Enable");
            if (enable != null)
                enable.boolValue = enabled;
        }

        private static bool BeginCopyChannel(
            SerializedProperty target,
            UIAnimationChannel source)
        {
            if (target == null || source == null)
                return false;

            SetChannelEnabled(target, source.Enabled);
            return source.Enabled;
        }

        private static void CopyTiming(
            SerializedProperty target,
            UIAnimationChannel source)
        {
            target.FindPropertyRelative("Duration").floatValue = source.Duration;
            target.FindPropertyRelative("Delay").floatValue = source.StartDelay;
            target.FindPropertyRelative("Ease").intValue = (int)source.Ease;
            target.FindPropertyRelative("PlayMode").intValue = (int)source.PlayMode;
            target.FindPropertyRelative("Loops").intValue = source.Loops;
        }

        private static void CopyMove(SerializedProperty target, UIMoveAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromDirection").intValue = (int)source.FromDirection;
            target.FindPropertyRelative("ToDirection").intValue = (int)source.ToDirection;
            target.FindPropertyRelative("FromCustom").vector3Value = source.FromCustom;
            target.FindPropertyRelative("ToCustom").vector3Value = source.ToCustom;
            target.FindPropertyRelative("FromOffset").vector3Value = source.FromOffset;
            target.FindPropertyRelative("ToOffset").vector3Value = source.ToOffset;
            CopyTiming(target, source);
        }

        private static void CopyFade(SerializedProperty target, UIFadeAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromCustom").floatValue = source.FromCustom;
            target.FindPropertyRelative("ToCustom").floatValue = source.ToCustom;
            target.FindPropertyRelative("FromOffset").floatValue = source.FromOffset;
            target.FindPropertyRelative("ToOffset").floatValue = source.ToOffset;
            CopyTiming(target, source);
        }

        private static void CopyScale(SerializedProperty target, UIScaleAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromCustom").vector2Value = source.FromCustom;
            target.FindPropertyRelative("ToCustom").vector2Value = source.ToCustom;
            target.FindPropertyRelative("FromOffset").vector2Value = source.FromOffset;
            target.FindPropertyRelative("ToOffset").vector2Value = source.ToOffset;
            CopyTiming(target, source);
        }

        private static void CopyRotate(SerializedProperty target, UIRotateAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromCustom").floatValue = source.FromCustom;
            target.FindPropertyRelative("ToCustom").floatValue = source.ToCustom;
            target.FindPropertyRelative("FromOffset").floatValue = source.FromOffset;
            target.FindPropertyRelative("ToOffset").floatValue = source.ToOffset;
            CopyTiming(target, source);
        }

        private static VisualElement CreateFadeCard(SerializedProperty dir)
        {
            return CreateReferenceChannelCard(
                dir.FindPropertyRelative("Fade"),
                "Fade",
                FadeColor,
                "Custom fade",
                "Offset");
        }

        private static VisualElement CreateScaleCard(SerializedProperty dir)
        {
            return CreateReferenceChannelCard(
                dir.FindPropertyRelative("Scale"),
                "Scale",
                ScaleColor,
                "Custom scale",
                "Offset");
        }

        private static VisualElement CreateRotateCard(SerializedProperty dir)
        {
            return CreateReferenceChannelCard(
                dir.FindPropertyRelative("Rotate"),
                "Rotate",
                RotateColor,
                "Custom angle (deg)",
                "Angle offset (deg)");
        }

        private static VisualElement CreateReferenceChannelCard(
            SerializedProperty channel,
            string channelName,
            Color color,
            string customLabel,
            string offsetLabel)
        {
            return BuildCard(channel, channelName, color, body =>
            {
                VisualElement fromSelector = ReferenceSelectorCard(
                    channel, $"{channelName} from", "FromType");
                VisualElement toSelector = ReferenceSelectorCard(
                    channel, $"{channelName} to", "ToType");
                body.Add(TwoColumnRow(fromSelector, toSelector, 8f));

                VisualElement fromValue = ReferenceValueCard(
                    channel,
                    "FromType",
                    "FromCustom",
                    "FromOffset",
                    $"From {customLabel}",
                    $"From {offsetLabel}");
                VisualElement toValue = ReferenceValueCard(
                    channel,
                    "ToType",
                    "ToCustom",
                    "ToOffset",
                    $"To {customLabel}",
                    $"To {offsetLabel}");
                VisualElement valueRow = TwoColumnRow(fromValue, toValue, 8f);
                valueRow.style.marginTop = 8f;
                body.Add(valueRow);

                AddTiming(body, channel);
            });
        }

        private static VisualElement ReferenceSelectorCard(
            SerializedProperty channel,
            string title,
            string typeRelative)
        {
            VisualElement card = CreateFieldCard(title);
            SerializedProperty type = channel?.FindPropertyRelative(typeRelative);
            if (type != null)
                card.Add(new PropertyField(type, string.Empty));
            return card;
        }

        private static VisualElement ReferenceValueCard(
            SerializedProperty channel,
            string typeRelative,
            string customRelative,
            string offsetRelative,
            string customTitle,
            string offsetTitle)
        {
            VisualElement card = CreateFieldCard(string.Empty);
            Label title = card.Q<Label>("field-card-title");
            SerializedProperty type = channel?.FindPropertyRelative(typeRelative);
            var customField = new PropertyField(
                channel?.FindPropertyRelative(customRelative), string.Empty);
            var offsetField = new PropertyField(
                channel?.FindPropertyRelative(offsetRelative), string.Empty);
            customField.style.flexGrow = 1f;
            offsetField.style.flexGrow = 1f;
            card.Add(customField);
            card.Add(offsetField);

            void Refresh()
            {
                bool custom = GetEnumName(type) == nameof(UIReferenceValue.CustomValue);
                if (title != null)
                    title.text = custom ? customTitle : offsetTitle;
                customField.style.display = custom ? DisplayStyle.Flex : DisplayStyle.None;
                offsetField.style.display = custom ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (type != null)
                card.TrackPropertyValue(type, _ => Refresh());
            Refresh();
            return card;
        }

        private static VisualElement BuildCard(
            SerializedProperty channel,
            string name,
            Color color,
            Action<VisualElement> addBody)
        {
            var card = new VisualElement
            {
                style =
                {
                    paddingTop = 10f,
                    paddingBottom = 10f,
                    paddingLeft = 11f,
                    paddingRight = 11f
                }
            };
            card.AddToClassList("nk-transition-channel-card");
            Round(card, 8f);

            if (channel == null)
            {
                card.Add(new Label($"'{name}' property is missing") { style = { color = HideAccent } });
                return card;
            }

            SerializedProperty enabled = channel.FindPropertyRelative("Enable");
            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 10f }
            };
            var dot = new VisualElement
            {
                style = { width = 9f, height = 9f, marginRight = 8f, backgroundColor = color }
            };
            Round(dot, 4.5f);
            header.Add(dot);
            var headerLabel = new Label($"{name} enabled")
            {
                style =
                {
                    fontSize = 12f,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1f
                }
            };
            headerLabel.AddToClassList("nk-transition-title");
            header.Add(headerLabel);

            var toggle = new Toggle { value = enabled != null && enabled.boolValue };
            header.Add(toggle);
            card.Add(header);

            var body = new VisualElement();
            addBody(body);
            card.Add(body);

            void Refresh() => body.SetEnabled(enabled != null && enabled.boolValue);
            if (enabled != null)
            {
                toggle.RegisterValueChangedCallback(evt =>
                {
                    enabled.boolValue = evt.newValue;
                    enabled.serializedObject.ApplyModifiedProperties();
                    Refresh();
                });
                toggle.TrackPropertyValue(enabled, _ =>
                {
                    toggle.SetValueWithoutNotify(enabled.boolValue);
                    Refresh();
                });
            }
            Refresh();
            return card;
        }

        /// <summary>
        /// Four custom corners, four outside bars, and the Doozy-compatible inner 3x3 positions.
        /// </summary>
        private const string DirectionCellOnClass = "nk-transition-direction-cell--on";

        private static VisualElement BuildDirectionGrid(SerializedProperty directionProperty)
        {
            const float cell = 8f;
            const float gap = 2f;
            const float bar = cell * 3f + gap * 2f;

            var widget = new VisualElement { style = { flexDirection = FlexDirection.Column, flexShrink = 0f } };
            var cells = new VisualElement[17];
            var directions = new UIMoveDirection[17];
            int count = 0;

            string lastValue = null;

            VisualElement NewCell(UIMoveDirection direction, float width, float height)
            {
                var element = new VisualElement
                {
                    tooltip = direction.ToString(),
                    style =
                    {
                        width = width,
                        height = height,
                        marginRight = gap,
                        marginBottom = gap,
                    }
                };
                element.AddToClassList("nk-transition-direction-cell");
                Round(element, 2f);
                element.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (directionProperty == null)
                        return;
                    int index = Array.IndexOf(directionProperty.enumNames, direction.ToString());
                    if (index < 0)
                        return;
                    directionProperty.enumValueIndex = index;
                    directionProperty.serializedObject.ApplyModifiedProperties();
                    RefreshCells();
                    evt.StopPropagation();
                });
                cells[count] = element;
                directions[count] = direction;
                count++;
                return element;
            }

            VisualElement Row() => new VisualElement { style = { flexDirection = FlexDirection.Row } };

            VisualElement top = Row();
            top.Add(NewCell(UIMoveDirection.CustomPosition, cell, cell));
            top.Add(NewCell(UIMoveDirection.Top, bar, cell));
            top.Add(NewCell(UIMoveDirection.CustomPosition, cell, cell));
            widget.Add(top);

            VisualElement middle = Row();
            middle.Add(NewCell(UIMoveDirection.Left, cell, bar));
            var grid = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            UIMoveDirection[,] map =
            {
                { UIMoveDirection.TopLeft, UIMoveDirection.TopCenter, UIMoveDirection.TopRight },
                { UIMoveDirection.MiddleLeft, UIMoveDirection.MiddleCenter, UIMoveDirection.MiddleRight },
                { UIMoveDirection.BottomLeft, UIMoveDirection.BottomCenter, UIMoveDirection.BottomRight }
            };
            for (int y = 0; y < 3; y++)
            {
                VisualElement gridRow = Row();
                for (int x = 0; x < 3; x++)
                    gridRow.Add(NewCell(map[y, x], cell, cell));
                grid.Add(gridRow);
            }
            middle.Add(grid);
            middle.Add(NewCell(UIMoveDirection.Right, cell, bar));
            widget.Add(middle);

            VisualElement bottom = Row();
            bottom.Add(NewCell(UIMoveDirection.CustomPosition, cell, cell));
            bottom.Add(NewCell(UIMoveDirection.Bottom, bar, cell));
            bottom.Add(NewCell(UIMoveDirection.CustomPosition, cell, cell));
            widget.Add(bottom);

            void RefreshCells()
            {
                string current = GetEnumName(directionProperty);
                if (current == lastValue)
                    return;

                lastValue = current;

                for (int i = 0; i < count; i++)
                    cells[i].EnableInClassList(DirectionCellOnClass, directions[i].ToString() == current);
            }

            if (directionProperty != null)
            {
                widget.schedule.Execute(() =>
                {
                    try
                    {
                        RefreshCells();
                    }
                    catch (Exception)
                    {
                    }
                }).Every(100);
            }

            RefreshCells();
            return widget;
        }

        private static void AddTiming(VisualElement body, SerializedProperty channel)
        {
            VisualElement delay = PropertyCard(channel, "Delay", "Start Delay (s)");
            VisualElement duration = PropertyCard(channel, "Duration", "Duration (s)");
            VisualElement loops = PropertyCard(channel, "Loops", "Loops");
            VisualElement timingRow = ThreeColumnRow(delay, duration, loops, 8f);
            timingRow.style.marginTop = 8f;
            body.Add(timingRow);

            VisualElement playMode = PropertyCard(channel, "PlayMode", "Play Mode");
            VisualElement ease = PropertyCard(channel, "Ease", "Ease");
            VisualElement playbackRow = TwoColumnRow(playMode, ease, 8f);
            playbackRow.style.marginTop = 8f;
            body.Add(playbackRow);

            SerializedProperty loopsProperty = channel?.FindPropertyRelative("Loops");
            SerializedProperty playModeProperty = channel?.FindPropertyRelative("PlayMode");
            void RefreshLoopMode()
            {
                if (loopsProperty == null || playModeProperty == null)
                    return;

                if (loopsProperty.intValue < -1)
                    loopsProperty.intValue = -1;

                bool infinite = loopsProperty.intValue == -1;
                if (infinite
                    && GetEnumName(playModeProperty) != nameof(UIAnimationPlayMode.PingPong))
                {
                    playModeProperty.intValue = (int)UIAnimationPlayMode.PingPong;
                    playModeProperty.serializedObject.ApplyModifiedProperties();
                }

                playMode.SetEnabled(!infinite);
                playMode.tooltip = infinite
                    ? "Loops = -1 forces Ping Pong and repeats forever."
                    : string.Empty;
            }

            if (loopsProperty != null)
                body.TrackPropertyValue(loopsProperty, _ => RefreshLoopMode());
            RefreshLoopMode();
        }

        private static VisualElement PropertyCard(
            SerializedProperty channel,
            string relative,
            string title)
        {
            VisualElement card = CreateFieldCard(title);
            SerializedProperty property = channel?.FindPropertyRelative(relative);
            if (property != null)
                card.Add(new PropertyField(property, string.Empty));
            return card;
        }
        
        private static void RunPreview(UIAnimationType type, Label hint)
        {
            if (!TryGetInspectedNavElement(out NavElement element))
            {
                hint.text = "Select a NavElement on the canvas";
                return;
            }

            CancelPreview();
            hint.text = string.Empty;

            PreviewTargets.Clear();
            PreviewTargets.Add(element);
            NavElement.CollectFollowerElements(element, PreviewTargets);

            var dispatcher = new ManualMotionDispatcher();
            float duration = 0f;
            bool infinite = false;
            bool anyAnimation = false;

            for (int i = 0; i < PreviewTargets.Count; i++)
            {
                NavElement target = PreviewTargets[i];
                target.Visibility.Scheduler = dispatcher.Scheduler;

                UITransitionSet set = target.Transitions;
                UIAnimation animation = set == null
                    ? null
                    : type == UIAnimationType.Show ? set.BuildShow() : set.BuildHide();

                if (type == UIAnimationType.Show)
                    target.Visibility.ShowAnimation = animation;
                else
                    target.Visibility.HideAnimation = animation;

                if (animation == null)
                    continue;

                anyAnimation = true;
                duration = Mathf.Max(duration, animation.TotalDuration);
                infinite |= animation.IsInfinite;
            }

            if (!anyAnimation)
            {
                PreviewTargets.Clear();
                element.InstantShow();
                return;
            }

            _previewDispatcher = dispatcher;
            _previewElement = element;
            _previewType = type;

            if (type == UIAnimationType.Show)
            {
                element.Visibility.InstantHide();
                element.Visibility.Show();
            }
            else
            {
                element.InstantShow();

                UIViewVisibility visibility = element.Visibility;
                _previewHideRestore = () =>
                {
                    visibility.HideFinished -= _previewHideRestore;
                    _previewHideRestore = null;
                    RestoreHidePose();
                };
                visibility.HideFinished += _previewHideRestore;
                visibility.Hide();
            }

            PumpPreview(duration, infinite);
        }

        /// <summary>
        /// Performs the restore hide pose operation.
        /// </summary>
        private static void RestoreHidePose()
        {
            for (int i = 0; i < PreviewTargets.Count; i++)
            {
                NavElement target = PreviewTargets[i];
                if (target?.panel == null)
                    continue;

                UIViewVisibility visibility = target.Visibility;
                UIAnimation animation = visibility.HideAnimation;
                visibility.Gate.style.display = DisplayStyle.Flex;

                if (animation == null)
                    continue;

                animation.Prepare(visibility.Gate);
                animation.ApplyAt(visibility.Gate, animation.TotalDuration);
            }
        }

        /// <summary>
        /// Performs the pump preview operation.
        /// </summary>
        private static void PumpPreview(float duration, bool infinite)
        {
            VisualElement pumpTarget = _previewElement;
            if (pumpTarget?.panel == null)
                return;

            pumpTarget.MarkDirtyRepaint();

            float elapsed = 0f;
            double previousTime = EditorApplication.timeSinceStartup;
            ManualMotionDispatcher dispatcher = _previewDispatcher;
            IVisualElementScheduledItem item = null;
            item = pumpTarget.schedule.Execute(() =>
            {
                if (dispatcher != _previewDispatcher)
                {
                    item.Pause();
                    return;
                }

                double now = EditorApplication.timeSinceStartup;
                float delta = Mathf.Clamp((float)(now - previousTime), 0f, 0.05f);
                previousTime = now;

                elapsed += delta;
                dispatcher.Update(delta);

                if (!infinite && elapsed >= Mathf.Max(0f, duration))
                    FinishPreviewPump(dispatcher);
            }).Every(0);

            _previewPump = item;
        }

        private static void FinishPreviewPump(ManualMotionDispatcher dispatcher)
        {
            _previewPump?.Pause();
            _previewPump = null;
            dispatcher?.Reset();
            if (!ReferenceEquals(dispatcher, _previewDispatcher))
                return;

            if (_previewElement != null)
            {
                if (_previewType == UIAnimationType.Show)
                    _previewElement.Visibility.InstantShow();
            }
            _previewDispatcher = null;
        }

        private static void UnsubscribeHideRestore()
        {
            if (_previewHideRestore == null)
                return;

            if (_previewElement != null)
                _previewElement.Visibility.HideFinished -= _previewHideRestore;
            _previewHideRestore = null;
        }

        private static void CancelPreview()
        {
            _previewPump?.Pause();
            _previewPump = null;
            _previewDispatcher?.Reset();
            _previewDispatcher = null;
            UnsubscribeHideRestore();

            if (_previewElement != null)
            {
                if (_previewElement.StartsHidden)
                    _previewElement.Visibility.InstantHide();
                else
                    _previewElement.Visibility.InstantShow();
            }

            PreviewTargets.Clear();
            _previewElement = null;
        }

        internal static bool TryGetInspectedNavElement(out NavElement element)
        {
            if (TryGetBuilderSelectedElement(out element))
                return true;
#if UNITY_6000_6_OR_NEWER
            if (TryGetViewportSelectedElement(out element))
                return true;
#endif
            return false;
        }

        private static bool TryGetBuilderSelectedElement(out NavElement element)
        {
            element = null;
            try
            {
                EnsureEditorTypesResolved();
                if (_builderType == null || _builderSelectionProperty == null) return false;

                UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(_builderType);
                if (windows == null || windows.Length == 0) return false;

                foreach (UnityEngine.Object window in windows)
                {
                    object selection = _builderSelectionProperty.GetValue(window);
                    if (selection == null) continue;

                    var listProp = selection.GetType().GetProperty("selection", InstanceMembers);
                    if (listProp?.GetValue(selection) is not System.Collections.IEnumerable selected) continue;

                    foreach (object item in selected)
                    {
                        if (item is not VisualElement selectedElement) continue;
                        NavElement found = selectedElement as NavElement
                                           ?? selectedElement.GetFirstAncestorOfType<NavElement>();
                        if (found != null) { element = found; return true; }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

#if UNITY_6000_6_OR_NEWER
        private static bool TryGetViewportSelectedElement(out NavElement element)
        {
            element = null;
            try
            {
                EnsureEditorTypesResolved();
                if (_viewportWindowType == null
                    || _manipulatorOverlayType == null
                    || _manipulatorTargetProperty == null)
                    return false;

                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(_viewportWindowType))
                {
                    if (obj is not EditorWindow window) continue;
                    VisualElement root = window.rootVisualElement;
                    if (root == null) continue;

                    foreach (VisualElement overlay in root.Query<VisualElement>().ToList())
                    {
                        if (!_manipulatorOverlayType.IsInstanceOfType(overlay)) continue;
                        if (_manipulatorTargetProperty.GetValue(overlay) is not VisualElement target) continue;

                        NavElement found = target as NavElement ?? target.GetFirstAncestorOfType<NavElement>();
                        if (found != null) { element = found; return true; }
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
#endif

        #region Editor Type Cache

        private const BindingFlags InstanceMembers =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private const string BuilderTypeName = "Unity.UI.Builder.Builder";
        private const string ViewportWindowTypeName = "Unity.UIToolkit.Editor.UIViewportWindow";
        private const string ManipulatorOverlayTypeName = "Unity.UIToolkit.Editor.VisualElementManipulatorOverlay";

        private static bool _editorTypesResolved;
        private static Type _builderType;
        private static PropertyInfo _builderSelectionProperty;
        private static Type _viewportWindowType;
        private static Type _manipulatorOverlayType;
        private static PropertyInfo _manipulatorTargetProperty;

        /// <summary>
        /// Performs the ensure editor types resolved operation.
        /// </summary>
        private static void EnsureEditorTypesResolved()
        {
            if (_editorTypesResolved)
                return;

            _editorTypesResolved = true;

            foreach (Type type in TypeCache.GetTypesDerivedFrom<EditorWindow>())
            {
                if (_builderType == null && type.FullName == BuilderTypeName)
                    _builderType = type;
                else if (_viewportWindowType == null && type.FullName == ViewportWindowTypeName)
                    _viewportWindowType = type;
            }

            foreach (Type type in TypeCache.GetTypesDerivedFrom<VisualElement>())
            {
                if (type.FullName == ManipulatorOverlayTypeName)
                {
                    _manipulatorOverlayType = type;
                    break;
                }
            }

            _builderSelectionProperty = _builderType?.GetProperty("selection", InstanceMembers);
            _manipulatorTargetProperty = _manipulatorOverlayType?.GetProperty("Target", InstanceMembers);
        }

        #endregion

        #endregion

        private static Button CreateIconButton(string iconPath, float iconSize)
        {
            var button = new Button
            {
                style =
                {
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    paddingLeft = 0f,
                    paddingRight = 0f,
                    paddingTop = 0f,
                    paddingBottom = 0f
                }
            };

            var icon = new VisualElement
            {
                style =
                {
                    width = iconSize,
                    height = iconSize,
                    flexShrink = 0f,
                    unityBackgroundImageTintColor = EditorGUIUtility.isProSkin
                        ? new Color(0.83f, 0.83f, 0.83f)
                        : new Color(0.25f, 0.25f, 0.25f)
                }
            };

            VectorImage vector = AssetDatabase.LoadAssetAtPath<VectorImage>(iconPath);
            if (vector != null)
                icon.style.backgroundImage = new StyleBackground(vector);

            button.Add(icon);
            return button;
        }

        internal static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }
    }
}
