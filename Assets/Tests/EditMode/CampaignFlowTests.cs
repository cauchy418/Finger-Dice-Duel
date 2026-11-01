using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class CampaignFlowTests
    {
        public static bool ClickGuide(CampaignLesson l)
        {
            var b=l.Board;
            if(!b.IsHumanChooser)return b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
            if(b.PendingEffect==HandEffect.BattleMasterManeuver)
            {
                foreach(var status in new[]{StatusEffect.Bleed,StatusEffect.Limp,StatusEffect.Stunned,StatusEffect.KnockedDown})if(l.AllowManeuver(status)&&b.ChooseBattleMasterManeuver(status))return true;
                if(l.AllowManeuver(StatusEffect.None))return b.DeclineBattleMasterManeuver();return false;
            }
            if(b.PendingSkillOptions.Count>0){foreach(var o in b.PendingSkillOptions)if(l.AllowsOption(o.Value))return b.ChooseSkillOption(o.Value);return false;}
            if(b.PendingEffect==HandEffect.SchoolOfEvocationDamageType){foreach(DamageType d in Enum.GetValues(typeof(DamageType)))if(l.AllowsDamageType(d)&&b.ChooseDamageType(d))return true;return false;}
            if(b.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType){foreach(var r in ResistanceNames.Choices)if(l.AllowsResistance(r)&&b.ChooseResistanceType(r))return true;return false;}
            if(b.PendingWizardChoice!=null){foreach(var h in b.PendingWizardChoice.Candidates)if(l.AllowsHand(h)&&b.ChooseWizardHand(h))return true;return l.AllowsDecline&&b.DeclinePendingChoice();}
            if(b.PendingRequest.HasValue){foreach(var h in b.PendingRequest.Value.Candidates)if(l.AllowsHand(h)&&b.ClickHand(h))return true;if(l.AllowsAccept&&b.AcceptPendingChoice())return true;return l.AllowsDecline&&b.DeclinePendingChoice();}
            if(!string.IsNullOrEmpty(b.CurrentCampaignAction))return b.UseCampaignAction();
            if(b.CanCastle&&l.AllowsCastling&&b.Phase==BoardPhase.ChoosingAttackerHand)return b.BeginCastling();
            if(b.CanGiveUpAttack&&l.AllowsDecline)return b.GiveUpAttack();
            if(b.CanSkipTurn&&l.AllowsSkip)return b.SkipTurn();
            var suggested=l.SuggestedHand;if(suggested!=null&&b.ClickHand(suggested))return true;
            foreach(var p in l.Seats)foreach(var h in p.Hands)if(l.AllowsHand(h)&&b.ClickHand(h))return true;
            if(l.AllowsAccept&&b.AcceptPendingChoice())return true;
            if(l.AllowsDecline&&b.DeclinePendingChoice())return true;
            return false;
        }
        public static void DriveGuide(CampaignLesson l)
        {
            l.BeginTutorial();l.Board.BeginTurn();var game=l.Game;var players=(PlayerState[])l.Seats.Clone();
            for(int i=0;i<1600&&!l.TeachingComplete&&!game.IsGameOver;i++)
            {
                l.Tick();if(l.TeachingComplete)break;
                Assert.IsTrue(ClickGuide(l),Diagnostic(l));
                Assert.IsTrue(ReferenceEquals(game,l.Game));for(int j=0;j<players.Length;j++)Assert.IsTrue(ReferenceEquals(players[j],l.Seats[j]));
            }
            l.Tick();Assert.IsTrue(l.TeachingComplete,Diagnostic(l));Assert.IsFalse(l.IsComplete,"教学不是通关");Assert.IsFalse(game.IsGameOver,"应在原战场接自由战");
            Assert.AreEqual(1,l.HornCount);foreach(var p in players)Assert.IsFalse(p.TrainingProtected);
            Assert.IsTrue(game.RoundCount<=30,"教学应保留至少20轮自主战斗，实际轮次"+game.RoundCount);
        }
        public static string Diagnostic(CampaignLesson l)
        {
            var hands=new List<string>();foreach(var p in l.Seats)hands.Add(p.Name+":"+p.LeftHand.Value+"/"+p.RightHand.Value+" HP"+p.Health);
            var log=l.Board.Log;int start=Math.Max(0,log.Count-6);var tail=new List<string>();for(int i=start;i<log.Count;i++)tail.Add(log[i]);
            return "L"+l.LevelNumber+" step="+l.Cursor+" "+l.Step?.Text+" phase="+l.Board.Phase+" effect="+l.Board.PendingEffect+" q="+l.Board.PendingWizardChoice?.Prompt+" actor="+l.Board.ActivePlayer?.Name+" option="+l.RequiredOption+" hp="+string.Join(";",hands)+" log="+string.Join(" | ",tail);
        }
        private static void Chapter(int first,int last)
        {
            var failures=new List<string>();for(int n=first;n<=last;n++)if(n!=5&&n%10!=0)
            {try{StatusRules.ResetInteractionRandomForTest(418);DriveGuide(CampaignLesson.Create(n));Console.WriteLine("GUIDE PASS L"+n);}catch(Exception e){failures.Add(e.Message);}}
            Assert.IsTrue(failures.Count==0,string.Join("\n",failures));
        }
        [Test] public void Random_status_branches_keep_all_guides_finishable()
        {for(int seed=0;seed<8;seed++)for(int n=1;n<=29;n++)if(n!=5&&n%10!=0){StatusRules.ResetInteractionRandomForTest(8100+seed);DriveGuide(CampaignLesson.Create(n));}}
        [Test] public void Chapter_one_continues_without_rebuilding(){Chapter(1,9);}
        [Test] public void Chapter_two_continues_without_rebuilding(){Chapter(11,19);}
        [Test] public void Chapter_three_continues_without_rebuilding(){Chapter(21,29);}
        [Test] public void Finale_values_and_roles_match_the_new_design()
        {Assert.AreEqual(40,CampaignLesson.Create(10).Seats[1].MaximumHealth);Assert.AreEqual(40,CampaignLesson.Create(20).Seats[1].MaximumHealth);var l=CampaignLesson.Create(30);Assert.AreEqual(36,l.Seats[1].MaximumHealth);Assert.AreEqual(28,l.Seats[2].MaximumHealth);Assert.AreEqual(CharacterDefinition.OathOfTheAncients,l.Seats[3].Definition);}
        [Test] public void Campaign_player_copy_contains_story_and_teaching_instead_of_implementation_notes()
        {
            var banned=new[]{"重置","重开一局","后台","预置","教学完成没有","同一场战斗","正常AI","未新增通关"};
            for(int n=1;n<=30;n++)
            {
                var l=CampaignLesson.Create(n);var copy=l.OpeningText+l.SummaryText+l.FailureText+l.Narration+l.RejectionHint;
                foreach(var step in l.Steps)copy+=step.Text;
                foreach(var phrase in banned)Assert.IsFalse(copy.Contains(phrase),"L"+n+"："+phrase);
                foreach(var p in l.Seats)if(p.Character is CampaignNpcLogic npc)
                {Assert.IsFalse(p.Definition.Description.Contains("剧情专用"));Assert.IsTrue(p.Definition.Description.Contains(npc.AbilitySummary));}
            }
        }
        [Test] public void Every_lesson_can_finish_real_combat_without_resetting()
        {
            for(int n=1;n<=30;n++)
            {
                StatusRules.ResetInteractionRandomForTest(418+n);var l=CampaignLesson.Create(n);
                if(l.UsesClassicSetup){l.BeginTutorial();l.Board.BeginTurn();}else DriveGuide(l);
                var g=l.Game;var b=l.Board;
                foreach(var p in l.Seats)b.BindPlayer(p,CampaignLesson.NewAi(g),(IPlayerChoiceSink)g.GetChoiceProvider(p));
                for(int i=0;i<3000&&!g.IsGameOver;i++)
                {l.Tick();bool acted=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();Assert.IsTrue(acted,Diagnostic(l));}
                l.Tick();Assert.IsTrue(g.IsGameOver,Diagnostic(l));Assert.IsTrue(l.IsFinished);
                Assert.AreEqual(g.Winner!=null&&g.IsTeammate(g.Winner,l.Player),l.IsComplete);Assert.IsTrue(ReferenceEquals(g,l.Game));
                Console.WriteLine("COMBAT PASS L"+n+" rounds="+g.RoundCount+" win="+l.IsComplete);
            }
        }
        [Test] public void Protection_and_obstacle_actions_are_scoped_and_cost_an_action()
        {
            var l=CampaignLesson.Create(4);l.BeginTutorial();l.Board.BeginTurn();l.Player.TakeDamage(100);Assert.AreEqual(1,l.Player.Health);
            l.SkipTutorial();int before=l.Board.CompletedActions;Assert.AreEqual(2,l.Game.DistanceBetween(l.Player,l.Seats[1]));
            Assert.IsTrue(l.Board.UseCampaignAction());Assert.AreEqual(before+1,l.Board.CompletedActions);Assert.AreEqual(1,l.Game.DistanceBetween(l.Player,l.Seats[1]));
            Assert.IsFalse(l.Player.TrainingProtected);Assert.AreEqual(0,l.HornCount);
            var ordinary=CharacterFactory.CreatePlayer("普通角色",CharacterDefinition.Champion);ordinary.TakeDamage(100);Assert.AreEqual(0,ordinary.Health);
        }
    }
}
