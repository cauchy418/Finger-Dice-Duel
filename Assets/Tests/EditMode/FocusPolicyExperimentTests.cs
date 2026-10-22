using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 集火对照实验**：baseline / focusSelf / focusMate。
    ///
    /// 口径（玩家要求）：
    ///   · 每个角色每段种子 600 局，共 **2 段**（0..599 / 10,000,000..10,000,599）；
    ///   · 同一局的三组共享**完全相同**的阵容、初始手值与术士·狂野魔法骰子；
    ///   · 只有**受试角色**的 AI 切换集火策略，其余三个座位始终 baseline；
    ///   · 受试角色的队伍胜 = 胜；平局 / 异常不计胜（平局率单独报）。
    ///
    /// 这样每个角色拿到的是"**我**用集火，队友和对手不变"的配对比较，
    /// 而不是"全场都集火"的混合效应。
    /// </summary>
    [TestFixture]
    public class FocusPolicyExperimentTests
    {
        private const int GamesPerSegment = 600;
        private const int TurnLimit = 50;          // 与正式 2v2 统计一致
        private const int SegmentBBase = 10000000;

        private static readonly int[] SegmentBases = { 0, SegmentBBase };
        private static readonly GreedyChoiceProvider.FocusPolicy[] Policies =
        {
            GreedyChoiceProvider.FocusPolicy.None,
            GreedyChoiceProvider.FocusPolicy.Self,
            GreedyChoiceProvider.FocusPolicy.Mate,
        };

        private sealed class Cell
        {
            public int Wins;
            public int Draws;
            public int Games;
            public int Errors;
        }

        private sealed class Row
        {
            public string Name;
            public int Range;
            public double BaseRate, SelfRate, MateRate;
            public double SelfDelta, MateDelta;
            public double SelfCi, MateCi;
            public bool SelfSignificant, MateSignificant;
            public string Verdict;
        }

        [Test]
        public void Three_focus_policies_per_role_600_games_two_seed_segments()
        {
            CharacterDefinition[] roster = CharacterDefinition.BuiltIn;
            int roles = roster.Length;

            // results[role, segment, policy]
            var results = new Cell[roles, 2, 3];
            for (int r = 0; r < roles; r++)
                for (int s = 0; s < 2; s++)
                    for (int p = 0; p < 3; p++)
                        results[r, s, p] = new Cell();

            // 配对比较的 discordant 计数：baseline 胜/负 与 policy 相反的次数
            // paired[r, segment, 0=Self / 1=Mate] = (b, c)
            var bCount = new int[roles, 2, 2];
            var cCount = new int[roles, 2, 2];
            var pairN = new int[roles, 2, 2];

            int totalGames = 0, totalErrors = 0;

            for (int r = 0; r < roles; r++)
            {
                for (int s = 0; s < 2; s++)
                {
                    for (int g = 0; g < GamesPerSegment; g++)
                    {
                        Lineup line = BuildLineup(roster, r, SegmentBases[s], g);
                        int gameSeed = unchecked(SegmentBases[s] + r * 100003 + g * 7919);

                        bool[] wins = new bool[3];
                        bool[] valid = new bool[3];

                        for (int p = 0; p < 3; p++)
                        {
                            OutcomeAndWin run = RunOne(line, gameSeed, Policies[p]);
                            Cell cell = results[r, s, p];

                            if (run.Error != null)
                            {
                                cell.Errors++;
                                totalErrors++;
                                continue;
                            }

                            cell.Games++;
                            totalGames++;
                            if (run.Won) cell.Wins++;
                            else if (run.Draw) cell.Draws++;

                            wins[p] = run.Won;
                            valid[p] = true;
                        }

                        if (!valid[0]) continue;

                        if (valid[1])
                        {
                            pairN[r, s, 0]++;
                            if (wins[0] && !wins[1]) bCount[r, s, 0]++;
                            if (!wins[0] && wins[1]) cCount[r, s, 0]++;
                        }

                        if (valid[2])
                        {
                            pairN[r, s, 1]++;
                            if (wins[0] && !wins[2]) bCount[r, s, 1]++;
                            if (!wins[0] && wins[2]) cCount[r, s, 1]++;
                        }
                    }
                }
            }

            // ---- 汇总成 24 行 ----
            var rows = new List<Row>(roles);
            double sumBase = 0, sumSelf = 0, sumMate = 0;
            int nBase = 0, nSelf = 0, nMate = 0;
            int drawsBase = 0, drawsSelf = 0, drawsMate = 0;

            for (int r = 0; r < roles; r++)
            {
                double bw = 0, sw = 0, mw = 0;
                int bn = 0, sn = 0, mn = 0;
                int bd = 0, sd = 0, md = 0;
                int bSelf = 0, cSelf = 0, bMate = 0, cMate = 0;
                int nSelfPair = 0, nMatePair = 0;

                for (int s = 0; s < 2; s++)
                {
                    Cell b = results[r, s, 0];
                    Cell se = results[r, s, 1];
                    Cell ma = results[r, s, 2];

                    bw += b.Wins; bn += b.Games; bd += b.Draws;
                    sw += se.Wins; sn += se.Games; sd += se.Draws;
                    mw += ma.Wins; mn += ma.Games; md += ma.Draws;

                    bSelf += bCount[r, s, 0]; cSelf += cCount[r, s, 0];
                    bMate += bCount[r, s, 1]; cMate += cCount[r, s, 1];
                    nSelfPair += pairN[r, s, 0]; nMatePair += pairN[r, s, 1];
                }

                double baseRate = bn == 0 ? 0 : bw / bn;
                double selfRate = sn == 0 ? 0 : sw / sn;
                double mateRate = mn == 0 ? 0 : mw / mn;

                PairedDiff(bSelf, cSelf, nSelfPair, out double selfDelta, out double selfCi);
                PairedDiff(bMate, cMate, nMatePair, out double mateDelta, out double mateCi);

                var row = new Row
                {
                    Name = roster[r].DisplayName,
                    Range = RangeOf(roster[r]),
                    BaseRate = baseRate * 100,
                    SelfRate = selfRate * 100,
                    MateRate = mateRate * 100,
                    SelfDelta = selfDelta * 100,
                    MateDelta = mateDelta * 100,
                    SelfCi = selfCi * 100,
                    MateCi = mateCi * 100,
                    SelfSignificant = selfDelta > 0 && selfDelta - selfCi > 0
                                      || selfDelta < 0 && selfDelta + selfCi < 0,
                    MateSignificant = mateDelta > 0 && mateDelta - mateCi > 0
                                      || mateDelta < 0 && mateDelta + mateCi < 0,
                };
                row.Verdict = VerdictOf(row);
                rows.Add(row);

                sumBase += baseRate; sumSelf += selfRate; sumMate += mateRate;
                nBase += bn; nSelf += sn; nMate += mn;
                drawsBase += bd; drawsSelf += sd; drawsMate += md;
            }

            rows.Sort((x, y) =>
            {
                double bx = Math.Max(x.SelfDelta, x.MateDelta);
                double by = Math.Max(y.SelfDelta, y.MateDelta);
                int cmp = by.CompareTo(bx);
                return cmp != 0 ? cmp : string.CompareOrdinal(x.Name, y.Name);
            });

            // ---- 写报告 ----
            var sb = new StringBuilder();
            sb.AppendLine("=== 2v2 集火策略对照实验 ===");
            sb.AppendLine($"口径：每角色每段 {GamesPerSegment} 局 × 2 段种子（共 {GamesPerSegment * 2} 局/组）；");
            sb.AppendLine("      同局三组共享阵容 / 初始手值 / 术士·狂野魔法骰子；只有受试角色的 AI 切换策略。");
            sb.AppendLine("      胜率 = 受试角色所在队伍的胜局 / 有效局；平局与异常不计胜。");
            sb.AppendLine();
            sb.AppendLine("角色        射程   基线      集火Self  集火Mate   ΔSelf      ΔMate      判定");
            sb.AppendLine(new string('-', 86));

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                sb.AppendLine(
                    $"{row.Name,-10} {RangeLabel(row.Range),-4} "
                    + $"{row.BaseRate,6:F1}%  {row.SelfRate,7:F1}%  {row.MateRate,7:F1}%  "
                    + $"{row.SelfDelta,7:+0.0;-0.0;0.0}  {row.MateDelta,8:+0.0;-0.0;0.0}  "
                    + $"{row.Verdict}");
            }

            sb.AppendLine();
            sb.AppendLine("判定：√ = 配对 95% 置信区间不含 0（显著）；+/- 为方向，— = 无显著差异。");
            sb.AppendLine("      `射程` 是角色的基础攻击距离（∞ = 无限）；近战打不到队友车道的敌人，");
            sb.AppendLine("      所以 focusMate 对射程 1 的角色**不应**改变行为（表中 Δ≈0 是预期结果）。");
            sb.AppendLine();

            // 分段稳定性
            int selfSameSign = 0, selfNonZero = 0, mateSameSign = 0, mateNonZero = 0;
            for (int r = 0; r < roles; r++)
            {
                double selfA = DeltaOf(results[r, 0, 0], results[r, 0, 1]);
                double selfB = DeltaOf(results[r, 1, 0], results[r, 1, 1]);
                double mateA = DeltaOf(results[r, 0, 0], results[r, 0, 2]);
                double mateB = DeltaOf(results[r, 1, 0], results[r, 1, 2]);

                if (Math.Abs(selfA) > 0.001 || Math.Abs(selfB) > 0.001) selfNonZero++;
                if (Math.Sign(selfA) == Math.Sign(selfB) && Math.Abs(selfA) > 0.001
                    && Math.Abs(selfB) > 0.001) selfSameSign++;

                if (Math.Abs(mateA) > 0.001 || Math.Abs(mateB) > 0.001) mateNonZero++;
                if (Math.Sign(mateA) == Math.Sign(mateB) && Math.Abs(mateA) > 0.001
                    && Math.Abs(mateB) > 0.001) mateSameSign++;
            }

            sb.AppendLine("=== 两段种子稳定性 ===");
            sb.AppendLine($"  Self：有差异的角色 {selfNonZero}/{roles}，两段同向 {selfSameSign}/{selfNonZero}");
            sb.AppendLine($"  Mate：有差异的角色 {mateNonZero}/{roles}，两段同向 {mateSameSign}/{mateNonZero}");
            sb.AppendLine();

            // 只列"任一组 |Δ| ≥ 1 点"的角色 —— 小到 0.x 的差异不值得逐段看
            sb.AppendLine("分种子段明细（单位：个百分点；A = 0..599，B = 10,000,000+）：");
            sb.AppendLine("角色        ΔSelf-A  ΔSelf-B   ΔMate-A  ΔMate-B");
            for (int r = 0; r < roles; r++)
            {
                double selfA = DeltaOf(results[r, 0, 0], results[r, 0, 1]) * 100;
                double selfB = DeltaOf(results[r, 1, 0], results[r, 1, 1]) * 100;
                double mateA = DeltaOf(results[r, 0, 0], results[r, 0, 2]) * 100;
                double mateB = DeltaOf(results[r, 1, 0], results[r, 1, 2]) * 100;

                if (Math.Abs(selfA) < 1 && Math.Abs(selfB) < 1
                    && Math.Abs(mateA) < 1 && Math.Abs(mateB) < 1) continue;

                sb.AppendLine($"{roster[r].DisplayName,-10} "
                              + $"{selfA,8:+0.0;-0.0;0.0}  {selfB,8:+0.0;-0.0;0.0}  "
                              + $"{mateA,8:+0.0;-0.0;0.0}  {mateB,8:+0.0;-0.0;0.0}");
            }
            sb.AppendLine();

            // 按射程分组
            AppendRangeSummary(sb, rows, rangeAtLeast2: true, label: "射程 ≥2（能跨车道）");
            AppendRangeSummary(sb, rows, rangeAtLeast2: false, label: "射程 1（近战）");
            sb.AppendLine();

            sb.AppendLine("=== 汇总 ===");
            sb.AppendLine($"总对局 {totalGames}（每组 {totalGames / 3}），异常 {totalErrors} 处");
            sb.AppendLine($"总胜率：基线 {SafePct(sumBase, roles)}，"
                          + $"Self {SafePct(sumSelf, roles)}，Mate {SafePct(sumMate, roles)}");
            sb.AppendLine($"平局率：基线 {SafePct(drawsBase, nBase)}，"
                          + $"Self {SafePct(drawsSelf, nSelf)}，Mate {SafePct(drawsMate, nMate)}");

            int selfUp = 0, selfDown = 0, mateUp = 0, mateDown = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].SelfSignificant)
                {
                    if (rows[i].SelfDelta > 0) selfUp++; else selfDown++;
                }
                if (rows[i].MateSignificant)
                {
                    if (rows[i].MateDelta > 0) mateUp++; else mateDown++;
                }
            }

            sb.AppendLine($"显著提升：Self {selfUp} 个角色、Mate {mateUp} 个角色；"
                          + $"显著下降：Self {selfDown} 个、Mate {mateDown} 个。");
            sb.AppendLine("（单个角色 1200 局，配对差约 ±1.5~2 个百分点才显著；小差异不要过度解读。）");

            AppendAnomalies(sb, results, roster);

            string report = sb.ToString();
            Console.WriteLine(report);

            try { System.IO.File.WriteAllText("Tools/focus-report.txt", report); }
            catch (Exception) { }

            Assert.True(totalGames > roles * 2 * GamesPerSegment * 3 * 9 / 10,
                $"有效对局太少（{totalGames}），统计不可信。");
        }

        // ============================================================
        // 组局
        // ============================================================

        private readonly struct Lineup
        {
            public readonly CharacterDefinition Seat1, Seat2, Seat3, Seat4;
            public readonly int SubjectSeat;       // 0..3
            public readonly bool SubjectOn14;

            public Lineup(CharacterDefinition s1, CharacterDefinition s2,
                          CharacterDefinition s3, CharacterDefinition s4,
                          int subjectSeat, bool on14)
            {
                Seat1 = s1; Seat2 = s2; Seat3 = s3; Seat4 = s4;
                SubjectSeat = subjectSeat; SubjectOn14 = on14;
            }
        }

        private static Lineup BuildLineup(CharacterDefinition[] roster, int roleIndex,
                                          int segmentBase, int game)
        {
            CharacterDefinition subject = roster[roleIndex];

            var pool = new List<CharacterDefinition>(roster.Length - 1);
            for (int i = 0; i < roster.Length; i++)
                if (i != roleIndex) pool.Add(roster[i]);

            // 同一 (角色, 段, 局) 的阵容对三组实验完全相同
            var rng = new Random(unchecked(segmentBase + roleIndex * 100003 + game * 7919));
            for (int i = 0; i < 3; i++)
            {
                int j = rng.Next(i, pool.Count);
                CharacterDefinition tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            CharacterDefinition mate = pool[0];
            CharacterDefinition foeA = pool[1];
            CharacterDefinition foeB = pool[2];

            // 交替把受试者放在 1&4 / 2&3，抵消座位偏差
            if ((game & 1) == 0)
                return new Lineup(subject, foeA, foeB, mate, 0, true);
            else
                return new Lineup(foeA, subject, mate, foeB, 1, false);
        }

        private readonly struct OutcomeAndWin
        {
            public readonly bool Won;
            public readonly bool Draw;
            public readonly string Error;

            public OutcomeAndWin(bool won, bool draw, string error)
            {
                Won = won; Draw = draw; Error = error;
            }
        }

        private static OutcomeAndWin RunOne(Lineup line, int gameSeed,
                                            GreedyChoiceProvider.FocusPolicy policy)
        {
            var providers = new List<IChoiceProvider>(4)
            {
                new GreedyChoiceProvider { UseLearnedFocus = false },
                new GreedyChoiceProvider { UseLearnedFocus = false },
                new GreedyChoiceProvider { UseLearnedFocus = false },
                new GreedyChoiceProvider { UseLearnedFocus = false },
            };

            // 只有受试座位切换策略；其余 3 个座位始终 baseline。
            ((GreedyChoiceProvider)providers[line.SubjectSeat]).Focus = policy;

            TwoVersusTwoHarness.Outcome outcome = TwoVersusTwoHarness.PlayWith(
                line.Seat1, line.Seat2, line.Seat3, line.Seat4,
                gameSeed, TurnLimit, providers,
                seated => SeedRandomSources(seated, gameSeed),
                out _);

            if (outcome.Error != null)
                return new OutcomeAndWin(false, false, outcome.Error);

            bool won = line.SubjectOn14 ? outcome.Seat14Won : outcome.Seat23Won;
            bool draw = !outcome.Seat14Won && !outcome.Seat23Won;
            return new OutcomeAndWin(won, draw, null);
        }

        /// <summary>
        /// 把角色自带的随机源换成**按局固定**的种子。
        ///
        /// 术士·狂野魔法每次攻击掷 1d100；用默认 `new Random()` 会让三组实验的骰子不同，
        /// 把"集火的效果"淹没在骰子噪声里（三份正式 report 每次跑也都会小幅漂移）。
        /// </summary>
        private static void SeedRandomSources(IReadOnlyList<PlayerState> seated, int gameSeed)
        {
            for (int i = 0; i < seated.Count; i++)
            {
                if (seated[i].Character is WildMagicLogic wildMagic)
                    wildMagic.Rng = new Random(unchecked(gameSeed * 31 + i * 97 + 1));
            }
        }

        // ============================================================
        // 统计 / 报告
        // ============================================================

        private static void PairedDiff(int b, int c, int n,
                                       out double delta, out double ci)
        {
            if (n <= 0)
            {
                delta = 0;
                ci = 0;
                return;
            }

            delta = (c - b) / (double)n;
            double variance = (b + c) - (b - c) * (double)(b - c) / n;
            double se = Math.Sqrt(Math.Max(0, variance)) / n;
            ci = 1.96 * se;
        }

        private static double DeltaOf(Cell baseline, Cell focused)
        {
            if (baseline.Games == 0 || focused.Games == 0) return 0;
            return (focused.Wins / (double)focused.Games)
                   - (baseline.Wins / (double)baseline.Games);
        }

        private static string VerdictOf(Row row)
        {
            string self = row.SelfSignificant ? Arrow("Self", row.SelfDelta) : null;
            string mate = row.MateSignificant ? Arrow("Mate", row.MateDelta) : null;

            if (self != null && mate != null) return self + " / " + mate;
            return self ?? mate ?? "—";
        }

        private static string Arrow(string policy, double delta)
            => delta >= 0 ? $"{policy}↑{delta:F1}" : $"{policy}↓{Math.Abs(delta):F1}";

        private static void AppendRangeSummary(StringBuilder sb, List<Row> rows,
                                               bool rangeAtLeast2, string label)
        {
            double selfSum = 0, mateSum = 0;
            int n = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                bool ranged = rows[i].Range >= 2 || rows[i].Range < 0;
                if (ranged != rangeAtLeast2) continue;
                selfSum += rows[i].SelfDelta;
                mateSum += rows[i].MateDelta;
                n++;
            }

            if (n == 0) return;

            sb.AppendLine($"  {label}：{n} 个角色，平均 ΔSelf {selfSum / n:+0.0;-0.0;0.0} 个点、"
                          + $"ΔMate {mateSum / n:+0.0;-0.0;0.0} 个点");
        }

        private static void AppendAnomalies(StringBuilder sb, Cell[,,] results,
                                            CharacterDefinition[] roster)
        {
            int count = 0;
            for (int r = 0; r < roster.Length; r++)
                for (int s = 0; s < 2; s++)
                    for (int p = 0; p < 3; p++)
                        count += results[r, s, p].Errors;

            if (count == 0) return;

            sb.AppendLine();
            sb.AppendLine($"=== 异常 {count} 处（按角色汇总）===");
            for (int r = 0; r < roster.Length; r++)
            {
                int errors = 0;
                for (int s = 0; s < 2; s++)
                    for (int p = 0; p < 3; p++)
                        errors += results[r, s, p].Errors;

                if (errors > 0)
                    sb.AppendLine($"  {roster[r].DisplayName}：{errors} 局");
            }
        }

        private static string RangeLabel(int range)
            => range >= 1000 ? "∞" : range.ToString();

        private static int RangeOf(CharacterDefinition definition)
        {
            PlayerState player = CharacterFactory.CreatePlayer("测试", definition);
            return GameManager.BaseRangeOf(player);
        }

        private static string SafePct(double sum, int count)
            => count == 0 ? "—" : $"{sum / count * 100:F1}%";
    }
}
