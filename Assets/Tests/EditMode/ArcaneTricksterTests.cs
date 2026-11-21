using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class ArcaneTricksterTests
    {
        private static GameManager Match() => CharacterFactory.CreateMatch(CharacterDefinition.ArcaneTrickster, CharacterDefinition.Blank, new SimpleChoiceProvider(), new SimpleChoiceProvider());
        private static List<WizardChoice> Capture(GameManager game)
        { var requests = new List<WizardChoice>(); game.DeferWizardChoice = c => { requests.Add(c); return true; }; return requests; }
        [Test] public void Human_start_and_end_exchange_are_independent_single_choices_before_action_and_handoff()
        {
            var game = Match(); var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner);
            owner.LeftHand.SetValue(7); game.PlayerB.LeftHand.SetValue(1);
            var board = new BoardController(game, (IPlayerChoiceSink)game.ChoiceProviderA, (IPlayerChoiceSink)game.ChoiceProviderB);
            board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice, board.Phase);
            var start = board.PendingWizardChoice;
            Assert.AreEqual(owner, start.Owner); Assert.True(start.Prompt.Contains("回合开始"));
            Assert.False(board.ClickHand(game.PlayerB.LeftHand)); Assert.True(board.ClickHand(owner.LeftHand));
            Assert.AreEqual(7, skill.MageHandValue); Assert.AreEqual(1, owner.LeftHand.Value);
            start.Choose(owner.RightHand); Assert.AreEqual(7, skill.MageHandValue);
            Assert.True(board.ClickHand(owner.LeftHand)); Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice, board.Phase);
            var end = board.PendingWizardChoice;
            Assert.True(end.Prompt.Contains("回合结束")); Assert.True(board.ClickHand(owner.LeftHand));
            Assert.AreEqual(7, owner.LeftHand.Value); Assert.AreEqual(2, skill.MageHandValue);
            end.Choose(owner.RightHand); Assert.AreEqual(2, skill.MageHandValue); Assert.AreEqual(game.PlayerB, board.ActivePlayer);
        }
        [Test] public void Start_exchange_is_only_for_active_owner_and_respects_freeze_locks_and_forms()
        {
            var game = Match(); var owner = game.PlayerA; var requests = Capture(game);
            owner.LeftHand.Lock(); game.BeginTurn();
            Assert.AreEqual(1, requests.Count); Assert.AreEqual(owner.RightHand, requests[0].Candidates[0]);
            Assert.AreEqual(1, requests[0].Candidates.Count); requests[0].Choose(null);
            requests.Clear(); game.GiveUpTurn(); game.BeginTurn(); Assert.AreEqual(0, requests.Count);
            game.GiveUpTurn(); owner.ApplyStatus(StatusEffect.Frozen); game.BeginTurn(); Assert.AreEqual(0, requests.Count);
            owner.RemoveStatus(StatusEffect.Frozen); owner.TryPolymorph(); game.BeginTurn(); Assert.AreEqual(0, requests.Count);
            owner.EndPolymorph(); game.BeginTurn(); Assert.AreEqual(1, requests.Count);
        }
        [Test] public void Start_decline_is_final_and_next_own_turn_offers_again()
        {
            var game = Match(); var owner = game.PlayerA; var requests = Capture(game);
            owner.LeftHand.SetValue(7); game.BeginTurn(); var start = requests[0];
            start.Choose(null); start.Choose(owner.LeftHand);
            Assert.AreEqual(7, owner.LeftHand.Value); Assert.AreEqual(1, ArcaneTricksterLogic.For(owner).MageHandValue);
            game.GiveUpTurn(); game.BeginTurn(); Assert.AreEqual(1, requests.Count);
            game.GiveUpTurn(); game.BeginTurn(); Assert.AreEqual(2, requests.Count);
            requests[1].Choose(owner.LeftHand); Assert.AreEqual(7, ArcaneTricksterLogic.For(owner).MageHandValue);
        }
        [Test] public void Start_pending_choice_revalidates_hand_status_and_shapeshift()
        {
            for (int mode = 0; mode < 4; mode++)
            {
                var owner = DualGeneralFactory.Create("A", CharacterDefinition.ArcaneTrickster, CharacterDefinition.CircleOfTheMoon);
                var game = new GameManager(owner, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), new SimpleChoiceProvider(), new SimpleChoiceProvider());
                var skill = ArcaneTricksterLogic.For(owner); var requests = Capture(game); owner.LeftHand.SetValue(7);
                game.BeginTurn(); var start = requests[0];
                if (mode == 0) owner.LeftHand.Lock();
                else if (mode == 1) owner.ApplyStatus(StatusEffect.Frozen);
                else if (mode == 2) owner.TryPolymorph();
                else { owner.LeftHand.SetValue(1); owner.RightHand.SetValue(9); Assert.True(owner.EnterWild(1, 0, game)); }
                start.Choose(owner.LeftHand); Assert.AreEqual(1, skill.MageHandValue);
            }
        }
        [Test] public void Human_start_exchange_breaks_normal_shield_and_then_allows_operation()
        {
            var game = Match(); var owner = game.PlayerA;
            owner.LeftHand.SetValue(5); owner.LeftHand.SetShieldState(ShieldState.Formed);
            var board = new BoardController(game, (IPlayerChoiceSink)game.ChoiceProviderA, (IPlayerChoiceSink)game.ChoiceProviderB);
            board.BeginTurn(); Assert.True(board.ClickHand(owner.LeftHand));
            Assert.AreEqual(1, owner.LeftHand.Value); Assert.False(owner.LeftHand.IsShield);
            Assert.AreEqual(5, ArcaneTricksterLogic.For(owner).MageHandValue);
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase); Assert.True(board.ClickHand(owner.LeftHand));
        }
        [Test] public void Registered_chaotic_neutral_rogue_has_explicit_profile_and_two_body_hands()
        {
            var game = Match(); var owner = game.PlayerA;
            Assert.AreEqual("rogue", owner.Definition.Profession.Id);
            Assert.AreEqual(Alignment.ChaoticNeutral, CharacterCodex.For(owner.Definition).Alignment);
            Assert.AreEqual(0, owner.Character.AttackBonus); Assert.AreEqual(-1, owner.Character.DefenseBonus);
            Assert.AreEqual(CharacterTemplate.InfiniteAttackRange, GameManager.BaseRangeOf(owner));
            Assert.True(CharacterFactory.IsRegistered(SpecialLogicType.ArcaneTrickster));
            Assert.True(HandValueProfiles.HasExplicitProfile(owner.Definition));
            Assert.AreEqual(owner.Definition, CharacterDefinition.Find("arcane_trickster"));
            Assert.AreEqual(2, owner.Hands.Length); Assert.AreEqual(1, ArcaneTricksterLogic.For(owner).MageHandValue);
            foreach (var move in game.GetLegalMoves(game.PlayerB)) Assert.True(owner.Owns(move.TargetHand));
        }
        [Test] public void Close_slashing_and_distant_piercing_use_real_attack_distance()
        {
            var players = new PlayerState[4]; var providers = new IChoiceProvider[4];
            for (int i = 0; i < 4; i++) { players[i] = CharacterFactory.CreatePlayer("P" + i, i == 0 ? CharacterDefinition.ArcaneTrickster : CharacterDefinition.Blank); providers[i] = new SimpleChoiceProvider(); }
            var game = GameManager.Create2v2(players, providers); var skill = ArcaneTricksterLogic.For(players[0]);
            Assert.AreEqual(AttackDamageType.Slashing, skill.AttackTypeOf(new AttackContext(players[0], players[1], players[0].LeftHand, 4, game)));
            Assert.AreEqual(2, game.DistanceBetween(players[0], players[2]));
            Assert.AreEqual(AttackDamageType.Piercing, skill.AttackTypeOf(new AttackContext(players[0], players[2], players[0].LeftHand, 4, game)));
        }
        [Test] public void Exchange_changes_only_digits_and_does_not_trigger_healing_attack_or_shield()
        {
            var game = Match(); var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner); owner.TakeDamage(10);
            foreach (int digit in new[] { 4, 6, 8, 9, 5 })
            {
                owner.LeftHand.SetValue(digit); Assert.True(skill.Exchange(owner, owner.LeftHand, game));
                Assert.AreEqual(digit, skill.MageHandValue); Assert.True(skill.Exchange(owner, owner.RightHand, game));
                Assert.AreEqual(digit, owner.RightHand.Value); Assert.False(owner.RightHand.IsShield);
                Assert.AreEqual(10, owner.Health); Assert.AreEqual(20, game.PlayerB.Health);
            }
            Assert.AreEqual(0, game.TurnCount);
        }
        [Test] public void Human_turn_end_pauses_for_body_hand_click_and_then_starts_next_player()
        {
            var game = Match(); var board = new BoardController(game, (IPlayerChoiceSink)game.ChoiceProviderA, (IPlayerChoiceSink)game.ChoiceProviderB);
            game.PlayerA.LeftHand.SetValue(1); game.PlayerB.LeftHand.SetValue(1); board.BeginTurn(); Assert.True(board.DeclinePendingChoice());
            Assert.True(board.ClickHand(game.PlayerA.LeftHand)); Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice, board.Phase);
            Assert.AreEqual(game.PlayerA, board.PendingWizardChoice.Owner); Assert.AreEqual(0, board.PendingSkillOptions.Count);
            Assert.False(board.ClickHand(game.PlayerB.LeftHand)); Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.AreEqual(1, game.PlayerA.LeftHand.Value); Assert.AreEqual(2, ArcaneTricksterLogic.For(game.PlayerA).MageHandValue);
            Assert.AreEqual(game.PlayerB, board.ActivePlayer); Assert.AreEqual(1, game.TurnCount);
        }
        [Test] public void Human_can_decline_without_changing_digits()
        {
            var game = Match(); var board = new BoardController(game, (IPlayerChoiceSink)game.ChoiceProviderA, (IPlayerChoiceSink)game.ChoiceProviderB);
            board.BeginTurn(); Assert.True(board.DeclinePendingChoice()); board.ClickHand(game.PlayerA.LeftHand); board.ClickHand(game.PlayerB.LeftHand);
            Assert.True(board.CanDeclinePendingChoice); Assert.True(board.DeclinePendingChoice());
            Assert.AreEqual(2, game.PlayerA.LeftHand.Value); Assert.AreEqual(1, ArcaneTricksterLogic.For(game.PlayerA).MageHandValue);
        }
        [Test] public void Locked_concealed_and_restrained_hands_are_excluded_and_revalidated()
        {
            var game = Match(); var owner = game.PlayerA; var requests = Capture(game); var skill = ArcaneTricksterLogic.For(owner);
            owner.LeftHand.Lock(); game.ResolveStatusTurnEnd(owner); Assert.AreEqual(1, requests.Count);
            Assert.AreEqual(1, requests[0].Candidates.Count); Assert.AreEqual(owner.RightHand, requests[0].Candidates[0]);
            owner.RightHand.Lock(); requests[0].Choose(owner.RightHand); Assert.AreEqual(1, skill.MageHandValue);
            requests.Clear(); game.ResolveStatusTurnEnd(owner); Assert.AreEqual(0, requests.Count);
            owner.LeftHand.Unlock(); owner.LeftHand.IsRestrained = true; Assert.False(skill.Exchange(owner, owner.LeftHand, game)); owner.LeftHand.IsRestrained = false; owner.LeftHand.SetShieldState(ShieldState.Concealed); Assert.False(skill.Exchange(owner, owner.LeftHand, game));
        }
        [Test] public void Frozen_last_turn_does_not_offer_exchange_after_status_expires()
        {
            var game = Match(); var requests = Capture(game); game.PlayerA.ApplyStatus(StatusEffect.Frozen, 1);
            game.ResolveStatusTurnEnd(game.PlayerA); Assert.AreEqual(0, requests.Count); Assert.False(game.PlayerA.HasStatus(StatusEffect.Frozen));
            game.ResolveStatusTurnEnd(game.PlayerA); Assert.AreEqual(1, requests.Count);
        }
        [Test] public void Real_board_double_shield_skip_offers_exchange_and_breaks_chosen_shield()
        {
            var game = Match(); var board = new BoardController(game, (IPlayerChoiceSink)game.ChoiceProviderA, (IPlayerChoiceSink)game.ChoiceProviderB);
            foreach (var hand in game.PlayerA.Hands) { hand.SetValue(5); hand.SetShieldState(ShieldState.Formed); }
            board.BeginTurn(); Assert.True(board.DeclinePendingChoice()); Assert.True(board.SkipTurn()); Assert.AreEqual(BoardPhase.ChoosingWizardChoice, board.Phase);
            Assert.True(board.ClickHand(game.PlayerA.LeftHand)); Assert.AreEqual(1, game.PlayerA.LeftHand.Value);
            Assert.False(game.PlayerA.LeftHand.IsShield); Assert.True(game.PlayerA.RightHand.IsShield);
        }
        private sealed class ShieldHook : CharacterTemplate
        {
            public int Breaks; public bool Kill;
            public override void OnOwnShieldBroken(PlayerState owner, Hand hand, GameManager manager) { Breaks++; if (Kill) owner.SetHealthExact(0); }
        }
        [Test] public void Shield_exchange_notifies_normal_break_hooks_and_does_not_form_new_shields()
        {
            var skill = new ArcaneTricksterLogic(); var hook = new ShieldHook(); var owner = new PlayerState("A", new DualGeneralBehavior(skill, hook));
            var game = new GameManager(owner, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), new SimpleChoiceProvider(), new SimpleChoiceProvider());
            owner.LeftHand.SetValue(5); owner.LeftHand.SetShieldState(ShieldState.Formed); Assert.True(skill.Exchange(owner, owner.LeftHand, game));
            Assert.AreEqual(1, hook.Breaks); Assert.AreEqual(5, skill.MageHandValue);
            owner.RightHand.SetValue(7); Assert.True(skill.Exchange(owner, owner.RightHand, game));
            Assert.AreEqual(5, owner.RightHand.Value); Assert.AreEqual(ShieldState.Broken, owner.RightHand.State); Assert.AreEqual(1, hook.Breaks);
        }
        [Test] public void Shield_break_death_aborts_exchange_and_dead_owner_cannot_receive_choices()
        {
            var skill = new ArcaneTricksterLogic(); var hook = new ShieldHook { Kill = true }; var owner = new PlayerState("A", new DualGeneralBehavior(skill, hook));
            var game = new GameManager(owner, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), new SimpleChoiceProvider(), new SimpleChoiceProvider());
            owner.LeftHand.SetValue(5); owner.LeftHand.SetShieldState(ShieldState.Formed); Assert.False(skill.Exchange(owner, owner.LeftHand, game));
            Assert.AreEqual(1, skill.MageHandValue); Assert.AreEqual(5, owner.LeftHand.Value);
            var requests = Capture(game); game.ResolveStatusTurnEnd(owner); Assert.AreEqual(0, requests.Count);
        }
        [Test] public void Pending_choice_checks_death_freeze_and_polymorph_again()
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                var game = Match(); var requests = Capture(game); var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner); owner.LeftHand.SetValue(8);
                game.ResolveStatusTurnEnd(owner);
                if (mode == 0) owner.SetHealthExact(0); else if (mode == 1) owner.ApplyStatus(StatusEffect.Frozen); else owner.TryPolymorph();
                requests[0].Choose(owner.LeftHand); Assert.AreEqual(8, owner.LeftHand.Value); Assert.AreEqual(1, skill.MageHandValue);
            }
        }
        [Test] public void Dual_general_in_either_slot_retains_store_across_sheep_and_wild_shape()
        {
            foreach (bool primary in new[] { false, true })
            {
                var owner = DualGeneralFactory.Create("A", primary ? CharacterDefinition.ArcaneTrickster : CharacterDefinition.CircleOfTheMoon,
                    primary ? CharacterDefinition.CircleOfTheMoon : CharacterDefinition.ArcaneTrickster);
                var game = new GameManager(owner, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), new SimpleChoiceProvider(), new SimpleChoiceProvider());
                var skill = ArcaneTricksterLogic.For(owner); owner.LeftHand.SetValue(8); skill.Exchange(owner, owner.LeftHand, game);
                owner.TryPolymorph(1); Assert.True(ArcaneTricksterLogic.For(owner) == null); Assert.AreEqual(skill, ArcaneTricksterLogic.StoredFor(owner));
                var requests = Capture(game); game.ResolveStatusTurnEnd(owner); Assert.AreEqual(0, requests.Count);
                Assert.AreEqual(8, ArcaneTricksterLogic.For(owner).MageHandValue);
                owner.LeftHand.SetValue(1); owner.RightHand.SetValue(9); Assert.True(owner.EnterWild(1, 0, game)); requests.Clear();
                game.ResolveStatusTurnEnd(owner); foreach (var request in requests) Assert.False(request.Prompt.Contains("法师之手诈术")); Assert.AreEqual(skill, ArcaneTricksterLogic.StoredFor(owner));
                owner.EndWild(); requests.Clear(); game.ResolveStatusTurnEnd(owner); Assert.AreEqual(1, requests.Count); Assert.AreEqual(8, skill.MageHandValue);
                game.ResetMatch(); Assert.AreEqual(1, skill.MageHandValue);
            }
        }
        [Test] public void Multiplayer_modes_keep_exchange_on_owner_even_when_current_player_has_advanced()
        {
            foreach (int size in new[] { 4, 8 })
            {
                var players = new PlayerState[size]; var providers = new IChoiceProvider[size]; var teams = new int[size];
                for (int i = 0; i < size; i++) { players[i] = CharacterFactory.CreatePlayer("P" + i, i == 0 ? CharacterDefinition.ArcaneTrickster : CharacterDefinition.Champion); providers[i] = new SimpleChoiceProvider(); teams[i] = i % 3; }
                var game = size == 4 ? GameManager.Create2v2(players, providers) : GameManager.CreateEight(players, providers, teams);
                var requests = Capture(game); game.GiveUpTurn(); game.ResolveStatusTurnEnd(players[0]);
                Assert.AreEqual(1, requests.Count); Assert.AreEqual(players[0], requests[0].Owner);
                players[0].RightHand.SetValue(7); requests[0].Choose(players[0].RightHand);
                Assert.AreEqual(7, ArcaneTricksterLogic.For(players[0]).MageHandValue); Assert.AreEqual(1, players[1].RightHand.Value);
            }
        }
        [Test] public void Identity_mode_and_dual_general_distant_attacks_use_the_same_skill()
        {
            var players = new PlayerState[8]; var providers = new IChoiceProvider[8];
            for (int i = 0; i < 8; i++) { players[i] = i == 0 ? DualGeneralFactory.Create("A", CharacterDefinition.Magician, CharacterDefinition.ArcaneTrickster) : CharacterFactory.CreatePlayer("P" + i, CharacterDefinition.Champion); providers[i] = new SimpleChoiceProvider(); }
            var roles = new[] { IdentityRole.Lord, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Loyalist, IdentityRole.Rebel, IdentityRole.Traitor, IdentityRole.Rebel };
            var game = GameManager.CreateIdentityEight(players, providers, roles);
            var hit = new AttackContext(players[0], players[4], players[0].LeftHand, 4, game); hit.Resolve(providers[4], game);
            Assert.AreEqual(AttackDamageType.Piercing, hit.AttackType);
            var requests = Capture(game); game.GiveUpTurn(); game.ResolveStatusTurnEnd(players[0]);
            Assert.AreEqual(1, requests.Count); players[0].LeftHand.SetValue(6); requests[0].Choose(players[0].LeftHand);
            Assert.AreEqual(6, ArcaneTricksterLogic.For(players[0]).MageHandValue); Assert.AreEqual(1, players[0].LeftHand.Value);
        }
        [Test] public void Campaign_graph_clone_retains_independent_stored_digit_and_body_hands()
        {
            var game = Match(); var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner);
            owner.LeftHand.SetValue(8); skill.Exchange(owner, owner.LeftHand, game);
            var cloneMethod = typeof(CampaignCheckpoint).GetMethod("Clone", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var copy = (PlayerState)cloneMethod.Invoke(new CampaignCheckpoint(), new object[] { owner });
            Assert.AreEqual(8, ArcaneTricksterLogic.For(copy).MageHandValue); Assert.False(ReferenceEquals(owner.LeftHand, copy.LeftHand));
            copy.LeftHand.SetValue(6); Assert.AreEqual(1, owner.LeftHand.Value);
            var otherGame = new GameManager(copy, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), new SimpleChoiceProvider(), new SimpleChoiceProvider());
            ArcaneTricksterLogic.For(copy).Exchange(copy, copy.LeftHand, otherGame); Assert.AreEqual(8, skill.MageHandValue); Assert.AreEqual(6, ArcaneTricksterLogic.For(copy).MageHandValue);
        }
        [Test] public void Same_digit_preserves_complete_shield_and_shield_storage_is_not_a_targetable_shield()
        {
            var game = Match(); var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner);
            owner.LeftHand.SetValue(5); skill.Exchange(owner, owner.LeftHand, game);
            owner.RightHand.SetValue(5); owner.RightHand.SetShieldState(ShieldState.Formed);
            Assert.True(skill.Exchange(owner, owner.RightHand, game)); Assert.True(owner.RightHand.IsShield); Assert.AreEqual(5, skill.MageHandValue);
            owner.RightHand.SetShieldState(ShieldState.Broken); Assert.False(owner.HasAnyShield);
            var hit = new AttackContext(game.PlayerB, owner, game.PlayerB.LeftHand, 4, game); hit.Resolve(game.ChoiceProviderA, game);
            Assert.True(owner.Health < 20); Assert.AreEqual(5, skill.MageHandValue);
        }
        [Test] public void Real_board_ai_resolves_start_and_end_exchange_without_human_prompt_and_full_match_finishes()
        {
            var game = Match(); var owner = game.PlayerA;
            owner.LeftHand.SetValue(7); owner.RightHand.SetValue(7); game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(3);
            var board = new BoardController(game, (IPlayerChoiceSink)game.ChoiceProviderA, (IPlayerChoiceSink)game.ChoiceProviderB, aiForA: ProductionAi.Create(game));
            board.BeginTurn(); Assert.AreEqual(7, ArcaneTricksterLogic.For(owner).MageHandValue);
            Assert.True(owner.LeftHand.Value == 1 || owner.RightHand.Value == 1); Assert.True(board.PendingWizardChoice == null);
            var source = owner.LeftHand.Value == 1 ? owner.LeftHand : owner.RightHand;
            Assert.True(board.ClickHand(source)); Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            if (ReferenceEquals(board.ActivePlayer, owner)) Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(game.PlayerB, board.ActivePlayer); Assert.True(board.PendingWizardChoice == null);
            var result = CharacterBalanceTests.RunOne(CharacterDefinition.ArcaneTrickster, CharacterDefinition.Champion, true, 761708, out string error);
            Assert.True(error == null, error); Assert.NotNull(result);
        }
        [Test] public void Ai_end_exchange_does_not_trade_safe_digits_for_an_attack_it_cannot_take()
        {
            var game = Match(); var owner = game.PlayerA; var ai = new GreedyChoiceProvider();
            owner.LeftHand.SetValue(7); owner.RightHand.SetValue(7);
            game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(3);
            game.SkillPolicyFor = _ => ai;
            // At start, reserve 1 enables 1+3=4. At end it only feeds the foe 3+1=4;
            // retaining 7 keeps both enemy operations at 1 until the next start swap.
            game.ResolveStatusTurnEnd(owner);
            Assert.AreEqual(7, owner.LeftHand.Value); Assert.AreEqual(7, owner.RightHand.Value);
            Assert.AreEqual(1, ArcaneTricksterLogic.For(owner).MageHandValue);
        }
        [Test] public void Projected_counterplay_includes_reserve_source_without_mutating_the_board()
        {
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.ArcaneTrickster,
                new SimpleChoiceProvider(), new SimpleChoiceProvider());
            var actor = game.PlayerA; var foe = game.PlayerB; var ai = new GreedyChoiceProvider();
            actor.LeftHand.SetValue(1); actor.RightHand.SetValue(1);
            foe.LeftHand.SetValue(2); foe.RightHand.SetValue(2);
            var move = new LegalMove(actor, actor.LeftHand, foe.LeftHand);
            var score = typeof(GreedyChoiceProvider).GetMethod("ProjectedCounterplayScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            int dangerous = (int)score.Invoke(ai, new object[] { move, actor, game, (int?)7 });
            foe.LeftHand.SetValue(3); ArcaneTricksterLogic.For(foe).Exchange(foe, foe.LeftHand, game);
            foe.LeftHand.SetValue(2);
            int safe = (int)score.Invoke(ai, new object[] { move, actor, game, (int?)7 });
            Assert.True(dangerous < safe, "Reserve 1+7 attacks; visible 2+7 only heals at full health.");
            Assert.AreEqual(1, actor.LeftHand.Value); Assert.AreEqual(2, foe.LeftHand.Value);
            Assert.AreEqual(3, ArcaneTricksterLogic.For(foe).MageHandValue);
        }
        [Test] public void Projected_exchange_to_stored_five_matches_actual_broken_shield_without_live_mutation()
        {
            var game = Match(); var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner);
            owner.RightHand.SetValue(5); skill.Exchange(owner, owner.RightHand, game);
            owner.LeftHand.SetValue(5); owner.LeftHand.SetShieldState(ShieldState.Formed);
            owner.RightHand.SetValue(2); game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(3);
            var ai = ProductionAi.Create(game);
            var value = typeof(GreedyChoiceProvider).GetMethod("MageHandBoardValue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            double projected = (double)value.Invoke(ai, new object[] { owner, owner.RightHand, 5, game });
            Assert.AreEqual(2, owner.RightHand.Value); Assert.True(owner.LeftHand.IsShield);
            Assert.True(skill.Exchange(owner, owner.RightHand, game));
            Assert.AreEqual(ShieldState.Broken, owner.RightHand.State);
            double actual = (double)value.Invoke(ai, new object[] { owner, null, 0, game });
            Assert.AreEqual(projected, actual);
        }
        private sealed class OperationInvalidator : CharacterTemplate, IActionInvalidator
        {
            public bool Active;
            public bool CanInvalidateAction(PlayerState owner, PlayerState actor) => Active;
            public bool TryInvalidateAction(PlayerState owner, PlayerState actor, Hand source, Hand operand,
                GameManager game, out string reason) { reason = "test suppression"; return Active; }
        }
        [Test] public void Exchange_projection_suppresses_phantom_effects_for_dizzy_and_invalidated_actions()
        {
            foreach (bool invalidated in new[] { false, true })
            {
                var invalidator = new OperationInvalidator { Active = invalidated };
                var game = new GameManager(CharacterFactory.CreatePlayer("A", CharacterDefinition.ArcaneTrickster),
                    new PlayerState("B", invalidator), new SimpleChoiceProvider(), new SimpleChoiceProvider());
                var owner = game.PlayerA; owner.LeftHand.SetValue(7); owner.RightHand.SetValue(7);
                game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(3);
                if (!invalidated) owner.ApplyStatus(StatusEffect.Dizzy);
                else Assert.True(game.WillSuppressOperationEffects(owner));
                var ai = ProductionAi.Create(game);
                var score = typeof(GreedyChoiceProvider).GetMethod("MageHandExchangeScore", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                double suppressed = (double)score.Invoke(ai, new object[] { 1, owner, game });
                owner.RemoveStatus(StatusEffect.Dizzy); invalidator.Active = false;
                double normal = (double)score.Invoke(ai, new object[] { 1, owner, game });
                Assert.True(normal > suppressed, "Only a real 1+3 attack may earn immediate attack value.");
                Assert.AreEqual(7, owner.LeftHand.Value); Assert.AreEqual(1, ArcaneTricksterLogic.For(owner).MageHandValue);
            }
        }
        [Test] public void Mage_hand_visual_frames_and_exchange_event_keep_immutable_digits()
        {
            var game = Match(); game.RecordCombatVisuals = true; var owner = game.PlayerA; var skill = ArcaneTricksterLogic.For(owner);
            owner.LeftHand.SetValue(7); var before = game.CaptureVisualFrame(); Assert.AreEqual(1, before.Players[0].MageHandValue.Value);
            Assert.True(skill.Exchange(owner, owner.LeftHand, game)); var after = game.CaptureVisualFrame();
            Assert.AreEqual(1, before.Players[0].MageHandValue.Value); Assert.AreEqual(7, after.Players[0].MageHandValue.Value);
            Assert.AreEqual(7, before.Players[0].Left.Value); Assert.AreEqual(1, after.Players[0].Left.Value);
            var events = game.DrainCombatVisuals(); Assert.AreEqual(1, events.Count); var cue = events[0];
            Assert.AreEqual("法师之手诈术", cue.RedirectKind); Assert.AreEqual(1, cue.Before.Players[0].MageHandValue.Value); Assert.AreEqual(7, cue.After.Players[0].MageHandValue.Value);
            Assert.AreEqual(owner.LeftHand, cue.SkillData.Source); Assert.AreEqual(1, cue.SkillData.Value); Assert.AreEqual(7, cue.SkillData.Previous);
            Assert.AreEqual(FinalSkillStyle.Exchange, FinalSkillAnimation.Style("法师之手诈术"));
            owner.LeftHand.SetValue(3); skill.Exchange(owner, owner.LeftHand, game);
            Assert.AreEqual(7, after.Players[0].MageHandValue.Value); Assert.AreEqual(7, cue.After.Players[0].MageHandValue.Value);
        }
        [Test] public void Mage_hand_visual_hides_while_dead_polymorphed_or_wild_shaped_and_returns_with_store()
        {
            var owner = DualGeneralFactory.Create("A", CharacterDefinition.ArcaneTrickster, CharacterDefinition.CircleOfTheMoon);
            var game = new GameManager(owner, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), new SimpleChoiceProvider(), new SimpleChoiceProvider()); game.RecordCombatVisuals = true;
            var skill = ArcaneTricksterLogic.For(owner); owner.LeftHand.SetValue(7); skill.Exchange(owner, owner.LeftHand, game);
            owner.TryPolymorph(); Assert.False(game.CaptureVisualFrame().Players[0].MageHandValue.HasValue);
            owner.EndPolymorph(); Assert.AreEqual(7, game.CaptureVisualFrame().Players[0].MageHandValue.Value);
            owner.LeftHand.SetValue(1); owner.RightHand.SetValue(9); Assert.True(owner.EnterWild(1, 0, game)); Assert.False(game.CaptureVisualFrame().Players[0].MageHandValue.HasValue);
            owner.EndWild(); Assert.AreEqual(7, game.CaptureVisualFrame().Players[0].MageHandValue.Value);
            owner.SetHealthExact(0); Assert.False(game.CaptureVisualFrame().Players[0].MageHandValue.HasValue);
        }
        [Test] public void Ai_exchange_scoring_is_readonly_and_can_select_a_useful_exchange()
        {
            var game = Match(); var owner = game.PlayerA; var ai = new GreedyChoiceProvider(); var skill = ArcaneTricksterLogic.For(owner);
            owner.LeftHand.SetValue(7); owner.RightHand.SetValue(7); game.PlayerB.LeftHand.SetValue(3); game.PlayerB.RightHand.SetValue(3);
            var options = new[] { new SkillChoiceOption(0,"none",""), new SkillChoiceOption(1,"left",""), new SkillChoiceOption(2,"right","") };
            int choice = ai.ChooseSkillOption(ArcaneTricksterLogic.ExchangeChoice, options, owner, null, game);
            Assert.True(choice == 1 || choice == 2); Assert.AreEqual(7, owner.LeftHand.Value); Assert.AreEqual(7, owner.RightHand.Value); Assert.AreEqual(1, skill.MageHandValue);
            game.SkillPolicyFor = _ => ai; game.BeginTurn();
            Assert.AreEqual(7, skill.MageHandValue); Assert.True(owner.LeftHand.Value == 1 || owner.RightHand.Value == 1);
        }
    }
}
