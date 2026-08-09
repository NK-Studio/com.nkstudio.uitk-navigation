using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    [Serializable]
    internal sealed class UINavigationRandomOutputDefinition : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
        private string outputId = Guid.NewGuid().ToString("N");

        [SerializeField, Min(0f)]
        private float weight = 100f;

        internal string OutputId => outputId;
        internal float Weight => weight;

        internal UINavigationRandomOutputDefinition()
        {
        }

        internal UINavigationRandomOutputDefinition(float weight)
            : this(Guid.NewGuid().ToString("N"), weight)
        {
        }

        internal UINavigationRandomOutputDefinition(string outputId, float weight)
        {
            this.outputId = string.IsNullOrEmpty(outputId)
                ? Guid.NewGuid().ToString("N")
                : outputId;
            this.weight = Mathf.Max(0f, weight);
        }

        internal string GetPortName()
        {
            EnsureId();
            return outputId;
        }

        public void OnBeforeSerialize()
        {
            EnsureId();
        }

        public void OnAfterDeserialize()
        {
            EnsureId();
        }

        private void EnsureId()
        {
            if (string.IsNullOrEmpty(outputId))
                outputId = Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    internal sealed class UINavigationRandomOutputCollection
    {
        [SerializeField]
        private UINavigationRandomOutputDefinition[] items = Array.Empty<UINavigationRandomOutputDefinition>();

        internal UINavigationRandomOutputDefinition[] Items => items ?? Array.Empty<UINavigationRandomOutputDefinition>();

        internal UINavigationRandomOutputCollection()
        {
        }

        internal UINavigationRandomOutputCollection(UINavigationRandomOutputDefinition[] items)
        {
            this.items = items ?? Array.Empty<UINavigationRandomOutputDefinition>();
        }
    }

    [Node("UI Navigation/Utils", "", "Random")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationRandomNode : UINavigationUINodeBase
    {
        internal const string RandomOutputsOption = "randomOutputs";

        internal UINavigationRandomOutputDefinition[] InitialOutputs { get; set; } = new[]
        {
            new UINavigationRandomOutputDefinition(100f),
            new UINavigationRandomOutputDefinition(100f)
        };

        protected override bool UsesDisplayNameOption => false;

        protected override bool UsesUseBackOption => false;

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<UINavigationRandomOutputCollection>(RandomOutputsOption)
                .WithDisplayName("Random Outputs")
                .WithTooltip("랜덤으로 분기할 출력들을 설정합니다.")
                .WithDefaultValue(new UINavigationRandomOutputCollection(InitialOutputs))
                .ShowInInspectorOnly()
                .Build();
        }

        protected override void DefineViewOptions(IOptionDefinitionContext context)
        {
        }

        protected override void DefineOutputPorts(IPortDefinitionContext context)
        {
            Title = "Random";
            DefaultColor = UINavigationNodeColors.Portal;
            
            UINavigationRandomOutputDefinition[] outputs = GetOutputs();
            var portNames = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < outputs.Length; index++)
            {
                UINavigationRandomOutputDefinition output = outputs[index];
                if (output == null)
                    continue;

                string portName = output.GetPortName();
                if (!portNames.Add(portName))
                    continue;

                context.AddOutputPort(portName)
                    .WithDisplayName(" ")
                    .WithTooltip("UI 또는 Action 노드에 직접 연결합니다.")
                    .WithConnectorUI(PortConnectorUI.Arrowhead)
                    .Build();
            }
        }

        internal UINavigationRandomOutputDefinition[] GetOutputs()
        {
            UINavigationRandomOutputCollection collection = GetOptionValue(
                RandomOutputsOption,
                new UINavigationRandomOutputCollection(InitialOutputs));
            return collection?.Items ?? Array.Empty<UINavigationRandomOutputDefinition>();
        }

        /// <summary>
        /// Performs the collect port shares operation.
        /// </summary>
        internal void CollectPortShares(List<(IPort Port, string Text)> results)
        {
            results.Clear();

            UINavigationRandomOutputDefinition[] outputs = GetOutputs();
            float totalWeight = 0f;

            foreach (UINavigationRandomOutputDefinition output in outputs)
            {
                if (output == null || !IsPortConnected(output))
                    continue;

                float weight = output.Weight;
                if (weight > 0f && !float.IsNaN(weight) && !float.IsInfinity(weight))
                    totalWeight += weight;
            }

            foreach (UINavigationRandomOutputDefinition output in outputs)
            {
                if (output == null)
                    continue;

                IPort port = GetOutputPortByName(output.GetPortName());
                if (port == null)
                    continue;

                results.Add((port, FormatShare(output, totalWeight, IsPortConnected(output))));
            }
        }

        private bool IsPortConnected(UINavigationRandomOutputDefinition output)
        {
            IPort port = GetOutputPortByName(output.GetPortName());
            return port != null && port.GetConnections().Count > 0;
        }

        private static string FormatShare(
            UINavigationRandomOutputDefinition output,
            float totalWeight,
            bool isConnected)
        {
            if (!isConnected)
                return "0%";

            if (totalWeight <= 0f)
                return "NaN%";

            float weight = output.Weight;
            if (weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight))
                return "0%";

            int percent = Mathf.Clamp(Mathf.RoundToInt(weight / totalWeight * 100f), 1, 100);
            return percent.ToString(CultureInfo.InvariantCulture) + "%";
        }
    }
}
