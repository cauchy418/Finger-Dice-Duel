using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **赌徒**的核心：1d9 的结果直接作为本次行动结果。
    ///
    /// 规则（玩家逐条确认）：
    ///   4/6/8 攻击、9 回血、5 合盾，其余无效果。
    ///   打谁在结果出来之后再选。
    /// </summary>
    [TestFixture]
    public class GamblerTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match(CharacterDefinition defB,
                                         out SimpleChoiceProvider sinkA,
                                         out SimpleChoiceProvider sinkB)
        {
            sinkA = new SimpleChoiceProvider();
            sinkB = new SimpleChoiceProvider();

            return new GameManager(Make("赌", CharacterDefinition.Gambler),
                                   Make("B", defB), sinkA, sinkB);
        }

        /// <summary>固定点数的随机源（让"赌出 N"可确定性复现）。</summary>
        private sealed class FixedRandom : System.Random
        {
            private readonly int _value;
            public FixedRandom(int value) { _value = value; }

            protected override double Sample() => 0.5;

            public override int Next(int minValue, int maxValue) => _value;
        }

        [Test]
        public void Gambler_stats_and_range()
        {
            PlayerState g = Make("赌", CharacterDefinition.Gambler);

            Assert.AreEqual(0, g.Character.AttackBonus);
            Assert.AreEqual(0, g.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(g), "赌徒攻击距离 1。");
        }

        [Test]
        public void Gamble_four_six_eight_produce_attacks()
        {
            foreach (int roll in new[] { 4, 6, 8 })
            {
                GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
                PlayerState gambler = game.PlayerA;
                PlayerState foe = game.PlayerB;

                int before = foe.Health;

                TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                                 new FixedRandom(roll), foe.LeftHand);

                Assert.True(r.WasGamble, $"{roll} 应记为赌博。");
                Assert.AreEqual(roll, r.GambleRoll, "应记录骰子点数。");
                Assert.AreEqual(roll, r.ResultValue, "骰子点数即本次结果。");
                Assert.True(foe.Health < before,
                    $"赌出 {roll} 应造成伤害（当前效果 {r.Effect}）。");
            }
        }

        [Test]
        public void Gamble_five_forms_a_shield()
        {
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;

            TurnResult r = null;
            string crash = null;
            try
            {
                r = game.ExecuteGamble(gambler, gambler.LeftHand, new FixedRandom(5));
            }
            catch (System.NullReferenceException ex)
            {
                crash = ex.StackTrace;
            }

            Assert.True(crash == null, "赌出 5 时抛了空引用：" + crash);
            Assert.NotNull(r);
            Assert.AreEqual(5, r.ResultValue);
            Assert.AreEqual(HandEffectType.Shield, r.Effect, "赌出 5 应是护盾。");
            Assert.AreEqual(ShieldState.Formed, gambler.LeftHand.State,
                "赌出 5 后运算源应成为成型护盾。");
        }

        [Test]
        public void Gamble_nine_heals_instead_of_attacking()
        {
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;
            PlayerState foe = game.PlayerB;

            gambler.TakeDamage(8);
            int gamblerBefore = gambler.Health;
            int foeBefore = foe.Health;

            TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                             new FixedRandom(9), foe.LeftHand);

            Assert.AreEqual(9, r.ResultValue);
            Assert.AreEqual(HandEffectType.Heal, r.Effect, "赌出 9 应是回复。");
            Assert.True(gambler.Health > gamblerBefore, "赌出 9 应回血。");
            Assert.AreEqual(foeBefore, foe.Health, "回复不该伤到对手。");
        }

        [Test]
        public void Gamble_one_two_three_seven_have_no_effect()
        {
            // 玩家只说了 4/6/8 攻击、9 回血、5 合盾 ——
            // 其余数字按既有规则就是"无效果"，所以不该造成任何伤害。
            foreach (int roll in new[] { 1, 2, 3, 7 })
            {
                GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
                PlayerState gambler = game.PlayerA;
                PlayerState foe = game.PlayerB;

                int foeBefore = foe.Health;
                int selfBefore = gambler.Health;

                TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                                 new FixedRandom(roll), foe.LeftHand);

                Assert.AreEqual(roll, r.ResultValue);
                Assert.AreEqual(HandEffectType.None, r.Effect,
                    $"赌出 {roll} 应为无效果（实际 {r.Effect}）。");
                Assert.AreEqual(foeBefore, foe.Health, $"赌出 {roll} 不该造成伤害。");
                Assert.AreEqual(selfBefore, gambler.Health, $"赌出 {roll} 不该回血。");
            }
        }

        [Test]
        public void Gamble_effect_rules_match_normal_operations()
        {
            // "赌出的数字"与"运算出的数字"必须走**同一套**效果规则，
            // 否则两处会漂移（这条测试是那个约定的守卫）。
            for (int value = 1; value <= 9; value++)
            {
                Assert.AreEqual(Hand.GetEffectType(value), GamblerLogic.EffectOf(value),
                    $"{value} 的效果应与普通运算一致。");
            }
        }

        [Test]
        public void Voluntary_gamble_lets_the_player_choose_a_normal_operation()
        {
            // ⚠️ **非强制**时赌徒有两条路（玩家确认："当然也可以继续赌"）：
            //      · 点他人的手 -> 正常运算
            //      · 点「开赌」   -> 赌博
            //
            //    我第一版**无条件**进赌博阶段，于是"已经赌过、本可以正常运算"
            //    的回合看起来也像被强制（玩家实测："上一回合已经赌过了
            //    但仍被强制赌博"）。那是设计缺口，不是状态 bug。
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.BeginTurn();

            Assert.False(game.PlayerA.MustGamble, "前置：非强制状态。");

            // 点自己的手 -> 走正常运算流程（不再被直接推进赌博）
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, board.Phase,
                "非强制时点自己的手应进入正常运算流程。");
            Assert.True(board.CanClick(game.PlayerB.LeftHand),
                "非强制时应能点他人的手做正常运算。");
        }

        [Test]
        public void Forced_gamble_goes_straight_to_the_gamble_phase()
        {
            // **强制**时：点自己的手 -> 直接进赌博阶段（不给别的选择）
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.GambleRng = new FixedRandom(3);
            board.BeginTurn();

            game.PlayerA.MustGamble = true;

            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingGamble, board.Phase,
                "强制赌博时点自己的手应直接进赌博阶段。");

            Assert.True(board.CanGamble, "此时应能开赌。");
            Assert.False(board.CanClick(game.PlayerB.LeftHand),
                "赌博阶段不该能点别人的手。");
        }

        [Test]
        public void Forced_gamble_rejects_clicking_someone_elses_hand()
        {
            // 玩家要求："强制赌博时点他人的手 -> 弹提示并拒绝"
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.GambleRng = new FixedRandom(5);
            board.BeginTurn();

            game.PlayerA.MustGamble = true;

            // 点别人的手 -> 被拒，且给出明确提示（不能静默无反应）
            Assert.False(board.ClickHand(game.PlayerB.LeftHand),
                "强制赌博时点他人的手应被拒绝。");
            Assert.True(board.Hint != null && board.Hint.Contains("必须赌博"),
                $"应弹提示说明原因，实际提示：{board.Hint}");

            // 点自己的手仍然可以
            Assert.True(board.ClickHand(game.PlayerA.LeftHand),
                "强制赌博时点自己的手仍然可以。");
        }

        [Test]
        public void Gamble_roll_is_cleared_at_the_start_of_each_turn()
        {
            // 点数跨回合残留会让下一回合沿用上一次的点数
            //（诊断字段跨回合残留我踩过多次，这里提前钉住）
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.GambleRng = new FixedRandom(3);
            board.BeginTurn();

            board.ClickHand(game.PlayerA.LeftHand);

            // 掷点之后、结算之前读"待结算点数"
            board.GambleRng = new FixedRandom(3);
            Assert.True(board.CanGamble);
            board.Gamble();

            Assert.AreEqual(3, board.LastGambleRoll, "应记录掷出的点数。");

            board.BeginTurn();
            Assert.AreEqual(0, board.PendingGambleRoll,
                "新回合开始时待结算点数应清零。");
        }

        [Test]
        public void Normal_operation_makes_the_next_turn_a_forced_gamble()
        {
            // 玩家确认的规则："若赌徒上一个自己的回合进行了正常的运算（非赌博），
            // 那么这一回合就必须赌博"；"赌完就解除"。
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.BeginTurn();

            // ⚠️ 赌徒点自己的手**总是**进赌博阶段（那是他的正常路径），
            //    所以想"做一次正常运算"要临时放行 ——
            //    这也符合实机：新开一局时 MustGamble 就是 false。
            board.ForceAllowNormalOperationForTest(true);

            // 一次**正常运算**（不是赌博）：1+2=3 -> 无效果，不需要确认目标玩家
            TestSetup.SetHand(game.PlayerA.LeftHand, 1);
            TestSetup.SetHand(game.PlayerB.LeftHand, 2);
            board.ClickHand(game.PlayerA.LeftHand);
            board.ClickHand(game.PlayerB.LeftHand);

            Assert.NotNull(board.LastTurn, "应完成一次结算。");
            Assert.False(board.LastTurn.WasGamble, "这次应是正常运算，不是赌博。");
            Assert.True(game.PlayerA.MustGamble,
                "做了正常运算后，赌徒应进入强制赌博状态。");

            // ⚠️ 一次结算后行动方**已经换成对手**了（回合一交替），
            //    所以要让对手也走一步，回合才会回到赌徒身上。
            board.ForceAllowNormalOperationForTest(false);
            board.BeginTurn();
            Assert.AreEqual(game.PlayerB, board.ActivePlayer,
                "前置：上一步结算后轮到对手。");

            // 对手随便走一步（用引擎直调，避免依赖对手的交互细节）
            game.ExecuteTurn(game.PlayerB, game.PlayerB.LeftHand, game.PlayerA.LeftHand);

            // 回合回到赌徒 -> 他必须赌博
            board.BeginTurn();
            Assert.AreEqual(game.PlayerA, board.ActivePlayer, "前置：应轮回赌徒。");
            Assert.True(game.PlayerA.MustGamble, "此时仍应是强制赌博状态。");

            board.GambleRng = new FixedRandom(3);
            board.ClickHand(game.PlayerA.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingGamble, board.Phase,
                "强制赌博状态下点自己的手应进入赌博阶段。");
            Assert.True(board.Gamble(), "应能开赌。");

            Assert.False(game.PlayerA.MustGamble,
                "赌完就解除，下一回合可以正常运算。");
        }

        [Test]
        public void Gamble_attack_waits_for_a_target_then_resolves()
        {
            // 玩家实测："赌完之后卡住"（掷出 8 = 攻击）。
            //
            // 根因：赌博路径**从没设过 `_selectedTarget`**（赌博没有被加数），
            // 而 ClickHand 的选目标分支走 ConfirmTargetPlayer()，
            // 那个方法要求 `_selectedTarget` 非 null —— 于是静默返回 false，
            // ClickHand 却返回 true，表现成"点了目标没反应"。
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.GambleRng = new FixedRandom(8);      // 8 = 攻击
            board.BeginTurn();

            board.ClickHand(game.PlayerA.LeftHand);
            Assert.True(board.Gamble(), "开赌应成功。");
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "掷出攻击应停在选打谁。");

            int before = game.PlayerB.Health;

            // 点对手的手 -> 应真的打出去
            Assert.True(board.ClickHand(game.PlayerB.LeftHand),
                "点目标应被接受。");

            Assert.NotNull(board.LastTurn, "点完目标应完成结算（不能卡住）。");
            Assert.True(board.LastTurn.WasGamble, "这次结算应记为赌博。");
            Assert.AreEqual(8, board.LastTurn.GambleRoll);
            Assert.True(game.PlayerB.Health < before,
                "赌出 8 应造成伤害。");
        }

        /// <summary>
        /// 每次都返回**不同**点数的随机源。
        ///
        /// 用它才能抓到"结算时又掷了一次"—— 固定点数的话两次掷出相同值，
        /// 根本看不出来（我第一版用的就是固定源，所以没抓到）。
        /// </summary>
        private sealed class CountingRandom : System.Random
        {
            private int _call;
            public int Calls => _call;

            protected override double Sample() => 0.5;

            /// <summary>第一次返回 4（攻击），之后每次递增且与 4 不同。</summary>
            public override int Next(int minValue, int maxValue)
            {
                _call++;
                return _call == 1 ? 4 : 1 + (_call % 3);   // 2/3/1 循环，永不为 4
            }
        }

        [Test]
        public void Gamble_roll_happens_once_not_again_at_resolution()
        {
            // 玩家实测："先说投出了 4，点战士时又说投出 2 无效果。
            //            疑似点攻击时赌徒又重新赌了一次。"
            //
            // 根因：掷出攻击（4/6/8）时流程是"开赌掷点 -> 选打谁 -> 结算"，
            // 掷点与结算**分属两次调用**；结算那次又调了 ExecuteGamble，
            // 于是重掷。修法是结算走 ExecuteGambleWithRoll（用已存的点数）。
            //
            // 这条测试用**每次都返回不同点数**的随机源，只要重掷就会立刻暴露。
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            var rng = new CountingRandom();
            board.GambleRng = rng;

            // 先调到"开赌必掷出 4"（4 = 攻击，会停下来选目标）
            board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand);
            board.Gamble();

            int firstRoll = board.LastGambleRoll;
            int callsAfterGamble = rng.Calls;

            Assert.AreEqual(4, firstRoll, "前置：首次应掷出 4（攻击）。");
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);

            // 点目标 -> 结算
            Assert.True(board.ClickHand(game.PlayerB.LeftHand), "点目标应被接受。");

            Assert.AreEqual(firstRoll, board.LastTurn?.GambleRoll,
                "结算用的点数必须与开赌时掷出的**同一次**（不能重掷）。");
            Assert.AreEqual(callsAfterGamble, rng.Calls,
                "结算不该再调用随机源（调用次数不该增加）。");
        }

        [Test]
        public void Battle_log_says_the_gambler_rolled_rather_than_computed()
        {
            // 玩家要求战报能看出这是赌博 —— 不能写成 "N + M = K"，
            // 那会让玩家以为自己做了一次运算（赌博时根本没点他人的手）。
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;

            TurnResult r = game.ExecuteGamble(gambler, gambler.LeftHand,
                                             new FixedRandom(8), game.PlayerB.LeftHand);

            string line = BoardController.DescribeTurn(r);

            Assert.True(line.Contains("开赌"),
                $"战报应写明是开赌。实际：{line}");
            Assert.True(line.Contains("8"),
                $"战报应写明掷出的点数。实际：{line}");
            Assert.False(line.Contains("+"),
                $"赌博不该写成加法运算。实际：{line}");
        }

        [Test]
        public void Ai_gambler_never_gets_stuck_on_the_gamble_phase()
        {
            // ⚠️ AI 不会点「开赌」按钮，所以 MakeAutomaticMove 必须代它点 ——
            //    否则会**卡死在 ChoosingGamble**。
            //
            //    这条测试对 1..9 **每一种点数**各跑一次，
            //    因为不同点数走向不同阶段（攻击要再选目标、其余直接结算），
            //    只测一个点数盖不住。
            for (int roll = 1; roll <= 9; roll++)
            {
                var ai = new GreedyChoiceProvider();
                var providers = new List<SimpleChoiceProvider>
                {
                    new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                };
                var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                           Make("B", CharacterDefinition.Soldier),
                                           providers[0], providers[1]);

                var board = new BoardController(game, providers[0], providers[1],
                                               aiForA: ai);
                board.GambleRng = new FixedRandom(roll);
                board.BeginTurn();

                // ⚠️ 置成**强制**状态，AI 才会走赌博流程 ——
                //    非强制时赌徒可以自选正常运算（那是玩家的选择，
                //    AI 目前按"走正常运算"处理，所以不会掷点）。
                game.PlayerA.MustGamble = true;

                Assert.True(board.MakeAutomaticMove(),
                    $"掷出 {roll} 时 AI 应能走完这一步。");
                Assert.True(board.Phase != BoardPhase.ChoosingGamble,
                    $"掷出 {roll} 后不该停在等开赌（否则对局卡死）。");
                Assert.AreEqual(roll, board.LastGambleRoll,
                    "应记录掷出的点数。");
            }
        }

        [Test]
        public void Forced_gamble_still_allows_picking_an_attack_target()
        {
            // 玩家实测："我在上一回合运算后，这一回合开赌投出 6 后
            //            无法点击他人攻击，攻击选择和运算选择他人一起被锁死。"
            //
            // 根因：那条"强制赌博时拒绝点他人的手"的守卫**没有限定阶段**，
            // 于是掷出攻击后的"选打谁"（ChoosingTargetPlayer）也被拦了 ——
            // 而选打谁本来就**必须**点他人的手。
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("赌", CharacterDefinition.Gambler),
                                       Make("B", CharacterDefinition.Soldier),
                                       providers[0], providers[1]);

            var board = new BoardController(game, providers[0], providers[1]);
            board.GambleRng = new FixedRandom(6);      // 6 = 攻击
            board.BeginTurn();

            game.PlayerA.MustGamble = true;            // 强制状态

            // ① 点自己的手 -> 进赌博阶段
            Assert.True(board.ClickHand(game.PlayerA.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingGamble, board.Phase);

            // ② 开赌 -> 掷出 6 -> 停在选打谁
            Assert.True(board.Gamble());
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);

            // ③ 强制赌博状态下，**选打谁仍然必须能点他人的手**
            Assert.True(board.CanClick(game.PlayerB.LeftHand),
                "选攻击目标时点他人的手必须被允许（哪怕处于强制赌博状态）。");

            int before = game.PlayerB.Health;
            Assert.True(board.ClickHand(game.PlayerB.LeftHand),
                "点目标应被接受 —— 不能被强制赌博的守卫拦掉。");

            Assert.NotNull(board.LastTurn, "应完成结算。");
            Assert.AreEqual(6, board.LastTurn.GambleRoll);
            Assert.True(game.PlayerB.Health < before, "赌出 6 应造成伤害。");
        }

        [Test]
        public void Only_gamblers_can_gamble()
        {
            GameManager game = new GameManager(Make("士", CharacterDefinition.Soldier),
                                               Make("B", CharacterDefinition.Soldier),
                                               new SimpleChoiceProvider(),
                                               new SimpleChoiceProvider());

            bool threw = false;
            try
            {
                game.ExecuteGamble(game.PlayerA, game.PlayerA.LeftHand, new FixedRandom(5));
            }
            catch (System.InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw, "非赌徒不该能赌博。");
        }

        [Test]
        public void Must_gamble_flag_resets_on_a_new_match()
        {
            // 强制赌博是跨回合状态，但新开一局必须清零
            GameManager game = Match(CharacterDefinition.Soldier, out _, out _);
            PlayerState gambler = game.PlayerA;

            gambler.MustGamble = true;
            game.ResetMatch();

            Assert.False(gambler.MustGamble, "新开一局应清掉强制赌博状态。");
        }
    }
}
