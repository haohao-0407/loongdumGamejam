if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
var cam=Camera.main;var rt=RenderTexture.GetTemporary(1440,990,24);var previous=RenderTexture.active;var target=cam.targetTexture;
var tex=new Texture2D(1440,990,TextureFormat.RGB24,false);
try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,990),0,0);tex.Apply();System.IO.File.WriteAllBytes("Captures/MazeAftermath/Play_AfterSwap.png",tex.EncodeToPNG());}
finally{cam.targetTexture=target;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(tex);}
return UnityEditor.SessionState.GetString("MazeAftermath.PlayResult","not run");
