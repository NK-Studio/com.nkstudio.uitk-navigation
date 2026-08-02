using System;
using NKStudio.UITKNavigation.Editor.Animation;
using NKStudio.UITKNavigation.Elements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Elements
{
    /// <summary>
    /// Provides UI View Startup Property Drawer functionality.
    /// </summary>
    [CustomPropertyDrawer(typeof(UIViewStartupInspectorAttribute))]
    internal sealed class UIViewStartupPropertyDrawer : PropertyDrawer
    {
        private static readonly Color StartAccent = new(0.216f, 0.541f, 0.867f);
        private static readonly Color HintColor = new(0.62f, 0.36f, 0.38f);

        private const string AlignedFieldClass = "unity-base-field__aligned";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            StyleSheet theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                UITransitionPropertyDrawer.ThemeStyleSheetPath);
            if (theme != null)
                root.styleSheets.Add(theme);
            root.AddToClassList("nk-transition-root");
            root.style.marginTop = 2f;
            root.style.marginBottom = 4f;

            SerializedProperty onStart = property.FindPropertyRelative("OnStart");
            SerializedProperty hideMode = property.FindPropertyRelative("HideMode");
            SerializedProperty useCustom = property.FindPropertyRelative("UseCustomStartPosition");
            SerializedProperty customPosition = property.FindPropertyRelative("CustomStartPosition");
            SerializedProperty autoHide = property.FindPropertyRelative("AutoHideAfterShow");
            SerializedProperty autoHideDelay = property.FindPropertyRelative("AutoHideDelay");

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
            UITransitionPropertyDrawer.Round(card, 8f);

            card.Add(SimpleField(
                onStart,
                "On Start",
                "패널에 붙는 시점의 동작입니다.\n"
                + "Disabled: 화면을 건드리지 않고 현재 display만 동기화\n"
                + "Instant Hide / Instant Show: 애니메이션 없이 즉시 전환\n"
                + "Animation Hide: 보인 상태에서 시작해 Hide 재생\n"
                + "Animation Show: 숨긴 상태에서 시작해 Show 재생"));

            VisualElement hideModeField = SimpleField(
                hideMode,
                "When Hidden",
                "숨김 상태에서 감추는 방식입니다.\n"
                + "Display: display: none — 레이아웃에서 완전히 빠짐\n"
                + "Visibility: visibility: hidden — 레이아웃 자리를 유지");
            hideModeField.style.marginBottom = 10f;
            card.Add(hideModeField);

            VisualElement startPositionBlock = CreateStartPositionBlock(useCustom, customPosition);
            startPositionBlock.style.marginBottom = 12f;
            card.Add(startPositionBlock);

            card.Add(CreateAutoHideBlock(autoHide, autoHideDelay));
            root.Add(card);

            return root;
        }

        private static VisualElement SimpleField(SerializedProperty property, string label, string tooltip)
        {
            if (property == null)
                return new VisualElement();

            var field = new PropertyField(property, label) { tooltip = tooltip };
            field.AddToClassList(AlignedFieldClass);
            field.style.marginBottom = 2f;
            return field;
        }

        #region Custom Start Position

        private static VisualElement CreateStartPositionBlock(
            SerializedProperty useCustom,
            SerializedProperty customPosition)
        {
            var block = new VisualElement();

            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6f }
            };
            var dot = new VisualElement
            {
                style = { width = 7f, height = 7f, marginRight = 7f, backgroundColor = StartAccent }
            };
            UITransitionPropertyDrawer.Round(dot, 3.5f);
            header.Add(dot);

            var headerLabel = new Label("Custom Start Position")
            {
                tooltip = "켜져 있으면 시작 시 한 번 style.translate를 이 값으로 맞춥니다. Move 애니메이션의 Start Value 기준점이 됩니다.",
                style = { fontSize = 12f, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1f }
            };
            headerLabel.AddToClassList("nk-transition-title");
            header.Add(headerLabel);

            var toggle = new Toggle { value = useCustom == null || useCustom.boolValue };
            header.Add(toggle);
            block.Add(header);

            var body = new VisualElement();
            block.Add(body);

            var positionField = new Vector3Field(string.Empty);
            if (customPosition != null)
                positionField.BindProperty(customPosition);
            positionField.labelElement.style.display = DisplayStyle.None;
            body.Add(positionField);

            var hint = new Label(string.Empty)
            {
                style =
                {
                    fontSize = 10f,
                    color = HintColor,
                    alignSelf = Align.Center,
                    flexGrow = 1f,
                    marginRight = 6f,
                    whiteSpace = WhiteSpace.Normal
                }
            };

            var buttonRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 6f }
            };
            buttonRow.Add(hint);
            buttonRow.Add(ActionButton(
                "Get",
                "선택한 Element의 현재 translate 값을 읽어옵니다.",
                () => GetFromView(customPosition, hint)));
            buttonRow.Add(ActionButton(
                "Set",
                "지금 값을 선택한 Element의 translate에 적용합니다.",
                () => SetToView(customPosition, hint)));
            buttonRow.Add(ActionButton(
                "Reset",
                "값을 0, 0, 0으로 되돌리고 Element에도 적용합니다.",
                () => ResetValue(customPosition, hint)));
            body.Add(buttonRow);

            void Refresh() => body.SetEnabled(useCustom == null || useCustom.boolValue);

            if (useCustom != null)
            {
                toggle.RegisterValueChangedCallback(evt =>
                {
                    useCustom.boolValue = evt.newValue;
                    useCustom.serializedObject.ApplyModifiedProperties();
                    Refresh();
                });
                toggle.TrackPropertyValue(useCustom, _ =>
                {
                    toggle.SetValueWithoutNotify(useCustom.boolValue);
                    Refresh();
                });
            }

            Refresh();
            return block;
        }

        #endregion

        #region Auto Hide after Show

        /// <summary>
        /// Creates a ut oh id eb lo ck.
        /// </summary>
        private static VisualElement CreateAutoHideBlock(
            SerializedProperty autoHide,
            SerializedProperty autoHideDelay)
        {
            var block = new VisualElement();

            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6f }
            };
            var dot = new VisualElement
            {
                style = { width = 7f, height = 7f, marginRight = 7f, backgroundColor = StartAccent }
            };
            UITransitionPropertyDrawer.Round(dot, 3.5f);
            header.Add(dot);

            var headerLabel = new Label("Auto Hide after Show")
            {
                tooltip = "켜져 있으면 이 Element가 표시된(Instant Show / Animation Show) 뒤 지정한 시간이 지날 때 자동으로 Hide가 실행됩니다.",
                style = { fontSize = 12f, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1f }
            };
            headerLabel.AddToClassList("nk-transition-title");
            header.Add(headerLabel);

            var toggle = new Toggle { value = autoHide != null && autoHide.boolValue };
            header.Add(toggle);
            block.Add(header);

            var body = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };
            block.Add(body);

            var delayField = new FloatField("Auto Hide Delay")
            {
                tooltip = "표시가 끝난 시점부터 자동 Hide까지 기다리는 시간(초)입니다.",
                style = { flexGrow = 1f, marginRight = 4f }
            };
            delayField.AddToClassList(AlignedFieldClass);
            if (autoHideDelay != null)
            {
                delayField.BindProperty(autoHideDelay);
                delayField.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue < 0f)
                        delayField.value = 0f;
                });
            }

            body.Add(delayField);
            body.Add(new Label("seconds") { style = { unityTextAlign = TextAnchor.MiddleLeft } });

            void Refresh() => body.SetEnabled(autoHide != null && autoHide.boolValue);

            if (autoHide != null)
            {
                toggle.RegisterValueChangedCallback(evt =>
                {
                    autoHide.boolValue = evt.newValue;
                    autoHide.serializedObject.ApplyModifiedProperties();
                    Refresh();
                });
                toggle.TrackPropertyValue(autoHide, _ =>
                {
                    toggle.SetValueWithoutNotify(autoHide.boolValue);
                    Refresh();
                });
            }

            Refresh();
            return block;
        }

        #endregion

        #region Shared

        private static Button ActionButton(string text, string tooltip, Action action)
        {
            return new Button(action)
            {
                text = text,
                tooltip = tooltip,
                style = { width = 54f, marginLeft = 4f, marginRight = 0f }
            };
        }

        #endregion

        #region Get / Set / Reset

        private static void GetFromView(SerializedProperty customPosition, Label hint)
        {
            if (customPosition == null || !TryGetElement(hint, out NavElement element))
                return;

            customPosition.vector3Value = element.resolvedStyle.translate;
            customPosition.serializedObject.ApplyModifiedProperties();
        }

        private static void SetToView(SerializedProperty customPosition, Label hint)
        {
            if (customPosition == null || !TryGetElement(hint, out NavElement element))
                return;

            ApplyTranslate(element, customPosition.vector3Value);
        }

        private static void ResetValue(SerializedProperty customPosition, Label hint)
        {
            if (customPosition == null)
                return;

            customPosition.vector3Value = Vector3.zero;
            customPosition.serializedObject.ApplyModifiedProperties();

            if (TryGetElement(hint, out NavElement element))
                ApplyTranslate(element, Vector3.zero);
        }

        private static void ApplyTranslate(NavElement element, Vector3 position)
        {
            if (!UIBuilderInlineStyle.TrySetTranslate(position))
            {
                element.style.translate = new StyleTranslate(new Translate(
                    new Length(position.x, LengthUnit.Pixel),
                    new Length(position.y, LengthUnit.Pixel),
                    position.z));
            }

            element.InvalidateAnimationStartValues();
        }

        private static bool TryGetElement(Label hint, out NavElement element)
        {
            if (UITransitionPropertyDrawer.TryGetInspectedNavElement(out element))
            {
                hint.text = string.Empty;
                return true;
            }

            hint.text = "Select a NavElement on the canvas";
            return false;
        }

        #endregion
    }
}
