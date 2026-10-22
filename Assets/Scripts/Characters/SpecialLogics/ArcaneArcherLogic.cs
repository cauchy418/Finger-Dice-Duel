using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **战士·魔射手**（攻 0 / 防 −1，**攻击距离 3**）。
    ///
    /// 玩家确认的规则（都按**攻击数字**区分）：
    ///   · **4** -> 改为**两段各 4 点**伤害（各自独立判盾 / 判闪避）
    ///   · **6** -> 伤害 **+2**
    ///   · **8** -> **无视盾牌**（但仍可被闪避、可被邪术师·至高妖精取消）
    ///
    /// ⚠️ 与武僧·忍宗的区别：武僧·忍宗是"两段**伤害减半**"（4→2+2、6→3+3、8→4+4），
    ///    而战士·魔射手是"两段**各为原值**"（4→4+4）—— 所以它更强，
    ///    代价是攻 0 / 防 −1 与"只有 4 才有这个待遇"。
    /// </summary>
    public sealed class ArcaneArcherLogic : CharacterTemplate, IAttackDamageRule,
                                            IAttackRangeProvider, IMultiHitAttack
    {
        public override string DisplayName => "战士·魔射手";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离 3（玩家确认）。</summary>
        public override int BaseAttackRange => 3;

        /// <summary>4 的攻击段数。</summary>
        public const int FourHitCount = 2;

        /// <summary>6 的额外伤害。</summary>
        public const int SixBonusDamage = 2;

        /// <summary>需要两段攻击时，提前告诉上层段数（每段各问一次闪避）。</summary>
        public int MaxHits => FourHitCount;

        /// <summary>
        /// 按攻击数字改写伤害与护盾规则。
        ///
        /// ⚠️ 必须在**伤害已定、护盾判定之前**调用 —— 由
        ///    <see cref="AttackContext"/> 在攻击方钩子之后、逐段结算之前调用。
        /// </summary>
        public void ApplyAttackRule(AttackContext context, int attackValue, GameManager manager)
        {
            if (context == null) return;

            switch (attackValue)
            {
                case 4:
                    // 两段各 4：段数交给 AttackContext 平均分配，
                    // 而"各为 4"意味着伤害总量是 8 —— 所以先把总伤害翻倍。
                    //
                    // ⚠️ 不能只设 HitCount = 2：那会把 4 平分成 2 + 2（武僧·忍宗的做法）。
                    context.FinalDamage *= FourHitCount;
                    context.HitCount = FourHitCount;
                    manager?.NoteSkill(context.Attacker, "魔射连击", "两段攻击，各以 4 为基础伤害");
                    break;

                case 6:
                    context.FinalDamage += SixBonusDamage;
                    manager?.NoteSkill(context.Attacker, "魔射增伤", "基础伤害＋2");
                    break;

                case 8:
                    // 无视盾牌 —— 与法师·咒法学派同一条通路。
                    // 闪避不受影响（规则只说"无视盾牌"）。
                    context.IgnoreShield = true;
                    manager?.NoteSkill(context.Attacker, "魔射穿盾", "本次攻击无视护盾");
                    break;
            }
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>战士·魔射手：**穿刺伤害**（玩家指定）。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Piercing;

}
}
