using System;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **赌徒**（攻 0 / 防 0，攻击距离 1）。
    ///
    /// 玩家确认的规则（逐条）：
    ///   · 运算时可以选择**赌博**：先点自己一只手，再点右侧「开赌」，
    ///     掷 **1d9**，**结果直接作为本次行动的结果**
    ///     （4/6/8 攻击、9 回血、5 合盾）
    ///   · 赌博**不需要**点他人的手 —— 打谁在**结果出来之后**再选
    ///   · 若**上一个自己的回合**做了正常运算（非赌博），
    ///     则**本回合必须赌博**，且点他人的手会**弹提示并拒绝**
    ///   · 跳过回合**不算**"正常运算"（不触发强制）
    ///   · 赌博**可以放弃**（与所有角色一样，攻击都不能放弃；
    ///     但"开赌"是可选动作 —— 不点就是放弃攻击）
    /// </summary>
    public sealed class GamblerLogic : CharacterTemplate
    {
        public override string DisplayName => "赌徒";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 0;

        /// <summary>攻击距离 1（玩家确认）。</summary>
        public override int BaseAttackRange => 1;

        /// <summary>赌博骰子的面数（1d9）。</summary>
        public const int GambleDie = 9;

        /// <summary>
        /// 掷一次赌博骰。返回 1..9。
        ///
        /// ⚠️ 随机源通过参数注入 —— 单元测试必须能确定性复现
        ///    （否则"赌出 5 就是盾"这类规则无法稳定验证）。
        /// </summary>
        public static int Roll(Random rng)
        {
            if (rng == null) rng = new Random();
            return rng.Next(1, GambleDie + 1);
        }

        /// <summary>
        /// 把赌博结果翻译成效果类型。
        ///
        /// 4 / 6 / 8 -> 攻击；9 -> 回复；5 -> 护盾；其余（1/2/3/7）-> 无效果。
        ///
        /// 直接复用 <see cref="Hand.GetEffectType"/> ——
        /// 这样"赌出的数字"与"运算出的数字"走**同一套**规则，
        /// 不会出现两处各写一份而漂移。
        /// </summary>
        public static HandEffectType EffectOf(int roll) => Hand.GetEffectType(roll);

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            // 强制赌博状态属于"跨回合"的，但**新开一局必须清零**
            owner.MustGamble = false;
        }
    }
}
