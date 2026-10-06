using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Loongdum.Levels.Editor
{
    public static class LevelTwoValidation
    {
        public const string Solution = "NNNNNNNNDDDDEDDSSWWWWSSDD";
        private static readonly List<string> checks = new List<string>();
        private static string[] Rows => JsonUtility.FromJson<LevelTwoLayout>(
            File.ReadAllText(LevelTwoSceneBuilder.Root + "/Level02Layout.json")).rows;

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("FAIL: " + description);
            checks.Add("PASS: " + description);
        }

        public static Vector2Int Direction(char command) => command == 'N' ? Vector2Int.down
            : command == 'S' ? Vector2Int.up : command == 'W' ? Vector2Int.left : Vector2Int.right;

        private static LevelTwoModel Follow(string path)
        {
            var model = new LevelTwoModel(Rows);
            foreach (char command in path)
            {
                bool ok = command == 'E' ? model.TryInteract() : model.TryMove(Direction(command));
                if (!ok) throw new InvalidOperationException("Route failed: " + path + " at " + model.Lower);
            }
            return model;
        }

        [MenuItem("Tools/Loongdum/Level 02/Validate Rules and Scene")]
        public static void Run()
        {
            checks.Clear();
            var source = Regex.Matches(File.ReadAllText("Docs/Level02/level-diagram-source.html"), "\"([#.GLUA1aXYpP]{11})\"")
                .Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
            Check(Rows.SequenceEqual(source), "All 121 tiles match the attached second-level diagram.");
            var model = new LevelTwoModel(Rows);
            Check(model.Lower == new Vector2Int(1, 9) && model.Upper == new Vector2Int(5, 5), "Correct start and fixed upper body.");
            Check(!model.IsOpen('a') && !model.IsOpen('X') && model.IsOpen('Y'), "Initial a/X closed, Y open.");
            Check(!model.TryInteract() && !model.CanReachLeverFrom(model.Upper), "Neither remote lower body nor fixed upper body can operate A.");
            Check(!model.TryMove(new Vector2Int(1, -1)), "Diagonal movement rejected.");
            Check(!model.TryMove(Vector2Int.down * 2), "Multi-cell jumps rejected.");
            Check(model.IsLit(new Vector2Int(5, 4)) && model.IsLit(model.Lever), "Glass transmits light to the distant lever.");
            Check(!model.IsLit(new Vector2Int(5, 1)), "Original beam stops after three cells.");
            Check(!model.IsLit(model.PortalEntry) && !model.IsLit(new Vector2Int(3, 3)), "Closed shutter prevents remote light at start.");
            Check(model.IsFelt(new Vector2Int(1, 8)) && model.IsFelt(new Vector2Int(0, 9))
                && !model.IsVisible(new Vector2Int(2, 8)), "Touch reveals four adjacent cells, never diagonal cells.");
            Check(model.BlocksMovement(new Vector2Int(5, 4)) && model.BlocksMovement(model.Lever)
                && model.BlocksMovement(model.PortalEntry) && model.BlocksMovement(model.PortalExit), "Glass, lever, and mirror frames block shortcut movement.");
            model = Follow("NNNNNNNNDDD");
            Check(model.CanReachLever && !model.TryInteract(), "Lever is reachable diagonally; an occupied door cannot close on the player.");
            model = Follow("NNNNNNNNDDDD");
            Check(!model.TryMove(Vector2Int.right), "Closed X prevents crossing before interaction.");
            Check(!model.TryMove(Vector2Int.up), "Lever pedestal prevents the central shortcut.");
            Check(model.TryInteract() && model.IsOpen('X') && !model.IsOpen('Y'), "A opens X and closes Y in the same action.");
            Check(!model.TryMove(Vector2Int.left), "Y blocks return after the throw.");
            Check(model.TryInteract() && !model.IsOpen('X') && model.IsOpen('Y'), "Double throw can be reversed, without latching both doors open.");
            model = Follow("NNNNNNNNDDDDEDDSS");
            Check(model.PlatePressed && model.IsOpen('a'), "Standing on 1 immediately opens a.");
            Check(model.IsLit(model.PortalEntry) && model.IsLit(model.PortalExit), "Light enters p and emerges from P.");
            Check(Enumerable.Range(3, 3).All(r => model.IsLit(new Vector2Int(3, r))), "Portal preserves southward direction and grants three fresh cells of light.");
            Check(!model.IsLit(new Vector2Int(3, 6)) && !model.IsLit(new Vector2Int(1, 3)), "Portal does not grant extra radius or an omnidirectional exit light.");
            Check(model.TryMove(Vector2Int.left) && !model.PlatePressed && !model.IsOpen('a')
                && !model.IsLit(new Vector2Int(3, 3)), "Leaving the plate immediately removes the temporary remote route.");
            model = Follow(Solution);
            Check(model.Won && model.MoveCount == 24, "Intended route wins after 24 moves and one lever interaction.");
            Check(!model.TryMove(Vector2Int.left) && !model.TryInteract(), "Completion freezes movement and mechanisms.");
            model.Reset();
            Check(!model.Won && !model.LeverThrown && model.MoveCount == 0 && !model.PlatePressed
                && model.Lower == new Vector2Int(1, 9), "Restart clears all puzzle state.");
            Check(FindShortest(false) == null, "No solution exists without the lever.");
            string shortest = FindShortest(true);
            Check(shortest != null && shortest.Count(c => c != 'E') == 24 && shortest.Count(c => c == 'E') == 1,
                "Exhaustive state search confirms the shortest solution.");

            if (!Application.isPlaying && EditorSceneManager.GetActiveScene().path != LevelTwoSceneBuilder.ScenePath)
                EditorSceneManager.OpenScene(LevelTwoSceneBuilder.ScenePath);
            var controller = UnityEngine.Object.FindFirstObjectByType<LevelTwoController>();
            Check(controller != null && controller.Cells.Length == 121, "Scene contains all 121 cells and its controller.");
            Check(controller.UpperVision != null
                && new SerializedObject(controller.UpperVision).FindProperty("visionRadius").floatValue == 10f
                && controller.UpperVision.transform.position == LevelTwoController.CellPosition(model.Upper) + Vector3.up * .5f,
                "The upper body uses a fixed radius-10 world-space vision source.");
            int obstacleLayer = LayerMask.NameToLayer("VisionObstacle");
            Check(obstacleLayer >= 0 && Enumerable.Range(0, 121).All(index =>
                controller.Cells[index].root.layer == ("#AaXY".IndexOf(model.Tile(
                    new Vector2Int(index % 11, index / 11))) >= 0 ? obstacleLayer : 0)),
                "Walls, lever, and doors occlude vision; glass and mirrors transmit it.");
            controller.Initialize();
            CheckScenePresentation(controller);
            foreach (char command in "NNNNNNNNDDDDEDDSS")
                if (command == 'E') controller.Interact(); else controller.Move(Direction(command));
            CheckScenePresentation(controller);
            Check(!controller.Cells[6 * 11 + 5].barrier.activeSelf && !controller.Cells[6 * 11 + 5].collider.enabled,
                "Pressure opens both the visible shutter and its collision.");
            controller.Move(Vector2Int.left);
            Check(controller.Cells[6 * 11 + 5].barrier.activeSelf && controller.Cells[6 * 11 + 5].collider.enabled,
                "Release restores shutter rendering and collision.");
            controller.Restart();
            CheckScenePresentation(controller);
            Check(EditorBuildSettings.scenes.Any(scene => scene.path == LevelTwoSceneBuilder.ScenePath && scene.enabled),
                "Second-level scene registered in the build list.");
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/Level02-Validation.txt", checks);
            Debug.Log("LEVEL02_VALIDATION_PASS " + checks.Count + " checks");
        }

        public static void CheckScenePresentation(LevelTwoController controller)
        {
            bool visibilityMatches = true, collisionMatches = true;
            for (int r = 0; r < 11; r++)
                for (int c = 0; c < 11; c++)
                {
                    var cell = new Vector2Int(c, r);
                    var visual = controller.Cells[r * 11 + c];
                    visibilityMatches &= visual.renderers.All(renderer => renderer.enabled
                        == (controller.UpperVision != null || controller.Model.IsVisible(cell)));
                    if (visual.collider != null)
                        collisionMatches &= visual.collider.enabled == controller.Model.BlocksMovement(cell);
                }
            Check(visibilityMatches, "Tile meshes remain available for the continuous vision mask.");
            Check(collisionMatches, "Colliders match movement blocking, including hidden obstacles.");
            Check(controller.LowerBody.position == LevelTwoController.CellPosition(controller.Model.Lower),
                "Lower-body transform matches its grid position.");
        }

        private static string FindShortest(bool allowInteraction)
        {
            var queue = new Queue<string>();
            var seen = new HashSet<string>();
            queue.Enqueue("");
            while (queue.Count > 0)
            {
                string path = queue.Dequeue();
                var model = Follow(path);
                string state = model.Lower + ":" + model.LeverThrown;
                if (!seen.Add(state)) continue;
                if (model.Won) return path;
                foreach (char action in allowInteraction ? "NSWDE" : "NSWD")
                {
                    var next = Follow(path);
                    if (action == 'E' ? next.TryInteract() : next.TryMove(Direction(action))) queue.Enqueue(path + action);
                }
            }
            return null;
        }
    }

    /// <summary>Batch Play-mode check with actual Input System keyboard events and screenshots.</summary>
    [InitializeOnLoad]
    public static class LevelTwoPlayValidation
    {
        private const string Pending = "LevelTwo.PlayValidation";
        private static Keyboard keyboard;
        private static LevelTwoController controller;
        private static int step, stage, lastFrame;
        private static bool hadRuntimeError;
        private static double started;
        private static InputSettings originalInputSettings;
        private static InputSettings testInputSettings;
        private static readonly List<string> results = new List<string>();

        static LevelTwoPlayValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayState;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(LevelTwoSceneBuilder.ScenePath);
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            controller = UnityEngine.Object.FindFirstObjectByType<LevelTwoController>();
            originalInputSettings = InputSystem.settings;
            testInputSettings = UnityEngine.Object.Instantiate(originalInputSettings);
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            step = 0;
            stage = 0;
            lastFrame = Time.frameCount;
            started = EditorApplication.timeSinceStartup;
            hadRuntimeError = false;
            results.Clear();
            Application.logMessageReceived += OnLog;
            Directory.CreateDirectory("Captures/Level02");
            EditorApplication.update += Tick;
        }

        private static void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && message.StartsWith("ArgumentOutOfRangeException", StringComparison.Ordinal)
                && trace.Contains("UnityEditor.Search.SearchDatabase"))
            {
                results.Add("EDITOR ISSUE (outside gameplay): Unity 6000.6 SearchDatabase startup indexing threw ArgumentOutOfRangeException.");
                return;
            }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) hadRuntimeError = true;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 180) { Finish(false, "Play validation timed out."); return; }
            if (Time.frameCount < lastFrame + 1) return;
            lastFrame = Time.frameCount;
            try
            {
                if (stage == 0)
                {
                    LevelTwoValidation.CheckScenePresentation(controller);
                    Capture("01-start");
                    if (Mathf.Abs(Shader.GetGlobalVector("_VisionSourcePositionRadius").w - 10f) > .001f
                        || Shader.GetGlobalFloat("_VisionMaskEnabled") < .5f)
                        throw new Exception("The rendered player view is not using the radius-10 vision mask.");
                    results.Add("PASS: Rendered player camera uses the radius-10 vision mask.");
                    stage = 1;
                    return;
                }
                if (stage == 1)
                {
                    if (step >= LevelTwoValidation.Solution.Length)
                    {
                        if (!controller.Model.Won || controller.Model.MoveCount != 24)
                            throw new Exception("Keyboard route did not finish in 24 moves: " + controller.Model.Lower + ", moves=" + controller.Model.MoveCount);
                        Capture("04-complete");
                        results.Add("PASS: Keyboard input completed the level in 24 moves plus E.");
                        stage = 3;
                        return;
                    }
                    char action = LevelTwoValidation.Solution[step];
                    Key key = action == 'N' ? Key.W : action == 'S' ? Key.DownArrow
                        : action == 'W' ? Key.A : action == 'D' ? Key.RightArrow : Key.E;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    stage = 2;
                    return;
                }
                if (stage == 2)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    step++;
                    results.Add("INPUT " + step + " " + LevelTwoValidation.Solution[step - 1] + " => " + controller.Model.Lower
                        + ", moves=" + controller.Model.MoveCount + ", lever=" + controller.Model.LeverThrown);
                    LevelTwoValidation.CheckScenePresentation(controller);
                    if (controller.Model.PlatePressed)
                    {
                        Capture("02-plate-held");
                        results.Add("PASS: Keyboard reaches pressure plate and reveals the portal route.");
                    }
                    if (step == 18) Capture("03-plate-released");
                    stage = 1;
                    return;
                }
                if (stage == 3)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    stage = 4;
                    return;
                }
                if (stage == 4)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (controller.Model.Won || controller.Model.MoveCount != 0 || controller.Model.LeverThrown)
                        throw new Exception("R failed to reset puzzle state.");
                    results.Add("PASS: R resets after completion.");
                    stage = 5;
                    return;
                }
                if (stage == 5)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F1));
                    stage = 6;
                    return;
                }
                if (stage == 6)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    if (!controller.Cells.All(cell => cell.renderers.All(renderer => renderer.enabled)))
                        throw new Exception("F1 did not reveal the complete designer map.");
                    Capture("05-designer-map");
                    results.Add("PASS: F1 toggles the editor design view.");
                }
                Finish(!hadRuntimeError, hadRuntimeError ? "Runtime logged an error." : "LEVEL02_PLAY_PASS");
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        private static void Finish(bool passed, string message)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (testInputSettings != null) UnityEngine.Object.DestroyImmediate(testInputSettings);
            results.Add(message);
            File.WriteAllLines("Logs/Level02-PlayValidation.txt", results);
            Debug.Log(message);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Capture(string name)
        {
            // Batch mode has no visible Game view. Explicitly render the real runtime camera;
            // these captures show the world and do not include the IMGUI overlay.
            var camera = controller.LevelCamera;
            var target = RenderTexture.GetTemporary(1280, 800, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                image.Apply();
                File.WriteAllBytes("Captures/Level02/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
