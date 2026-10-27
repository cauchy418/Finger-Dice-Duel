using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class TrickeryDomainLogic:CharacterTemplate,ITrickeryDomain
    {
        public override string DisplayName=>"牧师·诡术领域";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext c)=>ArcaneChoices.NearFar(c);
    }
    public sealed class TempestDomainLogic:CharacterTemplate,ITempestDomain,ITurnStartHook,IStatusApplyingAttack
    {
        public override string DisplayName=>"牧师·风暴领域";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext c)=>ArcaneChoices.NearFar(c);
        public StatusEffect StatusToApplyOnHit(AttackContext c)=>StatusNames.StatusOf(AttackDamageTypes.ToDamageType(c.AttackType));
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {if(ReferenceEquals(game.CurrentPlayer,owner))ClericChoices.FreeOperation(owner,game,null,true,"风暴启程");}
    }
}
