using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>身份局的实际阵营人数与已知关系分开计算；不修改可攻击关系或AI身份推测。</summary>
    public static class IdentityPresentation
    {
        public static bool SameSide(IdentityRole a, IdentityRole b)
            => IdentityRoleInfo.IsLordSide(a) && IdentityRoleInfo.IsLordSide(b)
                || a == IdentityRole.Rebel && b == IdentityRole.Rebel;

        public static void Counts(GameManager game, PlayerState viewer, out int teammates, out int opponents)
        {
            teammates = opponents = 0;
            if (game == null || viewer == null) return;
            foreach (var p in game.Players)
            {
                if (!p.IsOriginalAlive || ReferenceEquals(p, viewer)) continue;
                bool same = game.IsIdentityMode ? SameSide(game.IdentityOf(viewer), game.IdentityOf(p)) : game.OriginalTeamOf(viewer)==game.TeamOf(p);
                if (same) teammates++; else opponents++;
            }
        }

        public static RingRelation Relation(GameManager game, PlayerState viewer, PlayerState target)
        {
            if (game == null || viewer == null || target == null) return RingRelation.Unknown;
            if (ReferenceEquals(viewer, target)&&!target.IsSummon) return RingRelation.Self;
            if(target.IsSummon)return ReferenceEquals(viewer,target.SummonOwner)?RingRelation.Teammate:Relation(game,viewer,target.SummonOwner);
            if (!game.IsIdentityMode) return game.OriginalTeamOf(viewer)==game.TeamOf(target) ? RingRelation.Teammate : RingRelation.Opponent;
            // 内奸确定其他人都不是队友，无需暴露其具体身份。
            if (game.IdentityOf(viewer) == IdentityRole.Traitor) return RingRelation.Opponent;
            if (!game.IsIdentityPublic(target)) return RingRelation.Unknown;
            return SameSide(game.IdentityOf(viewer), game.IdentityOf(target)) ? RingRelation.Teammate : RingRelation.Opponent;
        }
    }
}
