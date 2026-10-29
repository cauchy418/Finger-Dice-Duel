using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class MageTowerTests
    {
        // 显式运行的难度采样，不纳入每次快速回归。
        public static void RunFinaleProbe(string output)
        {
            var rows=new System.Collections.Generic.List<string>{"role,games,player_wins,boss_wins,draws,safety_aborts"};
            foreach(var role in MageTowerChapter.FinaleRoster)
            {
                int wins=0,losses=0,draws=0,aborts=0;
                for(int seed=0;seed<200;seed++)
                {
                    var a=CharacterFactory.CreatePlayer(role.DisplayName,role);
                    var b=new PlayerState("法师幻影",new MagePhantomLogic(),32);b.BindDefinition(MageTowerChapter.Phantom);
                    var ca=new GreedyChoiceProvider{UseLearnedFocus=true};var cb=new GreedyChoiceProvider{UseLearnedFocus=true};
                    ca.ApplyDifficulty(AiDifficulty.Hard);cb.ApplyDifficulty(AiDifficulty.Hard);
                    var g=new GameManager(a,b,ca,cb);ca.Game=g;cb.Game=g;
                    StatusRules.ResetInteractionRandomForTest(seed+4100);
                    var start=new MatchStartController(g,new Random(seed+7300));start.Roll();
                    var advisor=new DiceSplitAdvisor();
                    while(!start.IsComplete)
                    {
                        var other=ReferenceEquals(start.Assignee,a)?b:a;
                        var pick=advisor.Choose(start.Assignee.Definition,other.Definition,start.Candidates,start.HasInfoAdvantage?start.PreviousSplit:null);
                        Assert.IsTrue(start.Submit(pick));
                    }
                    g.ForceActivePlayerForTest(start.FirstToAct);
                    var result=new MatchRunner{RecordHistory=false,MaxTurns=500}.Run(g,ca,cb);
                    if(result.HitTurnLimit)aborts++;
                    else if(result.WinnerIsPlayerA)wins++;
                    else if(result.WinnerIsPlayerB)losses++;
                    else draws++;
                }
                rows.Add(role.Id+",200,"+wins+","+losses+","+draws+","+aborts);
                Console.WriteLine(rows[rows.Count-1]);
                Assert.AreEqual(0,aborts,"终战不能卡在无合法操作的模拟器安全出口");
            }
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output)));
            System.IO.File.WriteAllLines(output,rows);
        }
        private static bool ResolveHuman(MageTowerLesson l)
        {
            var b=l.Board;var g=l.Game;
            if(b.PendingWizardChoice!=null)
            {
                var q=b.PendingWizardChoice;
                if(q.Options.Count>0)
                {
                    int value=l.RequiredOption??0;
                    if(l.LevelNumber==11)value=l.SampleIndex==0?(int)AttackDamageType.Fire:1;
                    if(l.LevelNumber==18)value=q.Prompt.Contains("幻术默发")?(l.SampleIndex==0?1:0):l.SampleIndex==2?1:0;
                    if(l.LevelNumber==15)value=g.TurnCount<=1?(int)StatusEffect.Disabled:g.TurnCount==4?(int)StatusEffect.Charm:0;
                    return b.ChooseSkillOption(value);
                }
                if(l.LevelNumber==16)
                {
                    Hand hand=null;
                    if(q.Prompt.Contains("免费攻击")||q.Prompt.Contains("惧怖收割"))hand=g.TurnCount==0||g.TurnCount==1?l.Soldier.LeftHand:null;
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
                    return b.ChooseResistanceType(l.Sample.Key=="sustained"&&g.TurnCount==2?AttackDamageType.Fire:l.Sample.ResistanceType);
                if(b.PendingEffect==HandEffect.SchoolOfEvocationDamageType)
                {
                    return b.ChooseDamageType(l.Sample.CastingType);
                }
                if(b.PendingEffect==HandEffect.SchoolOfNecromancyBlock)return b.ClickHand(l.Player.LeftHand);
                if(b.PendingEffect==HandEffect.SchoolOfAbjurationResistanceTarget)return b.ClickHand(l.Seats[l.Sample.Key=="sustained"&&g.TurnCount==2?1:l.Sample.ResistanceTargetSeat].LeftHand);
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
                {Assert.IsTrue(b.BeginCastling());Assert.IsTrue(b.CancelCastling());Assert.IsTrue(b.BeginCastling());return b.CompleteCastling(l.Seats[3]);}
                return b.ClickHand(l.Player.GetHand(turn.Source));
            }
            if(b.Phase==BoardPhase.ChoosingTargetHand)return b.ClickHand(l.Seats[turn.Target].GetHand(turn.Addend));
            return false;
        }
        public static void Drive(MageTowerLesson l)
        {
            l.BeginTutorial();l.Board.BeginTurn();
            for(int guard=0;guard<160;guard++)
            {
                l.Tick();if(l.IsFinished)break;
                AuditGuidedChoice(l);
                bool ok=l.Board.IsHumanChooser?ResolveHuman(l):l.Board.Phase==BoardPhase.ChoosingAttackerHand?l.Board.MakeAutomaticMove():l.Board.ResolvePendingAiChoice();
                Assert.IsTrue(ok,"无法推进 "+l.LevelNumber+"/"+l.Sample.Key+" turn="+l.Game.TurnCount+" phase="+l.Board.Phase+" effect="+l.Board.PendingEffect+" hint="+l.Board.Hint);
            }
            l.Tick();
            Assert.IsTrue(l.IsComplete,"目标失败 "+l.LevelNumber+"/"+l.Sample.Key+" turn="+l.Game.TurnCount+" hp="+l.Player.Health+"/"+l.Soldier.Health+" hands="+l.Player.LeftHand.Value+"/"+l.Player.RightHand.Value+" log="+string.Join(" | ",l.Board.Log));
        }
        private static void AuditGuidedChoice(MageTowerLesson l)
        {
            var b=l.Board;var rule=b.TutorialInput;if(rule==null||!b.IsHumanChooser)return;
            int turn=l.Game.TurnCount;
            if(b.PendingSkillOptions.Count>0)
            {
                int allowed=0;
                foreach(var option in b.PendingSkillOptions)
                    if(rule.AllowsOption(option.Value))allowed++;
                    else Assert.IsFalse(b.ChooseSkillOption(option.Value),"非教学选项不得绕过禁用按钮");
                Assert.AreEqual(1,allowed,"每次命名技能必须有唯一指定答案");
            }
            else if(b.PendingEffect==HandEffect.SchoolOfEvocationDamageType)
            {
                int allowed=0;foreach(var type in StatusNames.AllTypes)
                    if(rule.AllowsDamageType(type))allowed++;else Assert.IsFalse(b.ChooseDamageType(type));
                Assert.AreEqual(1,allowed);
            }
            else if(b.PendingEffect==HandEffect.SchoolOfAbjurationResistanceType)
            {
                int allowed=0;foreach(var type in ResistanceNames.Choices)
                    if(rule.AllowsResistance(type))allowed++;else Assert.IsFalse(b.ChooseResistanceType(type));
                Assert.AreEqual(1,allowed);
            }
            else if(b.PendingWizardChoice!=null||b.PendingRequest.HasValue)
            {
                if(!rule.AllowsDecline)Assert.IsFalse(b.DeclinePendingChoice(),"必须发动时不能放弃");
                if(!rule.AllowsAccept)Assert.IsFalse(b.AcceptPendingChoice(),"必须继续攻击时不能再次放弃攻击");
                foreach(var p in l.Game.Players)foreach(var hand in p.Hands)
                    if(!rule.AllowsHand(hand))Assert.IsFalse(b.ClickHand(hand),"非指定目标或手牌不得接受");
            }
            Assert.AreEqual(turn,l.Game.TurnCount,"误选不推进回合");
        }
        [Test] public void All_guided_samples_finish_through_real_board_choices()
        {for(int level=11;level<20;level++)for(int sample=0;sample<MageTowerChapter.Samples(level).Count;sample++)Drive(MageTowerLesson.Create(level,sample));}

        [Test] public void Samples_have_separate_state_and_stable_completion_keys()
        {
            for(int level=11;level<20;level++)
            {
                var keys=new System.Collections.Generic.HashSet<string>();
                for(int i=0;i<MageTowerChapter.Samples(level).Count;i++)
                {
                    var a=MageTowerLesson.Create(level,i);var b=MageTowerLesson.Create(level,i);
                    Assert.IsTrue(keys.Add(a.Sample.Key));Assert.IsFalse(ReferenceEquals(a.Game,b.Game));
                    Assert.IsFalse(a.IsComplete);Assert.IsFalse(a.Started);
                    Assert.AreEqual("FingerDiceDuel.Adventure.Level"+level+".Completed",a.CompletionKey);
                    Assert.AreEqual("Adventure/guide_arcane_codex",a.GuideAvatarKey);
                }
            }
        }
        [Test] public void Every_evocation_sample_includes_a_guard_observation_turn()
        {
            for(int i=0;i<MageTowerChapter.Samples(12).Count;i++)
            {
                var lesson=MageTowerLesson.Create(12,i);Assert.AreEqual(4,lesson.Sample.Turns.Count);
                Assert.AreEqual(1,lesson.Sample.Turns[3].Actor);Drive(lesson);
                Assert.IsTrue(ReferenceEquals(lesson.Board.ActivePlayer,lesson.Player));
                if(i==0)Assert.AreEqual(11,lesson.Soldier.Health);
                if(i==2)Assert.AreEqual(13,lesson.Soldier.Health);
                if(i==5)Assert.AreEqual(12,lesson.Soldier.Health);
                if(i==7)Assert.AreEqual(14,lesson.Soldier.Health);
                if(lesson.Sample.Key=="freeze")Assert.AreEqual(1,lesson.Soldier.StatusTurnsLeft(StatusEffect.Frozen));
            }
        }
        [Test] public void Finale_only_accepts_wizards_and_permanent_affinities_survive_hits()
        {
            Assert.AreEqual(9,MageTowerChapter.FinaleRoster.Count);
            foreach(var role in MageTowerChapter.FinaleRoster)
            {
                var l=MageTowerLesson.Create(20,0,role);
                Assert.IsTrue(l.UsesClassicSetup);Assert.AreEqual(32,l.Soldier.Health);
                Assert.IsTrue(l.Soldier.HasPermanentResistance(AttackDamageType.Fire));
                Assert.IsTrue(l.Soldier.HasPermanentResistance(AttackDamageType.Cold));
                Assert.IsTrue(l.Soldier.HasPermanentImmunity(AttackDamageType.Poison));
                l.Soldier.ClearTemporaryDamageAffinities();
                Assert.IsTrue(l.Soldier.HasResistance(AttackDamageType.Fire));
                Assert.IsTrue(l.Soldier.HasImmunity(AttackDamageType.Poison));
            }
            bool rejected=false;try{MageTowerLesson.Create(20,0,CharacterDefinition.EldritchKnight);}catch(ArgumentException){rejected=true;}
            Assert.IsTrue(rejected);
        }
        [Test] public void Phantom_circuit_counts_whole_normal_health_hits_and_decay_blocks_heal()
        {
            var l=MageTowerLesson.Create(20);var g=l.Game;var boss=l.Soldier;
            boss.SetHealthExact(20);l.Player.SetMaxHealth(100);l.Player.SetHealthExact(100);
            var skill=(MagePhantomLogic)boss.Character;
            for(int i=0;i<2;i++)new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(2,skill.CircuitProgress);Assert.AreEqual(20,boss.Health);
            boss.ApplyStatus(StatusEffect.Decay,2);
            new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(0,skill.CircuitProgress);Assert.AreEqual(20,boss.Health);
            boss.RemoveStatus(StatusEffect.Decay);
            for(int i=0;i<3;i++)new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(23,boss.Health);
            boss.ApplyStatus(StatusEffect.Disabled,2);
            for(int i=0;i<3;i++)new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(0,skill.CircuitProgress);
        }
        [Test] public void Reaction_with_no_new_status_does_not_restore_the_base_status()
        {
            foreach(var type in new[]{AttackDamageType.Fire,AttackDamageType.Cold})
            {
                var a=new PlayerState("法术护卫",new TowerGuardLogic(type));
                var b=CharacterFactory.CreatePlayer("靶",CharacterDefinition.Blank);
                var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                b.ApplyStatus(type==AttackDamageType.Fire?StatusEffect.Wet:StatusEffect.Burn,2);
                var hit=new AttackContext(a,b,a.LeftHand,6,g);hit.Resolve(g.ChoiceProviderB,g);
                Assert.AreEqual(9,hit.AppliedDamage);
                Assert.IsFalse(b.HasStatus(StatusEffect.Burn));Assert.IsFalse(b.HasStatus(StatusEffect.Frost));
            }
        }
        [Test] public void Phantom_barrier_false_life_and_sheep_do_not_advance_circuit()
        {
            var l=MageTowerLesson.Create(20);var boss=l.Soldier;var g=l.Game;
            var skill=(MagePhantomLogic)boss.Character;
            l.Player.GainFalseLife(10);
            new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(0,skill.CircuitProgress);
            l.Player.ClearFalseLife();l.Player.LeftHand.SetValue(5);l.Player.LeftHand.SetShieldState(ShieldState.Formed);
            new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(0,skill.CircuitProgress);
            new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(1,skill.CircuitProgress);
            boss.TryPolymorph();new AttackContext(boss,l.Player,boss.LeftHand,4,g).Resolve(g.ChoiceProviderA,g);
            Assert.AreEqual(1,skill.CircuitProgress);boss.EndPolymorph();
            Assert.AreEqual(1,((MagePhantomLogic)boss.Character).CircuitProgress);
        }
    }
}
