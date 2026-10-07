using System.Reflection;
using UnityEngine;

/// <summary>
/// 把 <see cref="VisionPortal"/> 的「不可见光门口径」对齐到镜面（mirror2）的真实轮廓，
/// 让视野遮罩的明暗边界正好压在镜面上 —— 视线看起来是从镜子里传导出来的。
///
/// 只移动不可见的功能口径：口径位移之后，所有可见子物体（镜面、出口门框等）
/// 会被还原到原来的世界变换，因此美术摆位一点不动。
///
/// 用法：挂在带 VisionPortal 的物体上，Inspector 右键 → 「对齐光门口到镜面」。
/// 也可以勾 autoFitOnPlay，进入 Play 时自动重算一次。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(VisionPortal))]
public sealed class VisionPortalMirrorFit : MonoBehaviour
{
    [Header("目标镜面")]
    [Tooltip("镜面物体。留空则自动取名为 mirror2 的直接子物体。")]
    [SerializeField] private Transform mirror;

    [Header("口径")]
    [Tooltip("口径比镜面轮廓向外放宽的量（米）。0 = 严丝合缝贴住镜面侧边。")]
    [SerializeField, Min(0f)] private float padding = 0f;

    [Tooltip("口径平面沿视线方向再往后留的余量（米）。0 = 正好放在镜面最后端；留一点可避免边缘闪烁。")]
    [SerializeField] private float depthOffset = 0.02f;

    [Tooltip("勾上则同时把口径宽度改成镜面宽度；关掉则只挪位置、保留现在的宽度。")]
    [SerializeField] private bool fitWidth = true;

    [Header("自动化")]
    [Tooltip("进入 Play 时自动重算一次，换场景或挪动镜面后依然贴合。")]
    [SerializeField] private bool autoFitOnPlay = true;

    [Header("状态（只读）")]
    [Tooltip("上次对齐后的口径宽度。")]
    [SerializeField] private float lastFittedWidth;

    private static readonly FieldInfo WidthField =
        typeof(VisionPortal).GetField("width", BindingFlags.NonPublic | BindingFlags.Instance);

    private void OnEnable()
    {
        if (autoFitOnPlay && Application.isPlaying)
            Fit();
    }

    /// <summary>把光门口径对齐到镜面轮廓。可重复执行，收敛后是无操作。</summary>
    [ContextMenu("对齐光门口到镜面")]
    public void Fit()
    {
        VisionPortal portal = GetComponent<VisionPortal>();
        if (portal == null)
            return;

        if (mirror == null)
            mirror = transform.Find("mirror2");
        if (mirror == null)
        {
            Debug.LogWarning("[VisionPortalMirrorFit] 找不到镜面物体（默认名字 mirror2），已跳过：" + name, this);
            return;
        }

        Vector3 right = portal.Right;
        Vector3 forward = portal.Forward;
        if (right.sqrMagnitude < 0.5f || forward.sqrMagnitude < 0.5f)
            return;

        Vector3 origin = transform.position;

        // 收集镜面所有顶点在「光门坐标系」里的横向 / 纵深范围。
        float lateralMin = float.MaxValue, lateralMax = float.MinValue;
        float depthMin = float.MaxValue;
        bool any = false;
        foreach (MeshFilter filter in mirror.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
                continue;
            Vector3[] vertices = mesh.vertices;
            Matrix4x4 localToWorld = filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 offset = localToWorld.MultiplyPoint3x4(vertices[i]) - origin;
                float lateral = Vector3.Dot(offset, right);
                float depth = Vector3.Dot(offset, forward);
                if (lateral < lateralMin) lateralMin = lateral;
                if (lateral > lateralMax) lateralMax = lateral;
                if (depth < depthMin) depthMin = depth;
                any = true;
            }
        }
        if (!any)
        {
            Debug.LogWarning("[VisionPortalMirrorFit] 镜面没有任何 MeshFilter/顶点，已跳过：" + name, this);
            return;
        }

        // 先记下所有可见子物体的世界变换，口径挪完之后原样还原。
        int childCount = transform.childCount;
        var worldPositions = new Vector3[childCount];
        var worldRotations = new Quaternion[childCount];
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            worldPositions[i] = child.position;
            worldRotations[i] = child.rotation;
        }

        // 口径横向中心 = 镜面轮廓中心；口径平面 = 镜面最后端再往后留 depthOffset。
        float lateralCenter = (lateralMin + lateralMax) * 0.5f;
        float planeDepth = depthMin - depthOffset;
        Vector3 target = origin + right * lateralCenter + forward * planeDepth;

        // 只改水平位置：Y 与口径高度保持不变。竖向只是「射线能不能穿过」的闸门，
        // 保持原样可以避免视线高度变化后光门突然不通。
        Vector3 newPosition = new Vector3(target.x, origin.y, target.z);

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(transform, "对齐光门口到镜面");
        UnityEditor.Undo.RecordObject(portal, "对齐光门口到镜面");
#endif
        transform.position = newPosition;

        float width = fitWidth
            ? (lateralMax - lateralMin) + padding * 2f
            : portal.HalfWidth * 2f;
        if (WidthField != null)
            WidthField.SetValue(portal, width);
        lastFittedWidth = width;

        // 可见美术还原：镜面、出口门框、指示条的位置一律不动。
        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            child.position = worldPositions[i];
            child.rotation = worldRotations[i];
        }

        // 只在编辑态标脏；运行时的自动重算由退出 Play 自动回滚，不应该污染场景。
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(transform);
            UnityEditor.EditorUtility.SetDirty(portal);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void OnValidate()
    {
        if (padding < 0f) padding = 0f;
    }
}
