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
    [TestFixture]
    public class TatteredBannersTests
    {
        public static void RunFinaleProbe(string output)
        {
            var rows=new List<string>{"role,games,player_team_wins,enemy_team_wins,draws,safety_aborts,mean_actions"};
            foreach(var role in TatteredBannersChapter.FinaleRoster)
            {
                int wins=0,losses=0,draws=0,aborts=0,actions=0;
                for(int seed=0;seed<200;seed++)
                {
                    var preview=TatteredBannersLesson.Create(30,0,role);var human=preview.Player;
                    var setup=TwoOnTwoStartController.RollAndBegin(preview.Seats,new[]{0,1,1,0},new Random(seed+9300));
                    setup.AutoResolve(p=>false,TwoOnTwoStartController.DefaultAiSplit);setup.ApplyToPlayers();
                    var seats=new PlayerState[4];for(int s=0;s<4;s++)seats[s]=setup.PlayerAtSeat(s+1);
                    var l=TatteredBannersLesson.AttachFinale(seats,human);var board=l.Board;
                    foreach(var p in seats)board.BindPlayer(p,TatteredBannersLesson.NewAi(l.Game),l.Game.GetChoiceProvider(p) as IPlayerChoiceSink);
                    StatusRules.ResetInteractionRandomForTest(seed+13100);board.BeginTurn();
                    bool stuck=false;
                    for(int guard=0;guard<4000&&!l.Game.IsGameOver;guard++)
                    {
                        bool ok=board.Phase==BoardPhase.ChoosingAttackerHand?board.MakeAutomaticMove():board.ResolvePendingAiChoice();
                        if(!ok||!string.IsNullOrEmpty(board.LastIllegalOperation)){stuck=true;break;}
                    }
                    actions+=board.CompletedActions;
                    if(stuck||!l.Game.IsGameOver)aborts++;
                    else if(l.Game.Winner==null)draws++;
                    else if(l.Game.IsTeammate(human,l.Game.Winner))wins++;
                    else losses++;
                }
                rows.Add(role.Id+",200,"+wins+","+losses+","+draws+","+aborts+","+(actions/200.0).ToString("F2",System.Globalization.CultureInfo.InvariantCulture));
                Console.WriteLine(rows[rows.Count-1]);Assert.AreEqual(0,aborts,"终战AI代理不能卡在安全出口");
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output)));
            System.IO.File.WriteAllLines(output,rows);
        }
        public static void Drive(TatteredBannersLesson lesson,bool audit=true)
        {
            var b=lesson.Board;lesson.BeginTutorial();b.BeginTurn();
            for(int guard=0;guard<240;guard++)
            {
                lesson.Tick();if(lesson.IsFinished)break;
                bool ok=false;
                if(b.IsHumanChooser)
                {
                    var rule=b.TutorialInput;
                    if(b.PendingSkillOptions.Count>0)
                    {
                        foreach(var option in b.PendingSkillOptions)
                        {if(rule.AllowsOption(option.Value)){ok=b.ChooseSkillOption(option.Value);break;}
                         else if(audit)Assert.IsFalse(b.ChooseSkillOption(option.Value));}
                    }
                    else
                    {
                        var hand=lesson.SuggestedHand;
                        if(audit)
                        {
                            int before=b.CompletedActions;
                            foreach(var p in lesson.Game.Players)foreach(var h in p.Hands)
                                if(!rule.AllowsHand(h))Assert.IsFalse(b.ClickHand(h),"错点不得推进");
                            Assert.AreEqual(before,b.CompletedActions);
                            if(!rule.AllowsDecline)Assert.IsFalse(b.DeclinePendingChoice());
                        }
                        ok=b.PendingWizardChoice!=null?b.ChooseWizardHand(hand):hand!=null&&b.ClickHand(hand);
                    }
                }
                else ok=b.Phase==BoardPhase.ChoosingAttackerHand?b.MakeAutomaticMove():b.ResolvePendingAiChoice();
                Assert.IsTrue(ok,"无法推进 "+lesson.LevelNumber+"/"+lesson.Sample.Key+" actions="+b.CompletedActions+" phase="+b.Phase+" effect="+b.PendingEffect+" hint="+b.Hint+" log="+string.Join(" | ",b.Log));
                Assert.IsTrue(string.IsNullOrEmpty(b.LastIllegalOperation),b.LastIllegalOperation);
            }
            lesson.Tick();Assert.IsTrue(lesson.IsComplete,"结果不符 "+lesson.LevelNumber+"/"+lesson.Sample.Key+" actions="+b.CompletedActions+" hp="+string.Join(",",Array.ConvertAll(lesson.Seats,p=>p.Health.ToString()))+" log="+string.Join(" | ",b.Log));
        }
        [Test]public void All_samples_use_real_multiplayer_board_and_finish()
        {for(int level=21;level<30;level++)for(int i=0;i<TatteredBannersChapter.Samples(level).Count;i++)Drive(TatteredBannersLesson.Create(level,i));}
        [Test]public void Borrowing_changes_only_source_and_target_is_independent()
        {var l=TatteredBannersLesson.Create(21);Drive(l);Assert.AreEqual(6,l.Player.LeftHand.Value);Assert.AreEqual(4,l.Player.RightHand.Value);Assert.AreEqual(5,l.Seats[3].LeftHand.Value);Assert.AreEqual(2,l.Seats[2].LeftHand.Value);Assert.AreEqual(8,l.Seats[1].Health);}
        [Test]public void Devotion_uses_two_distinct_shocks_and_ai_protects_real_teammate()
        {var l=TatteredBannersLesson.Create(23);Drive(l);Assert.AreEqual(14,l.Seats[1].Health);var a=TatteredBannersLesson.Create(24,2);Drive(a);Assert.AreEqual(6,a.Player.Health);Assert.AreEqual(20,a.Seats[1].Health);Assert.AreEqual(18,a.Seats[2].Health);}
        [Test]public void Restraint_sample_works_with_either_random_hand()
        {for(int seed=0;seed<12;seed++){StatusRules.ResetInteractionRandomForTest(seed);Drive(TatteredBannersLesson.Create(25,0));}}
        [Test]public void Reward_actions_do_not_advance_normal_round_or_turn()
        {var l=TatteredBannersLesson.Create(29,3);Drive(l);Assert.AreEqual(3,l.Board.CompletedActions);Assert.AreEqual(1,l.Game.TurnCount);Assert.AreEqual(1,l.Game.DistanceBetween(l.Player,l.Seats[2]));Assert.IsFalse(l.Board.IsExtraAction);}
        [Test]public void Samples_are_isolated_and_keys_are_stable()
        {
            var keys=new HashSet<string>();
            for(int n=21;n<30;n++)for(int i=0;i<TatteredBannersChapter.Samples(n).Count;i++)
            {var a=TatteredBannersLesson.Create(n,i);var b=TatteredBannersLesson.Create(n,i);Assert.IsFalse(ReferenceEquals(a.Game,b.Game));Assert.IsTrue(keys.Add(TatteredBannersChapter.SampleKey(n,a.Sample.Key)));Assert.IsFalse(a.IsComplete);Assert.IsFalse(a.Started);Assert.AreEqual(TatteredBannersChapter.Background,a.BattleBackgroundKey);}
        }
        [Test]public void Finale_accepts_only_paladins_and_wins_as_a_team()
        {
            Assert.AreEqual(6,TatteredBannersChapter.FinaleRoster.Count);
            foreach(var role in TatteredBannersChapter.FinaleRoster)
            {var l=TatteredBannersLesson.Create(30,0,role);Assert.IsTrue(l.UsesClassicSetup);Assert.AreEqual(24,l.Seats[1].Health);Assert.AreEqual(20,l.Seats[2].Health);Assert.AreEqual(CharacterDefinition.OathOfTheAncients,l.Seats[3].Definition);Assert.IsTrue(l.Game.IsTeammate(l.Player,l.Seats[3]));Assert.IsTrue(l.Board.TutorialInput==null);}
            bool rejected=false;try{TatteredBannersLesson.Create(30,0,CharacterDefinition.SchoolOfEvocation);}catch(ArgumentException){rejected=true;}Assert.IsTrue(rejected);
        }
        [Test]public void Classic_seating_preserves_human_teammate_and_first_seat()
        {
            var humanSeats=new HashSet<int>();
            for(int seed=0;seed<32;seed++)
            {
                var preview=TatteredBannersLesson.Create(30);var human=preview.Player;var ally=preview.Seats[3];
                var setup=TwoOnTwoStartController.RollAndBegin(preview.Seats,new[]{0,1,1,0},new Random(seed+9300));
                setup.AutoResolve(p=>false,TwoOnTwoStartController.DefaultAiSplit);setup.ApplyToPlayers();
                var seats=new PlayerState[4];for(int i=0;i<4;i++)seats[i]=setup.PlayerAtSeat(i+1);
                int left=human.LeftHand.Value,right=human.RightHand.Value;
                var l=TatteredBannersLesson.AttachFinale(seats,human);l.Board.BeginTurn();
                Assert.IsTrue(ReferenceEquals(human,l.Player));Assert.IsTrue(l.Game.IsTeammate(human,ally));
                Assert.IsTrue(ReferenceEquals(seats[0],l.Board.ActivePlayer));Assert.AreEqual(left,human.LeftHand.Value);Assert.AreEqual(right,human.RightHand.Value);
                humanSeats.Add(Array.IndexOf(seats,human));
            }
            Assert.AreEqual(4,humanSeats.Count,"玩家必须可能坐到四个席位，不能锁在一位");
        }
    }
}
