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
    /// 8 人身份局抽样：各身份胜率，以及每个身份下各角色的胜率。
    ///
    /// 口径：
    ///   · 2,000 局，全部 AI，无人类；
    ///   · 主公固定 1 号位、体力 +5；其余身份随机；
    ///   · 8 个角色不重复；
    ///   · 身份胜负按"阵营获胜"给该阵营**所有角色**记胜（阵亡也算阵营胜）；
    ///   · 反贼全灭后主公死亡仍判反贼胜，按规则计入。
    /// </summary>
    [TestFixture]
    public class IdentityBalanceTests
    {
        private const int Games = 2000;

        private sealed class ShieldAi : Assistant.IAiChoiceResolver
        {
            public Hand ChooseShieldToBlock(PlayerState defender,
                                            IReadOnlyList<Hand> candidates)
                => candidates != null && candidates.Count > 0 ? candidates[0] : null;
        }

        private static readonly string[] RoleNames = { "？", "主公", "忠臣", "反贼", "内奸" };

        private static void ApplyOpeningHands(PlayerState p, Random rng)
        {
            int roll = DiceRollSetup.Roll(rng);
            IReadOnlyList<HandSplit> splits = DiceRollSetup.DistinctSplits(roll);
            if (splits.Count == 0) return;
            HandSplit split = new DiceSplitAdvisor().Choose(p.Definition, null, splits);
            p.LeftHand.SetValue(split.Left);
            p.RightHand.SetValue(split.Right);
            MonkLogic.ApplyTaichiToFives(p);
        }

        private static bool LordSide(IdentityRole role)
            => role == IdentityRole.Lord || role == IdentityRole.Loyalist;

        private static bool WinningSide(IdentityRole winner, IdentityRole role)
        {
            if (winner == IdentityRole.None) return false;
            if (winner == IdentityRole.Rebel) return role == IdentityRole.Rebel;
            if (winner == IdentityRole.Traitor) return role == IdentityRole.Traitor;
            return LordSide(role);
        }

        [Test]
        public void Identity_role_and_character_win_rates_2000_games()
        {
            var rng = new Random(20261018);
            var roleGames = new Dictionary<IdentityRole, int>();
            var roleWins = new Dictionary<IdentityRole, int>();
            var charGames = new Dictionary<string, int>();
            var charWins = new Dictionary<string, int>();
            var roleCharGames = new Dictionary<string, int>();
            var roleCharWins = new Dictionary<string, int>();

            int draws = 0, stalls = 0, errors = 0;
            string firstError = null;
            int lordSurvived = 0;

            for (int g = 0; g < Games; g++)
            {
                List<CharacterDefinition> defs = PickCharacters(rng);
                IdentityRole[] roles = PickRoles(rng);

                var seated = new List<PlayerState>(8);
                var providers = new List<IChoiceProvider>(8);
                var selectors = new List<IMoveSelector>(8);
                var sinks = new List<SimpleChoiceProvider>(8);

                for (int i = 0; i < 8; i++)
                {
                    PlayerState p = CharacterFactory.CreatePlayer("P" + (i + 1), defs[i]);
                    var ai = new GreedyChoiceProvider();
                    var sink = new SimpleChoiceProvider();
                    seated.Add(p); providers.Add(ai); selectors.Add(ai); sinks.Add(sink);
                }

                GameManager game = GameManager.CreateIdentityEight(seated, providers, roles);
                game.RoundLimit = GameManager.DefaultRoundLimit;
                for (int i = 0; i < 8; i++) ApplyOpeningHands(seated[i], rng);

                var controller = new BoardController(game, sinks[0], sinks[1]);
                for (int i = 0; i < 8; i++)
                    controller.BindPlayer(seated[i], selectors[i], sinks[i]);
                controller.AiResolver = new ShieldAi();
                controller.SetFirstPlayer(seated[0]);
                controller.BeginTurn();

                int steps = 0;
                try
                {
                    while (!game.IsGameOver && steps < 2000)
                    {
                        if (controller.BardChoicePending)
                            controller.AutoResolveAiBardChoice();
                        if (!controller.MakeAutomaticMove())
                        {
                            if (controller.CanSkipTurn && controller.SkipTurn())
                            {
                                steps++;
                                continue;
                            }
                            stalls++;
                            break;
                        }
                        steps++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    if (firstError == null) firstError = ex.ToString();
                    continue;
                }

                bool resolved = game.IsGameOver;
                IdentityRole winner = game.WinningIdentity;
                if (!resolved || winner == IdentityRole.None) draws++;
                if (resolved && seated[0].IsAlive) lordSurvived++;

                for (int i = 0; i < 8; i++)
                {
                    IdentityRole role = roles[i];
                    string key = ((int)role) + "|" + defs[i].DisplayName;

                    roleGames.TryGetValue(role, out int rg);
                    roleGames[role] = rg + 1;
                    charGames.TryGetValue(defs[i].DisplayName, out int cg);
                    charGames[defs[i].DisplayName] = cg + 1;
                    roleCharGames.TryGetValue(key, out int rcg);
                    roleCharGames[key] = rcg + 1;

                    if (!resolved || !WinningSide(winner, role)) continue;
                    roleWins.TryGetValue(role, out int rw);
                    roleWins[role] = rw + 1;
                    charWins.TryGetValue(defs[i].DisplayName, out int cw);
                    charWins[defs[i].DisplayName] = cw + 1;
                    roleCharWins.TryGetValue(key, out int rcw);
                    roleCharWins[key] = rcw + 1;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine("=== 8 人身份局抽样统计 ===");
            sb.AppendLine($"总对局 {Games}，异常 {errors}，模拟器强停 {stalls}，未分出胜负 {draws}");
            if (firstError != null) sb.AppendLine("首个异常：" + firstError);
            sb.AppendLine($"主公存活结束 {lordSurvived} 局");
            sb.AppendLine();

            sb.AppendLine("身份        胜 / 场次    身份胜率");
            foreach (IdentityRole role in new[]
                     { IdentityRole.Lord, IdentityRole.Loyalist,
                       IdentityRole.Rebel, IdentityRole.Traitor })
            {
                roleGames.TryGetValue(role, out int games);
                roleWins.TryGetValue(role, out int wins);
                sb.AppendLine($"{RoleNames[(int)role],-6}{wins,6} / {games,-6}" +
                              $"{wins * 100.0 / Math.Max(1, games),8:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine("角色总胜率（所有身份合计）：");
            var chars = new List<string>(charGames.Keys);
            chars.Sort((a, b) =>
            {
                charWins.TryGetValue(a, out int wa); charWins.TryGetValue(b, out int wb);
                charGames.TryGetValue(a, out int ga); charGames.TryGetValue(b, out int gb);
                return (wb / (double)Math.Max(1, gb)).CompareTo(wa / (double)Math.Max(1, ga));
            });
            foreach (string c in chars)
            {
                charGames.TryGetValue(c, out int games);
                charWins.TryGetValue(c, out int wins);
                sb.AppendLine($"{c,-8}{wins,6} / {games,-6}" +
                              $"{wins * 100.0 / Math.Max(1, games),8:F1}%");
            }

            sb.AppendLine();
            sb.AppendLine("各身份角色胜率：");
            foreach (IdentityRole role in new[]
                     { IdentityRole.Lord, IdentityRole.Loyalist,
                       IdentityRole.Rebel, IdentityRole.Traitor })
            {
                sb.AppendLine();
                sb.AppendLine($"--- {RoleNames[(int)role]} ---");
                var keys = new List<string>();
                foreach (var kv in roleCharGames)
                    if (kv.Key.StartsWith(((int)role) + "|", StringComparison.Ordinal))
                        keys.Add(kv.Key);

                keys.Sort((a, b) =>
                {
                    roleCharWins.TryGetValue(a, out int wa); roleCharWins.TryGetValue(b, out int wb);
                    roleCharGames.TryGetValue(a, out int ga); roleCharGames.TryGetValue(b, out int gb);
                    return (wb / (double)Math.Max(1, gb)).CompareTo(wa / (double)Math.Max(1, ga));
                });

                foreach (string key in keys)
                {
                    string name = key.Substring(key.IndexOf('|') + 1);
                    roleCharGames.TryGetValue(key, out int games);
                    roleCharWins.TryGetValue(key, out int wins);
                    sb.AppendLine($"{name,-8}{wins,6} / {games,-6}" +
                                  $"{wins * 100.0 / Math.Max(1, games),8:F1}%");
                }
            }

            Console.WriteLine(sb.ToString());
            System.IO.File.WriteAllText("Tools/identity-report.txt", sb.ToString());

            Assert.True(errors == 0, "身份局抽样不应出现异常：" + firstError);
        }

        private static List<CharacterDefinition> PickCharacters(Random rng)
        {
            var pool = new List<CharacterDefinition>();
            foreach (CharacterDefinition d in CharacterDefinition.BuiltIn)
                if (CharacterFactory.IsRegistered(d.Logic)) pool.Add(d);

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                CharacterDefinition t = pool[i]; pool[i] = pool[j]; pool[j] = t;
            }

            var picked = new List<CharacterDefinition>(8);
            for (int i = 0; i < 8; i++) picked.Add(pool[i]);
            return picked;
        }

        private static IdentityRole[] PickRoles(Random rng)
        {
            var roles = new IdentityRole[8];
            roles[0] = IdentityRole.Lord;
            var rest = new List<IdentityRole>(7)
            {
                IdentityRole.Loyalist, IdentityRole.Loyalist,
                IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Traitor,
            };
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                IdentityRole t = rest[i]; rest[i] = rest[j]; rest[j] = t;
            }
            for (int i = 0; i < 7; i++) roles[i + 1] = rest[i];
            return roles;
        }
    }
}
