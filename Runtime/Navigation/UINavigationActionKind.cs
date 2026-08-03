namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Defines the available UI Navigation Action Kind values.
    /// </summary>
    internal enum UINavigationActionKind
    {
        SetTimeScale,
        ApplicationQuit,
        LoadScene,
        UnloadScene,
        SetActiveScene,
        DebugLog
    }

    /// <summary>
    /// Defines the available UI Navigation Debug Log Type values.
    /// </summary>
    internal enum UINavigationDebugLogType
    {
        Normal,
        Warning,
        Error
    }
}
