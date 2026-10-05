// Execute this snippet with Unity MCP execute_code while TestLevel is in Play mode.
// Exercises the existing WASD movement, glass collision, smoke and goal UI.
if (!Application.isPlaying) throw new System.InvalidOperationException("Run in Play mode.");
var flow = UnityEngine.Object.FindAnyObjectByType<LumenLevelFlow>();
var cc = GameObject.Find("Player").GetComponent<CharacterController>();
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Lumen Validation Keyboard");
keyboard.MakeCurrent();
flow.ResetLevel();
Application.runInBackground = true;
UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
var route = new[] {
 new Vector3(-9,0,4.2f), new Vector3(-3.5f,0,4.2f), new Vector3(-3.5f,0,-3.7f),
 new Vector3(2.5f,0,-3.7f), new Vector3(2.5f,0,4.2f), new Vector3(8.5f,0,4.2f), new Vector3(9,0,0)
};
int phase=0, index=0, visited=0, collisionFrames=0;
bool glassBlocked=false, smokeObserved=false;
float phaseStart=-1f;
double start=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.SessionState.SetString("LumenWalkResult","running");
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 try {
  if (!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup-start>55)
   throw new System.Exception("Validation timeout or Play mode exited.");
  var keys = new System.Collections.Generic.List<UnityEngine.InputSystem.Key>();
  if(phase==0){
   if(phaseStart < 0f) phaseStart=Time.time;
   keys.Add(UnityEngine.InputSystem.Key.D);
   smokeObserved |= UnityEngine.Object.FindObjectsByType<ParticleSystem>().Any(p=>p.gameObject.name.Contains("Clone"));
   collisionFrames++;
   if(collisionFrames > 80){
    glassBlocked=cc.transform.position.x>-6.7f && cc.transform.position.x<-6.3f;
    UnityEditor.SessionState.SetString("LumenGlassResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{blocked=glassBlocked,position=cc.transform.position.ToString(),smoke=smokeObserved}));
    flow.ResetLevel(); phase=1; keys.Clear();
   }
  } else if(phase==1) {
   if(flow.Completed){
    phase=2; keys.Clear();
    UnityEditor.SessionState.SetString("LumenWalkResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{
     success=true, keyboardMovement=true, glassBlocked, smokeObserved, waypoints=visited,
     milestones=flow.ReachedMilestones,completed=flow.Completed,time=flow.Elapsed,position=cc.transform.position.ToString(),
     victory=GameObject.Find("Victory")!=null,movementStopped=!cc.GetComponent<WhiteboxPlayerMovement>().enabled
    }));
    UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState());
    UnityEditor.EditorApplication.update-=tick;
    UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
    return;
   }
   if(index>=route.Length) throw new System.Exception("Route exhausted without completing goal.");
   var delta=route[index]-cc.transform.position; delta.y=0;
   if(delta.magnitude<.22f){index++;visited++;}
   else {
    if(delta.x>.12f)keys.Add(UnityEngine.InputSystem.Key.D);
    if(delta.x<-.12f)keys.Add(UnityEngine.InputSystem.Key.A);
    if(delta.z>.12f)keys.Add(UnityEngine.InputSystem.Key.W);
    if(delta.z<-.12f)keys.Add(UnityEngine.InputSystem.Key.S);
   }
  }
  keyboard.MakeCurrent();
  UnityEngine.InputSystem.LowLevel.InputState.Change(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
 } catch(System.Exception e){
  UnityEditor.SessionState.SetString("LumenWalkResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=false,error=e.Message,phase,index,position=cc.transform.position.ToString()}));
  UnityEditor.EditorApplication.update-=tick;
  if(keyboard.added) UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
 }
};
UnityEditor.EditorApplication.update+=tick;
return "Scheduled finite keyboard-driven collision and completion walkthrough.";
