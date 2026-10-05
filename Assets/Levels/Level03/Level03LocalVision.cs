using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Adds a small foot-side sensing view through the existing mask's
/// portal-view inputs. Does not change shared shaders or VisionSource.</summary>
[DefaultExecutionOrder(100)]
public sealed class Level03LocalVision : MonoBehaviour
{
    public VisionSource source;
    public Transform lowerBody;
    public Camera levelCamera;
    private Texture2D combined;
    private Vector2[] ranges;
    private readonly Vector4[] origins = new Vector4[8];
    private void OnEnable() => RenderPipelineManager.beginCameraRendering += AddLocalView;
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= AddLocalView;
        if (combined != null) Destroy(combined);
    }
    private void AddLocalView(ScriptableRenderContext context, Camera camera)
    {
        if (camera != levelCamera || !source.isActiveAndEnabled) return;
        int count = (int)Shader.GetGlobalFloat("_VisionRayCount");
        int portalCount = (int)Shader.GetGlobalFloat("_VisionPortalCount");
        if (count < 64 || portalCount >= 8) return;
        if (combined == null || combined.width != count)
        {
            if (combined != null) Destroy(combined);
            combined = new Texture2D(count, 8, TextureFormat.RGFloat, false, true)
            { name = "Level03 Foot Sensing", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            ranges = new Vector2[count * 8];
        }
        if (portalCount > 0)
        {
            var existing = Shader.GetGlobalTexture("_VisionPortalDistances") as Texture2D;
            if (existing == combined || existing == null) return;
            var data = existing.GetPixelData<Vector2>(0);
            for (int i = 0; i < portalCount * count; i++) ranges[i] = data[i];
            var sourceOrigins = Shader.GetGlobalVectorArray("_VisionPortalOrigins");
            for (int i = 0; i < portalCount; i++) origins[i] = sourceOrigins[i];
        }
        float radius = Level03Layout.CellSize * 1.12f;
        Vector3 origin = lowerBody.position;
        origin.y = source.transform.position.y;
        origins[portalCount] = new Vector4(origin.x, origin.y, origin.z, radius);
        Physics.SyncTransforms();
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2f * Mathf.PI / count;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            float limit = radius;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, radius, Physics.AllLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(lowerBody) || hit.collider.transform.IsChildOf(source.transform)) continue;
                // Give wall surfaces some visibility without exposing what is behind them.
                limit = Mathf.Min(limit, hit.distance + .12f);
            }
            ranges[portalCount * count + i] = new Vector2(0, limit / radius);
        }
        combined.SetPixelData(ranges, 0);
        combined.Apply(false, false);
        Shader.SetGlobalVectorArray("_VisionPortalOrigins", origins);
        Shader.SetGlobalTexture("_VisionPortalDistances", combined);
        Shader.SetGlobalFloat("_VisionPortalCount", portalCount + 1);
    }
}
