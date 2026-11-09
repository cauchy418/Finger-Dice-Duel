using System;
namespace FingerDiceDuel.Core
{
    public interface IBarbarian {}
    public abstract class BarbarianTemplate:CharacterTemplate,IBarbarian
    {
        protected PlayerState BarbarianOwner { get; private set; }
        internal void BindBarbarianOwner(PlayerState owner) => BarbarianOwner = owner;
        public override int DefaultAttackBonus=>-1;
        public override int DefaultDefenseBonus=>-1;
        public override int MaxHealthBonus=>5;
        public override AttackDamageType AttackTypeOf(AttackContext c)=>AttackDamageType.Slashing;
    }
    public static class BarbarianRules
    {
        public static int MissingHealthBonus(PlayerState p)=>p!=null&&!p.IsPolymorphed&&!p.IsWildShaped&&CharacterBehaviors.FirstImplementer<IBarbarian>(p)!=null?Math.Max(0,p.MaximumHealth-p.Health)/10*2:0;
    }
}
