using UnityEngine;

/// <summary>通过 DoorOpen 参数控制门的动画，由拉杆或其他交互脚本调用。</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class DoorAnimatorToggle : MonoBehaviour
{
    public const string OpenParameter = "DoorOpen";

    private static readonly int DoorOpenParameter = Animator.StringToHash(OpenParameter);

    private Animator doorAnimator;

    public bool IsOpen => GetAnimator().GetBool(DoorOpenParameter);

    private void Awake()
    {
        doorAnimator = GetComponent<Animator>();
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        GetAnimator().SetBool(DoorOpenParameter, open);
    }

    private Animator GetAnimator()
    {
        // 允许其他组件在自己的 Awake 中控制门，不依赖组件的 Awake 顺序。
        if (doorAnimator == null) doorAnimator = GetComponent<Animator>();
        return doorAnimator;
    }
}
