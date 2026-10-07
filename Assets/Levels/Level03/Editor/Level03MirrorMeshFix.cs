using System;
using UnityEditor;
using UnityEngine;

/// <summary>镜面模型默认不可读，VisionPortalMirrorFit 取不到顶点就会跳过，光门口径对不上镜面轮廓。</summary>
public static class Level03MirrorMeshFix
{
    private const string MirrorModelPath = "Assets/Prefabs/mirror2.fbx";

    [MenuItem("Tools/Loongdum/Level 03/Fix Mirror Mesh Read Write")]
    public static void Fix()
    {
        ModelImporter importer = AssetImporter.GetAtPath(MirrorModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("No imported model at " + MirrorModelPath + ".");
        if (importer.isReadable)
        {
            Debug.Log("[VisionPortalMirrorFit] " + MirrorModelPath + " is already readable; nothing to change.");
            return;
        }
        importer.isReadable = true;
        importer.SaveAndReimport();
        Debug.Log("[VisionPortalMirrorFit] Read/Write enabled on " + MirrorModelPath
            + "; the light gates can now follow the mirror outline.");
    }
}
