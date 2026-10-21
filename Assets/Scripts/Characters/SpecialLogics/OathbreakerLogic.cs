using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>破誓者：5 不再合盾，改为造成 5 点伤害。</summary>
    public sealed class OathbreakerLogic : CharacterTemplate,
        IHandEffectRewriter, IHandEffectDisplay, IResultTargetProvider
    {
        public const int AttackValue = 5;

        public override string DisplayName => "破誓者";
        public override int DefaultAttackBonus => 2;
        public override int DefaultDefenseBonus => 0;
        public override int BaseAttackRange => 1;

        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
        {
            if (actedHand != null && actedHand.Value == AttackValue)
                return HandEffectType.Attack4;
            return Hand.GetEffectType(actedHand?.Value ?? 0);
        }

        public HandEffectType DisplayedEffectOf(int resultValue)
            => resultValue == AttackValue ? HandEffectType.Attack4 : Hand.GetEffectType(resultValue);

        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context != null && context.ResultValue == AttackValue) return true;
            return base.ResultNeedsTarget(context);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || context.AttackValue != AttackValue) return;
            context.HitCount = 1;
            context.FinalDamage = AttackValue;
            context.Manager?.NoteSkill(context.Attacker, "破誓", "5 转为攻击");
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;
    }
}
