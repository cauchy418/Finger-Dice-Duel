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
    public class AiSkillPolicyAuditTests
    {
        [Test] public void Gambler_thief_sampling_seed_761708_completes_without_stalling()
        {
            var result = CharacterBalanceTests.RunOne(CharacterDefinition.Gambler, CharacterDefinition.Thief,
                true, 761708, out string error);
            Assert.True(error == null, error);
            Assert.NotNull(result);
        }

        [Test] public void Forced_gambling_takes_precedence_over_a_profitable_AI_steal()
        {
            var gambler = CharacterFactory.CreatePlayer("gambler", CharacterDefinition.Gambler);
            var thief = CharacterFactory.CreatePlayer("thief", CharacterDefinition.Thief);
            var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
            var game = new GameManager(gambler, thief, a, b);
            WizardChoice offered = null;
            game.DeferWizardChoice = choice => { offered = choice; return true; };
            gambler.LeftHand.SetValue(1);
            game.OfferCrowdGambling(gambler, gambler.LeftHand);
            Assert.True(offered.Choose(thief.LeftHand));
            gambler.LeftHand.SetValue(8); gambler.RightHand.SetValue(1);
            thief.LeftHand.SetValue(9); thief.RightHand.SetValue(1);
            var board = new BoardController(game, a, b, ProductionAi.Create(), ProductionAi.Create());
            board.SetFirstPlayer(thief); board.BeginTurn();
            Assert.True(game.RequiresCrowdGambling(thief));
            Assert.True(board.MakeAutomaticMove(), board.ThiefAiTrace + " " + board.DebugPhaseInfo());
            for (int i = 0; i < 10 && game.TurnCount == 0; i++)
                Assert.True(board.ResolvePendingAiChoice(), board.DebugPhaseInfo());
            Assert.True(board.LastTurn.WasGamble);
            Assert.False(game.HasCrowdGambling(thief));
            Assert.AreEqual(8, gambler.LeftHand.Value, "Forced gambling must not exchange hands through stealing.");
        }

        [Test] public void Roster_pre_attack_requests_receive_legal_policy_answers_for_attacking_and_defending()
        {
            int checkedRequests = 0;
            foreach (var role in CharacterDefinition.BuiltIn)
            {
                var owner = CharacterFactory.CreatePlayer("owner", role);
                var other = CharacterFactory.CreatePlayer("other", CharacterDefinition.Blank);
                var ai = new GreedyChoiceProvider();
                var game = new GameManager(owner, other, ai, new SimpleChoiceProvider());
                ai.SetGameForChoices(game); owner.SetHealthExact(8); owner.GainMonkKi(4);
                foreach (bool defending in new[] { false, true })
                for (int result = 1; result <= 9; result++)
                for (int held = 1; held <= 9; held++)
                {
                    owner.LeftHand.SetValue(held); owner.RightHand.SetValue(held);
                    var actor = defending ? other : owner;
                    var target = defending ? owner : other;
                    var context = new PreTurnChoiceContext(actor, target, actor.LeftHand, target.LeftHand)
                    { Manager = game, ResultValue = result, AlivePlayerCount = 2 };
                    var requests = new List<ChoiceRequest>();
                    ((IPreTurnChoiceProvider)owner.Character).CollectPreTurnChoices(context, requests);
                    foreach (var request in requests)
                    {
                        if (request.SkipWhen?.Invoke() == true) continue;
                        var decider = request.Decider ?? actor;
                        if (request.Options.Count > 0)
                        {
                            int value = ai.ChooseSkillOption(request.OptionKey, request.Options, decider, request.RelatedTarget ?? target, game);
                            bool legal = false; foreach (var option in request.Options) legal |= option.Value == value;
                            Assert.True(legal, role.Id + ": " + request.OptionKey);
                        }
                        else if (request.Candidates.Count > 0)
                        {
                            var picked = ai.ChooseHandFor(request, decider);
                            bool legal = picked == request.DeclineResult && request.AllowDecline;
                            foreach (var candidate in request.Candidates) legal |= candidate == picked;
                            Assert.True(legal, role.Id + ": " + request.Effect);
                        }
                        checkedRequests++;
                    }
                }
            }
            Assert.True(checkedRequests > 0);
            System.Console.WriteLine("58-role actual attacking/defending request audit: " + checkedRequests + " legal answers.");
        }

        private static PlayerState Dual(CharacterDefinition role, bool reverse) => DualGeneralFactory.Create("dual",
            reverse ? CharacterDefinition.Champion : role, reverse ? role : CharacterDefinition.Champion);

        [Test] public void Dual_thief_AI_performs_a_profitable_steal_in_both_orders()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var thief = Dual(CharacterDefinition.Thief, reverse);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = new GameManager(thief, foe, a, b);
                var board = new BoardController(game, a, b, aiForA: new GreedyChoiceProvider());
                thief.LeftHand.SetValue(9); thief.RightHand.SetValue(1);
                foe.LeftHand.SetValue(8); foe.RightHand.SetValue(1);
                board.BeginTurn();
                Assert.True(game.CanStealOn(thief));
                Assert.True(board.MakeAutomaticMove());
                Assert.AreEqual(8, thief.LeftHand.Value, board.ThiefAiTrace);
                Assert.AreEqual(9, foe.LeftHand.Value);
                Assert.False(game.CanStealOn(thief), "The next own action must respect the steal cooldown.");
            }
        }

        [Test] public void Human_dual_thief_can_enter_steal_selection()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var thief = Dual(CharacterDefinition.Thief, reverse);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = new GameManager(thief, foe, a, b);
                var board = new BoardController(game, a, b);
                thief.LeftHand.SetValue(9); foe.LeftHand.SetValue(8);
                board.BeginTurn(); board.ClickHand(thief.LeftHand);
                Assert.True(board.CanSteal);
                Assert.True(board.BeginSteal());
                Assert.True(board.ClickHand(foe.LeftHand));
                Assert.AreEqual(8, thief.LeftHand.Value);
                Assert.AreEqual(9, foe.LeftHand.Value);
            }
        }

        [Test] public void Dual_taichi_zero_is_previewed_and_touched_as_one()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var monk = Dual(CharacterDefinition.WayOfTheTaiChi, reverse);
                var actor = CharacterFactory.CreatePlayer("actor", CharacterDefinition.Blank);
                var game = new GameManager(actor, monk, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                actor.LeftHand.SetValue(3); monk.LeftHand.SetValue(0);
                Assert.AreEqual(4, game.PreviewOperationResult(actor, actor.LeftHand, monk.LeftHand));
                game.ExecuteTurn(actor, actor.LeftHand, monk.LeftHand);
                Assert.AreEqual(1, monk.LeftHand.Value);
                Assert.AreEqual(4, actor.LeftHand.Value);
            }
        }

        private static PlayerState Mage(bool reverse) => DualGeneralFactory.Create("mage",
            reverse ? CharacterDefinition.Champion : CharacterDefinition.SchoolOfEvocation,
            reverse ? CharacterDefinition.SchoolOfEvocation : CharacterDefinition.Champion);

        [Test] public void AI_evocation_preparation_answer_is_applied_once_before_the_type_choice()
        {
            foreach (bool dual in new[] { false, true })
            {
                var mage = dual ? Mage(false) : CharacterFactory.CreatePlayer("mage", CharacterDefinition.SchoolOfEvocation);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = new GameManager(mage, foe, a, b);
                var board = new BoardController(game, a, b, aiForA: new GreedyChoiceProvider());
                mage.LeftHand.SetValue(3); mage.RightHand.SetValue(3);
                foe.LeftHand.SetValue(1); foe.RightHand.SetValue(1);
                board.BeginTurn();
                Assert.True(board.MakeAutomaticMove());
                // The first deterministic preparation roll is 52 (<60), the next is 93.
                // Asking the same question twice silently discards the first accepted answer.
                Assert.True(CharacterBehaviors.FirstImplementer<SchoolOfEvocationLogic>(mage).HasPendingType,
                    "An accepted preparation answer must reach the damage-type policy.");
                Assert.AreEqual(20, foe.Health);
            }
        }

        [Test] public void AI_evocation_explicit_decline_is_not_rerolled_during_resolution()
        {
            foreach (bool dual in new[] { false, true })
            {
                var mage = dual ? Mage(true) : CharacterFactory.CreatePlayer("mage", CharacterDefinition.SchoolOfEvocation);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = new GameManager(mage, foe, a, b); var ai = new GreedyChoiceProvider();
                ai.SetGameForChoices(game);
                var request = new ChoiceRequest(HandEffect.SchoolOfEvocationAbandonAttack, "prepare",
                    new List<Hand>(mage.Hands), true, decider: mage, resultValue: 4);
                ai.ChooseHandFor(request, mage); // Advance past 52; actual move declines on 93.
                var board = new BoardController(game, a, b, aiForA: ai);
                mage.LeftHand.SetValue(3); mage.RightHand.SetValue(3);
                foe.LeftHand.SetValue(1); foe.RightHand.SetValue(1);
                board.BeginTurn(); Assert.True(board.MakeAutomaticMove());
                Assert.False(CharacterBehaviors.FirstImplementer<SchoolOfEvocationLogic>(mage).HasPendingType);
                Assert.True(foe.Health < 20, "The declined preparation must attack, rather than ask again on roll 15.");
            }
        }

        [Test] public void Dual_evocation_AI_can_choose_to_prepare_in_both_component_orders()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var mage = Mage(reverse);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var ai = new GreedyChoiceProvider();
                var game = new GameManager(mage, foe, ai, new SimpleChoiceProvider());
                ai.SetGameForChoices(game);
                var request = new ChoiceRequest(HandEffect.SchoolOfEvocationAbandonAttack, "prepare",
                    new List<Hand>(mage.Hands), true, decider: mage, resultValue: 4);
                bool prepared = false;
                for (int i = 0; i < 16; i++) prepared |= ai.ChooseHandFor(request, mage) != null;
                Assert.True(prepared, "Dual container must expose the same preparation choice as a single mage.");
            }
        }

        [Test] public void Dual_evocation_preparation_suppresses_this_hit_and_survives_until_next_attack()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var mage = Mage(reverse);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var game = new GameManager(mage, foe, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                var state = CharacterBehaviors.FirstImplementer<SchoolOfEvocationLogic>(mage);
                mage.LeftHand.SetValue(3); foe.LeftHand.SetValue(1);
                state.SetPendingDamageType(DamageType.Fire);
                state.WantsToAbandonThisTurn = true;
                game.ExecuteTurn(mage, mage.LeftHand, foe.LeftHand);
                Assert.AreEqual(20, foe.Health, "Preparing must suppress damage through the dual container.");
                Assert.AreEqual(DamageType.Fire, state.PendingDamageType);
                game.ExecuteTurn(foe, foe.RightHand, mage.RightHand);
                game.BeginTurn();
                Assert.False(state.WantsToAbandonThisTurn, "Preparation flag belongs to one action.");
                mage.LeftHand.SetValue(3);
                game.ExecuteTurn(mage, mage.LeftHand, foe.LeftHand);
                Assert.True(foe.Health < 20);
                Assert.True(foe.HasStatus(StatusEffect.Burn));
                Assert.False(state.HasPendingType);
            }
        }

        [Test] public void Human_dual_evocation_can_accept_and_choose_damage_type()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var mage = Mage(reverse);
                var foe = CharacterFactory.CreatePlayer("foe", CharacterDefinition.Blank);
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = new GameManager(mage, foe, a, b);
                var board = new BoardController(game, a, b);
                board.BeginTurn();
                mage.LeftHand.SetValue(3); foe.LeftHand.SetValue(1);
                board.ClickHand(mage.LeftHand); board.ClickHand(foe.LeftHand);
                Assert.AreEqual(HandEffect.SchoolOfEvocationAbandonAttack, board.PendingEffect);
                Assert.True(board.AcceptPendingChoice());
                Assert.AreEqual(HandEffect.SchoolOfEvocationDamageType, board.PendingEffect);
                Assert.True(board.ChooseDamageType(DamageType.Fire));
                Assert.AreEqual(DamageType.Fire, CharacterBehaviors.FirstImplementer<SchoolOfEvocationLogic>(mage).PendingDamageType);
            }
        }
    }
}
