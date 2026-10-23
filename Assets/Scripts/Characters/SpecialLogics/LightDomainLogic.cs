using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 牧师·光明领域 LightDomain（攻防 0 / -1，攻击距离无限）
    ///
    /// 技能（玩家口径，2026-10-23）：
    ///   1. **攻击恒为 4 点光耀伤害**：4/6/8 的最终伤害固定为 4，
    ///      无视攻防加成与数值增减益（倒伏 ×1.5 等不参与）；
    ///      护盾 / 闪避 / 转移 / 太极 / 抗性 / 屏障等**防御机制照常**。
    ///   2. **命中附带圣光（2 回合）**：施加时立即清除目标其他负面状态与全部抗性
    ///      （勇气战歌增益与奥术屏障是独立系统，不受影响）；
    ///      圣光存在期间目标不能被附加新状态与新抗性；
    ///      目标回合开始受 1 点（不可抵挡）、回复量减半（向上取整）。
    ///      命中口径与技能流血一致：盾挡 / 太极减免算命中，闪避 / 取消不算。
    ///
    /// 净化 / 阻止附加 / 减半 / 回合伤害的规则本体在
    /// <see cref="PlayerState.ApplyStatus"/> / <see cref="PlayerState.Heal"/> /
    /// <see cref="PlayerState.AddResistance"/> / <see cref="StatusRules.TurnStartDamage"/>
    /// —— 那里是所有路径的唯一入口，这里只负责"把圣光挂上"。
    ///
    /// 本角色没有跨回合状态。
    /// </summary>
    public sealed class LightDomainLogic : CharacterTemplate, ILethalThresholdAdvisor, IFixedNormalAttackDamage
    {
        /// <summary>攻击的固定伤害（玩家口径："造成的伤害固定为 4"）。</summary>
        public const int FixedDamage = 4;
        public int NormalAttackDamage => FixedDamage;
        public AttackDamageType NormalAttackType => AttackDamageType.Radiant;

        public override string DisplayName => "牧师·光明领域";
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>
        /// 所有**正常发起**的攻击改为：光耀类型 + 固定 4 伤害 + 圣光覆盖。
        ///
        /// ⚠️ 特殊攻击（剑士追加 / 奉献冲击 / 太极反弹 / 死灵免费暗蚀）**不接管**：
        ///    那些是搭档技能自己的伤害口径，双将时不把它们的数值改写成 4、
        ///    也不给它们挂圣光。攻击类型已带（反弹继承原类型）时同样不覆盖。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            if (context.IsStatusDamage || context.IsSpecialAttack || context.IsReflected || context.IsNecromancyFreeAttack) return;

            context.AttackType = AttackDamageType.Radiant;
            context.FinalDamage = FixedDamage;
            context.IgnoreStatBonuses = true;          // 无视攻防加成
            context.IgnoreIncomingModifiers = true;    // 无视倒伏 ×1.5 等目标侧增减
            context.AttackerStatusOverride = StatusEffect.HolyLight;
        }

        /// <summary>攻击类型兜底（OnResolveAttack 已设类型时不会被问到）。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Radiant;

        /// <summary>
        /// 斩杀线估算：任何攻击数字都是固定 4 点（攻防不参与）。
        /// AI 经 IFixedNormalAttackDamage 优先取固定值，避免双将容器较大估算覆盖。
        /// </summary>
        public int EstimateLethalDamage(LethalContext context, int resultValue,
                                        int attackBonus, int defenseBonus)
            => FixedDamage;
    }
}
