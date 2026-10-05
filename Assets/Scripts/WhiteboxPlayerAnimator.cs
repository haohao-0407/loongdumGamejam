using UnityEngine;

/// <summary>
/// Bridges planar movement to the Animator and flips the sprite to face the
/// direction of travel. Read-only with respect to movement: it samples
/// <see cref="CharacterController.velocity"/> and never writes to the controller.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public sealed class WhiteboxPlayerAnimator : MonoBehaviour
{
    public const int StateIdle = 0;
    public const int StateSide = 1;
    public const int StateForward = 2;
    public const int StateBehind = 3;

    private static readonly int MoveStateParam = Animator.StringToHash("MoveState");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    [Tooltip("Planar speed below this counts as idle, in m/s.")]
    [SerializeField, Min(0f)] private float idleSpeedThreshold = 0.1f;

    [Tooltip("Share of the planar speed that must go along Z before the walk counts as forward/behind instead of side. 0.6 means diagonals read as forward/behind.")]
    [SerializeField, Range(0.1f, 1f)] private float forwardShareThreshold = 0.6f;

    [Tooltip("Sideways speed (m/s) required before the sprite flips. Walking straight forward or backward keeps the current facing.")]
    [SerializeField, Min(0f)] private float flipXThreshold = 0.1f;

    [Tooltip("Sprite to mirror. Leave empty to auto-grab a SpriteRenderer on this object or its children.")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private CharacterController controller;
    private Animator animator;

    /// <summary>Last state pushed to the Animator. Read-only, handy for debugging.</summary>
    public int CurrentState { get; private set; }

    /// <summary>Last planar speed pushed to the Animator. Read-only, handy for debugging.</summary>
    public float CurrentSpeed { get; private set; }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void LateUpdate()
    {
        // controller.velocity already accounts for collision correction, so walking
        // into a wall reads as ~0 and the state falls back to idle.
        Vector3 velocity = controller.velocity;
        Vector2 planar = new Vector2(velocity.x, velocity.z);
        float speed = planar.magnitude;

        int state = StateIdle;

        if (speed > idleSpeedThreshold)
        {
            // +1 = away from the camera (W), -1 = towards the camera (S).
            float forwardShare = planar.y / speed;

            if (Mathf.Abs(forwardShare) >= forwardShareThreshold)
            {
                state = forwardShare > 0f ? StateBehind : StateForward;
            }
            else
            {
                state = StateSide;
            }
        }

        CurrentState = state;
        CurrentSpeed = speed;

        animator.SetInteger(MoveStateParam, state);
        animator.SetFloat(SpeedParam, speed);

        UpdateFacing(planar);
    }

    /// <summary>
    /// The art faces right, so travelling left mirrors the sprite. The threshold
    /// keeps straight forward/backward travel from toggling the flip every frame.
    /// </summary>
    private void UpdateFacing(Vector2 planar)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (Mathf.Abs(planar.x) <= flipXThreshold)
        {
            return;
        }

        spriteRenderer.flipX = planar.x < 0f;
    }
}
