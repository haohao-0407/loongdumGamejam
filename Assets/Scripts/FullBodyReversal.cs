using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tile-gated reversal skill that swaps all three axes: the lower body takes the
/// upper body's complete position and the upper body takes the lower body's.
/// <see cref="BodyReversal"/> keeps each body's own height; this one does not.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class FullBodyReversal : MonoBehaviour
{
    [SerializeField] private VisionSource upperBody;
    [SerializeField] private Key activationKey = Key.F;

    [Header("音效（留空则不发声）")]
    [Tooltip("按 F 反转成功时播放的音效，例如「SFX_UI_BottonClick」。")]
    [SerializeField] private AudioClip interactClip;

    [Tooltip("交互音效音量。")]
    [SerializeField, Range(0f, 1f)] private float interactVolume = 1f;

    [Header("Height snap (optional)")]
    [Tooltip("After swapping, drop the lower body onto the ground found below the target point. Off by default so the height swap stays exact.")]
    [SerializeField] private bool snapLowerBodyToGround;

    [Tooltip("How far above the target point the downward probe starts, in meters.")]
    [SerializeField, Min(0.1f)] private float groundProbeRise = 5f;

    [Tooltip("Layers treated as ground by the probe.")]
    [SerializeField] private LayerMask groundMask = ~0;

    private CharacterController controller;
    private string feedback;
    private float feedbackUntil;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (upperBody == null) upperBody = ResolveUpperBody();
        if (upperBody == null)
        {
            Debug.LogError("FullBodyReversal requires an upper-body VisionSource reference.", this);
            enabled = false;
        }
    }

    /// <summary>
    /// 预制体(prefab)无法保存对其他场景对象的引用，所以「脚」从别的场景搬过来时
    /// upperBody 会变空。这里做一次自动配对，让「反转格 + 头 + 脚」搬到任何场景都能直接用：
    ///   1. 优先取与自身同一根节点下的 VisionSource（同一个玩法套装）；
    ///   2. 否则取场景里唯一的那一个；
    ///   3. 有多个又没有共同根时返回 null —— 宁可报错也不猜，避免接错身体。
    /// 在 Inspector 里手动接好的引用不受影响（不为空时不会走到这里）。
    /// </summary>
    private VisionSource ResolveUpperBody()
    {
        VisionSource[] all = FindObjectsByType<VisionSource>(FindObjectsSortMode.None);
        if (all.Length == 1) return all[0];

        Transform myRoot = transform.root;
        VisionSource sameRoot = null;
        int sameRootCount = 0;
        foreach (VisionSource candidate in all)
        {
            if (candidate.transform.root == myRoot) { sameRoot = candidate; sameRootCount++; }
        }
        return sameRootCount == 1 ? sameRoot : null;
    }

    public bool CanReverse
    {
        get
        {
            if (!isActiveAndEnabled || controller == null || !controller.enabled || !controller.isGrounded
                || upperBody == null || !upperBody.isActiveAndEnabled) return false;
            return ReversalTile.ContainsFootPosition(FeetPosition(transform));
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[activationKey].wasPressedThisFrame)
            TryReverse();
    }

    /// <summary>按 F 时调用的反转入口。真的换位成功才发交互音效。</summary>
    public bool TryReverse()
    {
        bool success = TryReverseCore();
        if (success)
            AudioOneShot.Play(interactClip, gameObject, interactVolume);
        return success;
    }

    private bool TryReverseCore()
    {
        if (!CanReverse) return false;

        Vector3 lowerPosition = transform.position;
        Vector3 upperPosition = upperBody.transform.position;

        // The only difference from BodyReversal: the target is the partner's full
        // position, so x, y and z all move. Rotation and scale stay untouched.
        Vector3 lowerDestination = snapLowerBodyToGround ? SnapToGround(upperPosition) : upperPosition;
        if (DestinationBlocked(lowerDestination))
        {
            feedback = "Reversal blocked: destination occupied";
            feedbackUntil = Time.time + 2f;
            return false;
        }

        // Disable the controller while teleporting so it does not retain its old physics pose.
        controller.enabled = false;
        transform.position = lowerDestination;
        upperBody.transform.position = lowerPosition;
        controller.enabled = true;
        Physics.SyncTransforms();
        feedback = "Reversal complete";
        feedbackUntil = Time.time + 2f;
        return true;
    }

    /// <summary>Foot position of a body driven by this controller, in world space.</summary>
    private Vector3 FeetPosition(Transform body)
    {
        float halfHeight = controller.height * Mathf.Abs(body.lossyScale.y) * 0.5f;
        return body.TransformPoint(controller.center) - Vector3.up * halfHeight;
    }

    private Vector3 SnapToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * groundProbeRise;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, groundProbeRise * 2f,
            groundMask, QueryTriggerInteraction.Ignore);

        float nearest = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)
                || hit.collider.transform.IsChildOf(upperBody.transform)) continue;
            if (hit.distance < nearest) nearest = hit.distance;
        }
        if (float.IsPositiveInfinity(nearest)) return position;

        float groundY = origin.y - nearest;
        float pivotToFeet = FeetPosition(transform).y - transform.position.y;
        return new Vector3(position.x, groundY - pivotToFeet, position.z);
    }

    private bool DestinationBlocked(Vector3 position)
    {
        Vector3 scale = transform.lossyScale;
        float radius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfHeight = Mathf.Max(radius, controller.height * Mathf.Abs(scale.y) * 0.5f);
        Vector3 center = position + transform.TransformVector(controller.center);
        // Allow the controller's skin-width contact with the floor, while rejecting solid walls.
        float inset = Mathf.Min(controller.skinWidth, radius * 0.5f);
        Vector3 offset = Vector3.up * (halfHeight - radius);
        Physics.SyncTransforms();
        foreach (Collider obstacle in Physics.OverlapCapsule(center - offset, center + offset,
            radius - inset, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (obstacle.transform.IsChildOf(transform)
                || obstacle.transform.IsChildOf(upperBody.transform)) continue;
            return true;
        }
        return false;
    }

    private void OnGUI()
    {
        string message = Time.time < feedbackUntil ? feedback
            : CanReverse ? activationKey + "  /  Reverse all three axes" : null;
        if (message != null)
            GUI.Label(new Rect(20, Screen.height - 45, 440, 30), message);
    }
}
