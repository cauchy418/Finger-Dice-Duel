using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 整局可跑通：MatchRunner + 随机/贪心 AI 自动完成对局。
    /// 这是 P0 的核心验收项 —— 证明游戏逻辑已经"可玩"，而不只是"能编译"。
    /// </summary>
    [TestFixture]
    public class MatchTests
    {
        private static GameManager RandomMatch(int seed)
        {
            return CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank,
                new RandomChoiceProvider(seed), new RandomChoiceProvider(seed + 1));
        }

        private static MatchResult RunRandom(GameManager game, MatchRunner runner)
            => runner.Run(game,
                (RandomChoiceProvider)game.ChoiceProviderA,
                (RandomChoiceProvider)game.ChoiceProviderB);

        [Test]
        public void Random_ai_finishes_a_match_with_a_winner()
        {
            var game = RandomMatch(12345);
            var runner = new MatchRunner { MaxTurns = 500 };

            MatchResult result = RunRandom(game, runner);

            Assert.True(game.IsGameOver, "对局应在回合上限内结束。");
            Assert.False(result.HitTurnLimit, "不应卡在回合上限。");
            Assert.NotNull(result.Winner, "应分出胜负。");
            Assert.True(result.Turns > 0, "至少应进行 1 回合。");
            Assert.True(result.Winner.Health > 0, "赢家应还活着。");

            PlayerState loser = ReferenceEquals(result.Winner, game.PlayerA)
                ? game.PlayerB : game.PlayerA;
            Assert.True(loser.IsDead, "输家应已阵亡。");
        }

        [Test]
        public void Every_resolved_turn_respects_the_turn_contract()
        {
            var game = RandomMatch(2024);
            var runner = new MatchRunner { MaxTurns = 120 };
            MatchResult result = RunRandom(game, runner);

            Assert.True(result.History.Count > 0, "应记录到回合明细。");

            PlayerState expectedActor = game.PlayerA;
            foreach (TurnResult t in result.History)
            {
                Assert.NotNull(t.Actor);
                Assert.NotNull(t.ActedHand);
                Assert.NotNull(t.TargetHand);

                // 1. 出手顺序应为 A、B、A、B……
                Assert.AreSame(expectedActor, t.Actor, "回合顺序应为 A/B 交替。");

                // 2. 运算源必须属于行动方，目标是对方的手
                Assert.True(t.Actor.Owns(t.ActedHand), "运算源必须属于行动方。");
                Assert.True(game.GetOpponent(t.Actor).Owns(t.TargetHand), "目标必须是对方的手。");

                // 3. 行动方的手被写回为结果值
                //    注意：必须用 TurnResult 里的快照值，不能读 t.ActedHand.Value ——
                //    同一只手会在后续回合继续变化，引用反映的是最终状态。
                Assert.AreEqual(t.ResultValue, t.ActedHandValueAfter, "行动方的手应写回结果。");
                Assert.AreEqual(Hand.ComputeSum(t.ActedHandValueBefore, t.TargetHandValue),
                    t.ResultValue, "结果应等于运算前的值 + 目标手的值（归一化）。");

                // 4. 效果类型必须由结果值决定
                Assert.AreEqual(Hand.GetEffectType(t.ResultValue), t.Effect,
                    "效果类型应由结果值决定。");

                // 5. 被锁定的手不会被选为运算源
                Assert.False(t.ActedHand.IsLocked, "被锁定的手不应作为运算源。");

                expectedActor = game.GetOpponent(expectedActor);
            }
        }

        [Test]
        public void Match_runner_enforces_the_turn_limit()
        {
            var game = RandomMatch(7);
            var runner = new MatchRunner { MaxTurns = 3, RecordHistory = true };

            MatchResult result = RunRandom(game, runner);

            Assert.AreEqual(3, result.Turns);
            Assert.AreEqual(3, result.History.Count, "RecordHistory 应记录每回合。");
            Assert.True(result.HitTurnLimit, "3 回合不足以分出胜负时应标记命中上限。");
        }

        [Test]
        public void History_can_be_disabled()
        {
            var game = RandomMatch(11);
            var runner = new MatchRunner { MaxTurns = 5, RecordHistory = false };

            MatchResult result = RunRandom(game, runner);

            Assert.AreEqual(0, result.History.Count);
            Assert.True(result.Turns > 0);
        }

        [Test]
        public void OnTurnResolved_callback_fires_once_per_turn()
        {
            var game = RandomMatch(31);
            var runner = new MatchRunner { MaxTurns = 10 };
            int callbacks = 0;
            runner.OnTurnResolved = _ => callbacks++;

            MatchResult result = RunRandom(game, runner);

            Assert.AreEqual(result.Turns, callbacks, "回调次数应等于回合数。");
        }

        [Test]
        public void Greedy_ai_finishes_a_match()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank,
                new GreedyChoiceProvider(), new GreedyChoiceProvider());

            var runner = new MatchRunner { MaxTurns = 400 };
            MatchResult result = runner.Run(game,
                (GreedyChoiceProvider)game.ChoiceProviderA,
                (GreedyChoiceProvider)game.ChoiceProviderB);

            Assert.True(result.Turns > 0);
            Assert.True(game.IsGameOver, "贪心 AI 对局应在 400 回合内结束。");
            Assert.NotNull(result.Winner);
        }

        [Test]
        public void Greedy_move_is_legal()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank);
            var greedy = new GreedyChoiceProvider();

            LegalMove move = greedy.ChooseMove(game, game.PlayerA);

            Assert.NotNull(move.Actor);
            Assert.AreSame(game.PlayerA, move.Actor);
            Assert.True(game.IsLegalMove(move.Actor, move.ActedHand, move.TargetHand, out string reason),
                "贪心 AI 选的招必须合法：" + reason);
        }

        [Test]
        public void Move_selector_returns_no_move_after_game_over()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank);
            game.PlayerB.TakeDamage(20);
            game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);

            Assert.True(game.IsGameOver);
            Assert.AreEqual(0, game.GetLegalMoves(game.PlayerA).Count);
            Assert.Null(new GreedyChoiceProvider().ChooseMove(game, game.PlayerA).Actor);
            Assert.Null(new RandomChoiceProvider(1).ChooseMove(game, game.PlayerA).Actor);
        }

        [Test]
        public void Heal_at_full_health_wastes_the_turn()
        {
            // 已确认的规则（RULES.md 第 13 节）：满血时用 9 无事发生，照样消耗掉这个回合。
            // 不要"顺手修好"它 —— 这是一条明确的规则决定。
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank,
                new ScriptedChoiceProvider(), new ScriptedChoiceProvider());

            Assert.AreEqual(20, game.PlayerA.Health, "前置条件：A 应满血。");

            TestSetup.SetHand(game.PlayerA.LeftHand, 8);        // 8 + 1 = 9 -> Heal
            TurnResult r = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);

            Assert.AreEqual(HandEffectType.Heal, r.Effect, "8 + 1 = 9");
            Assert.AreEqual(0, r.Healed, "满血时回复量应为 0。");
            Assert.AreEqual(20, game.PlayerA.Health, "生命不应超过上限。");
            Assert.AreEqual(1, game.TurnCount, "回合照样被消耗。");
            Assert.AreSame(game.PlayerB, game.CurrentPlayer, "回合应交给对方。");
        }

        [Test]
        public void Heal_below_full_health_restores_nine()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank,
                new ScriptedChoiceProvider(), new ScriptedChoiceProvider());

            game.PlayerA.TakeDamage(10);                        // 20 -> 10
            Assert.AreEqual(10, game.PlayerA.Health);

            TestSetup.SetHand(game.PlayerA.LeftHand, 8);        // 8 + 1 = 9 -> Heal
            TurnResult r = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);

            Assert.AreEqual(HandEffectType.Heal, r.Effect);
            Assert.AreEqual(9, r.Healed, "应回复 9 点。");
            Assert.AreEqual(19, game.PlayerA.Health);
        }

        [Test]
        public void Match_runner_rejects_null_arguments()
        {
            var game = RandomMatch(1);
            var runner = new MatchRunner();

            Assert.Throws<System.ArgumentNullException>(
                () => runner.Run(null, new GreedyChoiceProvider(), new GreedyChoiceProvider()));
            Assert.Throws<System.ArgumentNullException>(
                () => runner.Run(game, null, new GreedyChoiceProvider()));
            Assert.Throws<System.ArgumentNullException>(
                () => runner.Run(game, new GreedyChoiceProvider(), null));
        }

        [Test]
        public void Random_ai_blocks_with_one_of_its_own_shields()
        {
            var provider = new RandomChoiceProvider(99);
            var a = new PlayerState("A");

            for (int i = 0; i < 50; i++)
            {
                Hand picked = provider.AskWhichShieldToBlock(a, a.LeftHand, a.RightHand, null);
                Assert.True(ReferenceEquals(picked, a.LeftHand) || ReferenceEquals(picked, a.RightHand),
                    "随机 AI 必须返回自己的一面盾。");
            }
        }

        [Test]
        public void Random_ai_can_be_seeded_for_reproducible_runs()
        {
            var runner1 = new MatchRunner { MaxTurns = 500 };
            var runner2 = new MatchRunner { MaxTurns = 500 };

            MatchResult r1 = RunRandom(RandomMatch(4242), runner1);
            MatchResult r2 = RunRandom(RandomMatch(4242), runner2);

            Assert.AreEqual(r1.Turns, r2.Turns, "相同种子应得到相同的回合数。");
            // 两局的 PlayerState 是不同实例，所以比较"谁赢了"而不是实例本身
            Assert.AreEqual(r1.WinnerIsPlayerA, r2.WinnerIsPlayerA, "相同种子应得到相同的胜者。");
            Assert.AreEqual(r1.WinnerIsPlayerB, r2.WinnerIsPlayerB, "相同种子应得到相同的胜者。");
            Assert.AreEqual(r1.IsDraw, r2.IsDraw, "相同种子应得到相同的结果。");
        }

        [Test]
        public void Summary_reports_win_counts()
        {
            var runner = new MatchRunner { MaxTurns = 500, RecordHistory = false };
            var results = new List<MatchResult>();

            for (int i = 0; i < 6; i++)
                results.Add(RunRandom(RandomMatch(1000 + i), runner));

            string text = MatchRunner.Summarize(results);
            Assert.NotNull(text);
            Assert.True(text.Length > 0);
            Assert.True(text.Contains("对局数 6"), "汇总应包含对局数。");
        }

        [Test]
        public void Scripted_move_selector_reproduces_a_fixed_opening()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank,
                new ScriptedChoiceProvider(), new ScriptedChoiceProvider());

            var a = new ScriptedMoveSelector().Add(0, 0);   // 左手打对方左手
            var b = new ScriptedMoveSelector().Add(1, 1);   // 右手打对方右手

            var runner = new MatchRunner { MaxTurns = 2 };
            MatchResult result = runner.Run(game, a, b);

            Assert.AreEqual(2, result.Turns);
            Assert.AreEqual(2, game.PlayerA.LeftHand.Value, "A 左手: 1+1 = 2");
            Assert.AreEqual(2, game.PlayerB.RightHand.Value, "B 右手: 1+1 = 2");
            Assert.AreEqual(1, game.PlayerA.RightHand.Value, "未参与的手不应改变。");
            Assert.AreEqual(1, game.PlayerB.LeftHand.Value, "未参与的手不应改变。");
        }

        [Test]
        public void Full_character_matrix_can_all_be_played_to_completion()
        {
            // 4 个内置角色两两对局（含内斗），确认没有任何组合会崩溃或卡死。
            // 注意：回合数有上限（GameManager.DefaultTurnLimit），打满上限会**按平局**结束，
            // 所以这里只断言"对局一定在有限回合内结束"，不再要求必须有胜者。
            foreach (CharacterDefinition a in CharacterDefinition.BuiltIn)
            {
                foreach (CharacterDefinition b in CharacterDefinition.BuiltIn)
                {
                    var game = CharacterFactory.CreateMatch(
                        a, b, new GreedyChoiceProvider(), new GreedyChoiceProvider());
                    var runner = new MatchRunner { MaxTurns = 600, RecordHistory = false };

                    MatchResult result = runner.Run(game,
                        (GreedyChoiceProvider)game.ChoiceProviderA,
                        (GreedyChoiceProvider)game.ChoiceProviderB);

                    Assert.True(game.IsGameOver,
                        $"{a.Id} vs {b.Id} 应在回合上限内结束。");
                    // 上限按**轮**算（统一 50 轮）；1v1 里一轮 = 2 个回合。
                    Assert.True(result.Turns <= GameManager.DefaultRoundLimit * 2,
                        $"{a.Id} vs {b.Id} 回合数 {result.Turns} 不应超过"
                        + $" {GameManager.DefaultRoundLimit} 轮（= {GameManager.DefaultRoundLimit * 2} 回合）。");
                }
            }
        }

        [Test]
        public void Match_ends_as_a_draw_at_the_turn_limit()
        {
            // 打满回合上限仍未分胜负 -> 平局（Winner == null）。
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank);
            var runner = new MatchRunner { MaxTurns = 1000, RecordHistory = false };
            var selector = new HealOnlySelector();

            MatchResult result = runner.Run(game, selector, selector);

            Assert.True(game.IsGameOver, "打满上限后对局应结束。");
            // ⚠️ 玩家要求把上限按**轮**算（统一 50 轮），而 `Turns` 记的是**回合**。
            //    1v1 里一轮 = 2 个回合，所以恰好打满 = 100 个回合。
            Assert.AreEqual(GameManager.DefaultRoundLimit * 2, result.Turns,
                "1v1 打满 50 轮 = 100 个回合。");
            Assert.Null(result.Winner, "打满上限应按平局记录（无胜者）。");
            Assert.Null(game.Winner);
            Assert.False(result.HitTurnLimit,
                "这是规则内的平局，不是'模拟器被迫中断'。");
        }

        /// <summary>只求活命的策略：不够血就回复。用来制造"打不死"的极端对局。</summary>
        private sealed class HealOnlySelector : Assistant.IMoveSelector
        {
            public string Describe => "heal-only";

            public LegalMove ChooseMove(GameManager game, PlayerState actor)
            {
                IReadOnlyList<LegalMove> moves = game.GetLegalMoves(actor);
                if (moves.Count == 0) return default;

                LegalMove best = moves[0];
                int bestScore = int.MinValue;

                foreach (LegalMove m in moves)
                {
                    int r = Hand.ComputeSum(m.ActedHand.Value, m.TargetHand.Value);
                    int score;
                    switch (Hand.GetEffectType(r))
                    {
                        case HandEffectType.Heal:
                            score = actor.Health >= actor.MaximumHealth ? 0 : 1000 - actor.Health;
                            break;
                        case HandEffectType.Shield: score = 500; break;
                        case HandEffectType.Attack8: score = 300; break;
                        default: score = 100; break;
                    }

                    if (score > bestScore) { bestScore = score; best = m; }
                }

                return best;
            }
        }
    }
}
