using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 独立拉杆门：拉杆和门都可以随便摆，走近拉杆按 F 就把门打开。
///
/// 门用的还是项目里原来那套 —— Door.prefab + Door.controller，靠 Animator 的
/// <c>DoorOpen</c> 布尔开关；开门时同时禁用挡路的碰撞块、把拉杆手柄转过去并换材质，
/// 行为对齐 Level03Flow / Level04Flow 里的拉杆逻辑（那边是 2.6 米内按 E）。
///
/// 用法：
///   1. 拉杆物体（或任意空物体）上挂本组件，Lever 留空 = 用自己。
///   2. Doors 里拖入门的 Animator（不是 GameObject）。
///   3. Barriers 里拖入挡路的碰撞块（可选）。
/// </summary>
[DisallowMultipleComponent]
public sealed class LeverDoorSwitch : MonoBehaviour
{
    [Header("拉杆")]
    [Tooltip("拉杆位置，判定「站得够不够近」用它。留空 = 用挂脚本的这个物体。")]
    [SerializeField] private Transform lever;

    [Tooltip("玩家站多近才能拨动（米）。原来 Level03/04 用的是 2.6。")]
    [SerializeField, Min(0.1f)] private float reachDistance = 2.6f;

    [Tooltip("拨动拉杆的按键。")]
    [SerializeField] private Key activationKey = Key.F;

    [Tooltip("拨动后手柄的旋转角度。原来 Level03/04 用的是 Z 轴 -35°。")]
    [SerializeField] private Vector3 thrownEuler = new Vector3(0f, 0f, -35f);

    [Tooltip("true = 只能拨一次（门开了就一直开着）；false = 再按一次可以复位关门。")]
    [SerializeField] private bool oneShot = true;

    [Tooltip("要求玩家落地了才允许拨动。")]
    [SerializeField] private bool requireGrounded = true;

    [Tooltip("要求玩家和拉杆之间没有实体遮挡（Level03/04 是开着的）。")]
    [SerializeField] private bool requireLineOfSight;

    [Header("手柄外观（可选，拨动后会转角度并换材质）")]
    [SerializeField] private Transform handle;
    [SerializeField] private Renderer handleRenderer;
    [SerializeField] private Material activeMaterial;

    [Header("门（自由放置：把门的 Animator 拖进来）")]
    [SerializeField] private Animator[] doors;

    [Tooltip("门的 Animator 布尔参数名，Door.controller 用的就是 DoorOpen。")]
    [SerializeField] private string doorParameter = "DoorOpen";

    [Tooltip("开门时要禁用的挡路碰撞块（Level03/04 里叫 Closed X 的那块）。")]
    [SerializeField] private Collider[] barriers;

    [Header("玩家（留空会自动找场景里的白盒玩家）")]
    [SerializeField] private CharacterController player;

    private Material idleMaterial;
    private string feedback;
    private float feedbackUntil;
    private bool thrown;

    public bool Thrown => thrown;

    private void Awake()
    {
        if (lever == null) lever = transform;
        if (handle == null && handleRenderer != null) handle = handleRenderer.transform;
        if (handleRenderer == null && handle != null) handleRenderer = handle.GetComponent<Renderer>();
        if (handleRenderer != null) idleMaterial = handleRenderer.sharedMaterial;

        if (doors == null) return;
        foreach (Animator door in doors)
        {
            if (door == null) continue;
            if (door.GetComponent<DoorAnimatorToggle>() != null)
            {
                Debug.LogWarning("[LeverDoorSwitch] " + door.name
                    + " 上还挂着 DoorAnimatorToggle，按 E 也会直接开关这扇门；建议把这个组件删掉。", door);
            }
        }
    }

    private void Start()
    {
        if (player != null) return;
        WhiteboxPlayerMovement movement = Object.FindAnyObjectByType<WhiteboxPlayerMovement>();
        if (movement != null) player = movement.GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (oneShot && thrown) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard[activationKey].wasPressedThisFrame) return;
        if (player == null) return;

        if (requireGrounded && !player.isGrounded)
        {
            Say("落地之后再拨动拉杆。");
            return;
        }
        if (Vector3.Distance(player.transform.position, lever.position) > reachDistance)
        {
            Say("走到拉杆旁边再按 " + activationKey + "。");
            return;
        }
        if (requireLineOfSight && BlockedOff())
        {
            Say("机关被实体隔开，先走到它旁边。");
            return;
        }

        SetThrown(!thrown);
    }

    /// <summary>玩家和拉杆之间隔着实体就不允许操作（和 Level03/04 的写法一致）。</summary>
    private bool BlockedOff()
    {
        Vector3 start = player.transform.position;
        Vector3 end = lever.position;
        start.y = end.y;
        float distance = Vector3.Distance(start, end);
        if (distance < 0.01f) return false;

        foreach (RaycastHit hit in Physics.RaycastAll(start, (end - start) / distance,
            Mathf.Max(0f, distance - 0.45f), Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(player.transform)) continue;
            if (hit.collider.transform.IsChildOf(lever)) continue;
            return true;
        }
        return false;
    }

    /// <summary>拨动/复位：门、挡路块、手柄外观一起切换。也可以从别的脚本直接调。</summary>
    public void SetThrown(bool open)
    {
        thrown = open;

        if (doors != null)
        {
            foreach (Animator door in doors)
            {
                if (door != null) door.SetBool(doorParameter, open);
            }
        }
        if (barriers != null)
        {
            foreach (Collider barrier in barriers)
            {
                if (barrier != null) barrier.enabled = !open;
            }
        }
        if (handle != null)
            handle.localRotation = open ? Quaternion.Euler(thrownEuler) : Quaternion.identity;
        if (handleRenderer != null && activeMaterial != null && idleMaterial != null)
            handleRenderer.sharedMaterial = open ? activeMaterial : idleMaterial;

        Say(open ? "拉杆已接通：门开了。" : "拉杆复位：门关了。");
    }

    private void Say(string message)
    {
        feedback = message;
        feedbackUntil = Time.time + 3f;
    }

    private void OnGUI()
    {
        if (Time.time < feedbackUntil)
        {
            GUI.Label(new Rect(20f, Screen.height - 70f, 620f, 24f), feedback);
            return;
        }
        if (oneShot && thrown) return;
        if (player == null) return;
        if (Vector3.Distance(player.transform.position, lever.position) > reachDistance) return;
        GUI.Label(new Rect(20f, Screen.height - 70f, 620f, 24f),
            activationKey + "  拨动拉杆开门");
    }
}
