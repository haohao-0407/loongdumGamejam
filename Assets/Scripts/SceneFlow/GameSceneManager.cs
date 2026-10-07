using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Loongdum.SceneFlow
{
    public enum SceneFlowState
    {
        LevelSelection,
        Loading,
        Playing,
        Completed
    }

    /// <summary>Owns selection, scene transitions and the current level's completion result.</summary>
    [DisallowMultipleComponent]
    public sealed class GameSceneManager : MonoBehaviour
    {
        private LevelCatalog catalog;
        private bool transitioning;
        private bool returnOnCompletion;
        private Scene levelScene;
        private string loadingLevelPath;

        public static GameSceneManager Instance { get; private set; }
        public LevelCatalog Catalog => catalog;
        public LevelEntry CurrentLevel { get; private set; }
        public LevelGoal ActiveGoal { get; private set; }
        public SceneFlowState State { get; private set; }
        public float LoadingProgress { get; private set; }
        public string LastError { get; private set; }
        public bool IsTransitioning => transitioning;
        public event Action Changed;
        public event Action<LevelEntry> LevelEntered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneFlowSettings settings = Resources.Load<SceneFlowSettings>(SceneFlowSettings.ResourcePath);
            if (settings == null || settings.Catalog == null) return;

            string scenePath = UnitySceneManager.GetActiveScene().path;
            if (settings.Catalog.SelectionScenePath != scenePath &&
                settings.Catalog.FindByScenePath(scenePath) == null) return;

            if (!settings.Catalog.Validate(out string error))
            {
                Debug.LogError("Scene Flow settings are incomplete. " + error, settings);
                return;
            }

            GameSceneManager existing = FindAnyObjectByType<GameSceneManager>();
            if (existing != null)
            {
                Instance = existing;
                DontDestroyOnLoad(existing.gameObject);
                existing.Initialize(settings);
                return;
            }

            var root = new GameObject("Scene Flow");
            root.SetActive(false);
            GameSceneManager manager = root.AddComponent<GameSceneManager>();
            root.SetActive(true);
            DontDestroyOnLoad(root);
            manager.Initialize(settings);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            UnitySceneManager.sceneLoaded -= OnSceneLoaded;
            UnitySceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnitySceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindGoal();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Initialize(SceneFlowSettings settings)
        {
            StopAllCoroutines();
            catalog = settings.Catalog;
            returnOnCompletion = settings.ReturnOnCompletion;
            transitioning = false;
            LastError = null;
            loadingLevelPath = null;
            Scene activeScene = UnitySceneManager.GetActiveScene();
            levelScene = catalog.FindByScenePath(activeScene.path) != null ? activeScene : default;
            BindScene(activeScene);
            if (levelScene.IsValid()) StartCoroutine(PrepareDirectLevelEntry());
            else EnterReadyState();
        }

        public bool CanLoad(LevelEntry level)
        {
            return level != null && !string.IsNullOrWhiteSpace(level.ScenePath) &&
                Application.CanStreamedLevelBeLoaded(level.ScenePath);
        }

        public bool LoadLevel(string levelId)
        {
            if (transitioning || catalog == null) return false;
            LevelEntry level = catalog.FindById(levelId);
            if (level == null) return RejectLoad("未找到这个关卡。", "Unknown level ID: " + levelId);
            return BeginLoad(level);
        }

        public bool ReturnToSelection()
        {
            if (transitioning || State == SceneFlowState.LevelSelection) return false;
            return BeginLoad(null);
        }

        public bool LoadNextLevel()
        {
            if (State != SceneFlowState.Completed || transitioning) return false;
            LevelEntry next = catalog.GetNext(CurrentLevel);
            return next != null && LoadLevel(next.Id);
        }

        private bool BeginLoad(LevelEntry level)
        {
            if (level != null && !CanLoad(level))
                return RejectLoad("暂时无法进入，请重新选择关卡。", "Scene is not enabled in the build: " + level.ScenePath);
            Scene uiScene = UnitySceneManager.GetSceneByPath(catalog.SelectionScenePath);
            if (!uiScene.isLoaded && !Application.CanStreamedLevelBeLoaded(catalog.SelectionScenePath))
                return RejectLoad("无法打开选关界面。", "Selection scene is not enabled in the build.");

            StartCoroutine(LoadScene(level));
            return true;
        }

        private bool RejectLoad(string message, string diagnostic)
        {
            LastError = message;
            Debug.LogWarning("[Scene Flow] " + diagnostic, this);
            Changed?.Invoke();
            return false;
        }

        private void BeginTransition()
        {
            transitioning = true;
            State = SceneFlowState.Loading;
            LoadingProgress = 0f;
            LastError = null;
            if (ActiveGoal != null) ActiveGoal.SetControlsEnabled(false);
            Changed?.Invoke();
        }

        // Keep direct Play from a level usable in the editor: load the same UI
        // scene that a normal game starts with, without reloading the level.
        private IEnumerator PrepareDirectLevelEntry()
        {
            BeginTransition();
            yield return EnsureUIScene();
            if (!UnitySceneManager.GetSceneByPath(catalog.SelectionScenePath).isLoaded)
            {
                transitioning = false;
                EnterReadyState();
                yield break;
            }
            ConfigureLevelEventSystems();
            UnitySceneManager.SetActiveScene(levelScene);
            yield return null;
            FinishTransition();
        }

        private IEnumerator EnsureUIScene()
        {
            if (UnitySceneManager.GetSceneByPath(catalog.SelectionScenePath).isLoaded) yield break;
            AsyncOperation operation = StartAdditiveLoad(catalog.SelectionScenePath);
            if (operation != null) yield return operation;
        }

        private AsyncOperation StartAdditiveLoad(string path)
        {
            try
            {
                AsyncOperation operation = UnitySceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                if (operation == null) RejectLoad("加载未完成，请重新选择关卡。", "No load operation for: " + path);
                return operation;
            }
            catch (Exception exception)
            {
                RejectLoad("加载未完成，请重新选择关卡。", exception.Message);
                return null;
            }
        }

        private IEnumerator LoadScene(LevelEntry target)
        {
            SceneFlowState previousState = State;
            BeginTransition();
            yield return EnsureUIScene();
            Scene uiScene = UnitySceneManager.GetSceneByPath(catalog.SelectionScenePath);
            if (!uiScene.isLoaded)
            {
                RestoreAfterFailedTransition(previousState);
                yield break;
            }

            // Hide the menu camera before the next level's Awake/Start callbacks.
            yield return null;
            UnitySceneManager.SetActiveScene(uiScene);

            // Unload first: scene scripts use global searches for cameras and
            // VisionSources, so two playable levels must never coexist.
            if (levelScene.IsValid() && levelScene.isLoaded)
            {
                AsyncOperation unload = null;
                try { unload = UnitySceneManager.UnloadSceneAsync(levelScene); }
                catch (Exception exception) { RejectLoad("无法离开当前关卡。", exception.Message); }
                if (unload == null)
                {
                    UnitySceneManager.SetActiveScene(levelScene);
                    RestoreAfterFailedTransition(previousState);
                    yield break;
                }
                yield return unload;
            }
            UnbindGoal();
            levelScene = default;
            CurrentLevel = null;

            if (target != null)
            {
                loadingLevelPath = target.ScenePath;
                AsyncOperation operation = StartAdditiveLoad(target.ScenePath);
                if (operation == null)
                {
                    loadingLevelPath = null;
                    FinishTransition();
                    yield break;
                }
                while (!operation.isDone)
                {
                    float progress = Mathf.Clamp01(operation.progress / 0.9f);
                    if (!Mathf.Approximately(LoadingProgress, progress))
                    {
                        LoadingProgress = progress;
                        Changed?.Invoke();
                    }
                    yield return null;
                }
                loadingLevelPath = null;
            }

            // Scene Start callbacks finish before input and the entry Dialog.
            yield return null;
            FinishTransition();
        }

        private void RestoreAfterFailedTransition(SceneFlowState previousState)
        {
            transitioning = false;
            State = previousState;
            if (ActiveGoal != null) ActiveGoal.SetControlsEnabled(State == SceneFlowState.Playing);
            Changed?.Invoke();
        }

        private void FinishTransition()
        {
            transitioning = false;
            LoadingProgress = 1f;
            EnterReadyState();
            if (State == SceneFlowState.Playing) LevelEntered?.Invoke(CurrentLevel);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (catalog == null || scene.path != loadingLevelPath) return;
            levelScene = scene;
            // This callback precedes Start, including scene-local object spawning.
            UnitySceneManager.SetActiveScene(scene);
            ConfigureLevelEventSystems();
            BindScene(scene);
        }

        private void ConfigureLevelEventSystems()
        {
            Scene uiScene = UnitySceneManager.GetSceneByPath(catalog.SelectionScenePath);
            if (!uiScene.isLoaded || !levelScene.IsValid() || !levelScene.isLoaded) return;
            EventSystem shared = null;
            foreach (GameObject root in uiScene.GetRootGameObjects())
                foreach (EventSystem candidate in root.GetComponentsInChildren<EventSystem>(true))
                    if (candidate.isActiveAndEnabled) shared = candidate;
            if (shared == null) return;
            foreach (GameObject root in levelScene.GetRootGameObjects())
                foreach (EventSystem duplicate in root.GetComponentsInChildren<EventSystem>(true))
                {
                    foreach (BaseInputModule module in duplicate.GetComponents<BaseInputModule>())
                        module.enabled = false;
                    duplicate.enabled = false;
                }
            EventSystem.current = shared;
        }

        private void BindScene(Scene scene)
        {
            UnbindGoal();
            CurrentLevel = catalog.FindByScenePath(scene.path);
            if (scene.path == catalog.SelectionScenePath) return;

            var goals = new List<LevelGoal>();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (LevelGoal goal in root.GetComponentsInChildren<LevelGoal>(true))
                    if (goal.isActiveAndEnabled) goals.Add(goal);

            if (goals.Count != 1 || !goals[0].IsConfigured)
            {
                Debug.LogError("[Scene Flow] A playable scene must contain exactly one configured LevelGoal: " + scene.path);
                return;
            }

            ActiveGoal = goals[0];
            ActiveGoal.PrepareForRun();
            ActiveGoal.SetControlsEnabled(!transitioning);
            ActiveGoal.Completed += OnGoalCompleted;
        }

        private void UnbindGoal()
        {
            if (ActiveGoal != null) ActiveGoal.Completed -= OnGoalCompleted;
            ActiveGoal = null;
        }

        private void EnterReadyState()
        {
            State = levelScene.IsValid() && levelScene.isLoaded && CurrentLevel != null
                ? SceneFlowState.Playing : SceneFlowState.LevelSelection;
            if (ActiveGoal != null) ActiveGoal.SetControlsEnabled(true);
            Changed?.Invoke();
        }

        private void OnGoalCompleted(LevelGoal goal)
        {
            if (transitioning || State != SceneFlowState.Playing || goal != ActiveGoal ||
                goal.gameObject.scene != levelScene) return;
            State = SceneFlowState.Completed;
            Changed?.Invoke();
            if (returnOnCompletion) ReturnToSelection();
        }
    }
}
