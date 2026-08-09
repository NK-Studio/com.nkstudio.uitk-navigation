using UnityEngine;

namespace NKStudio.UITKNavigation.Identity
{
    /// <summary>
    /// Identifies the Key Catalog collection from which an editor selector obtains values.
    /// </summary>
    public enum UIKeyCatalogKind
    {
        /// <summary>Selects registered view identifiers.</summary>
        View = 0,
        /// <summary>Selects toggle identifiers.</summary>
        Toggle = 1,
        /// <summary>Selects general navigation signal identifiers.</summary>
        Signal = 2
    }

    /// <summary>
    /// Provides UI Key Selector Attribute functionality.
    /// </summary>
    public sealed class UIKeySelectorAttribute : PropertyAttribute
    {
        /// <summary>
        /// Initializes a selector that pairs the decorated category field with a sibling key field.
        /// </summary>
        /// <param name="keyPropertyName">The serialized name of the sibling key property.</param>
        /// <param name="kind">The Key Catalog collection displayed by the editor selector.</param>
        public UIKeySelectorAttribute(
            string keyPropertyName,
            UIKeyCatalogKind kind = UIKeyCatalogKind.Signal)
        {
            KeyPropertyName = keyPropertyName;
            Kind = kind;
        }

        /// <summary>
        /// Gets the serialized name of the sibling property that stores the selected key.
        /// </summary>
        public string KeyPropertyName { get; }
        /// <summary>
        /// Gets the Key Catalog collection displayed by the selector.
        /// </summary>
        public UIKeyCatalogKind Kind { get; }
    }
}
