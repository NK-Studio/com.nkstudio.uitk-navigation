using System;
using System.Reflection;
using NKStudio.UITKNavigation.Elements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Resolves the NavElement the user currently has selected in UI Builder or the UI viewport.
    /// </summary>
    internal static class UITransitionSelectionResolver
    {
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
                        NavElement found = selectedElement as NavElement ?? selectedElement.GetFirstAncestorOfType<NavElement>();
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
    }
}
