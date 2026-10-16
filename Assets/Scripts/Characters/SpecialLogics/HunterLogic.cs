using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **猎人**（攻 0 / 防 −1，攻击距离 1）。
    ///
    /// 玩家给出的规则：
    ///   · **运算后**，若两只手**同时是 1/2/3 且数字相同**，
    ///     可以（**主动选择**）把**参与运算的那只手**暗置，进入**伏击**状态。
    ///   · 暗置的手：不能发起运算（类似被锁），
    ///     也**不能被当成运算数字**（对手点不到它）—— 但**数值可见**。
    ///   · 伏击状态：**攻击 +1**、**攻击距离 3**。
    ///   · 即将受到伤害时若处于伏击，则**恢复暗置的手**（解除伏击）并**抵挡此次伤害**；
    ///     触发时机在**武僧太极之后**。
    ///   · **咒术师可以穿透伏击**：伏击挡不住咒术攻击，咒术攻击也**不会解除**伏击。
    ///
    /// 玩家补充（与跳过回合有关）：
    ///   "若猎人处于潜伏状态，剩余的那只手被野蛮人控制住也要给出跳过回合按钮。"
    ///   —— 即**两只手都无法主动运算时，右侧都要有跳过按钮**。
    ///   （由 <see cref="PlayerState.HasNoUsableHand"/> 与
    ///     <see cref="GameManager.CanSkipTurnFor"/> 统管。）
    /// </summary>
    public sealed class HunterLogic : CharacterTemplate,
        IHandEffectRewriter, IHandEffectDisplay, IDamageMitigator,
        IAttackRangeProvider
    {
        /// <summary>伏击要求两只手都是 1/2/3 —— 上界（含）。</summary>
        public const int AmbushMaxValue = 3;

        /// <summary>伏击状态下的攻击加成。</summary>
        public const int AmbushAttackBonus = 1;

        /// <summary>伏击状态下的攻击距离。</summary>
        public const int AmbushRange = 3;

        public override string DisplayName => "猎人";

        // ⚠️ `DefaultAttackBonus` 在下面重写为"基础 0 + 伏击加成"（见伏击加成一节），
        //    所以这里**不能**再声明一次，否则重复定义。
        public override int DefaultDefenseBonus => -1;

        public override int BaseAttackRange => 1;

        /// <summary>处于伏击状态时暗置的那只手；未伏击为 null。</summary>
        private Hand _ambushHand;

        /// <summary>是否处于伏击状态。</summary>
        public bool IsAmbushing => _ambushHand != null && _ambushHand.IsConcealed;

        /// <summary>处于伏击时暗置的手（供界面与诊断）。</summary>
        public Hand AmbushHand => IsAmbushing ? _ambushHand : null;

        // ---------------------------------------------------------------
        // 伏击条件
        // ---------------------------------------------------------------

        /// <summary>
        /// 这次运算之后是否**满足伏击条件**：
        /// 两只手**同时是 1/2/3 且数字相同**。
        ///
        /// 玩家原话："运算后，若两只手同时是 1/2/3 且数字相同，可以……进入伏击状态。"
        /// </summary>
        public static bool MeetsAmbushCondition(PlayerState owner)
        {
            if (owner == null) return false;

            Hand left = owner.LeftHand;
            Hand right = owner.RightHand;
            if (left == null || right == null) return false;

            // 两只手都要是 1/2/3
            if (left.Value < 1 || left.Value > AmbushMaxValue) return false;
            if (right.Value < 1 || right.Value > AmbushMaxValue) return false;

            // 数字相同
            return left.Value == right.Value;
        }

        /// <summary>伏击可以发生在"参与运算的那只手"上。</summary>
        public Hand AmbushCandidate(PlayerState owner, Hand actedHand)
        {
            if (!MeetsAmbushCondition(owner)) return null;
            if (actedHand == null) return null;
            if (actedHand.IsLocked || actedHand.IsConcealed) return null;
            if (actedHand.State != ShieldState.None) return null;
            return actedHand;
        }

        /// <summary>进入伏击：暗置这只手。</summary>
        public void EnterAmbush(Hand hand)
        {
            if (hand == null) return;
            hand.Conceal();
            _ambushHand = hand;
        }

        /// <summary>解除伏击：恢复被暗置的手。返回被恢复的那只手（没在伏击则 null）。</summary>
        public Hand ReleaseAmbush()
        {
            Hand hand = AmbushHand;
            if (hand == null) return null;

            hand.Reveal();
            _ambushHand = null;
            return hand;
        }

        // ---------------------------------------------------------------
        // 伏击带来的加成
        // ---------------------------------------------------------------

        /// <summary>
        /// 伏击状态下**攻击加成 +1**（玩家确认）。
        ///
        /// ⚠️ 用 <c>DefaultAttackBonus</c> 而不是 <c>AdjustAttackBonus</c>：
        ///    后者只在“数据层没注入过攻防”时才生效
        ///    （实际对局里 <c>CharacterFactory</c> 一定会注入），
        ///    于是伏击的 +1 永远生效不了。
        ///    写成 <c>DefaultAttackBonus</c> 就能让注入的基础值上再加伏击加成。
        /// </summary>
        // ⚠️ 必须走 `AdjustAttackBonus`，**不能**走 `DefaultAttackBonus`。
        //
        //    `AttackBonus` 的读法是
        //      AdjustAttackBonus(_statsApplied ? _attackBonus : DefaultAttackBonus)
        //    而实际对局里 `CharacterFactory` **一定会注入**攻防（于是 `_statsApplied == true`），
        //    这时读到的是注入值 `_attackBonus` —— 我第一版把 +1 写在
        //    `DefaultAttackBonus` 上，**在注入路径上根本不会被读到**，
        //    伏击的 +1 等于没加（测试当场抓到：期望 1、实际 0）。
        //
        //    写在 `AdjustAttackBonus` 里就能让"注入的基础值 + 伏击加成"。
        protected override int AdjustAttackBonus(int baseBonus)
            => IsAmbushing ? baseBonus + AmbushAttackBonus : baseBonus;

        /// <summary>未注入时的默认值仍是 0（伏击加成由上一条统一叠加）。</summary>
        public override int DefaultAttackBonus => 0;

        /// <summary>伏击状态下攻击距离 3；否则 1。</summary>
        public int BaseAttackRangeEffective => IsAmbushing ? AmbushRange : BaseAttackRange;

        int IAttackRangeProvider.BaseAttackRange => BaseAttackRangeEffective;

        // ---------------------------------------------------------------
        // 效果改写：让上层知道"这次运算之后可以伏击"
        // ---------------------------------------------------------------

        /// <summary>
        /// 不改写效果类型 —— 猎人没有把某个数字变成别的东西。
        /// 他只需要一个"运算之后"的时机来询问是否伏击，那走
        /// <see cref="OnOperationResolved"/>（与狼人的"和为 7"同一处）。
        /// </summary>
        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
            => Hand.GetEffectType(actedHand?.Value ?? 0);

        public HandEffectType DisplayedEffectOf(int resultValue)
            => Hand.GetEffectType(resultValue);

        // ---------------------------------------------------------------
        // 抵伤：伏击挡下一次伤害（在太极之后）
        // ---------------------------------------------------------------

        /// <summary>
        /// 伏击抵伤。
        ///
        /// \u26a0\ufe0f 触发时机**在武僧太极之后**（玩家确认）：
        ///    本方法是 <see cref="IDamageMitigator"/>，被 `AttackContext` 在
        ///    **逐段结算之前**询问；而双将容器按"主将在前"的顺序询问，
        ///    所以"武僧主将 + 猎人副将"天然就是太极先、
        ///    只有太极没吃下这次伤害时才会问到猎人。
        ///
        /// \u26a0\ufe0f 咒术师的攻击**穿透**伏击：既不抵挡、也**不解除**伏击。
        /// </summary>
        bool IDamageMitigator.TryMitigate(PlayerState owner, AttackContext context,
                                          int segmentDamage, int stagesUsed,
                                          out int stages, out int totalReduced,
                                          out int totalReflect)
        {
            stages = 0;
            totalReduced = 0;
            totalReflect = 0;

            if (!IsAmbushing) return false;
            if (segmentDamage <= 0) return false;

            // 咒术攻击穿透：不抵挡、也不解除伏击
            if (MonkLogic.PiercesMitigation(context)) return false;

            // 伏击抵挡**整次伤害**（这是一个"消耗掉伏击"的免伤，不是减免）
            stages = 1;
            totalReduced = segmentDamage;
            totalReflect = 0;

            ReleaseAmbush();
            return true;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _ambushHand = null;

            // 重开一局必须把手上的暗置状态也清掉 ——
            // 否则第二局开局那只手仍然是"不能碰"的，而伏击状态已经没了。
            if (owner != null)
            {
                foreach (Hand h in owner.Hands) h?.Reveal();
            }
        }
    }
}
