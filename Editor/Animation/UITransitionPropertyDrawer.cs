using NKStudio.UITKNavigation.Animation;
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
    /// <remarks>
    /// This type only assembles the root layout. The pieces live next to it:
    /// <see cref="UITransitionPanelFactory"/> (tabs and channel panel),
    /// <see cref="UITransitionChannelCards"/> and <see cref="UITransitionCardLayout"/> (cards),
    /// <see cref="UITransitionPresetBlock"/> (preset selection),
    /// <see cref="UITransitionPreviewController"/> (play preview) and
    /// <see cref="UITransitionUsageHints"/> (usageHints authoring).
    /// </remarks>
    [CustomPropertyDrawer(typeof(UITransitionInspectorAttribute))]
    internal sealed class UITransitionPropertyDrawer : PropertyDrawer
    {
        private static readonly string[] ChannelNames = { "Move", "Rotate", "Scale", "Fade" };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty show = property.FindPropertyRelative("Show");
            SerializedProperty hide = property.FindPropertyRelative("Hide");

            var root = new VisualElement();
            StyleSheet theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(UITransitionDrawerStyles.ThemeStyleSheetPath);

            if (theme != null)
                root.styleSheets.Add(theme);

            root.AddToClassList("nk-transition-root");
            root.style.marginTop = 2f;
            root.style.marginBottom = 4f;

            root.RegisterCallback<DetachFromPanelEvent>(_ => UITransitionPreviewController.CancelPreview());

            VisualElement showPanel = show != null
                ? UITransitionPanelFactory.CreateDirectionPanel(show, UIAnimationType.Show)
                : null;
            VisualElement hidePanel = hide != null
                ? UITransitionPanelFactory.CreateDirectionPanel(hide, UIAnimationType.Hide)
                : null;

            var tabRow = new VisualElement();
            tabRow.style.flexDirection = FlexDirection.Row;
            tabRow.style.marginBottom = 6f;

            var showTab = UITransitionPanelFactory.CreateMainTab("Show", show);
            showTab.root.style.marginRight = 5f;
            var hideTab = UITransitionPanelFactory.CreateMainTab("Hide", hide);

            var hint = new Label(string.Empty)
            {
                style =
                {
                    fontSize = 10f,
                    color = UITransitionDrawerStyles.HideAccent,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginRight = 5f,
                    alignSelf = Align.Center
                }
            };
            var preview = UITransitionDrawerStyles.CreateIconButton(UITransitionDrawerStyles.PlayIconPath, 16f);
            preview.tooltip = "선택한 Element로 현재 탭의 애니메이션을 재생";
            preview.style.width = 26f;
            preview.style.alignSelf = Align.Center;
            
            var reset = UITransitionDrawerStyles.CreateIconButton(UITransitionDrawerStyles.ResetIconPath, 16f);
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
                showTab.SetActive(showSelected, UITransitionDrawerStyles.ShowAccent);
                hideTab.SetActive(!showSelected, UITransitionDrawerStyles.HideAccent);
                if (showPanel != null) showPanel.style.display = showSelected ? DisplayStyle.Flex : DisplayStyle.None;
                if (hidePanel != null) hidePanel.style.display = showSelected ? DisplayStyle.None : DisplayStyle.Flex;
            }

            showTab.button.clicked += () => { showSelected = true; UpdateTabs(); };
            hideTab.button.clicked += () => { showSelected = false; UpdateTabs(); };
            UpdateTabs();

            preview.clicked += () => UITransitionPreviewController.RunPreview(
                showSelected ? UIAnimationType.Show : UIAnimationType.Hide,
                hint);
            
            reset.clicked += () =>
            {
                UITransitionPreviewController.CancelPreview();
                if (UITransitionSelectionResolver.TryGetInspectedNavElement(out NavElement element))
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
                            UITransitionUsageHints.SyncUsageHints(property);
                        });
                    }
                }
            }
            RefreshActionButtons();

            return root;
        }
    }
}
