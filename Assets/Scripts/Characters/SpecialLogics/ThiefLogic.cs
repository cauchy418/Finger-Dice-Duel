namespace FingerDiceDuel.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// **盗贼**（攻 0 / 防 −1，攻击距离 1）。
    ///
    /// 他可以**放弃运算**，改为**偷窃**别人的数字：
    ///   ① 点自己的一只手（作为偷窃的"载体"）
    ///   ② 点「偷窃」按钮
    ///   ③ 点别人的一只手 —— 完成偷窃
    ///
    /// ## 判定（玩家给的规则）
    ///
    /// > 偷窃前的数字 **大于等于** 偷窃后的数字即生效。
    ///
    /// 例：
    ///   · 原 9 偷 4 → 9 ≥ 4 → 生效，按**偷来的 4** 发起攻击
    ///   · 原 2 偷 8 → 2 &lt; 8 → **不生效**（交换照做，但没有效果）
    ///   · 原 7 偷 5 → 7 ≥ 5 → 生效，**合成一面盾牌**
    ///     （玩家原话："无论之前这个 5 是什么状态，盗贼都会合成一面盾牌"）
    ///
    /// ## 交换与状态（玩家给的规则）
    ///
    /// > 双方点数互换，但**只有盗贼自己**可能触发新效果；
    /// > 被偷的人**仅仅是被变换了点数、不产生效果**。
    ///
    /// > 盗贼以完整的盾去换别人完整的盾，会**换来一面完整的盾**，
    /// > 而**对面的盾破碎**。
    /// > **太极其实就是破碎的盾**，仅仅因为武僧会给破碎的盾一个太极效果。
    ///
    /// 所以状态是**跟着数值一起交换**的：
    ///   · 完整盾 ↔ 完整盾（换完两边都还是完整盾，只是数值不同）
    ///   · 盗贼拿**碎盾**去换**完整盾** → 盗贼得到完整盾、对手得到碎盾
    ///   · 太极按"碎盾"参与交换，由武僧的逻辑重新解释
    ///
    /// ## 冷却
    ///
    /// > 若上回合**发起了**偷窃，本回合不能继续偷窃。
    ///
    /// "发起"= **只要点了偷窃就算**（哪怕判定不生效也算用掉了）。
    ///
    /// ## 其他
    ///
    /// · **无限距离**、**可以偷队友**
    /// · 消耗整个回合
    /// </summary>
    public sealed class ThiefLogic : CharacterTemplate, IPreTurnChoiceProvider
    {
        public override string DisplayName => "盗贼";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => -1;

        /// <summary>攻击距离 1。</summary>
        public override int BaseAttackRange => 1;

        /// <summary>
        /// **武僧的 0 被偷走时只能得到 1**（玩家补充规则）。
        ///
        /// > "盗贼偷武僧的 0 只能偷到 1，目前**只有武僧能持有 0**。"
        ///
        /// 也就是 0 是武僧太极形态的专属产物，**不可能流通到别人手上** ——
        /// 一旦经由偷窃换手就按"被触碰"处理（与
        /// <see cref="MonkLogic.OnTouchedAsAddend"/> 同一条规则）。
        /// </summary>
        public const int MonksZeroBecomesOne = 0;

        /// <summary>偷到 0 时实际得到的值（1）。</summary>
        public const int ZeroStolenBecomes = 1;

        /// <summary>
        /// 本回合能选择的偷窃目标（由上层在收集阶段填好），
        /// 结算时回读。
        /// </summary>
        public const int StealValue = 0;

        // ============================================================
        // 偷窃本身
        // ============================================================

        /// <summary>本次偷窃的判定结果。</summary>
        public readonly struct StealOutcome
        {
            /// <summary>盗贼偷窃前的点数（"原先"）。</summary>
            public int Original { get; }

            /// <summary>偷来的点数（"偷窃后"）。</summary>
            public int Stolen { get; }

            /// <summary>偷来的点数算不算"生效"（原先 ≥ 偷来的）。</summary>
            public bool Effective => Original >= Stolen;

            public StealOutcome(int original, int stolen)
            {
                Original = original;
                Stolen = stolen;
            }
        }

        /// <summary>
        /// **执行一次偷窃**：交换两只手的点数与状态，并返回判定结果。
        ///
        /// ⚠️ 交换**无条件发生**（"交换照做，只是没有效果"）；
        ///    "生效"只决定**要不要按偷来的数字触发效果**。
        ///
        /// 状态交换的规则（玩家给的）：
        ///   · 两边都是完整盾 -> 换完两边都还是完整盾
        ///   · 一边完整、一边破碎 -> 完整的那面**随数值一起**过去
        ///   · 太极按"碎盾"参与交换，之后由武僧的逻辑重新解释
        /// </summary>
        public static StealOutcome Steal(Hand mine, Hand theirs)
        {
            if (mine == null || theirs == null) return new StealOutcome(0, 0);

            int original = mine.Value;
            int stolen = theirs.Value;

            // 交换数值（SetValue 会把状态清成 None，所以状态要单独处理）
            mine.SetValue(stolen);
            theirs.SetValue(original);

            // ---- 状态规则（玩家逐条确认）----
            //
            // ① **状态只在数值仍然是 5 时才保留** ——
            //    否则会出现"值为 3 的碎盾"这种怪异状态。
            // ② **被偷的人拿到完整盾时，它变成碎盾**（玩家原话："它变成碎盾"）——
            //    因为他没有"合盾"这个动作。
            // ③ **盗贼偷到 5 时**（玩家最新规则）：
            //      · 用 **≥ 5** 的数字去偷 5 -> **完整盾**
            //      · 用 **1~4** 的数字去偷 5 -> **破碎的盾**
            //    直觉解释：自己带过去的底子够厚（≥5 本来就能当盾），
            //    才撑得起一面完整的盾。
            // ④ **不继承太极**（玩家最新规则）：
            //    "对于武僧，盗贼不会偷到一个带有太极的 5，
            //     他只会偷到盾 / 破碎的 5" ——
            //    太极是武僧专属的形态，换到别人手上只是普通盾。
            //
            // 盗贼这边
            if (stolen != 5)
            {
                mine.SetShieldState(ShieldState.None);
            }
            else if (original >= 5)
            {
                mine.SetShieldState(ShieldState.Formed);      // ≥5 去偷 5 -> 完整盾
            }
            else
            {
                mine.SetShieldState(ShieldState.Broken);      // 1~4 去偷 5 -> 碎盾
            }

            // 被偷者那边：拿到的那只手若仍是 5，状态按"他没合盾"处理
            // ⚠️ 若被偷者是**武僧**，他收到 5 会变成**太极**（玩家最新规则），
            //    由 MonkLogic.OnOtherPlayerStoleFromMe 处理 —— 见下面那个方法。
            if (original != 5)
            {
                theirs.SetShieldState(ShieldState.None);
            }
            else
            {
                theirs.SetShieldState(ShieldState.Broken);
            }

            return new StealOutcome(original, stolen);
        }

        /// <summary>
        /// **被偷者收到 5 之后的状态修正**（玩家最新规则）。
        ///
        /// "同理**武僧收到一个 5 也只会是太极状态**" ——
        /// 也就是这一条要由武僧自己解释，别人收到就是普通盾/碎盾。
        ///
        /// 由 <see cref="GameManager"/> 在交换之后调用。
        /// </summary>
        public static void ApplyVictimShieldSemantics(PlayerState victim, Hand received)
        {
            if (victim == null || received == null) return;
            if (received.Value != 5) return;

            // 武僧：5 就是太极形态（与开局那个 5 同一条规则）
            if (victim.Character is MonkLogic)
                received.SetShieldState(ShieldState.Taichi);
        }

        /// <summary>
        /// 偷来的数字会触发什么效果（**只看数字**，与大小关系无关）。
        ///
        /// 玩家确认："只看数字（4/5/6/8/9 就有效果）。"
        ///
        /// ⚠️ 注意 5：玩家说"无论之前这个 5 是什么状态，
        ///    盗贼都会**合成一面盾牌**" —— 所以 5 一律算**完整盾**，
        ///    不像普通运算那样受"已碎"影响。
        /// </summary>
        public static HandEffectType EffectOfStolen(int stolen)
            => Hand.GetEffectType(stolen);

        /// <summary>偷到 5 时，盗贼得到的是**完整盾**（玩家明确要求）。</summary>
        public static bool StolenFiveBecomesWholeShield => true;
    }
}
