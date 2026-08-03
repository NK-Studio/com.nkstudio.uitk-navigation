using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Rotate Animation functionality.
    /// </summary>
    [Serializable]
    internal sealed class UIRotateAnimation : UIAnimationChannel
    {
        private const float RestValue = 0f;

        [SerializeField] private UIReferenceValue fromReference = UIReferenceValue.StartValue;
        [SerializeField] private UIReferenceValue toReference = UIReferenceValue.StartValue;

        [SerializeField] private float fromCustom;
        [SerializeField] private float toCustom;

        [SerializeField] private float fromOffset = -8f;
        [SerializeField] private float toOffset;

        private float detachedAngle = -8f;
        private float restAngle;

        public UIReferenceValue FromReference { get => fromReference; set => fromReference = value; }
        public UIReferenceValue ToReference { get => toReference; set => toReference = value; }
        public float FromCustom { get => fromCustom; set => fromCustom = value; }
        public float ToCustom { get => toCustom; set => toCustom = value; }
        public float FromOffset { get => fromOffset; set => fromOffset = value; }
        public float ToOffset { get => toOffset; set => toOffset = value; }
        internal float DetachedAngle => detachedAngle;

        /// <inheritdoc />
        public override void Prepare(VisualElement target, UIAnimationType type)
        {
            float from = Resolve(fromReference, fromCustom, target) + fromOffset;
            float to = Resolve(toReference, toCustom, target) + toOffset;

            if (type == UIAnimationType.Show)
            {
                detachedAngle = from;
                restAngle = to;
            }
            else
            {
                detachedAngle = to;
                restAngle = from;
            }
        }

        private static float Resolve(UIReferenceValue reference, float custom, VisualElement target)
        {
            switch (reference)
            {
                case UIReferenceValue.CustomValue:
                    return custom;
                case UIReferenceValue.CurrentValue:
                    return target != null ? target.resolvedStyle.rotate.angle.ToDegrees() : RestValue;
                default:
                    return RestValue;
            }
        }

        /// <inheritdoc />
        public override void ApplyAt(VisualElement target, UIAnimationType type, float time)
        {
            if (target == null)
                return;

            float from = type == UIAnimationType.Show ? detachedAngle : restAngle;
            float to = type == UIAnimationType.Show ? restAngle : detachedAngle;
            float value = Mathf.LerpUnclamped(from, to, EvaluateAt(time));

            target.style.rotate = new StyleRotate(new Rotate(new Angle(value, AngleUnit.Degree)));
        }

        /// <inheritdoc />
        public override void ResetStyle(VisualElement target)
        {
            if (target != null)
                target.style.rotate = StyleKeyword.Null;
        }

        /// <inheritdoc />
        public override UIAnimationChannel Clone()
        {
            UIRotateAnimation clone = new UIRotateAnimation
            {
                fromReference = fromReference,
                toReference = toReference,
                fromCustom = fromCustom,
                toCustom = toCustom,
                fromOffset = fromOffset,
                toOffset = toOffset,
                detachedAngle = detachedAngle,
                restAngle = restAngle
            };
            CopySharedTo(clone);
            return clone;
        }
    }
}
