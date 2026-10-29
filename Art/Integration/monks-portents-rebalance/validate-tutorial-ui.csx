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
var keys=new[]{AdventureChapter.CompletionKey,"FingerDiceDuel.Adventure.Level17.Completed",MageTowerChapter.SampleKey(17,"portent_attack")};
var saved=keys.Select(k=>UnityEngine.PlayerPrefs.GetInt(k,-999)).ToArray();
try{
 boot.AiThinkSeconds=1000;UnityEngine.PlayerPrefs.SetInt(AdventureChapter.CompletionKey,1);
 invoke(boot,"StartMageTowerLevel",new object[]{17,false,0});var begin=UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Button>().First(b=>b.gameObject.activeInHierarchy&&b.GetComponentInChildren<UnityEngine.UI.Text>()?.text=="开始试验");click(begin,canvas);
 var l=(MageTowerLesson)field(boot,"_adventure");var c=l.Board;var g=l.Game;
 c.ClickHand(l.Player.LeftHand);c.ClickHand(l.Soldier.RightHand);l.Tick();
 check(g.TurnCount==1&&CharacterBehaviors.FirstImplementer<IPortentBook>(l.Player).Portents.Count==1,"actual tower lesson records portent4");
 check(c.MakeAutomaticMove(),"scripted guard performs its arithmetic");refresh();
 check(c.PendingWizardChoice!=null&&c.PendingWizardChoice.Prompt.Contains("预知未来"),"tower pauses for new divination skill");
 var buttons=(UnityEngine.UI.Button[])field(board,"_wizardButtons");
 check(buttons[0].gameObject.activeInHierarchy&&buttons[0].interactable,"required replacement button is visible and enabled");
 check(buttons[1].gameObject.activeInHierarchy&&!buttons[1].interactable,"decline stays visible but disabled in prescribed step");
 shot(canvas,"tower-portent-guided-choice");click(buttons[0],canvas);for(int n=0;n<12&&!l.IsFinished;n++){l.Tick();if(!l.IsFinished&&!c.IsHumanChooser)c.ResolvePendingAiChoice();}l.Tick();refresh();
 check(l.IsComplete&&l.Player.Health==15&&l.Soldier.LeftHand.Value==4,"new tower sample resolves rewritten damage");
 var end=(UnityEngine.UI.Button)field(board,"_trialEndButton");
 check(end.gameObject.activeInHierarchy&&end.interactable&&!(bool)field(boot,"_adventureVictoryShown"),"final outcome remains on battlefield until trial-end click");
 shot(canvas,"tower-portent-outcome-before-end");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"tutorial-validation.txt"),checks);return "PASS "+checks.Count+" tutorial UI checks";
}finally{boot.AiThinkSeconds=delay;boot.PlayerBIsAI=aiB;for(int i=0;i<keys.Length;i++){if(saved[i]==-999)UnityEngine.PlayerPrefs.DeleteKey(keys[i]);else UnityEngine.PlayerPrefs.SetInt(keys[i],saved[i]);}UnityEngine.PlayerPrefs.Save();}
