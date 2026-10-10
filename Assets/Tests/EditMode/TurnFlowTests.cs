using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 回合契约：只有行动方的手会改变、合法运算枚举、锁手限制、胜负与回合切换。
    /// 对应 RULES.md 第 5 节与第 7 节。
    /// </summary>
    [TestFixture]
    public class TurnFlowTests
    {
        private GameManager _game;
        private PlayerState _a;
        private PlayerState _b;

        [SetUp]
        public void SetUp()
        {
            _a = new PlayerState("A");
            _b = new PlayerState("B");
            _game = new GameManager(_a, _b, new ScriptedChoiceProvider(), new ScriptedChoiceProvider());
        }

        // ---------- 契约：只有行动方的手改变 ----------

        [Test]
        public void Only_actor_hand_changes_target_hand_is_untouched()
        {
            // A 的左手 1 + B 的右手 1 = 2
            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.RightHand);

            Assert.AreSame(_a.LeftHand, r.ActedHand);
            Assert.AreSame(_b.RightHand, r.TargetHand);
            Assert.AreEqual(2, _a.LeftHand.Value, "行动方的手应更新为结果。");
            Assert.AreEqual(1, _b.RightHand.Value, "对方的手不应改变（RULES.md 5.2）。");
            Assert.AreEqual(1, _b.LeftHand.Value, "对方另一只手也不应改变。");
            Assert.AreEqual(1, _a.RightHand.Value, "行动方另一只手不应改变。");
        }

        [Test]
        public void TurnResult_records_before_and_after_values()
        {
            TestSetup.SetHand(_a.LeftHand, 3);

            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.AreEqual(3, r.ActedHandValueBefore, "应记录运算前的值。");
            Assert.AreEqual(4, r.ResultValue, "3+1=4");
            Assert.AreEqual(HandEffectType.Attack4, r.Effect);
        }

        // ---------- 合法运算枚举 ----------

        [Test]
        public void Two_hands_each_side_yield_four_legal_moves()
        {
            var moves = _game.GetLegalMoves(_a);
            Assert.AreEqual(4, moves.Count, "2 只自己的手 × 2 只对方的手 = 4 步。");
        }

        [Test]
        public void Locked_hand_is_excluded_from_legal_moves()
        {
            _a.LeftHand.Lock();

            var moves = _game.GetLegalMoves(_a);
            Assert.AreEqual(2, moves.Count, "锁住一只手后只剩另一只手的 2 步。");

            for (int i = 0; i < moves.Count; i++)
                Assert.AreSame(_a.RightHand, moves[i].ActedHand, "被锁的手不应出现。");
        }

        // ---------- 非法运算必须抛出（不静默失败） ----------

        [Test]
        public void Cannot_use_own_hand_as_target()
        {
            Assert.Throws<InvalidOperationException>(
                () => _game.ExecuteTurn(_a, _a.LeftHand, _a.RightHand),
                "运算目标必须是对方的手。");
        }

        [Test]
        public void Cannot_use_locked_hand_as_source()
        {
            _a.LeftHand.Lock();

            Assert.Throws<InvalidOperationException>(
                () => _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand),
                "被锁定的手不能作为运算源（RULES.md 5.3）。");
        }

        [Test]
        public void Locked_hand_can_still_be_used_as_target()
        {
            _b.LeftHand.Lock();

            // 被锁的手可以挨打，只是不能主动出手
            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);
            Assert.AreEqual(2, r.ResultValue);
            Assert.AreEqual(1, _b.LeftHand.Value);
        }

        [Test]
        public void Illegal_move_does_not_advance_the_turn()
        {
            _a.LeftHand.Lock();

            Assert.Throws<InvalidOperationException>(
                () => _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand));

            Assert.AreEqual(0, _game.TurnCount, "非法运算不应推进回合计数。");
            Assert.AreSame(_a, _game.CurrentPlayer, "非法运算不应切换回合。");
        }

        [Test]
        public void Cannot_act_after_game_over()
        {
            _b.TakeDamage(20);                                // B: 0
            _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);   // 触发胜负判定

            Assert.True(_game.IsGameOver, "B 已归零，对局应已结束。");
            Assert.Throws<InvalidOperationException>(
                () => _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand),
                "比赛结束后不应还能运算。");
        }

        [Test]
        public void Legal_move_check_reports_reason()
        {
            bool ok = _game.IsLegalMove(_a, _a.LeftHand, _b.LeftHand, out string reason);
            Assert.True(ok);
            Assert.Null(reason);

            ok = _game.IsLegalMove(_a, _a.LeftHand, _a.RightHand, out reason);
            Assert.False(ok);
            Assert.NotNull(reason);
        }

        // ---------- 回合切换 ----------

        [Test]
        public void Current_player_alternates_and_turn_count_increments()
        {
            Assert.AreSame(_a, _game.CurrentPlayer);
            Assert.AreEqual(0, _game.TurnCount);

            _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);
            Assert.AreSame(_b, _game.CurrentPlayer, "回合应交给对方。");
            Assert.AreEqual(1, _game.TurnCount);

            _game.ExecuteTurn(_b, _b.LeftHand, _a.LeftHand);
            Assert.AreSame(_a, _game.CurrentPlayer);
            Assert.AreEqual(2, _game.TurnCount);
        }

        // ---------- 胜负判定 ----------

        [Test]
        public void Dropping_to_zero_ends_the_game()
        {
            TestSetup.SetHand(_a.LeftHand, 3);                 // 3+1 = 4 -> 攻击
            TestSetup.SetHealth(_b, 4);

            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.AreEqual(HandEffectType.Attack4, r.Effect);
            Assert.AreEqual(4, r.DamageDealt);
            Assert.AreEqual(0, _b.Health);
            Assert.True(r.GameOver);
            Assert.True(_game.IsGameOver);
            Assert.AreSame(_a, _game.Winner);
            Assert.AreSame(_a, r.Winner);
        }

        [Test]
        public void Turn_count_does_not_increment_on_the_finishing_turn()
        {
            TestSetup.SetHealth(_b, 1);
            TestSetup.SetHand(_a.LeftHand, 3);                 // 3+1 = 4 -> 击杀

            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.True(r.GameOver);
            Assert.AreEqual(0, _game.TurnCount, "结束回合不应推动回合计数。");
        }

        [Test]
        public void First_to_die_loses_when_only_one_side_reaches_zero()
        {
            TestSetup.SetHand(_a.LeftHand, 3);                 // 3+1 = 4
            TestSetup.SetHealth(_b, 4);

            _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.AreEqual(0, _b.Health);
            Assert.AreSame(_a, _game.Winner, "只有 B 归零时，A 获胜。");
            Assert.False(_a.IsDead);
        }

        [Test]
        public void Mutual_death_is_a_draw()
        {
            // 双方都归零时，赢家必须是 null（平局），而不是先手方或后手方。
            // 注意：一旦 IsGameOver 为 true 就不能再运算，所以必须在
            // "双方都已是 0" 之后立刻执行那一次结算。
            _a.TakeDamage(20);
            _b.TakeDamage(20);

            Assert.AreEqual(0, _a.Health);
            Assert.AreEqual(0, _b.Health);

            _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.True(_game.IsGameOver);
            Assert.Null(_game.Winner, "双方同时归零应为平局。");
        }
    }
}
