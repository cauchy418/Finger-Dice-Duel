using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class FeignedDeathSkipTests
    {
        private static BoardController Board(int count, out GameManager game, CharacterDefinition role = null)
        {
            var players = new PlayerState[count];
            var choices = new SimpleChoiceProvider[count];
            var teams = new int[count];
            for (int i = 0; i < count; i++)
            {
                players[i] = CharacterFactory.CreatePlayer("P" + i, i == 0 ? role ?? CharacterDefinition.Champion : CharacterDefinition.Blank);
                choices[i] = new SimpleChoiceProvider();
                teams[i] = i % 2;
            }
            game = count == 8 ? GameManager.CreateEight(players, choices, teams)
                : count == 4 ? GameManager.Create2v2(players, choices)
                : new GameManager(players[0], players[1], choices[0], choices[1]);
            for (int i = 1; i < count; i++) players[i].ApplyStatus(StatusEffect.FeignedDeath, 2);
            return new BoardController(game, choices[0], choices[1]);
        }

        [Test]
        public void All_other_players_feigned_offer_skip_before_and_after_source_selection()
        {
            foreach (int count in new[] { 2, 4, 8 })
            foreach (bool selectSource in new[] { false, true })
            {
                var board = Board(count, out var game);
                board.BeginTurn();
                Assert.False(game.IsGameOver);
                Assert.AreEqual(0, game.GetLegalMoves(game.PlayerA).Count);
                Assert.True(board.CanSkipTurn);
                if (selectSource)
                {
                    Assert.True(board.ClickHand(game.PlayerA.LeftHand));
                    Assert.AreEqual(BoardPhase.ChoosingTargetHand, board.Phase);
                    foreach (var p in game.Players)
                        if (!ReferenceEquals(p, game.PlayerA)) Assert.False(board.CanClick(p.LeftHand));
                    Assert.True(board.CanSkipTurn);
                }
                Assert.True(board.IsHumanTurn);
                Assert.True(board.SkipTurn());
                Assert.AreEqual(1, game.TurnCount);
                Assert.AreEqual(1, board.CompletedActions);
                Assert.AreSame(game.PlayerB, board.ActivePlayer);
                Assert.False(game.IsGameOver);
                Assert.AreEqual(2, game.PlayerB.StatusTurnsLeft(StatusEffect.FeignedDeath));
            }
        }

        [Test]
        public void Death_domain_can_borrow_feigned_hands_without_forced_skip()
        {
            foreach (int count in new[] { 2, 4, 8 })
            {
                var board = Board(count, out var game, CharacterDefinition.DeathDomain);
                board.BeginTurn();
                Assert.True(game.GetLegalMoves(game.PlayerA).Count > 0);
                Assert.True(game.HasAnyLegalOperation(game.PlayerA));
                Assert.False(board.CanSkipTurn);
                Assert.True(board.ClickHand(game.PlayerA.LeftHand));
                Assert.True(board.CanClick(game.PlayerB.LeftHand));
                Assert.False(board.CanSkipTurn);
                Assert.False(board.SkipTurn());
                Assert.AreEqual(0, game.TurnCount);
            }
        }

        [Test]
        public void Death_domain_dead_addend_counts_when_live_hands_are_concealed()
        {
            var board = Board(4, out var game, CharacterDefinition.DeathDomain);
            game.PlayerC.RemoveStatus(StatusEffect.FeignedDeath);
            game.PlayerC.TakeDamage(100);
            game.PlayerB.LeftHand.Conceal(); game.PlayerB.RightHand.Conceal();
            game.PlayerD.LeftHand.Conceal(); game.PlayerD.RightHand.Conceal();
            board.BeginTurn();
            Assert.True(game.GetLegalMoves(game.PlayerA).Count > 0);
            Assert.True(game.HasAnyLegalOperation(game.PlayerA));
            Assert.False(board.CanSkipTurn);
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.True(board.CanClick(game.PlayerC.LeftHand));
        }

        [Test]
        public void A_single_usable_addend_including_a_locked_hand_prevents_forced_skip()
        {
            var board = Board(4, out var game);
            game.PlayerD.RemoveStatus(StatusEffect.FeignedDeath);
            game.PlayerD.LeftHand.Lock(); game.PlayerD.RightHand.Conceal();
            game.PlayerA.LeftHand.Lock();
            board.BeginTurn();
            Assert.False(board.CanSkipTurn);
            Assert.False(board.CanClick(game.PlayerA.LeftHand));
            Assert.True(board.ClickHand(game.PlayerA.RightHand));
            Assert.True(board.CanClick(game.PlayerD.LeftHand));
            Assert.False(board.CanClick(game.PlayerD.RightHand));
            Assert.False(board.CanSkipTurn);
        }

        [Test]
        public void Self_addend_is_a_real_operation_when_all_other_players_feign()
        {
            var board = Board(4, out var game, CharacterDefinition.Magician);
            board.BeginTurn();
            Assert.True(game.GetLegalMoves(game.PlayerA).Count > 0);
            Assert.True(game.HasAnyLegalOperation(game.PlayerA));
            Assert.False(board.CanSkipTurn);
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.True(board.CanClick(game.PlayerA.RightHand));
        }

        [Test]
        public void Locks_frozen_concealment_and_private_addends_still_allow_no_operation_skip()
        {
            for (int scenario = 0; scenario < 4; scenario++)
            {
                var board = Board(4, out var game);
                game.PlayerD.RemoveStatus(StatusEffect.FeignedDeath);
                if (scenario == 0) { game.PlayerA.LeftHand.Lock(); game.PlayerA.RightHand.Lock(); }
                if (scenario == 1) game.PlayerA.ApplyStatus(StatusEffect.Frozen);
                if (scenario == 2) { game.PlayerD.LeftHand.Conceal(); game.PlayerD.RightHand.Conceal(); }
                if (scenario == 3) { game.PlayerD.LeftHand.SetTargetRestricted(true); game.PlayerD.RightHand.SetTargetRestricted(true); }
                board.BeginTurn();
                Assert.AreEqual(0, game.GetLegalMoves(game.PlayerA).Count);
                Assert.True(board.CanSkipTurn);
                Assert.True(board.SkipTurn());
                Assert.AreEqual(1, game.TurnCount);
                Assert.False(game.IsGameOver);
            }
        }

        [Test]
        public void Feigned_actor_with_available_addends_can_operate_and_expiry_restores_others()
        {
            var board = Board(2, out var game);
            game.PlayerB.RemoveStatus(StatusEffect.FeignedDeath);
            game.PlayerA.ApplyStatus(StatusEffect.FeignedDeath, 1);
            game.PlayerA.LeftHand.SetValue(1); game.PlayerB.LeftHand.SetValue(3);
            board.BeginTurn();
            Assert.False(board.CanSkipTurn);
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(1, game.TurnCount);
            Assert.AreEqual(4, game.PlayerA.LeftHand.Value);
            Assert.AreEqual(20, game.PlayerB.Health);
            Assert.False(game.PlayerA.HasStatus(StatusEffect.FeignedDeath));
            Assert.True(game.HasAnyLegalOperation(game.PlayerB));
            Assert.False(board.CanSkipTurn);
        }

        [Test]
        public void Local_human_on_another_seat_can_skip_and_ticks_own_feign_once()
        {
            foreach (int count in new[] { 4, 8 })
            {
                var board = Board(count, out var game);
                game.PlayerA.ApplyStatus(StatusEffect.FeignedDeath, 2);
                board.SetFirstPlayer(game.PlayerB);
                board.BeginTurn();
                Assert.True(board.IsHumanTurn);
                Assert.True(board.CanSkipTurn);
                Assert.True(board.SkipTurn());
                Assert.AreEqual(1, game.TurnCount);
                Assert.AreEqual(1, game.PlayerB.StatusTurnsLeft(StatusEffect.FeignedDeath));
                Assert.AreEqual(2, game.PlayerA.StatusTurnsLeft(StatusEffect.FeignedDeath));
            }
        }
    }
}
