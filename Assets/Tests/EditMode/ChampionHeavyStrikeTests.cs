using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class ChampionHeavyStrikeTests
    {
        private static PlayerState Make(CharacterDefinition d) => CharacterFactory.CreatePlayer(d.DisplayName, d);

        [Test]
        public void Real_operation_uses_post_operation_sum_and_multiplies_before_stats()
        {
            var a=Make(CharacterDefinition.Champion); var b=Make(CharacterDefinition.Blank);
            a.LeftHand.SetValue(1); a.RightHand.SetValue(6); b.LeftHand.SetValue(3);
            var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var result=game.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(4,a.LeftHand.Value);
            Assert.AreEqual(7,result.DamageDealt,"4×1.5+1=7; multiplying after attack bonus would incorrectly give 8.");
            Assert.AreEqual(1,((ISkillUseCounter)a.Character).SkillUseCount);
            Assert.IsTrue(string.Join("",game.TakeSkillLog()).Contains("十点重击"));
        }

        [Test]
        public void Pre_operation_sum_ten_does_not_trigger_if_attack_sum_is_not_ten()
        {
            var a=Make(CharacterDefinition.Champion); var b=Make(CharacterDefinition.Blank);
            a.LeftHand.SetValue(6); a.RightHand.SetValue(4); b.LeftHand.SetValue(2);
            var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var result=game.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(9,result.DamageDealt,"8+4=12 after operation: ordinary 8+1 damage.");
            Assert.AreEqual(0,((ISkillUseCounter)a.Character).SkillUseCount);
        }

        [Test]
        public void Four_six_and_eight_scale_to_six_nine_and_twelve_before_attack_and_defense()
        {
            foreach(int value in new[]{4,6,8})
            {
                var a=Make(CharacterDefinition.Champion);
                var b=Make(new CharacterDefinition("test_defense_two","guard",SpecialLogicType.None,defenseBonus:2));
                a.LeftHand.SetValue(value); a.RightHand.SetValue(10-value);
                var attack=new AttackContext(a,b,a.LeftHand,value); attack.Resolve(null,null);
                Assert.AreEqual(value*3/2+1-2,attack.AppliedDamage);
                Assert.AreEqual(1.5f,attack.AdditionalBaseDamageMultiplier);
            }
        }

        [Test]
        public void Attack_snapshot_and_independent_multiplier_survive_other_hooks_changing_hands()
        {
            var a=Make(CharacterDefinition.Champion); var b=Make(CharacterDefinition.Blank);
            a.LeftHand.SetValue(8); a.RightHand.SetValue(2);
            var attack=new AttackContext(a,b,a.LeftHand,8);
            a.RightHand.SetValue(3);
            attack.DamageMultiplier=1.5f; attack.Resolve(null,null);
            Assert.AreEqual(19,attack.AppliedDamage,"8×1.5×1.5+1=19, one rounding pass before stats.");
            Assert.AreEqual(10,attack.AttackerHandSumAtStart);
        }

        [Test]
        public void Dual_champion_and_swashbuckler_stack_in_both_orders()
        {
            foreach(bool championFirst in new[]{true,false})
            {
                var a=DualGeneralFactory.Create("dual",championFirst?CharacterDefinition.Champion:CharacterDefinition.Swashbuckler,
                    championFirst?CharacterDefinition.Swashbuckler:CharacterDefinition.Champion);
                var b=Make(CharacterDefinition.Blank); a.LeftHand.SetValue(8);a.RightHand.SetValue(2);
                var choices=new ScriptedChoiceProvider();choices.ChooseHand(a.RightHand);
                var attack=new AttackContext(a,b,a.LeftHand,8){AttackerProvider=choices,SourceHandValueBefore=1,TargetHandValueBefore=7};
                attack.Resolve(choices,null);
                Assert.AreEqual(19,attack.AppliedDamage);
                Assert.AreEqual(3,a.RightHand.Value);
                Assert.AreEqual(AttackDamageType.Slashing,attack.AttackType,"双将沿用原有伤害类型规则，重击不改写类型。");
            }
        }

        [Test]
        public void Champion_bonus_does_not_make_an_unempowered_dual_swashbuckler_attack_piercing()
        {
            var a=DualGeneralFactory.Create("dual",CharacterDefinition.Champion,CharacterDefinition.Swashbuckler);
            var b=Make(CharacterDefinition.Blank);a.LeftHand.SetValue(4);a.RightHand.SetValue(6);
            var attack=new AttackContext(a,b,a.LeftHand,4);attack.Resolve(null,null);
            Assert.AreEqual(7,attack.AppliedDamage);
            Assert.AreEqual(AttackDamageType.Slashing,attack.AttackType);
            Assert.AreEqual(1f,attack.DamageMultiplier);
        }

        [Test]
        public void Shield_still_blocks_heavy_strike_and_extra_attacks_do_not_gain_it()
        {
            var a=Make(CharacterDefinition.Champion);var b=Make(CharacterDefinition.Blank);
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Formed);
            var attack=new AttackContext(a,b,a.LeftHand,4);attack.Resolve(null,null);
            Assert.IsTrue(attack.WasBlocked);Assert.AreEqual(0,attack.AppliedDamage);
            var extra=new AttackContext(a,b,a.LeftHand,4){IsSpecialAttack=true};extra.Resolve(null,null);
            Assert.AreEqual(5,extra.AppliedDamage);Assert.AreEqual(1f,extra.AdditionalBaseDamageMultiplier);
            var reflected=new AttackContext(a,b,a.LeftHand,4){IsReflected=true};reflected.Resolve(null,null);
            Assert.AreEqual(5,reflected.AppliedDamage);
        }

        [Test]
        public void Multiplier_does_not_leak_into_next_attack_and_match_reset_clears_count()
        {
            var a=Make(CharacterDefinition.Champion);var b=Make(CharacterDefinition.Blank);
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);
            new AttackContext(a,b,a.LeftHand,4).Resolve(null,null);
            a.RightHand.SetValue(3);
            var next=new AttackContext(a,b,a.LeftHand,4);next.Resolve(null,null);
            Assert.AreEqual(5,next.AppliedDamage);Assert.AreEqual(1f,next.AdditionalBaseDamageMultiplier);
            Assert.AreEqual(1,((ISkillUseCounter)a.Character).SkillUseCount);
            a.Character.OnMatchReset(a,null);Assert.AreEqual(0,((ISkillUseCounter)a.Character).SkillUseCount);
        }

        [Test]
        public void Ai_projects_result_plus_other_hand_and_uses_same_damage_order()
        {
            var a=Make(CharacterDefinition.Champion);var b=Make(CharacterDefinition.Blank);
            a.LeftHand.SetValue(1);a.RightHand.SetValue(2);
            var ai=new GreedyChoiceProvider();var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(13,ai.TotalLethalDamage(move,a,b,8));
            a.RightHand.SetValue(3);Assert.AreEqual(9,ai.TotalLethalDamage(move,a,b,8));
        }
    }
}
