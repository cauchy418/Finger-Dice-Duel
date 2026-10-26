using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/tutorial-choices-and-divination");System.IO.Directory.CreateDirectory(output);
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


var keys=Enumerable.Range(1,10).Select(n=>"FingerDiceDuel.Adventure.Level"+n+".Completed").Concat(new[]{AdventureChapter.CompletionKey}).ToArray();
var saved=keys.ToDictionary(k=>k,k=>new[]{UnityEngine.PlayerPrefs.HasKey(k)?1:0,UnityEngine.PlayerPrefs.GetInt(k,0)});float delay=boot.AiThinkSeconds;
try{
 boot.AiThinkSeconds=1000;foreach(var key in keys)UnityEngine.PlayerPrefs.DeleteKey(key);
 foreach(int level in new[]{3,7,8}){
  invoke(boot,"StartAdventureLevel",new object[]{level,true});var lesson=(AdventureLesson)field(boot,"_adventure");var b=lesson.Board;
  var panel=field(board,"_adventurePanel");click((UnityEngine.UI.Button)field(panel,"_confirmButton"),canvas);
  for(int step=0;step<140;step++){
   refresh();if(lesson.IsFinished||lesson.IsFreeStage)break;
   bool ok=false;
   if(b.IsHumanChooser){
    if(b.PendingEffect==HandEffect.BattleMasterManeuver){
     var buttons=(UnityEngine.UI.Button[])field(board,"_wizardButtons");var decline=(UnityEngine.UI.Button)field(board,"_wizardDecline");
     var statuses=b.BattleMasterManeuverOptions;int enabled=0;
     for(int k=0;k<statuses.Length;k++){
      bool allowed=lesson.AllowManeuver(statuses[k]);check(buttons[k].gameObject.activeInHierarchy,"chapter1 maneuver visible "+k);
      check(buttons[k].interactable==allowed,"chapter1 maneuver restricted "+k);check((buttons[k].GetComponent<UnityEngine.UI.Outline>()?.enabled==true)==allowed,"chapter1 maneuver highlight "+k);
      if(allowed)enabled++;
     }
     bool no=lesson.AllowManeuver(StatusEffect.None);check(decline.gameObject.activeInHierarchy,"chapter1 no-effect choice remains visible");
     check(decline.interactable==no,"chapter1 no-effect restricted");check((decline.GetComponent<UnityEngine.UI.Outline>()?.enabled==true)==no,"chapter1 no-effect highlight");if(no)enabled++;
     check(enabled==1,"chapter1 one maneuver choice enabled");shot(canvas,no?"chapter1-maneuver-decline":"chapter1-maneuver-"+lesson.CurrentBeat.Maneuver.Value);
     if(no)click(decline,canvas);else click(buttons[System.Array.IndexOf(statuses,lesson.CurrentBeat.Maneuver.Value)],canvas);ok=true;
    }else if(lesson.CurrentBeat.Choices.TryGetValue(b.PendingEffect,out var required)){
     if(b.PendingEffect==HandEffect.SwordsmanFollowUp&&b.CanDeclinePendingChoice){
      var accept=(UnityEngine.UI.Button)field(board,"_acceptButton");var decline=(UnityEngine.UI.Button)field(board,"_declineButton");
      check(accept.gameObject.activeInHierarchy&&decline.gameObject.activeInHierarchy,"chapter1 both yes/no choices visible "+level+"/"+b.PendingEffect);
      check(accept.interactable==lesson.AllowAcceptChoice&&decline.interactable==lesson.AllowDeclineChoice,"chapter1 yes/no restriction "+level);
      check((accept.GetComponent<UnityEngine.UI.Outline>()?.enabled==true)==lesson.AllowAcceptChoice,"chapter1 yes highlight "+level);
      check((decline.GetComponent<UnityEngine.UI.Outline>()?.enabled==true)==lesson.AllowDeclineChoice,"chapter1 no highlight "+level);
      shot(canvas,"chapter1-choices-"+level+"-"+b.PendingEffect);
     }
     ok=required==null?b.DeclinePendingChoice():b.ClickHand(required);
    }else if(b.Phase==BoardPhase.ChoosingTargetPlayer)ok=b.CanGiveUpAttack?b.GiveUpAttack():b.ConfirmTargetPlayer();
    else if(b.Phase==BoardPhase.ChoosingDefenseShield)ok=b.ClickHand(lesson.Player.LeftHand.IsShield?lesson.Player.LeftHand:lesson.Player.RightHand);
    else if(lesson.SuggestedHand!=null)ok=b.ClickHand(lesson.SuggestedHand);
   }else ok=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
   check(ok,"chapter1 progress "+level+"/"+lesson.Game.TurnCount+"/"+b.Phase);
  }
  refresh();check(lesson.IsComplete||lesson.IsFreeStage,"chapter1 guided section completes "+level);check(!board.AdventureModalVisible,"chapter1 remains on battlefield "+level);
 }
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"chapter1-validation.txt"),checks);return "PASS "+checks.Count+" first chapter choice checks";
}finally{foreach(var pair in saved){if(pair.Value[0]==1)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value[1]);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}UnityEngine.PlayerPrefs.Save();boot.AiThinkSeconds=delay;}
