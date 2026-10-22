using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>战士·战术家：按场上最高与最低体力差获得额外攻防。</summary>
    public sealed class TacticianLogic : CharacterTemplate, IDynamicStats
    {
        public override string DisplayName => "战士·战术家";
        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => 1;

        public void RefreshDynamicStats(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return;
            int min = int.MaxValue, max = int.MinValue;
            for (int i = 0; i < manager.Players.Count; i++)
            {
                PlayerState p = manager.Players[i];
                if (p == null || !p.IsAlive) continue;
                if (p.Health < min) min = p.Health;
                if (p.Health > max) max = p.Health;
            }
            int x = min == int.MaxValue ? 0 : (max - min) / 3;
            owner.SetDynamicBonuses(x, x);
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Slashing;
    }
}
