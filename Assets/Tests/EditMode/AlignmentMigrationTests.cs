using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class AlignmentMigrationTests
    {
        private static readonly string[] Expected={
            "Champion|NeutralGood",
            "Breaker|LawfulEvil",
            "SchoolOfConjuration|LawfulNeutral",
            "PathOfTheWarden|ChaoticGood",
            "LifeDomain|LawfulGood",
            "Swashbuckler|ChaoticGood",
            "PathOfTheBeast|ChaoticEvil",
            "Swordsman|LawfulNeutral",
            "WayOfTheNinja|TrueNeutral",
            "Assassin|ChaoticEvil",
            "OathOfDevotion|LawfulGood",
            "TheArchfey|ChaoticEvil",
            "ArcaneArcher|LawfulNeutral",
            "Gambler|ChaoticNeutral",
            "OathOfTheCrown|LawfulGood",
            "WayOfTheTaiChi|NeutralGood",
            "PathOfTheGiant|ChaoticEvil",
            "Thief|NeutralEvil",
            "GloomStalker|ChaoticGood",
            "Magician|ChaoticNeutral",
            "SchoolOfEvocation|TrueNeutral",
            "WildMagic|ChaoticNeutral",
            "TheGreatOldOne|NeutralEvil",
            "CollegeOfValor|LawfulGood",
            "Oathbreaker|LawfulEvil",
            "PathOfTheBerserker|NeutralEvil",
            "Tactician|ChaoticGood",
            "SchoolOfDivination|TrueNeutral",
            "SchoolOfNecromancy|NeutralEvil",
            "SchoolOfAbjuration|LawfulGood",
            "LightDomain|NeutralGood",
            "WarDomain|LawfulNeutral",
            "BattleMaster|LawfulNeutral",
            "DraconicBloodline|ChaoticNeutral",
            "RangerHunter|NeutralGood",
            "WayOfTheOpenHand|TrueNeutral",
            "WayOfTheFourElements|TrueNeutral",
            "CircleOfTheMoon|ChaoticGood",
            "CircleOfTheStars|LawfulNeutral",
            "CircleOfTheLand|TrueNeutral",
            "OathOfTheAncients|NeutralGood",
            "TheFiend|LawfulEvil",
            "CollegeOfLore|LawfulNeutral",
            "StormSorcery|ChaoticNeutral",
            "SchoolOfIllusion|ChaoticNeutral",
            "SchoolOfTransmutation|ChaoticNeutral",
            "SchoolOfEnchantment|NeutralEvil",
            "EldritchKnight|LawfulGood",
            "Bladesinging|ChaoticGood",
            "TheHexblade|NeutralEvil",
        };
        private static PlayerState Solo(CharacterDefinition d) => CharacterFactory.CreatePlayer(d.DisplayName,d);
        private static bool Attack(HandEffectType effect) => effect==HandEffectType.Attack4 || effect==HandEffectType.Attack6 || effect==HandEffectType.Attack8;
        private static bool ActiveCapability(ICharacterBehavior part,Type capability)
        {
            if(!capability.IsInstanceOfType(part)) return false;
            if(capability==typeof(IRangeBreakEffect))
                return part.GetType().GetMethod("ApplyRangeBreak").DeclaringType!=typeof(CharacterTemplate);
            return true;
        }
        private static PlayerState Pair(bool reverse,CharacterDefinition a,CharacterDefinition b)
            => DualGeneralFactory.Create("双将",reverse?b:a,reverse?a:b);
        private static GameManager Arrange(PlayerState a,int result,int other,out PlayerState b)
        {
            b=Solo(CharacterDefinition.Blank);
            a.LeftHand.SetValue(1);a.RightHand.SetValue(other);b.LeftHand.SetValue(result-1);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.BeginTurn();return g;
        }

        [Test]
        public void All_50_alignments_match_the_latest_assignment_and_cancelled_berserker_move()
        {
            Assert.AreEqual(50,Expected.Length);
            foreach(string row in Expected)
            {
                var v=row.Split('|');
                var id=typeof(CharacterDefinition).GetField(v[0]+"Id").GetRawConstantValue() as string;
                var d=CharacterDefinition.Find(id);
                Assert.AreEqual((Alignment)Enum.Parse(typeof(Alignment),v[1]),CharacterCodex.For(d).Alignment,d.Id);
                Assert.IsTrue(CharacterCodex.Filter(Alignments.LawAxisOf(CharacterCodex.For(d).Alignment),Alignments.GoodAxisOf(CharacterCodex.For(d).Alignment)).Contains(d));
            }
            Assert.AreEqual(Alignment.NeutralEvil,CharacterCodex.For(CharacterDefinition.PathOfTheBerserker).Alignment);
            Assert.IsFalse(DualGeneralFactory.SameAxis(CharacterDefinition.PathOfTheBerserker,CharacterDefinition.PathOfTheGiant));
            Assert.IsFalse(DualGeneralFactory.SameAxis(CharacterDefinition.Champion,CharacterDefinition.Swashbuckler));
            // 2026-10-23 三名新角色的同阵营配对资格
            Assert.IsTrue(DualGeneralFactory.SameAxis(CharacterDefinition.LightDomain,CharacterDefinition.WayOfTheTaiChi));
            Assert.IsTrue(DualGeneralFactory.SameAxis(CharacterDefinition.WarDomain,CharacterDefinition.BattleMaster));
            Assert.IsFalse(DualGeneralFactory.SameAxis(CharacterDefinition.LightDomain,CharacterDefinition.LifeDomain));
        }

        [Test]
        public void Current_axis_populations_produce_425_pairs_and_both_orders_can_be_built()
        {
            // 当前52子职：守序18、中立17、混乱17，共425对。
            Assert.AreEqual(18,CharacterCodex.Filter("守序",null).Count);
            Assert.AreEqual(17,CharacterCodex.Filter("中立",null).Count);
            Assert.AreEqual(17,CharacterCodex.Filter("混乱",null).Count);
            var all=DualVersusDualHarness.AllDuals();Assert.AreEqual(425,all.Count);
            int built=0;
            foreach(var pair in all)foreach(bool reverse in new[]{false,true})
            {
                var actor=Pair(reverse,pair.Primary,pair.Secondary);
                Assert.AreEqual(Solo(pair.Primary).Character.AttackBonus+Solo(pair.Secondary).Character.AttackBonus,actor.Character.AttackBonus);
                Assert.AreEqual(Solo(pair.Primary).Character.DefenseBonus+Solo(pair.Secondary).Character.DefenseBonus,actor.Character.DefenseBonus);
                Assert.AreEqual(Math.Max(CharacterFactory.MaxHealthOf(pair.Primary),CharacterFactory.MaxHealthOf(pair.Secondary)),actor.MaximumHealth);
                built++;
            }
            Assert.AreEqual(850,built);
        }

        [Test]
        public void Every_selective_rewriter_declares_only_the_results_it_actually_handles()
        {
            foreach(var def in CharacterDefinition.BuiltIn)
            {
                var part=Solo(def).Character;
                if(!(part is IHandEffectRewriter))continue;
                Assert.IsTrue(part is ISelectiveHandEffectRewriter,def.Id);
                foreach(int v in new[]{1,2,3,4,5,6,7,8,9})
                    if(!((ISelectiveHandEffectRewriter)part).HandlesResult(v) && part is IHandEffectDisplay display)
                        Assert.AreEqual(Hand.GetEffectType(v),display.DisplayedEffectOf(v),def.Id+" value "+v);
            }
        }

        [Test]
        public void Magician_and_giant_keep_two_attack_and_five_double_attack_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=Pair(reverse,CharacterDefinition.Magician,CharacterDefinition.PathOfTheGiant);
                var g=Arrange(a,2,3,out var b);var hit=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                Assert.AreEqual(4,hit.DamageDealt,"2+魔术师0+巨人2=4");Assert.AreEqual(1,hit.HitCount);
                a=Pair(reverse,CharacterDefinition.Magician,CharacterDefinition.PathOfTheGiant);
                g=Arrange(a,5,3,out b);hit=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                Assert.AreEqual(10,hit.DamageDealt,"两段各(另一手3+攻2)=10");Assert.AreEqual(2,hit.HitCount);
                Assert.AreEqual(ShieldState.None,a.LeftHand.State);
            }
        }

        [Test]
        public void Magician_and_giant_keep_dead_numbers_and_double_healing_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                foreach(int v in new[]{4,6,8})
                {
                    var a=Pair(reverse,CharacterDefinition.Magician,CharacterDefinition.PathOfTheGiant);
                    var g=Arrange(a,v,3,out var b);var turn=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                    Assert.AreEqual(HandEffectType.None,turn.Effect);Assert.AreEqual(0,turn.DamageDealt);
                    Assert.AreEqual(HandEffectType.None,((IHandEffectDisplay)a.Character).DisplayedEffectOf(v));
                }
                var healer=Pair(reverse,CharacterDefinition.Magician,CharacterDefinition.PathOfTheGiant);
                var healGame=Arrange(healer,9,3,out var foe);healer.TakeDamage(12);int before=healer.Health;
                var heal=healGame.ExecuteTurn(healer,healer.LeftHand,foe.LeftHand);
                Assert.AreEqual(6,heal.HealAmount);Assert.AreEqual(before+6,healer.Health);
            }
        }

        [Test]
        public void Magician_giant_self_add_and_target_choice_work_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=Pair(reverse,CharacterDefinition.Magician,CharacterDefinition.PathOfTheGiant);
                var g=Arrange(a,2,1,out var b);var board=new BoardController(g,new SimpleChoiceProvider());board.BeginTurn();
                Assert.IsTrue(CharacterBehaviors.ActorHasSelfAddend(a,a.RightHand));
                Assert.IsTrue(board.ClickHand(a.LeftHand));Assert.IsTrue(board.CanAct(a.RightHand));
                Assert.IsTrue(board.ClickHand(a.RightHand));
                Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);Assert.IsTrue(board.ConfirmTargetPlayer());
                Assert.AreEqual(4,board.LastTurn.DamageDealt);
            }
        }

        [Test]
        public void Attack_target_prompt_and_effect_display_agree_for_two_five_and_dead_numbers()
        {
            foreach(bool reverse in new[]{false,true})foreach(int v in new[]{2,4,5,6,8,9})
            {
                var a=Pair(reverse,CharacterDefinition.Magician,CharacterDefinition.PathOfTheGiant);
                var g=Arrange(a,v,3,out var b);var board=new BoardController(g,new SimpleChoiceProvider());board.BeginTurn();
                var context=new PreTurnChoiceContext(a,b,a.LeftHand,b.LeftHand);
                Assert.AreEqual(v,context.EffectiveResult);
                Assert.AreEqual(v==2||v==5,context.IsAttack);
                Assert.AreEqual(v==2||v==5,((IResultTargetProvider)a.Character).ResultNeedsTarget(context));
                Assert.AreEqual(context.Effect,((IHandEffectDisplay)a.Character).DisplayedEffectOf(v));
                Assert.IsTrue(board.ClickHand(a.LeftHand));Assert.IsTrue(board.ClickHand(b.LeftHand));
                Assert.AreEqual(v==2||v==5,board.Phase==BoardPhase.ChoosingTargetPlayer,"value "+v+" reverse "+reverse);
            }
        }

        [Test]
        public void Other_disjoint_rewriters_cannot_mask_partner_results()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var law=Pair(reverse,CharacterDefinition.OathOfDevotion,CharacterDefinition.Oathbreaker);
                Assert.AreEqual(HandEffectType.Shield,((IHandEffectDisplay)law.Character).DisplayedEffectOf(4));
                Assert.IsTrue(Attack(((IHandEffectDisplay)law.Character).DisplayedEffectOf(5)));
                var chaos=Pair(reverse,CharacterDefinition.GloomStalker,CharacterDefinition.Magician);
                Assert.IsTrue(Attack(((IHandEffectDisplay)chaos.Character).DisplayedEffectOf(2)));
                var neutral=Pair(reverse,CharacterDefinition.WayOfTheTaiChi,CharacterDefinition.PathOfTheBerserker);
                Assert.IsTrue(Attack(((IHandEffectDisplay)neutral.Character).DisplayedEffectOf(9)));
            }
        }

        [Test]
        public void Newly_lawful_conjuration_duals_keep_mitigation_piercing_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=Pair(reverse,CharacterDefinition.SchoolOfConjuration,CharacterDefinition.Breaker);
                var b=Solo(CharacterDefinition.WayOfTheTaiChi);a.LeftHand.SetValue(4);
                b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Taichi);
                var attack=new AttackContext(a,b,a.LeftHand,4);attack.Resolve(new GreedyChoiceProvider(),null);
                Assert.AreEqual(4,attack.AppliedDamage);Assert.AreEqual(ShieldState.Taichi,b.LeftHand.State);
                Assert.IsTrue(((IPiercesMitigation)a.Character).PiercesMitigation);
            }
        }

        [Test]
        public void Newly_chaotic_archfey_duals_keep_health_swap_skip_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=Pair(reverse,CharacterDefinition.TheArchfey,CharacterDefinition.Magician);
                var g=Arrange(a,2,3,out var b);a.TakeDamage(5);
                var board=new BoardController(g,new SimpleChoiceProvider());board.BeginTurn();
                Assert.IsTrue(board.CanSkipTurn);Assert.IsTrue(board.SkipTurn());
                Assert.AreEqual(BoardPhase.ChoosingHealthSwapTarget,board.Phase);
                Assert.IsTrue(board.ApplyHealthSwapAndEndTurn(b));
                Assert.AreEqual(a.Health,b.Health);Assert.AreEqual(1,g.TurnCount);
            }
        }

        [Test]
        public void Historical_castling_and_archfey_skip_remain_independent_choices()
        {
            foreach(bool reverse in new[]{false,true})
            {
                // 已跨阵营，只验证旧机制；正式工厂不放宽资格。
                var a=new PlayerState("历史双将",new DualGeneralBehavior(
                    CharacterFactory.CreateBehavior(reverse?CharacterDefinition.SchoolOfConjuration:CharacterDefinition.TheArchfey),
                    CharacterFactory.CreateBehavior(reverse?CharacterDefinition.TheArchfey:CharacterDefinition.SchoolOfConjuration)));
                var g=Arrange(a,2,1,out var b);a.TakeDamage(5);b.LeftHand.SetValue(1);b.RightHand.SetValue(1);
                var board=new BoardController(g,new SimpleChoiceProvider());board.BeginTurn();
                Assert.IsTrue(board.CanCastle);Assert.IsTrue(board.CanSkipTurn);
                Assert.IsTrue(board.BeginCastling());Assert.IsTrue(board.CompleteCastling(b));
                Assert.AreEqual(2,g.SeatOf(a));Assert.AreEqual(20,b.Health);Assert.AreEqual(1,g.TurnCount);
            }
        }

        [Test]
        public void Secondary_breaker_range_break_is_not_masked_by_a_default_noop()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=Pair(reverse,CharacterDefinition.SchoolOfConjuration,CharacterDefinition.Breaker);
                var b=Solo(CharacterDefinition.Blank);var sink=new SimpleChoiceProvider();
                var g=new GameManager(a,b,sink,new SimpleChoiceProvider());
                a.LeftHand.SetValue(1);a.RightHand.SetValue(5);a.RightHand.SetShieldState(ShieldState.Formed);
                b.LeftHand.SetValue(3);sink.SetChoice(HandEffect.BreakerRangeBreak,a.RightHand);
                var hit=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                Assert.AreEqual(ShieldState.Broken,a.RightHand.State);
                Assert.IsTrue(hit.DamageDealt>0);Assert.IsTrue(b.HasStatus(StatusEffect.Bleed));
            }
        }

        [Test]
        public void Audit_all_111_pairs_for_competing_number_handlers_and_real_operation_failures()
        {
            var lines=new List<string>{"主将\t副将\t结果\t效果\t伤害\t段数\t回复量\t手牌状态"};
            var clashes=new List<string>();int operations=0;
            foreach(var pair in DualVersusDualHarness.AllDuals())
            {
                var x=Solo(pair.Primary).Character;var y=Solo(pair.Secondary).Character;
                foreach(var capability in new[]{typeof(IHealOverride),typeof(IHealAmountProvider),typeof(IHealTargetAdvisor),
                    typeof(IAttackCanceller),typeof(IAttackDamageRule),typeof(IAttackRedirector),typeof(IRangeBreakEffect),typeof(ITurnSkipRule)})
                    if(ActiveCapability(x,capability)&&ActiveCapability(y,capability))
                    {
                        // 此组合在控制器显式分开体力改写与潜行等待，并有双序回归；其余竞争仍报警。
                        bool separateSkip=capability==typeof(ITurnSkipRule)
                            && (x is AssassinLogic && y is TheArchfeyLogic || x is TheArchfeyLogic && y is AssassinLogic);
                        if(!separateSkip)clashes.Add(pair.Name+" "+capability.Name);
                    }
                if(x is ISelectiveHandEffectRewriter a && y is ISelectiveHandEffectRewriter b)
                    for(int v=1;v<=9;v++)if(a.HandlesResult(v)&&b.HandlesResult(v))clashes.Add(pair.Name+" value "+v);
                foreach(bool reverse in new[]{false,true})foreach(int v in new[]{2,4,5,6,8,9})
                {
                    var actor=Pair(reverse,pair.Primary,pair.Secondary);var game=Arrange(actor,v,3,out var foe);
                    actor.TakeDamage(10);game.BeginTurn();var result=game.ExecuteTurn(actor,actor.LeftHand,foe.LeftHand);
                    lines.Add((reverse?pair.Secondary:pair.Primary).DisplayName+"\t"+(reverse?pair.Primary:pair.Secondary).DisplayName+"\t"+v+"\t"+result.Effect+"\t"+result.DamageDealt+"\t"+result.HitCount+"\t"+result.HealAmount+"\t"+actor.LeftHand.State);
                    Assert.IsTrue(result.DamageDealt>=0);Assert.IsTrue(result.HealAmount>=0);operations++;
                }
            }
            Assert.AreEqual(0,clashes.Count,"同一个数字被两个子职同时接管："+string.Join(" / ",clashes));
            Assert.AreEqual(5100,operations);
            Directory.CreateDirectory("Tools");File.WriteAllLines("Tools/dual-alignment-probes.tsv",lines);
        }
    }
}
