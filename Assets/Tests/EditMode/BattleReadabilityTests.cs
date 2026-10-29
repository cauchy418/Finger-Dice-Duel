using System;
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
    public class BattleReadabilityTests
    {
        private static BoardController Board(PlayerState actor, out PlayerState other, out GameManager game)
        {
            other = CharacterFactory.CreatePlayer("对手", CharacterDefinition.Blank);
            var a = new SimpleChoiceProvider(); var b = new SimpleChoiceProvider();
            game = new GameManager(actor, other, a, b);
            actor.LeftHand.SetValue(1); actor.RightHand.SetValue(1);
            other.LeftHand.SetValue(1); other.RightHand.SetValue(1);
            var board = new BoardController(game, a, b);
            board.BeginTurn(); return board;
        }

        [Test]
        public void OathOfDevotion_four_and_six_only_ask_for_shock_then_form_a_shield()
        {
            foreach (int value in new[] { 4, 6 })
            {
                var actor = CharacterFactory.CreatePlayer("圣武士·奉献之誓", CharacterDefinition.OathOfDevotion);
                var board = Board(actor, out var other, out _);
                actor.LeftHand.SetValue(value - 1);
                board.ClickHand(actor.LeftHand); board.ClickHand(other.LeftHand);
                Assert.AreEqual(HandEffect.OathOfDevotionShockTarget, board.PendingEffect);
                Assert.IsTrue(board.Hint.Contains("【冲击】"));
                Assert.IsFalse(board.Phase == BoardPhase.ChoosingTargetPlayer);
                board.ClickHand(other.LeftHand);
                Assert.AreEqual(ShieldState.Formed, actor.LeftHand.State);
                Assert.AreEqual(18, other.Health);
                Assert.AreEqual(HandEffectType.Shield, board.LastTurn.Effect);
                Assert.AreEqual(other, board.ActivePlayer);
            }
        }

        [Test]
        public void OathOfDevotion_breaking_and_forming_a_shield_keeps_both_shock_choices()
        {
            var actor = CharacterFactory.CreatePlayer("圣武士·奉献之誓", CharacterDefinition.OathOfDevotion);
            var board = Board(actor, out var other, out _);
            actor.LeftHand.SetValue(4); actor.LeftHand.SetShieldState(ShieldState.Formed);
            board.ClickHand(actor.LeftHand); board.ClickHand(other.LeftHand);
            Assert.AreEqual(1, board.PendingRequest.Value.Variant);
            board.ClickHand(other.LeftHand);
            Assert.AreEqual(0, board.PendingRequest.Value.Variant);
            board.ClickHand(other.LeftHand);
            Assert.AreEqual(16, other.Health);
            Assert.AreEqual(ShieldState.Formed, actor.LeftHand.State);
        }

        [Test]
        public void OathOfDevotion_eight_still_requires_an_attack_target()
        {
            var actor = CharacterFactory.CreatePlayer("圣武士·奉献之誓", CharacterDefinition.OathOfDevotion);
            var board = Board(actor, out var other, out _);
            actor.LeftHand.SetValue(7);
            board.ClickHand(actor.LeftHand); board.ClickHand(other.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
        }

        [Test]
        public void Dual_paladin_does_not_restore_the_redundant_attack_question()
        {
            var actor = DualGeneralFactory.Create("双将", CharacterDefinition.OathOfDevotion, CharacterDefinition.OathOfTheCrown);
            var board = Board(actor, out var other, out _);
            actor.LeftHand.SetValue(3);
            board.ClickHand(actor.LeftHand); board.ClickHand(other.LeftHand);
            Assert.AreEqual(HandEffect.OathOfDevotionShockTarget, board.PendingEffect);
            board.ClickHand(other.LeftHand);
            Assert.AreEqual(HandEffectType.Shield, board.LastTurn.Effect);
        }

        [Test]
        public void Healing_another_player_reports_actual_capped_health_gain()
        {
            var actor = CharacterFactory.CreatePlayer("牧师·生命领域", CharacterDefinition.LifeDomain);
            var board = Board(actor, out var other, out _);
            actor.LeftHand.SetValue(8); other.SetHealthExact(19);
            board.ClickHand(actor.LeftHand); board.ClickHand(other.LeftHand);
            board.ClickHand(other.LeftHand);
            Assert.AreEqual(1, board.LastTurn.ActualHealed);
            Assert.IsTrue(BoardController.DescribeTurn(board.LastTurn).Contains("对手 1 点生命"));
        }

        [Test]
        public void Declining_power_shot_does_not_log_it_as_activated()
        {
            var actor = CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler);
            var board = Board(actor, out var other, out _);
            actor.LeftHand.SetValue(3); actor.RightHand.SetValue(2);
            board.ClickHand(actor.LeftHand); board.ClickHand(other.LeftHand);
            for (int i = 0; i < 6 && board.LastTurn == null; i++)
            {
                if (board.Phase == BoardPhase.ChoosingTargetPlayer) board.ClickHand(other.LeftHand);
                else if (board.CanDeclinePendingChoice) board.DeclinePendingChoice();
                else break;
            }
            Assert.NotNull(board.LastTurn);
            Assert.IsFalse(string.Join("\n", board.Log).Contains("【潇洒无畏】"));
            Assert.AreEqual(2, actor.RightHand.Value);
        }

        [Test]
        public void Pending_steal_attack_never_claims_an_electrified_status()
        {
            var actor = CharacterFactory.CreatePlayer("游荡者·盗贼", CharacterDefinition.Thief);
            var board = Board(actor, out var other, out var game);
            actor.LeftHand.SetValue(4); other.LeftHand.SetValue(4);
            game.ExecuteSteal(actor, actor.LeftHand, other.LeftHand);
            Assert.IsTrue(game.StealAttackAwaitingTarget);
            game.ResolvePendingStealAttack(actor, other);
            string log = string.Join("\n", game.TakeSkillLog());
            Assert.IsTrue(log.Contains("【快手】"));
            Assert.IsFalse(log.Contains("感电"));
            Assert.IsTrue(other.Health < other.MaximumHealth);
        }

        [Test]
        public void Every_choice_has_a_concise_named_prompt_with_the_correct_owner()
        {
            var player = CharacterFactory.CreatePlayer("防守者", CharacterDefinition.Blank);
            foreach (HandEffect effect in Enum.GetValues(typeof(HandEffect)))
            {
                if (effect == HandEffect.None) continue;
                var request = new ChoiceRequest(effect, "old", new List<Hand> { player.LeftHand }, decider: player, resultValue: 6);
                string prompt = CombatText.ChoicePrompt(request, null);
                Assert.IsTrue(prompt.StartsWith("防守者【"), effect.ToString());
                Assert.IsTrue(prompt.Length < 90, effect.ToString());
            }
        }

        [Test]
        public void Rules_describe_current_opening_modes_and_health_tiebreak()
        {
            string rules = RuleBook.Full(null);
            foreach (string term in new[] { "1d20", "19", "20", "队友", "总血量", "80", "易伤", "免疫", "战斗记录", "角色图鉴" })
                Assert.IsTrue(rules.Contains(term), term);
            Assert.IsFalse(rules.Contains("冲击"), "共同规则不再混入子职技能。");
            Assert.IsFalse(rules.Contains("主菜单右上角"));
            Assert.IsFalse(rules.Contains("仍未分出胜负则判和局"));
        }
    }
}
