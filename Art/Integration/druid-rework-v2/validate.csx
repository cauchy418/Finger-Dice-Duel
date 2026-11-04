using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.UI;
var boot=UnityEngine.Object.FindObjectOfType<GameBootstrap>();if(boot==null)throw new Exception("No bootstrap");
var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var board=(GameBoardUI)field(boot,"_board");var canvas=((RectTransform)field(board,"_canvasRoot")).GetComponent<Canvas>();
var front=((RectTransform)field(boot,"_selectRoot")).GetComponent<Canvas>();
string output=System.IO.Path.GetFullPath("Art/Integration/druid-rework-v2");System.IO.Directory.CreateDirectory(output);
var checks=new List<string>();Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception(label);checks.Add("PASS "+label);};
bool enabled=boot.enabled;float delay=boot.AiThinkSeconds;
Action refresh=()=>{((GameManager)field(boot,"_game")).ClearCombatVisuals();board.StopAnimations();invoke(boot,"RefreshView",new object[0]);Canvas.ForceUpdateCanvases();};
Action<Canvas,string,int,int> shot=(target,name,w,h)=>{
 var go=new GameObject("DruidPreview",typeof(Camera));var camera=go.GetComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
 var rt=new RenderTexture(w,h,24);var tex=new Texture2D(w,h,TextureFormat.RGB24,false);var prior=RenderTexture.active;var mode=target.renderMode;var cam=target.worldCamera;var dist=target.planeDistance;var scaler=target.GetComponent<CanvasScaler>();
 try{camera.targetTexture=rt;target.renderMode=RenderMode.ScreenSpaceCamera;target.worldCamera=camera;target.planeDistance=10;Canvas.ForceUpdateCanvases();scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();System.IO.File.WriteAllBytes(output+"/"+name+"-"+w+"x"+h+".png",ImageConversion.EncodeToPNG(tex));}
 finally{target.renderMode=mode;target.worldCamera=cam;target.planeDistance=dist;camera.targetTexture=null;RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);}
};
try
{
 boot.enabled=false;boot.AiThinkSeconds=999;
 foreach(var key in new[]{"moon_bear","moon_wolf","moon_eagle","moon_snake","moon_owlbear","moon_panther"})check(BattleArt.Get("Forms/"+key)!=null,"round shape sprite "+key);
 check(BattleArt.Get("Subclasses/druid_circle_of_spores")!=null&&BattleArt.Get("Effects/spore_life")!=null,"spores artwork");
 boot.StartMatch(CharacterDefinition.CircleOfTheMoon,CharacterDefinition.Blank);var g=(GameManager)field(boot,"_game");var b=(BoardController)field(boot,"_controller");g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);b.BeginTurn();refresh();
 check(b.PendingSkillOptions.Count==7,"six moon forms and decline");shot(canvas,"moon-choice",1920,1080);shot(canvas,"moon-choice",1280,720);
 check(b.ChooseSkillOption(5),"choose owlbear");check(b.ChooseSkillOption(0),"retain form after paid turn");refresh();
 check(g.PlayerA.Health==16&&g.PlayerA.WildCharges==1&&g.TurnCount==1,"owlbear pool and charge cost");
 check(((Image)field(board,"_badgeA")).sprite.name=="Forms/moon_owlbear","emblem replaced");shot(canvas,"owlbear-panel",1920,1080);
 boot.StartMatch(CharacterDefinition.CircleOfTheStars,CharacterDefinition.Blank);g=(GameManager)field(boot,"_game");b=(BoardController)field(boot,"_controller");g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);b.BeginTurn();b.ChooseSkillOption(2);refresh();
 check(b.PendingSkillOptions.Count==4,"second stage star choice");shot(canvas,"star-choice",1920,1080);b.ChooseSkillOption(3);b.ChooseSkillOption(0);
 g.ForceActivePlayerForTest(g.PlayerA);g.PlayerA.LeftHand.SetValue(1);g.PlayerA.RightHand.SetValue(2);g.PlayerB.LeftHand.SetValue(1);b.BeginTurn();refresh();
 check(b.PendingWizardChoice!=null&&b.PendingWizardChoice.AllowDecline&&b.Hint.Contains("巨龙座"),"dragon optional start");b.ChooseWizardHand(null);b.ClickHand(g.PlayerA.LeftHand);b.ClickHand(g.PlayerB.LeftHand);refresh();
 check(b.PendingWizardChoice!=null&&!b.PendingWizardChoice.AllowDecline,"dragon mandatory end");shot(canvas,"dragon-end-operation",1920,1080);b.ChooseWizardHand(g.PlayerA.LeftHand);b.ChooseWizardHand(g.PlayerB.LeftHand);b.ChooseSkillOption(0);check(g.TurnCount==2,"pure operation adds no turn");
 boot.StartMatch(CharacterDefinition.CircleOfSpores,CharacterDefinition.Blank);g=(GameManager)field(boot,"_game");b=(BoardController)field(boot,"_controller");refresh();
 check(b.Hint.Contains("一体共生"),"human optional symbiosis prompt");shot(canvas,"spores-choice",1920,1080);b.ChooseSkillOption(1);refresh();
 check(g.PlayerA.SporeLife==10&&g.PlayerA.WildCharges==1,"spore buffer and remaining charge");shot(canvas,"spores-panel",1920,1080);shot(canvas,"spores-panel",1280,720);
 invoke(boot,"CleanupMatch",new object[0]);invoke(boot,"OnCodexClicked",new object[0]);var codex=(CodexScreen)field(boot,"_codex");var cards=(List<CodexCardData>)field(codex,"_filtered");
 for(int i=0;i<cards.Count;i++)if(cards[i].NameZh.Contains("月光结社")||cards[i].NameZh.Contains("孢子结社"))
 {codex.GoTo(i);Canvas.ForceUpdateCanvases();shot(front,cards[i].NameZh.Contains("月光")?"moon-codex":"spores-codex",1920,1080);check(((Image)field(codex,"_emblem")).sprite!=null,"codex emblem "+cards[i].NameZh);}
 check(CharacterCodex.Filter(null,null,"druid").Count==4,"four druid subclasses in filter");
 System.IO.File.WriteAllLines(output+"/validation.txt",checks);return "PASS "+checks.Count+" native checks";
}
finally{invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(GameScreen.MainMenu);boot.enabled=enabled;boot.AiThinkSeconds=delay;}
