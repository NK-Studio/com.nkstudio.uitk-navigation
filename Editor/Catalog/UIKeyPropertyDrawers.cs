using NKStudio.UITKNavigation.Identity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    [CustomPropertyDrawer(typeof(UIKey))]
    internal sealed class UIKeyPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty category = property.FindPropertyRelative("category");
            SerializedProperty key = property.FindPropertyRelative("key");
            
            if (category == null || key == null)
                return new Label($"UIKey 직렬화 필드를 찾지 못했습니다: {property.propertyPath}");

            UIKeyPickerField field = UIKeyPickerField.Create(property.displayName, property, category, key);
            field.TrackPropertyValue(category, _ => field.Refresh());
            field.TrackPropertyValue(key, _ => field.Refresh());
            return field;
        }
    }

    [CustomPropertyDrawer(typeof(UIKeySelectorAttribute))]
    internal sealed class UIKeySelectorPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var selector = (UIKeySelectorAttribute)attribute;
            SerializedProperty key = FindSibling(property, selector.KeyPropertyName);
            
            if (key == null)
            {
                return new HelpBox(
                    $"Companion Key 속성 '{selector.KeyPropertyName}'을 찾지 못했습니다.",
                    HelpBoxMessageType.Error);
            }

            UIKeyPickerField field = UIKeyPickerField.Create(property.displayName.Replace(" Category", string.Empty),
                property, property, key, () => selector.Kind);
            
            field.TrackPropertyValue(property, _ => field.Refresh());
            field.TrackPropertyValue(key, _ => field.Refresh());
            return field;
        }

        private static SerializedProperty FindSibling(
            SerializedProperty property,
            string siblingName)
        {
            string parentPath = string.Empty;
            int separator = property.propertyPath.LastIndexOf('.');
            if (separator >= 0)
                parentPath = property.propertyPath.Substring(0, separator + 1);

            SerializedProperty sibling = property.serializedObject.FindProperty(parentPath + siblingName);
            if (sibling != null)
                return sibling;

            if (string.IsNullOrEmpty(siblingName))
                return null;

            string camelCase = char.ToLowerInvariant(siblingName[0]) + siblingName.Substring(1);
            return property.serializedObject.FindProperty(parentPath + camelCase);
        }
    }
}