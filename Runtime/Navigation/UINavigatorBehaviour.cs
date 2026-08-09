using System.Collections;
using NKStudio.UITKNavigation.Animation;
using NKStudio.UITKNavigation.Identity;
using NKStudio.UITKNavigation.Navigation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NKStudio.UITKNavigation.Navigation
{
    /// <summary>
    /// Provides UI Navigator Behaviour functionality.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UINavigatorBehaviour : MonoBehaviour
    {
        /// <summary>
        /// Defines the max initialize wait frames value.
        /// </summary>
        private const int MaxInitializeWaitFrames = 3;

        private static UINavigatorBehaviour _instance;
        private AsyncOperation _pendingSceneActivation;
        private bool _initializePending;
        private int _initializeWaitedFrames;

        [SerializeField]
        [Tooltip("Project 창에서 Create > UI Navigation > UI Navigation Graph로 생성한 .uinavgraph 에셋입니다.")]
        private UINavigationAsset navigationAsset;

        [SerializeField]
        [Tooltip("뒤로 가기 스택에 보관할 최대 깊이입니다.")]
        private int maxHistoryDepth = 32;

        /// <summary>
        /// Gets or sets the service.
        /// </summary>
        public UINavigationService Service { get; private set; }

        /// <summary>
        /// Gets the active.
        /// </summary>
        public static UINavigatorBehaviour Active => _instance;

        /// <summary>
        /// Gets a value indicating whether s pending scene activation.
        /// </summary>
        public bool HasPendingSceneActivation =>
            _pendingSceneActivation != null && !_pendingSceneActivation.isDone;

        /// <summary>
        /// Performs the activate pending scene operation.
        /// </summary>
        public bool ActivatePendingScene()
        {
            if (!HasPendingSceneActivation)
                return false;

            _pendingSceneActivation.allowSceneActivation = true;
            _pendingSceneActivation = null;
            return true;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                LogDuplicateAndDisable();
                return;
            }

            _instance = this;
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                LogDuplicateAndDisable();
                return;
            }

            _instance = this;

            Service = new UINavigationService(navigationAsset) { MaxHistoryDepth = maxHistoryDepth };
            Service.HideCommandsRequested += UINavigationEvents.ApplyViewHide;
            Service.ShowCommandsRequested += UINavigationEvents.ApplyViewShow;
            Service.ResyncViewsRequested += UINavigationEvents.ApplyViewResync;
            Service.ActionRequested += ExecuteAction;
            Service.NodeChanging += UINavigationEvents.RaiseNodeChanging;
            Service.NodeChanged += UINavigationEvents.RaiseNodeChanged;

            UINavigationEvents.BackRequested += OnBackRequested;
            UINavigationEvents.ForwardRequested += OnForwardRequested;
            UINavigationEvents.ResyncRequested += OnResyncRequested;
            UINavigationEvents.ToggleRequested += OnToggleRequested;
            UINavigationEvents.ViewTransitionStarted += OnViewTransitionStarted;
            UINavigationEvents.GoToNodeRequested += OnGoToNodeRequested;

            _initializePending = true;
            _initializeWaitedFrames = 0;
        }

        /// <summary>
        /// Determines whether initialize now.
        /// </summary>
        private bool CanInitializeNow()
        {
            if (UIViewRegistry.Count > 0)
                return true;

            _initializeWaitedFrames++;
            return _initializeWaitedFrames > MaxInitializeWaitFrames;
        }

        private void LogDuplicateAndDisable()
        {
            Debug.LogError(
                $"[UINavigation] '{name}'에 UINavigatorBehaviour가 중복으로 존재하여 비활성화합니다. " +
                $"이미 '{_instance.name}'가 활성 상태입니다.", this);
            enabled = false;
        }

        private void OnDisable()
        {
            UINavigationEvents.BackRequested -= OnBackRequested;
            UINavigationEvents.ForwardRequested -= OnForwardRequested;
            UINavigationEvents.ResyncRequested -= OnResyncRequested;
            UINavigationEvents.ToggleRequested -= OnToggleRequested;
            UINavigationEvents.ViewTransitionStarted -= OnViewTransitionStarted;
            UINavigationEvents.GoToNodeRequested -= OnGoToNodeRequested;

            if (Service != null)
            {
                Service.HideCommandsRequested -= UINavigationEvents.ApplyViewHide;
                Service.ShowCommandsRequested -= UINavigationEvents.ApplyViewShow;
                Service.ResyncViewsRequested -= UINavigationEvents.ApplyViewResync;
                Service.ActionRequested -= ExecuteAction;
                Service.NodeChanging -= UINavigationEvents.RaiseNodeChanging;
                Service.NodeChanged -= UINavigationEvents.RaiseNodeChanged;
                Service = null;
            }

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Handles the back requested event.
        /// </summary>
        private void OnBackRequested()
        {
            if (TryConsumeByVisibleView())
                return;

            if (Service?.ActiveNode?.UseBack == true && Service.Back())
                return;

            UINavigationEvents.RaiseBackUnhandled();
        }

        internal static bool TryConsumeByVisibleView()
        {
            var visibleViews = UIViewVisibility.VisibleViews;

            int highestPriority = int.MinValue;
            for (int i = visibleViews.Count - 1; i >= 0; i--)
            {
                UIViewVisibility view = visibleViews[i];
                if (view == null || !view.IsVisible ||
                    (!view.HideOnBackButton && !view.BlockBackButton))
                    continue;

                if (view.BackPriority > highestPriority)
                    highestPriority = view.BackPriority;
            }

            if (highestPriority == int.MinValue)
                return false;

            for (int i = visibleViews.Count - 1; i >= 0; i--)
            {
                UIViewVisibility view = visibleViews[i];
                if (view == null || !view.IsVisible)
                    continue;

                if (view.BackPriority == highestPriority && view.HideOnBackButton)
                {
                    if (view.BackHandler != null && view.BackHandler())
                        return true;

                    view.Hide();
                    return true;
                }
            }

            for (int i = visibleViews.Count - 1; i >= 0; i--)
            {
                UIViewVisibility view = visibleViews[i];
                if (view == null || !view.IsVisible)
                    continue;

                if (view.BackPriority == highestPriority && view.BlockBackButton)
                    return true;
            }

            return false;
        }

        private void OnForwardRequested()
        {
            Service?.Forward();
        }

        private void OnResyncRequested()
        {
            Service?.Resync();
        }

        private void OnToggleRequested(UIKey toggle, bool value)
        {
            Service?.TriggerToggle(toggle, value);
        }

        private void OnViewTransitionStarted(
            UIKey view,
            UIViewOutputCondition condition)
        {
            Service?.TriggerView(view, condition);
        }

        private void Update()
        {
            if (_initializePending && CanInitializeNow())
            {
                _initializePending = false;
                Service?.Initialize();
            }

            Service?.Tick(Time.unscaledDeltaTime);
        }

        private void OnGoToNodeRequested(string nodeId)
        {
            Service?.GoTo(nodeId);
        }

        private void ExecuteAction(UINavigationAction action)
        {
            switch (action.Kind)
            {
                case UINavigationActionKind.SetTimeScale:
                    Time.timeScale = action.TimeScale;
                    break;

                case UINavigationActionKind.ApplicationQuit:
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                    break;

                case UINavigationActionKind.LoadScene:
                    StartCoroutine(LoadSceneAsync(action));
                    break;

                case UINavigationActionKind.UnloadScene:
                    if (!TryGetLoadedScene(action, out Scene unloadScene))
                        break;

                    if (SceneManager.sceneCount == 1)
                    {
                        Debug.LogWarning(
                            $"[UINavigation] 마지막으로 로드된 Scene '{action.SceneName}'은 언로드할 수 없습니다.");
                        break;
                    }

                    SceneManager.UnloadSceneAsync(unloadScene);
                    break;

                case UINavigationActionKind.SetActiveScene:
                    if (TryGetLoadedScene(action, out Scene activeScene) &&
                        !SceneManager.SetActiveScene(activeScene))
                    {
                        Debug.LogWarning(
                            $"[UINavigation] Scene '{action.SceneName}'을 Active Scene으로 지정하지 못했습니다.");
                    }
                    break;

                case UINavigationActionKind.DebugLog:
                    switch (action.DebugLogType)
                    {
                        case UINavigationDebugLogType.Warning:
                            Debug.LogWarning(action.DebugMessage, this);
                            break;

                        case UINavigationDebugLogType.Error:
                            Debug.LogError(action.DebugMessage, this);
                            break;

                        default:
                            Debug.Log(action.DebugMessage, this);
                            break;
                    }
                    break;
            }
        }

        private IEnumerator LoadSceneAsync(UINavigationAction action)
        {
            AsyncOperation operation;
            if (action.SceneReferenceKind == UINavigationSceneReferenceKind.BuildIndex)
            {
                if (action.SceneBuildIndex < 0 ||
                    action.SceneBuildIndex >= SceneManager.sceneCountInBuildSettings)
                {
                    Debug.LogWarning(
                        $"[UINavigation] Build Index가 유효하지 않습니다: {action.SceneBuildIndex}.");
                    yield break;
                }

                operation = SceneManager.LoadSceneAsync(
                    action.SceneBuildIndex,
                    action.LoadSceneMode);
            }
            else
            {
                if (!ValidateSceneName(action))
                    yield break;

                operation = SceneManager.LoadSceneAsync(
                    action.SceneName,
                    action.LoadSceneMode);
            }

            if (operation == null)
                yield break;

            bool delayActivation = action.SceneActivationDelay > 0f;
            operation.allowSceneActivation =
                action.AllowSceneActivation && !delayActivation;

            if (!action.AllowSceneActivation)
            {
                _pendingSceneActivation = operation;
                yield break;
            }

            if (delayActivation)
            {
                while (operation.progress < 0.9f)
                    yield return null;

                yield return new WaitForSecondsRealtime(action.SceneActivationDelay);
                operation.allowSceneActivation = true;
            }

            while (!operation.isDone)
                yield return null;
        }

        private static bool ValidateSceneName(UINavigationAction action)
        {
            if (!string.IsNullOrWhiteSpace(action.SceneName))
                return true;

            Debug.LogWarning($"[UINavigation] {action.Kind} 액션의 Scene Name이 비어 있습니다.");
            return false;
        }

        private static bool TryGetLoadedScene(UINavigationAction action, out Scene scene)
        {
            scene = default;
            if (!ValidateSceneName(action))
                return false;

            scene = SceneManager.GetSceneByName(action.SceneName);
            if (!scene.IsValid())
                scene = SceneManager.GetSceneByPath(action.SceneName);

            if (scene.IsValid() && scene.isLoaded)
                return true;

            Debug.LogWarning(
                $"[UINavigation] 로드된 Scene을 찾지 못했습니다: '{action.SceneName}'.");
            return false;
        }
    }
}
