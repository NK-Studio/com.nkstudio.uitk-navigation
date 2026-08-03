using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Popup
{
    /// <summary>
    /// Provides UI Popup Action Button functionality.
    /// </summary>
    [UxmlElement]
    public partial class UIPopupActionButton : Button
    {
        /// <summary>
        /// Gets or sets the identifier raised when this action button is selected.
        /// </summary>
        [UxmlAttribute("action-id")]
        public string ActionId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether invoking this action also closes its containing popup.
        /// </summary>
        [UxmlAttribute("close-popup")]
        public bool ClosePopup { get; set; } = true;

        /// <summary>
        /// Initializes a popup action button and wires its click handler.
        /// </summary>
        public UIPopupActionButton()
        {
            AddToClassList("ui-popup-action-button");
            clicked += OnClicked;
        }

        private void OnClicked()
        {
            GetFirstAncestorOfType<UIPopupView>()
                ?.RequestAction(ActionId, ClosePopup);
        }
    }
}
