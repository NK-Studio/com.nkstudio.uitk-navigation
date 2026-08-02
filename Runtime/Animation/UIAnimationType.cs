namespace NKStudio.UITKNavigation.Animation
{
    /// <summary>
    /// Defines the available UI Animation Type values.
    /// </summary>
    internal enum UIAnimationType
    {
        /// <summary>
        /// Represents the show option.
        /// </summary>
        Show,

        /// <summary>
        /// Defines the available UI Animation Play Mode values.
        /// </summary>
        Hide
    }

    /// <summary>
    /// Describes how a channel travels between its authored From and To values.
    /// </summary>
    internal enum UIAnimationPlayMode
    {
        /// <summary>From to To. A new loop restarts at From.</summary>
        Normal,

        /// <summary>From to To and back to From during one cycle.</summary>
        PingPong,

        /// <summary>Damped oscillation that returns to From.</summary>
        Spring,

        /// <summary>Deterministic shake between the authored values that returns to From.</summary>
        Shake
    }
}
