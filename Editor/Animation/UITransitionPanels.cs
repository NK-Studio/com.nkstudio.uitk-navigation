using NKStudio.UITKNavigation.Animation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Holds the elements of one Show / Hide tab so the drawer can toggle its active state.
    /// </summary>
    internal struct UITransitionMainTab
    {
        internal VisualElement root;
        internal Button button;
        private VisualElement _bar;

        internal void SetActive(bool active, Color accent)
        {
            button.EnableInClassList("nk-transition-tab--active", active);
            _bar.style.backgroundColor = active ? accent : Color.clear;
        }

        internal UITransitionMainTab(VisualElement r, Button b, VisualElement bar)
        {
            root = r;
            button = b;
            _bar = bar;
        }
    }

    /// <summary>
    /// Builds the Show / Hide tabs and the per-direction channel panel.
    /// </summary>
    internal static class UITransitionPanelFactory
    {
        internal static UITransitionMainTab CreateMainTab(string title, SerializedProperty dir)
        {
            var button = new Button();
            button.style.flexGrow = 1f;
            button.style.paddingTop = 5f;
            button.style.paddingBottom = 0f;
            button.style.flexDirection = FlexDirection.Column;
            button.style.alignItems = Align.Center;
            UITransitionDrawerStyles.Round(button, 4f);

            var label = new Label(title);
            label.style.fontSize = 12f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.AddToClassList("nk-transition-title");
            button.AddToClassList("nk-transition-tab");
            button.Add(label);

            var dots = new VisualElement();
            dots.style.flexDirection = FlexDirection.Row;
            dots.style.marginTop = 3f;
            dots.Add(ChannelDot(dir, "Move", UITransitionDrawerStyles.MoveColor));
            dots.Add(ChannelDot(dir, "Rotate", UITransitionDrawerStyles.RotateColor));
            dots.Add(ChannelDot(dir, "Scale", UITransitionDrawerStyles.ScaleColor));
            dots.Add(ChannelDot(dir, "Fade", UITransitionDrawerStyles.FadeColor));
            button.Add(dots);

            var bar = new VisualElement();
            bar.style.height = 2f;
            bar.style.marginTop = 4f;
            bar.style.width = Length.Percent(70);
            UITransitionDrawerStyles.Round(bar, 1f);
            button.Add(bar);

            return new UITransitionMainTab(button, button, bar);
        }

        private static VisualElement ChannelDot(SerializedProperty dir, string channel, Color onColor)
        {
            var dot = new VisualElement();
            dot.style.width = 5f;
            dot.style.height = 5f;
            dot.style.marginLeft = 1.5f;
            dot.style.marginRight = 1.5f;
            UITransitionDrawerStyles.Round(dot, 2.5f);

            SerializedProperty enableProp = dir?.FindPropertyRelative(channel)?.FindPropertyRelative("Enable");

            void Refresh() => dot.style.backgroundColor = enableProp != null && enableProp.boolValue
                ? onColor
                : UITransitionDrawerStyles.CellOff;

            if (enableProp != null) dot.TrackPropertyValue(enableProp, _ => Refresh());
            Refresh();
            return dot;
        }

        internal static VisualElement CreateDirectionPanel(SerializedProperty dir, UIAnimationType type)
        {
            var panel = new VisualElement();

            SerializedProperty categoryProp = dir.FindPropertyRelative("PresetCategory");
            SerializedProperty variantProp = dir.FindPropertyRelative("PresetVariant");
            if (categoryProp != null && variantProp != null)
                panel.Add(UITransitionPresetBlock.CreatePresetBlock(dir, categoryProp, variantProp, type));

            var subRow = new VisualElement();
            subRow.style.flexDirection = FlexDirection.Row;
            subRow.style.marginBottom = 6f;

            var moveTab = CreateSubTab("Move", UITransitionDrawerStyles.MoveColor);
            var rotateTab = CreateSubTab("Rotate", UITransitionDrawerStyles.RotateColor);
            var scaleTab = CreateSubTab("Scale", UITransitionDrawerStyles.ScaleColor);
            var fadeTab = CreateSubTab("Fade", UITransitionDrawerStyles.FadeColor);
            moveTab.style.marginRight = 4f;
            rotateTab.style.marginRight = 4f;
            scaleTab.style.marginRight = 4f;
            subRow.Add(moveTab);
            subRow.Add(rotateTab);
            subRow.Add(scaleTab);
            subRow.Add(fadeTab);
            panel.Add(subRow);

            VisualElement moveCard = UITransitionChannelCards.CreateMoveCard(dir, type);
            VisualElement rotateCard = UITransitionChannelCards.CreateRotateCard(dir);
            VisualElement scaleCard = UITransitionChannelCards.CreateScaleCard(dir);
            VisualElement fadeCard = UITransitionChannelCards.CreateFadeCard(dir);
            panel.Add(moveCard);
            panel.Add(rotateCard);
            panel.Add(scaleCard);
            panel.Add(fadeCard);

            var tabs = new[] { moveTab, rotateTab, scaleTab, fadeTab };
            var cards = new[] { moveCard, rotateCard, scaleCard, fadeCard };

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
            
            UITransitionDrawerStyles.Round(b, 4f);

            var dot = new VisualElement();
            dot.style.width = 6f;
            dot.style.height = 6f;
            dot.style.marginRight = 5f;
            dot.style.backgroundColor = dotColor;
            
            UITransitionDrawerStyles.Round(dot, 3f);
            
            b.Add(dot);
            var label = new Label(name);
            label.style.fontSize = 12f;
            label.AddToClassList("nk-transition-title");
            
            b.AddToClassList("nk-transition-tab");
            b.Add(label);
            
            return b;
        }
    }
}