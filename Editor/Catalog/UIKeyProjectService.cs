using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    internal enum UIKeyUsageKind
    {
        View,
        Toggle,
        Signal,
        ShowOnEnter,
        HideOnEnter,
        ShowOnExit,
        HideOnExit
    }

    internal readonly struct UIKeyUsage
    {
        internal UIKeyUsage(
            UIKey value,
            string assetPath,
            UIKeyUsageKind kind,
            string context)
        {
            Value = value;
            AssetPath = assetPath;
            Kind = kind;
            Context = context;
        }

        internal UIKey Value { get; }
        internal string AssetPath { get; }
        internal UIKeyUsageKind Kind { get; }
        internal UIKeyCatalogKind CatalogKind => UIKeyProjectService.GetCatalogKind(Kind);
        internal string Context { get; }
    }

    internal static class UIKeyProjectService
    {
        private static bool _catalogMigrationInProgress;

        internal static IReadOnlyList<UIKeyUsage> ScanProject(bool logFailures = true)
        {
            var usages = new List<UIKeyUsage>();
            ScanUxml(usages, logFailures);
            ScanGraphs(usages, logFailures);
            return usages;
        }

        internal static void EnsureCatalogIsSeparated()
        {
            if (_catalogMigrationInProgress ||
                !UIKeyCatalog.instance.NeedsSeparatedCatalogMigration)
            {
                return;
            }

            _catalogMigrationInProgress = true;
            try
            {
                UIKeyCatalog.instance.MigrateToSeparatedCatalog(ScanProject(false));
            }
            finally
            {
                _catalogMigrationInProgress = false;
            }
        }

        internal static int CountUsages(
            UIKey value,
            IReadOnlyList<UIKeyUsage> usages,
            UIKeyCatalogKind? kind = null)
        {
            return usages?.AsValueEnumerable().Count(usage =>
                usage.Value == value &&
                (!kind.HasValue || usage.CatalogKind == kind.Value)) ?? 0;
        }

        internal static int CountCategoryUsages(
            string category,
            IReadOnlyList<UIKeyUsage> usages,
            UIKeyCatalogKind? kind = null)
        {
            return usages?.AsValueEnumerable().Count(usage =>
                string.Equals(usage.Value.Category, category, StringComparison.Ordinal) &&
                (!kind.HasValue || usage.CatalogKind == kind.Value)) ?? 0;
        }

        internal static UIKeyCatalogKind GetCatalogKind(UIKeyUsageKind kind)
        {
            return kind switch
            {
                UIKeyUsageKind.View or
                    UIKeyUsageKind.ShowOnEnter or
                    UIKeyUsageKind.HideOnEnter or
                    UIKeyUsageKind.ShowOnExit or
                    UIKeyUsageKind.HideOnExit => UIKeyCatalogKind.View,
                UIKeyUsageKind.Toggle => UIKeyCatalogKind.Toggle,
                _ => UIKeyCatalogKind.Signal
            };
        }

        internal static bool RenameCategory(
            string oldCategory,
            string newCategory,
            UIKeyCatalogKind kind,
            out string error)
        {
            return Rename(
                key => string.Equals(key.Category, oldCategory, StringComparison.Ordinal)
                    ? new UIKey(newCategory, key.Key)
                    : key,
                key => string.Equals(key.Category, oldCategory, StringComparison.Ordinal),
                kind,
                out error);
        }

        internal static bool RenameKey(
            UIKey oldValue,
            string newKey,
            UIKeyCatalogKind kind,
            out string error)
        {
            return Rename(
                key => key == oldValue
                    ? new UIKey(oldValue.Category, newKey)
                    : key,
                key => key == oldValue,
                kind,
                out error);
        }

        private static bool Rename(
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches,
            UIKeyCatalogKind kind,
            out string error)
        {
            error = null;
            IReadOnlyList<UIKeyUsage> usages = ScanProject();
            string[] paths = usages
                .AsValueEnumerable()
                .Where(usage => usage.CatalogKind == kind && matches(usage.Value))
                .Select(usage => usage.AssetPath)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                return true;

            var backups = new Dictionary<string, string>(StringComparer.Ordinal);
            try
            {
                foreach (string path in paths)
                {
                    if (!File.Exists(path))
                        throw new FileNotFoundException("사용 중인 자산을 찾지 못했습니다.", path);
                    backups[path] = File.ReadAllText(path);
                }

                var uxmlDocuments = new Dictionary<string, XmlDocument>(StringComparer.Ordinal);
                foreach (string path in paths.AsValueEnumerable().Where(IsUxmlPath))
                    uxmlDocuments[path] = LoadUxml(path);

                foreach ((string path, XmlDocument document) in uxmlDocuments)
                {
                    if (ReplaceUxml(document, replace, matches, kind))
                        document.Save(path);
                }

                foreach (string path in paths.AsValueEnumerable().Where(IsGraphPath))
                {
                    UINavigationAuthoringGraph graph =
                        GraphDatabase.LoadGraph<UINavigationAuthoringGraph>(path);
                    if (graph == null)
                        throw new InvalidOperationException($"Graph를 불러오지 못했습니다: {path}");

                    if (ReplaceGraph(graph, replace, matches, kind))
                        GraphDatabase.SaveGraph(graph);
                }

                foreach (string path in paths)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                foreach ((string path, string contents) in backups)
                {
                    try
                    {
                        File.WriteAllText(path, contents);
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception restoreException)
                    {
                        Debug.LogException(restoreException);
                    }
                }

                AssetDatabase.Refresh();
                error = exception.Message;
                Debug.LogException(exception);
                return false;
            }
        }

        private static void ScanUxml(List<UIKeyUsage> usages, bool logFailures)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:VisualTreeAsset"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsUxmlPath(path))
                    continue;

                try
                {
                    XmlDocument document = LoadUxml(path);
                    foreach (XmlElement element in EnumerateElements(document))
                    {
                        if (element.LocalName ==
                            nameof(NKStudio.UITKNavigation.Elements.NavElement))
                        {
                            AddUxmlUsage(
                                usages,
                                path,
                                element,
                                "view-category",
                                "view-key",
                                UIKeyUsageKind.View);
                        }
                        else if (element.LocalName ==
                                 nameof(NKStudio.UITKNavigation.Elements.NavButton))
                        {
                            AddUxmlUsage(
                                usages,
                                path,
                                element,
                                "signal-category",
                                "signal-key",
                                UIKeyUsageKind.Signal);
                        }
                        else if (element.LocalName == "NavToggle")
                        {
                            AddUxmlUsage(
                                usages,
                                path,
                                element,
                                "toggle-category",
                                "toggle-key",
                                UIKeyUsageKind.Toggle);
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (logFailures)
                        Debug.LogWarning($"UXML 주소를 읽지 못했습니다: {path}\n{exception.Message}");
                }
            }
        }

        private static void ScanGraphs(List<UIKeyUsage> usages, bool logFailures)
        {
            foreach (string path in AssetDatabase.GetAllAssetPaths().AsValueEnumerable().Where(IsGraphPath))
            {
                try
                {
                    UINavigationAuthoringGraph graph =
                        GraphDatabase.LoadGraph<UINavigationAuthoringGraph>(path);
                    if (graph == null)
                        continue;

                    foreach (INode node in graph.GetNodes())
                    {
                        if (node is UINavigationSendSignalNode destination)
                        {
                            UINavigationSignalAddress address = destination.GetAddress();
                            if (address.Kind == UINavigationSignalAddressKind.Database &&
                                address.DatabaseSignal.IsValid)
                            {
                                usages.Add(new UIKeyUsage(
                                    address.DatabaseSignal,
                                    path,
                                    UIKeyUsageKind.Signal,
                                    destination.ToString()));
                            }
                        }
                        else if (node is UINavigationPortalNode portal)
                        {
                            UINavigationPortalCondition condition = portal.GetCondition();
                            UIKey key = condition.Key;
                            if (!(condition.RuntimeTriggerKind == UINavigationTriggerKind.Signal &&
                                  condition.SignalAddressKind == UINavigationSignalAddressKind.Custom) &&
                                key.IsValid)
                            {
                                usages.Add(new UIKeyUsage(
                                    key,
                                    path,
                                    condition.RuntimeTriggerKind is
                                        UINavigationTriggerKind.Toggle
                                        ? UIKeyUsageKind.Toggle
                                        : UIKeyUsageKind.Signal,
                                    portal.ToString()));
                            }
                        }
                        else if (node is UINavigationUINode uiNode)
                        {
                            AddScreenUsages(usages, path, uiNode);
                            foreach (UINavigationOutputDefinition output in uiNode
                                         .GetOutputs()
                                         .AsValueEnumerable()
                                         .Where(item => item != null &&
                                                        item.Trigger != UINavigationTriggerKind.TimeDelay &&
                                                        !(item.Trigger == UINavigationTriggerKind.Signal &&
                                                          item.SignalAddressKind == UINavigationSignalAddressKind.Custom) &&
                                                        item.Key.IsValid))
                            {
                                usages.Add(new UIKeyUsage(
                                    output.Key,
                                    path,
                                    output.Trigger switch
                                    {
                                        UINavigationTriggerKind.Toggle =>
                                            UIKeyUsageKind.Toggle,
                                        UINavigationTriggerKind.UIView =>
                                            UIKeyUsageKind.View,
                                        _ => UIKeyUsageKind.Signal
                                    },
                                    context: uiNode.ToString()));
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (logFailures)
                        Debug.LogWarning($"Navigation Graph 주소를 읽지 못했습니다: {path}\n{exception.Message}");
                }
            }
        }

        private static void AddScreenUsages(
            ICollection<UIKeyUsage> usages,
            string path,
            UINavigationUINode screen)
        {
            string context = screen.GetOptionValue(
                UINavigationUINodeBase.DisplayNameOption,
                "Screen");

            AddValues(screen.GetOnEnterUI()?.Show, UIKeyUsageKind.ShowOnEnter);
            AddValues(screen.GetOnEnterUI()?.Hide, UIKeyUsageKind.HideOnEnter);
            AddValues(screen.GetOnExitUI()?.Show, UIKeyUsageKind.ShowOnExit);
            AddValues(screen.GetOnExitUI()?.Hide, UIKeyUsageKind.HideOnExit);

            void AddValues(IEnumerable<UINavigationViewCommand> values, UIKeyUsageKind kind)
            {
                if (values == null)
                    return;

                foreach (UINavigationViewCommand value in values.AsValueEnumerable().Where(item => item.IsValid))
                    usages.Add(new UIKeyUsage(value.View, path, kind, context));
            }
        }

        private static void AddUxmlUsage(
            ICollection<UIKeyUsage> usages,
            string path,
            XmlElement element,
            string categoryAttribute,
            string keyAttribute,
            UIKeyUsageKind kind)
        {
            var value = new UIKey(
                element.GetAttribute(categoryAttribute),
                element.GetAttribute(keyAttribute));
            if (!value.IsValid)
                return;

            usages.Add(new UIKeyUsage(
                value,
                path,
                kind,
                element.GetAttribute("name")));
        }

        private static bool ReplaceUxml(
            XmlDocument document,
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches,
            UIKeyCatalogKind targetKind)
        {
            bool changed = false;
            foreach (XmlElement element in EnumerateElements(document))
            {
                string categoryAttribute;
                string keyAttribute;
                UIKeyCatalogKind elementKind;
                if (element.LocalName ==
                    nameof(NKStudio.UITKNavigation.Elements.NavElement))
                {
                    categoryAttribute = "view-category";
                    keyAttribute = "view-key";
                    elementKind = UIKeyCatalogKind.View;
                }
                else if (element.LocalName ==
                         nameof(NKStudio.UITKNavigation.Elements.NavButton))
                {
                    categoryAttribute = "signal-category";
                    keyAttribute = "signal-key";
                    elementKind = UIKeyCatalogKind.Signal;
                }
                else if (element.LocalName == "NavToggle")
                {
                    categoryAttribute = "toggle-category";
                    keyAttribute = "toggle-key";
                    elementKind = UIKeyCatalogKind.Toggle;
                }
                else
                {
                    continue;
                }

                if (elementKind != targetKind)
                    continue;

                var current = new UIKey(
                    element.GetAttribute(categoryAttribute),
                    element.GetAttribute(keyAttribute));
                if (!matches(current))
                    continue;

                UIKey next = replace(current);
                element.SetAttribute(categoryAttribute, next.Category);
                element.SetAttribute(keyAttribute, next.Key);
                changed = true;
            }

            return changed;
        }

        private static bool ReplaceGraph(
            UINavigationAuthoringGraph graph,
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches,
            UIKeyCatalogKind targetKind)
        {
            bool changed = false;
            foreach (INode node in graph.GetNodes())
            {
                if (node is UINavigationSendSignalNode destination)
                {
                    if (targetKind != UIKeyCatalogKind.Signal)
                        continue;

                    UINavigationSignalAddress address = destination.GetAddress();
                    if (address.Kind != UINavigationSignalAddressKind.Database ||
                        !matches(address.DatabaseSignal))
                    {
                        continue;
                    }

                    INodeOption option = destination.GetNodeOptionByName(
                        UINavigationSendSignalNode.AddressOption);
                    address.SetDatabaseSignal(replace(address.DatabaseSignal));
                    changed |= TrySetOptionValue(option, address);
                }
                else if (node is UINavigationPortalNode portal)
                {
                    UINavigationPortalCondition condition = portal.GetCondition();
                    if (condition.RuntimeTriggerKind == UINavigationTriggerKind.Signal &&
                        condition.SignalAddressKind == UINavigationSignalAddressKind.Custom)
                    {
                        continue;
                    }
                    UIKeyCatalogKind portalKind =
                        condition.RuntimeTriggerKind is
                            UINavigationTriggerKind.Toggle
                            ? UIKeyCatalogKind.Toggle
                            : UIKeyCatalogKind.Signal;
                    if (portalKind != targetKind)
                        continue;

                    INodeOption option = portal.GetNodeOptionByName(
                        UINavigationPortalNode.ConditionOption);
                    if (option != null && matches(condition.Key))
                    {
                        condition.SetKey(replace(condition.Key));
                        changed |= TrySetOptionValue(option, condition);
                    }
                }
                else if (node is UINavigationUINode uiNode)
                {
                    if (targetKind == UIKeyCatalogKind.View)
                    {
                        changed |= ReplaceUIPhase(
                            uiNode,
                            UINavigationUINode.OnEnterUIOption,
                            replace,
                            matches);
                        changed |= ReplaceUIPhase(
                            uiNode,
                            UINavigationUINode.OnExitUIOption,
                            replace,
                            matches);
                    }

                    INodeOption outputsOption =
                        uiNode.GetNodeOptionByName(UINavigationUINode.DynamicOutputsOption);
                    UINavigationOutputDefinition[] outputs = uiNode.GetOutputs();
                    bool outputChanged = false;
                    foreach (UINavigationOutputDefinition output in outputs)
                    {
                        if (output == null ||
                            output.Trigger == UINavigationTriggerKind.TimeDelay ||
                            (output.Trigger == UINavigationTriggerKind.Signal &&
                             output.SignalAddressKind == UINavigationSignalAddressKind.Custom) ||
                            (output.Trigger switch
                            {
                                UINavigationTriggerKind.Toggle =>
                                    UIKeyCatalogKind.Toggle,
                                UINavigationTriggerKind.UIView =>
                                    UIKeyCatalogKind.View,
                                _ => UIKeyCatalogKind.Signal
                            }) != targetKind ||
                            !matches(output.Key))
                            continue;

                        output.SetKey(replace(output.Key));
                        outputChanged = true;
                    }

                    if (outputChanged)
                    {
                        changed |= TrySetOptionValue(
                            outputsOption,
                            new UINavigationOutputCollection(outputs));
                    }
                }
            }

            return changed;
        }

        private static bool ReplaceUIPhase(
            UINavigationUINode node,
            string optionName,
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches)
        {
            INodeOption option = node.GetNodeOptionByName(optionName);
            if (option == null ||
                !option.TryGetValue(out UINavigationUIPhase phase) ||
                phase == null)
                return false;

            bool changed = false;
            UINavigationViewCommand[] show =
                ReplaceCommands(phase.Show, replace, matches, ref changed);
            UINavigationViewCommand[] hide =
                ReplaceCommands(phase.Hide, replace, matches, ref changed);
            return changed &&
                   TrySetOptionValue(option, new UINavigationUIPhase(show, hide, phase.IsExit));
        }

        private static UINavigationViewCommand[] ReplaceCommands(
            UINavigationViewCommand[] values,
            Func<UIKey, UIKey> replace,
            Func<UIKey, bool> matches,
            ref bool changed)
        {
            values ??= Array.Empty<UINavigationViewCommand>();
            var result = new UINavigationViewCommand[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                UINavigationViewCommand value = values[index];
                UIKey next = matches(value.View) ? replace(value.View) : value.View;
                result[index] = new UINavigationViewCommand(next, value.Mode);
                changed |= next != value.View;
            }

            return result;
        }

        private static bool TrySetOptionValue<T>(INodeOption option, T value)
        {
            return UINavigationNodeOptionWriter.TrySetValue(option, value);
        }

        private static XmlDocument LoadUxml(string path)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(path);
            return document;
        }

        private static IEnumerable<XmlElement> EnumerateElements(XmlDocument document)
        {
            if (document?.DocumentElement == null)
                yield break;

            var stack = new Stack<XmlNode>();
            stack.Push(document.DocumentElement);
            while (stack.Count > 0)
            {
                XmlNode node = stack.Pop();
                if (node is XmlElement element)
                    yield return element;

                for (int index = node.ChildNodes.Count - 1; index >= 0; index--)
                    stack.Push(node.ChildNodes[index]);
            }
        }

        private static bool IsUxmlPath(string path)
        {
            return path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsGraphPath(string path)
        {
            return path.EndsWith(
                "." + UINavigationAuthoringGraph.Extension,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
