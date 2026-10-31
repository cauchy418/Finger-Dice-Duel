using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.UI;
using UnityEngine;
using UnityEngine.UI;
UnityEditor.EditorApplication.isPaused=false;
var boot=UnityEngine.Object.FindObjectOfType<GameBootstrap>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
System.Action<string,object[]> invoke=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var music=(MusicPlayer)field(boot,"_music");var canvas=((RectTransform)field(boot,"_selectRoot")).GetComponent<Canvas>();
var folder=System.IO.Path.GetFullPath("Art/Integration/music-v1");
var checks=new System.Collections.Generic.List<string>();System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};
System.Action<string> shot=name=>{
 var go=new GameObject("MusicReviewCamera",typeof(Camera));var cam=go.GetComponent<Camera>();cam.enabled=false;cam.orthographic=true;cam.clearFlags=CameraClearFlags.SolidColor;
 var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;var mode=canvas.renderMode;var oldCam=canvas.worldCamera;float distance=canvas.planeDistance;
 try{cam.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=cam;canvas.planeDistance=10;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name+".png"),ImageConversion.EncodeToPNG(tex));}
 finally{canvas.renderMode=mode;canvas.worldCamera=oldCam;canvas.planeDistance=distance;cam.targetTexture=null;RenderTexture.active=old;UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
};
bool had=PlayerPrefs.HasKey(MusicPlayer.PreferenceKey);int saved=PlayerPrefs.GetInt(MusicPlayer.PreferenceKey,1);bool background=Application.runInBackground;Application.runInBackground=true;float delay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;
float listener=AudioListener.volume;var sources=UnityEngine.Object.FindObjectsOfType<AudioSource>();var oldMute=new System.Collections.Generic.Dictionary<AudioSource,bool>();foreach(var s in sources)if(!s.name.StartsWith("Music"))oldMute[s]=s.mute;
System.Action cleanup=()=>{invoke("CleanupMatch",new object[0]);boot.ShowScreen(GameScreen.MainMenu);music.SetMusicEnabled(saved!=0);invoke("RefreshMusicSetting",new object[0]);if(!had)PlayerPrefs.DeleteKey(MusicPlayer.PreferenceKey);PlayerPrefs.Save();boot.AiThinkSeconds=delay;Application.runInBackground=background;};
int stage=0,cue=0;double began=UnityEditor.EditorApplication.timeSinceStartup,next=0;double join=0;object bank=null;AudioSource intro=null,loop=null,drumIntro=null,drumLoop=null;
UnityEditor.EditorApplication.CallbackFunction tick=null;
tick=()=>{try{
 UnityEditor.EditorApplication.QueuePlayerLoopUpdate();double now=AudioSettings.dspTime;if(UnityEditor.EditorApplication.timeSinceStartup-began>55)throw new System.Exception("native music timeout stage="+stage);
 if(stage==0){
  boot.ShowScreen(GameScreen.MainMenu);shot("main-menu");check(music.CurrentCue==MusicCue.Dusk,"main menu uses dusk");
  var settings=((RectTransform)field(boot,"_selectRoot")).Find("Settings").GetComponent<Button>();check(settings.gameObject.activeInHierarchy,"settings footer visible");settings.onClick.Invoke();check(boot.CurrentScreen==GameScreen.Settings,"footer opens settings");
  music.SetMusicEnabled(true);invoke("RefreshMusicSetting",new object[0]);var toggle=(Button)field(boot,"_musicToggle");toggle.onClick.Invoke();check(!music.MusicEnabled,"toggle disables music");check(PlayerPrefs.GetInt(MusicPlayer.PreferenceKey,1)==0,"disabled preference persisted");foreach(var s in music.GetComponentsInChildren<AudioSource>())check(s.mute,"muted "+s.name);
  foreach(var p in oldMute)check(p.Key.mute==p.Value,"sfx mute unchanged "+p.Key.name);check(AudioListener.volume==listener,"global listener unchanged");shot("settings-off");
  var probe=new GameObject("MusicPreferenceProbe").AddComponent<MusicPlayer>();probe.Initialize();check(!probe.MusicEnabled,"new player restores disabled preference");UnityEngine.Object.DestroyImmediate(probe.gameObject);
  toggle.onClick.Invoke();check(music.MusicEnabled,"toggle reenables music");shot("settings-on");
  var active=(System.Array)field(music,"_banks");var existing=active.GetValue((int)field(music,"_active"));boot.ShowScreen(GameScreen.Codex);boot.ShowScreen(GameScreen.MainMenu);check(object.ReferenceEquals(existing,active.GetValue((int)field(music,"_active"))),"menu navigation keeps playback bank");
  boot.StartMatch(CharacterDefinition.Blank,CharacterDefinition.Blank);var game=(GameManager)field(boot,"_game");invoke("UpdateMusic",new object[0]);check(music.CurrentCue==MusicCue.Battlefield,"ordinary match uses battlefield");check(!music.PercussionActive,"full health has no added drums");game.PlayerB.SetHealthExact(10);invoke("UpdateMusic",new object[0]);check(music.PercussionActive,"enemy half health enables drums");game.PlayerB.SetHealthExact(20);invoke("UpdateMusic",new object[0]);check(music.PercussionActive,"healing retains drums this match");
  foreach(int level in new[]{10,20,30}){AdventureLesson lesson=level==10?(AdventureLesson)AdventureLevel10.Attach(game,(BoardController)field(boot,"_controller")):level==20?(AdventureLesson)MageTowerLesson.Create(20,0,CharacterDefinition.SchoolOfEvocation):TatteredBannersLesson.Create(30,0);boot.GetType().GetField("_adventure",flags).SetValue(boot,lesson);invoke("UpdateMusic",new object[0]);check(music.CurrentCue==MusicCue.Dawn,"chapter finale uses dawn "+level);}
  invoke("CleanupMatch",new object[0]);boot.ShowScreen(GameScreen.MainMenu);check(music.CurrentCue==MusicCue.Dusk&&!music.PercussionActive,"return clears battle intensity");
  stage=1;
 }
 else if(stage==1){
  if(cue==1){boot.StartMatch(CharacterDefinition.Blank,CharacterDefinition.Blank);((GameManager)field(boot,"_game")).PlayerB.SetHealthExact(10);invoke("UpdateMusic",new object[0]);}if(cue==2){boot.GetType().GetField("_adventure",flags).SetValue(boot,AdventureLevel10.Attach((GameManager)field(boot,"_game"),(BoardController)field(boot,"_controller")));invoke("UpdateMusic",new object[0]);}music.SetCue((MusicCue)cue,cue>0);bank=((System.Array)field(music,"_banks")).GetValue((int)field(music,"_active"));intro=(AudioSource)field(bank,"Intro");loop=(AudioSource)field(bank,"Loop");drumIntro=(AudioSource)field(bank,"DrumIntro");drumLoop=(AudioSource)field(bank,"DrumLoop");
  check(intro.clip!=null&&loop.clip!=null,"clips imported "+cue);check(intro.clip.channels==2&&loop.clip.channels==2,"stereo preserved "+cue);check(intro.clip.loadType==AudioClipLoadType.Streaming,"streaming enabled "+cue);check(!intro.loop&&loop.loop,"intro once loop repeats "+cue);
  if(cue>0){check(intro.clip.samples==drumIntro.clip.samples,"intro stem samples match "+cue);check(loop.clip.samples==drumLoop.clip.samples,"loop stem samples match "+cue);}
  // Audition the actual tail and scheduled loop in 1 s, without waiting several minutes.
  intro.Stop();loop.Stop();drumIntro.Stop();drumLoop.Stop();intro.timeSamples=intro.clip.samples-intro.clip.frequency;
  double start=now+.3;join=start+1;intro.PlayScheduled(start);loop.PlayScheduled(join);
  if(cue>0){drumIntro.timeSamples=drumIntro.clip.samples-drumIntro.clip.frequency;drumIntro.PlayScheduled(start);drumLoop.PlayScheduled(join);}
  next=join+.45;stage=2;
 }
 else if(stage==2&&now>next){
  check(!intro.isPlaying&&loop.isPlaying&&loop.timeSamples>0,"DSP intro to real loop plays "+cue);
  if(cue>0){check(!drumIntro.isPlaying&&drumLoop.isPlaying,"drum stem transitions to loop "+cue);check(System.Math.Abs(loop.timeSamples-drumLoop.timeSamples)<2048,"drum/main phase alignment "+cue);}
  cue++;stage=cue==3?3:1;
 }
 else if(stage==3){
  cleanup();UnityEditor.EditorApplication.update-=tick;System.IO.File.WriteAllLines(System.IO.Path.Combine(folder,"native-validation.txt"),checks);
 }
 }catch(System.Exception ex){UnityEditor.EditorApplication.update-=tick;cleanup();System.IO.File.WriteAllText(System.IO.Path.Combine(folder,"native-validation.txt"),string.Join("\n",checks)+"\nFAIL "+ex);}};
UnityEditor.EditorApplication.update+=tick;return "Scheduled music UI, routing, preference, half-health and DSP seam checks.";
