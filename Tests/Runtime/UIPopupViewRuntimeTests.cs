using NKStudio.UITKNavigation.Popup;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Tests
{
    public sealed class UIPopupViewRuntimeTests
    {
        private UIPopupView _view;
        private UIPopupBackdrop _backdrop;
        private UIPopupContent _content;
        private UIPopupCloseReason _closeReason;
        private string _closeActionId;
        private string _invokedActionId;

        [SetUp]
        public void SetUp()
        {
            _view = new UIPopupView
            {
                BackdropTransitions = null,
                ContentTransitions = null
            };
            _backdrop = new UIPopupBackdrop();
            _content = new UIPopupContent();
            _view.Add(_backdrop);
            _view.Add(_content);
            _view.InitializeRuntime(
                _backdrop,
                _content,
                (reason, actionId) =>
                {
                    _closeReason = reason;
                    _closeActionId = actionId;
                    return true;
                },
                actionId => _invokedActionId = actionId);
        }

        [TearDown]
        public void TearDown()
        {
            _view?.DisposeRuntime();
        }

        [Test]
        public void ShowAndHideWithoutTransitionsFinishImmediately()
        {
            _view.ShowRuntime();
            Assert.That(_view.IsVisible, Is.True);

            _view.HideRuntime();
            Assert.That(_view.IsVisible, Is.False);
        }

        [Test]
        public void ActionCanNotifyWithoutClosingOrRequestActionClose()
        {
            _view.RequestAction("keep", false);
            Assert.That(_invokedActionId, Is.EqualTo("keep"));
            Assert.That(_closeActionId, Is.Null);

            _view.RequestAction("confirm", true);
            Assert.That(_invokedActionId, Is.EqualTo("confirm"));
            Assert.That(_closeReason, Is.EqualTo(UIPopupCloseReason.Action));
            Assert.That(_closeActionId, Is.EqualTo("confirm"));
        }

        [TestCase(UIPopupBackBehavior.Close, true, false)]
        [TestCase(UIPopupBackBehavior.Block, false, true)]
        [TestCase(UIPopupBackBehavior.PassThrough, false, false)]
        public void TopmostBackPolicyMatchesTemplateSetting(
            UIPopupBackBehavior behavior,
            bool hides,
            bool blocks)
        {
            _view.BackBehavior = behavior;
            _view.SetTopmost(true);

            Assert.That(_view.Visibility.HideOnBackButton, Is.EqualTo(hides));
            Assert.That(_view.Visibility.BlockBackButton, Is.EqualTo(blocks));
            Assert.That(_view.Visibility.BackPriority, Is.GreaterThan(0));

            _view.SetTopmost(false);
            Assert.That(_view.Visibility.HideOnBackButton, Is.False);
            Assert.That(_view.Visibility.BlockBackButton, Is.False);
            Assert.That(_view.Visibility.BackPriority, Is.Zero);
        }
    }
}
