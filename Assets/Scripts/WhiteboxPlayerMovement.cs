using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class WhiteboxPlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    [Tooltip("让移动输入跟随相机朝向：相机在上方俯视时，向前输入始终是画面里的“向上”。默认关闭，保持原有世界坐标方向。")]
    [SerializeField] private bool alignToCamera;

    [Tooltip("用来取朝向的相机。留空则用 Camera.main。")]
    [SerializeField] private Camera inputCamera;

    [Header("走路音效（留空则不发声）")]
    [Tooltip("走路时每一步播放的单步音效，例如「Play_Player_Move」。")]
    [SerializeField] private AudioClip stepClip;

    [Tooltip("每两声脚步之间的间隔（秒）。数值越小步频越密，数值越大步频越疏。")]
    [SerializeField, Min(0.05f)] private float stepInterval = 0.45f;

    [Tooltip("脚步音量。觉得吵就直接往下调，这是最有效的一档。")]
    [SerializeField, Range(0f, 1f)] private float stepVolume = 0.35f;

    [Tooltip("每次脚步的音高随机浮动量。0 = 完全不随机（会听出「复读机」）；0.06 ≈ 音高在 0.94~1.06 之间跳；超过 0.15 就像换了个人的脚步声。")]
    [SerializeField, Range(0f, 0.3f)] private float stepPitchJitter = 0.06f;

    [Tooltip("每次脚步的音量随机浮动量（只往小浮动，不会比 stepVolume 更响）。让连续脚步不会一模一样。")]
    [SerializeField, Range(0f, 0.5f)] private float stepVolumeJitter = 0.12f;

    [Tooltip("脚步专用低通滤波的截止频率（Hz）。调低 = 削掉高频，声音变闷、变远，不再刺耳；22000 ≈ 完全不削（保留原始亮度）。刺耳主要来自 2k~6k Hz，所以默认 2600。")]
    [SerializeField, Range(500f, 22000f)] private float stepLowPassHz = 2600f;

    private CharacterController controller;
    private InputAction moveAction;
    private float verticalSpeed;
    private float stepTimer;
    private AudioSource stepSource;
    private AudioLowPassFilter stepFilter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // PlayerInput 使用该玩家自己的动作；未挂载时使用项目全局 Input Actions。
        // 动作的启停由 PlayerInput 或 Input System 管理，避免绕过动作图切换。
        PlayerInput playerInput = GetComponentInParent<PlayerInput>();
        InputActionAsset actions = playerInput != null ? playerInput.actions : InputSystem.actions;
        moveAction = actions != null ? actions.FindAction("Player/Move") : null;

        if (moveAction == null)
            Debug.LogError("[WhiteboxPlayerMovement] 找不到 Player/Move 动作，请在 Input Actions 中配置 Vector2 类型的 Move 动作。", this);
    }

    private void Update()
    {
        Vector2 moveInput = moveAction != null && moveAction.enabled
            ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);

        if (alignToCamera)
        {
            Camera cam = inputCamera != null ? inputCamera : Camera.main;
            if (cam != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
                if (forward.sqrMagnitude > 1e-4f)
                {
                    forward.Normalize();
                    Vector3 right = Vector3.Cross(Vector3.up, forward);
                    direction = right * moveInput.x + forward * moveInput.y;
                }
            }
        }

        direction = Vector3.ClampMagnitude(direction, 1f);

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -10f;
        }

        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        Vector3 velocity = direction * moveSpeed + Vector3.up * verticalSpeed;
        controller.Move(velocity * Time.deltaTime);

        // 走路时按固定间隔播单步音效。走路是「连续量」，不能每帧播，
        // 所以这里做节流：站定时计时器清零，起步第一步立刻响，之后每 stepInterval 响一次。
        bool walking = direction.sqrMagnitude > 0.01f && controller.isGrounded;
        if (!walking)
        {
            stepTimer = 0f;
            return;
        }

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f)
            return;

        stepTimer = Mathf.Max(0.05f, stepInterval);
        PlayStep();
    }

    /// <summary>播一次脚步。音高和音量每次都抖一点，避免连续脚步听成「复读机」；
    /// 低通滤波削掉高频，让脚步不再刺耳。</summary>
    private void PlayStep()
    {
        if (stepClip == null) return;
        if (stepSource == null) CreateStepSource();

        stepFilter.cutoffFrequency = Mathf.Clamp(stepLowPassHz, 500f, 22000f);
        stepSource.pitch = 1f + Random.Range(-stepPitchJitter, stepPitchJitter);
        float volume = Mathf.Clamp01(stepVolume * (1f - Random.Range(0f, stepVolumeJitter)));
        stepSource.PlayOneShot(stepClip, volume);
    }

    /// <summary>脚步单独挂在一个子物体上，而不是复用玩家身上的 AudioSource：
    /// AudioLowPassFilter 会作用于同一个 GameObject 上的所有 AudioSource，
    /// 挂在玩家身上会把交互音效一起闷掉。</summary>
    private void CreateStepSource()
    {
        var holder = new GameObject("Footstep Audio");
        holder.transform.SetParent(transform, false);
        stepSource = holder.AddComponent<AudioSource>();
        stepSource.playOnAwake = false;
        stepSource.loop = false;
        stepSource.spatialBlend = 0f;
        stepSource.dopplerLevel = 0f;
        stepFilter = holder.AddComponent<AudioLowPassFilter>();
        stepFilter.cutoffFrequency = Mathf.Clamp(stepLowPassHz, 500f, 22000f);
    }
}
