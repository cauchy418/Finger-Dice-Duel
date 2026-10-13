using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **武僧**与**巨人**的行为。
    ///
    /// 规则都由玩家逐条确认（含 8 个澄清问题的答复），这里把每条钉成断言。
    /// </summary>
    [TestFixture]
    public class MonkAndGiantTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match(CharacterDefinition defA, CharacterDefinition defB,
                                         out SimpleChoiceProvider sinkA,
                                         out SimpleChoiceProvider sinkB)
        {
            sinkA = new SimpleChoiceProvider();
            sinkB = new SimpleChoiceProvider();

            return new GameManager(Make("A", defA), Make("B", defB), sinkA, sinkB);
        }

        // ============================================================
        // 武僧
        // ============================================================

        [Test]
        public void Monk_stats_and_range()
        {
            PlayerState m = Make("僧", CharacterDefinition.Monk);

            Assert.AreEqual(0, m.Character.AttackBonus);
            Assert.AreEqual(0, m.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(m), "武僧攻击距离 1。");
        }

        [Test]
        public void Monk_five_becomes_taichi_instead_of_a_shield()
        {
            // 玩家确认："5 无法合成盾牌，而是改为太极形态"
            GameManager game = Match(CharacterDefinition.Monk, CharacterDefinition.Soldier,
                                     out _, out _);
            PlayerState monk = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 1 + 4 = 5
            TestSetup.SetHand(monk.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 4);

            game.ExecuteTurn(monk, monk.LeftHand, foe.LeftHand);

            Assert.AreEqual(5, monk.LeftHand.Value);
            Assert.AreEqual(ShieldState.Taichi, monk.LeftHand.State,
                "5 应进入太极形态，而不是护盾。");
            Assert.False(monk.LeftHand.IsShield, "太极**不是**盾。");
            Assert.False(monk.HasDoubleShield,
                "太极不该被算作'双盾在手'（那是跳过回合的条件）。");
        }

        [Test]
        public void Monk_taichi_halves_damage_and_reflects_the_rest()
        {
            // 玩家确认："被攻击时可以把 5 变成 0，受到本次攻击应有的一半伤害，
            //            将剩余的伤害反弹给攻击方"
            GameManager game = Match(CharacterDefinition.Soldier, CharacterDefinition.Monk,
                                     out _, out _);
            PlayerState foe = game.PlayerA;      // 士兵打武僧
            PlayerState monk = game.PlayerB;

            // 武僧有一只手处于太极形态
            monk.RightHand.SetValue(5);
            monk.RightHand.SetShieldState(ShieldState.Taichi);

            int monkBefore = monk.Health;
            int foeBefore = foe.Health;

            // 士兵 3 + 1 = 4（攻击 4）；士兵攻 +1、武僧防 0 -> 实际 5 点
            foe.LeftHand.SetValue(3);
            TestSetup.SetHand(monk.LeftHand, 1);

            game.ExecuteTurn(foe, foe.LeftHand, monk.LeftHand);

            // 太极发动：那只 5 变成 0
            Assert.AreEqual(0, monk.RightHand.Value,
                "太极发动后那只手应变成 0。");
            Assert.AreEqual(ShieldState.None, monk.RightHand.State,
                "变成 0 之后不再是太极形态。");

            int monkLost = monkBefore - monk.Health;
            int foeLost = foeBefore - foe.Health;

            Assert.True(monkLost > 0, "武僧应受到一半伤害（不是完全免伤）。");
            Assert.True(foeLost > 0, "攻击方应受到反弹伤害。");

            // 减半：武僧受的是 ceil(5/2)=3，反弹 floor(5/2)=2
            Assert.AreEqual(3, monkLost, "武僧应受一半（上取整）。");
            Assert.AreEqual(2, foeLost, "攻击方应受剩余的一半。");
        }

        [Test]
        public void Monk_taichi_does_not_work_against_the_curseweaver()
        {
            // 玩家确认："无法削减并反弹咒术师的攻击"，且"5 不消耗"
            GameManager game = Match(CharacterDefinition.Curseweaver,
                                     CharacterDefinition.Monk, out _, out _);
            PlayerState foe = game.PlayerA;      // 咒术师
            PlayerState monk = game.PlayerB;

            monk.RightHand.SetValue(5);
            monk.RightHand.SetShieldState(ShieldState.Taichi);

            int monkBefore = monk.Health;
            int foeBefore = foe.Health;

            // 咒术师 3 + 1 = 4（无视护盾与正向防御加成）
            foe.LeftHand.SetValue(3);
            TestSetup.SetHand(monk.LeftHand, 1);

            game.ExecuteTurn(foe, foe.LeftHand, monk.LeftHand);

            Assert.AreEqual(5, monk.RightHand.Value,
                "对咒术师的攻击，太极的 5 **不该被消耗**。");
            Assert.AreEqual(ShieldState.Taichi, monk.RightHand.State,
                "太极形态应保持。");
            Assert.True(monk.Health < monkBefore, "武僧应正常受到伤害（不减半）。");
            Assert.AreEqual(foeBefore, foe.Health,
                "不该有反弹（咒术师不受伤）。");
        }

        [Test]
        public void Monk_zero_becomes_one_when_someone_else_uses_it_as_addend()
        {
            // 玩家确认："0 被他人触碰后会立即变成 1"，
            // 触碰 = "别人把它当**加数**用"，且"当成 1 参与运算"
            GameManager game = Match(CharacterDefinition.Soldier, CharacterDefinition.Monk,
                                     out _, out _);
            PlayerState foe = game.PlayerA;
            PlayerState monk = game.PlayerB;

            monk.RightHand.SetValue(0);          // 武僧有一只 0
            foe.LeftHand.SetValue(3);

            // 士兵拿武僧的 0 当加数 -> 3 + 0 = 3；但 0 应先变成 1 -> 3 + 1 = 4
            TurnResult r = game.ExecuteTurn(foe, foe.LeftHand, monk.RightHand);

            Assert.AreEqual(1, monk.RightHand.Value,
                "被他人当加数触碰后，0 应立即变成 1。");
            Assert.AreEqual(4, r.ResultValue,
                "而且**当成 1 参与本次运算**（3+1=4，不是 3+0=3）。");
        }

        [Test]
        public void Monk_zero_does_not_change_when_the_monk_uses_it_itself()
        {
            // "他人触碰" —— 自己用自己的 0 不算
            GameManager game = Match(CharacterDefinition.Monk, CharacterDefinition.Soldier,
                                     out _, out _);
            PlayerState monk = game.PlayerA;
            PlayerState foe = game.PlayerB;

            monk.LeftHand.SetValue(0);
            foe.LeftHand.SetValue(3);

            TurnResult r = game.ExecuteTurn(monk, monk.LeftHand, foe.LeftHand);

            Assert.AreEqual(3, monk.LeftHand.Value,
                $"自己用不算触碰，0 应正常参与运算变成 3。" +
                $"[诊断 note={game.MonkZeroTouchNote} 结果={r.ResultValue}]");
        }

        // ============================================================
        // 巨人
        // ============================================================

        [Test]
        public void Giant_stats_and_range()
        {
            PlayerState g = Make("巨", CharacterDefinition.Giant);

            Assert.AreEqual(2, g.Character.AttackBonus);
            Assert.AreEqual(2, g.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(g), "巨人攻击距离 1。");
        }

        [Test]
        public void Giant_four_six_eight_have_no_effect()
        {
            // 玩家确认："4/6/8 无法攻击"，且是"**完全无效果**"
            foreach (int target in new[] { 3, 5, 7 })   // 1+3=4, 1+5=6, 1+7=8
            {
                GameManager game = Match(CharacterDefinition.Giant,
                                         CharacterDefinition.Soldier, out _, out _);
                PlayerState giant = game.PlayerA;
                PlayerState foe = game.PlayerB;

                giant.LeftHand.SetValue(1);
                foe.LeftHand.SetValue(target);

                int foeBefore = foe.Health;
                int giantBefore = giant.Health;

                TurnResult r = game.ExecuteTurn(giant, giant.LeftHand, foe.LeftHand);

                Assert.AreEqual(HandEffectType.None, r.Effect,
                    $"巨人打出 {r.ResultValue} 应完全无效果（实际 {r.Effect}）。");
                Assert.AreEqual(foeBefore, foe.Health, "不该造成伤害。");
                Assert.AreEqual(giantBefore, giant.Health, "也不该回血。");
            }
        }

        [Test]
        public void Giant_five_makes_two_attacks_using_the_other_hand()
        {
            // 玩家确认："打出 5 时发起两段攻击，伤害点数为另一只手的点数"
            GameManager game = Match(CharacterDefinition.Giant,
                                     CharacterDefinition.Soldier, out _, out _);
            PlayerState giant = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 另一只手 = 3，运算出 5（1 + 4）
            giant.RightHand.SetValue(3);
            TestSetup.SetHand(giant.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 4);

            int foeBefore = foe.Health;

            TurnResult r = game.ExecuteTurn(giant, giant.LeftHand, foe.LeftHand);

            Assert.AreEqual(5, r.ResultValue, "前置：结果应是 5。");
            Assert.AreEqual(8, foeBefore - foe.Health,
                $"诊断：HitCount={r.HitCount} LandedHits={r.LandedHits}" +
                $" DamageDealt={r.DamageDealt} Effect={r.Effect}" +
                $" 巨人其他手={((GiantLogic)giant.Character).OtherHandValueThisOperation}" +
                $" 巨攻={giant.Character.AttackBonus} 兵防={foe.Character.DefenseBonus}");
        }

        [Test]
        public void Giant_five_does_not_form_a_shield()
        {
            // 玩家确认："5 无法合盾"
            GameManager game = Match(CharacterDefinition.Giant,
                                     CharacterDefinition.Soldier, out _, out _);
            PlayerState giant = game.PlayerA;
            PlayerState foe = game.PlayerB;

            giant.RightHand.SetValue(3);
            TestSetup.SetHand(giant.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 4);

            TurnResult r = game.ExecuteTurn(giant, giant.LeftHand, foe.LeftHand);

            Assert.AreEqual(HandEffectType.Attack6, r.Effect,
                "5 应被改写成攻击（借 Attack6 这条通路）。");
            Assert.False(giant.LeftHand.IsShield, "巨人的 5 不形成护盾。");
        }

        [Test]
        public void Giant_nine_heals_twice_the_other_hand()
        {
            // 玩家确认："打出 9 时进行两段回复，回复量为另一只手的点数"、"都回自己"
            GameManager game = Match(CharacterDefinition.Giant,
                                     CharacterDefinition.Soldier, out _, out _);
            PlayerState giant = game.PlayerA;
            PlayerState foe = game.PlayerB;

            giant.TakeDamage(15);                       // 让回血看得出来
            int before = giant.Health;

            // 另一只手 = 3，运算出 9（4 + 5）
            giant.RightHand.SetValue(3);
            TestSetup.SetHand(giant.LeftHand, 4);
            TestSetup.SetHand(foe.LeftHand, 5);

            TurnResult r = game.ExecuteTurn(giant, giant.LeftHand, foe.LeftHand);

            Assert.AreEqual(9, r.ResultValue, "前置：结果应是 9。");
            Assert.AreEqual(6, giant.Health - before,
                "两段各回 3（另一只手点数），共 6。");
            Assert.AreEqual(6, r.HealAmount, "战报里的回复量也应是 6。");
        }
    }
}
