using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class OathbreakerTransferTests
    {
        private static GameManager Match(out List<SimpleChoiceProvider> sinks, CharacterDefinition recipient = null)
        {
            var players = new List<PlayerState> {
                CharacterFactory.CreatePlayer("攻击者", CharacterDefinition.Soldier),
                CharacterFactory.CreatePlayer("破誓者", CharacterDefinition.Oathbreaker),
                CharacterFactory.CreatePlayer("持盾者", recipient ?? CharacterDefinition.Soldier),
                CharacterFactory.CreatePlayer("圣武士", CharacterDefinition.Paladin) };
            sinks = new List<SimpleChoiceProvider>(); var providers = new List<IChoiceProvider>();
            for (int i = 0; i < 4; i++) { var sink = new SimpleChoiceProvider(); sinks.Add(sink); providers.Add(sink); }
            var game = GameManager.Create2v2(players, providers);
            players[0].LeftHand.SetValue(3); players[1].RightHand.SetValue(5);
            players[2].LeftHand.SetValue(5); players[2].LeftHand.SetShieldState(ShieldState.Formed);
            return game;
        }
        private static AttackContext Hit(GameManager game)
        {
            var ctx = new AttackContext(game.PlayerA, game.PlayerB, game.PlayerA.LeftHand, 4)
                { SourceHandValueBefore = 3, TargetHandValueBefore = 1 };
            ctx.Resolve(game.ChoiceProviderB, game); return ctx;
        }
        private static BoardController Board(GameManager game, List<SimpleChoiceProvider> sinks)
        {
            var board = new BoardController(game, sinks[0], sinks[1]);
            board.BindPlayer(game.Players[2], null, sinks[2]); board.BindPlayer(game.Players[3], null, sinks[3]);
            board.BeginTurn(); board.ClickHand(game.PlayerA.LeftHand); board.ClickHand(game.PlayerB.LeftHand);
            board.ConfirmTargetPlayer(); return board;
        }
        [Test]
        public void Whole_attack_is_transferred_and_the_recipients_shield_blocks_it()
        {
            var game = Match(out var sinks); var target = game.Players[2];
            sinks[1].SetChoice(HandEffect.OathbreakerTransfer, target.RightHand);
            var ctx = Hit(game);
            Assert.AreSame(target, ctx.Defender); Assert.True(ctx.WasRedirected);
            Assert.AreEqual(20, game.PlayerB.Health); Assert.AreEqual(20, target.Health);
            Assert.AreEqual(ShieldState.Broken, target.LeftHand.State);
            Assert.AreEqual(5, game.PlayerB.RightHand.Value);
        }
        [Test]
        public void No_five_means_no_transfer_even_with_a_recorded_answer()
        {
            var game = Match(out var sinks); game.PlayerB.RightHand.SetValue(2);
            sinks[1].SetChoice(HandEffect.OathbreakerTransfer, game.Players[2].LeftHand);
            Assert.AreSame(game.PlayerB, Hit(game).Defender);
        }
        [Test]
        public void Declining_keeps_the_original_target()
        {
            var game = Match(out _); var ctx = Hit(game);
            Assert.False(ctx.WasRedirected); Assert.AreEqual(15, game.PlayerB.Health);
        }
        [Test]
        public void Attacker_self_and_broken_shields_are_not_candidates()
        {
            var game = Match(out var sinks); game.PlayerA.RightHand.SetValue(5);
            game.PlayerA.RightHand.SetShieldState(ShieldState.Formed);
            game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            game.Players[2].LeftHand.SetShieldState(ShieldState.Broken);
            foreach (var hand in new[] { game.PlayerA.RightHand, game.PlayerB.RightHand, game.Players[2].LeftHand })
            {
                sinks[1].SetChoice(HandEffect.OathbreakerTransfer, hand);
                Assert.AreSame(game.PlayerB, Hit(game).Defender);
            }
        }
        [Test]
        public void A_target_that_died_or_lost_its_shield_is_revalidated()
        {
            foreach (bool dead in new[] { false, true })
            {
                var game = Match(out var sinks); var target = game.Players[2];
                sinks[1].SetChoice(HandEffect.OathbreakerTransfer, target.LeftHand);
                if (dead) target.TakeDamage(100); else target.LeftHand.SetShieldState(ShieldState.Broken);
                Assert.AreSame(game.PlayerB, Hit(game).Defender);
            }
        }
        [Test]
        public void Actual_recipient_uses_its_own_dodge_choice_and_keeps_its_shield()
        {
            var game = Match(out var sinks, CharacterDefinition.Ranger); var target = game.Players[2];
            sinks[1].SetChoice(HandEffect.OathbreakerTransfer, target.LeftHand);
            sinks[2].SetChoice(HandEffect.RangerDodge, target.RightHand);
            var ctx = Hit(game); Assert.True(ctx.WasDodged);
            Assert.AreEqual(2, target.RightHand.Value); Assert.AreEqual(ShieldState.Formed, target.LeftHand.State);
        }
        [Test]
        public void Human_defender_can_select_transfer_in_the_real_board_flow()
        {
            var game = Match(out var sinks); var board = Board(game, sinks);
            Assert.AreEqual(HandEffect.OathbreakerTransfer, board.PendingEffect);
            Assert.AreSame(game.PlayerB, board.Chooser); Assert.True(board.IsHumanChooser);
            Assert.True(board.CanDeclinePendingChoice);
            Assert.True(board.ClickHand(game.Players[2].RightHand));
            Assert.AreEqual(HandEffect.PaladinRedirect, board.PendingEffect);
            Assert.True(board.DeclinePendingChoice());
            Assert.AreEqual(20, game.PlayerB.Health);
            Assert.AreEqual(ShieldState.Broken, game.Players[2].LeftHand.State);
        }
        [Test]
        public void Paladin_gets_a_visible_choice_after_transfer_and_can_take_the_attack()
        {
            var game = Match(out var sinks); var board = Board(game, sinks);
            board.ClickHand(game.Players[2].LeftHand);
            Assert.AreEqual(HandEffect.PaladinRedirect, board.PendingEffect);
            Assert.AreSame(game.Players[3], board.Chooser);
            Assert.True(board.ClickHand(game.Players[3].LeftHand));
            Assert.AreEqual(16, game.Players[3].Health);
            Assert.AreEqual(20, game.PlayerB.Health);
            Assert.AreEqual(ShieldState.Formed, game.Players[2].LeftHand.State);
        }
        [Test]
        public void Human_decline_finishes_the_attack_without_reprompting_transfer()
        {
            var game = Match(out var sinks); var board = Board(game, sinks);
            Assert.True(board.DeclinePendingChoice());
            Assert.AreEqual(15, game.PlayerB.Health);
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase);
        }
        [Test]
        public void One_on_one_has_no_eligible_transfer_target()
        {
            var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Soldier);
            var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Oathbreaker);
            b.RightHand.SetValue(5); a.RightHand.SetValue(5); a.RightHand.SetShieldState(ShieldState.Formed);
            var game = new GameManager(a, b, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            var requests = new List<ChoiceRequest>(); a.LeftHand.SetValue(3);
            ((OathbreakerLogic)b.Character).CollectPreTurnChoices(new PreTurnChoiceContext(a,b,a.LeftHand,b.LeftHand) { Manager=game }, requests);
            Assert.AreEqual(0, requests.Count);
        }
        [Test]
        public void Greedy_ai_prefers_an_enemy_shield_holder_over_its_teammate()
        {
            var game = Match(out _); var enemy = game.Players[3];
            enemy.RightHand.SetValue(5); enemy.RightHand.SetShieldState(ShieldState.Formed);
            var requests = new List<ChoiceRequest>();
            ((OathbreakerLogic)game.PlayerB.Character).CollectPreTurnChoices(
                new PreTurnChoiceContext(game.PlayerA,game.PlayerB,game.PlayerA.LeftHand,game.PlayerB.LeftHand) {Manager=game}, requests);
            var ai = new GreedyChoiceProvider { Game=game };
            Assert.AreSame(enemy, game.OwnerOfHand(ai.ChooseHandFor(requests[0],game.PlayerB)));
        }
        [Test]
        public void A_previous_paladin_answer_cannot_redirect_a_later_unanswered_attack()
        {
            var game = Match(out var sinks); var paladin = game.Players[3];
            sinks[1].SetChoice(HandEffect.OathbreakerTransfer, game.Players[2].LeftHand);
            sinks[3].SetChoice(HandEffect.PaladinRedirect, paladin.LeftHand);
            game.AnsweredLookupFor = (owner, effect, variant) =>
                owner == game.PlayerB && effect == HandEffect.OathbreakerTransfer ? game.Players[2].LeftHand : null;
            var ctx = Hit(game);
            Assert.AreSame(game.Players[2], ctx.Defender);
            Assert.AreEqual(20, paladin.Health);
            Assert.AreEqual(ShieldState.Broken, game.Players[2].LeftHand.State);
        }
    }
}
