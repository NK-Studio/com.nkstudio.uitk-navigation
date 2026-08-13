using System.Collections;
using System.Collections.Generic;
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
        public void RepeatedLookups_DoNotRebuildNormalizedCollections()
        {
            var catalog = (UIKeyCatalog)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(UIKeyCatalog));
            var first = new UIKeyCatalog.CategoryEntry(" Demo ");
            first.Add(" Beta ");
            first.Add("Alpha");
            var duplicate = new UIKeyCatalog.CategoryEntry("Demo");
            duplicate.Add("Ignored");

            typeof(UIKeyCatalog)
                .GetField(
                    "viewCategories",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic)
                .SetValue(catalog, new List<UIKeyCatalog.CategoryEntry> { first, duplicate });

            IReadOnlyList<UIKeyCatalog.CategoryEntry> categories =
                catalog.GetCategories(UIKeyCatalogKind.View);
            IEnumerable<UIKey> keys = catalog.GetKeys(UIKeyCatalogKind.View);

            Assert.AreEqual(1, categories.Count);
            CollectionAssert.AreEqual(new[] { "Alpha", "Beta" }, categories[0].Keys);
            Assert.IsTrue(catalog.Contains(new UIKey("Demo", "Alpha"), UIKeyCatalogKind.View));
            Assert.IsFalse(catalog.Contains(new UIKey("Demo", "Missing"), UIKeyCatalogKind.View));
            Assert.AreSame(categories, catalog.GetCategories(UIKeyCatalogKind.View));
            Assert.AreSame(keys, catalog.GetKeys(UIKeyCatalogKind.View));
        }

        [Test]
        public void UIKeyPicker_GraphInspectorLayout_MatchesConstantFieldHierarchy()
        {
            var picker = new UIKeyPickerField(
                "Database Signal",
                () => new UIKey("Default", "Home"),
                _ => { },
                () => UIKeyCatalogKind.Signal,
                graphInspectorLayout: true);

            Assert.IsTrue(picker.ClassListContains("ge-model-property-field"));
            Assert.IsTrue(picker.ClassListContains("unity-property-field"));
            Assert.IsFalse(picker.ClassListContains("unity-base-field"));
            Assert.AreEqual(1, picker.childCount);

            VisualElement field = picker[0];
            Assert.AreEqual("field", field.name);
            Assert.IsTrue(field.ClassListContains("unity-base-field"));

            // The picker rows live in the content container, so the label and the input
            // are reached through the hierarchy instead of the content indexer.
            Assert.AreEqual(2, field.hierarchy.childCount);

            Label label = field.hierarchy[0] as Label;
            Assert.IsNotNull(label);
            Assert.IsTrue(label.ClassListContains("unity-base-field__label"));
            Assert.IsTrue(label.ClassListContains("ge-model-property-field__label"));
            Assert.IsTrue(label.ClassListContains("unity-property-field__label"));

            VisualElement input = field.hierarchy[1];
            Assert.IsTrue(input.ClassListContains("unity-base-field__input"));
            Assert.IsTrue(input.ClassListContains("ge-model-property-field__input"));
            Assert.IsTrue(input.ClassListContains("unity-property-field__input"));
            Assert.AreEqual(StyleKeyword.Null, input.style.marginLeft.keyword);
            Assert.AreEqual(StyleKeyword.Null, input.style.marginRight.keyword);

            List<Button> buttons = input.Query<Button>().ToList();
            Assert.IsNotEmpty(buttons);
            Assert.IsTrue(buttons.TrueForAll(button =>
                button.ClassListContains(Button.ussClassName)));
        }

        [UnityTest]
        public IEnumerator UIKeyPicker_GraphInspectorLayout_FitsStandardFieldBounds()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.position = new Rect(100f, 100f, 840f, 240f);
                VisualElement host = window.rootVisualElement;
                host.style.paddingLeft = 24f;
                host.style.paddingRight = 24f;
                host.AddToClassList("unity-inspector-element");
                host.AddToClassList("unity-inspector-main-container");

                StyleSheet graphFieldStyle = EditorGUIUtility.Load(
                    "StyleSheets/GraphToolkit/Field.uss") as StyleSheet;
                Assert.IsNotNull(graphFieldStyle);
                host.styleSheets.Add(graphFieldStyle);
                StyleSheet customizableFieldStyle = EditorGUIUtility.Load(
                    "StyleSheets/GraphToolkit/CustomizableModelPropertyField.uss") as
                    StyleSheet;
                Assert.IsNotNull(customizableFieldStyle);
                host.styleSheets.Add(customizableFieldStyle);

                var referenceRoot = new VisualElement();
                referenceRoot.AddToClassList("ge-model-property-field");
                referenceRoot.AddToClassList("unity-property-field");
                var referenceField = new TextField("Display Name");
                referenceField.AddToClassList(
                    BaseField<string>.alignedFieldUssClassName);
                referenceField.labelElement.AddToClassList(
                    "ge-model-property-field__label");
                referenceField.labelElement.AddToClassList(
                    "unity-property-field__label");
                VisualElement referenceInput = referenceField.Q<VisualElement>(
                    className: "unity-base-field__input");
                referenceInput.AddToClassList("ge-model-property-field__input");
                referenceInput.AddToClassList("unity-property-field__input");
                referenceRoot.Add(referenceField);
                host.Add(referenceRoot);

                var picker = new UIKeyPickerField(
                    "Database Signal",
                    () => new UIKey("LayoutProbe", "Missing"),
                    _ => { },
                    () => UIKeyCatalogKind.Signal,
                    graphInspectorLayout: true);
                host.Add(picker);

                window.Show();
                yield return null;
                yield return null;

                VisualElement pickerInput = picker.Q<VisualElement>(
                    className: "ge-model-property-field__input");
                Assert.IsNotNull(pickerInput);
                Assert.That(
                    pickerInput.worldBound.xMin,
                    Is.EqualTo(referenceInput.worldBound.xMin).Within(0.5f));
                Assert.That(
                    pickerInput.worldBound.xMax,
                    Is.EqualTo(referenceInput.worldBound.xMax).Within(0.5f));

                List<Button> buttons = pickerInput.Query<Button>().ToList();
                Assert.IsNotEmpty(buttons);
                Assert.IsTrue(buttons.TrueForAll(button =>
                    button.worldBound.xMin >= pickerInput.worldBound.xMin - 0.5f &&
                    button.worldBound.xMax <= pickerInput.worldBound.xMax + 0.5f));
            }
            finally
            {
                window.Close();
            }
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
        public void ProjectScan_ClassifiesNavButtonAsSignalAndNavToggleAsToggle()
        {
            File.WriteAllText(
                TempUxml,
                @"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:nav=""NKStudio.UITKNavigation.Elements"">
    <nav:NavButton name=""test-button"" signal-category=""Demo"" signal-key=""Open"" />
    <nav:NavToggle name=""test-toggle"" toggle-category=""Demo"" toggle-key=""Music"" />
</ui:UXML>");
            AssetDatabase.ImportAsset(
                TempUxml,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            UIKeyUsage[] usages = UIKeyProjectService.ScanProject(false)
                .AsValueEnumerable()
                .Where(usage => usage.AssetPath == TempUxml)
                .ToArray();

            Assert.AreEqual(2, usages.Length);
            Assert.IsTrue(usages.AsValueEnumerable().Any(usage =>
                usage.Value == new UIKey("Demo", "Open") &&
                usage.CatalogKind == UIKeyCatalogKind.Signal));
            Assert.IsTrue(usages.AsValueEnumerable().Any(usage =>
                usage.Value == new UIKey("Demo", "Music") &&
                usage.CatalogKind == UIKeyCatalogKind.Toggle));
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
                    UIKeyCatalogKind.Signal,
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
