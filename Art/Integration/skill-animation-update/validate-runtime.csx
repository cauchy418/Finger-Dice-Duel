using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string> invoke=n=>boot.GetType().GetMethod(n,flags).Invoke(boot,new object[0]);
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");
var canvas=(UnityEngine.RectTransform)field(board,"_canvasRoot");
var report="Art/Integration/skill-animation-update/runtime-validation.txt";
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
float oldDelay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;bool oldBackground=UnityEngine.Application.runInBackground;UnityEngine.Application.runInBackground=true;
var scenarios=new[]{"shape overflow","hunter shield","spore overflow","grave strength","sheep skip","fatal reroll"};
int current=0,phase=0;bool finished=false;GameManager game=null;System.Collections.Generic.IReadOnlyList<CombatVisualEvent> cues=null;
double start=UnityEditor.EditorApplication.timeSinceStartup;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 if(UnityEditor.EditorApplication.timeSinceStartup-start>70)throw new System.Exception("runtime validation timeout");
 if(current==scenarios.Length){
  UnityEditor.EditorApplication.update-=tick;board.StopAnimations();invoke("ReturnToSelection");boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=oldBackground;System.IO.File.WriteAllLines(report,checks);return;
 }
 var name=scenarios[current];
 if(phase==0){
  var a=current==1?CharacterDefinition.RangerHunter:current==5?CharacterDefinition.Gambler:CharacterDefinition.Blank;
  var b=current==0?CharacterDefinition.CircleOfTheMoon:current==2?CharacterDefinition.CircleOfSpores:current==3?CharacterDefinition.ShadowMagic:CharacterDefinition.Blank;
  boot.StartMatch(a,b);game=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");
  foreach(var p in game.Players)controller.BindPlayer(p,null,new SimpleChoiceProvider());
  game.RecordCombatVisuals=true;game.ClearCombatVisuals();var target=game.PlayerB;
  if(current==0){target.LeftHand.SetValue(4);target.RightHand.SetValue(6);check(target.EnterWild(5,0,game),"owlbear enter");game.ClearCombatVisuals();}
  if(current==1){CharacterBehaviors.FirstImplementer<RangerHunterLogic>(game.PlayerA).Mark(target);target.LeftHand.SetShieldState(ShieldState.Formed);}
  if(current==2)target.GainSporeLife();
  if(current==3)target.SetHealthExact(2);
  if(current==4){check(target.TryPolymorph(1),"sheep enter");target.TickStatuses(game);}
  if(current==5){game.PlayerA.SetHealthExact(1);game.PayForGambleReroll(game.PlayerA,2,new System.Random(42));}
  if(current<4)new AttackContext(game.PlayerA,target,game.PlayerA.LeftHand,current==0?20:current==2?14:4,game){IgnoreStatBonuses=current==0}.Resolve(game.GetChoiceProvider(target),game);
  cues=game.DrainCombatVisuals();var final=game.CaptureVisualFrame();
  check(cues.Count>=1,"real rule cue "+name);
  if(current==0)check(cues[0].After.Players[1].Health==0&&cues[1].After.Players[1].Health==20&&cues[2].After.Players[1].Health==16,"break -> base health -> overflow");
  if(current==1)check(cues[1].Outcome==CombatVisualOutcome.Blocked&&cues[2].Kind==CombatVisualKind.ShieldBreak&&cues[3].SkillData.Variant=="guard","block -> shield break -> true damage");
  if(current==2)check(cues[1].SkillData.Variant=="break"&&cues[2].After.Players[1].Health==16,"spore -> ordinary health");
  if(current==3)check(cues[0].After.Players[1].Health==0&&cues[1].After.Players[1].Health==1,"zero -> grave restore");
  if(current==5)check(cues[0].SkillData.Variant=="fatal"&&cues[0].SkillData.Value==-1,"fatal payment has no roll");
  invoke("RefreshView");
  finished=false;board.PlayCombatEvents(cues,final,()=>finished=true);
  check(board.CombatAnimationBusy&&!canvas.GetComponent<UnityEngine.CanvasGroup>().interactable,"input locked "+name);
  if(current==4){var anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");foreach(var button in ((UnityEngine.GameObject)field(anim,"_controls")).GetComponentsInChildren<UnityEngine.UI.Button>())if(button.name=="Skip")button.onClick.Invoke();}
  phase=1;
 }else if(finished){
  check(!board.CombatAnimationBusy&&canvas.GetComponent<UnityEngine.CanvasGroup>().interactable,"queue finished and input restored "+name);
  var anim=(FingerDiceDuel.UI.CombatAnimationPlayer)field(board,"_combatAnimations");
  check(anim.Current==null&&!anim.WindupVisible,"no stale effect "+name);
  current++;phase=0;
 }
}catch(System.Exception ex){
 UnityEditor.EditorApplication.update-=tick;board.StopAnimations();boot.AiThinkSeconds=oldDelay;UnityEngine.Application.runInBackground=oldBackground;
 System.IO.File.WriteAllText(report,string.Join("\n",checks)+"\nFAIL "+ex);
}};
UnityEditor.EditorApplication.update+=tick;
return "Scheduled six actual-rule animation queues.";
