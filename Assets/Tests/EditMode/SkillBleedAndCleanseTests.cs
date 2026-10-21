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
    public class SkillBleedAndCleanseTests
    {
        private static readonly string[] Skills = { "source-shield", "range-shield", "power-shot", "stealth", "follow-up" };

        private static GameManager Match(CharacterDefinition role, out SimpleChoiceProvider a,
                                         out SimpleChoiceProvider b, CharacterDefinition target = null)
        {
            a = new SimpleChoiceProvider(); b = new SimpleChoiceProvider();
            return new GameManager(CharacterFactory.CreatePlayer("A", role),
                CharacterFactory.CreatePlayer("B", target ?? CharacterDefinition.Blank), a, b);
        }

        private static GameManager Skill(string skill, CharacterDefinition target = null)
        {
            var role = skill == "source-shield" || skill == "range-shield" ? CharacterDefinition.Fighter
                : skill == "power-shot" ? CharacterDefinition.Ranger
                : skill == "stealth" ? CharacterDefinition.Assassin : CharacterDefinition.Swordsman;
            var game = Match(role, out var sink, out _, target);
            var a = game.PlayerA; game.PlayerB.LeftHand.SetValue(1);
            a.LeftHand.SetValue(3); a.RightHand.SetValue(1);
            if (skill == "source-shield")
            {
                a.LeftHand.SetValue(5); a.LeftHand.SetShieldState(ShieldState.Formed);
            }
            if (skill == "range-shield")
            {
                a.RightHand.SetValue(5); a.RightHand.SetShieldState(ShieldState.Formed);
                sink.SetChoice(HandEffect.FighterRangeBreak, a.RightHand);
            }
            if (skill == "power-shot")
            {
                a.RightHand.SetValue(2); sink.SetChoice(HandEffect.RangerPowerShot, a.RightHand);
            }
            if (skill == "stealth")
            {
                a.LeftHand.SetValue(7); a.RightHand.SetValue(7);
                ((AssassinLogic)a.Character).EnterStealthForTest();
            }
            if (skill == "follow-up")
            {
                a.RightHand.SetValue(4); sink.SetChoice(HandEffect.SwordsmanFollowUp, a.RightHand);
            }
            return game;
        }

        private static TurnResult Attack(GameManager game)
            => game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);

        private static void AllStatuses(PlayerState target, PlayerState source)
        {
            foreach (var status in StatusNames.AllStatuses) target.ApplyStatus(status, source: source);
        }

        [Test]
        public void All_five_skill_paths_apply_two_turn_bleed_with_the_attacker_as_source()
        {
            foreach (var skill in Skills)
            {
                var game = Skill(skill); Attack(game);
                Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed), skill);
                Assert.AreEqual(2, game.PlayerB.StatusTurnsLeft(StatusEffect.Bleed), skill);
                Assert.AreSame(game.PlayerA, game.PlayerB.StatusSourceOf(StatusEffect.Bleed), skill);
            }
        }

        [Test]
        public void A_shield_does_not_prevent_skill_bleed()
        {
            foreach (var skill in Skills)
            {
                var game = Skill(skill);
                game.PlayerB.RightHand.SetValue(5); game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
                Attack(game);
                Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed), skill);
                Assert.AreEqual(ShieldState.Broken, game.PlayerB.RightHand.State, skill);
            }
        }

        [Test]
        public void Dodging_the_skill_attack_prevents_bleed()
        {
            foreach (var skill in Skills)
            {
                var game = Skill(skill, CharacterDefinition.Ranger);
                var sink = (SimpleChoiceProvider)game.ChoiceProviderB;
                sink.SetChoice(HandEffect.RangerDodge, game.PlayerB.LeftHand);
                Attack(game);
                Assert.False(game.PlayerB.HasStatus(StatusEffect.Bleed), skill);
            }
        }

        [Test]
        public void Cancelling_an_empowered_main_attack_prevents_bleed()
        {
            foreach (var skill in new[] { "source-shield", "range-shield", "power-shot", "stealth" })
            {
                var game = Skill(skill, CharacterDefinition.Hexer);
                game.PlayerB.RightHand.SetValue(skill == "source-shield" ? 6 : skill == "stealth" ? 8 : 4);
                Attack(game);
                Assert.False(game.PlayerB.HasStatus(StatusEffect.Bleed), skill);
                Assert.AreEqual(20, game.PlayerB.Health, skill);
            }
        }

        [Test]
        public void Follow_up_bleed_survives_cancellation_of_the_later_main_attack()
        {
            var game = Skill("follow-up", CharacterDefinition.Hexer);
            game.PlayerB.RightHand.SetValue(4);
            Attack(game);
            Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed));
            Assert.AreEqual(16, game.PlayerB.Health, "追加攻击数字为0，仍不可被妖术师取消；只取消主攻击。");
        }

        [Test]
        public void Ordinary_attacks_of_these_roles_do_not_apply_bleed()
        {
            foreach (var role in new[] { CharacterDefinition.Fighter, CharacterDefinition.Ranger, CharacterDefinition.Assassin, CharacterDefinition.Swordsman })
            {
                var game = Match(role, out _, out _);
                game.PlayerA.LeftHand.SetValue(3); Attack(game);
                Assert.False(game.PlayerB.HasStatus(StatusEffect.Bleed), role.Id);
            }
        }

        [Test]
        public void A_previously_broken_shield_does_not_empower_the_next_attack()
        {
            var game = Match(CharacterDefinition.Fighter, out _, out _);
            game.PlayerA.RightHand.SetValue(5); game.PlayerA.RightHand.SetShieldState(ShieldState.Broken);
            game.PlayerA.LeftHand.SetValue(3); Attack(game);
            Assert.False(game.PlayerB.HasStatus(StatusEffect.Bleed));
        }

        [Test]
        public void Declining_the_swordsman_follow_up_does_not_empower_the_main_hit()
        {
            var game = Skill("follow-up"); ((SimpleChoiceProvider)game.ChoiceProviderA).SetChoice(HandEffect.SwordsmanFollowUp, null);
            Attack(game); Assert.False(game.PlayerB.HasStatus(StatusEffect.Bleed));
        }

        [Test]
        public void A_real_stealth_operation_keeps_the_bonus_and_adds_bleed()
        {
            var game = Skill("stealth"); var turn = Attack(game);
            Assert.AreEqual(17, turn.DamageDealt, "运算前潜行：8基础＋7突袭＋2攻击。");
            Assert.AreEqual(8, game.PlayerA.LeftHand.Value);
            Assert.False(((AssassinLogic)game.PlayerA.Character).IsStealthActive(game.PlayerA));
            Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed));
        }

        [Test]
        public void Initial_double_seven_without_entering_stealth_has_no_bleed()
        {
            var game = Match(CharacterDefinition.Assassin, out _, out _);
            game.PlayerA.LeftHand.SetValue(7); game.PlayerA.RightHand.SetValue(7);
            var turn = Attack(game);
            Assert.AreEqual(10, turn.DamageDealt);
            Assert.False(game.PlayerB.HasStatus(StatusEffect.Bleed));
        }

        [Test]
        public void Follow_up_bleed_reduces_defense_before_the_main_hit()
        {
            var game = Skill("follow-up"); Attack(game);
            Assert.AreEqual(9, game.PlayerB.Health, "追加4点后流血使防御-1，主攻击4+2+1=7点。");
        }

        [Test]
        public void Refreshing_bleed_restores_two_turns_without_stacking()
        {
            var game = Skill("power-shot"); Attack(game); game.PlayerB.TickStatuses();
            game.PlayerB.Heal(20); game.ForceActivePlayerForTest(game.PlayerA);
            game.PlayerA.LeftHand.SetValue(3); game.PlayerA.RightHand.SetValue(2); Attack(game);
            Assert.AreEqual(2, game.PlayerB.StatusTurnsLeft(StatusEffect.Bleed));
            Assert.AreEqual(1, game.PlayerB.Statuses.Count);
        }

        [Test]
        public void Zero_damage_landed_hits_still_apply_bleed()
        {
            var game = Match(CharacterDefinition.Soldier, out _, out _, CharacterDefinition.Soldier);
            var ctx = new AttackContext(game.PlayerA, game.PlayerB, game.PlayerA.LeftHand, 0) { InflictsBleed = true };
            ctx.Resolve(game.ChoiceProviderB, game);
            Assert.AreEqual(0, ctx.AppliedDamage);
            Assert.True(game.PlayerB.HasStatus(StatusEffect.Bleed));
        }

        [Test]
        public void Cleric_nine_cleanses_all_eighteen_statuses_before_healing_decay()
        {
            var game = Match(CharacterDefinition.Cleric, out var sink, out _);
            game.PlayerA.LeftHand.SetValue(8); game.PlayerB.TakeDamage(10); AllStatuses(game.PlayerB, game.PlayerA);
            sink.SetChoice(HandEffect.ClericHealTarget, game.PlayerB.LeftHand); Attack(game);
            Assert.AreEqual(19, game.PlayerB.Health);
            Assert.AreEqual(0, game.PlayerB.Statuses.Count);
            Assert.Null(game.PlayerB.StatusSourceOf(StatusEffect.Panic));
        }

        [Test]
        public void Cleric_healing_cleanses_even_a_full_health_target_without_breaking_shields()
        {
            var game = Match(CharacterDefinition.Cleric, out var sink, out _);
            game.PlayerA.LeftHand.SetValue(8); AllStatuses(game.PlayerB, game.PlayerA);
            game.PlayerB.RightHand.SetValue(5); game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            sink.SetChoice(HandEffect.ClericHealTarget, game.PlayerB.LeftHand); Attack(game);
            Assert.AreEqual(20, game.PlayerB.Health); Assert.AreEqual(0, game.PlayerB.Statuses.Count);
            Assert.AreEqual(ShieldState.Formed, game.PlayerB.RightHand.State);
        }

        [Test]
        public void Cleric_converted_four_six_and_eight_all_cleanse_the_selected_target()
        {
            foreach (int result in new[] { 4, 6, 8 })
            {
                var game = Match(CharacterDefinition.Cleric, out var sink, out _);
                game.PlayerA.LeftHand.SetValue(result - 1); game.PlayerB.TakeDamage(10); AllStatuses(game.PlayerB, game.PlayerA);
                sink.SetChoice(HandEffect.ClericAttackOrHeal, game.PlayerA.LeftHand);
                sink.SetChoice(HandEffect.ClericHealTarget, game.PlayerB.LeftHand); Attack(game);
                Assert.AreEqual(10 + result, game.PlayerB.Health); Assert.AreEqual(0, game.PlayerB.Statuses.Count);
            }
        }

        [Test]
        public void Cleric_double_nine_cleanses_both_independently_selected_targets()
        {
            var game = Match(CharacterDefinition.Cleric, out var sink, out _);
            game.PlayerA.LeftHand.SetValue(8); game.PlayerA.RightHand.SetValue(9);
            game.PlayerA.TakeDamage(10); game.PlayerA.ApplyStatus(StatusEffect.Decay);
            game.PlayerB.TakeDamage(10); AllStatuses(game.PlayerB, game.PlayerA);
            sink.SetChoice(HandEffect.ClericExtraHealTarget, game.PlayerB.LeftHand);
            sink.SetChoice(HandEffect.ClericHealTarget, game.PlayerA.LeftHand); Attack(game);
            Assert.AreEqual(19, game.PlayerA.Health); Assert.AreEqual(19, game.PlayerB.Health);
            Assert.AreEqual(0, game.PlayerA.Statuses.Count); Assert.AreEqual(0, game.PlayerB.Statuses.Count);
        }

        [Test]
        public void Other_roles_healing_does_not_cleanse_decay()
        {
            var game = Match(CharacterDefinition.Soldier, out _, out _);
            game.PlayerA.LeftHand.SetValue(8); game.PlayerA.TakeDamage(10); game.PlayerA.ApplyStatus(StatusEffect.Decay);
            Attack(game); Assert.AreEqual(10, game.PlayerA.Health); Assert.True(game.PlayerA.HasStatus(StatusEffect.Decay));
        }

        [Test]
        public void Dual_cleric_cleansing_works_with_cleric_in_either_slot()
        {
            foreach (bool clericFirst in new[] { true, false })
            {
                var a = DualGeneralFactory.Create("双将", clericFirst ? CharacterDefinition.Cleric : CharacterDefinition.Hunter,
                    clericFirst ? CharacterDefinition.Hunter : CharacterDefinition.Cleric);
                var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
                var sink = new SimpleChoiceProvider(); var game = new GameManager(a, b, sink, new SimpleChoiceProvider());
                a.LeftHand.SetValue(3); b.TakeDamage(10); AllStatuses(b, a);
                sink.SetChoice(HandEffect.ClericAttackOrHeal, a.LeftHand); sink.SetChoice(HandEffect.ClericHealTarget, b.LeftHand);
                game.ExecuteTurn(a, a.LeftHand, b.LeftHand);
                Assert.AreEqual(14, b.Health); Assert.AreEqual(0, b.Statuses.Count);
            }
        }
        [Test]
        public void Consecutive_bleed_deaths_advance_to_the_next_living_player()
        {
            var players = new System.Collections.Generic.List<PlayerState>();
            var providers = new System.Collections.Generic.List<IChoiceProvider>();
            for (int i = 0; i < 4; i++)
            {
                players.Add(CharacterFactory.CreatePlayer("P" + i, CharacterDefinition.Blank));
                providers.Add(new SimpleChoiceProvider());
            }
            var game = GameManager.Create2v2(players, providers);
            for (int i = 0; i < 2; i++)
            {
                players[i].TakeDamage(19);
                players[i].ApplyStatus(StatusEffect.Bleed);
            }
            var board = new BoardController(game, (IPlayerChoiceSink)providers[0], (IPlayerChoiceSink)providers[1]);
            board.BindPlayer(players[2], null, (IPlayerChoiceSink)providers[2]);
            board.BindPlayer(players[3], null, (IPlayerChoiceSink)providers[3]);
            board.BeginTurn();
            Assert.False(players[0].IsAlive); Assert.False(players[1].IsAlive);
            Assert.True(string.Join(" | ", board.Log).Contains("流血"));
            Assert.False(string.Join(" | ", board.Log).Contains("中毒"));
            Assert.False(game.IsGameOver);
            Assert.AreSame(players[2], game.CurrentPlayer);
            Assert.AreSame(players[2], board.ActivePlayer);
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase);
            Assert.True(board.CanClick(players[2].LeftHand));
        }
        [Test]
        public void Ai_double_nine_does_not_overwrite_game_over_after_bleed_kills_the_next_player()
        {
            var game = Match(CharacterDefinition.Cleric, out var a, out var b);
            game.PlayerA.LeftHand.SetValue(8); game.PlayerA.RightHand.SetValue(9);
            game.PlayerA.TakeDamage(10);
            game.PlayerB.TakeDamage(19); game.PlayerB.ApplyStatus(StatusEffect.Bleed);
            var board = new BoardController(game, a, b, new GreedyChoiceProvider(), new GreedyChoiceProvider());
            board.BeginTurn();
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.True(game.IsGameOver);
            Assert.AreEqual(BoardPhase.GameOver, board.Phase);
            Assert.AreEqual(20, game.PlayerA.Health);
            Assert.AreEqual(1, game.TurnCount);
        }
    }
}
