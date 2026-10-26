using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class NewWizardSchoolsTests
    {
        private static GameManager Game(CharacterDefinition a, CharacterDefinition b,
                                        out PlayerState pA, out PlayerState pB,
                                        out SimpleChoiceProvider ca, out SimpleChoiceProvider cb)
        {
            pA = CharacterFactory.CreatePlayer("A", a);
            pB = CharacterFactory.CreatePlayer("B", b);
            ca = new SimpleChoiceProvider();
            cb = new SimpleChoiceProvider();
            return new GameManager(pA, pB, ca, cb);
        }

        [Test]
        public void Wizard_schools_have_the_confirmed_base_stats()
        {
            CharacterDefinition[] defs =
            {
                CharacterDefinition.SchoolOfDivination,
                CharacterDefinition.SchoolOfNecromancy,
                CharacterDefinition.SchoolOfAbjuration,
            };
            foreach (CharacterDefinition def in defs)
            {
                Assert.AreEqual(0, def.AttackBonus, def.Id);
                Assert.AreEqual(-1, def.DefenseBonus, def.Id);
                Assert.AreEqual(CharacterProfession.Find("wizard"), def.Profession, def.Id);
                PlayerState p = CharacterFactory.CreatePlayer("P", def);
                Assert.AreEqual(CharacterTemplate.InfiniteAttackRange,
                    ((IAttackRangeProvider)p.Character).BaseAttackRange, def.Id);
            }
        }

        [Test]
        public void Divination_seven_window_invalidates_a_matching_action()
        {
            GameManager game = Game(CharacterDefinition.SchoolOfDivination,
                                    CharacterDefinition.Champion,
                                    out PlayerState diviner,
                                    out PlayerState other,
                                    out _, out _);
            other.LeftHand.SetValue(4);
            other.RightHand.SetValue(1);
            diviner.LeftHand.SetValue(3);
            diviner.RightHand.SetValue(4);

            game.ExecuteTurn(diviner, diviner.LeftHand, other.LeftHand);
            Assert.AreEqual(7, diviner.LeftHand.Value);
            int before = other.Health;
            // 对手另一只手是 4，正好等于预言者打出 7 时的另一手快照。
            other.LeftHand.SetValue(4);
            TurnResult invalid = game.ExecuteTurn(other, other.LeftHand, diviner.RightHand);
            Assert.IsTrue(invalid.ActionInvalidated);
            Assert.AreEqual(before, other.Health);
        }

        private static GameManager Window(CharacterDefinition otherDefinition,
            out PlayerState diviner, out PlayerState other)
        {
            var game = Game(CharacterDefinition.SchoolOfDivination, otherDefinition,
                out diviner, out other, out _, out _);
            diviner.LeftHand.SetValue(3); diviner.RightHand.SetValue(4);
            other.LeftHand.SetValue(1); other.RightHand.SetValue(4);
            game.ExecuteTurn(diviner, diviner.LeftHand, other.RightHand);
            return game;
        }

        [Test] public void Divination_changes_every_result_but_never_attacks_heals_or_forms_shield()
        {
            foreach (int raw in new[] { 4, 5, 6, 8, 9 })
            {
                var game = Window(CharacterDefinition.Breaker, out var diviner, out var other);
                other.SetHealthExact(10); other.LeftHand.SetValue(raw - 4);
                var result = game.ExecuteTurn(other, other.LeftHand, diviner.RightHand);
                Assert.True(result.ActionInvalidated);
                Assert.AreEqual(raw, other.LeftHand.Value);
                Assert.AreEqual(raw - 4, result.ActedHandValueBefore);
                Assert.AreEqual(4, result.TargetHandValue);
                Assert.AreEqual(raw, result.ResultValue);
                Assert.AreEqual(raw, result.ActedHandValueAfter);
                Assert.AreEqual(HandEffectType.None, result.Effect);
                Assert.AreEqual(ShieldState.None, other.LeftHand.State);
                Assert.AreEqual(20, diviner.Health);
                Assert.AreEqual(10, other.Health);
                Assert.AreEqual(2, game.TurnCount);
                Assert.True(BattleTurnText.Describe(result).Contains("仅改变数字"));
            }
        }
        [Test] public void Divination_does_not_activate_shield_break_enchantment_or_chain_break()
        {
            foreach (var role in new[] { CharacterDefinition.EldritchKnight, CharacterDefinition.Breaker })
            {
                var game = Window(role, out var diviner, out var other);
                other.SetHealthExact(10); other.LeftHand.SetValue(5); other.LeftHand.SetShieldState(ShieldState.Formed);
                diviner.LeftHand.SetValue(1); diviner.LeftHand.SetShieldState(ShieldState.Formed);
                int choices = 0; game.DeferWizardChoice = q => { choices++; return true; };
                var board = new BoardController(game, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                board.BeginTurn();
                Assert.True(board.ClickHand(other.LeftHand));
                Assert.True(board.ClickHand(diviner.LeftHand));
                Assert.True(board.LastTurn.ActionInvalidated);
                Assert.AreEqual(6, other.LeftHand.Value);
                Assert.False(other.LeftHand.IsShield);
                Assert.True(diviner.LeftHand.IsShield);
                Assert.AreEqual(20, diviner.Health);
                Assert.AreEqual(10, other.Health);
                Assert.AreEqual(0, choices);
                Assert.True(board.PendingWizardChoice == null);
                Assert.False(board.PendingRequest.HasValue);
            }
        }
        [Test] public void Divination_skips_evocation_and_abjuration_skill_prompts()
        {
            foreach (var role in new[] { CharacterDefinition.SchoolOfEvocation, CharacterDefinition.SchoolOfAbjuration })
            {
                var game = Window(role, out var diviner, out var other);
                var board = new BoardController(game, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                board.BeginTurn();
                Assert.True(board.ClickHand(other.LeftHand));
                Assert.True(board.ClickHand(diviner.LeftHand));
                Assert.AreEqual(8, other.LeftHand.Value);
                Assert.AreEqual(20, diviner.Health);
                Assert.True(board.LastTurn.ActionInvalidated);
                Assert.False(board.PendingRequest.HasValue);
                Assert.True(board.PendingWizardChoice == null);
                Assert.AreEqual(2, game.TurnCount);
            }
        }
        [Test] public void Divination_keeps_status_numeric_modifiers_and_status_clock()
        {
            var game = Window(CharacterDefinition.Blank, out var diviner, out var other);
            other.ApplyStatus(StatusEffect.Frost, 2);
            other.LeftHand.SetValue(4);
            var result = game.ExecuteTurn(other, other.LeftHand, diviner.RightHand);
            Assert.AreEqual(4, other.LeftHand.Value); // 4+4=8，再覆霜减半。
            Assert.AreEqual(8, result.FreezeHalvedFrom);
            Assert.AreEqual(20, diviner.Health);
            game.ResolveStatusTurnEnd(other);
            Assert.AreEqual(1, other.StatusTurnsLeft(StatusEffect.Frost));
        }
        [Test] public void Divination_suppresses_operation_and_new_turn_end_skills_in_both_dual_positions()
        {
            foreach (bool reverse in new[] { false, true })
            {
                var diviner = CharacterFactory.CreatePlayer("D", CharacterDefinition.SchoolOfDivination);
                var lore = new CollegeOfLoreLogic(); var transmute = new SchoolOfTransmutationLogic();
                var other = new PlayerState("M", new DualGeneralBehavior(reverse ? (ICharacterBehavior)lore : transmute, reverse ? (ICharacterBehavior)transmute : lore));
                var game = new GameManager(diviner, other, new SimpleChoiceProvider(), new SimpleChoiceProvider());
                diviner.LeftHand.SetValue(3); diviner.RightHand.SetValue(4); other.LeftHand.SetValue(4); other.RightHand.SetValue(4);
                game.ExecuteTurn(diviner, diviner.LeftHand, other.RightHand);
                diviner.LeftHand.SetValue(8); // 匹配变形术的终局手值8/4。
                int choices = 0; game.DeferWizardChoice = q => { choices++; return true; };
                game.ExecuteTurn(other, other.LeftHand, diviner.RightHand);
                game.ResolveStatusTurnEnd(other);
                Assert.AreEqual(8, other.LeftHand.Value);
                Assert.AreEqual(0, lore.RecordCount);
                Assert.AreEqual(0, choices);
                Assert.False(other.IsPolymorphed);
                Assert.False(diviner.IsPolymorphed);
            }
        }
        [Test] public void Divination_checks_held_values_before_operation_not_the_borrowed_addend()
        {
            var game = Window(CharacterDefinition.Blank, out var diviner, out var other);
            other.LeftHand.SetValue(1); other.RightHand.SetValue(2);
            var result = game.ExecuteTurn(other, other.LeftHand, diviner.RightHand);
            Assert.False(result.ActionInvalidated);
            Assert.True(other.LeftHand.IsShield); // 借入4不等于自己持有4。
        }
        [Test] public void Divination_window_ends_on_next_owner_turn_and_extra_operations_also_match()
        {
            var game = Window(CharacterDefinition.Blank, out var diviner, out var other);
            var result = game.ExecuteTurn(other, other.LeftHand, diviner.LeftHand, extraAction: true);
            Assert.True(result.ActionInvalidated);
            Assert.AreEqual(8, other.LeftHand.Value);
            Assert.AreEqual(1, game.TurnCount);
            game.ForceActivePlayerForTest(diviner); game.BeginTurn();
            Assert.False(game.WillSuppressOperationEffects(other));
            result = game.ExecuteTurn(other, other.LeftHand, diviner.RightHand);
            Assert.False(result.ActionInvalidated);
        }
        [Test] public void Divination_pure_preview_has_no_side_effects_and_ai_ignores_immediate_effect_value()
        {
            var game = Window(CharacterDefinition.Blank, out var diviner, out var other);
            var ai = new GreedyChoiceProvider();
            var move = new LegalMove(other, other.LeftHand, diviner.LeftHand);
            int before = ai.ScoreMoveForTest(move, other, game);
            other.SetHealthExact(1); diviner.SetHealthExact(1);
            for (int i = 0; i < 3; i++) Assert.True(game.WillSuppressOperationEffects(other));
            Assert.AreEqual(before, ai.ScoreMoveForTest(move, other, game), "无虚假的斩杀/回复收益");
            Assert.AreEqual(1, other.LeftHand.Value);
            game.ResetMatch(); Assert.False(game.WillSuppressOperationEffects(other));
        }

        [Test] public void Divination_expired_shield_lock_does_not_become_permanent()
        {
            var game = Game(CharacterDefinition.SchoolOfDivination, CharacterDefinition.PathOfTheWarden,
                out var diviner, out var warden, out _, out var choice);
            diviner.LeftHand.SetValue(3); diviner.RightHand.SetValue(4);
            warden.LeftHand.SetValue(1); warden.RightHand.SetValue(4);
            choice.SetChoice(HandEffect.PathOfTheWardenLockTarget, diviner.RightHand);
            game.ExecuteTurn(warden, warden.LeftHand, diviner.RightHand);
            Assert.True(diviner.RightHand.IsLocked);
            game.ExecuteTurn(diviner, diviner.LeftHand, warden.RightHand);
            var result = game.ExecuteTurn(warden, warden.LeftHand, diviner.LeftHand);
            Assert.True(result.ActionInvalidated);
            Assert.AreEqual(2, warden.LeftHand.Value);
            Assert.False(warden.LeftHand.IsShield);
            Assert.False(diviner.RightHand.IsLocked);
        }
        [Test] public void Divination_gamble_changes_roll_without_target_or_skill_questions()
        {
            var game = Window(CharacterDefinition.Gambler, out var diviner, out var other);
            var board = new BoardController(game, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            for (int seed = 0; seed < 100; seed++)
                if (GamblerLogic.Roll(new System.Random(seed)) == 6) { board.GambleRng = new System.Random(seed); break; }
            board.BeginTurn(); Assert.True(board.ClickHand(other.LeftHand));
            Assert.True(board.Gamble());
            Assert.True(board.LastTurn.ActionInvalidated);
            Assert.AreEqual(6, other.LeftHand.Value);
            Assert.AreEqual(20, diviner.Health);
            Assert.AreEqual(2, game.TurnCount);
            Assert.True(board.PendingWizardChoice == null);
            Assert.False(board.PendingRequest.HasValue);
        }

        [Test]
        public void Necromancy_two_blocks_one_segment_and_is_consumed()
        {
            GameManager game = Game(CharacterDefinition.Champion,
                                    CharacterDefinition.SchoolOfNecromancy,
                                    out PlayerState attacker,
                                    out PlayerState necro,
                                    out _, out _);
            necro.LeftHand.SetValue(2);
            int before = necro.Health;
            var ctx = new AttackContext(attacker, necro, attacker.LeftHand, 4)
            {
                AttackType = AttackDamageType.Slashing,
                IgnoreStatBonuses = true,
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);
            Assert.AreEqual(0, ctx.AppliedDamage);
            Assert.AreEqual(before, necro.Health);
            Assert.AreEqual(1, necro.LeftHand.Value);
        }

        [Test]
        public void Necromancy_start_attack_is_free_and_applies_decay()
        {
            GameManager game = Game(CharacterDefinition.SchoolOfNecromancy,
                                    CharacterDefinition.Champion,
                                    out PlayerState necro,
                                    out PlayerState target,
                                    out _, out _);
            necro.LeftHand.SetValue(1);
            int before = target.Health;
            game.BeginTurn();
            Assert.AreEqual(before - 1, target.Health);
            Assert.IsTrue(target.HasStatus(StatusEffect.Decay));
            Assert.AreEqual(1, necro.LeftHand.Value, "免费暗蚀不消耗 1。");
        }

        [Test]
        public void Abjuration_resistance_rounds_up_and_barrier_absorbs_after_it()
        {
            GameManager game = Game(CharacterDefinition.Champion,
                                    CharacterDefinition.SchoolOfAbjuration,
                                    out PlayerState attacker,
                                    out PlayerState abjurer,
                                    out _, out _);
            abjurer.AddResistance(AttackDamageType.Bludgeoning);
            var abj = (SchoolOfAbjurationLogic)abjurer.Character;
            abj.StoreBarrier(10);

            var ctx = new AttackContext(attacker, abjurer, attacker.LeftHand, 5)
            {
                AttackType = AttackDamageType.Bludgeoning,
                IgnoreStatBonuses = true,
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);
            Assert.AreEqual(0, ctx.AppliedDamage, "5 抗性后向上取整为 3，屏障 10 全部吸收。");
            Assert.AreEqual(0, abj.BarrierPoints);
            Assert.IsFalse(abjurer.HasResistance(AttackDamageType.Bludgeoning),
                "一次实际伤害后抗性消耗。");
        }
    }
}
