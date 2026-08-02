using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides UI Navigation Node Option Writer functionality.
    /// </summary>
    internal static class UINavigationNodeOptionWriter
    {
        private static readonly PropertyInfo PortModelProperty =
            typeof(INodeOption).Assembly
                .GetType("Unity.GraphToolkit.Editor.NodeOption")
                ?.GetProperty(
                    "PortModel",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        internal static bool TrySetValue<T>(INodeOption option, T value)
        {
            if (option == null || PortModelProperty == null)
                return false;

            return PortModelProperty.GetValue(option) is IPort port &&
                   port.TrySetValue(value);
        }
    }
}
