using System;
using NKStudio.UITKNavigation.Animation;
using UnityEngine;
using UnityEngine.UIElements;

namespace NKStudio.UITKNavigation.Elements
{
    /// <summary>
    /// Provides UI View Startup Settings functionality.
    /// </summary>
    [Serializable]
    [UxmlObject]
    internal partial class UIViewStartupSettings
    {
        /// <summary>
        /// Gets or sets the on start.
        /// </summary>
        [UxmlAttribute("on-start")]
        public UIViewStartBehaviour OnStart { get; set; } = UIViewStartBehaviour.Disabled;

        /// <summary>
        /// Gets or sets the hide mode.
        /// </summary>
        [UxmlAttribute("hide-mode")]
        public UIViewHideMode HideMode { get; set; } = UIViewHideMode.Display;

        /// <summary>
        /// Gets or sets the use custom start position.
        /// </summary>
        [UxmlAttribute("use-custom-start-position")]
        public bool UseCustomStartPosition { get; set; } = true;

        /// <summary>
        /// Gets or sets the custom start position.
        /// </summary>
        [UxmlAttribute("custom-start-position")]
        public Vector3 CustomStartPosition { get; set; }

        /// <summary>
        /// Gets or sets the auto hide after show.
        /// </summary>
        [UxmlAttribute("auto-hide-after-show")]
        public bool AutoHideAfterShow { get; set; }

        /// <summary>
        /// Gets or sets the auto hide delay.
        /// </summary>
        [UxmlAttribute("auto-hide-delay")]
        public float AutoHideDelay { get; set; } = 3f;

        /// <summary>
        /// Gets the starts hidden.
        /// </summary>
        public bool StartsHidden =>
            OnStart is UIViewStartBehaviour.InstantHide or UIViewStartBehaviour.AnimationHide;
    }
}
