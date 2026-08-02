using System.Collections.Generic;
using LitMotion;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Elements;
using NKStudio.UITKNavigation.Identity;
using NUnit.Framework;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI View Fan Out Tests functionality.
    /// </summary>
    public sealed class UIViewFanOutTests
    {
        private ManualMotionDispatcher _dispatcher;
        private readonly List<UIAnimationPreset> _presets = new List<UIAnimationPreset>();

        [SetUp]
        public void SetUp()
        {
            _dispatcher = new ManualMotionDispatcher();
        }

        [TearDown]
        public void TearDown()
        {
            UIViewRegistry.Clear();
            _dispatcher.Reset();

            for (int i = 0; i < _presets.Count; i++)
            {
                if (_presets[i] != null)
                    Object.DestroyImmediate(_presets[i]);
            }

            _presets.Clear();
        }

        #region Same Address Fan-Out

        [Test]
        public void Show_SameAddress_DrivesEveryRegisteredView()
        {
            UIKey key = new UIKey("Test", "Hud");
            StubView[] views =
            {
                new StubView(), new StubView(), new StubView(), new StubView()
            };

            for (int i = 0; i < views.Length; i++)
                UIViewRegistry.Register(key, views[i]);

            Assert.AreEqual(1, UIViewRegistry.Count, "주소는 하나로 세어야 한다.");
            Assert.AreEqual(4, UIViewRegistry.GetViews(key).Count, "같은 주소에 4개가 모두 남아 있어야 한다.");

            UIViewRegistry.Show(key);
            for (int i = 0; i < views.Length; i++)
                Assert.IsTrue(views[i].IsVisible, $"{i}번 View도 함께 표시돼야 한다.");

            UIViewRegistry.Hide(key);
            for (int i = 0; i < views.Length; i++)
                Assert.IsFalse(views[i].IsVisible, $"{i}번 View도 함께 숨겨져야 한다.");
        }

        [Test]
        public void Unregister_RemovesOnlyThatView()
        {
            UIKey key = new UIKey("Test", "Hud");
            StubView kept = new StubView();
            StubView removed = new StubView();

            UIViewRegistry.Register(key, kept);
            UIViewRegistry.Register(key, removed);
            UIViewRegistry.Unregister(key, removed);

            UIViewRegistry.Show(key);

            Assert.IsTrue(kept.IsVisible);
            Assert.IsFalse(removed.IsVisible, "해제된 View는 더 이상 명령을 받지 않아야 한다.");
        }

        #endregion

        #region Parent → Child Propagation

        [Test]
        public void Show_Propagates_ToChildrenAndGrandChildren()
        {
            Tree tree = BuildTree(parentDuration: 0.1f, childDuration: 0.3f, grandChildDuration: 0.5f);

            tree.Parent.Show();

            Assert.AreEqual(VisibilityState.Showing, tree.Parent.Visibility.State);
            Assert.AreEqual(VisibilityState.Showing, tree.Child.Visibility.State, "자식도 함께 시작해야 한다.");
            Assert.AreEqual(VisibilityState.Showing, tree.GrandChild.Visibility.State, "손자까지 내려가야 한다.");
        }

        [Test]
        public void Show_ParentStaysShowing_UntilDeepestDescendantFinishes()
        {
            Tree tree = BuildTree(parentDuration: 0.1f, childDuration: 0.3f, grandChildDuration: 0.5f);

            tree.Parent.Show();

            _dispatcher.Update(0.15f);
            Assert.AreEqual(
                VisibilityState.Showing,
                tree.Parent.Visibility.State,
                "자기 애니메이션이 끝나도 하위가 남아 있으면 Visible로 확정하면 안 된다.");

            _dispatcher.Update(0.2f);
            Assert.AreEqual(VisibilityState.Showing, tree.Child.Visibility.State, "자식도 손자를 기다려야 한다.");
            Assert.AreEqual(VisibilityState.Showing, tree.Parent.Visibility.State);

            _dispatcher.Update(0.2f);
            Assert.AreEqual(VisibilityState.Visible, tree.GrandChild.Visibility.State);
            Assert.AreEqual(VisibilityState.Visible, tree.Child.Visibility.State);
            Assert.AreEqual(VisibilityState.Visible, tree.Parent.Visibility.State);
        }

        [Test]
        public void Hide_ParentDefersGateClose_UntilDeepestDescendantFinishes()
        {
            Tree tree = BuildTree(parentDuration: 0.1f, childDuration: 0.3f, grandChildDuration: 0.5f);

            tree.Parent.InstantShow();
            Assert.AreEqual(VisibilityState.Visible, tree.GrandChild.Visibility.State, "Instant도 하위까지 퍼져야 한다.");

            tree.Parent.Hide();
            Assert.AreEqual(VisibilityState.Hiding, tree.Child.Visibility.State);
            Assert.AreEqual(VisibilityState.Hiding, tree.GrandChild.Visibility.State);

            _dispatcher.Update(0.35f);
            Assert.AreEqual(
                VisibilityState.Hiding,
                tree.Parent.Visibility.State,
                "손자가 아직 도는 동안 상위가 NotVisible로 내려가면 게이트가 닫혀 하위 애니메이션이 잘린다.");

            _dispatcher.Update(0.2f);
            Assert.AreEqual(VisibilityState.NotVisible, tree.GrandChild.Visibility.State);
            Assert.AreEqual(VisibilityState.NotVisible, tree.Child.Visibility.State);
            Assert.AreEqual(VisibilityState.NotVisible, tree.Parent.Visibility.State);
        }

        [Test]
        public void FollowParentDisabled_MakesSubtreeIndependent()
        {
            Tree tree = BuildTree(parentDuration: 0.1f, childDuration: 0.3f, grandChildDuration: 0.5f);
            tree.Child.FollowParent = false;

            tree.Parent.Show();

            Assert.AreEqual(VisibilityState.Showing, tree.Parent.Visibility.State);
            Assert.AreEqual(
                VisibilityState.NotVisible,
                tree.Child.Visibility.State,
                "follow-parent를 끈 View는 상위 명령을 받지 않아야 한다.");
            Assert.AreEqual(
                VisibilityState.NotVisible,
                tree.GrandChild.Visibility.State,
                "독립 섬 아래로는 상위가 내려가지 않아야 한다.");

            _dispatcher.Update(0.15f);
            Assert.AreEqual(VisibilityState.Visible, tree.Parent.Visibility.State);
        }

        [Test]
        public void ShowDuringHide_CancelsThePendingDependentWait()
        {
            Tree tree = BuildTree(parentDuration: 0.1f, childDuration: 0.3f, grandChildDuration: 0.5f);

            tree.Parent.InstantShow();
            tree.Parent.Hide();
            _dispatcher.Update(0.15f); // 상위 자신의 Hide는 끝났고 하위 완료를 기다리는 중

            tree.Parent.Show();
            Assert.AreEqual(VisibilityState.Showing, tree.Parent.Visibility.State);

            _dispatcher.Update(1f);
            Assert.AreEqual(
                VisibilityState.Visible,
                tree.Parent.Visibility.State,
                "취소된 Hide 대기가 살아남아 뒤늦게 NotVisible로 끌어내리면 안 된다.");
            Assert.AreEqual(VisibilityState.Visible, tree.GrandChild.Visibility.State);
        }

        #endregion

        private Tree BuildTree(float parentDuration, float childDuration, float grandChildDuration)
        {
            NavElement parent = CreateNavElement(parentDuration);
            NavElement child = CreateNavElement(childDuration);
            NavElement grandChild = CreateNavElement(grandChildDuration);

            parent.Add(child);
            child.Add(grandChild);

            parent.InstantHide();

            return new Tree(parent, child, grandChild);
        }

        private NavElement CreateNavElement(float duration)
        {
            UIAnimationPreset preset = TestPresets.Create(duration, 0f);
            _presets.Add(preset);

            NavElement element = new NavElement();
            element.Visibility.Preset = preset;
            element.Visibility.Scheduler = _dispatcher.Scheduler;
            return element;
        }

        private readonly struct Tree
        {
            public readonly NavElement Parent;
            public readonly NavElement Child;
            public readonly NavElement GrandChild;

            public Tree(NavElement parent, NavElement child, NavElement grandChild)
            {
                Parent = parent;
                Child = child;
                GrandChild = grandChild;
            }
        }

        /// <summary>
        /// Provides Stub View functionality.
        /// </summary>
        private sealed class StubView : IUIVisibleView
        {
            public bool IsVisible { get; private set; }

            public void Show() => IsVisible = true;
            public void Hide() => IsVisible = false;
            public void InstantShow() => IsVisible = true;
            public void InstantHide() => IsVisible = false;
        }
    }
}
