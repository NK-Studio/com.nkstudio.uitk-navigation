using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides the shared stylesheet of the graph node inspector.
    /// </summary>
    internal static class UINavigationInspectorStyles
    {
        internal static void Attach(VisualElement root)
        {
            if (root == null)
                return;

            StyleSheet styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.nkstudio.uitk-navigation/Editor/Styles/UINavigationGraphInspector.uss");
            if (styles != null && !root.styleSheets.Contains(styles))
                root.styleSheets.Add(styles);
        }
    }

    /// <summary>
    /// Aligns custom fields with the node option fields drawn by Graph Toolkit.
    /// </summary>
    internal static class UINavigationInspectorLayout
    {
        /// <summary>
        /// Nudges the input box of <paramref name="field"/> so it lines up with the node
        /// option fields around it, which Graph Toolkit lays out on its own.
        /// </summary>
        internal static void MatchInputBoundsToNodeOption(VisualElement owner, VisualElement field)
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
}
