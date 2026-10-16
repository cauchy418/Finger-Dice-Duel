namespace FingerDiceDuel.Characters.SpecialLogics
{
    using System.Collections.Generic;
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
        IStatusApplyingAttack, IDodgeBlocker, IMageState, IPreTurnChoiceProvider
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
        // 抉择收集（玩家确认的顺序：先问放弃 -> 选类型 -> 再选目标）
        // ============================================================

        /// <summary>
        /// 运算结果是攻击数字（4/6/8）时，登记两个抉择。
        ///
        /// 玩家确认的顺序：
        ///   ① 先问"是否放弃攻击"
        ///   ② 选"放弃" -> 弹 10 种伤害类型
        ///   ③ 选完类型 -> 再选攻击目标
        ///
        /// ⚠️ 每次算出攻击数字**都要问**（玩家明确要求）。
        ///    所以要放在"用结果值判断"的地方，而不是"每回合只问一次"。
        ///
        /// ⚠️ 与猎人/剑客同一模式：必须在**运算前收集**，
        ///    结算一旦开始就一次跑完，没法中途问人类玩家。
        /// </summary>
        /// ⚠️⚠️ 签名必须与基类**完全一致**：`IList<ChoiceRequest>` 而不是
        ///     `List<ChoiceRequest>`。
        ///
        ///     我第一版写成了 `List<ChoiceRequest>`（也没有 `override`），
        ///     于是它**没有覆盖基类方法**，只是一个名字相同的新方法 ——
        ///     通过 `IPreTurnChoiceProvider` 调用时走的是基类的**空实现**，
        ///     表现成"法师从来不问要不要放弃攻击、AI 从不挂状态"。
        ///     C# 9 没有默认接口成员，所以这种签名不匹配**不会报错**，只会静默失效。
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            

            if (context == null || requests == null) return;

            

            // 归属校验：双将容器里可能挂着别的角色，别替别人登记
            if (!CharacterBehaviors.ActorHas(context.Actor, this)) return;

            // 只有"结果落在攻击数字上"才问
            if (!IsAttackResult(context.ResultValue)) return;

            // 已经有待用类型时不再问"要不要放弃" ——
            // 玩家确认："法师最少要两个回合才能完成一次带效果的攻击"，
            // 所以铺一次就够，重复放弃只是白白浪费回合。
            if (HasPendingType) return;

            var abandon = new ChoiceRequest(
                HandEffect.MageAbandonAttack,
                "是否放弃这次攻击？放弃后可以为下一次攻击指定一种伤害类型。",
                new List<Hand>(context.Actor.Hands),
                allowDecline: true,
                declineResult: null,                     // 放弃 = 放弃"放弃" = 照常攻击
                decider: context.Actor,
                acceptLabel: "放弃攻击");
            requests.Add(abandon);

            // ② 10 种伤害类型。候选先填好，玩家选了"放弃"才有意义 ——
            //    但它必须**同时登记**，因为收集阶段只有这一次机会。
            //
            //    ⚠️ `skipWhen`：玩家没选"放弃"时这一步自动跳过，
            //       不会出现"选了照常攻击却被追问类型"。
            var pick = new ChoiceRequest(
                HandEffect.MageDamageType,
                "选择伤害类型（10 选 1）",
                new List<Hand>(context.Actor.Hands),
                allowDecline: true,
                declineResult: null,
                decider: context.Actor,
                acceptLabel: "就选它",
                skipWhen: () => !WantsToAbandonThisTurn);
            requests.Add(pick);

            
        }

        /// <summary>
        /// 本次是否已经决定"放弃攻击"（由 sink 回读，供 <c>skipWhen</c> 用）。
        /// </summary>
        public bool WantsToAbandonThisTurn { get; set; }

        /// <summary>
        /// 从抉择器（AI / 脚本 / 界面）**回读**这一次的选择并落到自己身上。
        ///
        /// 由上层在"收集完抉择之后、真正结算之前"调用。
        ///
        /// 规则：
        ///   · 玩家选了"放弃攻击" -> 记下类型（**覆盖旧的**，只保留最新）
        ///   · 玩家没放弃           -> 什么都不做（照常攻击）
        /// </summary>
        public void ReadFromSink(IDamageTypeChoiceSink sink)
        {
            if (sink == null) return;

            WantsToAbandonThisTurn = sink.WantsToAbandonAttack;
            if (!WantsToAbandonThisTurn) return;

            if (sink.ChosenDamageType != DamageType.None)
                SetPendingDamageType(sink.ChosenDamageType);
        }

        /// <summary>结果值是不是"攻击数字"（4/6/8）。</summary>
        public static bool IsAttackResult(int value)
            => value == 4 || value == 6 || value == 8;

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
