using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// <b>双将模式的批量测试台</b>。
    ///
    /// 为什么需要它：真人试玩**只能碰到运气好的那几个组合**。
    /// 上一次那个致命 bug（两名子角色都不实现改写器时返回了 None，
    /// 于是所有数字都变成无效果）恰好被玩家碰到 —— 但那是运气。
    /// 这个测试台把**所有合法双将组合交叉对战**，一次跑出全貌。
    ///
    /// 组合规模：每个阵营 6 名角色 -> C(6,2) = 15，三个阵营共 **45 个双将**；
    /// 交叉对战 45 x 44 / 2 = **990 对**。
    /// </summary>
    internal static class DualVersusDualHarness
    {
        /// <summary>一局的结果。</summary>
        public sealed class Outcome
        {
            /// <summary>赢家是不是"甲"（第一个双将）。null = 平局。</summary>
            public bool? FirstWon;
            public bool HitLimit;
            public string Error;
            public int Turns;
        }

        /// <summary>一个双将（主将 + 副将，同阵营）。</summary>
        public sealed class Dual
        {
            public CharacterDefinition Primary;
            public CharacterDefinition Secondary;

            /// <summary>阵营序列（守序 / 中立 / 混乱）。</summary>
            public string Axis;

            public string Name => Primary.DisplayName + "+" + Secondary.DisplayName;

            public override string ToString() => Name;
        }

        /// <summary>列出全部合法双将（每阵营 C(6,2) = 15，共 45 个）。</summary>
        public static List<Dual> AllDuals()
        {
            var list = new List<Dual>();
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;

            for (int i = 0; i < all.Length; i++)
            {
                for (int j = i + 1; j < all.Length; j++)
                {
                    if (all[i] == null || all[j] == null) continue;
                    if (!DualGeneralFactory.SameAxis(all[i], all[j])) continue;

                    list.Add(new Dual
                    {
                        Primary = all[i],
                        Secondary = all[j],
                        Axis = AxisOf(all[i]),
                    });
                }
            }
            return list;
        }

        /// <summary>角色所属的阵营序列（守序 / 中立 / 混乱）。</summary>
        public static string AxisOf(CharacterDefinition def)
        {
            CodexEntry e = CharacterCodex.For(def);
            return e == null ? "未知" : Alignments.LawAxisOf(e.Alignment);
        }

        /// <summary>
        /// 跑一局双将对双将。
        ///
        /// <paramref name="firstIsA"/> 控制谁坐 A 位 —— 同一对组合跑两遍交换座位，
        /// 把座位偏差从胜率里抵消掉（与 1v1 测试台同一做法）。
        /// </summary>
        public static Outcome Play(Dual first, Dual second, bool firstIsA, int seed)
        {
            var result = new Outcome();

            try
            {
                Dual a = firstIsA ? first : second;
                Dual b = firstIsA ? second : first;

                PlayerState pA = DualGeneralFactory.Create("甲", a.Primary, a.Secondary);
                PlayerState pB = DualGeneralFactory.Create("乙", b.Primary, b.Secondary);

                var choiceA = new GreedyChoiceProvider();
                var choiceB = new GreedyChoiceProvider();
                var game = new GameManager(pA, pB, choiceA, choiceB);

                PrepareStart(game, seed);

                var runner = new MatchRunner { RecordHistory = false, MaxTurns = 200 };
                MatchResult r = runner.Run(game, choiceA, choiceB);

                result.HitLimit = r.HitTurnLimit;
                result.Turns = r.Turns;

                if (r.Winner == null) result.FirstWon = null;
                else result.FirstWon = ReferenceEquals(r.Winner, pA) == firstIsA;
            }
            catch (Exception ex)
            {
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }

            return result;
        }

        /// <summary>按 1d20 掷骰分配初始手值（与实机经典模式一致）。</summary>
        private static void PrepareStart(GameManager game, int seed)
        {
            var rng = new System.Random(seed * 31 + 7);

            ApplyInitialHands(game.PlayerA, DiceRollSetup.Roll(rng));
            ApplyInitialHands(game.PlayerB, DiceRollSetup.Roll(rng));
        }

        private static void ApplyInitialHands(PlayerState player, int roll)
        {
            List<HandSplit> splits = DiceRollSetup.DistinctSplits(roll);
            if (splits.Count == 0) return;

            // 取"两手最接近"的拆分 —— 与 1v1 测试台一致，避免极端开局主导统计
            HandSplit best = splits[0];
            int bestGap = int.MaxValue;
            for (int i = 0; i < splits.Count; i++)
            {
                int gap = Math.Abs(splits[i].Left - splits[i].Right);
                if (gap < bestGap) { bestGap = gap; best = splits[i]; }
            }

            TestSetup.SetHand(player.LeftHand, best.Left);
            TestSetup.SetHand(player.RightHand, best.Right);

            // 武僧的 5 要进入太极（与实机一致：开局那个 5 就是太极）
            MonkLogic.ApplyTaichiToFives(player);
        }

        /// <summary>累计战绩。</summary>
        public sealed class Tally
        {
            public int Games;
            public int Wins;
            public int Draws;
            public int LimitHits;

            public double Rate => Games == 0 ? 0 : Wins / (double)Games;
        }

        /// <summary>
        /// 全部组合交叉对战并统计：
        ///   · 每个<b>角色</b>的胜率（它出现在哪些双将里就计哪些）
        ///   · 每个<b>双将</b>的胜率
        ///   · 每个<b>阵营</b>的胜率（<b>只统计外战</b>，即不同阵营之间的对局）
        /// </summary>
        public static string RunRoundRobin(int gamesPerPair, out int totalGames)
        {
            List<Dual> duals = AllDuals();

            var dualTally = new Dictionary<string, Tally>();
            var roleTally = new Dictionary<string, Tally>();
            var axisTally = new Dictionary<string, Tally>();

            var anomalies = new List<string>();

            for (int i = 0; i < duals.Count; i++) dualTally[duals[i].Name] = new Tally();
            for (int i = 0; i < duals.Count; i++)
            {
                AddRole(roleTally, duals[i].Primary);
                AddRole(roleTally, duals[i].Secondary);
            }
            axisTally["守序"] = new Tally();
            axisTally["中立"] = new Tally();
            axisTally["混乱"] = new Tally();

            int games = 0;

            for (int i = 0; i < duals.Count; i++)
            {
                for (int j = i + 1; j < duals.Count; j++)
                {
                    Dual first = duals[i];
                    Dual second = duals[j];
                    bool crossAxis = first.Axis != second.Axis;

                    for (int g = 0; g < gamesPerPair; g++)
                    {
                        // 每个配对用**自己的**种子，避免所有配对复用同一批开局
                        int seed = i * 1009 + j * 17 + g;

                        for (int swap = 0; swap < 2; swap++)
                        {
                            bool firstIsA = swap == 0;
                            Outcome o = Play(first, second, firstIsA, seed);
                            games++;

                            if (o.Error != null)
                            {
                                if (anomalies.Count < 40)
                                    anomalies.Add($"{first.Name} vs {second.Name} (seed={seed})：{o.Error}");
                                continue;
                            }

                            if (o.HitLimit)
                            {
                                dualTally[first.Name].LimitHits++;
                                dualTally[second.Name].LimitHits++;
                                if (anomalies.Count < 40)
                                    anomalies.Add($"{first.Name} vs {second.Name} (seed={seed})：打到回合上限");
                            }

                            // a 就是先手方（firstIsA 已按需要交换过）
                            Dual a = firstIsA ? first : second;
                            Dual b = firstIsA ? second : first;

                            dualTally[first.Name].Games++;
                            dualTally[second.Name].Games++;

                            if (o.FirstWon == null)
                            {
                                dualTally[first.Name].Draws++;
                                dualTally[second.Name].Draws++;

                                if (crossAxis)
                                {
                                    axisTally[first.Axis].Games++;
                                    axisTally[first.Axis].Draws++;
                                    axisTally[second.Axis].Games++;
                                    axisTally[second.Axis].Draws++;
                                }
                                continue;
                            }

                            Dual winner = o.FirstWon.Value ? a : b;
                            Dual loser = o.FirstWon.Value ? b : a;

                            dualTally[winner.Name].Wins++;

                            AddRole(roleTally, winner.Primary, won: true);
                            AddRole(roleTally, winner.Secondary, won: true);
                            AddRole(roleTally, loser.Primary, won: false);
                            AddRole(roleTally, loser.Secondary, won: false);

                            if (crossAxis)
                            {
                                axisTally[winner.Axis].Games++;
                                axisTally[winner.Axis].Wins++;
                                axisTally[loser.Axis].Games++;
                            }
                        }
                    }
                }
            }

            totalGames = games;
            return Report(duals, dualTally, roleTally, axisTally, gamesPerPair, games, anomalies);
        }

        private static void AddRole(Dictionary<string, Tally> map, CharacterDefinition def,
                                    bool? won = null)
        {
            string key = def.DisplayName;
            if (!map.TryGetValue(key, out Tally t))
            {
                t = new Tally();
                map[key] = t;
            }

            if (won == null) return;
            t.Games++;
            if (won.Value) t.Wins++;
        }

        private static string Report(List<Dual> duals,
                                     Dictionary<string, Tally> dualTally,
                                     Dictionary<string, Tally> roleTally,
                                     Dictionary<string, Tally> axisTally,
                                     int gamesPerPair, int totalGames,
                                     List<string> anomalies)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== 双将交叉对战测试台 ===");
            sb.AppendLine($"合法双将 {duals.Count} 个、交叉配对 {duals.Count * (duals.Count - 1) / 2} 对、"
                        + $"每对 {gamesPerPair} 轮（每轮交换座位 = 2 局）");
            sb.AppendLine($"总对局 {totalGames}");

            sb.AppendLine();
            sb.AppendLine("--- 角色胜率（角色出现在哪个双将里就计哪个）---");
            sb.AppendLine("角色        胜     负   场次     胜率");
            var roles = new List<KeyValuePair<string, Tally>>(roleTally);
            roles.Sort((x, y) => y.Value.Rate.CompareTo(x.Value.Rate));
            for (int i = 0; i < roles.Count; i++)
            {
                Tally t = roles[i].Value;
                sb.AppendLine($"{roles[i].Key,-8}{t.Wins,6}{t.Games - t.Wins,6}{t.Games,6}   {t.Rate * 100,5:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine("--- 阵营胜率（只统计外战：不同阵营之间的对局）---");
            string[] order = { "守序", "中立", "混乱" };
            for (int k = 0; k < order.Length; k++)
            {
                Tally t = axisTally[order[k]];
                sb.AppendLine($"{order[k],-6}{t.Wins,6} / {t.Games,-6}   {t.Rate * 100,5:F1}%"
                            + $"   （平 {t.Draws}）");
            }

            sb.AppendLine();
            sb.AppendLine("--- 双将胜率 ---");
            var all = new List<KeyValuePair<string, Tally>>(dualTally);
            all.Sort((x, y) => y.Value.Rate.CompareTo(x.Value.Rate));

            sb.AppendLine("  最强 15：");
            for (int i = 0; i < all.Count && i < 15; i++)
                sb.AppendLine($"    {i + 1,2}. {all[i].Key,-16}{all[i].Value.Rate * 100,5:F1}%"
                            + $"  （{all[i].Value.Wins}/{all[i].Value.Games}）");

            sb.AppendLine("  最弱 15：");
            for (int i = Math.Max(0, all.Count - 15); i < all.Count; i++)
                sb.AppendLine($"    {i + 1,2}. {all[i].Key,-16}{all[i].Value.Rate * 100,5:F1}%"
                            + $"  （{all[i].Value.Wins}/{all[i].Value.Games}）");

            int limitTotal = 0;
            foreach (var kv in dualTally) limitTotal += kv.Value.LimitHits;

            sb.AppendLine();
            sb.AppendLine($"僵局（达回合上限）合计 {limitTotal / 2} 局");

            if (anomalies.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"!!! 异常 {anomalies.Count} 条（最多列 40 条）!!!");
                for (int i = 0; i < anomalies.Count; i++) sb.AppendLine("  " + anomalies[i]);
            }
            else
            {
                sb.AppendLine("异常：无");
            }

            return sb.ToString();
        }
    }
}
