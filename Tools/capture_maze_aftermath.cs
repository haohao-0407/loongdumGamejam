if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Maze_Aftermath")throw new InvalidOperationException("Wrong scene");
var art=GameObject.Find("Maze Aftermath - Environment").transform;
Vector3 P(float x,float y,float z){return art.TransformPoint(new Vector3(x,y,z));}
var sources=UnityEngine.Object.FindObjectsByType<VisionSource>(FindObjectsSortMode.None);
var sourceStates=sources.Select(s=>s.enabled).ToArray();
foreach(var ps in art.GetComponentsInChildren<ParticleSystem>())ps.Simulate(7,true,true,true);
System.IO.Directory.CreateDirectory("Captures/MazeAftermath");
void Capture(Camera cam,string file){
    var rt=RenderTexture.GetTemporary(1600,1100,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
    var old=cam.targetTexture;var active=RenderTexture.active;
    var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);
    try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();System.IO.File.WriteAllBytes(file,tex.EncodeToPNG());}
    finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
}
var temp=new GameObject("Temporary Art Capture");var c=temp.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.08f;c.farClipPlane=150;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=RenderSettings.fogColor;c.fieldOfView=48;
var data=temp.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
if(System.IO.File.Exists("SourceArt/MazeAftermath/renderer.json"))data.SetRenderer((int)Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("SourceArt/MazeAftermath/renderer.json"))["rendererIndex"]);
try{
    for(int i=0;i<sources.Length;i++)sources[i].enabled=false;
    Shader.SetGlobalFloat("_VisionMaskEnabled",0);Shader.SetGlobalFloat("_VisionOcclusionEnabled",0);
    temp.transform.position=P(24,31,-30);temp.transform.LookAt(P(0,.3f,0));Capture(c,"Captures/MazeAftermath/Overview.png");
    temp.transform.position=P(-3.5f,3.6f,-7.3f);temp.transform.LookAt(P(-1,1.05f,4));c.fieldOfView=66;Capture(c,"Captures/MazeAftermath/Courtyard.png");
    temp.transform.position=P(17.2f,3.1f,-10);temp.transform.LookAt(P(10.8f,1,-2));c.fieldOfView=56;Capture(c,"Captures/MazeAftermath/Wreck.png");
}
finally{for(int i=0;i<sources.Length;i++)sources[i].enabled=sourceStates[i];UnityEngine.Object.DestroyImmediate(temp);}
if(Camera.main!=null)Capture(Camera.main,"Captures/MazeAftermath/Gameplay.png");
return "Saved Overview, Courtyard, Wreck, Gameplay screenshots (art shots temporarily bypass vision mask; gameplay capture retains it).";
