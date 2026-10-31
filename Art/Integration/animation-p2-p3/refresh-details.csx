using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/animation-p2-p3");System.IO.Directory.CreateDirectory(folder);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
var raw=System.IO.Path.GetFullPath("Temp/AnimationP23Frames");System.IO.Directory.CreateDirectory(raw);
System.Action reviewPose=null;
System.Action<string> shot=name=>{
 var go=new UnityEngine.GameObject("AnimationReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1280,720,24);var tex=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float dist=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();reviewPose?.Invoke();UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(raw,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=dist;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
var clips=new[]{"p2-23-rage|狂怒|PathOfTheBerserker||9|2|None|None|self","p2-24-rage|狂乱|PathOfTheBerserker|frenzy|4|2|None|None|self","p3-31-measure|战术优势|Tactician||12|2|None|None|self","p2-48-book|无与伦比技艺|CollegeOfLore|growth|1|2|None|None|self","p2-11-eye-expire|思维之盾|TheGreatOldOne|expire|0|2|None|None|target"};int index=0;double began=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(index>=clips.Length){UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-review-details.txt"),checks);return;}
 if(UnityEditor.EditorApplication.timeSinceStartup-began>300)throw new System.Exception("capture timeout");
 var c=clips[index].Split('|');var definition=c[2]=="Phantom"?MageTowerChapter.Phantom:(CharacterDefinition)typeof(CharacterDefinition).GetProperty(c[2]).GetValue(null);
 boot.StartMatch(definition,CharacterDefinition.Blank);var g=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");foreach(var player in g.Players)controller.BindPlayer(player,null,new SimpleChoiceProvider());
 g.RecordCombatVisuals=false;
 for(int pending=0;pending<8&&controller.PendingWizardChoice!=null;pending++){
  var choice=controller.PendingWizardChoice;if(choice.Options!=null&&choice.Options.Count>0){int selected=choice.Options[0].Value;int desired=(int)(AttackDamageType)System.Enum.Parse(typeof(AttackDamageType),c[6]);foreach(var option in choice.Options)if(option.Value==desired)selected=desired;check(controller.ChooseSkillOption(selected),"resolve initial choice "+c[0]);}
  else check(controller.ChooseWizardHand(choice.AllowDecline?null:choice.Candidates[0]),"resolve initial hand "+c[0]);}
 g.RecordCombatVisuals=true;g.ClearCombatVisuals();g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);g.PlayerB.LeftHand.SetValue(3);g.PlayerB.RightHand.SetValue(8);
 if(c[1]=="气"||c[1]=="醉拳技巧"||c[1]=="酣醉若狂"){g.PlayerA.GainMonkKi(6);g.PlayerA.TrySpendMonkKi(6-int.Parse(c[4]));var stance=CharacterBehaviors.FirstImplementer<FingerDiceDuel.Characters.SpecialLogics.WayOfTheDrunkenMasterLogic>(g.PlayerA);if(stance!=null&&c[3]!="expire")stance.TryEnter(g.PlayerA);}
 if(c[1]=="狂怒")g.PlayerA.LeftHand.SetValue(9);
 if(c[1]=="狂乱"){g.PlayerA.SetHealthExact(g.PlayerA.MaximumHealth-int.Parse(c[4])*5);CharacterBehaviors.FirstImplementer<IDynamicStats>(g.PlayerA)?.RefreshDynamicStats(g.PlayerA,g);}
 if(c[1]=="战术优势"){g.PlayerB.SetHealthExact(g.PlayerA.Health-int.Parse(c[4]));CharacterBehaviors.FirstImplementer<IDynamicStats>(g.PlayerA)?.RefreshDynamicStats(g.PlayerA,g);}
 if(c[1]=="战争祝福"){g.PlayerA.LeftHand.SetValue(9);g.PlayerA.ApplyStatus(StatusEffect.WarBlessing,2,g.PlayerA);}
 var target=c[8]=="self"?g.PlayerA:g.PlayerB;var status=(StatusEffect)System.Enum.Parse(typeof(StatusEffect),c[7]);if(status!=StatusEffect.None)target.ApplyStatus(status,2,g.PlayerA);
 invoke("RefreshView",new object[0]);board.ShowCombatWindup(g.PlayerA,g.PlayerA.LeftHand,"weapon_sword",AttackDamageType.Slashing);board.HideCombatWindup();
 var anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");
 var data=new SkillVisualData(g.PlayerA.LeftHand,c[1]=="快手"||c[1]=="守望束缚"||c[1]=="大地行者"?g.PlayerB.RightHand:null,int.Parse(c[4]),int.Parse(c[5]),c[3],g.PlayerB,(AttackDamageType)System.Enum.Parse(typeof(AttackDamageType),c[6]),status);
 g.RecordSkillVisual(g.PlayerA,c[1],target,data);var events=g.DrainCombatVisuals();check(events.Count==1,"cue "+c[0]);
 System.IO.Directory.CreateDirectory(System.IO.Path.Combine(raw,c[0]));
 reviewPose=()=>{board.SyncCombatStatus(g.CaptureVisualFrame());anim.PreviewFinalSkill(events[0],.28f);};shot(c[0]+"/windup");reviewPose=()=>{board.SyncCombatStatus(g.CaptureVisualFrame());anim.PreviewFinalSkill(events[0],.62f);};shot(c[0]+"/peak");reviewPose=null;
 int visibleText=0;foreach(var text in ((UnityEngine.RectTransform)field(anim,"_root")).GetComponentsInChildren<UnityEngine.UI.Text>())if(text.name=="SkillNumber"&&text.gameObject.activeInHierarchy)visibleText++;
 check(visibleText==((System.Collections.ICollection)field(anim,"_finalNumbers")).Count,"no stale digits "+c[0]);
 int visibleSprites=0;foreach(var sprite in ((UnityEngine.RectTransform)field(anim,"_root")).GetComponentsInChildren<UnityEngine.UI.Image>())if(sprite.enabled&&sprite.gameObject.activeInHierarchy){check(sprite.sprite!=null,"sprite "+c[0]);if(sprite.color.a>.02f)visibleSprites++;}check(visibleSprites>0,"visible effect "+c[0]);
 anim.StopPlayback();check(!board.CombatAnimationBusy,"cleanup "+c[0]);index++;
 }catch(System.Exception ex){UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;invoke("ReturnToSelection",new object[0]);UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-error-details.txt"),ex.ToString());}};
UnityEditor.EditorApplication.update+=tick;return "Started P2/P3 native capture: "+clips.Length+" variants / two frames each.";
