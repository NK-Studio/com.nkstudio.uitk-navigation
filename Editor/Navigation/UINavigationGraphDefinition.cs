using System;
using System.Collections.Generic;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using Unity.GraphToolkit.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZLinq;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [Serializable]
    internal sealed class UINavigationNodeIdentity
    {
        [SerializeField, HideInInspector]
        private string value;

        internal string Value => value;

        internal UINavigationNodeIdentity()
            : this(null)
        {
        }

        internal UINavigationNodeIdentity(string value)
        {
            this.value = string.IsNullOrWhiteSpace(value)
                ? Guid.NewGuid().ToString("N")
                : value.Trim();
        }
    }

    /// <summary>
    /// Provides UI Navigation Node Colors functionality.
    /// </summary>
    internal static class UINavigationNodeColors
    {
        internal static readonly Color Start = new(0.24f, 0.59f, 0.26f);
        internal static readonly Color UI = new(0.21f, 0.40f, 0.59f);
        internal static readonly Color Portal = new(0.43f, 0.31f, 0.59f);
        internal static readonly Color Destination = new(0.12f, 0.52f, 0.50f);
        internal static readonly Color Action = new(0.59f, 0.38f, 0.17f);
    }

    [Graph(
        UINavigationAuthoringGraph.Extension,
        GraphOptions.DisableAutoInclusionOfNodesFromGraphAssembly)]
    [Serializable]
    internal sealed class UINavigationAuthoringGraph : Graph
    {
        internal const string Extension = "uinavgraph";

        public override void OnGraphChanged(GraphLogger graphLogger)
        {
            base.OnGraphChanged(graphLogger);
            UINavigationNodeIdRepair.ScheduleEnsureUniqueNodeIds(this);
            UINavigationRandomPortPreview.Register(this);
            UINavigationGraphCompiler.Validate(this, graphLogger);
        }
    }

    [Serializable]
    internal abstract class UINavigationUINodeBase : Node
    {
        internal const string NodeIdOption = "identity";
        internal const string DisplayNameOption = "displayName";
        internal const string DescriptionOption = "description";
        internal const string ClearHistoryOption = "clearHistory";
        internal const string UseBackOption = "useBack";
        internal const string EnterPort = "enter";

        internal string InitialNodeId { get; set; }
        internal string InitialDisplayName { get; set; }
        internal string InitialDescription { get; set; }
        internal bool InitialClearHistory { get; set; }
        internal bool InitialUseBack { get; set; }
        internal UIKey[] InitialShowOnEnter { get; set; } = Array.Empty<UIKey>();
        internal UIKey[] InitialHideOnEnter { get; set; } = Array.Empty<UIKey>();
        internal UIKey[] InitialShowOnExit { get; set; } = Array.Empty<UIKey>();
        internal UIKey[] InitialHideOnExit { get; set; } = Array.Empty<UIKey>();

        /// <summary>
        /// Gets the uses display name option.
        /// </summary>
        protected virtual bool UsesDisplayNameOption => true;

        protected virtual bool UsesUseBackOption => true;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<UINavigationNodeIdentity>(NodeIdOption)
                .WithDisplayName(string.Empty)
                .WithTooltip("자동 생성되는 내부 식별자입니다.")
                .WithDefaultValue(new UINavigationNodeIdentity(InitialNodeId))
                .ShowInInspectorOnly()
                .Build();

            if (UsesDisplayNameOption)
            {
                context.AddOption<string>(DisplayNameOption)
                    .WithDisplayName("Display Name")
                    .WithDefaultValue(string.IsNullOrEmpty(InitialDisplayName) ? "Screen" : InitialDisplayName)
                    .ShowInInspectorOnly()
                    .Delayed()
                    .Build();
            }

            context.AddOption<string>(DescriptionOption)
                .WithDisplayName("Node Description")
                .WithDefaultValue(InitialDescription ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();

            context.AddOption<bool>(ClearHistoryOption)
                .WithDisplayName("Clear History")
                .WithTooltip("이 화면에 진입할 때 Back/Forward 히스토리를 비웁니다.")
                .WithDefaultValue(InitialClearHistory)
                .ShowInInspectorOnly()
                .Build();

            if (UsesUseBackOption)
            {
                context.AddOption<bool>(UseBackOption)
                    .WithDisplayName("Use Back")
                    .WithTooltip("켜면 Esc 또는 마우스 뒤로 버튼으로 Navigation 히스토리의 이전 Screen으로 돌아갑니다.")
                    .WithDefaultValue(InitialUseBack)
                    .ShowInInspectorOnly()
                    .Build();
            }

            DefineViewOptions(context);
        }

        protected abstract void DefineViewOptions(IOptionDefinitionContext context);

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            Title = GetOptionValue(
                DisplayNameOption,
                string.IsNullOrEmpty(InitialDisplayName) ? "UI" : InitialDisplayName);
            DefaultColor = UINavigationNodeColors.UI;
            context.AddInputPort(EnterPort)
                .WithDisplayName("Enter")
                .WithTooltip("Start, UI 출력, Portal 또는 Action에서 진입하는 연결입니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            DefineOutputPorts(context);
        }

        protected virtual void DefineOutputPorts(IPortDefinitionContext context)
        {
        }

        internal T GetOptionValue<T>(string optionName, T fallback = default)
        {
            INodeOption option = GetNodeOptionByName(optionName);
            return option != null && option.TryGetValue(out T value) ? value : fallback;
        }

        internal string GetNodeId()
        {
            UINavigationNodeIdentity identity = GetOptionValue<UINavigationNodeIdentity>(
                NodeIdOption,
                null);
            if (!string.IsNullOrEmpty(identity?.Value))
                return identity.Value;

            return string.Empty;
        }

    }

    [Node("UI Navigation/Life Cycles", "", "Start")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationStartNode : Node
    {
        internal const string StartPort = "start";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            Title = "Start";
            DefaultColor = UINavigationNodeColors.Start;

            context.AddOutputPort(StartPort)
                .WithDisplayName("Start")
                .WithTooltip("앱 시작 시 처음 진입할 Screen의 Enter 포트에 연결합니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }

    [Serializable]
    internal sealed class UINavigationOutputDefinition : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private string outputId = Guid.NewGuid().ToString("N");

        [SerializeField]
        private UINavigationTriggerKind trigger = UINavigationTriggerKind.Signal;

        [SerializeField]
        private UIKey key = new("Default", "Next");

        [SerializeField]
        private UINavigationSignalAddressKind signalAddressKind =
            UINavigationSignalAddressKind.Database;

        [SerializeField]
        private string customSignal = "Home";

        [SerializeField, Min(0f)]
        private float delaySeconds = 1f;


        [SerializeField]
        private UIToggleOutputCondition toggleCondition = UIToggleOutputCondition.On;

        [SerializeField]
        private UIViewOutputCondition viewCondition = UIViewOutputCondition.Show;

        [SerializeField, HideInInspector]
        private bool upgraded;

        internal string OutputId => outputId;
        internal UINavigationTriggerKind Trigger =>
            UINavigationTriggerKindUtility.Normalize(trigger);
        internal UIKey Key => key;
        internal UINavigationSignalAddressKind SignalAddressKind => signalAddressKind;
        internal string CustomSignal => customSignal?.Trim() ?? string.Empty;
        internal float DelaySeconds => delaySeconds;
        internal UIToggleOutputCondition ToggleCondition => toggleCondition;
        internal UIViewOutputCondition ViewCondition => viewCondition;
        internal bool NeedsLegacyToggleSplit => !upgraded && trigger == UINavigationTriggerKind.Toggle;
        internal void SetKey(UIKey value) => key = value;

        internal static UINavigationOutputDefinition CreateCustomSignal(string signal)
        {
            return new UINavigationOutputDefinition(
                UINavigationTriggerKind.Signal,
                default,
                0f,
                UINavigationTransitionKind.Push)
            {
                signalAddressKind = UINavigationSignalAddressKind.Custom,
                customSignal = signal?.Trim() ?? string.Empty
            };
        }

        internal UINavigationOutputDefinition()
        {
        }

        internal UINavigationOutputDefinition(
            UINavigationTriggerKind trigger,
            UIKey key,
            float delaySeconds,
            UINavigationTransitionKind history)
            : this(
                trigger,
                key,
                delaySeconds,
                UIToggleOutputCondition.On,
                UIViewOutputCondition.Show)
        {
        }

        internal UINavigationOutputDefinition(
            UINavigationTriggerKind trigger,
            UIKey key,
            float delaySeconds,
            UIToggleOutputCondition toggleCondition,
            UIViewOutputCondition viewCondition)
        {
            outputId = Guid.NewGuid().ToString("N");
            this.trigger = UINavigationTriggerKindUtility.Normalize(trigger);
            this.key = key;
            this.delaySeconds = Mathf.Max(0f, delaySeconds);
            this.toggleCondition = toggleCondition;
            this.viewCondition = viewCondition;
            upgraded = true;
        }

        internal UINavigationOutputDefinition CreateMigratedToggle(
            UIToggleOutputCondition condition)
        {
            return new UINavigationOutputDefinition(
                trigger,
                key,
                delaySeconds,
                condition,
                viewCondition)
            {
                outputId = outputId,
                upgraded = true
            };
        }

        internal void MarkUpgraded()
        {
            upgraded = true;
        }

        internal string GetPortName()
        {
            EnsureId();
            if (trigger != UINavigationTriggerKind.Toggle)
                return outputId;

            string suffix = toggleCondition switch
            {
                UIToggleOutputCondition.On => "true",
                UIToggleOutputCondition.Off => "false",
                _ => "any"
            };
            return $"{outputId}:{suffix}";
        }

        internal string GetPortName(bool toggleValue)
        {
            EnsureId();
            return $"{outputId}:{(toggleValue ? "true" : "false")}";
        }

        public void OnBeforeSerialize()
        {
            EnsureId();
        }

        public void OnAfterDeserialize()
        {
            EnsureId();
            trigger = UINavigationTriggerKindUtility.Normalize(trigger);
        }

        private void EnsureId()
        {
            if (string.IsNullOrEmpty(outputId))
                outputId = Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    internal sealed class UINavigationUIPhase : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private bool isExit;

        [SerializeField, HideInInspector]
        private UIKey[] show = Array.Empty<UIKey>();

        [SerializeField, HideInInspector]
        private UIKey[] hide = Array.Empty<UIKey>();

        [SerializeField]
        private UINavigationViewCommand[] showCommands =
            Array.Empty<UINavigationViewCommand>();

        [SerializeField]
        private UINavigationViewCommand[] hideCommands =
            Array.Empty<UINavigationViewCommand>();

        [SerializeField, HideInInspector]
        private bool upgraded;

        internal UINavigationViewCommand[] Show
        {
            get
            {
                EnsureMigrated();
                return showCommands ?? Array.Empty<UINavigationViewCommand>();
            }
        }

        internal UINavigationViewCommand[] Hide
        {
            get
            {
                EnsureMigrated();
                return hideCommands ?? Array.Empty<UINavigationViewCommand>();
            }
        }

        internal bool IsExit => isExit;

        internal UINavigationUIPhase()
        {
        }

        internal UINavigationUIPhase(UIKey[] show, UIKey[] hide, bool isExit = false)
            : this(ToCommands(show), ToCommands(hide), isExit)
        {
        }

        internal UINavigationUIPhase(
            UINavigationViewCommand[] show,
            UINavigationViewCommand[] hide,
            bool isExit = false)
        {
            this.isExit = isExit;
            showCommands = show ?? Array.Empty<UINavigationViewCommand>();
            hideCommands = hide ?? Array.Empty<UINavigationViewCommand>();
            this.show = ToKeys(showCommands);
            this.hide = ToKeys(hideCommands);
            upgraded = true;
        }

        public void OnBeforeSerialize()
        {
            EnsureMigrated();
            show = ToKeys(showCommands);
            hide = ToKeys(hideCommands);
        }

        public void OnAfterDeserialize()
        {
            EnsureMigrated();
        }

        private void EnsureMigrated()
        {
            if (upgraded)
                return;

            showCommands = ToCommands(show);
            hideCommands = ToCommands(hide);
            upgraded = true;
        }

        private static UINavigationViewCommand[] ToCommands(UIKey[] keys)
        {
            if (keys == null || keys.Length == 0)
                return Array.Empty<UINavigationViewCommand>();

            var result = new UINavigationViewCommand[keys.Length];
            for (int i = 0; i < keys.Length; i++)
                result[i] = new UINavigationViewCommand(keys[i]);
            return result;
        }

        private static UIKey[] ToKeys(UINavigationViewCommand[] commands)
        {
            if (commands == null || commands.Length == 0)
                return Array.Empty<UIKey>();

            var result = new UIKey[commands.Length];
            for (int i = 0; i < commands.Length; i++)
                result[i] = commands[i].View;
            return result;
        }
    }

    [Serializable]
    internal sealed class UINavigationOutputCollection : ISerializationCallbackReceiver
    {
        [SerializeField]
        private UINavigationOutputDefinition[] items =
            Array.Empty<UINavigationOutputDefinition>();

        internal UINavigationOutputDefinition[] Items
        {
            get
            {
                UpgradeLegacyItems();
                return items ?? Array.Empty<UINavigationOutputDefinition>();
            }
        }

        internal UINavigationOutputCollection()
        {
        }

        internal UINavigationOutputCollection(UINavigationOutputDefinition[] items)
        {
            this.items = items ?? Array.Empty<UINavigationOutputDefinition>();
            UpgradeLegacyItems();
        }

        public void OnBeforeSerialize()
        {
            UpgradeLegacyItems();
        }

        public void OnAfterDeserialize()
        {
            UpgradeLegacyItems();
        }

        private void UpgradeLegacyItems()
        {
            if (items == null || items.Length == 0)
            {
                items = Array.Empty<UINavigationOutputDefinition>();
                return;
            }

            var upgradedItems = new List<UINavigationOutputDefinition>(items.Length + 1);
            for (int i = 0; i < items.Length; i++)
            {
                UINavigationOutputDefinition item = items[i];
                if (item == null)
                    continue;

                if (item.NeedsLegacyToggleSplit)
                {
                    upgradedItems.Add(item.CreateMigratedToggle(UIToggleOutputCondition.On));
                    upgradedItems.Add(item.CreateMigratedToggle(UIToggleOutputCondition.Off));
                }
                else
                {
                    item.MarkUpgraded();
                    upgradedItems.Add(item);
                }
            }

            items = upgradedItems.ToArray();
        }
    }

    [Node("UI Navigation/UI Manager", "", "UI")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationUINode : UINavigationUINodeBase
    {
        internal const string OnEnterUIOption = "onEnterUI";
        internal const string OnExitUIOption = "onExitUI";
        internal const string DynamicOutputsOption = "dynamicOutputs";
        internal UINavigationOutputDefinition[] InitialOutputs { get; set; } =
            Array.Empty<UINavigationOutputDefinition>();

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<UINavigationOutputCollection>(DynamicOutputsOption)
                .WithDisplayName("Outputs")
                .WithTooltip("UI Button, Signal, Time Delay 또는 Toggle 출력을 추가합니다.")
                .WithDefaultValue(new UINavigationOutputCollection(InitialOutputs))
                .ShowInInspectorOnly()
                .Build();
        }

        protected override void DefineViewOptions(IOptionDefinitionContext context)
        {
            context.AddOption<UINavigationUIPhase>(OnEnterUIOption)
                .WithDisplayName("On Enter UI")
                .WithTooltip("UI 진입 시 표시하거나 숨길 View 주소입니다.")
                .WithDefaultValue(new UINavigationUIPhase(
                    InitialShowOnEnter,
                    InitialHideOnEnter))
                .ShowInInspectorOnly()
                .Build();

            context.AddOption<UINavigationUIPhase>(OnExitUIOption)
                .WithDisplayName("On Exit UI")
                .WithTooltip("UI 이탈 시 표시하거나 숨길 View 주소입니다.")
                .WithDefaultValue(new UINavigationUIPhase(
                    InitialShowOnExit,
                    InitialHideOnExit,
                    true))
                .ShowInInspectorOnly()
                .Build();
        }

        protected override void DefineOutputPorts(IPortDefinitionContext context)
        {
            UINavigationOutputDefinition[] outputs = GetOutputs();

            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationOutputDefinition output = outputs[index];
                if (output == null)
                    continue;

                switch (output.Trigger)
                {
                    case UINavigationTriggerKind.Toggle:
                        AddOutputPort(
                            context,
                            output.GetPortName(),
                            $"Toggle · {FormatKey(output.Key, "Unassigned")} · {output.ToggleCondition}");
                        break;

                    case UINavigationTriggerKind.UIView:
                        AddOutputPort(
                            context,
                            output.GetPortName(),
                            $"UI View · {FormatKey(output.Key, "Unassigned")} · {output.ViewCondition}");
                        break;

                    case UINavigationTriggerKind.TimeDelay:
                        AddOutputPort(
                            context,
                            output.GetPortName(),
                            $"Delay · {output.DelaySeconds:0.###} s");
                        break;

                    case UINavigationTriggerKind.Signal:
                        AddOutputPort(
                            context,
                            output.GetPortName(),
                            output.SignalAddressKind == UINavigationSignalAddressKind.Custom
                                ? $"Signal · {output.CustomSignal}"
                                : $"Signal · {FormatKey(output.Key, "Unassigned")}");
                        break;

                    default:
                        AddOutputPort(
                            context,
                            output.GetPortName(),
                            $"UI Button · {FormatKey(output.Key, "Unassigned")}");
                        break;
                }
            }
        }

        internal UINavigationOutputDefinition[] GetOutputs()
        {
            UINavigationOutputCollection collection = GetOptionValue(
                DynamicOutputsOption,
                new UINavigationOutputCollection(InitialOutputs));
            return collection?.Items ?? Array.Empty<UINavigationOutputDefinition>();
        }

        internal UINavigationUIPhase GetOnEnterUI()
        {
            UINavigationUIPhase phase = GetOptionValue(
                OnEnterUIOption,
                new UINavigationUIPhase(InitialShowOnEnter, InitialHideOnEnter));
            return phase ?? new UINavigationUIPhase(
                InitialShowOnEnter,
                InitialHideOnEnter);
        }

        internal UINavigationUIPhase GetOnExitUI()
        {
            UINavigationUIPhase phase = GetOptionValue(
                OnExitUIOption,
                new UINavigationUIPhase(InitialShowOnExit, InitialHideOnExit, true));
            return phase ?? new UINavigationUIPhase(
                InitialShowOnExit,
                InitialHideOnExit,
                true);
        }

        private static void AddOutputPort(
            IPortDefinitionContext context,
            string portName,
            string displayName)
        {
            context.AddOutputPort(portName)
                .WithDisplayName(displayName)
                .WithTooltip("UI 또는 Action 노드에 직접 연결합니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        internal static string FormatKey(UIKey key, string fallback)
        {
            return key.IsValid ? key.ToString() : fallback;
        }
    }

    [Serializable]
    internal sealed class UINavigationSignalAddress
    {
        [SerializeField]
        private UINavigationSignalAddressKind kind =
            UINavigationSignalAddressKind.Custom;

        [SerializeField]
        private string customSignal = "Home";

        [SerializeField]
        private UIKey databaseSignal = new("Default", "Home");

        internal UINavigationSignalAddressKind Kind => kind;
        internal string CustomSignal => customSignal?.Trim() ?? string.Empty;
        internal UIKey DatabaseSignal => databaseSignal;

        internal UINavigationSignalAddress()
        {
        }

        internal UINavigationSignalAddress(string signal)
        {
            kind = UINavigationSignalAddressKind.Custom;
            customSignal = signal?.Trim() ?? string.Empty;
        }

        internal UINavigationSignalAddress(UIKey signal)
        {
            kind = UINavigationSignalAddressKind.Database;
            databaseSignal = signal;
        }

        internal void SetDatabaseSignal(UIKey signal)
        {
            databaseSignal = signal;
        }
    }

    [Node("UI Navigation/UI Manager", "", "Send Signal")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationSendSignalNode : UINavigationUINodeBase
    {
        internal const string AddressOption = "destination";

        internal UINavigationSignalAddress InitialAddress { get; set; } =
            new UINavigationSignalAddress();

        protected override bool UsesUseBackOption => false;

        protected override bool UsesDisplayNameOption => false;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(DisplayNameOption)
                .WithDisplayName("Display Name")
                .WithDefaultValue(InitialDisplayName ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();

            base.OnDefineOptions(context);
        }

        protected override void DefineViewOptions(IOptionDefinitionContext context)
        {
            context.AddOption<UINavigationSignalAddress>(AddressOption)
                .WithDisplayName("Go To")
                .WithTooltip("같은 Signal 주소를 가진 Portal이 연결한 UI로 이동합니다.")
                .WithDefaultValue(InitialAddress ?? new UINavigationSignalAddress())
                .ShowInInspectorOnly()
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            string displayName = GetOptionValue(DisplayNameOption, string.Empty);
            Title = string.IsNullOrWhiteSpace(displayName)
                ? $"Send Signal - {FormatSignal(GetAddress())}"
                : displayName.Trim();
            DefaultColor = UINavigationNodeColors.Destination;

            context.AddInputPort(EnterPort)
                .WithDisplayName("Enter")
                .WithTooltip("Signal, Time Delay 등 UI 전이 출력을 직접 연결합니다.")
                .WithCapacity(PortCapacity.Multi)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        internal UINavigationSignalAddress GetAddress()
        {
            return GetOptionValue(
                       AddressOption,
                       InitialAddress ?? new UINavigationSignalAddress()) ??
                   new UINavigationSignalAddress();
        }

        private static string FormatSignal(UINavigationSignalAddress address)
        {
            if (address == null)
                return "None";

            if (address.Kind == UINavigationSignalAddressKind.Custom)
            {
                return string.IsNullOrWhiteSpace(address.CustomSignal)
                    ? "None"
                    : address.CustomSignal;
            }

            return address.DatabaseSignal.IsValid
                ? address.DatabaseSignal.Key
                : "None";
        }
    }

    internal enum UINavigationPortalConditionKind
    {
        Signal = 1,
        Toggle = 2
    }

    [Serializable]
    internal sealed class UINavigationPortalCondition : ISerializationCallbackReceiver
    {
        [SerializeField]
        private UINavigationPortalConditionKind kind =
            UINavigationPortalConditionKind.Signal;

        [SerializeField]
        private UIKey key = new("Default", "Portal");

        [SerializeField]
        private UINavigationSignalAddressKind signalAddressKind =
            UINavigationSignalAddressKind.Database;

        [SerializeField]
        private string customSignal = "Home";

        [SerializeField]
        private bool toggleValue = true;

        internal UINavigationPortalConditionKind Kind =>
            (int)kind == 0 ? UINavigationPortalConditionKind.Signal : kind;
        internal UIKey Key => key;
        internal UINavigationSignalAddressKind SignalAddressKind => signalAddressKind;
        internal string CustomSignal => customSignal?.Trim() ?? string.Empty;
        internal bool ToggleValue => toggleValue;
        internal void SetKey(UIKey value) => key = value;
        internal void SetCustomSignal(string value)
        {
            signalAddressKind = UINavigationSignalAddressKind.Custom;
            customSignal = value?.Trim() ?? string.Empty;
        }

        internal UINavigationTriggerKind RuntimeTriggerKind => Kind switch
        {
            UINavigationPortalConditionKind.Toggle => UINavigationTriggerKind.Toggle,
            _ => UINavigationTriggerKind.Signal
        };

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if ((int)kind == 0)
                kind = UINavigationPortalConditionKind.Signal;
        }
    }

    [Node("UI Navigation/UI Manager", "", "Portal")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationPortalNode : Node
    {
        internal const string DisplayNameOption = "displayName";
        internal const string DescriptionOption = "description";
        internal const string ConditionOption = "condition";
        internal const string HistoryOption = "history";
        internal const string NextPort = "next";

        internal string InitialDisplayName { get; set; }
        internal UINavigationPortalCondition InitialCondition { get; set; }
        internal UINavigationTransitionKind InitialHistory { get; set; } = UINavigationTransitionKind.Push;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>(DisplayNameOption)
                .WithDisplayName("Display Name")
                .WithDefaultValue(InitialDisplayName ?? string.Empty)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();
            context.AddOption<string>(DescriptionOption)
                .WithDisplayName("Node Description")
                .WithDefaultValue(string.Empty)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();
            context.AddOption<UINavigationPortalCondition>(ConditionOption)
                .WithDisplayName("Condition")
                .WithDefaultValue(InitialCondition ?? new UINavigationPortalCondition())
                .ShowInInspectorOnly()
                .Build();
            context.AddOption<UINavigationTransitionKind>(HistoryOption)
                .WithDisplayName("History")
                .WithDefaultValue(InitialHistory)
                .ShowInInspectorOnly()
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            DefaultColor = UINavigationNodeColors.Portal;

            UINavigationPortalCondition condition = GetCondition();
            string keyName =
                condition.SignalAddressKind == UINavigationSignalAddressKind.Custom
                    ? condition.CustomSignal
                    : condition.Key.IsValid
                        ? condition.Key.ToString()
                        : "Unassigned";

            string displayName = GetOptionValue(DisplayNameOption, string.Empty);
            Title = string.IsNullOrWhiteSpace(displayName)
                ? $"Portal - {keyName}"
                : displayName.Trim();

            string portDisplayName = condition.Kind switch
            {
                UINavigationPortalConditionKind.Signal => $"Signal · {keyName}",
                UINavigationPortalConditionKind.Toggle => $"Toggle · {keyName} · {(condition.ToggleValue ? "On" : "Off")}",
                _ => $"UI Button · {keyName}"
            };

            context.AddOutputPort(NextPort)
                .WithDisplayName(portDisplayName)
                .WithTooltip("조건이 발생하면 현재 UI와 무관하게 이 연결로 강제 전이합니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        internal T GetOptionValue<T>(string optionName, T fallback = default)
        {
            INodeOption option = GetNodeOptionByName(optionName);
            return option != null && option.TryGetValue(out T value)
                ? value
                : fallback;
        }

        internal UINavigationPortalCondition GetCondition()
        {
            return GetOptionValue(
                       ConditionOption,
                       new UINavigationPortalCondition()) ??
                   new UINavigationPortalCondition();
        }
    }

    [Serializable]
    internal abstract class UINavigationActionNodeBase : Node
    {
        internal const string EnterPort = "enter";
        internal const string DisplayNameOption = "displayName";
        internal const string DescriptionOption = "description";

        protected void AddMetadataOptions(
            IOptionDefinitionContext context,
            string defaultDisplayName)
        {
            context.AddOption<string>(DisplayNameOption)
                .WithDisplayName("Display Name")
                .WithDefaultValue(defaultDisplayName)
                .Delayed()
                .Build();

            context.AddOption<string>(DescriptionOption)
                .WithDisplayName("Node Description")
                .WithDefaultValue(string.Empty)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();
        }

        protected void AddEnterPort(IPortDefinitionContext context)
        {
            DefaultColor = UINavigationNodeColors.Action;

            INodeOption displayName = GetNodeOptionByName(DisplayNameOption);
            if (displayName != null &&
                displayName.TryGetValue(out string value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                Title = value.Trim();
            }

            context.AddInputPort(EnterPort)
                .WithDisplayName("Enter")
                .WithTooltip("Transition 또는 이전 Action에서 들어오는 실행 연결입니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }

    [Node("UI Navigation/Time", "", "Time Scale")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationSetTimeScaleNode : UINavigationActionNodeBase
    {
        internal const string TimeScaleOption = "timeScale";
        internal const string NextPort = "next";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            AddMetadataOptions(context, "Time Scale");
            context.AddOption<float>(TimeScaleOption)
                .WithDisplayName("Time Scale")
                .WithTooltip("0은 일시정지, 1은 기본 속도입니다.")
                .WithDefaultValue(1f)
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            AddEnterPort(context);
            context.AddOutputPort(NextPort)
                .WithDisplayName("Next")
                .WithTooltip("다음 Screen 또는 Action에 연결합니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        internal float GetTimeScale()
        {
            INodeOption option = GetNodeOptionByName(TimeScaleOption);
            return option != null && option.TryGetValue(out float value) ? value : 1f;
        }
    }

    [Node("UI Navigation/Utils", "", "Debug Log")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationDebugLogNode : UINavigationActionNodeBase
    {
        internal const string LogTypeOption = "logType";
        internal const string MessageOption = "message";
        internal const string NextPort = "next";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<UINavigationDebugLogType>(LogTypeOption)
                .WithDisplayName("Log Type")
                .WithTooltip("Normal은 일반, Warning은 노랑, Error는 빨강으로 출력합니다.")
                .WithDefaultValue(UINavigationDebugLogType.Normal)
                .Build();
            context.AddOption<string>(MessageOption)
                .WithDisplayName("Message")
                .WithTooltip("Unity Console에 출력할 메시지입니다.")
                .WithDefaultValue(string.Empty)
                .ShowInInspectorOnly()
                .Delayed()
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            Title = "Debug Log";
            AddEnterPort(context);
            context.AddOutputPort(NextPort)
                .WithDisplayName("Next")
                .WithTooltip("로그 출력 후 실행할 Screen 또는 Action에 연결합니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        internal UINavigationDebugLogType GetLogType()
        {
            INodeOption option = GetNodeOptionByName(LogTypeOption);
            return option != null &&
                   option.TryGetValue(out UINavigationDebugLogType value)
                ? value
                : UINavigationDebugLogType.Normal;
        }

        internal string GetMessage()
        {
            INodeOption option = GetNodeOptionByName(MessageOption);
            return option != null && option.TryGetValue(out string value)
                ? value ?? string.Empty
                : string.Empty;
        }
    }

    [Node("UI Navigation/System", "", "Application Quit")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationApplicationQuitNode : UINavigationActionNodeBase
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            AddMetadataOptions(context, "Application Quit");
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            AddEnterPort(context);
        }
    }

    [Serializable]
    internal abstract class UINavigationSceneActionNodeBase : UINavigationActionNodeBase
    {
        internal const string SceneNameOption = "sceneName";
        internal const string NextPort = "next";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            AddMetadataOptions(context, GetType().Name
                .Replace("UINavigation", string.Empty)
                .Replace("Node", string.Empty));
            context.AddOption<string>(SceneNameOption)
                .WithDisplayName("Scene Name")
                .WithTooltip("Build Settings에 등록된 Scene 이름 또는 경로입니다.")
                .WithDefaultValue(string.Empty)
                .Delayed()
                .Build();

            DefineSceneOptions(context);
        }

        protected virtual void DefineSceneOptions(IOptionDefinitionContext context)
        {
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            AddEnterPort(context);
            context.AddOutputPort(NextPort)
                .WithDisplayName("Next")
                .WithTooltip("선택 사항입니다. 다음 Screen/Action으로 이어가거나 비워 두어 체인을 끝냅니다.")
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }

        internal string GetSceneName()
        {
            INodeOption option = GetNodeOptionByName(SceneNameOption);
            return option != null && option.TryGetValue(out string value)
                ? value?.Trim()
                : string.Empty;
        }
    }

    [Serializable]
    internal sealed class UINavigationLoadSceneSettings
    {
        [SerializeField]
        private UINavigationSceneReferenceKind referenceKind =
            UINavigationSceneReferenceKind.Name;

        [SerializeField]
        private string sceneName = string.Empty;

        [SerializeField, Min(0)]
        private int buildIndex;

        [SerializeField]
        private LoadSceneMode loadMode = LoadSceneMode.Single;

        [SerializeField]
        private bool allowSceneActivation = true;

        [SerializeField, Min(0f)]
        private float sceneActivationDelay = 0.2f;

        internal UINavigationSceneReferenceKind ReferenceKind => referenceKind;
        internal string SceneName => sceneName?.Trim() ?? string.Empty;
        internal int BuildIndex => buildIndex;
        internal LoadSceneMode LoadMode => loadMode;
        internal bool AllowSceneActivation => allowSceneActivation;
        internal float SceneActivationDelay => sceneActivationDelay;
    }

    [Node("UI Navigation/Scene Management", "", "Load Scene")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationLoadSceneNode : UINavigationSceneActionNodeBase
    {
        internal const string SettingsOption = "loadSceneSettings";

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            AddMetadataOptions(context, "Load Scene");
            context.AddOption<UINavigationLoadSceneSettings>(SettingsOption)
                .WithDisplayName("Scene")
                .WithDefaultValue(new UINavigationLoadSceneSettings())
                .ShowInInspectorOnly()
                .Build();
        }

        internal UINavigationLoadSceneSettings GetSettings()
        {
            INodeOption option = GetNodeOptionByName(SettingsOption);
            return option != null &&
                   option.TryGetValue(out UINavigationLoadSceneSettings value) &&
                   value != null
                ? value
                : new UINavigationLoadSceneSettings();
        }
    }

    [Node("UI Navigation/Scene Management", "", "Unload Scene")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationUnloadSceneNode : UINavigationSceneActionNodeBase
    {
    }

    [Node("UI Navigation/Scene Management", "", "Set Active Loaded Scene")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationSetActiveSceneNode : UINavigationSceneActionNodeBase
    {
    }

    internal static class UINavigationGraphPortExtensions
    {
        internal static List<IPort> GetConnections(this IPort port)
        {
            var connections = new List<IPort>();
            port?.GetConnectedPorts(connections);
            return connections;
        }

        internal static IEnumerable<TNode> GetConnectedNodes<TNode>(this IPort port)
            where TNode : class, INode
        {
            return port.GetConnections()
                .AsValueEnumerable()
                .Select(INodeExtensions.GetNode)
                .Where(node => node is TNode)
                .Select(node => (TNode)node)
                .ToArray();
        }
    }
}
