using System.Collections.Generic;
using UnityEngine;

/// <summary>A vertical aperture that redirects horizontal vision to a linked aperture.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class VisionPortal : MonoBehaviour
{
    [SerializeField] private VisionPortal linkedPortal;
    [SerializeField, Min(0.05f)] private float width = 2f;
    [SerializeField, Min(0.05f)] private float height = 3f;
    [Tooltip("关闭时只有正面可以进入视线。蓝色箭头指向正面。")]
    [SerializeField] private bool twoSided;

    private static readonly List<VisionPortal> active = new List<VisionPortal>();
    public static IReadOnlyList<VisionPortal> Active => active;
    public VisionPortal LinkedPortal { get => linkedPortal; set => linkedPortal = value; }
    public bool CanTransmit => isActiveAndEnabled && linkedPortal != null
        && linkedPortal != this && linkedPortal.isActiveAndEnabled;
    public float HalfWidth => width * Mathf.Abs(transform.lossyScale.x) * 0.5f;
    public float HalfHeight => height * Mathf.Abs(transform.lossyScale.y) * 0.5f;
    public Vector3 Forward => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
    public Vector3 Right => Vector3.Cross(Vector3.up, Forward);

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    private void OnDisable() => active.Remove(this);

    public bool TryIntersect(Vector3 origin, Vector3 direction, float maxDistance,
        out float distance, bool ignoreFacing = false, float apertureHalfWidth = -1f)
    {
        distance = 0f;
        Vector3 forward = Forward;
        float denominator = Vector3.Dot(direction, forward);
        if (Mathf.Abs(denominator) < 0.00001f
            || (!ignoreFacing && !twoSided && denominator >= 0f)) return false;
        distance = Vector3.Dot(transform.position - origin, forward) / denominator;
        if (distance <= 0.001f || distance > maxDistance) return false;
        Vector3 local = origin + direction * distance - transform.position;
        float halfWidth = apertureHalfWidth >= 0f ? apertureHalfWidth : HalfWidth;
        return Mathf.Abs(Vector3.Dot(local, Right)) <= halfWidth
            && Mathf.Abs(local.y) <= HalfHeight;
    }

    // Use horizontal orthonormal bases, so scale does not add or remove sight distance.
    public Vector3 MapPointToExit(Vector3 point) => MapPoint(this, linkedPortal, point);
    public Vector3 MapPointFromExit(Vector3 point) => MapPoint(linkedPortal, this, point);

    private static Vector3 MapPoint(VisionPortal from, VisionPortal to, Vector3 point)
    {
        Vector3 offset = point - from.transform.position;
        return to.transform.position - to.Right * Vector3.Dot(offset, from.Right)
            + Vector3.up * offset.y - to.Forward * Vector3.Dot(offset, from.Forward);
    }

    private void OnValidate()
    {
        width = Mathf.Max(0.05f, width);
        height = Mathf.Max(0.05f, height);
    }

    private void OnDrawGizmos()
    {
        Vector3 center = transform.position;
        Vector3 right = Right * HalfWidth;
        Vector3 up = Vector3.up * HalfHeight;
        Gizmos.color = CanTransmit ? Color.cyan : Color.gray;
        Gizmos.DrawLine(center - right - up, center + right - up);
        Gizmos.DrawLine(center + right - up, center + right + up);
        Gizmos.DrawLine(center + right + up, center - right + up);
        Gizmos.DrawLine(center - right + up, center - right - up);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(center, Forward);
        if (linkedPortal != null)
        {
            Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
            Gizmos.DrawLine(center, linkedPortal.transform.position);
        }
    }
}
