using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    internal static class UINavigationGraphValidator
    {
        internal static List<string> Validate(UINavigationAsset asset)
        {
            var errors = new List<string>();

            if (asset == null)
            {
                errors.Add("Navigation Graph가 연결되지 않았습니다.");
                return errors;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < asset.Nodes.Count; i++)
            {
                UINavigationNode node = asset.Nodes[i];
                if (node == null)
                {
                    errors.Add($"Nodes[{i}]가 비어 있습니다.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(node.Id))
                    errors.Add($"'{node.DisplayName}' 노드의 ID가 비어 있습니다.");
                else if (!ids.Add(node.Id))
                    errors.Add($"중복된 노드 ID가 있습니다: {node.Id}");
            }

            if (string.IsNullOrEmpty(asset.StartNodeId) || !ids.Contains(asset.StartNodeId))
                errors.Add("유효한 시작 노드가 지정되지 않았습니다.");

            foreach (UINavigationNode node in asset.Nodes)
            {
                if (node == null)
                    continue;

                var signals = new HashSet<(UINavigationTriggerKind, UIKey)>();
                foreach (UINavigationTransition transition in node.Transitions)
                {
                    if (transition == null)
                    {
                        errors.Add($"'{node.DisplayName}' 노드에 비어 있는 전환이 있습니다.");
                        continue;
                    }

                    if (transition.TriggerKind != UINavigationTriggerKind.TimeDelay &&
                        transition.TriggerKind != UINavigationTriggerKind.Random)
                    {
                        if (!transition.Signal.IsValid)
                            errors.Add(
                                $"'{node.DisplayName}' 노드에 {transition.TriggerKind} Category/Key가 없는 전환이 있습니다.");
                        else if (!signals.Add((transition.TriggerKind, transition.Signal)))
                            errors.Add(
                                $"'{node.DisplayName}' 노드에 중복 {transition.TriggerKind}이 있습니다: {transition.Signal}");
                    }

                    if (!string.IsNullOrEmpty(transition.TargetNodeId) && !ids.Contains(transition.TargetNodeId))
                        errors.Add($"'{node.DisplayName}' → '{transition.TargetNodeId}' 대상 노드를 찾을 수 없습니다.");
                }
            }

            var portalKeys = new HashSet<(UINavigationTriggerKind, UIKey, bool)>();
            foreach (UINavigationTransition portal in asset.Portals)
            {
                if (portal == null)
                {
                    errors.Add("비어 있는 Portal 전환이 있습니다.");
                    continue;
                }

                bool toggleValue = portal.TriggerKind == UINavigationTriggerKind.Toggle &&
                                   portal.ToggleValue;
                if (!portal.Signal.IsValid)
                    errors.Add("Category/Key가 없는 Portal 전환이 있습니다.");
                else if (!portalKeys.Add((portal.TriggerKind, portal.Signal, toggleValue)))
                    errors.Add($"중복된 Portal 조건입니다: {portal.TriggerKind} {portal.Signal}");

                if (!string.IsNullOrEmpty(portal.TargetNodeId) &&
                    !ids.Contains(portal.TargetNodeId))
                    errors.Add($"Portal 대상 노드를 찾을 수 없습니다: '{portal.TargetNodeId}'");
            }

            return errors;
        }
    }
}
