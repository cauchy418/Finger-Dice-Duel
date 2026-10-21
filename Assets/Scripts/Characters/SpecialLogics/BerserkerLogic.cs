using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>狂战士：9 改为攻击；每少 5 点生命获得 +1/+1；造成伤害回复一半。</summary>
    public sealed class BerserkerLogic : CharacterTemplate,
        IHandEffectRewriter, IHandEffectDisplay, IResultTargetProvider, IOnHitLanded,
        IDynamicStats
    {
        public const int AttackValue = 9;

        public override string DisplayName => "狂战士";
        public override int DefaultAttackBonus => -1;
        public override int DefaultDefenseBonus => -2;
        public override int MaxHealthBonus => 5;
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
            context.Manager?.NoteSkill(context.Attacker, "狂战", "9 转为攻击");
        }

        public void OnHitLanded(AttackContext context, GameManager manager)
        {
            if (context == null || context.AppliedDamage <= 0) return;
            int heal = context.AppliedDamage / 2;
            if (heal > 0) context.Attacker?.Heal(heal);
        }

        public void RefreshDynamicStats(PlayerState owner, GameManager manager)
        {
            if (owner == null) return;
            int missing = owner.MaximumHealth - owner.Health;
            int bonus = missing / 5;
            if (bonus < 0) bonus = 0;
            owner.SetDynamicBonuses(bonus, bonus);
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Slashing;
    }
}
