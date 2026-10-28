namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 法师·咒法学派（SchoolOfConjuration）特殊逻辑：
    /// —— 攻击结算时无视对方护盾，直接造成伤害；
    /// —— 其攻击**不可被闪避**（游荡者·风流剑客的闪避对它无效）。
    ///
    /// 实现方式：在 OnResolveAttack 钩子中将 AttackContext.IgnoreShield 置为 true，
    /// 由 AttackContext.Resolve 在后续流程中据此跳过护盾抵挡判定。
    /// </summary>
    public class SchoolOfConjurationLogic : CharacterTemplate, IDodgeBlocker, IPiercesMitigation, ISeatSwapSkill
    {
        /// <summary>
        /// 法师·咒法学派的攻击**穿透减免**（玩家确认）：
        ///   · 武僧·太极宗的太极对他无效（原有行为，不变）；
        ///   · 游侠·幽域追踪者的伏击**不能抵挡**他的攻击，**也不会被解除**。
        ///
        /// 由 `WayOfTheTaiChiLogic` 与 `GloomStalkerLogic` 统一查询本接口 ——
        /// 之前 WayOfTheTaiChiLogic 写的是 `is SchoolOfConjurationLogic`（硬编码角色类型），
        /// 如今游侠·幽域追踪者也要判同一件事，再硬编码一次就是"两个角色各自认识法师·咒法学派"。
        /// </summary>
        public bool PiercesMitigation => true;

        public bool CanSwapSeats(PlayerState owner, PlayerState target, GameManager game)
        {
            if (game == null || game.IsGameOver || owner == null || target == null
                || !ReferenceEquals(game.CurrentPlayer, owner) || !CharacterBehaviors.ActorHas(owner, this)
                || ReferenceEquals(owner, target) || !owner.IsAlive || !target.IsAlive
                || !StatusRules.CanAct(owner) || game.SeatOf(owner)<1 || game.SeatOf(target)<1) return false;
            return (owner.LeftHand.Value==target.LeftHand.Value && owner.RightHand.Value==target.RightHand.Value)
                || (owner.LeftHand.Value==target.RightHand.Value && owner.RightHand.Value==target.LeftHand.Value);
        }

        public bool TrySwapSeats(PlayerState owner, PlayerState target, GameManager game)
            => CanSwapSeats(owner,target,game) && game.TrySwapSeats(owner,target);

        public override string DisplayName => "法师·咒法学派";

        /// <summary>
        /// 法师·咒法学派的攻击不可被闪避。
        ///
        /// 判定依据是"攻击者是法师·咒法学派"，**不是**"伤害是 8"：
        /// 法师·咒法学派同样会打出 4 / 6，而其他角色打出 8 时游荡者·风流剑客**应该**能闪避。
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
            context.Manager?.NoteSkill(context.Attacker, "次级咒术", "无视护盾、正防御与闪避");

            // 说明：不在此处修改 FinalDamage，
            // 保持"法师·咒法学派只改变结算路径，不改变伤害数值"的语义纯净性。
            // 其攻击 -1 由 CharacterDefinition.AttackBonus 注入。
        }

        // OnShieldFormed / OnOwnShieldBroken / OnMatchReset 无需重写，
        // 走 CharacterTemplate 默认空实现即可：法师·咒法学派不持有跨回合状态。

        /// <summary>
        /// **攻击距离无限**（玩家确认）：法师·咒法学派不受座位距离限制。
        /// </summary>
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>默认攻击 -1（可被 CharacterDefinition.AttackBonus 覆盖）。</summary>
        public override int DefaultAttackBonus => -1;

        /// <summary>
        /// 法师·咒法学派：**力场伤害**（玩家指定）。
        ///
        /// 咒术的招牌是"无法被任何方式抵挡/闪避"，与力场同源。
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Force;

}
}
