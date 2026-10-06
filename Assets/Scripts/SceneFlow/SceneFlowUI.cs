using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Loongdum.SceneFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument), typeof(GameSceneManager))]
    public sealed class SceneFlowUI : MonoBehaviour
    {
        private SceneFlowSettings settings;
        private GameSceneManager manager;
        private VisualElement root;
        private VisualElement selectionView;
        private VisualElement playingView;
        private VisualElement loadingView;
        private VisualElement completionView;
        private Label playingTitle;
        private Label completionTitle;
        private Label errorBanner;
        private ProgressBar loadingProgress;
        private Button returnButton;
        private Button completedReturnButton;
        private Button nextButton;
        private Font systemFont;
        private Button firstLevelButton;
        private SceneFlowState? renderedState;

        internal void Configure(SceneFlowSettings flowSettings) => settings = flowSettings;

        private void OnEnable()
        {
            manager = GetComponent<GameSceneManager>();
            manager.Changed += Render;
            BindDocument();
        }

        private void Start()
        {
            // UIDocument can finish enabling after this component's OnEnable.
            if (root == null) BindDocument();
        }

        private void BindDocument()
        {
            if (root != null || settings == null) return;
            root = GetComponent<UIDocument>().rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            selectionView = root.Q("selection-view");
            playingView = root.Q("playing-view");
            loadingView = root.Q("loading-view");
            completionView = root.Q("completion-view");
            playingTitle = root.Q<Label>("playing-title");
            completionTitle = root.Q<Label>("completion-title");
            errorBanner = root.Q<Label>("error-banner");
            loadingProgress = root.Q<ProgressBar>("loading-progress");
            returnButton = root.Q<Button>("return-button");
            completedReturnButton = root.Q<Button>("completed-return-button");
            nextButton = root.Q<Button>("next-button");

            if (selectionView == null || playingView == null || loadingView == null ||
                completionView == null || playingTitle == null || completionTitle == null ||
                errorBanner == null || loadingProgress == null || returnButton == null ||
                completedReturnButton == null || nextButton == null)
            {
                Debug.LogError("Scene Flow UXML is missing a required named element.", this);
                return;
            }

            // The HUD's transparent area must not consume gameplay pointer input.
            playingView.pickingMode = PickingMode.Ignore;
            root.Q("playing-heading").pickingMode = PickingMode.Ignore;
            playingTitle.pickingMode = PickingMode.Ignore;
            root.Q<Label>("playing-objective").pickingMode = PickingMode.Ignore;

            ApplyFont();
            PopulateLevels();
            returnButton.clicked += ReturnToSelection;
            completedReturnButton.clicked += ReturnToSelection;
            nextButton.clicked += GoToNextLevel;
            Render();
        }

        private void ApplyFont()
        {
            if (settings.UIFont != null)
            {
                root.style.unityFontDefinition = FontDefinition.FromSDFFont(settings.UIFont);
                return;
            }

            var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.Ordinal);
            string[] preferred = { "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC" };
            foreach (string family in preferred)
            {
                if (!installed.Contains(family)) continue;
                systemFont = Font.CreateDynamicFontFromOSFont(family, 24);
                root.style.unityFontDefinition = FontDefinition.FromFont(systemFont);
                break;
            }
        }

        private void PopulateLevels()
        {
            ScrollView list = root.Q<ScrollView>("level-list");
            list.Clear();
            firstLevelButton = null;
            int count = settings.Catalog.Levels.Count;
            root.Q<Label>("level-count").text = count + " 个关卡";

            for (int i = 0; i < count; i++)
            {
                LevelEntry level = settings.Catalog.Levels[i];
                if (level == null) continue;
                var card = new VisualElement();
                card.AddToClassList("level-card");
                var number = new Label((i + 1).ToString("00"));
                number.AddToClassList("level-number");
                card.Add(number);

                var content = new VisualElement();
                content.AddToClassList("level-content");
                var title = new Label(level.Title);
                title.AddToClassList("level-title");
                var description = new Label(level.Description);
                description.AddToClassList("level-description");
                content.Add(title);
                content.Add(description);
                card.Add(content);

                string id = level.Id;
                bool available = manager.CanLoad(level);
                var button = new Button(() => manager.LoadLevel(id))
                {
                    name = "play-" + id,
                    text = available ? "进入关卡  →" : "暂不可进入"
                };
                button.AddToClassList("primary-button");
                button.SetEnabled(available);
                card.Add(button);
                list.Add(card);
                if (firstLevelButton == null && available) firstLevelButton = button;
            }

            if (count == 0)
            {
                var empty = new Label("新的归途，即将开启。");
                empty.AddToClassList("empty-label");
                list.Add(empty);
            }
        }

        private void Render()
        {
            if (root == null || selectionView == null || manager.Catalog == null) return;

            SetVisible(selectionView, manager.State == SceneFlowState.LevelSelection);
            SetVisible(playingView, manager.State == SceneFlowState.Playing);
            SetVisible(loadingView, manager.State == SceneFlowState.Loading);
            SetVisible(completionView, manager.State == SceneFlowState.Completed);
            SetVisible(errorBanner, !string.IsNullOrEmpty(manager.LastError));
            errorBanner.text = manager.LastError ?? string.Empty;

            string title = manager.CurrentLevel != null ? manager.CurrentLevel.Title : string.Empty;
            playingTitle.text = title;
            completionTitle.text = title;
            loadingProgress.value = manager.LoadingProgress * 100f;
            loadingProgress.title = Mathf.RoundToInt(manager.LoadingProgress * 100f) + "%";
            SetVisible(nextButton, settings.Catalog.GetNext(manager.CurrentLevel) != null);

            if (renderedState != manager.State)
            {
                renderedState = manager.State;
                if (manager.State == SceneFlowState.LevelSelection && firstLevelButton != null)
                    firstLevelButton.schedule.Execute(() => firstLevelButton.Focus());
                else if (manager.State == SceneFlowState.Completed)
                    completedReturnButton.schedule.Execute(() => completedReturnButton.Focus());
                else
                    root.focusController?.focusedElement?.Blur();
            }
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            root.EnableInClassList("compact", evt.newRect.width < 900f);
        }

        private void ReturnToSelection() => manager.ReturnToSelection();
        private void GoToNextLevel() => manager.LoadNextLevel();

        private void OnDisable()
        {
            if (manager != null) manager.Changed -= Render;
            if (root != null) root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            if (returnButton != null) returnButton.clicked -= ReturnToSelection;
            if (completedReturnButton != null) completedReturnButton.clicked -= ReturnToSelection;
            if (nextButton != null) nextButton.clicked -= GoToNextLevel;
            root = null;
            renderedState = null;
            if (systemFont != null) Destroy(systemFont);
            systemFont = null;
        }
    }
}
