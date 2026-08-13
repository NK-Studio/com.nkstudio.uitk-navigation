using System;
using NKStudio.UITKNavigation.Animation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Builds the Move / Rotate / Scale / Fade channel cards of one direction panel.
    /// </summary>
    internal static class UITransitionChannelCards
    {
        internal static VisualElement CreateMoveCard(SerializedProperty dir, UIAnimationType type)
        {
            SerializedProperty channel = dir.FindPropertyRelative("Move");
            return UITransitionCardLayout.BuildCard(channel, "Move", UITransitionDrawerStyles.MoveColor, body =>
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

                body.Add(UITransitionCardLayout.TwoColumnRow(from, to, 8f));

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
                VisualElement valueRow = UITransitionCardLayout.TwoColumnRow(fromValue, toValue, 8f);
                valueRow.style.marginTop = 8f;
                body.Add(valueRow);

                UITransitionCardLayout.AddTiming(body, channel);
            });
        }

        private static VisualElement MoveDirectionCard(SerializedProperty channel, string title, string directionRelative)
        {
            VisualElement card = UITransitionCardLayout.CreateFieldCard(title);
            SerializedProperty direction = channel?.FindPropertyRelative(directionRelative);
            var selectorRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart, minHeight = 58f }
            };
            selectorRow.Add(UITransitionDirectionGrid.BuildDirectionGrid(direction));
            var directionField = new PropertyField(direction, string.Empty)
            {
                style = { flexGrow = 1f, marginLeft = 8f }
            };
            selectorRow.Add(directionField);
            card.Add(selectorRow);
            return card;
        }

        private static VisualElement MoveReferenceCard(SerializedProperty channel, string title, string typeRelative)
        {
            VisualElement card = UITransitionCardLayout.CreateFieldCard(title);
            SerializedProperty type = channel?.FindPropertyRelative(typeRelative);
            var selectorArea = new VisualElement { style = { minHeight = 58f } };
            selectorArea.Add(new PropertyField(type, string.Empty));
            card.Add(selectorArea);
            return card;
        }

        private static VisualElement MoveValueCard(SerializedProperty channel, string customRelative, string offsetRelative, string controlRelative, string customName, string customTitle, string offsetTitle)
        {
            VisualElement card = UITransitionCardLayout.CreateFieldCard(offsetTitle);
            Label title = card.Q<Label>("field-card-title");
            Vector3Field customField = UITransitionCardLayout.BoundVector3Field(
                channel?.FindPropertyRelative(customRelative));
            Vector3Field offsetField = UITransitionCardLayout.BoundVector3Field(
                channel?.FindPropertyRelative(offsetRelative));
            SerializedProperty control = channel?.FindPropertyRelative(controlRelative);
            card.Add(customField);
            card.Add(offsetField);

            void Refresh()
            {
                bool custom = UITransitionPropertyUtility.GetEnumName(control) == customName;
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

        internal static VisualElement CreateFadeCard(SerializedProperty dir)
        {
            return CreateReferenceChannelCard(
                dir.FindPropertyRelative("Fade"),
                "Fade",
                UITransitionDrawerStyles.FadeColor,
                "Custom fade",
                "Offset");
        }

        internal static VisualElement CreateScaleCard(SerializedProperty dir)
        {
            return CreateReferenceChannelCard(
                dir.FindPropertyRelative("Scale"),
                "Scale",
                UITransitionDrawerStyles.ScaleColor,
                "Custom scale",
                "Offset");
        }

        internal static VisualElement CreateRotateCard(SerializedProperty dir)
        {
            return CreateReferenceChannelCard(
                dir.FindPropertyRelative("Rotate"),
                "Rotate",
                UITransitionDrawerStyles.RotateColor,
                "Custom angle (deg)",
                "Angle offset (deg)");
        }

        private static VisualElement CreateReferenceChannelCard(SerializedProperty channel, string channelName, Color color, string customLabel, string offsetLabel)
        {
            return UITransitionCardLayout.BuildCard(channel, channelName, color, body =>
            {
                VisualElement fromSelector = ReferenceSelectorCard(
                    channel, $"{channelName} from", "FromType");
                VisualElement toSelector = ReferenceSelectorCard(
                    channel, $"{channelName} to", "ToType");
                body.Add(UITransitionCardLayout.TwoColumnRow(fromSelector, toSelector, 8f));

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
                VisualElement valueRow = UITransitionCardLayout.TwoColumnRow(fromValue, toValue, 8f);
                valueRow.style.marginTop = 8f;
                body.Add(valueRow);

                UITransitionCardLayout.AddTiming(body, channel);
            });
        }

        private static VisualElement ReferenceSelectorCard(SerializedProperty channel, string title, string typeRelative)
        {
            VisualElement card = UITransitionCardLayout.CreateFieldCard(title);
            SerializedProperty type = channel?.FindPropertyRelative(typeRelative);
            if (type != null)
                card.Add(new PropertyField(type, string.Empty));
            return card;
        }

        private static VisualElement ReferenceValueCard(SerializedProperty channel, string typeRelative, string customRelative, string offsetRelative, string customTitle, string offsetTitle)
        {
            VisualElement card = UITransitionCardLayout.CreateFieldCard(string.Empty);
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
                bool custom = UITransitionPropertyUtility.GetEnumName(type) == nameof(UIReferenceValue.CustomValue);
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
    }

    /// <summary>
    /// Builds the 17-cell move direction widget.
    /// </summary>
    internal static class UITransitionDirectionGrid
    {
        /// <summary>
        /// Four custom corners, four outside bars, and the Doozy-compatible inner 3x3 positions.
        /// </summary>
        private const string DirectionCellOnClass = "nk-transition-direction-cell--on";

        /// <summary>
        /// Builds the widget. A <see langword="null"/> property renders the cells without binding.
        /// </summary>
        internal static VisualElement BuildDirectionGrid(SerializedProperty directionProperty)
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
                UITransitionDrawerStyles.Round(element, 2f);
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
                string current = UITransitionPropertyUtility.GetEnumName(directionProperty);
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
                        // ignored
                    }
                }).Every(100);
            }

            RefreshCells();
            return widget;
        }
    }
}
