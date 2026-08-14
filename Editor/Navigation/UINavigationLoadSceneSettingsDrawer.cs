using NKStudio.UITKNavigation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [CustomPropertyDrawer(typeof(UINavigationLoadSceneSettings))]
    internal sealed class UINavigationLoadSceneSettingsDrawer : PropertyDrawer
    {
        private static readonly Color Panel = new(0.145f, 0.155f, 0.175f, 0.96f);
        private static readonly Color Muted = new(0.56f, 0.59f, 0.65f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty referenceKind =
                property.FindPropertyRelative("referenceKind");
            SerializedProperty sceneName = property.FindPropertyRelative("sceneName");
            SerializedProperty buildIndex = property.FindPropertyRelative("buildIndex");
            SerializedProperty loadMode = property.FindPropertyRelative("loadMode");
            SerializedProperty allowActivation =
                property.FindPropertyRelative("allowSceneActivation");
            SerializedProperty activationDelay =
                property.FindPropertyRelative("sceneActivationDelay");

            var root = new VisualElement();
            root.style.marginTop = 6f;
            root.style.marginBottom = 7f;

            root.Add(CreateSectionTitle("SCENE REFERENCE"));
            var referencePanel = CreatePanel();
            var referenceRow = CreateRow();

            var referenceField = new EnumField(
                (UINavigationSceneReferenceKind)referenceKind.enumValueIndex);
            VisualElement referenceColumn = CreateColumn(
                "GET SCENE BY",
                referenceField,
                145f);
            referenceColumn.style.flexGrow = 0f;
            referenceRow.Add(referenceColumn);

            var nameField = new TextField { value = sceneName.stringValue, isDelayed = true };
            nameField.tooltip = "Build Settings에 등록된 Scene 이름 또는 경로입니다.";
            VisualElement nameColumn = CreateColumn("SCENE NAME", nameField);
            nameColumn.style.flexGrow = 1f;
            referenceRow.Add(nameColumn);

            var indexField = new IntegerField
            {
                value = Mathf.Max(0, buildIndex.intValue),
                isDelayed = true
            };
            indexField.tooltip = "Build Settings의 Scene 인덱스입니다.";
            VisualElement indexColumn = CreateColumn("BUILD INDEX", indexField);
            indexColumn.style.flexGrow = 1f;
            referenceRow.Add(indexColumn);

            var modeField = new EnumField(
                (LoadSceneMode)loadMode.enumValueIndex);
            modeField.tooltip =
                "Single은 현재 Scene을 교체하고, Additive는 현재 Scene 위에 추가로 로드합니다.";
            VisualElement modeColumn = CreateColumn(
                "LOAD SCENE MODE",
                modeField,
                145f);
            modeColumn.style.flexGrow = 0f;
            modeColumn.style.marginRight = 0f;
            referenceRow.Add(modeColumn);
            referencePanel.Add(referenceRow);
            root.Add(referencePanel);

            root.Add(CreateSectionTitle("ACTIVATION"));
            var activationPanel = CreatePanel();
            var activationRow = CreateRow();

            var allowField = new Toggle("Allow Scene Activation")
            {
                value = allowActivation.boolValue
            };
            allowField.tooltip =
                "로드가 90%에 도달했을 때 Scene을 자동으로 활성화합니다.";
            allowField.style.flexGrow = 1f;
            allowField.style.unityFontStyleAndWeight = FontStyle.Bold;
            activationRow.Add(allowField);

            var delayField = new FloatField
            {
                value = Mathf.Max(0f, activationDelay.floatValue),
                isDelayed = true
            };
            delayField.tooltip =
                "비동기 로드가 90%에 도달한 뒤 활성화하기 전까지 기다릴 Unscaled Time입니다.";
            VisualElement delayColumn = CreateColumn(
                "ACTIVATION DELAY (SEC)",
                delayField,
                185f);
            delayColumn.style.flexGrow = 0f;
            activationRow.Add(delayColumn);
            activationPanel.Add(activationRow);
            root.Add(activationPanel);

            var pendingHint = new Label("90%에서 대기 · 수동 활성화 필요");
            pendingHint.tooltip =
                "UINavigatorBehaviour.ActivatePendingScene()으로 활성화를 계속합니다.";
            pendingHint.style.fontSize = 10f;
            pendingHint.style.color = Muted;
            pendingHint.style.marginTop = 4f;
            pendingHint.style.marginLeft = 4f;
            activationPanel.Add(pendingHint);

            void Refresh()
            {
                bool useIndex =
                    (UINavigationSceneReferenceKind)referenceKind.enumValueIndex ==
                    UINavigationSceneReferenceKind.BuildIndex;
                nameColumn.style.display =
                    useIndex ? DisplayStyle.None : DisplayStyle.Flex;
                indexColumn.style.display =
                    useIndex ? DisplayStyle.Flex : DisplayStyle.None;
                delayColumn.SetEnabled(allowActivation.boolValue);
                pendingHint.style.display =
                    allowActivation.boolValue
                        ? DisplayStyle.None
                        : DisplayStyle.Flex;
            }

            referenceField.RegisterValueChangedCallback(evt =>
            {
                referenceKind.enumValueIndex =
                    (int)(UINavigationSceneReferenceKind)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            nameField.RegisterValueChangedCallback(evt =>
            {
                sceneName.stringValue = evt.newValue?.Trim() ?? string.Empty;
                property.serializedObject.ApplyModifiedProperties();
            });
            indexField.RegisterValueChangedCallback(evt =>
            {
                int value = Mathf.Max(0, evt.newValue);
                buildIndex.intValue = value;
                property.serializedObject.ApplyModifiedProperties();
                if (value != evt.newValue)
                    indexField.SetValueWithoutNotify(value);
            });
            modeField.RegisterValueChangedCallback(evt =>
            {
                loadMode.enumValueIndex = (int)(LoadSceneMode)evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });
            allowField.RegisterValueChangedCallback(evt =>
            {
                allowActivation.boolValue = evt.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            });
            delayField.RegisterValueChangedCallback(evt =>
            {
                float value = Mathf.Max(0f, evt.newValue);
                activationDelay.floatValue = value;
                property.serializedObject.ApplyModifiedProperties();
                if (!Mathf.Approximately(value, evt.newValue))
                    delayField.SetValueWithoutNotify(value);
            });
            Refresh();
            return root;
        }

        private static Label CreateSectionTitle(string text)
        {
            var title = new Label(text);
            title.style.fontSize = 10f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Muted;
            title.style.marginTop = 6f;
            title.style.marginBottom = 3f;
            title.style.marginLeft = 3f;
            return title;
        }

        private static VisualElement CreatePanel()
        {
            var panel = new VisualElement();
            panel.style.backgroundColor = Panel;
            panel.style.paddingTop = 7f;
            panel.style.paddingBottom = 7f;
            panel.style.paddingLeft = 8f;
            panel.style.paddingRight = 8f;
            panel.style.borderTopLeftRadius = 4f;
            panel.style.borderTopRightRadius = 4f;
            panel.style.borderBottomLeftRadius = 4f;
            panel.style.borderBottomRightRadius = 4f;
            return panel;
        }

        private static VisualElement CreateRow()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            return row;
        }

        private static VisualElement CreateColumn(
            string label,
            VisualElement field,
            float width = 0f)
        {
            var column = new VisualElement();
            column.style.marginRight = 7f;
            if (width > 0f)
                column.style.width = width;

            var caption = new Label(label);
            caption.style.fontSize = 9f;
            caption.style.color = Muted;
            caption.style.marginBottom = 2f;
            column.Add(caption);
            column.Add(field);
            return column;
        }
    }
}
