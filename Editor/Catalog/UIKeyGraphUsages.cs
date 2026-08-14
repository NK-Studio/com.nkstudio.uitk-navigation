using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Editor.Navigation;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Catalog
{
    /// <summary>
    /// Reads and rewrites UIKey addresses stored in navigation authoring graphs.
    /// </summary>
    internal static class UIKeyGraphUsages
    {
        internal static void ScanGraphs(List<UIKeyUsage> usages, bool logFailures)
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

        internal static bool ReplaceGraph(
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

        internal static bool IsGraphPath(string path)
        {
            return path.EndsWith(
                "." + UINavigationAuthoringGraph.Extension,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
