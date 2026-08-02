using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI View Visibility Tests functionality.
    /// </summary>
    public sealed class UIViewVisibilityTests
    {
        private const float Duration = 0.3f;

        private ManualMotionDispatcher _dispatcher;
        private VisualElement _element;
        private UIViewVisibility _visibility;
        private UIAnimationPreset _preset;

        [SetUp]
        public void SetUp()
        {
            _dispatcher = new ManualMotionDispatcher();
            _element = new VisualElement();
            _preset = TestPresets.Create(Duration, 0f);

            _visibility = new UIViewVisibility(_element)
            {
                Preset = _preset,
                Scheduler = _dispatcher.Scheduler
            };
            _visibility.InstantHide();
        }

        [TearDown]
        public void TearDown()
        {
            _visibility?.Dispose();
            _dispatcher.Reset();

            if (_preset != null)
                Object.DestroyImmediate(_preset);
        }

        [Test]
        public void Show_TransitionsThroughShowingToVisible()
        {
            Assert.AreEqual(VisibilityState.NotVisible, _visibility.State);

            _visibility.Show();
            Assert.AreEqual(VisibilityState.Showing, _visibility.State);
            Assert.AreEqual(0f, _visibility.VisibilityProgress, 1e-4f);

            _dispatcher.Update(Duration * 0.5f);
            Assert.AreEqual(VisibilityState.Showing, _visibility.State);
            Assert.That(_visibility.VisibilityProgress, Is.GreaterThan(0.4f).And.LessThan(0.6f));

            _dispatcher.Update(Duration * 0.5f);
            Assert.AreEqual(VisibilityState.Visible, _visibility.State);
            Assert.AreEqual(1f, _visibility.VisibilityProgress, 1e-4f);
        }

        [Test]
        public void Show_ClearsInlineStylesAndKeepsDisplayFlexWhenFinished()
        {
            _visibility.Show();
            _dispatcher.Update(Duration);

            Assert.AreEqual(StyleKeyword.Null, _element.style.translate.keyword);
            Assert.AreEqual(StyleKeyword.Null, _element.style.scale.keyword);
            Assert.AreEqual(StyleKeyword.Null, _element.style.rotate.keyword);
            Assert.AreEqual(StyleKeyword.Null, _element.style.opacity.keyword);
            Assert.AreEqual(DisplayStyle.Flex, _element.style.display.value);
        }

        [Test]
        public void Show_WritesDetachedValuesOnTheFrameTheViewBecomesVisible()
        {
            _visibility.Show();

            Assert.AreEqual(DisplayStyle.Flex, _element.style.display.value);
            Assert.AreEqual(0f, _element.style.opacity.value, 1e-4f);
            Assert.AreEqual(100f, _element.style.translate.value.y.value, 1e-3f);
            Assert.AreEqual(LengthUnit.Pixel, _element.style.translate.value.y.unit);
        }

        [Test]
        public void Show_SuppressesUssTransitionsUntilMotionFinishes()
        {
            _element.style.transitionDuration = new System.Collections.Generic.List<TimeValue>
            {
                new TimeValue(0.24f)
            };

            _visibility.Show();
            Assert.AreEqual(0f, _element.style.transitionDuration.value[0].value, 1e-4f);
            Assert.AreEqual(0f, _element.style.opacity.value, 1e-4f);

            _dispatcher.Update(Duration);
            Assert.AreEqual(0.24f, _element.style.transitionDuration.value[0].value, 1e-4f);
            Assert.AreEqual(VisibilityState.Visible, _visibility.State);
        }
        [Test]
        public void Hide_InterruptingShow_ResumesFromCurrentVisualProgress()
        {
            _visibility.Show();
            _dispatcher.Update(Duration * 0.25f);

            float progressBeforeHide = _visibility.VisibilityProgress;
            Assert.That(progressBeforeHide, Is.GreaterThan(0.2f).And.LessThan(0.3f));

            _visibility.Hide();
            Assert.AreEqual(VisibilityState.Hiding, _visibility.State);
            Assert.AreEqual(progressBeforeHide, _visibility.VisibilityProgress, 1e-3f);

            _dispatcher.Update(Duration);
            Assert.AreEqual(VisibilityState.NotVisible, _visibility.State);
            Assert.AreEqual(DisplayStyle.None, _element.style.display.value);
            Assert.AreEqual(PickingMode.Ignore, _element.pickingMode);
        }

        [Test]
        public void Show_WhileAlreadyVisible_IsIgnored()
        {
            _visibility.Show();
            _dispatcher.Update(Duration);
            Assert.AreEqual(VisibilityState.Visible, _visibility.State);

            int showStartedCount = 0;
            _visibility.ShowStarted += () => showStartedCount++;

            _visibility.Show();
            Assert.AreEqual(0, showStartedCount);
            Assert.AreEqual(VisibilityState.Visible, _visibility.State);
        }

        [Test]
        public void InstantChanges_DoNotRaiseAnimatedStartEvents()
        {
            int showStarted = 0;
            int hideStarted = 0;
            _visibility.ShowStarted += () => showStarted++;
            _visibility.HideStarted += () => hideStarted++;

            _visibility.InstantShow();
            _visibility.InstantHide();

            Assert.AreEqual(0, showStarted);
            Assert.AreEqual(0, hideStarted);
        }
        [Test]
        public void Channel_StartDelayIsHonoured()
        {
            UIAnimationPreset delayed = TestPresets.Create(0.2f, 0.1f);
            VisualElement element = new VisualElement();
            UIViewVisibility visibility = new UIViewVisibility(element)
            {
                Preset = delayed,
                Scheduler = _dispatcher.Scheduler
            };
            visibility.InstantHide();

            try
            {
                visibility.Show();

                _dispatcher.Update(0.05f);
                Assert.AreEqual(100f, element.style.translate.value.y.value, 1e-3f);

                _dispatcher.Update(0.3f);
                Assert.AreEqual(VisibilityState.Visible, visibility.State);
                Assert.AreEqual(StyleKeyword.Null, element.style.translate.keyword);
            }
            finally
            {
                visibility.Dispose();
                Object.DestroyImmediate(delayed);
            }
        }

        [Test]
        public void Dispose_MidMotion_CancelsAndResetsInlineStyles()
        {
            _visibility.Show();
            _dispatcher.Update(Duration * 0.5f);
            Assert.AreNotEqual(StyleKeyword.Null, _element.style.opacity.keyword);

            _visibility.Dispose();

            Assert.AreEqual(StyleKeyword.Null, _element.style.translate.keyword);
            Assert.AreEqual(StyleKeyword.Null, _element.style.scale.keyword);
            Assert.AreEqual(StyleKeyword.Null, _element.style.rotate.keyword);
            Assert.AreEqual(StyleKeyword.Null, _element.style.opacity.keyword);

            _dispatcher.Update(Duration);
            Assert.AreEqual(StyleKeyword.Null, _element.style.opacity.keyword);
        }

        [Test]
        public void Preset_HandsOutClonesSoTheAssetIsNeverMutated()
        {
            UIAnimation first = _preset.GetShow();
            UIAnimation second = _preset.GetShow();

            Assert.AreNotSame(first, second);
            Assert.AreNotSame(first.Fade, second.Fade);

            first.Fade.Duration = 99f;
            Assert.AreNotEqual(99f, _preset.GetShow().Fade.Duration);
        }

        [Test]
        public void NullPreset_BehavesAsAnInstantDisplayToggle()
        {
            VisualElement element = new VisualElement();
            UIViewVisibility visibility = new UIViewVisibility(element);
            visibility.InstantHide();

            try
            {
                visibility.Show();
                Assert.AreEqual(VisibilityState.Visible, visibility.State);
                Assert.AreEqual(DisplayStyle.Flex, element.style.display.value);

                visibility.Hide();
                Assert.AreEqual(VisibilityState.NotVisible, visibility.State);
                Assert.AreEqual(DisplayStyle.None, element.style.display.value);
            }
            finally
            {
                visibility.Dispose();
            }
        }

        [Test]
        public void VisibleViews_TracksShowAndHide()
        {
            Assert.IsFalse(UIViewVisibility.VisibleViews.AsValueEnumerable().Contains(_visibility));

            _visibility.Show();
            Assert.IsTrue(UIViewVisibility.VisibleViews.AsValueEnumerable().Contains(_visibility));

            _dispatcher.Update(Duration);
            _visibility.Hide();
            _dispatcher.Update(Duration);

            Assert.IsFalse(UIViewVisibility.VisibleViews.AsValueEnumerable().Contains(_visibility));
        }

        [Test]
        public void MultipleLayers_ShareOneTimelineAndFinishTogether()
        {
            VisualElement host = new VisualElement();
            VisualElement backdrop = new VisualElement();
            VisualElement card = new VisualElement();
            host.Add(backdrop);
            host.Add(card);

            UIAnimationPreset backdropPreset = TestPresets.Create(0.2f, 0f);
            UIAnimationPreset cardPreset = TestPresets.Create(0.2f, 0.1f);

            UIViewVisibility visibility = new UIViewVisibility(host, false)
            {
                Scheduler = _dispatcher.Scheduler
            };
            visibility.AddLayer(backdrop, backdropPreset);
            visibility.AddLayer(card, cardPreset);
            visibility.InstantHide();

            try
            {
                visibility.Show();

                _dispatcher.Update(0.15f);
                Assert.AreEqual(VisibilityState.Showing, visibility.State);
                Assert.AreNotEqual(0f, card.style.translate.value.y.value);

                _dispatcher.Update(0.15f);
                Assert.AreEqual(VisibilityState.Visible, visibility.State);
                Assert.AreEqual(StyleKeyword.Null, backdrop.style.translate.keyword);
                Assert.AreEqual(StyleKeyword.Null, card.style.translate.keyword);
            }
            finally
            {
                visibility.Dispose();
                Object.DestroyImmediate(backdropPreset);
                Object.DestroyImmediate(cardPreset);
            }
        }
    }
}
