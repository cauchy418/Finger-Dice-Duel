using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class CampaignV3Tests
    {
        public static void Guide(CampaignLesson lesson)
        {
            lesson.BeginTutorial();lesson.Board.BeginTurn();
            for(int i=0;i<2400&&!lesson.TrialConfirmationPending&&!lesson.Game.IsGameOver;i++)
            {
                lesson.Tick();if(lesson.TrialConfirmationPending)break;
                Assert.IsTrue(CampaignFlowTests.ClickGuide(lesson),CampaignFlowTests.Diagnostic(lesson));
            }
            Assert.IsTrue(lesson.TrialConfirmationPending,CampaignFlowTests.Diagnostic(lesson));
            Assert.IsFalse(lesson.TeachingComplete,"确认前应保留战场");
            Assert.IsFalse(lesson.IsComplete);
        }
        [Test] public void All_four_chapters_finish_guidance_before_confirming_trial()
        {
            var errors=new List<string>();
            for(int n=1;n<=40;n++)if(n!=5&&n%10!=0)
            {
                try
                {
                    StatusRules.ResetInteractionRandomForTest(418);var l=CampaignLesson.CreateV3(n);var original=l.Game;
                    Guide(l);Assert.IsTrue(l.Game.RoundCount<=30,"教学超30轮 L"+n+" "+l.Game.RoundCount);
                    Assert.IsTrue(l.ConfirmTrial());Assert.IsTrue(object.ReferenceEquals(original,l.Game));
                    Assert.IsFalse(l.ConfirmTrial());Assert.IsTrue(l.CanRetryTrial);Assert.AreEqual(1,l.HornCount);
                    Console.WriteLine("V3 GUIDE PASS L"+n+" round="+l.Game.RoundCount);
                }
                catch(Exception e){errors.Add("L"+n+" "+e);}
            }
            Assert.IsTrue(errors.Count==0,string.Join("\n",errors));
        }
        [Test] public void Stable_ids_separate_gallery_from_legacy_tower()
        {
            Assert.AreEqual("gallery.01",CampaignLesson.CreateV3(11).StableId);
            Assert.AreEqual("tower.01",CampaignLesson.CreateV3(21).StableId);
            Assert.AreEqual("banners.01",CampaignLesson.CreateV3(31).StableId);
            Assert.AreEqual(CharacterDefinition.SchoolOfConjuration,CampaignLesson.CreateV3(22).Player.Definition);
            Assert.AreEqual(CharacterDefinition.Bladesinging,CampaignLesson.CreateV3(29).Player.Definition);
            Assert.AreEqual(4,CampaignChapter.Gallery.FinaleRoster.Count);
            for(int level=1;level<=40;level++)Assert.IsTrue(CampaignGuideText.For(level).Length<=10);
            for(int level=1;level<=40;level++)
            {
                var lesson=CampaignLesson.CreateV3(level);
                if(lesson.UsesClassicSetup)continue;
                foreach(var line in CampaignGuideText.For(level))
                {bool included=false;foreach(var step in lesson.Steps)if(step.Text.Contains(line))included=true;Assert.IsTrue(included,"漏指引 L"+level+"："+line);}
            }
        }
        [Test] public void All_finale_subclasses_have_a_playable_retry_and_correct_npc_identity()
        {
            foreach(var chapter in CampaignChapter.All)foreach(var role in chapter.FinaleRoster)
            {
                var lesson=CampaignLesson.CreateV3(chapter.Number*10,role);lesson.BeginTutorial();lesson.Board.BeginTurn();
                for(int i=0;i<30&&!lesson.CanRetryTrial;i++)
                {lesson.CaptureTrial();if(lesson.CanRetryTrial)break;Assert.IsTrue(CampaignFlowTests.ClickGuide(lesson));}
                Assert.IsTrue(lesson.CanRetryTrial,role.DisplayName);
                var retry=lesson.RetryTrial();Assert.AreEqual(role,retry.Player.Definition);
                foreach(var p in retry.Seats)retry.Board.BindPlayer(p,CampaignLesson.NewAi(retry.Game),(IPlayerChoiceSink)retry.Game.GetChoiceProvider(p));
                for(int i=0;i<3000&&!retry.Game.IsGameOver;i++)
                    Assert.IsTrue(retry.Board.Phase==BoardPhase.ChoosingAttackerHand?retry.Board.MakeAutomaticMove():retry.Board.ResolvePendingAiChoice(),role.DisplayName);
                Assert.IsTrue(retry.Game.IsGameOver,role.DisplayName);
            }
            foreach(int level in new[]{23,29})
            {var lesson=CampaignLesson.CreateV3(level);Assert.AreEqual("奥法护卫",lesson.Seats[1].Name);Assert.AreEqual("Adventure/npc_arcane_guard",lesson.Seats[1].BattleEmblemKey);}
        }
        [Test] public void Gallery_uses_thirteen_real_typed_hits_and_bladesong_survives_healing()
        {
            var reaction=CampaignLesson.CreateV3(18);reaction.BeginTutorial();reaction.Board.BeginTurn();
            var types=new List<AttackDamageType>();
            for(int i=0;i<2200&&!reaction.TrialConfirmationPending;i++)
            {
                var step=reaction.Step;int cursor=reaction.Cursor;
                Assert.IsTrue(CampaignFlowTests.ClickGuide(reaction),CampaignFlowTests.Diagnostic(reaction));
                if(reaction.Cursor!=cursor&&step.Actor==0)
                {
                    var hit=reaction.Board.LastTurn;types.Add(hit.AttackType);
                    Console.WriteLine("REACTION "+types.Count+" "+hit.AttackType+" damage="+hit.DamageDealt+" applied="+hit.AppliedStatus);
                    Assert.IsTrue(hit.DamageDealt>0&&!hit.WasCancelled&&!hit.WasDodged,"指定元素必须真正命中 "+CampaignFlowTests.Diagnostic(reaction));
                    int count=types.Count;
                    var target=reaction.Seats[1];
                    if(count==1)Assert.IsTrue(target.HasStatus(StatusEffect.Dizzy));
                    if(count==2||count==3)Assert.IsTrue(target.HasStatus(StatusEffect.Tinnitus));
                    if(count==4||count==5)Assert.IsTrue(target.HasStatus(StatusEffect.Frozen));
                    if(count==6)Assert.IsTrue(hit.DamageDealt>=9);
                    if(count==7||count==8||count==9)Assert.IsFalse(target.HasStatus(StatusEffect.Burn));
                    if(count==10)Assert.IsTrue(target.HasStatus(StatusEffect.Burn));
                    if(count==11)Assert.IsTrue(target.HasStatus(StatusEffect.Wet)&&target.HasStatus(StatusEffect.Shock));
                    if(count>=12)Assert.IsTrue(target.HasStatus(StatusEffect.Poison)&&target.HasStatus(StatusEffect.Acid));
                }
            }
            Assert.AreEqual(13,types.Count);Assert.AreEqual(AttackDamageType.Thunder,types[0]);Assert.AreEqual(AttackDamageType.Poison,types[12]);
            var blade=CampaignLesson.CreateV3(29);blade.BeginTutorial();blade.Board.BeginTurn();
            bool sawHealing=false;
            for(int i=0;i<400&&!blade.TrialConfirmationPending;i++)
            {
                int cursor=blade.Cursor;Assert.IsTrue(CampaignFlowTests.ClickGuide(blade),CampaignFlowTests.Diagnostic(blade));
                if(cursor==2&&blade.Cursor>cursor)
                {Assert.AreEqual(20,blade.Player.Health);Assert.IsTrue(((BladesingingLogic)blade.Player.Character).Singing);sawHealing=true;}
            }
            Assert.IsTrue(sawHealing);Assert.IsFalse(((BladesingingLogic)blade.Player.Character).Singing);
        }
        [Test] public void Every_trial_and_retry_can_resolve_a_complete_battle()
        {
            for(int level=1;level<=40;level++)
            {
                var original=CampaignLesson.CreateV3(level);
                if(original.UsesClassicSetup)
                {
                    original.BeginTutorial();original.Board.BeginTurn();original.CaptureTrial();
                    for(int i=0;i<30&&!original.CanRetryTrial;i++){Assert.IsTrue(CampaignFlowTests.ClickGuide(original));original.CaptureTrial();}
                }
                else{Guide(original);original.ConfirmTrial();}
                var lesson=original.RetryTrial();var game=lesson.Game;var board=lesson.Board;
                foreach(var p in lesson.Seats)board.BindPlayer(p,CampaignLesson.NewAi(game),(IPlayerChoiceSink)game.GetChoiceProvider(p));
                for(int i=0;i<4000&&!game.IsGameOver;i++)
                {
                    lesson.Tick();
                    Assert.IsTrue(board.Phase==BoardPhase.ChoosingAttackerHand?board.MakeAutomaticMove():board.ResolvePendingAiChoice(),CampaignFlowTests.Diagnostic(lesson));
                }
                lesson.Tick();Assert.IsTrue(game.IsGameOver,CampaignFlowTests.Diagnostic(lesson));Assert.IsTrue(lesson.IsFinished);
                Assert.IsFalse(original.Game.IsGameOver,"实战克隆不能回写原局 L"+level);
                Assert.AreEqual(game.Winner!=null&&game.IsTeammate(game.Winner,lesson.Player),lesson.IsComplete);
                Console.WriteLine("V3 COMBAT PASS L"+level+" rounds="+game.RoundCount);
            }
        }
        [Test] public void Heavy_fighter_demonstrates_attack_and_defense_without_champion()
        {
            var l=CampaignLesson.CreateV3(1);l.BeginTutorial();l.Board.BeginTurn();
            for(int i=0;i<80&&l.Cursor<2;i++)Assert.IsTrue(CampaignFlowTests.ClickGuide(l),CampaignFlowTests.Diagnostic(l));
            Assert.AreEqual("story_heavy_fighter",l.Player.Definition.Id);
            Assert.AreEqual(SpecialLogicType.None,l.Player.Definition.Logic);
            Assert.AreEqual(2,l.Player.Character.AttackBonus);Assert.AreEqual(2,l.Player.Character.DefenseBonus);
            Assert.AreEqual(6,l.Seats[1].Health);Assert.AreEqual(18,l.Player.Health);
        }
        [Test] public void Retry_restores_trial_model_and_delegates_without_reapplying_rewards()
        {
            var l=CampaignLesson.CreateV3(22);Guide(l);l.ConfirmTrial();
            int hp=l.Player.Health,left=l.Player.LeftHand.Value,round=l.Game.RoundCount;
            int seat=l.Game.DistanceBetween(l.Player,l.Seats[1]);
            l.Player.TakeDamage(100);var retry=l.RetryTrial();
            Assert.IsFalse(object.ReferenceEquals(l.Game,retry.Game));Assert.AreEqual(hp,retry.Player.Health);
            Assert.AreEqual(left,retry.Player.LeftHand.Value);Assert.AreEqual(round,retry.Game.RoundCount);
            Assert.AreEqual(seat,retry.Game.DistanceBetween(retry.Player,retry.Seats[1]));
            Assert.AreEqual(1,retry.HornCount);Assert.IsTrue(retry.CanRetryTrial);
            foreach(var p in retry.Seats)retry.Board.BindPlayer(p,CampaignLesson.NewAi(retry.Game),(IPlayerChoiceSink)retry.Game.GetChoiceProvider(p));
            for(int i=0;i<80&&!retry.Game.IsGameOver;i++)
                Assert.IsTrue(retry.Board.Phase==BoardPhase.ChoosingAttackerHand?retry.Board.MakeAutomaticMove():retry.Board.ResolvePendingAiChoice(),CampaignFlowTests.Diagnostic(retry));
            Assert.AreEqual(0,l.Player.Health,"克隆回调不得修改旧局");
            var again=retry.RetryTrial();Assert.AreEqual(hp,again.Player.Health);Assert.AreEqual(1,again.HornCount);
        }
    }
}
