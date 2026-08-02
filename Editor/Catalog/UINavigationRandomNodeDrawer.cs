using System;
using NKStudio.UITKNavigation.Editor.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    [CustomPropertyDrawer(typeof(UINavigationRandomOutputCollection))]
    internal sealed class UINavigationRandomOutputCollectionDrawer : PropertyDrawer
    {
        private const float MaxWeight = 100f;
        private const float DefaultWeight = 100f;
        private const float RowHeight = 26f;

        private const string RefreshIconPath =
            "Packages/com.nkstudio.uitk-navigation/Editor/Assets/RefreshIcon.svg";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty itemsProp = property.FindPropertyRelative("items");

            VisualElement section = UINavigationListDrawerUtility.CreateList(
                "Random Outputs",
                itemsProp,
                RowHeight,
                CreateWeightCard,
                InitializeNewItem,
                "No random outputs.");

            section.AddToClassList("uinavigation-view-section");
            return section;
        }

        private static VisualElement CreateWeightCard(
            SerializedProperty element,
            int index,
            Action remove)
        {
            SerializedProperty weightProp = element.FindPropertyRelative("weight");

            var card = new VisualElement();
            card.AddToClassList("uinavigation-random-card");

            var order = new Label((index + 1).ToString());
            order.AddToClassList("uinavigation-random-card__index");
            card.Add(order);

            var value = new Label(FormatWeight(weightProp.floatValue));
            value.AddToClassList("uinavigation-random-card__value");
            card.Add(value);

            var slider = new Slider(0f, MaxWeight);
            slider.AddToClassList("uinavigation-random-card__slider");
            slider.BindProperty(weightProp);
            slider.RegisterValueChangedCallback(evt => value.text = FormatWeight(evt.newValue));
            card.Add(slider);

            Button refresh = CreateIconButton(RefreshIconPath, 11f);
            refresh.tooltip = $"Weight를 기본값 {DefaultWeight:0}으로 되돌립니다.";
            refresh.AddToClassList("uinavigation-random-card__refresh");
            refresh.clicked += () =>
            {
                weightProp.serializedObject.Update();
                weightProp.floatValue = DefaultWeight;
                weightProp.serializedObject.ApplyModifiedProperties();
                value.text = FormatWeight(DefaultWeight);
            };
            card.Add(refresh);

            var divider = new VisualElement();
            divider.AddToClassList("uinavigation-command-card__divider");
            card.Add(divider);

            card.Add(UINavigationListDrawerUtility.CreateRemoveButton(
                remove,
                "Remove random output",
                "-"));
            return card;
        }

        /// <summary>
        /// Initializes new item.
        /// </summary>
        private static void InitializeNewItem(SerializedProperty element)
        {
            SerializedProperty idProp = element.FindPropertyRelative("outputId");
            if (idProp != null)
                idProp.stringValue = Guid.NewGuid().ToString("N");

            SerializedProperty weightProp = element.FindPropertyRelative("weight");
            if (weightProp != null)
                weightProp.floatValue = DefaultWeight;
        }

        private static string FormatWeight(float weight)
        {
            if (float.IsNaN(weight) || float.IsInfinity(weight))
                return "0";

            return Mathf.RoundToInt(Mathf.Clamp(weight, 0f, MaxWeight)).ToString();
        }

        private static Button CreateIconButton(string iconPath, float iconSize)
        {
            var button = new Button();

            var icon = new VisualElement
            {
                style =
                {
                    width = iconSize,
                    height = iconSize,
                    flexShrink = 0f,
                    unityBackgroundImageTintColor = EditorGUIUtility.isProSkin
                        ? new Color(0.78f, 0.78f, 0.78f)
                        : new Color(0.25f, 0.25f, 0.25f)
                }
            };

            VectorImage vector = AssetDatabase.LoadAssetAtPath<VectorImage>(iconPath);
            if (vector != null)
                icon.style.backgroundImage = new StyleBackground(vector);

            button.Add(icon);
            return button;
        }
    }
}
