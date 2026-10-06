using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class Level03Validation
{
    // This structural audit is independent of Level03Flow. It checks the
    // diagram's access dependencies and compares its route to real colliders.
    public static object Audit()
    {
        var flow = UnityEngine.Object.FindAnyObjectByType<Level03Flow>();
        if (flow == null || Application.isPlaying) throw new InvalidOperationException("Open Level03 in Edit Mode.");
        bool initialReachesTile = Reach(new Vector2Int(16,8), new Vector2Int(13,4), "");
        bool initialCannotReachVault = !Reach(new Vector2Int(16,8), new Vector2Int(2,16), "");
        bool firstSwapCannotExitWithoutD = !Reach(new Vector2Int(2,16), new Vector2Int(10,17), "X");
        bool corridorCannotReturnWithoutZ = !Reach(new Vector2Int(10,17), new Vector2Int(13,4), "XD");
        bool finalCanReunite = Reach(new Vector2Int(13,4), new Vector2Int(10,17), "XDZ");
        var paths = new List<List<Vector2Int>> {
            Path(new Vector2Int(16,8),new Vector2Int(13,4),""),
            Path(new Vector2Int(2,16),new Vector2Int(4,16),""),
            Path(new Vector2Int(4,16),new Vector2Int(4,15),"X"),
            Path(new Vector2Int(4,15),new Vector2Int(10,17),"XD"),
            Path(new Vector2Int(13,4),new Vector2Int(16,6),"XD"),
            Path(new Vector2Int(16,6),new Vector2Int(9,7),"XD"),
            Path(new Vector2Int(9,7),new Vector2Int(10,17),"XDZ")
        };
        var failures = new List<string>();
        int samples = 0;
        foreach (var barrier in flow.barriers) barrier.enabled = false;
        Physics.SyncTransforms();
        foreach (var path in paths)
        {
            if (path.Count == 0) { failures.Add("No route"); continue; }
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 a=Level03Layout.Position(path[i].x,path[i].y,1.08f);
                Vector3 b=i+1<path.Count?Level03Layout.Position(path[i+1].x,path[i+1].y,1.08f):a;
                for(int step=0;step<6;step++)
                {
                    samples++;
                    Vector3 p=Vector3.Lerp(a,b,step/6f);
                    foreach(var collider in Physics.OverlapCapsule(p-Vector3.up*.5f,p+Vector3.up*.5f,.42f,Physics.AllLayers,QueryTriggerInteraction.Ignore))
                    {
                        if(collider.transform.IsChildOf(flow.player.transform)||collider.transform.IsChildOf(flow.source.transform))continue;
                        failures.Add(path[i]+" blocked by "+collider.name);
                    }
                }
            }
        }
        foreach (var barrier in flow.barriers) barrier.enabled = true;
        Physics.SyncTransforms();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        int missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true))
            .Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        return new { initialReachesTile, initialCannotReachVault, firstSwapCannotExitWithoutD,
            corridorCannotReturnWithoutZ, finalCanReunite, samples, failures=failures.Distinct().ToArray(), missing,
            tileCount=UnityEngine.Object.FindObjectsByType<ReversalTile>().Length,
            portalCount=UnityEngine.Object.FindObjectsByType<VisionPortal>().Length,
            movement="Continuous WASD; key dependency order is unique, walking paths are not." };
    }

    private static bool Reach(Vector2Int start,Vector2Int end,string open) => Path(start,end,open).Count>0;
    public static List<Vector2Int> Path(Vector2Int start,Vector2Int end,string open)
    {
        var queue=new Queue<Vector2Int>();
        var previous=new Dictionary<Vector2Int,Vector2Int>();
        queue.Enqueue(start);previous[start]=start;
        var dirs=new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right};
        while(queue.Count>0)
        {
            var point=queue.Dequeue();
            if(point==end)
            {
                var path=new List<Vector2Int>();
                for(var p=end;;p=previous[p]) {path.Add(p);if(p==start)break;}
                path.Reverse();return path;
            }
            foreach(var direction in dirs)
            {
                var next=point+direction;
                char ch=Level03Layout.At(next.x,next.y);
                if(previous.ContainsKey(next)||ch==' '||"#GABCS".Contains(ch))continue;
                if("XDZa".Contains(ch)&&!open.Contains(ch))continue;
                previous[next]=point;queue.Enqueue(next);
            }
        }
        return new List<Vector2Int>();
    }
}
