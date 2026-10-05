const string pack = "Assets/Generated/ChibiCityRuins";
var original = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (UnityEditor.EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before finalizing assets.");
if (original.isDirty) throw new InvalidOperationException("Save current scene changes before rebuilding the generated pack.");
var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
System.IO.Directory.CreateDirectory(pack + "/Prefabs");
System.IO.Directory.CreateDirectory(pack + "/Scenes");
System.IO.Directory.CreateDirectory(pack + "/Materials");
UnityEditor.AssetDatabase.Refresh();
var names = new[] { "Ruin_SlabPile_T2", "Ruin_WallCorner_T2", "Ruin_BrokenColumn_T2", "Ruin_RubbleMound_T2" };
var targets = new[] { 3.2f, 2.6f, 2.8f, 2.2f };
var positions = new[] { new Vector3(-2.6f, 0, -1.7f), new Vector3(2.6f, 0, 1.6f), new Vector3(2.6f, 0, -1.8f), new Vector3(-2.6f, 0, 1.8f) };
var rotations = new[] { 20f, 205f, -30f, 5f };
Bounds Measure(GameObject o) {
    var rs = o.GetComponentsInChildren<Renderer>(true);
    if (rs.Length == 0) throw new InvalidOperationException("No renderers: " + o.name);
    var b = rs[0].bounds;
    foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
    return b;
}
float[] V(Vector3 v) { return new[] { v.x, v.y, v.z }; }
void PreviewLayer(GameObject o) { foreach (var t in o.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 0; }
var reports = new List<object>();
var previewObjects = new List<GameObject>();
for (int i = 0; i < names.Length; i++) {
    string modelPath = pack + "/Models/" + names[i] + ".glb";
    var model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
    if (model == null) throw new InvalidOperationException("Missing imported model: " + modelPath);
    var root = new GameObject(names[i]);
    var geometry = UnityEngine.Object.Instantiate(model);
    geometry.name = "Geometry";
    geometry.transform.SetParent(root.transform, false);
    geometry.transform.localPosition = Vector3.zero;
    geometry.transform.localRotation = Quaternion.identity;
    geometry.transform.localScale = Vector3.one;
    var raw = Measure(root);
    float factor = targets[i] / Mathf.Max(raw.size.x, raw.size.z);
    geometry.transform.localScale = Vector3.one * factor;
    var scaled = Measure(root);
    geometry.transform.localPosition -= new Vector3(scaled.center.x, scaled.min.y, scaled.center.z);
    foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) {
        if (f.sharedMesh == null) continue;
        var mc = f.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = f.sharedMesh;
        mc.convex = false;
        mc.isTrigger = false;
    }
    foreach (var t in root.GetComponentsInChildren<Transform>(true)) {
        t.gameObject.isStatic = true;
        t.gameObject.layer = 0;
    }
    var measured = Measure(root);
    string prefabPath = pack + "/Prefabs/" + names[i] + ".prefab";
    var prefab = UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
    if (prefab == null) throw new InvalidOperationException("Prefab save failed: " + prefabPath);
    var renderers = root.GetComponentsInChildren<Renderer>(true);
    var filters = root.GetComponentsInChildren<MeshFilter>(true);
    var materials = renderers.SelectMany(r => r.sharedMaterials).Distinct().ToArray();
    var colliders = root.GetComponentsInChildren<MeshCollider>(true);
    int triangles = filters.Sum(f => f.sharedMesh == null ? 0 : Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(s => (int)f.sharedMesh.GetIndexCount(s) / 3));
    var textures = materials.Where(m => m != null).SelectMany(m => m.GetTexturePropertyNames().Select(p => m.GetTexture(p))).Where(t => t != null).Distinct().Select(t => new { name = t.name, width = t.width, height = t.height }).ToArray();
    Physics.SyncTransforms();
    bool collisionRayHit = false;
    foreach (var mc in colliders) {
        RaycastHit h;
        if (mc.Raycast(new Ray(measured.center + Vector3.up * (measured.size.y + 2f), Vector3.down), out h, measured.size.y * 2 + 4)) collisionRayHit = true;
    }
    reports.Add(new {
        name = names[i], modelPath, prefabPath, rawSize = V(raw.size), geometryScale = factor,
        sizeMetres = V(measured.size), boundsMin = V(measured.min), rootScale = V(root.transform.localScale),
        triangles, meshCount = filters.Length, materialCount = materials.Length, colliderCount = colliders.Length,
        missingMaterials = materials.Count(m => m == null), shaders = materials.Where(m => m != null).Select(m => m.shader == null ? "MISSING" : m.shader.name).ToArray(),
        textures, collisionRayHit, collision = "Static non-convex MeshCollider; no Rigidbody"
    });
    UnityEngine.Object.DestroyImmediate(root);
    var instance = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab, scene);
    instance.transform.position = positions[i];
    instance.transform.rotation = Quaternion.Euler(0, rotations[i], 0);
    PreviewLayer(instance);
    previewObjects.Add(instance);
}
var lit = Shader.Find("Universal Render Pipeline/Lit");
if (lit == null) throw new InvalidOperationException("URP Lit shader missing.");
var groundMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(pack + "/Materials/PreviewGround.mat");
if (groundMaterial == null) {
    groundMaterial = new Material(lit);
    groundMaterial.name = "PreviewGround";
    UnityEditor.AssetDatabase.CreateAsset(groundMaterial, pack + "/Materials/PreviewGround.mat");
}
groundMaterial.SetColor("_BaseColor", new Color(0.35f, 0.38f, 0.4f, 1));
groundMaterial.SetFloat("_Smoothness", 0f);
groundMaterial.SetFloat("_Metallic", 0f);
var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
ground.name = "Preview Ground";
ground.transform.position = new Vector3(0, -0.15f, 0);
ground.transform.localScale = new Vector3(12, 0.3f, 9);
ground.GetComponent<Renderer>().sharedMaterial = groundMaterial;
ground.layer = 0;
var lightObject = new GameObject("Ruin Preview Key Light");
var light = lightObject.AddComponent<Light>();
light.type = LightType.Directional;
light.color = new Color(1f, 0.95f, 0.88f);
light.intensity = 1.5f;
light.shadows = LightShadows.Soft;
light.cullingMask = ~0;
lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
RenderSettings.ambientLight = new Color(0.36f, 0.4f, 0.46f);
RenderSettings.fog = false;
RenderSettings.skybox = null;
var cameraObject = new GameObject("Ruin Preview Camera");
var camera = cameraObject.AddComponent<Camera>();
cameraObject.transform.position = new Vector3(8f, 10f, -13f);
cameraObject.transform.LookAt(new Vector3(0, 0.9f, 0));
camera.orthographic = true;
camera.orthographicSize = 5f;
camera.clearFlags = CameraClearFlags.SolidColor;
camera.backgroundColor = new Color(0.79f, 0.82f, 0.86f);
camera.cullingMask = ~0;
camera.tag = "MainCamera";
camera.nearClipPlane = 0.1f;
camera.farClipPlane = 80;
camera.depth = -100;
var cameraData = cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
cameraData.renderPostProcessing = false;
cameraData.volumeLayerMask = 1;
UnityEditor.AssetDatabase.SaveAssets();
string scenePath = pack + "/Scenes/ChibiCityRuins_Preview.unity";
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
System.IO.File.WriteAllText(pack + "/validation_report.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { originalScene = original.path, previewScene = scenePath, model = "meshy-t2", generatedFormat = "glb", assets = reports }, Newtonsoft.Json.Formatting.Indented));
UnityEditor.AssetDatabase.ImportAsset(pack + "/validation_report.json");
UnityEditor.Selection.activeObject = previewObjects[0];
return new { previewScene = scenePath, activeOriginalScene = original.path, assets = reports };
