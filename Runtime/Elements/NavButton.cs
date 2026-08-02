using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Elements
{
    /// <summary>
    /// Provides Nav Button functionality.
    /// </summary>
    [UxmlElement]
    public partial class NavButton : Button
    {
        /// <summary>
        /// Gets or sets the catalog category used to resolve this button's navigation signal.
        /// </summary>
        [UIKeySelector(nameof(SignalKey), UIKeyCatalogKind.Button)]
        [UxmlAttribute("signal-category")]
        public string SignalCategory { get; set; } = "Default";

        /// <summary>
        /// Gets or sets the key used to identify this button's navigation signal within its category.
        /// </summary>
        [UnityEngine.HideInInspector]
        [UxmlAttribute("signal-key")]
        public string SignalKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets the normalized navigation signal represented by the configured category and key.
        /// </summary>
        public UIKey Signal => new UIKey(SignalCategory, SignalKey);

        /// <summary>
        /// Initializes a new navigation button and wires its click event to the navigation event bus.
        /// </summary>
        public NavButton()
        {
            AddToClassList("ui-navigation-button");
            clicked += EmitSignal;
        }

        private void EmitSignal()
        {
            UIKey signal = Signal;
            if (!signal.IsValid)
            {
                Debug.LogWarning($"[NavButton] '{name}'의 Signal Category/Key가 비어 있습니다.");
                return;
            }

            UINavigationEvents.RequestButtonSignal(signal);
        }
    }
}
