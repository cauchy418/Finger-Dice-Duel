// Prepend ui-common.csx. Leave the real roll animation for the next frame/request.
var oldChapter=UnityEngine.PlayerPrefs.HasKey(MageTowerChapter.CompletionKey)?(int?)UnityEngine.PlayerPrefs.GetInt(MageTowerChapter.CompletionKey):null;
var oldLevel=UnityEngine.PlayerPrefs.HasKey("FingerDiceDuel.Adventure.Level20.Completed")?(int?)UnityEngine.PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level20.Completed"):null;
float oldDelay=boot.AiThinkSeconds;
try
{
 boot.AiThinkSeconds=1000;set(boot,"_storyChapter",1);
 UnityEngine.PlayerPrefs.DeleteKey(MageTowerChapter.CompletionKey);UnityEngine.PlayerPrefs.DeleteKey("FingerDiceDuel.Adventure.Level20.Completed");
 check(!(bool)boot.GetType().GetProperty("BannersUnlocked",flags).GetValue(boot),"third chapter locked before tower victory");
 UnityEngine.PlayerPrefs.SetInt("FingerDiceDuel.Adventure.Level20.Completed",1);
 check((bool)boot.GetType().GetProperty("BannersUnlocked",flags).GetValue(boot),"legacy tower completion unlocks third chapter");
 invoke(boot,"OpenStoryChapter",new object[]{3});
 var heading=(UnityEngine.UI.Text)field(boot,"_storyHeading");check(heading.text=="第三章：残旗战场","chapter heading");
 var battleButtons=(UnityEngine.UI.Button[])field(boot,"_storyBattleButtons");
 check(battleButtons.Length==10&&battleButtons[9].GetComponentInChildren<UnityEngine.UI.Text>().text.Contains("终战"),"ten battles in chapter menu");shot(front,"chapter-three-lessons");
 battleButtons[9].GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=0;UnityEngine.Canvas.ForceUpdateCanvases();
 click(battleButtons[9],front);
 var roster=(FingerDiceDuel.UI.CharacterRosterView)field(boot,"_finaleRoster");
 check(roster.Count==6&&roster.IsChoosingProfession&&roster.Professions.Count==1&&roster.Professions[0].Id=="paladin","finale chooses paladin profession before six subclasses");
 var cards=(System.Collections.Generic.List<UnityEngine.UI.Button>)field(roster,"_cards");click(cards.First(b=>b.gameObject.activeInHierarchy),front);
 check(!roster.IsChoosingProfession&&roster.VisibleIndices.Count==6,"all six paladins available");
 click(cards.First(b=>b.gameObject.activeInHierarchy&&b.interactable),front);
 var foe=((UnityEngine.RectTransform)field(boot,"_selectRoot")).Find("FinaleKnight");
 var stats=foe.Find("Stats").GetComponent<UnityEngine.UI.Text>();
 check(stats.text.Contains("古贤之誓")&&stats.text.Contains("24")&&stats.text.Contains("距2")&&stats.preferredHeight<=stats.rectTransform.rect.height,"ally and both enemy stats fit preview");shot(front,"finale-paladin-selection");
 var start=((UnityEngine.RectTransform)field(boot,"_selectRoot")).Find("FinaleStart").GetComponent<UnityEngine.UI.Button>();click(start,front);
 var lesson=(TatteredBannersLesson)field(boot,"_adventure");check(lesson.IsFinale&&board.AdventureModalVisible,"finale briefing before dice");
 var guide=field(board,"_adventurePanel");click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);
 var initiative=(FingerDiceDuel.UI.TwoOnTwoInitiativeScreen)field(boot,"_screenTwoOnTwoInitiative");
 var setup=(TwoOnTwoStartController)field(boot,"_twoOnTwoStart");
 check(setup!=null&&!setup.IsFinished,"real four-player initiative created");
 check(((UnityEngine.UI.Text)field(initiative,"_lessonText")).gameObject.activeInHierarchy,"sergeant explains 2v2 initiative on dice screen");
 shot(front,"classic-two-on-two-dice");
 click((UnityEngine.UI.Button)field(initiative,"_rollButton"),front);
 System.IO.File.WriteAllText(System.IO.Path.GetFullPath("Tools/UiCompileCheck/bin/banners-finale-state.json"),UnityEngine.JsonUtility.ToJson(new UnityEngine.Vector2(oldDelay,0)));
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"finale-start-validation.txt"),checks);
 return "PASS "+checks.Count+" finale entry/selection/dice checks; roll animation started";
}
finally
{
 if(oldChapter.HasValue)UnityEngine.PlayerPrefs.SetInt(MageTowerChapter.CompletionKey,oldChapter.Value);else UnityEngine.PlayerPrefs.DeleteKey(MageTowerChapter.CompletionKey);
 if(oldLevel.HasValue)UnityEngine.PlayerPrefs.SetInt("FingerDiceDuel.Adventure.Level20.Completed",oldLevel.Value);else UnityEngine.PlayerPrefs.DeleteKey("FingerDiceDuel.Adventure.Level20.Completed");
 UnityEngine.PlayerPrefs.Save();
}
