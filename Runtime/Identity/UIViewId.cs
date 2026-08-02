using UnityEngine;

namespace NKStudio.UITKNavigation.Identity
{
    /// <summary>
    /// Provides UI View Id functionality.
    /// </summary>
    [CreateAssetMenu(menuName = "UITK Navigation/UI View Id", fileName = "UIViewId")]
    internal sealed class UIViewId : ScriptableObject
    {
        [SerializeField]
        private string category = "General";

        [SerializeField, TextArea(1, 4)]
        private string description;

        /// <summary>
        /// Gets the category.
        /// </summary>
        public string Category => category;

        /// <summary>
        /// Gets the description.
        /// </summary>
        public string Description => description;

        /// <summary>
        /// Performs the to string operation.
        /// </summary>
        public override string ToString()
        {
            return name;
        }
    }
}
