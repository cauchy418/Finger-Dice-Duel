using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
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

            /// <summary>模拟器被 MaxTurns 强行中断（异常情况，应当为 0）。</summary>
            public bool HitLimit;

            /// <summary>正常打满了对局的轮次上限（按血量判定；血量相同才平局）。</summary>
            public bool ReachedLimit;

            public string Error;
            public int Turns;
        }

        /// <summary>一个双将（主将 + 副将，同阵营）。</summary>
        private static string OutcomeHeader(int games,List<Dual> duals)
        {
            var ids=new List<string>();foreach(var d in duals)ids.Add(d.Primary.Id+"+"+d.Secondary.Id);
            return "FDD_DUAL_V1\t"+typeof(GameManager).Assembly.ManifestModule.ModuleVersionId+"\t"+games+"\t"+string.Join("|",ids);
        }
        // 独立进程分片，避免并发共享战局或随机源；种子仍由原配对及轮次决定。
        public static void RunShard(int shard,int shards,string path,int games=12)
        {
            if(shards<1||shard<0||shard>=shards)throw new ArgumentException("invalid shard");
            var duals=AllDuals();using var writer=new StreamWriter(path,false,new UTF8Encoding(false));writer.WriteLine(OutcomeHeader(games,duals));
            int pair=0,played=0;
            for(int i=0;i<duals.Count;i++)for(int j=i+1;j<duals.Count;j++,pair++)
            {
                if(pair%shards!=shard)continue;
                for(int g=0;g<games;g++)for(int swap=0;swap<2;swap++)
                {
                    var o=Play(duals[i],duals[j],swap==0,i*1009+j*17+g);
                    writer.WriteLine(string.Join("\t",i,j,g,swap,o.FirstWon.HasValue?(o.FirstWon.Value?1:0):-1,o.HitLimit?1:0,o.ReachedLimit?1:0,o.Turns,Convert.ToBase64String(Encoding.UTF8.GetBytes(o.Error??""))));played++;
                }
            }
            Console.WriteLine("shard "+shard+" completed "+played+" games");
        }
        private static StreamReader OpenReplay(int games,List<Dual> duals)
        {
            string path=Environment.GetEnvironmentVariable("FDD_DUAL_OUTCOMES");if(string.IsNullOrEmpty(path))return null;
            var reader=new StreamReader(path,Encoding.UTF8);if(reader.ReadLine()!=OutcomeHeader(games,duals)){reader.Dispose();throw new InvalidDataException("dual replay build/roster mismatch");}return reader;
        }
        private static Outcome ReadOutcome(StreamReader reader,int i,int j,int g,int swap)
        {
            var line=reader.ReadLine();var p=line?.Split('\t');
            if(p==null||p.Length!=9||int.Parse(p[0])!=i||int.Parse(p[1])!=j||int.Parse(p[2])!=g||int.Parse(p[3])!=swap)throw new InvalidDataException("dual replay missing or misordered match");
            string error=Encoding.UTF8.GetString(Convert.FromBase64String(p[8]));
            return new Outcome{FirstWon=p[4]=="-1"?(bool?)null:p[4]=="1",HitLimit=p[5]=="1",ReachedLimit=p[6]=="1",Turns=int.Parse(p[7]),Error=error.Length==0?null:error};
        }
        public sealed class Dual
        {
            public CharacterDefinition Primary;
            public CharacterDefinition Secondary;

            /// <summary>阵营序列（守序 / 中立 / 混乱）。</summary>
            public string Axis;

            public string Name => Primary.DisplayName + "+" + Secondary.DisplayName;

            public override string ToString() => Name;
        }

        /// <summary>按当前阵营表动态列出全部同阵营双将；兼容性与选将资格分别检查。</summary>
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
        public static Outcome Play(Dual first, Dual second, bool firstIsA, int seed, bool historicalMechanicsOnly = false)
        {
            var result = new Outcome();

            try
            {
                Dual a = firstIsA ? first : second;
                Dual b = firstIsA ? second : first;

                PlayerState pA = historicalMechanicsOnly ? HistoricalMechanicsPair("甲",a) : DualGeneralFactory.Create("甲", a.Primary, a.Secondary);
                PlayerState pB = historicalMechanicsOnly ? HistoricalMechanicsPair("乙",b) : DualGeneralFactory.Create("乙", b.Primary, b.Secondary);

                var choiceA = new GreedyChoiceProvider {UseChainBreak=true,UseValidatedTuning=true};
                var choiceB = new GreedyChoiceProvider {UseChainBreak=true,UseValidatedTuning=true};
                var game = new GameManager(pA, pB, choiceA, choiceB);
                game.StatusRandom=new Random(seed+71);
                StatusRules.ResetInteractionRandomForTest(seed+73);
                foreach(var player in new[]{pA,pB})foreach(var part in CharacterBehaviors.Implementers<ICharacterBehavior>(player))
                {
                    if(part is FingerDiceDuel.Characters.SpecialLogics.StormSorceryLogic storm)storm.Rng=new Random(seed+79);
                    if(part is FingerDiceDuel.Characters.SpecialLogics.SchoolOfIllusionLogic illusion)illusion.Rng=new Random(seed+83);
                    if(part is FingerDiceDuel.Characters.SpecialLogics.WildMagicLogic wild)wild.Rng=new Random(seed+89);
                }

                // ⚠⚠ **回合上限必须放宽到 300**。
                //
                //    默认上限是 50，而实测：只要双方都有野蛮人·巨人道途（各 +2 防、+5 生命），
                //    互相打不动，平均需要 **166 回合**才分出胜负。
                //    限在 50 会把它们全部截断成平局，而平局在胜率里算半负 ——
                //    于是“野蛮人·野兽道途+野蛮人·巨人道途”看起来只有 23.7%，放开后实际是 98.8% 胜。
                // 玩家要求：所有模式的轮次上限统一为 50 轮。
                game.RoundLimit = GameManager.DefaultRoundLimit;

                PrepareStart(game, seed);

                // ⚠️ 上限已改成按**轮**算（1 轮 = 双方各行动一次 = 2 个回合），
                //    所以模拟器的行动次数上限必须大于“轮数 x 每轮行动数” ——
                //    否则会出现“模拟器被迫中断”（改成轮口径后实测踩到 40+ 条）。
                var runner = new MatchRunner { RecordHistory = false, MaxTurns = 700 };
                MatchResult r = runner.Run(game, choiceA, choiceB);

                result.HitLimit = r.HitTurnLimit;
                result.ReachedLimit = r.ReachedTurnLimit;
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

        // 仅供保留已跨阵营的历史死锁现场；正式选将与全量组合仍严格走工厂校验。
        private static PlayerState HistoricalMechanicsPair(string name,Dual pair)
        {
            var primary=CharacterFactory.CreatePlayer("primary",pair.Primary);
            var secondary=CharacterFactory.CreatePlayer("secondary",pair.Secondary);
            var behavior=new DualGeneralBehavior(primary.Character,secondary.Character);
            behavior.SetNames(pair.Primary.DisplayName,pair.Secondary.DisplayName);
            var player=new PlayerState(name,behavior,Math.Max(primary.MaximumHealth,secondary.MaximumHealth));
            player.BindDefinition(pair.Primary);
            return player;
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

            // 武僧·太极宗的 5 要进入太极（与实机一致：开局那个 5 就是太极）
            WayOfTheTaiChiLogic.ApplyTaichiToFives(player);
        }

        /// <summary>累计战绩。</summary>
        public sealed class Tally
        {
            public int Games;
            public int Wins;

            /// <summary>真正的平局（轮次上限结算后两队/两人血量相同，或其他和平结束）。</summary>
            public int Draws;

            /// <summary>打满轮次上限的局数（其中大多数会按血量判出胜负，只有相同才进 Draws）。</summary>
            public int LimitHits;

            /// <summary>胜率（分母包含平局，供参考）。</summary>
            public double Rate => Games == 0 ? 0 : Wins / (double)Games;

            /// <summary>
            /// **决出胜负那些局里的胜率**。
            ///
            /// 为何看这个：真实平局只是少数；打满 50 轮的局已按血量判胜，
            /// 不能再当成“没分出胜负”从分母里剔除。
            /// </summary>
            public double DecisiveRate
            {
                get
                {
                    // 分母 = 真正分出胜负的局数 = 总局数 - 平局。
                    // ⚠️ 打满上限的局**已经按血量判出胜负**（除血量相同），
                    //    不能再从分母里减掉 LimitHits。
                    int decisive = Games - Draws;
                    return decisive <= 0 ? 0 : Wins / (double)decisive;
                }
            }

            /// <summary>打满上限的比例（越高说明这组越拖）。</summary>
            public double LimitRate => Games == 0 ? 0 : LimitHits / (double)Games;

            /// <summary>真实平局率（血量相同 / 其他和平结束）。</summary>
            public double DrawRate => Games == 0 ? 0 : Draws / (double)Games;
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
            using var replay=OpenReplay(gamesPerPair,duals);

            var dualTally = new Dictionary<string, Tally>();
            var roleTally = new Dictionary<string, Tally>();
            var axisTally = new Dictionary<string, Tally>();

            var anomalies = new List<string>();

            for (int i = 0; i < duals.Count; i++) dualTally[duals[i].Name] = new Tally();
            for (int i = 0; i < duals.Count; i++)
            {
                RoleOf(roleTally, duals[i].Primary);
                RoleOf(roleTally, duals[i].Secondary);
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
                            Outcome o = replay==null?Play(first, second, firstIsA, seed):ReadOutcome(replay,i,j,g,swap);
                            games++;

                            if (o.Error != null)
                            {
                                if (anomalies.Count < 40)
                                    anomalies.Add($"{first.Name} vs {second.Name} (seed={seed})：{o.Error}");
                                continue;
                            }

                            if (o.HitLimit)
                            {
                                // 模拟器被迫中断 —— 这才是真异常
                                if (anomalies.Count < 40)
                                    anomalies.Add($"{first.Name} vs {second.Name} (seed={seed})：模拟器被迫中断");
                            }

                            // ⚠️ **场次必须在所有分支之前记** —— 所有分支都用同一份 Games。
                            dualTally[first.Name].Games++;
                            dualTally[second.Name].Games++;
                            AddRoleGame(roleTally, first);
                            AddRoleGame(roleTally, second);
                            if (crossAxis)
                            {
                                axisTally[first.Axis].Games++;
                                axisTally[second.Axis].Games++;
                            }

                            // 打满 50 轮：**按血量判定胜负**，只有血量相同才是平局。
                            // LimitHits 仍记录"打满上限"的频率，但它不再等于平局。
                            if (o.ReachedLimit)
                            {
                                dualTally[first.Name].LimitHits++;
                                dualTally[second.Name].LimitHits++;
                                AddRoleLimit(roleTally, first);
                                AddRoleLimit(roleTally, second);
                                if (crossAxis)
                                {
                                    axisTally[first.Axis].LimitHits++;
                                    axisTally[second.Axis].LimitHits++;
                                }
                            }

                            // a 就是先手方（firstIsA 已按需要交换过）
                            Dual a = firstIsA ? first : second;
                            Dual b = firstIsA ? second : first;

                            if (o.FirstWon == null)
                            {
                                dualTally[first.Name].Draws++;
                                dualTally[second.Name].Draws++;
                                AddRoleDraw(roleTally, first);
                                AddRoleDraw(roleTally, second);
                                if (crossAxis)
                                {
                                    axisTally[first.Axis].Draws++;
                                    axisTally[second.Axis].Draws++;
                                }
                                continue;
                            }

                            Dual winner = o.FirstWon.Value ? a : b;
                            dualTally[winner.Name].Wins++;
                            AddRoleWin(roleTally, winner);
                            if (crossAxis) axisTally[winner.Axis].Wins++;
                        }
                    }
                }
            }

            if(replay!=null&&replay.ReadLine()!=null)throw new InvalidDataException("dual replay has extra matches");
            totalGames = games;
            return Report(duals, dualTally, roleTally, axisTally, gamesPerPair, games, anomalies);
        }

        /// <summary>给某个双将里的两名角色各记一次场次。</summary>
        private static void AddRoleGame(Dictionary<string, Tally> map, Dual dual)
        {
            RoleOf(map, dual.Primary).Games++;
            RoleOf(map, dual.Secondary).Games++;
        }

        /// <summary>给某个双将里的两名角色各记一次"打满上限"（不重复记场次）。</summary>
        private static void AddRoleLimit(Dictionary<string, Tally> map, Dual dual)
        {
            RoleOf(map, dual.Primary).LimitHits++;
            RoleOf(map, dual.Secondary).LimitHits++;
        }

        /// <summary>给某个双将里的两名角色各记一次真实平局。</summary>
        private static void AddRoleDraw(Dictionary<string, Tally> map, Dual dual)
        {
            RoleOf(map, dual.Primary).Draws++;
            RoleOf(map, dual.Secondary).Draws++;
        }

        /// <summary>给某个双将里的两名角色各记一次胜场。</summary>
        private static void AddRoleWin(Dictionary<string, Tally> map, Dual dual)
        {
            RoleOf(map, dual.Primary).Wins++;
            RoleOf(map, dual.Secondary).Wins++;
        }

        private static Tally RoleOf(Dictionary<string, Tally> map, CharacterDefinition def)
        {
            string key = def.DisplayName;
            if (!map.TryGetValue(key, out Tally t))
            {
                t = new Tally();
                map[key] = t;
            }

            return t;
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
            sb.AppendLine("角色        胜     负   场次   决胜胜率   打满上限");
            var roles = new List<KeyValuePair<string, Tally>>(roleTally);
            roles.Sort((x, y) => y.Value.DecisiveRate.CompareTo(x.Value.DecisiveRate));
            for (int i = 0; i < roles.Count; i++)
            {
                Tally t = roles[i].Value;
                sb.AppendLine($"{roles[i].Key,-8}{t.Wins,6}{t.Games - t.Wins,6}{t.Games,6}"
                            + $"   {t.DecisiveRate * 100,6:F1}%   {t.LimitRate * 100,6:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine("--- 阵营胜率（只统计外战：不同阵营之间的对局）---");
            sb.AppendLine("阵营      胜 / 场次    决胜胜率   打满上限");
            string[] order = { "守序", "中立", "混乱" };
            for (int k = 0; k < order.Length; k++)
            {
                Tally t = axisTally[order[k]];
                sb.AppendLine($"{order[k],-6}{t.Wins,6} / {t.Games,-6}   {t.DecisiveRate * 100,6:F1}%"
                            + $"   {t.LimitRate * 100,6:F1}%   （平 {t.Draws}）");
            }

            sb.AppendLine();
            sb.AppendLine("--- 双将胜率 ---");
            var all = new List<KeyValuePair<string, Tally>>(dualTally);
            all.Sort((x, y) => y.Value.Rate.CompareTo(x.Value.Rate));

            sb.AppendLine("  最强 15（按决胜胜率）：");
            var byDecisive = new List<KeyValuePair<string, Tally>>(dualTally);
            byDecisive.Sort((x, y) => y.Value.DecisiveRate.CompareTo(x.Value.DecisiveRate));
            for (int i = 0; i < byDecisive.Count && i < 15; i++)
                sb.AppendLine($"    {i + 1,2}. {byDecisive[i].Key,-16}"
                            + $"{byDecisive[i].Value.DecisiveRate * 100,5:F1}%"
                            + $"  打满 {byDecisive[i].Value.LimitRate * 100,5:F1}%"
                            + $"  （{byDecisive[i].Value.Wins}/{byDecisive[i].Value.Games}）");

            sb.AppendLine("  最弱 15（按决胜胜率）：");
            for (int i = Math.Max(0, byDecisive.Count - 15); i < byDecisive.Count; i++)
                sb.AppendLine($"    {i + 1,2}. {byDecisive[i].Key,-16}"
                            + $"{byDecisive[i].Value.DecisiveRate * 100,5:F1}%"
                            + $"  打满 {byDecisive[i].Value.LimitRate * 100,5:F1}%"
                            + $"  （{byDecisive[i].Value.Wins}/{byDecisive[i].Value.Games}）");

            var byDraw = new List<KeyValuePair<string, Tally>>(dualTally);
            byDraw.Sort((x, y) => y.Value.DrawRate.CompareTo(x.Value.DrawRate));

            sb.AppendLine("  最容易平局的 10 组（真实平局：血量相同 / 其他和平结束）：");
            for (int i = 0; i < byDraw.Count && i < 10; i++)
                sb.AppendLine($"    {i + 1,2}. {byDraw[i].Key,-16}"
                            + $"平局率 {byDraw[i].Value.DrawRate * 100,5:F1}%");

            int limitTotal = 0;
            foreach (var kv in dualTally) limitTotal += kv.Value.LimitHits;

            sb.AppendLine();
            sb.AppendLine($"打满轮次上限合计 {limitTotal / 2} 局（其中大部分按血量判出胜负）");

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
