using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Provides UI Scale Animation functionality.
    /// </summary>
    [Serializable]
    internal sealed class UIScaleAnimation : UIAnimationChannel
    {
        private static readonly Vector2 RestValue = Vector2.one;

        [SerializeField] private UIReferenceValue fromReference = UIReferenceValue.StartValue;
        [SerializeField] private UIReferenceValue toReference = UIReferenceValue.StartValue;

        [SerializeField] private Vector2 fromCustom = Vector2.one;
        [SerializeField] private Vector2 toCustom = Vector2.one;

        [SerializeField] private Vector2 fromOffset;
        [SerializeField] private Vector2 toOffset;

        private Vector2 detachedScale = Vector2.zero;
        private Vector2 restScale = Vector2.one;

        public UIReferenceValue FromReference { get => fromReference; set => fromReference = value; }
        public UIReferenceValue ToReference { get => toReference; set => toReference = value; }
        public Vector2 FromCustom { get => fromCustom; set => fromCustom = value; }
        public Vector2 ToCustom { get => toCustom; set => toCustom = value; }
        public Vector2 FromOffset { get => fromOffset; set => fromOffset = value; }
        public Vector2 ToOffset { get => toOffset; set => toOffset = value; }
        internal Vector2 DetachedScale => detachedScale;

        /// <inheritdoc />
        public override void Prepare(VisualElement target, UIAnimationType type)
        {
            Vector2 from = Resolve(fromReference, fromCustom, target) + fromOffset;
            Vector2 to = Resolve(toReference, toCustom, target) + toOffset;

            if (type == UIAnimationType.Show)
            {
                detachedScale = from;
                restScale = to;
            }
            else
            {
                detachedScale = to;
                restScale = from;
            }
        }

        private static Vector2 Resolve(UIReferenceValue reference, Vector2 custom, VisualElement target)
        {
            switch (reference)
            {
                case UIReferenceValue.CustomValue:
                    return custom;
                case UIReferenceValue.CurrentValue:
                    if (target == null) return RestValue;
                    Vector3 s = target.resolvedStyle.scale.value;
                    return new Vector2(s.x, s.y);
                default:
                    return RestValue;
            }
        }

        /// <inheritdoc />
        public override void ApplyAt(VisualElement target, UIAnimationType type, float time)
        {
            if (target == null)
                return;

            Vector2 from = type == UIAnimationType.Show ? detachedScale : restScale;
            Vector2 to = type == UIAnimationType.Show ? restScale : detachedScale;
            Vector2 value = Vector2.LerpUnclamped(from, to, EvaluateAt(time));

            target.style.scale = new StyleScale(new Scale(new Vector3(value.x, value.y, 1f)));
        }

        /// <inheritdoc />
        public override void ResetStyle(VisualElement target)
        {
            if (target != null)
                target.style.scale = StyleKeyword.Null;
        }

        /// <inheritdoc />
        public override UIAnimationChannel Clone()
        {
            UIScaleAnimation clone = new UIScaleAnimation
            {
                fromReference = fromReference,
                toReference = toReference,
                fromCustom = fromCustom,
                toCustom = toCustom,
                fromOffset = fromOffset,
                toOffset = toOffset,
                detachedScale = detachedScale,
                restScale = restScale
            };
            CopySharedTo(clone);
            return clone;
        }
    }
}
