// Prepend ui-common.csx after the actual dice animation has finished.
var prefs=new System.Collections.Generic.Dictionary<string,int?>();
foreach(var key in new[]{"FingerDiceDuel.Adventure.Level30.Completed",TatteredBannersChapter.CompletionKey})prefs[key]=UnityEngine.PlayerPrefs.HasKey(key)?(int?)UnityEngine.PlayerPrefs.GetInt(key):null;
float originalDelay=UnityEngine.JsonUtility.FromJson<UnityEngine.Vector2>(System.IO.File.ReadAllText(System.IO.Path.GetFullPath("Tools/UiCompileCheck/bin/banners-finale-state.json"))).x;
try
{
 var preview=(TatteredBannersLesson)field(boot,"_adventure");var human=preview.Player;var ally=preview.Seats[3];
 var setup=(TwoOnTwoStartController)field(boot,"_twoOnTwoStart");
 var screen=(FingerDiceDuel.UI.TwoOnTwoInitiativeScreen)field(boot,"_screenTwoOnTwoInitiative");
 for(int i=0;i<8&&!setup.IsFinished;i++)
 {
  var options=(System.Collections.Generic.List<UnityEngine.GameObject>)field(screen,"_optionButtons");
  check(options.Count>0,"human initiative candidates visible");click(options[0].GetComponent<UnityEngine.UI.Button>(),front);
 }
 check(setup.IsFinished,"classic assignment completes through real candidate buttons");
 var lesson=(TatteredBannersLesson)field(boot,"_adventure");var b=lesson.Board;
 check(object.ReferenceEquals(human,lesson.Player)&&lesson.Game.IsTeammate(human,ally),"actual human and ally preserved after dice seating");
 check(object.ReferenceEquals(setup.PlayerAtSeat(1),b.ActivePlayer),"actual first seat starts");
 check(b.TutorialInput==null&&lesson.Started,"finale has no guided restrictions");
 refresh();shot(canvas,"classic-two-on-two-battle");
 foreach(var p in lesson.Seats)b.BindPlayer(p,TatteredBannersLesson.NewAi(lesson.Game),lesson.Game.GetChoiceProvider(p) as IPlayerChoiceSink);
 for(int guard=0;guard<4000&&!lesson.Game.IsGameOver;guard++)
 {bool ok=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();check(ok,"free finale AI proxy advances");}
 refresh();check(lesson.Game.IsGameOver,"free finale reaches ordinary victory/defeat");
 bool won=lesson.Game.Winner!=null&&lesson.Game.IsTeammate(lesson.Player,lesson.Game.Winner);
 check(lesson.IsComplete==won,"chapter completion uses team victory");
 check(!board.AdventureModalVisible,"finale result remains on battlefield");
 var end=(UnityEngine.UI.Button)field(board,"_trialEndButton");click(end,canvas);check(board.AdventureModalVisible,"finale summary opens only by trial end");
 check(UnityEngine.PlayerPrefs.GetInt(TatteredBannersChapter.CompletionKey,0)==(won?1:prefs[TatteredBannersChapter.CompletionKey]??0),"win saves completion; loss preserves old result");
 var guide=field(board,"_adventurePanel");var reselect=(UnityEngine.UI.Button)field(guide,"_skipTutorialButton");
 check(reselect.gameObject.activeInHierarchy&&reselect.GetComponentInChildren<UnityEngine.UI.Text>().text=="重新选择圣武士","reselect offered on finale result");shot(canvas,"finale-summary");
 click(reselect,canvas);var roster=(FingerDiceDuel.UI.CharacterRosterView)field(boot,"_finaleRoster");
 check(roster.IsChoosingProfession&&roster.Count==6,"reselect returns to paladin profession");
 check(field(boot,"_adventure")==null&&field(boot,"_controller")==null,"leaving clears chapter director/controller");
 check(!board.AdventureModalVisible,"leaving closes result modal");
 boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"finale-finish-validation.txt"),checks);
 return "PASS "+checks.Count+" real classic finale checks";
}
finally
{
 boot.AiThinkSeconds=originalDelay;
 foreach(var pair in prefs){if(pair.Value.HasValue)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}
 UnityEngine.PlayerPrefs.Save();
}
