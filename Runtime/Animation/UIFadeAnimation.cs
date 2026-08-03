using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Fade Animation functionality.
    /// </summary>
    [Serializable]
    internal sealed class UIFadeAnimation : UIAnimationChannel
    {
        /// <summary>
        /// Defines the rest value value.
        /// </summary>
        private const float RestValue = 1f;

        [SerializeField] private UIReferenceValue fromReference = UIReferenceValue.StartValue;
        [SerializeField] private UIReferenceValue toReference = UIReferenceValue.StartValue;

        [SerializeField, Range(0f, 1f)] private float fromCustom = 1f;
        [SerializeField, Range(0f, 1f)] private float toCustom = 1f;

        [SerializeField] private float fromOffset;
        [SerializeField] private float toOffset;

        private float detachedOpacity;
        private float restOpacity = 1f;

        public UIReferenceValue FromReference { get => fromReference; set => fromReference = value; }
        public UIReferenceValue ToReference { get => toReference; set => toReference = value; }
        public float FromCustom { get => fromCustom; set => fromCustom = Mathf.Clamp01(value); }
        public float ToCustom { get => toCustom; set => toCustom = Mathf.Clamp01(value); }
        public float FromOffset { get => fromOffset; set => fromOffset = value; }
        public float ToOffset { get => toOffset; set => toOffset = value; }

        /// <summary>
        /// Performs the approximately operation.
        /// </summary>
        public bool RestIsOpaque => Mathf.Approximately(restOpacity, 1f);

        /// <inheritdoc />
        public override void Prepare(VisualElement target, UIAnimationType type)
        {
            float from = Mathf.Clamp01(Resolve(fromReference, fromCustom, target) + fromOffset);
            float to = Mathf.Clamp01(Resolve(toReference, toCustom, target) + toOffset);

            if (type == UIAnimationType.Show)
            {
                detachedOpacity = from;
                restOpacity = to;
            }
            else
            {
                detachedOpacity = to;
                restOpacity = from;
            }
        }

        private static float Resolve(UIReferenceValue reference, float custom, VisualElement target)
        {
            switch (reference)
            {
                case UIReferenceValue.CustomValue: return custom;
                case UIReferenceValue.CurrentValue: return target?.resolvedStyle.opacity ?? RestValue;
                default: return RestValue;
            }
        }

        /// <inheritdoc />
        public override void ApplyAt(VisualElement target, UIAnimationType type, float time)
        {
            if (target == null)
                return;

            float from = type == UIAnimationType.Show ? detachedOpacity : restOpacity;
            float to = type == UIAnimationType.Show ? restOpacity : detachedOpacity;
            target.style.opacity = Mathf.LerpUnclamped(from, to, EvaluateAt(time));
        }

        /// <inheritdoc />
        public override void ResetStyle(VisualElement target)
        {
            if (target != null)
                target.style.opacity = StyleKeyword.Null;
        }

        /// <inheritdoc />
        public override UIAnimationChannel Clone()
        {
            UIFadeAnimation clone = new UIFadeAnimation
            {
                fromReference = fromReference,
                toReference = toReference,
                fromCustom = fromCustom,
                toCustom = toCustom,
                fromOffset = fromOffset,
                toOffset = toOffset,
                detachedOpacity = detachedOpacity,
                restOpacity = restOpacity
            };
            CopySharedTo(clone);
            return clone;
        }
    }
}
