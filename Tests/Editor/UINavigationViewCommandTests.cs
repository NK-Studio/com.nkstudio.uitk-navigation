using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI Navigation View Command Tests functionality.
    /// </summary>
    public sealed class UINavigationViewCommandTests
    {
        private const float Duration = 0.3f;

        private TestNavigationGraphBuilder _builder;
        private ManualMotionDispatcher _dispatcher;
        private UIAnimationPreset _preset;
        private VisualElement _element;
        private UIViewVisibility _visibility;
        private UIKey _viewKey;

        [SetUp]
        public void SetUp()
        {
            _builder = new TestNavigationGraphBuilder();
            _dispatcher = new ManualMotionDispatcher();
            _preset = TestPresets.Create(Duration, 0f);
            _element = new VisualElement();
            _visibility = new UIViewVisibility(_element)
            {
                Preset = _preset,
                Scheduler = _dispatcher.Scheduler
            };
            _visibility.InstantHide();

            _viewKey = new UIKey("Test", "MainLobby");
            UIViewRegistry.Register(_viewKey, new StubView(_visibility));
        }

        [TearDown]
        public void TearDown()
        {
            UIViewRegistry.Clear();
            _visibility?.Dispose();
            _dispatcher.Reset();
            _builder.Dispose();

            if (_preset != null)
                Object.DestroyImmediate(_preset);
        }

        [Test]
        public void DelayedTransition_IntoAnimatedShow_PlaysAnimationInsteadOfHardCut()
        {
            UINavigationService service = CreateService();

            service.Initialize();
            Assert.AreEqual("Screen", service.ActiveNode.Id);
            Assert.AreEqual(VisibilityState.NotVisible, _visibility.State, "빈 노드는 아무 View도 건드리지 않아야 한다.");

            service.Tick(1.1f);
            Assert.AreEqual("Main", service.ActiveNode.Id);
            Assert.AreEqual(
                VisibilityState.Showing,
                _visibility.State,
                "Animated Show는 즉시 Visible로 튀지 않고 Showing 상태로 애니메이션을 시작해야 한다.");

            _dispatcher.Update(Duration * 0.5f);
            Assert.That(_visibility.VisibilityProgress, Is.GreaterThan(0.4f).And.LessThan(0.6f));

            _dispatcher.Update(Duration * 0.5f);
            Assert.AreEqual(VisibilityState.Visible, _visibility.State);
        }

        [Test]
        public void InstantModeCommand_SkipsAnimation()
        {
            UINavigationService service = CreateService(UIViewTransitionMode.Instant);

            service.Initialize();
            service.Tick(1.1f);

            Assert.AreEqual(
                VisibilityState.Visible,
                _visibility.State,
                "Instant 모드는 애니메이션 없이 곧바로 Visible이어야 한다.");
        }

        private UINavigationService CreateService(
            UIViewTransitionMode mode = UIViewTransitionMode.Animated)
        {
            _builder.AddNode("Screen");
            _builder.AddNode("Main");
            _builder.AddView(
                "Main",
                TestNavigationGraphBuilder.ViewSlot.ShowOnEnter,
                _viewKey,
                mode);
            _builder.AddOutput(
                "Screen",
                UINavigationTriggerKind.TimeDelay,
                "Delay",
                1f,
                false,
                "Main");

            var service = new UINavigationService(_builder.Build());
            service.ShowCommandsRequested += UINavigationEvents.ApplyViewShow;
            service.HideCommandsRequested += UINavigationEvents.ApplyViewHide;
            service.ResyncRequested += UINavigationEvents.RaiseViewResync;
            return service;
        }

        /// <summary>
        /// Provides Stub View functionality.
        /// </summary>
        private sealed class StubView : IUIVisibleView
        {
            private readonly UIViewVisibility _visibility;

            public StubView(UIViewVisibility visibility)
            {
                _visibility = visibility;
            }

            public bool IsVisible => _visibility.IsVisible;
            public void Show() => _visibility.Show();
            public void Hide() => _visibility.Hide();
            public void InstantShow() => _visibility.InstantShow();
            public void InstantHide() => _visibility.InstantHide();
        }
    }
}
