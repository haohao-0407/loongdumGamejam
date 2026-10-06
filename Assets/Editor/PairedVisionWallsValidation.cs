using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated real physics/visibility tests. Never opens or saves a gameplay scene.</summary>
public static class PairedVisionWallsValidation
{
    /// <summary>Call after the Game view renders, before/after each real editor mode transition.</summary>
    public static object CheckLoadedDemo()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/Tests/PairedVisionWalls_Test.unity")
            throw new InvalidOperationException("Open the independent wall demo first.");
        var pair = UnityEngine.Object.FindAnyObjectByType<PairedVisionWalls>();
        var source = UnityEngine.Object.FindAnyObjectByType<VisionSource>();
        int endpoints = pair.GetComponentsInChildren<VisionPortal>(true).Length;
        bool remote = source.IsPointVisible(new Vector3(14, 1.25f, -4));
        bool localShadow = source.IsPointVisible(new Vector3(0, 1.25f, -3));
        bool wallVisible = source.IsPointVisible(new Vector3(0, 1.25f, 0));
        if (!pair.IsOperational || endpoints != 4 || VisionPortal.Active.Count != 4
            || source.PortalViewCount != 1 || !remote || localShadow || !wallVisible)
            throw new InvalidOperationException("Relay failed after editor lifecycle transition.");
        return new { playing = Application.isPlaying, endpoints, views = source.PortalViewCount,
            remote, localShadow, wallVisible, sceneDirty = scene.isDirty };
    }

    [MenuItem("Tools/Loongdum/Paired Vision Walls/Validate")]
    public static void RunMenu() => Debug.Log(Newtonsoft.Json.JsonConvert.SerializeObject(Run(), Newtonsoft.Json.Formatting.Indented));

    public static object Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
        var activeScene = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var checks = new List<string>();
        Vector3 offset = new Vector3(1000, 0, 1000);
        GameObject Root(string name)
        {
            var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
        }
        BoxCollider Wall(string name, Vector3 position, Vector3 size)
        {
            var go = Root(name); go.layer = 8; go.tag = "VisionObstacle";
            go.transform.position = offset + position;
            var collider = go.AddComponent<BoxCollider>(); collider.size = size; return collider;
        }
        void Check(bool ok, string name)
        {
            if (!ok) throw new InvalidOperationException("FAIL: " + name);
            checks.Add(name);
        }
        try
        {
            var a = Wall("Wall A", new Vector3(0, 1.5f, 0), new Vector3(3, 3, .4f));
            var b = Wall("Wall B", new Vector3(20, 1.5f, 0), new Vector3(3, 3, .4f));
            var pair = Root("Pair").AddComponent<PairedVisionWalls>(); pair.Configure(a, b);
            var source = Root("Upper vision").AddComponent<VisionSource>();
            source.transform.position = offset + new Vector3(0, .25f, 4);
            var data = new SerializedObject(source);
            data.FindProperty("visionRadius").floatValue = 10;
            data.FindProperty("rayCount").intValue = 1024;
            data.FindProperty("maxPortalHops").intValue = 3;
            data.FindProperty("obstacleLayers").intValue = 1 << 8;
            data.ApplyModifiedPropertiesWithoutUndo();
            var refresh = typeof(VisionSource).GetMethod("RefreshVisibilityTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            void Refresh() { pair.RefreshGeometry(); refresh.Invoke(source, null); }
            bool Seen(float x, float z) => source.IsPointVisible(offset + new Vector3(x, 1.25f, z));

            Refresh();
            Check(pair.IsOperational && source.PortalViewCount == 1, "Front wall activates exactly one relay");
            Check(Seen(20, -2), "A front relays to B back");
            Check(Seen(0, 0), "The transmitting wall itself stays visible");
            data.Update(); data.FindProperty("keepObstacleVisible").boolValue = false; data.ApplyModifiedPropertiesWithoutUndo();
            Refresh(); Check(Seen(0, 0) && !Seen(0, -2), "Paired wall remains visible even with Whitebox1's obstacle visibility setting");
            data.Update(); data.FindProperty("keepObstacleVisible").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            Check(!Seen(0, -2), "A continues to block its own local shadow");
            Check(!Seen(20, 2), "B front does not receive a new omnidirectional light");
            Check(Seen(20, -5.5f) && !Seen(20, -7), "Input and output path share the original ten-metre distance budget");
            Check(Physics.Raycast(offset + new Vector3(0, 1.25f, 4), Vector3.back, out var hit, 6, 1 << 8)
                && hit.collider == a, "Wall remains a solid physics obstacle");

            var blocker = Wall("Input blocker", new Vector3(0, 1.5f, 2), new Vector3(4, 3, .3f));
            Refresh(); Check(!Seen(20, -2) && source.PortalViewCount == 0, "Occluded entrance does not relay");
            blocker.transform.position = offset + new Vector3(-.75f, 1.5f, 2);
            blocker.size = new Vector3(1.5f, 3, .3f);
            Refresh(); Check(!Seen(19, -2) && Seen(21, -2), "Partial input occlusion crops the corresponding exit rays");
            blocker.transform.position = offset + new Vector3(20, 1.5f, -1);
            blocker.size = new Vector3(4, 3, .3f);
            Refresh(); Check(!Seen(20, -2), "Output obstacles still block sight");
            blocker.enabled = false;

            b.transform.rotation = Quaternion.Euler(0, 90, 0);
            Refresh(); Check(Seen(18, 0) && !Seen(20, -2), "Rotating B maps sight to its rotated back face");
            b.transform.rotation = Quaternion.identity;
            source.transform.position = offset + new Vector3(20, .25f, 4);
            Refresh(); Check(Seen(0, -2), "B front symmetrically relays to A back");
            source.transform.position = offset + new Vector3(0, .25f, -4);
            Refresh(); Check(!Seen(20, -2), "Looking at A from its back does not activate its front");
            source.transform.position = offset + new Vector3(0, .25f, 12);
            Refresh(); Check(!Seen(20, -2), "Entrance outside source radius stays inactive");
            source.transform.position = offset + new Vector3(0, .25f, 4);

            pair.enabled = false; refresh.Invoke(source, null);
            Check(!Seen(20, -2) && a.enabled && b.enabled, "Disabling relay leaves both solid walls in place");
            pair.enabled = true; Refresh(); Check(Seen(20, -2), "Re-enabling rebuilds relay endpoints");
            b.enabled = false; Refresh(); Check(!Seen(20, -2), "Disabled wall stops both directions");
            b.enabled = true; b.size = new Vector3(2, 3, .4f);
            Refresh(); Check(!pair.IsOperational && !Seen(20, -2), "Mismatched apertures fail closed");
            b.size = new Vector3(3, 3, .4f); b.transform.rotation = Quaternion.Euler(20, 0, 0);
            Refresh(); Check(!pair.IsOperational, "Tilted wall fails closed for horizontal vision");
            b.transform.rotation = Quaternion.identity; b.transform.position += Vector3.right * 5;
            Refresh(); Check(Seen(25, -2) && !Seen(20, -2), "Moving a wall updates the exit without retaining old visibility");
            b.transform.position -= Vector3.right * 5;

            var c = Wall("Second input", new Vector3(20, 1.5f, -3), new Vector3(3, 3, .4f));
            var d = Wall("Second output", new Vector3(40, 1.5f, 0), new Vector3(3, 3, .4f));
            var second = Root("Second pair").AddComponent<PairedVisionWalls>(); second.Configure(c, d);
            data.Update(); data.FindProperty("maxPortalHops").intValue = 1; data.ApplyModifiedPropertiesWithoutUndo();
            Refresh(); Check(!Seen(40, -2), "One-hop limit prevents a second relay");
            data.Update(); data.FindProperty("maxPortalHops").intValue = 2; data.ApplyModifiedPropertiesWithoutUndo();
            Refresh(); Check(Seen(40, -2) && source.PortalViewCount == 2, "Two-hop chain works within the shared distance budget");
            data.Update(); data.FindProperty("portalVision").boolValue = false; data.ApplyModifiedPropertiesWithoutUndo();
            Refresh(); Check(!Seen(20, -2) && source.PortalViewCount == 0, "VisionSource Portal Vision toggle controls wall relays too");
            data.Update(); data.FindProperty("portalVision").boolValue = true; data.ApplyModifiedPropertiesWithoutUndo();
            pair.enabled = false; second.enabled = false;
            var ordinaryEntry = Root("Ordinary portal entry").AddComponent<VisionPortal>();
            var ordinaryExit = Root("Ordinary portal exit").AddComponent<VisionPortal>();
            ordinaryEntry.transform.position = offset + new Vector3(0, 1.5f, .21f);
            ordinaryExit.transform.position = offset + new Vector3(20, 1.5f, -.21f);
            ordinaryExit.transform.rotation = Quaternion.Euler(0, 180, 0);
            ordinaryEntry.transform.localScale = ordinaryExit.transform.localScale = new Vector3(1.5f, 1, 1);
            ordinaryEntry.LinkedPortal = ordinaryExit;
            refresh.Invoke(source, null);
            Check(Seen(20, -2) && !Seen(0, 0), "Ordinary portals retain their original aperture clipping behavior");
            return new { success = true, count = checks.Count, checks = checks.ToArray(), isolated = true };
        }
        finally
        {
            foreach (var root in scene.GetRootGameObjects()) if (root != null) UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(activeScene);
        }
    }
}
