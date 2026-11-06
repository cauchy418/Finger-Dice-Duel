namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 战士·勇士：用运算前为1的手发起普通攻击，基础伤害×1.5，命中附物理易伤。
    /// </summary>
    public sealed class ChampionLogic : CharacterTemplate, ILethalThresholdAdvisor, ISkillUseCounter
    {
        public override string DisplayName => "战士·勇士";

        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => 0;

        public const float HeavyStrikeMultiplier = 1.5f;
        public int SkillUseCount { get; private set; }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null || !CharacterBehaviors.ActorHas(context.Attacker, this)
                || context.IsSpecialAttack || context.IsStatusDamage || context.IsReflected || context.SourceHandValueBefore != 1) return;

            // 独立倍率不改变风流剑客“是否使用潇洒无畏”的伤害类型判定，也不会被其倍率覆盖。
            context.AdditionalBaseDamageMultiplier *= HeavyStrikeMultiplier;
            SkillUseCount++;context.VisualPower=System.Math.Max(context.VisualPower,1.35f);
            context.Manager?.NoteSkill(context.Attacker, "精通重击", "用1发起攻击，基础伤害×1.5",context.Defender,new SkillVisualData(source:context.SourceHand,value:context.AttackValue));
            context.ApplyAttachedStatus(context.Defender, StatusEffect.PhysicalVulnerability, 2, context.Attacker, context.Manager, "物理易伤");
        }

        public int EstimateLethalDamage(LethalContext context, int resultValue,
                                       int attackBonus, int defenseBonus)
        {
            int damage = context.ActedHand?.Value == 1
                ? (int)System.Math.Round(resultValue * HeavyStrikeMultiplier, System.MidpointRounding.AwayFromZero)
                : resultValue;
            return AttackContext.ApplyStatBonuses(damage, attackBonus, defenseBonus, false);
        }

        /// <summary>攻击距离 1（与白板一致）。</summary>
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager) { SkillUseCount = 0; }
    }
}
