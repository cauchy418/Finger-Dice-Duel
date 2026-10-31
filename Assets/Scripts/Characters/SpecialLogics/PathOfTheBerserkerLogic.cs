using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>野蛮人·狂战士道途：9 改为攻击；每少 5 点生命获得 +1/+1；造成伤害回复一半。</summary>
    public sealed class PathOfTheBerserkerLogic : CharacterTemplate,
        IHandEffectRewriter, ISelectiveHandEffectRewriter, IHandEffectDisplay, IResultTargetProvider, IOnHitLanded,
        IDynamicStats
    {
        public const int AttackValue = 9;

        public bool HandlesResult(int value) => value == AttackValue;

        public override string DisplayName => "野蛮人·狂战士道途";
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
            if(context!=null&&!context.IsStatusDamage){int tier=System.Math.Max(0,(context.Attacker.MaximumHealth-context.Attacker.Health)/5);if(tier>0)context.Manager?.RecordSkillVisual(context.Attacker,"狂乱",context.Attacker,new SkillVisualData(value:tier));}
            if (context == null || context.AttackValue != AttackValue) return;
            context.HitCount = 1;
            context.FinalDamage = AttackValue;
            context.Manager?.NoteSkill(context.Attacker, "狂怒", "9 转为攻击",context.Defender,new SkillVisualData(source:context.SourceHand,value:context.AttackValue));
        }

        public void OnHitLanded(AttackContext context, GameManager manager)
        {
            if (context == null || context.AppliedDamage <= 0) return;
            int heal = context.AppliedDamage / 2;
            if (heal > 0&&context.Attacker!=null){int before=context.Attacker.Health;context.Attacker.Heal(heal);manager?.RecordSkillVisual(context.Attacker,"嗜血",context.Defender,new SkillVisualData(value:context.Attacker.Health-before));}
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
