using System;
using System.Collections.Generic;
using UnityEngine;

namespace NKStudio.UITKNavigation.Animation.Presets
{
    /// <summary>
    /// Provides UI Transition Preset Library functionality.
    /// </summary>
    internal static class UITransitionPresetLibrary
    {
        private static readonly Dictionary<UITransitionPresetCategory, PresetVariant[]> Tables =
            new Dictionary<UITransitionPresetCategory, PresetVariant[]>();

        private static AnimationCurve[] _curves;

        /// <summary>
        /// Gets the categories.
        /// </summary>
        public static IReadOnlyList<UITransitionPresetCategory> Categories { get; } =
            (UITransitionPresetCategory[])Enum.GetValues(typeof(UITransitionPresetCategory));

        /// <summary>
        /// Gets the variant count.
        /// </summary>
        public static int GetVariantCount(UITransitionPresetCategory category)
        {
            PresetVariant[] table = GetTable(category);
            return table?.Length ?? 0;
        }

        /// <summary>
        /// Gets the variant name.
        /// </summary>
        public static string GetVariantName(UITransitionPresetCategory category, int variant)
        {
            PresetVariant[] table = GetTable(category);
            if (table == null || variant < 1 || variant > table.Length)
                return string.Empty;

            return table[variant - 1].Name;
        }

        /// <summary>
        /// Builds member.
        /// </summary>
        internal static UIAnimation Build(
            UITransitionPresetCategory category,
            int variant,
            UIAnimationType type)
        {
            PresetVariant[] table = GetTable(category);
            if (table == null || variant < 1 || variant > table.Length)
                return null;

            PresetVariant preset = table[variant - 1];
            var animation = new UIAnimation { Type = type };

            ApplyMove(preset.GetMove(type), animation.Move);
            ApplyRotate(preset.GetRotate(type), animation.Rotate);
            ApplyScale(preset.GetScale(type), animation.Scale);
            ApplyFade(preset.GetFade(type), animation.Fade);

            return animation.HasEnabledChannel ? animation : null;
        }

        internal static AnimationCurve GetCurve(int curveId)
        {
            _curves ??= UITransitionPresetCurveTable.Create();
            if (curveId < 0 || curveId >= _curves.Length)
                return null;

            return new AnimationCurve(_curves[curveId].keys);
        }

        private static void ApplyMove(PresetChannel source, UIMoveAnimation target)
        {
            if (!source.Enabled)
                return;

            source.ApplyTimingTo(target);
            target.FromReference = source.FromReference;
            target.ToReference = source.ToReference;
            target.FromCustom = source.FromCustom;
            target.ToCustom = source.ToCustom;
            target.FromOffset = source.FromOffset;
            target.ToOffset = source.ToOffset;
            target.FromDirection = source.FromDirection;
            target.ToDirection = source.ToDirection;
        }

        private static void ApplyRotate(PresetChannel source, UIRotateAnimation target)
        {
            if (!source.Enabled)
                return;

            source.ApplyTimingTo(target);
            target.FromReference = source.FromReference;
            target.ToReference = source.ToReference;

            target.FromCustom = source.FromCustom.z;
            target.ToCustom = source.ToCustom.z;
            target.FromOffset = source.FromOffset.z;
            target.ToOffset = source.ToOffset.z;
        }

        private static void ApplyScale(PresetChannel source, UIScaleAnimation target)
        {
            if (!source.Enabled)
                return;

            source.ApplyTimingTo(target);
            target.FromReference = source.FromReference;
            target.ToReference = source.ToReference;
            target.FromCustom = new Vector2(source.FromCustom.x, source.FromCustom.y);
            target.ToCustom = new Vector2(source.ToCustom.x, source.ToCustom.y);
            target.FromOffset = new Vector2(source.FromOffset.x, source.FromOffset.y);
            target.ToOffset = new Vector2(source.ToOffset.x, source.ToOffset.y);
        }

        private static void ApplyFade(PresetChannel source, UIFadeAnimation target)
        {
            if (!source.Enabled)
                return;

            source.ApplyTimingTo(target);
            target.FromReference = source.FromReference;
            target.ToReference = source.ToReference;
            target.FromCustom = source.FromCustom.x;
            target.ToCustom = source.ToCustom.x;
            target.FromOffset = source.FromOffset.x;
            target.ToOffset = source.ToOffset.x;
        }

        private static PresetVariant[] GetTable(UITransitionPresetCategory category)
        {
            if (category == UITransitionPresetCategory.None)
                return null;

            if (Tables.TryGetValue(category, out PresetVariant[] table))
                return table;

            table = CreateTable(category);
            Tables[category] = table;
            return table;
        }

        private static PresetVariant[] CreateTable(UITransitionPresetCategory category)
        {
            return category switch
            {
                UITransitionPresetCategory.Back => UITransitionPresetBackTable.Create(),
                UITransitionPresetCategory.Basic1 => UITransitionPresetBasic1Table.Create(),
                UITransitionPresetCategory.Basic2 => UITransitionPresetBasic2Table.Create(),
                UITransitionPresetCategory.Bounce => UITransitionPresetBounceTable.Create(),
                UITransitionPresetCategory.Default => UITransitionPresetDefaultTable.Create(),
                UITransitionPresetCategory.Discrete => UITransitionPresetDiscreteTable.Create(),
                UITransitionPresetCategory.Drift => UITransitionPresetDriftTable.Create(),
                UITransitionPresetCategory.Drop => UITransitionPresetDropTable.Create(),
                UITransitionPresetCategory.Fade => UITransitionPresetFadeTable.Create(),
                UITransitionPresetCategory.Flip => UITransitionPresetFlipTable.Create(),
                UITransitionPresetCategory.Ghost => UITransitionPresetGhostTable.Create(),
                UITransitionPresetCategory.Gradual => UITransitionPresetGradualTable.Create(),
                UITransitionPresetCategory.Jelly => UITransitionPresetJellyTable.Create(),
                UITransitionPresetCategory.Organic1 => UITransitionPresetOrganic1Table.Create(),
                UITransitionPresetCategory.Organic2 => UITransitionPresetOrganic2Table.Create(),
                UITransitionPresetCategory.Rotate => UITransitionPresetRotateTable.Create(),
                UITransitionPresetCategory.Shake => UITransitionPresetShakeTable.Create(),
                UITransitionPresetCategory.Slide1 => UITransitionPresetSlide1Table.Create(),
                UITransitionPresetCategory.Slide2 => UITransitionPresetSlide2Table.Create(),
                UITransitionPresetCategory.Spin => UITransitionPresetSpinTable.Create(),
                UITransitionPresetCategory.Zoom => UITransitionPresetZoomTable.Create(),
                _ => null
            };
        }
    }
}
