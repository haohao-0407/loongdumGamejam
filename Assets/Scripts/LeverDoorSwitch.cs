using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 玩家进入拉杆范围后，通过 Input System 的 Interact 动作反复开关门。
/// 同步门动画、可选挡路碰撞块和手柄外观。
/// </summary>
[DisallowMultipleComponent]
public sealed class LeverDoorSwitch : MonoBehaviour
{
    private const string DefaultInteractPath = "Player/Interact";

    // 多个拉杆可以共用未被其他组件启用的 Action，最后一个停用时再释放它。
    private static readonly Dictionary<InputAction, int> ManagedActions = new Dictionary<InputAction, int>();

    [Header("拉杆")]
    [Tooltip("拉杆位置，判定「站得够不够近」用它。留空 = 用挂脚本的这个物体。")]
    [SerializeField] private Transform lever;

    [Tooltip("玩家站多近才能拨动（米）。原来 Level03/04 用的是 2.6。")]
    [SerializeField, Min(0.1f)] private float reachDistance = 2.6f;

    [Header("Input System")]
    [Tooltip("交互动作。留空时使用项目全局 Input Actions 中的 Player/Interact，支持其中配置的键盘、手柄和重绑定。")]
    [SerializeField] private InputActionReference interactAction;

    [Header("拉杆状态")]
    [Tooltip("拨动后手柄的旋转角度。原来 Level03/04 用的是 Z 轴 -35°。")]
    [SerializeField] private Vector3 thrownEuler = new Vector3(0f, 0f, -35f);

    [Tooltip("true = 只能拨一次（门开了就一直开着）；false = 再按一次可以复位关门。")]
    [SerializeField] private bool oneShot;

    [Tooltip("要求玩家落地了才允许拨动。")]
    [SerializeField] private bool requireGrounded;

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
    private Quaternion idleRotation;
    private InputAction input;
    private bool managesInput;
    private bool started;
    private string feedback;
    private float feedbackUntil;
    private bool thrown;

    public bool Thrown => thrown;

    public bool IsPlayerInRange => player != null && player.enabled && player.gameObject.activeInHierarchy
        && (player.transform.position - (lever != null ? lever.position : transform.position)).sqrMagnitude
            <= reachDistance * reachDistance;

    public bool CanInteract => isActiveAndEnabled && !(oneShot && thrown) && IsPlayerInRange
        && (!requireGrounded || player.isGrounded) && (!requireLineOfSight || !BlockedOff());

    private void Awake()
    {
        if (lever == null) lever = transform;
        if (handle == null && handleRenderer != null) handle = handleRenderer.transform;
        if (handleRenderer == null && handle != null) handleRenderer = handle.GetComponent<Renderer>();
        if (handleRenderer != null) idleMaterial = handleRenderer.sharedMaterial;
        if (handle != null) idleRotation = handle.localRotation;
    }

    private void Start()
    {
        started = true;
        if (player == null)
        {
            WhiteboxPlayerMovement movement = Object.FindAnyObjectByType<WhiteboxPlayerMovement>();
            if (movement != null) player = movement.GetComponent<CharacterController>();
        }

        // 以第一扇门的初始状态为准，避免已经打开的门第一次交互仍然执行开门。
        if (doors != null)
        {
            foreach (Animator door in doors)
            {
                if (door == null) continue;
                thrown = door.GetBool(doorParameter);
                break;
            }
        }
        ApplyState();
        BindInput();
    }

    private void OnEnable()
    {
        if (started) BindInput();
    }

    private void OnDisable()
    {
        ReleaseInput();
    }

    private void BindInput()
    {
        ReleaseInput();

        PlayerInput playerInput = player != null ? player.GetComponentInParent<PlayerInput>() : null;
        if (playerInput != null)
        {
            // PlayerInput 可能使用独立的 Action 副本，应跟随该玩家的重绑定和动作图切换。
            string actionId = interactAction != null && interactAction.action != null
                ? interactAction.action.id.ToString() : DefaultInteractPath;
            input = playerInput.actions != null ? playerInput.actions.FindAction(actionId) : null;
        }
        else
        {
            input = interactAction != null ? interactAction.action
                : InputSystem.actions != null ? InputSystem.actions.FindAction(DefaultInteractPath) : null;

            if (input != null && input.actionMap?.asset != InputSystem.actions)
            {
                if (ManagedActions.TryGetValue(input, out int users))
                {
                    ManagedActions[input] = users + 1;
                    managesInput = true;
                }
                else if (!input.enabled)
                {
                    ManagedActions.Add(input, 1);
                    managesInput = true;
                    input.Enable();
                }
            }
        }

        if (input == null)
            Debug.LogError("[LeverDoorSwitch] 找不到 Interact 动作。请设置项目全局 Player/Interact，或在 Interact Action 中指定动作。", this);
    }

    private void ReleaseInput()
    {
        if (managesInput && input != null && ManagedActions.TryGetValue(input, out int users))
        {
            if (users > 1) ManagedActions[input] = users - 1;
            else
            {
                ManagedActions.Remove(input);
                input.Disable();
            }
        }
        managesInput = false;
        input = null;
    }

    private void Update()
    {
        if (input != null && input.enabled && input.WasPressedThisFrame() && IsPlayerInRange)
            TryInteract();
    }

    /// <summary>仅当玩家处于范围内且满足条件时拨动拉杆。成功时返回 true。</summary>
    public bool TryInteract()
    {
        if (!isActiveAndEnabled || (oneShot && thrown) || !IsPlayerInRange) return false;
        if (requireGrounded && !player.isGrounded)
        {
            Say("落地之后再拨动拉杆。");
            return false;
        }
        if (requireLineOfSight && BlockedOff())
        {
            Say("机关被实体隔开，先走到它旁边。");
            return false;
        }

        SetThrown(!thrown);
        return true;
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
        ApplyState();
        Say(open ? "拉杆已接通：门开了。" : "拉杆复位：门关了。");
    }

    private void ApplyState()
    {
        if (doors != null)
        {
            foreach (Animator door in doors)
            {
                if (door == null) continue;
                DoorAnimatorToggle toggle = door.GetComponent<DoorAnimatorToggle>();
                if (toggle != null && doorParameter == DoorAnimatorToggle.OpenParameter)
                    toggle.SetOpen(thrown);
                else
                    door.SetBool(doorParameter, thrown);
            }
        }
        if (barriers != null)
        {
            foreach (Collider barrier in barriers)
            {
                if (barrier != null) barrier.enabled = !thrown;
            }
        }
        if (handle != null)
            handle.localRotation = thrown ? idleRotation * Quaternion.Euler(thrownEuler) : idleRotation;
        if (handleRenderer != null && activeMaterial != null && idleMaterial != null)
            handleRenderer.sharedMaterial = thrown ? activeMaterial : idleMaterial;
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
        if (input == null || !input.enabled || !CanInteract) return;
        string binding = input.GetBindingDisplayString();
        if (string.IsNullOrEmpty(binding)) binding = input.name;
        GUI.Label(new Rect(20f, Screen.height - 70f, 620f, 24f),
            binding + (thrown ? "  拨动拉杆关门" : "  拨动拉杆开门"));
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lever != null ? lever.position : transform.position, reachDistance);
    }
}
