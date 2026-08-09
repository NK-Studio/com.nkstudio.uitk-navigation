using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Editor.Catalog;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    internal static class UINavigationGraphCompiler
    {
        internal static UINavigationAsset Compile(
            UINavigationAuthoringGraph graph,
            ICollection<string> errors = null)
        {
            UINavigationAsset asset = ScriptableObject.CreateInstance<UINavigationAsset>();
            if (graph == null)
            {
                errors?.Add("Navigation Graph를 불러오지 못했습니다.");
                asset.SetContents(null, Array.Empty<UINavigationNode>());
                return asset;
            }

            List<UINavigationUINode> screens = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationUINode)
                .Select(node => (UINavigationUINode)node)
                .ToList();
            List<UINavigationSendSignalNode> destinationNodes = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationSendSignalNode)
                .Select(node => (UINavigationSendSignalNode)node)
                .ToList();
            List<UINavigationRandomNode> randomNodes = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationRandomNode)
                .Select(node => (UINavigationRandomNode)node)
                .ToList();
            List<UINavigationStartNode> startNodes = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationStartNode)
                .Select(node => (UINavigationStartNode)node)
                .ToList();
            List<UINavigationActionNodeBase> actionNodes = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationActionNodeBase)
                .Select(node => (UINavigationActionNodeBase)node)
                .ToList();
            List<UINavigationPortalNode> portalNodes = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationPortalNode)
                .Select(node => (UINavigationPortalNode)node)
                .ToList();
            CollectImportErrors(
                screens,
                startNodes,
                actionNodes,
                portalNodes,
                destinationNodes,
                errors);

            var runtimeByScreen = new Dictionary<UINavigationUINodeBase, UINavigationNode>();
            foreach (UINavigationUINode screen in screens)
            {
                string id = screen.GetNodeId();
                string displayName = screen.GetOptionValue(
                    UINavigationUINodeBase.DisplayNameOption,
                    id);
                bool clearHistory = screen.GetOptionValue(
                    UINavigationUINodeBase.ClearHistoryOption,
                    false);
                bool useBack = screen.GetOptionValue(
                    UINavigationUINodeBase.UseBackOption,
                    false);
                string description = screen.GetOptionValue(
                    UINavigationUINodeBase.DescriptionOption,
                    string.Empty);

                var runtimeNode = new UINavigationNode(
                    id,
                    displayName,
                    clearHistory,
                    useBack,
                    description);
                runtimeByScreen.Add(screen, runtimeNode);
            }

            foreach (UINavigationSendSignalNode destination in destinationNodes)
            {
                string id = destination.GetNodeId();
                string displayName = destination.GetOptionValue(
                    UINavigationUINodeBase.DisplayNameOption,
                    id);
                bool clearHistory = destination.GetOptionValue(
                    UINavigationUINodeBase.ClearHistoryOption,
                    false);
                string description = destination.GetOptionValue(
                    UINavigationUINodeBase.DescriptionOption,
                    string.Empty);
                var runtimeNode = new UINavigationNode(
                    id,
                    displayName,
                    clearHistory,
                    false,
                    description);
                UINavigationSignalAddress address = destination.GetAddress();
                runtimeNode.SetDestination(
                    address.Kind,
                    address.DatabaseSignal,
                    address.CustomSignal);
                runtimeByScreen.Add(destination, runtimeNode);
            }

            foreach (UINavigationRandomNode random in randomNodes)
            {
                string id = random.GetNodeId();
                var runtimeNode = new UINavigationNode(id, "Random", false, false, string.Empty);
                runtimeByScreen.Add(random, runtimeNode);
            }

            foreach (var kvp in runtimeByScreen)
            {
                UINavigationUINodeBase nodeBase = kvp.Key;
                UINavigationNode runtimeNode = kvp.Value;
                var transitions = new List<UINavigationTransition>();

                if (nodeBase is UINavigationUINode screen)
                {
                    CompileUIOutputs(screen, runtimeByScreen, transitions, errors);
                    UINavigationUIPhase enter = screen.GetOnEnterUI();
                    UINavigationUIPhase exit = screen.GetOnExitUI();
                    runtimeNode.SetContents(
                        ValidCommands(enter?.Show),
                        ValidCommands(enter?.Hide),
                        ValidCommands(exit?.Show),
                        ValidCommands(exit?.Hide),
                        transitions.ToArray());
                }
                else if (nodeBase is UINavigationRandomNode randomNode)
                {
                    CompileRandomOutputs(randomNode, runtimeByScreen, transitions, errors);
                    runtimeNode.SetContents(
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationViewCommand>(),
                        transitions.ToArray());
                }
                else if (nodeBase is UINavigationSendSignalNode)
                {
                    runtimeNode.SetContents(
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationViewCommand>(),
                        Array.Empty<UINavigationTransition>());
                }
            }

            var portalTransitions = new List<UINavigationTransition>();
            var portalKeys = new HashSet<(UINavigationTriggerKind, UIKey, bool)>();
            var customPortalKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationPortalNode portalNode in portalNodes)
            {
                UINavigationPortalCondition condition = portalNode.GetCondition();
                UIKey key = condition.Key;
                UINavigationTriggerKind trigger = condition.RuntimeTriggerKind;
                bool toggleValue = trigger == UINavigationTriggerKind.Toggle &&
                                   condition.ToggleValue;
                bool customSignal =
                    trigger == UINavigationTriggerKind.Signal &&
                    condition.SignalAddressKind == UINavigationSignalAddressKind.Custom;
                bool validAddress = customSignal
                    ? !string.IsNullOrEmpty(condition.CustomSignal) &&
                      customPortalKeys.Add(condition.CustomSignal)
                    : key.IsValid && portalKeys.Add((trigger, key, toggleValue));
                if (!validAddress ||
                    !TryCompileActionChain(
                        portalNode.GetOutputPortByName(UINavigationPortalNode.NextPort),
                        $"Portal '{key}'",
                        runtimeByScreen,
                        errors,
                        out string targetNodeId,
                        out UINavigationAction[] actions))
                    continue;

                portalTransitions.Add(new UINavigationTransition(
                    trigger,
                    key,
                    customSignal ? condition.CustomSignal : string.Empty,
                    0f,
                    toggleValue
                        ? UIToggleOutputCondition.On
                        : UIToggleOutputCondition.Off,
                    UIViewOutputCondition.Show,
                    100f,
                    targetNodeId,
                    portalNode.GetOptionValue(
                        UINavigationPortalNode.HistoryOption,
                        UINavigationTransitionKind.Push),
                    actions));
            }

            var runtimeNodes = new List<UINavigationNode>(
                screens.Count + randomNodes.Count + destinationNodes.Count);
            foreach (UINavigationUINode screen in screens)
                runtimeNodes.Add(runtimeByScreen[screen]);
            foreach (UINavigationRandomNode random in randomNodes)
                runtimeNodes.Add(runtimeByScreen[random]);
            foreach (UINavigationSendSignalNode destination in destinationNodes)
                runtimeNodes.Add(runtimeByScreen[destination]);

            UINavigationUINodeBase start = GetStartTarget(startNodes);
            asset.SetContents(
                start != null && runtimeByScreen.TryGetValue(start, out UINavigationNode startNode)
                    ? startNode.Id
                    : null,
                runtimeNodes.ToArray(),
                portalTransitions.ToArray());
            return asset;
        }

        internal static void Validate(UINavigationAuthoringGraph graph, GraphLogger logger)
        {
            if (graph == null || logger == null)
                return;

            List<UINavigationUINodeBase> screens = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationUINodeBase)
                .Select(node => (UINavigationUINodeBase)node)
                .ToList();
            List<UINavigationStartNode> starts = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationStartNode)
                .Select(node => (UINavigationStartNode)node)
                .ToList();
            List<UINavigationActionNodeBase> actions = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationActionNodeBase)
                .Select(node => (UINavigationActionNodeBase)node)
                .ToList();
            List<UINavigationPortalNode> portals = graph.GetNodes()
                .AsValueEnumerable()
                .Where(node => node is UINavigationPortalNode)
                .Select(node => (UINavigationPortalNode)node)
                .ToList();
            List<UINavigationSendSignalNode> destinations = screens
                .AsValueEnumerable()
                .Where(node => node is UINavigationSendSignalNode)
                .Select(node => (UINavigationSendSignalNode)node)
                .ToList();
            if (!screens.AsValueEnumerable().Any(node => node is not UINavigationSendSignalNode))
                logger.LogError("Screen 노드가 하나 이상 필요합니다.");

            if (starts.Count == 0)
                logger.LogError("Start 노드가 필요합니다.");
            else if (starts.Count > 1)
            {
                foreach (UINavigationStartNode start in starts)
                    logger.LogError("Start 노드는 하나만 사용할 수 있습니다.", start);
            }

            foreach (UINavigationStartNode start in starts)
                ValidateStart(start, logger);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationUINodeBase screen in screens)
            {
                string id = screen.GetNodeId();

                if (string.IsNullOrEmpty(id))
                    logger.LogError("Node ID가 비어 있습니다.", screen);
                else if (!ids.Add(id))
                    logger.LogError($"중복된 Node ID입니다: {id}", screen);

                ValidateScreenConnections(screen, logger);
            }

            foreach (UINavigationActionNodeBase action in actions)
                ValidateAction(action, logger);

            var portalKeys = new HashSet<(UINavigationTriggerKind, UIKey, bool)>();
            var customPortalKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationPortalNode portal in portals)
                ValidatePortal(portal, portalKeys, customPortalKeys, logger);

            foreach (UINavigationSendSignalNode destination in destinations)
                ValidateDestination(destination, portals, logger);

            foreach (UINavigationUINodeBase screen in screens)
            {
                if (screen is UINavigationUINode uiNode)
                    ValidateRegisteredViewKeys(uiNode, logger);
            }
        }

        private static void ValidateScreenConnections(
            UINavigationUINodeBase screen,
            GraphLogger logger)
        {
            foreach (IPort port in screen
                         .GetInputPortByName(UINavigationUINodeBase.EnterPort)
                         .GetConnections())
            {
                INode source = ResolveBackward(port);
                if (source is not UINavigationUINodeBase &&
                    source is not UINavigationPortalNode &&
                    source is not UINavigationStartNode &&
                    !IsContinuingActionNode(source))
                {
                    logger.LogError(
                        "UI의 Enter에는 Start, 다른 UI 출력 또는 계속 실행되는 Action만 연결할 수 있습니다.",
                        screen);
                }
            }

            if (screen is UINavigationUINode uiNode)
                ValidateUIOutputs(uiNode, logger);
            if (screen is UINavigationRandomNode randomNode)
                ValidateRandomOutputs(randomNode, logger);
        }

        private static void ValidateRandomOutputs(
            UINavigationRandomNode randomNode,
            GraphLogger logger)
        {
            UINavigationRandomOutputDefinition[] outputs = randomNode.GetOutputs();
            var portNames = new HashSet<string>(StringComparer.Ordinal);
            int connected = 0;
            float totalWeight = 0f;

            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationRandomOutputDefinition output = outputs[index];
                if (output == null)
                {
                    logger.LogWarning($"Random Output #{index + 1}이 비어 있습니다.", randomNode);
                    continue;
                }

                if (!portNames.Add(output.GetPortName()))
                {
                    logger.LogError(
                        $"Random Output #{index + 1}의 식별자가 다른 출력과 중복되어 포트가 만들어지지 않았습니다.",
                        randomNode);
                    continue;
                }

                if (output.Weight < 0f ||
                    float.IsNaN(output.Weight) ||
                    float.IsInfinity(output.Weight))
                {
                    logger.LogError(
                        $"Random Output #{index + 1}의 Weight는 0 이상의 유한한 값이어야 합니다.",
                        randomNode);
                    continue;
                }

                IPort port = randomNode.GetOutputPortByName(output.GetPortName());
                List<IPort> targets = port == null
                    ? new List<IPort>()
                    : port.GetConnections();
                if (targets.Count == 0)
                    continue;

                if (targets.Count != 1 ||
                    targets[0] == null ||
                    !IsActionChainTarget(ResolveForward(targets[0])))
                {
                    logger.LogError(
                        $"Random Output #{index + 1}은 하나의 UI 또는 Action에 연결해야 합니다.",
                        randomNode);
                    continue;
                }

                connected++;
                totalWeight += output.Weight;
            }

            if (connected == 0)
                logger.LogError("Random은 연결된 출력이 하나 이상 필요합니다.", randomNode);
            else if (totalWeight <= 0f)
                logger.LogError("Random 출력의 Weight 합이 0보다 커야 합니다.", randomNode);
        }

        private static void ValidateStart(
            UINavigationStartNode start,
            GraphLogger logger)
        {
            List<IPort> targets = start
                .GetOutputPortByName(UINavigationStartNode.StartPort)
                .GetConnections();
            INode target = targets.Count == 1 && targets[0] != null
                ? ResolveForward(targets[0])
                : null;
            if (target is not UINavigationUINodeBase || target is UINavigationSendSignalNode)
            {
                logger.LogError("Start는 정확히 하나의 Screen Enter에 연결해야 합니다.", start);
            }
        }

        private static void ValidateAction(
            UINavigationActionNodeBase action,
            GraphLogger logger)
        {
            List<IPort> sources = action
                .GetInputPortByName(UINavigationActionNodeBase.EnterPort)
                .GetConnections();
            INode source = sources.Count == 1 && sources[0] != null
                ? ResolveBackward(sources[0])
                : null;
            if (source is not UINavigationUINodeBase &&
                source is not UINavigationPortalNode &&
                !IsContinuingActionNode(source))
            {
                logger.LogError(
                    "Action의 Enter는 정확히 하나의 Transition 또는 계속 실행되는 Action에 연결해야 합니다.",
                    action);
            }

            if (action is UINavigationSetTimeScaleNode timeScaleNode)
            {
                float value = timeScaleNode.GetTimeScale();
                if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                    logger.LogError("Time Scale은 0 이상의 유한한 값이어야 합니다.", timeScaleNode);
            }

            if (action is UINavigationLoadSceneNode loadScene)
            {
                UINavigationLoadSceneSettings settings = loadScene.GetSettings();
                if (settings.ReferenceKind == UINavigationSceneReferenceKind.Name &&
                    string.IsNullOrEmpty(settings.SceneName))
                    logger.LogError("Scene Name이 비어 있습니다.", loadScene);
                if (settings.ReferenceKind == UINavigationSceneReferenceKind.BuildIndex &&
                    settings.BuildIndex < 0)
                    logger.LogError("Build Index는 0 이상이어야 합니다.", loadScene);
                if (settings.SceneActivationDelay < 0f ||
                    float.IsNaN(settings.SceneActivationDelay) ||
                    float.IsInfinity(settings.SceneActivationDelay))
                    logger.LogError("Scene Activation Delay가 올바르지 않습니다.", loadScene);
            }
            else if (action is UINavigationSceneActionNodeBase sceneNode &&
                string.IsNullOrEmpty(sceneNode.GetSceneName()))
            {
                logger.LogError("Scene Name이 비어 있습니다.", sceneNode);
            }

            if (!TryGetNextPort(action, out IPort output))
                return;

            List<IPort> targets = output.GetConnections();
            bool mayBeTerminal = action is UINavigationSceneActionNodeBase;
            bool invalidCount = mayBeTerminal ? targets.Count > 1 : targets.Count != 1;
            bool invalidTarget = targets.Count == 1 &&
                                 (targets[0] == null ||
                                  !IsActionChainTarget(ResolveForward(targets[0])));
            if (invalidCount || invalidTarget)
            {
                logger.LogError(
                    mayBeTerminal
                        ? "Scene Action의 Next는 비워 두거나 하나의 Screen/Action에 연결해야 합니다."
                        : "Action의 Next는 정확히 하나의 Screen 또는 Action에 연결해야 합니다.",
                    action);
            }
        }

        private static void ValidateDestination(
            UINavigationSendSignalNode destination,
            IReadOnlyCollection<UINavigationPortalNode> portals,
            GraphLogger logger)
        {
            UINavigationSignalAddress address = destination.GetAddress();
            bool valid = address.Kind == UINavigationSignalAddressKind.Custom
                ? !string.IsNullOrEmpty(address.CustomSignal)
                : address.DatabaseSignal.IsValid;
            if (!valid)
                logger.LogError("Destination의 이동 Signal 주소가 비어 있습니다.", destination);
            else
            {
                bool hasPortal = portals.AsValueEnumerable().Any(portal =>
                {
                    UINavigationPortalCondition condition = portal.GetCondition();
                    if (condition.RuntimeTriggerKind != UINavigationTriggerKind.Signal ||
                        condition.SignalAddressKind != address.Kind)
                    {
                        return false;
                    }

                    return address.Kind == UINavigationSignalAddressKind.Custom
                        ? string.Equals(
                            condition.CustomSignal,
                            address.CustomSignal,
                            StringComparison.Ordinal)
                        : condition.Key == address.DatabaseSignal;
                });
                if (!hasPortal)
                    logger.LogError("Destination과 같은 Signal 주소를 가진 Portal이 없습니다.", destination);
            }

            List<IPort> sources = destination
                .GetInputPortByName(UINavigationUINodeBase.EnterPort)
                .GetConnections();
            if (sources.Count == 0)
                logger.LogError("Destination은 하나 이상의 전이 출력과 연결해야 합니다.", destination);
        }

        private static void ValidatePortal(
            UINavigationPortalNode portal,
            ISet<(UINavigationTriggerKind, UIKey, bool)> portalKeys,
            ISet<string> customPortalKeys,
            GraphLogger logger)
        {
            UINavigationPortalCondition condition = portal.GetCondition();
            UIKey key = condition.Key;
            UINavigationTriggerKind trigger = condition.RuntimeTriggerKind;
            bool toggleValue = trigger == UINavigationTriggerKind.Toggle &&
                               condition.ToggleValue;

            bool customSignal =
                trigger == UINavigationTriggerKind.Signal &&
                condition.SignalAddressKind == UINavigationSignalAddressKind.Custom;
            if (customSignal && string.IsNullOrEmpty(condition.CustomSignal))
                logger.LogError("Portal의 Custom Signal이 비어 있습니다.", portal);
            else if (customSignal && !customPortalKeys.Add(condition.CustomSignal))
                logger.LogError("중복된 Portal Custom Signal입니다: " + condition.CustomSignal, portal);
            else if (!customSignal && !key.IsValid)
                logger.LogError("Portal의 Category/Key가 비어 있습니다.", portal);
            else if (!customSignal)
            {
                if (!portalKeys.Add((trigger, key, toggleValue)))
                    logger.LogError($"중복된 Portal 조건입니다: {trigger} {key}", portal);
                if (!UIKeyCatalog.instance.Contains(key, GetCatalogKind(trigger)))
                    logger.LogWarning($"Key Catalog에 등록되지 않은 Portal 조건입니다: {key}", portal);
            }

            List<IPort> targets = portal
                .GetOutputPortByName(UINavigationPortalNode.NextPort)
                .GetConnections();
            if (targets.Count != 1 ||
                targets[0] == null ||
                !IsActionChainTarget(ResolveForward(targets[0])))
            {
                logger.LogError(
                    "Portal은 정확히 하나의 UI 또는 Action에 연결해야 합니다.",
                    portal);
            }
        }

        private static void ValidateRegisteredViewKeys(
            UINavigationUINode screen,
            GraphLogger logger)
        {
            UINavigationUIPhase enter = screen.GetOnEnterUI();
            UINavigationUIPhase exit = screen.GetOnExitUI();
            UINavigationViewCommand[][] groups =
            {
                enter?.Show,
                enter?.Hide,
                exit?.Show,
                exit?.Hide
            };

            foreach (UINavigationViewCommand[] values in groups)
            {
                if (values == null)
                    continue;

                foreach (UIKey value in values.AsValueEnumerable().Where(item => item.IsValid).Select(item => item.View))
                {
                    if (!UIKeyCatalog.instance.Contains(value, UIKeyCatalogKind.View))
                        logger.LogWarning(
                            $"Key Catalog에 등록되지 않은 View 주소입니다: {value}",
                            screen);
                }
            }
        }

        private static void CollectImportErrors(
            IReadOnlyCollection<UINavigationUINode> screens,
            IReadOnlyCollection<UINavigationStartNode> starts,
            IReadOnlyCollection<UINavigationActionNodeBase> actions,
            IReadOnlyCollection<UINavigationPortalNode> portals,
            IReadOnlyCollection<UINavigationSendSignalNode> destinations,
            ICollection<string> errors)
        {
            if (errors == null)
                return;

            if (screens.Count == 0)
                AddError(errors, "Screen 노드가 하나 이상 필요합니다.");

            if (starts.Count != 1)
                AddError(errors, $"Start 노드는 정확히 하나여야 합니다. 현재: {starts.Count}");

            foreach (UINavigationStartNode start in starts)
            {
                List<IPort> targets = start
                    .GetOutputPortByName(UINavigationStartNode.StartPort)
                    .GetConnections();
                INode target = targets.Count == 1 && targets[0] != null
                    ? ResolveForward(targets[0])
                    : null;
                if (target is not UINavigationUINodeBase || target is UINavigationSendSignalNode)
                {
                    AddError(errors, "Start는 정확히 하나의 Screen Enter에 연결해야 합니다.");
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationUINode screen in screens)
            {
                string id = screen.GetNodeId();
                if (string.IsNullOrEmpty(id))
                    AddError(errors, "Node ID가 비어 있는 Screen이 있습니다.");
                else if (!ids.Add(id))
                    AddError(errors, $"중복된 Node ID입니다: {id}");

            }

            foreach (UINavigationSendSignalNode destination in destinations)
            {
                string id = destination.GetNodeId();
                if (string.IsNullOrEmpty(id))
                    AddError(errors, "Node ID가 비어 있는 Destination이 있습니다.");
                else if (!ids.Add(id))
                    AddError(errors, "중복된 Node ID입니다: " + id);
            }

            foreach (UINavigationActionNodeBase action in actions)
            {
                List<IPort> sources = action
                    .GetInputPortByName(UINavigationActionNodeBase.EnterPort)
                    .GetConnections();
                INode source = sources.Count == 1 && sources[0] != null
                    ? ResolveBackward(sources[0])
                    : null;
                if (source is not UINavigationUINodeBase &&
                    source is not UINavigationPortalNode &&
                    !IsContinuingActionNode(source))
                {
                    AddError(errors, "Action의 Enter 연결이 올바르지 않습니다.");
                }

                if (action is UINavigationSetTimeScaleNode timeScaleNode)
                {
                    float value = timeScaleNode.GetTimeScale();
                    if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                        AddError(errors, "Time Scale은 0 이상의 유한한 값이어야 합니다.");
                }

                if (action is UINavigationLoadSceneNode loadScene)
                {
                    UINavigationLoadSceneSettings settings = loadScene.GetSettings();
                    if (settings.ReferenceKind == UINavigationSceneReferenceKind.Name &&
                        string.IsNullOrEmpty(settings.SceneName))
                        AddError(errors, "Load Scene의 Scene Name이 비어 있습니다.");
                    if (settings.ReferenceKind == UINavigationSceneReferenceKind.BuildIndex &&
                        settings.BuildIndex < 0)
                        AddError(errors, "Load Scene의 Build Index는 0 이상이어야 합니다.");
                    if (settings.SceneActivationDelay < 0f ||
                        float.IsNaN(settings.SceneActivationDelay) ||
                        float.IsInfinity(settings.SceneActivationDelay))
                        AddError(errors, "Scene Activation Delay가 올바르지 않습니다.");
                }
                else if (action is UINavigationSceneActionNodeBase sceneNode &&
                    string.IsNullOrEmpty(sceneNode.GetSceneName()))
                {
                    AddError(errors, "Scene Name이 비어 있는 Scene Action이 있습니다.");
                }

                if (!TryGetNextPort(action, out IPort output))
                    continue;

                List<IPort> targets = output.GetConnections();
                bool mayBeTerminal = action is UINavigationSceneActionNodeBase;
                bool invalidCount = mayBeTerminal ? targets.Count > 1 : targets.Count != 1;
                bool invalidTarget = targets.Count == 1 &&
                                     (targets[0] == null ||
                                      !IsActionChainTarget(ResolveForward(targets[0])));
                if (invalidCount || invalidTarget)
                {
                    AddError(errors, "Action의 Next 연결이 올바르지 않습니다.");
                }
            }

            var portalKeys = new HashSet<(UINavigationTriggerKind, UIKey, bool)>();
            var customPortalKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (UINavigationPortalNode portal in portals)
            {
                UINavigationPortalCondition condition = portal.GetCondition();
                UIKey key = condition.Key;
                UINavigationTriggerKind trigger = condition.RuntimeTriggerKind;
                bool toggleValue = trigger == UINavigationTriggerKind.Toggle &&
                                   condition.ToggleValue;
                bool customSignal =
                    trigger == UINavigationTriggerKind.Signal &&
                    condition.SignalAddressKind == UINavigationSignalAddressKind.Custom;
                if (customSignal && string.IsNullOrEmpty(condition.CustomSignal))
                    AddError(errors, "Portal의 Custom Signal이 비어 있습니다.");
                else if (customSignal && !customPortalKeys.Add(condition.CustomSignal))
                    AddError(errors, "중복된 Portal Custom Signal입니다: " + condition.CustomSignal);
                else if (!customSignal && !key.IsValid)
                    AddError(errors, "Portal의 Category/Key가 비어 있습니다.");
                else if (!customSignal && !portalKeys.Add((trigger, key, toggleValue)))
                    AddError(errors, $"중복된 Portal 조건입니다: {trigger} {key}");

                List<IPort> targets = portal
                    .GetOutputPortByName(UINavigationPortalNode.NextPort)
                    .GetConnections();
                if (targets.Count != 1 ||
                    targets[0] == null ||
                    !IsActionChainTarget(ResolveForward(targets[0])))
                    AddError(errors, $"Portal '{key}'의 대상 연결이 올바르지 않습니다.");
            }

            foreach (UINavigationSendSignalNode destination in destinations)
            {
                UINavigationSignalAddress address = destination.GetAddress();
                if (address.Kind == UINavigationSignalAddressKind.Custom)
                {
                    if (string.IsNullOrEmpty(address.CustomSignal))
                        AddError(errors, "Destination의 Custom Signal이 비어 있습니다.");
                }
                else if (!address.DatabaseSignal.IsValid)
                {
                    AddError(errors, "Destination의 Database Signal이 비어 있습니다.");
                }

                List<IPort> sources = destination
                    .GetInputPortByName(UINavigationUINodeBase.EnterPort)
                    .GetConnections();
                if (sources.Count == 0)
                    AddError(errors, "Destination의 Enter 연결이 올바르지 않습니다.");
            }
        }

        private static bool TryCompileActionChain(
            IPort startPort,
            string label,
            IReadOnlyDictionary<UINavigationUINodeBase, UINavigationNode> runtimeByScreen,
            ICollection<string> errors,
            out string targetNodeId,
            out UINavigationAction[] actions)
        {
            targetNodeId = null;
            var compiledActions = new List<UINavigationAction>();
            var visited = new HashSet<INode>();
            IPort output = startPort;

            while (true)
            {
                List<IPort> connections = output.GetConnections();
                if (connections.Count != 1 || connections[0] == null)
                {
                    AddError(
                        errors,
                        $"{label}의 실행 체인이 끊어져 있습니다.");
                    actions = Array.Empty<UINavigationAction>();
                    return false;
                }

                INode next = INodeExtensions.GetNode(connections[0]);
                if (next == null || !visited.Add(next))
                {
                    AddError(
                        errors,
                        $"{label}의 실행 체인에 순환 연결이 있습니다.");
                    actions = Array.Empty<UINavigationAction>();
                    return false;
                }

                if (next is UINavigationUINodeBase screen)
                {
                    if (!runtimeByScreen.TryGetValue(screen, out UINavigationNode runtimeNode))
                    {
                        AddError(errors, "Transition 대상 Screen을 런타임 노드로 변환하지 못했습니다.");
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    targetNodeId = runtimeNode.Id;
                    actions = compiledActions.ToArray();
                    return true;
                }

                if (next is UINavigationApplicationQuitNode)
                {
                    compiledActions.Add(UINavigationAction.ApplicationQuit());
                    actions = compiledActions.ToArray();
                    return true;
                }

                if (next is UINavigationSetTimeScaleNode timeScaleNode)
                {
                    float value = timeScaleNode.GetTimeScale();
                    if (value < 0f || float.IsNaN(value) || float.IsInfinity(value))
                    {
                        AddError(errors, "Time Scale은 0 이상의 유한한 값이어야 합니다.");
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    compiledActions.Add(UINavigationAction.SetTimeScale(value));
                    output = timeScaleNode.GetOutputPortByName(UINavigationSetTimeScaleNode.NextPort);
                    continue;
                }

                if (next is UINavigationDebugLogNode debugLogNode)
                {
                    compiledActions.Add(UINavigationAction.DebugLog(
                        debugLogNode.GetLogType(),
                        debugLogNode.GetMessage()));
                    output = debugLogNode.GetOutputPortByName(
                        UINavigationDebugLogNode.NextPort);
                    continue;
                }

                if (next is UINavigationLoadSceneNode loadSceneNode)
                {
                    UINavigationLoadSceneSettings settings = loadSceneNode.GetSettings();
                    if (settings.ReferenceKind == UINavigationSceneReferenceKind.Name &&
                        !ValidateSceneName(settings.SceneName, errors))
                    {
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    if (settings.ReferenceKind == UINavigationSceneReferenceKind.BuildIndex &&
                        settings.BuildIndex < 0)
                    {
                        AddError(errors, "Load Scene의 Build Index는 0 이상이어야 합니다.");
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    if (settings.SceneActivationDelay < 0f ||
                        float.IsNaN(settings.SceneActivationDelay) ||
                        float.IsInfinity(settings.SceneActivationDelay))
                    {
                        AddError(errors, "Scene Activation Delay는 0 이상의 유한한 값이어야 합니다.");
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    compiledActions.Add(UINavigationAction.LoadScene(
                        settings.ReferenceKind,
                        settings.SceneName,
                        settings.BuildIndex,
                        settings.LoadMode,
                        settings.AllowSceneActivation,
                        settings.SceneActivationDelay));
                    output = loadSceneNode.GetOutputPortByName(
                        UINavigationSceneActionNodeBase.NextPort);
                    if (TryFinishTerminalSceneAction(output, compiledActions, out actions))
                        return true;
                    continue;
                }

                if (next is UINavigationUnloadSceneNode unloadSceneNode)
                {
                    string sceneName = unloadSceneNode.GetSceneName();
                    if (!ValidateSceneName(sceneName, errors))
                    {
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    compiledActions.Add(UINavigationAction.UnloadScene(sceneName));
                    output = unloadSceneNode.GetOutputPortByName(
                        UINavigationSceneActionNodeBase.NextPort);
                    if (TryFinishTerminalSceneAction(output, compiledActions, out actions))
                        return true;
                    continue;
                }

                if (next is UINavigationSetActiveSceneNode activeSceneNode)
                {
                    string sceneName = activeSceneNode.GetSceneName();
                    if (!ValidateSceneName(sceneName, errors))
                    {
                        actions = Array.Empty<UINavigationAction>();
                        return false;
                    }

                    compiledActions.Add(UINavigationAction.SetActiveScene(sceneName));
                    output = activeSceneNode.GetOutputPortByName(
                        UINavigationSceneActionNodeBase.NextPort);
                    if (TryFinishTerminalSceneAction(output, compiledActions, out actions))
                        return true;
                    continue;
                }

                AddError(
                    errors,
                    $"{label}의 실행 체인에 지원하지 않는 노드가 연결되어 있습니다.");
                actions = Array.Empty<UINavigationAction>();
                return false;
            }
        }

        private static bool IsActionChainTarget(INode node)
        {
            return node is UINavigationUINodeBase ||
                   node is UINavigationActionNodeBase;
        }

        private static INode ResolveForward(IPort targetPort)
        {
            return INodeExtensions.GetNode(targetPort);
        }

        private static INode ResolveBackward(IPort sourcePort)
        {
            return INodeExtensions.GetNode(sourcePort);
        }

        private static UIKeyCatalogKind GetCatalogKind(UINavigationTriggerKind trigger)
        {
            return trigger switch
            {
                UINavigationTriggerKind.Toggle => UIKeyCatalogKind.Toggle,
                UINavigationTriggerKind.UIView => UIKeyCatalogKind.View,
                _ => UIKeyCatalogKind.Signal
            };
        }

        private static void CompileUIOutputs(
            UINavigationUINode uiNode,
            IReadOnlyDictionary<UINavigationUINodeBase, UINavigationNode> runtimeByScreen,
            ICollection<UINavigationTransition> transitions,
            ICollection<string> errors)
        {
            UINavigationOutputDefinition[] outputs = uiNode.GetOutputs();
            var keyed = new HashSet<(UINavigationTriggerKind, UIKey)>();
            var customSignals = new HashSet<string>(StringComparer.Ordinal);
            var toggles = new Dictionary<UIKey, HashSet<UIToggleOutputCondition>>();
            var views = new HashSet<(UIKey, UIViewOutputCondition)>();

            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationOutputDefinition output = outputs[index];
                if (output == null)
                    continue;

                bool customSignal =
                    output.Trigger == UINavigationTriggerKind.Signal &&
                    output.SignalAddressKind == UINavigationSignalAddressKind.Custom;
                if (customSignal && string.IsNullOrEmpty(output.CustomSignal))
                {
                    AddError(errors, $"UI Output #{index + 1} has an empty Custom Signal.");
                    continue;
                }

                if (output.Trigger != UINavigationTriggerKind.TimeDelay &&
                    !customSignal &&
                    !output.Key.IsValid)
                {
                    AddError(errors, $"UI Output #{index + 1} has an empty Category/Key.");
                    continue;
                }

                if (!TryRegisterOutput(
                        output,
                        keyed,
                        customSignals,
                        toggles,
                        views,
                        out string conflict))
                {
                    AddError(errors, conflict);
                    continue;
                }

                if (output.Trigger == UINavigationTriggerKind.TimeDelay &&
                    (output.DelaySeconds < 0f ||
                     float.IsNaN(output.DelaySeconds) ||
                     float.IsInfinity(output.DelaySeconds)))
                {
                    AddError(errors, $"UI Delay #{index + 1} must be a finite value greater than or equal to zero.");
                    continue;
                }

                CompileUIOutputBranch(uiNode, output, runtimeByScreen, transitions, errors);
            }
        }

        private static void CompileUIOutputBranch(
            UINavigationUINode uiNode,
            UINavigationOutputDefinition output,
            IReadOnlyDictionary<UINavigationUINodeBase, UINavigationNode> runtimeByScreen,
            ICollection<UINavigationTransition> transitions,
            ICollection<string> errors)
        {
            IPort port = uiNode.GetOutputPortByName(output.GetPortName());
            if (port == null || port.GetConnections().Count == 0)
                return;

            string label = output.Trigger switch
            {
                UINavigationTriggerKind.Toggle =>
                    $"UI Toggle '{output.Key}' ({output.ToggleCondition})",
                UINavigationTriggerKind.UIView =>
                    $"UI View '{output.Key}' ({output.ViewCondition})",
                _ => $"UI {output.Trigger} Output"
            };

            if (!TryCompileActionChain(
                    port,
                    label,
                    runtimeByScreen,
                    errors,
                    out string targetNodeId,
                    out UINavigationAction[] actions))
            {
                return;
            }

            transitions.Add(new UINavigationTransition(
                output.Trigger,
                output.Key,
                output.SignalAddressKind == UINavigationSignalAddressKind.Custom
                    ? output.CustomSignal
                    : string.Empty,
                output.DelaySeconds,
                output.ToggleCondition,
                output.ViewCondition,
                100f,
                targetNodeId,
                UINavigationTransitionKind.Push,
                actions,
                output.GetPortName()));
        }

        private static void ValidateUIOutputs(
            UINavigationUINode uiNode,
            GraphLogger logger)
        {
            UINavigationOutputDefinition[] outputs = uiNode.GetOutputs();
            var keyed = new HashSet<(UINavigationTriggerKind, UIKey)>();
            var customSignals = new HashSet<string>(StringComparer.Ordinal);
            var toggles = new Dictionary<UIKey, HashSet<UIToggleOutputCondition>>();
            var views = new HashSet<(UIKey, UIViewOutputCondition)>();

            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationOutputDefinition output = outputs[index];
                if (output == null)
                {
                    logger.LogWarning($"Output #{index + 1} is empty.", uiNode);
                    continue;
                }

                bool customSignal =
                    output.Trigger == UINavigationTriggerKind.Signal &&
                    output.SignalAddressKind == UINavigationSignalAddressKind.Custom;
                if (customSignal)
                {
                    if (string.IsNullOrEmpty(output.CustomSignal))
                        logger.LogError($"Output #{index + 1} has an empty Custom Signal.", uiNode);
                }
                else if (output.Trigger != UINavigationTriggerKind.TimeDelay)
                {
                    if (!output.Key.IsValid)
                    {
                        logger.LogError($"Output #{index + 1} has an empty Category/Key.", uiNode);
                    }
                    else if (!UIKeyCatalog.instance.Contains(
                                 output.Key,
                                 GetCatalogKind(output.Trigger)))
                    {
                        logger.LogWarning($"Address is not registered in Key Catalog: {output.Key}", uiNode);
                    }
                }

                if (!TryRegisterOutput(
                        output,
                        keyed,
                        customSignals,
                        toggles,
                        views,
                        out string conflict))
                    logger.LogError(conflict, uiNode);

                if (output.Trigger == UINavigationTriggerKind.TimeDelay &&
                    (output.DelaySeconds < 0f ||
                     float.IsNaN(output.DelaySeconds) ||
                     float.IsInfinity(output.DelaySeconds)))
                {
                    logger.LogError("Delay must be a finite value greater than or equal to zero.", uiNode);
                }

                ValidateUIBranch(uiNode, output, logger);
            }
        }

        private static void CompileRandomOutputs(
            UINavigationRandomNode randomNode,
            IReadOnlyDictionary<UINavigationUINodeBase, UINavigationNode> runtimeByScreen,
            ICollection<UINavigationTransition> transitions,
            ICollection<string> errors)
        {
            UINavigationRandomOutputDefinition[] outputs = randomNode.GetOutputs();
            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationRandomOutputDefinition output = outputs[index];
                if (output == null)
                    continue;

                IPort port = randomNode.GetOutputPortByName(output.GetPortName());
                if (port == null || port.GetConnections().Count == 0)
                    continue;

                if (!TryCompileActionChain(
                        port,
                        $"Random Output #{index + 1}",
                        runtimeByScreen,
                        errors,
                        out string targetNodeId,
                        out UINavigationAction[] actions))
                {
                    continue;
                }

                transitions.Add(new UINavigationTransition(
                    UINavigationTriggerKind.Random,
                    new UIKey(string.Empty, string.Empty),
                    0f,
                    UIToggleOutputCondition.Any,
                    UIViewOutputCondition.Show,
                    output.Weight,
                    targetNodeId,
                    UINavigationTransitionKind.Push,
                    actions,
                    output.GetPortName()));
            }
        }

        private static bool TryRegisterOutput(
            UINavigationOutputDefinition output,
            ISet<(UINavigationTriggerKind, UIKey)> keyed,
            ISet<string> customSignals,
            IDictionary<UIKey, HashSet<UIToggleOutputCondition>> toggles,
            ISet<(UIKey, UIViewOutputCondition)> views,
            out string error)
        {
            error = null;
            if (output.Trigger == UINavigationTriggerKind.TimeDelay)
                return true;

            if (output.Trigger == UINavigationTriggerKind.Signal &&
                output.SignalAddressKind == UINavigationSignalAddressKind.Custom)
            {
                if (string.IsNullOrEmpty(output.CustomSignal))
                    return true;

                if (!customSignals.Add(output.CustomSignal))
                {
                    error = "Duplicate Custom Signal: " + output.CustomSignal;
                    return false;
                }

                return true;
            }

            if (!output.Key.IsValid)
                return true;

            if (output.Trigger == UINavigationTriggerKind.Toggle)
            {
                if (!toggles.TryGetValue(output.Key, out HashSet<UIToggleOutputCondition> conditions))
                {
                    conditions = new HashSet<UIToggleOutputCondition>();
                    toggles.Add(output.Key, conditions);
                }

                bool overlaps = conditions.Contains(output.ToggleCondition) ||
                                (output.ToggleCondition == UIToggleOutputCondition.Any && conditions.Count > 0) ||
                                (output.ToggleCondition != UIToggleOutputCondition.Any &&
                                 conditions.Contains(UIToggleOutputCondition.Any));
                if (overlaps)
                {
                    error = $"Toggle '{output.Key}' cannot mix Any with On/Off or repeat the same condition.";
                    return false;
                }

                conditions.Add(output.ToggleCondition);
                return true;
            }

            if (output.Trigger == UINavigationTriggerKind.UIView)
            {
                if (!views.Add((output.Key, output.ViewCondition)))
                {
                    error = $"UI View '{output.Key}' repeats the {output.ViewCondition} condition.";
                    return false;
                }

                return true;
            }

            if (!keyed.Add((output.Trigger, output.Key)))
            {
                error = $"Duplicate {output.Trigger} address: {output.Key}";
                return false;
            }

            return true;
        }

        private static void ValidateUIBranch(
            UINavigationUINode uiNode,
            UINavigationOutputDefinition output,
            GraphLogger logger)
        {
            IPort port = uiNode.GetOutputPortByName(output.GetPortName());
            List<IPort> targets = port?.GetConnections() ?? new List<IPort>();
            if (targets.Count == 0)
                return;

            if (targets.Count != 1 ||
                targets[0] == null ||
                !IsActionChainTarget(ResolveForward(targets[0])))
            {
                logger.LogError("A UI output can connect to exactly one UI or Action node.", uiNode);
            }
        }
        private static bool IsContinuingActionNode(INode node)
        {
            return node is UINavigationSetTimeScaleNode ||
                   node is UINavigationDebugLogNode ||
                   node is UINavigationSceneActionNodeBase;
        }

        private static bool TryGetNextPort(
            UINavigationActionNodeBase action,
            out IPort output)
        {
            switch (action)
            {
                case UINavigationSetTimeScaleNode timeScaleNode:
                    output = timeScaleNode.GetOutputPortByName(
                        UINavigationSetTimeScaleNode.NextPort);
                    return true;

                case UINavigationDebugLogNode debugLogNode:
                    output = debugLogNode.GetOutputPortByName(
                        UINavigationDebugLogNode.NextPort);
                    return true;

                case UINavigationSceneActionNodeBase sceneNode:
                    output = sceneNode.GetOutputPortByName(
                        UINavigationSceneActionNodeBase.NextPort);
                    return true;

                default:
                    output = null;
                    return false;
            }
        }

        private static bool ValidateSceneName(
            string sceneName,
            ICollection<string> errors)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
                return true;

            AddError(errors, "Scene Name이 비어 있는 Scene Action이 있습니다.");
            return false;
        }

        private static bool TryFinishTerminalSceneAction(
            IPort output,
            List<UINavigationAction> compiledActions,
            out UINavigationAction[] actions)
        {
            if (output.GetConnections().Count > 0)
            {
                actions = null;
                return false;
            }

            actions = compiledActions.ToArray();
            return true;
        }

        private static void AddError(ICollection<string> errors, string error)
        {
            if (errors != null && !errors.Contains(error))
                errors.Add(error);
        }

        private static UINavigationUINodeBase GetStartTarget(
            IReadOnlyCollection<UINavigationStartNode> starts)
        {
            if (starts.Count != 1)
                return null;

            List<IPort> targets = starts.AsValueEnumerable().First()
                .GetOutputPortByName(UINavigationStartNode.StartPort)
                .GetConnections();
            if (targets.Count != 1 || targets[0] == null)
                return null;

            UINavigationUINodeBase target = ResolveForward(targets[0]) as UINavigationUINodeBase;
            return target is UINavigationSendSignalNode ? null : target;
        }

        private static UINavigationViewCommand[] ValidCommands(
            UINavigationViewCommand[] values)
        {
            if (values == null || values.Length == 0)
                return Array.Empty<UINavigationViewCommand>();

            return values.AsValueEnumerable().Where(value => value.IsValid).ToArray();
        }
    }
}
