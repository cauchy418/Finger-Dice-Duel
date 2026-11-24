using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
 public sealed class RadiantLightDomainLogic:ClericTemplate
 {
  public override string DisplayName=>"牧师·光明领域";
  public override int DefaultDefenseBonus=>-1;
  public override int BaseAttackRange=>InfiniteAttackRange;
  public override AttackDamageType AttackTypeOf(AttackContext c)=>AttackDamageType.Radiant;
 }
}
