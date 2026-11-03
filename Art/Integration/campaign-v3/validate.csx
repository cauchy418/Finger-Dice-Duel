using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.UI;
var boot=UnityEngine.Object.FindObjectOfType<GameBootstrap>();
if(boot==null)throw new Exception("No GameBootstrap");
var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
Func<object,string,object> field=(obj,name)=>obj.GetType().GetField(name,flags).GetValue(obj);
Action<object,string,object[]> invoke=(obj,name,args)=>obj.GetType().GetMethod(name,flags).Invoke(obj,args);
var board=(GameBoardUI)field(boot,"_board");
var canvas=((RectTransform)field(board,"_canvasRoot")).GetComponent<Canvas>();
var front=((RectTransform)field(boot,"_selectRoot")).GetComponent<Canvas>();
string output=System.IO.Path.GetFullPath("Art/Integration/campaign-v3");System.IO.Directory.CreateDirectory(output);
var checks=new List<string>();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add("PASS "+label);};
var saved=new Dictionary<string,int?>();Action<string> remember=key=>{if(!saved.ContainsKey(key))saved[key]=PlayerPrefs.HasKey(key)?(int?)PlayerPrefs.GetInt(key):null;};
foreach(var chapter in CampaignChapter.All){remember(chapter.CompletionKey);remember("FingerDiceDuel.Adventure.V3.Access."+chapter.Id);}
for(int i=1;i<=40;i++)remember(CampaignChapter.LevelKey(i));
for(int i=1;i<=30;i++)remember("FingerDiceDuel.Adventure.Level"+i+".Completed");
foreach(var key in new[]{AdventureChapter.CompletionKey,MageTowerChapter.CompletionKey,TatteredBannersChapter.CompletionKey,"FingerDiceDuel.Adventure.V3.Migrated","FingerDiceDuel.Adventure.V3.Archive.tower.evocation.Completed"})remember(key);
bool wasEnabled=boot.enabled;float think=boot.AiThinkSeconds;
Action<Canvas,string,int,int> shot=(target,name,w,h)=>{
 // Sample the idle state instead of the transient first-frame status burst.
 if(target==canvas&&field(board,"_statusMotion") is CombatStatusMotion motion)
 {
  foreach(System.Collections.DictionaryEntry entry in (System.Collections.IDictionary)field(motion,"_auras"))
      entry.Value.GetType().GetField("Flash",flags).SetValue(entry.Value,0f);
  invoke(motion,"Update",new object[0]);
 }
 var go=new GameObject("CampaignPreviewCamera",typeof(Camera));var camera=go.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
 var rt=new RenderTexture(w,h,24);var tex=new Texture2D(w,h,TextureFormat.RGB24,false);var old=RenderTexture.active;var mode=target.renderMode;var prior=target.worldCamera;var distance=target.planeDistance;var scaler=target.GetComponent<CanvasScaler>();
 try{camera.targetTexture=rt;target.renderMode=RenderMode.ScreenSpaceCamera;target.worldCamera=camera;target.planeDistance=10;Canvas.ForceUpdateCanvases();scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();System.IO.File.WriteAllBytes(output+"/"+name+"-"+w+"x"+h+".png",ImageConversion.EncodeToPNG(tex));}
 finally{target.renderMode=mode;target.worldCamera=prior;target.planeDistance=distance;camera.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);Canvas.ForceUpdateCanvases();}
};
Action refresh=()=>{var game=(GameManager)field(boot,"_game");game.ClearCombatVisuals();board.StopAnimations();boot.GetType().GetField("_pauseUntil",flags).SetValue(boot,0f);invoke(boot,"RefreshView",new object[0]);Canvas.ForceUpdateCanvases();};
Func<CampaignLesson,bool> clickGuide=l=>{
 var b=l.Board;
 if(!b.IsHumanChooser)return b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
 if(b.PendingEffect==HandEffect.BattleMasterManeuver){foreach(var status in new[]{StatusEffect.Bleed,StatusEffect.Limp,StatusEffect.Stunned,StatusEffect.KnockedDown,StatusEffect.None})if(l.AllowManeuver(status))return status==StatusEffect.None?b.DeclineBattleMasterManeuver():b.ChooseBattleMasterManeuver(status);}
 if(b.PendingSkillOptions.Count>0){foreach(var option in b.PendingSkillOptions)if(l.AllowsOption(option.Value))return b.ChooseSkillOption(option.Value);return false;}
 if(b.PendingEffect==HandEffect.SchoolOfEvocationDamageType){foreach(DamageType type in Enum.GetValues(typeof(DamageType)))if(l.AllowsDamageType(type)&&b.ChooseDamageType(type))return true;return false;}
 if(b.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType){foreach(var type in ResistanceNames.Choices)if(l.AllowsResistance(type)&&b.ChooseResistanceType(type))return true;return false;}
 if(b.PendingWizardChoice!=null){foreach(var hand in b.PendingWizardChoice.Candidates)if(l.AllowsHand(hand)&&b.ChooseWizardHand(hand))return true;return l.AllowsDecline&&b.DeclinePendingChoice();}
 if(b.PendingRequest.HasValue){foreach(var hand in b.PendingRequest.Value.Candidates)if(l.AllowsHand(hand)&&b.ClickHand(hand))return true;if(l.AllowsAccept&&b.AcceptPendingChoice())return true;return l.AllowsDecline&&b.DeclinePendingChoice();}
 if(b.CurrentCampaignAction!=null)return b.UseCampaignAction();
 if(b.CanCastle&&l.AllowsCastling&&b.Phase==BoardPhase.ChoosingAttackerHand)return b.BeginCastling();
 if(b.CanGiveUpAttack&&l.AllowsDecline)return b.GiveUpAttack();
 if(b.CanSkipTurn&&l.AllowsSkip)return b.SkipTurn();
 if(l.SuggestedHand!=null&&b.ClickHand(l.SuggestedHand))return true;
 foreach(var person in l.Seats)foreach(var hand in person.Hands)if(l.AllowsHand(hand)&&b.ClickHand(hand))return true;
 if(l.AllowsAccept&&b.AcceptPendingChoice())return true;return l.AllowsDecline&&b.DeclinePendingChoice();
};
try
{
 boot.enabled=false;boot.AiThinkSeconds=1000;
 foreach(var pair in saved)PlayerPrefs.DeleteKey(pair.Key);
 PlayerPrefs.SetInt("FingerDiceDuel.Adventure.Level11.Completed",1);PlayerPrefs.SetInt("FingerDiceDuel.Adventure.Level20.Completed",1);
 invoke(boot,"MigrateCampaignProgress",new object[0]);
 check(PlayerPrefs.GetInt(CampaignChapter.LevelKey(21))==1&&PlayerPrefs.GetInt(CampaignChapter.LevelKey(11))==0,"legacy tower maps to tower, never gallery");
 check(PlayerPrefs.GetInt(CampaignChapter.Tower.CompletionKey)==1&&PlayerPrefs.GetInt(CampaignChapter.LevelKey(29))==0,"legacy finale retained, new bladesinger remains unpassed");
 foreach(var chapter in CampaignChapter.All)PlayerPrefs.SetInt(chapter.CompletionKey,1);
 boot.ShowScreen(GameScreen.StoryChapterSelect);
 var root=(RectTransform)field(boot,"_selectRoot");
 for(int i=1;i<=4;i++)check(root.Find("StoryChapter0"+i).gameObject.activeInHierarchy,"chapter button "+i);
 shot(front,"four-chapters",1920,1080);shot(front,"four-chapters",1280,720);
 foreach(int number in new[]{1,11,22,23,29})
 {
  invoke(boot,"StartCampaignLevel",new object[]{number,true});var lesson=(CampaignLesson)field(boot,"_adventure");
  board.HideAdventureGuide();invoke(boot,"OnAdventureIntroConfirmed",new object[0]);refresh();
  check(lesson.Version3&&lesson.LevelNumber==number,"actual UI starts v3 lesson "+number);
  foreach(var person in lesson.Seats)check(BattleArt.Get(person.BattleEmblemKey)!=null,"battle and skill emblem "+person.Name);
  if(number==23||number==29)check(lesson.Seats[1].Name=="奥法护卫"&&lesson.Seats[1].BattleEmblemKey=="Adventure/npc_arcane_guard","new tower guard identity "+number);
  if(number==11||number==1){shot(canvas,number==11?"gallery-choice":"heavy-fighter",1920,1080);shot(canvas,number==11?"gallery-choice":"heavy-fighter",1280,720);}
  bool captured=false;
  for(int ticks=0;ticks<1600&&!lesson.TrialConfirmationPending;ticks++)
  {
   check(clickGuide(lesson),"advance lesson "+number+" step "+lesson.Cursor);lesson.Tick();
   if(!captured&&number==22&&lesson.Board.Phase==BoardPhase.ChoosingSeatSwapTarget){refresh();shot(canvas,"castle-targets",1920,1080);captured=true;}
   if(!captured&&number==23&&lesson.Board.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType){refresh();shot(canvas,"ward-choice",1920,1080);captured=true;}
  }
  check(lesson.TrialConfirmationPending,"trial awaits confirmation "+number);refresh();
  check(board.AdventureModalVisible,"central trial modal "+number);
  if(number==29)shot(canvas,"trial-confirmation",1920,1080);
  var panel=field(board,"_adventurePanel");var confirm=(Button)field(panel,"_confirmButton");
  confirm.onClick.Invoke();refresh();
  check(lesson.TeachingComplete&&lesson.CanRetryTrial&&lesson.HornCount==1,"confirmed trial snapshot "+number);
  var retry=lesson.RetryTrial();check(retry.Player.Health==lesson.Player.Health&&!object.ReferenceEquals(retry.Player,lesson.Player),"Mono snapshot "+number);
  if(number==29)
  {
   lesson.Player.TakeDamage(1000);lesson.Game.RefreshGameOver();refresh();
   check(lesson.IsFinished&&!lesson.IsComplete,"real loss fixture");
   var end=(Button)field(board,"_trialEndButton");check(end.gameObject.activeInHierarchy,"loss stays on battlefield until trial-end button");end.onClick.Invoke();
   var retryButton=(Button)field(panel,"_skipTutorialButton");
   check(((Button)field(panel,"_confirmButton")).GetComponentInChildren<Text>().text=="重新指引"&&retryButton.gameObject.activeInHierarchy&&retryButton.GetComponentInChildren<Text>().text=="再次试炼","loss exposes both retry paths");
   shot(canvas,"failure-choices",1920,1080);retryButton.onClick.Invoke();
   var attached=(CampaignLesson)field(boot,"_adventure");check(attached.TeachingComplete&&attached.HornCount==1&&attached.Player.IsAlive,"UI direct retry attaches snapshot");
  }
 }
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(GameScreen.MainMenu);
 System.IO.File.WriteAllLines(output+"/validation.txt",checks);return "PASS "+checks.Count+" native campaign checks, screenshots saved";
}
finally
{
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(GameScreen.MainMenu);boot.enabled=wasEnabled;boot.AiThinkSeconds=think;
 foreach(var pair in saved){if(pair.Value.HasValue)PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(pair.Key);}PlayerPrefs.Save();
}
