namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 战士·勇士：攻 +1 / 防 0，距离 1。发起攻击时双手和为 10，基础伤害 ×1.5。
    /// 使用攻击建立时的快照，避免双将另一技能改手后影响判定；额外攻击不属于发起攻击。
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
                || context.IsSpecialAttack || context.IsReflected || context.AttackerHandSumAtStart != 10) return;

            // 独立倍率不改变风流剑客“是否使用潇洒无畏”的伤害类型判定，也不会被其倍率覆盖。
            context.AdditionalBaseDamageMultiplier *= HeavyStrikeMultiplier;
            SkillUseCount++;
            context.Manager?.NoteSkill(context.Attacker, "精通重击", "双手和10，基础伤害×1.5");
        }

        public int EstimateLethalDamage(LethalContext context, int resultValue,
                                       int attackBonus, int defenseBonus)
        {
            int damage = resultValue + context.OtherHandValue == 10
                ? (int)System.Math.Round(resultValue * HeavyStrikeMultiplier, System.MidpointRounding.AwayFromZero)
                : resultValue;
            return AttackContext.ApplyStatBonuses(damage, attackBonus, defenseBonus, false);
        }

        /// <summary>攻击距离 1（与白板一致）。</summary>
        public override int BaseAttackRange => 1;

        public override void OnMatchReset(PlayerState owner, GameManager manager) { SkillUseCount = 0; }
    }
}
