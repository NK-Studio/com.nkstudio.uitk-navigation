using System.Collections.Generic;
using System.Reflection;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI Navigation Service Tests functionality.
    /// </summary>
    public sealed class UINavigationServiceTests
    {
        private TestNavigationGraphBuilder _builder;

        [SetUp]
        public void SetUp()
        {
            _builder = new TestNavigationGraphBuilder();
        }

        [TearDown]
        public void TearDown()
        {
            _builder.Dispose();
        }

        [Test]
        public void BackTransition_PopsHistoryInsteadOfPushing()
        {
            UINavigationService service = CreateTwoNodeService();

            service.Trigger(Signal("ToB"));
            Assert.AreEqual("B", service.ActiveNode.Id);
            Assert.AreEqual(1, service.BackStack.Count);

            service.Trigger(Signal("BackToA"));
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.AreEqual(0, service.BackStack.Count, "Back 전환은 스택을 쌓지 않고 꺼내야 한다.");
            Assert.AreEqual(1, service.ForwardStack.Count);
        }

        [Test]
        public void PushTransition_PushesPreviousAndClearsForward()
        {
            UINavigationService service = CreateThreeNodeService();

            service.GoTo("B");
            service.Back();
            Assert.AreEqual(1, service.ForwardStack.Count);

            service.GoTo("C");
            Assert.AreEqual("C", service.ActiveNode.Id);
            Assert.AreEqual(0, service.ForwardStack.Count, "새 Push는 앞으로 가기 스택을 비워야 한다.");
            Assert.AreEqual(1, service.BackStack.Count);
        }

        [Test]
        public void ReplaceTransition_LeavesBothStacksUntouched()
        {
            UINavigationService service = CreateThreeNodeService();

            service.GoTo("B");
            int backBefore = service.BackStack.Count;
            int forwardBefore = service.ForwardStack.Count;

            service.GoTo("C", UINavigationTransitionKind.Replace);

            Assert.AreEqual("C", service.ActiveNode.Id);
            Assert.AreEqual(backBefore, service.BackStack.Count);
            Assert.AreEqual(forwardBefore, service.ForwardStack.Count);
        }

        [Test]
        public void Back_WithEmptyStack_ReturnsFalseAndEmitsNoViewEvents()
        {
            UINavigationService service = CreateTwoNodeService();
            int viewEvents = 0;
            service.ShowRequested += _ => viewEvents++;
            service.HideRequested += _ => viewEvents++;

            Assert.IsFalse(service.Back());
            Assert.AreEqual(0, viewEvents);
            Assert.AreEqual("A", service.ActiveNode.Id);
        }

        [Test]
        public void BackThenForward_RestoresNodeAndStacks()
        {
            UINavigationService service = CreateThreeNodeService();

            service.GoTo("B");
            service.GoTo("C");
            Assert.AreEqual(2, service.BackStack.Count);

            service.Back();
            Assert.AreEqual("B", service.ActiveNode.Id);

            service.Forward();
            Assert.AreEqual("C", service.ActiveNode.Id);
            Assert.AreEqual(2, service.BackStack.Count);
            Assert.AreEqual(0, service.ForwardStack.Count);
        }

        [Test]
        public void RequestBeforeInitialize_IsQueuedInsteadOfDropped()
        {
            UINavigationService service = new UINavigationService(BuildThreeNodeGraph());

            Assert.IsTrue(service.GoTo("C"));
            Assert.IsNull(service.ActiveNode);

            service.Initialize();

            Assert.AreEqual("C", service.ActiveNode.Id, "초기화 전 요청은 버려지지 않고 초기화 직후에 적용되어야 한다.");
            Assert.AreEqual(1, service.BackStack.Count, "시작 노드가 뒤로 가기 스택에 들어가 있어야 한다.");
        }

        [Test]
        public void Initialize_HidesAllThenRunsConfiguredShowCommand()
        {
            UIKey mainId = _builder.CreateViewId("Main");
            _builder
                .AddNode("A")
                .AddView(
                    "A",
                    TestNavigationGraphBuilder.ViewSlot.ShowOnEnter,
                    mainId,
                    UIViewTransitionMode.Animated);
            UINavigationService service = new UINavigationService(_builder.Build());

            UIKey[] resync = null;
            UINavigationViewCommand[] show = null;
            service.ResyncRequested += ids => resync = ids;
            service.ShowCommandsRequested += commands => show = commands;

            service.Initialize();

            Assert.IsNotNull(resync);
            Assert.IsEmpty(resync);
            Assert.IsNotNull(show);
            Assert.AreEqual(1, show.Length);
            Assert.AreEqual(mainId, show[0].View);
            Assert.AreEqual(UIViewTransitionMode.Animated, show[0].Mode);
        }
        [Test]
        public void ViewInBothHideAndShow_AppearsOnlyInShowList()
        {
            UIKey shared = _builder.CreateViewId("Shared");
            _builder.AddNode("A").AddView("A", TestNavigationGraphBuilder.ViewSlot.HideOnExit, shared);
            _builder.AddNode("B").AddView("B", TestNavigationGraphBuilder.ViewSlot.ShowOnEnter, shared);

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            UIKey[] show = null;
            UIKey[] hide = null;
            service.ShowRequested += ids => show = ids;
            service.HideRequested += ids => hide = ids;

            service.GoTo("B");

            CollectionAssert.Contains(show, shared);
            CollectionAssert.DoesNotContain(hide, shared);
        }

        [Test]
        public void EventOrder_IsChangingThenHideThenShowThenChanged()
        {
            UINavigationService service = CreateTwoNodeService();
            List<string> order = new List<string>();

            service.NodeChanging += _ => order.Add("changing");
            service.HideRequested += _ => order.Add("hide");
            service.ShowRequested += _ => order.Add("show");
            service.NodeChanged += _ => order.Add("changed");

            service.GoTo("B");

            CollectionAssert.AreEqual(new[] { "changing", "hide", "show", "changed" }, order);
        }

        [Test]
        public void NodeChangingCancel_AbortsTransitionCompletely()
        {
            UINavigationService service = CreateTwoNodeService();
            service.NodeChanging += change => change.Cancel = true;

            int viewEvents = 0;
            service.ShowRequested += _ => viewEvents++;
            service.HideRequested += _ => viewEvents++;

            Assert.IsFalse(service.GoTo("B"));
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.AreEqual(0, service.BackStack.Count);
            Assert.AreEqual(0, viewEvents);
        }

        [Test]
        public void EmittedArrays_AreCopiesNotTheNodeBackingLists()
        {
            UIKey viewId = _builder.CreateViewId("View");
            _builder.AddNode("A");
            _builder.AddNode("B").AddView("B", TestNavigationGraphBuilder.ViewSlot.ShowOnEnter, viewId);

            UINavigationAsset asset = _builder.Build();
            UINavigationService service = new UINavigationService(asset);
            service.Initialize();

            UIKey[] captured = null;
            service.ShowRequested += ids => captured = ids;
            service.GoTo("B");

            Assert.AreEqual(1, captured.Length);
            captured[0] = default;

            asset.TryGetNode("B", out UINavigationNode node);
            Assert.AreEqual(viewId, node.ShowOnEnter[0], "구독자가 배열을 고쳐도 노드 데이터는 그대로여야 한다.");
        }

        [Test]
        public void TriggerFromInsideNodeChanged_IsQueuedNotRecursive()
        {
            UINavigationService service = CreateThreeNodeService();
            int changedCount = 0;
            bool navigatedOnce = false;

            service.NodeChanged += _ =>
            {
                changedCount++;

                if (navigatedOnce)
                    return;

                navigatedOnce = true;
                service.GoTo("C");
            };

            service.GoTo("B");

            Assert.AreEqual("C", service.ActiveNode.Id);
            Assert.AreEqual(2, changedCount, "중첩 요청은 큐에 쌓였다가 한 번씩만 처리되어야 한다.");
        }

        [Test]
        public void MaxHistoryDepth_TrimsOldestEntries()
        {
            _builder.AddNode("A").AddNode("B").AddNode("C").AddNode("D");
            UINavigationService service = new UINavigationService(_builder.Build()) { MaxHistoryDepth = 2 };
            service.Initialize();

            service.GoTo("B");
            service.GoTo("C");
            service.GoTo("D");

            Assert.AreEqual(2, service.BackStack.Count);
            CollectionAssert.AreEqual(new[] { "C", "B" }, service.BackStack.AsValueEnumerable().ToArray());
        }

        [Test]
        public void ClearHistoryNode_ResetsBothStacks()
        {
            _builder.AddNode("A").AddNode("B").AddNode("C", clearHistory: true);
            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            service.GoTo("B");
            Assert.AreEqual(1, service.BackStack.Count);

            service.GoTo("C");
            Assert.AreEqual(0, service.BackStack.Count);
            Assert.AreEqual(0, service.ForwardStack.Count);
        }

        [Test]
        public void BackTransition_WithEmptyStack_DoesNotUseGraphFallback()
        {
            _builder.AddNode("A");
            _builder.AddNode("B");
            _builder.AddTransition("A", "BackFromRoot", "B", UINavigationTransitionKind.Back);

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            Assert.IsFalse(service.Trigger(Signal("BackFromRoot")));
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.AreEqual(0, service.BackStack.Count);
            Assert.AreEqual(0, service.ForwardStack.Count);
        }

        [Test]
        public void TriggerButton_WithBackKey_PopsHistoryStack()
        {
            _builder.AddNode("A").AddNode("B");
            _builder.AddTransition("A", "ToB", "B", UINavigationTransitionKind.Push);

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            service.Trigger(Signal("ToB"));
            Assert.AreEqual("B", service.ActiveNode.Id);
            Assert.AreEqual(1, service.BackStack.Count);

            bool success = service.TriggerButton(new UIKey("Demo", "Back"));
            Assert.IsTrue(success);
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.AreEqual(0, service.BackStack.Count);
        }

        [Test]
        public void BackTransition_WithoutTargetNodeId_PopsHistoryStack()
        {
            _builder.AddNode("A").AddNode("B");
            _builder.AddTransition("A", "ToB", "B", UINavigationTransitionKind.Push);
            _builder.AddOutput("B", UINavigationTriggerKind.UIButton, "Back", 0f, false, null, UINavigationTransitionKind.Back);

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            service.Trigger(Signal("ToB"));
            Assert.AreEqual("B", service.ActiveNode.Id);
            Assert.AreEqual(1, service.BackStack.Count);

            bool success = service.TriggerButton(new UIKey("Demo", "Back"));
            Assert.IsTrue(success);
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.AreEqual(0, service.BackStack.Count);
        }

        [Test]
        public void TimeScaleAction_IsRequestedBeforeScreenTransition()
        {
            _builder.AddNode("A").AddNode("B");
            _builder.AddTransition(
                "A",
                "Pause",
                "B",
                UINavigationTransitionKind.Push,
                UINavigationAction.SetTimeScale(0f));

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();
            var order = new List<string>();
            UINavigationAction captured = null;
            service.ActionRequested += action =>
            {
                captured = action;
                order.Add("action");
            };
            service.NodeChanging += _ => order.Add("navigation");

            Assert.IsTrue(service.Trigger(Signal("Pause")));

            Assert.IsNotNull(captured);
            Assert.AreEqual(UINavigationActionKind.SetTimeScale, captured.Kind);
            Assert.AreEqual(0f, captured.TimeScale);
            Assert.AreEqual("B", service.ActiveNode.Id);
            CollectionAssert.AreEqual(new[] { "action", "navigation" }, order);
        }

        [Test]
        public void ApplicationQuitAction_CanTerminateWithoutChangingScreen()
        {
            _builder.AddNode("A");
            _builder.AddTransition(
                "A",
                "Quit",
                null,
                UINavigationTransitionKind.Replace,
                UINavigationAction.ApplicationQuit());

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();
            UINavigationAction captured = null;
            service.ActionRequested += action => captured = action;

            Assert.IsTrue(service.Trigger(Signal("Quit")));

            Assert.IsNotNull(captured);
            Assert.AreEqual(UINavigationActionKind.ApplicationQuit, captured.Kind);
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.AreEqual(0, service.BackStack.Count);
        }

        [Test]
        public void SceneActions_PreserveTheirValuesAndExecutionOrder()
        {
            _builder.AddNode("A").AddNode("B");
            _builder.AddTransition(
                "A",
                "ChangeScene",
                "B",
                UINavigationTransitionKind.Replace,
                UINavigationAction.LoadScene("Gameplay", UnityEngine.SceneManagement.LoadSceneMode.Additive),
                UINavigationAction.SetActiveScene("Gameplay"),
                UINavigationAction.UnloadScene("Bootstrap"));

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();
            var actions = new List<UINavigationAction>();
            service.ActionRequested += actions.Add;

            Assert.IsTrue(service.Trigger(Signal("ChangeScene")));

            Assert.AreEqual(3, actions.Count);
            Assert.AreEqual(UINavigationActionKind.LoadScene, actions[0].Kind);
            Assert.AreEqual("Gameplay", actions[0].SceneName);
            Assert.AreEqual(
                UnityEngine.SceneManagement.LoadSceneMode.Additive,
                actions[0].LoadSceneMode);
            Assert.AreEqual(UINavigationActionKind.SetActiveScene, actions[1].Kind);
            Assert.AreEqual("Gameplay", actions[1].SceneName);
            Assert.AreEqual(UINavigationActionKind.UnloadScene, actions[2].Kind);
            Assert.AreEqual("Bootstrap", actions[2].SceneName);
            Assert.AreEqual("B", service.ActiveNode.Id);
        }

        [Test]
        public void DebugLogAction_PreservesLogTypeAndMessage()
        {
            UINavigationAction action = UINavigationAction.DebugLog(
                UINavigationDebugLogType.Warning,
                "Navigation warning");

            Assert.AreEqual(UINavigationActionKind.DebugLog, action.Kind);
            Assert.AreEqual(UINavigationDebugLogType.Warning, action.DebugLogType);
            Assert.AreEqual("Navigation warning", action.DebugMessage);
        }

        [TestCase((int)UINavigationDebugLogType.Normal, LogType.Log)]
        [TestCase((int)UINavigationDebugLogType.Warning, LogType.Warning)]
        [TestCase((int)UINavigationDebugLogType.Error, LogType.Error)]
        public void Navigator_OutputsDebugLogWithSelectedType(
            int debugLogTypeValue,
            LogType expectedLogType)
        {
            UINavigationDebugLogType debugLogType =
                (UINavigationDebugLogType)debugLogTypeValue;
            GameObject navigatorObject = new GameObject("Debug Navigator");
            navigatorObject.SetActive(false);

            try
            {
                UINavigatorBehaviour navigator =
                    navigatorObject.AddComponent<UINavigatorBehaviour>();
                MethodInfo executeAction = typeof(UINavigatorBehaviour).GetMethod(
                    "ExecuteAction",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                LogAssert.Expect(expectedLogType, "Debug node message");
                executeAction.Invoke(
                    navigator,
                    new object[]
                    {
                        UINavigationAction.DebugLog(
                            debugLogType,
                            "Debug node message")
                    });
            }
            finally
            {
                Object.DestroyImmediate(navigatorObject);
            }
        }

        private static UIKey Signal(string key) => new UIKey("Test", key);

        [Test]
        public void Delay_UsesExplicitUnscaledDelta_AndRunsOncePerEntry()
        {
            _builder.AddNode("A").AddNode("B");
            _builder.AddOutput(
                "A",
                UINavigationTriggerKind.TimeDelay,
                string.Empty,
                2f,
                false,
                "B");
            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            Assert.IsFalse(service.Tick(1.5f));
            Assert.AreEqual("A", service.ActiveNode.Id);
            Assert.IsTrue(service.Tick(0.5f));
            Assert.AreEqual("B", service.ActiveNode.Id);
            Assert.IsFalse(service.Tick(10f));
        }

        [Test]
        public void Toggle_OnlyRunsMatchingChangedValueBranch()
        {
            _builder.AddNode("A").AddNode("True").AddNode("False");
            _builder.AddOutput(
                "A",
                UINavigationTriggerKind.Toggle,
                "Music",
                0f,
                true,
                "True");
            _builder.AddOutput(
                "A",
                UINavigationTriggerKind.Toggle,
                "Music",
                0f,
                false,
                "False");
            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            Assert.IsTrue(service.TriggerToggle(Signal("Music"), false));
            Assert.AreEqual("False", service.ActiveNode.Id);
        }

        [Test]
        public void Portal_OverridesActiveNodeLocalOutput()
        {
            _builder
                .AddNode("A")
                .AddNode("Local")
                .AddNode("Global")
                .AddOutput(
                    "A",
                    UINavigationTriggerKind.Signal,
                    "Go",
                    0f,
                    false,
                    "Local")
                .AddPortal(
                    UINavigationTriggerKind.Signal,
                    "Go",
                    false,
                    "Global");

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            Assert.IsTrue(service.Trigger(Signal("Go")));
            Assert.AreEqual("Global", service.ActiveNode.Id);
        }

        [Test]
        public void DestinationEnterViewCommand_OverridesExitCommandMode()
        {
            UIKey shared = Signal("SharedView");
            _builder
                .AddNode("A")
                .AddView(
                    "A",
                    TestNavigationGraphBuilder.ViewSlot.ShowOnExit,
                    shared,
                    UIViewTransitionMode.Instant)
                .AddNode("B")
                .AddView(
                    "B",
                    TestNavigationGraphBuilder.ViewSlot.ShowOnEnter,
                    shared,
                    UIViewTransitionMode.Animated);

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();
            UINavigationViewCommand[] captured = null;
            service.ShowCommandsRequested += commands => captured = commands;

            service.GoTo("B");

            Assert.AreEqual(1, captured.Length);
            Assert.AreEqual(shared, captured[0].View);
            Assert.AreEqual(UIViewTransitionMode.Animated, captured[0].Mode);
        }

        [Test]
        public void ToggleAny_MatchesBothValues()
        {
            _builder
                .AddNode("A")
                .AddNode("B")
                .AddOutput(
                    "A",
                    UINavigationTriggerKind.Toggle,
                    "Music",
                    0f,
                    UIToggleOutputCondition.Any,
                    UIViewOutputCondition.Show,
                    "B");

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            Assert.IsTrue(service.TriggerToggle(Signal("Music"), false));
            Assert.AreEqual("B", service.ActiveNode.Id);
        }

        [Test]
        public void UIViewOutput_MatchesViewAndShowHideCondition()
        {
            _builder
                .AddNode("A")
                .AddNode("B")
                .AddOutput(
                    "A",
                    UINavigationTriggerKind.UIView,
                    "Panel",
                    0f,
                    UIToggleOutputCondition.On,
                    UIViewOutputCondition.Hide,
                    "B");

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();

            Assert.IsFalse(service.TriggerView(Signal("Panel"), UIViewOutputCondition.Show));
            Assert.IsTrue(service.TriggerView(Signal("Panel"), UIViewOutputCondition.Hide));
            Assert.AreEqual("B", service.ActiveNode.Id);
        }

        [Test]
        public void UIViewOutputRaisedDuringViewDispatch_IsQueued()
        {
            UIKey panel = Signal("Panel");
            _builder
                .AddNode("A")
                .AddNode("B")
                .AddView("B", TestNavigationGraphBuilder.ViewSlot.ShowOnEnter, panel)
                .AddNode("C")
                .AddOutput(
                    "B",
                    UINavigationTriggerKind.UIView,
                    "Panel",
                    0f,
                    UIToggleOutputCondition.On,
                    UIViewOutputCondition.Show,
                    "C");

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();
            service.ShowCommandsRequested += commands =>
            {
                for (int i = 0; i < commands.Length; i++)
                {
                    if (commands[i].View == panel &&
                        commands[i].Mode == UIViewTransitionMode.Animated)
                    {
                        service.TriggerView(panel, UIViewOutputCondition.Show);
                    }
                }
            };

            Assert.IsTrue(service.GoTo("B"));
            Assert.AreEqual("C", service.ActiveNode.Id);
        }
        [Test]
        public void DuplicateNavigator_DisablesSecondInstance()
        {
            GameObject firstObject = new GameObject("Navigator A");
            GameObject secondObject = new GameObject("Navigator B");
            firstObject.SetActive(false);
            secondObject.SetActive(false);

            try
            {
                UINavigatorBehaviour first = firstObject.AddComponent<UINavigatorBehaviour>();
                UINavigatorBehaviour second = secondObject.AddComponent<UINavigatorBehaviour>();
                MethodInfo awake = typeof(UINavigatorBehaviour).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo onEnable = typeof(UINavigatorBehaviour).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                awake.Invoke(first, null);
                onEnable.Invoke(first, null);

                LogAssert.Expect(
                    LogType.Error,
                    "[UINavigation] 'Navigator B'에 UINavigatorBehaviour가 중복으로 존재하여 비활성화합니다. " +
                    "이미 'Navigator A'가 활성 상태입니다.");
                awake.Invoke(second, null);

                Assert.IsTrue(first.enabled);
                Assert.IsFalse(second.enabled);
                Assert.IsNotNull(first.Service);
                Assert.IsNull(second.Service);
            }
            finally
            {
                Object.DestroyImmediate(secondObject);
                Object.DestroyImmediate(firstObject);
            }
        }

        private UINavigationService CreateTwoNodeService()
        {
            _builder.AddNode("A").AddNode("B");
            _builder.AddTransition("A", "ToB", "B", UINavigationTransitionKind.Push);
            _builder.AddTransition("B", "BackToA", "A", UINavigationTransitionKind.Back);

            UINavigationService service = new UINavigationService(_builder.Build());
            service.Initialize();
            return service;
        }

        private UINavigationService CreateThreeNodeService()
        {
            UINavigationService service = new UINavigationService(BuildThreeNodeGraph());
            service.Initialize();
            return service;
        }

        private UINavigationAsset BuildThreeNodeGraph()
        {
            _builder.AddNode("A").AddNode("B").AddNode("C");
            return _builder.Build();
        }
    }
}
