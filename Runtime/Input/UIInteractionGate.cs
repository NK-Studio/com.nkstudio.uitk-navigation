namespace NKStudio.UITKNavigation.Input
{
    /// <summary>
    /// Provides UI Interaction Gate functionality.
    /// </summary>
    internal static class UIInteractionGate
    {
        private static int _blockLevel;

        /// <summary>
        /// Gets a value indicating whether blocked.
        /// </summary>
        public static bool IsBlocked => _blockLevel > 0;

        /// <summary>
        /// Gets or sets a value indicating whether text input focused.
        /// </summary>
        public static bool IsTextInputFocused { get; private set; }

        /// <summary>
        /// Performs the push block operation.
        /// </summary>
        public static void PushBlock()
        {
            _blockLevel++;
        }

        /// <summary>
        /// Performs the pop block operation.
        /// </summary>
        public static void PopBlock()
        {
            if (_blockLevel > 0)
                _blockLevel--;
        }

        /// <summary>
        /// Resets b lo ck s.
        /// </summary>
        public static void ResetBlocks()
        {
            _blockLevel = 0;
        }

        /// <summary>
        /// Sets t ex ti np ut fo cu se d.
        /// </summary>
        public static void SetTextInputFocused(bool focused)
        {
            IsTextInputFocused = focused;
        }
    }
}
