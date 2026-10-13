using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **赌徒**的核心：1d9 的结果直接作为本次行动结果。
    ///
    /// 规则（玩家逐条确认）：
    ///   4/6/8 攻击、9 回血、5 合盾，其余无效果。
    ///   打谁在结果出来之后再选。
    /// </summary>
    [TestFixture]
    public class GamblerTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match(CharacterDefinition defB,
                                         out SimpleChoiceProvider sinkA,
                                         out SimpleChoiceProvider sinkB)
        {
            sinkA = new SimpleChoiceProvider();
            sinkB = new SimpleChoiceProvider();

            return new GameManager(Make("赌", CharacterDefinition.Gambler),
                                   Make("B", defB), sinkA, sinkB);
        }

        /// <summary>固定点数的随机源（让"赌出 N"可确定性复现）。</summary>
        private sealed class FixedRandom : System.Random
        {
            private readonly int _value;
            public FixedRandom(int value) { _value = value; }

            protected override double Sample() => 0.5;

            public override int Next(int minValue, int maxValue) => _value;
        }

        [Test]
        public void Gambler_stats_and_range()
        {
            PlayerState g = Make("赌", CharacterDefinition.Gambler);

            Assert.AreEqual(0, g.Character.AttackBonus);
            Assert.AreEqual(0, g.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(g), "赌徒攻击距离 1。");
        }

        [Test]
        public void Gamble_four_six_eight_produce_attacks()
        {
            foreach (int roll in new[] { 4, 6, 8 })
            {
                GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
                PlayerState gambler = game.PlayerA;
                PlayerState foe = game.PlayerB;

                int before = foe.Health;

                TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                                 new FixedRandom(roll), foe.LeftHand);

                Assert.True(r.WasGamble, $"{roll} 应记为赌博。");
                Assert.AreEqual(roll, r.GambleRoll, "应记录骰子点数。");
                Assert.AreEqual(roll, r.ResultValue, "骰子点数即本次结果。");
                Assert.True(foe.Health < before,
                    $"赌出 {roll} 应造成伤害（当前效果 {r.Effect}）。");
            }
        }

        [Test]
        public void Gamble_five_forms_a_shield()
        {
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;

            TurnResult r = null;
            string crash = null;
            try
            {
                r = game.ExecuteGamble(gambler, gambler.LeftHand, new FixedRandom(5));
            }
            catch (System.NullReferenceException ex)
            {
                crash = ex.StackTrace;
            }

            Assert.True(crash == null, "赌出 5 时抛了空引用：" + crash);
            Assert.NotNull(r);
            Assert.AreEqual(5, r.ResultValue);
            Assert.AreEqual(HandEffectType.Shield, r.Effect, "赌出 5 应是护盾。");
            Assert.AreEqual(ShieldState.Formed, gambler.LeftHand.State,
                "赌出 5 后运算源应成为成型护盾。");
        }

        [Test]
        public void Gamble_nine_heals_instead_of_attacking()
        {
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;
            PlayerState foe = game.PlayerB;

            gambler.TakeDamage(8);
            int gamblerBefore = gambler.Health;
            int foeBefore = foe.Health;

            TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                             new FixedRandom(9), foe.LeftHand);

            Assert.AreEqual(9, r.ResultValue);
            Assert.AreEqual(HandEffectType.Heal, r.Effect, "赌出 9 应是回复。");
            Assert.True(gambler.Health > gamblerBefore, "赌出 9 应回血。");
            Assert.AreEqual(foeBefore, foe.Health, "回复不该伤到对手。");
        }

        [Test]
        public void Gamble_one_two_three_seven_have_no_effect()
        {
            // 玩家只说了 4/6/8 攻击、9 回血、5 合盾 ——
            // 其余数字按既有规则就是"无效果"，所以不该造成任何伤害。
            foreach (int roll in new[] { 1, 2, 3, 7 })
            {
                GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
                PlayerState gambler = game.PlayerA;
                PlayerState foe = game.PlayerB;

                int foeBefore = foe.Health;
                int selfBefore = gambler.Health;

                TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                                 new FixedRandom(roll), foe.LeftHand);

                Assert.AreEqual(roll, r.ResultValue);
                Assert.AreEqual(HandEffectType.None, r.Effect,
                    $"赌出 {roll} 应为无效果（实际 {r.Effect}）。");
                Assert.AreEqual(foeBefore, foe.Health, $"赌出 {roll} 不该造成伤害。");
                Assert.AreEqual(selfBefore, gambler.Health, $"赌出 {roll} 不该回血。");
            }
        }

        [Test]
        public void Gamble_effect_rules_match_normal_operations()
        {
            // "赌出的数字"与"运算出的数字"必须走**同一套**效果规则，
            // 否则两处会漂移（这条测试是那个约定的守卫）。
            for (int value = 1; value <= 9; value++)
            {
                Assert.AreEqual(Hand.GetEffectType(value), GamblerLogic.EffectOf(value),
                    $"{value} 的效果应与普通运算一致。");
            }
        }

        [Test]
        public void Only_gamblers_can_gamble()
        {
            GameManager game = new GameManager(Make("士", CharacterDefinition.Soldier),
                                               Make("B", CharacterDefinition.Soldier),
                                               new SimpleChoiceProvider(),
                                               new SimpleChoiceProvider());

            bool threw = false;
            try
            {
                game.ExecuteGamble(game.PlayerA, game.PlayerA.LeftHand, new FixedRandom(5));
            }
            catch (System.InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw, "非赌徒不该能赌博。");
        }

        [Test]
        public void Must_gamble_flag_resets_on_a_new_match()
        {
            // 强制赌博是跨回合状态，但新开一局必须清零
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;

            gambler.MustGamble = true;
            game.ResetMatch();

            Assert.False(gambler.MustGamble, "新开一局应清掉强制赌博状态。");
        }
    }
}
