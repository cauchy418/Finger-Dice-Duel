using System.Collections.Generic;
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
    }
}
