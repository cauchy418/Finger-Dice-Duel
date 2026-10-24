using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 冒险模式 · 剧情模式 · 第一关「初战 · 边境哨站」的剧本回归。
    ///
    /// 玩家 = 战士·勇士（攻+1/防0/生命20），士兵 = 白板（攻0/防0/生命12）。
    /// 逐回合数值与 Docs/Adventure/level-01.md 的演算一一对应：
    ///   1+3=4→5 伤（12→7）；士兵 3+1=4→4 伤（20→16）；1+4=5 合盾；
    ///   士兵 1+5=6 被盾挡、盾碎；5+4=9 回血；
    ///   士兵 6+4=10→1 满十归一；回血（16→20，上限截断）；4+4=8→9 伤终结（7−9 阵亡）。
    /// </summary>
    [TestFixture]
    public class AdventureLevel1Tests
    {
        private static AdventureLevel1 Setup(out BoardController board)
        {
            var ca = new SimpleChoiceProvider();
            var cb = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Champion,
                                                     CharacterDefinition.Blank, ca, cb);
            board = new BoardController(game, ca, cb);
            var level = AdventureLevel1.Attach(game, board);
            AdventureLevel1.PrepareSoldier(game.PlayerB);
            level.Bind(board);
            return level;
        }

        /// <summary>替玩家走一次引导步：点运算源 → 点加数 →（仅攻击）确认目标。</summary>
        private static void PlayGuided(BoardController board, AdventureLevel1 level,
                                        Hand acted, Hand addend)
        {
            Assert.IsTrue(board.ClickHand(acted), "点运算源");
            Assert.IsTrue(board.ClickHand(addend), "点加数");
            if (board.Phase == BoardPhase.ChoosingTargetPlayer)
                Assert.IsTrue(board.ConfirmTargetPlayer(), "确认攻击目标");
            level.Tick();
        }

        /// <summary>
        /// 士兵脚本回合（与界面 AI 泵同一节奏）：
        /// 出招后若停在"选目标"等后续阶段，继续用 ResolvePendingAiChoice 代答，
        /// 直到回合结算完毕、轮到人类玩家。
        /// </summary>
        private static void PlaySoldier(BoardController board, AdventureLevel1 level)
        {
            for (int guard = 0; guard < 12; guard++)
            {
                level.Tick();
                if (level.IsComplete) return;
                if (board.Phase == BoardPhase.ChoosingAttackerHand
                    && ReferenceEquals(board.ActivePlayer, level.Player))
                {
                    level.Tick();
                    return;      // 士兵回合结算完毕，轮到玩家
                }

                bool acted = board.Phase == BoardPhase.ChoosingAttackerHand
                    ? board.MakeAutomaticMove()
                    : board.ResolvePendingAiChoice();
                Assert.IsTrue(acted || board.Phase == BoardPhase.GameOver,
                    "士兵回合推进失败：" + board.LastAiMoveDiagnostic
                    + " | phase=" + board.Phase
                    + " | hint=" + board.Hint
                    + " | illegal=" + (board.LastIllegalOperation ?? "无"));
            }

            Assert.IsTrue(false, "士兵回合未在限定步数内完成：phase=" + board.Phase
                                 + " | " + board.LastAiMoveDiagnostic);
        }

        [Test]
        public void Full_script_plays_out_with_verified_values_and_player_wins()
        {
            var level = Setup(out var board);
            PlayerState player = level.Player;
            PlayerState soldier = level.Soldier;

            Assert.AreEqual("边境士兵", soldier.Name);
            Assert.AreEqual(AdventureLevel1.SoldierMaxHealth, soldier.Health, "士兵生命 12（旧伤）");
            Assert.AreEqual(3, soldier.LeftHand.Value);
            Assert.AreEqual(1, soldier.RightHand.Value);
            Assert.AreEqual(20, player.Health);
            Assert.AreEqual(1, player.LeftHand.Value);
            Assert.AreEqual(1, player.RightHand.Value);

            board.BeginTurn();
            level.BeginTutorial();
            Assert.IsTrue(AdventureLevel1.IntroText.Length > 0, "开场文案就绪");

            // 拍 1：攻击 1+3=4 → 4+1攻=5 伤
            level.Tick();
            Assert.IsTrue(level.Narration != null && level.Narration.Contains("1+3=4"),
                "拍 1 旁白就绪");
            PlayGuided(board, level, player.LeftHand, soldier.LeftHand);
            Assert.AreEqual(7, soldier.Health, "12 − 5 = 7");
            Assert.AreEqual(4, player.LeftHand.Value);

            // 拍 2：士兵还手 3+1=4 → 4 伤
            level.Tick();
            PlaySoldier(board, level);
            Assert.AreEqual(16, player.Health, "20 − 4 = 16");
            Assert.AreEqual(4, soldier.LeftHand.Value, "士兵的运算源变成 4");

            // 拍 3：合盾 1+4=5
            level.Tick();
            PlayGuided(board, level, player.RightHand, soldier.LeftHand);
            Assert.AreEqual(ShieldState.Formed, player.RightHand.State, "右手合成盾");

            // 拍 4：士兵 1+5=6 → 被盾挡下、盾碎
            level.Tick();
            PlaySoldier(board, level);
            Assert.AreEqual(ShieldState.Broken, player.RightHand.State, "盾挡一次后碎裂");
            Assert.AreEqual(16, player.Health, "盾挡下，不掉血");
            Assert.AreEqual(6, soldier.RightHand.Value);

            // 拍 5：回血 5+4=9 → 16+9=20（上限截断）
            level.Tick();
            PlayGuided(board, level, player.RightHand, soldier.LeftHand);
            Assert.AreEqual(20, player.Health, "回复被上限截断到 20");
            Assert.AreEqual(9, player.RightHand.Value);
            Assert.AreEqual(ShieldState.None, player.RightHand.State, "碎盾手运算后回到普通手");

            // 拍 6：士兵 6+4=10 → 1（满十归一）
            level.Tick();
            PlaySoldier(board, level);
            Assert.AreEqual(1, soldier.RightHand.Value, "6+4=10 → 个位 1");

            // 拍 7：半自由决胜 4+4=8 → 8+1=9 伤，士兵 7−9 阵亡
            level.Tick();
            var beat = level.CurrentBeat;
            Assert.IsTrue(beat.FreeClicks, "决胜拍放开点击");
            PlayGuided(board, level, player.LeftHand, soldier.LeftHand);
            Assert.IsFalse(soldier.IsAlive, "士兵 7 − 9 阵亡");
            Assert.IsTrue(level.IsComplete, "关卡完成");
            Assert.Null(level.CurrentBeat, "通关后无待办拍");
            Assert.IsTrue(level.Game.IsGameOver);
            Assert.AreSame(player, level.Game.Winner);
        }

        [Test]
        public void Guided_beats_reject_wrong_hands_without_advancing()
        {
            var level = Setup(out var board);
            board.BeginTurn();
            level.BeginTutorial();
            level.Tick();

            // 拍 1（选运算源）：只许点玩家左手
            Assert.IsTrue(level.AllowClick(level.Player.LeftHand));
            Assert.IsFalse(level.AllowClick(level.Player.RightHand));
            Assert.IsFalse(level.AllowClick(level.Soldier.LeftHand));
            Assert.AreEqual(0, level.Game.TurnCount, "被拒绝的点击不推进回合");

            // 点了不被允许的手也硬走不通：直接替玩家点了左手后，
            // 选加数阶段只许点士兵左手（点士兵右手应被守门拒绝）。
            Assert.IsTrue(board.ClickHand(level.Player.LeftHand));
            Assert.IsTrue(level.AllowClick(level.Soldier.LeftHand));
            Assert.IsFalse(level.AllowClick(level.Soldier.RightHand));
            Assert.IsFalse(level.AllowClick(level.Player.RightHand));
            Assert.AreEqual(0, level.Game.TurnCount);
        }

        [Test]
        public void Guarded_soldier_never_heals_or_shields_and_keeps_attacks_reachable()
        {
            var level = Setup(out var board);
            PlayerState player = level.Player;
            PlayerState soldier = level.Soldier;
            board.BeginTurn();
            level.BeginTutorial();
            level.Tick();

            // 按剧本走到拍 7（自由阶段）
            PlayGuided(board, level, player.LeftHand, soldier.LeftHand);   // 拍 1 攻击
            level.Tick();
            PlaySoldier(board, level);                                       // 拍 2 士兵还手
            level.Tick();
            PlayGuided(board, level, player.RightHand, soldier.LeftHand);   // 拍 3 合盾
            level.Tick();
            PlaySoldier(board, level);                                       // 拍 4 被盾挡
            level.Tick();
            PlayGuided(board, level, player.RightHand, soldier.LeftHand);   // 拍 5 回血
            level.Tick();
            PlaySoldier(board, level);                                       // 拍 6 满十归一
            level.Tick();
            Assert.IsTrue(level.CurrentBeat.FreeClicks, "进入自由阶段");

            // 玩家先不合击杀：左手 4 + 士兵右手 1 = 5，自己合一面盾（合法但不致命）
            Assert.IsTrue(board.ClickHand(player.LeftHand));
            Assert.IsTrue(board.ClickHand(soldier.RightHand));
            Assert.False(board.Phase == BoardPhase.ChoosingTargetPlayer, "合盾无目标步");
            level.Tick();

            // 士兵进入收官保护：脚本步已耗尽，走保护算法。
            // 现场推演：士兵 4/1，玩家 5(盾)/9 ——
            //   士兵 4+5=9（回血，禁）、4+9=3（无效果，之后玩家 5+3=8 可攻击）、
            //   1+5=6（攻击，之后玩家 5+4=9、9+6=5、9+4=3、5+6=1 —— 无攻击可达）、
            //   1+9=10→1（无效果，之后玩家 5+1=6 可攻击）。
            // 保护算法优先"无效果 + 玩家可达"，两个 1100 分里取先枚举到的 4+9=3。
            int soldierHealthBefore = soldier.Health;
            Assert.IsTrue(board.MakeAutomaticMove());
            Assert.AreEqual(soldierHealthBefore, soldier.Health, "收官士兵永不回血");
            Assert.IsFalse(soldier.LeftHand.State == ShieldState.Formed
                           || soldier.RightHand.State == ShieldState.Formed,
                "收官士兵永不合盾");

            // 收官保证：玩家下一回合至少一个攻击可达（5+3=8 或 9+…按现场手值枚举）
            bool canAttack = false;
            int[] mine = { player.LeftHand.Value, player.RightHand.Value };
            int[] theirs = { soldier.LeftHand.Value, soldier.RightHand.Value };
            for (int i = 0; i < mine.Length && !canAttack; i++)
                for (int j = 0; j < theirs.Length && !canAttack; j++)
                {
                    int result = Hand.ComputeSum(mine[i], theirs[j]);
                    if (result == 4 || result == 6 || result == 8) canAttack = true;
                }
            Assert.IsTrue(canAttack, "收官保护保证玩家有攻击可达");

            // 玩家按提示完成击杀，关卡结束
            level.Tick();
            // 现场：玩家 5/9，士兵 3/1 → 5+3=8 攻击，8+1=9 伤
            Assert.IsTrue(board.ClickHand(player.LeftHand));
            Assert.IsTrue(board.ClickHand(soldier.LeftHand));
            Assert.IsTrue(board.ConfirmTargetPlayer());
            level.Tick();
            Assert.IsTrue(level.IsComplete, "自由阶段击杀后通关");
        }

        [Test]
        public void Free_practice_keeps_soldier_safe_for_many_turns_and_round_limit()
        {
            for (int seed = 0; seed < 32; seed++)
            {
                var level = Setup(out var board);
                board.BeginTurn();
                level.SkipTutorial();
                var random = new System.Random(seed);
                for (int guard = 0; guard < 120 && !level.Game.IsGameOver; guard++)
                {
                    Assert.AreSame(level.Player, board.ActivePlayer, "每轮回到玩家");
                    var moves = level.Game.GetLegalMoves(level.Player);
                    var move = moves[random.Next(moves.Count)];
                    PlayGuided(board, level, move.ActedHand, move.TargetHand);
                    if (level.Game.IsGameOver) break;
                    int health = level.Player.Health;
                    int soldierHealth = level.Soldier.Health;
                    PlaySoldier(board, level);
                    Assert.AreEqual(health, level.Player.Health, "收官不造成伤害 seed=" + seed);
                    Assert.AreEqual(soldierHealth, level.Soldier.Health, "收官不治疗士兵");
                    Assert.IsFalse(level.Soldier.HasAnyShield, "收官不合盾");
                }
                Assert.IsTrue(level.IsComplete, "攻击或到限均由玩家取胜 seed=" + seed);
            }
        }

        [Test]
        public void Soldier_rests_when_every_operation_attacks_heals_or_shields()
        {
            var level = Setup(out var board);
            board.BeginTurn();
            level.SkipTutorial();
            PlayGuided(board, level, level.Player.LeftHand, level.Soldier.RightHand);
            // 2/2 对 3/7：只会算出5或9，不能让默认合法运算绕过收官限制。
            level.Player.LeftHand.SetValue(2);
            level.Player.RightHand.SetValue(2);
            level.Soldier.LeftHand.SetValue(3);
            level.Soldier.RightHand.SetValue(7);
            Assert.AreSame(level.Soldier, board.ActivePlayer);
            Assert.IsTrue(board.CanSkipTurn, "仅NPC获得关卡休整资格");
            int turns = level.Game.TurnCount;
            int health = level.Player.Health;
            PlaySoldier(board, level);
            Assert.AreEqual(turns + 1, level.Game.TurnCount, "休整只结束一次行动");
            Assert.AreEqual(health, level.Player.Health);
            Assert.AreEqual(3, level.Soldier.LeftHand.Value);
            Assert.AreEqual(7, level.Soldier.RightHand.Value);
            Assert.IsFalse(board.CanSkipTurn, "关卡玩家不因此获得任意跳过");
        }

        [Test]
        public void Skipping_completed_tutorial_omits_opening_and_new_level_resets()
        {
            var level = Setup(out var board);
            board.BeginTurn();
            level.SkipTutorial();
            Assert.IsTrue(level.Started);
            Assert.IsTrue(level.IsFreeStage);
            Assert.AreEqual(7, level.StepNumber);
            PlayGuided(board, level, level.Player.LeftHand, level.Soldier.LeftHand);
            PlaySoldier(board, level);
            Assert.AreEqual(20, level.Player.Health, "跳过后不回放士兵首次4伤");
            var fresh = Setup(out var nextBoard);
            nextBoard.BeginTurn();
            Assert.IsFalse(fresh.Started);
            fresh.BeginTutorial();
            Assert.AreEqual(1, fresh.StepNumber);
            Assert.AreEqual(12, fresh.Soldier.Health);
            Assert.AreEqual(1, fresh.Player.LeftHand.Value);
            Assert.AreEqual(3, fresh.Soldier.LeftHand.Value);
        }

        [Test]
        public void Ordinary_match_has_no_scripted_rest_permission()
        {
            var choice = new SimpleChoiceProvider();
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Champion, CharacterDefinition.Blank, choice, choice);
            var board = new BoardController(game, choice, choice);
            board.BeginTurn();
            Assert.IsFalse(board.CanSkipTurn);
            Assert.IsFalse(board.SkipTurn());
            Assert.AreEqual(0, game.TurnCount);
        }

        [Test]
        public void Level_metadata_and_texts_are_ready()
        {
            Assert.AreEqual("初战 · 边境哨站", AdventureLevel1.Title);
            Assert.AreEqual("Adventure/guide_soldier", AdventureLevel1.GuideAvatarKey);
            Assert.AreEqual("老兵", AdventureLevel1.GuideName);
            Assert.IsTrue(AdventureLevel1.IntroText.Contains("边境哨站"));
            Assert.IsTrue(AdventureLevel1.VictoryText.Contains("十点重击"));
            Assert.IsTrue(AdventureLevel1.VictoryText.Contains("满十归一"));
        }
    }
}
