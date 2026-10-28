using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/normalized-skills-hunter-wild");System.IO.Directory.CreateDirectory(output);
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
 invoke(boot,"OnCodexClicked",new object[0]);
 var codex=(FingerDiceDuel.UI.CodexScreen)field(boot,"_codex");
 var cards=(System.Collections.Generic.List<FingerDiceDuel.UI.CodexCardData>)field(codex,"_filtered");
 var body=(UnityEngine.UI.Text)field(codex,"_body");
 var ids=new[]{"fighter_arcane_archer","fighter_battle_master","fighter_eldritch_knight","ranger_hunter","cleric_life_domain","paladin_oath_of_the_ancients","wizard_school_of_abjuration","sorcerer_wild_magic"};
 var expected=new[]{"奥术射击","卓越战技","奥法武器","灭族者","维持生命","不灭哨卫","奥术守御","狂野魔法浪涌"};
 for(int i=0;i<ids.Length;i++){
  int index=cards.FindIndex(c=>c.Id==ids[i]);check(index>=0,"codex role exists "+ids[i]);codex.GoTo(index);UnityEngine.Canvas.ForceUpdateCanvases();
  string text=body.text.Replace("\n","");check(text.Contains(expected[i])&&text.Contains("｜职业：")&&!text.Contains("被动技能（"),"actual codex uses Chinese canonical skill and bare passive label "+ids[i]);
  check(body.rectTransform.sizeDelta.y>=body.preferredHeight,"codex scroll content includes complete copy "+ids[i]);
  if(i==0||i==3||i==6)shot(front,"codex-"+ids[i]);
 }
 int monks=cards.Count(c=>c.ProfessionId=="monk"&&!c.UsesNormalizedSkills);check(monks==4,"all four monk entries keep original format");
 boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);boot.StartMatch(CharacterDefinition.RangerHunter,CharacterDefinition.Blank);
 var ps=new System.Collections.Generic.List<PlayerState>{CharacterFactory.CreatePlayer("我",CharacterDefinition.RangerHunter),CharacterFactory.CreatePlayer("正面护卫",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("标记护卫",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("队友",CharacterDefinition.Blank)};
 var choices=new[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()};
 var g=GameManager.Create2v2(ps,choices);var controller=new BoardController(g,choices[0],choices[1]);
 set(boot,"_game",g);set(boot,"_controller",controller);set(boot,"_isTwoOnTwo",true);set(boot,"_twoOnTwoSeated",ps);set(boot,"_twoOnTwoMeIndex",0);set(boot,"_twoOnTwoMySeat",1);
 board.BindPlayersRing(ps[0],ps,1);board.SetVisible(true);ps[0].LeftHand.SetValue(1);ps[1].LeftHand.SetValue(3);
 ((FingerDiceDuel.Characters.SpecialLogics.RangerHunterLogic)ps[0].Character).Mark(ps[2]);controller.BeginTurn();controller.ClickHand(ps[0].LeftHand);controller.ClickHand(ps[1].LeftHand);
 if(controller.Phase==BoardPhase.ChoosingTargetPlayer)controller.ClickHand(ps[1].LeftHand);refresh();
 check(controller.PendingWizardChoice?.TargetsPlayer==true&&controller.Hint.Contains("灭族者"),"real 2v2 pauses for optional hunter extra target");
 var buttons=(UnityEngine.UI.Button[])field(board,"_skillChoiceButtons");var targetButton=buttons.First(b=>b!=null&&b.gameObject.activeInHierarchy);
 check(targetButton.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("标记护卫"),"target button shows player name without hand suffix");
 var decline=(UnityEngine.UI.Button)field(board,"_declineButton");check(decline.gameObject.activeInHierarchy&&decline.interactable,"hunter decline button is visible and enabled");
 shot(canvas,"hunter-extra-target-2v2");click(targetButton,canvas);refresh();
 check(ps[2].Health==16&&g.TurnCount==1,"clicking actual hunter target button resolves one extra attack without an extra turn");shot(canvas,"hunter-extra-result-2v2");
 invoke(boot,"CleanupMatch",new object[0]);set(boot,"_isTwoOnTwo",false);set(boot,"_twoOnTwoSeated",null);
 boot.StartMatch(CharacterDefinition.WildMagic,CharacterDefinition.Blank);g=(GameManager)field(boot,"_game");g.PlayerB.SetMaxHealth(100);g.PlayerB.SetHealthExact(100);
 int selected=-1;for(int seed=0;seed<10000;seed++){var random=new System.Random(seed);int a=random.Next(100),b=random.Next(100);if(a>=40&&a<48&&b>=56&&b<64){selected=seed;break;}}
 check(selected>=0,"deterministic fire then lightning sample found");((FingerDiceDuel.Characters.SpecialLogics.WildMagicLogic)g.PlayerA.Character).Rng=new System.Random(selected);
 var hit=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){HitCount=2,IgnoreStatBonuses=true};hit.Resolve(g.GetChoiceProvider(g.PlayerB),g);refresh();
 check(hit.SegmentTypes.SequenceEqual(new[]{AttackDamageType.Fire,AttackDamageType.Lightning})&&g.PlayerB.HasStatus(StatusEffect.Burn)&&g.PlayerB.HasStatus(StatusEffect.Shock),"independent mixed types produce both actual battle status icons");shot(canvas,"wild-mixed-statuses");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" normalized skills / hunter / wild UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;}
