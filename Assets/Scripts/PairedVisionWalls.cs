using UnityEngine;
using System.Collections.Generic;

/// <summary>Two solid walls relay front-face sight to the other wall's back face.
/// Uses the existing VisionPortal ray clipping and distance budget; never moves actors.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class PairedVisionWalls : MonoBehaviour
{
    [SerializeField] private BoxCollider wallA;
    [SerializeField] private BoxCollider wallB;
    [Tooltip("World-space clearance outside the collider faces, to avoid starting inside a wall.")]
    [SerializeField, Range(.002f, .05f)] private float faceClearance = .01f;

    private VisionPortal entryA, backA, entryB, backB;
    private BoxCollider builtA, builtB;
    private static readonly Dictionary<VisionPortal, BoxCollider> entryWalls = new Dictionary<VisionPortal, BoxCollider>();

    internal static bool TryGetWall(VisionPortal entry, out BoxCollider wall)
        => entryWalls.TryGetValue(entry, out wall);

    public BoxCollider WallA => wallA;
    public BoxCollider WallB => wallB;
    public bool IsOperational => gameObject.scene.IsValid() && gameObject.scene.isLoaded
        && isActiveAndEnabled && Valid(wallA) && Valid(wallB)
        && wallA != wallB && MatchingApertures();

    public void Configure(BoxCollider first, BoxCollider second)
    {
        wallA = first; wallB = second;
        RefreshGeometry();
    }

    // VisionSource renders in Edit Mode too. Keep the same relay alive in both modes,
    // including after loading a scene or returning from Play with scene reload disabled.
    private void OnEnable() => RefreshGeometry();
    private void LateUpdate() => RefreshGeometry();
    private void OnDisable() => ClearEndpoints();
    private void OnDestroy() => ClearEndpoints();

    /// <summary>Also usable for an explicit editor preview/test. Generated endpoints are not assets.</summary>
    public void RefreshGeometry()
    {
        if (!IsOperational)
        {
            ClearEndpoints();
            return;
        }
        if (builtA != wallA || builtB != wallB || entryA == null || entryB == null
            || backA == null || backB == null)
        {
            ClearEndpoints();
            builtA = wallA; builtB = wallB;
            entryA = Endpoint("Wall A front", wallA);
            backA = Endpoint("Wall A back", wallA);
            entryB = Endpoint("Wall B front", wallB);
            backB = Endpoint("Wall B back", wallB);
            // Exit-only apertures must not pick up sight behind their own wall and relay it again.
            entryA.LinkedPortal = backB;
            entryB.LinkedPortal = backA;
            entryWalls[entryA] = wallA;
            entryWalls[entryB] = wallB;
        }
        Place(entryA, wallA, true); Place(backA, wallA, false);
        Place(entryB, wallB, true); Place(backB, wallB, false);
        // Repair links/registration if Unity retained objects across a mode or domain transition.
        entryA.LinkedPortal = backB; entryB.LinkedPortal = backA;
        entryWalls[entryA] = wallA; entryWalls[entryB] = wallB;
    }

    private static VisionPortal Endpoint(string name, BoxCollider wall)
    {
        var go = new GameObject("Paired vision · " + name);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(wall.transform, false);
        return go.AddComponent<VisionPortal>();
    }

    private void Place(VisionPortal endpoint, BoxCollider wall, bool front)
    {
        var scale = wall.transform.lossyScale;
        float halfDepth = wall.size.z * .5f + Mathf.Clamp(faceClearance, .002f, .05f) / scale.z;
        endpoint.transform.localPosition = wall.center + Vector3.forward * (front ? halfDepth : -halfDepth);
        endpoint.transform.localRotation = front ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
        // VisionPortal defaults to a 2 x 3 aperture. Use transform scale without modifying its API.
        endpoint.transform.localScale = new Vector3(wall.size.x / 2f, wall.size.y / 3f, 1f);
    }

    private static bool Valid(BoxCollider wall)
    {
        if (wall == null || !wall.enabled || wall.isTrigger || !wall.gameObject.activeInHierarchy) return false;
        var scale = wall.transform.lossyScale;
        if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0
            || wall.size.x <= 0 || wall.size.y <= 0 || wall.size.z <= 0) return false;
        // The existing vision model is horizontal. Reject tilted or sheared walls rather than leak sight.
        return Vector3.Dot(wall.transform.up, Vector3.up) > .9999f
            && Mathf.Abs(Vector3.Dot(wall.transform.TransformVector(Vector3.right).normalized,
                wall.transform.TransformVector(Vector3.forward).normalized)) < .0001f;
    }

    private bool MatchingApertures()
    {
        var a = Vector3.Scale(wallA.size, wallA.transform.lossyScale);
        var b = Vector3.Scale(wallB.size, wallB.transform.lossyScale);
        return Mathf.Abs(a.x - b.x) < .001f && Mathf.Abs(a.y - b.y) < .001f;
    }

    private void ClearEndpoints()
    {
        foreach (var portal in new[] { entryA, backA, entryB, backB })
        {
            if (portal == null) continue;
            entryWalls.Remove(portal);
            portal.enabled = false; // Remove it from the active list immediately, including mid-frame disable.
            if (Application.isPlaying) Destroy(portal.gameObject);
            else DestroyImmediate(portal.gameObject);
        }
        entryA = backA = entryB = backB = null;
        builtA = builtB = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (wallA == null || wallB == null) return;
        Gizmos.color = IsOperational ? Color.cyan : Color.gray;
        Gizmos.DrawLine(wallA.bounds.center, wallB.bounds.center);
        foreach (var wall in new[] { wallA, wallB })
        {
            var center = wall.transform.TransformPoint(wall.center);
            Gizmos.DrawRay(center, wall.transform.forward * 2);
        }
    }
}
