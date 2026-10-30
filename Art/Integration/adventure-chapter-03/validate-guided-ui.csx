// Prepend ui-common.csx. Use the real runtime bootstrap, visual buttons, and raycasts.
int firstLevel=21,lastLevel=29;
var saved=new System.Collections.Generic.Dictionary<string,int?>();
System.Action<string> remember=key=>{if(!saved.ContainsKey(key))saved[key]=UnityEngine.PlayerPrefs.HasKey(key)?(int?)UnityEngine.PlayerPrefs.GetInt(key):null;};
remember(MageTowerChapter.CompletionKey);UnityEngine.PlayerPrefs.SetInt(MageTowerChapter.CompletionKey,1);
for(int n=21;n<=30;n++){remember("FingerDiceDuel.Adventure.Level"+n+".Completed");foreach(var sample in TatteredBannersChapter.Samples(n))remember(TatteredBannersChapter.SampleKey(n,sample.Key));}
remember(TatteredBannersChapter.CompletionKey);
float delay=boot.AiThinkSeconds;
System.Func<UnityEngine.RectTransform,UnityEngine.Rect> bounds=rect=>{var corners=new UnityEngine.Vector3[4];rect.GetWorldCorners(corners);return UnityEngine.Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);};
try
{
 boot.AiThinkSeconds=1000;
 for(int level=firstLevel;level<=lastLevel;level++)for(int sample=0;sample<TatteredBannersChapter.Samples(level).Count;sample++)
 {
  invoke(boot,"StartBannersLevel",new object[]{level,true,sample});
  var lesson=(TatteredBannersLesson)field(boot,"_adventure");var b=lesson.Board;
  var guide=field(board,"_adventurePanel");
  check(board.AdventureModalVisible,"intro modal "+level+"/"+sample);
  click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);refresh();
  check(lesson.Started&&!board.AdventureModalVisible,"intro starts actual lesson "+level+"/"+sample);
  if(level==21||level==29&&sample==0)shot(canvas,level==21?"borrow-four-players":"eight-faction-battlefield");
  bool historyChecked=false;
  for(int step=0;step<220&&!lesson.IsFinished;step++)
  {
   refresh();
   var guideRect=(UnityEngine.RectTransform)field(guide,"_guideRoot");
   foreach(var panel in (System.Collections.IEnumerable)field(board,"_ringPanels"))
   {
    var root=(UnityEngine.RectTransform)field(panel,"Root");if(!root.gameObject.activeInHierarchy)continue;
    check(!bounds(root).Overlaps(bounds(guideRect)),"guide does not overlap player "+level+"/"+sample+" step "+step);
    var emblem=(UnityEngine.UI.Image)field(panel,"Emblem");check(emblem.sprite!=null,"NPC/player emblem resolves "+level+"/"+sample);
   }
   if(!historyChecked&&b.CompletedActions>0)
   {
    var prev=(UnityEngine.UI.Button)field(guide,"_previousGuide");
    if(prev.interactable){int before=b.CompletedActions;click(prev,canvas);click((UnityEngine.UI.Button)field(guide,"_currentGuide"),canvas);check(b.CompletedActions==before,"guide history does not undo or replay "+level+"/"+sample);historyChecked=true;}
   }
   if(b.IsHumanChooser)
   {
    UnityEngine.UI.Button picked=null;
    if(b.PendingSkillOptions.Count>0)
    {
     var options=(UnityEngine.UI.Button[])field(board,"_wizardButtons");
     check(options[0].gameObject.activeInHierarchy&&options[1].gameObject.activeInHierarchy,"both skill options remain visible");
     picked=options.First(button=>button!=null&&button.gameObject.activeInHierarchy&&button.interactable);
     check(options.Count(button=>button!=null&&button.gameObject.activeInHierarchy&&button.interactable)==1,"only required option enabled");
    }
    else
    {
     var required=lesson.SuggestedHand;check(required!=null,"human has required hand/target "+level+"/"+sample);
     var hands=(Hand[])field(board,"_skillHands");var buttons=(UnityEngine.UI.Button[])field(board,"_skillChoiceButtons");
     for(int i=0;i<hands.Length;i++)if(hands[i]!=null&&buttons[i].gameObject.activeInHierarchy&&buttons[i].interactable&&object.ReferenceEquals(lesson.Game.OwnerOfHand(hands[i]),lesson.Game.OwnerOfHand(required)))picked=buttons[i];
     if(picked==null)foreach(var panel in (System.Collections.IEnumerable)field(board,"_ringPanels"))
       foreach(var card in (System.Collections.IEnumerable)field(panel,"Cards"))if(object.ReferenceEquals(field(card,"Hand"),required))picked=(UnityEngine.UI.Button)field(card,"Button");
     bool declineExpected=b.PendingWizardChoice?.AllowDecline==true||b.PendingRequest?.AllowDecline==true;
     if(declineExpected){var decline=(UnityEngine.UI.Button)field(board,"_declineButton");check(decline.gameObject.activeInHierarchy&&!decline.interactable,"decline remains visible but disabled "+level+"/"+sample);}
    }
    check(picked!=null&&picked.interactable,"correct button enabled "+level+"/"+sample);
    if(level==24&&sample==1&&b.PendingRequest.HasValue||level==28&&sample==2&&b.PendingSkillOptions.Count>0)shot(canvas,"choice-"+level+"-"+sample+"-"+step);
    click(picked,canvas);refresh();
   }
   else
   {
    bool ok=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
    check(ok,"NPC action/skill advances "+level+"/"+sample+" phase "+b.Phase);refresh();
   }
  }
  refresh();check(lesson.IsComplete,"actual UI sample finishes "+level+"/"+sample);
  check(!board.AdventureModalVisible,"last result remains on battlefield "+level+"/"+sample);
  var end=(UnityEngine.UI.Button)field(board,"_trialEndButton");check(end.gameObject.activeInHierarchy&&end.interactable,"trial end available "+level+"/"+sample);
  foreach(var panel in (System.Collections.IEnumerable)field(board,"_ringPanels")){var root=(UnityEngine.RectTransform)field(panel,"Root");if(root.gameObject.activeInHierarchy)check(!bounds(root).Overlaps(bounds((UnityEngine.RectTransform)end.transform)),"trial end does not cover player "+level+"/"+sample);}
  click((UnityEngine.UI.Button)field(board,"_historyButton"),canvas);check(board.IsHistoryOpen,"combat log available after last action");click((UnityEngine.UI.Button)field(board,"_historyButton"),canvas);
  if(level==23||level==29&&sample==3)shot(canvas,"result-"+level+"-"+sample);
  click(end,canvas);check(board.AdventureModalVisible,"summary opens only after end click "+level+"/"+sample);
  check(UnityEngine.PlayerPrefs.GetInt(TatteredBannersChapter.SampleKey(level,lesson.Sample.Key),0)==1,"sample progress saved "+level+"/"+sample);
 }
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.StoryChapterSelect);
 shot(front,"three-chapters");
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"guided-validation-"+firstLevel+"-"+lastLevel+".txt"),checks);
 return "PASS "+checks.Count+" real guided UI checks";
}
finally
{
 boot.AiThinkSeconds=delay;
 foreach(var pair in saved){if(pair.Value.HasValue)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}
 UnityEngine.PlayerPrefs.Save();
}
