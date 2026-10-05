using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>Authoring utility. All generated geometry remains editable in TestLevel.</summary>
public static class LumenLevelBuilder
{
    private const string AssetRoot = "Assets/Levels/TestLevel";
    private const string LevelRootName = "L01 - Lumen Corridor";
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static int meshIndex;
    private static Font font;

    [MenuItem("Tools/Loongdum/TestLevel/Build Lumen Corridor")]
    public static void Build()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/TestLevel.unity")
            throw new InvalidOperationException("Open TestLevel in Edit mode before building.");
        if (GameObject.Find(LevelRootName) != null)
            throw new InvalidOperationException("The authored level already exists. Edit its objects directly.");

        EnsureFolder(AssetRoot + "/Materials");
        EnsureFolder(AssetRoot + "/Meshes");
        meshIndex = 0;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        MaterialFor("Paper", new Color(.3f, .3f, .288f));
        MaterialFor("Path", new Color(.65f, .65f, .624f));
        MaterialFor("Wall", new Color(.26f, .26f, .2496f));
        MaterialFor("Cap", new Color(.49f, .49f, .4704f));
        MaterialFor("Ink", new Color(.08f, .08f, .075f));
        MaterialFor("Light", new Color(.98f, .9f, .63f), true);

        var root = Group(LevelRootName, null);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Lumen Corridor");
        var architecture = Group("01 - Architecture", root);
        var route = Group("02 - Route and Landmarks", root);
        var portals = Group("03 - Light Relays (vision only)", root);
        var actors = Group("04 - Player and Vision Source", root);
        var logic = Group("05 - Objective and UI", root);

        Box("Foundation", architecture, new Vector3(0, -.27f, 0), new Vector3(25, .5f, 14.6f), "Ink", true);
        Box("Paper Floor", architecture, new Vector3(0, -.08f, 0), new Vector3(24, .16f, 13.6f), "Paper", true);
        // A fine, low-contrast tile grid helps judge distance without becoming a route solution.
        for (int x = -10; x <= 10; x += 2)
            Box("Floor Joint X " + x, route, new Vector3(x, .007f, 0), new Vector3(.025f, .008f, 13.4f), "Cap");
        for (int z = -6; z <= 6; z += 2)
            Box("Floor Joint Z " + z, route, new Vector3(0, .008f, z), new Vector3(23.7f, .008f, .025f), "Cap");

        Wall("North Boundary", architecture, new Vector3(0, .72f, 6.8f), new Vector3(24.4f, 1.44f, .38f));
        Wall("South Boundary", architecture, new Vector3(0, .72f, -6.8f), new Vector3(24.4f, 1.44f, .38f));
        Wall("West Boundary", architecture, new Vector3(-12, .72f, 0), new Vector3(.38f, 1.44f, 13.6f));
        Wall("East Boundary", architecture, new Vector3(12, .72f, 0), new Vector3(.38f, 1.44f, 13.6f));

        var glass = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Glass.prefab"));
        glass.name = "01 - Glass Barrier (solid, transmits sight)";
        glass.transform.SetParent(architecture, false);
        glass.transform.position = new Vector3(-6, .9f, -2.05f);
        glass.transform.localScale = new Vector3(.18f, 1.8f, 9.1f);
        glass.layer = 0;
        Box("Glass Bottom Rail", architecture, new Vector3(-6, .12f, -2.05f), new Vector3(.3f, .24f, 9.1f), "Ink", true);
        Box("Glass Top Rail", architecture, new Vector3(-6, 1.83f, -2.05f), new Vector3(.24f, .12f, 9.1f), "Ink");
        for (int i = 0; i < 4; i++)
            Box("Glass Mullion " + i, architecture, new Vector3(-6, .92f, -6.5f + i * 3f), new Vector3(.28f, 1.84f, .12f), "Ink", true);

        Wall("02 - North Chicane", architecture, new Vector3(0, .9f, 2.9f), new Vector3(.65f, 1.8f, 7.6f));
        Wall("03 - South Chicane", architecture, new Vector3(5, .9f, -3f), new Vector3(.65f, 1.8f, 7.6f));
        // Sparse buttresses and banding give the walls a readable architectural silhouette.
        foreach (float z in new[] { .25f, 2.5f, 4.75f, 6.25f })
            Box("North Wall Pier " + z, architecture, new Vector3(0, 1.03f, z), new Vector3(.92f, 2.06f, .38f), "Cap", true, true);
        foreach (float z in new[] { -6.25f, -4.25f, -2.25f, -.1f })
            Box("South Wall Pier " + z, architecture, new Vector3(5, 1.03f, z), new Vector3(.92f, 2.06f, .38f), "Cap", true, true);

        PathStrip("Start Walk", route, new Vector3(-9, .017f, -.2f), new Vector3(1.45f, .02f, 8.5f));
        PathStrip("Glass Detour", route, new Vector3(-6.2f, .018f, 4.2f), new Vector3(6.9f, .02f, 1.45f));
        PathStrip("West Descent", route, new Vector3(-3.5f, .019f, .2f), new Vector3(1.45f, .02f, 7.8f));
        PathStrip("Lower Crossing", route, new Vector3(-.5f, .02f, -3.7f), new Vector3(7.4f, .02f, 1.45f));
        PathStrip("Inner Ascent", route, new Vector3(2.5f, .021f, .25f), new Vector3(1.45f, .02f, 8.2f));
        PathStrip("Court Approach", route, new Vector3(5.7f, .022f, 4.2f), new Vector3(7.9f, .02f, 1.45f));
        PathStrip("Final Walk", route, new Vector3(9, .023f, 2.1f), new Vector3(1.45f, .02f, 4.5f));

        FloorLabel("01", route, new Vector3(-9, .06f, 2.9f), .16f);
        FloorLabel("02", route, new Vector3(-3.5f, .06f, -2f), .16f);
        FloorLabel("03", route, new Vector3(2.5f, .06f, 1.9f), .16f);
        FloorLabel("START", route, new Vector3(-10.6f, .06f, -3.5f), .07f);
        FloorLabel("VISION SOURCE", route, new Vector3(9, .06f, -1.4f), .05f);
        foreach (var point in new[] { new Vector3(-9, .04f, 0), new Vector3(-6, .04f, 4.2f), new Vector3(-3.5f, .04f, .4f), new Vector3(0, .04f, -3.7f), new Vector3(2.5f, .04f, .5f), new Vector3(5, .04f, 4.2f) })
            Cylinder("Route Stud", route, point, .15f, .045f, "Ink", false);

        var spawn = Group("Player Spawn", logic);
        spawn.position = new Vector3(-9, .87f, -4.4f);
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/whiteboxplayer.prefab"));
        player.name = "Player";
        player.tag = "Player";
        player.transform.SetParent(actors, false);
        player.transform.position = spawn.position;
        player.transform.localScale = Vector3.one;
        var controller = player.GetComponent<CharacterController>();
        controller.height = 1.6f;
        controller.radius = .32f;
        controller.skinWidth = .04f;
        controller.stepOffset = .22f;
        player.GetComponent<MeshRenderer>().enabled = false;
        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Player Silhouette";
        body.transform.SetParent(player.transform, false);
        body.transform.localScale = new Vector3(.64f, .8f, .64f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = materials["Ink"];
        var backpack = Box("Player Backpack", player.transform, spawn.position, new Vector3(.38f, .5f, .18f), "Light");
        backpack.transform.localPosition = new Vector3(0, 0, -.33f);
        var movement = player.GetComponent<WhiteboxPlayerMovement>();
        var moveSettings = new SerializedObject(movement);
        moveSettings.FindProperty("moveSpeed").floatValue = 4.2f;
        moveSettings.ApplyModifiedPropertiesWithoutUndo();

        var beacon = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/vision source.prefab"));
        beacon.name = "Vision Source";
        beacon.transform.SetParent(actors, false);
        beacon.transform.position = new Vector3(9, .65f, 0);
        beacon.transform.localScale = Vector3.one * .78f;
        beacon.GetComponent<SphereCollider>().enabled = false;
        beacon.GetComponent<MeshRenderer>().sharedMaterial = materials["Light"];
        var source = beacon.GetComponent<VisionSource>();
        var vision = new SerializedObject(source);
        vision.FindProperty("visionRadius").floatValue = 27f;
        vision.FindProperty("sightHeight").floatValue = .45f;
        vision.FindProperty("edgeSoftness").floatValue = .13f;
        vision.FindProperty("rayCount").intValue = 1024;
        vision.FindProperty("keepObstacleVisible").boolValue = true;
        vision.FindProperty("maxPortalHops").intValue = 1;
        vision.FindProperty("targetCamera").objectReferenceValue = Camera.main;
        vision.ApplyModifiedPropertiesWithoutUndo();

        Cylinder("Source Dais", route, new Vector3(9, .07f, 0), 1.45f, .14f, "Cap", true);
        Cylinder("Dais Inlay", route, new Vector3(9, .147f, 0), 1.12f, .012f, "Ink", false);
        Ring("Source Arrival Ring", route, new Vector3(9, .18f, 0), 1.2f, .065f, "Light");
        Ring("Source Halo", route, new Vector3(9, 1.25f, 0), .6f, .035f, "Light");
        var light = Group("Source Glow", actors).gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.transform.position = new Vector3(9, 2, 0);
        light.color = new Color(1f, .9f, .66f);
        light.intensity = 2;
        light.range = 6;
        light.shadows = LightShadows.None;

        // These are apertures for light, never physical teleporters or additional VisionSources.
        Relay("A - Glass Passage", portals, new Vector3(9, 1.1f, 5.4f), 180, new Vector3(-9.4f, 1.1f, -5.8f), 0, 4.6f);
        Relay("B - West Descent", portals, new Vector3(10.8f, 1.1f, 0), 270, new Vector3(-3.5f, 1.1f, 5.8f), 180, 3.5f);
        Relay("C - Inner Ascent", portals, new Vector3(7.4f, 1.1f, 0), 90, new Vector3(2.5f, 1.1f, -5.8f), 0, 3.5f);

        var milestones = new[] {
            Landmark("01 - Glass Cleared", logic, new Vector3(-3.5f, 0, 4.2f)),
            Landmark("02 - Shadow Turn Cleared", logic, new Vector3(2.5f, 0, -3.7f)),
            Landmark("03 - Inner Court Reached", logic, new Vector3(8.5f, 0, 4.2f))
        };
        var camera = Camera.main;
        Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform }, "Frame TestLevel");
        camera.transform.position = new Vector3(0, 22, -12);
        camera.transform.LookAt(new Vector3(0, 0, 1.1f));
        camera.orthographic = true;
        camera.orthographicSize = 9.6f;
        camera.nearClipPlane = .1f;
        camera.farClipPlane = 100;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.23f, .23f, .23f);
        var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = false;
        cameraData.antialiasing = AntialiasingMode.None;
        cameraData.requiresDepthTexture = true;
        var sun = UnityEngine.Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
        Undo.RecordObjects(new UnityEngine.Object[] { sun, sun.transform }, "Light TestLevel");
        sun.transform.rotation = Quaternion.Euler(55, -32, 0);
        sun.intensity = 1.65f;
        sun.color = new Color(1f, .98f, .92f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.64f, .63f, .6f);
        RenderSettings.fog = false;
        RenderSettings.skybox = null;

        BuildUI(logic, controller, movement, source, camera, spawn, milestones);
        PrefabUtility.RecordPrefabInstancePropertyModifications(player);
        PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(beacon);
        PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        PrefabUtility.RecordPrefabInstancePropertyModifications(glass);
        foreach (var component in new Component[] { player.transform, player.GetComponent<MeshRenderer>(), beacon.transform, beacon.GetComponent<MeshRenderer>(), beacon.GetComponent<SphereCollider>(), glass.transform })
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0, 0, 0), Quaternion.Euler(62, 0, 0), 19);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("TestLevel: Lumen Corridor authored and saved. WASD moves; R restarts.");
    }

    private static Transform Group(string name, Transform parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj.transform;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int split = path.LastIndexOf('/');
        string parent = path.Substring(0, split);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
    }

    private static void MaterialFor(string name, Color color, bool emissive = false)
    {
        string path = AssetRoot + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", .12f);
        if (emissive)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.6f);
        }
        EditorUtility.SetDirty(material);
        materials[name] = material;
    }

    private static GameObject FinishMesh(ProBuilderMesh mesh, string name, Transform parent, Vector3 position, string material, bool solid, bool blocksSight)
    {
        var obj = mesh.gameObject;
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.position = position;
        mesh.ToMesh();
        mesh.Refresh();
        var unityMesh = obj.GetComponent<MeshFilter>().sharedMesh;
        unityMesh.hideFlags = HideFlags.None;
        unityMesh.name = "Lumen_" + (++meshIndex).ToString("000");
        AssetDatabase.CreateAsset(unityMesh, AssetRoot + "/Meshes/" + unityMesh.name + ".asset");
        obj.GetComponent<MeshRenderer>().sharedMaterial = materials[material];
        var collider = obj.GetComponent<Collider>();
        if (!solid && collider != null) UnityEngine.Object.DestroyImmediate(collider);
        if (solid && collider == null)
        {
            var box = obj.AddComponent<BoxCollider>();
            box.center = unityMesh.bounds.center;
            box.size = unityMesh.bounds.size;
        }
        if (blocksSight) { obj.layer = LayerMask.NameToLayer("VisionObstacle"); obj.tag = "VisionObstacle"; }
        return obj;
    }

    private static GameObject Box(string name, Transform parent, Vector3 position, Vector3 size, string material, bool solid = false, bool blocksSight = false)
        => FinishMesh(ShapeGenerator.GenerateCube(PivotLocation.Center, size), name, parent, position, material, solid, blocksSight);

    private static void Wall(string name, Transform parent, Vector3 position, Vector3 size)
    {
        Box(name, parent, position, size, "Wall", true, true);
        Box(name + " / Cap", parent, position + Vector3.up * (size.y / 2 + .07f), new Vector3(size.x + .1f, .14f, size.z + .1f), "Cap");
        Box(name + " / Foot", parent, new Vector3(position.x, .1f, position.z), new Vector3(size.x + .1f, .2f, size.z + .1f), "Ink", true);
    }

    private static void PathStrip(string name, Transform parent, Vector3 position, Vector3 size)
        => Box(name, parent, position, size, "Path");

    private static GameObject Cylinder(string name, Transform parent, Vector3 position, float radius, float height, string material, bool solid)
    {
        var obj = FinishMesh(ShapeGenerator.GenerateCylinder(PivotLocation.Center, 32, radius, height, 0), name, parent, position, material, false, false);
        if (solid) { var collider = obj.AddComponent<MeshCollider>(); collider.sharedMesh = obj.GetComponent<MeshFilter>().sharedMesh; }
        return obj;
    }

    private static void Ring(string name, Transform parent, Vector3 position, float radius, float thickness, string material)
        => FinishMesh(ShapeGenerator.GenerateTorus(PivotLocation.Center, 8, 48, thickness, radius, true, 360, 360), name, parent, position, material, false, false);

    private static void FloorLabel(string text, Transform parent, Vector3 position, float size)
    {
        var label = Group("Floor Label " + text, parent).gameObject;
        label.transform.position = position;
        label.transform.rotation = Quaternion.Euler(90, 0, 0);
        var mesh = label.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.font = font;
        mesh.fontSize = 64;
        mesh.characterSize = size;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.color = new Color(.09f, .09f, .08f);
        label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    private static void Relay(string name, Transform parent, Vector3 input, float inputYaw, Vector3 output, float outputYaw, float width)
    {
        var pair = Group(name, parent);
        var entry = Aperture("Input - near source", pair, input, inputYaw, width);
        var exit = Aperture("Output - corridor", pair, output, outputYaw, width);
        entry.LinkedPortal = exit;
        exit.LinkedPortal = entry;
    }

    private static VisionPortal Aperture(string name, Transform parent, Vector3 position, float yaw, float width)
    {
        var node = Group(name, parent);
        node.position = position;
        node.rotation = Quaternion.Euler(0, yaw, 0);
        var portal = node.gameObject.AddComponent<VisionPortal>();
        var properties = new SerializedObject(portal);
        properties.FindProperty("width").floatValue = width;
        properties.FindProperty("height").floatValue = 2.2f;
        properties.FindProperty("twoSided").boolValue = false;
        properties.ApplyModifiedPropertiesWithoutUndo();
        foreach (float x in new[] { -width / 2 - .07f, width / 2 + .07f })
        {
            var frame = Box("Light Relay Post", node, position, new Vector3(.13f, 2.2f, .13f), "Ink");
            frame.transform.localPosition = new Vector3(x, 0, 0);
            frame.transform.localRotation = Quaternion.identity;
        }
        var header = Box("Light Relay Lintel", node, position, new Vector3(width + .27f, .13f, .13f), "Light");
        header.transform.localPosition = new Vector3(0, 1.1f, 0);
        header.transform.localRotation = Quaternion.identity;
        var threshold = Box("Light Relay Threshold", node, position, new Vector3(width, .04f, .18f), "Light");
        threshold.transform.localPosition = new Vector3(0, -1.06f, 0);
        threshold.transform.localRotation = Quaternion.identity;
        return portal;
    }

    private static Transform Landmark(string name, Transform parent, Vector3 position)
    {
        var marker = Group(name, parent);
        marker.position = position;
        return marker;
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private static Text Label(string name, Transform parent, string text, Vector2 lower, Vector2 upper, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var rect = Rect(name, parent, Vector2.zero, Vector2.one, lower, upper);
        var label = rect.gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        return label;
    }

    private static void BuildUI(Transform parent, CharacterController player, WhiteboxPlayerMovement movement, VisionSource source, Camera camera, Transform spawn, Transform[] milestones)
    {
        var canvasObject = new GameObject("Lumen HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        Color paper = new Color(.95f, .94f, .89f);
        Color ink = new Color(.13f, .13f, .12f);
        Color gold = new Color(.95f, .84f, .5f);
        var header = Rect("Header", canvas.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -160), new Vector2(470, -30));
        header.gameObject.AddComponent<Image>().color = ink;
        Label("Level Title", header, "L01  /  LUMEN CORRIDOR", new Vector2(20, 82), new Vector2(-20, -8), 19, gold);
        var objective = Label("Objective", header, "Reach the Vision Source", new Vector2(20, 39), new Vector2(-20, -36), 26, paper);
        var stage = Label("Stage", header, "01  /  GLASS PASSAGE", new Vector2(20, 4), new Vector2(-20, -91), 16, paper);

        var timer = Rect("Timer", canvas.transform, Vector2.one, Vector2.one, new Vector2(-143, -86), new Vector2(-34, -30));
        timer.gameObject.AddComponent<Image>().color = ink;
        var timerText = Label("Clock", timer, "00:00", new Vector2(6, 0), new Vector2(-6, 0), 24, paper, TextAnchor.MiddleCenter);
        var footer = Rect("Controls", canvas.transform, Vector2.zero, new Vector2(1, 0), new Vector2(34, 24), new Vector2(-34, 88));
        footer.gameObject.AddComponent<Image>().color = ink;
        Label("Keyboard", footer, "W A S D  /  MOVE     R  /  RESTART", new Vector2(22, 0), new Vector2(-750, 0), 18, paper);
        Label("Relay Hint", footer, "GLASS BLOCKS YOU.  RELAYS CARRY LIGHT.", new Vector2(730, 0), new Vector2(-22, 0), 16, gold, TextAnchor.MiddleRight);
        var segments = new RectTransform[3];
        for (int i = 0; i < 3; i++)
        {
            segments[i] = Rect("Progress " + (i + 1), canvas.transform, new Vector2(.5f, 1), new Vector2(.5f, 1), new Vector2(-65 + i * 48, -50), new Vector2(-25 + i * 48, -44));
            segments[i].gameObject.AddComponent<Image>().color = i == 0 ? gold : new Color(.35f, .35f, .34f);
        }

        var locator = Rect("Player Locator", canvas.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-10, -10), new Vector2(10, 10));
        var locatorText = Label("Locator", locator, "+", new Vector2(-6, -6), new Vector2(6, 6), 25, gold, TextAnchor.MiddleCenter);
        var shadow = locatorText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = ink;
        shadow.effectDistance = new Vector2(2, -2);

        var victory = Rect("Victory", canvas.transform, new Vector2(.5f, .5f), new Vector2(.5f, .5f), new Vector2(-235, -140), new Vector2(235, 140));
        victory.gameObject.AddComponent<Image>().color = ink;
        Label("Success Label", victory, "SOURCE REACHED", new Vector2(25, 152), new Vector2(-25, -28), 34, gold, TextAnchor.MiddleCenter);
        Label("Success Copy", victory, "You found your way through the shadows.", new Vector2(20, 110), new Vector2(-20, -106), 18, paper, TextAnchor.MiddleCenter);
        var victoryTime = Label("Finish Time", victory, "TIME  00:00", new Vector2(20, 68), new Vector2(-20, -158), 20, paper, TextAnchor.MiddleCenter);
        var buttonRect = Rect("Restart Button", victory, Vector2.zero, Vector2.zero, new Vector2(90, 23), new Vector2(380, 70));
        buttonRect.gameObject.AddComponent<Image>().color = gold;
        var button = buttonRect.gameObject.AddComponent<Button>();
        Label("Restart Label", buttonRect, "PLAY AGAIN  [R]", Vector2.zero, Vector2.zero, 19, ink, TextAnchor.MiddleCenter);

        var eventSystem = Group("UI EventSystem", parent).gameObject;
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
        var flow = parent.gameObject.AddComponent<LumenLevelFlow>();
        var settings = new SerializedObject(flow);
        settings.FindProperty("player").objectReferenceValue = player;
        settings.FindProperty("movement").objectReferenceValue = movement;
        settings.FindProperty("source").objectReferenceValue = source;
        settings.FindProperty("levelCamera").objectReferenceValue = camera;
        settings.FindProperty("spawn").objectReferenceValue = spawn;
        settings.FindProperty("objectiveText").objectReferenceValue = objective;
        settings.FindProperty("stageText").objectReferenceValue = stage;
        settings.FindProperty("timerText").objectReferenceValue = timerText;
        settings.FindProperty("victoryPanel").objectReferenceValue = victory.gameObject;
        settings.FindProperty("victoryTimeText").objectReferenceValue = victoryTime;
        settings.FindProperty("playerLocator").objectReferenceValue = locator;
        settings.FindProperty("locatorCanvas").objectReferenceValue = canvas.transform;
        var milestonesProperty = settings.FindProperty("routeMilestones");
        milestonesProperty.arraySize = milestones.Length;
        for (int i = 0; i < milestones.Length; i++) milestonesProperty.GetArrayElementAtIndex(i).objectReferenceValue = milestones[i];
        var segmentsProperty = settings.FindProperty("progressSegments");
        segmentsProperty.arraySize = segments.Length;
        for (int i = 0; i < segments.Length; i++) segmentsProperty.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
        settings.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.Events.UnityEventTools.AddPersistentListener(button.onClick, flow.ResetLevel);
        victory.gameObject.SetActive(false);
    }
}
