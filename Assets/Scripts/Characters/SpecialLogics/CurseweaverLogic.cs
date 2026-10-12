namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 咒术师（Curseweaver）特殊逻辑：
    /// —— 攻击结算时无视对方护盾，直接造成伤害；
    /// —— 其攻击**不可被闪避**（游侠的闪避对它无效）。
    ///
    /// 实现方式：在 OnResolveAttack 钩子中将 AttackContext.IgnoreShield 置为 true，
    /// 由 AttackContext.Resolve 在后续流程中据此跳过护盾抵挡判定。
    /// </summary>
    public class CurseweaverLogic : CharacterTemplate, IDodgeBlocker
    {
        public override string DisplayName => "Curseweaver";

        /// <summary>
        /// 咒术师的攻击不可被闪避。
        ///
        /// 判定依据是"攻击者是咒术师"，**不是**"伤害是 8"：
        /// 咒术师同样会打出 4 / 6，而其他角色打出 8 时游侠**应该**能闪避。
        /// </summary>
        public bool PreventsDodge => true;

        /// <summary>
        /// 攻击结算前介入：无视护盾 + 无视对方正向防御加成。
        /// 该钩子由 attacker.Character?.OnResolveAttack(context) 触发，
        /// 因此 context.Attacker 一定是本角色所有者，无需再做归属校验。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;

            // 核心能力：无视护盾（护盾不消耗、不破碎、不挡伤）
            context.IgnoreShield = true;
            context.IgnorePositiveDefense = true;

            // 说明：不在此处修改 FinalDamage，
            // 保持"咒术师只改变结算路径，不改变伤害数值"的语义纯净性。
            // 其攻击 -1 由 CharacterDefinition.AttackBonus 注入。
        }

        // OnShieldFormed / OnOwnShieldBroken / OnMatchReset 无需重写，
        // 走 CharacterTemplate 默认空实现即可：咒术师不持有跨回合状态。

        /// <summary>
        /// **攻击距离无限**（玩家确认）：咒术师不受座位距离限制。
        /// </summary>
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>默认攻击 -1（可被 CharacterDefinition.AttackBonus 覆盖）。</summary>
        public override int DefaultAttackBonus => -1;
    }
}
