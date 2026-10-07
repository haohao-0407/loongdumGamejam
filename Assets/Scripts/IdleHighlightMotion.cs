using UnityEngine;

/// <summary>
/// 闲置状态下的「可交互提示」表现：物体缓慢自转 + 上下浮动。
/// 注意：必须挂在轴心位于几何中心的父物体（pivot）上，否则自转会变成绕圈甩动。
/// </summary>
[DisallowMultipleComponent]
public sealed class IdleHighlightMotion : MonoBehaviour
{
    [Header("旋转 Rotation")]
    [Tooltip("旋转轴（世界空间）。默认绕世界竖直轴 Y 水平自转。")]
    public Vector3 rotationAxis = Vector3.up;

    [Tooltip("旋转速度（度/秒）。0 = 不旋转；负值 = 反向。")]
    public float rotationSpeed = 45f;

    [Header("上下浮动 Float")]
    [Tooltip("是否启用上下浮动。")]
    public bool enableFloat = true;

    [Tooltip("浮动幅度（米）。")]
    public float floatAmplitude = 0.08f;

    [Tooltip("浮动频率（次/秒）。")]
    public float floatFrequency = 1.2f;

    [Header("其他")]
    [Tooltip("启用后使用 unscaledTime：游戏暂停（Time.timeScale = 0）时提示动画仍会播放。")]
    public bool useUnscaledTime = false;

    private Vector3 startPosition;
    private Quaternion startRotation;
    private float currentAngle;
    private float elapsed;

    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        elapsed += dt;

        // 绕世界轴自转：pivot 原点即几何中心，所以旋转不会改变物体位置。
        if (rotationSpeed != 0f && rotationAxis.sqrMagnitude > 0f)
        {
            currentAngle += rotationSpeed * dt;
            transform.rotation = Quaternion.AngleAxis(currentAngle, rotationAxis.normalized) * startRotation;
        }

        // 围绕初始位置做正弦上下浮动。
        if (enableFloat && floatAmplitude != 0f)
        {
            float offsetY = Mathf.Sin(elapsed * floatFrequency * Mathf.PI * 2f) * floatAmplitude;
            transform.position = startPosition + Vector3.up * offsetY;
        }
    }

    private void OnValidate()
    {
        if (floatFrequency < 0f) floatFrequency = 0f;
        if (floatAmplitude < 0f) floatAmplitude = 0f;
    }
}
