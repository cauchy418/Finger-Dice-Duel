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
            var a = Make("A", CharacterDefinition.Soldier);
            var b = Make("B", CharacterDefinition.Soldier);
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
    }
}
