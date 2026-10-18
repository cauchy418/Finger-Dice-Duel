using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **8 人阵营混战**的击杀奖惩队列。
    ///
    /// 只在这个模式启用；1v1 / 2v2 / 双将即使出现队友击杀也不触发。
    /// 返回的行动列表按"立即插入队首"的顺序排好。
    /// </summary>
    public static class KillRewardPlanner
    {
        public static bool IsEnabled(GameManager game)
            => game != null && (game.IsEightPlayerFactionMode || game.IsIdentityMode);

        /// <summary>
        /// 根据本次结算和行动前血量快照，生成奖惩行动。
        /// 没有击杀 / 非直接攻击击杀 / 已分出胜负时返回空列表。
        /// </summary>
        public static List<PlayerState> Build(GameManager game, TurnResult result,
                                              IReadOnlyDictionary<PlayerState, int> healthBefore)
        {
            var rewards = new List<PlayerState>();
            if (!IsEnabled(game) || game.IsGameOver)
                return rewards;

            if (game.IsIdentityMode)
                return BuildIdentity(game, result, healthBefore);
            if (result?.Actor == null || result.Defender == null)
                return rewards;
            if (healthBefore == null
                || !healthBefore.TryGetValue(result.Defender, out int before)
                || before <= 0)
                return rewards;
            if (result.Defender.IsAlive)
                return rewards;

            // 只认直接攻击击杀（含追加攻击）；状态伤害 / 反弹不计。
            if (result.DamageDealt <= 0
                && !(result.ExtraAttack.HasValue && result.ExtraAttack.Value.Dealt > 0))
                return rewards;

            PlayerState killer = result.Actor;
            PlayerState victim = result.Defender;
            if (ReferenceEquals(killer, victim) || !killer.IsAlive)
                return rewards;

            int killerTeam = game.TeamOf(killer);
            int victimTeam = game.TeamOf(victim);
            if (killerTeam < 0 || victimTeam < 0)
                return rewards;

            if (victimTeam != killerTeam)
            {
                // 击杀不同阵营：X = 受害者阵营**击杀后**存活人数。
                int x = game.AliveTeamCount(victimTeam);
                for (int i = 0; i < x; i++) rewards.Add(killer);
            }
            else
            {
                // 击杀同阵营：从击杀者下家开始，其他阵营每个存活角色各一次。
                int n = game.Players.Count;
                int start = game.SeatOf(killer) - 1;
                for (int step = 1; step <= n; step++)
                {
                    PlayerState p = game.Players[(start + step) % n];
                    if (p == null || !p.IsAlive) continue;
                    if (game.TeamOf(p) == killerTeam) continue;
                    rewards.Add(p);
                }
            }

            return rewards;
        }

        /// <summary>
        /// 身份局奖惩：
        ///   · 任何玩家击杀反贼 -> 额外行动 1 次；
        ///   · 主公击杀忠臣 -> 从主公下家开始，所有存活反贼各行动一次。
        /// </summary>
        private static List<PlayerState> BuildIdentity(
            GameManager game, TurnResult result,
            IReadOnlyDictionary<PlayerState, int> healthBefore)
        {
            var rewards = new List<PlayerState>();
            if (result?.Actor == null || result.Defender == null) return rewards;
            if (!healthBefore.TryGetValue(result.Defender, out int before) || before <= 0)
                return rewards;
            if (result.Defender.IsAlive) return rewards;
            if (result.DamageDealt <= 0
                && !(result.ExtraAttack.HasValue && result.ExtraAttack.Value.Dealt > 0))
                return rewards;

            IdentityRole killerRole = game.IdentityOf(result.Actor);
            IdentityRole victimRole = game.IdentityOf(result.Defender);

            if (victimRole == IdentityRole.Rebel)
            {
                rewards.Add(result.Actor);
                return rewards;
            }

            if (killerRole == IdentityRole.Lord && victimRole == IdentityRole.Loyalist)
            {
                int n = game.Players.Count;
                int start = game.SeatOf(result.Actor) - 1;
                for (int step = 1; step <= n; step++)
                {
                    PlayerState p = game.Players[(start + step) % n];
                    if (p == null || !p.IsAlive) continue;
                    if (game.IdentityOf(p) == IdentityRole.Rebel) rewards.Add(p);
                }
            }

            return rewards;
        }
    }
}
