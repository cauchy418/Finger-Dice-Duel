using System;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class AdventureLaterLevelsTests
    {
        private static AdventureLesson Setup(int number, bool begin = true)
        {
            var ca = new SimpleChoiceProvider(); var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Breaker, CharacterDefinition.Blank, ca, cb);
            var board = new BoardController(game, ca, cb);
            AdventureLesson lesson;
            if (number == 3) { AdventureLevel3.Prepare(game); lesson = AdventureLevel3.Attach(game, board); }
            else if (number == 4) { AdventureLevel4.Prepare(game); lesson = AdventureLevel4.Attach(game, board); }
            else { game.PlayerB.Rename("训练教官"); lesson = AdventureLevel5.Attach(game, board); }
            lesson.Bind(board);
            if (begin) { board.BeginTurn(); lesson.BeginTutorial(); }
            return lesson;
        }

        private static bool GuidedChoice(AdventureLesson lesson)
        {
            var b = lesson.Board;
            if (lesson.CurrentBeat != null && lesson.CurrentBeat.Choices.TryGetValue(b.PendingEffect, out var hand))
            {
                Assert.IsTrue(hand == null ? b.DeclinePendingChoice() : b.ClickHand(hand), "引导选择 " + b.PendingEffect);
                return true;
            }
            if (b.Phase == BoardPhase.ChoosingDefenseShield)
            {
                Assert.IsTrue(b.ClickHand(lesson.Player.LeftHand.IsShield ? lesson.Player.LeftHand : lesson.Player.RightHand));
                return true;
            }
            return false;
        }

        private static void Human(AdventureLesson lesson, Hand acted, Hand addend)
        {
            var b = lesson.Board;
            Assert.IsTrue(b.ClickHand(acted)); Assert.IsTrue(b.ClickHand(addend));
            for (int guard = 0; guard < 12; guard++)
            {
                if (b.Phase == BoardPhase.ChoosingTargetPlayer)
                {
                    if (b.CanGiveUpAttack) Assert.IsTrue(b.GiveUpAttack());
                    else Assert.IsTrue(b.ConfirmTargetPlayer());
                }
                else if (!GuidedChoice(lesson)) break;
            }
            lesson.Tick();
        }

        private static void Opponent(AdventureLesson lesson)
        {
            var b = lesson.Board;
            for (int guard = 0; guard < 20; guard++)
            {
                lesson.Tick();
                if (lesson.IsFinished || ReferenceEquals(b.ActivePlayer, lesson.Player)) return;
                bool acted = b.IsHumanChooser ? GuidedChoice(lesson)
                    : b.Phase == BoardPhase.ChoosingAttackerHand ? b.MakeAutomaticMove() : b.ResolvePendingAiChoice();
                Assert.IsTrue(acted || lesson.Game.IsGameOver, "教官推进 " + b.Phase + " " + b.PendingEffect + " " + b.Hint);
            }
            Assert.IsTrue(false, "教官回合未结束");
        }

        [Test]
        public void Third_passive_break_chains_without_bleed_and_reforming_repairs_both()
        {
            var l = Setup(3);
            Assert.IsTrue(l.Soldier.RightHand.IsShield, "关卡预先合成的盾");
            Human(l, l.Player.LeftHand, l.Soldier.LeftHand);
            Opponent(l);
            Assert.AreEqual(20, l.Player.Health);
            Assert.AreEqual(ShieldState.Broken, l.Player.LeftHand.State, string.Join(" | ",l.Board.Log)+" phase="+l.Board.Phase+" NPC="+l.Soldier.LeftHand+"/"+l.Soldier.RightHand);
            Assert.AreEqual(ShieldState.Broken, l.Soldier.RightHand.State);
            Assert.IsFalse(l.Soldier.HasStatus(StatusEffect.Bleed));
            Assert.AreEqual(3, l.StepNumber);
            Assert.IsTrue(string.Join("", l.Board.Log).Contains("连锁破盾"));
            Human(l, l.Player.RightHand, l.Soldier.LeftHand);
            Assert.IsTrue(l.Player.LeftHand.IsShield && l.Player.RightHand.IsShield);
        }

        [Test]
        public void Third_active_shield_attack_adds_bleed_and_free_stage_is_reached()
        {
            var l = Setup(3);
            Human(l, l.Player.LeftHand, l.Soldier.LeftHand); Opponent(l);
            Human(l, l.Player.RightHand, l.Soldier.LeftHand); Opponent(l);
            Assert.AreEqual(5, l.StepNumber);
            Human(l, l.Player.LeftHand, l.Soldier.RightHand);
            Assert.AreEqual(7, l.Board.LastTurn.DamageDealt);
            Assert.IsTrue(l.Soldier.HasStatus(StatusEffect.Bleed));
            Assert.AreEqual(10, l.Soldier.Health, "18-7-回合开始流血1");
            Assert.IsTrue(l.Player.RightHand.IsShield, "未消耗另一面盾");
            Assert.AreEqual(6, l.StepNumber);
            Opponent(l);
            Assert.IsTrue(l.IsFreeStage);
            Assert.IsTrue(string.Join("", l.Board.Log).Contains("流血"));
            Assert.AreEqual(20, l.Player.Health);
        }

        [Test]
        public void Third_choice_gate_requires_enemy_chain_and_declines_own_extra_shields()
        {
            var l = Setup(3);
            Human(l, l.Player.LeftHand, l.Soldier.LeftHand);
            for (int i = 0; i < 10 && l.Board.PendingEffect != HandEffect.BreakerChainBreak; i++)
            {
                if (l.Board.Phase == BoardPhase.ChoosingAttackerHand) l.Board.MakeAutomaticMove();
                else l.Board.ResolvePendingAiChoice();
            }
            Assert.AreEqual(HandEffect.BreakerChainBreak, l.Board.PendingEffect);
            Assert.IsFalse(l.AllowDeclineChoice);
            Assert.IsTrue(l.AllowAcceptChoice);
            Assert.IsTrue(l.AllowClick(l.Soldier.RightHand));
            Assert.IsFalse(l.AllowClick(l.Player.LeftHand));
            Opponent(l); Human(l, l.Player.RightHand, l.Soldier.LeftHand); Opponent(l);
            l.Board.ClickHand(l.Player.LeftHand); l.Board.ClickHand(l.Soldier.RightHand);
            Assert.AreEqual(HandEffect.BreakerRangeBreak, l.Board.PendingEffect);
            Assert.IsFalse(l.AllowAcceptChoice);
            Assert.IsTrue(l.AllowDeclineChoice);
            Assert.IsFalse(l.AllowClick(l.Player.RightHand));
        }

        [Test]
        public void Fourth_out_of_range_can_borrow_addend_but_must_abandon_without_damage()
        {
            var l = Setup(4); var b = l.Board;
            Assert.AreEqual(2, l.Game.Players.Count);
            Assert.AreEqual(2, l.Game.StaticSeatObstacleCount);
            Assert.AreEqual(2, l.Game.DistanceBetween(l.Player, l.Soldier));
            Assert.IsTrue(b.ClickHand(l.Player.LeftHand)); Assert.IsTrue(b.ClickHand(l.Soldier.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, b.Phase);
            Assert.IsFalse(b.CanBeAttacked(l.Soldier), "可达判据"); Assert.IsFalse(b.ConfirmTargetPlayer(), "超距确认");
            Assert.IsTrue(b.CanGiveUpAttack); Assert.IsTrue(b.GiveUpAttack()); l.Tick();
            Assert.AreEqual(4, l.Player.LeftHand.Value); Assert.AreEqual(18, l.Soldier.Health);
            Opponent(l);
            Assert.AreEqual(3, l.StepNumber);
            Assert.AreEqual(2, l.Game.TurnCount, "占位物不插入回合");
        }

        [Test]
        public void Fourth_range_break_enables_target_deals_nine_and_finishes_objective()
        {
            var l = Setup(4); var b = l.Board;
            Human(l, l.Player.LeftHand, l.Soldier.LeftHand); Opponent(l);
            Human(l, l.Player.RightHand, l.Soldier.RightHand); Opponent(l);
            Assert.AreEqual(5, l.StepNumber);
            b.ClickHand(l.Player.LeftHand); b.ClickHand(l.Soldier.RightHand);
            Assert.AreEqual(HandEffect.BreakerRangeBreak, b.PendingEffect);
            Assert.IsFalse(b.CanBeAttacked(l.Soldier));
            Assert.IsFalse(l.AllowDeclineChoice); Assert.IsTrue(l.AllowAcceptChoice);
            Assert.IsTrue(b.AcceptPendingChoice(), "发动换射程 "+b.Phase+" "+b.Hint);
            Assert.IsTrue(b.CanBeAttacked(l.Soldier), "发动后可达 "+b.Hint); Assert.IsTrue(b.ConfirmTargetPlayer(), "确认远攻 "+b.Hint); l.Tick();
            Assert.AreEqual(9, b.LastTurn.DamageDealt, b.Phase+" "+b.PendingEffect+" "+b.Hint+" "+b.ChoiceStateProbe+" "+string.Join(" | ",b.Log));
            Assert.IsTrue(l.IsComplete && l.IsFinished);
            Assert.IsFalse(l.Game.IsGameOver, "完成训练目标，不伪造击杀");
            Assert.IsTrue(l.Soldier.IsAlive && l.Soldier.HasStatus(StatusEffect.Bleed));
            Assert.AreEqual(1, GameManager.BaseRangeOf(l.Player));
            Assert.AreEqual(2, l.Game.DistanceBetween(l.Player, l.Soldier));
            Assert.AreEqual(ShieldState.Broken, l.Player.RightHand.State);
            Assert.IsFalse(l.AllowsTutorialSkip);
        }

        [Test]
        public void Seat_obstacles_do_not_change_normal_distance_targets_or_winner()
        {
            var l = Setup(5, false);
            Assert.AreEqual(0, l.Game.StaticSeatObstacleCount);
            Assert.AreEqual(1, l.Game.DistanceBetween(l.Player, l.Soldier));
            l.Game.ConfigureSeatObstacles(1, 1);
            var legal = l.Game.GetLegalMoves(l.Player);
            foreach (var move in legal) Assert.IsTrue(l.Soldier.Owns(move.TargetHand));
            l.Soldier.SetHealthExact(1); l.Player.LeftHand.SetValue(3);
            l.Game.ConfigureSeatObstacles(0, 0);
            l.Board.BeginTurn(); l.BeginTutorial(); Human(l, l.Player.LeftHand, l.Soldier.LeftHand);
            Assert.IsTrue(l.Game.IsGameOver && l.IsComplete);
        }

        [Test]
        public void Fifth_uses_real_classic_allocation_and_higher_roller_first()
        {
            foreach (var rolls in new[] { new[] { 6, 12 }, new[] { 14, 8 }, new[] { 10, 10 }, new[] { 1, 20 }, new[] { 19, 1 } })
            {
                var l = Setup(5, false); var start = new MatchStartController(l.Game);
                start.RollWith(rolls[0], rolls[1]);
                Assert.AreEqual(rolls[0] >= rolls[1] ? l.Player : l.Soldier, start.FirstToAct);
                while (!start.IsComplete) Assert.IsTrue(start.Submit(start.Candidates[0]));
                Assert.AreEqual(0, l.Game.TurnCount, "分配不消耗行动");
                Assert.IsFalse(l.Player.HasAnyShield || l.Soldier.HasAnyShield);
                l.Board.SetFirstPlayer(start.FirstToAct); l.BeginTutorial(); l.Board.BeginTurn();
                Assert.AreSame(start.FirstToAct, l.Board.ActivePlayer);
                Assert.IsTrue(l.UsesClassicSetup && !l.AllowsTutorialSkip);
                Assert.IsTrue(l.Narration.Contains("正常进攻"));
            }
        }

        [Test]
        public void Fifth_ai_can_attack_kill_player_and_failure_is_not_completion()
        {
            var l = Setup(5, false);
            l.Player.SetHealthExact(1);
            l.Player.LeftHand.SetValue(1); l.Player.RightHand.SetValue(1);
            l.Soldier.LeftHand.SetValue(3); l.Soldier.RightHand.SetValue(7);
            l.Board.SetFirstPlayer(l.Soldier); l.BeginTutorial(); l.Board.BeginTurn();
            Assert.Null(l.Board.ScriptedRestAllowed);
            Opponent(l);
            Assert.IsTrue(l.Game.IsGameOver && l.IsFinished);
            Assert.IsFalse(l.IsComplete);
            Assert.AreSame(l.Soldier, l.Game.Winner);
        }

        [Test]
        public void Fifth_ai_can_heal_and_form_shields_without_protection_filter()
        {
            var l = Setup(5, false);
            l.Player.LeftHand.SetValue(4); l.Player.RightHand.SetValue(4);
            l.Soldier.LeftHand.SetValue(5); l.Soldier.RightHand.SetValue(5);
            l.Soldier.SetHealthExact(2);
            l.Board.SetFirstPlayer(l.Soldier); l.BeginTutorial(); l.Board.BeginTurn();
            Opponent(l);
            Assert.IsTrue(l.Soldier.Health > 2, "正常AI可回复而非安全剧本");
        }

        [Test]
        public void Later_lesson_metadata_keys_background_and_replay_data_are_independent()
        {
            foreach (int n in new[] { 3, 4, 5 })
            {
                var l = Setup(n, false);
                Assert.AreEqual(AdventureChapter.InstructorName, l.Soldier.Name);
                Assert.AreEqual(n, l.LevelNumber);
                Assert.AreEqual("FingerDiceDuel.Adventure.Level" + n + ".Completed", l.CompletionKey);
                Assert.AreEqual("Backgrounds/border_outpost_chapter_01", l.BattleBackgroundKey);
                Assert.IsFalse(l.Started); l.Player.SetHealthExact(1);
                var fresh = Setup(n, false); Assert.AreEqual(20, fresh.Player.Health);
                Assert.AreEqual(n == 5 ? 20 : 18, fresh.Soldier.Health);
            }
            Assert.IsTrue(AdventureLevel5.InitiativeHint.Contains("小点先分配") && AdventureLevel5.InitiativeHint.Contains("大点先行动"));
        }
        [Test]
        public void Passive_chain_uses_actual_selected_shield_even_when_addend_is_not_shield()
        {
            var ca = new SimpleChoiceProvider(); var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.Breaker, ca, cb);
            var b = new BoardController(game, ca, cb);
            game.PlayerA.LeftHand.SetValue(3); game.PlayerA.RightHand.SetValue(5);
            game.PlayerA.RightHand.SetShieldState(ShieldState.Formed);
            game.PlayerB.LeftHand.SetValue(5); game.PlayerB.RightHand.SetValue(5);
            game.PlayerB.LeftHand.SetShieldState(ShieldState.Formed); game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            b.BeginTurn(); b.ClickHand(game.PlayerA.LeftHand); b.ClickHand(game.PlayerB.RightHand); b.ConfirmTargetPlayer();
            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, b.Phase);
            Assert.IsTrue(b.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(HandEffect.BreakerChainBreak, b.PendingEffect);
            Assert.IsTrue(b.ClickHand(game.PlayerA.RightHand));
            Assert.AreEqual(ShieldState.Broken, game.PlayerA.RightHand.State);
            Assert.AreEqual(ShieldState.Broken, game.PlayerB.LeftHand.State);
            Assert.IsTrue(game.PlayerB.RightHand.IsShield);
            Assert.AreEqual(1, game.TurnCount, "收集连锁不能重复结算攻击");
        }

        [Test]
        public void Seat_obstacle_setup_rejects_invalid_counts_and_tracks_seats_after_swap()
        {
            var l = Setup(5, false);
            bool invalid = false;
            try { l.Game.ConfigureSeatObstacles(1); } catch (ArgumentException) { invalid = true; }
            Assert.IsTrue(invalid);
            invalid = false;
            try { l.Game.ConfigureSeatObstacles(-1, 1); } catch (ArgumentOutOfRangeException) { invalid = true; }
            Assert.IsTrue(invalid);
            l.Game.ConfigureSeatObstacles(1, 1);
            Assert.IsTrue(l.Game.TrySwapSeats(l.Player, l.Soldier));
            Assert.AreEqual(2, l.Game.DistanceBetween(l.Player, l.Soldier));
            Assert.AreEqual(2, l.Game.Players.Count);
        }

    }
}
