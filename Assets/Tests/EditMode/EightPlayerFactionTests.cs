using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **8 人阵营混战**的抽样统计台（全部 AI）。
    ///
    /// 24 个角色不可能做全组合；这里每局随机抽 8 个不重复角色，
    /// 按阵营组成 **3 / 3 / 2** 三队，跑 2,000 局。
    /// </summary>
    [TestFixture]
    public class EightPlayerFactionTests
    {
        private const int Games = 2000;

        private static readonly string[] FactionNames = { "守序", "中立", "混乱" };

        private static int FactionOf(CharacterDefinition def)
        {
            CodexEntry entry = CharacterCodex.For(def);
            string axis = entry == null ? "中立" : Alignments.LawAxisOf(entry.Alignment);
            if (axis == "守序") return 0;
            if (axis == "混乱") return 2;
            return 1;
        }

        private static List<CharacterDefinition> PickEight(Random rng, out List<int> teams)
        {
            var byFaction = new List<CharacterDefinition>[3];
            for (int i = 0; i < 3; i++) byFaction[i] = new List<CharacterDefinition>();
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
                byFaction[FactionOf(def)].Add(def);

            int weak = rng.Next(3);             // 2 人的阵营
            int[] counts = { 3, 3, 3 };
            counts[weak] = 2;

            var picked = new List<CharacterDefinition>(8);
            teams = new List<int>(8);

            for (int f = 0; f < 3; f++)
            {
                List<CharacterDefinition> pool = byFaction[f];
                // Fisher-Yates
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    CharacterDefinition tmp = pool[i]; pool[i] = pool[j]; pool[j] = tmp;
                }

                for (int k = 0; k < counts[f]; k++)
                {
                    picked.Add(pool[k]);
                    teams.Add(f);
                }
            }

            // 打乱座位
            for (int i = picked.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                CharacterDefinition d = picked[i]; picked[i] = picked[j]; picked[j] = d;
                int t = teams[i]; teams[i] = teams[j]; teams[j] = t;
            }

            return picked;
        }

        private static void ApplyOpeningHands(PlayerState player, Random rng)
        {
            int roll = DiceRollSetup.Roll(rng);
            IReadOnlyList<HandSplit> splits = DiceRollSetup.DistinctSplits(roll);
            if (splits.Count == 0) return;

            HandSplit split = new DiceSplitAdvisor().Choose(player.Definition, null, splits);
            player.LeftHand.SetValue(split.Left);
            player.RightHand.SetValue(split.Right);
            MonkLogic.ApplyTaichiToFives(player);
        }

        [Test]
        public void Eight_player_faction_battle_2000_games()
        {
            var rng = new Random(20260213);
            var roleGames = new Dictionary<string, int>();
            var roleWins = new Dictionary<string, int>();
            var factionGames = new int[3];
            var factionWins = new int[3];
            var factionWeakGames = new int[3];
            var factionWeakWins = new int[3];

            int totalTurns = 0;
            int draws = 0;
            int errors = 0;
            string firstError = null;

            for (int g = 0; g < Games; g++)
            {
                List<CharacterDefinition> defs = PickEight(rng, out List<int> teams);

                var seated = new List<PlayerState>(8);
                var providers = new List<IChoiceProvider>(8);
                var selectors = new List<IMoveSelector>(8);

                int weakFaction = -1;
                for (int f = 0; f < 3; f++)
                {
                    int count = 0;
                    for (int i = 0; i < 8; i++) if (teams[i] == f) count++;
                    if (count == 2) weakFaction = f;
                }

                for (int i = 0; i < 8; i++)
                {
                    PlayerState p = CharacterFactory.CreatePlayer(defs[i].DisplayName, defs[i]);
                    var ai = new GreedyChoiceProvider();
                    seated.Add(p);
                    providers.Add(ai);
                    selectors.Add(ai);

                    roleGames.TryGetValue(defs[i].DisplayName, out int n);
                    roleGames[defs[i].DisplayName] = n + 1;
                    factionGames[teams[i]]++;
                    if (teams[i] == weakFaction) factionWeakGames[teams[i]]++;
                }

                GameManager game = GameManager.CreateEight(seated, providers, teams);
                game.RoundLimit = GameManager.DefaultRoundLimit;

                for (int i = 0; i < 8; i++) ApplyOpeningHands(seated[i], rng);

                try
                {
                    RunEight(game, selectors);
                }
                catch (Exception ex)
                {
                    errors++;
                    if (firstError == null) firstError = ex.ToString();
                    continue;
                }

                totalTurns += game.TurnCount;
                if (game.Winner == null)
                {
                    draws++;
                    continue;
                }

                int winnerTeam = game.WinningTeam;
                if (winnerTeam < 0) { draws++; continue; }
                factionWins[winnerTeam]++;
                if (winnerTeam == weakFaction) factionWeakWins[winnerTeam]++;

                for (int i = 0; i < 8; i++)
                {
                    if (teams[i] != winnerTeam) continue;
                    roleWins.TryGetValue(defs[i].DisplayName, out int w);
                    roleWins[defs[i].DisplayName] = w + 1;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 8 人阵营混战抽样统计 ===");
            sb.AppendLine($"总对局 {Games}，平均行动 {totalTurns / (double)Games:F1}，平局 {draws}，异常 {errors}");
            if (firstError != null) sb.AppendLine("首个异常：" + firstError);
            sb.AppendLine();

            sb.AppendLine("阵营        胜 / 局数    胜率（按局）  作为 2 人阵营时");
            for (int f = 0; f < 3; f++)
                sb.AppendLine($"{FactionNames[f],-6}{factionWins[f],6} / {Games,-6}" +
                              $"{factionWins[f] * 100.0 / Games,10:F1}%" +
                              $"{factionWeakWins[f],8} / {factionWeakGames[f],-5}" +
                              $"{factionWeakWins[f] * 100.0 / Math.Max(1, factionWeakGames[f]),7:F1}%");

            sb.AppendLine();
            sb.AppendLine("角色        胜 / 场次    胜率");
            var rows = new List<KeyValuePair<string, int>>(roleGames);
            rows.Sort((a, b) =>
            {
                double ra = roleWins.TryGetValue(a.Key, out int wa) ? wa / (double)a.Value : 0;
                double rb = roleWins.TryGetValue(b.Key, out int wb) ? wb / (double)b.Value : 0;
                return rb.CompareTo(ra);
            });

            foreach (var kv in rows)
            {
                roleWins.TryGetValue(kv.Key, out int wins);
                sb.AppendLine($"{kv.Key,-8}{wins,6} / {kv.Value,-6}" +
                              $"{wins * 100.0 / Math.Max(1, kv.Value),6:F1}%");
            }

            Console.WriteLine(sb.ToString());
            System.IO.File.WriteAllText("Tools/8p-report.txt", sb.ToString());
        }

        /// <summary>8 人局的整局驱动：与 MatchRunner 同职责，但支持任意人数。</summary>
        private static Dictionary<PlayerState, int> SnapshotHealth(GameManager game)
        {
            var hp = new Dictionary<PlayerState, int>(game.Players.Count);
            for (int i = 0; i < game.Players.Count; i++)
                if (game.Players[i] != null) hp[game.Players[i]] = game.Players[i].Health;
            return hp;
        }

        /// <summary>执行一次击杀奖惩队列；额外行动不推进 TurnCount / RoundCount。</summary>
        private static void RunKillRewards(GameManager game,
                                           List<IMoveSelector> selectors,
                                           List<PlayerState> queue)
        {
            int safety = 64;
            while (queue.Count > 0 && !game.IsGameOver && safety-- > 0)
            {
                PlayerState actor = queue[0];
                queue.RemoveAt(0);
                if (actor == null || !actor.IsAlive) continue;

                game.ForceActivePlayerForTest(actor);
                game.BeginTurn();
                game.ResolveStatusTurnStart(actor);
                if (game.IsGameOver) break;
                if (!ReferenceEquals(game.CurrentPlayer, actor)) continue;

                IMoveSelector selector = selectors[game.SeatOf(actor) - 1];
                LegalMove move = selector.ChooseMove(game, actor);
                if (move.Actor == null)
                {
                    if (game.CanSkipTurnFor(actor)) game.SkipTurn();
                    continue;
                }

                Dictionary<PlayerState, int> before = SnapshotHealth(game);
                TurnResult turn = game.ExecuteTurn(actor, move.ActedHand, move.TargetHand,
                                                   extraAction: true);
                List<PlayerState> nested = KillRewardPlanner.Build(game, turn, before);
                if (nested.Count > 0) queue.InsertRange(0, nested);

                game.ResolveStatusTurnEnd(actor);
                for (int i = 0; i < game.Players.Count; i++)
                    BardAi.TryActivate(game.Players[i], game);
            }
        }

        private static void RunEight(GameManager game, List<IMoveSelector> selectors)
        {
            int safety = 2000;
            while (!game.IsGameOver && safety-- > 0)
            {
                game.BeginTurn();
                PlayerState actor = game.CurrentPlayer;
                if (actor == null) break;

                game.ResolveStatusTurnStart(actor);
                if (game.IsGameOver) break;
                if (!ReferenceEquals(game.CurrentPlayer, actor)) continue;

                IMoveSelector selector = selectors[game.SeatOf(actor) - 1];
                LegalMove move = selector.ChooseMove(game, actor);
                if (move.Actor == null)
                {
                    if (game.CanSkipTurnFor(actor))
                    {
                        game.SkipTurn();
                        continue;
                    }
                    break;
                }

                Dictionary<PlayerState, int> before = SnapshotHealth(game);
                TurnResult turn = game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
                PlayerState resume = game.CurrentPlayer;

                if (KillRewardPlanner.IsEnabled(game))
                {
                    List<PlayerState> rewards = KillRewardPlanner.Build(game, turn, before);
                    if (rewards.Count > 0 && !game.IsGameOver)
                    {
                        RunKillRewards(game, selectors, rewards);
                        if (!game.IsGameOver && resume != null)
                            game.ForceActivePlayerForTest(resume);
                    }
                }

                game.ResolveStatusTurnEnd(actor);
                for (int i = 0; i < game.Players.Count; i++)
                    BardAi.TryActivate(game.Players[i], game);
            }
        }
    }
}
