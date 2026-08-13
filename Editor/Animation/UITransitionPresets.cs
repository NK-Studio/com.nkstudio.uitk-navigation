using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Animation.Presets;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Animation
{
    /// <summary>
    /// Builds the preset category / variant selector shown above the channel tabs.
    /// </summary>
    internal static class UITransitionPresetBlock
    {
        /// <summary>
        /// Creates p re se tb lo ck.
        /// </summary>
        internal static VisualElement CreatePresetBlock(SerializedProperty dir, SerializedProperty categoryProp, SerializedProperty variantProp, UIAnimationType type)
        {
            var block = new VisualElement { style = { marginBottom = 6f } };

            var categoryField = new PropertyField(categoryProp, "Preset");
            categoryField.AddToClassList(UITransitionDrawerStyles.AlignedFieldClass);
            block.Add(categoryField);

            var variantRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginTop = 2f }
            };

            var variantField = new DropdownField("Variant") { style = { flexGrow = 1f } };
            variantField.AddToClassList(UITransitionDrawerStyles.AlignedFieldClass);

            var applyButton = new Button
            {
                text = "Apply",
                tooltip = $"선택한 프리셋을 현재 {type} 채널 값으로 구워 넣습니다.",
                style = { width = 64f, marginLeft = 6f }
            };

            variantRow.Add(variantField);
            variantRow.Add(applyButton);
            block.Add(variantRow);

            UITransitionPresetCategory CurrentCategory() =>
                Enum.TryParse(UITransitionPropertyUtility.GetEnumName(categoryProp), out UITransitionPresetCategory parsed)
                    ? parsed
                    : UITransitionPresetCategory.None;

            void RebuildVariants()
            {
                UITransitionPresetCategory category = CurrentCategory();
                int count = UITransitionPresetLibrary.GetVariantCount(category);

                var names = new List<string>(count);
                for (int i = 1; i <= count; i++)
                    names.Add(UITransitionPresetLibrary.GetVariantName(category, i));

                variantField.choices = names;
                bool hasVariants = count > 0;
                variantRow.style.display = hasVariants ? DisplayStyle.Flex : DisplayStyle.None;
                applyButton.SetEnabled(category != UITransitionPresetCategory.None);

                if (!hasVariants)
                    return;

                int variant = Mathf.Clamp(variantProp.intValue, 1, count);
                if (variant != variantProp.intValue)
                {
                    variantProp.intValue = variant;
                    variantProp.serializedObject.ApplyModifiedProperties();
                }

                variantField.SetValueWithoutNotify(names[variant - 1]);
            }

            variantField.RegisterValueChangedCallback(evt =>
            {
                int index = variantField.choices.IndexOf(evt.newValue);
                if (index < 0)
                    return;

                variantProp.intValue = index + 1;
                variantProp.serializedObject.ApplyModifiedProperties();
            });

            bool resetPending = false;

            void ResetCategoryIfPending()
            {
                if (!resetPending)
                    return;

                resetPending = false;

                if (categoryProp.serializedObject == null || categoryProp.serializedObject.targetObject == null)
                    return;

                categoryProp.intValue = (int)UITransitionPresetCategory.None;
                categoryProp.serializedObject.ApplyModifiedProperties();
                RebuildVariants();
            }

            applyButton.clicked += () =>
            {
                UITransitionPresetApplier.ApplyPreset(dir, categoryProp, variantProp, type);
                resetPending = true;
                RebuildVariants();
            };

            block.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (evt.relatedTarget is VisualElement next && (next == block || block.Contains(next)))
                    return;

                ResetCategoryIfPending();
            });

            block.RegisterCallback<DetachFromPanelEvent>(_ => ResetCategoryIfPending());

            block.TrackPropertyValue(categoryProp, _ => RebuildVariants());
            block.TrackPropertyValue(variantProp, _ => RebuildVariants());
            RebuildVariants();
            return block;
        }
    }

    /// <summary>
    /// Bakes a preset into the serialized channel values of one direction.
    /// </summary>
    internal static class UITransitionPresetApplier
    {
        /// <summary>
        /// Applies p re se t.
        /// </summary>
        internal static void ApplyPreset(SerializedProperty direction, SerializedProperty categoryProperty, SerializedProperty variantProperty, UIAnimationType type)
        {
            if (direction == null || categoryProperty == null || variantProperty == null)
                return;

            if (!Enum.TryParse(UITransitionPropertyUtility.GetEnumName(categoryProperty), out UITransitionPresetCategory category))
                return;

            SerializedObject serializedObject = direction.serializedObject;
            Undo.RecordObjects(serializedObject.targetObjects, $"Apply {type} Transition Preset");
            serializedObject.Update();

            if (category == UITransitionPresetCategory.None)
            {
                SetAllChannelEnabled(direction, false);
            }
            else
            {
                UIAnimation animation = UITransitionFactory.BuildPreset(category, variantProperty.intValue, type);
                if (animation == null)
                    return;

                CopyMove(direction.FindPropertyRelative("Move"), animation.Move);
                CopyFade(direction.FindPropertyRelative("Fade"), animation.Fade);
                CopyScale(direction.FindPropertyRelative("Scale"), animation.Scale);
                CopyRotate(direction.FindPropertyRelative("Rotate"), animation.Rotate);
            }

            serializedObject.ApplyModifiedProperties();
            foreach (UnityEngine.Object target in serializedObject.targetObjects)
                EditorUtility.SetDirty(target);
        }

        private static void SetAllChannelEnabled(SerializedProperty direction, bool enabled)
        {
            SetChannelEnabled(direction.FindPropertyRelative("Move"), enabled);
            SetChannelEnabled(direction.FindPropertyRelative("Fade"), enabled);
            SetChannelEnabled(direction.FindPropertyRelative("Scale"), enabled);
            SetChannelEnabled(direction.FindPropertyRelative("Rotate"), enabled);
        }

        private static void SetChannelEnabled(SerializedProperty channel, bool enabled)
        {
            SerializedProperty enable = channel?.FindPropertyRelative("Enable");
            if (enable != null)
                enable.boolValue = enabled;
        }

        private static bool BeginCopyChannel(SerializedProperty target, UIAnimationChannel source)
        {
            if (target == null || source == null)
                return false;

            SetChannelEnabled(target, source.Enabled);
            return source.Enabled;
        }

        private static void CopyTiming(SerializedProperty target, UIAnimationChannel source)
        {
            target.FindPropertyRelative("Duration").floatValue = source.Duration;
            target.FindPropertyRelative("Delay").floatValue = source.StartDelay;
            target.FindPropertyRelative("Ease").intValue = (int)source.Ease;
            target.FindPropertyRelative("PlayMode").intValue = (int)source.PlayMode;
            target.FindPropertyRelative("Loops").intValue = source.Loops;
        }

        private static void CopyMove(SerializedProperty target, UIMoveAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromDirection").intValue = (int)source.FromDirection;
            target.FindPropertyRelative("ToDirection").intValue = (int)source.ToDirection;
            target.FindPropertyRelative("FromCustom").vector3Value = source.FromCustom;
            target.FindPropertyRelative("ToCustom").vector3Value = source.ToCustom;
            target.FindPropertyRelative("FromOffset").vector3Value = source.FromOffset;
            target.FindPropertyRelative("ToOffset").vector3Value = source.ToOffset;
            CopyTiming(target, source);
        }

        private static void CopyFade(SerializedProperty target, UIFadeAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromCustom").floatValue = source.FromCustom;
            target.FindPropertyRelative("ToCustom").floatValue = source.ToCustom;
            target.FindPropertyRelative("FromOffset").floatValue = source.FromOffset;
            target.FindPropertyRelative("ToOffset").floatValue = source.ToOffset;
            CopyTiming(target, source);
        }

        private static void CopyScale(SerializedProperty target, UIScaleAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromCustom").vector2Value = source.FromCustom;
            target.FindPropertyRelative("ToCustom").vector2Value = source.ToCustom;
            target.FindPropertyRelative("FromOffset").vector2Value = source.FromOffset;
            target.FindPropertyRelative("ToOffset").vector2Value = source.ToOffset;
            CopyTiming(target, source);
        }

        private static void CopyRotate(SerializedProperty target, UIRotateAnimation source)
        {
            if (!BeginCopyChannel(target, source))
                return;

            target.FindPropertyRelative("FromType").intValue = (int)source.FromReference;
            target.FindPropertyRelative("ToType").intValue = (int)source.ToReference;
            target.FindPropertyRelative("FromCustom").floatValue = source.FromCustom;
            target.FindPropertyRelative("ToCustom").floatValue = source.ToCustom;
            target.FindPropertyRelative("FromOffset").floatValue = source.FromOffset;
            target.FindPropertyRelative("ToOffset").floatValue = source.ToOffset;
            CopyTiming(target, source);
        }
    }
}
