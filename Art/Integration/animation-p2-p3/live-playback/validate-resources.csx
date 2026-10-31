using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/animation-p2-p3/live-playback");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();board.SyncCombatStatus(((GameManager)field(boot,"_game")).CaptureVisualFrame());cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool background=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
try{
 System.Action<GameManager,string,int,int> inspect=(g,key,value,points)=>{
  board.SyncCombatStatus(g.CaptureVisualFrame());var motion=(FingerDiceDuel.UI.CombatStatusMotion)field(board,"_statusMotion");var entries=(System.Collections.IDictionary)field(motion,"_auras");bool found=false;
  foreach(System.Collections.DictionaryEntry item in entries){var aura=item.Value;if((string)field(aura,"Key")!=key||!object.ReferenceEquals(field(aura,"Player"),g.PlayerA))continue;found=true;check((int)field(aura,"Value")==value,"typed counter "+key+"="+value);check(((System.Collections.ICollection)field(aura,"Bits")).Count==points,"point slots "+key+"="+points);}
  check(found,"aura exists "+key);
 };
 board.StopAnimations();boot.StartMatch(CharacterDefinition.WayOfTheNinja,CharacterDefinition.Blank);var game=(GameManager)field(boot,"_game");game.RecordCombatVisuals=false;game.PlayerA.LeftHand.SetValue(4);game.PlayerA.RightHand.SetValue(4);MonkRules.AfterAction(game.PlayerA,game);invoke("RefreshView",new object[0]);inspect(game,"Effects/ki",3,6);shot("live-ki-gain");
 game.PlayerA.TrySpendMonkKi(3);invoke("RefreshView",new object[0]);inspect(game,"Effects/ki",0,6);shot("live-ki-empty");
 board.StopAnimations();boot.StartMatch(CharacterDefinition.CollegeOfLore,CharacterDefinition.Blank);game=(GameManager)field(boot,"_game");game.RecordCombatVisuals=false;var book=CharacterBehaviors.FirstImplementer<FingerDiceDuel.Characters.SpecialLogics.CollegeOfLoreLogic>(game.PlayerA);
 for(int n=1;n<=9;n++){game.PlayerA.LeftHand.SetValue(n);book.OnOperationResolved(game.PlayerA,game.PlayerA.LeftHand,0,0,HandEffectType.None,game,null);}
 invoke("RefreshView",new object[0]);inspect(game,"Subclasses/bard_college_of_lore",9,9);shot("live-lore-full");
 board.StopAnimations();boot.StartMatch(CharacterDefinition.WayOfTheDrunkenMaster,CharacterDefinition.Blank);game=(GameManager)field(boot,"_game");game.RecordCombatVisuals=false;game.PlayerA.LeftHand.SetValue(9);var stance=CharacterBehaviors.FirstImplementer<FingerDiceDuel.Characters.SpecialLogics.WayOfTheDrunkenMasterLogic>(game.PlayerA);check(stance.TryEnter(game.PlayerA),"enter drunken stance");invoke("RefreshView",new object[0]);board.SyncCombatStatus(game.CaptureVisualFrame());shot("live-drunken-ambient");
 board.StopAnimations();var ambient=(FingerDiceDuel.UI.CombatStatusMotion)field(board,"_statusMotion");check(ambient.Count==0,"all ambient resources clear");
 check(((System.Collections.IDictionary)field(ambient,"_stats")).Count==0,"stat accents clear");
 System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"resource-validation.txt"),checks);return "Resource loops and cleanup verified.";
}finally{board.StopAnimations();invoke("ReturnToSelection",new object[0]);boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=background;}
