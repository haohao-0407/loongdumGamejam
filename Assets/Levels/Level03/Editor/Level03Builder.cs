using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Copies Whitebox1, then changes only the new scene's layout.</summary>
public static class Level03Builder
{
    public const string ScenePath = "Assets/Scenes/Level03_LightGates.unity";
    private static GameObject cubeTemplate;
    private static GameObject labelTemplate;
    private static Transform architecture;
    private static Material plain;
    private static Material entry;
    private static Material exit;

    [MenuItem("Tools/Loongdum/Level 03/Create From Whitebox1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene present; save it manually first.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Refusing to overwrite an existing Level03 scene.");
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Whitebox1.unity", ScenePath))
            throw new InvalidOperationException("Scene copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ConfigureCopy(scene);
    }

    public static void ConfigureCopy(Scene scene)
    {
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Only the new Level03 copy may be configured.");
        if (scene.GetRootGameObjects().Any(g => g.name == "Level 03 - Light Gates"))
            throw new InvalidOperationException("Refusing to overwrite an authored layout.");
        var roots = scene.GetRootGameObjects();
        Func<string, GameObject> root = name => roots.First(g => g != null && g.name == name);
        cubeTemplate = UnityEngine.Object.Instantiate(root("Walls").transform.GetChild(0).gameObject);
        cubeTemplate.name = "Level03 construction template";
        cubeTemplate.SetActive(false);
        labelTemplate = UnityEngine.Object.Instantiate(root("Reversal Tiles").GetComponentsInChildren<TextMesh>().First().gameObject);
        labelTemplate.SetActive(false);
        plain = cubeTemplate.GetComponent<Renderer>().sharedMaterial;
        entry = root("Vision Portal Pair").GetComponentsInChildren<Renderer>().First().sharedMaterial;
        exit = root("Vision Portal Pair").GetComponentsInChildren<Renderer>().Last().sharedMaterial;
        Material glass = root("Glass").GetComponentsInChildren<Renderer>().First().sharedMaterial;
        var tileTemplate = UnityEngine.Object.Instantiate(root("Reversal Tiles").GetComponentsInChildren<ReversalTile>().First().gameObject);
        tileTemplate.SetActive(false);
        var doorTemplate = UnityEngine.Object.Instantiate(root("Door"));
        doorTemplate.SetActive(false);
        var portalTemplate = UnityEngine.Object.Instantiate(root("Vision Portal Pair").GetComponentsInChildren<VisionPortal>().First().gameObject);
        portalTemplate.SetActive(false);
        foreach (string name in new[] { "Walls", "Glass", "Layer", "Vision Portal Pair", "Door", "Reversal Tiles" })
            UnityEngine.Object.DestroyImmediate(root(name));

        var level = new GameObject("Level 03 - Light Gates");
        architecture = Group("Architecture", level.transform);
        var floorGroup = Group("Floor", level.transform);
        var devices = Group("Devices", level.transform);
        // Terrain is Whitebox1's visible ground. Do not cover its material with
        // wall-material tiles or offset the terrain below the gameplay floor.
        var player = root("whiteboxplayer");
        var upper = root("vision source");
        var camera = root("Main Camera").GetComponent<Camera>();
        var source = upper.GetComponent<VisionSource>();
        var flow = level.AddComponent<Level03Flow>();
        flow.player = player.GetComponent<CharacterController>();
        flow.source = source;
        flow.reversal = player.GetComponent<BodyReversal>();
        flow.levelCamera = camera;
        flow.idleMarker = entry;
        flow.activeMarker = exit;
        flow.levers = new Transform[3];
        flow.leverHandles = new Renderer[3];
        flow.doors = new Animator[4];
        flow.barriers = new Collider[4];
        flow.lowerSpawn = Level03Layout.Find('L', 1.08f);
        flow.upperSpawn = Level03Layout.Find('U', .25f);
        player.transform.position = flow.lowerSpawn;
        upper.transform.position = flow.upperSpawn;
        // Preserve Whitebox1's solid upper-body collider; reunion is by proximity.
        var sourceData = new SerializedObject(source);
        sourceData.FindProperty("visionRadius").floatValue = 30f;
        sourceData.FindProperty("maxPortalHops").intValue = 3;
        sourceData.FindProperty("rayCount").intValue = 1024;
        sourceData.FindProperty("targetCamera").objectReferenceValue = camera;
        sourceData.ApplyModifiedPropertiesWithoutUndo();
        var reversalData = new SerializedObject(flow.reversal);
        reversalData.FindProperty("upperBody").objectReferenceValue = source;
        reversalData.ApplyModifiedPropertiesWithoutUndo();
        var animatorData = new SerializedObject(player.GetComponent<WhiteboxPlayerAnimator>());
        animatorData.FindProperty("spriteRenderer").objectReferenceValue = player.GetComponent<SpriteRenderer>();
        animatorData.ApplyModifiedPropertiesWithoutUndo();

        float cell = Level03Layout.CellSize;
        for (int r = 0; r < Level03Layout.Rows.Length; r++)
        for (int c = 0; c < Level03Layout.Rows[r].Length; c++)
        {
            char ch = Level03Layout.At(r, c);
            Vector3 pos = Level03Layout.Position(r, c);
            if (ch == ' ')
            {
                // Terrain remains the visual ground; the physical perimeter
                // blocks leaving via the diagram's open southern edge.
                Blocker("Outside " + r + "," + c, pos, architecture);
                continue;
            }
            var floor = new GameObject("Floor " + r + "," + c);
            floor.transform.SetParent(floorGroup, false);
            floor.transform.position = pos + Vector3.down * .06f;
            floor.AddComponent<BoxCollider>().size = new Vector3(cell - .025f, .12f, cell - .025f);
            if (ch == '#' || ch == 'G')
            {
                Cube((ch == '#' ? "Wall " : "Glass ") + r + "," + c, pos + Vector3.up * .5f,
                    new Vector3(cell, 1, cell), ch == 'G' ? glass : plain, architecture, true, ch == '#');
            }
            else if ("XZD a".Replace(" ", "").Contains(ch))
            {
                int index = ch == 'X' ? 0 : ch == 'D' ? 1 : ch == 'Z' ? 2 : 3;
                Transform hinge = Group("Hinge " + ch, devices);
                hinge.position = pos;
                hinge.rotation = Quaternion.Euler(0, ch == 'Z' || ch == 'a' ? 90 : 0, 0);
                var door = UnityEngine.Object.Instantiate(doorTemplate, hinge);
                door.name = "Door " + ch;
                door.SetActive(true);
                if (PrefabUtility.IsPartOfPrefabInstance(door)) PrefabUtility.UnpackPrefabInstance(door, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                door.transform.localPosition = new Vector3(-cell * .48f, 1.14f, 0);
                door.transform.localRotation = Quaternion.identity;
                door.transform.localScale = new Vector3(cell, 1, 1);
                UnityEngine.Object.DestroyImmediate(door.GetComponent<DoorAnimatorToggle>());
                foreach (Collider collider in door.GetComponentsInChildren<Collider>()) collider.enabled = false;
                flow.doors[index] = door.GetComponent<Animator>();
                // Full diagram cell barrier is authoritative, independent of the
                // copied swing animation; open leaves no collision in the passage.
                flow.barriers[index] = Blocker("Closed " + ch, pos, devices);
                Label(ch.ToString(), pos + Vector3.up * .08f, devices);
            }
            else if ("ABC".Contains(ch))
            {
                int index = ch == 'A' ? 0 : ch == 'C' ? 1 : 2;
                Transform lever = Group("Lever " + ch, devices);
                lever.position = pos;
                Cube("Base", pos + Vector3.up * .25f, new Vector3(.8f, .5f, .8f), plain, lever, true, false);
                var handle = Cube("Handle", pos + Vector3.up * .7f, new Vector3(.12f, .8f, .12f), entry, lever, false, false);
                flow.levers[index] = lever;
                flow.leverHandles[index] = handle.GetComponent<Renderer>();
                Label(ch + " → " + (ch == 'A' ? "X" : ch == 'C' ? "D" : "Z"), pos + Vector3.up * .06f, devices);
            }
            else if (ch == 'S')
            {
                Cube("Reversal pedestal", pos + Vector3.up * .2f, new Vector3(.7f, .4f, .7f), entry, devices, true, false);
            }
            else if (ch == '1')
            {
                var marker = Cube("Pressure plate 1", pos + Vector3.up * .025f, new Vector3(1.65f, .05f, 1.65f), entry, devices, false, false);
                flow.plateMarker = marker.GetComponent<Renderer>();
                Label("1 → a", pos + Vector3.up * .065f, devices);
            }
        }
        for (int c = -1; c <= 21; c++)
        {
            Blocker("South perimeter " + c, Level03Layout.Position(17, c), architecture);
            Blocker("North perimeter " + c, Level03Layout.Position(-1, c), architecture);
        }
        for (int r = 0; r < 17; r++)
        {
            Blocker("West perimeter " + r, Level03Layout.Position(r, -1), architecture);
            Blocker("East perimeter " + r, Level03Layout.Position(r, 21), architecture);
        }

        foreach (Vector3 position in new[] { Level03Layout.FirstTile, Level03Layout.SecondTile })
        {
            var tile = UnityEngine.Object.Instantiate(tileTemplate, devices);
            tile.name = position == Level03Layout.FirstTile ? "F Reversal 1" : "F Reversal 2";
            tile.transform.position = position;
            if (position == Level03Layout.SecondTile) tile.transform.localScale = new Vector3(.4f, 1, .4f);
            tile.SetActive(true);
        }
        // Three co-located aperture pairs redirect continuous sight, retaining
        // the original total distance budget. Gate 1 turns NW light westwards;
        // a and G are physically between gates 1 and 2, so a must open first.
        int[] columns = { 15, 12, 10 };
        for (int i = 0; i < columns.Length; i++)
        {
            Vector3 center = Level03Layout.Position(8, columns[i], 1.5f);
            var incoming = UnityEngine.Object.Instantiate(portalTemplate, devices);
            var outgoing = UnityEngine.Object.Instantiate(portalTemplate, devices);
            incoming.name = "Light gate " + (i + 1) + " Entry";
            outgoing.name = "Light gate " + (i + 1) + " Exit";
            incoming.transform.SetPositionAndRotation(center + (i == 0 ? Vector3.forward * .8f : Vector3.zero),
                Quaternion.Euler(0, i == 0 ? Mathf.Atan2(5.6f, -5.1f) * Mathf.Rad2Deg : 90, 0));
            outgoing.transform.SetPositionAndRotation(center + Vector3.left * (i == 0 ? 1.1f : .04f), Quaternion.Euler(0, 270, 0));
            incoming.SetActive(true); outgoing.SetActive(true);
            var a = incoming.GetComponent<VisionPortal>();
            var b = outgoing.GetComponent<VisionPortal>();
            a.LinkedPortal = b; b.LinkedPortal = a;
            foreach (Renderer rend in outgoing.GetComponentsInChildren<Renderer>()) rend.sharedMaterial = exit;
            Label("M" + (i + 1), Level03Layout.Position(8, columns[i], .06f), devices);
        }
        // Preserve lens, rotation, renderer, post-processing, lights and volume;
        // pull back this scene's camera solely to fit its larger footprint.
        camera.transform.position = new Vector3(0, 44, -29);
        Label("F → 玻璃房", Level03Layout.Position(15, 5, .07f), devices);
        Label("D → F", Level03Layout.Position(7, 17, .07f), devices);
        Label("X → B", Level03Layout.Position(11, 7, .07f), devices);

        foreach (var go in new[] { cubeTemplate, labelTemplate, tileTemplate, doorTemplate, portalTemplate }) UnityEngine.Object.DestroyImmediate(go);
        // Only the newly created scene is saved. No SaveAssets call and no build
        // settings mutation, so shared materials, prefabs and configs stay intact.
        foreach (var instance in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            if (PrefabUtility.IsPartOfPrefabInstance(instance)) PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Level03 save failed.");
        Selection.activeGameObject = level;
        Debug.Log("Level03 created from the current on-disk Whitebox1. Shared assets were not saved.");
    }

    private static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }
    private static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool collision, bool opaque)
    {
        var go = UnityEngine.Object.Instantiate(cubeTemplate, parent);
        if (PrefabUtility.IsPartOfPrefabInstance(go)) PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name; go.SetActive(true);
        go.transform.SetPositionAndRotation(position, Quaternion.identity);
        go.transform.localScale = scale;
        go.layer = opaque ? 8 : 0;
        go.tag = opaque ? "VisionObstacle" : "Untagged";
        go.GetComponent<Renderer>().sharedMaterial = material;
        var collider = go.GetComponent<BoxCollider>();
        collider.enabled = collision;
        return go;
    }
    private static Collider Blocker(string name, Vector3 position, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        go.transform.position = position + Vector3.up * .5f;
        go.layer = 8; go.tag = "VisionObstacle";
        var collider = go.AddComponent<BoxCollider>();
        collider.size = new Vector3(Level03Layout.CellSize, 1, Level03Layout.CellSize);
        return collider;
    }
    private static void Label(string text, Vector3 position, Transform parent)
    {
        var go = UnityEngine.Object.Instantiate(labelTemplate, parent);
        go.name = "Label " + text; go.SetActive(true);
        go.transform.position = position;
        var label = go.GetComponent<TextMesh>();
        label.text = text; label.fontSize = 40; label.characterSize = .1f;
    }
}
