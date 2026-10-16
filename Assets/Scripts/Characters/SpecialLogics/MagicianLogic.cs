using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **魔术师**（攻 0 / 防 −1，攻击距离 2）。
    ///
    /// 玩家给出的规则：
    ///   · **可以选择自己的手当被加数**
    ///     （例：左手 3、右手 5 -> 拿左手 3 碰自己的 5 凑出 8）。
    ///     两只手**任选一个方向**，变的是**先点的那只手**（即发起运算的手）；
    ///     重复点同一只手不生效（一只手不能加自己）。
    ///   · **运算出 2 可以攻击**，吃攻防加成的 **2 点**伤害。
    ///
    /// \u26a0\ufe0f 第 2 条是与他角色的**关键差别**：别人 2 是死牌，他是攻击。
    /// </summary>
    public sealed class MagicianLogic : CharacterTemplate,
        ISelfAddendProvider, IHandEffectRewriter, IHandEffectDisplay,
        IResultTargetProvider, IAttackDamageRule
    {
        /// <summary>魔术师能把结果 2 当攻击打出的伤害（吃攻防加成）。</summary>
        public const int MagicAttackValue = 2;

        /// <summary>
        /// **魔术师攻击的固定伤害**（玩家改动：2/4/6/8 一律 4 点、不吃攻防加成）。
        /// </summary>
        public const int FixedAttackDamage = 4;

        public override string DisplayName => "魔术师";

        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离 2。</summary>
        public override int BaseAttackRange => 2;

        /// <summary>允许拿自己的另一只手当被加数。</summary>
        public bool AllowsSelfAddend => true;

        /// <summary>
        /// 把结果 **2** 改写成攻击。
        ///
        /// \u26a0\ufe0f 只借一个"攻击类型"让上层走攻击分派 ——
        ///    真正的伤害在 <see cref="OnResolveAttack"/> 里按
        ///    <see cref="MagicAttackValue"/> 设定（与其他借 Attack6 的角色同一手法）。
        ///    这里借 <see cref="HandEffectType.Attack4"/>，因为 Attack6/8 会被
        ///    "伤害就是数字本身"的默认逻辑按 6 / 8 结算。
        /// </summary>
        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
        {
            int result = actedHand?.Value ?? 0;

            // 统计"本局自加过几次"（供技能发动率）。
            // 判据：被加数就是发起运算的那只手 —— 即"拿自己的手当被加数"。
            if (actedHand != null && ReferenceEquals(actedHand, _selfAddendMark))
                SelfAddendCount++;

            if (result == MagicAttackValue)
                return HandEffectType.Attack4;

            return Hand.GetEffectType(result);
        }

        /// <summary>
        /// **本局自加几次**（供"技能发动率"统计）。
        /// </summary>
        public int SelfAddendCount { get; private set; }

        /// <summary>
        /// 由上层在施行自加时告知"被加数就是这一只手"，用于计数。
        ///
        /// \u26a0\ufe0f 只用于**计数**，不参与任何规则判定 ——
        ///    规则的判定一律从手牌归属直接推出（见
        ///    <see cref="CharacterBehaviors.ActorHasSelfAddend"/>），
        ///    这里刻意不重复那条逻辑，免得又出现"两处口径不一致"。
        /// </summary>
        private Hand _selfAddendMark;

        /// <summary>标记"这一次运算是拿自己的手当被加数"。</summary>
        public void MarkSelfAddendHand(Hand targetHand) => _selfAddendMark = targetHand;

        /// <summary>显示：结果 2 也要显示成"攻击"（红），否则界面与真实结算不一致。</summary>
        public HandEffectType DisplayedEffectOf(int resultValue)
        {
            if (resultValue == MagicAttackValue) return HandEffectType.Attack4;
            return Hand.GetEffectType(resultValue);
        }

        /// <summary>
        /// **2 的攻击也要问"打谁"**（玩家要求：与 4/6/8 一样再选一次目标）。
        ///
        /// \u26a0\ufe0f 为什么必须显式实现它：
        ///    `BoardController.NeedsTargetAfterOperation` 先问角色的
        ///    <see cref="IResultTargetProvider"/>，角色不实现时才走兜底 ——
        ///    而兜底是**按 `context.Effect` 判断**的，
        ///    2 的效果类型是 `None`（不是 Attack4/6/8），于是返回 false：
        ///    **2 被当成"无效果"，直接自动挑了一个目标就打出去**。
        ///    这正是玩家实测的"2 现在是自动攻击目标了"。
        ///
        ///    4/6/8 之所以没这个问题，是因为兜底认得它们；
        ///    魔术师的 2 是**借** Attack4 的类型，兜底认不出来。
        /// </summary>
        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context == null) return false;

            // ⚠️⚠️ **只补 2，其余必须交回默认规则**。
            //
            //    我上一轮写成 `return context.ResultValue == 2;` ——
            //    那等于**替 4/6/8 返回了 false**，把它们的目标询问关掉了。
            //    玩家实测："现在 4 也不选择目标了直接攻击了"。
            //    正确做法是：**认得的自己答，认不得的交给兜底**
            //    （兜底认得 Attack4/6/8）。所以这里只对 2 返回 true，
            //    其余一律 `base`。
            if (context.ResultValue == MagicAttackValue) return true;

            return base.ResultNeedsTarget(context);
        }

        /// <summary>
        /// **魔术师的攻击一律是固定 4 点伤害、且不吃攻防加成**（玩家改动）。
        ///
        /// 玩家原话："把魔术师的攻击（2/4/6/8）均改成造成 4 点固定伤害（不吃攻防加成）"。
        ///
        /// ⚠️ 为什么必须走 <see cref="IAttackDamageRule"/> 而不是只改 FinalDamage：
        ///    AttackContext.Resolve 在调用 OnResolveAttack 之后还有一句
        ///      FinalDamage = ApplyBonuses(FinalDamage);
        ///    —— 只设最终伤害的话，攻防加成会**在后面被加回去**，
        ///    "不吃攻防加成"就落空了。所以必须
        ///      · 用这个钩子（它拿到 attackValue，能判断是不是攻击）；
        ///      · 并同时把 IgnoreStatBonuses 打开。
        ///
        /// 段数压成 1：攻击牌原本是 4/6/8，若不压，
        /// 别的机制可能按 4/6/8 去分段（例如双将里配魔射手）。
        /// </summary>
        void IAttackDamageRule.ApplyAttackRule(AttackContext context, int attackValue,
                                               GameManager manager)
        {
            if (context == null) return;
            if (!IsAttackValue(attackValue)) return;

            context.HitCount = 1;
            context.FinalDamage = FixedAttackDamage;

            // 不吃攻防加成（也顺带跳过"无视正向防御"那些分支）
            context.IgnoreStatBonuses = true;
        }

        /// <summary>2 / 4 / 6 / 8 都算魔术师的攻击牌。</summary>
        public static bool IsAttackValue(int value)
            => value == MagicAttackValue || value == 4 || value == 6 || value == 8;

        /// <summary>
        /// 追加一道兜底：把伤害再次钉成固定值。
        ///
        /// 与 <see cref="IAttackDamageRule.ApplyAttackRule"/> 同时存在是**刻意**的：
        /// 前者在攻防加成**之前**生效，这里在之后 —— 两道都钉，
        /// 保证任何路径下魔术师的攻击都是固定 4 点。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            if (!IsAttackValue(context.AttackValue)) return;

            context.HitCount = 1;
            context.FinalDamage = FixedAttackDamage;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            SelfAddendCount = 0;
            _selfAddendMark = null;
        }
    }
}
