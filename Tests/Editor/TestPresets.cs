using LitMotion;
using NKStudio.UITKNavigation.Animation;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides Test Presets functionality.
    /// </summary>
    internal static class TestPresets
    {
        /// <summary>
        /// Creates member.
        /// </summary>
        public static UIAnimationPreset Create(float duration, float moveStartDelay)
        {
            UIAnimationPreset preset = ScriptableObject.CreateInstance<UIAnimationPreset>();
            preset.SetAnimations(
                BuildAnimation(UIAnimationType.Show, duration, moveStartDelay),
                BuildAnimation(UIAnimationType.Hide, duration, moveStartDelay));
            return preset;
        }

        /// <summary>
        /// Creates a ni ma ti on.
        /// </summary>
        public static UIAnimation CreateAnimation(
            UIAnimationType type,
            bool move = false,
            bool rotate = false,
            bool scale = false,
            bool fade = false)
        {
            UIAnimation animation = new UIAnimation { Type = type };

            animation.Move.Enabled = move;
            animation.Rotate.Enabled = rotate;
            animation.Scale.Enabled = scale;
            animation.Fade.Enabled = fade;

            ApplyTiming(animation.Move, 0.1f, 0f);
            ApplyTiming(animation.Rotate, 0.1f, 0f);
            ApplyTiming(animation.Scale, 0.1f, 0f);
            ApplyTiming(animation.Fade, 0.1f, 0f);

            return animation;
        }

        private static UIAnimation BuildAnimation(UIAnimationType type, float duration, float moveStartDelay)
        {
            UIAnimation animation = new UIAnimation { Type = type };

            animation.Move.Enabled = true;
            animation.Move.FromDirection = UIMoveDirection.CustomPosition;
            animation.Move.ToDirection = UIMoveDirection.CustomPosition;
            animation.Move.FromCustom = new Vector3(0f, 100f, 0f);
            animation.Move.ToCustom = new Vector3(0f, 100f, 0f);
            ApplyTiming(animation.Move, duration, moveStartDelay);

            animation.Fade.Enabled = true;
            animation.Fade.FromReference = UIReferenceValue.CustomValue;
            animation.Fade.FromCustom = 0f;
            animation.Fade.ToReference = UIReferenceValue.StartValue;
            ApplyTiming(animation.Fade, duration, 0f);

            return animation;
        }

        private static void ApplyTiming(UIAnimationChannel channel, float duration, float startDelay)
        {
            channel.EaseType = UIAnimationEaseType.Ease;
            channel.Ease = Ease.Linear;
            channel.Duration = duration;
            channel.StartDelay = startDelay;
        }
    }
}
