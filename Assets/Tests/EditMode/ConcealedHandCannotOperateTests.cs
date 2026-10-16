using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **暗置（伏击）的手不能主动发起运算** —— 玩家实测的 bug 回归测试。
    ///
    /// 玩家报告：
    ///   "猎人伏击的手还能主动发起运算"
    ///   "我先拿伏击的手主动做了运算破除了伏击，这是不被允许的，
    ///    伏击的手不能主动发起运算，看上去和被锁是一样的"
    ///
    /// 根因：**两层口径不一致**。
    ///   领域层（`GetLegalMoves` / `IsLegalMove`）用的是 `Hand.CanBeOperated`
    ///   （= 未锁 **且** 未暗置）；
    ///   而界面层 `BoardController.CanClick` 的 `ChoosingAttackerHand` 分支
    ///   还停在 `!hand.IsLocked` —— **只认得"被锁"**。
    ///   于是暗置的手在领域层已被拒绝，**界面上却仍然点得动**。
    ///
    /// \u26a0\ufe0f 这个 bug 的**连锁后果**比"多点一下"严重得多：
    ///    用暗置的手运算会**破除伏击**（手被改写成结果值、`
    ///    HunterLogic` 的暗置状态随即失效），
    ///    那只手就不再是暗置的 —— 于是**对手接着就能把它当加数**。
    ///    我第一版加的"暗置手不能被当被加数"校验因此形同虚设。
    /// </summary>
    [TestFixture]
    public class ConcealedHandCannotOperateTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        /// <summary>建一局：猎人 vs 战士，猎人已进入伏击（暗置左手）。</summary>
        private static BoardController BuildAmbushing(out GameManager game,
                                                      out PlayerState hunter)
        {
            hunter = Make("猎人", CharacterDefinition.Hunter);
            var fighter = Make("战士", CharacterDefinition.Fighter);

            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            fighter.LeftHand.SetValue(1);
            fighter.RightHand.SetValue(1);

            game = new GameManager(hunter, fighter,
                                   new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());

            // ⚠️ `CurrentPlayer` 是私有 setter，测试不能直接指定 ——
            //    而这个测试要验证的正是"行动方自己点自己的手"。
            //    所以让战士先走一步，把回合交给猎人。
            //
            // ⚠️ 这一步必须**合法**：不能拿自己的两只手运算
            //    （"一只手不能加自己"那条校验会拒绝，我第一版就踩了这个）。
            //    所以让战士碰**猎人的右手** 2：1 + 2 = 3，无效果，猎人的手不变。
            game.ExecuteTurn(fighter, fighter.LeftHand, hunter.RightHand);
            Assert.AreSame(hunter, game.CurrentPlayer,
                "前提：走完战士的回合后应由猎人行动。");

            // 猎人进入伏击：暗置左手
            ((HunterLogic)hunter.Character).EnterAmbush(hunter.LeftHand);
            Assert.True(hunter.LeftHand.IsConcealed, "前提：左手已暗置。");

            var board = new BoardController(game, new SimpleChoiceProvider(),
                                            new SimpleChoiceProvider());

            // ⚠️ **必须调 BeginTurn()**：`BoardController.ActivePlayer` 是**缓存字段**，
            //    只有 BeginTurn() 会把它从 game.CurrentPlayer 取过来。
            //    不调的话 ActivePlayer 是 null，CanClick 一律返回 false ——
            //    我第一版就踩了这个，表现为"未暗置的手也说点不动"。
            board.BeginTurn();
            Assert.AreSame(hunter, board.ActivePlayer,
                "前提：BeginTurn 后行动方应是猎人。");

            return board;
        }

        [Test]
        public void Concealed_hand_is_not_clickable_as_the_operation_source()
        {
            BoardController board = BuildAmbushing(out _, out PlayerState hunter);

            // 处在"选运算源"阶段时，暗置的手必须点不动
            Assert.False(board.CanClick(hunter.LeftHand),
                "暗置（伏击）的手不能当运算源 —— 界面上必须点不动。");

            // 对照：另一只手正常可点
            Assert.True(board.CanClick(hunter.RightHand),
                "未暗置的那只手仍应可以正常发起运算。"
                + " [诊断] phase=" + board.Phase
                + " active=" + (board.ActivePlayer?.Name ?? "?")
                + " 期望active=" + hunter.Name
                + " 右手可运算=" + hunter.RightHand.CanBeOperated
                + " 右手值=" + hunter.RightHand.Value
                + " 己方所有=" + hunter.Owns(hunter.RightHand)
                + " 游戏结束=" + board.Game.IsGameOver);
        }

        [Test]
        public void Concealed_hand_is_rejected_by_the_domain_layer_too()
        {
            // 界面层与领域层必须**同一口径** —— 这条守住领域层那一侧
            GameManager game = BuildAmbushing(out _, out PlayerState hunter)
                .Game;

            Assert.False(game.IsLegalMove(hunter, hunter.LeftHand, hunter.RightHand, out _),
                "领域层也应拒绝用暗置的手发起运算。");

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(hunter);
            for (int i = 0; i < moves.Count; i++)
                Assert.False(ReferenceEquals(moves[i].ActedHand, hunter.LeftHand),
                    "暗置的手不应出现在合法运算源里。");
        }

        [Test]
        public void Concealed_hand_stays_concealed_because_it_cannot_be_used()
        {
            // 玩家描述的连锁后果：用暗置的手运算会破除伏击，
            // 于是那只手不再是暗置的、对手接着就能拿它当加数。
            // 现在它点不动，所以伏击**不会被自己破掉**。
            BoardController board = BuildAmbushing(out GameManager game,
                                                   out PlayerState hunter);
            var fighter = game.PlayerB;

            // 尝试用暗置的手运算：应被拒绝，且伏击状态不受影响
            bool clicked = board.ClickHand(hunter.LeftHand);

            Assert.False(clicked, "点击暗置的手应被拒绝。");
            Assert.True(hunter.LeftHand.IsConcealed, "伏击不应被自己的点击破除。");
            Assert.True(((HunterLogic)hunter.Character).IsAmbushing, "仍应处于伏击状态。");

            // 对手也不能拿它当被加数
            Assert.False(game.IsLegalMove(fighter, fighter.LeftHand, hunter.LeftHand, out _),
                "暗置的手不能被对手当成运算数字。");
        }

        [Test]
        public void Ambush_is_not_broken_by_its_own_rejected_click()
        {
            // 反复点击暗置的手都不应改变任何状态（与"被锁"的手一致）
            BoardController board = BuildAmbushing(out _, out PlayerState hunter);

            int before = hunter.LeftHand.Value;
            for (int i = 0; i < 3; i++) board.ClickHand(hunter.LeftHand);

            Assert.AreEqual(before, hunter.LeftHand.Value, "暗置的手数值不应改变。");
            Assert.True(hunter.LeftHand.IsConcealed, "反复点击不应解除暗置。");
        }
    }
}
