using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>净化圣焰：普通攻击固定5火焰，命中附圣火与灼伤。内部类名保留兼容。</summary>
    public sealed class LightDomainLogic : ClericTemplate, ILethalThresholdAdvisor, IFixedNormalAttackDamage
    {
        public const int FixedDamage=5;
        public int NormalAttackDamage=>FixedDamage;
        public AttackDamageType NormalAttackType=>AttackDamageType.Fire;
        public override string DisplayName=>"牧师·净化领域";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override void OnResolveAttack(AttackContext c)
        {
            if(c==null||c.IsStatusDamage||c.IsSpecialAttack||c.IsReflected||c.IsNecromancyFreeAttack)return;
            c.AttackType=AttackDamageType.Fire;c.FinalDamage=FixedDamage;c.IgnoreStatBonuses=true;c.IgnoreIncomingModifiers=true;
            c.AttackerStatusOverride=StatusEffect.HolyLight;
            c.Manager?.RecordSkillVisual(c.Attacker,"净化圣焰",c.Defender,new SkillVisualData(source:c.SourceHand,element:AttackDamageType.Fire));
        }
        public override AttackDamageType AttackTypeOf(AttackContext c)=>AttackDamageType.Fire;
        public int EstimateLethalDamage(LethalContext c,int value,int attack,int defense)=>FixedDamage;
    }
}
