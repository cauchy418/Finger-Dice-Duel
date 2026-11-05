using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/skill-animation-update");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
var raw=System.IO.Path.GetFullPath("Temp/AnimationNovemberFrames");System.IO.Directory.CreateDirectory(raw);
System.Action reviewPose=null;
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();reviewPose?.Invoke();UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(raw,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
var clips=new[]{
    "wild-bear-enter|荒野形态|CircleOfTheMoon|enter|1|0|1|self|None|None",
    "wild-bear-exit|解除荒野|CircleOfTheMoon|exit|1|0|0|self|None|None",
    "wild-bear-break|解除荒野|CircleOfTheMoon|break|1|0|0|self|None|None",
    "wild-wolf-enter|荒野形态|CircleOfTheMoon|enter|2|0|1|self|None|None",
    "wild-wolf-exit|解除荒野|CircleOfTheMoon|exit|2|0|0|self|None|None",
    "wild-wolf-break|解除荒野|CircleOfTheMoon|break|2|0|0|self|None|None",
    "wild-eagle-enter|荒野形态|CircleOfTheMoon|enter|3|0|1|self|None|None",
    "wild-eagle-exit|解除荒野|CircleOfTheMoon|exit|3|0|0|self|None|None",
    "wild-eagle-break|解除荒野|CircleOfTheMoon|break|3|0|0|self|None|None",
    "wild-snake-enter|荒野形态|CircleOfTheMoon|enter|4|0|1|self|None|None",
    "wild-snake-exit|解除荒野|CircleOfTheMoon|exit|4|0|0|self|None|None",
    "wild-snake-break|解除荒野|CircleOfTheMoon|break|4|0|0|self|None|None",
    "wild-owlbear-enter|荒野形态|CircleOfTheMoon|enter|5|0|2|self|None|None",
    "wild-owlbear-exit|解除荒野|CircleOfTheMoon|exit|5|0|0|self|None|None",
    "wild-owlbear-break|解除荒野|CircleOfTheMoon|break|5|0|0|self|None|None",
    "wild-panther-enter|荒野形态|CircleOfTheMoon|enter|6|0|2|self|None|None",
    "wild-panther-exit|解除荒野|CircleOfTheMoon|exit|6|0|0|self|None|None",
    "wild-panther-break|解除荒野|CircleOfTheMoon|break|6|0|0|self|None|None",
    "stars-archer-enter|荒野形态|CircleOfTheStars|enter|1|1|1|self|None|None",
    "stars-archer-exit|解除荒野|CircleOfTheStars|exit|1|1|0|self|None|None",
    "stars-archer-break|解除荒野|CircleOfTheStars|break|1|1|0|self|None|None",
    "stars-chalice-enter|荒野形态|CircleOfTheStars|enter|2|2|1|self|None|None",
    "stars-chalice-exit|解除荒野|CircleOfTheStars|exit|2|2|0|self|None|None",
    "stars-chalice-break|解除荒野|CircleOfTheStars|break|2|2|0|self|None|None",
    "stars-dragon-enter|荒野形态|CircleOfTheStars|enter|3|3|1|self|None|None",
    "stars-dragon-exit|解除荒野|CircleOfTheStars|exit|3|3|0|self|None|None",
    "stars-dragon-break|解除荒野|CircleOfTheStars|break|3|3|0|self|None|None",
    "stars-dragon-operation|巨龙座|CircleOfTheStars|operation|7|4|3|self|None|None",
    "stars-chalice-heal|圣杯座|CircleOfTheStars|heal|7|-1|0|foe|None|None",
    "spore-apply|孢子环绕|CircleOfSpores|apply|2|-1|0|foe|None|Decay",
    "spore-gain|一体共生|CircleOfSpores||10|-1|1|self|None|None",
    "spore-absorb|孢子生命|CircleOfSpores|absorb|4|6|0|self|None|None",
    "spore-break|孢子生命|CircleOfSpores|break|6|0|0|self|None|None",
    "spore-restore|真菌感染|CircleOfSpores||2|1|0|self|None|None",
    "overflow|余伤|CircleOfTheMoon||4|-1|0|self|None|None",
    "hunter-consume|巨像屠夫|RangerHunter|consume|4|-1|0|foe|None|None",
    "hunter-guard|巨像屠夫|RangerHunter|guard|1|-1|0|foe|None|None",
    "gambler-reroll|恶之强运|Gambler|reroll|8|2|3|self|None|None",
    "gambler-fatal|恶之强运|Gambler|fatal|-1|2|3|self|None|None",
    "shadow-darkness|黑暗术|ShadowMagic||2|-1|0|foe|None|DarknessBlind",
    "shadow-grave|坟冢之力|ShadowMagic||1|-1|0|self|None|None",
    "shadow-eyes|幽暗之瞳|ShadowMagic||2|-1|0|foe|Necrotic|DarknessBlind",
    "blessed-healing|神佑医者|LifeDomain|blessing|2|-1|0|foe|None|BlessedHealing",
    "dragon-extend-fire|龙族体魄|DraconicBloodline|extend|3|2|0|foe|Fire|Burn",
    "dragon-extend-cold|龙族体魄|DraconicBloodline|extend|3|2|0|foe|Cold|Frost",
    "dragon-extend-lightning|龙族体魄|DraconicBloodline|extend|3|2|0|foe|Lightning|Shock",
    "dragon-extend-poison|龙族体魄|DraconicBloodline|extend|3|2|0|foe|Poison|Poison",
    "dragon-extend-acid|龙族体魄|DraconicBloodline|extend|3|2|0|foe|Acid|Acid",
    "storm-pierce-lightning|暴风狂怒|StormSorcery|pierce|-1|-1|0|foe|Lightning|None",
    "storm-pierce-thunder|暴风狂怒|StormSorcery|pierce|-1|-1|0|foe|Thunder|None",
    "gloom-enter|伏击|GloomStalker|conceal|2|-1|0|self|None|None",
    "gloom-reveal|阴影闪避|GloomStalker|reveal|6|-1|0|self|None|None",
    "land-hand|大地行者|CircleOfTheLand|roots|-1|-1|0|self|None|None",
    "monk-ki-fire|四象门徒|WayOfTheFourElements||-1|-1|2|foe|Fire|None",
    "monk-ki-cold|四象门徒|WayOfTheFourElements||-1|-1|2|foe|Cold|None",
    "monk-ki-lightning|四象门徒|WayOfTheFourElements||-1|-1|2|foe|Lightning|None",
    "monk-ki-thunder|四象门徒|WayOfTheFourElements||-1|-1|2|foe|Thunder|None",
    "monk-ki-acid|四象门徒|WayOfTheFourElements||-1|-1|2|foe|Acid|None",
    "illusion-guard|幻影实体|SchoolOfIllusion||2|3|0|self|None|None",
    "sheep-enter|变形术|SchoolOfTransmutation|enter|5|-1|0|foe|None|None",
    "sheep-expire|变形术|SchoolOfTransmutation|expire|-1|-1|0|foe|None|None",
    "sheep-break|变形术|SchoolOfTransmutation|break|-1|-1|0|foe|None|None",
    "specter-dismiss|咒缚幽灵|TheHexblade|death|-1|-1|0|foe|None|None"
};

int index=0;double began=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(index>=clips.Length){UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-review.txt"),checks);return;}
 if(UnityEditor.EditorApplication.timeSinceStartup-began>180)throw new System.Exception("native review timeout");
 var c=clips[index].Split('|');var definition=(CharacterDefinition)typeof(CharacterDefinition).GetProperty(c[2]).GetValue(null);
 boot.StartMatch(definition,CharacterDefinition.Blank);var g=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");foreach(var p in g.Players)controller.BindPlayer(p,null,new SimpleChoiceProvider());
 g.RecordCombatVisuals=false;
 for(int pending=0;pending<8&&controller.PendingWizardChoice!=null;pending++){
  var choice=controller.PendingWizardChoice;if(choice.Options!=null&&choice.Options.Count>0){int selected=choice.Options[0].Value;check(controller.ChooseSkillOption(selected),"initial choice "+c[0]);}
  else check(controller.ChooseWizardHand(choice.AllowDecline?null:choice.Candidates[0]),"initial hand "+c[0]);}
 g.RecordCombatVisuals=true;g.ClearCombatVisuals();g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);g.PlayerB.LeftHand.SetValue(3);g.PlayerB.RightHand.SetValue(8);
 var target=c[7]=="self"?g.PlayerA:g.PlayerB;var status=(StatusEffect)System.Enum.Parse(typeof(StatusEffect),c[9]);if(status!=StatusEffect.None)target.ApplyStatus(status,2,g.PlayerA);
 var source=g.PlayerA.RightHand;
 bool dragon=c[1]=="巨龙座",land=c[1]=="大地行者";
 var destination=dragon?g.PlayerB.LeftHand:land?source:null;
 var anchor=dragon||c[1]=="真菌感染"?g.PlayerB:g.PlayerA;
 if(land)source.SetTargetRestricted(true);
 if(c[3]=="conceal")source.Conceal();
 if(c[1]=="坟冢之力")target.SetHealthExact(1);
 if(c[1]=="孢子生命"||c[1]=="一体共生")target.GainSporeLife();
 int value=int.Parse(c[4]),previous=int.Parse(c[5]),amount=int.Parse(c[6]);
 if(c[1]=="荒野形态")check(g.PlayerA.EnterWild(value,previous,g),"true form entry "+c[0]);
 else if(c[1]=="解除荒野"){
  check(g.PlayerA.EnterWild(value,previous,g),"true form preparation "+c[0]);g.ClearCombatVisuals();
  if(c[3]=="break")new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,35,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerA),g);
  else {var before=g.CaptureVisualFrame();g.PlayerA.EndWild();g.RecordSkillVisual(g.PlayerA,c[1],target,new SkillVisualData(value:value,previous:previous,variant:c[3]),before);}
 }
 else {
  var data=new SkillVisualData(source,destination,value,previous,c[3],anchor,(AttackDamageType)System.Enum.Parse(typeof(AttackDamageType),c[8]),status,amount);
  g.RecordSkillVisual(g.PlayerA,c[1],target,data);
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
 }catch(System.Exception ex){UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-error.txt"),ex.ToString());}};
UnityEditor.EditorApplication.update+=tick;return "Started native update capture: "+clips.Length+" variants / two frames each.";
