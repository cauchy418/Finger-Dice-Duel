using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/illusion-choice");System.IO.Directory.CreateDirectory(output);
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
 boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.SchoolOfIllusion);
 var g=(GameManager)field(boot,"_game");var ctl=(BoardController)field(boot,"_controller");var a=g.PlayerA;var b=g.PlayerB;
 a.LeftHand.SetValue(1);b.LeftHand.SetValue(2);b.RightHand.SetValue(7);ctl.BeginTurn();refresh();
 var own=(System.Array)field(board,"_cardsA");var foes=(System.Array)field(board,"_cardsB");
 click((UnityEngine.UI.Button)field(own.GetValue(0),"Button"),canvas);refresh();
 click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();
 check(ctl.Chooser==b&&ctl.IsHumanChooser,"addend owner controls the response");
 check(ctl.Hint.Contains("左手1")&&ctl.Hint.Contains("左手2")&&ctl.Hint.Contains("右手7"),"intent contains both original hands and replacement");
 check(a.LeftHand.Value==1&&g.TurnCount==0,"operation waits for the response");
 var buttons=(UnityEngine.UI.Button[])field(board,"_wizardButtons");
 check(buttons[0].gameObject.activeInHierarchy&&buttons[1].gameObject.activeInHierarchy&&buttons[0].interactable&&buttons[1].interactable,"both named choices visible and clickable");
 shot(canvas,"choose-addend");
 click(buttons[0],canvas);refresh();check(ctl.Phase==BoardPhase.ChoosingTargetPlayer,"changed attack asks for actual victim");
 click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();
 check(a.LeftHand.Value==8&&b.Health<20&&g.TurnCount==1,"replacement sum drives attack");
 boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.SchoolOfIllusion);
 g=(GameManager)field(boot,"_game");ctl=(BoardController)field(boot,"_controller");a=g.PlayerA;b=g.PlayerB;
 a.LeftHand.SetValue(1);b.LeftHand.SetValue(2);b.RightHand.SetValue(7);ctl.BeginTurn();refresh();
 own=(System.Array)field(board,"_cardsA");foes=(System.Array)field(board,"_cardsB");
 click((UnityEngine.UI.Button)field(own.GetValue(0),"Button"),canvas);refresh();click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);refresh();
 click(((UnityEngine.UI.Button[])field(board,"_wizardButtons"))[1],canvas);refresh();
 check(a.LeftHand.Value==3&&b.Health==20&&g.TurnCount==1,"decline keeps the original addend");
 boot.StartMatch(CharacterDefinition.CircleOfTheStars,CharacterDefinition.SchoolOfIllusion);
 g=(GameManager)field(boot,"_game");ctl=(BoardController)field(boot,"_controller");a=g.PlayerA;b=g.PlayerB;
 a.LeftHand.SetValue(4);a.RightHand.SetValue(6);b.LeftHand.SetValue(2);b.RightHand.SetValue(7);
 check(((FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic)a.Character).TryEnter(FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic.StarryForm.Dragon,a,g),"dragon form entered");
 ctl.BeginTurn();refresh();own=(System.Array)field(board,"_cardsA");foes=(System.Array)field(board,"_cardsB");
 click((UnityEngine.UI.Button)field(own.GetValue(0),"Button"),canvas);refresh();click((UnityEngine.UI.Button)field(foes.GetValue(1),"Button"),canvas);refresh();
 click(((UnityEngine.UI.Button[])field(board,"_wizardButtons"))[0],canvas);refresh();check(ctl.Chooser==a,"dragon chooses its second addend");
 click((UnityEngine.UI.Button)field(foes.GetValue(1),"Button"),canvas);refresh();
 check(ctl.Chooser==b&&ctl.Hint.Contains("左手6"),"second choice uses intermediate source value");
 shot(canvas,"dragon-second-addend");click(((UnityEngine.UI.Button[])field(board,"_wizardButtons"))[1],canvas);refresh();
 check(a.LeftHand.Value==3&&b.Health==20&&g.TurnCount==1,"two distinct answers resolve exactly one operation");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" illusion UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;}
