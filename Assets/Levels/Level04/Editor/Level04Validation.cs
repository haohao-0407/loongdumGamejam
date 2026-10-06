using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Level04Validation
{
    public static List<Vector2Int> Path(Vector2Int start, Vector2Int end, string open)
    {
        var pending = new Queue<Vector2Int>(); var previous = new Dictionary<Vector2Int, Vector2Int>();
        pending.Enqueue(start); previous[start] = start;
        var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (pending.Count > 0)
        {
            var p = pending.Dequeue();
            if (p == end)
            {
                var result = new List<Vector2Int>();
                for (var t = end; ; t = previous[t]) { result.Add(t); if (t == start) break; }
                result.Reverse(); return result;
            }
            foreach (var direction in directions)
            {
                var t = p + direction; char ch = Level04Layout.At(t.x, t.y);
                if (previous.ContainsKey(t) || ch == ' ' || "#GABC".Contains(ch)) continue;
                if ("XDYZa".Contains(ch) && !open.Contains(ch)) continue;
                previous[t] = p; pending.Enqueue(t);
            }
        }
        return new List<Vector2Int>();
    }
    public static List<Vector2Int>[] Routes() => new[] {
        Path(new Vector2Int(16,15),new Vector2Int(13,13),""),
        Path(new Vector2Int(8,13),new Vector2Int(6,14),""),
        Path(new Vector2Int(6,14),new Vector2Int(14,21),"XD"),
        Path(new Vector2Int(13,13),new Vector2Int(10,4),"XD"),
        Path(new Vector2Int(10,4),new Vector2Int(14,5),"XD"),
        Path(new Vector2Int(14,5),new Vector2Int(4,5),"XDY"),
        Path(new Vector2Int(4,5),new Vector2Int(14,21),"XDYZ")
    };
    public static object Audit()
    {
        var f = UnityEngine.Object.FindAnyObjectByType<Level04Flow>();
        if (f == null || Application.isPlaying) throw new InvalidOperationException("Open Level04 in Edit Mode.");
        bool initialOnlyCourtyard = Path(new Vector2Int(16,15),new Vector2Int(14,5),"").Count == 0;
        bool centralNeedsA = Path(new Vector2Int(8,13),new Vector2Int(14,21),"").Count == 0;
        bool westNeedsB = Path(new Vector2Int(14,5),new Vector2Int(4,5),"XD").Count == 0;
        bool returnNeedsC = Path(new Vector2Int(4,5),new Vector2Int(14,21),"XDY").Count == 0;
        var failures = new HashSet<string>(); int samples = 0;
        var saved = f.barriers.Select(b => b.enabled).ToArray();
        try
        {
            foreach (var b in f.barriers) b.enabled = false; Physics.SyncTransforms();
            foreach (var route in Routes())
            {
                if (route.Count == 0) { failures.Add("Missing route"); continue; }
                for (int i = 0; i < route.Count; i++)
                {
                    var a = Level04Layout.Position(route[i].x,route[i].y,1.08f);
                    var b = i + 1 < route.Count ? Level04Layout.Position(route[i+1].x,route[i+1].y,1.08f) : a;
                    for (int step = 0; step < 6; step++)
                    {
                        var p = Vector3.Lerp(a,b,step/6f); samples++;
                        foreach (var hit in Physics.OverlapCapsule(p-Vector3.up*.5f,p+Vector3.up*.5f,.42f,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                        {
                            if (hit.transform.IsChildOf(f.player.transform)||hit.transform.IsChildOf(f.source.transform)) continue;
                            failures.Add(route[i] + " blocked by " + hit.name);
                        }
                    }
                }
            }
        }
        finally { for (int i=0;i<saved.Length;i++) f.barriers[i].enabled=saved[i]; Physics.SyncTransforms(); }
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int missing = scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
            .Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        return new {initialOnlyCourtyard,centralNeedsA,westNeedsB,returnNeedsC,samples,failures=failures.ToArray(),missing,
            tiles=UnityEngine.Object.FindObjectsByType<ReversalTile>().Length,
            portals=UnityEngine.Object.FindObjectsByType<VisionPortal>().Length,
            globalDoorToggles=UnityEngine.Object.FindObjectsByType<DoorAnimatorToggle>().Length};
    }
}
