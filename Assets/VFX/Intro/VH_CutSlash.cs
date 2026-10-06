using UnityEngine;

/// <summary>
/// 结尾的「黑屏 → 巨大斩击」—— 表示一刀切开目标（右侧胶囊体）的腰部。
///
/// 时间轴（t = 0 就是**劈中的那一瞬**，负数 = 之前）：
/// <code>
///   t = -blackoutLead ──► 屏幕瞬间全黑（硬切，可调淡入）
///   t =  0            ──► ★ 巨大刀光出现的同一瞬间，屏幕恢复可见
///   0 → fadeIn        ──► 刀光淡入（同时刀身从 openLengthScale 迅速拉开）
///   fadeIn → +hold    ──► 保持
///   → +fadeOut        ──► 淡出
/// </code>
///
/// 两个四边形都是**相机子物体**，都挂到 <see cref="m_Layer"/>（默认 3 = PostExempt）：
///   · 挂相机 ⇒ 永远不会被地形/角色挡住，且屏幕位置对准目标腰部就能读成「切开腰」
///   · PostExempt ⇒ 在 `PC_Renderer` 的 `VH_Bubble_Exempt`（Event 603）里被原样重绘，
///     不受 Comic Ink 去色/网点影响 ⇒ 红是真红、黑是真黑
///
/// 排序靠材质的 renderQueue：刀光 3980 &lt; 黑屏 3999。
/// ⚠️ 注意 `Loongdum/VH Domain Slash` 的 Pass 是 `LightMode = UniversalForward`
///   （不是 SRPDefaultUnlit）—— `VH_Bubble_Exempt` 的 PassNames 留空时
///   RenderObjectsPass 会同时挂上 SRPDefaultUnlit / UniversalForward / UniversalForwardOnly 三个，
///   所以正好覆盖，不用额外配置。
/// </summary>
[AddComponentMenu("Loongdum/VH Cut Slash")]
[DisallowMultipleComponent]
public sealed class VH_CutSlash : MonoBehaviour
{
    // ─────────────────────────── ① 被切的目标 ───────────────────────────

    [Header("① 被切的目标")]
    [Tooltip("要切腰的物体（场景里的 Capsule (1)）。刀光会被摆到它的腰部屏幕位置上。")]
    [SerializeField] Transform m_Target;

    [Tooltip("切点相对目标中心的垂直偏移（米，沿世界 Y）。0 = 目标几何中心。")]
    [SerializeField] float m_WaistOffsetY = 0f;

    [Tooltip("留空则自动用 Camera.main。")]
    [SerializeField] Camera m_Camera;

    // ─────────────────────────── ② 刀光 ───────────────────────────

    [Header("② 刀光（内红外黑）")]
    [Tooltip("刀光材质。用 Loongdum/VH Domain Slash，_CoreColor 给红、_EdgeColor 给黑。")]
    [SerializeField] Material m_BladeMaterial;

    [Tooltip("刀光离相机多远（米）。放在相机前方 ⇒ 永远不会被地形/物件遮挡。")]
    [SerializeField, Min(0.1f)] float m_Distance = 2.0f;

    [Tooltip("★ 刀身长度（屏幕高的倍数，1.0 = 满屏高；16:9 下 1.78 ≈ 满屏宽）。")]
    [SerializeField, Min(0.1f)] float m_LengthInViewH = 2.6f;

    [Tooltip("刀身宽度（屏幕高的倍数）。内含黑边 / 红芯 / 黑边三段。")]
    [SerializeField, Min(0.005f)] float m_WidthInViewH = 0.14f;

    [Tooltip("★ 刀身倾角（度，屏幕内旋转）。0 = 水平切腰；负值 = 左上→右下。")]
    [SerializeField] float m_Angle = -14f;

    // ─────────────────────────── ③ 刀光的节奏 ───────────────────────────

    [Header("③ 刀光节奏（秒，t=0 为劈中瞬间）")]
    [SerializeField, Min(0f)] float m_FadeIn = 0.04f;
    [SerializeField, Min(0f)] float m_Hold = 0.35f;
    [SerializeField, Min(0f)] float m_FadeOut = 0.25f;

    [Tooltip("「张开」时长：刀身从下面的比例长度迅速拉到满（0 = 不张开，直接整根出现）。")]
    [SerializeField, Min(0f)] float m_OpenTime = 0.06f;

    [Tooltip("张开起始的长度比例。0.55 = 从半长开始拉。")]
    [SerializeField, Range(0.05f, 1f)] float m_OpenLengthScale = 0.55f;

    // ─────────────────────────── ④ 黑屏 ───────────────────────────

    [Header("④ 黑屏（斩击来临之前）")]
    [Tooltip("★ 比斩击早多少秒黑屏（秒）。0 = 不黑屏。")]
    [SerializeField, Min(0f)] float m_BlackoutLead = 0.18f;

    [Tooltip("黑屏淡入时长。0 = 瞬间黑（你要的就是这个）。")]
    [SerializeField, Min(0f)] float m_BlackoutFadeIn = 0f;

    [Tooltip("黑屏淡出时长。0 = 瞬间恢复可见（和斩击同一瞬间）。")]
    [SerializeField, Min(0f)] float m_BlackoutFadeOut = 0f;

    [Tooltip("黑屏材质。用 Loongdum/VH Domain Darken，_Color = (0,0,0,1)。")]
    [SerializeField] Material m_BlackoutMaterial;

    [Tooltip("黑屏四边形离相机多远（米）。越小越贴脸。")]
    [SerializeField, Min(0.01f)] float m_BlackoutDistance = 0.4f;

    // ─────────────────────────── ⑤ 图层 ───────────────────────────

    [Header("⑤ 图层")]
    [Tooltip("两个四边形都挂这个层。3 = PostExempt（不被 Comic Ink 影响）。")]
    [SerializeField] int m_Layer = 3;

    [Tooltip("刀光的 sortingOrder（同一 renderQueue 内才起作用，留着方便微调）。")]
    [SerializeField] int m_BladeSortingOrder = 20;

    [SerializeField] int m_BlackoutSortingOrder = 30;

    // ─────────────────────────── 内部状态 ───────────────────────────

    Transform m_Blade;
    Transform m_Blackout;
    MeshRenderer m_BladeRenderer;
    MeshRenderer m_BlackoutRenderer;
    Material m_BladeMat;
    Material m_BlackoutMat;
    Mesh m_QuadMesh;
    float m_BaseCoreAlpha = 1f;
    float m_BaseEdgeAlpha = 0.85f;
    bool m_Prepared;

    /// <summary>当前黑屏不透明度（0~1）。</summary>
    public float BlackoutAlpha { get; private set; }

    /// <summary>当前刀光不透明度（0~1）。</summary>
    public float BladeAlpha { get; private set; }

    /// <summary>运行期四边形是否已建好。</summary>
    public bool IsPrepared { get { return m_Prepared; } }

    /// <summary>从「开始黑屏」到「刀光彻底淡完」的总时长（秒）。</summary>
    public float TotalDuration
    {
        get
        {
            return Mathf.Max(0f, m_BlackoutLead)
                 + Mathf.Max(0f, m_FadeIn)
                 + Mathf.Max(0f, m_Hold)
                 + Mathf.Max(0f, m_FadeOut);
        }
    }

    // ─────────────────────────── 对外 API ───────────────────────────

    /// <summary>按「相对劈中瞬间」的时间 t 摆状态。幂等。</summary>
    public void Evaluate(float t)
    {
        Prepare();
        BlackoutAlpha = BlackoutAlphaAt(t);
        BladeAlpha = BladeAlphaAt(t);
        ApplyBlackout(BlackoutAlpha, t);
        ApplyBlade(BladeAlpha, t);
    }

    /// <summary>把两个四边形收掉（编辑器里验证完、或演出结束时用）。</summary>
    public void Cleanup()
    {
        Kill(m_Blade != null ? m_Blade.gameObject : null);
        Kill(m_Blackout != null ? m_Blackout.gameObject : null);
        Kill(m_BladeMat);
        Kill(m_BlackoutMat);
        Kill(m_QuadMesh);
        m_Blade = null;
        m_Blackout = null;
        m_BladeRenderer = null;
        m_BlackoutRenderer = null;
        m_BladeMat = null;
        m_BlackoutMat = null;
        m_QuadMesh = null;
        m_Prepared = false;
    }

    /// <summary>黑屏在 t 时刻的不透明度。</summary>
    public float BlackoutAlphaAt(float t)
    {
        if (m_BlackoutLead <= 0f && m_BlackoutFadeOut <= 0f) return 0f;

        float start = -Mathf.Max(0f, m_BlackoutLead);
        if (t < start) return 0f;

        if (t < 0f)
        {
            float fin = Mathf.Max(0f, m_BlackoutFadeIn);
            return fin <= 0f ? 1f : Mathf.Clamp01((t - start) / fin);
        }

        float fout = Mathf.Max(0f, m_BlackoutFadeOut);
        return fout <= 0f ? 0f : Mathf.Clamp01(1f - t / fout);
    }

    /// <summary>刀光在 t 时刻的不透明度。</summary>
    public float BladeAlphaAt(float t)
    {
        if (t < 0f) return 0f;

        float fin = Mathf.Max(0f, m_FadeIn);
        if (fin > 0f && t < fin) return t / fin;

        float holdEnd = fin + Mathf.Max(0f, m_Hold);
        if (t <= holdEnd) return 1f;

        float fout = Mathf.Max(0f, m_FadeOut);
        if (fout > 0f && t < holdEnd + fout) return 1f - (t - holdEnd) / fout;
        return 0f;
    }

    void OnDestroy()
    {
        Cleanup();
    }

    // ─────────────────────────── 建四边形 ───────────────────────────

    void Prepare()
    {
        if (m_Prepared) return;

        if (m_Camera == null) m_Camera = Camera.main;
        if (m_Camera == null) return;
        if (m_BladeMaterial == null || m_BlackoutMaterial == null) return;

        m_QuadMesh = BuildQuadMesh();

        m_BladeMat = new Material(m_BladeMaterial) { name = m_BladeMaterial.name + " (runtime)" };
        m_BaseCoreAlpha = m_BladeMat.GetFloat("_CoreAlpha");
        m_BaseEdgeAlpha = m_BladeMat.GetFloat("_EdgeAlpha");

        m_BlackoutMat = new Material(m_BlackoutMaterial) { name = m_BlackoutMaterial.name + " (runtime)" };

        m_Blade = MakeQuad("VH_CutSlash_Blade", m_BladeMat, m_BladeSortingOrder, out m_BladeRenderer);
        m_Blackout = MakeQuad("VH_CutSlash_Blackout", m_BlackoutMat, m_BlackoutSortingOrder, out m_BlackoutRenderer);

        m_Prepared = true;
    }

    Transform MakeQuad(string name, Material mat, int sortingOrder, out MeshRenderer renderer)
    {
        var go = new GameObject(name);
        go.hideFlags = HideFlags.DontSave;
        go.layer = m_Layer;
        go.transform.SetParent(m_Camera.transform, false);

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = m_QuadMesh;

        renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = mat;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        return go.transform;
    }

    static Mesh BuildQuadMesh()
    {
        var mesh = new Mesh { name = "VH_CutSlash Quad", hideFlags = HideFlags.DontSave };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f),
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f),
        };
        mesh.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // ─────────────────────────── 摆放 ───────────────────────────

    void ApplyBlackout(float alpha, float t)
    {
        if (m_Blackout == null || m_BlackoutRenderer == null) return;

        m_BlackoutRenderer.enabled = alpha > 0.001f;
        if (!m_BlackoutRenderer.enabled) return;

        float d = Mathf.Max(0.01f, m_BlackoutDistance);
        float viewH = 2f * d * Mathf.Tan(m_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewW = viewH * m_Camera.aspect;

        m_Blackout.localRotation = Quaternion.identity;
        m_Blackout.localPosition = new Vector3(0f, 0f, d);
        m_Blackout.localScale = new Vector3(viewW * 1.2f, viewH * 1.2f, 1f);

        m_BlackoutMat.SetColor("_Color", new Color(0f, 0f, 0f, Mathf.Clamp01(alpha)));
    }

    void ApplyBlade(float alpha, float t)
    {
        if (m_Blade == null || m_BladeRenderer == null) return;

        m_BladeRenderer.enabled = alpha > 0.001f;
        if (!m_BladeRenderer.enabled) return;

        // 屏幕位置对准目标腰部
        Vector3 waist = m_Target != null
            ? m_Target.position + Vector3.up * m_WaistOffsetY
            : m_Camera.transform.position + m_Camera.transform.forward * 10f;
        Vector3 vp = m_Camera.WorldToViewportPoint(waist);

        float d = Mathf.Max(0.1f, m_Distance);
        float viewH = 2f * d * Mathf.Tan(m_Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewW = viewH * m_Camera.aspect;

        m_Blade.localPosition = new Vector3((vp.x - 0.5f) * viewW, (vp.y - 0.5f) * viewH, d);
        m_Blade.localRotation = Quaternion.Euler(0f, 0f, m_Angle);

        // 「张开」：前半段把刀身从 openLengthScale 拉到满
        float openK = 1f;
        if (m_OpenTime > 0f && t < m_OpenTime)
            openK = Mathf.Lerp(m_OpenLengthScale, 1f, Mathf.Clamp01(t / m_OpenTime));

        m_Blade.localScale = new Vector3(viewH * m_LengthInViewH * openK, viewH * m_WidthInViewH, 1f);

        // 预乘 shader 的不透明度靠这两个 uniform 乘出来（顶点色 alpha 在静态 mesh 上固定为 1）
        m_BladeMat.SetFloat("_CoreAlpha", m_BaseCoreAlpha * alpha);
        m_BladeMat.SetFloat("_EdgeAlpha", m_BaseEdgeAlpha * alpha);
    }

    static void Kill(UnityEngine.Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(o);
        else UnityEngine.Object.DestroyImmediate(o);
    }
}
