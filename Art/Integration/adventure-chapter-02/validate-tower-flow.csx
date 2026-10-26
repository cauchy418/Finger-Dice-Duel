using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
// Run via execute_csharp_script in Play mode. Names, resources, raycast clicks and layout.
var boot=UnityEngine.Object.FindObjectOfType<FingerDiceDuel.UI.GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<object,string,object[]> invoke=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
var output=System.IO.Path.GetFullPath("Art/Integration/adventure-chapter-02");System.IO.Directory.CreateDirectory(output);
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

        
var keys=new System.Collections.Generic.List<string>{AdventureChapter.CompletionKey,MageTowerChapter.CompletionKey,"FingerDiceDuel.Adventure.Level10.Completed"};
for(int lv=11;lv<=20;lv++){keys.Add("FingerDiceDuel.Adventure.Level"+lv+".Completed");foreach(var trial in MageTowerChapter.Samples(lv))keys.Add(MageTowerChapter.SampleKey(lv,trial.Key));}
var saved=keys.ToDictionary(k=>k,k=>new[]{UnityEngine.PlayerPrefs.HasKey(k)?1:0,UnityEngine.PlayerPrefs.GetInt(k,0)});
var panel=field(board,"_adventurePanel");var confirm=(UnityEngine.UI.Button)field(panel,"_confirmButton");var next=(UnityEngine.UI.Button)field(panel,"_skipTutorialButton");
float oldDelay=boot.AiThinkSeconds;
try{
 boot.AiThinkSeconds=1000;foreach(var k in keys)UnityEngine.PlayerPrefs.DeleteKey(k);
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.StoryChapterSelect);
 invoke(boot,"OpenStoryChapter",new object[]{2});check(field(boot,"_adventure")==null,"locked tower does not launch a match");
 // Close the standard notice before testing the old-save unlock.
 var modal=field(boot,"_modal");invoke(modal,"Hide",new object[0]);
 UnityEngine.PlayerPrefs.SetInt("FingerDiceDuel.Adventure.Level10.Completed",1);
 invoke(boot,"OpenStoryChapter",new object[]{2});check(boot.CurrentScreen==FingerDiceDuel.UI.GameScreen.StoryBattleSelect,"old level10 save unlocks tower");
 UnityEngine.PlayerPrefs.SetInt(MageTowerChapter.SampleKey(12,"Fire"),1);
 invoke(boot,"StartMageTowerLevel",new object[]{12,false,-1});var resume=(MageTowerLesson)field(boot,"_adventure");
 check(resume.Sample.Key=="Cold","resume first unfinished sample");
 invoke(boot,"StartMageTowerLevel",new object[]{11,true,0});var l=(MageTowerLesson)field(boot,"_adventure");click(confirm,canvas);
 check(l.Board.ClickHand(l.Player.LeftHand)&&l.Board.ClickHand(l.Soldier.LeftHand)&&l.Board.ConfirmTargetPlayer(),"wrong-choice fixture starts normal attack");
 check(l.Board.ChooseSkillOption(0),"decline shield enchantment");refresh();
 check(l.Board.MakeAutomaticMove(),"training NPC observation after wrong choice");refresh();
 check(l.IsFinished&&!l.IsComplete,"wrong element fails observation rather than softlocks");
 check(UnityEngine.PlayerPrefs.GetInt(l.CompletionKey,0)==0&&UnityEngine.PlayerPrefs.GetInt(MageTowerChapter.SampleKey(11,"shield"),0)==0,"failed sample grants no completion");
 click(confirm,canvas);l=(MageTowerLesson)field(boot,"_adventure");check(l.SampleIndex==0&&l.Player.LeftHand.IsShield&&l.Soldier.Health==20,"retry resets current sample");
 invoke(boot,"StartMageTowerLevel",new object[]{20,true,0});l=(MageTowerLesson)field(boot,"_adventure");l.Player.SetHealthExact(0);l.Game.RefreshGameOver();refresh();
 check(!l.IsComplete&&UnityEngine.PlayerPrefs.GetInt(l.CompletionKey,0)==0&&UnityEngine.PlayerPrefs.GetInt(MageTowerChapter.CompletionKey,0)==0,"finale loss grants no completion");
 invoke(boot,"StartMageTowerLevel",new object[]{20,true,0});l=(MageTowerLesson)field(boot,"_adventure");l.Soldier.SetHealthExact(0);l.Game.RefreshGameOver();refresh();
 check(l.IsComplete&&UnityEngine.PlayerPrefs.GetInt(l.CompletionKey,0)==1&&UnityEngine.PlayerPrefs.GetInt(MageTowerChapter.CompletionKey,0)==1,"finale win stores level and chapter");
 invoke(boot,"StartMageTowerLevel",new object[]{20,true,0});l=(MageTowerLesson)field(boot,"_adventure");l.Player.SetHealthExact(0);l.Game.RefreshGameOver();refresh();
 check(UnityEngine.PlayerPrefs.GetInt(MageTowerChapter.CompletionKey,0)==1,"later loss preserves old victory");
 invoke(boot,"StartAdventureLevel",new object[]{10,true});var first=(AdventureLesson)field(boot,"_adventure");first.Soldier.SetHealthExact(0);first.Game.RefreshGameOver();refresh();
 check(next.gameObject.activeInHierarchy&&next.GetComponentInChildren<UnityEngine.UI.Text>().text=="前往法师高塔","knight victory offers tower transition");
 click(next,canvas);for(int page=0;page<3;page++){check(((UnityEngine.UI.Text)field(panel,"_modalBody")).text.Length>10,"transition dialogue "+page);click(confirm,canvas);}
 check(field(boot,"_adventure")==null&&boot.CurrentScreen==FingerDiceDuel.UI.GameScreen.StoryBattleSelect&&(int)field(boot,"_storyChapter")==2,"transition enters tower directory");
 boot.StartMatch(CharacterDefinition.WarDomain,CharacterDefinition.Blank);var g=(GameManager)field(boot,"_game");var b=(BoardController)field(boot,"_controller");
 g.PlayerB.LeftHand.SetValue(3);refresh();
 check(g.PlayerA.Character.AttackBonus==0&&g.PlayerA.Character.DefenseBonus==0,"war base 0/0");
 check(b.ClickHand(g.PlayerA.LeftHand)&&b.ClickHand(g.PlayerB.LeftHand)&&b.ConfirmTargetPlayer(),"war player initiates attack through board");refresh();
 check(g.PlayerB.Health==16&&g.PlayerA.DynamicAttackBonus==1&&g.PlayerA.DynamicDefenseBonus==0,"war attack four then attack-only growth");
 new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);refresh();
 check(g.PlayerA.Health==16&&g.PlayerA.DynamicAttackBonus==1&&g.PlayerA.DynamicDefenseBonus==1,"war received attack four then defense-only growth");shot(canvas,"war-independent-growth");
 var entry=FingerDiceDuel.Characters.CharacterCodex.For(CharacterDefinition.WarDomain);
 check(CharacterDefinition.WarDomain.Description.Contains("攻防0/0")&&CharacterDefinition.WarDomain.Description.Contains("发起攻击后攻击+1"),"war latest definition and UI text");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"flow-validation.txt"),checks);return "PASS "+checks.Count+" persistence, transition and war checks";
}finally{
 foreach(var pair in saved){if(pair.Value[0]==1)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value[1]);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}UnityEngine.PlayerPrefs.Save();boot.AiThinkSeconds=oldDelay;
}
