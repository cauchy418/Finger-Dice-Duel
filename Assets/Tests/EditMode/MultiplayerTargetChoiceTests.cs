using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class MultiplayerTargetChoiceTests
    {
        private static BoardController Build(int count, int actorIndex, CharacterDefinition role,
            out List<PlayerState> players, out GameManager game)
        {
            players = new List<PlayerState>();
            var sinks = new List<SimpleChoiceProvider>();
            for (int i = 0; i < count; i++)
            {
                players.Add(CharacterFactory.CreatePlayer("P" + i, i == actorIndex ? role : CharacterDefinition.Soldier));
                sinks.Add(new SimpleChoiceProvider());
            }
            var providers = sinks.ConvertAll(s => (IChoiceProvider)s);
            game = count == 4 ? GameManager.Create2v2(players, providers)
                : GameManager.CreateIdentityEight(players, providers, new[] { IdentityRole.Lord, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel,
                    IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Traitor });
            var board = new BoardController(game, sinks[0], sinks[1]);
            for (int i = 0; i < count; i++)
            {
                board.BindPlayer(players[i], null, sinks[i]);
                players[i].LeftHand.SetValue(1); players[i].RightHand.SetValue(1); players[i].SetHealthExact(10);
            }
            board.SetFirstPlayer(players[actorIndex]);
            return board;
        }

        [Test]
        public void Four_player_cleric_can_heal_a_player_outside_the_operation_pair()
        {
            var board = Build(4, 2, CharacterDefinition.Cleric, out var players, out _);
            players[2].LeftHand.SetValue(8); board.BeginTurn();
            board.ClickHand(players[2].LeftHand); board.ClickHand(players[0].LeftHand);
            Assert.AreEqual(HandEffect.ClericHealTarget, board.PendingEffect);
            Assert.AreEqual(8, board.ChoiceCandidates.Count);
            Assert.IsTrue(board.ClickHand(players[3].RightHand));
            Assert.AreEqual(19, players[3].Health);
            Assert.AreEqual(10, players[2].Health);
        }

        [Test]
        public void Eight_player_double_nine_can_choose_two_different_distant_recipients()
        {
            var board = Build(8, 0, CharacterDefinition.Cleric, out var players, out _);
            players[0].LeftHand.SetValue(8); players[0].RightHand.SetValue(9); board.BeginTurn();
            board.ClickHand(players[0].LeftHand); board.ClickHand(players[1].LeftHand);
            Assert.AreEqual(HandEffect.ClericExtraHealTarget, board.PendingEffect);
            Assert.AreEqual(16, board.ChoiceCandidates.Count);
            Assert.IsTrue(board.ClickHand(players[6].LeftHand));
            Assert.AreEqual(HandEffect.ClericHealTarget, board.PendingEffect);
            Assert.AreEqual(16, board.ChoiceCandidates.Count);
            Assert.IsTrue(board.ClickHand(players[7].RightHand));
            Assert.AreEqual(19, players[6].Health);
            Assert.AreEqual(19, players[7].Health);
            Assert.AreEqual(10, players[0].Health);
        }

        [Test]
        public void Eight_player_fighter_can_chain_break_the_last_players_shield()
        {
            var board = Build(8, 0, CharacterDefinition.Fighter, out var players, out _);
            foreach (var player in players)
            {
                player.LeftHand.SetValue(5); player.LeftHand.SetShieldState(ShieldState.Formed);
                player.RightHand.SetValue(5); player.RightHand.SetShieldState(ShieldState.Formed);
            }
            board.BeginTurn(); board.ClickHand(players[0].LeftHand); board.ClickHand(players[1].LeftHand);
            Assert.AreEqual(HandEffect.FighterChainBreak, board.PendingEffect);
            Assert.AreEqual(15, board.ChoiceCandidates.Count);
            Assert.IsTrue(board.ClickHand(players[7].RightHand));
            Assert.AreEqual(ShieldState.Broken, players[7].RightHand.State);
            Assert.AreEqual(ShieldState.Formed, players[7].LeftHand.State);
        }
    }
}
