using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public abstract class MonkLogic : CharacterTemplate, IMonkResource
    {
        protected PlayerState MonkOwner;
        public int Ki=>MonkOwner?.MonkKi??2;
        public const int MaxKi=6;
        public void BindMonkOwner(PlayerState owner)=>MonkOwner=owner;
        protected bool Spend(int amount)=>MonkOwner?.TrySpendMonkKi(amount)==true;
        public override int DefaultAttackBonus=>0;
        public override int DefaultDefenseBonus=>0;
        public override int BaseAttackRange=>1;
        public override AttackDamageType AttackTypeOf(AttackContext attack)=>AttackDamageType.Bludgeoning;
        public override void OnMatchReset(PlayerState owner,GameManager game){BindMonkOwner(owner);owner.ResetMonkKi();}
    }
}
