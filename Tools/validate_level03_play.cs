// Execute via Unity MCP execute_code in Play Mode. Synthetic keyboard drives
// the existing movement and input handlers; no direct actor teleport is used.
if (!Application.isPlaying) throw new System.InvalidOperationException("Play Mode required.");
var flow = UnityEngine.Object.FindAnyObjectByType<Level03Flow>();
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Level03 Validation Keyboard");
keyboard.MakeCurrent();
flow.ResetLevel();
bool oldBackground = Application.runInBackground;
Application.runInBackground = true;
UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
var paths = new[] {
 Level03Validation.Path(new Vector2Int(16,8),new Vector2Int(13,4),""),
 Level03Validation.Path(new Vector2Int(2,16),new Vector2Int(4,16),""),
 Level03Validation.Path(new Vector2Int(4,16),new Vector2Int(4,15),"X"),
 Level03Validation.Path(new Vector2Int(4,15),new Vector2Int(10,17),"XD"),
 Level03Validation.Path(new Vector2Int(13,4),new Vector2Int(16,6),"XD"),
 Level03Validation.Path(new Vector2Int(16,6),new Vector2Int(9,7),"XD"),
 Level03Validation.Path(new Vector2Int(9,7),new Vector2Int(10,17),"XDZ")
};
var waypoints = paths.Select(p=>p.Select(cell=>Level03Layout.Position(cell.x,cell.y)).ToList()).ToArray();
waypoints[3].Add(Level03Layout.SecondTile);
waypoints[6].Add(Level03Layout.SecondTile);
int phase=0,index=0,visited=0,frames=0;
float actionStart=-1f;
float actionRelease=-1f;
double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.SessionState.SetString("Level03PlayResult","running");
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 try {
  if(!Application.isPlaying||UnityEditor.EditorApplication.timeSinceStartup-started>100)
   throw new System.Exception("Play validation timeout or stopped.");
  frames++;
  var keys=new System.Collections.Generic.List<UnityEngine.InputSystem.Key>();
  if(phase==12 && flow.Completed){phase=13;index=0;}
  bool walk=phase==0||phase==2||phase==4||phase==6||phase==8||phase==10||phase==12;
  if(walk){
   int route=phase/2;
   if(index>=waypoints[route].Count){phase++;index=0;actionStart=-1f;actionRelease=-1f;}
   else{
    var delta=waypoints[route][index]-flow.player.transform.position;delta.y=0;
    if(delta.magnitude<.15f){index++;visited++;}
    else{
     // One axis at a time keeps the traced path off wall corners.
     if(Mathf.Abs(delta.x)>.1f)keys.Add(delta.x>0?UnityEngine.InputSystem.Key.D:UnityEngine.InputSystem.Key.A);
     else if(Mathf.Abs(delta.z)>.1f)keys.Add(delta.z>0?UnityEngine.InputSystem.Key.W:UnityEngine.InputSystem.Key.S);
    }
   }
  } else if(phase==13){
   if(!flow.Completed)throw new System.Exception("Goal reached without completion.");
   UnityEditor.SessionState.SetString("Level03PlayResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{
    success=true,syntheticKeyboard=true,teleports=false,visited,frames,seconds=UnityEditor.EditorApplication.timeSinceStartup-started,
    flow.AOpened,flow.COpened,flow.BObserved,flow.BOpened,flow.Completed,flow.Stage,
    player=flow.player.transform.position.ToString(),upper=flow.source.transform.position.ToString(),
    movementStopped=!flow.player.GetComponent<WhiteboxPlayerMovement>().enabled
   }));
   UnityEditor.EditorApplication.update-=tick;
   UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
   Application.runInBackground=oldBackground;
   return;
  } else if(phase==9){
   if(flow.BObserved){phase++;index=0;}
   else{if(actionStart<0)actionStart=Time.time;if(Time.time-actionStart>4)throw new System.Exception("Plate did not reveal B.");}
  } else {
   if(actionStart<0)actionStart=Time.time;
   // Start with a released-key frame, then hold long enough to exercise edges.
   if(Time.time-actionStart>.15f && Time.time-actionStart<.4f)
    keys.Add(phase==1||phase==7?UnityEngine.InputSystem.Key.F:UnityEngine.InputSystem.Key.E);
   if(Time.time-actionStart>.65f){
    bool ok=phase==1?flow.Stage==1:phase==3?flow.AOpened:phase==5?flow.COpened:phase==7?flow.Stage==3:phase==11?flow.BOpened:false;
    if(!ok)throw new System.Exception("Key action failed in phase "+phase+"; stage "+flow.Stage);
    phase++;index=0;actionStart=-1f;
   }
  }
  keyboard.MakeCurrent();
  UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
 }catch(System.Exception e){
  UnityEditor.SessionState.SetString("Level03PlayResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=false,error=e.Message,phase,index,position=flow.player.transform.position.ToString(),flow.Stage,frames}));
  UnityEditor.EditorApplication.update-=tick;
  if(keyboard.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
  Application.runInBackground=oldBackground;
 }
};
UnityEditor.EditorApplication.update+=tick;
return "Scheduled keyboard walkthrough with 100-second limit.";
