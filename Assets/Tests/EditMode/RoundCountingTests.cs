using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **轮次计数**（玩家确认的规则）。
    ///
    /// 一轮 = **所有角色都经过一次行动**：
    ///   · 1v1：从 1 号位行动，到**再次回到 1 号位行动**就是第二轮；
    ///   · 2v2：回到 1 号位行动 = 下一轮；
    ///   · 1 号位阵亡则以"下一个还活着的座位"作为轮的起点；
    ///   · 所有模式的**轮次**上限统一为 50。
    ///
    /// ⚠️ 回合制**没有被删除** —— `TurnCount` / `TurnLimit` 原样保留，
    ///    轮次只是另一组工具（`RoundCount` / `RoundLimit`）。
    /// </summary>
    [TestFixture]
    public class RoundCountingTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager OneVsOne()
        {
            var game = new GameManager(Make("A", CharacterDefinition.Blank),
                                       Make("B", CharacterDefinition.Blank),
                                       new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());
            TestSetup.SetAllHands(game.PlayerA, game.PlayerB, 1);
            game.ResetMatch();
            return game;
        }

        [Test]
        public void One_versus_one_round_is_two_turns()
        {
            GameManager game = OneVsOne();
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            Assert.AreEqual(0, game.RoundCount, "开局应在第 0 轮（还没人行动）。");

            // 第 1 次行动：A 行动 -> 轮到 B，还没回到锚点
            game.ExecuteTurn(a, game.GetLegalMoves(a)[0].ActedHand,
                                game.GetLegalMoves(a)[0].TargetHand);
            Assert.AreEqual(0, game.RoundCount,
                "1v1：A 行动完后还没回到 A，轮次不应增加。");

            // 第 2 次行动：B 行动 -> 下一个又是 A（锚点）-> 进入第 2 轮
            game.ExecuteTurn(b, game.GetLegalMoves(b)[0].ActedHand,
                                game.GetLegalMoves(b)[0].TargetHand);
            Assert.AreEqual(1, game.RoundCount,
                "1v1：B 行动完后下一个人是 A，应记 1 轮。");
        }

        [Test]
        public void Round_limit_is_fifty_for_every_mode()
        {
            Assert.AreEqual(50, GameManager.DefaultRoundLimit,
                "所有模式的轮次上限都应是 50 轮。");

            GameManager game = OneVsOne();
            Assert.AreEqual(50, game.RoundLimit, "默认轮次上限应是 50。");
        }

        [Test]
        public void Round_anchor_skips_to_the_next_alive_seat()
        {
            // 1 号位阵亡后，轮的起点应顺延到下一个还活着的座位。
            GameManager game = OneVsOne();
            PlayerState a = game.PlayerA;

            Assert.AreSame(a, game.RoundAnchor, "1 号位活着时锚点就是它。");

            a.TakeDamage(999);
            Assert.False(a.IsAlive, "1 号位应已阵亡。");
            Assert.AreSame(game.PlayerB, game.RoundAnchor,
                "1 号位阵亡后，锚点应顺延到下一个还活着的座位。");
        }

        [Test]
        public void Two_versus_two_round_is_four_turns()
        {
            var seated = new List<PlayerState>
            {
                Make("P1", CharacterDefinition.Blank),
                Make("P2", CharacterDefinition.Blank),
                Make("P3", CharacterDefinition.Blank),
                Make("P4", CharacterDefinition.Blank),
            };

            var providers = new List<IChoiceProvider>
            {
                new GreedyChoiceProvider(), new GreedyChoiceProvider(),
                new GreedyChoiceProvider(), new GreedyChoiceProvider(),
            };

            GameManager game = GameManager.Create2v2(seated, providers);
            TestSetup.SetAllFourHands(game, 1);
            game.ResetMatch();

            // 2v2 一轮 = 4 次行动：座位 1 -> 2 -> 3 -> 4 -> 回到 1 才算一轮
            int[] seenRounds = new int[5];
            for (int step = 0; step < 4; step++)
            {
                PlayerState actor = game.CurrentPlayer;
                IReadOnlyList<LegalMove> moves = game.GetLegalMoves(actor);
                Assert.True(moves.Count > 0, $"座位 {step + 1} 应有合法步。");

                game.ExecuteTurn(actor, moves[0].ActedHand, moves[0].TargetHand);
                seenRounds[step] = game.RoundCount;
            }

            Assert.AreEqual(0, seenRounds[0], "座位 1 行动后还没成轮。");
            Assert.AreEqual(0, seenRounds[1], "座位 2 行动后还没成轮。");
            Assert.AreEqual(0, seenRounds[2], "座位 3 行动后还没成轮。");
            Assert.AreEqual(1, seenRounds[3], "座位 4 行动后回到座位 1，应记 1 轮。");
        }

        [Test]
        public void Turn_counting_still_works_alongside_round_counting()
        {
            // 玩家明确要求：**不删除回合制**，只新增轮次计数。
            GameManager game = OneVsOne();
            PlayerState a = game.PlayerA;

            game.ExecuteTurn(a, game.GetLegalMoves(a)[0].ActedHand,
                                game.GetLegalMoves(a)[0].TargetHand);

            Assert.AreEqual(1, game.TurnCount, "回合数仍应在累加。");
            Assert.AreEqual(50, game.TurnLimit, "回合上限仍然存在（原义不变）。");
        }
    }
}
