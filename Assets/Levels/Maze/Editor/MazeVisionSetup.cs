#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 在 Maze 场景里一次性搭出「视野玩法」三件套：
///   1. 头：VisionSource，站在入口高台上。只有它视野半径内的区域可见，其余全黑
///      （复用 PC_Renderer 上已有的 Vision Range Mask 渲染特性，和 Level03 / Whitebox1 同一套）。
///   2. 脚：白盒玩家，开局站在高台下面、迷宫入口缺口处。
///   3. 按压板：迷宫中心的反转格，站上去按 F 和头互换三轴位置（FullBodyReversal）。
///
/// 菜单：Tools ▸ Maze ▸ 搭建视野玩法。重复执行会先删掉上次生成的 VisionGameplay 根节点再重建。
/// </summary>
public static class MazeVisionSetup
{
    private const string ScenePath = "Assets/Scenes/Maze.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/whiteboxplayer.prefab";
    private const string VisionPrefabPath = "Assets/Prefabs/vision source.prefab";
    private const string PadMaterialPath = "Assets/Materials/ReversalPad.mat";
    private const string HeadMaterialPath = "Assets/Materials/VisionHead.mat";
    private const string RootName = "VisionGameplay";
    private const string ObstacleTag = "VisionObstacle";

    private const float VisionRadius = 10f;   // 视野半径，米
    private const float HeadAboveDeck = 1f;   // 头高出高台台面多少米
    private const float PlayerDrop = 0.6f;    // 玩家生成高度，之后靠重力落地
    private const float PadHalfSize = 1f;     // 按压板触发范围 = 2×2 米（和 Whitebox1 的反转格一致）
    private const float PadHeight = 0.7f;     // 触发范围高度，脚部容差
    private const float WallHeight = 1.8f;    // 围墙抬到多高（世界高度，米）
    private static readonly bool PerimeterOnly = false; // true = 只抬最外圈 PlatformWall_*
    private static readonly bool ShowHeadMarker = false; // 改成 true 会给「头」加一个可见小球（默认不加）
    private const float ContactDistance = 1f; // 头和脚多近算撞上（米）

    [MenuItem("Tools/Maze/搭建视野玩法（头 / 脚 / 按压板）")]
    public static void Build()
    {
        if (!CheckScene()) return;

        GameObject platform = GameObject.Find("EntranceHighWall");
        GameObject deck = GameObject.Find("PlatformBase");
        if (platform == null)
        {
            EditorUtility.DisplayDialog("找不到入口高台",
                "场景里没有名为 EntranceHighWall 的对象。Maze 场景是 MazeBuilder 生成的，请先生成迷宫。", "好");
            return;
        }

        Bounds platformBounds = WorldBounds(platform);
        float floorY = deck != null ? WorldBounds(deck).max.y : platformBounds.min.y;

        if (!TryGetMazeBounds(out Bounds mazeBounds))
        {
            EditorUtility.DisplayDialog("找不到迷宫墙",
                "场景里没有 WallH_* / WallV_* 之类的迷宫墙体，无法定位入口和中心。", "好");
            return;
        }

        // 入口缺口在高台正北（+Z 方向），也就是墙体范围最小 Z 那一侧
        Vector3 mazeCenter = new Vector3(mazeBounds.center.x, floorY, mazeBounds.center.z);
        Vector3 feetStart = new Vector3(platformBounds.center.x, floorY + PlayerDrop, mazeBounds.min.z + 1f);
        Vector3 headStart = new Vector3(platformBounds.center.x, platformBounds.max.y + HeadAboveDeck, platformBounds.center.z);

        Clear(false);
        int wallsRaised = RaiseWalls();

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "搭建视野玩法");
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // ---- 1. 头：视野源 ----
        var headPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisionPrefabPath);
        if (headPrefab == null)
        {
            EditorUtility.DisplayDialog("缺预制体", "找不到 " + VisionPrefabPath, "好");
            return;
        }
        GameObject head = (GameObject)PrefabUtility.InstantiatePrefab(headPrefab, root.transform);
        head.name = "头（视野源）";
        head.transform.SetPositionAndRotation(headStart, Quaternion.identity);
        Undo.RegisterCreatedObjectUndo(head, "搭建视野玩法");

        VisionSource vision = head.GetComponent<VisionSource>();
        if (vision == null) vision = head.GetComponentInChildren<VisionSource>();
        if (vision == null)
        {
            EditorUtility.DisplayDialog("预制体不对", VisionPrefabPath + " 上没有 VisionSource 组件。", "好");
            return;
        }
        SetFloat(vision, "visionRadius", VisionRadius);
        SetFloat(vision, "sightHeight", 0f);

        if (ShowHeadMarker)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "头（标记）";
            marker.transform.SetParent(head.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = Vector3.one * 0.35f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<MeshRenderer>().sharedMaterial =
                FlatMaterial(HeadMaterialPath, new Color(0.3f, 0.85f, 1f, 1f));
        }

        // ---- 2. 脚：玩家 ----
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            EditorUtility.DisplayDialog("缺预制体", "找不到 " + PlayerPrefabPath, "好");
            return;
        }
        if (playerPrefab.GetComponent<CharacterController>() == null)
        {
            EditorUtility.DisplayDialog("预制体不对",
                PlayerPrefabPath + " 上没有 CharacterController，FullBodyReversal 需要它。", "好");
            return;
        }
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, root.transform);
        player.name = "脚（玩家）";
        player.transform.SetPositionAndRotation(feetStart, Quaternion.identity);
        Undo.RegisterCreatedObjectUndo(player, "搭建视野玩法");

        FullBodyReversal reversal = player.GetComponent<FullBodyReversal>();
        if (reversal == null) reversal = Undo.AddComponent<FullBodyReversal>(player);
        SetRef(reversal, "upperBody", vision);
        SetEnumByName(reversal, "activationKey", "F");

        // 相机在 +Z 侧俯视（yaw 180），WASD 必须跟着相机走，否则画面上下是反的
        WhiteboxPlayerMovement movement = player.GetComponent<WhiteboxPlayerMovement>();
        if (movement == null) movement = player.GetComponentInChildren<WhiteboxPlayerMovement>();
        if (movement != null)
        {
            SetBool(movement, "alignToCamera", true);
            SetRef(movement, "inputCamera", Camera.main);
        }
        else
        {
            Debug.LogWarning("[MazeVisionSetup] 玩家预制体上没有 WhiteboxPlayerMovement，WASD 方向需要手动处理。");
        }

        // 头和脚撞上就游戏结束，并实时显示脚的位置
        HeadFeetGameOver gameOver = player.GetComponent<HeadFeetGameOver>();
        if (gameOver == null) gameOver = Undo.AddComponent<HeadFeetGameOver>(player);
        SetRef(gameOver, "head", head.transform);
        SetFloat(gameOver, "contactDistance", ContactDistance);

        // ---- 3. 按压板：迷宫中心的反转格 ----
        var pad = new GameObject("按压板（反转格）");
        Undo.RegisterCreatedObjectUndo(pad, "搭建视野玩法");
        pad.transform.SetParent(root.transform, false);
        pad.transform.position = mazeCenter;

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "板面";
        plate.transform.SetParent(pad.transform, false);
        plate.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        plate.transform.localScale = new Vector3(PadHalfSize * 1.8f, 0.12f, PadHalfSize * 1.8f);
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.GetComponent<MeshRenderer>().sharedMaterial =
            FlatMaterial(PadMaterialPath, new Color(1f, 0.55f, 0.12f, 1f));

        var trigger = pad.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(PadHalfSize * 2f, PadHeight, PadHalfSize * 2f);
        trigger.center = new Vector3(0f, PadHeight * 0.5f, 0f);
        pad.AddComponent<ReversalTile>();

        // ---- 4. 迷宫墙体打成视野遮挡物，让墙能挡视线 ----
        int tagged = TagObstacles();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Selection.activeGameObject = player;

        Debug.Log(string.Format(
            "[MazeVisionSetup] 完成。\n  头（视野源） {0}   半径 {1} 米\n  脚（玩家）   {2}\n  按压板       {3}\n" +
            "  视野遮挡物   {4} 个\n  围墙高度     {5} 米（调整了 {6} 块）\n" +
            "  碰撞判定     头和脚距离 ≤ {7} 米即结束\n" +
            "  相机请自行对准迷宫；运行后除了视野范围其余会是全黑。",
            headStart, VisionRadius, feetStart, mazeCenter, tagged, WallHeight, wallsRaised, ContactDistance));
    }

    [MenuItem("Tools/Maze/抬高围墙（俯视可见 / 平视挡视线）")]
    public static void RaiseWallsMenu()
    {
        if (!CheckScene()) return;
        int count = RaiseWalls();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log(string.Format("[MazeVisionSetup] 围墙已抬到 {0} 米，调整了 {1} 块。", WallHeight, count));
    }

    /// <summary>
    /// 把所有围墙（外墙 PlatformWall_* + 迷宫墙 WallH_* / WallV_*）抬到 WallHeight 米：
    /// 底面不动，只把高度拉高。抬到「比地面视线高、比高台上的视线低」，
    /// 于是站在高台上能俯视全图，落到地面后视线被围墙截住、看不到外面。
    /// 重复执行是幂等的（按绝对高度算，不叠加）。
    /// </summary>
    private static int RaiseWalls()
    {
        int count = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name;
            bool isMazeWall = n.StartsWith("WallH_") || n.StartsWith("WallV_");
            bool isPlatformWall = n.StartsWith("PlatformWall_");
            if (!isMazeWall && !isPlatformWall) continue;
            if (PerimeterOnly && !isPlatformWall) continue;

            Bounds b = WorldBounds(t.gameObject);
            float height = b.size.y;
            if (height <= 0.0001f) continue;
            if (Mathf.Abs(height - WallHeight) < 0.0005f) continue;   // 已经是目标高度

            float parentY = t.parent != null ? Mathf.Abs(t.parent.lossyScale.y) : 1f;
            if (parentY < 0.0001f) parentY = 1f;

            Undo.RecordObject(t, "抬高围墙");
            Vector3 scale = t.localScale;
            t.localScale = new Vector3(scale.x, scale.y * (WallHeight / height), scale.z);
            Vector3 pos = t.localPosition;
            pos.y += (WallHeight - height) * 0.5f / parentY;           // 底面保持不动
            t.localPosition = pos;
            count++;
        }
        return count;
    }

    [MenuItem("Tools/Maze/清除视野玩法")]
    public static void ClearMenu()
    {
        Clear(true);
    }

    private static void Clear(bool dialog)
    {
        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
            Undo.DestroyObjectImmediate(old);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            if (dialog) Debug.Log("[MazeVisionSetup] 已删除 " + RootName + "。");
        }
        else if (dialog)
        {
            Debug.Log("[MazeVisionSetup] 场景里没有 " + RootName + "，无需清除。");
        }
    }

    private static bool CheckScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("场景不对",
                "请先打开 " + ScenePath + "，当前是：" + (string.IsNullOrEmpty(scene.path) ? "未保存的场景" : scene.path), "好");
            return false;
        }
        return true;
    }

    private static bool TryGetMazeBounds(out Bounds bounds)
    {
        bounds = new Bounds();
        bool found = false;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name;
            bool isMazeWall = n.StartsWith("WallH_") || n.StartsWith("WallV_");
            if (!isMazeWall) continue;
            Bounds b = WorldBounds(t.gameObject);
            if (!found) { bounds = b; found = true; }
            else bounds.Encapsulate(b);
        }
        return found;
    }

    private static int TagObstacles()
    {
        int count = 0;
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name;
            bool blocking = n.StartsWith("WallH_") || n.StartsWith("WallV_") || n.StartsWith("PlatformWall_")
                || n.StartsWith("Entrance") || n == "PlatformBase";
            if (!blocking || t.gameObject.tag == ObstacleTag) continue;
            if (t.GetComponent<Renderer>() == null && t.GetComponentInChildren<Renderer>() == null) continue;
            Undo.RecordObject(t.gameObject, "标记视野遮挡物");
            t.gameObject.tag = ObstacleTag;
            count++;
        }
        return count;
    }

    private static Material FlatMaterial(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        mat.color = color;
        mat.name = System.IO.Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static Bounds WorldBounds(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static void SetFloat(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning("[MazeVisionSetup] 找不到字段 " + field + "（" + target.GetType().Name + "）");
            return;
        }
        p.floatValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning("[MazeVisionSetup] 找不到字段 " + field + "（" + target.GetType().Name + "）");
            return;
        }
        p.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetBool(Object target, string field, bool value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning("[MazeVisionSetup] 找不到字段 " + field + "（" + target.GetType().Name + "）");
            return;
        }
        p.boolValue = value;
        so.ApplyModifiedProperties();
    }

    private static void SetEnumByName(Object target, string field, string name)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null)
        {
            Debug.LogWarning("[MazeVisionSetup] 找不到字段 " + field + "（" + target.GetType().Name + "）");
            return;
        }
        int index = System.Array.IndexOf(p.enumNames, name);
        if (index >= 0) p.enumValueIndex = index;
        so.ApplyModifiedProperties();
    }
}
#endif
