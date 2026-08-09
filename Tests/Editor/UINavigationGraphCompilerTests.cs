using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI Navigation Graph Compiler Tests functionality.
    /// </summary>
    internal sealed class UINavigationGraphCompilerTests
    {
        private const string GraphPath = "Assets/UINavigationGraphCompilerTests.uinavgraph";

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (File.Exists(GraphPath))
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [Test]
        public void LegacyButtonEnumValue_NormalizesToSignal()
        {
            var runtimeTransition = new UINavigationTransition(
                (UINavigationTriggerKind)0,
                new UIKey("Test", "Legacy"),
                0f,
                false,
                "target",
                UINavigationTransitionKind.Push,
                System.Array.Empty<UINavigationAction>());
            Assert.AreEqual(UINavigationTriggerKind.Signal, runtimeTransition.TriggerKind);

            var output = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Test", "Legacy"),
                0f,
                UINavigationTransitionKind.Push);
            typeof(UINavigationOutputDefinition)
                .GetField("trigger", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(output, (UINavigationTriggerKind)0);
            output.OnAfterDeserialize();
            Assert.AreEqual(UINavigationTriggerKind.Signal, output.Trigger);

            var portal = new UINavigationPortalCondition();
            typeof(UINavigationPortalCondition)
                .GetField("kind", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(portal, (UINavigationPortalConditionKind)0);
            portal.OnAfterDeserialize();
            Assert.AreEqual(UINavigationPortalConditionKind.Signal, portal.Kind);
            Assert.AreEqual(UINavigationTriggerKind.Signal, portal.RuntimeTriggerKind);
        }

        [Test]
        public void CompiledTransitions_KeepSourcePortName()
        {
            LogAssert.ignoreFailingMessages = true;

            var toShop = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Test", "Enter"),
                0f,
                UINavigationTransitionKind.Push);
            var toShopAgain = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Test", "EnterAgain"),
                0f,
                UINavigationTransitionKind.Push);

            UINavigationAuthoringGraph graph =
                GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(GraphPath);
            Assert.IsNotNull(graph, "테스트 그래프를 만들지 못했습니다.");

            var start = new UINavigationStartNode { Position = new Vector2(0f, 0f) };
            var home = new UINavigationUINode
            {
                Position = new Vector2(240f, 0f),
                InitialNodeId = "home",
                InitialDisplayName = "Home",
                InitialOutputs = new[] { toShop, toShopAgain }
            };
            var shop = new UINavigationUINode
            {
                Position = new Vector2(520f, 0f),
                InitialNodeId = "shop",
                InitialDisplayName = "Shop"
            };

            graph.AddNode(start);
            graph.AddNode(home);
            graph.AddNode(shop);

            Connect(graph, start, UINavigationStartNode.StartPort, home);
            Connect(graph, home, toShop.GetPortName(), shop);
            Connect(graph, home, toShopAgain.GetPortName(), shop);
            GraphDatabase.SaveGraph(graph);

            var errors = new List<string>();
            UINavigationAsset asset = UINavigationGraphCompiler.Compile(graph, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));

            Assert.IsTrue(asset.TryGetNode("home", out UINavigationNode homeNode));
            string[] portNames = homeNode.Transitions
                .AsValueEnumerable()
                .Select(transition => transition.SourcePortName)
                .ToArray();

            Assert.AreEqual(
                new[] { toShop.GetPortName(), toShopAgain.GetPortName() },
                portNames,
                "두 전이가 각자의 출력 포트 이름을 가져야 와이어를 구분할 수 있습니다.");

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void Destination_IsTargetedBySignalAndDelayOutputs()
        {
            LogAssert.ignoreFailingMessages = true;
            UINavigationAuthoringGraph graph =
                GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(GraphPath);
            Assert.IsNotNull(graph);

            var start = new UINavigationStartNode { Position = Vector2.zero };
            UINavigationOutputDefinition homeSignal =
                UINavigationOutputDefinition.CreateCustomSignal("Home");
            var delay = new UINavigationOutputDefinition(
                UINavigationTriggerKind.TimeDelay,
                default,
                2f,
                UINavigationTransitionKind.Push);
            var a = new UINavigationUINode
            {
                Position = new Vector2(240f, 0f),
                InitialNodeId = "A",
                InitialDisplayName = "A",
                InitialOutputs = new[] { homeSignal }
            };
            var b = new UINavigationUINode
            {
                Position = new Vector2(240f, 220f),
                InitialNodeId = "B",
                InitialDisplayName = "B",
                InitialOutputs = new[] { delay }
            };
            var goHome = new UINavigationSendSignalNode
            {
                Position = new Vector2(560f, 100f),
                InitialNodeId = "GotoHome",
                InitialDisplayName = "Goto Home",
                InitialAddress = new UINavigationSignalAddress("Home")
            };
            var portalCondition = new UINavigationPortalCondition();
            portalCondition.SetCustomSignal("Home");
            var homePortal = new UINavigationPortalNode
            {
                Position = new Vector2(560f, 320f),
                InitialDisplayName = "Home Portal",
                InitialCondition = portalCondition
            };
            var home = new UINavigationUINode
            {
                Position = new Vector2(800f, 320f),
                InitialNodeId = "Home",
                InitialDisplayName = "Home"
            };

            graph.AddNode(start);
            graph.AddNode(a);
            graph.AddNode(b);
            graph.AddNode(goHome);
            graph.AddNode(homePortal);
            graph.AddNode(home);
            Connect(graph, start, UINavigationStartNode.StartPort, a);
            Connect(graph, a, homeSignal.GetPortName(), goHome);
            Connect(graph, b, delay.GetPortName(), goHome);
            Connect(graph, homePortal, UINavigationPortalNode.NextPort, home);
            GraphDatabase.SaveGraph(graph);

            var errors = new List<string>();
            UINavigationAsset asset = UINavigationGraphCompiler.Compile(graph, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            var service = new UINavigationService(asset);
            service.Initialize();
            Assert.IsTrue(service.Trigger("Home"));
            Assert.AreEqual("Home", service.ActiveNode.Id);
            Assert.IsTrue(service.GoTo("B"));
            Assert.IsTrue(service.Tick(2f));
            Assert.AreEqual("Home", service.ActiveNode.Id);

            Object.DestroyImmediate(asset);
        }

        [Test]
        public void SignalOutput_DuplicateCustomKeysProduceCompileError()
        {
            LogAssert.ignoreFailingMessages = true;
            UINavigationAuthoringGraph graph =
                GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(GraphPath);
            Assert.IsNotNull(graph);

            var start = new UINavigationStartNode();
            UINavigationOutputDefinition firstSignal =
                UINavigationOutputDefinition.CreateCustomSignal("Home");
            UINavigationOutputDefinition secondSignal =
                UINavigationOutputDefinition.CreateCustomSignal("Home");
            var source = new UINavigationUINode
            {
                InitialNodeId = "Source",
                InitialDisplayName = "Source",
                InitialOutputs = new[] { firstSignal, secondSignal }
            };
            var first = new UINavigationSendSignalNode
            {
                InitialNodeId = "First",
                InitialDisplayName = "First"
            };
            var second = new UINavigationSendSignalNode
            {
                InitialNodeId = "Second",
                InitialDisplayName = "Second"
            };

            graph.AddNode(start);
            graph.AddNode(source);
            graph.AddNode(first);
            graph.AddNode(second);
            Connect(graph, start, UINavigationStartNode.StartPort, source);
            Connect(graph, source, firstSignal.GetPortName(), first);
            Connect(graph, source, secondSignal.GetPortName(), second);
            GraphDatabase.SaveGraph(graph);

            var errors = new List<string>();
            UINavigationAsset asset = UINavigationGraphCompiler.Compile(graph, errors);

            Assert.IsTrue(errors.Exists(error =>
                error.Contains("Duplicate Custom Signal")));
            Object.DestroyImmediate(asset);
        }

        private static void Connect(Graph graph, Node from, string output, Node to)
        {
            Assert.IsTrue(
                graph.Connect(
                    from.GetOutputPortByName(output),
                    to.GetInputPortByName(UINavigationUINodeBase.EnterPort)),
                $"연결에 실패했습니다: {output}");
        }
    }
}
