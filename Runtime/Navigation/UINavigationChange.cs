namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Provides UI Navigation Change functionality.
    /// </summary>
    internal sealed class UINavigationChange
    {
        /// <summary>
        /// Initializes a new instance of <see cref="UINavigationChange"/>.
        /// </summary>
        public UINavigationChange(UINavigationNode previous, UINavigationNode next, UINavigationTransitionKind kind)
        {
            Previous = previous;
            Next = next;
            Kind = kind;
        }

        /// <summary>
        /// Gets the previous.
        /// </summary>
        public UINavigationNode Previous { get; }

        /// <summary>
        /// Gets the next.
        /// </summary>
        public UINavigationNode Next { get; }

        /// <summary>
        /// Gets the kind.
        /// </summary>
        public UINavigationTransitionKind Kind { get; }

        /// <summary>
        /// Gets or sets the transition.
        /// </summary>
        public UINavigationTransition Transition { get; internal set; }

        /// <summary>
        /// Gets or sets the cancel.
        /// </summary>
        public bool Cancel { get; set; }
    }
}
