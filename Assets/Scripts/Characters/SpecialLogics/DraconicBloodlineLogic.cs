using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>术士·龙族血脉：选择一种元素，攻击与抗性永久转化为该元素。</summary>
    public sealed class DraconicBloodlineLogic : CharacterTemplate,
        IAttackTypeProvider
    {
        public AttackDamageType Element { get; private set; } = AttackDamageType.Fire;

        public override string DisplayName => "术士·龙族血脉";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        public void ChooseElement(AttackDamageType element)
        {
            if (!IsDraconicElement(element)) return;
            Element = element;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            if (owner != null) owner.AddPermanentResistance(Element);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context != null) context.StatusDuration = 3;
        }

        public override AttackDamageType AttackTypeOf(AttackContext context) => Element;

        public static bool IsDraconicElement(AttackDamageType type)
            => type == AttackDamageType.Cold
            || type == AttackDamageType.Fire
            || type == AttackDamageType.Lightning
            || type == AttackDamageType.Poison
            || type == AttackDamageType.Acid;
    }
}
