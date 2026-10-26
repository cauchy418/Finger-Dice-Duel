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

        bool ResolveHuman(MageTowerLesson l)
        {
            var b=l.Board;var g=l.Game;
            if(b.PendingWizardChoice!=null)
            {
                var q=b.PendingWizardChoice;
                if(q.Options.Count>0)
                {
                    int value=0;
                    if(l.LevelNumber==11)value=l.SampleIndex==0?(int)AttackDamageType.Fire:1;
                    if(l.LevelNumber==15)value=g.TurnCount<=1?(int)StatusEffect.Disabled:g.TurnCount==4?(int)StatusEffect.Charm:0;
                    return b.ChooseSkillOption(value);
                }
                if(l.LevelNumber==16)
                {
                    Hand hand=null;
                    if(q.Prompt.Contains("免费攻击")||q.Prompt.Contains("死灵暗蚀"))hand=g.TurnCount==0||g.TurnCount==1?l.Soldier.LeftHand:null;
                    if(q.Prompt.Contains("选自己的手"))hand=l.Player.RightHand.Value==3?l.Player.RightHand:null;
                    if(q.Prompt.Contains("选择死者"))hand=l.Soldier.RightHand;
                    return b.ChooseWizardHand(hand);
                }
                if(l.LevelNumber==19)return b.ChooseWizardHand(l.SampleIndex==0&&g.TurnCount==1?l.Soldier.LeftHand:null);
                return q.AllowDecline&&b.ChooseWizardHand(null);
            }
            if(b.PendingRequest.HasValue)
            {
                if(b.PendingSkillOptions.Count>0)return b.ChooseSkillOption(l.LevelNumber==18&&l.SampleIndex==2?1:0);
                if(b.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType)
                    return b.ChooseResistanceType(l.SampleIndex==1?AttackDamageType.Fire:AttackDamageType.Bludgeoning);
                if(b.PendingEffect==HandEffect.SchoolOfEvocationDamageType)
                {
                    var types=new[]{DamageType.Fire,DamageType.Cold,DamageType.Lightning,DamageType.Thunder,DamageType.Acid,DamageType.Poison,DamageType.Radiant,DamageType.Necrotic,DamageType.Psychic,DamageType.Force,DamageType.Cold,DamageType.Fire,DamageType.Lightning};
                    return b.ChooseDamageType(types[l.SampleIndex]);
                }
                if(b.PendingEffect==HandEffect.SchoolOfNecromancyBlock)return b.ClickHand(l.Player.LeftHand);
                if(b.PendingEffect==HandEffect.SchoolOfAbjurationResistanceTarget)return b.ClickHand(l.Player.LeftHand);
                bool accept=b.PendingEffect==HandEffect.SchoolOfAbjurationBarrier
                    ||b.PendingEffect==HandEffect.SchoolOfEvocationAbandonAttack&&g.TurnCount==0;
                return accept?b.AcceptPendingChoice():b.DeclinePendingChoice();
            }
            if(b.Phase==BoardPhase.ChoosingTargetPlayer)return b.ConfirmTargetPlayer();
            if(b.Phase==BoardPhase.ChoosingDefenseShield)return b.ClickHand(b.DefenseShieldChoices[0]);
            var turn=g.TurnCount<l.Sample.Turns.Count?l.Sample.Turns[g.TurnCount]:null;
            if(turn==null)return false;
            if(b.Phase==BoardPhase.ChoosingAttackerHand)
            {
                if(l.LevelNumber==13&&l.SampleIndex==1)
                {check(b.BeginCastling(),"begin castle");check(b.CancelCastling(),"cancel castle");check(b.BeginCastling(),"begin castle");return b.CompleteCastling(l.Seats[3]);}
                return b.ClickHand(l.Player.GetHand(turn.Source));
            }
            if(b.Phase==BoardPhase.ChoosingTargetHand)return b.ClickHand(l.Seats[turn.Target].GetHand(turn.Addend));
            return false;
        }

var keys=new System.Collections.Generic.List<string>{AdventureChapter.CompletionKey,MageTowerChapter.CompletionKey};
for(int lv=11;lv<=20;lv++){keys.Add("FingerDiceDuel.Adventure.Level"+lv+".Completed");foreach(var s in MageTowerChapter.Samples(lv))keys.Add(MageTowerChapter.SampleKey(lv,s.Key));}
var saved=keys.ToDictionary(k=>k,k=>new[]{UnityEngine.PlayerPrefs.HasKey(k)?1:0,UnityEngine.PlayerPrefs.GetInt(k,0)});
float oldDelay=boot.AiThinkSeconds;
try{
 boot.AiThinkSeconds=1000; foreach(var k in keys)UnityEngine.PlayerPrefs.DeleteKey(k);
 UnityEngine.PlayerPrefs.SetInt(AdventureChapter.CompletionKey,1);
 var menu=(UnityEngine.RectTransform)field(boot,"_selectRoot");boot.ShowScreen(FingerDiceDuel.UI.GameScreen.StoryChapterSelect);
 click(menu.Find("StoryChapter02").GetComponent<UnityEngine.UI.Button>(),front);
 check(boot.CurrentScreen==FingerDiceDuel.UI.GameScreen.StoryBattleSelect,"tower chapter entry");shot(front,"chapter-battles");
 click(menu.Find("StoryBattleScroll").GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.name=="StoryFirst"),front);
 var first=(MageTowerLesson)field(boot,"_adventure");check(first.LevelNumber==11,"first battle opens correct lesson");shot(canvas,"knight-intro");
 var panel=field(board,"_adventurePanel");click((UnityEngine.UI.Button)field(panel,"_confirmButton"),canvas);
 var cards=(System.Array)field(board,"_cardsA");
 var left=(UnityEngine.UI.Button)field(cards.GetValue(0),"Button");click(left,canvas);
 var foes=(System.Array)field(board,"_cardsB");click((UnityEngine.UI.Button)field(foes.GetValue(0),"Button"),canvas);
 check(first.Board.ConfirmTargetPlayer(),"confirm knight attack");refresh();check(first.Board.PendingWizardChoice!=null,"shield source opens element choice before damage");shot(canvas,"knight-element-choice");
 for(int lv=11;lv<=19;lv++)for(int si=0;si<MageTowerChapter.Samples(lv).Count;si++){
  invoke(boot,"StartMageTowerLevel",new object[]{lv,true,si});var lesson=(MageTowerLesson)field(boot,"_adventure");
  check(((UnityEngine.UI.Image)field(board,"_battleBackground")).sprite?.name==lesson.BattleBackgroundKey,"tower background "+lv+"/"+si);
  var body=(UnityEngine.UI.Text)field(panel,"_modalBody");check(body.preferredHeight<=body.rectTransform.rect.height,"intro text fits "+lv+"/"+si);
  click((UnityEngine.UI.Button)field(panel,"_confirmButton"),canvas);
  for(int tick=0;tick<160;tick++){
   refresh();check(((UnityEngine.UI.Image)field(board,"_battleBackground")).sprite?.name==lesson.BattleBackgroundKey,"background after refresh "+lv+"/"+si);if(lesson.IsFinished)break;
   if((lv==13&&si==1&&lesson.Game.TurnCount==1)||(lv==19&&si==0&&lesson.Soldier.IsPolymorphed&&lesson.Game.TurnCount==1))shot(canvas,lv==13?"castling-ring":"sheep-form");
   bool ok=lesson.Board.IsHumanChooser?ResolveHuman(lesson):lesson.Board.Phase==BoardPhase.ChoosingAttackerHand?lesson.Board.MakeAutomaticMove():lesson.Board.ResolvePendingAiChoice();
   check(ok,"advance "+lv+"/"+si+" turn "+lesson.Game.TurnCount+" "+lesson.Board.Phase);
  }
  refresh();check(lesson.IsComplete,"sample completes "+lv+"/"+si);
  check(((UnityEngine.UI.Text)field(panel,"_modalGuideName")).text=="秘法典籍","result guide "+lv+"/"+si);
  check(body.preferredHeight<=body.rectTransform.rect.height,"result text fits "+lv+"/"+si);
 }
 invoke(boot,"StartMageTowerLevel",new object[]{20,false,-1});UnityEngine.Canvas.ForceUpdateCanvases();
 check(boot.CurrentScreen==FingerDiceDuel.UI.GameScreen.AdventureFinaleSelect,"wizard finale selection");shot(front,"phantom-selection");
 invoke(boot,"StartMageTowerLevel",new object[]{20,true,0});var finale=(MageTowerLesson)field(boot,"_adventure");
 click((UnityEngine.UI.Button)field(panel,"_confirmButton"),canvas);check(boot.CurrentScreen==FingerDiceDuel.UI.GameScreen.DiceRoll,"finale real dice screen");shot(front,"phantom-initiative");
 invoke(boot,"ReturnToAdventure",new object[0]);check(field(boot,"_adventure")==null,"exit clears chapter match");
 boot.StartMatch(CharacterDefinition.Champion,CharacterDefinition.Breaker);refresh();check(field(boot,"_adventure")==null,"normal match unaffected");
 invoke(boot,"CleanupMatch",new object[0]);boot.ShowScreen(FingerDiceDuel.UI.GameScreen.MainMenu);
 System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"validation.txt"),checks);return "PASS "+checks.Count+" tower UI checks";
}finally{
 foreach(var pair in saved){if(pair.Value[0]==1)UnityEngine.PlayerPrefs.SetInt(pair.Key,pair.Value[1]);else UnityEngine.PlayerPrefs.DeleteKey(pair.Key);}
 UnityEngine.PlayerPrefs.Save();boot.AiThinkSeconds=oldDelay;
}
