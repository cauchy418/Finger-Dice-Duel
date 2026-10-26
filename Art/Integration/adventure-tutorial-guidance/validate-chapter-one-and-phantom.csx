using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/adventure-tutorial-guidance");System.IO.Directory.CreateDirectory(output);
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

        

var keys=new System.Collections.Generic.List<string>{AdventureChapter.CompletionKey,MageTowerChapter.CompletionKey};
for(int lv=1;lv<=20;lv++){keys.Add("FingerDiceDuel.Adventure.Level"+lv+".Completed");if(lv>=11)foreach(var trial in MageTowerChapter.Samples(lv))keys.Add(MageTowerChapter.SampleKey(lv,trial.Key));}
var saved=keys.ToDictionary(k=>k,k=>new[]{UnityEngine.PlayerPrefs.HasKey(k)?1:0,UnityEngine.PlayerPrefs.GetInt(k,0)});
float oldDelay=boot.AiThinkSeconds;
try{
 boot.AiThinkSeconds=1000;foreach(var k in keys)UnityEngine.PlayerPrefs.DeleteKey(k);
 var panel=field(board,"_adventurePanel");
 for(int lv=1;lv<=10;lv++){
  invoke(boot,"StartAdventureLevel",new object[]{lv,true});var lesson=(AdventureLesson)field(boot,"_adventure");
  invoke(panel,"HideModal",new object[0]);lesson.BeginTutorial();
  if(lv==1){
   lesson.Board.BeginTurn();LegalMove chosen=default;
   for(int guard=0;guard<160&&!lesson.IsFinished;guard++){
    var b=lesson.Board;lesson.Tick();if(lesson.IsFinished)break;bool acted=false;
    if(!b.IsHumanChooser)acted=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
    else if(b.PendingRequest.HasValue){
      Hand picked=null;bool required=lesson.CurrentBeat!=null&&lesson.CurrentBeat.Choices.TryGetValue(b.PendingEffect,out picked);
      if(required)acted=picked==null?b.DeclinePendingChoice():b.ClickHand(picked);
      else acted=b.CanDeclinePendingChoice?b.DeclinePendingChoice():b.ClickHand(b.ChoiceCandidates[0]);
    }
    else if(b.Phase==BoardPhase.ChoosingDefenseShield)acted=b.ClickHand(b.DefenseShieldChoices[0]);
    else if(b.Phase==BoardPhase.ChoosingTargetPlayer)acted=b.ConfirmTargetPlayer();
    else if(b.Phase==BoardPhase.ChoosingAttackerHand){
      chosen=lesson.Game.GetLegalMoves(lesson.Player).OrderByDescending(m=>{int v=Hand.ComputeSum(m.ActedHand.Value,m.TargetHand.Value);return v==4||v==6||v==8?1000+v:v;}).First();
      acted=b.ClickHand(lesson.IsFreeStage?chosen.ActedHand:lesson.CurrentBeat.ActedHand);
    }
    else if(b.Phase==BoardPhase.ChoosingTargetHand)acted=b.ClickHand(lesson.IsFreeStage?chosen.TargetHand:lesson.CurrentBeat.AddendHand);
    check(acted,"first tutorial actual progression "+b.Phase);refresh();
   }
   check(lesson.IsComplete&&lesson.Board.Log.Count>0,"actual first tutorial victory");
  }
  else {lesson.Soldier.SetHealthExact(0);lesson.Game.RefreshGameOver();}
  refresh();
  check(!board.AdventureModalVisible,"chapter one retains battlefield "+lv);
  var end=(UnityEngine.UI.Button)field(board,"_trialEndButton");check(end.gameObject.activeInHierarchy,"chapter one end button "+lv);
  int turn=lesson.Game.TurnCount;invoke(boot,"Update",new object[0]);check(lesson.Game.TurnCount==turn,"chapter one no extra action "+lv);
  var logButton=canvas.GetComponentsInChildren<UnityEngine.UI.Button>().Single(x=>x.gameObject.activeInHierarchy&&x.GetComponentInChildren<UnityEngine.UI.Text>()?.text.Contains("战斗记录")==true);click(logButton,canvas);check(((UnityEngine.RectTransform)field(board,"_battleLog")).gameObject.activeInHierarchy,"battle log can open before summary "+lv);
  if(lv==1)shot(canvas,"chapter-one-before-summary");
  click(end,canvas);check(board.AdventureModalVisible,"chapter one result on click "+lv);
 }
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.StoryChapterSelect);
 var menu=(UnityEngine.RectTransform)field(boot,"_selectRoot");
 check(menu.Find("StoryChapter01/Sub").GetComponent<UnityEngine.UI.Text>().text=="十场试炼 · 老兵的指尖骰术指导","chapter one ten-trials wording");shot(front,"two-chapters-ten-trials");
 check(FingerDiceDuel.UI.BattleArt.Get("Subclasses/"+MageTowerChapter.GuardId)?.name=="Adventure/npc_arcane_guard","NPC guard legacy callers resolve");
 check(FingerDiceDuel.UI.BattleArt.Get("Subclasses/"+MageTowerChapter.PhantomId)?.name=="Adventure/npc_mage_phantom","NPC phantom legacy callers resolve");
 var mageIndex=MageTowerChapter.FinaleRoster.Select((r,i)=>new{r,i}).Single(x=>x.r==CharacterDefinition.SchoolOfTransmutation).i;
 set(boot,"_storyChapter",2);set(boot,"_finaleIndex",mageIndex);invoke(boot,"StartMageTowerLevel",new object[]{20,true,0});
 var finale=(MageTowerLesson)field(boot,"_adventure");invoke(panel,"HideModal",new object[0]);finale.BeginTutorial();finale.Board.BeginTurn();
 finale.Player.LeftHand.SetValue(8);finale.Player.RightHand.SetValue(6);finale.Soldier.LeftHand.SetValue(6);finale.Soldier.RightHand.SetValue(4);refresh();
 check(finale.Board.TutorialInput==null,"finale free choice remains unrestricted");
 check(finale.Board.ClickHand(finale.Player.LeftHand)&&finale.Board.ClickHand(finale.Soldier.LeftHand)&&finale.Board.ConfirmTargetPlayer(),"finale transmutation opens real target choice");refresh();
 check(finale.Board.PendingWizardChoice?.TargetsPlayer==true,"finale target request");
 var buttons=(UnityEngine.UI.Button[])field(board,"_skillChoiceButtons");var hands=(Hand[])field(board,"_skillHands");bool phantom=false;
 for(int i=0;i<buttons.Length;i++)if(buttons[i]!=null&&buttons[i].gameObject.activeInHierarchy){
  var icon=buttons[i].transform.Find("SkillIcon").GetComponent<UnityEngine.UI.Image>();check(icon.enabled&&icon.sprite!=null,"finale target icon present "+i);
  check(buttons[i].interactable,"finale target remains selectable "+i);
  if(finale.Soldier.Owns(hands[i])){check(icon.sprite.name=="Adventure/npc_mage_phantom","actual phantom icon in skill target button");phantom=true;}
 }
 check(phantom,"phantom candidate actually displayed");shot(canvas,"phantom-in-skill-targets");
 check(finale.Board.ChooseWizardHand(null),"finale can decline optional skill");
 invoke(boot,"CleanupMatch",new object[0]);boot.StartMatch(CharacterDefinition.SchoolOfEvocation,CharacterDefinition.Champion);refresh();
 check(((BoardController)field(boot,"_controller")).TutorialInput==null,"normal game has no forced tutorial inputs");
 check(!((UnityEngine.UI.Button)field(board,"_trialEndButton")).gameObject.activeSelf,"normal game hides end button");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"chapter-one-and-phantom-validation.txt"),checks);return "PASS "+checks.Count+" chapter-one and phantom checks";
}finally{foreach(var pair in saved){if(pair.Value[0]==1)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value[1]);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}UnityEngine.PlayerPrefs.Save();boot.AiThinkSeconds=oldDelay;}
