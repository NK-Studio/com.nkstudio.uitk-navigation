using System.Collections.Generic;
using System.IO;
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
        public void CompiledTransitions_KeepSourcePortName()
        {
            LogAssert.ignoreFailingMessages = true;

            var toShop = new UINavigationOutputDefinition(
                UINavigationTriggerKind.UIButton,
                new UIKey("Test", "Enter"),
                0f,
                UINavigationTransitionKind.Push);
            var toShopAgain = new UINavigationOutputDefinition(
                UINavigationTriggerKind.UIButton,
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
