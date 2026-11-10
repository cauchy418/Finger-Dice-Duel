using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>{var method=boot.GetType().GetMethod(n,flags);var parameters=method.GetParameters();var args=new object[parameters.Length];for(int j=0;j<args.Length;j++)args[j]=j<a.Length?a[j]:parameters[j].DefaultValue;method.Invoke(boot,args);};
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/skill-animation-rework");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
var raw=System.IO.Path.GetFullPath("Temp/AnimationReworkFrames");System.IO.Directory.CreateDirectory(raw);
System.Action reviewPose=null;
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();reviewPose?.Invoke();UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(raw,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};

boot.AiThinkSeconds=999;
var names=new[]{"knowledge-loop","confusion-loop","holy-loop","scorch-loop"};
var states=new[]{StatusEffect.KnowledgeBlessing,StatusEffect.Confusion,StatusEffect.HolyLight,StatusEffect.Scorch};
for(int i=0;i<states.Length;i++){
 boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.Blank);
 var g=(GameManager)field(boot,"_game");g.RecordCombatVisuals=false;
 if(i==0){g.PlayerA.GainKnowledge();g.PlayerA.GainKnowledge();g.PlayerA.GainKnowledge();}else g.PlayerA.ApplyStatus(states[i],2);
 invoke("RefreshView",new object[0]);board.ShowCombatWindup(g.PlayerA,g.PlayerA.RightHand,"weapon_sword",AttackDamageType.Slashing);board.HideCombatWindup();
 var frame=g.CaptureVisualFrame();var apply=board.GetType().GetMethod("ApplyCombatFrame",flags);
 var motion=(FingerDiceDuel.UI.CombatStatusMotion)field(board,"_statusMotion");
 var auras=(System.Collections.IDictionary)field(motion,"_auras");
 var pose=motion.GetType().GetMethod("PoseSecondaryAura",flags);
 System.IO.Directory.CreateDirectory(System.IO.Path.Combine(raw,names[i]));
 for(int j=0;j<2;j++){
  float phase=j==0?.5f:2.2f;
  reviewPose=()=>{apply.Invoke(board,new object[]{frame});foreach(System.Collections.DictionaryEntry pair in auras){var aura=pair.Value;pose.Invoke(motion,new object[]{aura,phase,true,1f});}};
  shot(names[i]+(j==0?"/windup":"/peak"));
  foreach(var image in motion.GetComponentsInChildren<UnityEngine.UI.Image>())if(image.enabled)check(image.sprite!=null,"status sprite "+names[i]);
 }
 reviewPose=null;check(motion.Count>0,"status active "+names[i]);
}
System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-status-review.txt"),checks);
invoke("ReturnToSelection",new object[0]);return "Captured four persistent status loops at two controlled phases.";
