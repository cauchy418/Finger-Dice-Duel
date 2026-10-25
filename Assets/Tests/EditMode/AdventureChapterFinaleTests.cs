using System;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class AdventureChapterFinaleTests
    {
        private static AdventureLesson Setup(int number, bool begin = true, CharacterDefinition selected = null)
        {
            var ca = new SimpleChoiceProvider(); var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(selected ?? AdventureChapter.PlayerDefinition(number),
                number == 10 ? AdventureChapter.Knight : CharacterDefinition.Blank, ca, cb);
            var board = new BoardController(game, ca, cb); AdventureLesson lesson;
            switch (number)
            {
                case 6: AdventureLevel6.Prepare(game); lesson = AdventureLevel6.Attach(game, board); break;
                case 7: AdventureLevel7.Prepare(game); lesson = AdventureLevel7.Attach(game, board); break;
                case 8: AdventureLevel8.Prepare(game); lesson = AdventureLevel8.Attach(game, board); break;
                case 9: AdventureLevel9.Prepare(game); lesson = AdventureLevel9.Attach(game, board); break;
                default: lesson = AdventureLevel10.Attach(game, board); break;
            }
            lesson.Bind(board);
            if (begin) { board.BeginTurn(); lesson.BeginTutorial(); }
            return lesson;
        }
        private static void Human(AdventureLesson l)
        {
            var b = l.Board; var beat = l.CurrentBeat;
            Assert.IsTrue(l.AllowClick(beat.ActedHand)); Assert.IsFalse(l.AllowClick(l.Player.GetOtherHand(beat.ActedHand)));
            Assert.IsTrue(b.ClickHand(beat.ActedHand)); Assert.IsTrue(l.AllowClick(beat.AddendHand)); Assert.IsTrue(b.ClickHand(beat.AddendHand));
            for (int i = 0; i < 15 && b.IsHumanChooser && ReferenceEquals(b.ActivePlayer, l.Player); i++)
            {
                if (b.PendingEffect == HandEffect.BattleMasterManeuver)
                {
                    Assert.IsTrue(beat.Maneuver.HasValue);
                    Assert.IsTrue(beat.Maneuver.Value == StatusEffect.None ? b.DeclineBattleMasterManeuver() : b.ChooseBattleMasterManeuver(beat.Maneuver.Value));
                }
                else if (b.Phase == BoardPhase.ChoosingTargetPlayer) Assert.IsTrue(b.ConfirmTargetPlayer());
                else if (beat.Choices.TryGetValue(b.PendingEffect, out var hand)) Assert.IsTrue(hand == null ? b.DeclinePendingChoice() : b.ClickHand(hand));
                else break;
            }
            l.Tick();
        }
        private static void Npc(AdventureLesson l)
        {
            for (int i = 0; i < 15; i++)
            {
                l.Tick(); if (l.IsFinished || ReferenceEquals(l.Board.ActivePlayer, l.Player)) return;
                Assert.IsFalse(l.Board.IsHumanChooser, "unexpected player defence");
                Assert.IsTrue(l.Board.Phase == BoardPhase.ChoosingAttackerHand ? l.Board.MakeAutomaticMove() : l.Board.ResolvePendingAiChoice(), l.Board.Hint);
            }
            Assert.IsTrue(false, "NPC stalled");
        }
        [Test]
        public void Sixth_real_controller_pierces_then_adds_two_then_breaks_and_kills()
        {
            var l = Setup(6); Human(l);
            Assert.AreEqual(8, l.Board.LastTurn.DamageDealt); Assert.AreEqual(12, l.Soldier.Health); Assert.IsTrue(l.Soldier.RightHand.IsShield);
            Npc(l); Assert.AreEqual(3, l.Soldier.RightHand.Value); Assert.IsFalse(l.Soldier.RightHand.IsShield);
            Human(l); Assert.AreEqual(8, l.Board.LastTurn.DamageDealt); Assert.AreEqual(4, l.Soldier.Health);
            Npc(l); Assert.IsTrue(l.Soldier.RightHand.IsShield);
            Human(l); Assert.IsTrue(l.IsComplete); Assert.AreEqual(0, l.Soldier.Health); Assert.AreEqual(ShieldState.Broken, l.Soldier.RightHand.State);
            string log = string.Join(" | ", l.Board.Log);
            Assert.IsTrue(log.Contains("魔射穿盾") && log.Contains("魔射增伤") && log.Contains("魔射连击"), log);
        }
        [Test]
        public void Seventh_bleed_limp_and_stunned_apply_real_penalties()
        {
            var l = Setup(7); Human(l);
            Assert.AreEqual(35, l.Soldier.Health, "4 damage plus real turn-start bleed");
            Assert.AreEqual(-1, StatusRules.EffectiveAttackBonus(l.Soldier)); Assert.AreEqual(-1, StatusRules.EffectiveDefenseBonus(l.Soldier));
            Npc(l); Human(l);
            Assert.IsTrue(l.Soldier.HasStatus(StatusEffect.Limp)); Assert.AreEqual(1, StatusRules.EffectiveAttackRange(l.Soldier, l.Game));
            Assert.IsFalse(StatusRules.CanAttackTarget(l.Soldier, l.Player, l.Game)); Npc(l); Human(l);
            Assert.IsTrue(l.Soldier.HasStatus(StatusEffect.Stunned)); Npc(l);
            Assert.AreEqual(8, l.Soldier.LeftHand.Value); Assert.AreEqual(20, l.Player.Health);
            Assert.IsTrue(string.Join(" | ", l.Board.Log).Contains("放弃"));
        }
        [Test]
        public void Seventh_knocked_down_increases_next_hit_not_application_hit_and_completes_alive()
        {
            var l = Setup(7); Human(l); Npc(l); Human(l); Npc(l); Human(l); Npc(l);
            Human(l); Assert.AreEqual(4, l.Board.LastTurn.DamageDealt); Assert.IsTrue(l.Soldier.HasStatus(StatusEffect.KnockedDown));
            Npc(l); Assert.AreEqual(9, l.StepNumber); Human(l);
            Assert.AreEqual(6, l.Board.LastTurn.DamageDealt); Assert.IsTrue(l.IsComplete);
            Assert.IsTrue(l.Soldier.IsAlive); Assert.IsFalse(l.Game.IsGameOver);
        }
        [Test]
        public void Seventh_maneuver_gate_requires_all_four_choices_then_decline()
        {
            var l = Setup(7);
            foreach (var expected in new[] { StatusEffect.Bleed, StatusEffect.Limp, StatusEffect.Stunned, StatusEffect.KnockedDown, StatusEffect.None })
            {
                Assert.IsTrue(l.AllowManeuver(expected));
                foreach (var s in new[] { StatusEffect.Bleed, StatusEffect.Limp, StatusEffect.Stunned, StatusEffect.KnockedDown, StatusEffect.None })
                    Assert.AreEqual(s == expected, l.AllowManeuver(s));
                Human(l); if (!l.IsComplete) Npc(l);
            }
        }
        [Test]
        public void Eighth_follow_up_hits_first_bleed_increases_main_and_total_is_eleven()
        {
            var l = Setup(8); l.Board.ClickHand(l.Player.LeftHand); l.Board.ClickHand(l.Soldier.LeftHand);
            if (l.Board.Phase == BoardPhase.ChoosingTargetPlayer) Assert.IsTrue(l.Board.ConfirmTargetPlayer());
            Assert.AreEqual(HandEffect.SwordsmanFollowUp, l.Board.PendingEffect); Assert.IsFalse(l.AllowDeclineChoice); Assert.IsTrue(l.AllowAcceptChoice);
            Assert.IsTrue(l.Board.ClickHand(l.Player.RightHand));
            if (l.Board.Phase == BoardPhase.ChoosingTargetPlayer) Assert.IsTrue(l.Board.ConfirmTargetPlayer());
            Assert.IsTrue(l.IsComplete); Assert.AreEqual(7, l.Board.LastTurn.DamageDealt);
            string log = string.Join(" | ", l.Board.Log); Assert.IsTrue(log.Contains("追加") && log.Contains("流血"), log);
        }
        [Test]
        public void Ninth_health_gaps_zero_five_thirteen_produce_five_eight_thirteen_damage()
        {
            var l = Setup(9); Assert.AreEqual(1, StatusRules.EffectiveAttackBonus(l.Player)); Assert.AreEqual(-1, StatusRules.EffectiveDefenseBonus(l.Player));
            Human(l); Assert.AreEqual(5, l.Board.LastTurn.DamageDealt); Assert.AreEqual(15, l.Soldier.Health);
            Assert.AreEqual(2, StatusRules.EffectiveAttackBonus(l.Player)); Assert.AreEqual(0, StatusRules.EffectiveDefenseBonus(l.Player)); Npc(l);
            Human(l); Assert.AreEqual(8, l.Board.LastTurn.DamageDealt); Assert.AreEqual(7, l.Soldier.Health);
            Assert.AreEqual(5, StatusRules.EffectiveAttackBonus(l.Player)); Assert.AreEqual(3, StatusRules.EffectiveDefenseBonus(l.Player)); Npc(l);
            Human(l); Assert.AreEqual(13, l.Board.LastTurn.DamageDealt); Assert.IsTrue(l.IsComplete);
        }
        [Test]
        public void Finale_fixed_knight_is_plus_one_plus_one_without_crown_skills_and_not_in_roster()
        {
            var l = Setup(10, false, CharacterDefinition.SchoolOfEvocation);
            Assert.AreEqual(20, l.Soldier.Health); Assert.AreEqual(1, StatusRules.EffectiveAttackBonus(l.Soldier)); Assert.AreEqual(1, StatusRules.EffectiveDefenseBonus(l.Soldier));
            Assert.AreEqual(1, GameManager.BaseRangeOf(l.Soldier)); Assert.AreEqual(SpecialLogicType.None, l.Soldier.Definition.Logic);
            foreach (var role in CharacterDefinition.BuiltIn) Assert.IsFalse(role.Id == AdventureChapter.KnightId);
            Assert.IsTrue(l.UsesClassicSetup); Assert.IsFalse(l.Started); Assert.AreEqual(0, l.Game.TurnCount); Assert.IsTrue(l.Board.ScriptedRestAllowed == null);
        }
        [Test]
        public void Finale_all_seven_fighter_subclasses_keep_their_skill_and_other_professions_are_excluded()
        {
            Assert.AreEqual(7, AdventureChapter.FinaleRoster.Count);
            foreach (var role in AdventureChapter.FinaleRoster)
            {
                Assert.AreEqual("fighter", role.Profession.Id);
                var l = Setup(10, false, role); Assert.AreEqual(role.Id, l.Player.Definition.Id); Assert.IsFalse(l.IsComplete);
                Assert.IsTrue(l.OpeningText.Contains("本场子职：" + role.DisplayName));
                Assert.IsFalse(l.OpeningText.Contains("刚才选择"));
                Assert.AreEqual(role.Logic, l.Player.Definition.Logic); Assert.IsFalse(l.AllowsTutorialSkip);
                Assert.AreEqual("FingerDiceDuel.Adventure.Level10.Completed", l.CompletionKey);
            }
        }
        [Test]
        public void Finale_real_free_ai_can_defeat_player_and_does_not_complete_on_loss()
        {
            var l = Setup(10); l.Player.SetHealthExact(1);
            l.Player.LeftHand.SetValue(2); l.Player.RightHand.SetValue(2); l.Soldier.LeftHand.SetValue(2); l.Soldier.RightHand.SetValue(2);
            Assert.IsTrue(l.Board.ClickHand(l.Player.LeftHand)); Assert.IsTrue(l.Board.ClickHand(l.Soldier.LeftHand));
            if (l.Board.Phase == BoardPhase.ChoosingTargetPlayer) l.Board.ConfirmTargetPlayer();
            Npc(l); Assert.IsTrue(l.Game.IsGameOver); Assert.IsFalse(l.IsComplete);
        }
        [Test]
        public void New_lessons_are_replayable_have_distinct_keys_and_cannot_skip_demonstrations()
        {
            for (int n = 6; n <= 10; n++)
            {
                var l = Setup(n, false); Assert.IsFalse(l.Started); Assert.IsFalse(l.IsComplete); Assert.IsFalse(l.AllowsTutorialSkip);
                if (n < 10) Assert.AreEqual(AdventureChapter.InstructorName, l.Soldier.Name);
                Assert.AreEqual("FingerDiceDuel.Adventure.Level" + n + ".Completed", l.CompletionKey);
                Assert.IsTrue(l.DisplayTitle.EndsWith(AdventureChapter.BattleName(n)));
                Assert.IsTrue(l.SummaryText.Length > 20 && l.OpeningText.Length > 20);
            }
            Assert.AreEqual(10, AdventureChapter.BattleCount);
        }
    }
}
