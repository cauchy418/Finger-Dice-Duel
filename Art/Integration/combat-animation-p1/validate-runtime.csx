using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/combat-animation-p1");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
int test=0,stage=0;double began=UnityEditor.EditorApplication.timeSinceStartup;float captureAt=0;bool captured=false;GameManager g=null;BoardController controller=null;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(UnityEditor.EditorApplication.timeSinceStartup-began>45)throw new System.Exception("runtime timeout");
 if(stage==0){
  invoke("CleanupMatch",new object[0]);
  if(test==6||test==7||test==9||test==10){var lesson=TatteredBannersLesson.Create(test==6?21:test==7?29:test==9?24:26,test>=9?1:0);invoke("BindBannersMatch",new object[]{lesson});boot.GetType().GetField("_adventure",flags).SetValue(boot,null);lesson.Board.TutorialInput=null;invoke("StartPreparedMatch",new object[]{false});}
  else boot.StartMatch(test==5?CharacterDefinition.OathOfDevotion:CharacterDefinition.Blank,test==3?CharacterDefinition.TheArchfey:test==4?CharacterDefinition.Swashbuckler:CharacterDefinition.Blank);
  g=(GameManager)field(boot,"_game");controller=(BoardController)field(boot,"_controller");
  foreach(var p in g.Players)controller.BindPlayer(p,null,new SimpleChoiceProvider());
  g.RecordCombatVisuals=true;g.ClearCombatVisuals();
  if(test==1){g.PlayerB.ApplyStatus(StatusEffect.Wet,2);g.PlayerB.ApplyStatus(StatusEffect.WarBlessing,2);g.PlayerB.ApplyStatus(StatusEffect.Frozen,2);invoke("RefreshView",new object[0]);var ambient=(FingerDiceDuel.UI.CombatStatusMotion)field(board,"_statusMotion");check(ambient.Count>=3,"persistent negative and role effects registered");shot("persistent-states");}
  if(test==6||test==7){g.ForceActivePlayerForTest(g.Players[0]);g.Players[0].LeftHand.SetValue(2);g.Players[1].LeftHand.SetValue(6);controller.BeginTurn();controller.ClickHand(g.Players[0].LeftHand);controller.ClickHand(g.Players[1].LeftHand);invoke("RefreshView",new object[0]);var anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");check(anim.WindupVisible,"target-selection windup mode "+test);shot("windup-"+test);controller.ClickHand(g.Players[1].LeftHand);}
  else if(test==8)g.RecordSkillVisual(g.PlayerA,"预知未来",g.PlayerB);
  else if(test==9){g.AnsweredLookupFor=(p,e,v)=>e==HandEffect.OathOfDevotionRedirect?g.Players[0].LeftHand:e==HandEffect.OathOfDevotionShockTarget?g.Players[1].LeftHand:null;new AttackContext(g.Players[1],g.Players[3],g.Players[1].LeftHand,4,g).Resolve(g.GetChoiceProvider(g.Players[3]),g);}
  else if(test==10){g.Players[2].LeftHand.SetShieldState(ShieldState.Formed);g.AnsweredLookupFor=(p,e,v)=>e==HandEffect.OathbreakerTransfer?g.Players[2].LeftHand:null;new AttackContext(g.Players[1],g.Players[0],g.Players[1].LeftHand,4,g).Resolve(g.GetChoiceProvider(g.Players[0]),g);}
  else if(test==0){g.PlayerA.LeftHand.SetValue(3);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.SetHealthExact(1);controller.BeginTurn();controller.ClickHand(g.PlayerA.LeftHand);controller.ClickHand(g.PlayerB.LeftHand);if(controller.Phase==BoardPhase.ChoosingTargetPlayer)controller.ClickHand(g.PlayerB.LeftHand);}
  else if(test==5){g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);}
  else{if(test==2)g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);if(test==3)g.PlayerB.LeftHand.SetValue(4);if(test==4){g.PlayerB.LeftHand.SetValue(1);((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerB)).SetChoice(HandEffect.SwashbucklerDodge,g.PlayerB.LeftHand);}
   var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){HitCount=test==2?2:1,AttackType=test==1?AttackDamageType.Fire:AttackDamageType.Slashing};attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);}
  invoke("RefreshView",new object[0]);check(board.CombatAnimationBusy,"queue busy "+test);check(!canvas.GetComponent<UnityEngine.CanvasGroup>().interactable,"gameplay locked "+test);if(test==0)check(!((UnityEngine.GameObject)field(board,"_gameOverPanel")).activeSelf,"last hit not hidden by victory");
  if(test==11){var anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");foreach(var button in ((UnityEngine.GameObject)field(anim,"_controls")).GetComponentsInChildren<UnityEngine.UI.Button>())if(button.name=="Skip")button.onClick.Invoke();}
  captureAt=UnityEngine.Time.unscaledTime+.15f;captured=false;stage=1;
 }else if(stage==1){
  if(!captured&&UnityEngine.Time.unscaledTime>=captureAt){shot("flight-"+test);captured=true;}
  if(board.CombatAnimationBusy)return;
  check(canvas.GetComponent<UnityEngine.CanvasGroup>().interactable,"input restored "+test);
  check(!((FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations")).WindupVisible,"windup cleared "+test);
  if(test==2)check(g.PlayerB.Health==18,"two segments use one shield and one hit");
  if(test==3)check(g.PlayerB.Health==20,"fey no damage");
  if(test==4)check(g.PlayerB.Health==20,"dodge no damage");
  if(test==0)check(((UnityEngine.GameObject)field(board,"_gameOverPanel")).activeSelf,"victory after last-hit animation");
  test++;stage=0;
  if(test==12){UnityEditor.EditorApplication.update-=tick;invoke("ReturnToSelection",new object[0]);boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"runtime-validation.txt"),checks);}
 }
}catch(System.Exception e){UnityEditor.EditorApplication.update-=tick;boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=oldBackground;board.StopAnimations();System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"runtime-validation.txt"),string.Join("\n",checks)+"\nFAIL "+e);}};
UnityEditor.EditorApplication.update+=tick;
return "Scheduled 8 live combat animation scenarios.";
