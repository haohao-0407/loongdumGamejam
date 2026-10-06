if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
const string path="Assets/Environment/MazeAftermath/Aftermath_Renderer.asset";
var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
if(pipeline==null)throw new InvalidOperationException("URP pipeline required");
var serialized=new UnityEditor.SerializedObject(pipeline);
var list=serialized.FindProperty("m_RendererDataList");
var defaultIndex=serialized.FindProperty("m_DefaultRendererIndex").intValue;
var original=list.GetArrayElementAtIndex(defaultIndex).objectReferenceValue;
if(!System.IO.File.Exists(path)) {
    if(!UnityEditor.AssetDatabase.CopyAsset(UnityEditor.AssetDatabase.GetAssetPath(original),path))throw new Exception("Renderer copy failed");
}
var renderer=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.ScriptableRendererData>(path);
foreach(var feature in renderer.rendererFeatures)if(feature!=null&&feature.name=="Hand Drawn Comic Ink")feature.SetActive(false);
renderer.SetDirty();UnityEditor.EditorUtility.SetDirty(renderer);
int index=-1;for(int i=0;i<list.arraySize;i++)if(list.GetArrayElementAtIndex(i).objectReferenceValue==renderer)index=i;
if(index<0){index=list.arraySize;list.InsertArrayElementAtIndex(index);list.GetArrayElementAtIndex(index).objectReferenceValue=renderer;serialized.ApplyModifiedPropertiesWithoutUndo();}
Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().SetRenderer(index);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
System.IO.File.WriteAllText("SourceArt/MazeAftermath/renderer.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{pipeline=UnityEditor.AssetDatabase.GetAssetPath(pipeline),renderer=path,rendererIndex=index,originalDefaultIndex=defaultIndex,features=renderer.rendererFeatures.Where(f=>f!=null).Select(f=>new{f.name,f.isActive}).ToArray()},Newtonsoft.Json.Formatting.Indented));
return Newtonsoft.Json.JsonConvert.SerializeObject(new{pipeline=UnityEditor.AssetDatabase.GetAssetPath(pipeline),rendererIndex=index,features=renderer.rendererFeatures.Select(f=>new{f.name,f.isActive}).ToArray()});
