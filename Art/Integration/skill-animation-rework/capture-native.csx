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
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
var clips=new[]{
    "knowledge-carry|岁月知识|KnowledgeDomain||3|-1|0|self|None|KnowledgeBlessing",
    "knowledge-heal|知识赐福|KnowledgeDomain||3|-1|0|foe|None|KnowledgeBlessing",
    "knowledge-consume|知识祝福|KnowledgeDomain|consume|0|3|0|self|Force|None",
    "knowledge-force|知识祝福|KnowledgeDomain||3|-1|0|foe|Force|None",
    "confusion-carry|困惑|KnowledgeDomain|carry|1|14|0|self|None|Confusion",
    "confusion-apply|困惑|KnowledgeDomain|apply|2|-1|0|foe|None|Confusion",
    "holy-flame|净化圣焰|LightDomain||2|-1|0|foe|Fire|HolyLight",
    "holy-pierce|净化圣焰|LightDomain|pierce|-1|-1|0|foe|Fire|None",
    "holy-heal|圣焰赐福|LightDomain||2|-1|0|foe|Fire|HolyLight",
    "scorch|灼伤|LightDomain||3|-1|0|self|Fire|Scorch",
    "beast-gain|野兽形态|PathOfTheBeast|gain|3|2|0|self|None|None",
    "beast-consume|野兽形态|PathOfTheBeast|consume|0|3|6|self|None|None",
    "berserker-five|狂怒|PathOfTheBerserker||5|-1|0|foe|None|None",
    "berserker-nine|狂怒|PathOfTheBerserker||9|-1|0|foe|None|None",
    "berserker-heal|嗜血|PathOfTheBerserker||3|-1|0|self|None|None",
    "giant-strike|巨人之力|PathOfTheGiant||4|-1|2|foe|None|None",
    "giant-recovery|巨人恢复|PathOfTheGiant||6|-1|0|self|None|None",
    "war-heal|战争赐福|WarDomain||-1|-1|0|foe|None|WarBlessing",
    "storm-heal|风暴赐福|TempestDomain||7|3|4|foe|None|None",
    "vengeance-operation|复仇之魂|OathOfVengeance||7|3|4|self|None|None",
    "giant-blade-fire|元素战刃|PathOfTheGiant|prepare|-1|-1|0|self|Fire|None",
    "giant-blade-consume|元素战刃|PathOfTheGiant|consume|-1|-1|0|foe|Fire|None",
    "giant-blade-acid|元素战刃|PathOfTheGiant|prepare|-1|-1|0|self|Acid|None",
    "giant-blade-cold|元素战刃|PathOfTheGiant|prepare|-1|-1|0|self|Cold|None",
    "giant-blade-thunder|元素战刃|PathOfTheGiant|prepare|-1|-1|0|self|Thunder|None",
    "giant-blade-lightning|元素战刃|PathOfTheGiant|prepare|-1|-1|0|self|Lightning|None",
    "trickery-heal|诡术赐福|TrickeryDomain||2|8|0|foe|None|None",
    "warden-force|守望誓约|PathOfTheWarden|convert|-1|-1|0|self|Force|None",
    "transmutation-operation|变化莫测|SchoolOfTransmutation|operation|8|4|0|self|None|None",
    "magician-trickery|诡术打击|Magician||2|-1|0|foe|None|None",
    "wolf-healing|狼形态|CircleOfTheMoon||2|-1|0|self|None|None"
};

int index=0;double began=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(index>=clips.Length){System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-review.txt"),checks);UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-review.txt"),checks);return;}
 if(UnityEditor.EditorApplication.timeSinceStartup-began>180)throw new System.Exception("native review timeout");
 var c=clips[index].Split('|');var definition=(CharacterDefinition)typeof(CharacterDefinition).GetProperty(c[2]).GetValue(null);
 boot.StartMatch(definition,CharacterDefinition.Blank);var g=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");foreach(var p in g.Players)controller.BindPlayer(p,null,new SimpleChoiceProvider());
 g.RecordCombatVisuals=false;
 for(int pending=0;pending<8&&controller.PendingWizardChoice!=null;pending++){
  var choice=controller.PendingWizardChoice;if(choice.Options!=null&&choice.Options.Count>0){int selected=choice.Options[0].Value;check(controller.ChooseSkillOption(selected),"initial choice "+c[0]);}
  else check(controller.ChooseWizardHand(choice.AllowDecline?null:choice.Candidates[0]),"initial hand "+c[0]);}
 g.RecordCombatVisuals=true;g.ClearCombatVisuals();g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);g.PlayerB.LeftHand.SetValue(3);g.PlayerB.RightHand.SetValue(8);
 var target=c[7]=="self"?g.PlayerA:g.PlayerB;var status=(StatusEffect)System.Enum.Parse(typeof(StatusEffect),c[9]);if(status!=StatusEffect.None)target.ApplyStatus(status,2,g.PlayerA);
 var source=c[1]=="风暴赐福"?g.PlayerB.RightHand:g.PlayerA.RightHand;
 bool dragon=c[1]=="巨龙座",land=c[1]=="大地行者";
 var destination=c[1]=="诡术赐福"?g.PlayerB.RightHand:dragon?g.PlayerB.LeftHand:land?source:null;
 var anchor=dragon||c[1]=="真菌感染"||c[1]=="诡术赐福"?g.PlayerB:g.PlayerA;
 if(land)source.SetTargetRestricted(true);
 if(c[3]=="conceal")source.Conceal();
 if(c[1]=="坟冢之力")target.SetHealthExact(1);
 if(c[1]=="孢子生命"||c[1]=="一体共生")target.GainSporeLife();
 int value=int.Parse(c[4]),previous=c[1]=="守望誓约"?(int)AttackDamageType.Fire:int.Parse(c[5]),amount=int.Parse(c[6]);
 if(c[1]=="狼形态"){check(g.PlayerA.EnterWild(2,0,g),"wolf form entry");g.ClearCombatVisuals();}
 if(c[1]=="荒野形态")check(g.PlayerA.EnterWild(value,previous,g),"true form entry "+c[0]);
 else if(c[1]=="解除荒野"){
  check(g.PlayerA.EnterWild(value,previous,g),"true form preparation "+c[0]);g.ClearCombatVisuals();
  if(c[3]=="break")new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,35,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerA),g);
  else {var before=g.CaptureVisualFrame();g.PlayerA.EndWild();g.RecordSkillVisual(g.PlayerA,c[1],target,new SkillVisualData(value:value,previous:previous,variant:c[3]),before);}
 }
 else {
  var data=new SkillVisualData(c[1]=="诡术赐福"?null:source,destination,value,previous,c[3],anchor,(AttackDamageType)System.Enum.Parse(typeof(AttackDamageType),c[8]),status,amount);
  g.RecordSkillVisual(c[1]=="风暴赐福"?g.PlayerB:g.PlayerA,c[1],target,data);
 }
 var events=g.DrainCombatVisuals();CombatVisualEvent cue=null;foreach(var e in events)if(e.RedirectKind==c[1]){cue=e;break;}
 check(cue!=null,"cue "+c[0]);
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
 }catch(System.Exception ex){System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-error.txt"),ex.ToString());UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-error.txt"),ex.ToString());}};
UnityEditor.EditorApplication.update+=tick;return "Started native update capture: "+clips.Length+" variants / two frames each.";
