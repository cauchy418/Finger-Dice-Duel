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
        ISelfAddendProvider, IHandEffectRewriter, IHandEffectDisplay
    {
        /// <summary>魔术师能把结果 2 当攻击打出的伤害（吃攻防加成）。</summary>
        public const int MagicAttackValue = 2;

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
            if (result == MagicAttackValue && !_isSelfAddend)
                return HandEffectType.Attack4;

            return Hand.GetEffectType(result);
        }

        /// <summary>显示：结果 2 也要显示成"攻击"（红），否则界面与真实结算不一致。</summary>
        public HandEffectType DisplayedEffectOf(int resultValue)
        {
            if (resultValue == MagicAttackValue) return HandEffectType.Attack4;
            return Hand.GetEffectType(resultValue);
        }

        /// <summary>本次运算是不是"拿自己的手当被加数"（由上层在运算前告知）。</summary>
        private bool _isSelfAddend;

        /// <summary>
        /// 由上层在**运算前**调用，告知这一次是不是自己加自己。
        ///
        /// \u26a0\ufe0f 为什么需要它：结果 2 只有在**自己加自己**时才当攻击。
        ///    若把 2 无条件变成攻击，那么"和对手的 1 运算出 2"也变成攻击 ——
        ///    那不是玩家说的规则（他指的是"自己的手互相凑"）。
        /// </summary>
        public void MarkSelfAddend(bool value) => _isSelfAddend = value;

        /// <summary>
        /// 追加伤害设定：把这次的伤害固定成 <see cref="MagicAttackValue"/>（吃攻防加成）。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            if (context.AttackValue != MagicAttackValue) return;

            // \u26a0\ufe0f 必须同时把段数压成 1：`Attack4` 只是借来的"攻击类型"，
            //    若不压，别的机制可能按 4 去分段（例如双将里配魔射手）。
            context.HitCount = 1;
            context.FinalDamage = MagicAttackValue;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _isSelfAddend = false;
        }
    }
}
