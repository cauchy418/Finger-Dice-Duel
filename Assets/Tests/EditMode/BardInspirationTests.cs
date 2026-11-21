using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class BardInspirationTests
    {
        private sealed class Suppressor : CharacterTemplate, IActionInvalidator
        {
            public bool CanInvalidateAction(PlayerState owner, PlayerState actor) => true;
            public bool TryInvalidateAction(PlayerState owner, PlayerState actor, Hand source, Hand target, GameManager game, out string reason)
            { reason = "test suppression"; return true; }
        }
        private sealed class AdjustPolicy : AbstractChoiceProvider
        {
            public int Delta = 1, Calls;
            public override int ChooseSkillOption(string key, IReadOnlyList<SkillChoiceOption> options, PlayerState owner, PlayerState target, GameManager game)
            { if (key == "bard_result_adjust") { Calls++; return Delta; } return base.ChooseSkillOption(key, options, owner, target, game); }
        }
        [Test] public void Human_both_bards_choose_adjustment_before_target_and_commit_final_digit()
        {
            foreach (var role in new[] { CharacterDefinition.CollegeOfLore, CharacterDefinition.CollegeOfValor })
            {
                var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
                var game = CharacterFactory.CreateMatch(role, CharacterDefinition.Blank, a, b);
                game.PlayerA.LeftHand.SetValue(2);
                var board = new BoardController(game, a, b); board.BeginTurn();
                Assert.True(board.ClickHand(game.PlayerA.LeftHand)); Assert.True(board.ClickHand(game.PlayerB.LeftHand));
                Assert.AreEqual(BoardPhase.ChoosingWizardChoice, board.Phase);
                Assert.AreEqual(3, board.PendingSkillOptions.Count); Assert.True(board.ChooseSkillOption(1));
                Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
                Assert.AreEqual(4, board.PreviewHandValue(game.PlayerA.LeftHand).Value);
                Assert.True(board.ClickHand(game.PlayerB.LeftHand)); Assert.AreEqual(4, game.PlayerA.LeftHand.Value);
                Assert.True(game.PlayerB.Health < 20);
                var lore = CharacterBehaviors.FirstImplementer<CollegeOfLoreLogic>(game.PlayerA);
                if (lore != null) { Assert.True(lore.HasRecord(4)); Assert.False(lore.HasRecord(3)); }
            }
        }
        [Test] public void Headless_choice_is_once_per_operation_and_uses_existing_decimal_boundaries()
        {
            foreach (int delta in new[] { -1, 0, 1 })
            {
                var policy = new AdjustPolicy { Delta = delta };
                var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfLore, CharacterDefinition.Blank, policy, new SimpleChoiceProvider());
                game.PlayerA.LeftHand.SetValue(delta == 1 ? 8 : 9);
                game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);
                Assert.AreEqual(1, game.PlayerA.LeftHand.Value);
                Assert.AreEqual(1, policy.Calls);
            }
        }
        [Test] public void Cutting_words_inflicts_two_turn_confusion_directly_with_defender_as_source()
        {
            var policy = new AdjustPolicy();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.CollegeOfLore, new SimpleChoiceProvider(), policy);
            var lore = (CollegeOfLoreLogic)game.PlayerB.Character;
            game.PlayerB.LeftHand.SetValue(4); lore.OnOperationResolved(game.PlayerB, game.PlayerB.LeftHand, 1, 3, HandEffectType.Attack4, game, new SimpleChoiceProvider());
            game.SkillPolicyFor = _ => new GreedyChoiceProvider();
            new AttackContext(game.PlayerA, game.PlayerB, game.PlayerA.LeftHand, 4, game).Resolve(policy, game);
            Assert.AreEqual(2, game.PlayerA.StatusTurnsLeft(StatusEffect.Confusion));
            Assert.AreEqual(game.PlayerB, game.PlayerA.StatusSourceOf(StatusEffect.Confusion)); Assert.False(lore.HasRecord(4));
        }
        [Test] public void Ai_scores_bard_adjusted_lethal_result_without_mutation_and_uses_it_in_real_turn()
        {
            foreach (var role in new[] { CharacterDefinition.CollegeOfLore, CharacterDefinition.CollegeOfValor })
            foreach (bool dual in new[] { false, true })
            {
                var owner = dual ? DualGeneralFactory.Create("A", role == CharacterDefinition.CollegeOfLore ? CharacterDefinition.CollegeOfValor : CharacterDefinition.CollegeOfLore, role) : CharacterFactory.CreatePlayer("A", role);
                var enemy = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank); var ai = new GreedyChoiceProvider();
                var game = new GameManager(owner, enemy, new SimpleChoiceProvider(), new SimpleChoiceProvider()); game.SkillPolicyFor = _ => ai;
                owner.LeftHand.SetValue(2); owner.RightHand.SetValue(1); enemy.SetHealthExact(4);
                var move = ai.ChooseMove(game, owner);
                Assert.AreEqual(owner.LeftHand, move.ActedHand); Assert.AreEqual(2, owner.LeftHand.Value);
                Assert.AreEqual(20, owner.Health); Assert.AreEqual(4, enemy.Health);
                game.ExecuteTurn(owner, move.ActedHand, move.TargetHand);
                Assert.AreEqual(4, owner.LeftHand.Value); Assert.AreEqual(0, enemy.Health);
            }
        }
        [Test] public void Effectless_operations_do_not_offer_bard_choice_or_trigger_effects()
        {
            var policy = new AdjustPolicy();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfLore, CharacterDefinition.Blank, policy, new SimpleChoiceProvider());
            game.PlayerA.LeftHand.SetValue(2);
            Assert.True(game.ApplyEffectlessOperation(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand, "test"));
            Assert.AreEqual(3, game.PlayerA.LeftHand.Value); Assert.AreEqual(0, policy.Calls);
            Assert.AreEqual(0, ((CollegeOfLoreLogic)game.PlayerA.Character).RecordCount);
        }
        [Test] public void Keep_result_is_explicit_and_repeated_choice_cannot_adjust_twice()
        {
            var ca = new SimpleChoiceProvider(); var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfLore, CharacterDefinition.Blank, ca, cb);
            game.PlayerA.LeftHand.SetValue(2); var board = new BoardController(game, ca, cb); board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand); board.ClickHand(game.PlayerB.LeftHand);
            var request = board.PendingWizardChoice; Assert.True(board.ChooseSkillOption(0));
            request.ChooseOption(1); Assert.AreEqual(3, game.PlayerA.LeftHand.Value);
            Assert.AreEqual(0, game.PlayerB.StatusTurnsLeft(StatusEffect.Confusion));
        }
        [Test] public void Prepared_bard_choice_is_cleared_on_cancel_and_revalidates_polymorph()
        {
            var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfLore, CharacterDefinition.Blank, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            var owner = game.PlayerA; owner.LeftHand.SetValue(2); WizardChoice choice = null;
            game.DeferWizardChoice = request => { choice = request; return true; };
            game.RequestBardOperation(owner, owner.LeftHand, game.PlayerB.LeftHand, () => {});
            game.ClearPreparedOperationAddend(); choice.ChooseOption(1);
            Assert.False(game.HasPreparedBard(owner, owner.LeftHand, game.PlayerB.LeftHand));
            game.RequestBardOperation(owner, owner.LeftHand, game.PlayerB.LeftHand, () => {});
            owner.TryPolymorph(); choice.ChooseOption(1);
            Assert.AreEqual(3, game.PreviewOperationResult(owner, owner.LeftHand, game.PlayerB.LeftHand));
        }
        [Test] public void Adjusted_attack_number_is_used_by_defensive_lore_records()
        {
            var policy = new AdjustPolicy();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfValor, CharacterDefinition.CollegeOfLore, policy, new SimpleChoiceProvider());
            var lore = (CollegeOfLoreLogic)game.PlayerB.Character;
            game.PlayerB.RightHand.SetValue(4); lore.OnOperationResolved(game.PlayerB, game.PlayerB.RightHand, 1, 3, HandEffectType.Attack4, game, new SimpleChoiceProvider());
            game.SkillPolicyFor = p => ReferenceEquals(p, game.PlayerA) ? (ISkillChoicePolicy)policy : new GreedyChoiceProvider();
            game.PlayerA.LeftHand.SetValue(2); game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);
            Assert.AreEqual(17, game.PlayerB.Health); Assert.False(lore.HasRecord(4));
            Assert.AreEqual(2, game.PlayerA.StatusTurnsLeft(StatusEffect.Confusion));
        }
        [Test] public void Counterplay_accounts_for_both_bards_adjusting_three_into_an_attack()
        {
            foreach (var role in new[] { CharacterDefinition.CollegeOfLore, CharacterDefinition.CollegeOfValor })
            {
                var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, role, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                game.PlayerA.RightHand.SetValue(9); game.PlayerA.SetHealthExact(4);
                var move = new LegalMove(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);
                var ai = new GreedyChoiceProvider(); int normal = ai.ProjectedCounterplayScoreForTest(move, game.PlayerA, game);
                game.PlayerB.TryPolymorph(); int paused = ai.ProjectedCounterplayScoreForTest(move, game.PlayerA, game);
                Assert.True(normal < paused); Assert.AreEqual(1, game.PlayerB.LeftHand.Value);
            }
        }
        [Test] public void Suppressed_and_dizzy_operations_do_not_offer_bard_skills()
        {
            foreach (bool dizzy in new[] { false, true })
            {
                var owner = CharacterFactory.CreatePlayer("A", CharacterDefinition.CollegeOfLore);
                var enemy = dizzy ? CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank) : new PlayerState("B", new Suppressor());
                var policy = new AdjustPolicy(); var game = new GameManager(owner, enemy, policy, new SimpleChoiceProvider());
                owner.LeftHand.SetValue(2); if (dizzy) owner.ApplyStatus(StatusEffect.Dizzy);
                game.ExecuteTurn(owner, owner.LeftHand, enemy.LeftHand);
                Assert.AreEqual(0, policy.Calls); Assert.AreEqual(dizzy ? 1 : 3, owner.LeftHand.Value);
                if (!dizzy) Assert.AreEqual(0, ((CollegeOfLoreLogic)owner.Character).RecordCount);
            }
        }
        [Test] public void Portent_replacement_follows_bard_adjustment_and_wins_in_preview_and_commit()
        {
            var ca = new SimpleChoiceProvider(); var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfLore, CharacterDefinition.SchoolOfDivination, ca, cb);
            game.PlayerA.LeftHand.SetValue(2); game.PlayerB.LeftHand.SetValue(7); game.PlayerB.RightHand.SetValue(1);
            ((SchoolOfDivinationLogic)game.PlayerB.Character).RecordPortent(4);
            var board = new BoardController(game, ca, cb); board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand); board.ClickHand(game.PlayerB.RightHand);
            Assert.True(board.PendingWizardChoice.Prompt.Contains("吟游激励")); board.ChooseSkillOption(1);
            Assert.True(board.PendingWizardChoice.Prompt.Contains("预知未来")); Assert.AreEqual(4, game.CurrentPortentResult);
            board.ChooseSkillOption(4); Assert.AreEqual(4, board.PreviewHandValue(game.PlayerA.LeftHand).Value);
            board.ClickHand(game.PlayerB.RightHand); Assert.AreEqual(4, game.PlayerA.LeftHand.Value);
            Assert.AreEqual(0, ((SchoolOfDivinationLogic)game.PlayerB.Character).Portents.Count);
        }
        [Test] public void Only_final_adjusted_digit_triggers_attack_shield_or_healing()
        {
            for (int mode = 0; mode < 3; mode++)
            {
                var policy = new AdjustPolicy { Delta = mode == 0 ? -1 : 1 };
                var game = CharacterFactory.CreateMatch(CharacterDefinition.CollegeOfLore, CharacterDefinition.Blank, policy, new SimpleChoiceProvider());
                var owner = game.PlayerA; owner.SetHealthExact(10); owner.LeftHand.SetValue(mode == 2 ? 7 : 3);
                var turn = game.ExecuteTurn(owner, owner.LeftHand, game.PlayerB.LeftHand);
                Assert.AreEqual(mode == 0 ? 3 : mode == 1 ? 5 : 9, owner.LeftHand.Value);
                Assert.AreEqual(mode == 2 ? 19 : 10, owner.Health); Assert.AreEqual(20, game.PlayerB.Health);
                Assert.AreEqual(mode == 1, owner.LeftHand.IsShield);
                Assert.AreEqual(mode == 0 ? HandEffectType.None : mode == 1 ? HandEffectType.Shield : HandEffectType.Heal, turn.Effect);
            }
        }
    }
}
