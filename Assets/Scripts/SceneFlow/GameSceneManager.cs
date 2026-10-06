using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
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

        public static GameSceneManager Instance { get; private set; }
        public LevelCatalog Catalog => catalog;
        public LevelEntry CurrentLevel { get; private set; }
        public LevelGoal ActiveGoal { get; private set; }
        public SceneFlowState State { get; private set; }
        public float LoadingProgress { get; private set; }
        public string LastError { get; private set; }
        public bool IsTransitioning => transitioning;
        public event Action Changed;

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

            if (!settings.Catalog.Validate(out string error) || settings.PanelSettings == null ||
                settings.UIDocument == null)
            {
                Debug.LogError("Scene Flow settings are incomplete. " + error, settings);
                return;
            }

            GameSceneManager existing = FindAnyObjectByType<GameSceneManager>();
            if (existing != null)
            {
                Instance = existing;
                existing.Initialize(settings.Catalog);
                return;
            }

            var root = new GameObject("Scene Flow");
            root.SetActive(false);
            GameSceneManager manager = root.AddComponent<GameSceneManager>();
            var document = root.AddComponent<UIDocument>();
            document.panelSettings = settings.PanelSettings;
            document.visualTreeAsset = settings.UIDocument;
            document.sortingOrder = 100;
            root.AddComponent<SceneFlowUI>().Configure(settings);
            root.SetActive(true);
            DontDestroyOnLoad(root);
            manager.Initialize(settings.Catalog);
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

        private void Initialize(LevelCatalog levelCatalog)
        {
            StopAllCoroutines();
            catalog = levelCatalog;
            transitioning = false;
            LastError = null;
            BindScene(UnitySceneManager.GetActiveScene());
            EnterReadyState();
        }

        public bool CanLoad(LevelEntry level)
        {
            return level != null && !string.IsNullOrWhiteSpace(level.ScenePath) &&
                Application.CanStreamedLevelBeLoaded(level.ScenePath);
        }

        public bool LoadLevel(string levelId)
        {
            if (transitioning) return false;
            LevelEntry level = catalog.FindById(levelId);
            if (level == null) return RejectLoad("未找到这个关卡。", "Unknown level ID: " + levelId);
            return BeginLoad(level.ScenePath);
        }

        public bool ReturnToSelection()
        {
            if (transitioning || State == SceneFlowState.LevelSelection) return false;
            return BeginLoad(catalog.SelectionScenePath);
        }

        public bool LoadNextLevel()
        {
            if (State != SceneFlowState.Completed || transitioning) return false;
            LevelEntry next = catalog.GetNext(CurrentLevel);
            return next != null && LoadLevel(next.Id);
        }

        private bool BeginLoad(string path)
        {
            if (!Application.CanStreamedLevelBeLoaded(path))
                return RejectLoad("暂时无法进入，请重新选择关卡。", "Scene is not enabled in the build: " + path);

            StartCoroutine(LoadScene(path));
            return true;
        }

        private bool RejectLoad(string message, string diagnostic)
        {
            LastError = message;
            Debug.LogWarning("[Scene Flow] " + diagnostic, this);
            Changed?.Invoke();
            return false;
        }

        private IEnumerator LoadScene(string path)
        {
            SceneFlowState previousState = State;
            transitioning = true;
            State = SceneFlowState.Loading;
            LoadingProgress = 0f;
            LastError = null;
            if (ActiveGoal != null) ActiveGoal.SetControlsEnabled(false);
            Changed?.Invoke();

            // Allow the loading panel to render even when the next scene is very small.
            yield return null;

            AsyncOperation operation = null;
            string error = null;
            try
            {
                operation = UnitySceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                error = exception.Message;
            }

            if (operation == null)
            {
                transitioning = false;
                State = previousState;
                if (ActiveGoal != null) ActiveGoal.SetControlsEnabled(State == SceneFlowState.Playing);
                RejectLoad("加载未完成，请重新选择关卡。", error ?? "LoadSceneAsync returned no operation.");
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

            // sceneLoaded binds references; input becomes available only after scene Start callbacks.
            yield return null;
            transitioning = false;
            LoadingProgress = 1f;
            EnterReadyState();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (catalog == null || mode != LoadSceneMode.Single) return;
            BindScene(scene);
            if (!transitioning) EnterReadyState();
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
            State = UnitySceneManager.GetActiveScene().path == catalog.SelectionScenePath
                ? SceneFlowState.LevelSelection : SceneFlowState.Playing;
            if (ActiveGoal != null) ActiveGoal.SetControlsEnabled(true);
            Changed?.Invoke();
        }

        private void OnGoalCompleted(LevelGoal goal)
        {
            if (transitioning || State != SceneFlowState.Playing || goal != ActiveGoal ||
                goal.gameObject.scene != UnitySceneManager.GetActiveScene()) return;
            State = SceneFlowState.Completed;
            Changed?.Invoke();
        }
    }
}
