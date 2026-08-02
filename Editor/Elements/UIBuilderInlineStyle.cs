using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Elements
{
    /// <summary>
    /// Provides UI Builder Inline Style functionality.
    /// </summary>
    internal static class UIBuilderInlineStyle
    {
        private const string BuilderTypeName = "Unity.UI.Builder.Builder";
        private const string TranslateFieldTypeName = "Unity.UI.Builder.TranslateStyleField";

        private static bool _typesResolved;
        private static Type _builderType;
        private static Type _translateFieldType;
        private static PropertyInfo _translateValueProperty;

        /// <summary>
        /// Attempts to set translate.
        /// </summary>
        internal static bool TrySetTranslate(Vector3 position)
        {
            try
            {
                EnsureTypesResolved();
                if (_builderType == null || _translateFieldType == null || _translateValueProperty == null)
                    return false;

                var translate = new Translate(
                    new Length(position.x, LengthUnit.Pixel),
                    new Length(position.y, LengthUnit.Pixel),
                    position.z);

                foreach (UnityEngine.Object obj in Resources.FindObjectsOfTypeAll(_builderType))
                {
                    if (obj is not EditorWindow window)
                        continue;

                    VisualElement root = window.rootVisualElement;
                    if (root == null)
                        continue;

                    foreach (VisualElement field in root.Query<VisualElement>().ToList())
                    {
                        if (!_translateFieldType.IsInstanceOfType(field))
                            continue;

                        _translateValueProperty.SetValue(field, translate);
                        return true;
                    }
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static void EnsureTypesResolved()
        {
            if (_typesResolved)
                return;

            _typesResolved = true;

            foreach (Type type in TypeCache.GetTypesDerivedFrom<EditorWindow>())
            {
                if (type.FullName == BuilderTypeName)
                {
                    _builderType = type;
                    break;
                }
            }

            foreach (Type type in TypeCache.GetTypesDerivedFrom<VisualElement>())
            {
                if (type.FullName == TranslateFieldTypeName)
                {
                    _translateFieldType = type;
                    break;
                }
            }

            _translateValueProperty = _translateFieldType?.GetProperty(
                "value",
                BindingFlags.Public | BindingFlags.Instance);
        }
    }
}
