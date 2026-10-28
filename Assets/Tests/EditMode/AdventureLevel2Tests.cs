using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class AdventureLevel2Tests
    {
        private static AdventureLevel2 Setup(out BoardController board)
        {
            var ca = new SimpleChoiceProvider();
            var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Champion, CharacterDefinition.Blank, ca, cb);
            board = new BoardController(game, ca, cb);
            AdventureLevel2.Prepare(game);
            var lesson = AdventureLevel2.Attach(game, board);
            lesson.Bind(board);
            board.BeginTurn();
            return lesson;
        }

        private static int Uses(AdventureLesson level) => ((ISkillUseCounter)level.Player.Character).SkillUseCount;

        private static void Human(AdventureLesson level, Hand acted, Hand addend)
        {
            Assert.IsTrue(level.Board.ClickHand(acted));
            Assert.IsTrue(level.Board.ClickHand(addend));
            if (level.Board.Phase == BoardPhase.ChoosingTargetPlayer)
                Assert.IsTrue(level.Board.ConfirmTargetPlayer());
            level.Tick();
        }

        private static void Instructor(AdventureLesson level)
        {
            for (int guard = 0; guard < 12; guard++)
            {
                level.Tick();
                if (level.Game.IsGameOver || ReferenceEquals(level.Board.ActivePlayer, level.Player)) return;
                bool acted = level.Board.Phase == BoardPhase.ChoosingAttackerHand
                    ? level.Board.MakeAutomaticMove() : level.Board.ResolvePendingAiChoice();
                Assert.IsTrue(acted || level.Game.IsGameOver, level.Board.LastAiMoveDiagnostic);
            }
            Assert.IsTrue(false, "教官行动未结束");
        }

        private static void ReachHeavyStrike(AdventureLevel2 level)
        {
            level.BeginTutorial();
            Human(level, level.Player.LeftHand, level.Soldier.LeftHand);
            Instructor(level);
            Assert.AreEqual(3, level.StepNumber);
        }

        [Test]
        public void Full_script_compares_ordinary_seven_with_heavy_seven_and_wins()
        {
            var level = Setup(out var board);
            Assert.AreEqual(AdventureChapter.InstructorName, level.Soldier.Name);
            level.BeginTutorial();
            Assert.AreEqual(5, level.StepCount);
            Assert.IsFalse(level.Soldier.LeftHand.IsShield, "起手5不是完整盾（遵循初始5的碎盾口径）");
            Human(level, level.Player.LeftHand, level.Soldier.LeftHand);
            Assert.AreEqual(11, level.Soldier.Health, "普通6+1=7伤");
            Assert.AreEqual(12, level.Player.LeftHand.Value + level.Player.RightHand.Value);
            Assert.AreEqual(0, Uses(level));
            Instructor(level);
            Assert.AreEqual(1, level.Soldier.LeftHand.Value);
            Human(level, level.Player.LeftHand, level.Soldier.RightHand);
            Assert.AreEqual(4, level.Soldier.Health, "重击4×1.5+1=7伤");
            Assert.AreEqual(4, level.Player.LeftHand.Value);
            Assert.AreEqual(6, level.Player.RightHand.Value);
            Assert.AreEqual(1, Uses(level));
            Assert.IsTrue(string.Join("", board.Log).Contains("精通重击"), "真实控制器战报记录技能");
            Instructor(level);
            Assert.AreEqual(2, level.Soldier.RightHand.Value);
            Assert.AreEqual(5, level.StepNumber);
            Assert.IsTrue(level.IsFreeStage);
            Human(level, level.Player.RightHand, level.Soldier.RightHand);
            Assert.IsTrue(level.IsComplete);
            Assert.AreEqual(20, level.Player.Health);
            Assert.AreEqual(1, Uses(level), "终结时4/8不再重击");
            Assert.Null(level.CurrentBeat);
        }

        [Test]
        public void Heavy_strike_step_restricts_source_and_addend_and_waits_for_confirmation()
        {
            var level = Setup(out var board);
            ReachHeavyStrike(level);
            Assert.AreSame(level.Player.LeftHand, level.SuggestedHand);
            Assert.IsFalse(level.AllowClick(level.Player.RightHand));
            Assert.IsTrue(board.ClickHand(level.Player.LeftHand));
            Assert.AreSame(level.Soldier.RightHand, level.SuggestedHand);
            Assert.IsFalse(level.AllowClick(level.Soldier.LeftHand));
            Assert.IsTrue(board.ClickHand(level.Soldier.RightHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
            level.Tick();
            Assert.AreEqual(3, level.StepNumber);
            Assert.AreEqual(11, level.Soldier.Health, "未确认时不结算伤害");
            Assert.AreEqual(0, Uses(level));
            Assert.IsTrue(level.AllowClick(level.Soldier.LeftHand));
            Assert.IsTrue(level.AllowClick(level.Soldier.RightHand));
            Assert.IsFalse(level.AllowClick(level.Player.LeftHand));
            Assert.IsTrue(board.ConfirmTargetPlayer());
            level.Tick();
            Assert.AreEqual(4, level.StepNumber);
            Assert.AreEqual(1, Uses(level));
        }

        [Test]
        public void Both_initial_player_choices_and_observation_beats_have_correct_guidance()
        {
            var level = Setup(out var board);
            Assert.IsFalse(level.Started);
            level.BeginTutorial();
            level.BeginTutorial();
            Assert.AreEqual(1, level.StepNumber);
            Assert.IsFalse(level.AllowClick(level.Player.RightHand));
            Assert.IsTrue(level.RejectionHint.Contains("左手"));
            Human(level, level.Player.LeftHand, level.Soldier.LeftHand);
            Assert.AreEqual(2, level.StepNumber);
            Assert.IsFalse(level.AllowClick(level.Player.LeftHand));
            Instructor(level);
            Assert.IsTrue(level.Narration.Contains("4×1.5"));
        }

        [Test]
        public void Free_practice_stays_safe_over_random_games_and_round_limit()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                var level = Setup(out var board);
                level.SkipTutorial();
                var random = new System.Random(seed);
                for (int guard = 0; guard < 120 && !level.Game.IsGameOver; guard++)
                {
                    var moves = level.Game.GetLegalMoves(level.Player);
                    var move = moves[random.Next(moves.Count)];
                    Human(level, move.ActedHand, move.TargetHand);
                    if (level.Game.IsGameOver) break;
                    int health = level.Soldier.Health;
                    Instructor(level);
                    Assert.AreEqual(20, level.Player.Health, "安全教官不伤害玩家");
                    Assert.AreEqual(health, level.Soldier.Health, "教官不回复");
                    Assert.IsFalse(level.Soldier.HasAnyShield);
                }
                Assert.IsTrue(level.IsComplete, "玩家攻击或到限均可通关 seed=" + seed);
            }
        }

        [Test]
        public void Instructor_rests_if_every_operation_has_an_effect()
        {
            var level = Setup(out var board);
            level.SkipTutorial();
            Human(level, level.Player.LeftHand, level.Soldier.RightHand); // 9，回复，交给教官
            level.Player.LeftHand.SetValue(2);
            level.Player.RightHand.SetValue(2);
            level.Soldier.LeftHand.SetValue(3);
            level.Soldier.RightHand.SetValue(7);
            Assert.IsTrue(board.CanSkipTurn);
            Instructor(level);
            Assert.AreEqual(20, level.Player.Health);
            Assert.AreEqual(3, level.Soldier.LeftHand.Value);
            Assert.AreEqual(7, level.Soldier.RightHand.Value);
            Assert.IsFalse(board.CanSkipTurn);
        }

        [Test]
        public void Replay_resets_health_hands_skill_uses_and_guided_progress()
        {
            var old = Setup(out var board);
            ReachHeavyStrike(old);
            Human(old, old.Player.LeftHand, old.Soldier.RightHand);
            Assert.AreEqual(1, Uses(old));
            var fresh = Setup(out var next);
            Assert.IsFalse(fresh.Started);
            Assert.AreEqual(20, fresh.Player.Health);
            Assert.AreEqual(18, fresh.Soldier.Health);
            Assert.AreEqual(1, fresh.Player.LeftHand.Value);
            Assert.AreEqual(6, fresh.Player.RightHand.Value);
            Assert.AreEqual(5, fresh.Soldier.LeftHand.Value);
            Assert.AreEqual(8, fresh.Soldier.RightHand.Value);
            Assert.AreEqual(0, Uses(fresh));
            fresh.BeginTutorial();
            Assert.AreEqual(1, fresh.StepNumber);
        }

        [Test]
        public void Skipping_guide_uses_safe_moves_instead_of_opening_script()
        {
            var level = Setup(out var board);
            level.SkipTutorial();
            Assert.AreEqual(5, level.StepNumber);
            Assert.IsTrue(level.AllowClick(level.Player.RightHand));
            Assert.IsTrue(level.AllowClick(level.Soldier.RightHand));
            Human(level, level.Player.RightHand, level.Soldier.LeftHand); // 6+5=1
            Instructor(level);
            Assert.AreEqual(20, level.Player.Health);
            Assert.AreEqual(18, level.Soldier.Health);
            Assert.IsFalse(level.Soldier.HasAnyShield);
        }

        [Test]
        public void Metadata_uses_distinct_keys_and_full_chapter_names()
        {
            var level = Setup(out var board);
            Assert.AreEqual("第一章：边境哨站·次战", level.DisplayTitle);
            Assert.AreEqual("第一章：边境哨站·初战", AdventureLevel1.Title);
            Assert.AreEqual("FingerDiceDuel.Adventure.Level2.Completed", level.CompletionKey);
            Assert.AreEqual(2, level.LevelNumber);
            Assert.IsTrue(level.OpeningText.Contains("自动发动"));
            Assert.IsTrue(level.SummaryText.Contains("4×1.5"));
        }
    }
}
