using System;
using Unity.GraphToolkit.Editor;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Defines the available UI Navigation Pivot Rotation values.
    /// </summary>
    internal enum UINavigationPivotRotation
    {
        /// <summary>
        /// Represents the right option.
        /// </summary>
        Right = 0,

        /// <summary>
        /// Represents the down option.
        /// </summary>
        Down = 1,

        /// <summary>
        /// Represents the left option.
        /// </summary>
        Left = 2,

        /// <summary>
        /// Represents the up option.
        /// </summary>
        Up = 3,
    }

    /// <summary>
    /// Verifies whether Graph Toolkit moves reused port views when a node is redefined.
    /// The port names intentionally stay unchanged between orientations so existing
    /// connections and port models are reused.
    /// </summary>
    [Node(
        "UI Navigation/Utils",
        "",
        "Pivot",
        "Packages/com.nkstudio.uitk-navigation/Editor/Styles/UINavigationPivotPrototypeNode.uss")]
    [UseWithGraph(typeof(UINavigationAuthoringGraph))]
    [Serializable]
    internal sealed class UINavigationPivotPrototypeNode : Node
    {
        internal const string RotationOption = "rotation";
        internal const string EnterPort = "enter";
        internal const string ExitPort = "exit";

        internal IPort GetEnterPort()
        {
            return GetInputPortByName(EnterPort);
        }

        internal IPort GetExitPort()
        {
            return GetOutputPortByName(ExitPort);
        }

        /// <summary>
        /// Determines whether vertical.
        /// </summary>
        internal static bool IsVertical(UINavigationPivotRotation rotation)
        {
            return rotation is UINavigationPivotRotation.Down or UINavigationPivotRotation.Up;
        }

        /// <summary>
        /// Determines whether reversed.
        /// </summary>
        internal static bool IsReversed(UINavigationPivotRotation rotation)
        {
            return rotation is UINavigationPivotRotation.Left or UINavigationPivotRotation.Up;
        }

        internal UINavigationPivotRotation Rotation
        {
            get
            {
                INodeOption option = GetNodeOptionByName(RotationOption);
                return option != null && option.TryGetValue(out UINavigationPivotRotation value)
                    ? value
                    : UINavigationPivotRotation.Right;
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<UINavigationPivotRotation>(RotationOption)
                .WithDisplayName(string.Empty)
                .WithTooltip("포트 배치를 시계방향으로 회전합니다.")
                .WithDefaultValue(UINavigationPivotRotation.Right)
                .Build();
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            Title = string.Empty;
            DefaultColor = UINavigationNodeColors.Portal;

            context.AddInputPort(EnterPort)
                .WithDisplayName(string.Empty)
                .WithTooltip("Pivot input")
                .WithCapacity(PortCapacity.Multi)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();

            context.AddOutputPort(ExitPort)
                .WithDisplayName(string.Empty)
                .WithTooltip("Pivot output")
                .WithCapacity(PortCapacity.Single)
                .WithConnectorUI(PortConnectorUI.Arrowhead)
                .Build();
        }
    }
}
