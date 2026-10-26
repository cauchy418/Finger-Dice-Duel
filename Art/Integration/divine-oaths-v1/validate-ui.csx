using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/divine-oaths-v1");System.IO.Directory.CreateDirectory(output);
var checks=new System.Collections.Generic.List<string>();
System.Action<bool,string> check=(ok,msg)=>{if(!ok)throw new System.Exception(msg);checks.Add("PASS "+msg);};
System.Action<UnityEngine.Canvas,string> shot=(canvas,name)=>{
 var go=new UnityEngine.GameObject("ProfessionReviewCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(1920,1080,24);var pixels=new UnityEngine.Texture2D(1920,1080,UnityEngine.TextureFormat.RGB24,false);var prior=UnityEngine.RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;var distance=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();if(name!=null){UnityEngine.RenderTexture.active=rt;pixels.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);pixels.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(pixels));}}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=distance;cam.targetTexture=null;UnityEngine.RenderTexture.active=prior;UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Canvas.ForceUpdateCanvases();}
};


System.Action<object,string,object> set=(o,n,v)=>o.GetType().GetField(n,flags).SetValue(o,v);
var front=((UnityEngine.RectTransform)field(boot,"_selectRoot")).GetComponentInParent<UnityEngine.Canvas>();
var board=(FingerDiceDuel.UI.GameBoardUI)field(boot,"_board");
var canvas=((UnityEngine.RectTransform)field(board,"_canvasRoot")).GetComponent<UnityEngine.Canvas>();
System.Action refresh=()=>{invoke(boot,"RefreshView",new object[0]);UnityEngine.Canvas.ForceUpdateCanvases();};
System.Action<UnityEngine.UI.Button,UnityEngine.Canvas> click=(button,c)=>{
 shot(c,null);var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=UnityEngine.RectTransformUtility.WorldToScreenPoint(null,button.transform.position)};
 var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(data,hits);
 check(button.gameObject.activeInHierarchy&&button.interactable&&hits.Count>0&&UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(hits[0].gameObject)==button.gameObject,"raycast "+button.name);
 UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,data,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);UnityEngine.Canvas.ForceUpdateCanvases();
};


float delay=boot.AiThinkSeconds;bool aiB=boot.PlayerBIsAI;
try{
 boot.AiThinkSeconds=1000;boot.PlayerBIsAI=false;
 foreach(var key in new[]{"Status/war_blessing","Effects/bladesong","Effects/vow_of_enmity","Subclasses/paladin_oath_of_conquest","Subclasses/paladin_oath_of_vengeance"})
  check(FingerDiceDuel.UI.BattleArt.Get(key)!=null,"resource "+key);
 boot.ShowScreen(FingerDiceDuel.UI.GameScreen.CharacterSelect);UnityEngine.Canvas.ForceUpdateCanvases();
 var roster=(FingerDiceDuel.UI.CharacterRosterView)field(boot,"_rosterViewA");check(roster.Count==52,"52 roles in selection");check(roster.IsChoosingProfession,"class selection is first");check(roster.SelectProfession("paladin"),"choose paladin class");check(roster.VisibleIndices.Count==6,"six paladin subclasses visible");
 var selectionCards=(System.Collections.Generic.List<UnityEngine.UI.Button>)field(roster,"_cards");var selectionDefs=(System.Collections.Generic.List<CharacterDefinition>)field(roster,"_roster");
 int conquestIndex=selectionDefs.FindIndex(def=>def==CharacterDefinition.OathOfConquest);int visibleIndex=roster.VisibleIndices.ToList().IndexOf(conquestIndex);
 click(selectionCards[visibleIndex],front);check(roster.SelectedIndex==conquestIndex,"conquest selection callback");shot(front,"paladin-selection");
 var ro=(System.Collections.Generic.List<CharacterDefinition>)field(roster,"_roster");check(roster.VisibleIndices.Any(i=>ro[i]==CharacterDefinition.OathOfConquest)&&roster.VisibleIndices.Any(i=>ro[i]==CharacterDefinition.OathOfVengeance),"new oaths selectable");
 invoke(boot,"OnCodexClicked",new object[0]);var codex=(FingerDiceDuel.UI.CodexScreen)field(boot,"_codex");var cards=(System.Collections.Generic.List<FingerDiceDuel.UI.CodexCardData>)field(codex,"_all");
 check(cards.Count==52,"codex has 52 roles");foreach(var def in new[]{CharacterDefinition.OathOfConquest,CharacterDefinition.OathOfVengeance,CharacterDefinition.WarDomain}){
  int index=cards.FindIndex(c=>c.Id==def.Id);check(index>=0,"codex entry "+def.Id);codex.GoTo(index);UnityEngine.Canvas.ForceUpdateCanvases();
  check(codex.CurrentName.Contains(def.SubclassNameZh),"correct detail "+def.Id);foreach(var text in ((UnityEngine.RectTransform)field(codex,"_bodyContent")).GetComponentsInChildren<UnityEngine.UI.Text>())check(text.preferredHeight<=text.rectTransform.rect.height+2,"codex body text fits "+def.Id+"/"+text.name);
  shot(front,"codex-"+def.Id);
 }
 invoke(boot,"OnMagicOrbClicked",new object[0]);codex.GoTo(1);UnityEngine.Canvas.ForceUpdateCanvases();var guide=(UnityEngine.RectTransform)field(codex,"_guideContent");
 var headings=guide.GetComponentsInChildren<UnityEngine.UI.Text>().Where(t=>t.name=="GroupTitle").Select(t=>t.text).ToArray();check(headings.Contains("负面状态")&&headings.Contains("角色状态"),"manual separates negative and character states");
 foreach(var id in new[]{"holy_light","bladesong","war_blessing"}){var icon=guide.Find("PaintedIcon_"+id).GetComponent<UnityEngine.UI.Image>();check(icon.sprite!=null,"manual icon "+id);}
 foreach(var text in guide.GetComponentsInChildren<UnityEngine.UI.Text>())check(text.preferredHeight<=text.rectTransform.rect.height+2,"manual text fits "+text.name);
 var scroll=(UnityEngine.UI.ScrollRect)field(codex,"_guideScroll");scroll.verticalNormalizedPosition=0;UnityEngine.Canvas.ForceUpdateCanvases();shot(front,"manual-character-states");
 boot.StartMatch(CharacterDefinition.WarDomain,CharacterDefinition.Champion);var g=(GameManager)field(boot,"_game");var ctl=(BoardController)field(boot,"_controller");var a=g.PlayerA;var b=g.PlayerB;
 a.LeftHand.SetValue(4);a.RightHand.SetValue(6);ctl.BeginTurn();refresh();check(ctl.PendingWizardChoice?.TargetsPlayer==true,"blessing chooses players");
 var buttons=(UnityEngine.UI.Button[])field(board,"_skillChoiceButtons");var hands=(Hand[])field(board,"_skillHands");int self=System.Array.IndexOf(hands,a.LeftHand);check(self>=0,"self is valid blessing target");shot(canvas,"war-blessing-target");click(buttons[self],canvas);refresh();
 check(a.HasStatus(StatusEffect.WarBlessing),"self blessing applied");check(ctl.PendingWizardChoice!=null&&!ctl.PendingWizardChoice.Candidates.Contains(a.LeftHand),"second blessing excludes selected player");click((UnityEngine.UI.Button)field(board,"_declineButton"),canvas);refresh();check(ctl.PendingWizardChoice==null,"can stop after one blessing");
 a.LeftHand.SetValue(8);a.RightHand.SetValue(1);b.LeftHand.SetValue(1);refresh();check(ctl.ClickHand(a.LeftHand)&&ctl.ClickHand(b.LeftHand),"compute blessed nine");refresh();check(ctl.PendingSkillOptions.Count==2,"nine shows attack and healing options");shot(canvas,"blessed-nine-choice");click(((UnityEngine.UI.Button[])field(board,"_wizardButtons"))[0],canvas);refresh();check(ctl.Phase==BoardPhase.ChoosingTargetPlayer,"nine attack still selects target");check(ctl.ClickHand(b.LeftHand),"confirm blessed nine attack");refresh();check(ctl.LastTurn.DamageDealt==9,"war blessing offsets base -1 then nine damage");check(FingerDiceDuel.UI.HandVisual.StatusText(a.LeftHand,a)=="攻／回 9","blessed nine card states attack or heal");
 boot.StartMatch(CharacterDefinition.OathOfConquest,CharacterDefinition.Champion);g=(GameManager)field(boot,"_game");ctl=(BoardController)field(boot,"_controller");a=g.PlayerA;b=g.PlayerB;a.LeftHand.SetValue(5);
 for(int i=0;i<3;i++){var hit=new AttackContext(a,b,a.LeftHand,4,g);hit.Resolve(g.GetChoiceProvider(b),g);}ctl.BeginTurn();refresh();check(StatusRules.EffectiveAttackBonus(a)==3,"conquest stacks shown in stats");check(b.HasStatus(StatusEffect.Panic),"conquest fear shown");shot(canvas,"conquest-growth");
 boot.StartMatch(CharacterDefinition.Bladesinging,CharacterDefinition.Champion);g=(GameManager)field(boot,"_game");a=g.PlayerA;a.LeftHand.SetValue(4);a.RightHand.SetValue(5);a.SetHealthExact(9);var savedDefer=g.DeferWizardChoice;g.DeferWizardChoice=null;
 ((FingerDiceDuel.Characters.SpecialLogics.BladesingingLogic)a.Character).OnTurnEnded(a,g);g.DeferWizardChoice=savedDefer;a.ApplyStatus(StatusEffect.WarBlessing,2);refresh();
 System.Func<string,int> visibleIcons=key=>canvas.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.gameObject.activeInHierarchy&&i.enabled&&i.sprite?.name==key);
 check(visibleIcons("Effects/bladesong")>0&&visibleIcons("Status/war_blessing")>0,"song and blessing use dedicated battle icons");shot(canvas,"song-and-blessing");a.ApplyStatus(StatusEffect.HolyLight,2);refresh();check(visibleIcons("Effects/bladesong")==0&&visibleIcons("Status/war_blessing")==0,"holy light removes both battle icons");check(visibleIcons("Status/holy_light")>0,"holy light remains visible");
 boot.StartMatch(CharacterDefinition.OathOfVengeance,CharacterDefinition.Champion);g=(GameManager)field(boot,"_game");ctl=(BoardController)field(boot,"_controller");a=g.PlayerA;b=g.PlayerB;refresh();check(ctl.PendingWizardChoice!=null&&!ctl.PendingWizardChoice.AllowDecline,"initial vow mandatory human choice");
 buttons=(UnityEngine.UI.Button[])field(board,"_skillChoiceButtons");hands=(Hand[])field(board,"_skillHands");click(buttons[System.Array.IndexOf(hands,b.LeftHand)],canvas);refresh();check(visibleIcons("Effects/vow_of_enmity")>0,"vow icon displayed on enemy");
 a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);a.RightHand.SetValue(1);b.LeftHand.SetValue(3);b.RightHand.SetValue(3);g.ForceActivePlayerForTest(b);ctl.BeginTurn();refresh();
 check(ctl.ClickHand(b.LeftHand)&&ctl.ClickHand(a.RightHand)&&ctl.ClickHand(a.RightHand),"marked enemy breaks shield");refresh();check(ctl.PendingWizardChoice!=null&&!ctl.PendingWizardChoice.TargetsPlayer,"reactive operation chooses source on battlefield");
 check(!((UnityEngine.UI.Button[])field(board,"_skillChoiceButtons")).Any(btn=>btn!=null&&btn.gameObject.activeInHierarchy),"no long hand-option button list");shot(canvas,"vengeance-free-operation");int turn=g.TurnCount;int enemyHp=b.Health;
 var own=(System.Array)field(board,"_cardsA");click((UnityEngine.UI.Button)field(own.GetValue(0),"Button"),canvas);refresh();var foes=(System.Array)field(board,"_cardsB");click((UnityEngine.UI.Button)field(foes.GetValue(1),"Button"),canvas);refresh();check(a.LeftHand.Value==8&&g.TurnCount==turn&&b.Health==enemyHp,"extra operation changes only hand value");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" divine UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;}
