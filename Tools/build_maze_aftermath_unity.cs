// Execute as a method body with Unity MCP execute_code (Roslyn). Creates a new scene.
const string pack = "Assets/Environment/MazeAftermath";
const string scenePath = "Assets/Scenes/Maze_Aftermath.unity";
if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring.");
var original = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (original.path != "Assets/Scenes/Maze.unity" || original.isDirty)
    throw new InvalidOperationException("Start from the clean saved Maze scene; preserve unsaved user work.");
if (System.IO.File.Exists(scenePath)) throw new InvalidOperationException("Output already exists; do not overwrite an edited scene.");
UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var manifest = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("SourceArt/MazeAftermath/manifest.json"));
var models = new[] { "Models/MazeAftermath_Architecture.glb", "Meshy/BurnedJeep_T2.glb", "Meshy/RebarRubble_T2.glb" };
foreach (var path in models)
    if (UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pack + "/" + path) == null)
        throw new InvalidOperationException("Model not imported: " + path);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(original, scenePath, true);
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
var maze = GameObject.Find("GeneratedMaze").transform;
var originLocal = manifest["localOrigin"].Select(v => (float)v).ToArray();
var origin = maze.TransformPoint(new Vector3(originLocal[0], originLocal[1], originLocal[2]));
var rotation = maze.rotation;
Vector3 P(float x, float y, float z) { return origin + rotation * new Vector3(x, y, z); }
float[] V(Vector3 v) { return new[] { v.x, v.y, v.z }; }
Bounds Measure(GameObject o) {
    var rs = o.GetComponentsInChildren<Renderer>(true);
    if (rs.Length == 0) throw new InvalidOperationException("No renderers: " + o.name);
    var b = rs[0].bounds;
    foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
    return b;
}
float cellX = (float)manifest["cellSize"][0], cellZ = (float)manifest["cellSize"][1];
Vector3 Cell(int x, int z) { return P((x - 2) * cellX, .85f, (z - 2) * cellZ); }
List<string> Edges() {
    Physics.SyncTransforms();
    var edges = new List<string>();
    for (int z=0;z<5;z++) for (int x=0;x<5;x++) {
        foreach (var d in new[]{new Vector2Int(1,0),new Vector2Int(0,1)}) {
            int xx=x+d.x, zz=z+d.y; if(xx>=5||zz>=5) continue;
            var a=Cell(x,z); var b=Cell(xx,zz); var v=b-a;
            var hits=Physics.SphereCastAll(a,.34f,v.normalized,v.magnitude,~0,QueryTriggerInteraction.Ignore);
            if (!hits.Any(h=>h.collider!=null && !(h.collider is CharacterController) && h.collider.GetComponent<VisionSource>()==null))
                edges.Add((z*5+x)+"-"+(zz*5+xx));
        }
    }
    return edges;
}
var beforeEdges=Edges();
var originalColliders=maze.GetComponentsInChildren<Collider>().Select(c=>new{c.name,position=V(c.transform.position),scale=V(c.transform.lossyScale)}).ToArray();
foreach (var r in maze.GetComponentsInChildren<Renderer>()) r.enabled=false;

var art = new GameObject("Maze Aftermath - Environment");
art.transform.SetPositionAndRotation(origin,rotation);
var architecture=(GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pack+"/Models/MazeAftermath_Architecture.glb"),art.transform);
architecture.name="Blender Architecture";
architecture.transform.localPosition=Vector3.zero;
// glTFast reflects X; Blender's +Y exports to -Z. This rotation restores the measured Maze axes.
architecture.transform.localRotation=Quaternion.Euler(0,180,0);
architecture.transform.localScale=Vector3.one;
foreach(var f in architecture.GetComponentsInChildren<MeshFilter>()) {
    f.gameObject.isStatic=true;
    if (f.name.StartsWith("Aftermath_Backdrop") || f.name.StartsWith("Aftermath_StreetDebris") || f.name.StartsWith("Aftermath_Surround")) {
        var mc=f.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh=f.sharedMesh;
    }
}
var setDressing=new GameObject("Meshy T2 - Wrecks and Rubble"); setDressing.transform.SetParent(art.transform,false);
System.IO.Directory.CreateDirectory(pack+"/Prefabs");
System.IO.Directory.CreateDirectory(pack+"/Materials");
UnityEditor.AssetDatabase.Refresh();
var prefabByName=new Dictionary<string,GameObject>();
var modelReports=new List<object>();
foreach(var name in new[]{"BurnedJeep_T2","RebarRubble_T2"}) {
    var model=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(pack+"/Meshy/"+name+".glb");
    var root=new GameObject(name+"_1m");
    var geo=UnityEngine.Object.Instantiate(model,root.transform); geo.name="Geometry";
    geo.transform.localPosition=Vector3.zero; geo.transform.localRotation=Quaternion.identity; geo.transform.localScale=Vector3.one;
    var raw=Measure(root); float factor=1f/Mathf.Max(raw.size.x,raw.size.z); geo.transform.localScale=Vector3.one*factor;
    var b=Measure(root); geo.transform.position-=new Vector3(b.center.x,b.min.y,b.center.z);
    foreach(var f in root.GetComponentsInChildren<MeshFilter>()) {
        var mc=f.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh=f.sharedMesh; mc.convex=false;
        f.gameObject.isStatic=true;
    }
    var mats=root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
    var textures=mats.Where(m=>m!=null).SelectMany(m=>m.GetTexturePropertyNames().Select(p=>m.GetTexture(p))).Where(t=>t!=null).Distinct().Select(t=>new{t.name,t.width,t.height}).ToArray();
    modelReports.Add(new{name,rawBounds=V(raw.size),normalizedBounds=V(Measure(root).size),scaleFactor=factor,triangles=root.GetComponentsInChildren<MeshFilter>().Sum(f=>(long)f.sharedMesh.triangles.Length/3),missingMaterials=mats.Count(m=>m==null),textures});
    var prefab=UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,pack+"/Prefabs/"+name+".prefab");
    prefabByName[name]=prefab; UnityEngine.Object.DestroyImmediate(root);
}
var propReports=new List<object>(); int propIndex=0;
foreach(var p in manifest["props"]) {
    string name=(string)p["asset"];
    var o=(GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefabByName[name],setDressing.transform);
    o.name=name+"_"+(++propIndex).ToString("00");
    o.transform.localPosition=new Vector3((float)p["x"],.018f,(float)p["y"]);
    o.transform.localRotation=Quaternion.Euler(0,180f-(float)p["angle"],0);
    o.transform.localScale=Vector3.one*(float)p["size"];
    propReports.Add(new{o.name,boundsMetres=V(Measure(o).size),groundOffset=Measure(o).min.y-origin.y});
}

Material MaterialAsset(string name,Shader shader,Color color) {
    var m=new Material(shader){name=name}; m.SetColor("_BaseColor",color);
    UnityEditor.AssetDatabase.CreateAsset(m,pack+"/Materials/"+name+".mat"); return m;
}
// Soft particle mask is authored mathematically for runtime smoke billboards.
var smokeTex=new Texture2D(64,64,TextureFormat.RGBA32,false){name="SmokeSoftDisc",wrapMode=TextureWrapMode.Clamp};
for(int y=0;y<64;y++) for(int x=0;x<64;x++) {
    float nx=(x-31.5f)/31.5f, ny=(y-31.5f)/31.5f;
    float a=Mathf.Clamp01(1-nx*nx-ny*ny); a=a*a*a;
    smokeTex.SetPixel(x,y,new Color(1,1,1,a));
}
smokeTex.Apply(); UnityEditor.AssetDatabase.CreateAsset(smokeTex,pack+"/Materials/SmokeSoftDisc.asset");
var particleShader=Shader.Find("Universal Render Pipeline/Particles/Unlit");
if(particleShader==null) throw new InvalidOperationException("URP particle shader missing");
var smokeMat=MaterialAsset("DustSmoke",particleShader,Color.white);
smokeMat.SetTexture("_BaseMap",smokeTex); smokeMat.SetFloat("_Surface",1); smokeMat.SetFloat("_Blend",0);
smokeMat.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
smokeMat.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
smokeMat.SetFloat("_ZWrite",0); smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); smokeMat.renderQueue=3000;
var atmosphere=new GameObject("Aftermath Atmosphere"); atmosphere.transform.SetParent(art.transform,false);
int fireIndex=0;
foreach(var p in manifest["fireSites"]) {
    var o=new GameObject("Smouldering debris "+(++fireIndex));o.transform.SetParent(atmosphere.transform,false);
    o.transform.localPosition=new Vector3((float)p[0],.12f,(float)p[1]);
    var ps=o.AddComponent<ParticleSystem>(); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    var main=ps.main; main.duration=12;main.loop=true;main.prewarm=true;main.startLifetime=new ParticleSystem.MinMaxCurve(6,9);
    main.startSpeed=new ParticleSystem.MinMaxCurve(.17f,.28f);main.startSize=new ParticleSystem.MinMaxCurve(.5f,.8f);
    main.startColor=new Color(.22f,.215f,.20f,.42f);main.maxParticles=70;main.simulationSpace=ParticleSystemSimulationSpace.World;
    main.startRotation=new ParticleSystem.MinMaxCurve(-3.14f,3.14f);
    var emission=ps.emission;emission.rateOverTime=5;
    var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.35f;shape.rotation=new Vector3(-90,0,0);
    var vel=ps.velocityOverLifetime;vel.enabled=true;vel.space=ParticleSystemSimulationSpace.World;
    vel.x=new ParticleSystem.MinMaxCurve(.12f);vel.y=new ParticleSystem.MinMaxCurve(.3f);vel.z=new ParticleSystem.MinMaxCurve(.04f);
    var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.4f),new Keyframe(1,3f)));
    var col=ps.colorOverLifetime;col.enabled=true;var grad=new Gradient();grad.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.65f,.2f),new GradientAlphaKey(0,1)});col.color=grad;
    var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=smokeMat;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
    ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(313+fireIndex);ps.Play();
    var light=o.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.23f,.035f);light.intensity=1.3f;light.range=2.8f;light.shadows=LightShadows.None;
}
var sun=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Light>()).FirstOrDefault(l=>l.type==LightType.Directional);
if(sun!=null) {sun.color=new Color(1,.96f,.89f);sun.intensity=1.8f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-38,0);}
RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
RenderSettings.ambientSkyColor=new Color(.49f,.54f,.61f);RenderSettings.ambientEquatorColor=new Color(.30f,.32f,.34f);RenderSettings.ambientGroundColor=new Color(.15f,.14f,.12f);
RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.009f;RenderSettings.fogColor=new Color(.38f,.40f,.42f);RenderSettings.skybox=null;
var profile=ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
UnityEditor.AssetDatabase.CreateAsset(profile,pack+"/AftermathVolume.asset");
var ca=profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);ca.saturation.Override(-13);ca.contrast.Override(12);ca.postExposure.Override(.15f);
var tone=profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);tone.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
var bloom=profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);bloom.intensity.Override(.18f);bloom.threshold.Override(1.2f);
foreach(var c in profile.components) UnityEditor.AssetDatabase.AddObjectToAsset(c,profile);
var vo=new GameObject("Aftermath Global Volume");vo.transform.SetParent(atmosphere.transform,false);var volume=vo.AddComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=profile;
var camera=Camera.main;
if(camera!=null) {
    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=RenderSettings.fogColor;camera.farClipPlane=120;
    var data=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>()??camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=true;
}
var afterEdges=Edges();var lost=beforeEdges.Except(afterEdges).ToArray();
var seen=new HashSet<int>{2};var queue=new Queue<int>();queue.Enqueue(2);
while(queue.Count>0){int n=queue.Dequeue();foreach(var edge in afterEdges){var ns=edge.Split('-').Select(int.Parse).ToArray();int dest=ns[0]==n?ns[1]:ns[1]==n?ns[0]:-1;if(dest>=0&&seen.Add(dest))queue.Enqueue(dest);}}
var sourceMaterialCount=architecture.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Count(m=>m==null);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,scenePath);
var report=new{scene=scenePath,sourceScene="Assets/Scenes/Maze.unity",wallColliders=originalColliders.Length,sourceColliderTransforms=originalColliders,baselineOpenEdges=beforeEdges,openEdges=afterEdges,lostEdges=lost,reachableCells=seen.Count,totalCells=25,probeRadiusMetres=.34f,probeHeightMetres=.85f,missingArchitectureMaterials=sourceMaterialCount,modelReports,propReports,playModeVerified=false};
System.IO.File.WriteAllText("SourceArt/MazeAftermath/validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
UnityEditor.Selection.activeGameObject=art;
if(UnityEditor.SceneView.lastActiveSceneView!=null)UnityEditor.SceneView.lastActiveSceneView.LookAt(P(0,0,0),Quaternion.Euler(54,-35,0),24);
return Newtonsoft.Json.JsonConvert.SerializeObject(new{scene=scenePath,wallColliders=originalColliders.Length,props=propReports.Count,lostEdges=lost,reachableCells=seen.Count,missingMaterials=sourceMaterialCount,modelReports});
