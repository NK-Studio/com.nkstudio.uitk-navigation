using LitMotion;
using UnityEngine;

namespace NKStudio.UITKNavigation.Animation.Presets
{
    /// <summary>
    /// Represents Preset Channel data.
    /// </summary>
    internal readonly struct PresetChannel
    {
        /// <summary>
        /// Gets the disabled.
        /// </summary>
        internal static PresetChannel Disabled => default;

        internal readonly bool Enabled;
        internal readonly UIReferenceValue FromReference;
        internal readonly UIReferenceValue ToReference;
        internal readonly Vector3 FromCustom;
        internal readonly Vector3 ToCustom;
        internal readonly Vector3 FromOffset;
        internal readonly Vector3 ToOffset;
        internal readonly UIMoveDirection FromDirection;
        internal readonly UIMoveDirection ToDirection;
        internal readonly Ease Ease;

        /// <summary>
        /// Gets the curve id.
        /// </summary>
        internal readonly int CurveId;

        internal readonly float StartDelay;
        internal readonly float Duration;
        internal readonly int Loops;
        internal readonly UIAnimationPlayMode PlayMode;

        internal PresetChannel(
            UIReferenceValue fromReference,
            UIReferenceValue toReference,
            Vector3 fromCustom,
            Vector3 toCustom,
            Vector3 fromOffset,
            Vector3 toOffset,
            UIMoveDirection fromDirection,
            UIMoveDirection toDirection,
            Ease ease,
            int curveId,
            float startDelay,
            float duration,
            int loops,
            UIAnimationPlayMode playMode)
        {
            Enabled = true;
            FromReference = fromReference;
            ToReference = toReference;
            FromCustom = fromCustom;
            ToCustom = toCustom;
            FromOffset = fromOffset;
            ToOffset = toOffset;
            FromDirection = fromDirection;
            ToDirection = toDirection;
            Ease = ease;
            CurveId = curveId;
            StartDelay = startDelay;
            Duration = duration;
            Loops = loops;
            PlayMode = playMode;
        }

        /// <summary>
        /// Applies t im in gt o.
        /// </summary>
        internal void ApplyTimingTo(UIAnimationChannel target)
        {
            target.Enabled = true;
            target.StartDelay = StartDelay;
            target.Duration = Duration;
            target.Loops = Loops;
            target.PlayMode = PlayMode;

            if (CurveId >= 0)
            {
                target.EaseType = UIAnimationEaseType.AnimationCurve;
                target.Curve = UITransitionPresetLibrary.GetCurve(CurveId);
            }
            else
            {
                target.EaseType = UIAnimationEaseType.Ease;
                target.Ease = Ease;
            }
        }
    }
}
