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
        /// </summary>
        public bool TryCancelAttack(PlayerState canceller, PlayerState attacker,
                                    PlayerState target, int attackValue, GameManager manager)
        {
            if (!AutoCancel) return false;
            if (canceller == null || attackValue <= 0) return false;

            // 找到数字相同的那只手（左右都可以）
            Hand matched = null;
            if (canceller.LeftHand != null && canceller.LeftHand.Value == attackValue)
                matched = canceller.LeftHand;
            else if (canceller.RightHand != null && canceller.RightHand.Value == attackValue)
                matched = canceller.RightHand;

            if (matched == null) return false;

            // 把那只手 +1（**永久** —— 与游侠的 1 变 2 同一性质）。
            // SetValue 会把手值夹到 1..9，并把护盾状态清为 None ——
            // 这一点很关键：+1 之后不再是 5 的手不该继续挂着"盾"。
            matched.SetValue(matched.Value + 1);

            return true;
        }

        // ============================================================
        // 跳过回合：把一人的体力改成与自己一致
        // ============================================================

        /// <summary>
        /// 能否跳过回合换体力：
        ///   · 自己**体力不满**
        ///   · 场上**存在体力与自己不同的人**（都相同就没意义）
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
    }
}
