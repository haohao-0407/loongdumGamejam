using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Loongdum.SceneFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace Loongdum.EditorTools
{
    public static class SceneFlowSetup
    {
        public const string SelectionScenePath = "Assets/Scenes/LevelSelect.unity";
        public const string WhiteboxScenePath = "Assets/Scenes/Whitebox1.unity";
        public const string CatalogPath = "Assets/SceneFlow/LevelCatalog.asset";
        public const string SettingsPath = "Assets/Resources/SceneFlow/SceneFlowSettings.asset";

        [MenuItem("Tools/Loongdum/Scene Flow/Set Up Whitebox Selection")]
        public static void SetUp()
        {
            RequireEditableScenes();
            Scene original = SceneManager.GetActiveScene();
            Scene whitebox = SceneManager.GetSceneByPath(WhiteboxScenePath);
            bool openedWhitebox = !whitebox.IsValid() || !whitebox.isLoaded;
            if (openedWhitebox) whitebox = EditorSceneManager.OpenScene(WhiteboxScenePath, OpenSceneMode.Additive);

            try
            {
                var players = FindInScene<WhiteboxPlayerMovement>(whitebox);
                var sources = FindInScene<VisionSource>(whitebox);
                var goals = FindInScene<LevelGoal>(whitebox);
                if (players.Length != 1 || sources.Length != 1 || goals.Length > 1)
                    throw new InvalidOperationException("Whitebox setup requires one player, one VisionSource and at most one LevelGoal.");

                Directory.CreateDirectory("Assets/SceneFlow");
                Directory.CreateDirectory("Assets/Resources/SceneFlow");
                AssetDatabase.Refresh();

                LevelCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
                if (catalog == null)
                {
                    catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                    catalog.SetInitialConfiguration(SelectionScenePath,
                        new LevelEntry("whitebox-1", "白盒关卡", "循着视线，让彼此重新汇合。", WhiteboxScenePath));
                    AssetDatabase.CreateAsset(catalog, CatalogPath);
                }

                SceneFlowSettings settings = AssetDatabase.LoadAssetAtPath<SceneFlowSettings>(SettingsPath);
                if (settings == null)
                {
                    settings = ScriptableObject.CreateInstance<SceneFlowSettings>();
                    InputActionReference cancel = AssetDatabase.LoadAllAssetsAtPath("Assets/InputSystem_Actions.inputactions")
                        .OfType<InputActionReference>().FirstOrDefault(reference => reference.action != null &&
                            reference.action.actionMap.name == "UI" && reference.action.name == "Cancel");
                    settings.Configure(catalog, cancel);
                    AssetDatabase.CreateAsset(settings, SettingsPath);
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SelectionScenePath) == null)
                    CreateSelectionScene();

                if (goals.Length == 0)
                {
                    var goalObject = new GameObject("Level Goal");
                    SceneManager.MoveGameObjectToScene(goalObject, whitebox);
                    LevelGoal goal = goalObject.AddComponent<LevelGoal>();
                    var controls = new List<Behaviour> { players[0] };
                    BodyReversal reversal = players[0].GetComponent<BodyReversal>();
                    if (reversal != null) controls.Add(reversal);
                    goal.Configure(players[0].transform, sources[0], controls.ToArray());
                    EditorSceneManager.MarkSceneDirty(whitebox);
                    if (!EditorSceneManager.SaveScene(whitebox))
                        throw new InvalidOperationException("Could not save Whitebox LevelGoal references.");
                }

                SyncBuildScenes(catalog);
                AssetDatabase.SaveAssets();
                Debug.Log("[Scene Flow] Whitebox selection is ready. Open Assets/Scenes/LevelSelect.unity and enter Play.");
            }
            finally
            {
                if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
                if (openedWhitebox && whitebox.IsValid() && whitebox.isLoaded)
                    EditorSceneManager.CloseScene(whitebox, true);
            }
        }

        public static void SyncBuildScenes(LevelCatalog catalog)
        {
            if (!catalog.Validate(out string error)) throw new InvalidOperationException(error);
            var result = new List<EditorBuildSettingsScene>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            result.Add(new EditorBuildSettingsScene(catalog.SelectionScenePath, true));
            paths.Add(catalog.SelectionScenePath);

            // Preserve existing scenes and their relative order; the selection scene is the entry point.
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (paths.Add(scene.path)) result.Add(new EditorBuildSettingsScene(scene.path, scene.enabled));

            foreach (LevelEntry level in catalog.Levels)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(level.ScenePath) == null)
                    throw new InvalidOperationException("Level scene does not exist: " + level.ScenePath);
                EditorBuildSettingsScene existing = result.Find(scene => scene.path == level.ScenePath);
                if (existing != null) existing.enabled = true;
                else if (paths.Add(level.ScenePath)) result.Add(new EditorBuildSettingsScene(level.ScenePath, true));
            }

            EditorBuildSettings.scenes = result.ToArray();
        }

        private static void CreateSelectionScene()
        {
            Scene menu = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var cameraObject = new GameObject("Main Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, menu);
                cameraObject.tag = "MainCamera";
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(25 / 255f, 36 / 255f, 31 / 255f);
                cameraObject.AddComponent<AudioListener>();

                var lightObject = new GameObject("Directional Light");
                SceneManager.MoveGameObjectToScene(lightObject, menu);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1f;
                lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);

                if (!EditorSceneManager.SaveScene(menu, SelectionScenePath))
                    throw new InvalidOperationException("Could not save the level selection scene.");
            }
            finally
            {
                EditorSceneManager.CloseScene(menu, true);
            }
        }

        private static T[] FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        }

        private static void RequireEditableScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before configuring Scene Flow.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save scene changes before configuring Scene Flow.");
        }
    }
}
