using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/cleric-domains-v1/review-final3");System.IO.Directory.CreateDirectory(output);
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
 foreach(var key in new[]{"Subclasses/cleric_trickery_domain","Subclasses/cleric_tempest_domain","Status/tinnitus"})
  check(FingerDiceDuel.UI.BattleArt.Get(key)!=null,"painted resource "+key);
 boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.TrickeryDomain);
 var g=(GameManager)field(boot,"_game");var ctl=(BoardController)field(boot,"_controller");var a=g.PlayerA;var b=g.PlayerB;
 a.LeftHand.SetValue(2);b.LeftHand.SetValue(4);b.RightHand.SetValue(3);ctl.BeginTurn();refresh();
 var own=(System.Array)field(board,"_cardsA");var foes=(System.Array)field(board,"_cardsB");
 click((UnityEngine.UI.Button)field(own.GetValue(0),"Button"),canvas);refresh();click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();
 check(ctl.Chooser==b&&ctl.Hint.Contains("诡诈换位"),"human addend owner chooses swap");
 shot(canvas,"trickery-swap");var buttons=(UnityEngine.UI.Button[])field(board,"_wizardButtons");click(buttons[0],canvas);refresh();
 check(ctl.Hint.Contains("魅惑")&&a.LeftHand.Value==2,"charm asked separately before operation");
 click(((UnityEngine.UI.Button[])field(board,"_wizardButtons"))[1],canvas);refresh();
 if(ctl.Phase==BoardPhase.ChoosingTargetPlayer){click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();}
 check(a.LeftHand.Value==4&&b.LeftHand.Value==6&&g.TurnCount==1,"swap numeric effect resolved once");
 boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.TempestDomain);
 g=(GameManager)field(boot,"_game");ctl=(BoardController)field(boot,"_controller");a=g.PlayerA;b=g.PlayerB;
 a.LeftHand.SetValue(1);a.RightHand.SetValue(3);b.LeftHand.SetValue(4);b.RightHand.SetValue(3);ctl.BeginTurn();refresh();
 own=(System.Array)field(board,"_cardsA");foes=(System.Array)field(board,"_cardsB");
 click((UnityEngine.UI.Button)field(own.GetValue(0),"Button"),canvas);refresh();click((UnityEngine.UI.Button)field(foes.GetValue(1),"Button"),canvas);refresh();
 if(ctl.Phase==BoardPhase.ChoosingTargetPlayer){click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();}
 check(ctl.Chooser==b&&ctl.Hint.Contains("风暴狂怒")&&b.Health==20&&g.TurnCount==0,"reaction pauses incoming damage");
 shot(canvas,"tempest-hand");click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();
 buttons=(UnityEngine.UI.Button[])field(board,"_wizardButtons");check(buttons[0].gameObject.activeInHierarchy&&buttons[1].gameObject.activeInHierarchy,"lightning and thunder visible");shot(canvas,"tempest-element");
 click(buttons[0],canvas);refresh();
 check(a.Health<20&&b.Health==20&&ctl.PendingWizardChoice!=null&&!ctl.PendingWizardChoice.AllowDecline,"counter resolves before mandatory same-hand addition");
 shot(canvas,"tempest-followup");click((UnityEngine.UI.Button)field(own.GetValue(1),"Button"),canvas);refresh();
 check(b.LeftHand.Value==7&&b.Health<20&&g.TurnCount==1&&!g.PendingClericResolution&&ctl.LastTurn!=null,"incoming attack resumes once after pure operation");
 b.ApplyStatus(StatusEffect.Tinnitus,2,a);refresh();shot(canvas,"tinnitus-battle");
 check(b.HasStatus(StatusEffect.Tinnitus)&&canvas.GetComponentsInChildren<UnityEngine.UI.Image>().Any(i=>i.sprite!=null&&i.sprite.name.Contains("tinnitus")),"ear marker present in battle state");
 invoke(boot,"OnMagicOrbClicked",new object[0]);refresh();
 var codex=(FingerDiceDuel.UI.CodexScreen)field(boot,"_codex");codex.GoTo(1);refresh();shot(front,"manual-statuses");
 var guide=(UnityEngine.RectTransform)field(codex,"_guideContent");
 check(guide.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("耳鸣")),"manual includes tinnitus");
 ((UnityEngine.UI.ScrollRect)field(codex,"_guideScroll")).verticalNormalizedPosition=0;UnityEngine.Canvas.ForceUpdateCanvases();shot(front,"manual-statuses-bottom");
 codex.GoTo(2);refresh();shot(front,"manual-reactions");
 check(guide.GetComponentsInChildren<UnityEngine.UI.Text>().Count(t=>t.name.StartsWith("Entry_")&&t.text.Contains("耳鸣"))==2,"both sonic reactions in manual");
 ((UnityEngine.UI.ScrollRect)field(codex,"_guideScroll")).verticalNormalizedPosition=0;UnityEngine.Canvas.ForceUpdateCanvases();shot(front,"manual-reactions-bottom");
 invoke(boot,"OnCodexClicked",new object[0]);codex.ToggleProfession("cleric");codex.GoTo(3);refresh();shot(front,"codex-trickery");
 check(front.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("Trickery Domain")),"trickery detail is present");
 codex.GoTo(4);refresh();shot(front,"codex-tempest");check(front.GetComponentsInChildren<UnityEngine.UI.Text>().Any(t=>t.text.Contains("Tempest Domain")),"tempest detail is present");
 var screens=new[]{FingerDiceDuel.UI.GameScreen.CharacterSelect,FingerDiceDuel.UI.GameScreen.TwoOnTwoCharacter,FingerDiceDuel.UI.GameScreen.FactionSelect,FingerDiceDuel.UI.GameScreen.IdentitySelect};
 var rosterFields=new[]{"_rosterViewA","_rosterView2Me","_fbRoster","_idRoster"};
 for(int mode=0;mode<screens.Length;mode++){
  boot.ShowScreen(screens[mode]);var roster=(FingerDiceDuel.UI.CharacterRosterView)field(boot,rosterFields[mode]);check(roster.SelectProfession("cleric"),"cleric profession in "+screens[mode]);UnityEngine.Canvas.ForceUpdateCanvases();
  var defs=(System.Collections.Generic.List<CharacterDefinition>)field(roster,"_roster");
  foreach(var def in new[]{CharacterDefinition.TrickeryDomain,CharacterDefinition.TempestDomain}){
   int visible=-1;for(int j=0;j<roster.VisibleIndices.Count;j++)if(defs[roster.VisibleIndices[j]].Id==def.Id)visible=j;
   check(visible>=0,"selectable "+def.Id+" in "+screens[mode]);
  }
  if(mode==0)shot(front,"cleric-selection");
 }
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" cleric UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;}
