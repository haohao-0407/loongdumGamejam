using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Loongdum.Levels.Editor
{
    public static class LevelTwoSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Level02_UnreachableLever.unity";
        public const string Root = "Assets/Levels/Level02";

        [MenuItem("Tools/Loongdum/Level 02/Open Scene")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Tools/Loongdum/Level 02/Rebuild Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding.");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            // Keep the second level's fixed upper body on the first level's vision settings.
            EditorSceneManager.OpenScene("Assets/Scenes/Whitebox1.unity");
            var firstLevelVision = UnityEngine.Object.FindFirstObjectByType<VisionSource>();
            if (firstLevelVision == null) throw new InvalidOperationException("The first-level vision source is missing.");
            string visionSettings = EditorJsonUtility.ToJson(firstLevelVision);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Root + "/Materials");
            AssetDatabase.Refresh();
            var layout = AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "/Level02Layout.json");
            var model = new LevelTwoModel(JsonUtility.FromJson<LevelTwoLayout>(layout.text).rows);
            var dark = Material("Floor", new Color(.13f, .14f, .18f));
            var lit = Material("Light", new Color(.75f, .52f, .2f));
            var felt = Material("Touch", new Color(.32f, .41f, .5f));
            var wall = Material("Wall", new Color(.37f, .38f, .42f));
            var mirror = Material("Mirror", new Color(.7f, .82f, .88f));
            var lever = Material("Lever", new Color(.19f, .45f, .32f));
            var plate = Material("Plate", new Color(.22f, .65f, .71f));
            var shutter = Material("Shutter", new Color(.49f, .29f, .65f));
            var door = Material("Door", new Color(.57f, .3f, .17f));
            var upper = Material("UpperBody", new Color(1f, .77f, .34f));
            var lower = Material("LowerBody", new Color(.5f, .68f, .87f));
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Glass.mat");
            if (glass == null) throw new InvalidOperationException("The repository glass material is missing.");

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .028f, .04f);
            camera.orthographic = true;
            camera.orthographicSize = 12.8f;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 70f;
            camera.transform.position = new Vector3(0, 25, -11);
            camera.transform.LookAt(Vector3.zero);
            cameraObject.AddComponent<AudioListener>();
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;
            cameraData.requiresDepthTexture = true;

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.55f, .56f, .6f);
            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(55, -30, 0);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            // Keep the first-level vision mask as the sole visibility boundary.
            light.shadows = LightShadows.None;

            var root = new GameObject("Level 02 - Unreachable Lever");
            var controller = root.AddComponent<LevelTwoController>();
            var board = new GameObject("Board - rows north to south").transform;
            board.SetParent(root.transform, false);
            var cells = new LevelTwoController.CellVisual[model.Width * model.Height];
            for (int r = 0; r < model.Height; r++)
                for (int c = 0; c < model.Width; c++)
                {
                    var position = new Vector2Int(c, r);
                    char tile = model.Tile(position);
                    var cell = new GameObject($"Cell [{r},{c}] {tile}");
                    cell.transform.SetParent(board, false);
                    cell.transform.position = LevelTwoController.CellPosition(position);
                    var visual = new LevelTwoController.CellVisual { root = cell };
                    visual.floor = Shape("Floor", PrimitiveType.Cube, cell.transform,
                        new Vector3(0, -.09f, 0), new Vector3(1.52f, .16f, 1.52f), dark).GetComponent<Renderer>();
                    if (tile == '#')
                        Shape("Wall", PrimitiveType.Cube, cell.transform, new Vector3(0, .5f, 0), new Vector3(1.52f, 1f, 1.52f), wall);
                    if (tile == 'G')
                        Shape("Glass - blocks legs, transmits light", PrimitiveType.Cube, cell.transform,
                            new Vector3(0, .54f, 0), new Vector3(1.5f, 1.05f, .35f), glass);
                    if (tile == 'p' || tile == 'P')
                    {
                        Shape("Mirror plinth", PrimitiveType.Cube, cell.transform, new Vector3(0, .12f, 0), new Vector3(1.16f, .24f, .6f), wall);
                        Shape("Mirror", PrimitiveType.Cube, cell.transform, new Vector3(0, .8f, 0), new Vector3(1.05f, 1.25f, .13f), mirror);
                        Label(tile.ToString(), cell.transform, new Vector3(0, 1.47f, -.24f));
                    }
                    if (tile == 'A')
                    {
                        Shape("Pedestal", PrimitiveType.Cube, cell.transform, new Vector3(0, .25f, 0), new Vector3(.7f, .5f, .7f), lever);
                        var pivot = new GameObject("Lever pivot").transform;
                        pivot.SetParent(cell.transform, false);
                        pivot.localPosition = new Vector3(0, .5f, 0);
                        Shape("Handle", PrimitiveType.Cube, pivot, new Vector3(0, .28f, 0), new Vector3(.13f, .56f, .13f), mirror);
                        Shape("Grip", PrimitiveType.Sphere, pivot, new Vector3(0, .55f, 0), Vector3.one * .25f, lever);
                        visual.leverHandle = pivot;
                        Label("A", cell.transform, new Vector3(0, .57f, -.51f));
                    }
                    if (tile == '1')
                    {
                        for (int side = -1; side <= 1; side += 2)
                        {
                            Shape("Plate edge", PrimitiveType.Cube, cell.transform, new Vector3(side * .51f, .025f, 0), new Vector3(.08f, .05f, 1.1f), plate);
                            Shape("Plate edge", PrimitiveType.Cube, cell.transform, new Vector3(0, .025f, side * .51f), new Vector3(1.1f, .05f, .08f), plate);
                        }
                        Label("1", cell.transform, new Vector3(0, .09f, 0));
                    }
                    if ("aXY".IndexOf(tile) >= 0)
                    {
                        var material = tile == 'a' ? shutter : door;
                        bool horizontal = tile == 'a';
                        for (int side = -1; side <= 1; side += 2)
                            Shape("Door frame", PrimitiveType.Cube, cell.transform,
                                horizontal ? new Vector3(side * .67f, .6f, 0) : new Vector3(0, .6f, side * .67f),
                                new Vector3(.16f, 1.2f, .16f), material);
                        visual.barrier = Shape("Closed gate", PrimitiveType.Cube, cell.transform, new Vector3(0, .48f, 0),
                            horizontal ? new Vector3(1.2f, .95f, .18f) : new Vector3(.18f, .95f, 1.2f), material);
                        Label(tile.ToString(), cell.transform, new Vector3(-.36f, 1.28f, -.4f));
                    }
                    if (tile == 'U')
                    {
                        Shape("Upper torso - fixed", PrimitiveType.Capsule, cell.transform, new Vector3(0, .49f, 0), new Vector3(.56f, .28f, .4f), upper);
                        Shape("Head", PrimitiveType.Sphere, cell.transform, new Vector3(0, .93f, 0), Vector3.one * .43f, upper);
                    }
                    if ("#GApPaXY".IndexOf(tile) >= 0)
                    {
                        var collider = cell.AddComponent<BoxCollider>();
                        collider.center = new Vector3(0, .5f, 0);
                        collider.size = new Vector3(LevelTwoController.CellSize, 1f, LevelTwoController.CellSize);
                        visual.collider = collider;
                    }
                    if ("#AaXY".IndexOf(tile) >= 0)
                        cell.layer = LayerMask.NameToLayer("VisionObstacle");
                    visual.renderers = cell.GetComponentsInChildren<Renderer>(true);
                    cells[r * model.Width + c] = visual;
                }
            var lowerObject = new GameObject("Lower body - controlled").transform;
            lowerObject.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2)
                Shape("Leg", PrimitiveType.Capsule, lowerObject, new Vector3(side * .19f, .38f, 0), new Vector3(.24f, .35f, .28f), lower);
            var visionObject = new GameObject("Upper body vision - Whitebox1 settings");
            visionObject.transform.SetParent(root.transform, false);
            visionObject.transform.position = LevelTwoController.CellPosition(model.Upper) + Vector3.up * .5f;
            var vision = visionObject.AddComponent<VisionSource>();
            EditorJsonUtility.FromJsonOverwrite(visionSettings, vision);
            var visionProperties = new SerializedObject(vision);
            visionProperties.FindProperty("visionRadius").floatValue = 10f;
            visionProperties.FindProperty("targetCamera").objectReferenceValue = camera;
            visionProperties.ApplyModifiedPropertiesWithoutUndo();
            var entry = Portal("p - vision entrance", model.PortalEntry, 0, root.transform);
            var exit = Portal("P - vision exit", model.PortalExit, 180, root.transform);
            entry.LinkedPortal = exit;
            exit.LinkedPortal = entry;
            controller.Configure(layout, cells, lowerObject, dark, lit, felt, camera, vision);
            controller.Initialize();
            // Keep the saved Scene view complete; the Play camera applies the vision mask.
            foreach (var visual in cells)
                foreach (var renderer in visual.renderers) renderer.enabled = true;
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Level 02 scene saved: " + ScenePath);
        }

        public static void BuildAndValidate()
        {
            Build();
            LevelTwoValidation.Run();
        }

        private static VisionPortal Portal(string name, Vector2Int cell, float yaw, Transform parent)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            item.transform.position = LevelTwoController.CellPosition(cell) + Vector3.up * .5f;
            item.transform.rotation = Quaternion.Euler(0, yaw, 0);
            item.transform.position += item.transform.forward * .09f;
            var portal = item.AddComponent<VisionPortal>();
            var properties = new SerializedObject(portal);
            properties.FindProperty("width").floatValue = 1.05f;
            properties.FindProperty("height").floatValue = 1.25f;
            properties.FindProperty("twoSided").boolValue = false;
            properties.ApplyModifiedPropertiesWithoutUndo();
            return portal;
        }

        private static Material Material(string name, Color color)
        {
            string path = Root + "/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .15f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());
            var renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return item;
        }

        private static void Label(string text, Transform parent, Vector3 position)
        {
            var item = new GameObject("Marker " + text);
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var mesh = item.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize = 48;
            mesh.characterSize = .12f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = new Color(.97f, .96f, .85f);
            var renderer = item.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = mesh.font.material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
