using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>{var method=boot.GetType().GetMethod(n,flags);var parameters=method.GetParameters();var args=new object[parameters.Length];for(int j=0;j<args.Length;j++)args[j]=j<a.Length?a[j]:parameters[j].DefaultValue;method.Invoke(boot,args);};
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/skill-animation-rework");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
checks.Add("Capture healing evidence: actual gameplay callbacks, then production preview frames.");
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
var raw=System.IO.Path.GetFullPath("Temp/AnimationReworkFrames");System.IO.Directory.CreateDirectory(raw);
System.Action reviewPose=null;
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();reviewPose?.Invoke();UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(raw,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
var clips=new[]{
 "wolf-healing|狼形态|CircleOfTheMoon",
 "chalice-healing|圣杯座|CircleOfTheStars",
 "fiend-healing|黑暗强运|TheFiend",
 "harmony-healing|混元体|WayOfTheOpenHand",
 "ancient-healing|不灭哨卫|OathOfTheAncients"
};
int index=0;double began=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(index>=clips.Length){System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-healing-review.txt"),checks);UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-healing-review.txt"),checks);return;}
 if(UnityEditor.EditorApplication.timeSinceStartup-began>180)throw new System.Exception("native review timeout");
 var c=clips[index].Split('|');var definition=(CharacterDefinition)typeof(CharacterDefinition).GetProperty(c[2]).GetValue(null);
 boot.StartMatch(definition,CharacterDefinition.Blank);var g=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");foreach(var p in g.Players)controller.BindPlayer(p,null,new SimpleChoiceProvider());
 g.RecordCombatVisuals=false;
 for(int pending=0;pending<8&&controller.PendingWizardChoice!=null;pending++){
  var choice=controller.PendingWizardChoice;if(choice.Options!=null&&choice.Options.Count>0){int selected=choice.Options[0].Value;check(controller.ChooseSkillOption(selected),"initial choice "+c[0]);}
  else check(controller.ChooseWizardHand(choice.AllowDecline?null:choice.Candidates[0]),"initial hand "+c[0]);}
 g.RecordCombatVisuals=true;g.ClearCombatVisuals();g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);g.PlayerB.LeftHand.SetValue(3);g.PlayerB.RightHand.SetValue(8);
 var source=g.PlayerA.LeftHand;var target=c[0]=="chalice-healing"?g.PlayerB:g.PlayerA;
 if(c[0]=="wolf-healing")check(g.PlayerA.EnterWild(2,0,g),"wolf form entry");
 if(c[0]=="chalice-healing")check(g.PlayerA.EnterWild(2,2,g),"chalice form entry");
 target.SetHealthExact(c[0]=="harmony-healing"?2:c[0]=="ancient-healing"?10:5);
 target.ApplyStatus(StatusEffect.Scorch,2,c[0]=="chalice-healing"?g.PlayerA:g.PlayerB);g.ClearCombatVisuals();
 if(c[0]=="chalice-healing"){
  g.DeferWizardChoice=r=>{r.Choose(g.PlayerB.LeftHand);return true;};source.SetValue(9);
  g.PlayerA.Character.OnOperationResolved(g.PlayerA,source,8,1,HandEffectType.Heal,g,null);
 }else if(c[0]=="harmony-healing")check(CharacterBehaviors.FirstImplementer<WayOfTheOpenHandLogic>(g.PlayerA).TryHarmonyBody(g.PlayerA,g),"harmony activation");
 else if(c[0]=="ancient-healing")new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,4,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerA),g);
 else {if(c[0]=="fiend-healing")g.PlayerB.SetHealthExact(10);new AttackContext(g.PlayerA,g.PlayerB,source,4,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerB),g);}
 var events=g.DrainCombatVisuals();CombatVisualEvent cue=null;foreach(var e in events)if(e.RedirectKind==c[1]){cue=e;break;}
 check(cue!=null,"cue "+c[0]);
 CombatVisualEvent scorch=null;int hi=-1,si=-1;for(int n=0;n<events.Count;n++){if(events[n]==cue)hi=n;if(events[n].RedirectKind=="灼伤"){scorch=events[n];si=n;}}
 int expected=c[0]=="chalice-healing"?9:c[0]=="fiend-healing"?3:c[0]=="harmony-healing"?4:2;
 int burn=c[0]=="chalice-healing"?5:c[0]=="fiend-healing"||c[0]=="harmony-healing"?2:1;
 check(cue.Target==target,"actual healing target "+c[0]);check(cue.SkillData.Value==expected,"gross healing "+expected+" "+c[0]);
 check(scorch!=null&&scorch.SkillData.Value==burn,"separate actual Scorch "+burn+" "+c[0]);check(si>hi,"healing precedes Scorch "+c[0]);
 int healedHp=0,burnBefore=0,burnAfter=0;foreach(var v in cue.After.Players)if(v.Player==target)healedHp=v.Health;foreach(var v in scorch.Before.Players)if(v.Player==target)burnBefore=v.Health;foreach(var v in scorch.After.Players)if(v.Player==target)burnAfter=v.Health;
 check(healedHp==burnBefore&&burnBefore-burnAfter==burn,"snapshot continuity "+c[0]+" hp "+healedHp+" -> "+burnAfter);
 invoke("RefreshView",new object[0]);board.ShowCombatWindup(g.PlayerA,source,"weapon_sword",AttackDamageType.Slashing);board.HideCombatWindup();
 var anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");
 var apply=board.GetType().GetMethod("ApplyCombatFrame",flags);
 System.IO.Directory.CreateDirectory(System.IO.Path.Combine(raw,c[0]));
 reviewPose=()=>{apply.Invoke(board,new object[]{cue.Before});anim.PreviewFinalSkill(cue,.28f);};shot(c[0]+"/windup");
 reviewPose=()=>{apply.Invoke(board,new object[]{cue.After});anim.PreviewFinalSkill(cue,.62f);};shot(c[0]+"/peak");reviewPose=null;
 int visibleSprites=0;
 foreach(var sprite in ((UnityEngine.RectTransform)field(anim,"_root")).GetComponentsInChildren<UnityEngine.UI.Image>())if(sprite.enabled&&sprite.gameObject.activeInHierarchy){check(sprite.sprite!=null,"sprite "+c[0]);if(sprite.color.a>.02f)visibleSprites++;}
 check(visibleSprites>0,"visible effect "+c[0]);
 anim.StopPlayback();check(!board.CombatAnimationBusy,"cleanup "+c[0]);index++;
 }catch(System.Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-healing-error.txt"),ex.ToString());UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-healing-error.txt"),ex.ToString());}};
UnityEditor.EditorApplication.update+=tick;return "Started native update capture: "+clips.Length+" variants / two frames each.";
