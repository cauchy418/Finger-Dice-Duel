using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/skill-animation-finals");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
var specs=new[]{"王车易位||-1|-1|target","预兆||4|-1|self","预知未来||7|3|target","幻术默发||2|6|target","诡诈换位||2|8|target","不死奴仆||3|9|target","奥术守御||4|8|self","奥术守御||-1|-1|self","法术抗性|grant|-1|-1|target","不死适应||-1|-1|self","变形术||-1|-1|target","咒缚幽灵||-1|-1|target","暗杀||-1|-1|self","伏击||-1|-1|self","阴影闪避||-1|-1|self","影分身||-1|-1|target","渗透劲||-1|-1|target","渗透劲|detonate|4|-1|target","剑歌||-1|-1|self","剑歌||0|-1|self","战争祭司||-1|-1|target","咒剑诅咒||-1|-1|target","猎人印记||-1|-1|target","荒野形态|moon_bear|1|-1|self","解除荒野|moon_bear|0|-1|self","荒野形态|moon_wolf|1|-1|self","解除荒野|moon_wolf|0|-1|self","荒野形态|moon_eagle|1|-1|self","解除荒野|moon_eagle|0|-1|self","荒野形态|moon_snake|1|-1|self","解除荒野|moon_snake|0|-1|self","星耀形态|stars_archer|1|-1|self","解除星耀|stars_archer|0|-1|self","星耀形态|stars_chalice|1|-1|self","解除星耀|stars_chalice|0|-1|self","星耀形态|stars_dragon|1|-1|self","解除星耀|stars_dragon|0|-1|self","风暴之怒|lightning|-1|-1|target","风暴之怒|thunder|-1|-1|target","破阵强袭||-1|-1|target","元素反应|cold_frost|-1|-1|target","元素反应|cold_wet|-1|-1|target","元素反应|cold_frozen|-1|-1|target","元素反应|cold_burn|-1|-1|target","元素反应|fire_frost|-1|-1|target","元素反应|fire_wet|-1|-1|target","元素反应|fire_greased|-1|-1|target","元素反应|lightning_wet|-1|-1|target","元素反应|thunder_resonance|-1|-1|target","元素反应|lightning_resonance|-1|-1|target","元素反应|thunder_shock|-1|-1|target","元素反应|acid_poison|-1|-1|target","元素反应|poison_acid|-1|-1|target"};int group=0,stage=0,count=0,baseline=0;bool done=false;CombatVisualEvent current=null;UnityEngine.Vector2 fixedOrigin=UnityEngine.Vector2.zero;
double began=UnityEditor.EditorApplication.timeSinceStartup;GameManager g=null;FingerDiceDuel.UI.CombatAnimationPlayer anim=null;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(UnityEditor.EditorApplication.timeSinceStartup-began>110)throw new System.Exception("queue review timeout");
 if(stage==0){
  invoke("CleanupMatch",new object[0]);
  if(group>0){var lesson=TatteredBannersLesson.Create(group==1?21:29,0);invoke("BindBannersMatch",new object[]{lesson});boot.GetType().GetField("_adventure",flags).SetValue(boot,null);lesson.Board.TutorialInput=null;invoke("StartPreparedMatch",new object[]{false});}else boot.StartMatch(CharacterDefinition.Blank,CharacterDefinition.Blank);
  g=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");foreach(var p in g.Players)controller.BindPlayer(p,null,new SimpleChoiceProvider());g.RecordCombatVisuals=true;g.ClearCombatVisuals();invoke("RefreshView",new object[0]);
  board.ShowCombatWindup(g.Players[0],g.Players[0].LeftHand,"weapon_sword",AttackDamageType.Slashing);board.HideCombatWindup();anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");baseline=anim.PlayedEvents;
  if(group==0)foreach(var spec in specs){var c=spec.Split('|');var target=c[4]=="self"?g.PlayerA:g.PlayerB;var data=new SkillVisualData(g.PlayerA.LeftHand,g.PlayerB.RightHand,int.Parse(c[2]),int.Parse(c[3]),c[1]);g.RecordSkillVisual(g.PlayerA,c[0],target,data);}
  else g.RecordSkillVisual(g.Players[0],"王车易位",g.Players[2]);
  var events=g.DrainCombatVisuals();count=events.Count;done=false;current=null;board.PlayCombatEvents(events,g.CaptureVisualFrame(),()=>done=true);check(board.CombatAnimationBusy,"queue starts "+group);check(!canvas.GetComponent<UnityEngine.CanvasGroup>().interactable,"input locks "+group);stage=1;
 }else{
  if(anim.Current!=null){var origin=(UnityEngine.Vector2)field(anim,"_skillOrigin");if(!object.ReferenceEquals(current,anim.Current)){current=anim.Current;fixedOrigin=origin;}else if(origin!=fixedOrigin)throw new System.Exception("moving path anchor "+group);}
  if(!done)return;
  check(!board.CombatAnimationBusy,"queue completes "+group);check(anim.PlayedEvents-baseline==count,"every clip played "+group+" count="+count);check(canvas.GetComponent<UnityEngine.CanvasGroup>().interactable,"input restores "+group);
  check(((System.Collections.ICollection)field(anim,"_finalNumbers")).Count==0,"digit helpers cleaned "+group);check(((System.Collections.IDictionary)field(board,"_evadeOrigins")).Count==0,"poses restored "+group);
  check(g.Players.Count==(group==0?2:group==1?4:8),"expected layout "+group);shot("queue-mode-"+group);group++;stage=0;
  if(group==3){UnityEditor.EditorApplication.update-=tick;invoke("ReturnToSelection",new object[0]);boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"queue-validation.txt"),checks);}
 }
 }catch(System.Exception ex){UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"queue-validation.txt"),string.Join("\n",checks)+"\nFAIL "+ex);}};
UnityEditor.EditorApplication.update+=tick;return "Scheduled all 53 dedicated clips and 2v2/8-player portal queues.";
