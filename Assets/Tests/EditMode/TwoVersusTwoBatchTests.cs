using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 批量对局验证**：四个 AI 自动打完整局。
    ///
    /// 目的不是"验证 AI 强不强"，而是**找出卡死**：
    /// 玩家实测报告"经典里战士攻击卡住了"，所以这里把大量角色组合
    /// 与随机种子跑一遍，只要有一局没在步数上限内结束就报出来，
    /// 并把**卡在哪一步**（阶段 / 行动方 / 候选）一起打出来便于定位。
    ///
    /// 覆盖点：
    ///   · 四人都是 AI 时能自动走完（不等待人类）
    ///   · 回合按座位轮转，不会某个座位永远轮不到
    ///   · 一定在步数上限内结束（不会无限循环）
    ///   · 结束后不再推进
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoBatchTests
    {
        private static readonly CharacterDefinition[] Roster =
        {
            CharacterDefinition.Blank,
            CharacterDefinition.Warrior,
            CharacterDefinition.Barbarian,
            CharacterDefinition.Assassin,
            CharacterDefinition.Swordsman,
            CharacterDefinition.Ninja,
            CharacterDefinition.Werewolf,
            CharacterDefinition.Ranger,
            CharacterDefinition.Curseweaver,
            CharacterDefinition.Priest,
        };

        /// <summary>一局 2v2 的驱动结果（用于失败时报告现场）。</summary>
        private sealed class BatchResult
        {
            public bool Finished;
            public int Steps;
            public int TurnCount;
            public string LastPhase;
            public string LastActor;
            public string LastHint;
            public string Seated;
            public bool WinnerKnown;
        }

        /// <summary>
        /// 造一局 2v2（四人都是 AI）并自动打完。
        ///
        /// 座位顺序按 [0,1,2,3] 直接建；队伍 0/1 一队、2/3 一队。
        /// </summary>
        private static BatchResult PlayOne(CharacterDefinition d0, CharacterDefinition d1,
                                           CharacterDefinition d2, CharacterDefinition d3,
                                           int safety, out GameManager gameOut)
        {
            var seated = new List<PlayerState>(4)
            {
                CharacterFactory.CreatePlayer("P1", d0),
                CharacterFactory.CreatePlayer("P2", d1),
                CharacterFactory.CreatePlayer("P3", d2),
                CharacterFactory.CreatePlayer("P4", d3),
            };

            var providers = new List<IChoiceProvider>(4);
            for (int i = 0; i < 4; i++) providers.Add(new SimpleChoiceProvider());

            var game = GameManager.Create2v2(seated, providers);
            gameOut = game;

            // 四人全配 AI（GreedyChoiceProvider 同时是 IMoveSelector 与 IChoiceProvider）
            var board = new BoardController(game,
                providers[0] as IPlayerChoiceSink, providers[1] as IPlayerChoiceSink,
                aiForA: new GreedyChoiceProvider(),
                aiForB: new GreedyChoiceProvider());

            board.BindPlayer(seated[2], new GreedyChoiceProvider(),
                             providers[2] as IPlayerChoiceSink);
            board.BindPlayer(seated[3], new GreedyChoiceProvider(),
                             providers[3] as IPlayerChoiceSink);

            var result = new BatchResult
            {
                Seated = $"{d0.DisplayName}/{d1.DisplayName} vs {d2.DisplayName}/{d3.DisplayName}",
            };

            int steps = 0;
            board.BeginTurn();

            while (board.Phase != BoardPhase.GameOver && steps < safety)
            {
                result.LastPhase = board.Phase.ToString();
                result.LastActor = board.ActivePlayer?.Name;
                result.LastHint = board.Hint;

                if (board.IsHumanChooser)
                {
                    // 四人都是 AI -> 绝不该等人类。等到就是卡死，直接退出报现场。
                    result.Finished = false;
                    result.Steps = steps;
                    return result;
                }

                if (!board.MakeAutomaticMove())
                {
                    // 现场 = MakeAutomaticMove 内部**在失败点当场记录**的诊断。
                    // 只报"卡住了"是定位不到原因的（我为此白查过一轮）。
                    result.LastHint = "AI 出手失败：" + board.LastAiMoveDiagnostic;

                    result.Finished = false;
                    result.Steps = steps;
                    return result;
                }

                steps++;
            }

            result.Steps = steps;
            result.TurnCount = game.TurnCount;
            result.Finished = board.Phase == BoardPhase.GameOver;
            result.WinnerKnown = game.Winner != null || game.IsGameOver;
            return result;
        }

        private static string Describe(BatchResult r)
            => $"座位={r.Seated} 步数={r.Steps} 回合={r.TurnCount} " +
               $"最后阶段={r.LastPhase} 行动方={r.LastActor} 提示={r.LastHint}";

        // ============================================================
        // 单局：全 AI
        // ============================================================

        [Test]
        public void Four_ais_finish_a_two_versus_two_match()
        {
            BatchResult r = PlayOne(CharacterDefinition.Warrior, CharacterDefinition.Priest,
                                    CharacterDefinition.Barbarian, CharacterDefinition.Ninja,
                                    4000, out GameManager game);

            Assert.True(r.Finished, "四个 AI 应能打完 2v2：" + Describe(r));
            Assert.True(game.IsGameOver, "结束后 IsGameOver 应为 true。");
        }

        [Test]
        public void Four_blank_ais_finish_a_two_versus_two_match()
        {
            BatchResult r = PlayOne(CharacterDefinition.Blank, CharacterDefinition.Blank,
                                    CharacterDefinition.Blank, CharacterDefinition.Blank,
                                    4000, out GameManager game);

            Assert.True(r.Finished, "四个白板也应能打完：" + Describe(r));
            Assert.True(game.IsGameOver);
        }

        // ============================================================
        // 战士（玩家报告卡住的角色）专项
        // ============================================================

        [Test]
        public void A_warrior_attack_does_not_stall_in_two_versus_two()
        {
            // 玩家实测："经典里战士攻击卡住了"。
            // 这里把战士放在每个座位各跑一局，确保它出手不会卡。
            for (int seat = 0; seat < 4; seat++)
            {
                var defs = new[]
                {
                    CharacterDefinition.Blank, CharacterDefinition.Blank,
                    CharacterDefinition.Blank, CharacterDefinition.Blank,
                };
                defs[seat] = CharacterDefinition.Warrior;

                BatchResult r = PlayOne(defs[0], defs[1], defs[2], defs[3],
                                        4000, out GameManager game);

                Assert.True(r.Finished,
                    $"战士坐在第 {seat + 1} 位时卡住了：" + Describe(r));
            }
        }

        [Test]
        public void Every_character_can_act_in_two_versus_two()
        {
            // 每个角色分别放在每个座位各跑一局 —— 覆盖"某个角色的技能
            // 在 2v2 里问不出答案而卡住"这类问题
            for (int c = 0; c < Roster.Length; c++)
            {
                for (int seat = 0; seat < 4; seat++)
                {
                    var defs = new[]
                    {
                        CharacterDefinition.Blank, CharacterDefinition.Blank,
                        CharacterDefinition.Blank, CharacterDefinition.Blank,
                    };
                    defs[seat] = Roster[c];

                    BatchResult r = PlayOne(defs[0], defs[1], defs[2], defs[3],
                                            4000, out GameManager _);

                    Assert.True(r.Finished,
                        $"角色 {Roster[c].DisplayName} 在第 {seat + 1} 位时卡住了：" +
                        Describe(r));
                }
            }
        }

        // ============================================================
        // 批量：随机组合
        // ============================================================

        [Test]
        public void Two_hundred_random_two_versus_two_matches_all_finish()
        {
            var rng = new Random(20240607);
            int finished = 0;
            int draws = 0;

            for (int i = 0; i < 200; i++)
            {
                CharacterDefinition d0 = Roster[rng.Next(Roster.Length)];
                CharacterDefinition d1 = Roster[rng.Next(Roster.Length)];
                CharacterDefinition d2 = Roster[rng.Next(Roster.Length)];
                CharacterDefinition d3 = Roster[rng.Next(Roster.Length)];

                BatchResult r = PlayOne(d0, d1, d2, d3, 6000, out GameManager game);

                Assert.True(r.Finished,
                    $"第 {i + 1} 局没打完（{Describe(r)}）。" +
                    "这一步的输出就是卡死现场：阶段 / 行动方 / 提示。");

                if (game.Winner == null) draws++;
                finished++;
            }

            Assert.AreEqual(200, finished);
            // 记录平局数（不设阈值，只是让它出现在输出里便于观察）
            Assert.True(draws >= 0);
        }

        [Test]
        public void Turn_order_visits_all_four_seats()
        {
            // 四人都是 AI 时，回合应按座位轮转 ——
            // 不能出现"某个座位永远轮不到"（那正是 1v1 写法会造成的 bug）
            var seated = new List<PlayerState>(4);
            for (int i = 1; i <= 4; i++)
                seated.Add(CharacterFactory.CreatePlayer("P" + i, CharacterDefinition.Blank));

            var providers = new List<IChoiceProvider>(4);
            for (int i = 0; i < 4; i++) providers.Add(new SimpleChoiceProvider());

            var game = GameManager.Create2v2(seated, providers);
            var board = new BoardController(game,
                providers[0] as IPlayerChoiceSink, providers[1] as IPlayerChoiceSink,
                aiForA: new GreedyChoiceProvider(),
                aiForB: new GreedyChoiceProvider());

            board.BindPlayer(seated[2], new GreedyChoiceProvider(),
                             providers[2] as IPlayerChoiceSink);
            board.BindPlayer(seated[3], new GreedyChoiceProvider(),
                             providers[3] as IPlayerChoiceSink);

            board.BeginTurn();

            var visited = new List<string>();
            int steps = 0;

            while (board.Phase != BoardPhase.GameOver && steps < 4000)
            {
                string actor = board.ActivePlayer?.Name;
                if (actor != null && (visited.Count == 0 || visited[visited.Count - 1] != actor))
                    visited.Add(actor);

                if (!board.MakeAutomaticMove()) break;
                steps++;
            }

            Assert.True(visited.Count >= 4,
                $"回合应至少经过四个座位，实际只看到：{string.Join(" -> ", visited)}");

            // 前四个（不重复的）行动方应恰好是四个人
            var firstFour = new List<string>();
            for (int i = 0; i < visited.Count && firstFour.Count < 4; i++)
                if (!firstFour.Contains(visited[i])) firstFour.Add(visited[i]);

            Assert.AreEqual(4, firstFour.Count,
                $"四个人都应轮到，实际只轮到 {firstFour.Count} 人：" +
                string.Join(" -> ", visited));
        }
    }
}
