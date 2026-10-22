namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **圣武士·王冠之誓**（攻 0 / 防 −1，攻击距离 1）。
    ///
    /// 玩家确认的规则：
    ///   · **自己的盾牌破碎后**，攻击距离**永久 +1**
    ///   · **攻击加成恒等于攻击距离** —— 两者联动，不是各自累加
    ///     （初始都是 1；碎一次 -> 距离 2、加成 2；碎两次 -> 距离 3、加成 3）
    ///   · 只有**自己的**盾碎才算（队友的盾不算）
    ///
    /// 实现要点：`AttackBonus` 与 `BaseAttackRange` 都是
    /// <see cref="CharacterTemplate"/> 的 virtual 属性，所以圣武士·王冠之誓把两者
    /// **都**接到同一个计数上即可保证"恒等于" —— 不可能漂移。
    /// </summary>
    public sealed class OathOfTheCrownLogic : CharacterTemplate
    {
        public override string DisplayName => "圣武士·王冠之誓";

        /// <summary>初始攻击距离（也是初始攻击加成）。</summary>
        public const int BaseRange = 1;

        /// <summary>本局以来**自己的**盾牌碎过几次。</summary>
        private int _shieldBreaks;

        /// <summary>
        /// 攻击加成恒等于攻击距离（玩家要求）。
        ///
        /// ⚠️ 必须用 <see cref="CharacterTemplate.AdjustAttackBonus"/> 这个钩子，
        ///    不能用 `DefaultAttackBonus` ——
        ///    后者只在工厂注入数据**之前**生效，注入之后恒被覆盖
        ///    （我第一版那样写，测试立刻抓到"恒为 0"）。
        /// </summary>
        protected override int AdjustAttackBonus(int baseBonus) => BaseAttackRange;

        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离 = 1 + 自己碎盾次数。</summary>
        public override int BaseAttackRange => BaseRange + _shieldBreaks;

        /// <summary>当前攻击加成（供界面/战报显示，与距离同值）。</summary>
        public int CurrentAttackBonus => BaseAttackRange;

        /// <summary>
        /// 自己的盾碎了（**主动碎盾与被打碎都会走这里** ——
        /// <see cref="GameManager.NotifyOwnShieldBroken"/> 在两条路径上都被调用）。
        /// </summary>
        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand,
                                              GameManager manager)
        {
            _shieldBreaks++;
            manager?.NoteSkill(owner, "王冠威仪", $"永久攻击／距离＋1（现 {BaseAttackRange}）");
        }

        /// <summary>
        /// ⚠️ 被动被打碎时 <see cref="GameManager.BreakShieldAndNotify"/> 会**同时**
        ///    调 OnOwnShieldBroken 与 OnOtherShieldBroken ——
        ///    所以这里**不能**再加计数，否则一次碎盾算两次。
        /// </summary>
        public override void OnOtherShieldBroken(PlayerState owner, Hand brokenHand,
                                                GameManager manager) { }

        /// <summary>跨回合保留（距离是永久成长），但新开一局必须清零。</summary>
        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _shieldBreaks = 0;
        }

        /// <summary>供测试与界面读取：已经碎过几次盾。</summary>
        public int ShieldBreakCount => _shieldBreaks;

        /// <summary>圣武士·王冠之誓：**穿刺伤害**（玩家指定）。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Piercing;

}
}
