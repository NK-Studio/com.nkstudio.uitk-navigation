using System;
using System.Threading;
using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Navigation;
using NKStudio.UITKNavigation.Popup;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Properties;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    public sealed class UIPopupStackTests
    {
        private const string Fixture =
            "Packages/com.nkstudio.uitk-navigation/Tests/Editor/PopupFixture.uxml";
        private const string AlternateFixture =
            "Packages/com.nkstudio.uitk-navigation/Tests/Editor/PopupAlternateFixture.uxml";
        private const string AnimatedFixture =
            "Packages/com.nkstudio.uitk-navigation/Tests/Editor/PopupAnimatedFixture.uxml";

        private VisualElement _root;
        private UIPopupStack _stack;

        [SetUp]
        public void SetUp()
        {
            _root = new VisualElement();
            _stack = new UIPopupStack(_root);
        }

        [TearDown]
        public void TearDown()
        {
            _stack?.Dispose();
        }

        [Test]
        public void SameTemplate_KeepsDataSourcesIndependent_AndUsesLifoOrder()
        {
            VisualTreeAsset template = Load(Fixture);
            var firstModel = new Model("first");
            var secondModel = new Model("second");

            UIPopupHandle first = _stack.Show(template, firstModel);
            UIPopupHandle second = _stack.Show(template, secondModel);

            Assert.That(_stack.Count, Is.EqualTo(2));
            Assert.That(_stack.Top, Is.SameAs(second));
            Assert.That(first.View, Is.Not.SameAs(second.View));
            Assert.That(first.View.dataSource, Is.SameAs(firstModel));
            Assert.That(second.View.dataSource, Is.SameAs(secondModel));
            Assert.That(first.View.Q<Label>("popup-label").text, Is.EqualTo("first"));
            Assert.That(second.View.Q<Label>("popup-label").text, Is.EqualTo("second"));
            Assert.That(first.View.Visibility.HideOnBackButton, Is.False);
            Assert.That(second.View.Visibility.HideOnBackButton, Is.True);

            Assert.That(second.Close(), Is.True);
            Assert.That(second.Completion.Result.Reason, Is.EqualTo(UIPopupCloseReason.Programmatic));
            Assert.That(_stack.Top, Is.SameAs(first));
            Assert.That(first.View.Visibility.HideOnBackButton, Is.True);
            Assert.That(second.Close(), Is.False);

            first.Close();
            Assert.That(_stack.Count, Is.Zero);
        }

        [Test]
        public void DifferentTemplates_AreShownThroughTheSameApi()
        {
            UIPopupHandle first = _stack.Show(Load(Fixture));
            UIPopupHandle second = _stack.Show(Load(AlternateFixture));

            Assert.That(first.View.name, Is.EqualTo("popup-view"));
            Assert.That(second.View.name, Is.EqualTo("alternate-popup"));
            Assert.That(_stack.Count, Is.EqualTo(2));

            _stack.CloseAll();
            Assert.That(_stack.Count, Is.Zero);
        }

        [Test]
        public void AnimatedCloseCompletesOnlyAfterHideMotionFinishes()
        {
            var dispatcher = new ManualMotionDispatcher();
            UIPopupHandle popup = _stack.Show(Load(AnimatedFixture));
            popup.View.Visibility.InstantShow();
            popup.View.Visibility.Scheduler = dispatcher.Scheduler;

            try
            {
                Assert.That(popup.Close(), Is.True);
                Assert.That(popup.IsOpen, Is.True);
                Assert.That(popup.Completion.IsCompleted, Is.False);
                Assert.That(_stack.Count, Is.EqualTo(1));

                dispatcher.Update(0.1f);
                Assert.That(popup.Completion.IsCompleted, Is.False);

                dispatcher.Update(0.1f);
                Assert.That(popup.Completion.IsCompleted, Is.True);
                Assert.That(popup.IsOpen, Is.False);
                Assert.That(_stack.Count, Is.Zero);
            }
            finally
            {
                dispatcher.Reset();
            }
        }

        [Test]
        public void Action_CanNotifyWithoutClosing_ThenCompleteWithActionResult()
        {
            UIPopupHandle handle = _stack.Show(Load(Fixture));
            string invoked = null;
            handle.ActionInvoked += actionId => invoked = actionId;

            handle.View.RequestAction("keep", false);

            Assert.That(invoked, Is.EqualTo("keep"));
            Assert.That(handle.IsOpen, Is.True);
            Assert.That(_stack.Count, Is.EqualTo(1));

            handle.View.RequestAction("confirm", true);

            UIPopupResult result = handle.Completion.Result;
            Assert.That(result.ActionId, Is.EqualTo("confirm"));
            Assert.That(result.Reason, Is.EqualTo(UIPopupCloseReason.Action));
            Assert.That(handle.IsOpen, Is.False);
            Assert.That(handle.Close(), Is.False);
        }

        [Test]
        public void Back_BlockHasPriorityOverNavigation_ThenCloseConsumesBack()
        {
            UIPopupHandle closePopup = _stack.Show(
                Load(Fixture),
                configure: view => view.BackBehavior = UIPopupBackBehavior.Close);
            UIPopupHandle blockPopup = _stack.Show(
                Load(Fixture),
                configure: view => view.BackBehavior = UIPopupBackBehavior.Block);

            var navigationElement = new VisualElement();
            bool navigationBackInvoked = false;
            var navigationVisibility = new UIViewVisibility(navigationElement)
            {
                HideOnBackButton = true,
                BackHandler = () =>
                {
                    navigationBackInvoked = true;
                    return true;
                }
            };
            navigationVisibility.InstantShow();

            try
            {
                Assert.That(blockPopup.View.Visibility.BlockBackButton, Is.True);
                Assert.That(
                    blockPopup.View.Visibility.BackPriority,
                    Is.GreaterThan(navigationVisibility.BackPriority));
                Assert.That(UINavigatorBehaviour.TryConsumeByVisibleView(), Is.True);
                Assert.That(navigationBackInvoked, Is.False);
                Assert.That(blockPopup.IsOpen, Is.True);

                blockPopup.Close();
                Assert.That(UINavigatorBehaviour.TryConsumeByVisibleView(), Is.True);
                Assert.That(closePopup.Completion.Result.Reason, Is.EqualTo(UIPopupCloseReason.Back));
                Assert.That(navigationBackInvoked, Is.False);

                Assert.That(UINavigatorBehaviour.TryConsumeByVisibleView(), Is.True);
                Assert.That(navigationBackInvoked, Is.True);
            }
            finally
            {
                navigationVisibility.Dispose();
            }
        }

        [Test]
        public void Back_PassThroughLeavesPopupOpen()
        {
            UIPopupHandle popup = _stack.Show(
                Load(Fixture),
                configure: view => view.BackBehavior = UIPopupBackBehavior.PassThrough);
            var navigationElement = new VisualElement();
            bool navigationBackInvoked = false;
            var navigationVisibility = new UIViewVisibility(navigationElement)
            {
                HideOnBackButton = true,
                BackHandler = () => navigationBackInvoked = true
            };
            navigationVisibility.InstantShow();

            try
            {
                Assert.That(UINavigatorBehaviour.TryConsumeByVisibleView(), Is.True);
                Assert.That(navigationBackInvoked, Is.True);
                Assert.That(popup.IsOpen, Is.True);
            }
            finally
            {
                navigationVisibility.Dispose();
            }
        }

        [Test]
        public void Backdrop_OnlyClosesWhenOptedIn()
        {
            UIPopupHandle popup = _stack.Show(Load(Fixture));
            UIPopupBackdrop backdrop = popup.View.Q<UIPopupBackdrop>();

            SendClick(backdrop);
            Assert.That(popup.IsOpen, Is.True);

            popup.View.CloseOnBackdrop = true;
            SendClick(backdrop);

            Assert.That(popup.Completion.Result.Reason, Is.EqualTo(UIPopupCloseReason.Backdrop));
            Assert.That(_stack.Count, Is.Zero);
        }

        [Test]
        public void CancellationImmediatelyRemovesPopupAndCancelsCompletion()
        {
            using var cancellation = new CancellationTokenSource();
            UIPopupHandle popup = _stack.Show(Load(Fixture), cancellationToken: cancellation.Token);

            cancellation.Cancel();

            Assert.That(_stack.Count, Is.Zero);
            Assert.That(popup.IsOpen, Is.False);
            Assert.That(popup.Completion.IsCanceled, Is.True);
        }

        [Test]
        public void AlreadyCancelledTokenCreatesNoPopupState()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            UIPopupHandle popup = _stack.Show(
                Load(Fixture),
                cancellationToken: cancellation.Token);

            Assert.That(_stack.Count, Is.Zero);
            Assert.That(popup.View, Is.Null);
            Assert.That(popup.Completion.IsCanceled, Is.True);
        }

        [Test]
        public void DisposeCompletesOpenPopupsAsHostDetached()
        {
            UIPopupHandle first = _stack.Show(Load(Fixture));
            UIPopupHandle second = _stack.Show(Load(Fixture));

            _stack.Dispose();

            Assert.That(first.Completion.Result.Reason, Is.EqualTo(UIPopupCloseReason.HostDetached));
            Assert.That(second.Completion.Result.Reason, Is.EqualTo(UIPopupCloseReason.HostDetached));
            Assert.That(_root.Q("ui-popup-layer"), Is.Null);
        }

        [Test]
        public void ConfigureExceptionLeavesNoPopupLayerChildren()
        {
            Assert.Throws<InvalidOperationException>(() =>
                _stack.Show(
                    Load(Fixture),
                    configure: _ => throw new InvalidOperationException("configure failed")));

            Assert.That(_stack.Count, Is.Zero);
            Assert.That(_root.Q("ui-popup-layer").childCount, Is.Zero);
        }

        [TestCase("PopupNoViewFixture.uxml")]
        [TestCase("PopupDuplicateViewFixture.uxml")]
        [TestCase("PopupMissingContentFixture.uxml")]
        [TestCase("PopupDuplicateContentFixture.uxml")]
        [TestCase("PopupDuplicateBackdropFixture.uxml")]
        public void InvalidMarkerStructureThrowsWithoutLeavingState(string fileName)
        {
            string path =
                $"Packages/com.nkstudio.uitk-navigation/Tests/Editor/{fileName}";

            Assert.Throws<InvalidOperationException>(() => _stack.Show(Load(path)));
            Assert.That(_stack.Count, Is.Zero);
            Assert.That(_root.Q("ui-popup-layer").childCount, Is.Zero);
        }

        [Test]
        public void UnpreparedHostThrowsWithoutCreatingPopup()
        {
            var gameObject = new GameObject("Unprepared Popup Host");
            gameObject.SetActive(false);
            UIPopupHost host = gameObject.AddComponent<UIPopupHost>();

            try
            {
                Assert.Throws<InvalidOperationException>(() => host.Show(Load(Fixture)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static VisualTreeAsset Load(string path)
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, $"Fixture was not imported: {path}");
            return asset;
        }

        private static void SendClick(VisualElement target)
        {
            using ClickEvent evt = ClickEvent.GetPooled();
            target.SendEvent(evt);
        }

        private sealed class Model
        {
            public Model(string value) => Value = value;
            [CreateProperty] public string Value { get; }
        }
    }
}
