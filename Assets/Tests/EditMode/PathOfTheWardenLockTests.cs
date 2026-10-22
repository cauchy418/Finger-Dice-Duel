using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **野蛮人·守望者道途锁手**：`同时只能控制对手一只手` 这条限制**只在场上仅剩两人时**生效。
    ///
    /// 玩家确认的规则（新）：
    ///   · **多人局**（2v2 / 8 人局）：已经锁住某人的一只手后，
    ///     新合成一面盾**可以再锁同一个人的另一只手**
    ///   · **场上仅剩两名玩家时**（本来就 1v1，或多人局打到只剩两人）：
    ///     不能"两只手两面盾锁同一个人的两只手"
    ///
    /// 我理解的道理：1v1 里把对手双手都锁住等于让他完全无法行动，是压倒性的；
    /// 多人局里还有队友能解围，所以允许。
    ///
    /// ⚠️ 建局方式照着既有野蛮人·守望者道途测试写（`new GameManager(a, b, sink, sink)`
    ///    然后 `new BoardController(game, sink, sink)`）—— 直连
    ///    `GameManager.ExecuteTurn` 不经过"收集 -> sink"，锁手根本不会施加。
    /// </summary>
    [TestFixture]
    public class PathOfTheWardenLockTests
    {
        /// <summary>野蛮人·守望者道途是否就"锁手"发问（只看收集阶段）。</summary>
        private static bool OffersLockChoice(PlayerState pathOfTheWarden, PlayerState foe,
                                             Hand actedHand, Hand targetHand,
                                             int aliveCount)
        {
            var context = new PreTurnChoiceContext(pathOfTheWarden, foe, actedHand, targetHand)
            {
                AlivePlayerCount = aliveCount,
            };

            var requests = new List<ChoiceRequest>();
            (pathOfTheWarden.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                if (requests[i].Effect == HandEffect.PathOfTheWardenLockTarget) return true;

            return false;
        }

        /// <summary>
        /// 1v1：白板（A）与野蛮人·守望者道途（B），推进到"野蛮人·守望者道途刚合出盾、等选锁手"。
        ///
        /// 手值安排（与既有野蛮人·守望者道途测试同一套路）：
        ///   · 回合1 A 打 B：白板右 3 + 野蛮人·守望者道途左 1 = 4 -> 攻击
        ///   · 回合2 B 合盾：野蛮人·守望者道途右 4 + 白板左 1 = 5 -> 问锁手
        /// </summary>
        private static BoardController ArrangeAtLockPrompt(out PlayerState blank,
                                                          out PlayerState barb)
        {
            blank = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);
            barb = CharacterFactory.CreatePlayer("B", CharacterDefinition.PathOfTheWarden);

            var sink = new SimpleChoiceProvider();
            var game = new GameManager(blank, barb, sink, sink);
            var board = new BoardController(game, sink, sink);

            TestSetup.SetAllHands(blank, barb, 1);
            TestSetup.SetHand(blank.RightHand, 3);
            TestSetup.SetHand(barb.RightHand, 4);

            board.BeginTurn();

            board.ClickHand(blank.RightHand);
            board.ClickHand(barb.LeftHand);
            board.ConfirmTargetPlayer();

            board.ClickHand(barb.RightHand);
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();

            return board;
        }

        // ============================================================
        // 前置能力：锁手本身是好用的
        // ============================================================

        [Test]
        public void PathOfTheWarden_forms_a_shield_and_locks_the_chosen_hand()
        {
            BoardController board = ArrangeAtLockPrompt(out PlayerState blank,
                                                        out PlayerState barb);

            Assert.AreEqual(BoardPhase.ChoosingLockTarget, board.Phase,
                "野蛮人·守望者道途合盾后应停在锁手询问。");
            Assert.NotNull(barb.Character);

            Assert.True(board.ClickHand(blank.RightHand), "锁手目标应可点。");
            board.ConfirmTargetPlayer();

            Assert.True(blank.RightHand.IsLocked, "被选中的手应被锁定。");
        }

        // ============================================================
        // 收集阶段的判定：存活人数决定要不要施加"只能一只"的限制
        // ============================================================

        [Test]
        public void Lock_choice_is_offered_when_nothing_is_controlled_yet()
        {
            var barb = CharacterFactory.CreatePlayer("B", CharacterDefinition.PathOfTheWarden);
            var blank = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);

            // ⚠️ 必须凑成 **4 + 1 = 5**（合盾）才会问锁手 ——
            //    用默认的 1/1 会得到"无效果"，锁手当然不提供。
            TestSetup.SetHand(barb.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 1);

            Assert.True(OffersLockChoice(barb, blank, barb.RightHand, blank.LeftHand, 2),
                "未控制任何手时，应提供锁手。");
        }

        [Test]
        public void Four_alive_players_do_not_trigger_the_one_hand_limit()
        {
            // 关键断言：**存活 4 人**时不该判定为"仅剩两人"，
            // 于是"已控制一只手"那段早退不生效 -> 允许再锁同一个人的另一只手。
            var barb = CharacterFactory.CreatePlayer("B", CharacterDefinition.PathOfTheWarden);
            var blank = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);

            // 同样要凑成 4 + 1 = 5（合盾）
            TestSetup.SetHand(barb.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 1);

            var two = new PreTurnChoiceContext(barb, blank, barb.RightHand, blank.LeftHand)
            {
                AlivePlayerCount = 2,
            };
            var four = new PreTurnChoiceContext(barb, blank, barb.RightHand, blank.LeftHand)
            {
                AlivePlayerCount = 4,
            };

            Assert.True(two.OnlyTwoPlayersLeft, "2 人应判定为'仅剩两人'。");
            Assert.False(four.OnlyTwoPlayersLeft, "4 人不该判定为'仅剩两人'。");

            Assert.True(OffersLockChoice(barb, blank, barb.RightHand, blank.LeftHand, 4),
                "4 人存活时应提供锁手。");
            Assert.True(OffersLockChoice(barb, blank, barb.RightHand, blank.LeftHand, 3),
                "3 人存活时也应提供锁手。");
        }

        [Test]
        public void One_versus_one_blocks_a_second_lock_after_a_real_lock()
        {
            BoardController board = ArrangeAtLockPrompt(out PlayerState blank,
                                                        out PlayerState barb);

            // 第一面盾（野蛮人·守望者道途右手）锁住对手左手
            Assert.True(board.ClickHand(blank.LeftHand));
            board.ConfirmTargetPlayer();
            Assert.True(blank.LeftHand.IsLocked, "应锁住对手左手。");

            // 现在野蛮人·守望者道途已控制一只手。用**另一只手的盾**（左手）
            // 在"存活 2 人"的上下文里问收集阶段 -> 不该再提供锁手。
            var context = new PreTurnChoiceContext(barb, blank, barb.LeftHand, blank.RightHand)
            {
                AlivePlayerCount = 2,
            };

            var requests = new List<ChoiceRequest>();
            (barb.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                Assert.False(requests[i].Effect == HandEffect.PathOfTheWardenLockTarget,
                    "1v1 里已控制一只手时，用另一只盾不该再提供锁手。");
        }

        // ============================================================
        // 多人局打到只剩两人：限制重新生效
        // ============================================================

        [Test]
        public void When_multiplayer_drops_to_two_players_the_limit_returns()
        {
            var p1 = CharacterFactory.CreatePlayer("P1", CharacterDefinition.PathOfTheWarden);
            var p2 = CharacterFactory.CreatePlayer("P2", CharacterDefinition.Blank);
            var p3 = CharacterFactory.CreatePlayer("P3", CharacterDefinition.Blank);
            var p4 = CharacterFactory.CreatePlayer("P4", CharacterDefinition.Blank);

            var seated = new List<PlayerState> { p1, p2, p3, p4 };
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };

            GameManager game = GameManager.Create2v2(seated, providers);
            Assert.AreEqual(4, game.AlivePlayerCount, "2v2 开局应有 4 人存活。");

            // 队伍 {P1,P4} 与 {P2,P3}：把 P3 打死 -> 3 人
            p3.TakeDamage(PlayerState.DefaultMaxHealth);
            Assert.AreEqual(3, game.AlivePlayerCount, "打死 P3 后应剩 3 人。");

            // 再把队友 P4 打死 -> 只剩 P1 与 P2
            p4.TakeDamage(PlayerState.DefaultMaxHealth);
            Assert.AreEqual(2, game.AlivePlayerCount,
                "场上应只剩两人 -> 限制重新生效。");

            var context = new PreTurnChoiceContext(p1, p2, p1.RightHand, p2.LeftHand)
            {
                AlivePlayerCount = game.AlivePlayerCount,
            };

            Assert.True(context.OnlyTwoPlayersLeft,
                "多人局打到只剩两人时，应判定为'仅剩两人'（限制重新生效）。");
        }

        // ============================================================
        // 存活人数的默认值（安全默认）
        // ============================================================

        [Test]
        public void Unknown_alive_count_is_treated_as_one_versus_one()
        {
            // 单元测试直连时上下文可能没填存活人数 ——
            // 这时**按保守方式**处理（当作仅剩两人，限制生效），
            // 以免多人局的放行规则被误用到对局里。
            var context = new PreTurnChoiceContext(null, null, null, null);

            Assert.AreEqual(PreTurnChoiceContext.UnknownAliveCount,
                context.AlivePlayerCount, "默认应是'未知'。");
            Assert.True(context.OnlyTwoPlayersLeft,
                "未知时应保守地当作'仅剩两人'。");
        }

        [Test]
        public void Only_two_players_left_covers_one_player_too()
        {
            var context = new PreTurnChoiceContext(null, null, null, null);
            context.AlivePlayerCount = 1;

            Assert.True(context.OnlyTwoPlayersLeft,
                "只剩 1 人也应算作'仅剩两人以内'。");
        }

        [Test]
        public void Board_collection_fills_the_alive_count_from_the_game()
        {
            // 控制器必须在收集时**自动填**存活人数 ——
            // 否则角色读到"未知"，多人局的放行规则永远不生效。
            BoardController board = ArrangeAtLockPrompt(out PlayerState blank,
                                                        out PlayerState barb);

            // 1v1：存活 2 人
            Assert.AreEqual(2, board.GameAlivePlayerCountForTest,
                "1v1 应报出 2 人存活。");
            Assert.NotNull(blank);
            Assert.NotNull(barb);
        }
    }
}
