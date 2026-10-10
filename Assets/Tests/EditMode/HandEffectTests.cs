using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 数字效果映射：4/6/8 攻击、9 回血、5 合盾、1/2/3/7 无效果。
    /// 对应 RULES.md 第 3 节。
    /// </summary>
    [TestFixture]
    public class HandEffectTests
    {
        [Test]
        public void Four_six_eight_are_attacks_with_matching_damage()
        {
            Assert.AreEqual(HandEffectType.Attack4, Hand.GetEffectType(4));
            Assert.AreEqual(HandEffectType.Attack6, Hand.GetEffectType(6));
            Assert.AreEqual(HandEffectType.Attack8, Hand.GetEffectType(8));

            // 伤害等于效果值本身
            Assert.AreEqual(4, (int)HandEffectType.Attack4);
            Assert.AreEqual(6, (int)HandEffectType.Attack6);
            Assert.AreEqual(8, (int)HandEffectType.Attack8);
        }

        [Test]
        public void Five_is_shield()
        {
            Assert.AreEqual(HandEffectType.Shield, Hand.GetEffectType(5));
        }

        [Test]
        public void Nine_is_heal()
        {
            Assert.AreEqual(HandEffectType.Heal, Hand.GetEffectType(9));
        }

        [Test]
        public void One_two_three_seven_have_no_effect()
        {
            Assert.AreEqual(HandEffectType.None, Hand.GetEffectType(1));
            Assert.AreEqual(HandEffectType.None, Hand.GetEffectType(2));
            Assert.AreEqual(HandEffectType.None, Hand.GetEffectType(3));
            Assert.AreEqual(HandEffectType.None, Hand.GetEffectType(7));
        }

        [Test]
        public void Zero_is_not_a_valid_effect_value()
        {
            Assert.AreEqual(HandEffectType.None, Hand.GetEffectType(0));
        }

        [Test]
        public void PerformOperation_forming_shield_sets_state()
        {
            var hand = new Hand(1);
            HandEffectType effect = hand.PerformOperation(4); // 1+4=5

            Assert.AreEqual(5, hand.Value);
            Assert.AreEqual(HandEffectType.Shield, effect);
            Assert.AreEqual(ShieldState.Formed, hand.State);
            Assert.True(hand.IsShield);
        }

        [Test]
        public void PerformOperation_leaving_shield_value_clears_previous_shield()
        {
            var hand = new Hand(1);
            hand.PerformOperation(4);                       // -> 5，成盾
            Assert.AreEqual(ShieldState.Formed, hand.State);

            hand.PerformOperation(1);                       // 5+1=6 -> 攻击
            Assert.AreEqual(6, hand.Value);
            Assert.AreEqual(ShieldState.None, hand.State, "不再是 5 就不应还是盾。");
        }

        [Test]
        public void Heal_amount_constant_is_nine()
        {
            Assert.AreEqual(9, Hand.HealAmount);
        }

        [Test]
        public void Heal_is_capped_at_max_health()
        {
            var player = new PlayerState("A");
            Assert.AreEqual(20, player.Health);

            player.TakeDamage(5);
            Assert.AreEqual(15, player.Health);

            player.Heal(Hand.HealAmount);
            Assert.AreEqual(20, player.Health, "回血不应超过上限。");

            player.Heal(100);
            Assert.AreEqual(20, player.Health);
        }

        [Test]
        public void Damage_cannot_make_health_negative()
        {
            var player = new PlayerState("A");
            player.TakeDamage(999);
            Assert.AreEqual(0, player.Health);
            Assert.True(player.IsDead);
            Assert.False(player.IsAlive);
        }
    }
}
