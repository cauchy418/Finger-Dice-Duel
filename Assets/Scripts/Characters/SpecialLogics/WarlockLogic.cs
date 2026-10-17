using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **邪术师**（攻 0 / 防 −1，**攻击距离无限**）。
    ///
    /// 玩家确认的规则：
    ///   · 有人**发起攻击**时，若**攻击数字**与自己某只手的数字相同，
    ///     可以**把那只手 +1**（永久，参考游侠 1 变 2）来**取消这次攻击**
    ///   · 取消是**最早结算**的 —— 能取消掉咒术师那种"在钩子里改写伤害"的攻击
    ///   · 剑客的追加攻击与圣武士的冲击**攻击数字视为 0**（因此无法被取消）
    ///   · **不消耗行动**（双方都能继续）
    ///   · 体力不满时可以**跳过回合**，把**场上一人**的体力改成与自己一致
    ///     （场上所有人体力都相同则无法发动）
    ///
    /// "取消"是否询问玩家：改为**看局面自动决定**。
    /// 理由：+1 是纯收益（手值变大 = 更强），所以"能取消就取消"是严格更优的选择，
    /// 弹一次确认只会增加点击负担。想改成可选时把
    /// <see cref="AutoCancel"/> 设为 false 即可（届时需要接上抉择 UI）。
    /// </summary>
    public sealed class WarlockLogic : CharacterTemplate, IAttackCanceller,
                                       FingerDiceDuel.Core.ISkillUseCounter,
                                       IAttackRangeProvider, ITurnSkipRule
    {
        public override string DisplayName => "邪术师";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离**无限**（玩家确认）。</summary>
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>
        /// 能取消时是否自动取消。
        ///
        /// 默认 true（+1 是纯收益）。设为 false 时会**不取消** ——
        /// 留作日后接"要不要取消"抉择的开关。
        /// </summary>
        public bool AutoCancel { get; set; } = true;

        // ============================================================
        // 取消攻击
        // ============================================================

        /// <summary>
        /// 攻击数字与自己某只手相同 -> 把那只手 +1（永久）并取消这次攻击。
        ///
        /// **两种来源**（玩家要求把人类改成"询问式"）：
        ///   · 人类：<paramref name="askAnswer"/> 是"是否问过 + 玩家选了什么"
        ///     （问过且选了手 -> 取消；问过但选了不发动 -> 放行）
        ///   · AI / 没问过：按 <see cref="AutoCancel"/> 自动取消
        /// </summary>
        public bool TryCancelAttack(PlayerState canceller, PlayerState attacker,
                                    PlayerState target, int attackValue, GameManager manager)
        {
            if (canceller == null || attackValue <= 0) return false;

            // 找到数字相同的那只手（左右都可以）
            Hand matched = null;
            if (canceller.LeftHand != null && canceller.LeftHand.Value == attackValue)
                matched = canceller.LeftHand;
            else if (canceller.RightHand != null && canceller.RightHand.Value == attackValue)
                matched = canceller.RightHand;

            if (matched == null) return false;

            CancelWith(canceller, matched);
            return true;
        }

        /// <summary>
        /// **带玩家回答**的取消判定（人类邪术师的"询问式"路径）。
        ///
        /// <paramref name="wasAsked"/> 为 true 表示这次攻击**确实问过**该玩家，
        /// 此时 <paramref name="answer"/> 为 null 就意味着"玩家选了不发动"。
        /// </summary>
        public bool TryCancelAttackWithAnswer(PlayerState canceller, int attackValue,
                                              bool wasAsked, Hand answer)
        {
            if (canceller == null || attackValue <= 0) return false;

            if (wasAsked)
            {
                // 问过 -> 完全按玩家的意思，**不再自动取消**
                if (answer == null) return false;
                if (answer.Value != attackValue) return false;

                CancelWith(canceller, answer);
                CancelCount++;
                return true;
            }

            // 没问过（AI）-> 自动策略
            if (!AutoCancel) return false;

            Hand matched = null;
            if (canceller.LeftHand != null && canceller.LeftHand.Value == attackValue)
                matched = canceller.LeftHand;
            else if (canceller.RightHand != null && canceller.RightHand.Value == attackValue)
                matched = canceller.RightHand;

            if (matched == null) return false;

            CancelWith(canceller, matched);
            CancelCount++;
            return true;
        }

        /// <summary>
        /// 本局**取消过几次攻击**。
        ///
        /// ⚠️ 为什么要计数器："只要能取消敌人的攻击就取消"是邪术师的主动策略，
        ///    但取消的**效果只体现在敌人没掉血上** —— 场上没有任何痕迹，
        ///    所以"AI 从不取消"这种问题根本测不出来。
        /// </summary>
        public int CancelCount { get; private set; }

        /// <summary>本局发动招牌技能（取消攻击）几次 —— <see cref="ISkillUseCounter"/>。</summary>
        public int SkillUseCount => CancelCount;

        /// <summary>把某只手 +1（永久）来取消攻击。</summary>
        private static void CancelWith(PlayerState canceller, Hand hand)
        {
            // SetValue 会把手值夹到 1..9，并把护盾状态清为 None ——
            // 这一点很关键：+1 之后不再是 5 的手不该继续挂着"盾"。
            hand.SetValue(hand.Value + 1);
        }

        // ============================================================
        // 跳过回合：把一人的体力改成与自己一致
        // ============================================================

        /// <summary>
        /// 能否跳过回合换体力：
        ///   · 自己**体力不满**
        ///   · 场上**存在体力与自己不同的人**（都相同就没意义）
        ///
        /// ⚠️ 我一度把这个限制去掉了（以为那句"若自己的体力不满"
        ///    是在描述**目标**）—— 玩家确认**限制保留**，
        ///    是当时的测试局面摆得不对。现已恢复。
        /// </summary>
        public bool CanSkip(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return false;
            if (owner.Health >= owner.MaximumHealth) return false;   // 满血不能发动

            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, owner)) continue;

                if (p.Health != owner.Health) return true;           // 有人体力不同 -> 可发动
            }

            return false;
        }

        /// <summary>跳过回合本身不做改体力 —— 目标由玩家点手指定（见 <see cref="ApplyHealthSwapAsync"/>）。</summary>
        public void OnTurnSkipped(PlayerState owner, GameManager manager) { }

        /// <summary>
        /// 把某人的体力改成与 <paramref name="owner"/> 一致。
        ///
        /// 由上层（BoardController）在玩家点了某个人的手之后调用 ——
        /// 因为"点手发动技能"这个交互只有控制器知道。
        /// </summary>
        public static bool ApplyHealthSwap(PlayerState owner, PlayerState target)
        {
            if (owner == null || target == null) return false;
            if (ReferenceEquals(owner, target)) return false;
            if (!target.IsAlive) return false;
            if (target.Health == owner.Health) return false;   // 本来就一样

            target.SetHealthExact(owner.Health);
            return true;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>同步"能否跳过"的内部状态 —— 邪术师没有需要同步的缓存。</summary>
        public void SyncSkipState(PlayerState owner) { }
    
        /// <summary>
        /// 邪术师：对**距离 1** 的是钝击，**距离 1 以外**的是穿刺（玩家指定）。
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => ByDistance(AttackDamageType.Bludgeoning, AttackDamageType.Piercing);

}
}
