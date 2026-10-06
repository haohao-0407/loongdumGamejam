#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 把选中的墙截短，只保留「伸出去的部分」被切掉之后剩下的那一段。
///
/// 两种模式，对着不同的参照物裁：
///   伸到围墙外 → 参照 BasePlatform（围墙以内的地面），把墙截到围墙里
///   伸到方块外 → 参照 Step_1 / Step_2 这些台阶方块，把墙截到方块范围内
///
/// 只动墙沿自己长轴的长度和位置，厚度、高度、朝向、另外一端都不变。
/// 用法：在 Hierarchy 里选中要截的墙（可多选），然后点对应菜单。
/// </summary>
public static class StairsDividerTrim
{
    private const string ScenePath = "Assets/Scenes/Stairs.unity";

    [MenuItem("Tools/Stairs/截掉伸出围墙的部分（选中物体）")]
    public static void TrimToPlatform()
    {
        Trim(true);
    }

    [MenuItem("Tools/Stairs/截掉伸出方块的部分（选中物体）")]
    public static void TrimToSteps()
    {
        Trim(false);
    }

    private static void Trim(bool toPlatform)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            EditorUtility.DisplayDialog("场景不对",
                "请先打开 " + ScenePath + "，当前是：" + (string.IsNullOrEmpty(scene.path) ? "未保存的场景" : scene.path), "好");
            return;
        }

        var targets = new List<Transform>();
        foreach (Transform t in Selection.transforms)
        {
            if (t.GetComponent<Renderer>() != null) targets.Add(t);
        }

        var reference = new List<Transform>();
        if (toPlatform)
        {
            GameObject platform = GameObject.Find("BasePlatform");
            if (platform != null) reference.Add(platform.transform);
        }
        else
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t.GetComponent<Renderer>() != null && IsStep(t.name)) reference.Add(t);
            }
        }

        if (targets.Count == 0 || reference.Count == 0)
        {
            EditorUtility.DisplayDialog("先选中要截的墙",
                toPlatform
                    ? "在 Hierarchy 里选中伸出围墙的墙（可多选），再点菜单。\n参照物：BasePlatform（围墙以内的地面）。"
                    : "在 Hierarchy 里选中伸出方块外的墙（可多选），再点菜单。\n参照物：Step_1 / Step_2 这些台阶方块。", "好");
            return;
        }

        int changed = 0;
        var report = new System.Text.StringBuilder();
        foreach (Transform t in targets)
        {
            float before;
            float after;
            if (TrimOne(t, reference, out before, out after))
            {
                changed++;
                report.AppendFormat("\n  {0}：{1:F2} → {2:F2} 米", t.name, before, after);
            }
            else
            {
                report.AppendFormat("\n  {0}：已经在范围内，跳过", t.name);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(string.Format("[StairsDividerTrim] 按「{0}」处理了 {1} 个物体，改动 {2} 个。{3}",
            toPlatform ? "围墙范围" : "方块范围", targets.Count, changed, report));
    }

    private static bool IsStep(string name)
    {
        if (!name.StartsWith("Step_")) return false;      // StepDividerWall 不匹配
        if (name.Length <= 5) return false;
        return char.IsDigit(name[5]);
    }

    private static bool TrimOne(Transform t, List<Transform> reference, out float before, out float after)
    {
        before = 0f;
        after = 0f;

        // 长轴 = 局部缩放更大的那一轴（隔墙是 长 × 高 × 厚，长轴要么 X 要么 Z）
        int axis = Mathf.Abs(t.localScale.x) >= Mathf.Abs(t.localScale.z) ? 0 : 2;
        Vector3 dir = axis == 0 ? t.right : t.forward;
        dir.y = 0f;
        if (dir.sqrMagnitude < 1e-6f) return false;
        dir.Normalize();

        float curMin, curMax;
        Project(t, dir, out curMin, out curMax);

        float refMin = float.PositiveInfinity, refMax = float.NegativeInfinity;
        foreach (Transform other in reference)
        {
            float lo, hi;
            Project(other, dir, out lo, out hi);
            refMin = Mathf.Min(refMin, lo);
            refMax = Mathf.Max(refMax, hi);
        }

        float curLen = curMax - curMin;
        float keepMin = Mathf.Max(curMin, refMin);   // 只保留和参照物重叠的那一段
        float keepMax = Mathf.Min(curMax, refMax);
        float keepLen = keepMax - keepMin;
        before = curLen;
        after = Mathf.Max(0f, keepLen);
        if (curLen <= 0.0001f) return false;
        if (keepLen <= 0.001f) return false;                        // 整面墙都在参照物之外
        if (Mathf.Abs(curLen - keepLen) < 0.001f) return false;      // 本来就没伸出来

        Undo.RecordObject(t, "截掉隔墙伸出部分");

        Vector3 scale = t.localScale;
        scale[axis] *= keepLen / curLen;
        t.localScale = scale;

        // 缩放是绕自身中心做的，中心没动，所以只需把中心沿 dir 挪到该保留段的中心
        float delta = (keepMin + keepMax) * 0.5f - (curMin + curMax) * 0.5f;
        t.position += dir * delta;
        return true;
    }

    /// <summary>把一个立方体物体投影到 dir 方向上的范围（用真实尺寸，不受父物体旋转导致的包围盒放大影响）。</summary>
    private static void Project(Transform t, Vector3 dir, out float min, out float max)
    {
        Vector3 c = t.position;
        Vector3 ex = t.right * (t.lossyScale.x * 0.5f);
        Vector3 ey = t.up * (t.lossyScale.y * 0.5f);
        Vector3 ez = t.forward * (t.lossyScale.z * 0.5f);

        min = float.PositiveInfinity;
        max = float.NegativeInfinity;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c
                + ((i & 1) == 0 ? -ex : ex)
                + ((i & 2) == 0 ? -ey : ey)
                + ((i & 4) == 0 ? -ez : ez);
            float d = Vector3.Dot(corner, dir);
            min = Mathf.Min(min, d);
            max = Mathf.Max(max, d);
        }
    }
}
#endif
