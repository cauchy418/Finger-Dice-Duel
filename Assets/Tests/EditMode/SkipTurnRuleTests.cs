using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **“两只手都不能用”时的跳过规则**（玩家确认）：
    ///
    /// 1. **两只手都被锁** → 可以跳过回合。
    /// 2. **一只被锁、另一只是完整盾** → **不能跳过**，
    ///    强制用那只完整盾行动（即使会破盾）。
    /// 3. **两只都是完整盾** → 可以跳过（原有规则）。
    /// </summary>
    [TestFixture]
    public class SkipTurnRuleTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match()
        {
            var a = Make("A", CharacterDefinition.Champion);
            var b = Make("B", CharacterDefinition.Champion);
            return new GameManager(a, b, new GreedyChoiceProvider(),
                                   new GreedyChoiceProvider());
        }

        [Test]
        public void Both_hands_locked_allows_skipping_the_turn()
        {
            GameManager game = Match();
            PlayerState a = game.PlayerA;

            a.LeftHand.Lock();
            a.RightHand.Lock();

            Assert.True(a.HasNoUsableHand, "两只手都被锁应视为无手可用。");
            Assert.True(game.CanSkipTurnFor(a),
                "两只手都被锁 -> 应允许跳过回合。");
        }

        [Test]
        public void One_locked_hand_plus_a_whole_shield_does_not_allow_skipping()
        {
            GameManager game = Match();
            PlayerState a = game.PlayerA;

            a.LeftHand.Lock();
            a.RightHand.SetValue(5);
            a.RightHand.SetShieldState(ShieldState.Formed);

            Assert.False(a.HasNoUsableHand,
                "另一只是完整盾 -> 仍然有手可用。");
            Assert.False(game.CanSkipTurnFor(a),
                "一锁一盾时不应允许跳过 —— 要强制用那只盾行动。");

            // 而且那只盾真的能当运算源（用它运算会主动破盾）。
            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(a);
            Assert.True(moves.Count > 0, "应有合法步。");

            bool usesShield = false;
            for (int i = 0; i < moves.Count; i++)
                if (ReferenceEquals(moves[i].ActedHand, a.RightHand)) usesShield = true;

            Assert.True(usesShield, "完整盾应能作为运算源。");
        }

        [Test]
        public void Double_shield_still_allows_skipping()
        {
            GameManager game = Match();
            PlayerState a = game.PlayerA;

            a.LeftHand.SetValue(5);
            a.LeftHand.SetShieldState(ShieldState.Formed);
            a.RightHand.SetValue(5);
            a.RightHand.SetShieldState(ShieldState.Formed);

            Assert.True(game.CanSkipTurnFor(a),
                "两只都是完整盾时仍可跳过（原有规则）。");
        }

        private static BoardController Board(out GameManager game, CharacterDefinition a = null,
                                             CharacterDefinition b = null, bool aiB = false)
        {
            var ca = new SimpleChoiceProvider();
            var cb = new SimpleChoiceProvider();
            game = new GameManager(Make("A", a ?? CharacterDefinition.Champion),
                Make("B", b ?? CharacterDefinition.Breaker), ca, cb);
            return new BoardController(game, ca, cb, aiForB: aiB ? new GreedyChoiceProvider() : null);
        }

        [Test]
        public void Frozen_human_keeps_the_match_open_and_can_only_skip()
        {
            var board = Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Frozen);
            board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase);
            Assert.False(game.IsGameOver);
            Assert.True(board.CanSkipTurn);
            Assert.False(board.CanClick(game.PlayerA.LeftHand));
            Assert.False(board.ClickHand(game.PlayerA.RightHand));
            Assert.True(board.SkipTurn());
            Assert.AreSame(game.PlayerB, board.ActivePlayer);
            Assert.AreEqual(1, game.PlayerA.StatusTurnsLeft(StatusEffect.Frozen));
            Assert.AreEqual(1, game.PlayerA.LeftHand.Value);
            Assert.AreEqual(1, game.TurnCount);
        }

        [Test]
        public void Frozen_ai_fighter_skips_instead_of_showing_game_over()
        {
            var board = Board(out var game, CharacterDefinition.SchoolOfEvocation, CharacterDefinition.Breaker, aiB: true);
            game.PlayerB.ApplyStatus(StatusEffect.Frozen);
            game.ForceActivePlayerForTest(game.PlayerB);
            board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase);
            Assert.True(board.MakeAutomaticMove());
            Assert.False(game.IsGameOver);
            Assert.AreSame(game.PlayerA, board.ActivePlayer);
            Assert.AreEqual(1, game.PlayerB.StatusTurnsLeft(StatusEffect.Frozen));
        }

        [Test]
        public void Actual_cold_hit_freezes_ai_fighter_without_ending_the_match()
        {
            var board = Board(out var game, CharacterDefinition.SchoolOfEvocation, CharacterDefinition.Breaker, aiB: true);
            game.PlayerB.LeftHand.SetValue(3);
            game.PlayerB.ApplyStatus(StatusEffect.Frost, source: game.PlayerA);
            ((FingerDiceDuel.Characters.SpecialLogics.SchoolOfEvocationLogic)game.PlayerA.Character).SetPendingDamageType(DamageType.Cold);
            game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);
            Assert.True(game.PlayerB.HasStatus(StatusEffect.Frozen), "真实寒冷命中覆霜应转为冻结。");
            board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase);
            Assert.True(board.MakeAutomaticMove());
            Assert.AreSame(game.PlayerA, board.ActivePlayer);
            Assert.False(game.IsGameOver);
        }

        [Test]
        public void Two_frozen_players_thaw_after_two_of_their_own_skips()
        {
            var board = Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Frozen);
            game.PlayerB.ApplyStatus(StatusEffect.Frozen);
            board.BeginTurn();
            for (int i = 0; i < 4; i++) Assert.True(board.SkipTurn(), "冻结不能误判结束或无限持续。");
            Assert.False(game.PlayerA.HasStatus(StatusEffect.Frozen));
            Assert.False(game.PlayerB.HasStatus(StatusEffect.Frozen));
            Assert.AreEqual(4, game.TurnCount);
            Assert.True(board.CanClick(game.PlayerA.LeftHand));
            Assert.False(board.CanSkipTurn, "解冻且有合法运算时不再提供无条件跳过。");
        }

        [Test]
        public void Locked_and_concealed_hand_combinations_all_offer_a_skip()
        {
            for (int mask = 0; mask < 4; mask++)
            {
                var board = Board(out var game);
                if ((mask & 1) == 0) game.PlayerA.LeftHand.Lock(); else game.PlayerA.LeftHand.Conceal();
                if ((mask & 2) == 0) game.PlayerA.RightHand.Lock(); else game.PlayerA.RightHand.Conceal();
                board.BeginTurn();
                Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase, "手牌组合 " + mask);
                Assert.True(board.CanSkipTurn);
                Assert.False(board.CanClick(game.PlayerA.LeftHand));
                Assert.False(board.CanClick(game.PlayerA.RightHand));
                Assert.True(board.SkipTurn());
                Assert.False(game.IsGameOver);
            }
        }

        [Test]
        public void Ai_with_no_usable_hand_automatically_skips()
        {
            var board = Board(out var game, aiB: true);
            game.PlayerB.LeftHand.Lock();
            game.PlayerB.RightHand.Conceal();
            game.ForceActivePlayerForTest(game.PlayerB);
            board.BeginTurn();
            Assert.True(board.MakeAutomaticMove());
            Assert.AreSame(game.PlayerA, board.ActivePlayer);
            Assert.AreEqual(1, game.TurnCount);
        }

        [Test]
        public void Frozen_skip_resolves_burn_and_ticks_statuses_once()
        {
            var board = Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Frozen);
            game.PlayerA.ApplyStatus(StatusEffect.Burn);
            board.BeginTurn();
            Assert.True(board.SkipTurn());
            Assert.AreEqual(17, game.PlayerA.Health);
            Assert.AreEqual(1, game.PlayerA.StatusTurnsLeft(StatusEffect.Burn));
            Assert.AreEqual(1, game.PlayerA.StatusTurnsLeft(StatusEffect.Frozen));
        }

        [Test]
        public void Frozen_hexer_cannot_activate_health_swap_while_skipping()
        {
            var board = Board(out var game, CharacterDefinition.TheArchfey);
            game.PlayerA.TakeDamage(4);
            game.PlayerA.ApplyStatus(StatusEffect.Frozen);
            board.BeginTurn();
            Assert.True(board.SkipTurn());
            Assert.AreSame(game.PlayerB, board.ActivePlayer);
            Assert.AreEqual(20, game.PlayerB.Health);
            Assert.AreEqual(1, game.PlayerA.StatusTurnsLeft(StatusEffect.Frozen));
        }

        [Test]
        public void Voluntary_hexer_skip_also_finishes_status_duration()
        {
            var board = Board(out var game, CharacterDefinition.TheArchfey);
            game.PlayerA.TakeDamage(4);
            game.PlayerA.ApplyStatus(StatusEffect.Acid);
            board.BeginTurn();
            Assert.True(board.SkipTurn());
            Assert.AreEqual(BoardPhase.ChoosingHealthSwapTarget, board.Phase);
            Assert.True(board.ApplyHealthSwapAndEndTurn(game.PlayerB));
            Assert.AreEqual(16, game.PlayerB.Health);
            Assert.AreEqual(1, game.PlayerA.StatusTurnsLeft(StatusEffect.Acid));
        }

        [Test]
        public void Stunned_player_can_compute_and_abandon_an_unavailable_attack()
        {
            var board = Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Stunned);
            game.PlayerB.LeftHand.SetValue(3);
            Assert.True(game.GetLegalMoves(game.PlayerA).Count > 0);
            board.BeginTurn();
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
            Assert.True(board.CanGiveUpAttack);
            Assert.False(board.CanClick(game.PlayerB.LeftHand));
            Assert.True(board.GiveUpAttack());
            Assert.AreEqual(4, game.PlayerA.LeftHand.Value);
            Assert.AreEqual(20, game.PlayerB.Health);
            Assert.AreEqual(1, game.PlayerA.StatusTurnsLeft(StatusEffect.Stunned));
        }

        [Test]
        public void Panic_against_the_only_opponent_offers_give_up()
        {
            var board = Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Panic, source: game.PlayerB);
            game.PlayerB.LeftHand.SetValue(3);
            board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand);
            board.ClickHand(game.PlayerB.LeftHand);
            Assert.True(board.CanGiveUpAttack);
            Assert.True(board.GiveUpAttack());
            Assert.AreEqual(4, game.PlayerA.LeftHand.Value);
            Assert.AreEqual(20, game.PlayerB.Health);
        }

        [Test]
        public void Direct_resolution_cannot_bypass_status_attack_restrictions()
        {
            foreach (var status in new[] { StatusEffect.Stunned, StatusEffect.Panic })
            {
                Board(out var game);
                game.PlayerA.ApplyStatus(status, source: game.PlayerB);
                game.PlayerB.LeftHand.SetValue(3);
                var turn = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);
                Assert.True(turn.AttackSuppressed);
                Assert.AreEqual(4, game.PlayerA.LeftHand.Value);
                Assert.AreEqual(20, game.PlayerB.Health);
            }
        }

        [Test]
        public void Human_frozen_turn_cannot_be_skipped_by_the_ai_entry_point()
        {
            var board = Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Frozen);
            board.BeginTurn();
            Assert.False(board.MakeAutomaticMove());
            Assert.AreEqual(0, game.TurnCount);
        }

        [Test]
        public void Skip_cannot_bypass_the_attack_target_confirmation()
        {
            var board = Board(out var game);
            game.PlayerA.LeftHand.SetValue(5);
            game.PlayerA.RightHand.SetValue(5);
            game.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);
            game.PlayerA.RightHand.SetShieldState(ShieldState.Formed);
            game.PlayerB.LeftHand.SetValue(3);
            board.BeginTurn();
            Assert.True(board.CanSkipTurn);
            board.ClickHand(game.PlayerA.LeftHand);
            board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
            Assert.False(board.CanSkipTurn);
            Assert.False(board.CanGiveUpAttack);
        }

        [Test]
        public void Headless_match_also_expires_frozen_status_when_skipping()
        {
            Board(out var game);
            game.PlayerA.ApplyStatus(StatusEffect.Frozen);
            game.PlayerB.ApplyStatus(StatusEffect.Frozen);
            var runner = new MatchRunner { MaxTurns = 4 };
            runner.Run(game, new GreedyChoiceProvider(), new GreedyChoiceProvider());
            Assert.False(game.PlayerA.HasStatus(StatusEffect.Frozen));
            Assert.False(game.PlayerB.HasStatus(StatusEffect.Frozen));
        }
    }
}
