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
        if (upperBody == null)
        {
            Debug.LogError("BodyReversal requires an upper-body VisionSource reference.", this);
            enabled = false;
        }
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
