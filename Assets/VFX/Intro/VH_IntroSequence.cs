using UnityEngine;

/// <summary>
/// 《Vampire Hunt》开场小演出 ——「右侧角色入场 → 两人对话 → 领域展开」。
///
/// 时间轴（每段都可在 Inspector 调）：
/// <code>
///   t = 0 ──[capsuleStartDelay]──► 胶囊体从画面外平移入场
///                                   （入场时长 = 起点→落点距离 ÷ m_CapsuleEnterSpeed）
///                                  ──► 落位
///   落位 ──[bubble1Delay]──► 气泡1 淡入 ──[bubble1Hold]──► 淡出
///   气泡1 ──[bubble2Delay]──► 气泡2 淡入 ──[bubble2Hold]──► 淡出
///   气泡全淡完 ──[bubblesToSlashGap]──► Play() 斩击（宿傩领域展开）
/// </code>
///
/// 设计要点：把「按绝对时间求值」独立成 <see cref="Evaluate"/>，
/// 所以运行时 Update() 只是 Evaluate(elapsed)，而编辑器里可以不开 Play
/// 直接 Evaluate(t) + 离屏渲染逐帧验证。
/// </summary>
[AddComponentMenu("Loongdum/VH Intro Sequence")]
[DisallowMultipleComponent]
public sealed class VH_IntroSequence : MonoBehaviour
{
    public enum EnterSide { Right, Left, Top, Bottom }

    // ─────────────────────────── ① 入场 ───────────────────────────

    [Header("① 入场的胶囊体（右侧那位）")]
    [Tooltip("要从画面外平移进来的胶囊体。场景里是 Capsule (1)。")]
    [SerializeField] Transform m_Capsule;

    [Tooltip("关掉 = 跳过整段入场，气泡直接从 0 秒开始。")]
    [SerializeField] bool m_MoveCapsule = true;

    [Tooltip("开场先等多久他才开始动（秒）。")]
    [SerializeField, Min(0f)] float m_CapsuleStartDelay = 0.25f;

    [Tooltip("★ 入场速度（米/秒）—— 越大越快。入场时长 = 起点到落点的距离 ÷ 本值。")]
    [SerializeField, Min(0.05f)] float m_CapsuleEnterSpeed = 4f;

    [Tooltip("从画面哪一侧进场。")]
    [SerializeField] EnterSide m_CapsuleEnterFrom = EnterSide.Right;

    [Tooltip("起点在画面外再多留一点余量（视口比例，0.15 = 再往外 15%）。")]
    [SerializeField, Min(0.01f)] float m_CapsuleOffscreenMargin = 0.15f;

    [Tooltip("勾上 = 只沿他自己的水平/深度滑动，Y 高度锁死在落位时的高度（人贴着地走）。")]
    [SerializeField] bool m_KeepCapsuleGrounded = true;

    [Tooltip("入场缓动。默认两头慢中间快；想要「冲进来急停」就把末段切线拉平。")]
    [SerializeField] AnimationCurve m_CapsuleEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("勾上 = 取「播放瞬间他站的位置」当落位点（推荐）。关掉则用下面那个显式坐标。")]
    [SerializeField] bool m_UseCurrentPositionAsTarget = true;

    [Tooltip("关掉上面那个开关时，用这个世界坐标当落位点。")]
    [SerializeField] Vector3 m_CapsuleTargetPosition = Vector3.zero;

    // ─────────────────────────── ② 对话气泡 ───────────────────────────

    [Header("② 对话气泡")]
    [Tooltip("先说话的那个。")]
    [SerializeField] SpriteRenderer m_Bubble1;

    [Tooltip("后说话的那个。")]
    [SerializeField] SpriteRenderer m_Bubble2;

    [Tooltip("气泡1 相对「胶囊体落位」之后多久出现（秒）。")]
    [SerializeField, Min(0f)] float m_Bubble1Delay = 0.15f;

    [Tooltip("气泡1 停留多久（仅在「气泡2 顶掉气泡1」关掉时生效）。")]
    [SerializeField, Min(0.01f)] float m_Bubble1Hold = 1.6f;

    [Tooltip("★ 气泡2 相对「气泡1 出现」之后多久出现（秒）—— 这就是两人你来我往的节奏。")]
    [SerializeField, Min(0f)] float m_Bubble2Delay = 0.9f;

    [Tooltip("气泡2 停留多久。")]
    [SerializeField, Min(0.01f)] float m_Bubble2Hold = 1.8f;

    [Tooltip("淡入时长（秒）。0 = 硬切。")]
    [SerializeField, Min(0f)] float m_BubbleFadeIn = 0.12f;

    [Tooltip("淡出时长（秒）。0 = 硬切。")]
    [SerializeField, Min(0f)] float m_BubbleFadeOut = 0.12f;

    [Tooltip("勾上 = 气泡2 一冒头，气泡1 就淡出（真·一来一往）。关掉 = 两个气泡同时挂着到结束。")]
    [SerializeField] bool m_Bubble1YieldToBubble2 = true;

    // ─────────────────────────── ③ 接斩击 ───────────────────────────

    [Header("③ 接斩击（宿傩领域展开）")]
    [Tooltip("气泡全部淡完之后再等多久开始斩击（秒）。")]
    [SerializeField, Min(0f)] float m_BubblesToSlashGap = 0.2f;

    [Tooltip("要接的斩击。留空则自动找场景里的 SukunaDomainEffect。")]
    [SerializeField] SukunaDomainEffect m_Slash;

    [SerializeField] bool m_PlaySlash = true;

    // ─────────────────────────── ④ 结尾：黑屏 + 巨大斩击 ───────────────────────────

    [Header("④ 结尾：黑屏 + 巨大斩击（切开右侧胶囊体腰部）")]
    [Tooltip("留空则不做这一段（整段演出到斩击为止）。")]
    [SerializeField] VH_CutSlash m_Cut;

    [Tooltip("领域展开本体放完之后（其自身时长之后）再等多久开始这一段。默认 0.7s ≈ 压暗余韵。")]
    [SerializeField, Min(0f)] float m_DomainToCutDelay = 0.7f;

    // ─────────────────────────── ⑤ 触发 ───────────────────────────

    [Header("⑤ 触发")]
    [Tooltip("进 Play 就自动开始整段演出。")]
    [SerializeField] bool m_PlayOnStart = true;

    [Tooltip("演出第一帧就把两个气泡藏掉（本段要求：一开场气泡不可见）。")]
    [SerializeField] bool m_HideBubblesOnStart = true;

    // ─────────────────────────── 内部状态 ───────────────────────────

    float m_Elapsed;
    bool m_Playing;
    bool m_SlashFired;
    bool m_Captured;

    Vector3 m_StartPos;
    Vector3 m_EndPos;
    Camera m_Camera;

    const float kMinDuration = 0.0001f;

    // ─────────────────────────── 时间轴读数 ───────────────────────────

    /// <summary>胶囊体从画面外滑到落位需要多久（秒）= 距离 ÷ 速度。</summary>
    public float CapsuleEnterDuration
    {
        get
        {
            if (!m_MoveCapsule || m_Capsule == null) return 0f;
            EnsureCaptured();
            return Vector3.Distance(m_StartPos, m_EndPos) / Mathf.Max(0.05f, m_CapsuleEnterSpeed);
        }
    }

    public float CapsuleArriveTime { get { return Mathf.Max(0f, m_CapsuleStartDelay) + CapsuleEnterDuration; } }
    public float Bubble1AppearTime { get { return CapsuleArriveTime + Mathf.Max(0f, m_Bubble1Delay); } }
    public float Bubble2AppearTime { get { return Bubble1AppearTime + Mathf.Max(0f, m_Bubble2Delay); } }

    public float Bubble1HideTime
    {
        get { return m_Bubble1YieldToBubble2 ? Bubble2AppearTime : Bubble1AppearTime + Mathf.Max(0.01f, m_Bubble1Hold); }
    }

    public float Bubble2HideTime { get { return Bubble2AppearTime + Mathf.Max(0.01f, m_Bubble2Hold); } }

    /// <summary>最后一个气泡彻底淡完的时刻。</summary>
    public float BubblesGoneTime
    {
        get { return Mathf.Max(Bubble1HideTime, Bubble2HideTime) + Mathf.Max(0f, m_BubbleFadeOut); }
    }

    /// <summary>斩击 Play() 的时刻。</summary>
    public float SlashTime { get { return BubblesGoneTime + Mathf.Max(0f, m_BubblesToSlashGap); } }

    /// <summary>领域展开本体放完的时刻（Play() + 它自己的时长）。</summary>
    public float DomainEndTime
    {
        get { return SlashTime + (m_Slash != null ? Mathf.Max(0f, m_Slash.Duration) : 2f); }
    }

    /// <summary>★ 巨大斩击劈中的时刻 —— 也是屏幕从黑恢复可见的同一瞬间。</summary>
    public float CutTime { get { return DomainEndTime + Mathf.Max(0f, m_DomainToCutDelay); } }

    /// <summary>整段演出总时长（秒，含结尾的黑屏 + 巨大斩击）。</summary>
    public float TotalDuration
    {
        get { return CutTime + (m_Cut != null ? m_Cut.TotalDuration : 0f); }
    }

    // ─────────────────────────── 运行状态（只读） ───────────────────────────

    /// <summary>演出已推进的绝对时间（秒）。</summary>
    public float Elapsed { get { return m_Elapsed; } }

    /// <summary>是否正在推进。</summary>
    public bool IsPlaying { get { return m_Playing; } }

    /// <summary>斩击是否已被本序列触发过（防止重复触发）。</summary>
    public bool SlashFired { get { return m_SlashFired; } }

    /// <summary>胶囊体当前的入场起点（世界坐标，只读，调试用）。</summary>
    public Vector3 CapsuleStartPosition { get { EnsureCaptured(); return m_StartPos; } }

    /// <summary>胶囊体的落位点（世界坐标，只读，调试用）。</summary>
    public Vector3 CapsuleEndPosition { get { EnsureCaptured(); return m_EndPos; } }

    // ─────────────────────────── 生命周期 ───────────────────────────

    void Awake()
    {
        Capture();
        if (m_HideBubblesOnStart)
        {
            ApplyBubble(m_Bubble1, 0f);
            ApplyBubble(m_Bubble2, 0f);
        }

        if (m_PlayOnStart) Play();
        else Evaluate(0f);
    }

    void Update()
    {
        if (!m_Playing) return;
        m_Elapsed += Time.deltaTime;
        Evaluate(m_Elapsed);
    }

    // ─────────────────────────── 对外 API ───────────────────────────

    /// <summary>把「胶囊体当前坐标」记为落位点，并缓存相机、重算起点。只会真正执行一次。</summary>
    public void Capture()
    {
        if (m_Captured) return;

        if (m_Capsule != null && m_UseCurrentPositionAsTarget)
            m_CapsuleTargetPosition = m_Capsule.position;

        m_EndPos = m_CapsuleTargetPosition;
        if (m_Camera == null) m_Camera = Camera.main;
        m_StartPos = ComputeStartPos();

        if (m_Slash == null) m_Slash = FindSlashInScene();

        m_Captured = true;
    }

    /// <summary>强制重取落位点（在 Inspector 里改了胶囊体位置后想立刻生效时用）。</summary>
    [ContextMenu("↻ 重新取落位点 + 缓存相机")]
    public void Recapture()
    {
        m_Captured = false;
        Capture();
    }

    /// <summary>从 0 秒开始重播整段演出。</summary>
    [ContextMenu("▶ 播放整段演出")]
    public void Play()
    {
        Capture();

        if (m_PlaySlash)
        {
            SukunaDomainEffect s = ResolveSlash();
            if (s != null) s.Stop();
        }

        m_SlashFired = false;
        m_Elapsed = 0f;
        m_Playing = true;
        Evaluate(0f);
    }

    /// <summary>停止推进（已摆出来的姿势保持不动）。</summary>
    [ContextMenu("■ 停止推进")]
    public void StopSequence()
    {
        m_Playing = false;
    }

    /// <summary>按绝对时间 t（秒）把整段动画摆到该时刻的状态。幂等。</summary>
    public void Evaluate(float t)
    {
        Capture();

        // ── ① 胶囊体入场 ──
        if (m_MoveCapsule && m_Capsule != null)
        {
            float arrive = CapsuleArriveTime;
            if (t <= m_CapsuleStartDelay)
            {
                m_Capsule.position = m_StartPos;
            }
            else if (t >= arrive)
            {
                m_Capsule.position = m_EndPos;
            }
            else
            {
                float k = Mathf.Clamp01((t - m_CapsuleStartDelay) / Mathf.Max(kMinDuration, CapsuleEnterDuration));
                float e = m_CapsuleEase != null ? m_CapsuleEase.Evaluate(k) : k;
                m_Capsule.position = Vector3.LerpUnclamped(m_StartPos, m_EndPos, e);
            }
        }

        // ── ② 对话气泡 ──
        ApplyBubble(m_Bubble1, BubbleAlpha(t, Bubble1AppearTime, Bubble1HideTime));
        ApplyBubble(m_Bubble2, BubbleAlpha(t, Bubble2AppearTime, Bubble2HideTime));

        // ── ③ 接斩击 ──
        if (m_PlaySlash && !m_SlashFired && t >= SlashTime)
        {
            m_SlashFired = true;
            SukunaDomainEffect s = ResolveSlash();
            if (s != null) s.Play();
        }

        // ── ④ 结尾：黑屏 + 巨大斩击（相对劈中瞬间的时间送进去） ──
        if (m_Cut != null) m_Cut.Evaluate(t - CutTime);
    }

    /// <summary>把时间轴打成一行行文本（策划核对节奏用）。</summary>
    public string TimelineReport()
    {
        Capture();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("── VH_IntroSequence 时间轴 ──");
        sb.AppendLine(string.Format("  0.00s            开场（两个气泡已隐藏）"));
        sb.AppendLine(string.Format("  {0:0.00}s            胶囊体开始入场", Mathf.Max(0f, m_CapsuleStartDelay)));
        sb.AppendLine(string.Format("  {0:0.00}s            胶囊体落位（入场 {1:0.00}s，速度 {2:0.0} m/s，位移 {3:0.0} m）",
            CapsuleArriveTime, CapsuleEnterDuration, m_CapsuleEnterSpeed, Vector3.Distance(m_StartPos, m_EndPos)));
        sb.AppendLine(string.Format("  {0:0.00}s            气泡1 淡入", Bubble1AppearTime));
        sb.AppendLine(string.Format("  {0:0.00}s            气泡1 淡出", Bubble1HideTime));
        sb.AppendLine(string.Format("  {0:0.00}s            气泡2 淡入", Bubble2AppearTime));
        sb.AppendLine(string.Format("  {0:0.00}s            气泡2 淡出", Bubble2HideTime));
        sb.AppendLine(string.Format("  {0:0.00}s            气泡全部消失", BubblesGoneTime));
        sb.AppendLine(string.Format("  {0:0.00}s            ★ 斩击 Play()（宿傩领域展开）", SlashTime));
        sb.AppendLine(string.Format("  {0:0.00}s            领域展开本体放完", DomainEndTime));
        if (m_Cut != null)
        {
            sb.AppendLine(string.Format("  {0:0.00}s            ☠ 巨大斩击劈中 / 屏幕同时恢复可见（黑屏从它往前推 m_BlackoutLead 秒起）", CutTime));
            sb.AppendLine(string.Format("  {0:0.00}s            整段演出结束", TotalDuration));
        }
        else
        {
            sb.AppendLine("  （④ 结尾留空 ⇒ 本段演出到斩击为止）");
        }
        return sb.ToString();
    }

    [ContextMenu("⏱ 打印时间轴到 Console")]
    void LogTimeline()
    {
        Debug.Log(TimelineReport(), this);
    }

    // ─────────────────────────── 内部实现 ───────────────────────────

    void EnsureCaptured()
    {
        if (!m_Captured) Capture();
    }

    Vector3 ComputeStartPos()
    {
        if (m_Camera == null) m_Camera = Camera.main;
        if (m_Camera == null) return m_EndPos;

        Vector3 vp = m_Camera.WorldToViewportPoint(m_EndPos);
        float margin = Mathf.Max(0.01f, m_CapsuleOffscreenMargin);
        float u = vp.x;
        float v = vp.y;

        switch (m_CapsuleEnterFrom)
        {
            case EnterSide.Right: u = 1f + margin; break;
            case EnterSide.Left: u = -margin; break;
            case EnterSide.Top: v = 1f + margin; break;
            default: v = -margin; break;
        }

        Vector3 start = m_Camera.ViewportToWorldPoint(new Vector3(u, v, vp.z));
        if (m_KeepCapsuleGrounded) start.y = m_EndPos.y;
        return start;
    }

    float BubbleAlpha(float t, float appear, float hide)
    {
        if (t < appear) return 0f;

        float fadeIn = Mathf.Max(0f, m_BubbleFadeIn);
        if (fadeIn > 0f && t < appear + fadeIn) return (t - appear) / fadeIn;
        if (t <= hide) return 1f;

        float fadeOut = Mathf.Max(0f, m_BubbleFadeOut);
        if (fadeOut > 0f && t < hide + fadeOut) return 1f - (t - hide) / fadeOut;
        return 0f;
    }

    void ApplyBubble(SpriteRenderer sr, float alpha)
    {
        if (sr == null) return;
        alpha = Mathf.Clamp01(alpha);
        Color c = sr.color;
        if (!Mathf.Approximately(c.a, alpha))
        {
            c.a = alpha;
            sr.color = c;
        }
        sr.enabled = alpha > 0.001f;
    }

    SukunaDomainEffect ResolveSlash()
    {
        if (m_Slash == null) m_Slash = FindSlashInScene();
        return m_Slash;
    }

    static SukunaDomainEffect FindSlashInScene()
    {
        var found = Resources.FindObjectsOfTypeAll<SukunaDomainEffect>();
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].gameObject.scene.IsValid() && found[i].gameObject.scene.isLoaded)
                return found[i];
        }
        return null;
    }
}
