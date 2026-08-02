using System.IO;
using NKStudio.UITKNavigation.Editor.Navigation;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine.TestTools;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    /// <summary>
    /// Provides UI Navigation Node Id Repair Tests functionality.
    /// </summary>
    internal sealed class UINavigationNodeIdRepairTests
    {
        private const string GraphPath = "Assets/UINavigationNodeIdRepairTests.uinavgraph";

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            if (File.Exists(GraphPath))
                AssetDatabase.DeleteAsset(GraphPath);
        }

        [Test]
        public void DuplicateNodeId_IsReassigned()
        {
            LogAssert.ignoreFailingMessages = true;
            UINavigationAuthoringGraph graph = CreateGraph("dup", "dup");

            Assert.IsTrue(UINavigationNodeIdRepair.EnsureUniqueNodeIdsNow(graph));

            string[] ids = GetNodeIds(graph);
            Assert.AreEqual(2, ids.Length);
            Assert.AreEqual("dup", ids[0], "먼저 있던 노드는 Node ID를 유지해야 합니다.");
            Assert.AreNotEqual("dup", ids[1]);
            Assert.IsNotEmpty(ids[1]);
        }

        [Test]
        public void UniqueNodeIds_AreLeftUntouched()
        {
            LogAssert.ignoreFailingMessages = true;
            UINavigationAuthoringGraph graph = CreateGraph("home", "shop");

            Assert.IsFalse(UINavigationNodeIdRepair.EnsureUniqueNodeIdsNow(graph));
            Assert.AreEqual(new[] { "home", "shop" }, GetNodeIds(graph));
        }

        private static UINavigationAuthoringGraph CreateGraph(params string[] nodeIds)
        {
            if (File.Exists(GraphPath))
                AssetDatabase.DeleteAsset(GraphPath);

            UINavigationAuthoringGraph graph =
                GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(GraphPath);
            Assert.IsNotNull(graph, "테스트 그래프를 만들지 못했습니다.");

            foreach (string nodeId in nodeIds)
            {
                graph.AddNode(new UINavigationUINode
                {
                    InitialNodeId = nodeId,
                    InitialDisplayName = nodeId
                });
            }

            GraphDatabase.SaveGraph(graph);
            return graph;
        }

        private static string[] GetNodeIds(UINavigationAuthoringGraph graph)
        {
            return graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationUINodeBase)
                .Select(node => ((UINavigationUINodeBase)node).GetNodeId())
                .ToArray();
        }
    }
}
