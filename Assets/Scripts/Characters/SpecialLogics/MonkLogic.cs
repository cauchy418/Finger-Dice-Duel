using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public abstract class MonkLogic : CharacterTemplate, IMonkResource
    {
        protected PlayerState MonkOwner;
        public int Ki=>MonkOwner?.MonkKi??2;
        public const int MaxKi=6;
        public void BindMonkOwner(PlayerState owner)=>MonkOwner=owner;
        private GameManager _visualGame;
        protected bool Spend(int amount)
        {int before=Ki;if(MonkOwner?.TrySpendMonkKi(amount)!=true)return false;_visualGame?.RecordSkillVisual(MonkOwner,"气",MonkOwner,new SkillVisualData(value:Ki,previous:before,variant:"spend"));return true;}
        public override int DefaultAttackBonus=>0;
        public override int DefaultDefenseBonus=>0;
        public override int BaseAttackRange=>1;
        public override AttackDamageType AttackTypeOf(AttackContext attack)=>AttackDamageType.Bludgeoning;
        public override void OnMatchReset(PlayerState owner,GameManager game){_visualGame=game;BindMonkOwner(owner);owner.ResetMonkKi();}
    }
}
