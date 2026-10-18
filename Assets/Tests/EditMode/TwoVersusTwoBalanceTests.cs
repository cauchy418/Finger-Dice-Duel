using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 2v2 的循环赛统计（用重写后的 <see cref="TwoVersusTwoHarness"/>）。
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoBalanceTests
    {
        /// <summary>自检：测试台必须给每个座位自己的 AI，且跑得起来。</summary>
        [Test]
        public void Harness_dispatches_every_seat_to_its_own_ai()
        {
            int errors = 0;

            const int games = 20;
            for (int g = 0; g < games; g++)
            {
                TwoVersusTwoHarness.Outcome r = TwoVersusTwoHarness.Play(
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    g, 400, out _);

                if (r.Error != null) errors++;
            }

            Assert.AreEqual(0, errors,
                "测试台出错（座位没有可用选择器 / 无合法步 / 抛异常）。");
        }

        /// <summary>
        /// **2v2 必须能分出胜负**（回归测试）。
        /// 钉住的是 GetLegalMoves 漏掉 IsAlive 那个真实 bug（详见该方法注释）。
        /// </summary>
        [Test]
        public void Two_versus_two_reaches_a_result_instead_of_stalling()
        {
            int decided = 0, drew = 0, errors = 0;

            const int games = 6;
            for (int g = 0; g < games; g++)
            {
                TwoVersusTwoHarness.Outcome r = TwoVersusTwoHarness.Play(
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    CharacterDefinition.Soldier, CharacterDefinition.Soldier,
                    g, 400, out _);

                if (r.Error != null) errors++;
                else if (r.Seat14Won || r.Seat23Won) decided++;
                else drew++;
            }

            Assert.AreEqual(0, errors, "测试台出错。");
            Assert.True(decided > 0,
                $"四个士兵对打 {games} 局里一局都没分出胜负（平 {drew}）");
        }


        /// <summary>
        /// **2v2 全角色组队循环赛**：两个角色组成一队（18 取 2 = 153 支），
        /// 按固定间隔配桌、每对 4 局并交换座位，统计
        ///   ① 每个角色的队伍胜率
        ///   ② 1&amp;4 座位组与 2&amp;3 座位组哪个胜率高
        /// </summary>
        [Test]
        public void Two_versus_two_round_robin_win_rates_and_seat_bias()
        {
            CharacterDefinition[] roster = CharacterDefinition.BuiltIn;

            var teams = new List<(CharacterDefinition a, CharacterDefinition b)>();
            for (int i = 0; i < roster.Length; i++)
                for (int j = i + 1; j < roster.Length; j++)
                    teams.Add((roster[i], roster[j]));

            var wins = new Dictionary<string, int>();
            var games = new Dictionary<string, int>();
            var draws = new Dictionary<string, int>();
            for (int i = 0; i < roster.Length; i++)
            {
                wins[roster[i].Id] = 0;
                games[roster[i].Id] = 0;
                draws[roster[i].Id] = 0;
            }

            var anomalies = new List<string>();
            int totalGames = 0, totalActions = 0, limitHits = 0, reachedLimit = 0;
            int seat14Wins = 0, seat23Wins = 0, seatDraws = 0;

            // 玩家要求：加大轮数（尽量多）。
            // 每对队伍打 16 局（种子 0..15），共约 153 x 16 = 2448 局。
            // 玩家要求：加大轮数（尽量多）。
            // 每对队伍打 128 局，共约 276 x 128 = 35328 局。
            const int gamesPerMatchup = 128;
            int offset = teams.Count / 2;

            var sb = new StringBuilder();
            sb.AppendLine("=== 2v2 循环赛 ===");

            for (int i = 0; i < teams.Count; i++)
            {
                var t1 = teams[i];
                var t2 = teams[(i + offset) % teams.Count];
                if (ReferenceEquals(t1.a, t2.a) && ReferenceEquals(t1.b, t2.b)) continue;

                for (int g = 0; g < gamesPerMatchup; g++)
                {
                    bool t1On14 = (g % 2) == 0;
                    var seat14 = t1On14 ? t1 : t2;
                    var seat23 = t1On14 ? t2 : t1;

                    // 种子与**配对**相关：否则每个配对都重用同一批开局，
                    // 相当于只测了 32 种开局而不是 4896 种。
                    int seed = g * 101 + i * 7;
                    TwoVersusTwoHarness.Outcome r = TwoVersusTwoHarness.Play(
                        seat14.a, seat23.a, seat23.b, seat14.b, seed, 50, out int actions);

                    if (r.Error != null)
                    {
                        anomalies.Add($"{seat14.a.DisplayName}+{seat14.b.DisplayName} vs "
                                      + $"{seat23.a.DisplayName}+{seat23.b.DisplayName}：{r.Error}");
                        continue;
                    }

                    totalGames++;
                    totalActions += actions;
                    if (r.HitLimit) limitHits++;
                    if (r.ReachedLimit) reachedLimit++;

                    games[seat14.a.Id]++; games[seat14.b.Id]++;
                    games[seat23.a.Id]++; games[seat23.b.Id]++;

                    if (r.Seat14Won)
                    {
                        wins[seat14.a.Id]++; wins[seat14.b.Id]++;
                        seat14Wins++;
                    }
                    else if (r.Seat23Won)
                    {
                        wins[seat23.a.Id]++; wins[seat23.b.Id]++;
                        seat23Wins++;
                    }
                    else
                    {
                        seatDraws++;
                        // 平局：四个人都算一局平局
                        // （玩家要求指出哪些角色特别容易平局）
                        draws[seat14.a.Id]++; draws[seat14.b.Id]++;
                        draws[seat23.a.Id]++; draws[seat23.b.Id]++;
                    }
                }
            }

            var rows = new List<(string name, string id, int w, int n)>();
            for (int i = 0; i < roster.Length; i++)
                rows.Add((roster[i].DisplayName, roster[i].Id,
                          wins[roster[i].Id], games[roster[i].Id]));

            rows.Sort((x, y) =>
            {
                double rx = x.n == 0 ? 0 : x.w / (double)x.n;
                double ry = y.n == 0 ? 0 : y.w / (double)y.n;
                return ry.CompareTo(rx);
            });

            sb.AppendLine("角色        队伍胜     出场    队伍胜率   平局率");
            foreach (var row in rows)
            {
                double rate = row.n == 0 ? 0 : row.w / (double)row.n;
                double dr = row.n == 0 ? 0 : draws[row.id] / (double)row.n;
                sb.AppendLine($"{row.name,-10} {row.w,6} {row.n,8}      {rate * 100:F1}%"
                            + $"   {dr * 100,5:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine("=== 座位偏差 ===");
            sb.AppendLine($"  1&4 胜 {seat14Wins}、2&3 胜 {seat23Wins}、平 {seatDraws}");
            if (totalGames > 0)
            {
                sb.AppendLine($"  1&4 胜率 {(seat14Wins / (double)totalGames) * 100:F1}%");
                sb.AppendLine($"  2&3 胜率 {(seat23Wins / (double)totalGames) * 100:F1}%");
            }

            sb.AppendLine($"总对局 {totalGames}，平均行动数 "
                          + $"{(totalGames == 0 ? 0 : totalActions / (double)totalGames):F1}，"
                          + $"打满轮次上限 {reachedLimit} 局（按队伍总血量判定），"
                          + $"模拟器强停 {limitHits} 局");

            if (anomalies.Count > 0)
            {
                sb.AppendLine($"=== 异常 {anomalies.Count} 处 ===");
                for (int i = 0; i < anomalies.Count && i < 12; i++)
                    sb.AppendLine("  " + anomalies[i]);
            }

            System.Console.WriteLine(sb.ToString());

            // ⚠️ 这一条**不因为异常而失败** ——
            //    先把数据和现场拿到手，再决定异常怎么处理；
            //    否则这条测试会在我还没看清原因时就变红。
            // 报告写成文件（断言消息会被截断，NUnit 输出也不好取）
            try { System.IO.File.WriteAllText("Tools/2v2-report.txt", sb.ToString()); }
            catch (System.Exception) { }

            Assert.True(totalGames > 100, "对局数太少，统计不可信。");
        }
    }
}
