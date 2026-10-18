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
    /// **8 人阵营混战**的核心规则：
    ///   · 8 个座位、3 个阵营队伍（人数可为 3/3/2）；
    ///   · 队友不能互打，但可以拿队友的手当被加数；
    ///   · 轮次上限按**阵营总血量**判定胜负。
    /// </summary>
    [TestFixture]
    public class EightPlayerTests
    {
        private static GameManager MatchEight(out List<PlayerState> seated)
        {
            seated = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                seated.Add(CharacterFactory.CreatePlayer("P" + (i + 1),
                                                         CharacterDefinition.Blank));

            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());

            // 守序 3 人、中立 3 人、混乱 2 人（座位 1..8）
            var teams = new List<int> { 0, 0, 0, 1, 1, 1, 2, 2 };
            return GameManager.CreateEight(seated, providers, teams);
        }

        [Test]
        public void Eight_players_and_three_faction_teams_are_registered()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);

            Assert.AreEqual(8, game.Players.Count);
            Assert.AreEqual(0, game.TeamOf(seated[0]));
            Assert.AreEqual(0, game.TeamOf(seated[2]));
            Assert.AreEqual(1, game.TeamOf(seated[3]));
            Assert.AreEqual(2, game.TeamOf(seated[7]));
            Assert.AreEqual(3, game.AliveTeamCount(0));
            Assert.AreEqual(3, game.AliveTeamCount(1));
            Assert.AreEqual(2, game.AliveTeamCount(2));
        }

        [Test]
        public void Teammates_are_not_attack_targets_but_enemy_teams_are()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);

            Assert.IsTrue(game.IsTeammate(seated[0], seated[1]));
            Assert.IsFalse(game.IsTeammate(seated[0], seated[3]));

            // OpponentsOf 只列不同队的存活者：3 + 2 = 5 人。
            Assert.AreEqual(5, game.OpponentsOf(seated[0]).Count);
        }

        [Test]
        public void Round_limit_gives_the_win_to_the_faction_with_most_total_health()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);
            game.RoundLimit = 0;

            // 守序 24、中立 18、混乱 10
            seated[0].SetHealthExact(10);
            seated[1].SetHealthExact(8);
            seated[2].SetHealthExact(6);
            seated[3].SetHealthExact(9);
            seated[4].SetHealthExact(5);
            seated[5].SetHealthExact(4);
            seated[6].SetHealthExact(7);
            seated[7].SetHealthExact(3);

            game.BeginTurn();
            PlayerState actor = game.CurrentPlayer;
            LegalMove move = game.GetLegalMoves(actor)[0];
            game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);

            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(0, game.WinningTeam, "守序阵营总血量最高，应获胜。");
            Assert.AreSame(seated[0], game.Winner, "代表取队内血量最高的存活者。");
        }

        [Test]
        public void Round_limit_is_a_draw_when_top_faction_totals_tie()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);
            game.RoundLimit = 0;

            // 三个阵营各 20
            seated[0].SetHealthExact(10); seated[1].SetHealthExact(5); seated[2].SetHealthExact(5);
            seated[3].SetHealthExact(10); seated[4].SetHealthExact(5); seated[5].SetHealthExact(5);
            seated[6].SetHealthExact(10); seated[7].SetHealthExact(10);

            game.BeginTurn();
            PlayerState actor = game.CurrentPlayer;
            LegalMove move = game.GetLegalMoves(actor)[0];
            game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);

            Assert.IsTrue(game.IsGameOver);
            Assert.Null(game.Winner, "最高总血量并列时按平局。");
        }

        [Test]
        public void Eight_actions_advance_one_round()
        {
            GameManager game = MatchEight(out _);
            game.RoundLimit = 50;

            for (int i = 0; i < 8; i++)
            {
                PlayerState actor = game.CurrentPlayer;
                LegalMove move = game.GetLegalMoves(actor)[0];
                game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
            }

            Assert.AreEqual(1, game.RoundCount, "8 人各行动一次 = 1 轮。");
            Assert.AreEqual(8, game.TurnCount);
        }
    }
}
