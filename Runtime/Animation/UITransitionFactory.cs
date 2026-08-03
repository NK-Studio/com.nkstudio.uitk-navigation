using LitMotion;
using NKStudio.UITKNavigation.Animation.Presets;
using UnityEngine;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Represents Move Channel Options data.
    /// </summary>
    internal struct MoveChannelOptions
    {
        public bool Enabled;
        public UIReferenceValue FromReference;
        public UIReferenceValue ToReference;
        public UIMoveDirection FromDirection;
        public UIMoveDirection ToDirection;
        public Vector3 FromCustom;
        public Vector3 ToCustom;
        public Vector3 FromOffset;
        public Vector3 ToOffset;
        public float Duration;
        public float Delay;
        public Ease Ease;
        public UIAnimationPlayMode PlayMode;
        public int Loops;

        public static MoveChannelOptions DefaultShow => new MoveChannelOptions
        {
            Enabled = false,
            FromReference = UIReferenceValue.StartValue,
            ToReference = UIReferenceValue.StartValue,
            FromDirection = UIMoveDirection.Bottom,
            ToDirection = UIMoveDirection.Bottom,
            FromCustom = Vector3.zero,
            ToCustom = Vector3.zero,
            FromOffset = Vector3.zero,
            ToOffset = Vector3.zero,
            Duration = 0.3f,
            Delay = 0f,
            Ease = Ease.InOutSine
        };
    }

    /// <summary>
    /// Represents Fade Channel Options data.
    /// </summary>
    internal struct FadeChannelOptions
    {
        public bool Enabled;
        public UIReferenceValue FromReference;
        public UIReferenceValue ToReference;
        public float FromCustom;
        public float ToCustom;
        public float FromOffset;
        public float ToOffset;
        public float Duration;
        public float Delay;
        public Ease Ease;
        public UIAnimationPlayMode PlayMode;
        public int Loops;

        public static FadeChannelOptions DefaultShow => new FadeChannelOptions
        {
            Enabled = false,
            FromReference = UIReferenceValue.StartValue,
            ToReference = UIReferenceValue.StartValue,
            FromCustom = 1f,
            ToCustom = 1f,
            FromOffset = 0f,
            ToOffset = 0f,
            Duration = 0.3f,
            Delay = 0f,
            Ease = Ease.InOutSine
        };
    }

    /// <summary>
    /// Represents Scale Channel Options data.
    /// </summary>
    internal struct ScaleChannelOptions
    {
        public bool Enabled;
        public UIReferenceValue FromReference;
        public UIReferenceValue ToReference;
        public Vector2 FromCustom;
        public Vector2 ToCustom;
        public Vector2 FromOffset;
        public Vector2 ToOffset;
        public float Duration;
        public float Delay;
        public Ease Ease;
        public UIAnimationPlayMode PlayMode;
        public int Loops;

        public static ScaleChannelOptions DefaultShow => new ScaleChannelOptions
        {
            Enabled = false,
            FromReference = UIReferenceValue.StartValue,
            ToReference = UIReferenceValue.StartValue,
            FromCustom = Vector2.one,
            ToCustom = Vector2.one,
            FromOffset = Vector2.zero,
            ToOffset = Vector2.zero,
            Duration = 0.3f,
            Delay = 0f,
            Ease = Ease.InOutSine
        };
    }

    /// <summary>
    /// Represents Rotate Channel Options data.
    /// </summary>
    internal struct RotateChannelOptions
    {
        public bool Enabled;
        public UIReferenceValue FromReference;
        public UIReferenceValue ToReference;
        public float FromCustom;
        public float ToCustom;
        public float FromOffset;
        public float ToOffset;
        public float Duration;
        public float Delay;
        public Ease Ease;
        public UIAnimationPlayMode PlayMode;
        public int Loops;

        public static RotateChannelOptions DefaultShow => new RotateChannelOptions
        {
            Enabled = false,
            FromReference = UIReferenceValue.StartValue,
            ToReference = UIReferenceValue.StartValue,
            FromCustom = 0f,
            ToCustom = 0f,
            FromOffset = -15f,
            ToOffset = 0f,
            Duration = 0.3f,
            Delay = 0f,
            Ease = Ease.InOutSine
        };
    }

    /// <summary>
    /// Provides UI Transition Factory functionality.
    /// </summary>
    internal static class UITransitionFactory
    {
        /// <summary>
        /// Builds member.
        /// </summary>
        public static UIAnimation Build(
            UITransitionPresetCategory category,
            int variant,
            UIAnimationType type,
            MoveChannelOptions moveOpts,
            FadeChannelOptions fadeOpts,
            ScaleChannelOptions scaleOpts,
            RotateChannelOptions rotateOpts)
        {
            UIAnimation animation = UITransitionPresetLibrary.Build(category, variant, type)
                                    ?? new UIAnimation { Type = type };

            if (moveOpts.Enabled)
            {
                BuildMoveChannel(moveOpts, animation.Move);
            }

            if (fadeOpts.Enabled)
            {
                BuildFadeChannel(fadeOpts, animation.Fade);
            }

            if (scaleOpts.Enabled)
            {
                BuildScaleChannel(scaleOpts, animation.Scale);
            }

            if (rotateOpts.Enabled)
            {
                BuildRotateChannel(rotateOpts, animation.Rotate);
            }

            return animation.HasEnabledChannel ? animation : null;
        }

        /// <summary>
        /// Builds p re se t.
        /// </summary>
        public static UIAnimation BuildPreset(
            UITransitionPresetCategory category,
            int variant,
            UIAnimationType type)
        {
            return UITransitionPresetLibrary.Build(category, variant, type);
        }
        #region Channel Builders

        private static void BuildMoveChannel(MoveChannelOptions opts, UIMoveAnimation target)
        {
            target.Enabled = true;
            target.FromReference = opts.FromReference;
            target.ToReference = opts.ToReference;
            target.FromDirection = opts.FromDirection;
            target.ToDirection = opts.ToDirection;
            target.FromCustom = opts.FromCustom;
            target.ToCustom = opts.ToCustom;
            target.FromOffset = opts.FromOffset;
            target.ToOffset = opts.ToOffset;
            target.Duration = opts.Duration;
            target.StartDelay = opts.Delay;
            target.EaseType = UIAnimationEaseType.Ease;
            target.Ease = opts.Ease;
            target.PlayMode = opts.PlayMode;
            target.Loops = opts.Loops;
        }

        private static void BuildFadeChannel(FadeChannelOptions opts, UIFadeAnimation target)
        {
            target.Enabled = true;
            target.FromReference = opts.FromReference;
            target.ToReference = opts.ToReference;
            target.FromCustom = opts.FromCustom;
            target.ToCustom = opts.ToCustom;
            target.FromOffset = opts.FromOffset;
            target.ToOffset = opts.ToOffset;
            target.Duration = opts.Duration;
            target.StartDelay = opts.Delay;
            target.EaseType = UIAnimationEaseType.Ease;
            target.Ease = opts.Ease;
            target.PlayMode = opts.PlayMode;
            target.Loops = opts.Loops;
        }

        private static void BuildScaleChannel(ScaleChannelOptions opts, UIScaleAnimation target)
        {
            target.Enabled = true;
            target.FromReference = opts.FromReference;
            target.ToReference = opts.ToReference;
            target.FromCustom = opts.FromCustom;
            target.ToCustom = opts.ToCustom;
            target.FromOffset = opts.FromOffset;
            target.ToOffset = opts.ToOffset;
            target.Duration = opts.Duration;
            target.StartDelay = opts.Delay;
            target.EaseType = UIAnimationEaseType.Ease;
            target.Ease = opts.Ease;
            target.PlayMode = opts.PlayMode;
            target.Loops = opts.Loops;
        }

        private static void BuildRotateChannel(RotateChannelOptions opts, UIRotateAnimation target)
        {
            target.Enabled = true;
            target.FromReference = opts.FromReference;
            target.ToReference = opts.ToReference;
            target.FromCustom = opts.FromCustom;
            target.ToCustom = opts.ToCustom;
            target.FromOffset = opts.FromOffset;
            target.ToOffset = opts.ToOffset;
            target.Duration = opts.Duration;
            target.StartDelay = opts.Delay;
            target.EaseType = UIAnimationEaseType.Ease;
            target.Ease = opts.Ease;
            target.PlayMode = opts.PlayMode;
            target.Loops = opts.Loops;
        }

        #endregion

    }
}
