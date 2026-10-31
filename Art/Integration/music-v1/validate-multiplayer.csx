using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using FingerDiceDuel.UI;
var boot=UnityEngine.Object.FindObjectOfType<GameBootstrap>();var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance;
System.Func<object,string,object> field=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);System.Action<string,object[]> call=(n,a)=>boot.GetType().GetMethod(n,flags).Invoke(boot,a);
var checks=new System.Collections.Generic.List<string>();System.Action<bool,string> check=(ok,s)=>{if(!ok)throw new System.Exception(s);checks.Add("PASS "+s);};float delay=boot.AiThinkSeconds;boot.AiThinkSeconds=999;
try{
 foreach(int level in new[]{21,29}){
  call("CleanupMatch",new object[0]);var lesson=TatteredBannersLesson.Create(level,0);call("BindBannersMatch",new object[]{lesson});boot.GetType().GetField("_adventure",flags).SetValue(boot,null);lesson.Board.TutorialInput=null;foreach(var p in lesson.Game.Players)p.SetHealthExact(p.MaximumHealth);call("StartPreparedMatch",new object[]{false});call("UpdateMusic",new object[0]);
  var music=(MusicPlayer)field(boot,"_music");check(lesson.Game.Players.Count==(level==21?4:8),"actual multiplayer layout "+level);check(music.CurrentCue==MusicCue.Battlefield&&!music.PercussionActive,"full health multiplayer music "+level);
  var target=lesson.Game.Players[3];target.SetHealthExact(target.MaximumHealth/2);call("UpdateMusic",new object[0]);check(music.PercussionActive,"non-duel participant enables drums "+level);
  target.SetHealthExact(target.MaximumHealth);call("UpdateMusic",new object[0]);check(music.PercussionActive,"multiplayer healing keeps intensity "+level);
 }
 System.IO.File.WriteAllLines("Art/Integration/music-v1/multiplayer-validation.txt",checks);return "Eight multiplayer music checks pass.";
}finally{call("CleanupMatch",new object[0]);boot.ShowScreen(GameScreen.MainMenu);boot.AiThinkSeconds=delay;}
