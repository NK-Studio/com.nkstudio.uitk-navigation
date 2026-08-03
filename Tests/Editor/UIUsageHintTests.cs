using NKStudio.UITKNavigation.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI Usage Hint Tests functionality.
    /// </summary>
    public sealed class UIUsageHintTests
    {
        private VisualElement _element;
        private UIViewVisibility _visibility;

        [SetUp]
        public void SetUp()
        {
            _element = new VisualElement();
            _visibility = new UIViewVisibility(_element);
        }

        [TearDown]
        public void TearDown()
        {
            _visibility?.Dispose();
        }

        [Test]
        public void ShowAnimation_MoveOnly_AddsDynamicTransformOnly()
        {
            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true);

            Assert.AreEqual(UsageHints.DynamicTransform, _element.usageHints);
        }

        [Test]
        public void ShowAnimation_FadeOnly_AddsDynamicColorOnly()
        {
            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, fade: true);

            Assert.AreEqual(UsageHints.DynamicColor, _element.usageHints);
        }

        [Test]
        public void ShowAnimation_RotateOrScale_AddsDynamicTransform()
        {
            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, rotate: true);
            Assert.AreEqual(UsageHints.DynamicTransform, _element.usageHints);

            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, scale: true);
            Assert.AreEqual(UsageHints.DynamicTransform, _element.usageHints);
        }

        /// <summary>
        /// Shows a nd hi de d if fe re nt ch an ne ls u ni on sh in ts.
        /// </summary>
        [Test]
        public void ShowAndHide_DifferentChannels_UnionsHints()
        {
            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true);
            _visibility.HideAnimation = TestPresets.CreateAnimation(UIAnimationType.Hide, fade: true);

            Assert.AreEqual(
                UsageHints.DynamicTransform | UsageHints.DynamicColor,
                _element.usageHints);
        }

        [Test]
        public void DisabledChannel_RemovesOnlyOurHint()
        {
            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true, fade: true);
            Assert.AreEqual(
                UsageHints.DynamicTransform | UsageHints.DynamicColor,
                _element.usageHints);

            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true);
            Assert.AreEqual(UsageHints.DynamicTransform, _element.usageHints);

            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show);
            Assert.AreEqual(UsageHints.None, _element.usageHints);
        }

        /// <summary>
        /// Performs the authored hint survives channel disable operation.
        /// </summary>
        [Test]
        public void AuthoredHint_SurvivesChannelDisable()
        {
            var element = new VisualElement { usageHints = UsageHints.DynamicTransform };
            var visibility = new UIViewVisibility(element);

            try
            {
                visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, fade: true);
                Assert.AreEqual(
                    UsageHints.DynamicTransform | UsageHints.DynamicColor,
                    element.usageHints);

                visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show);
                Assert.AreEqual(UsageHints.DynamicTransform, element.usageHints);
            }
            finally
            {
                visibility.Dispose();
            }
        }

        [Test]
        public void Animations_AppliesAndRevertsHints()
        {
            var (showAnim, hideAnim) = TestPresets.Create(0.2f, 0f);

            _visibility.ShowAnimation = showAnim;
            _visibility.HideAnimation = hideAnim;
            Assert.AreEqual(
                UsageHints.DynamicTransform | UsageHints.DynamicColor,
                _element.usageHints);

            _visibility.ShowAnimation = null;
            _visibility.HideAnimation = null;
            Assert.AreEqual(UsageHints.None, _element.usageHints);
        }

        [Test]
        public void Dispose_ClearsOurHintsAndKeepsAuthoredHint()
        {
            var element = new VisualElement { usageHints = UsageHints.DynamicColor };
            var visibility = new UIViewVisibility(element);

            visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true);
            Assert.AreEqual(
                UsageHints.DynamicTransform | UsageHints.DynamicColor,
                element.usageHints);

            visibility.Dispose();
            Assert.AreEqual(UsageHints.DynamicColor, element.usageHints);
        }

        /// <summary>
        /// Performs the non animated gate gets no hints while layers do operation.
        /// </summary>
        [Test]
        public void NonAnimatedGate_GetsNoHintsWhileLayersDo()
        {
            var host = new VisualElement();
            var backdrop = new VisualElement();
            var card = new VisualElement();
            host.Add(backdrop);
            host.Add(card);

            var visibility = new UIViewVisibility(host, false);

            try
            {
                visibility.AddLayer(
                    backdrop,
                    TestPresets.CreateAnimation(UIAnimationType.Show, fade: true),
                    TestPresets.CreateAnimation(UIAnimationType.Hide, fade: true));
                visibility.AddLayer(
                    card,
                    TestPresets.CreateAnimation(UIAnimationType.Show, move: true),
                    TestPresets.CreateAnimation(UIAnimationType.Hide, move: true));

                Assert.AreEqual(UsageHints.None, host.usageHints);
                Assert.AreEqual(UsageHints.DynamicColor, backdrop.usageHints);
                Assert.AreEqual(UsageHints.DynamicTransform, card.usageHints);
            }
            finally
            {
                visibility.Dispose();
            }
        }

        /// <summary>
        /// Performs the repeated assignment keeps hints stable operation.
        /// </summary>
        [Test]
        public void RepeatedAssignment_KeepsHintsStable()
        {
            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true);
            UsageHints first = _element.usageHints;

            _visibility.ShowAnimation = TestPresets.CreateAnimation(UIAnimationType.Show, move: true);

            Assert.AreEqual(first, _element.usageHints);
            Assert.AreEqual(UsageHints.DynamicTransform, _element.usageHints);
        }
    }
}
