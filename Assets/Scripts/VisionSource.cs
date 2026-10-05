using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VisionSource : MonoBehaviour
{
    public enum ObstacleFilter
    {
        Layer,
        Tag,
        LayerOrTag
    }

    [Header("Vision Range")]
    [SerializeField, Min(0.01f)] private float visionRadius = 5f;
    [SerializeField, Min(0f)] private float edgeSoftness = 0.2f;
    [SerializeField] private Camera targetCamera;

    [Header("Line of Sight")]
    [SerializeField] private bool obstacleOcclusion = true;
    [Tooltip("LayerOrTag: 满足 Layer 或 Tag 任意一个条件就会阻挡视线。")]
    [SerializeField] private ObstacleFilter obstacleFilter = ObstacleFilter.LayerOrTag;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private string[] obstacleTags = { "VisionObstacle" };
    [Tooltip("检查碰撞体及其父对象，支持在模型根对象上设置 Tag/Layer。")]
    [SerializeField] private bool checkParents = true;
    [Tooltip("水平视线相对于视野源位置的高度。低于射线的障碍不会挡住视线。")]
    [SerializeField] private float sightHeight = 1f;
    [Tooltip("射线数量越高，遮挡边缘越精细；同时增加物理查询开销。")]
    [SerializeField, Range(64, 2048)] private int rayCount = 512;
    [Tooltip("把遮挡起点延伸至碰撞体包围盒后缘，让障碍本体可见。关闭后从射线命中处遮挡。")]
    [SerializeField] private bool keepObstacleVisible = true;
    [Tooltip("保留障碍表面附近的一小段可见区域，避免表面闪烁。")]
    [SerializeField, Min(0f)] private float surfacePadding = 0.05f;

    [Header("Vision Portals")]
    [SerializeField] private bool portalVision = true;
    [Tooltip("允许视线连续穿过的门数量。每个视野源最多生成 8 个出口视野。")]
    [SerializeField, Range(1, 4)] private int maxPortalHops = 1;

    private const int MaxPortalViews = 8;
    private const float PortalOffset = 0.002f;
    private static readonly int PortalCountId = Shader.PropertyToID("_VisionPortalCount");
    private static readonly int PortalOriginsId = Shader.PropertyToID("_VisionPortalOrigins");
    private static readonly int PortalDistancesId = Shader.PropertyToID("_VisionPortalDistances");

    private sealed class VisibilityView
    {
        public Vector3 origin;
        public int parent;
        public int depth;
        public VisionPortal entry;
        public VisionPortal gate;
        public float[] minimum;
        public float[] maximum;
        public readonly List<VisionPortal> reachedPortals = new List<VisionPortal>();
    }

    private VisibilityView[] views;
    private Texture2D portalTexture;
    private Vector2[] portalRanges;
    private readonly Vector4[] portalOrigins = new Vector4[MaxPortalViews];
    private int portalViewCount;
    public int PortalViewCount => portalViewCount;

    private static readonly int PositionRadiusId = Shader.PropertyToID("_VisionSourcePositionRadius");
    private static readonly int SoftnessId = Shader.PropertyToID("_VisionEdgeSoftness");
    private static readonly int EnabledId = Shader.PropertyToID("_VisionMaskEnabled");
    private static readonly int OcclusionEnabledId = Shader.PropertyToID("_VisionOcclusionEnabled");
    private static readonly int DistancesId = Shader.PropertyToID("_VisionObstacleDistances");
    private static readonly int RayCountId = Shader.PropertyToID("_VisionRayCount");
    private static readonly int SurfacePaddingId = Shader.PropertyToID("_VisionSurfacePadding");

    private readonly Dictionary<Collider, bool> obstacleCache = new Dictionary<Collider, bool>();
    private RaycastHit[] raycastHits = new RaycastHit[16];
    private Texture2D visibilityTexture;
    private float[] visibilityDistances;
    private Vector3[] rayDirections;
    private Vector3 lastRayOrigin;
    private float lastRayRadius;

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
        Shader.SetGlobalFloat(OcclusionEnabledId, 0f);
        Shader.SetGlobalFloat(PortalCountId, 0f);
        Shader.SetGlobalTexture(DistancesId, Texture2D.whiteTexture);
        ReleaseVisibilityTexture();
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
        RefreshVisibilityTexture();
        Shader.SetGlobalFloat(OcclusionEnabledId,
            obstacleOcclusion || (portalVision && VisionPortal.Active.Count > 0) ? 1f : 0f);
        Shader.SetGlobalTexture(DistancesId, visibilityTexture);
        Shader.SetGlobalFloat(RayCountId, rayDirections.Length);
        Shader.SetGlobalFloat(SurfacePaddingId, Mathf.Max(0f, surfacePadding));
        Shader.SetGlobalFloat(PortalCountId, portalViewCount);
        if (portalViewCount > 0)
        {
            Shader.SetGlobalVectorArray(PortalOriginsId, portalOrigins);
            Shader.SetGlobalTexture(PortalDistancesId, portalTexture);
        }
    }

    private void RefreshVisibilityTexture()
    {
        int count = Mathf.Clamp(rayCount, 64, 2048);
        if (visibilityTexture == null || visibilityTexture.width != count)
        {
            ReleaseVisibilityTexture();
            visibilityTexture = new Texture2D(count, 1, TextureFormat.RFloat, false, true)
            {
                name = "Vision Obstacle Distances",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            visibilityDistances = new float[count];
            rayDirections = new Vector3[count];
            views = new VisibilityView[MaxPortalViews + 1];
            for (int v = 0; v < views.Length; v++)
                views[v] = new VisibilityView { minimum = new float[count], maximum = new float[count] };
            portalRanges = new Vector2[count * MaxPortalViews];
            for (int i = 0; i < count; i++)
            {
                float angle = i * (2f * Mathf.PI / count);
                rayDirections[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
        }

        Physics.SyncTransforms();
        obstacleCache.Clear();
        lastRayOrigin = transform.position + Vector3.up * sightHeight;
        lastRayRadius = Mathf.Max(0.01f, visionRadius);
        portalViewCount = 0;
        BuildView(0, lastRayOrigin, -1, null);
        for (int parent = 0; parent <= portalViewCount && portalVision; parent++)
        {
            VisibilityView view = views[parent];
            if (view.depth >= Mathf.Clamp(maxPortalHops, 1, 4)) continue;
            foreach (VisionPortal entry in view.reachedPortals)
            {
                if (portalViewCount >= MaxPortalViews) break;
                // Do not revisit a pair within the same path.
                bool visited = false;
                for (int ancestor = parent; ancestor > 0; ancestor = views[ancestor].parent)
                    if (views[ancestor].entry == entry || views[ancestor].gate == entry
                        || views[ancestor].entry == entry.LinkedPortal)
                    { visited = true; break; }
                if (!visited && BuildView(portalViewCount + 1,
                    entry.MapPointToExit(view.origin), parent, entry)) portalViewCount++;
            }
        }
        for (int i = 0; i < count; i++)
            visibilityDistances[i] = views[0].maximum[i] / lastRayRadius;
        visibilityTexture.SetPixelData(visibilityDistances, 0);
        visibilityTexture.Apply(false, false);
        if (portalViewCount == 0) return;
        if (portalTexture == null)
            portalTexture = new Texture2D(count, MaxPortalViews, TextureFormat.RGFloat, false, true)
            {
                name = "Vision Portal Distances", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
            };
        for (int v = 0; v < portalViewCount; v++)
        {
            VisibilityView view = views[v + 1];
            portalOrigins[v] = new Vector4(view.origin.x, view.origin.y, view.origin.z, lastRayRadius);
            for (int i = 0; i < count; i++)
                portalRanges[v * count + i] = new Vector2(view.minimum[i], view.maximum[i]) / lastRayRadius;
        }
        portalTexture.SetPixelData(portalRanges, 0);
        portalTexture.Apply(false, false);
    }

    private bool BuildView(int index, Vector3 origin, int parent, VisionPortal entry)
    {
        VisibilityView view = views[index];
        view.origin = origin;
        view.parent = parent;
        view.depth = parent < 0 ? 0 : views[parent].depth + 1;
        view.entry = entry;
        view.gate = entry == null ? null : entry.LinkedPortal;
        view.reachedPortals.Clear();
        bool hasVisibleRay = false;
        // Query all layers so a parent's Layer can also classify a child collider.
        int queryLayers = obstacleFilter == ObstacleFilter.Layer && !checkParents
            ? obstacleLayers.value : Physics.AllLayers;

        for (int i = 0; i < rayDirections.Length; i++)
        {
            Vector3 direction = rayDirections[i];
            float minimum = 0f;
            if (entry != null && (!view.gate.TryIntersect(origin, direction, lastRayRadius,
                out minimum, true, Mathf.Min(entry.HalfWidth, view.gate.HalfWidth))
                || !ContainsPoint(views[parent], entry.MapPointFromExit(origin + direction * minimum))))
            {
                view.minimum[i] = lastRayRadius + 1f;
                view.maximum[i] = 0f;
                continue;
            }
            float start = entry == null ? 0f : minimum + PortalOffset;
            Vector3 rayOrigin = origin + direction * start;
            int hitCount = 0;
            if (obstacleOcclusion && start < lastRayRadius)
            {
                hitCount = Physics.RaycastNonAlloc(rayOrigin, direction, raycastHits,
                    lastRayRadius - start, queryLayers, QueryTriggerInteraction.Ignore);
                while (hitCount == raycastHits.Length)
                {
                    Array.Resize(ref raycastHits, raycastHits.Length * 2);
                    hitCount = Physics.RaycastNonAlloc(rayOrigin, direction, raycastHits,
                        lastRayRadius - start, queryLayers, QueryTriggerInteraction.Ignore);
                }
            }
            float visibleDistance = lastRayRadius;
            Collider nearestObstacle = null;
            for (int h = 0; h < hitCount; h++)
            {
                RaycastHit hit = raycastHits[h];
                if (start + hit.distance < visibleDistance && IsObstacle(hit.collider))
                {
                    visibleDistance = start + hit.distance;
                    nearestObstacle = hit.collider;
                }
            }
            float blockingDistance = visibleDistance;
            if (keepObstacleVisible && nearestObstacle != null)
            {
                // Reveal the blocking collider's footprint; its shadow begins at the far edge.
                Bounds bounds = nearestObstacle.bounds;
                float exitX = Mathf.Abs(direction.x) > 0.00001f
                    ? ((direction.x > 0f ? bounds.max.x : bounds.min.x) - origin.x) / direction.x
                    : float.PositiveInfinity;
                float exitZ = Mathf.Abs(direction.z) > 0.00001f
                    ? ((direction.z > 0f ? bounds.max.z : bounds.min.z) - origin.z) / direction.z
                    : float.PositiveInfinity;
                visibleDistance = Mathf.Min(lastRayRadius, Mathf.Min(exitX, exitZ));
            }
            VisionPortal nearestPortal = null;
            float portalDistance = blockingDistance;
            if (portalVision)
                foreach (VisionPortal portal in VisionPortal.Active)
                    if (portal != null && portal != view.gate && portal.CanTransmit
                        && portal.TryIntersect(origin, direction, portalDistance, out float distance,
                            false, Mathf.Min(portal.HalfWidth, portal.LinkedPortal.HalfWidth))
                        && distance >= start)
                    { nearestPortal = portal; portalDistance = distance; }
            if (nearestPortal != null)
            {
                visibleDistance = portalDistance;
                if (!view.reachedPortals.Contains(nearestPortal)) view.reachedPortals.Add(nearestPortal);
            }
            view.minimum[i] = minimum;
            view.maximum[i] = visibleDistance;
            hasVisibleRay |= visibleDistance >= minimum;
        }
        return hasVisibleRay;
    }

    private bool ContainsPoint(VisibilityView view, Vector3 point)
    {
        Vector3 offset = point - view.origin;
        float distance = new Vector2(offset.x, offset.z).magnitude;
        int index = Mathf.RoundToInt(Mathf.Atan2(offset.z, offset.x) * rayDirections.Length / (2f * Mathf.PI));
        index = (index % rayDirections.Length + rayDirections.Length) % rayDirections.Length;
        return distance >= view.minimum[index] - PortalOffset
            && distance <= view.maximum[index] + surfacePadding && distance <= lastRayRadius;
    }

    /// <summary>Queries the latest horizontal visibility, including portal exits.</summary>
    public bool IsPointVisible(Vector3 point)
    {
        if (views == null) return false;
        for (int v = 0; v <= portalViewCount; v++)
            if (ContainsPoint(views[v], point)) return true;
        return false;
    }

    private bool IsObstacle(Collider collider)
    {
        if (obstacleCache.TryGetValue(collider, out bool cached))
            return cached;

        bool blocks = false;
        if (!collider.transform.IsChildOf(transform))
        {
            for (Transform candidate = collider.transform; candidate != null;
                candidate = checkParents ? candidate.parent : null)
            {
                bool layerMatches = (obstacleLayers.value & (1 << candidate.gameObject.layer)) != 0;
                if (obstacleFilter != ObstacleFilter.Tag && layerMatches)
                {
                    blocks = true;
                    break;
                }

                if (obstacleFilter != ObstacleFilter.Layer && obstacleTags != null)
                {
                    foreach (string obstacleTag in obstacleTags)
                    {
                        // String comparison also tolerates tags that have not been created yet.
                        if (!string.IsNullOrEmpty(obstacleTag)
                            && string.Equals(candidate.tag, obstacleTag, StringComparison.Ordinal))
                        {
                            blocks = true;
                            break;
                        }
                    }
                }
                if (blocks)
                    break;
            }
        }
        obstacleCache[collider] = blocks;
        return blocks;
    }

    private void ReleaseVisibilityTexture()
    {
        if (visibilityTexture == null)
            return;
        if (Application.isPlaying)
            Destroy(visibilityTexture);
        else
            DestroyImmediate(visibilityTexture);
        visibilityTexture = null;
        if (portalTexture != null)
        {
            if (Application.isPlaying) Destroy(portalTexture);
            else DestroyImmediate(portalTexture);
        }
        portalTexture = null;
        views = null;
        portalRanges = null;
        portalViewCount = 0;
        visibilityDistances = null;
        rayDirections = null;
    }

    private void OnValidate()
    {
        visionRadius = Mathf.Max(0.01f, visionRadius);
        edgeSoftness = Mathf.Clamp(edgeSoftness, 0f, visionRadius);
        rayCount = Mathf.Clamp(rayCount, 64, 2048);
        surfacePadding = Mathf.Max(0f, surfacePadding);
        maxPortalHops = Mathf.Clamp(maxPortalHops, 1, 4);
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

        if (obstacleOcclusion && visibilityDistances != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < visibilityDistances.Length; i++)
            {
                int next = (i + 1) % visibilityDistances.Length;
                Gizmos.DrawLine(lastRayOrigin + rayDirections[i] * (visibilityDistances[i] * lastRayRadius),
                    lastRayOrigin + rayDirections[next] * (visibilityDistances[next] * lastRayRadius));
            }
        }
    }
}
