using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>8 人身份局的身份。</summary>
    public enum IdentityRole
    {
        None = 0,
        Lord = 1,
        Loyalist = 2,
        Rebel = 3,
        Traitor = 4,
    }

    public static class IdentityRoleInfo
    {
        public static string DisplayName(IdentityRole role)
        {
            switch (role)
            {
                case IdentityRole.Lord: return "主公";
                case IdentityRole.Loyalist: return "忠臣";
                case IdentityRole.Rebel: return "反贼";
                case IdentityRole.Traitor: return "内奸";
                default: return "？";
            }
        }

        public static bool IsLordSide(IdentityRole role)
            => role == IdentityRole.Lord || role == IdentityRole.Loyalist;

        public static bool IsRebelSide(IdentityRole role)
            => role == IdentityRole.Rebel;

        public static bool IsTraitor(IdentityRole role)
            => role == IdentityRole.Traitor;
    }
    public sealed class IdentityDraftPreference
    {
        private readonly Dictionary<(IdentityRole, string), (int Wins, int Games)> _rates = new Dictionary<(IdentityRole, string), (int, int)>();
        private readonly Dictionary<IdentityRole, (int Wins, int Games)> _totals = new Dictionary<IdentityRole, (int, int)>();

        public IdentityDraftPreference(string table)
        {
            if (string.IsNullOrEmpty(table)) return;
            foreach (var line in table.Split('\n'))
            {
                var fields = line.Trim().Split('\t');
                if (fields.Length != 4 || !Enum.TryParse(fields[0], out IdentityRole role) || role == IdentityRole.None
                    || !int.TryParse(fields[2], out int wins) || !int.TryParse(fields[3], out int games)
                    || wins < 0 || wins > games || _rates.ContainsKey((role, fields[1]))) continue;
                _rates[(role, fields[1])] = (wins, games);
                _totals.TryGetValue(role, out var prior);
                _totals[role] = (prior.Wins + wins, prior.Games + games);
            }
        }

        public CharacterDefinition Choose(IdentityRole role, IReadOnlyList<CharacterDefinition> candidates, Random random)
        {
            if (candidates == null || candidates.Count == 0) return null;
            if (!_totals.TryGetValue(role, out var total) || total.Games <= 0)
                return candidates[random.Next(candidates.Count)];
            double baseline = (double)total.Wins / total.Games;
            const int priorGames = 24;
            var weights = new double[candidates.Count];
            double sum = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                _rates.TryGetValue((role, candidates[i].Id), out var rate);
                double winRate = (rate.Wins + baseline * priorGames) / (rate.Games + priorGames);
                weights[i] = Math.Exp(6 * (winRate - baseline));
                sum += weights[i];
            }
            double roll = random.NextDouble() * sum;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll < 0) return candidates[i];
            }
            return candidates[candidates.Count - 1];
        }
    }
}
