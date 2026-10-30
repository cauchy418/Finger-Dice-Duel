// Prepend ui-common.csx. Fixtures exercise progress, replay, transition, and layout.
var saved=new System.Collections.Generic.Dictionary<string,int?>();
System.Action<string> remember=key=>{if(!saved.ContainsKey(key))saved[key]=UnityEngine.PlayerPrefs.HasKey(key)?(int?)UnityEngine.PlayerPrefs.GetInt(key):null;};
foreach(var key in new[]{AdventureChapter.CompletionKey,MageTowerChapter.CompletionKey,"FingerDiceDuel.Adventure.Level20.Completed","FingerDiceDuel.Adventure.Level24.Completed"})remember(key);
foreach(var sample in TatteredBannersChapter.Samples(24))remember(TatteredBannersChapter.SampleKey(24,sample.Key));
float delay=boot.AiThinkSeconds;
System.Func<UnityEngine.RectTransform,UnityEngine.Rect> box=rect=>{var corners=new UnityEngine.Vector3[4];rect.GetWorldCorners(corners);return UnityEngine.Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);};
System.Action<string> checkLayout=label=>{
 var guide=field(board,"_adventurePanel");var root=(UnityEngine.RectTransform)field(guide,"_guideRoot");
 foreach(var panel in (System.Collections.IEnumerable)field(board,"_ringPanels")){var rect=(UnityEngine.RectTransform)field(panel,"Root");if(rect.gameObject.activeInHierarchy)check(!box(root).Overlaps(box(rect)),label+" guide/player bounds");}
 var action=field(board,"_ringActionRoot") as UnityEngine.RectTransform;if(action!=null&&action.gameObject.activeInHierarchy)check(!box(root).Overlaps(box(action)),label+" guide/action bounds");
};
System.Action<UnityEngine.Canvas,string,int,int> render=(target,name,w,h)=>{
 var go=new UnityEngine.GameObject("Chapter3ResolutionCamera",typeof(UnityEngine.Camera));var cam=go.GetComponent<UnityEngine.Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=UnityEngine.CameraClearFlags.SolidColor;
 var rt=new UnityEngine.RenderTexture(w,h,24);var tex=new UnityEngine.Texture2D(w,h,UnityEngine.TextureFormat.RGB24,false);var old=UnityEngine.RenderTexture.active;var mode=target.renderMode;var oldCamera=target.worldCamera;var distance=target.planeDistance;
 var scaler=target.GetComponent<UnityEngine.UI.CanvasScaler>();
 try{cam.targetTexture=rt;target.renderMode=UnityEngine.RenderMode.ScreenSpaceCamera;target.worldCamera=cam;target.planeDistance=10;UnityEngine.Canvas.ForceUpdateCanvases();scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);UnityEngine.Canvas.ForceUpdateCanvases();cam.Render();
  check((int)target.pixelRect.width==w&&(int)target.pixelRect.height==h,"actual render target "+w+"x"+h);checkLayout(name);
  UnityEngine.RenderTexture.active=rt;tex.ReadPixels(new UnityEngine.Rect(0,0,w,h),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,name+".png"),UnityEngine.ImageConversion.EncodeToPNG(tex));}
 finally{target.renderMode=mode;target.worldCamera=oldCamera;target.planeDistance=distance;cam.targetTexture=null;UnityEngine.RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);scaler.GetType().GetMethod("Handle",flags).Invoke(scaler,new object[0]);UnityEngine.Canvas.ForceUpdateCanvases();}
};
try
{
 boot.AiThinkSeconds=1000;UnityEngine.PlayerPrefs.SetInt(MageTowerChapter.CompletionKey,1);UnityEngine.PlayerPrefs.SetInt(AdventureChapter.CompletionKey,1);
 UnityEngine.PlayerPrefs.DeleteKey("FingerDiceDuel.Adventure.Level24.Completed");
 var samples=TatteredBannersChapter.Samples(24);for(int i=0;i<samples.Count;i++)UnityEngine.PlayerPrefs.SetInt(TatteredBannersChapter.SampleKey(24,samples[i].Key),i==0?1:0);
 invoke(boot,"StartBannersLevel",new object[]{24,false,-1});var lesson=(TatteredBannersLesson)field(boot,"_adventure");
 check(lesson.SampleIndex==1,"resume first incomplete sample");
 var guide=field(board,"_adventurePanel");click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);
 lesson.Board.MakeAutomaticMove();refresh();check(lesson.Board.PendingEffect==HandEffect.OathOfDevotionRedirect,"resume reaches real protection request");
 render(canvas,"protection-1280x720",1280,720);render(canvas,"protection-1920x1080",1920,1080);
 for(int i=0;i<20&&!lesson.IsFinished;i++)
 {var b=lesson.Board;if(b.IsHumanChooser)b.ClickHand(lesson.SuggestedHand);else if(b.Phase==BoardPhase.ChoosingAttackerHand)b.MakeAutomaticMove();else b.ResolvePendingAiChoice();refresh();}
 check(lesson.IsComplete,"resumed sample completes");click((UnityEngine.UI.Button)field(board,"_trialEndButton"),canvas);
 click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);var replay=(TatteredBannersLesson)field(boot,"_adventure");
 check(!object.ReferenceEquals(lesson.Game,replay.Game)&&replay.SampleIndex==1&&replay.Player.LeftHand.IsShield&&replay.Seats[3].Health==12,"retry rebuilds same sample with fresh state");
 invoke(boot,"ReturnToAdventure",new object[0]);check(field(boot,"_adventure")==null&&!(bool)field(boot,"_isTwoOnTwo")&&!(bool)field(boot,"_isEightPlayer"),"leave resets director and multiplayer flags");
 invoke(boot,"StartBannersLevel",new object[]{29,true,1});guide=field(board,"_adventurePanel");click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);
 lesson=(TatteredBannersLesson)field(boot,"_adventure");lesson.Board.MakeAutomaticMove();refresh();
 render(canvas,"eight-protection-1280x720",1280,720);render(canvas,"eight-protection-1920x1080",1920,1080);
 invoke(boot,"CleanupMatch",new object[0]);
 // Explicit one-HP tower fixture tests the additional transition button, not boss difficulty.
 invoke(boot,"StartMageTowerLevel",new object[]{20,true,0});var tower=(MageTowerLesson)field(boot,"_adventure");
 tower.Soldier.SetHealthExact(1);tower.Player.LeftHand.SetValue(1);tower.Player.RightHand.SetValue(2);tower.Soldier.LeftHand.SetValue(3);
 board.HideAdventureGuide();tower.BeginTutorial();tower.Board.BeginTurn();tower.Board.ClickHand(tower.Player.LeftHand);tower.Board.ClickHand(tower.Soldier.LeftHand);
 for(int i=0;i<15&&!tower.Game.IsGameOver;i++)
 {var b=tower.Board;if(b.Phase==BoardPhase.ChoosingTargetPlayer)b.ClickHand(tower.Soldier.LeftHand);else if(b.PendingRequest.HasValue)b.DeclinePendingChoice();else if(b.PendingWizardChoice!=null)b.ChooseWizardHand(null);else b.ResolvePendingAiChoice();}
 refresh();check(tower.IsComplete,"tower victory fixture resolves actual attack");click((UnityEngine.UI.Button)field(board,"_trialEndButton"),canvas);guide=field(board,"_adventurePanel");
 var oldNext=(UnityEngine.UI.Button)field(guide,"_skipTutorialButton");var extra=(UnityEngine.UI.Button)field(guide,"_extraVictoryButton");
 check(oldNext.gameObject.activeInHierarchy&&oldNext.GetComponentInChildren<UnityEngine.UI.Text>().text=="重新选择法师","tower victory retains reselection");
 check(extra.gameObject.activeInHierarchy&&extra.GetComponentInChildren<UnityEngine.UI.Text>().text=="前往残旗战场","tower victory adds third chapter transition");
 check(!box((UnityEngine.RectTransform)oldNext.transform).Overlaps(box((UnityEngine.RectTransform)extra.transform)),"extra transition does not overlap old controls");
 var body=(UnityEngine.UI.Text)field(guide,"_modalBody");UnityEngine.Canvas.ForceUpdateCanvases();
 check(body.text==tower.SummaryText&&body.preferredHeight<=body.rectTransform.rect.height,"modal preserves author paragraphs and fits without injected short lines");shot(canvas,"tower-to-battlefield");
 click(extra,canvas);click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);click((UnityEngine.UI.Button)field(guide,"_confirmButton"),canvas);
 check((int)field(boot,"_storyChapter")==3&&field(boot,"_adventure")==null,"two transition pages reach chapter three");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"regression-validation.txt"),checks);return "PASS "+checks.Count+" progress/transition/resolution checks";
}
finally
{
 boot.AiThinkSeconds=delay;foreach(var pair in saved){if(pair.Value.HasValue)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value.Value);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}UnityEngine.PlayerPrefs.Save();
}
