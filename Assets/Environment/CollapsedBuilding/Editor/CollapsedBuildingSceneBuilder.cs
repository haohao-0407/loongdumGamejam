using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using Loongdum.Environment;

public static class CollapsedBuildingSceneBuilder
{
    const string Root = "Assets/Environment/CollapsedBuilding";
    const string ScenePath = "Assets/Scenes/CollapsedBuilding_Interior.unity";

    [MenuItem("Tools/Loongdum/Build Collapsed Building Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before rebuilding.");
        var active = EditorSceneManager.GetActiveScene();
        if (active.isDirty) EditorSceneManager.SaveScene(active);
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var mats = Materials();
        var modelPath = Root + "/Models/CollapsedBuilding.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.globalScale = 1;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null) throw new InvalidOperationException("Blender FBX has not imported.");
        var building = UnityEngine.Object.Instantiate(source);
        building.name = "CollapsedBuilding / Blender Architecture";
        building.transform.position = Vector3.zero;
        // Blender FBX import rotates horizontal axes; align authored X/Y to Unity X/Z.
        building.transform.rotation = Quaternion.Euler(0,180,0);
        foreach (var renderer in building.GetComponentsInChildren<MeshRenderer>())
        {
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m != null && mats.ContainsKey(m.name) ? mats[m.name] : m).ToArray();
            var filter = renderer.GetComponent<MeshFilter>();
            if (renderer.name.StartsWith("COL_") && filter != null)
            {
                var collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic);
        }
        var bounds = CombinedBounds(building);
        if (bounds.size.x < 30 || bounds.size.x > 42 || bounds.size.y > 22)
            throw new InvalidOperationException("Unexpected FBX units/orientation: " + bounds);
        MeshyProps();
        Lighting();
        Explorer();
        var navObject = new GameObject("Navigation / Both Floors");
        var surface = navObject.AddComponent<NavMeshSurface>();
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.collectObjects = CollectObjects.All;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.12f;
        surface.BuildNavMesh();
        surface.navMeshData.name="CollapsedBuildingNavMesh";
        string navPath = Root + "/CollapsedBuildingNavMesh.asset";
        // Preserve the asset GUID when rebuilding so scene references stay stable.
        var previousData = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
        if (previousData == null) AssetDatabase.CreateAsset(surface.navMeshData, navPath);
        else
        {
            EditorUtility.CopySerialized(surface.navMeshData, previousData);
            surface.RemoveData();
            surface.navMeshData = previousData;
            surface.AddData();
        }
        PrefabUtility.SaveAsPrefabAsset(building, Root + "/Prefabs/CollapsedBuildingArchitecture.prefab");
        EditorSceneManager.SaveScene(scene, ScenePath);
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == ScenePath)) scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        Validate();
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(0, 3, 0), Quaternion.Euler(26, 25, 0), 23);
        Debug.Log("Collapsed building scene saved: " + ScenePath);
    }

    static Dictionary<string, Material> Materials()
    {
        var colors = new Dictionary<string, Color> {
            {"Ruin_Concrete",new Color(.73f,.71f,.66f)}, {"Ruin_Plaster",new Color(.82f,.79f,.7f)},
            {"Ruin_Tile",new Color(.31f,.32f,.29f)}, {"Ruin_RustedSteel",new Color(.3f,.15f,.06f)},
            {"Ruin_DarkMetal",new Color(.08f,.105f,.11f)}, {"Ruin_Wood",new Color(.31f,.21f,.11f)},
            {"Ruin_Black",new Color(.025f,.033f,.035f)}, {"Ruin_Paper",new Color(.66f,.61f,.48f)},
            {"Ruin_ExitGreen",new Color(.03f,.25f,.11f)}, {"Ruin_SignWhite",new Color(.82f,.86f,.7f)},
            {"Ruin_Lamp",new Color(.9f,.42f,.08f)} };
        string normalPath = Root + "/Textures/concrete_wall_008_nor_gl_2k.jpg";
        var ti = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
        var diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/concrete_wall_008_Diffuse_2k.jpg");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (diffuse == null || normal == null) throw new InvalidOperationException("Concrete PBR maps missing.");
        var result = new Dictionary<string, Material>();
        foreach (var pair in colors)
        {
            var path = Root + "/Materials/" + pair.Key + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", pair.Value);
            m.SetFloat("_Smoothness", .12f);
            m.SetFloat("_Metallic", pair.Key.Contains("Steel") || pair.Key.Contains("Metal") ? .55f : 0);
            if (pair.Key == "Ruin_Concrete" || pair.Key == "Ruin_Plaster")
            {
                m.SetTexture("_BaseMap",diffuse);
                m.SetTexture("_BumpMap",normal);
                m.SetFloat("_BumpScale",.6f);
                m.EnableKeyword("_NORMALMAP");
            }
            if (pair.Key == "Ruin_Lamp" || pair.Key == "Ruin_ExitGreen")
            { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor",pair.Value * 1.8f); }
            EditorUtility.SetDirty(m);
            result.Add(pair.Key,m);
        }
        return result;
    }

    static Bounds CombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static void MeshyProps()
    {
        var props = new GameObject("Meshy / Generated Set Dressing");
        PlaceMeshy("RebarConcreteDebris", new[] {new Vector3(5.8f,0,2.2f),new Vector3(12.2f,0,8),new Vector3(-5.5f,0,-2),new Vector3(12,4.2f,2)}, 3.4f, props.transform);
        PlaceMeshy("DamagedOfficeDesk", new[] {new Vector3(12,0,-7),new Vector3(-14,0,-1),new Vector3(15,4.2f,-9)}, 1.8f, props.transform);
    }

    static void PlaceMeshy(string name, Vector3[] positions, float size, Transform parent)
    {
        var files = Directory.GetFiles(Root + "/Meshy", "*.fbx", SearchOption.AllDirectories).Where(p => p.Replace('\\','/').Contains(name)).ToArray();
        if (files.Length == 0) throw new InvalidOperationException("Meshy model not ready: " + name);
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(files[0].Replace('\\','/'));
        var material = MeshyMaterial(name, files[0].Replace('\\','/'));
        int i = 0;
        foreach (var position in positions)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = name + "_" + (++i);
            go.transform.SetParent(parent);
            var b = CombinedBounds(go);
            go.transform.localScale *= size / Mathf.Max(b.size.x, Mathf.Max(b.size.y,b.size.z));
            go.transform.rotation = Quaternion.Euler(0,i*71,0);
            b = CombinedBounds(go);
            go.transform.position += position - new Vector3(b.center.x,b.min.y,b.center.z);
            foreach(var renderer in go.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            // Preserve desk leg openings with accurate static collision.
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null) filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
        }
    }

    static Material MeshyMaterial(string name, string modelPath)
    {
        var folder = Root + "/Meshy/Textures/" + name;
        Directory.CreateDirectory(folder);
        if(!File.Exists(folder + "/Image_0.jpg"))
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.ExtractTextures(folder);
            importer.SaveAndReimport();
        }
        var normalPath = folder + "/Image_2.jpg";
        var ni = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if(ni != null && ni.textureType != TextureImporterType.NormalMap)
        { ni.textureType = TextureImporterType.NormalMap; ni.SaveAndReimport(); }
        string packedPath = folder + "/MetallicSmoothness.png";
        if(!File.Exists(packedPath))
        {
            var metalPath=folder+"/texture_0_metallic.png";
            var roughPath=folder+"/texture_0_roughness.png";
            foreach(var p in new[]{metalPath,roughPath})
            {
                var ti=(TextureImporter)AssetImporter.GetAtPath(p);
                ti.sRGBTexture=false; ti.isReadable=true; ti.textureCompression=TextureImporterCompression.Uncompressed; ti.SaveAndReimport();
            }
            var metal=AssetDatabase.LoadAssetAtPath<Texture2D>(metalPath);
            var rough=AssetDatabase.LoadAssetAtPath<Texture2D>(roughPath);
            var pixels=metal.GetPixels32(); var roughPixels=rough.GetPixels32();
            for(int i=0;i<pixels.Length;i++) pixels[i]=new Color32(pixels[i].r,0,0,(byte)(255-roughPixels[i].r));
            var packed=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,false,true);
            packed.SetPixels32(pixels); packed.Apply();
            File.WriteAllBytes(packedPath,packed.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(packedPath);
            var pi=(TextureImporter)AssetImporter.GetAtPath(packedPath); pi.sRGBTexture=false; pi.SaveAndReimport();
            foreach(var p in new[]{metalPath,roughPath})
            { var ti=(TextureImporter)AssetImporter.GetAtPath(p); ti.isReadable=false; ti.textureCompression=TextureImporterCompression.Compressed; ti.SaveAndReimport(); }
        }
        var path=Root+"/Materials/Meshy_"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
        m.SetColor("_BaseColor",new Color(.85f,.85f,.8f));
        m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Image_0.jpg"));
        m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        m.SetFloat("_BumpScale",.85f); m.EnableKeyword("_NORMALMAP");
        m.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));
        m.SetFloat("_Smoothness",.65f); m.EnableKeyword("_METALLICSPECGLOSSMAP");
        EditorUtility.SetDirty(m);
        return m;
    }

    static Light Light(string name, Vector3 position, Color color, float intensity, float range, LightType type)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var l = go.AddComponent<Light>();
        l.type=type; l.color=color; l.intensity=intensity; l.range=range;
        return l;
    }

    static void Lighting()
    {
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.62f,.69f,.77f);
        RenderSettings.ambientEquatorColor=new Color(.45f,.49f,.53f);
        RenderSettings.ambientGroundColor=new Color(.25f,.24f,.22f);
        RenderSettings.ambientIntensity=1.3f;
        RenderSettings.fog=true;
        RenderSettings.fogMode=FogMode.ExponentialSquared;
        RenderSettings.fogColor=new Color(.25f,.29f,.32f);
        RenderSettings.fogDensity=.011f;
        var sky = new Material(Shader.Find("Skybox/Procedural"));
        sky.name="RuinSky";
        sky.SetColor("_SkyTint",new Color(.42f,.49f,.56f));
        sky.SetFloat("_Exposure",1.0f);
        sky.SetFloat("_AtmosphereThickness",1.25f);
        string path = Root + "/Materials/RuinSky.mat";
        var oldSky=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(oldSky==null) AssetDatabase.CreateAsset(sky,path);
        else { EditorUtility.CopySerialized(sky,oldSky); UnityEngine.Object.DestroyImmediate(sky); sky=oldSky; }
        RenderSettings.skybox=sky;
        DynamicGI.UpdateEnvironment();
        var sun=Light("Sun / Through Broken Roof",new Vector3(8,15,8),new Color(1,.88f,.7f),1.5f,100,LightType.Directional);
        sun.transform.rotation=Quaternion.Euler(48,-32,0);
        sun.shadows=LightShadows.Soft;
        sun.shadowBias=.03f;
        RenderSettings.sun=sun;
        Light("Atrium / Sky Bounce",new Vector3(0,6.5f,2),new Color(.69f,.8f,1),35,20,LightType.Point);
        Light("Entrance / Daylight Bounce",new Vector3(0,2.5f,-11),new Color(.77f,.83f,1),12,15,LightType.Point);
        foreach(var p in new[]{new Vector3(-12,2.8f,0),new Vector3(12,2.8f,-7),new Vector3(-13,6.8f,-7),new Vector3(12,6.8f,0),new Vector3(-13.5f,3.5f,8)})
            Light("Room / Ambient Bounce",p,new Color(.7f,.76f,.8f),8,12,LightType.Point);
        Light("Stair / Emergency Amber",new Vector3(-8.6f,2.9f,6),new Color(1,.39f,.1f),6,6,LightType.Point);
        var v=new GameObject("Atmosphere / Global Volume").AddComponent<Volume>();
        v.isGlobal=true;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        var tonemap=profile.Add<Tonemapping>(); tonemap.mode.Override(TonemappingMode.ACES);
        var bloom=profile.Add<Bloom>(); bloom.intensity.Override(.16f); bloom.threshold.Override(1.2f);
        var color=profile.Add<ColorAdjustments>(); color.postExposure.Override(.2f); color.saturation.Override(-18); color.contrast.Override(8);
        var vignette=profile.Add<Vignette>(); vignette.intensity.Override(.17f);
        string volumePath=Root+"/RuinAtmosphere.asset";
        // Volume sub-assets must be persisted separately along with their profile.
        var existing=AssetDatabase.LoadAssetAtPath<VolumeProfile>(volumePath);
        if(existing!=null) { v.sharedProfile=existing; UnityEngine.Object.DestroyImmediate(profile); }
        else
        {
            AssetDatabase.CreateAsset(profile,volumePath);
            foreach(var c in profile.components) AssetDatabase.AddObjectToAsset(c,profile);
            v.sharedProfile=profile;
        }
        var probe=new GameObject("Atrium / Reflection Probe").AddComponent<ReflectionProbe>();
        probe.transform.position=new Vector3(0,3,0);
        probe.size=new Vector3(36,10,28);
        probe.mode=ReflectionProbeMode.Realtime;
        probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;
        probe.resolution=128;
        probe.boxProjection=true;
    }

    static void Explorer()
    {
        var go=new GameObject("Player / Interior Explorer");
        go.transform.position=new Vector3(0,.05f,-11.7f);
        var cc=go.AddComponent<CharacterController>();
        cc.height=1.8f; cc.radius=.3f; cc.center=new Vector3(0,.9f,0);
        cc.stepOffset=.3f; cc.slopeLimit=48;
        var camera=new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag="MainCamera";
        camera.transform.SetParent(go.transform,false);
        camera.transform.localPosition=new Vector3(0,1.65f,0);
        camera.fieldOfView=72; camera.nearClipPlane=.06f; camera.farClipPlane=180;
        camera.allowHDR=true;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        camera.gameObject.AddComponent<AudioListener>();
        var flashlight=Light("Flashlight",Vector3.zero,new Color(1,.9f,.72f),4,18,LightType.Spot);
        flashlight.transform.SetParent(camera.transform,false);
        flashlight.transform.localPosition=new Vector3(.12f,-.13f,.12f);
        flashlight.spotAngle=58; flashlight.innerSpotAngle=25; flashlight.shadows=LightShadows.Soft;
        flashlight.enabled=false;
        var explorer=go.AddComponent<RuinExplorer>();
        explorer.viewCamera=camera; explorer.flashlight=flashlight;
    }

    [MenuItem("Tools/Loongdum/Validate Collapsed Building")]
    public static void Validate()
    {
        Physics.SyncTransforms();
        var routes = new[] {
            new[]{new Vector3(0,0,-11),new Vector3(0,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(-13.5f,0,3.5f)},
            new[]{new Vector3(-13.5f,0,3.5f),new Vector3(-13.5f,4.2f,12.8f)},
            new[]{new Vector3(-13.5f,4.2f,12.8f),new Vector3(12,4.2f,10)},
            new[]{new Vector3(12,4.2f,10),new Vector3(12,4.2f,-10)} };
        var report=new List<string>{"Scene: "+EditorSceneManager.GetActiveScene().path,"Unity: "+Application.unityVersion};
        int complete=0;
        for(int i=0;i<routes.Length;i++)
        {
            var path=new NavMeshPath();
            NavMeshHit a=default, b=default;
            bool ok=NavMesh.SamplePosition(routes[i][0],out a,2,NavMesh.AllAreas) && NavMesh.SamplePosition(routes[i][1],out b,2,NavMesh.AllAreas);
            if(ok) ok=NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
            if(ok) complete++;
            report.Add("Route "+(i+1)+": "+(ok?"PASS":"FAIL")+" corners="+path.corners.Length);
        }
        report.Add("Connected routes: "+complete+"/"+routes.Length);
        report.Add("Missing scripts: "+UnityEngine.Object.FindObjectsByType<Transform>().Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
        report.Add("Mesh colliders: "+UnityEngine.Object.FindObjectsByType<MeshCollider>().Length);
        Directory.CreateDirectory("Captures");
        File.WriteAllLines("Captures/SceneValidation.txt",report);
        Debug.Log(string.Join("\n",report));
    }

    [MenuItem("Tools/Loongdum/Check Runtime Traversal")]
    public static void CheckRuntimeTraversal()
    {
        if(!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        var player=UnityEngine.Object.FindFirstObjectByType<RuinExplorer>();
        var cc=player.GetComponent<CharacterController>();
        var start=player.transform.position;
        var targets=new[]{new Vector3(0,0,1),new Vector3(-13.5f,0,3.5f),new Vector3(-13.5f,4.2f,12.8f),new Vector3(12,4.2f,10),new Vector3(12,4.2f,-10)};
        var report=new List<string>{"Play mode / CharacterController collision traversal"};
        try
        {
            foreach(var target in targets)
            {
                var path=new NavMeshPath();
                NavMeshHit from=default,to=default;
                bool ok=NavMesh.SamplePosition(player.transform.position,out from,2,NavMesh.AllAreas) && NavMesh.SamplePosition(target,out to,2,NavMesh.AllAreas);
                if(ok) ok=NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,path) && path.status==NavMeshPathStatus.PathComplete;
                int corner=1;
                for(int tick=0;ok && tick<1800 && corner<path.corners.Length;tick++)
                {
                    Vector3 delta=path.corners[corner]-player.transform.position;
                    var horizontal=new Vector3(delta.x,0,delta.z);
                    if(horizontal.magnitude<.13f) { corner++; continue; }
                    float step=Mathf.Min(horizontal.magnitude,3.5f/60);
                    cc.Move(horizontal.normalized*step+Vector3.down*(2f/60));
                }
                ok=ok && Vector3.Distance(player.transform.position,to.position)<.4f;
                report.Add((ok?"PASS ":"FAIL ")+target+" actual="+player.transform.position);
            }
        }
        finally
        {
            cc.enabled=false;
            player.transform.position=start;
            cc.enabled=true;
        }
        File.WriteAllLines("Captures/RuntimeTraversal.txt",report);
        Debug.Log(string.Join("\n",report));
    }
}
