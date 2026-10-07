using System;
using MineHeart.GamePlay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Loongdum.SceneFlow
{
    /// <summary>Connects the authored selection artwork to the scene catalog.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelSelectUI : MonoBehaviour
    {
        [Serializable]
        private sealed class LevelButton
        {
            public Button button;
            public string levelId;
        }

        [SerializeField] private LevelButton[] levels = Array.Empty<LevelButton>();
        [SerializeField] private CanvasGroup selectionGroup;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private GameObject titlePage;
        [SerializeField] private GameObject levelSelectionPage;
        [SerializeField] private Button startButton;
        [SerializeField] private UIStarBlackWhiteFlash startFlash;
        [SerializeField] private UIRightPanelFocus panelFocus;
        [SerializeField] private RectTransform settingsPage;
        [SerializeField] private RectTransform dialogPage;
        [SerializeField] private GameObject[] rightPages = Array.Empty<GameObject>();
        [Tooltip("Menu-only camera and lighting; disabled while a playable scene is loaded.")]
        [SerializeField] private GameObject[] menuEnvironment = Array.Empty<GameObject>();

        private static bool selectionOpened;
        private bool showingLevels;
        private bool wasReady;
        private bool dialogShownForCurrentEntry;
        private bool[] menuEnvironmentActive;
        private SceneFlowState? renderedState;
        private InputAction settingsAction;
        private GameSceneManager manager;
        private UnityEngine.Events.UnityAction[] clickHandlers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTitleState() => selectionOpened = false;

        private void Awake()
        {
            menuEnvironmentActive = new bool[menuEnvironment.Length];
            for (int i = 0; i < menuEnvironment.Length; i++)
                menuEnvironmentActive[i] = menuEnvironment[i] != null && menuEnvironment[i].activeSelf;
            SceneFlowSettings settings = Resources.Load<SceneFlowSettings>(SceneFlowSettings.ResourcePath);
            settingsAction = settings != null && settings.OpenSettingsAction != null
                ? settings.OpenSettingsAction.action?.Clone() : null;
            // Returning from a level goes straight to selection; a fresh game
            // starts on the authored Title page.
            showingLevels = selectionOpened || (GameSceneManager.Instance != null &&
                GameSceneManager.Instance.IsTransitioning);
            SetPageVisibility();
            if (dialogPage != null) dialogPage.gameObject.SetActive(false);
            if (startButton != null)
            {
                Image image = startButton.GetComponent<Image>();
                if (image != null && image.sprite != null && image.sprite.texture.isReadable)
                    image.alphaHitTestMinimumThreshold = 0.05f;
            }
            clickHandlers = new UnityEngine.Events.UnityAction[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                LevelButton entry = levels[i];
                if (entry.button == null) continue;

                // The artwork uses full-page transparent sprites. Rectangular hit
                // areas would allow the last button to cover all earlier levels.
                Image image = entry.button.GetComponent<Image>();
                if (image != null && image.sprite != null && image.sprite.texture.isReadable)
                    image.alphaHitTestMinimumThreshold = 0.05f;
                foreach (Graphic graphic in entry.button.GetComponentsInChildren<Graphic>(true))
                    if (graphic != image) graphic.raycastTarget = false;

                clickHandlers[i] = () => LoadLevel(entry.levelId);
            }
        }

        private void OnEnable()
        {
            settingsAction?.Enable();
            if (startButton != null) startButton.onClick.AddListener(BeginGame);
            if (startFlash != null) startFlash.Completed += ShowLevelSelection;
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].button != null && clickHandlers[i] != null)
                    levels[i].button.onClick.AddListener(clickHandlers[i]);
            if (uiManager != null) uiManager.SetCursorRequest(this, true);
            TryBindManager();
        }

        // The scene UI enables before the manager's AfterSceneLoad bootstrap.
        private void Start() => TryBindManager();

        private void Update()
        {
            if (manager != null && !manager.IsTransitioning && settingsAction != null &&
                settingsAction.WasPressedThisFrame() && panelFocus != null && settingsPage != null)
                panelFocus.Toggle(settingsPage);
        }

        private void TryBindManager()
        {
            if (manager != null) return;
            manager = GameSceneManager.Instance;
            if (manager == null) return;
            manager.Changed += Render;
            manager.LevelEntered += OnLevelEntered;
            Render();
        }

        private void FocusCurrentPage()
        {
            if (manager == null || manager.State != SceneFlowState.LevelSelection) return;
            if (!showingLevels && startButton != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(startButton.gameObject);
                return;
            }
            if (EventSystem.current != null)
                foreach (LevelButton entry in levels)
                    if (entry.button != null && entry.button.isActiveAndEnabled && entry.button.IsInteractable())
                    {
                        EventSystem.current.SetSelectedGameObject(entry.button.gameObject);
                        break;
                    }
        }

        private void BeginGame()
        {
            if (showingLevels) return;
            if (startFlash != null) startFlash.Play();
            else ShowLevelSelection();
        }

        public void ShowLevelSelection()
        {
            if (manager != null && manager.State != SceneFlowState.LevelSelection) return;
            showingLevels = true;
            selectionOpened = true;
            SetPageVisibility();
            if (manager != null) Render();
            FocusCurrentPage();
        }

        private void SetPageVisibility()
        {
            GameSceneManager current = manager != null ? manager : GameSceneManager.Instance;
            bool inMenu = current == null || current.State == SceneFlowState.LevelSelection;
            if (titlePage != null) titlePage.SetActive(inMenu && !showingLevels);
            if (levelSelectionPage != null) levelSelectionPage.SetActive(inMenu && showingLevels);
            foreach (GameObject page in rightPages)
                if (page != null) page.SetActive(true);
            SetMenuEnvironmentVisible(inMenu);
        }

        private void SetMenuEnvironmentVisible(bool visible)
        {
            if (menuEnvironmentActive == null) return;
            for (int i = 0; i < menuEnvironment.Length; i++)
                if (menuEnvironment[i] != null)
                    menuEnvironment[i].SetActive(visible && menuEnvironmentActive[i]);
        }

        private void OnLevelEntered(LevelEntry level)
        {
            showingLevels = true;
            selectionOpened = true;
            if (dialogPage == null || dialogShownForCurrentEntry) return;
            dialogShownForCurrentEntry = true;
            // Dialog's existing focus offset is its off-screen position. Replay
            // the reverse path to slide it in each time a level is entered.
            if (panelFocus != null) panelFocus.ReplayReturn(dialogPage, dismissAfterClick: true);
            else dialogPage.gameObject.SetActive(true);
        }

        public void LoadLevel(string levelId)
        {
            if (!showingLevels) return;
            TryBindManager();
            if (manager != null) manager.LoadLevel(levelId);
        }

        private void Render()
        {
            if (renderedState != manager.State)
            {
                if (manager.State == SceneFlowState.Loading) dialogShownForCurrentEntry = false;
                if (renderedState.HasValue)
                {
                    if (panelFocus != null) panelFocus.ResetPanels();
                    if (manager.State == SceneFlowState.LevelSelection) showingLevels = true;
                }
                if (dialogPage != null) dialogPage.gameObject.SetActive(false);
                if (manager.State != SceneFlowState.LevelSelection && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(null);
                renderedState = manager.State;
            }
            SetPageVisibility();
            bool ready = manager.State == SceneFlowState.LevelSelection && !manager.IsTransitioning;
            if (selectionGroup != null) selectionGroup.interactable = ready;
            if (startButton != null) startButton.interactable = ready && !showingLevels;
            foreach (LevelButton entry in levels)
                if (entry.button != null)
                    entry.button.interactable = ready && showingLevels &&
                        manager.CanLoad(manager.Catalog.FindById(entry.levelId));
            if (ready && !wasReady) FocusCurrentPage();
            wasReady = ready;
        }

        private void OnDisable()
        {
            settingsAction?.Disable();
            if (startButton != null) startButton.onClick.RemoveListener(BeginGame);
            if (startFlash != null) startFlash.Completed -= ShowLevelSelection;
            if (manager != null)
            {
                manager.Changed -= Render;
                manager.LevelEntered -= OnLevelEntered;
            }
            manager = null;
            wasReady = false;
            renderedState = null;
            SetMenuEnvironmentVisible(true);
            for (int i = 0; i < levels.Length; i++)
                if (levels[i].button != null && clickHandlers[i] != null)
                    levels[i].button.onClick.RemoveListener(clickHandlers[i]);
            if (uiManager != null) uiManager.SetCursorRequest(this, false);
        }

        private void OnDestroy() => settingsAction?.Dispose();
    }
}
