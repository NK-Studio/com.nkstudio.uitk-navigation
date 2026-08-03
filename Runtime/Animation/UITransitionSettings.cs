using System;
using LitMotion;
using NKStudio.UITKNavigation.Animation.Presets;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Move Settings functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UIMoveSettings
    {
        [UxmlAttribute("enable")]
        public bool Enable { get; set; }

        [UxmlAttribute("from-type")]
        public UIReferenceValue FromType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("to-type")]
        public UIReferenceValue ToType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("from-direction")]
        public UIMoveDirection FromDirection { get; set; } = UIMoveDirection.Bottom;

        [UxmlAttribute("to-direction")]
        public UIMoveDirection ToDirection { get; set; } = UIMoveDirection.Bottom;

        [UxmlAttribute("from-custom")]
        public Vector3 FromCustom { get; set; }

        [UxmlAttribute("to-custom")]
        public Vector3 ToCustom { get; set; }

        [UxmlAttribute("from-offset")]
        public Vector3 FromOffset { get; set; }

        [UxmlAttribute("to-offset")]
        public Vector3 ToOffset { get; set; }

        [UxmlAttribute("duration")]
        public float Duration { get; set; } = 0.3f;

        [UxmlAttribute("delay")]
        public float Delay { get; set; } = 0f;

        [UxmlAttribute("ease")]
        public Ease Ease { get; set; } = Ease.InOutSine;

        [UxmlAttribute("play-mode")]
        public UIAnimationPlayMode PlayMode { get; set; } = UIAnimationPlayMode.Normal;

        [UxmlAttribute("loops")]
        public int Loops { get; set; }


        public MoveChannelOptions ToOptions()
        {
            return new MoveChannelOptions
            {
                Enabled = Enable,
                FromReference = FromType,
                ToReference = ToType,
                FromDirection = FromDirection,
                ToDirection = ToDirection,
                FromCustom = FromCustom,
                ToCustom = ToCustom,
                FromOffset = FromOffset,
                ToOffset = ToOffset,
                Duration = Duration,
                Delay = Delay,
                Ease = Ease,
                PlayMode = PlayMode,
                Loops = Loops
            };
        }
    }

    /// <summary>
    /// Provides UI Fade Settings functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UIFadeSettings
    {
        [UxmlAttribute("enable")]
        public bool Enable { get; set; }

        [UxmlAttribute("from-type")]
        public UIReferenceValue FromType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("to-type")]
        public UIReferenceValue ToType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("from-custom")]
        public float FromCustom { get; set; } = 1f;

        [UxmlAttribute("to-custom")]
        public float ToCustom { get; set; } = 1f;

        [UxmlAttribute("from-offset")]
        public float FromOffset { get; set; } = 0f;

        [UxmlAttribute("to-offset")]
        public float ToOffset { get; set; } = 0f;

        [UxmlAttribute("duration")]
        public float Duration { get; set; } = 0.3f;

        [UxmlAttribute("delay")]
        public float Delay { get; set; } = 0f;

        [UxmlAttribute("ease")]
        public Ease Ease { get; set; } = Ease.InOutSine;

        [UxmlAttribute("play-mode")]
        public UIAnimationPlayMode PlayMode { get; set; } = UIAnimationPlayMode.Normal;

        [UxmlAttribute("loops")]
        public int Loops { get; set; }


        public FadeChannelOptions ToOptions()
        {
            return new FadeChannelOptions
            {
                Enabled = Enable,
                FromReference = FromType,
                ToReference = ToType,
                FromCustom = FromCustom,
                ToCustom = ToCustom,
                FromOffset = FromOffset,
                ToOffset = ToOffset,
                Duration = Duration,
                Delay = Delay,
                Ease = Ease,
                PlayMode = PlayMode,
                Loops = Loops
            };
        }
    }

    /// <summary>
    /// Provides UI Scale Settings functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UIScaleSettings
    {
        [UxmlAttribute("enable")]
        public bool Enable { get; set; }

        [UxmlAttribute("from-type")]
        public UIReferenceValue FromType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("to-type")]
        public UIReferenceValue ToType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("from-custom")]
        public Vector2 FromCustom { get; set; } = Vector2.one;

        [UxmlAttribute("to-custom")]
        public Vector2 ToCustom { get; set; } = Vector2.one;

        [UxmlAttribute("from-offset")]
        public Vector2 FromOffset { get; set; }

        [UxmlAttribute("to-offset")]
        public Vector2 ToOffset { get; set; }

        [UxmlAttribute("duration")]
        public float Duration { get; set; } = 0.3f;

        [UxmlAttribute("delay")]
        public float Delay { get; set; } = 0f;

        [UxmlAttribute("ease")]
        public Ease Ease { get; set; } = Ease.InOutSine;

        [UxmlAttribute("play-mode")]
        public UIAnimationPlayMode PlayMode { get; set; } = UIAnimationPlayMode.Normal;

        [UxmlAttribute("loops")]
        public int Loops { get; set; }


        public ScaleChannelOptions ToOptions()
        {
            return new ScaleChannelOptions
            {
                Enabled = Enable,
                FromReference = FromType,
                ToReference = ToType,
                FromCustom = FromCustom,
                ToCustom = ToCustom,
                FromOffset = FromOffset,
                ToOffset = ToOffset,
                Duration = Duration,
                Delay = Delay,
                Ease = Ease,
                PlayMode = PlayMode,
                Loops = Loops
            };
        }
    }

    /// <summary>
    /// Provides UI Rotate Settings functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UIRotateSettings
    {
        [UxmlAttribute("enable")]
        public bool Enable { get; set; }

        [UxmlAttribute("from-type")]
        public UIReferenceValue FromType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("to-type")]
        public UIReferenceValue ToType { get; set; } = UIReferenceValue.StartValue;

        [UxmlAttribute("from-custom")]
        public float FromCustom { get; set; } = 0f;

        [UxmlAttribute("to-custom")]
        public float ToCustom { get; set; } = 0f;

        [UxmlAttribute("from-offset")]
        public float FromOffset { get; set; } = -15f;

        [UxmlAttribute("to-offset")]
        public float ToOffset { get; set; } = 0f;

        [UxmlAttribute("duration")]
        public float Duration { get; set; } = 0.3f;

        [UxmlAttribute("delay")]
        public float Delay { get; set; } = 0f;

        [UxmlAttribute("ease")]
        public Ease Ease { get; set; } = Ease.InOutSine;

        [UxmlAttribute("play-mode")]
        public UIAnimationPlayMode PlayMode { get; set; } = UIAnimationPlayMode.Normal;

        [UxmlAttribute("loops")]
        public int Loops { get; set; }


        public RotateChannelOptions ToOptions()
        {
            return new RotateChannelOptions
            {
                Enabled = Enable,
                FromReference = FromType,
                ToReference = ToType,
                FromCustom = FromCustom,
                ToCustom = ToCustom,
                FromOffset = FromOffset,
                ToOffset = ToOffset,
                Duration = Duration,
                Delay = Delay,
                Ease = Ease,
                PlayMode = PlayMode,
                Loops = Loops
            };
        }
    }

    /// <summary>
    /// Provides UI Transition Settings functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UITransitionSettings
    {
        /// <summary>
        /// Gets or sets the preset category.
        /// </summary>
        [UxmlAttribute("preset-category")]
        public UITransitionPresetCategory PresetCategory { get; set; } = UITransitionPresetCategory.None;

        /// <summary>
        /// Gets or sets the preset variant.
        /// </summary>
        [UxmlAttribute("preset-variant")]
        public int PresetVariant { get; set; } = 1;

        [UxmlObjectReference("move")]
        public UIMoveSettings Move { get; set; } = new UIMoveSettings();

        [UxmlObjectReference("fade")]
        public UIFadeSettings Fade { get; set; } = new UIFadeSettings();

        [UxmlObjectReference("scale")]
        public UIScaleSettings Scale { get; set; } = new UIScaleSettings();

        [UxmlObjectReference("rotate")]
        public UIRotateSettings Rotate { get; set; } = new UIRotateSettings();

        internal UIAnimation BuildAnimation(UIAnimationType type)
        {
            return UITransitionFactory.Build(
                PresetCategory,
                PresetVariant,
                type,
                Move?.ToOptions() ?? default,
                Fade?.ToOptions() ?? default,
                Scale?.ToOptions() ?? default,
                Rotate?.ToOptions() ?? default);
        }
    }
}
