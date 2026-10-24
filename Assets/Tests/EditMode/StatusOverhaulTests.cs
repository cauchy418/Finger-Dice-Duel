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
        public void Disabled_suppresses_attack_bonus_and_negative_defense_on_every_hit()
        {
            foreach (int attack in new[] { 0, 4 })
            foreach (int defense in new[] { 0, -3 })
            foreach (int hits in new[] { 1, 2 })
            {
                var a = Make(); var b = Make();
                a.SetDynamicBonuses(attack, 0); b.SetDynamicBonuses(0, defense);
                a.ApplyStatus(StatusEffect.Disabled);
                var ctx = new AttackContext(a, b, a.LeftHand, 8) { HitCount = hits };
                ctx.Resolve(new SimpleChoiceProvider(), null);
                Assert.AreEqual(0, ctx.AppliedDamage);
                Assert.AreEqual(20, b.Health);
                Assert.AreEqual(hits, ctx.LandedHits);
            }
        }

        [Test]
        public void Disabled_keeps_elemental_and_skill_statuses_without_dealing_damage()
        {
            var a = Make(); var b = Make();
            a.SetDynamicBonuses(5, 0); b.SetDynamicBonuses(0, -2);
            a.ApplyStatus(StatusEffect.Disabled); b.ApplyStatus(StatusEffect.KnockedDown);
            b.AddVulnerability(AttackDamageType.Fire);
            var ctx = new AttackContext(a, b, a.LeftHand, 6)
            { AttackType = AttackDamageType.Fire, AttackerStatusOverride = StatusEffect.Burn, InflictsBleed = true };
            ctx.Resolve(new SimpleChoiceProvider(), null);
            Assert.AreEqual(0, ctx.AppliedDamage); Assert.AreEqual(20, b.Health);
            Assert.True(b.HasStatus(StatusEffect.Burn)); Assert.True(b.HasStatus(StatusEffect.Bleed));
        }

        [Test]
        public void Disabled_still_breaks_shields_instead_of_cancelling_the_attack()
        {
            var a = Make(); var b = Make(); a.ApplyStatus(StatusEffect.Disabled);
            a.SetDynamicBonuses(5, 0); b.SetDynamicBonuses(0, -2);
            b.LeftHand.SetValue(5); b.LeftHand.SetShieldState(ShieldState.Formed);
            var ctx = new AttackContext(a, b, a.LeftHand, 6) { HitCount = 2 };
            ctx.Resolve(new SimpleChoiceProvider(), null);
            Assert.AreEqual(1, ctx.BlockedHits); Assert.AreEqual(1, ctx.LandedHits);
            Assert.False(b.LeftHand.IsShield); Assert.AreEqual(20, b.Health);
        }

        [Test]
        public void Disabled_suppresses_special_and_extra_true_damage_without_consuming_false_life()
        {
            var a = Make(); var b = Make(); a.ApplyStatus(StatusEffect.Disabled); b.GainFalseLife(8);
            var ctx = new AttackContext(a, b, a.LeftHand, 6)
            { IsSpecialAttack = true, IgnoreStatBonuses = true, ExtraTrueDamage = 5 };
            ctx.Resolve(new SimpleChoiceProvider(), null);
            Assert.AreEqual(0, ctx.AppliedDamage); Assert.AreEqual(20, b.Health); Assert.AreEqual(8, b.FalseLife);
        }

        [Test]
        public void Ai_disabled_damage_preview_is_zero_including_role_followups()
        {
            foreach (var def in new[] { CharacterDefinition.Blank, CharacterDefinition.Swordsman })
            {
                var a = CharacterFactory.CreatePlayer("A", def); var b = Make();
                a.ApplyStatus(StatusEffect.Disabled); a.SetDynamicBonuses(4, 0); b.SetDynamicBonuses(0, -3);
                a.RightHand.SetValue(6);
                var ai = new GreedyChoiceProvider();
                Assert.AreEqual(0, ai.TotalLethalDamage(new LegalMove(a, a.LeftHand, b.LeftHand), a, b, 6));
            }
        }

        [Test]
        public void Disabled_does_not_protect_its_owner_from_periodic_burning()
        {
            var a = Make(); var b = Make();
            var game = new GameManager(a, b, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            a.ApplyStatus(StatusEffect.Disabled, 2); a.ApplyStatus(StatusEffect.Burn, 2);
            game.ResolveStatusTurnEnd(a);
            Assert.AreEqual(17, a.Health, "失能只清零发起攻击的伤害，不能免除已有燃烧。");
        }

        [Test]
        public void Ordinary_attack_without_disabled_still_applies_stat_bonuses_per_hit()
        {
            var a = Make(); var b = Make(); a.SetDynamicBonuses(2, 0); b.SetDynamicBonuses(0, -1);
            var ctx = new AttackContext(a, b, a.LeftHand, 8) { HitCount = 2 };
            ctx.Resolve(new SimpleChoiceProvider(), null);
            Assert.AreEqual(14, ctx.AppliedDamage); Assert.AreEqual(6, b.Health);
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
