namespace FingerDiceDuel.Characters.SpecialLogics
{
    using FingerDiceDuel.Core;

    /// <summary>
    /// **法师（Mage）**：攻 0 / 防 −1，攻击距离 **1**。
    ///
    /// 玩家设计的核心机制：
    ///   运算出**攻击数字（4/6/8）**时，可以选择**放弃这次攻击**，
    ///   改为给**下一次**攻击指定一种**伤害类型**（10 选 1）。
    ///   命中的那一次攻击会给目标挂上对应的**负面状态**（持续目标自己的 2 个回合）。
    ///
    /// ## 为什么是"放弃一次换下一次"
    ///
    /// 玩家原话："运算出攻击数字时，可以选择放弃攻击，将**下一次**攻击改为..."
    /// 所以这是一个**两回合的铺垫**：第一回合白扔一次攻击、只为设定类型；
    /// 第二回合真正打出去、才挂上状态。
    ///
    /// ⚠️ 玩家确认：**连续放弃时只保留最新的选择**（后选的覆盖先选的）。
    ///
    /// ⚠️ 玩家确认：若下一次攻击**没命中**（被闪避 / 被取消），
    ///    那么这次的**放弃也白费** —— 类型**作废**，
    ///    法师必须**再放弃一次攻击、重新选**才能再次附加。
    ///    （见 <see cref="ConsumeDamageType"/>。）
    ///
    /// ## 力场
    ///
    /// 第 10 种"力场"**不挂状态** —— 它只是"这一击按咒术师的方式穿透"：
    /// 无视护盾、无视正向防御、不可被闪避。
    /// 玩家表示之后可以把咒术师的描述改成"伤害改为力场伤害"。
    /// </summary>
    public sealed class MageLogic : CharacterTemplate,
        IStatusApplyingAttack, IDodgeBlocker
    {
        /// <summary>法师能"放弃攻击"的数字（与其他角色一致，2 不是攻击）。</summary>
        public const int MinAttackValue = 4;

        public override string DisplayName => "法师";

        // ============================================================
        // 伤害类型（跨回合状态）
        // ============================================================

        /// <summary>
        /// 已选定的伤害类型（<see cref="DamageType.None"/> = 没选）。
        ///
        /// 这是**跨回合**的：本次放弃攻击设定它，下一次攻击消耗它。
        /// </summary>
        public DamageType PendingDamageType { get; private set; } = DamageType.None;

        /// <summary>本次放弃攻击时**曾经选过**类型（用于战报 / 测试）。</summary>
        public bool HasPendingType => PendingDamageType != DamageType.None;

        /// <summary>
        /// 设定伤害类型（覆盖旧的 —— 玩家确认"只保留最新的选择"）。
        /// </summary>
        public void SetPendingDamageType(DamageType type) => PendingDamageType = type;

        /// <summary>清掉待用的伤害类型（没命中 / 已用掉）。</summary>
        public void ClearPendingDamageType() => PendingDamageType = DamageType.None;

        /// <summary>
        /// 把待用类型**取走并清空**（攻击结算命中时调用）。
        ///
        /// ⚠️ 玩家确认："若没命中就作废" —— 所以**无论命中与否都要清空**，
        ///    没命中时清掉就等于作废（法师要重新放弃一次攻击才能再挂）。
        /// </summary>
        public DamageType ConsumeDamageType()
        {
            DamageType t = PendingDamageType;
            PendingDamageType = DamageType.None;
            return t;
        }

        // ============================================================
        // 攻击命中 -> 挂状态
        // ============================================================

        /// <summary>
        /// 本次攻击要挂上的状态。
        ///
        /// 归零规则：
        ///   · 没选类型（<see cref="DamageType.None"/>）-> 不挂
        ///   · 力场 -> 不挂（它只有穿透）
        ///   · 其余 9 种 -> 挂对应的状态
        ///
        /// ⚠️ 这里**只读不改** —— 真正的"取走并清空"由 <see cref="ConsumeDamageType"/>
        ///    在结算结束时做，因为只有那时才知道有没有命中。
        /// </summary>
        public StatusEffect StatusToApplyOnHit(AttackContext context)
            => StatusNames.StatusOf(PendingDamageType);

        // ============================================================
        // 力场：同咒术师
        // ============================================================

        /// <summary>
        /// 力场按咒术师的方式结算（穿透护盾、无视正向防御、不可闪避）。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            if (!IsForceAttack) return;

            context.IgnoreShield = true;
            context.IgnorePositiveDefense = true;
        }

        /// <summary>当前待用的是不是"力场"。</summary>
        public bool IsForceAttack => PendingDamageType == DamageType.Force;

        /// <summary>力场攻击不可被闪避（与咒术师同源）。</summary>
        public bool PreventsDodge => IsForceAttack;

        // ============================================================
        // 开局复位
        // ============================================================

        public override void OnMatchReset(PlayerState owner, GameManager manager)
            => PendingDamageType = DamageType.None;
    }
}
