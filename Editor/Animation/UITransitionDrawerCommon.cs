using System;
using NKStudio.UITKNavigation.Animation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Provides the shared colors, asset paths, and element helpers used by the transition inspector.
    /// </summary>
    internal static class UITransitionDrawerStyles
    {
        internal static readonly Color MoveColor = new(0.388f, 0.600f, 0.133f);
        internal static readonly Color RotateColor = new(0.729f, 0.459f, 0.090f);
        internal static readonly Color ScaleColor = new(0.831f, 0.325f, 0.494f);
        internal static readonly Color FadeColor = new(0.216f, 0.541f, 0.867f);

        internal static readonly Color ShowAccent = new(0.32f, 0.55f, 0.40f);
        internal static readonly Color HideAccent = new(0.62f, 0.36f, 0.38f);

        internal static readonly Color CellOff = new(0.30f, 0.31f, 0.34f);

        internal const string AlignedFieldClass = "unity-base-field__aligned";
        internal const string ThemeStyleSheetPath = "Packages/com.nkstudio.uitk-navigation/Editor/Animation/UITransitionPropertyDrawer.uss";
        internal const string PlayIconPath = "Packages/com.nkstudio.uitk-navigation/Editor/Assets/PlayIcon.svg";
        internal const string ResetIconPath = "Packages/com.nkstudio.uitk-navigation/Editor/Assets/ResetIcon.svg";

        /// <summary>
        /// Creates a square button that draws a vector icon and no text.
        /// </summary>
        internal static Button CreateIconButton(string iconPath, float iconSize)
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

        /// <summary>
        /// Applies the same corner radius to all four corners.
        /// </summary>
        internal static void Round(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }
    }

    /// <summary>
    /// Provides SerializedProperty helpers shared by the transition inspector.
    /// </summary>
    internal static class UITransitionPropertyUtility
    {
        /// <summary>
        /// Gets the selected enum member name, or an empty string when the property is unusable.
        /// </summary>
        internal static string GetEnumName(SerializedProperty property)
        {
            return property != null
                   && property.enumValueIndex >= 0
                   && property.enumValueIndex < property.enumNames.Length
                ? property.enumNames[property.enumValueIndex]
                : string.Empty;
        }
    }

    /// <summary>
    /// Builds the card, row, and timing primitives that every channel card is assembled from.
    /// </summary>
    internal static class UITransitionCardLayout
    {
        internal static Vector3Field BoundVector3Field(SerializedProperty property)
        {
            var field = new Vector3Field(string.Empty);
            if (property != null)
                field.BindProperty(property);
            field.labelElement.style.display = DisplayStyle.None;
            field.style.flexGrow = 1f;
            return field;
        }

        internal static VisualElement TwoColumnRow(VisualElement left, VisualElement right, float gap)
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

        internal static VisualElement ThreeColumnRow(VisualElement left, VisualElement middle, VisualElement right, float gap)
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

        internal static VisualElement CreateFieldCard(string title)
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
            UITransitionDrawerStyles.Round(card, 6f);
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

        /// <summary>
        /// Builds a channel card with its enable toggle, and fills the body through the callback.
        /// </summary>
        internal static VisualElement BuildCard(SerializedProperty channel, string name, Color color, Action<VisualElement> addBody)
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
            UITransitionDrawerStyles.Round(card, 8f);

            if (channel == null)
            {
                card.Add(new Label($"'{name}' property is missing") { style = { color = UITransitionDrawerStyles.HideAccent } });
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
            UITransitionDrawerStyles.Round(dot, 4.5f);
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
        /// Appends the delay / duration / loops and play mode / ease rows to a channel body.
        /// </summary>
        internal static void AddTiming(VisualElement body, SerializedProperty channel)
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
                    && UITransitionPropertyUtility.GetEnumName(playModeProperty) != nameof(UIAnimationPlayMode.PingPong))
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

        private static VisualElement PropertyCard(SerializedProperty channel, string relative, string title)
        {
            VisualElement card = CreateFieldCard(title);
            SerializedProperty property = channel?.FindPropertyRelative(relative);
            if (property != null)
                card.Add(new PropertyField(property, string.Empty));
            return card;
        }
    }
}
