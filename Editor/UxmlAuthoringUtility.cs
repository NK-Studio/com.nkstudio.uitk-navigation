using System.Text;
using UnityEditor;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor
{
    /// <summary>
    /// Provides Uxml Authoring Utility functionality.
    /// </summary>
    internal static class UxmlAuthoringUtility
    {
        /// <summary>
        /// Finds o wn er pr op er ty.
        /// </summary>
        internal static SerializedProperty FindOwnerProperty(SerializedProperty property, string name)
        {
            if (property == null)
                return null;

            string path = property.propertyPath;

            while (true)
            {
                int separator = path.LastIndexOf('.');
                if (separator < 0)
                    break;

                path = path.Substring(0, separator);
                SerializedProperty found = property.serializedObject.FindProperty($"{path}.{name}");
                if (found != null)
                    return found;
            }

            return property.serializedObject.FindProperty(name);
        }

        /// <summary>
        /// Determines whether write attribute.
        /// </summary>
        internal static bool CanWriteAttribute(SerializedProperty anchor, string attributeName)
        {
            return FindOwnerProperty(anchor, attributeName) != null
                   && FindOwnerProperty(anchor, $"{attributeName}_UxmlAttributeFlags") != null;
        }

        /// <summary>
        /// Sets s tr in ga tt ri bu te.
        /// </summary>
        internal static bool SetStringAttribute(SerializedProperty anchor, string attributeName, string value)
        {
            SerializedProperty target = FindOwnerProperty(anchor, attributeName);
            SerializedProperty flags = FindOwnerProperty(anchor, $"{attributeName}_UxmlAttributeFlags");
            if (target == null || flags == null || target.propertyType != SerializedPropertyType.String)
                return false;

            bool clear = string.IsNullOrEmpty(value);
            if (target.stringValue == value && !clear)
                return false;

            target.stringValue = value;

            flags.intValue = (int)(clear
                ? UxmlSerializedData.UxmlAttributeFlags.Ignore
                : UxmlSerializedData.UxmlAttributeFlags.OverriddenInUxml);

            target.serializedObject.ApplyModifiedProperties();
            return true;
        }

        /// <summary>
        /// Performs the to kebab case operation.
        /// </summary>
        internal static string ToKebabCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length + 8);
            bool pendingSeparator = false;

            for (int i = 0; i < value.Length; i++)
                {
                char current = value[i];

                if (current is ' ' or '_' or '-' or '.' or '/')
                {
                    pendingSeparator = builder.Length > 0;
                    continue;
                }

                if (!char.IsLetterOrDigit(current))
                    continue;

                if (char.IsUpper(current) && builder.Length > 0)
                {
                    char previous = value[i - 1];
                    bool afterLowerOrDigit = char.IsLower(previous) || char.IsDigit(previous);
                    bool endOfUpperRun = char.IsUpper(previous)
                                         && i + 1 < value.Length
                                         && char.IsLower(value[i + 1]);
                    if (afterLowerOrDigit || endOfUpperRun)
                        pendingSeparator = true;
                }

                if (pendingSeparator)
                {
                    builder.Append('-');
                    pendingSeparator = false;
                }

                builder.Append(char.ToLowerInvariant(current));
            }

            return builder.ToString();
        }
    }
}
