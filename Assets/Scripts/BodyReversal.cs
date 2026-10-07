using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Exchanges body positions only while the lower body stands on a reversal tile.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class BodyReversal : MonoBehaviour
{
    [SerializeField] private VisionSource upperBody;
    [SerializeField] private Key activationKey = Key.F;

    private CharacterController controller;
    private string feedback;
    private float feedbackUntil;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (upperBody == null) upperBody = ResolveUpperBody();
        if (upperBody == null)
        {
            Debug.LogError("BodyReversal requires an upper-body VisionSource reference.", this);
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
            float halfHeight = controller.height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
            Vector3 feet = transform.TransformPoint(controller.center) - Vector3.up * halfHeight;
            return ReversalTile.ContainsFootPosition(feet);
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard[activationKey].wasPressedThisFrame)
            TryReverse();
    }

    public bool TryReverse()
    {
        if (!CanReverse) return false;

        Vector3 lowerPosition = transform.position;
        Vector3 upperPosition = upperBody.transform.position;
        // The two bodies have different ground offsets; swapping Y would bury the capsule.
        Vector3 destination = new Vector3(upperPosition.x, lowerPosition.y, upperPosition.z);
        if (DestinationBlocked(destination))
        {
            feedback = "Reversal blocked: destination occupied";
            feedbackUntil = Time.time + 2f;
            return false;
        }

        // Disable the controller while teleporting so it does not retain its old physics pose.
        controller.enabled = false;
        transform.position = destination;
        upperBody.transform.position = new Vector3(lowerPosition.x, upperPosition.y, lowerPosition.z);
        controller.enabled = true;
        Physics.SyncTransforms();
        feedback = "Reversal complete";
        feedbackUntil = Time.time + 2f;
        return true;
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
            : CanReverse ? activationKey + "  /  Reverse upper and lower bodies" : null;
        if (message != null)
            GUI.Label(new Rect(20, Screen.height - 45, 440, 30), message);
    }
}
