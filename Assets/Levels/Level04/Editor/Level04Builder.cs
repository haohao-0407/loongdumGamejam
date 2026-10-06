using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>Creates a new scene from the on-disk Whitebox1; never saves shared assets.</summary>
public static class Level04Builder
{
    public const string ScenePath = "Assets/Scenes/Level04_WindowReturn.unity";
    private static GameObject cubeTemplate;
    private static GameObject labelTemplate;
    private static Material plain;
    private static Material entry;
    private static Material exit;

    [MenuItem("Tools/Loongdum/Level 04/Create From Whitebox1")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Unsaved scene present; refusing to replace it.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("Refusing to overwrite Level04.");
        if (!AssetDatabase.CopyAsset("Assets/Scenes/Whitebox1.unity", ScenePath))
            throw new InvalidOperationException("Scene copy failed.");
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ConfigureCopy(scene);
    }
    public static void ConfigureCopy(UnityEngine.SceneManagement.Scene scene)
    {
        if (scene.path != ScenePath || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Only the Level04 copy may be configured in Edit Mode.");
        if (scene.GetRootGameObjects().Any(g => g.name == "Level 04 - Window Return"))
            throw new InvalidOperationException("Refusing to overwrite an authored layout.");
        var roots = scene.GetRootGameObjects();
        Func<string, GameObject> root = name => roots.First(g => g != null && g.name == name);
        cubeTemplate = UnityEngine.Object.Instantiate(root("Walls").transform.GetChild(0).gameObject);
        cubeTemplate.SetActive(false);
        labelTemplate = UnityEngine.Object.Instantiate(root("Reversal Tiles").GetComponentsInChildren<TextMesh>().First().gameObject);
        labelTemplate.SetActive(false);
        plain = cubeTemplate.GetComponent<Renderer>().sharedMaterial;
        entry = root("Vision Portal Pair").GetComponentsInChildren<Renderer>().First().sharedMaterial;
        exit = root("Vision Portal Pair").GetComponentsInChildren<Renderer>().Last().sharedMaterial;
        Material glass = root("Glass").GetComponentsInChildren<Renderer>().First().sharedMaterial;
        var tileTemplate = UnityEngine.Object.Instantiate(root("Reversal Tiles").GetComponentsInChildren<ReversalTile>().First().gameObject);
        var doorTemplate = UnityEngine.Object.Instantiate(root("Door"));
        var portalTemplate = UnityEngine.Object.Instantiate(root("Vision Portal Pair").GetComponentsInChildren<VisionPortal>().First().gameObject);
        tileTemplate.SetActive(false); doorTemplate.SetActive(false); portalTemplate.SetActive(false);
        foreach (string name in new[] { "Walls", "Glass", "Layer", "Vision Portal Pair", "Door", "Reversal Tiles" })
            UnityEngine.Object.DestroyImmediate(root(name));

        var level = new GameObject("Level 04 - Window Return");
        var architecture = Group("Architecture", level.transform);
        var floorGroup = Group("Floor colliders", level.transform);
        var devices = Group("Devices", level.transform);
        var flow = level.AddComponent<Level04Flow>();
        flow.player = root("whiteboxplayer").GetComponent<CharacterController>();
        flow.source = root("vision source").GetComponent<VisionSource>();
        flow.reversal = flow.player.GetComponent<BodyReversal>();
        flow.levelCamera = root("Main Camera").GetComponent<Camera>();
        flow.lowerSpawn = Level04Layout.Find('L', 1.08f); flow.upperSpawn = Level04Layout.Find('U', .25f);
        flow.player.transform.position = flow.lowerSpawn; flow.source.transform.position = flow.upperSpawn;
        flow.levers = new Transform[3]; flow.leverHandles = new Renderer[3];
        flow.doors = new Animator[5]; flow.barriers = new Collider[5];
        flow.idleMarker = entry; flow.activeMarker = exit;
        var sourceData = new SerializedObject(flow.source);
        sourceData.FindProperty("visionRadius").floatValue = 10f;
        sourceData.FindProperty("maxPortalHops").intValue = 1;
        sourceData.FindProperty("rayCount").intValue = 512;
        sourceData.FindProperty("targetCamera").objectReferenceValue = flow.levelCamera;
        sourceData.ApplyModifiedPropertiesWithoutUndo();
        var reversalData = new SerializedObject(flow.reversal);
        reversalData.FindProperty("upperBody").objectReferenceValue = flow.source;
        reversalData.ApplyModifiedPropertiesWithoutUndo();
        var animatorData = new SerializedObject(flow.player.GetComponent<WhiteboxPlayerAnimator>());
        animatorData.FindProperty("spriteRenderer").objectReferenceValue = flow.player.GetComponent<SpriteRenderer>();
        animatorData.ApplyModifiedPropertiesWithoutUndo();

        float cell = Level04Layout.CellSize;
        for (int r = 0; r < Level04Layout.Rows.Length; r++)
        for (int c = 0; c < Level04Layout.Rows[r].Length; c++)
        {
            var p = Level04Layout.Position(r, c); char ch = Level04Layout.At(r, c);
            if (ch == ' ') { Blocker("Outside " + r + "," + c, p, architecture); continue; }
            var floor = new GameObject("Floor " + r + "," + c);
            floor.transform.SetParent(floorGroup, false); floor.transform.position = p + Vector3.down * .06f;
            floor.AddComponent<BoxCollider>().size = new Vector3(cell, .12f, cell);
            if (ch == '#' || ch == 'G')
                Cube((ch == '#' ? "Wall " : "Glass ") + r + "," + c, p + Vector3.up * .5f,
                    new Vector3(cell, 1, cell), ch == 'G' ? glass : plain, architecture, true, ch == '#');
            else if ("XDYZa".Contains(ch))
            {
                int index = "XDYZa".IndexOf(ch);
                var hinge = Group("Hinge " + ch, devices); hinge.position = p;
                hinge.rotation = Quaternion.Euler(0, ch == 'Y' ? 0 : 90, 0);
                var door = UnityEngine.Object.Instantiate(doorTemplate, hinge);
                Unpack(door); door.name = "Door " + ch; door.SetActive(true);
                door.transform.localPosition = new Vector3(-cell * .48f, 1.14f, 0);
                door.transform.localRotation = Quaternion.identity;
                door.transform.localScale = new Vector3(cell, 1, 1);
                UnityEngine.Object.DestroyImmediate(door.GetComponent<DoorAnimatorToggle>());
                foreach (var collider in door.GetComponentsInChildren<Collider>()) collider.enabled = false;
                flow.doors[index] = door.GetComponent<Animator>();
                flow.barriers[index] = Blocker("Closed " + ch, p, devices);
                Label(ch + " ← " + (ch == 'X' || ch == 'D' ? "A" : ch == 'Y' ? "B" : ch == 'Z' ? "C" : "1"), p + Vector3.up * .07f, devices);
            }
            else if ("ABC".Contains(ch))
            {
                int index = "ABC".IndexOf(ch); var lever = Group("Lever " + ch, devices); lever.position = p;
                Cube("Base", p + Vector3.up * .25f, new Vector3(.7f, .5f, .7f), plain, lever, true, false);
                var handle = Cube("Handle", p + Vector3.up * .7f, new Vector3(.12f, .8f, .12f), entry, lever, false, false);
                flow.levers[index] = lever; flow.leverHandles[index] = handle.GetComponent<Renderer>();
                Label(ch + " → " + (ch == 'A' ? "D / X" : ch == 'B' ? "Y" : "Z"), p + Vector3.up * .06f, devices);
            }
            else if (ch == '1')
            {
                flow.plateMarker = Cube("Pressure plate 1", p + Vector3.up * .025f,
                    new Vector3(1.65f, .05f, 1.65f), entry, devices, false, false).GetComponent<Renderer>();
                Label("1 → a", p + Vector3.up * .065f, devices);
            }
        }
        for (int c = -1; c <= 26; c++)
        {
            Blocker("North boundary " + c, Level04Layout.Position(-1, c), architecture);
            Blocker("South boundary " + c, Level04Layout.Position(20, c), architecture);
        }
        for (int r = 0; r < 20; r++)
        {
            Blocker("West boundary " + r, Level04Layout.Position(r, -1), architecture);
            Blocker("East boundary " + r, Level04Layout.Position(r, 26), architecture);
        }
        foreach (var p in new[] { Level04Layout.FirstTile, Level04Layout.SecondTile })
        {
            var tile = UnityEngine.Object.Instantiate(tileTemplate, devices); Unpack(tile);
            tile.name = p == Level04Layout.FirstTile ? "F Reversal 1" : "F Reversal 2";
            tile.transform.position = p; tile.SetActive(true);
            if (p == Level04Layout.SecondTile) tile.transform.localScale = new Vector3(.4f, 1, .4f);
            tile.GetComponentInChildren<TextMesh>().text = p == Level04Layout.FirstTile ? "F1 / REVERSE" : "F2 / REVERSE";
        }
        var incoming = UnityEngine.Object.Instantiate(portalTemplate, devices);
        var outgoing = UnityEngine.Object.Instantiate(portalTemplate, devices);
        incoming.name = "Window portal Entry"; outgoing.name = "Window portal Exit";
        incoming.transform.SetPositionAndRotation(Level04Layout.PortalEntry, Quaternion.Euler(0, 270, 0));
        outgoing.transform.SetPositionAndRotation(Level04Layout.PortalExit, Quaternion.Euler(0, 90, 0));
        incoming.SetActive(true); outgoing.SetActive(true);
        incoming.GetComponent<VisionPortal>().LinkedPortal = outgoing.GetComponent<VisionPortal>();
        outgoing.GetComponent<VisionPortal>().LinkedPortal = incoming.GetComponent<VisionPortal>();
        foreach (var renderer in outgoing.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = exit;
        Label("M IN", Level04Layout.PortalEntry + Vector3.down * 1.43f, devices);
        Label("M OUT → B", Level04Layout.PortalExit + Vector3.down * 1.43f, devices);
        Label("F1 → A", Level04Layout.Position(15, 12, .07f), devices);
        Label("D → F2", Level04Layout.Position(10, 21, .07f), devices);
        Label("X → 1 / B", Level04Layout.Position(12, 5, .07f), devices);
        Label("Y → C → Z", Level04Layout.Position(5, 3, .07f), devices);
        flow.levelCamera.transform.position = new Vector3(0, 43, -29);
        foreach (var template in new[] { cubeTemplate, labelTemplate, tileTemplate, doorTemplate, portalTemplate })
            UnityEngine.Object.DestroyImmediate(template);
        foreach (var instance in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)))
            if (PrefabUtility.IsPartOfPrefabInstance(instance)) PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Level04 save failed.");
        Selection.activeGameObject = level;
    }
    private static void Unpack(GameObject go)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(go))
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
    }
    private static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
    }
    private static GameObject Cube(string name, Vector3 p, Vector3 size, Material material, Transform parent, bool collision, bool opaque)
    {
        var go = UnityEngine.Object.Instantiate(cubeTemplate, parent); Unpack(go);
        go.name = name; go.SetActive(true); go.transform.SetPositionAndRotation(p, Quaternion.identity); go.transform.localScale = size;
        go.layer = opaque ? 8 : 0; go.tag = opaque ? "VisionObstacle" : "Untagged";
        go.GetComponent<Renderer>().sharedMaterial = material; go.GetComponent<BoxCollider>().enabled = collision; return go;
    }
    private static Collider Blocker(string name, Vector3 p, Transform parent)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = p + Vector3.up * .5f;
        go.layer = 8; go.tag = "VisionObstacle";
        var collider = go.AddComponent<BoxCollider>(); collider.size = new Vector3(Level04Layout.CellSize, 1, Level04Layout.CellSize); return collider;
    }
    private static void Label(string text, Vector3 p, Transform parent)
    {
        var go = UnityEngine.Object.Instantiate(labelTemplate, parent); go.SetActive(true); go.name = "Label " + text;
        go.transform.position = p; var label = go.GetComponent<TextMesh>(); label.text = text; label.fontSize = 40; label.characterSize = .1f;
    }
}
