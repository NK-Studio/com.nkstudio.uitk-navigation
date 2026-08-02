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

        [MenuItem(MenuPath, priority = 100)]
        private static void Toggle()
        {
            UINavigationGraphVisualizer.Enabled = !UINavigationGraphVisualizer.Enabled;
            Menu.SetChecked(MenuPath, UINavigationGraphVisualizer.Enabled);
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, UINavigationGraphVisualizer.Enabled);
            return true;
        }
    }
}
#endif
