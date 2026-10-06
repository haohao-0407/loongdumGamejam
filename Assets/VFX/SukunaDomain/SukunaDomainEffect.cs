// 宿傩·领域展开 —— 释放演出总控
//
// 三层结构：
//   L1 刀光层  ParticleSystem（Billboard 单面片 + VH_DomainSlash）
//   L2 压暗层  相机下的全屏四边形（自带材质，VH_DomainDarken）
//   L3 本脚本  Play() / Stop() 把上面两层串起来
//
// 重要约定：
//  · 脚本是**参数的唯一真源**：每次 Play() 都会把 Inspector 上的数值写进粒子系统
//    的各模块。⇒ 调参请改**本脚本**上的字段，改粒子系统上的数值会被覆盖。
//  · 尺寸参数全部是**视口比例**（相对相机在 m_BoxDistance 处的可视高度），
//    所以换 FOV / 换分辨率都不用重调。
//  · 压暗层不用 Volume：本工程相机后处理是关的（post=False），美术全走 Renderer Feature。
//    用 Volume 会逼迫打开相机后处理 ⇒ 全局改画面，不做。
//  · 🔴 压暗层也**不用 Canvas**，用相机下的全屏四边形。原因（踩过的两个坑）：
//      a) ScreenSpaceOverlay 画布永远盖在相机 3D 输出之上 ⇒ 会连刀光一起压暗，
//         与参考图（背景压暗、刀光在最上层）正好相反。
//      b) ScreenSpaceCamera 画布虽然进相机队列，但运行时新建的 Canvas 在编辑器里
//         RectTransform.lossyScale 是 (0,0,0)、Image 网格尚未生成，既拍不到也调不准。
//    ⇒ 用普通几何体 + renderQueue 精确控序（压暗 2990 < 刀光 3000），且可离屏验证。
//  · 压暗层是**屏幕效果**，所以无论 m_AttachToCamera 如何，它都挂在相机下跟随相机。
//
// 时间模型（两套时间，别搞混）：
//  · **演出总时长** m_Duration —— 领域展开持续多久。期间按 m_RefreshInterval 节奏**不断刷新**刀光。
//  · **单条刀光寿命** m_FadeIn + m_Hold + m_FadeOut —— 每一道长条自己的淡入/保持/淡出。
//  两者互相独立：把 m_Duration 拉长，屏幕上的刀光就会持续刷新，而不是只闪一下。
//  任意时刻同时存在的刀光数 ≈ m_SlashCount × (单条寿命 / m_RefreshInterval)。
//  例：16 × (0.195 / 0.20) ≈ 15.6 条。
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Loongdum/VFX/Sukuna Domain Effect")]
[DisallowMultipleComponent]
public sealed class SukunaDomainEffect : MonoBehaviour
{
    const string kDarkenShaderName = "Loongdum/VH Domain Darken";
    const int kDarkenQueue = 2990;      // 必须 < 刀光材质队列（3000）

    [Header("L1 刀光 · 数量与尺寸（尺寸 = 视口比例，1.0 = 满屏高）")]
    [SerializeField, Min(1)] int m_SlashCount = 16;
    [SerializeField] Vector2 m_LengthRange = new Vector2(0.55f, 1.30f);
    [SerializeField] Vector2 m_ThicknessRange = new Vector2(0.007f, 0.020f);

    [Header("L1 刀光 · 位置（相机前方薄盒）")]
    [SerializeField, Min(0.1f)] float m_BoxDistance = 2.0f;
    [SerializeField, Min(1f)] float m_BoxPadding = 1.35f;
    [SerializeField, Min(0.01f)] float m_BoxDepth = 1.0f;

    [Header("L1 刀光 · 角度（度）")]
    [SerializeField] bool m_DiagonalBandsOnly = true;
    [SerializeField] float m_AngleMin = -55f;
    [SerializeField] float m_AngleMax = 55f;

    [Header("L1 刀光 · 单条刀光的寿命（秒）")]
    [SerializeField, Min(0f)] float m_FadeIn = 0.035f;
    [SerializeField, Min(0f)] float m_Hold = 0.100f;
    [SerializeField, Min(0f)] float m_FadeOut = 0.060f;

    [Header("L1 刀光 · 演出总时长与刷新节奏（秒）")]
    [Tooltip("领域展开持续多久。期间会不断刷新刀光。")]
    [SerializeField, Min(0.05f)] float m_Duration = 2.0f;
    [Tooltip("每隔多久刷出一波刀光。越小越密（同时在场条数 ≈ m_SlashCount × 单条寿命 / 本值）。")]
    [SerializeField, Min(0.02f)] float m_RefreshInterval = 0.20f;
    [Tooltip("开场是否额外汇总爆一波（让领域展开的第一眼有冲击力）。")]
    [SerializeField] bool m_OpenBurst = true;

    [Header("L2 压暗（相机下全屏四边形）")]
    [SerializeField] bool m_DarkenEnabled = true;
    [SerializeField, Range(0f, 1f)] float m_DarkenAmount = 0.95f;
    [SerializeField] Color m_DarkenColor = new Color(0.30f, 0.018f, 0.028f, 1f);
    [SerializeField, Min(0f)] float m_DarkenIn = 0.06f;
    [SerializeField, Min(0f)] float m_DarkenOut = 0.45f;
    [SerializeField, Min(0f)] float m_DarkenHoldAfterSlash = 0.25f;
    [SerializeField] int m_OverlaySortingOrder = -100;
    // 必须用序列化引用而不是纯 Shader.Find：只有被材质/序列化字段引用到的着色器
    // 才会被打进 Build，否则运行时 Find 会返回 null。
    [SerializeField] Shader m_DarkenShader;

    [Header("其它")]
    [SerializeField] bool m_AttachToCamera = true;
    [SerializeField] ParticleSystemSimulationSpace m_SimulationSpace = ParticleSystemSimulationSpace.Local;
    [SerializeField] bool m_PlayOnStart = false;

    ParticleSystem m_Particles;
    ParticleSystemRenderer m_ParticleRenderer;
    Transform m_Emitter;
    Transform m_OverlayQuad;
    MeshRenderer m_OverlayRenderer;
    Material m_OverlayMaterial;
    Mesh m_OverlayMesh;
    Camera m_Camera;

    /// <summary>单条刀光的总寿命（淡入 + 保持 + 淡出），秒。</summary>
    public float SlashLifetime
    {
        get { return Mathf.Max(0.02f, m_FadeIn + m_Hold + m_FadeOut); }
    }

    /// <summary>演出总时长（秒）。代码里可改写，改完下次 Play() 生效。</summary>
    public float Duration
    {
        get { return Mathf.Max(0.05f, m_Duration); }
        set { m_Duration = Mathf.Max(0.05f, value); }
    }

    /// <summary>任意时刻同时存在的刀光数估算 = m_SlashCount × (单条寿命 / 刷新间隔)。</summary>
    public float ConcurrentSlashEstimate
    {
        get { return m_SlashCount * (SlashLifetime / Mathf.Max(0.02f, m_RefreshInterval)); }
    }

    void Awake()
    {
        m_Particles = GetComponentInChildren<ParticleSystem>(true);
        if (m_Particles == null)
        {
            Debug.LogError("[SukunaDomainEffect] 预制体里找不到 ParticleSystem，效果无法播放。", this);
            enabled = false;
            return;
        }

        m_ParticleRenderer = m_Particles.GetComponent<ParticleSystemRenderer>();
        m_Emitter = m_Particles.transform;
    }

    void Start()
    {
        if (m_PlayOnStart) Play();
    }

    void OnDestroy()
    {
        if (m_OverlayQuad != null) Destroy(m_OverlayQuad.gameObject);
        m_OverlayQuad = null;
        m_OverlayRenderer = null;
        if (m_OverlayMaterial != null) Destroy(m_OverlayMaterial);
        m_OverlayMaterial = null;
        if (m_OverlayMesh != null) Destroy(m_OverlayMesh);
        m_OverlayMesh = null;
    }

    void OnValidate()
    {
        m_SlashCount = Mathf.Max(1, m_SlashCount);
        m_BoxDistance = Mathf.Max(0.1f, m_BoxDistance);
        m_BoxPadding = Mathf.Max(1f, m_BoxPadding);
        m_BoxDepth = Mathf.Max(0.01f, m_BoxDepth);
        m_Duration = Mathf.Max(0.05f, m_Duration);
        m_RefreshInterval = Mathf.Max(0.02f, m_RefreshInterval);
        if (m_LengthRange.y < m_LengthRange.x) m_LengthRange.y = m_LengthRange.x;
        if (m_ThicknessRange.y < m_ThicknessRange.x) m_ThicknessRange.y = m_ThicknessRange.x;
    }

    /// <summary>播放一次领域展开。可重复调用（会先重置）。</summary>
    [ContextMenu("▶ 播放领域展开")]
    public void Play()
    {
        if (m_Particles == null)
        {
            Debug.LogWarning("[SukunaDomainEffect] ParticleSystem 未就绪。", this);
            return;
        }

        m_Camera = Camera.main;
        if (m_Camera == null)
        {
            Debug.LogWarning("[SukunaDomainEffect] 场景里找不到 MainCamera，刀光无法定位到相机前方。", this);
            return;
        }

        if (m_AttachToCamera && m_Emitter.parent != m_Camera.transform)
        {
            m_Emitter.SetParent(m_Camera.transform, false);
        }

        ApplySettings(m_Camera);

        StopAllCoroutines();
        m_Particles.Clear(true);
        m_Particles.Play(true);

        if (m_DarkenEnabled) StartCoroutine(RoutineDarken());
    }

    /// <summary>立刻停止（清掉粒子并把遮罩收回 0）。</summary>
    [ContextMenu("■ 停止")]
    public void Stop()
    {
        StopAllCoroutines();
        if (m_Particles != null)
            m_Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        SetDarken(0f);
    }

    // ------------------------------------------------------------------
    // 把 Inspector 参数写进粒子系统
    // ------------------------------------------------------------------
    void ApplySettings(Camera cam)
    {
        float life = SlashLifetime;
        float refresh = Mathf.Max(0.02f, m_RefreshInterval);
        float rate = m_SlashCount / refresh;      // 每秒刷出多少条刀光

        // 相机在 m_BoxDistance 处的可视高度 / 宽度 —— 所有尺寸都以此为基准
        float viewH = 2f * m_BoxDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float viewW = viewH * cam.aspect;

        var main = m_Particles.main;
        // duration = **发射窗口**：非循环系统在这个时长之后停止发射，已存在的粒子继续活完自己那一辈子。
        // 末尾多给一个 life，让最后刷出来的那几道刀光能好好淡出，不会突然消失。
        main.duration = Duration + life;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = life;
        main.startSpeed = 0f;
        main.startColor = Color.white;
        main.simulationSpace = m_SimulationSpace;
        // 上限必须按「同时在场的最大数量」给够，否则新粒子会把还没死的老粒子挤掉 ⇒ 刷新会抽帧
        main.maxParticles = Mathf.Max(32, Mathf.CeilToInt(rate * life * 1.5f) + m_SlashCount);

        main.startRotation3D = false;
        float a0 = m_DiagonalBandsOnly ? Mathf.Min(m_AngleMin, m_AngleMax) : -180f;
        float a1 = m_DiagonalBandsOnly ? Mathf.Max(m_AngleMin, m_AngleMax) : 180f;
        main.startRotation = new ParticleSystem.MinMaxCurve(a0 * Mathf.Deg2Rad, a1 * Mathf.Deg2Rad);

        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(viewH * m_LengthRange.x, viewH * m_LengthRange.y);
        main.startSizeY = new ParticleSystem.MinMaxCurve(viewH * m_ThicknessRange.x, viewH * m_ThicknessRange.y);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(viewH * m_ThicknessRange.x, viewH * m_ThicknessRange.y);

        // 寿命曲线：短淡入 → 保持 → 淡出
        float t1 = Mathf.Clamp01(m_FadeIn / life);
        float t2 = Mathf.Clamp01((m_FadeIn + m_Hold) / life);
        if (t2 <= t1) t2 = Mathf.Min(1f, t1 + 0.001f);

        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, t1),
                new GradientAlphaKey(1f, t2),
                new GradientAlphaKey(0f, 1f)
            });

        // ⚠️ ColorOverLifetimeModule 上没有 colorMode 这个成员
        // （写了会 CS1061 编译失败）。模式由 MinMaxGradient 自身承载。
        var col = m_Particles.colorOverLifetime;
        col.enabled = true;
        var gradient = new ParticleSystem.MinMaxGradient(grad);
        gradient.mode = ParticleSystemGradientMode.Gradient;
        col.color = gradient;

        // 发射：按 m_RefreshInterval 的节奏**持续刷新**刀光（不再是只爆一次）
        var emission = m_Particles.emission;
        emission.enabled = true;
        emission.rateOverTime = rate;
        if (m_OpenBurst)
        {
            // 开场额外同时爆一波，让「领域展开」的第一眼有冲击力。不想要就把 m_OpenBurst 关掉。
            short burst = (short)Mathf.Clamp(m_SlashCount, 0, 2000);
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst, burst) });
        }
        else
        {
            emission.SetBursts(new ParticleSystem.Burst[0]);
        }

        // 形状：相机前方的薄盒（发射器已是相机子物体，所以盒子的局部空间就是相机空间）
        var shape = m_Particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = Vector3.zero;
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(viewW * m_BoxPadding, viewH * m_BoxPadding, m_BoxDepth);
        shape.randomDirectionAmount = 0f;

        m_Emitter.localPosition = new Vector3(0f, 0f, m_BoxDistance);
        m_Emitter.localRotation = Quaternion.identity;

        if (m_ParticleRenderer != null)
        {
            m_ParticleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            m_ParticleRenderer.alignment = ParticleSystemRenderSpace.View;
            m_ParticleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_ParticleRenderer.receiveShadows = false;
            m_ParticleRenderer.allowOcclusionWhenDynamic = false;
            // 刀光的排序值必须**高于**压暗层（-100），否则会被压在压暗层下面
            m_ParticleRenderer.sortingOrder = 10;
            // 显式声明顶点流，确保寿命颜色（Color over Lifetime）一定传进 shader
            m_ParticleRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV
            });
        }

        LayoutOverlay(cam);
    }

    // ------------------------------------------------------------------
    // L2 压暗
    // ------------------------------------------------------------------
    IEnumerator RoutineDarken()
    {
        EnsureOverlay();

        if (m_OverlayMaterial == null) yield break;

        if (m_DarkenIn > 0.0001f)
        {
            float t = 0f;
            while (t < m_DarkenIn)
            {
                t += Time.deltaTime;
                SetDarken(Mathf.Clamp01(t / m_DarkenIn));
                yield return null;
            }
        }
        SetDarken(1f);

        // 压暗必须盖住**整场演出**（m_Duration），演出结束后再按 m_DarkenHoldAfterSlash 多留一拍
        float hold = Mathf.Max(0f, Duration - m_DarkenIn) + m_DarkenHoldAfterSlash;
        if (hold > 0.0001f) yield return new WaitForSeconds(hold);

        if (m_DarkenOut > 0.0001f)
        {
            float t = 0f;
            while (t < m_DarkenOut)
            {
                t += Time.deltaTime;
                SetDarken(1f - Mathf.Clamp01(t / m_DarkenOut));
                yield return null;
            }
        }
        SetDarken(0f);
    }

    /// <summary>惰性创建压暗用的全屏四边形（挂在相机下）。</summary>
    void EnsureOverlay()
    {
        if (m_OverlayQuad != null) return;

        m_Camera = m_Camera != null ? m_Camera : Camera.main;
        if (m_Camera == null) return;

        var shader = m_DarkenShader != null ? m_DarkenShader : Shader.Find(kDarkenShaderName);
        if (shader == null)
        {
            Debug.LogError("[SukunaDomainEffect] 找不到着色器 " + kDarkenShaderName
                + "。请把 VH_DomainDarken.shader 拖到本组件的 Darken Shader 槽位。", this);
            return;
        }

        var go = new GameObject("VH_DomainOverlay");
        go.hideFlags = HideFlags.DontSave;

        var mf = go.AddComponent<MeshFilter>();
        m_OverlayMesh = BuildQuadMesh();
        mf.sharedMesh = m_OverlayMesh;

        m_OverlayRenderer = go.AddComponent<MeshRenderer>();
        m_OverlayRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        m_OverlayRenderer.receiveShadows = false;
        m_OverlayRenderer.allowOcclusionWhenDynamic = false;
        m_OverlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        m_OverlayRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        m_OverlayRenderer.sortingOrder = m_OverlaySortingOrder;

        m_OverlayMaterial = new Material(shader);
        m_OverlayMaterial.name = "M_VH_DomainDarken (runtime)";
        m_OverlayMaterial.renderQueue = kDarkenQueue;
        m_OverlayRenderer.sharedMaterial = m_OverlayMaterial;

        m_OverlayQuad = go.transform;
        m_OverlayQuad.SetParent(m_Camera.transform, false);

        SetDarken(0f);
        LayoutOverlay(m_Camera);
    }

    /// <summary>把全屏四边形摆到相机近平面外一点点，并按视锥尺寸放大到刚好铺满。</summary>
    void LayoutOverlay(Camera cam)
    {
        if (m_OverlayQuad == null || cam == null) return;

        // 必须在近平面之外，否则会被裁掉。ZTest Always ⇒ 不需要考虑遮挡关系。
        float dist = Mathf.Max(cam.nearClipPlane * 2f, 0.5f);
        float h = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * cam.aspect;

        m_OverlayQuad.SetParent(cam.transform, false);
        m_OverlayQuad.localPosition = new Vector3(0f, 0f, dist);
        m_OverlayQuad.localRotation = Quaternion.identity;
        // 四边形网格是 1x1（±0.5），乘 1.1 留点余量，防止边缘露缝
        m_OverlayQuad.localScale = new Vector3(w * 1.1f, h * 1.1f, 1f);
    }

    static Mesh BuildQuadMesh()
    {
        var m = new Mesh();
        m.name = "VH_DomainDarkenQuad";
        m.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3( 0.5f, -0.5f, 0f),
            new Vector3( 0.5f,  0.5f, 0f),
            new Vector3(-0.5f,  0.5f, 0f)
        };
        m.uv = new[]
        {
            new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 1f), new Vector2(0f, 1f)
        };
        m.normals = new[]
        {
            new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -1f),
            new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, -1f)
        };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>k = 0 全透明（无效果），k = 1 满强度。</summary>
    void SetDarken(float k)
    {
        if (m_OverlayMaterial == null) return;
        float a = Mathf.Clamp01(m_DarkenColor.a * m_DarkenAmount * Mathf.Clamp01(k));
        m_OverlayMaterial.SetColor("_Color",
            new Color(m_DarkenColor.r, m_DarkenColor.g, m_DarkenColor.b, a));
    }
}
