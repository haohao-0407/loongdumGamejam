using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class DoorAnimatorToggle : MonoBehaviour
{
    private static readonly int DoorOpenParameter = Animator.StringToHash("DoorOpen");

    private Animator doorAnimator;

    private void Awake()
    {
        doorAnimator = GetComponent<Animator>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
        {
            return;
        }

        doorAnimator.SetBool(DoorOpenParameter, !doorAnimator.GetBool(DoorOpenParameter));
    }
}
