using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Elements
{
    /// <summary>
    /// Provides Nav Toggle functionality.
    /// </summary>
    [UxmlElement]
    public partial class NavToggle : Toggle
    {
        /// <summary>
        /// Gets or sets the catalog category used to resolve this toggle's navigation output.
        /// </summary>
        [UIKeySelector(nameof(ToggleKey), UIKeyCatalogKind.Button)]
        [UxmlAttribute("toggle-category")]
        public string ToggleCategory { get; set; } = "Default";

        /// <summary>
        /// Gets or sets the key used to identify this toggle's navigation output within its category.
        /// </summary>
        [HideInInspector]
        [UxmlAttribute("toggle-key")]
        public string ToggleKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets the normalized navigation key represented by the configured category and key.
        /// </summary>
        public UIKey Toggle => new UIKey(ToggleCategory, ToggleKey);

        /// <summary>
        /// Initializes a new navigation toggle and wires value changes to the navigation event bus.
        /// </summary>
        public NavToggle()
        {
            AddToClassList("ui-navigation-toggle");
            RegisterCallback<ChangeEvent<bool>>(OnValueChanged);
        }

        private void OnValueChanged(ChangeEvent<bool> evt)
        {
            UIKey toggle = Toggle;
            if (!toggle.IsValid)
            {
                Debug.LogWarning($"[NavToggle] '{name}'의 Toggle Category/Key가 비어 있습니다.");
                return;
            }

            UINavigationEvents.RequestToggle(toggle, evt.newValue);
        }
    }
}
