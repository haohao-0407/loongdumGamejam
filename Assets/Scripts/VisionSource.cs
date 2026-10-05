using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VisionSource : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float visionRadius = 5f;
    [SerializeField, Min(0f)] private float edgeSoftness = 0.2f;
    [SerializeField] private Camera targetCamera;

    private static readonly int PositionRadiusId = Shader.PropertyToID("_VisionSourcePositionRadius");
    private static readonly int SoftnessId = Shader.PropertyToID("_VisionEdgeSoftness");
    private static readonly int EnabledId = Shader.PropertyToID("_VisionMaskEnabled");

    private void Reset()
    {
        targetCamera = Camera.main;
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += UpdateVisionShader;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= UpdateVisionShader;
        Shader.SetGlobalFloat(EnabledId, 0f);
    }

    private void UpdateVisionShader(ScriptableRenderContext context, Camera camera)
    {
        bool showMask = camera.cameraType == CameraType.Game
            && (targetCamera == null || camera == targetCamera);
        Shader.SetGlobalFloat(EnabledId, showMask ? 1f : 0f);
        if (!showMask)
            return;

        Vector3 position = transform.position;
        Shader.SetGlobalVector(PositionRadiusId,
            new Vector4(position.x, position.y, position.z, Mathf.Max(0.01f, visionRadius)));
        Shader.SetGlobalFloat(SoftnessId, Mathf.Max(0f, edgeSoftness));
    }

    private void OnValidate()
    {
        visionRadius = Mathf.Max(0.01f, visionRadius);
        edgeSoftness = Mathf.Clamp(edgeSoftness, 0f, visionRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Vector3 previous = center + Vector3.right * visionRadius;
        const int segments = 64;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * (2f * Mathf.PI / segments);
            Vector3 next = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * visionRadius;
            Gizmos.DrawLine(previous, next);
            previous = next;
        }
    }
}
