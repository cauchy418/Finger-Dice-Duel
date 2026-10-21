using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class StatusOverhaulTests
    {
        private static PlayerState Make()
            => CharacterFactory.CreatePlayer("测试", CharacterDefinition.Blank);

        [Test]
        public void Frost_and_wet_modify_result_and_frost_expires_into_wet()
        {
            PlayerState p = Make();
            p.ApplyStatus(StatusEffect.Frost);
            Assert.AreEqual(4, StatusRules.ApplyResultModifiers(p, 9));

            p.ApplyStatus(StatusEffect.Wet);
            Assert.AreEqual(3, StatusRules.ApplyResultModifiers(p, 9));

            p.RemoveStatus(StatusEffect.Wet);
            p.TickStatuses();
            p.TickStatuses();
            Assert.IsTrue(p.HasStatus(StatusEffect.Wet),
                "覆霜结束后应附加潮湿。");

            p.RemoveStatus(StatusEffect.Wet);
            p.ApplyStatus(StatusEffect.Dizzy);
            Assert.AreEqual(0, StatusRules.ApplyResultModifiers(p, 9));
            p.RemoveStatus(StatusEffect.Dizzy);
            p.ApplyStatus(StatusEffect.Stunned);
            Assert.AreEqual(9, StatusRules.ApplyResultModifiers(p, 9),
                "震慑只是不能攻击，不改变运算结果。");
        }

        [Test]
        public void Bleed_penalizes_both_stats_and_knockdown_raises_incoming_damage()
        {
            PlayerState p = Make();
            p.ApplyStatus(StatusEffect.Bleed);
            Assert.AreEqual(-1, StatusRules.EffectiveAttackBonus(p));
            Assert.AreEqual(-1, StatusRules.EffectiveDefenseBonus(p));
            p.ApplyStatus(StatusEffect.KnockedDown);
            Assert.AreEqual(6, StatusRules.ApplyIncomingDamage(p, 4));
        }

        [Test]
        public void Damage_type_interactions_choose_expected_status_and_multiplier()
        {
            PlayerState target = Make();
            target.ApplyStatus(StatusEffect.Wet);

            int lightning = StatusRules.ApplyDamageTypeInteraction(
                target, DamageType.Lightning, 4,
                out StatusEffect status, out _);

            Assert.AreEqual(6, lightning);
            Assert.AreEqual(StatusEffect.Shock, status);

            target.ApplyStatus(StatusEffect.Frost);
            int fire = StatusRules.ApplyDamageTypeInteraction(
                target, DamageType.Fire, 4,
                out status, out _);
            Assert.AreEqual(6, fire);
            Assert.AreEqual(StatusEffect.None, status);
        }

        [Test]
        public void Disabled_sets_attack_damage_to_zero_but_keeps_the_attack()
        {
            PlayerState attacker = Make();
            PlayerState defender = Make();
            attacker.ApplyStatus(StatusEffect.Disabled);
            attacker.LeftHand.SetValue(3);
            defender.LeftHand.SetValue(1);

            var ctx = new AttackContext(attacker, defender, attacker.LeftHand, 6);
            ctx.Resolve(new SimpleChoiceProvider(), null);

            Assert.AreEqual(0, ctx.AppliedDamage);
            Assert.AreEqual(1, ctx.HitCount);
        }

        [Test]
        public void Frozen_is_skippable_and_stunned_cannot_attack()
        {
            PlayerState a = Make();
            PlayerState b = Make();
            PlayerState c = Make();
            PlayerState d = Make();
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(
                new[] { a, b, c, d }, providers);

            a.ApplyStatus(StatusEffect.Frozen);
            Assert.False(StatusRules.CanAct(a));
            Assert.True(game.CanSkipTurnFor(a),
                "冻结角色应出现跳过回合出口。");

            a.RemoveStatus(StatusEffect.Frozen);
            a.ApplyStatus(StatusEffect.Stunned);
            Assert.False(StatusRules.CanAttackTarget(a, b, game),
                "震慑角色不能攻击。");
        }
    }
}
