using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Provides UI Popup Content functionality.
    /// </summary>
    [UxmlElement]
    public partial class UIPopupContent : VisualElement
    {
        /// <summary>
        /// Initializes the content container used inside a popup view.
        /// </summary>
        public UIPopupContent()
        {
            AddToClassList("ui-popup-content");
            pickingMode = PickingMode.Position;
        }
    }
}
