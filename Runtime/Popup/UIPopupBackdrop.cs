using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Provides UI Popup Backdrop functionality.
    /// </summary>
    [UxmlElement]
    public partial class UIPopupBackdrop : VisualElement
    {
        /// <summary>
        /// Initializes a full-size popup backdrop element.
        /// </summary>
        public UIPopupBackdrop()
        {
            AddToClassList("ui-popup-backdrop");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.top = 0f;
            style.bottom = 0f;
            pickingMode = PickingMode.Position;
        }
    }
}
