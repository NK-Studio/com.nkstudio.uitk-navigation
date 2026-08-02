using System.IO;
using System.Text.RegularExpressions;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Elements;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Tests
{
    public sealed class UIKeyCatalogTests
    {
        private const string TempFolder = "Assets/__UITKNavigationCatalogTests";
        private const string TempUxml = TempFolder + "/CatalogTest.uxml";
        private const string TempGraph = TempFolder + "/CatalogTest.uinavgraph";

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolder))
                AssetDatabase.CreateFolder("Assets", "__UITKNavigationCatalogTests");
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TempFolder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void CategoryEntry_NormalizesAndRejectsDuplicates()
        {
            var category = new UIKeyCatalog.CategoryEntry(" Demo ");

            Assert.AreEqual("Demo", category.Name);
            Assert.IsTrue(category.Add(" Settings "));
            Assert.IsFalse(category.Add("Settings"));
            Assert.IsTrue(category.Contains("Settings"));
            Assert.IsTrue(category.Rename("Settings", "Shop"));
            Assert.IsFalse(category.Contains("Settings"));
            Assert.IsTrue(category.Contains("Shop"));
            Assert.IsTrue(category.Remove("Shop"));
            Assert.IsEmpty(category.Keys);
        }

        [Test]
        public void ExistingStringUxmlAttributes_CreateCustomElements()
        {
            WriteUxml("CatalogTest", "Main");

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TempUxml);
            Assert.IsNotNull(asset);

            TemplateContainer root = asset.CloneTree();
            NavElement element = root.Q<NavElement>("test-element");
            NavButton button = root.Q<NavButton>("test-button");

            Assert.IsNotNull(element);
            Assert.IsNotNull(button);
            Assert.AreEqual(new UIKey("CatalogTest", "Main"), element.Id);
            Assert.AreEqual(new UIKey("CatalogTest", "Main"), button.Signal);
        }

        [Test]
        public void ProjectScan_FindsViewAndSignalAddresses()
        {
            WriteUxml("CatalogTest", "Main");

            var usages = UIKeyProjectService.ScanProject(false)
                .AsValueEnumerable()
                .Where(usage => usage.AssetPath == TempUxml)
                .ToArray();

            Assert.AreEqual(2, usages.Length);
            Assert.IsTrue(usages.AsValueEnumerable().All(usage =>
                usage.Value == new UIKey("CatalogTest", "Main")));
        }

        [Test]
        public void RenameKey_UpdatesOnlySelectedCatalogKind()
        {
            WriteUxml("CatalogTest", "Main");

            Assert.IsTrue(
                UIKeyProjectService.RenameKey(
                    new UIKey("CatalogTest", "Main"),
                    "Settings",
                    UIKeyCatalogKind.View,
                    out string error),
                error);

            string contents = File.ReadAllText(TempUxml);
            StringAssert.Contains("view-key=\"Settings\"", contents);
            StringAssert.Contains("signal-key=\"Main\"", contents);
            StringAssert.DoesNotContain("view-key=\"Main\"", contents);

            Assert.IsTrue(
                UIKeyProjectService.RenameKey(
                    new UIKey("CatalogTest", "Main"),
                    "Settings",
                    UIKeyCatalogKind.Button,
                    out error),
                error);
            contents = File.ReadAllText(TempUxml);
            StringAssert.Contains("signal-key=\"Settings\"", contents);
        }

        [Test]
        public void NavigationToggle_UxmlAddressAndChangedValue_AreForwarded()
        {
            File.WriteAllText(
                TempUxml,
                @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:nav=""NKStudio.UITKNavigation.Elements"">
    <nav:NavToggle name=""test-toggle"" toggle-category=""Demo"" toggle-key=""Music"" />
</ui:UXML>");
            AssetDatabase.ImportAsset(
                TempUxml,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(TempUxml);
            NavToggle toggle = asset.CloneTree().Q<NavToggle>("test-toggle");
            UIKey received = default;
            bool receivedValue = false;
            void Handler(UIKey key, bool value)
            {
                received = key;
                receivedValue = value;
            }

            UINavigationEvents.ToggleRequested += Handler;
            try
            {
                bool nextValue = !toggle.value;
                using (ChangeEvent<bool> change =
                       ChangeEvent<bool>.GetPooled(toggle.value, nextValue))
                {
                    typeof(NavToggle)
                        .GetMethod(
                            "OnValueChanged",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic)
                        .Invoke(toggle, new object[] { change });
                }
            }
            finally
            {
                UINavigationEvents.ToggleRequested -= Handler;
            }

            Assert.AreEqual(new UIKey("Demo", "Music"), toggle.Toggle);
            Assert.AreEqual(toggle.Toggle, received);
            Assert.AreEqual(!toggle.value, receivedValue);
        }

        [Test]
        public void UiNode_CreatesStableDynamicPorts_AfterSaveAndReload()
        {
            UINavigationOutputDefinition signal = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                new UIKey("Demo", "Open"),
                0f,
                UINavigationTransitionKind.Push);
            UINavigationOutputDefinition delay = new UINavigationOutputDefinition(
                UINavigationTriggerKind.TimeDelay,
                default,
                2f,
                UINavigationTransitionKind.Replace);
            UINavigationOutputDefinition toggle = new UINavigationOutputDefinition(
                UINavigationTriggerKind.Toggle,
                new UIKey("Demo", "Music"),
                0f,
                UIToggleOutputCondition.Any,
                UIViewOutputCondition.Show);
            UINavigationOutputDefinition element = new UINavigationOutputDefinition(
                UINavigationTriggerKind.UIView,
                new UIKey("Demo", "Panel"),
                0f,
                UIToggleOutputCondition.On,
                UIViewOutputCondition.Hide);
            LogAssert.Expect(LogType.Error, new Regex("Screen.*"));
            LogAssert.Expect(LogType.Error, new Regex("Start.*0"));
            LogAssert.Expect(LogType.Error, new Regex(".*"));
            UINavigationAuthoringGraph graph =
                GraphDatabase.CreateGraph<UINavigationAuthoringGraph>(TempGraph);
            var ui = new UINavigationUINode
            {
                InitialOutputs = new[] { signal, delay, toggle, element }
            };
            graph.AddNode(ui);

            Assert.IsNotNull(ui.GetOutputPortByName(signal.GetPortName()));
            Assert.IsNotNull(ui.GetOutputPortByName(delay.GetPortName()));
            Assert.IsNotNull(ui.GetOutputPortByName(toggle.GetPortName()));
            Assert.IsNotNull(ui.GetOutputPortByName(element.GetPortName()));
            LogAssert.Expect(LogType.Error, new Regex("Start.*0"));
            LogAssert.Expect(LogType.Error, new Regex(".*"));
            GraphDatabase.SaveGraph(graph);

            UINavigationUINode loaded = GraphDatabase
                .LoadGraph<UINavigationAuthoringGraph>(TempGraph)
                .GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationUINode)
                .Select(node => (UINavigationUINode)node)
                .Single();
            UINavigationOutputDefinition[] outputs = loaded.GetOutputs();
            Assert.AreEqual(4, outputs.Length);
            Assert.IsNotNull(loaded.GetOutputPortByName(outputs[0].GetPortName()));
            Assert.IsNotNull(loaded.GetOutputPortByName(outputs[1].GetPortName()));
            Assert.IsNotNull(loaded.GetOutputPortByName(outputs[2].GetPortName()));
            Assert.IsNotNull(loaded.GetOutputPortByName(outputs[3].GetPortName()));
            Assert.AreEqual(UIToggleOutputCondition.Any, outputs[2].ToggleCondition);
            Assert.AreEqual(UIViewOutputCondition.Hide, outputs[3].ViewCondition);
        }

        private static void WriteUxml(string category, string key)
        {
            File.WriteAllText(
                TempUxml,
                $@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:nav=""NKStudio.UITKNavigation.Elements"">
    <nav:NavElement name=""test-element"" view-category=""{category}"" view-key=""{key}"">
        <nav:NavButton name=""test-button"" signal-category=""{category}"" signal-key=""{key}"" text=""Open"" />
    </nav:NavElement>
</ui:UXML>");
            AssetDatabase.ImportAsset(
                TempUxml,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
    }
}
