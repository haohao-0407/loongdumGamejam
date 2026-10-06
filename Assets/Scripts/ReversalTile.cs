using System.Collections.Generic;
using UnityEngine;

/// <summary>A floor region in which the lower body can use reversal.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class ReversalTile : MonoBehaviour
{
    private static readonly List<ReversalTile> active = new List<ReversalTile>();
    private BoxCollider area;

    private void Awake()
    {
        area = GetComponent<BoxCollider>();
    }

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnEnable()
    {
        if (!active.Contains(this)) active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }

    public static bool ContainsFootPosition(Vector3 position)
    {
        foreach (ReversalTile tile in active)
        {
            if (tile.area == null || !tile.area.enabled) continue;
            Vector3 offset = tile.transform.InverseTransformPoint(position) - tile.area.center;
            Vector3 halfSize = tile.area.size * 0.5f;
            if (Mathf.Abs(offset.x) <= halfSize.x && Mathf.Abs(offset.y) <= halfSize.y
                && Mathf.Abs(offset.z) <= halfSize.z) return true;
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = previous;
    }
}
