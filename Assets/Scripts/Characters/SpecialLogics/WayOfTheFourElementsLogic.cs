using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>武僧·四象宗：气点切换元素攻击，受元素攻击时自动耗气获得抗性。</summary>
    public sealed class WayOfTheFourElementsLogic : CharacterTemplate,
        IAttackTypeProvider, IIncomingElementReaction, IKiResource
    {
        public const int MaxKi = 6;
        public int Ki { get; private set; } = 2;

        public AttackDamageType ChosenElement { get; set; } = AttackDamageType.Fire;

        public override string DisplayName => "武僧·四象宗";
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            Ki = 2;
        }

        public override void OnOperationResolved(PlayerState owner, Hand actedHand,
                                                  int actedHandValueBefore, int targetValue,
                                                  HandEffectType effect, GameManager manager,
                                                  IPreTurnChoiceSink choiceSink)
        {
            if (owner?.LeftHand == null || owner.RightHand == null) return;
            if (owner.LeftHand.Value == owner.RightHand.Value)
                Ki = System.Math.Min(MaxKi, Ki + 1);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || Ki < 2) return;
            AttackDamageType type = ChosenElement;
            if (!IsElement(type)) type = AttackDamageType.Fire;
            Ki -= 2;
            context.AttackType = type;
            context.Manager?.NoteSkill(context.Attacker, "四象元素", $"本次攻击改为 {AttackDamageTypes.NameOf(type)}");
        }

        public void OnIncomingElementAttack(AttackContext context, GameManager manager)
        {
            if (context == null || Ki <= 0) return;
            AttackDamageType type = context.AttackType;
            if (!IsElement(type)) return;
            if (context.Defender != null && context.Defender.HasResistance(type)) return;
            Ki--;
            context.Defender.AddResistance(type);
            manager?.NoteSkill(context.Defender, "四象抗性", $"消耗 1 气，获得 {AttackDamageTypes.NameOf(type)} 抗性");
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;

        private static bool IsElement(AttackDamageType type)
            => type == AttackDamageType.Fire
            || type == AttackDamageType.Cold
            || type == AttackDamageType.Lightning
            || type == AttackDamageType.Thunder
            || type == AttackDamageType.Acid;
    }
}
