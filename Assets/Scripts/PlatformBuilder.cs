// PlatformBuilder.cs
// 独立的平台结构生成脚本，不含任何迷宫 / 玩法逻辑。
//
// 生成内容（全部挂在 GeneratedPlatform 下，全部用 Cube 图元，标准灰色材质）：
//   1. 底座平台  : platformSize x platformSize，厚 baseThickness，底面贴在 centerPosition.y。
//   2. 外围围墙  : 四面封闭、无缺口；内侧面贴平台边缘，向外伸出 outerWallThickness；
//                  墙顶比最高台阶再高 wallHeightAboveTopStep（也可用 outerWallHeight 直接覆盖）。
//   3. 内部台阶  : stepCount 级，每级抬高 stepHeight；每级水平尺寸 stepSize x stepSize；
//                  位于平台左上角（-X / +Z），靠角的那一级最高，沿 -Z 向平台内部依次降低。
//   4. 台阶隔墙  : 位于第 1、2 级台阶交界处；长度默认 = stepSize，高度默认 = 外围墙高。
//
// 用法：把本脚本挂到任意物体上，调好参数（尤其是 centerPosition），菜单 Tools > 生成平台。

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

public class PlatformBuilder : MonoBehaviour
{
    [Header("通用参数")]
    [Tooltip("平台整体中心坐标：X/Z 为中心，Y 视为底面所在的地面高度")]
    public Vector3 centerPosition = Vector3.zero;

    [Tooltip("底座平台长宽（不含围墙宽度）")]
    public float platformSize = 24f;

    [Tooltip("底座厚度")]
    public float baseThickness = 0.2f;

    [Tooltip("外围墙厚度")]
    public float outerWallThickness = 1f;

    [Tooltip("外围墙高度（相对平台地面）；<=0 表示自动 = 最高台阶 + wallHeightAboveTopStep")]
    public float outerWallHeight = -1f;

    [Tooltip("围墙比最高台阶再高出的量（outerWallHeight <= 0 时生效）")]
    public float wallHeightAboveTopStep = 2f;

    [Tooltip("入口缺口宽度（当前要求无缺口，此参数保留未使用）")]
    public float entranceWidth = 0.6f;

    [Header("内部台阶")]
    [Tooltip("每级台阶的抬高量")]
    public float stepHeight = 4f;

    [Tooltip("台阶级数")]
    public int stepCount = 2;

    [Tooltip("每级台阶的水平尺寸（正方形边长）")]
    public float stepSize = 10f;

    [Tooltip("台阶贴近哪一侧墙：true = -X 侧（左），false = +X 侧（右）")]
    public bool stepsAgainstNegativeX = true;

    [Header("台阶隔墙")]
    [Tooltip("台阶隔墙厚度")]
    public float dividerWallThickness = 0.04f;

    [Tooltip("台阶隔墙长度；<=0 表示自动 = stepSize")]
    public float dividerWallLength = -1f;

    [Tooltip("台阶隔墙高度；<=0 表示自动 = 外围墙高度")]
    public float dividerWallHeight = -1f;

    const string RootName = "GeneratedPlatform";

#if UNITY_EDITOR
    [MenuItem("Tools/生成平台")]
    static void BuildFromMenu()
    {
        PlatformBuilder builder = Object.FindFirstObjectByType<PlatformBuilder>();
        if (builder == null)
        {
            GameObject host = new GameObject("PlatformBuilder");
            Undo.RegisterCreatedObjectUndo(host, "Create PlatformBuilder");
            builder = host.AddComponent<PlatformBuilder>();
            Debug.Log("[PlatformBuilder] 场景里没有 PlatformBuilder，已自动新建一个（centerPosition 在原点）。改好它的参数后再点一次菜单即可。");
        }

        builder.Generate();
    }
#endif

    public void Generate()
    {
        // ---- 生成前先清空旧的 GeneratedPlatform，避免重复堆积 ----
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
        Undo.RegisterCreatedObjectUndo(root, "Generate Platform");
#endif

        Material grey = CreateGreyMaterial();

        int count = Mathf.Max(1, stepCount);
        float wt = outerWallThickness;
        float half = platformSize * 0.5f;

        float baseBottomY = centerPosition.y;                 // 底面贴地
        float floorY = baseBottomY + baseThickness;           // 底座顶面 = 平台地面
        float tallestStep = stepHeight * count;               // 最高台阶顶面（相对平台地面）
        float wallH = outerWallHeight > 0f ? outerWallHeight : tallestStep + wallHeightAboveTopStep;

        if ((float)count * stepSize > platformSize)
            Debug.LogWarning("[PlatformBuilder] 台阶总进深 " + (count * stepSize) + " 超过平台边长 " + platformSize + "，可能会超出平台。");

        // ---- 1) 底座平台 ----
        CreateCube(root, grey, "BasePlatform",
            new Vector3(centerPosition.x, baseBottomY + baseThickness * 0.5f, centerPosition.z),
            new Vector3(platformSize, baseThickness, platformSize));

        // ---- 2) 外围围墙（四面封闭，无缺口）----
        // 内侧面贴在平台边缘（±half），向外伸出 wt；墙底落到地面，墙顶 = 平台地面 + wallH。
        float wallTopY = floorY + wallH;
        float wallMidY = (baseBottomY + wallTopY) * 0.5f;
        float wallMeshH = wallTopY - baseBottomY;
        float offset = half + wt * 0.5f;

        // 前后两面（沿 X 方向），长度补上两个角
        CreateCube(root, grey, "OuterWall_South",
            new Vector3(centerPosition.x, wallMidY, centerPosition.z - offset),
            new Vector3(platformSize + wt * 2f, wallMeshH, wt));

        CreateCube(root, grey, "OuterWall_North",
            new Vector3(centerPosition.x, wallMidY, centerPosition.z + offset),
            new Vector3(platformSize + wt * 2f, wallMeshH, wt));

        // 左右两面（沿 Z 方向），长度刚好接上前后两面
        CreateCube(root, grey, "OuterWall_West",
            new Vector3(centerPosition.x - offset, wallMidY, centerPosition.z),
            new Vector3(wt, wallMeshH, platformSize));

        CreateCube(root, grey, "OuterWall_East",
            new Vector3(centerPosition.x + offset, wallMidY, centerPosition.z),
            new Vector3(wt, wallMeshH, platformSize));

        // ---- 3) 内部台阶：+Z 边、靠一侧墙；靠角的那级最高，向平台内部(-Z)依次降低 ----
        float stepW = stepSize;
        // stepsAgainstNegativeX = true 时贴 -X 侧墙（左），否则贴 +X 侧墙（右）
        float stepCenterX = stepsAgainstNegativeX
            ? centerPosition.x - half + stepW * 0.5f
            : centerPosition.x + half - stepW * 0.5f;

        for (int k = 1; k <= count; k++)
        {
            int fromCorner = k - 1;                                            // 0 = 贴角那一级
            float top = stepHeight * (count - fromCorner);                     // 贴角的是最高级，向内依次降低
            float stepCenterZ = centerPosition.z + half - stepW * (fromCorner + 0.5f);

            CreateCube(root, grey, "Step_" + k,
                new Vector3(stepCenterX, floorY + top * 0.5f, stepCenterZ),
                new Vector3(stepW, top, stepW));
        }

        // ---- 4) 台阶隔墙：贴角级与下一级的交界处 ----
        if (count >= 2)
        {
            float divH = dividerWallHeight > 0f ? dividerWallHeight : wallH;
            float divL = dividerWallLength > 0f ? dividerWallLength : stepSize;
            float borderZ = centerPosition.z + half - stepW;   // 交界线

            CreateCube(root, grey, "StepDividerWall",
                new Vector3(stepCenterX, floorY + divH * 0.5f, borderZ),
                new Vector3(divL, divH, dividerWallThickness));
        }

#if UNITY_EDITOR
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[PlatformBuilder] 已生成平台：底座 " + platformSize + "x" + platformSize
            + "，围墙顶面高 " + wallH + "（相对平台地面），台阶 " + count + " 级 x " + stepSize
            + "，贴 " + (stepsAgainstNegativeX ? "-X" : "+X") + " 侧墙。场景已标记为已修改，记得 Ctrl+S 保存。");
#else
        Debug.Log("[PlatformBuilder] 已生成平台结构。");
#endif
    }

    static void CreateCube(GameObject root, Material mat, string name, Vector3 position, Vector3 scale)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(root.transform, false);
        go.transform.position = position;
        go.transform.localScale = scale;

        MeshRenderer rend = go.GetComponent<MeshRenderer>();
        if (rend != null && mat != null) rend.sharedMaterial = mat;

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Generate Platform");
#endif
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
