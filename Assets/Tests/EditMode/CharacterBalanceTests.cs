using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **全角色循环赛**：每对角色各打若干局（双方交替先手），统计胜率。
    ///
    /// 用途有两个：
    ///   ① 平衡性调试 —— 谁明显过强 / 过弱一眼可见
    ///   ② **发现 bug** —— 任何一局里抛出异常、卡死、或超过回合上限
    ///      都会作为"异常"记下来（比零散的功能测试更容易撞出边角情况）
    ///
    /// 默认**不是**断言型的（胜率本来就是浮动的），
    /// 只在出现"异常"时才失败 —— 那才是真正需要处理的东西。
    /// </summary>
    [TestFixture]
    public class CharacterBalanceTests
    {
        /// <summary>每对角色打多少局（双方各占一半先手）。</summary>
        private const int GamesPerPair = 40;

        private static CharacterDefinition[] Roster()
            => CharacterDefinition.BuiltIn;

        /// <summary>
        /// 跑完整循环赛并输出胜率表。
        ///
        /// 通过环境变量控制规模，方便"快速看一眼"与"认真统计"两种用法。
        /// </summary>
        [Test]
        public void All_characters_round_robin_win_rates()
        {
            CharacterDefinition[] roster = Roster();

            var wins = new Dictionary<string, int>();
            var losses = new Dictionary<string, int>();
            var draws = new Dictionary<string, int>();
            var anomalies = new List<string>();
            int totalGames = 0, totalTurns = 0, limitHits = 0;
            var pairRows = new List<(string aName, string bName, int wins, int total, double rate)>();

            var sb = new StringBuilder();
            sb.AppendLine("=== 全角色循环赛（每对 " + GamesPerPair + " 局）===");

            for (int i = 0; i < roster.Length; i++)
            {
                wins[roster[i].Id] = 0;
                losses[roster[i].Id] = 0;
                draws[roster[i].Id] = 0;
            }

            for (int i = 0; i < roster.Length; i++)
            {
                for (int j = i + 1; j < roster.Length; j++)
                {
                    CharacterDefinition defA = roster[i];
                    CharacterDefinition defB = roster[j];

                    var results = new List<MatchResult>();
                    for (int g = 0; g < GamesPerPair; g++)
                    {
                        // 偶数局 A 先手、奇数局 B 先手（消掉先手偏差）
                        bool aFirst = (g % 2) == 0;

                        MatchResult r = RunOne(defA, defB, aFirst, g,
                                               out string anomaly);
                        if (anomaly != null) anomalies.Add(anomaly);
                        if (r != null) results.Add(r);
                    }

                    // 统计（注意"先手方"与 defA/defB 的对应）
                    int pairWins = 0, pairTotal = 0;
                    for (int k = 0; k < results.Count; k++)
                    {
                        MatchResult r = results[k];
                        bool aFirst = (k % 2) == 0;

                        totalGames++;
                        totalTurns += r.Turns;
                        if (r.HitTurnLimit) limitHits++;

                        CharacterDefinition winner =
                            r.WinnerIsPlayerA ? (aFirst ? defA : defB)
                            : r.WinnerIsPlayerB ? (aFirst ? defB : defA)
                            : null;

                        if (winner == null)
                        {
                            draws[defA.Id]++;
                            draws[defB.Id]++;
                        }
                        else if (winner.Id == defA.Id)
                        {
                            wins[defA.Id]++;
                            losses[defB.Id]++;
                        }
                        else
                        {
                            wins[defB.Id]++;
                            losses[defA.Id]++;
                        }

                        pairTotal++;
                        if (winner != null && winner.Id == defA.Id) pairWins++;
                    }

                    if (pairTotal > 0)
                    {
                        pairRows.Add((defA.DisplayName, defB.DisplayName,
                                      pairWins, pairTotal, pairWins / (double)pairTotal));
                    }
                }
            }

            // ---- 输出胜率表（按胜率排序）----
            var rows = new List<(string name, int w, int l, int d)>();
            foreach (CharacterDefinition def in roster)
            {
                rows.Add((def.DisplayName, wins[def.Id], losses[def.Id], draws[def.Id]));
            }
            rows.Sort((x, y) =>
            {
                double rx = Rate(x.w, x.l, x.d), ry = Rate(y.w, y.l, y.d);
                return ry.CompareTo(rx);
            });

            sb.AppendLine("角色        胜     负     平    胜率   平局率");
            foreach (var row in rows)
            {
                int tot = row.w + row.l + row.d;
                double drawRate = tot == 0 ? 0 : row.d / (double)tot;
                sb.AppendLine($"{row.name,-8} {row.w,5} {row.l,6} {row.d,6}   " +
                              $"{Rate(row.w, row.l, row.d) * 100:F1}%   {drawRate * 100,5:F1}%");
            }

            // 全局健康度：平均回合数 + 达到上限的局数
            // （平局大量出现时，八成是"打不动"而不是"势均力敌"）
            sb.AppendLine();
            sb.AppendLine($"总对局 {totalGames}，平均回合数 " +
                          $"{(totalGames == 0 ? 0 : totalTurns / (double)totalGames):F1}，" +
                          $"达回合上限 {limitHits} 局");

            // ---- 配对层面的极端值（可能是 bug，也可能是克制关系）----
            sb.AppendLine();
            sb.AppendLine("=== 最悬殊的配对（先手方胜率）===");
            pairRows.Sort((x, y) => y.rate.CompareTo(x.rate));
            for (int i = 0; i < pairRows.Count && i < 6; i++)
            {
                var pr = pairRows[i];
                sb.AppendLine($"  {pr.aName} 打 {pr.bName}：{pr.wins}/{pr.total} " +
                              $"({pr.rate * 100:F0}%)");
            }
            sb.AppendLine("  …");
            for (int i = System.Math.Max(0, pairRows.Count - 6); i < pairRows.Count; i++)
            {
                var pr = pairRows[i];
                sb.AppendLine($"  {pr.aName} 打 {pr.bName}：{pr.wins}/{pr.total} " +
                              $"({pr.rate * 100:F0}%)");
            }

            if (anomalies.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"=== 异常 {anomalies.Count} 处 ===");
                for (int i = 0; i < anomalies.Count && i < 30; i++)
                    sb.AppendLine("  " + anomalies[i]);
            }

            // ⚠️ 无头运行器不带 UnityEngine，所以用 Console 输出
            //    （无头测试的日志就是从 stdout 读的）。
            System.Console.WriteLine(sb.ToString());
            // 报告写成文件（断言消息会被截断，NUnit 输出也不好取）
            try { System.IO.File.WriteAllText("Tools/1v1-report.txt", sb.ToString()); }
            catch (System.Exception) { }

            Assert.AreEqual(0, anomalies.Count,
                "循环赛里出现了异常（崩溃 / 卡死 / 超时），详见日志。");
        }

        /// <summary>
        /// **1v1 经典开局**：双方各投 1d20，点数之和 = 两手初始值。
        ///
        /// 这里用确定性种子（而不是界面那条交互路径），
        /// 因为循环赛要的是"统计"而不是"UI 流程"。
        /// 拆分取**面向总和的中庸值**（尽量均衡，避免给某方送极端手值）。
        /// </summary>
        private static void PrepareClassicStart(GameManager game, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);

            int rollA = DiceRollSetup.Roll(rng);
            int rollB = DiceRollSetup.Roll(rng);

            ApplyInitialHands(game.PlayerA, rollA);
            ApplyInitialHands(game.PlayerB, rollB);

            // ⚠️ 先手由 `aFirst` 控制（见 RunOne）—— 这里不动 CurrentPlayer。
            //    掷骰只用来决定**初始手值**，避免把"先手偏差"混进胜率统计。
        }

        /// <summary>按 1d20 的点数给某玩家分配初始手值。</summary>
        private static void ApplyInitialHands(PlayerState player, int roll)
        {
            List<HandSplit> splits = DiceRollSetup.DistinctSplits(roll);
            if (splits.Count == 0) return;

            // 取"两手最接近"的那个拆分（最稳，也最不容易制造极端局面）
            HandSplit best = splits[0];
            int bestGap = int.MaxValue;
            for (int i = 0; i < splits.Count; i++)
            {
                int gap = System.Math.Abs(splits[i].Left - splits[i].Right);
                if (gap < bestGap) { bestGap = gap; best = splits[i]; }
            }

            TestSetup.SetHand(player.LeftHand, best.Left);
            TestSetup.SetHand(player.RightHand, best.Right);

            // 武僧的 5 要进入太极（与实机一致：开局那个 5 就是太极）
            MonkLogic.ApplyTaichiToFives(player);
        }

        private static double Rate(int w, int l, int d)
        {
            int total = w + l + d;
            return total == 0 ? 0 : w / (double)total;
        }

        /// <summary>
        /// 跑一局。任何异常都被捕获成"异常描述"返回，而不是让整个测试挂掉 ——
        /// 这样一次就能看到**所有**角色的所有问题。
        /// </summary>
        private static MatchResult RunOne(CharacterDefinition defA, CharacterDefinition defB,
                                          bool aFirst, int seed, out string anomaly)
        {
            anomaly = null;

            try
            {
                PlayerState pA = CharacterFactory.CreatePlayer(defA.DisplayName, defA);
                PlayerState pB = CharacterFactory.CreatePlayer(defB.DisplayName, defB);

                var game = new GameManager(pA, pB, new GreedyChoiceProvider(),
                                           new GreedyChoiceProvider());

                // 1v1 经典：先掷 1d20 决定初始手值（见 PrepareClassicStart）
                PrepareClassicStart(game, seed);

                // 玩家要求：所有模式的上限统一为 **50 轮**。
                // 1v1 一轮 = 2 个回合，所以 50 轮 = 100 个回合。
                game.RoundLimit = GameManager.DefaultRoundLimit;

                var runner = new MatchRunner { RecordHistory = false, MaxTurns = 200 };
                MatchResult r = runner.Run(game, new GreedyChoiceProvider(),
                                           new GreedyChoiceProvider());

                if (r.HitTurnLimit)
                {
                    anomaly = $"【僵局】{defA.DisplayName} vs {defB.DisplayName} " +
                              $"(seed={seed}) 打到 {r.Turns} 回合仍未分胜负";
                }

                return r;
            }
            catch (System.Exception ex)
            {
                anomaly = $"【崩溃】{defA.DisplayName} vs {defB.DisplayName} " +
                          $"(seed={seed})：{ex.GetType().Name} {ex.Message}";
                return null;
            }
        }
    }
}
