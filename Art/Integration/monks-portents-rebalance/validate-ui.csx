using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/monks-portents-rebalance");System.IO.Directory.CreateDirectory(output);
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
 foreach(var key in new[]{"Effects/portent","Effects/drunken_fist","Effects/hidden_force","Subclasses/monk_way_of_the_drunken_master"}){
  var image=UnityEngine.Resources.Load<UnityEngine.Texture2D>("Art/Battle/v1/"+key);check(image!=null&&image.width==512&&image.height==512,"runtime texture "+key);
 }
 invoke(boot,"OnCodexClicked",new object[0]);
 var codex=(FingerDiceDuel.UI.CodexScreen)field(boot,"_codex");
 var cards=(System.Collections.Generic.List<FingerDiceDuel.UI.CodexCardData>)field(codex,"_filtered");var body=(UnityEngine.UI.Text)field(codex,"_body");
 check(cards.Count==55,"55 selectable subclass codex entries");
 foreach(var id in new[]{"monk_way_of_the_drunken_master","monk_way_of_the_ninja","monk_way_of_the_open_hand","monk_way_of_the_tai_chi","wizard_school_of_divination","wizard_school_of_evocation","wizard_school_of_abjuration"}){
  codex.GoTo(cards.FindIndex(c=>c.Id==id));UnityEngine.Canvas.ForceUpdateCanvases();check(body.text.Contains("｜职业："),"canonical profession text "+id);check(body.rectTransform.sizeDelta.y>=body.preferredHeight,"full scroll height "+id);shot(front,"codex-"+id);
 }
 boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);boot.StartMatch(CharacterDefinition.SchoolOfDivination,CharacterDefinition.Blank);
 var g=(GameManager)field(boot,"_game");var controller=(BoardController)field(boot,"_controller");
 var omen=(FingerDiceDuel.Characters.SpecialLogics.SchoolOfDivinationLogic)g.PlayerA.Character;omen.RecordPortent(5);g.PlayerA.LeftHand.SetValue(7);g.PlayerA.RightHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);
 controller.SetFirstPlayer(g.PlayerB);controller.BeginTurn();controller.ClickHand(g.PlayerB.LeftHand);controller.ClickHand(g.PlayerA.LeftHand);refresh();
 check(controller.PendingWizardChoice!=null&&controller.Hint.Contains("预知未来"),"human can interrupt opponent result before targeting");check(controller.PreviewHandValue(g.PlayerB.LeftHand)==8,"pre-replacement result 8 is displayed");
 var buttons=(UnityEngine.UI.Button[])field(board,"_wizardButtons");var replace=buttons.First(b=>b!=null&&b.gameObject.activeInHierarchy&&b.GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("替换为5"));shot(canvas,"portent-choice");click(replace,canvas);refresh();
 check(g.PlayerB.LeftHand.Value==5&&g.PlayerB.LeftHand.IsShield&&g.PlayerA.Health==20,"actual button replaces 8 with shield 5 before damage");check(omen.Portents.Count==0,"only selected record is consumed");shot(canvas,"portent-shield-result");
 invoke(boot,"CleanupMatch",new object[0]);boot.StartMatch(CharacterDefinition.WayOfTheDrunkenMaster,CharacterDefinition.Blank);g=(GameManager)field(boot,"_game");((FingerDiceDuel.Characters.SpecialLogics.WayOfTheDrunkenMasterLogic)g.PlayerA.Character).TryEnter(g.PlayerA);g.PlayerB.SetHiddenForce(g.PlayerA);refresh();shot(canvas,"drunken-and-hidden-force");
 check(CombatStatusIcons.For(g.PlayerA,g).Any(i=>i.ResourceKey=="Effects/drunken_fist"),"drunken role state uses new icon");check(CombatStatusIcons.For(g.PlayerB,g).Any(i=>i.ResourceKey=="Effects/hidden_force"),"hidden force target marker has source");
 // Export actual runtime data for documentation, without hand-copying arrays or alignments.
 var rows=new System.Collections.Generic.List<string>{"id\tname\tenglish\tprofession\talignment\tattack\tdefense\trange\tweights\tdeny\t1v1\t2v2\tguide\tcounter\tcombos"};
 foreach(var d in CharacterDefinition.BuiltIn){
  var p=CharacterFactory.CreatePlayer(d.DisplayName,d);var c=FingerDiceDuel.Characters.CharacterCodex.For(d);var h=HandValueProfiles.For(d);var opponent=HandValueProfiles.For(CharacterDefinition.Blank,d);var plain=HandValueProfiles.For(CharacterDefinition.Blank);
  var weights=new System.Collections.Generic.List<int>();var deny=new System.Collections.Generic.List<int>();for(int n=0;n<=9;n++){weights.Add(h.WeightOf(n));deny.Add(opponent.WeightOf(n)-plain.WeightOf(n));}
  var q=CharacterFactory.CreatePlayer("对手",CharacterDefinition.Blank);var gm=new GameManager(p,q,new SimpleChoiceProvider(),new SimpleChoiceProvider());var ai=new GreedyChoiceProvider();ValidatedAiTuning.Apply(ai,gm,p);string one=(ai.UseProjectedCounterplay?"P":"L")+"/"+ai.HandValueWeight+"/"+ai.DenyRatioPercent;ValidatedAiTuning.Apply(ai,null,p);string two="L/"+ai.HandValueWeight+"/"+ai.DenyRatioPercent;
  rows.Add(string.Join("\t",d.Id,d.DisplayName,d.SubclassNameEn,d.Profession.Id,c.Alignment,p.Character.AttackBonus,p.Character.DefenseBonus,GameManager.BaseRangeOf(p),string.Join(",",weights),string.Join(",",deny),one,two,c.PlayGuide.Replace("\n"," "),c.Counter.Replace("\n"," "),string.Join("；",h.Combos.Select(x=>x.Reason+"+"+x.Bonus))));
 }
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"runtime-roles.tsv"),rows);check(rows.Count==56,"export all 55 runtime role profiles");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" revision UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;}
