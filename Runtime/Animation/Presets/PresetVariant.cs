namespace NKStudio.UITKNavigation.Animation.Presets
{
    /// <summary>
    /// Represents Preset Variant data.
    /// </summary>
    internal readonly struct PresetVariant
    {
        /// <summary>
        /// Gets the name.
        /// </summary>
        internal readonly string Name;

        internal readonly PresetChannel ShowMove;
        internal readonly PresetChannel ShowRotate;
        internal readonly PresetChannel ShowScale;
        internal readonly PresetChannel ShowFade;

        internal readonly PresetChannel HideMove;
        internal readonly PresetChannel HideRotate;
        internal readonly PresetChannel HideScale;
        internal readonly PresetChannel HideFade;

        internal PresetVariant(
            string name,
            PresetChannel showMove,
            PresetChannel showRotate,
            PresetChannel showScale,
            PresetChannel showFade,
            PresetChannel hideMove,
            PresetChannel hideRotate,
            PresetChannel hideScale,
            PresetChannel hideFade)
        {
            Name = name;
            ShowMove = showMove;
            ShowRotate = showRotate;
            ShowScale = showScale;
            ShowFade = showFade;
            HideMove = hideMove;
            HideRotate = hideRotate;
            HideScale = hideScale;
            HideFade = hideFade;
        }

        internal PresetChannel GetMove(UIAnimationType type) =>
            type == UIAnimationType.Show ? ShowMove : HideMove;

        internal PresetChannel GetRotate(UIAnimationType type) =>
            type == UIAnimationType.Show ? ShowRotate : HideRotate;

        internal PresetChannel GetScale(UIAnimationType type) =>
            type == UIAnimationType.Show ? ShowScale : HideScale;

        internal PresetChannel GetFade(UIAnimationType type) =>
            type == UIAnimationType.Show ? ShowFade : HideFade;
    }
}
