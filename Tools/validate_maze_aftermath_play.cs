// Real WASD / F input through the existing game scripts; no actor teleport.
if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
var cc=UnityEngine.Object.FindAnyObjectByType<CharacterController>();
var player=cc.transform;var head=UnityEngine.Object.FindAnyObjectByType<VisionSource>();
var plate=UnityEngine.Object.FindAnyObjectByType<ReversalTile>();
var reversal=cc.GetComponent<FullBodyReversal>();var gameOver=cc.GetComponent<HeadFeetGameOver>();
var art=GameObject.Find("Maze Aftermath - Environment").transform;
var report=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("SourceArt/MazeAftermath/validation.json"));
var layout=Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("SourceArt/MazeAftermath/manifest.json"));
float cx=(float)layout["cellSize"][0],cz=(float)layout["cellSize"][1];
int Node(Vector3 p){var q=art.InverseTransformPoint(p);return Mathf.Clamp(Mathf.RoundToInt(q.z/cz)+2,0,4)*5+Mathf.Clamp(Mathf.RoundToInt(q.x/cx)+2,0,4);}
int start=Node(player.position),goal=Node(plate.transform.position);
var neighbors=new Dictionary<int,List<int>>();for(int n=0;n<25;n++)neighbors[n]=new List<int>();
foreach(var e in report["openEdges"]){var ns=((string)e).Split('-').Select(int.Parse).ToArray();neighbors[ns[0]].Add(ns[1]);neighbors[ns[1]].Add(ns[0]);}
var prev=new Dictionary<int,int>{{start,-1}};var queue=new Queue<int>();queue.Enqueue(start);
while(queue.Count>0){int n=queue.Dequeue();foreach(int d in neighbors[n])if(!prev.ContainsKey(d)){prev[d]=n;queue.Enqueue(d);}}
if(!prev.ContainsKey(goal))throw new Exception("No route to reversal plate");
var route=new List<int>();for(int n=goal;n>=0;n=prev[n])route.Add(n);route.Reverse();
var waypoints=route.Select(n=>art.TransformPoint(new Vector3((n%5-2)*cx,0,(n/5-2)*cz))).ToList();waypoints.Add(plate.transform.position);
bool align=new UnityEditor.SerializedObject(cc.GetComponent<WhiteboxPlayerMovement>()).FindProperty("alignToCamera").boolValue;
var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>("Maze Aftermath Validation Keyboard");keyboard.MakeCurrent();
bool background=Application.runInBackground;Application.runInBackground=true;
UnityEditor.EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor")).Focus();
int index=0,frames=0;bool groundedAtStart=cc.isGrounded,attempted=false;Vector3 headBefore=head.transform.position,feetBefore=Vector3.zero;
double started=UnityEditor.EditorApplication.timeSinceStartup;float actionTime=-1;
UnityEditor.SessionState.SetString("MazeAftermath.PlayResult","running");
UnityEditor.EditorApplication.CallbackFunction tick=null;
void Finish(bool success,string error){
    var result=new{success,error,syntheticKeyboard=true,actorTeleports=false,groundedAtStart,route,waypointsReached=index,totalWaypoints=waypoints.Count,frames,seconds=UnityEditor.EditorApplication.timeSinceStartup-started,swapAttempted=attempted,headSwapDistance=Vector3.Distance(head.transform.position,feetBefore),playerSwapHorizontalDistance=Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(headBefore.x,headBefore.z)),gameOver=gameOver.IsOver};
    string json=Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented);
    UnityEditor.SessionState.SetString("MazeAftermath.PlayResult",json);System.IO.File.WriteAllText("SourceArt/MazeAftermath/play_validation.json",json);
    UnityEditor.EditorApplication.update-=tick;if(keyboard.added)UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);Application.runInBackground=background;
}
tick=()=>{
    try{
        if(!Application.isPlaying||UnityEditor.EditorApplication.timeSinceStartup-started>90){Finish(false,"Walkthrough timeout or stopped");return;}
        frames++;if(gameOver.IsOver){Finish(false,"Unexpected game over");return;}
        var keys=new List<UnityEngine.InputSystem.Key>();
        if(index<waypoints.Count){
            var delta=waypoints[index]-player.position;delta.y=0;
            if(delta.magnitude<.23f)index++;
            else{
                var forward=align?Vector3.ProjectOnPlane(Camera.main.transform.forward,Vector3.up).normalized:Vector3.forward;
                var right=Vector3.Cross(Vector3.up,forward);float x=Vector3.Dot(delta,right),z=Vector3.Dot(delta,forward);
                // Alternate cardinal steps to track the rotated maze centreline precisely.
                if(Mathf.Abs(x)>Mathf.Abs(z))keys.Add(x>0?UnityEngine.InputSystem.Key.D:UnityEngine.InputSystem.Key.A);
                else keys.Add(z>0?UnityEngine.InputSystem.Key.W:UnityEngine.InputSystem.Key.S);
            }
        }else{
            if(actionTime<0){actionTime=Time.time;headBefore=head.transform.position;feetBefore=player.position;}
            float elapsed=Time.time-actionTime;
            if(elapsed>.15f&&elapsed<.4f){keys.Add(UnityEngine.InputSystem.Key.F);attempted=true;}
            if(elapsed>1){bool success=Vector3.Distance(head.transform.position,feetBefore)<.15f&&Vector2.Distance(new Vector2(player.position.x,player.position.z),new Vector2(headBefore.x,headBefore.z))<.15f;Finish(success,success?null:"F did not swap the bodies");return;}
        }
        keyboard.MakeCurrent();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
    }catch(Exception ex){Finish(false,ex.Message);}
};
UnityEditor.EditorApplication.update+=tick;
return Newtonsoft.Json.JsonConvert.SerializeObject(new{scheduled=true,start,goal,route,waypoints=waypoints.Count});
