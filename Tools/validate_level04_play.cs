// Execute via Unity MCP execute_code in Play Mode. No actor teleports: uses
// real Input System keys, CharacterController movement and BodyReversal.
if (!Application.isPlaying) throw new System.InvalidOperationException("Play Mode required.");
var flow = UnityEngine.Object.FindAnyObjectByType<Level04Flow>();
var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Level04 Validation Keyboard");
keyboard.MakeCurrent(); flow.ResetLevel();
bool oldBackground = Application.runInBackground; Application.runInBackground = true;
UnityEditor.EditorWindow.GetWindow(System.Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
var waypoints = Level04Validation.Routes().Select(p=>p.Select(c=>Level04Layout.Position(c.x,c.y)).ToList()).ToArray();
int phase=0,index=0,visited=0,frames=0;
float actionStart=-1;
double started=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.SessionState.SetString("Level04PlayResult","running");
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{
 try {
  if(!Application.isPlaying || UnityEditor.EditorApplication.timeSinceStartup-started>100)
   throw new System.Exception("Walkthrough timeout or stopped.");
  frames++; var keys=new System.Collections.Generic.List<UnityEngine.InputSystem.Key>();
  if(phase==12 && flow.Completed) phase=13;
  if(phase==13){
   if(!flow.Completed)throw new System.Exception("No completion.");
   UnityEditor.SessionState.SetString("Level04PlayResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{
    success=true,syntheticKeyboard=true,teleports=false,visited,frames,seconds=UnityEditor.EditorApplication.timeSinceStartup-started,
    flow.AOpened,flow.BObserved,flow.BOpened,flow.COpened,flow.Completed,flow.Stage,
    player=flow.player.transform.position.ToString(),upper=flow.source.transform.position.ToString(),
    movementStopped=!flow.player.GetComponent<WhiteboxPlayerMovement>().enabled
   }));
   UnityEditor.EditorApplication.update-=tick;UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
   Application.runInBackground=oldBackground;return;
  }
  if(phase%2==0){
   var route=waypoints[phase/2];
   if(index>=route.Count){phase++;index=0;actionStart=-1;}
   else{
    var delta=route[index]-flow.player.transform.position;delta.y=0;
    if(delta.magnitude<.15f){index++;visited++;}
    else if(Mathf.Abs(delta.x)>.1f)keys.Add(delta.x>0?UnityEngine.InputSystem.Key.D:UnityEngine.InputSystem.Key.A);
    else if(Mathf.Abs(delta.z)>.1f)keys.Add(delta.z>0?UnityEngine.InputSystem.Key.W:UnityEngine.InputSystem.Key.S);
   }
  }else if(phase==7){
   if(flow.BObserved){phase++;index=0;actionStart=-1;}
   else{if(actionStart<0)actionStart=Time.time;if(Time.time-actionStart>4)throw new System.Exception("Plate did not reveal B.");}
  }else{
   if(actionStart<0)actionStart=Time.time;
   if(Time.time-actionStart>.15f && Time.time-actionStart<.4f)
    keys.Add(phase==1||phase==5?UnityEngine.InputSystem.Key.F:UnityEngine.InputSystem.Key.E);
   if(Time.time-actionStart>.65f){
    bool ok=phase==1?flow.Stage==1:phase==3?flow.AOpened:phase==5?flow.Stage==3:phase==9?flow.BOpened:phase==11?flow.COpened:false;
    if(!ok)throw new System.Exception("Key action failed at phase "+phase+", stage "+flow.Stage);
    phase++;index=0;actionStart=-1;
   }
  }
  keyboard.MakeCurrent();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
 }catch(System.Exception e){
  UnityEditor.SessionState.SetString("Level04PlayResult",Newtonsoft.Json.JsonConvert.SerializeObject(new{
   success=false,error=e.Message,phase,index,position=flow.player.transform.position.ToString(),flow.Stage,frames
  }));
  UnityEditor.EditorApplication.update-=tick;if(keyboard.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
  Application.runInBackground=oldBackground;
 }
};
UnityEditor.EditorApplication.update+=tick;
return "Scheduled 100-second keyboard walkthrough.";
