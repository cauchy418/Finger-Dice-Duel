using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **玩家确认的规则**：一手合法运算都走不出来时，**允许跳过回合**。
    ///
    /// 触发场景：目盲（只能用距离正好 1 的人的手当被加数）
    /// + 邪术师的距离修正（把这一对推到 3）
    /// -> 对方**没有任何可用的被加数**（自己的手不许自己加）-> 无合法步。
    /// </summary>
    [TestFixture]
    public class NoLegalMoveAllowsSkipTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        [Test]
        public void Blind_plus_warlock_distance_leaves_no_legal_operation()
        {
            PlayerState w = Make("邪", CharacterDefinition.Warlock);
            PlayerState foe = Make("战", CharacterDefinition.Soldier);
            var game = new GameManager(w, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            // 邪术师命中 -> 距离变 3
            w.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);
            game.ExecuteTurn(w, w.LeftHand, foe.LeftHand);
            Assert.AreEqual(3, game.DistanceBetween(w, foe));

            // 给对手挂目盲：只能用"距离正好 1"的人的手当被加数
            foe.ApplyStatus(StatusEffect.Blind, PlayerState.StatusDurationTurns, w);

            Assert.IsFalse(game.HasAnyLegalOperation(foe),
                "距离 3 + 目盲 -> 一手都走不出来。");

            Assert.IsTrue(game.CanSkipTurnFor(foe),
                "玩家确认：无合法步时**可以跳过回合**。");
        }

        [Test]
        public void A_normal_position_still_has_legal_operations()
        {
            PlayerState a = Make("甲", CharacterDefinition.Soldier);
            PlayerState b = Make("乙", CharacterDefinition.Fighter);
            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            Assert.IsTrue(game.HasAnyLegalOperation(a), "正常局面当然有合法步。");
            Assert.IsFalse(game.CanSkipTurnFor(a),
                "正常局面**不该**给跳过（只有双盾在手 / 角色特例 / 无合法步才给）。");
        }

        [Test]
        public void Blind_alone_does_not_block_the_operation_in_one_versus_one()
        {
            // 1v1 初始距离 1 -> 目盲照样能用手
            PlayerState a = Make("甲", CharacterDefinition.Soldier);
            PlayerState b = Make("乙", CharacterDefinition.Fighter);
            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            a.ApplyStatus(StatusEffect.Blind, PlayerState.StatusDurationTurns, b);

            Assert.IsTrue(game.HasAnyLegalOperation(a),
                "距离 1 时目盲不影响运算。");
            Assert.IsFalse(game.CanSkipTurnFor(a));
        }
    }
}
