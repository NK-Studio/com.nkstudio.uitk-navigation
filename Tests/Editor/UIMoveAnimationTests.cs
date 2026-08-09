using System;
using System.Collections.Generic;
using System.Reflection;
using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Animation.Presets;
using NKStudio.UITKNavigation.Editor.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    public sealed class UIMoveAnimationTests
    {
        private static readonly object[] DirectionCases =
        {
            new object[] { UIMoveDirection.Left, new Vector3(-30f, 4f, 5f) },
            new object[] { UIMoveDirection.Top, new Vector3(3f, -30f, 5f) },
            new object[] { UIMoveDirection.Right, new Vector3(90f, 4f, 5f) },
            new object[] { UIMoveDirection.Bottom, new Vector3(3f, 60f, 5f) },
            new object[] { UIMoveDirection.TopLeft, new Vector3(-10f, -20f, 5f) },
            new object[] { UIMoveDirection.TopCenter, new Vector3(30f, -20f, 5f) },
            new object[] { UIMoveDirection.TopRight, new Vector3(70f, -20f, 5f) },
            new object[] { UIMoveDirection.MiddleLeft, new Vector3(-10f, 15f, 5f) },
            new object[] { UIMoveDirection.MiddleCenter, new Vector3(30f, 15f, 5f) },
            new object[] { UIMoveDirection.MiddleRight, new Vector3(70f, 15f, 5f) },
            new object[] { UIMoveDirection.BottomLeft, new Vector3(-10f, 50f, 5f) },
            new object[] { UIMoveDirection.BottomCenter, new Vector3(30f, 50f, 5f) },
            new object[] { UIMoveDirection.BottomRight, new Vector3(70f, 50f, 5f) }
        };

        [Test]
        public void DirectionEnum_MatchesDoozyFourteenValues()
        {
            UIMoveDirection[] values = (UIMoveDirection[])Enum.GetValues(typeof(UIMoveDirection));
            Assert.AreEqual(14, values.Length);
            CollectionAssert.AreEqual(new[]
            {
                UIMoveDirection.Left, UIMoveDirection.Top, UIMoveDirection.Right, UIMoveDirection.Bottom,
                UIMoveDirection.TopLeft, UIMoveDirection.TopCenter, UIMoveDirection.TopRight,
                UIMoveDirection.MiddleLeft, UIMoveDirection.MiddleCenter, UIMoveDirection.MiddleRight,
                UIMoveDirection.BottomLeft, UIMoveDirection.BottomCenter, UIMoveDirection.BottomRight,
                UIMoveDirection.CustomPosition
            }, values);
        }

        [TestCaseSource(nameof(DirectionCases))]
        internal void ResolveDirection_UsesOutsideBarsAndInsideThreeByThree(
            UIMoveDirection direction,
            Vector3 expected)
        {
            Rect parent = new Rect(0f, 0f, 100f, 80f);
            Rect target = new Rect(10f, 20f, 20f, 10f);
            Vector3 actual = UIMoveAnimation.ResolveDirection(parent, target, direction, new Vector3(3f, 4f, 5f));
            Assert.AreEqual(expected.x, actual.x, 1e-4f);
            Assert.AreEqual(expected.y, actual.y, 1e-4f);
            Assert.AreEqual(expected.z, actual.z, 1e-4f);
        }

        [Test]
        public void DirectionWidget_HasSeventeenPartsAndFourCustomCorners()
        {
            MethodInfo method = typeof(UITransitionPropertyDrawer).GetMethod(
                "BuildDirectionGrid",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);
            VisualElement root = (VisualElement)method.Invoke(null, new object[] { null });
            List<UIMoveDirection> mapped = root.Query<VisualElement>().ToList()
                .AsValueEnumerable()
                .Where(element => Enum.TryParse(element.tooltip, out UIMoveDirection _))
                .Select(element => (UIMoveDirection)Enum.Parse(typeof(UIMoveDirection), element.tooltip))
                .ToList();

            Assert.AreEqual(17, mapped.Count);
            Assert.AreEqual(4, mapped.AsValueEnumerable().Count(value => value == UIMoveDirection.CustomPosition));
            Assert.AreEqual(14, mapped.AsValueEnumerable().Distinct().Count());
        }

        [Test]
        public void Animator_RestoresTransitionDurationAndSuppressesDuplicateElementOnce()
        {
            var element = new VisualElement();
            element.style.transitionDuration = new List<TimeValue>
            {
                new TimeValue(0.2f),
                new TimeValue(0.4f)
            };
            var animation = new UIAnimation();
            var animator = new UIAnimator();
            var bindings = (List<UIAnimationBinding>)typeof(UIAnimator)
                .GetField("_bindings", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(animator);
            bindings.Add(new UIAnimationBinding(element, animation));
            bindings.Add(new UIAnimationBinding(element, animation));

            typeof(UIAnimator)
                .GetMethod("SuppressTransitions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(animator, null);

            Assert.AreEqual(1, element.style.transitionDuration.value.Count);
            Assert.AreEqual(0f, element.style.transitionDuration.value[0].value);
            var overrides = (System.Collections.ICollection)typeof(UIAnimator)
                .GetField("_transitionOverrides", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(animator);
            Assert.AreEqual(1, overrides.Count);

            typeof(UIAnimator)
                .GetMethod("RestoreTransitions", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(animator, null);

            Assert.AreEqual(2, element.style.transitionDuration.value.Count);
            Assert.AreEqual(0.2f, element.style.transitionDuration.value[0].value);
            Assert.AreEqual(0.4f, element.style.transitionDuration.value[1].value);
        }

        [Test]
        public void Show_ResolvesDirectionToStartAndCustomIgnoresOffset()
        {
            VisualElement element = ElementAt(new Vector3(5f, 6f, 7f));
            var move = LinearMove();
            move.FromDirection = UIMoveDirection.CustomPosition;
            move.FromCustom = new Vector3(100f, 200f, 9f);
            move.FromOffset = new Vector3(999f, 999f, 999f);
            move.ToReference = UIReferenceValue.StartValue;
            move.ToOffset = new Vector3(2f, 3f, 4f);

            move.Prepare(element, UIAnimationType.Show);
            move.ApplyAt(element, UIAnimationType.Show, 0f);
            AssertVector(element, new Vector3(100f, 200f, 9f));
            move.ApplyAt(element, UIAnimationType.Show, 1f);
            AssertVector(element, new Vector3(7f, 9f, 11f));
        }

        [Test]
        public void Hide_ResolvesStartToDirectionAndCustomIgnoresOffset()
        {
            VisualElement element = ElementAt(new Vector3(5f, 6f, 7f));
            var move = LinearMove();
            move.FromReference = UIReferenceValue.StartValue;
            move.FromOffset = new Vector3(2f, 3f, 4f);
            move.ToDirection = UIMoveDirection.CustomPosition;
            move.ToCustom = new Vector3(100f, 200f, 9f);
            move.ToOffset = new Vector3(999f, 999f, 999f);

            move.Prepare(element, UIAnimationType.Hide);
            move.ApplyAt(element, UIAnimationType.Hide, 0f);
            AssertVector(element, new Vector3(7f, 9f, 11f));
            move.ApplyAt(element, UIAnimationType.Hide, 1f);
            AssertVector(element, new Vector3(100f, 200f, 9f));
        }

        [Test]
        public void CurrentAndCustomReferencesFollowPixelSemantics()
        {
            VisualElement element = ElementAt(new Vector3(20f, 30f, 40f));
            var move = LinearMove();
            move.FromDirection = UIMoveDirection.CustomPosition;
            move.FromCustom = Vector3.zero;
            move.ToReference = UIReferenceValue.CurrentValue;
            move.ToOffset = new Vector3(1f, 2f, 3f);
            move.Prepare(element, UIAnimationType.Show);
            move.ApplyAt(element, UIAnimationType.Show, 1f);
            AssertVector(element, new Vector3(21f, 32f, 43f));

            var custom = LinearMove();
            custom.FromDirection = UIMoveDirection.CustomPosition;
            custom.ToReference = UIReferenceValue.CustomValue;
            custom.ToCustom = new Vector3(8f, 9f, 10f);
            custom.ToOffset = Vector3.one * 100f;
            custom.Prepare(element, UIAnimationType.Show);
            custom.ApplyAt(element, UIAnimationType.Show, 1f);
            AssertVector(element, new Vector3(8f, 9f, 10f));
        }

        [Test]
        public void Clone_CopiesAuthoringDataWithoutSharingState()
        {
            var source = LinearMove();
            source.FromReference = UIReferenceValue.CurrentValue;
            source.ToReference = UIReferenceValue.CustomValue;
            source.FromDirection = UIMoveDirection.TopRight;
            source.ToDirection = UIMoveDirection.BottomLeft;
            source.FromCustom = new Vector3(1f, 2f, 3f);
            source.ToCustom = new Vector3(4f, 5f, 6f);
            source.FromOffset = new Vector3(7f, 8f, 9f);
            source.ToOffset = new Vector3(10f, 11f, 12f);
            source.PlayMode = UIAnimationPlayMode.Spring;
            source.Loops = 3;

            var clone = (UIMoveAnimation)source.Clone();
            Assert.AreNotSame(source, clone);
            Assert.AreEqual(source.FromDirection, clone.FromDirection);
            Assert.AreEqual(source.ToDirection, clone.ToDirection);
            Assert.AreEqual(source.FromCustom, clone.FromCustom);
            Assert.AreEqual(source.ToOffset, clone.ToOffset);
            Assert.AreEqual(source.PlayMode, clone.PlayMode);
            Assert.AreEqual(source.Loops, clone.Loops);
            clone.FromCustom = Vector3.zero;
            Assert.AreNotEqual(source.FromCustom, clone.FromCustom);
        }

        [Test]
        public void Preset_ShowDefinesFromSideAndHideDefinesToSide()
        {
            UIAnimation show = BuildPreset(UITransitionPresetCategory.Slide1, 1, UIAnimationType.Show);
            UIAnimation hide = BuildPreset(UITransitionPresetCategory.Slide1, 1, UIAnimationType.Hide);

            Assert.AreEqual(UIMoveDirection.Left, show.Move.FromDirection);
            Assert.AreEqual(UIMoveDirection.CustomPosition, show.Move.ToDirection);

            Assert.AreEqual(UIMoveDirection.CustomPosition, hide.Move.FromDirection);
            Assert.AreEqual(UIMoveDirection.Left, hide.Move.ToDirection);
        }

        [Test]
        public void Preset_UsesOppositeReferenceFlowForShowAndHide()
        {
            UIAnimation show = BuildPreset(UITransitionPresetCategory.Slide1, 1, UIAnimationType.Show);
            UIAnimation hide = BuildPreset(UITransitionPresetCategory.Slide1, 1, UIAnimationType.Hide);

            Assert.IsTrue(show.Fade.Enabled);
            Assert.AreEqual(UIReferenceValue.CustomValue, show.Fade.FromReference);
            Assert.AreEqual(UIReferenceValue.StartValue, show.Fade.ToReference);

            Assert.IsTrue(hide.Fade.Enabled);
            Assert.AreEqual(UIReferenceValue.StartValue, hide.Fade.FromReference);
            Assert.AreEqual(UIReferenceValue.CustomValue, hide.Fade.ToReference);
        }

        [Test]
        public void Preset_MapsDoozyEasyEaseToAnimationCurve()
        {
            UIAnimation show = BuildPreset(UITransitionPresetCategory.Slide1, 1, UIAnimationType.Show);

            Assert.AreEqual(UIAnimationEaseType.AnimationCurve, show.Fade.EaseType);
            Assert.IsNotNull(show.Fade.Curve);
            Assert.That(show.Fade.Curve.length, Is.GreaterThan(1));

            Assert.AreEqual(UIAnimationEaseType.Ease, show.Move.EaseType);
            Assert.AreEqual(Ease.OutExpo, show.Move.Ease);
        }

        [Test]
        public void PresetLibrary_ExposesVariantCountsAndOriginalNames()
        {
            Assert.AreEqual(25, UITransitionPresetLibrary.GetVariantCount(UITransitionPresetCategory.Slide1));
            Assert.AreEqual(1, UITransitionPresetLibrary.GetVariantCount(UITransitionPresetCategory.Default));

            Assert.AreEqual(10, UITransitionPresetLibrary.GetVariantCount(UITransitionPresetCategory.Drift));
            Assert.AreEqual("01Left", UITransitionPresetLibrary.GetVariantName(UITransitionPresetCategory.Drift, 1));
        }

        [Test]
        public void Preset_ReturnsNullForNoneOrOutOfRangeVariant()
        {
            Assert.IsNull(UITransitionFactory.BuildPreset(
                UITransitionPresetCategory.None, 1, UIAnimationType.Show));
            Assert.IsNull(UITransitionFactory.BuildPreset(
                UITransitionPresetCategory.Slide1, 0, UIAnimationType.Show));
            Assert.IsNull(UITransitionFactory.BuildPreset(
                UITransitionPresetCategory.Slide1, 999, UIAnimationType.Hide));
        }
        [Test]
        public void PingPong_TravelsToTargetAndReturnsWithinOneDuration()
        {
            UIMoveAnimation channel = LinearMove();
            channel.PlayMode = UIAnimationPlayMode.PingPong;

            Assert.AreEqual(0f, channel.EvaluateAt(0f), 1e-4f);
            Assert.AreEqual(1f, channel.EvaluateAt(0.5f), 1e-4f);
            Assert.AreEqual(0f, channel.EvaluateAt(1f), 1e-4f);
        }

        [Test]
        public void PositiveLoops_ExtendDurationAndRestartNormalMode()
        {
            UIMoveAnimation channel = LinearMove();
            channel.Loops = 2;

            Assert.AreEqual(3f, channel.TotalDuration, 1e-4f);
            Assert.AreEqual(0.25f, channel.EvaluateAt(1.25f), 1e-4f);
            Assert.AreEqual(1f, channel.EvaluateAt(3f), 1e-4f);
        }

        [Test]
        public void InfiniteLoops_ForcePingPongRegardlessOfAuthoredMode()
        {
            UIMoveAnimation channel = LinearMove();
            channel.PlayMode = UIAnimationPlayMode.Shake;
            channel.Loops = -1;

            Assert.IsTrue(channel.IsInfinite);
            Assert.AreEqual(UIAnimationPlayMode.PingPong, channel.EffectivePlayMode);
            Assert.AreEqual(1f, channel.EvaluateAt(0.5f), 1e-4f);
            Assert.AreEqual(0f, channel.EvaluateAt(1f), 1e-4f);
        }

        [Test]
        public void SpringAndShake_ReturnToFromValue()
        {
            UIMoveAnimation channel = LinearMove();
            channel.PlayMode = UIAnimationPlayMode.Spring;
            Assert.AreEqual(0f, channel.EvaluateAt(0f), 1e-4f);
            Assert.AreEqual(0f, channel.EvaluateAt(1f), 1e-4f);

            channel.PlayMode = UIAnimationPlayMode.Shake;
            Assert.AreEqual(0f, channel.EvaluateAt(0f), 1e-4f);
            Assert.AreEqual(0f, channel.EvaluateAt(1f), 1e-4f);
        }
        [Test]
        public void MoveSettings_MapsAllNewFromToFields()
        {
            var settings = new UIMoveSettings
            {
                Enable = true,
                FromType = UIReferenceValue.CurrentValue,
                ToType = UIReferenceValue.CustomValue,
                FromDirection = UIMoveDirection.TopCenter,
                ToDirection = UIMoveDirection.BottomCenter,
                FromCustom = new Vector3(1f, 2f, 3f),
                ToCustom = new Vector3(4f, 5f, 6f),
                FromOffset = new Vector3(7f, 8f, 9f),
                ToOffset = new Vector3(10f, 11f, 12f),
                PlayMode = UIAnimationPlayMode.PingPong,
                Loops = 2
            };
            MoveChannelOptions options = settings.ToOptions();
            Assert.AreEqual(settings.FromType, options.FromReference);
            Assert.AreEqual(settings.ToType, options.ToReference);
            Assert.AreEqual(settings.FromDirection, options.FromDirection);
            Assert.AreEqual(settings.ToDirection, options.ToDirection);
            Assert.AreEqual(settings.FromCustom, options.FromCustom);
            Assert.AreEqual(settings.ToOffset, options.ToOffset);
            Assert.AreEqual(settings.PlayMode, options.PlayMode);
            Assert.AreEqual(settings.Loops, options.Loops);
        }

        private static UIMoveAnimation LinearMove()
        {
            return new UIMoveAnimation
            {
                Enabled = true,
                Duration = 1f,
                EaseType = UIAnimationEaseType.Ease,
                Ease = Ease.Linear
            };
        }

        private static VisualElement ElementAt(Vector3 value)
        {
            var element = new VisualElement();
            element.style.translate = new Translate(
                new Length(value.x, LengthUnit.Pixel),
                new Length(value.y, LengthUnit.Pixel),
                value.z);
            return element;
        }

        private static void AssertVector(VisualElement element, Vector3 expected)
        {
            Translate actual = element.style.translate.value;
            Assert.AreEqual(LengthUnit.Pixel, actual.x.unit);
            Assert.AreEqual(LengthUnit.Pixel, actual.y.unit);
            Assert.AreEqual(expected.x, actual.x.value, 1e-4f);
            Assert.AreEqual(expected.y, actual.y.value, 1e-4f);
            Assert.AreEqual(expected.z, actual.z, 1e-4f);
        }

        private static UIAnimation BuildPreset(
            UITransitionPresetCategory category,
            int variant,
            UIAnimationType type)
        {
            return UITransitionFactory.Build(
                category,
                variant,
                type,
                default,
                default,
                default,
                default);
        }
    }
}
