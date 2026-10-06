using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
public sealed class WhiteboxPlayerMovement : MonoBehaviour
{
    [SerializeField, Min(0f)] private float moveSpeed = 5f;

    [Tooltip("让 WASD 跟随相机朝向：相机在上方俯视时，W 始终是画面里的“向上”。默认关闭，保持原有世界坐标方向。")]
    [SerializeField] private bool alignToCamera;

    [Tooltip("用来取朝向的相机。留空则用 Camera.main。")]
    [SerializeField] private Camera inputCamera;

    private CharacterController controller;
    private float verticalSpeed;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Vector3 direction = Vector3.zero;

        if (keyboard != null)
        {
            float rawX = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float rawZ = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            direction = new Vector3(rawX, 0f, rawZ);

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
                        direction = right * rawX + forward * rawZ;
                    }
                }
            }

            direction = Vector3.ClampMagnitude(direction, 1f);
        }

        if (controller.isGrounded && verticalSpeed < 0f)
        {
            verticalSpeed = -10f;
        }

        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        Vector3 velocity = direction * moveSpeed + Vector3.up * verticalSpeed;
        controller.Move(velocity * Time.deltaTime);
    }
}
