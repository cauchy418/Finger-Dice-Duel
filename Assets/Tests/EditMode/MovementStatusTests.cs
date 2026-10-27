using System;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class MovementStatusTests
    {
        private sealed class FixedRandom : Random
        {
            public int Value, Calls;
            public override int Next(int maxValue) { Calls++; return Value % maxValue; }
        }
        private static GameManager Game(CharacterDefinition a = null, CharacterDefinition b = null)
            => CharacterFactory.CreateMatch(a ?? CharacterDefinition.Blank, b ?? CharacterDefinition.Blank,
                new SimpleChoiceProvider(), new SimpleChoiceProvider());
        private static AttackContext Fire(GameManager g)
        {
            var hit = new AttackContext(g.PlayerA, g.PlayerB, g.PlayerA.LeftHand, 5)
                { AttackType = AttackDamageType.Fire, IgnoreStatBonuses = true };
            hit.Resolve(g.ChoiceProviderB, g); return hit;
        }

        [Test] public void Restraint_selects_one_hand_once_per_turn_and_can_change_next_turn()
        {
            var g = Game(); var rng = new FixedRandom(); g.StatusRandom = rng;
            var p = g.PlayerA; p.ApplyStatus(StatusEffect.Restrained); g.BeginTurn();
            Assert.True(p.LeftHand.IsRestrained); Assert.False(p.RightHand.IsRestrained);
            g.BeginTurn(); g.GetLegalMoves(p); g.HasAnyLegalOperation(p);
            Assert.AreEqual(1, rng.Calls, "刷新、枚举、重复BeginTurn不得重选");
            g.ResolveStatusTurnEnd(p); rng.Value = 1; g.BeginTurn();
            Assert.False(p.LeftHand.IsRestrained); Assert.True(p.RightHand.IsRestrained);
            Assert.AreEqual(2, rng.Calls);
        }
        [Test] public void Restraint_filters_human_buttons_AI_moves_and_direct_execution()
        {
            var g = Game(); g.StatusRandom = new FixedRandom(); g.PlayerA.ApplyStatus(StatusEffect.Restrained);
            var board = new BoardController(g, (SimpleChoiceProvider)g.ChoiceProviderA, (SimpleChoiceProvider)g.ChoiceProviderB);
            board.BeginTurn(); Assert.False(board.CanAct(g.PlayerA.LeftHand)); Assert.True(board.CanAct(g.PlayerA.RightHand));
            foreach (var move in g.GetLegalMoves(g.PlayerA)) Assert.AreSame(g.PlayerA.RightHand, move.ActedHand);
            var ai = new GreedyChoiceProvider(); var selected = ai.ChooseMove(g, g.PlayerA);
            Assert.AreSame(g.PlayerA.RightHand, selected.ActedHand);
            Assert.False(g.IsLegalMove(g.PlayerA, g.PlayerA.LeftHand, g.PlayerB.LeftHand, out _));
            Assert.Throws<InvalidOperationException>(() => g.ExecuteTurn(g.PlayerA, 0, 1));
        }
        [Test] public void Restraint_does_not_block_borrowing_the_hand_as_an_addend()
        {
            var g = Game(); g.StatusRandom = new FixedRandom(); g.PlayerB.ApplyStatus(StatusEffect.Restrained);
            g.GetLegalMoves(g.PlayerB);
            Assert.True(g.PlayerB.LeftHand.IsRestrained);
            Assert.True(g.IsLegalMove(g.PlayerA, g.PlayerA.LeftHand, g.PlayerB.LeftHand, out _));
            Assert.True(g.HasAnyLegalOperation(g.PlayerA));
        }
        [Test] public void Restraint_combined_with_other_lock_allows_skip_and_expires_cleanly()
        {
            var g = Game(); g.StatusRandom = new FixedRandom(); var p = g.PlayerA;
            p.ApplyStatus(StatusEffect.Restrained, 1); p.RightHand.Lock();
            var board = new BoardController(g, (SimpleChoiceProvider)g.ChoiceProviderA, (SimpleChoiceProvider)g.ChoiceProviderB);
            board.BeginTurn(); Assert.True(board.CanSkipTurn); Assert.AreEqual(0, g.GetLegalMoves(p).Count);
            Assert.True(board.SkipTurn()); Assert.False(p.HasStatus(StatusEffect.Restrained));
            Assert.False(p.LeftHand.IsRestrained); Assert.True(p.RightHand.IsLocked, "状态解除不能解开旧锁");
        }
        [Test] public void Cleanse_removal_HolyLight_and_reset_clear_restraint_without_unlocking_skill_locks()
        {
            var g = Game(); g.StatusRandom = new FixedRandom(); var p = g.PlayerA;
            p.RightHand.Lock(); p.ApplyStatus(StatusEffect.Restrained); g.BeginTurn(); p.ClearStatuses();
            Assert.False(p.LeftHand.IsRestrained); Assert.True(p.RightHand.IsLocked);
            p.ApplyStatus(StatusEffect.Restrained); g.BeginTurn(); p.RemoveStatus(StatusEffect.Restrained);
            Assert.False(p.LeftHand.IsRestrained);
            p.ApplyStatus(StatusEffect.Restrained); g.BeginTurn(); p.ApplyStatus(StatusEffect.HolyLight);
            Assert.False(p.HasStatus(StatusEffect.Restrained)); Assert.False(p.LeftHand.IsRestrained);
            p.ApplyStatus(StatusEffect.Greased); Assert.False(p.HasStatus(StatusEffect.Greased));
            p.Reset(); Assert.False(p.LeftHand.IsRestrained); Assert.False(p.RightHand.IsRestrained);
        }
        [Test] public void Restraint_cannot_be_bypassed_by_steal_or_gamble()
        {
            var g = Game(CharacterDefinition.Thief); g.StatusRandom = new FixedRandom();
            g.PlayerA.ApplyStatus(StatusEffect.Restrained); g.BeginTurn();
            Assert.Throws<InvalidOperationException>(() => g.ExecuteSteal(g.PlayerA, g.PlayerA.LeftHand, g.PlayerB.LeftHand));
            g = Game(CharacterDefinition.Gambler); g.StatusRandom = new FixedRandom();
            g.PlayerA.ApplyStatus(StatusEffect.Restrained); g.BeginTurn();
            Assert.Throws<InvalidOperationException>(() => g.ExecuteGambleWithRoll(g.PlayerA, g.PlayerA.LeftHand, 3));
        }
        [Test] public void Grease_proc_applies_two_full_subsequent_turns_and_preserves_source()
        {
            var g = Game(); var rng = new FixedRandom(); g.StatusRandom = rng; var p = g.PlayerA;
            p.ApplyStatus(StatusEffect.Greased, 2, g.PlayerB);
            g.ExecuteTurn(p, p.LeftHand, g.PlayerB.LeftHand);
            Assert.AreEqual(1, rng.Calls); Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.KnockedDown));
            Assert.AreSame(g.PlayerB, p.StatusSourceOf(StatusEffect.KnockedDown));
            g.ResolveStatusTurnEnd(p); Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.KnockedDown));
            g.ResolveStatusTurnEnd(p); Assert.AreEqual(1, p.StatusTurnsLeft(StatusEffect.KnockedDown));
            g.ResolveStatusTurnEnd(p); Assert.False(p.HasStatus(StatusEffect.KnockedDown));
        }
        [Test] public void Grease_other_half_does_not_knock_down_and_preview_does_not_roll()
        {
            var g = Game(); var rng = new FixedRandom { Value = 1 }; g.StatusRandom = rng;
            g.PlayerA.ApplyStatus(StatusEffect.Greased); g.GetLegalMoves(g.PlayerA); g.GetLegalMoves(g.PlayerA);
            Assert.AreEqual(0, rng.Calls);
            g.ExecuteTurn(g.PlayerA, g.PlayerA.LeftHand, g.PlayerB.LeftHand);
            Assert.AreEqual(1, rng.Calls); Assert.False(g.PlayerA.HasStatus(StatusEffect.KnockedDown));
        }
        [Test] public void Skip_and_gamble_do_not_roll_grease()
        {
            var g = Game(); var rng = new FixedRandom(); g.StatusRandom = rng;
            g.PlayerA.ApplyStatus(StatusEffect.Greased); g.PlayerA.ApplyStatus(StatusEffect.Frozen);
            g.SkipTurn(); Assert.AreEqual(0, rng.Calls);
            g = Game(CharacterDefinition.Gambler); g.StatusRandom = rng; g.PlayerA.ApplyStatus(StatusEffect.Greased);
            g.ExecuteGambleWithRoll(g.PlayerA, g.PlayerA.LeftHand, 3); Assert.AreEqual(0, rng.Calls);
        }
        [Test] public void Fire_grease_increases_damage_clears_oil_and_applies_burn()
        {
            var g = Game(); g.PlayerB.ApplyStatus(StatusEffect.Greased);
            Assert.AreEqual(8, Fire(g).AppliedDamage); Assert.False(g.PlayerB.HasStatus(StatusEffect.Greased));
            Assert.AreEqual(2, g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));
        }
        [Test] public void Fire_grease_shield_still_clears_oil_and_burns_but_dodge_preserves_it()
        {
            var g = Game(); g.PlayerB.ApplyStatus(StatusEffect.Greased); g.PlayerB.LeftHand.SetValue(5);
            g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed); Assert.True(Fire(g).WasBlocked);
            Assert.False(g.PlayerB.HasStatus(StatusEffect.Greased)); Assert.True(g.PlayerB.HasStatus(StatusEffect.Burn));
            g = Game(b: CharacterDefinition.Swashbuckler); g.PlayerB.ApplyStatus(StatusEffect.Greased);
            ((SimpleChoiceProvider)g.ChoiceProviderB).SetChoice(HandEffect.SwashbucklerDodge, g.PlayerB.LeftHand);
            Assert.True(Fire(g).WasDodged); Assert.True(g.PlayerB.HasStatus(StatusEffect.Greased));
            Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));
        }
        [Test] public void Fire_immunity_preserves_existing_oil_and_blocks_burn_and_reaction()
        {
            var g = Game(); g.PlayerB.ApplyStatus(StatusEffect.Greased); g.PlayerB.AddImmunity(AttackDamageType.Fire);
            Assert.AreEqual(0, Fire(g).AppliedDamage); Assert.True(g.PlayerB.HasStatus(StatusEffect.Greased));
            Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn)); Assert.False(g.PlayerB.HasImmunity(AttackDamageType.Fire));
        }
        [Test] public void Oil_reaction_has_priority_over_melting_and_preview_does_not_clear_anything()
        {
            var g = Game(); var p = g.PlayerB; p.ApplyStatus(StatusEffect.Greased); p.ApplyStatus(StatusEffect.Frost); p.ApplyStatus(StatusEffect.Wet);
            Assert.AreEqual(8, StatusRules.ApplyDamageTypeInteraction(p, DamageType.Fire, 5, out var status, out var clear));
            Assert.AreEqual(StatusEffect.Burn, status); Assert.AreEqual(StatusEffect.Greased, clear); Assert.True(p.HasStatus(StatusEffect.Greased));
            Assert.AreEqual(8, Fire(g).AppliedDamage); Assert.True(p.HasStatus(StatusEffect.Frost)); Assert.True(p.HasStatus(StatusEffect.Wet));
            Assert.False(p.HasStatus(StatusEffect.Greased)); Assert.True(p.HasStatus(StatusEffect.Burn));
        }
        [Test] public void Manual_and_combat_icons_include_both_new_statuses_and_ninth_reaction()
        {
            var p = Game().PlayerA; p.ApplyStatus(StatusEffect.Restrained); p.ApplyStatus(StatusEffect.Greased);
            Assert.AreEqual(23, StatusNames.AllStatuses.Length); Assert.AreEqual(2, CombatStatusIcons.For(p).Count);
            Assert.AreEqual("restrained", StatusNames.IconKeyOf(StatusEffect.Restrained));
            Assert.AreEqual("greased", StatusNames.IconKeyOf(StatusEffect.Greased));
            Assert.AreEqual(11, GuideSheets.ElementReactions().Lines.Count);
            Assert.True(GuideSheets.ElementReactions().Lines[10].Contains("油腻"));
        }
    }
}
