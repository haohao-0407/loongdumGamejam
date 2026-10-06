// MazeBuilder.cs
// 用法：
//   1. 在场景里新建一个空物体（如 MazeRoot），挂上本脚本。
//   2. 把“高墙 Cube 的正中间”那个点（可用空物体标出）拖到 startPoint。
//   3. 把高墙 Cube 拖到 wallCube、名为 plane 的地面拖到 groundPlane。
//   4. 菜单 Tools > 生成 5x5 迷宫。
//
// 生成内容（全部挂在 GeneratedMaze 下）：
//   - 迷宫：以 startPoint.position 为中心向外生成 5x5 网格迷宫，
//     墙高 = 高墙 Cube 高出地面 Plane 的高度的一半。
//   - 外围平台：一块平坦底座 + 一圈围墙；围墙正好落在迷宫最外圈上，
//     尺寸 = 网格数 x CellSize，边缘与迷宫外沿严格对齐、没有缝隙。
//     围墙在其中一面（entranceSide）的中间一格留出入口缺口，也就是迷宫入口。
//   - 入口高墙：贴着入口缺口外侧生成一堵高墙，顶面当成小平台。
//     高墙宽度 = 入口缺口宽度（两侧边缘与缺口边缘重合），向内贴住平台外沿，
//     向外延伸 entrancePlatformDepth。顶面三面（左、右、外）加围栏，
//     只有朝向迷宫的一面敞开；从顶面掉下去只会落进入口、也就是迷宫内部，
//     不会落到迷宫墙顶，也不会掉出地图。
//   - 墙体统一用 Cube，灰色材质。

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

public class MazeBuilder : MonoBehaviour
{
    // 入口缺口开在哪一面墙上
    public enum EntranceSide
    {
        NegativeZ = 0, // -Z 面（默认，正对 Scene 视角前方）
        PositiveZ = 1, // +Z 面
        NegativeX = 2, // -X 面
        PositiveX = 3, // +X 面
    }

    [Header("起点：放在高墙 Cube 的正中间")]
    public Transform startPoint;

    [Header("高度参考物")]
    [Tooltip("高墙 Cube：墙体高度取其高出地面的高度的一半")]
    public Transform wallCube;

    [Tooltip("地面（名为 plane 的物体）")]
    public Transform groundPlane;

    [Header("迷宫参数")]
    [Tooltip("找不到 wallCube 时使用的墙体高度")]
    public float fallbackWallHeight = 1f;

    [Tooltip("单格尺寸")]
    public float cellSize = 1f;

    [Tooltip("墙体厚度")]
    public float wallThickness = 0.1f;

    [Tooltip("随机种子；0 表示每次随机")]
    public int seed = 0;

    [Header("外围平台")]
    [Tooltip("是否生成外围平台（底座 + 围墙）")]
    public bool buildPlatform = true;

    [Tooltip("平台底座的厚度")]
    public float platformBaseThickness = 0.1f;

    [Tooltip("入口缺口开在哪一面墙上")]
    public EntranceSide entranceSide = EntranceSide.NegativeZ;

    [Header("入口高墙 / 小平台")]
    [Tooltip("是否在入口外侧生成一堵高墙（顶面当作小平台）")]
    public bool buildEntranceWall = true;

    [Tooltip("入口小平台向外延伸的深度（世界单位）")]
    public float entrancePlatformDepth = 1f;

    [Tooltip("入口高墙高度 = 迷宫墙高 x 该倍数（2 = 与场景里的高墙 Cube 同高）")]
    public float entranceWallHeightScale = 2f;

    [Tooltip("入口小平台围栏高度 = 迷宫墙高 x 该倍数")]
    public float entranceRailingHeightScale = 1f;

    public const int GridSize = 5;
    const string RootName = "GeneratedMaze";

#if UNITY_EDITOR
    [MenuItem("Tools/生成 5x5 迷宫")]
    static void BuildFromMenu()
    {
        MazeBuilder builder = Object.FindFirstObjectByType<MazeBuilder>();
        if (builder == null)
        {
            Debug.LogError("[MazeBuilder] 场景里没有 MazeBuilder 组件。请新建一个空物体，挂上本脚本，指定 startPoint 后再点菜单。");
            return;
        }

        builder.Generate();
    }
#endif

    public void Generate()
    {
        if (startPoint == null) startPoint = transform;
        if (startPoint == null)
        {
            Debug.LogError("[MazeBuilder] 没有设置 startPoint。");
            return;
        }

        Vector3 origin = startPoint.position;

        // 地面高度：优先取 plane 顶面，其次默认 y = 0。
        float planeTop = groundPlane != null ? GetTopY(groundPlane) : 0f;

        // 墙体高度 = 高墙 Cube 高出地面的高度的一半。
        float wallHeight;
        if (wallCube != null)
            wallHeight = Mathf.Max(0.01f, (GetTopY(wallCube) - planeTop) * 0.5f);
        else
        {
            wallHeight = Mathf.Max(0.01f, fallbackWallHeight);
            Debug.LogWarning("[MazeBuilder] 没有指定 wallCube，改用 fallbackWallHeight = " + fallbackWallHeight + "。");
        }

        int n = GridSize;

        // wallV[bx, z]：x 方向第 bx 条边界、第 z 行的竖直墙（bx = 0..n）。
        // wallH[x, bz]：第 x 列、z 方向第 bz 条边界的水平墙（bz = 0..n）。
        // true = 有墙，false = 已打通。
        bool[,] wallV = new bool[n + 1, n];
        bool[,] wallH = new bool[n, n + 1];
        for (int x = 0; x <= n; x++)
            for (int z = 0; z < n; z++)
                wallV[x, z] = true;
        for (int x = 0; x < n; x++)
            for (int z = 0; z <= n; z++)
                wallH[x, z] = true;

        CarveMaze(wallV, wallH);

        // 清掉上一次的生成结果。
        GameObject old = GameObject.Find(RootName);
        if (old != null)
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(old);
#else
            Destroy(old);
#endif
        }

        GameObject root = new GameObject(RootName);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(root, "Generate 5x5 Maze");
#endif

        Material wallMat = CreateGreyMaterial();

        float centerOffset = (n - 1) * 0.5f;
        float halfSpan = n * 0.5f;

        // 平台底座顶面的高度：迷宫墙和围墙都立在底座上。
        float baseTop = planeTop + (buildPlatform ? platformBaseThickness : 0f);
        float midY = baseTop + wallHeight * 0.5f;

        // ---- 1) 迷宫内墙 ----
        for (int z = 0; z < n; z++)
        {
            for (int bx = 0; bx <= n; bx++)
            {
                if (!wallV[bx, z]) continue;
                // 最外圈由平台围墙负责，避免两堵墙重叠。
                if (buildPlatform && (bx == 0 || bx == n)) continue;

                float px = origin.x + (bx - halfSpan) * cellSize;
                float pz = origin.z + (z - centerOffset) * cellSize;
                CreateWall(root, wallMat, "WallV_" + bx + "_" + z,
                    new Vector3(px, midY, pz),
                    new Vector3(wallThickness, wallHeight, cellSize + wallThickness));
            }
        }

        for (int x = 0; x < n; x++)
        {
            for (int bz = 0; bz <= n; bz++)
            {
                if (!wallH[x, bz]) continue;
                if (buildPlatform && (bz == 0 || bz == n)) continue;

                float px = origin.x + (x - centerOffset) * cellSize;
                float pz = origin.z + (bz - halfSpan) * cellSize;
                CreateWall(root, wallMat, "WallH_" + x + "_" + bz,
                    new Vector3(px, midY, pz),
                    new Vector3(cellSize + wallThickness, wallHeight, wallThickness));
            }
        }

        // ---- 2) 外围平台 ----
        if (buildPlatform)
            BuildPlatform(root, wallMat, origin, planeTop, baseTop, wallHeight, n);

        // ---- 3) 入口高墙 / 小平台 ----
        if (buildPlatform && buildEntranceWall)
            BuildEntranceWall(root, wallMat, origin, planeTop, wallHeight, n);

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[MazeBuilder] 已生成 5x5 迷宫，墙体高度 = " + wallHeight
            + (buildPlatform ? "（含外围平台" + (buildEntranceWall ? " + 入口高墙" : "") + "，入口在 " + entranceSide + "）" : "")
            + "。场景已标记为已修改，记得 Ctrl+S 手动保存。");
#else
        Debug.Log("[MazeBuilder] 已生成 5x5 迷宫，墙体高度 = " + wallHeight);
#endif
    }

    // 外围平台：平坦底座 + 一圈围墙，边缘和迷宫外沿严格对齐，留一个入口缺口。
    void BuildPlatform(GameObject root, Material mat, Vector3 origin, float planeTop, float baseTop, float wallHeight, int n)
    {
        float cell = cellSize;
        float wt = wallThickness;
        float centerOffset = (n - 1) * 0.5f;
        float halfSpan = n * 0.5f;
        float midY = baseTop + wallHeight * 0.5f;

        // 底座尺寸 = 迷宫总尺寸 + 一个墙厚，正好和围墙外沿齐平，不留缝。
        float baseSize = n * cell + wt;

        // 1) 平坦底座
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "PlatformBase";
        floor.transform.SetParent(root.transform, false);
        floor.transform.position = new Vector3(origin.x, planeTop + platformBaseThickness * 0.5f, origin.z);
        floor.transform.localScale = new Vector3(baseSize, platformBaseThickness, baseSize);

        MeshRenderer floorRend = floor.GetComponent<MeshRenderer>();
        if (floorRend != null) floorRend.sharedMaterial = mat;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(floor, "Generate 5x5 Maze");
#endif

        int gap = n / 2;

        // 2) 两条竖边（bx = 0 / bx = n），沿 z 分段；缺口在中间那一格。
        for (int z = 0; z < n; z++)
        {
            float pz = origin.z + (z - centerOffset) * cell;

            if (!(entranceSide == EntranceSide.NegativeX && z == gap))
                CreateWall(root, mat, "PlatformWall_X0_" + z,
                    new Vector3(origin.x - halfSpan * cell, midY, pz),
                    new Vector3(wt, wallHeight, cell + wt));

            if (!(entranceSide == EntranceSide.PositiveX && z == gap))
                CreateWall(root, mat, "PlatformWall_Xn_" + z,
                    new Vector3(origin.x + halfSpan * cell, midY, pz),
                    new Vector3(wt, wallHeight, cell + wt));
        }

        // 3) 两条横边（bz = 0 / bz = n），沿 x 分段；缺口在中间那一格。
        for (int x = 0; x < n; x++)
        {
            float px = origin.x + (x - centerOffset) * cell;

            if (!(entranceSide == EntranceSide.NegativeZ && x == gap))
                CreateWall(root, mat, "PlatformWall_Z0_" + x,
                    new Vector3(px, midY, origin.z - halfSpan * cell),
                    new Vector3(cell + wt, wallHeight, wt));

            if (!(entranceSide == EntranceSide.PositiveZ && x == gap))
                CreateWall(root, mat, "PlatformWall_Zn_" + x,
                    new Vector3(px, midY, origin.z + halfSpan * cell),
                    new Vector3(cell + wt, wallHeight, wt));
        }
    }

    // 入口高墙：贴着入口缺口外侧的一堵高墙，顶面是小平台；
    // 宽度 = 缺口宽度，内侧贴平台外沿，向外延伸 entrancePlatformDepth；
    // 顶面左/右/外三面加围栏，朝向迷宫的一面敞开，保证掉下去落进迷宫。
    void BuildEntranceWall(GameObject root, Material mat, Vector3 origin, float planeTop, float wallHeight, int n)
    {
        float cell = cellSize;
        float wt = wallThickness;
        float halfSpan = n * 0.5f;

        float gapWidth = Mathf.Max(0.01f, cell - wt);                       // 与入口缺口等宽
        float depth = Mathf.Max(0.01f, entrancePlatformDepth);             // 向外延伸的深度
        float tallHeight = Mathf.Max(0.01f, wallHeight * entranceWallHeightScale);
        float railHeight = Mathf.Max(0.01f, wallHeight * entranceRailingHeightScale);

        float baseOuter = halfSpan * cell + wt * 0.5f;                      // 平台外沿到中心的距离
        float halfGap = gapWidth * 0.5f;

        float blockMidY = planeTop + tallHeight * 0.5f;
        float railMidY = planeTop + tallHeight + railHeight * 0.5f;

        bool alongZ = entranceSide == EntranceSide.NegativeZ || entranceSide == EntranceSide.PositiveZ;
        float sign = (entranceSide == EntranceSide.PositiveZ || entranceSide == EntranceSide.PositiveX) ? 1f : -1f;

        if (alongZ)
        {
            float innerZ = origin.z + sign * baseOuter;   // 内侧：贴住平台外沿
            float outerZ = innerZ + sign * depth;         // 外侧：向外延伸
            float blockZ = (innerZ + outerZ) * 0.5f;

            // 高墙块（顶面即小平台）
            CreateWall(root, mat, "EntranceHighWall",
                new Vector3(origin.x, blockMidY, blockZ),
                new Vector3(gapWidth, tallHeight, depth));

            // 左右围栏（沿 z），内端与平台外沿齐平
            CreateWall(root, mat, "EntranceRail_Left",
                new Vector3(origin.x - halfGap + wt * 0.5f, railMidY, blockZ),
                new Vector3(wt, railHeight, depth));

            CreateWall(root, mat, "EntranceRail_Right",
                new Vector3(origin.x + halfGap - wt * 0.5f, railMidY, blockZ),
                new Vector3(wt, railHeight, depth));

            // 外侧围栏（沿 x）
            CreateWall(root, mat, "EntranceRail_Outer",
                new Vector3(origin.x, railMidY, outerZ - sign * wt * 0.5f),
                new Vector3(gapWidth, railHeight, wt));
        }
        else
        {
            float innerX = origin.x + sign * baseOuter;
            float outerX = innerX + sign * depth;
            float blockX = (innerX + outerX) * 0.5f;

            CreateWall(root, mat, "EntranceHighWall",
                new Vector3(blockX, blockMidY, origin.z),
                new Vector3(depth, tallHeight, gapWidth));

            CreateWall(root, mat, "EntranceRail_Left",
                new Vector3(blockX, railMidY, origin.z - halfGap + wt * 0.5f),
                new Vector3(depth, railHeight, wt));

            CreateWall(root, mat, "EntranceRail_Right",
                new Vector3(blockX, railMidY, origin.z + halfGap - wt * 0.5f),
                new Vector3(depth, railHeight, wt));

            CreateWall(root, mat, "EntranceRail_Outer",
                new Vector3(outerX - sign * wt * 0.5f, railMidY, origin.z),
                new Vector3(wt, railHeight, gapWidth));
        }
    }

    // 从正中间的格子开始，用深度优先回溯算法向外打通墙，生成一个 5x5 迷宫。
    void CarveMaze(bool[,] wallV, bool[,] wallH)
    {
        int n = GridSize;
        bool[,] visited = new bool[n, n];
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        List<Vector2Int> candidates = new List<Vector2Int>(4);

        Vector2Int[] dirs =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
        };

        int cx = n / 2;
        int cz = n / 2;
        visited[cx, cz] = true;
        stack.Push(new Vector2Int(cx, cz));

        System.Random rng = seed == 0 ? new System.Random() : new System.Random(seed);

        while (stack.Count > 0)
        {
            Vector2Int cur = stack.Peek();
            candidates.Clear();

            for (int i = 0; i < dirs.Length; i++)
            {
                int nx = cur.x + dirs[i].x;
                int nz = cur.y + dirs[i].y;
                if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                if (visited[nx, nz]) continue;
                candidates.Add(dirs[i]);
            }

            if (candidates.Count == 0)
            {
                stack.Pop();
                continue;
            }

            Vector2Int dir = candidates[rng.Next(candidates.Count)];
            if (dir.x == 1) wallV[cur.x + 1, cur.y] = false;
            else if (dir.x == -1) wallV[cur.x, cur.y] = false;
            else if (dir.y == 1) wallH[cur.x, cur.y + 1] = false;
            else wallH[cur.x, cur.y] = false;

            int tx = cur.x + dir.x;
            int tz = cur.y + dir.y;
            visited[tx, tz] = true;
            stack.Push(new Vector2Int(tx, tz));
        }
    }

    static void CreateWall(GameObject root, Material mat, string name, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        MeshRenderer rend = go.GetComponent<MeshRenderer>();
        if (rend != null) rend.sharedMaterial = mat;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Generate 5x5 Maze");
#endif
    }

    // 取物体（含子物体）包围盒顶面的世界 y。
    static float GetTopY(Transform t)
    {
        if (t == null) return 0f;

        Collider col = t.GetComponentInChildren<Collider>();
        if (col != null) return col.bounds.max.y;

        Renderer rend = t.GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.max.y;

        return t.position.y;
    }

    static Material CreateGreyMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        Material mat = new Material(shader);
        Color grey = new Color(0.5f, 0.5f, 0.5f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", grey);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", grey);
        mat.color = grey;
        return mat;
    }
}
