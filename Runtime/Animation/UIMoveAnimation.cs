using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Drives style.translate with Doozy-compatible Show/Hide move semantics.
    /// Show moves Direction to ReferenceValue; Hide moves ReferenceValue to Direction.
    /// Custom positions and offsets are expressed in pixels.
    /// </summary>
    [Serializable]
    internal sealed class UIMoveAnimation : UIAnimationChannel
    {
        [SerializeField] private UIReferenceValue fromReference = UIReferenceValue.StartValue;
        [SerializeField] private UIReferenceValue toReference = UIReferenceValue.StartValue;
        [SerializeField] private UIMoveDirection fromDirection = UIMoveDirection.Bottom;
        [SerializeField] private UIMoveDirection toDirection = UIMoveDirection.Bottom;
        [SerializeField] private Vector3 fromCustom;
        [SerializeField] private Vector3 toCustom;
        [SerializeField] private Vector3 fromOffset;
        [SerializeField] private Vector3 toOffset;

        private bool startValueCaptured;
        private Vector3 startValue;
        private Vector3 preparedFrom;
        private Vector3 preparedTo;

        public UIReferenceValue FromReference { get => fromReference; set => fromReference = value; }
        public UIReferenceValue ToReference { get => toReference; set => toReference = value; }
        public UIMoveDirection FromDirection { get => fromDirection; set => fromDirection = value; }
        public UIMoveDirection ToDirection { get => toDirection; set => toDirection = value; }
        public Vector3 FromCustom { get => fromCustom; set => fromCustom = value; }
        public Vector3 ToCustom { get => toCustom; set => toCustom = value; }
        public Vector3 FromOffset { get => fromOffset; set => fromOffset = value; }
        public Vector3 ToOffset { get => toOffset; set => toOffset = value; }

        /// <summary>
        /// Performs the invalidate start value operation.
        /// </summary>
        public void InvalidateStartValue()
        {
            startValueCaptured = false;
        }

        internal static bool HasValidLayout(VisualElement target)
        {
            if (target?.parent == null)
                return false;

            Rect parentRect = target.parent.contentRect;
            Rect targetRect = target.layout;
            return IsFiniteRect(parentRect)
                   && IsFiniteRect(targetRect)
                   && parentRect.width > 0f
                   && parentRect.height > 0f
                   && targetRect.width > 0f
                   && targetRect.height > 0f;
        }

        public override void Prepare(VisualElement target, UIAnimationType type)
        {
            Vector2 scale = target != null
                ? new Vector2(target.resolvedStyle.scale.value.x, target.resolvedStyle.scale.value.y)
                : Vector2.one;
            float angle = target != null ? target.resolvedStyle.rotate.angle.ToDegrees() : 0f;
            Prepare(target, type, scale, angle);
        }

        internal void Prepare(VisualElement target, UIAnimationType type, Vector2 directionScale, float directionAngle)
        {
            if (target == null)
                return;

            Vector3 current = target.resolvedStyle.translate;
            if (!startValueCaptured)
            {
                startValue = current;
                startValueCaptured = true;
            }

            if (type == UIAnimationType.Show)
            {
                preparedTo = ResolveReference(toReference, toCustom, toOffset, current);
                preparedFrom = ResolveDirection(
                    target,
                    fromDirection,
                    fromCustom,
                    fromOffset,
                    preparedTo,
                    directionScale,
                    directionAngle);
            }
            else
            {
                preparedFrom = ResolveReference(fromReference, fromCustom, fromOffset, current);
                preparedTo = ResolveDirection(
                    target,
                    toDirection,
                    toCustom,
                    toOffset,
                    preparedFrom,
                    directionScale,
                    directionAngle);
            }
        }

        private Vector3 ResolveReference(
            UIReferenceValue reference,
            Vector3 custom,
            Vector3 offset,
            Vector3 current)
        {
            return reference switch
            {
                UIReferenceValue.CustomValue => custom,
                UIReferenceValue.CurrentValue => current + offset,
                _ => startValue + offset
            };
        }

        private static Vector3 ResolveDirection(
            VisualElement target,
            UIMoveDirection direction,
            Vector3 custom,
            Vector3 offset,
            Vector3 anchor,
            Vector2 scale,
            float angle)
        {
            if (direction == UIMoveDirection.CustomPosition)
                return custom;

            Rect targetRect = GetTransformedLayoutRect(target, scale, angle);
            Rect parentRect = target?.parent != null ? target.parent.contentRect : default;

            if (!IsFiniteRect(parentRect)
                || !IsFiniteRect(targetRect)
                || parentRect.width <= 0f
                || parentRect.height <= 0f
                || targetRect.width <= 0f
                || targetRect.height <= 0f)
            {
                return ResolveFallbackDirection(target, direction, anchor) + offset;
            }

            return ResolveDirection(parentRect, targetRect, direction, anchor) + offset;
        }

        internal static Vector3 ResolveDirection(
            Rect parentRect,
            Rect targetRect,
            UIMoveDirection direction,
            Vector3 anchor)
        {
            float outsideLeft = parentRect.xMin - targetRect.xMax;
            float outsideRight = parentRect.xMax - targetRect.xMin;
            float outsideTop = parentRect.yMin - targetRect.yMax;
            float outsideBottom = parentRect.yMax - targetRect.yMin;
            float insideLeft = parentRect.xMin - targetRect.xMin;
            float insideCenterX = parentRect.center.x - targetRect.center.x;
            float insideRight = parentRect.xMax - targetRect.xMax;
            float insideTop = parentRect.yMin - targetRect.yMin;
            float insideCenterY = parentRect.center.y - targetRect.center.y;
            float insideBottom = parentRect.yMax - targetRect.yMax;

            return direction switch
            {
                UIMoveDirection.Left => new Vector3(outsideLeft, anchor.y, anchor.z),
                UIMoveDirection.Top => new Vector3(anchor.x, outsideTop, anchor.z),
                UIMoveDirection.Right => new Vector3(outsideRight, anchor.y, anchor.z),
                UIMoveDirection.Bottom => new Vector3(anchor.x, outsideBottom, anchor.z),
                UIMoveDirection.TopLeft => new Vector3(insideLeft, insideTop, anchor.z),
                UIMoveDirection.TopCenter => new Vector3(insideCenterX, insideTop, anchor.z),
                UIMoveDirection.TopRight => new Vector3(insideRight, insideTop, anchor.z),
                UIMoveDirection.MiddleLeft => new Vector3(insideLeft, insideCenterY, anchor.z),
                UIMoveDirection.MiddleCenter => new Vector3(insideCenterX, insideCenterY, anchor.z),
                UIMoveDirection.MiddleRight => new Vector3(insideRight, insideCenterY, anchor.z),
                UIMoveDirection.BottomLeft => new Vector3(insideLeft, insideBottom, anchor.z),
                UIMoveDirection.BottomCenter => new Vector3(insideCenterX, insideBottom, anchor.z),
                UIMoveDirection.BottomRight => new Vector3(insideRight, insideBottom, anchor.z),
                _ => anchor
            };
        }

        private static Vector3 ResolveFallbackDirection(
            VisualElement target,
            UIMoveDirection direction,
            Vector3 anchor)
        {
            float width = target != null && IsFinite(target.resolvedStyle.width)
                ? Mathf.Max(0f, target.resolvedStyle.width)
                : 0f;
            float height = target != null && IsFinite(target.resolvedStyle.height)
                ? Mathf.Max(0f, target.resolvedStyle.height)
                : 0f;
            Rect parent = new Rect(0f, 0f, width, height);
            Rect element = new Rect(0f, 0f, width, height);
            return ResolveDirection(parent, element, direction, anchor);
        }

        private static Rect GetTransformedLayoutRect(VisualElement target, Vector2 scale, float angle)
        {
            if (target == null)
                return default;

            Rect layout = target.layout;
            if (!IsFiniteRect(layout))
                return layout;

            Vector3 transformOrigin = target.resolvedStyle.transformOrigin;
            float originX = transformOrigin.x;
            float originY = transformOrigin.y;
            float radians = angle * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var origin = new Vector2(originX, originY);
            var offset = new Vector2(originX + layout.x, originY + layout.y);
            AccumulateCorner(Vector2.zero, origin, offset, scale, cos, sin, ref min, ref max);
            AccumulateCorner(new Vector2(layout.width, 0f), origin, offset, scale, cos, sin, ref min, ref max);
            AccumulateCorner(new Vector2(0f, layout.height), origin, offset, scale, cos, sin, ref min, ref max);
            AccumulateCorner(new Vector2(layout.width, layout.height), origin, offset, scale, cos, sin, ref min, ref max);

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static void AccumulateCorner(
            Vector2 corner,
            Vector2 origin,
            Vector2 offset,
            Vector2 scale,
            float cos,
            float sin,
            ref Vector2 min,
            ref Vector2 max)
        {
            Vector2 local = Vector2.Scale(corner - origin, scale);
            var point = new Vector2(
                local.x * cos - local.y * sin,
                local.x * sin + local.y * cos) + offset;
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        private static float ResolveOrigin(Length length, float size)
        {
            return length.unit == LengthUnit.Percent ? size * length.value * 0.01f : length.value;
        }

        private static bool IsFiniteRect(Rect rect)
        {
            return IsFinite(rect.x)
                   && IsFinite(rect.y)
                   && IsFinite(rect.width)
                   && IsFinite(rect.height);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public override void ApplyAt(VisualElement target, UIAnimationType type, float time)
        {
            if (target == null)
                return;

            Vector3 value = Vector3.LerpUnclamped(preparedFrom, preparedTo, EvaluateAt(time));
            target.style.translate = new StyleTranslate(new Translate(
                new Length(value.x, LengthUnit.Pixel),
                new Length(value.y, LengthUnit.Pixel),
                value.z));
        }

        public override void ResetStyle(VisualElement target)
        {
            if (target != null)
                target.style.translate = StyleKeyword.Null;
        }

        public override UIAnimationChannel Clone()
        {
            UIMoveAnimation clone = new UIMoveAnimation
            {
                fromReference = fromReference,
                toReference = toReference,
                fromDirection = fromDirection,
                toDirection = toDirection,
                fromCustom = fromCustom,
                toCustom = toCustom,
                fromOffset = fromOffset,
                toOffset = toOffset
            };
            CopySharedTo(clone);
            return clone;
        }
    }
}
