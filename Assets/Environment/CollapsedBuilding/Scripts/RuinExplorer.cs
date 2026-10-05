using UnityEngine;
using UnityEngine.InputSystem;

namespace Loongdum.Environment
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class RuinExplorer : MonoBehaviour
    {
        public Camera viewCamera;
        public Light flashlight;
        public float walkSpeed = 3.5f;
        public float runSpeed = 5.8f;
        public float mouseSensitivity = 0.09f;
        CharacterController controller;
        Vector3 spawn;
        float verticalSpeed;
        float pitch;
        bool captured;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawn = transform.position;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) Capture(false);
            if (!captured && mouse.leftButton.wasPressedThisFrame) Capture(true);
            if (keyboard.rKey.wasPressedThisFrame || transform.position.y < -8) Respawn();
            if (keyboard.fKey.wasPressedThisFrame && flashlight != null) flashlight.enabled = !flashlight.enabled;
            if (!captured) return;

            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            transform.Rotate(0, delta.x, 0);
            pitch = Mathf.Clamp(pitch - delta.y, -82, 82);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            float x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
            float z = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
            Vector3 move = Vector3.ClampMagnitude(transform.right * x + transform.forward * z, 1);
            float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            if (controller.isGrounded)
            {
                verticalSpeed = -2;
                if (keyboard.spaceKey.wasPressedThisFrame) verticalSpeed = 4.2f;
            }
            else verticalSpeed -= 15 * Time.deltaTime;
            controller.Move((move * speed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        void Capture(bool value)
        {
            captured = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        void Respawn()
        {
            controller.enabled = false;
            transform.position = spawn;
            controller.enabled = true;
            verticalSpeed = 0;
        }

        void OnDisable() { Capture(false); }

        void OnGUI()
        {
            GUI.color = new Color(0.88f, 0.9f, 0.85f, 0.9f);
            GUI.Label(new Rect(22, 20, 340, 24), "NORTH TOWER  /  " + (transform.position.y > 3 ? "LEVEL 02" : "LEVEL 01"));
            GUI.Label(new Rect(22, Screen.height - 36, 700, 24), "WASD  Move    Shift  Run    Space  Jump    F  Flashlight    R  Return    Esc  Cursor");
            if (!captured) GUI.Label(new Rect(Screen.width / 2 - 85, Screen.height / 2 - 15, 240, 30), "Click to explore the ruin");
            else GUI.Label(new Rect(Screen.width / 2 - 3, Screen.height / 2 - 8, 20, 20), "+");
        }
    }
}
