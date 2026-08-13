#if UNITY_6000_6_OR_NEWER
using UnityEditor;

namespace NKStudio.UITKNavigation.Editor.Navigation
{
    /// <summary>
    /// Provides UI Navigation Visualizer Menu functionality.
    /// </summary>
    internal static class UINavigationVisualizerMenu
    {
        private const string MenuPath = "Tools/UI Navigation/Follow Play Mode In Graph";

        // Ordered right after Tools/UI Navigation/Key Catalog (priority 100).
        [MenuItem(MenuPath, priority = 101)]
        private static void Toggle()
        {
            UINavigationGraphVisualizer.Enabled = !UINavigationGraphVisualizer.Enabled;
            Menu.SetChecked(MenuPath, UINavigationGraphVisualizer.Enabled);
        }

        [MenuItem(MenuPath, isValidateFunction: true, priority = 101)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, UINavigationGraphVisualizer.Enabled);
            return true;
        }
    }
}
#endif
