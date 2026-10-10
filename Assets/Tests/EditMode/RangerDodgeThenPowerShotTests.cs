using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 复现并钉住：**游侠对战士闪避后无法强化**（玩家实测提交的问题）。
    ///
    /// 根因（已修）：
    ///   `BoardController.AppendDodgeChoices` 与游侠自己的
    ///   <c>RangerLogic.AddDodgeRequests</c> **各生成了一个闪避询问**。
    ///   于是同一次攻击出现两个闪避槽位（variant 0 与 1），
    ///   而这一击实际只有 hitIndex=0 一段 —— 真正被读取的槽位答不上来，
    ///   技能表现为"发不动"。修法是删除控制器里的重复实现，
    ///   由角色自己按段数生成询问。
    /// </summary>
    [TestFixture]
    public class RangerDodgeThenPowerShotTests
    {
        /// <summary>A = 游侠（人类），B = 战士。返回时已把回合交给 A。</summary>
        private static (GameManager game, BoardController board,
                        SimpleChoiceProvider choiceA, PlayerState ranger, PlayerState warrior) Arrange()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("游侠", CharacterDefinition.Ranger),
                CharacterFactory.CreatePlayer("战士", CharacterDefinition.Warrior),
                choiceA, choiceB);

            var board = new BoardController(game, choiceA, choiceB);
            return (game, board, choiceA, game.PlayerA, game.PlayerB);
        }

        /// <summary>
        /// 把回合推进到"战士（B）行动"，并**把游侠的手值恢复成调用前的样子**。
        ///
        /// ⚠️ 为什么要恢复：游侠先手，测试需要先"让出"一个回给战士。
        /// 而那次运算会改变游侠的手值（运算源变成结果），
        /// 于是后续断言看到的已经不是测试设定的手值 —— 第一版就是这么错的。
        /// </summary>
        private static void AdvanceToWarriorTurn(BoardController board, PlayerState ranger,
                                                 PlayerState warrior)
        {
            board.BeginTurn();
            if (!ReferenceEquals(board.ActivePlayer, ranger)) return;

            int leftBefore = ranger.LeftHand.Value;
            int rightBefore = ranger.RightHand.Value;

            // 游侠先手 -> 随便走一步让出回合
            board.ClickHand(ranger.LeftHand);
            board.ClickHand(warrior.RightHand);
            board.ConfirmTargetPlayer();
            while (board.Phase == BoardPhase.ChoosingPreTurnChoice)
                if (!board.DeclinePendingChoice()) break;

            board.BeginTurn();

            // 复原（测试需要确定的现场）
            TestSetup.SetHand(ranger.LeftHand, leftBefore);
            TestSetup.SetHand(ranger.RightHand, rightBefore);
        }

        // ============================================================
        // ① 核心回归：一次攻击只应问一次闪避
        // ============================================================

        [Test]
        public void A_single_hit_attack_asks_for_dodge_exactly_once()
        {
            // 这是本次 bug 的**直接断言**：修之前队列里有两条 RangerDodge。
            var (game, board, _, ranger, warrior) = Arrange();

            TestSetup.SetHand(ranger.LeftHand, 5);
            TestSetup.SetHand(ranger.RightHand, 1);      // 唯一一只 1
            TestSetup.SetHand(warrior.LeftHand, 3);
            TestSetup.SetHand(warrior.RightHand, 1);

            AdvanceToWarriorTurn(board, ranger, warrior);

            // 战士用左手 3 打游侠的左手 5 -> 3 + 5 = 8 -> 攻击
            board.ClickHand(warrior.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();

            int dodgeCount = 0;
            for (int i = 0; i < board.PendingRequests.Count; i++)
                if (board.PendingRequests[i].Effect == HandEffect.RangerDodge) dodgeCount++;

            Assert.AreEqual(1, dodgeCount,
                $"一次单段攻击只应产生一个闪避询问，实际 {dodgeCount} 个。\n" + Dump(board));
        }

        [Test]
        public void A_dodge_request_for_a_single_candidate_offers_an_accept_button()
        {
            // 只有一只可闪的手时，应提供明确的"发动"按钮，
            // 否则 AcceptPendingChoice() 永远失败（曾经就是如此）。
            var (game, board, _, ranger, warrior) = Arrange();

            TestSetup.SetHand(ranger.LeftHand, 5);
            TestSetup.SetHand(ranger.RightHand, 1);
            TestSetup.SetHand(warrior.LeftHand, 3);
            TestSetup.SetHand(warrior.RightHand, 1);

            AdvanceToWarriorTurn(board, ranger, warrior);
            board.ClickHand(warrior.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(HandEffect.RangerDodge, board.PendingRequest?.Effect, Dump(board));
            Assert.False(string.IsNullOrEmpty(board.PendingAcceptLabel),
                "唯一候选的闪避应带'发动'文案，否则 UI 没有按钮可点。\n" + Dump(board));

            bool accepted = board.AcceptPendingChoice();
            Assert.True(accepted, "有了'发动'文案后 AcceptPendingChoice 应成功。\n" + Dump(board));
        }

        [Test]
        public void Multi_hit_attacks_still_ask_once_per_segment()
        {
            // 反向保护：修"重复问一次"时不能把"每段各问一次"也修掉。
            // 忍者两段 + 游侠两只 1 -> 应有两个闪避槽位，variant 分别是 0 与 1。
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("游侠", CharacterDefinition.Ranger),
                CharacterFactory.CreatePlayer("忍者", CharacterDefinition.Ninja),
                choiceA, choiceB);

            var board = new BoardController(game, choiceA, choiceB);
            PlayerState ranger = game.PlayerA;
            PlayerState ninja = game.PlayerB;

            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 1);
            TestSetup.SetHand(ninja.LeftHand, 3);        // 3 + 1 = 4 -> 两段
            TestSetup.SetHand(ninja.RightHand, 1);

            AdvanceToWarriorTurn(board, ranger, ninja);

            board.ClickHand(ninja.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();

            var variants = new List<int>();
            for (int i = 0; i < board.PendingRequests.Count; i++)
                if (board.PendingRequests[i].Effect == HandEffect.RangerDodge)
                    variants.Add(board.PendingRequests[i].Variant);

            Assert.AreEqual(2, variants.Count,
                $"忍者两段、游侠两只 1 -> 应有两个闪避槽位，实际 {variants.Count}。\n" + Dump(board));
            Assert.AreEqual(0, variants[0], "第一个槽位 variant 应为 0。\n" + Dump(board));
            Assert.AreEqual(1, variants[1], "第二个槽位 variant 应为 1。\n" + Dump(board));
        }

        // ============================================================
        // ② 现象本身：闪避之后的强化
        // ============================================================

        /// <summary>
        /// 第 ⑤ 步的精确现场：左手 6 攻击、**另一只手是闪避留下的 2**、目标战士左手 8。
        /// </summary>
        [Test]
        public void Ranger_with_a_two_left_over_from_dodging_can_power_shot()
        {
            var (game, board, _, ranger, warrior) = Arrange();

            TestSetup.SetHand(ranger.LeftHand, 6);
            TestSetup.SetHand(ranger.RightHand, 2);      // 闪避留下的那只
            TestSetup.SetHand(warrior.LeftHand, 8);
            TestSetup.SetHand(warrior.RightHand, 1);

            board.BeginTurn();
            Assert.True(ReferenceEquals(board.ActivePlayer, ranger),
                "A 先手，这里应轮到游侠。\n" + Dump(board));

            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(warrior.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "另一只手是 2 -> 应弹出强化提示。\n" + Dump(board));
            Assert.AreEqual(HandEffect.RangerPowerShot, board.PendingRequest?.Effect, Dump(board));

            bool accepted = board.AcceptPendingChoice();
            Assert.True(accepted, "强化应能被发动。\n" + Dump(board));
            Assert.AreEqual(3, ranger.RightHand.Value, "强化把 2 变成 3。\n" + Dump(board));
        }

        /// <summary>
        /// 翻滚真的执行后（1→2），紧接着自己攻击应能强化。
        ///
        /// 这里**分两段验证**，不去推动整局回合（推动容易把舞台搞错）：
        ///   ① 战士攻击 -> 游侠点手牌完成翻滚，右手 1→2；
        ///   ② 直接把现场设成"游侠回合、左手 6 攻击"，验证那只 2 能强化。
        /// </summary>
        [Test]
        public void After_an_actual_dodge_the_two_left_over_can_power_shot()
        {
            var (game, board, _, ranger, warrior) = Arrange();

            TestSetup.SetHand(ranger.LeftHand, 5);
            TestSetup.SetHand(ranger.RightHand, 1);
            TestSetup.SetHand(warrior.LeftHand, 3);
            TestSetup.SetHand(warrior.RightHand, 1);

            AdvanceToWarriorTurn(board, ranger, warrior);

            // ---- ① 战士 3 + 游侠左手 5 = 8 -> 攻击，游侠应被问翻滚 ----
            board.ClickHand(warrior.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(HandEffect.RangerDodge, board.PendingRequest?.Effect,
                "战士攻击后应问游侠翻滚。\n" + Dump(board));

            // 点那只 1 完成翻滚
            bool dodged = board.ClickHand(ranger.RightHand);
            board.ConfirmTargetPlayer();
            Assert.True(dodged, "点那只 1 应完成翻滚。\n" + Dump(board));

            Assert.AreEqual(2, ranger.RightHand.Value,
                "翻滚把右手 1 变成 2。\n" + Dump(board));

            // ---- ② 用另一局验证"那只 2 能强化"，避免回合推动带来的干扰 ----
            var (game2, board2, _, ranger2, warrior2) = Arrange();

            TestSetup.SetHand(ranger2.LeftHand, 6);
            TestSetup.SetHand(ranger2.RightHand, 2);     // = 上一步翻滚的结果
            TestSetup.SetHand(warrior2.LeftHand, 8);
            TestSetup.SetHand(warrior2.RightHand, 1);

            board2.BeginTurn();
            Assert.True(ReferenceEquals(board2.ActivePlayer, ranger2),
                "A 先手，这里应轮到游侠。\n" + Dump(board2));

            board2.ClickHand(ranger2.LeftHand);
            board2.ConfirmTargetPlayer();
            board2.ClickHand(warrior2.LeftHand);
            board2.ConfirmTargetPlayer();

            Assert.AreEqual(HandEffect.RangerPowerShot, board2.PendingRequest?.Effect,
                "翻滚留下的那只 2 应能用于强化。\n" + Dump(board2));

            Assert.True(board2.AcceptPendingChoice(), "强化应能发动。\n" + Dump(board2));
            Assert.AreEqual(3, ranger2.RightHand.Value, "强化把 2 变成 3。\n" + Dump(board2));
        }

        // ============================================================

        private static string Dump(BoardController board)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(board.DebugPhaseInfo()).Append('\n');

            for (int i = 0; i < board.PendingRequests.Count; i++)
            {
                ChoiceRequest r = board.PendingRequests[i];
                sb.Append($"  [{i}] {r.Effect} variant={r.Variant} cands={r.Candidates.Count} ")
                  .Append($"accept='{r.AcceptLabel}'\n");
            }

            return sb.ToString();
        }
    }
}
