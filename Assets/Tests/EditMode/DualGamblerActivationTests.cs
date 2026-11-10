using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class DualGamblerActivationTests
    {
        private sealed class Dice : System.Random
        {
            private readonly Queue<int> _values;
            public Dice(params int[] values) { _values = new Queue<int>(values); }
            public override int Next(int minValue, int maxValue) => _values.Dequeue();
        }
        private static GameManager Match(bool reverse, out SimpleChoiceProvider a, out SimpleChoiceProvider b)
        {
            a = new SimpleChoiceProvider(); b = new SimpleChoiceProvider();
            var gambler = DualGeneralFactory.Create("双赌", reverse ? CharacterDefinition.Swashbuckler : CharacterDefinition.Gambler,
                reverse ? CharacterDefinition.Gambler : CharacterDefinition.Swashbuckler);
            return new GameManager(gambler, CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank), a, b);
        }
        [Test] public void Dual_gambler_is_eligible_for_core_gambling_in_both_component_orders()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var g = Match(reverse, out _, out _);
                var result = g.ExecuteGambleWithRoll(g.PlayerA, g.PlayerA.LeftHand, 2);
                Assert.True(result.WasGamble); Assert.AreEqual(2, result.GambleRoll);
            }
        }
        [Test] public void Dual_gambler_core_ai_pays_health_and_uses_the_replacement_roll()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var g = Match(reverse, out _, out _); var ai = ProductionAi.Create(g);
                g.SkillPolicyFor = p => ai; g.PlayerA.SetHealthExact(10);
                var result = g.ExecuteGamble(g.PlayerA, g.PlayerA.LeftHand, new Dice(2, 3, 9));
                Assert.AreEqual(9, result.GambleRoll); Assert.AreEqual(16, g.PlayerA.Health);
            }
        }
        [Test] public void Dual_gambler_human_can_open_the_reroll_prompt_and_pay_for_a_new_roll()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var g = Match(reverse, out var a, out var b); g.PlayerA.SetHealthExact(10);
                var board = new BoardController(g, a, b) { GambleRng = new Dice(2, 3, 9) };
                board.BeginTurn(); Assert.True(board.ClickHand(g.PlayerA.LeftHand));
                Assert.True(board.CanGamble); Assert.True(board.Gamble());
                Assert.NotNull(board.PendingWizardChoice); Assert.AreEqual(2, board.LastGambleRoll);
                Assert.True(board.ChooseSkillOption(2));
                Assert.AreEqual(9, board.LastTurn.GambleRoll); Assert.AreEqual(16, g.PlayerA.Health);
            }
        }
        [Test] public void Dual_gambler_normal_operation_requires_next_gamble_and_gambling_clears_it()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var g = Match(reverse, out var a, out var b); var board = new BoardController(g, a, b);
                g.PlayerA.LeftHand.SetValue(1); g.PlayerB.LeftHand.SetValue(2);
                board.BeginTurn(); board.ClickHand(g.PlayerA.LeftHand); board.ClickHand(g.PlayerB.LeftHand);
                Assert.NotNull(board.LastTurn); Assert.False(board.LastTurn.WasGamble);
                Assert.True(g.PlayerA.MustGamble); Assert.True(g.MustUseGambling(g.PlayerA));
                g.ExecuteTurn(g.PlayerB, g.PlayerB.LeftHand, g.PlayerA.LeftHand);
                board.BeginTurn(); Assert.False(board.ClickHand(g.PlayerB.LeftHand));
                Assert.True(board.ClickHand(g.PlayerA.LeftHand)); Assert.AreEqual(BoardPhase.ChoosingGamble, board.Phase);
                board.GambleRng = new Dice(2); Assert.True(board.Gamble()); Assert.True(board.ChooseSkillOption(0));
                Assert.False(g.PlayerA.MustGamble); Assert.False(g.MustUseGambling(g.PlayerA));
            }
        }
        [Test] public void Dual_gambler_ai_gambles_when_forced_or_without_attack_and_keeps_good_normal_operations()
        {
            foreach (bool reverse in new[] { false, true }) foreach (int mode in new[] { 0, 1, 2 })
            {
                var g = Match(reverse, out var a, out var b); var ai = ProductionAi.Create(g);
                var board = new BoardController(g, a, b, aiForA: ai) { GambleRng = new Dice(5) };
                g.PlayerA.LeftHand.SetValue(1); g.PlayerA.RightHand.SetValue(1);
                g.PlayerB.LeftHand.SetValue(mode == 0 ? 1 : 3); g.PlayerB.RightHand.SetValue(mode == 0 ? 1 : 3);
                g.PlayerA.MustGamble = mode == 1; board.BeginTurn();
                Assert.True(board.MakeAutomaticMove()); Assert.AreEqual(mode == 2 ? 0 : 5, board.LastGambleRoll);
                if (mode != 2) Assert.False(g.PlayerA.MustGamble);
            }
        }
    }
}
