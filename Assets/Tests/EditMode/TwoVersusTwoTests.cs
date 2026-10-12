using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2**（四人各管自己）的引擎验证。
    ///
    /// 玩家确认的形态：四个人各管自己、每人仍是两只手；
    /// A/C 一队、B/D 一队（名单交错排列，便于 UI 左右各两人）。
    ///
    /// 这一组测试盯的是"把两人对偶泛化成名单 + 队伍"之后，
    /// 四条最容易出错的规则：
    ///   1. 队友**不是**对手（不能打队友）
    ///   2. 对手**可能不止一个**（目标选择要能区分）
    ///   3. 回合按名单轮转（不是 A↔B 互打、C/D 干看着）
    ///   4. 胜负按**队伍全灭**判（队友还活着就不能结束）
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoTests
    {
        /// <summary>造一局 2v2：A/C 一队、B/D 一队，四人都是白板（数值中性）。</summary>
        private static GameManager Match2v2()
        {
            var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);
            var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var c = CharacterFactory.CreatePlayer("C", CharacterDefinition.Blank);
            var d = CharacterFactory.CreatePlayer("D", CharacterDefinition.Blank);

            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            game.AddPlayersFor2v2(c, new SimpleChoiceProvider(),
                                  d, new SimpleChoiceProvider());
            return game;
        }

        // ============================================================
        // 名单与队伍
        // ============================================================

        [Test]
        public void Two_versus_two_registers_four_players_in_two_teams()
        {
            GameManager game = Match2v2();

            Assert.AreEqual(4, game.Players.Count, "2v2 应有 4 名玩家。");

            // 座位是环 1-2-3-4，**首尾同队**：{A, D} 与 {B, C}
            Assert.AreEqual(game.TeamOf(game.PlayerA), game.TeamOf(game.PlayerD),
                "A(座位1) 与 D(座位4) 首尾相邻 -> 同队。");
            Assert.AreEqual(game.TeamOf(game.PlayerB), game.TeamOf(game.PlayerC),
                "B(座位2) 与 C(座位3) 相邻 -> 同队。");
            Assert.True(game.TeamOf(game.PlayerA) != game.TeamOf(game.PlayerB),
                "两队必须是不同队伍。");
        }

        [Test]
        public void One_versus_one_is_still_two_players_in_two_teams()
        {
            // 1v1 不能因为"加了 2v2"而变样
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank,
                                                    CharacterDefinition.Warrior);

            Assert.AreEqual(2, game.Players.Count);
            Assert.True(game.TeamOf(game.PlayerA) != game.TeamOf(game.PlayerB));
            Assert.Null(game.PlayerC, "1v1 没有第三名玩家。");
            Assert.Null(game.PlayerD);
        }

        [Test]
        public void Teammates_are_not_opponents()
        {
            GameManager game = Match2v2();

            IReadOnlyList<PlayerState> foesOfA = game.OpponentsOf(game.PlayerA);

            Assert.AreEqual(2, foesOfA.Count, "A 有 2 个对手（B/C）—— 首尾同队，队友是 D。");
            for (int i = 0; i < foesOfA.Count; i++)
            {
                Assert.False(ReferenceEquals(foesOfA[i], game.PlayerD),
                    "队友 D 不该出现在对手名单里。");
                Assert.False(game.IsTeammate(game.PlayerA, foesOfA[i]),
                    "对手不应该是队友。");
            }

            Assert.True(game.IsTeammate(game.PlayerA, game.PlayerD), "A 与 D 是队友。");
            Assert.False(game.IsTeammate(game.PlayerA, game.PlayerB), "A 与 B 不是队友。");
        }

        [Test]
        public void Teammates_of_a_player_exclude_self_and_opponents()
        {
            GameManager game = Match2v2();

            IReadOnlyList<PlayerState> mates = game.TeammatesOf(game.PlayerA);

            Assert.AreEqual(1, mates.Count, "A 有 1 个队友（D）。");
            Assert.AreSame(game.PlayerD, mates[0]);
        }

        // ============================================================
        // 回合轮转：四个人都要轮到
        // ============================================================

        [Test]
        public void Turn_rotates_through_all_four_players()
        {
            // ⚠️ 这是最关键的一条：不能沿用"切换成对手"的 1v1 写法，
            // 否则 A↔B 会互相打、C/D 永远轮不到。
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            var order = new List<string>();
            PlayerState actor = game.CurrentPlayer;

            for (int i = 0; i < 4; i++)
            {
                order.Add(actor.Name);
                PlayerState foe = game.OpponentsOf(actor)[0];
                game.ExecuteTurn(actor, actor.LeftHand, foe.LeftHand);
                actor = game.CurrentPlayer;
            }

            Assert.AreEqual(4, order.Count);
            for (int i = 0; i < 4; i++)
                for (int j = i + 1; j < 4; j++)
                    Assert.False(order[i] == order[j],
                        $"四回合内不该重复同一个人（实际顺序：{string.Join("→", order)}）。");
        }

        // ============================================================
        // 座位距离（环形最短步数，阵亡者摘掉后环收缩）
        // ============================================================

        [Test]
        public void Four_player_ring_distances_match_the_specified_numbers()
        {
            // 玩家给的数字：1234 四人，12=1、13=2、14=1
            GameManager game = Match2v2();

            Assert.AreEqual(1, game.DistanceBetween(game.PlayerA, game.PlayerB), "1↔2 应为 1。");
            Assert.AreEqual(2, game.DistanceBetween(game.PlayerA, game.PlayerC), "1↔3 应为 2。");
            Assert.AreEqual(1, game.DistanceBetween(game.PlayerA, game.PlayerD), "1↔4 应为 1。");
            Assert.AreEqual(0, game.DistanceBetween(game.PlayerA, game.PlayerA), "自己到自己是 0。");
        }

        [Test]
        public void Eight_player_ring_distances_match_the_specified_numbers()
        {
            // 玩家给的数字（8 人）：14=3、15=4、16=3
            GameManager game = MatchEight();

            PlayerState p1 = game.Players[0];
            Assert.AreEqual(3, game.DistanceBetween(p1, game.Players[3]), "1↔4 应为 3。");
            Assert.AreEqual(4, game.DistanceBetween(p1, game.Players[4]), "1↔5 应为 4。");
            Assert.AreEqual(3, game.DistanceBetween(p1, game.Players[5]), "1↔6 应为 3。");
        }

        [Test]
        public void Distance_is_the_shortest_way_around_the_ring()
        {
            GameManager game = MatchEight();
            PlayerState p1 = game.Players[0];

            // 1↔8：|1-8|=7，但环上另一侧只有 1 步
            Assert.AreEqual(1, game.DistanceBetween(p1, game.Players[7]),
                "1↔8 应取环上更短的一侧（1 步），而不是 7。");
        }

        [Test]
        public void Dead_players_shrink_the_ring()
        {
            // 玩家确认："B 环收缩处理死亡玩家"。
            // 8 人局：2 号阵亡后，1↔5 的环位序号各减一 -> 距离从 4 变 3。
            GameManager game = MatchEight();
            PlayerState p1 = game.Players[0];
            PlayerState p5 = game.Players[4];

            Assert.AreEqual(4, game.DistanceBetween(p1, p5), "满员时 1↔5 应为 4。");

            game.Players[1].TakeDamage(PlayerState.DefaultMaxHealth);   // 2 号阵亡
            Assert.True(game.Players[1].IsDead);

            Assert.AreEqual(3, game.DistanceBetween(p1, p5),
                "2 号阵亡后环收缩，1↔5 应变成 3。");
        }

        [Test]
        public void Distance_to_a_dead_player_is_unknown()
        {
            GameManager game = Match2v2();

            game.PlayerB.TakeDamage(PlayerState.DefaultMaxHealth);
            Assert.AreEqual(-1, game.DistanceBetween(game.PlayerA, game.PlayerB),
                "阵亡者不在环上 -> 距离返回 -1（无意义）。");
        }

        /// <summary>造一局 8 人（单人一队，仅用于验证距离）。</summary>
        private static GameManager MatchEight()
        {
            var players = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                players.Add(CharacterFactory.CreatePlayer("P" + (i + 1),
                                                          CharacterDefinition.Blank));

            var game = new GameManager(players[0], players[1],
                                       new SimpleChoiceProvider(), new SimpleChoiceProvider());
            game.AddPlayersFor2v2(players[2], new SimpleChoiceProvider(),
                                  players[3], new SimpleChoiceProvider());
            game.AddPlayersFor8(players[4], new SimpleChoiceProvider(),
                                players[5], new SimpleChoiceProvider(),
                                players[6], new SimpleChoiceProvider(),
                                players[7], new SimpleChoiceProvider());
            return game;
        }

        // ============================================================
        // 目标合法性
        // ============================================================

        [Test]
        public void A_teammates_hand_can_be_used_as_the_addend()
        {
            // 玩家确认的规则："A 可以选择 B/C/D 任何一个数字作为被加数"。
            // 队友的手**可以**当被加数 —— 别人的数字不会被改动，
            // 只有行动方自己的手变成结果值。
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            TurnResult r = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand,
                                            game.PlayerC.LeftHand);

            Assert.NotNull(r, "应能用队友的手当被加数。");
            Assert.AreEqual(1, game.PlayerC.LeftHand.Value,
                "被加数（队友的手）数字**不该**改变。");
            Assert.AreEqual(2, game.PlayerA.LeftHand.Value,
                "行动方自己的手变成结果值（1+1=2）。");
        }

        [Test]
        public void Cannot_use_own_other_hand_as_the_addend()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            Assert.Throws<System.InvalidOperationException>(
                () => game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand,
                                       game.PlayerA.RightHand),
                "被加数不能是自己的手。");
        }

        [Test]
        public void Can_attack_either_opponent()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            // 打 B（第一个对手）
            TurnResult r1 = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand,
                                             game.PlayerB.LeftHand);
            Assert.NotNull(r1);
            Assert.AreSame(game.PlayerB, game.OwnerOfHand(game.PlayerB.LeftHand));

            // 换一局打 D（第二个对手）
            GameManager game2 = Match2v2();
            TestSetup.SetAllFourHands(game2, 1);

            TurnResult r2 = game2.ExecuteTurn(game2.PlayerA, game2.PlayerA.LeftHand,
                                              game2.PlayerD.LeftHand);
            Assert.NotNull(r2, "应能打第二个对手 D。");
            Assert.AreSame(game2.PlayerD, game2.OwnerOfHand(game2.PlayerD.LeftHand),
                "OwnerOfHand 要能认出这是 D 的手。");
        }

        [Test]
        public void Owner_of_hand_finds_any_of_the_four_players()
        {
            GameManager game = Match2v2();

            Assert.AreSame(game.PlayerA, game.OwnerOfHand(game.PlayerA.RightHand));
            Assert.AreSame(game.PlayerB, game.OwnerOfHand(game.PlayerB.RightHand));
            Assert.AreSame(game.PlayerC, game.OwnerOfHand(game.PlayerC.RightHand));
            Assert.AreSame(game.PlayerD, game.OwnerOfHand(game.PlayerD.RightHand));
        }

        // ============================================================
        // 胜负：按队伍全灭
        // ============================================================

        [Test]
        public void One_teammate_dying_does_not_end_the_match()
        {
            // 2v2 的关键差别：A 阵亡时 C 还活着 -> 对局必须继续
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            game.PlayerA.TakeDamage(PlayerState.DefaultMaxHealth);
            Assert.True(game.PlayerA.IsDead);
            Assert.False(game.IsGameOver,
                "A 阵亡但队友 C 还活着 -> 对局不该结束。");
        }

        [Test]
        public void The_match_ends_when_one_team_is_wiped()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            // {A, D} 全部阵亡 -> {B, C} 获胜
            game.PlayerA.TakeDamage(PlayerState.DefaultMaxHealth);
            game.PlayerD.TakeDamage(PlayerState.DefaultMaxHealth);

            // 触发一次结算来跑胜负判定。
            // 用**对手**的手当被加数（队友的手虽然也能用，但这里要确认胜负判定）。
            game.ExecuteTurn(game.PlayerB, game.PlayerB.LeftHand, game.PlayerA.LeftHand);

            Assert.True(game.IsGameOver, "一队全灭后对局应结束。");
            Assert.NotNull(game.Winner, "应产生赢家。");
            Assert.AreEqual(game.TeamOf(game.PlayerB), game.WinningTeam,
                "赢家应是 {B, C} 那一队。");
        }

        [Test]
        public void Dead_players_are_not_offered_as_targets()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            game.PlayerB.TakeDamage(PlayerState.DefaultMaxHealth);
            Assert.True(game.PlayerB.IsDead);

            IReadOnlyList<PlayerState> foesOfA = game.OpponentsOf(game.PlayerA);

            Assert.AreEqual(1, foesOfA.Count, "B 阵亡后 A 只剩 C 一个可打的对手。");
            Assert.AreSame(game.PlayerC, foesOfA[0]);
        }

        [Test]
        public void Turn_rotation_skips_dead_players()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            // B 阵亡 -> 轮转应跳过 B，A→C→D
            game.PlayerB.TakeDamage(PlayerState.DefaultMaxHealth);

            var visited = new List<string>();
            PlayerState actor = game.CurrentPlayer;

            for (int i = 0; i < 3; i++)
            {
                visited.Add(actor.Name);
                IReadOnlyList<PlayerState> foes = game.OpponentsOf(actor);
                Assert.True(foes.Count > 0, $"{actor.Name} 应还有对手。");

                game.ExecuteTurn(actor, actor.LeftHand, foes[0].LeftHand);
                actor = game.CurrentPlayer;
            }

            Assert.False(visited.Contains("B"), $"阵亡的 B 不该被轮到（实际：{string.Join("→", visited)}）。");
        }

        [Test]
        public void Both_teams_wiped_is_a_draw()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 1);

            game.PlayerA.TakeDamage(PlayerState.DefaultMaxHealth);
            game.PlayerC.TakeDamage(PlayerState.DefaultMaxHealth);
            game.PlayerB.TakeDamage(PlayerState.DefaultMaxHealth);
            game.PlayerD.TakeDamage(PlayerState.DefaultMaxHealth);

            Assert.True(game.IsTeamWiped(game.TeamOf(game.PlayerA)));
            Assert.True(game.IsTeamWiped(game.TeamOf(game.PlayerB)));
        }
    }
}
