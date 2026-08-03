using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Defines the available UI Navigation Scene Reference Kind values.
    /// </summary>
    internal enum UINavigationSceneReferenceKind
    {
        Name,
        BuildIndex
    }

    /// <summary>
    /// Provides UI Navigation Action functionality.
    /// </summary>
    [Serializable]
    internal sealed class UINavigationAction
    {
        [SerializeField]
        private UINavigationActionKind kind;

        [SerializeField]
        private float timeScale = 1f;

        [SerializeField]
        private string sceneName;

        [SerializeField]
        private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

        [SerializeField]
        private UINavigationSceneReferenceKind sceneReferenceKind =
            UINavigationSceneReferenceKind.Name;

        [SerializeField]
        private int sceneBuildIndex;

        [SerializeField]
        private bool allowSceneActivation = true;

        [SerializeField]
        private float sceneActivationDelay;

        [SerializeField]
        private UINavigationDebugLogType debugLogType;

        [SerializeField]
        private string debugMessage;

        private UINavigationAction(
            UINavigationActionKind kind,
            float timeScale,
            string sceneName,
            LoadSceneMode loadSceneMode,
            UINavigationSceneReferenceKind sceneReferenceKind =
                UINavigationSceneReferenceKind.Name,
            int sceneBuildIndex = 0,
            bool allowSceneActivation = true,
            float sceneActivationDelay = 0f,
            UINavigationDebugLogType debugLogType = UINavigationDebugLogType.Normal,
            string debugMessage = null)
        {
            this.kind = kind;
            this.timeScale = timeScale;
            this.sceneName = sceneName;
            this.loadSceneMode = loadSceneMode;
            this.sceneReferenceKind = sceneReferenceKind;
            this.sceneBuildIndex = sceneBuildIndex;
            this.allowSceneActivation = allowSceneActivation;
            this.sceneActivationDelay = Mathf.Max(0f, sceneActivationDelay);
            this.debugLogType = debugLogType;
            this.debugMessage = debugMessage;
        }

        /// <summary>
        /// Gets the kind.
        /// </summary>
        public UINavigationActionKind Kind => kind;

        /// <summary>
        /// Gets the time scale.
        /// </summary>
        public float TimeScale => timeScale;

        /// <summary>
        /// Gets the scene name.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Gets the load scene mode.
        /// </summary>
        public LoadSceneMode LoadSceneMode => loadSceneMode;
        public UINavigationSceneReferenceKind SceneReferenceKind => sceneReferenceKind;
        public int SceneBuildIndex => sceneBuildIndex;
        public bool AllowSceneActivation => allowSceneActivation;
        public float SceneActivationDelay => sceneActivationDelay;
        public UINavigationDebugLogType DebugLogType => debugLogType;
        public string DebugMessage => debugMessage ?? string.Empty;

        /// <summary>
        /// Sets t im es ca le.
        /// </summary>
        public static UINavigationAction SetTimeScale(float value)
        {
            return new UINavigationAction(
                UINavigationActionKind.SetTimeScale,
                Mathf.Max(0f, value),
                null,
                LoadSceneMode.Single);
        }

        /// <summary>
        /// Performs the application quit operation.
        /// </summary>
        public static UINavigationAction ApplicationQuit()
        {
            return new UINavigationAction(
                UINavigationActionKind.ApplicationQuit,
                1f,
                null,
                LoadSceneMode.Single);
        }

        /// <summary>
        /// Performs the debug log operation.
        /// </summary>
        public static UINavigationAction DebugLog(
            UINavigationDebugLogType logType,
            string message)
        {
            return new UINavigationAction(
                UINavigationActionKind.DebugLog,
                1f,
                null,
                LoadSceneMode.Single,
                debugLogType: logType,
                debugMessage: message ?? string.Empty);
        }

        /// <summary>
        /// Performs the load scene operation.
        /// </summary>
        public static UINavigationAction LoadScene(string name, LoadSceneMode mode)
        {
            return LoadScene(
                UINavigationSceneReferenceKind.Name,
                name,
                0,
                mode,
                true,
                0f);
        }

        /// <summary>
        /// Performs the load scene operation.
        /// </summary>
        public static UINavigationAction LoadScene(
            UINavigationSceneReferenceKind referenceKind,
            string name,
            int buildIndex,
            LoadSceneMode mode,
            bool allowActivation,
            float activationDelay)
        {
            return new UINavigationAction(
                UINavigationActionKind.LoadScene,
                1f,
                name,
                mode,
                referenceKind,
                Mathf.Max(0, buildIndex),
                allowActivation,
                activationDelay);
        }

        /// <summary>
        /// Performs the unload scene operation.
        /// </summary>
        public static UINavigationAction UnloadScene(string name)
        {
            return new UINavigationAction(
                UINavigationActionKind.UnloadScene,
                1f,
                name,
                LoadSceneMode.Single);
        }

        /// <summary>
        /// Sets a ct iv es ce ne.
        /// </summary>
        public static UINavigationAction SetActiveScene(string name)
        {
            return new UINavigationAction(
                UINavigationActionKind.SetActiveScene,
                1f,
                name,
                LoadSceneMode.Single);
        }
    }
}
