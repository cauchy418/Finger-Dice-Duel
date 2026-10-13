using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **角色价值表**：每个角色看重哪些手值、想凑什么组合。
    ///
    /// 调平衡只改这张表。每一项都附了机制依据，改之前请先读依据 ——
    /// 这些数字不是拍脑袋来的，而是从角色技能反推的。
    ///
    /// ## 基线（所有角色共享）
    ///
    /// | 手值 | 权重 | 理由 |
    /// |---:|---:|---|
    /// | 1 / 2 / 3 | 60 | 覆盖面 5/5、组合面 5/9（最高），但自身无效果 |
    /// | 7 | 60 | 同上（覆盖面满），**但角色会改它** —— 对狼人是死牌 |
    /// | 5 | 55 | 合盾；覆盖面与组合面次之（4/5、4/9） |
    /// | 4 / 6 / 8 / 9 | 50 | 同上，但它们**本身就是效果**（打到就是攻击/盾/回复） |
    ///
    /// ## 对手修正
    ///
    /// 面对某些角色时，**我自己**的某些数字会贬值（例如面对咒术师时 5 贬值，
    /// 因为他的攻击无视护盾）。这类修正单独列在 <see cref="OpponentAdjust"/> 里。
    /// </summary>
    public static class HandValueProfiles
    {
        /// <summary>角色对自己的价值表。</summary>
        private static readonly Dictionary<string, HandValueProfile> Own =
            new Dictionary<string, HandValueProfile>();

        /// <summary>面对该角色时，我自己的价值表要做的额外修正。</summary>
        private static readonly Dictionary<string, List<ValueAdjust>> OpponentAdjust =
            new Dictionary<string, List<ValueAdjust>>();

        private sealed class ValueAdjust
        {
            public int Value;
            public int Delta;

            public ValueAdjust(int value, int delta) { Value = value; Delta = delta; }
        }

        static HandValueProfiles()
        {
            BuildOwn();
            BuildOpponentAdjust();
        }

        // ============================================================
        // 角色自己的价值表
        // ============================================================

        private static void BuildOwn()
        {
            // ---- 士兵（攻防各 +1，取代了原来的白板）：只用基线 ----
            Own[CharacterDefinition.SoldierId] = new HandValueProfile();

            // ---- 战士（攻 +1）----
            // 5 = 成盾（挡一次伤害）。盾还能当运算源，主动碎掉触发**连锁破盾**
            // （再击碎场上一面成型盾）—— 所以 5 对他的价值尤其高。
            Own[CharacterDefinition.WarriorId] = new HandValueProfile()
                .Set(5, 95)                       // 成盾
                .Set(8, 60).Set(9, 58)            // 高伤攻击 / 回复
                .Set(7, 55)                       // 攻击 +1 使 7 略亏（7 非效果）
                .AddCombo(ComboRule.ContainsAll(60, "有 5 就能合盾", 5));

            // ---- 咒术师（攻 -1，攻击无视护盾与正向防御）----
            // ⚠️ 他的攻击无视**别人**的盾 —— 所以**他自己的盾是净赚的**
            // （对方挡不住他，他却能挡住对方）。因此 5 对他价值很高。
            // 攻 -1 使他更需要高数值来补伤害。
            Own[CharacterDefinition.CurseweaverId] = new HandValueProfile()
                .Set(5, 95)                       // 自己合盾 = 净赚
                .Set(8, 65).Set(6, 62)            // 补攻 -1 的伤害
                .AddCombo(ComboRule.ContainsAll(60, "有 5 就能合盾（他无视别人的盾）", 5));

            // ---- 野蛮人（防 +1）----
            // 5 = 成盾时**锁住对手一只手**（不能当运算源），强控制。
            Own[CharacterDefinition.BarbarianId] = new HandValueProfile()
                .Set(5, 95)                       // 合盾 + 锁手
                .Set(8, 60).Set(9, 58)
                .AddCombo(ComboRule.ContainsAll(70, "有 5 即可合盾并锁对手一只手", 5));

            // ---- 牧师（攻防 -1 / -1）----
            // 9 = 回复 9 血（近半条命）；4/6/8 可选当攻击或回复。
            // ⚠️ 但**起手 9 打不出 9**（9+1=10→1）—— 所以要的是"手上能凑到 9"，
            // 而不是"手值是 9"。9 仍然高（锁定了一个 9），但不能高到离谱。
            Own[CharacterDefinition.PriestId] = new HandValueProfile()
                .Set(9, 75)
                .Set(4, 70).Set(6, 70).Set(8, 72)   // 攻守兼备
                .Set(7, 50)                          // 对牧师无用
                .AddCombo(ComboRule.ContainsAll(70, "有 9 就能回复 9 血", 9))
                .AddCombo(ComboRule.ContainsAll(40, "有 4/6/8 可自由选择攻或回复", 4, 6, 8));

            // ---- 游侠（防 -2）----
            // 1 → 2 **完全闪避**一次攻击；2 → 3 让攻击基础值 ×1.5。
            // 所以 1 最好（闪避**并且**变成 2 之后还能强化），2 次之。
            // 同时拿到 1 和 2 是理想开局：既能闪避，又能强化。
            Own[CharacterDefinition.RangerId] = new HandValueProfile()
                .Set(1, 85)                       // 闪避（还能变成 2）
                .Set(2, 70)                       // 强化（也能由 1 变来）
                .Set(3, 45)                       // 多半是 2 变来的，本身没用
                .AddCombo(ComboRule.ContainsAll(90, "同时有 1 和 2：能闪避 + 能强化", 1, 2));

            // ---- 狼人（攻防 -2 / -2，生命 25）----
            // 运算后**双手数字之和为 7** -> 回复半血 + 攻防永久 +1。
            // ⚠️ "要有 7"和"要和为 7"是互斥的：7 + o 永远回不到 7，
            // 所以**7 对狼人是死牌**，必须大幅调低。
            Own[CharacterDefinition.WerewolfId] = new HandValueProfile()
                .Set(1, 75).Set(2, 72).Set(3, 72).Set(4, 68)   // 凑 7 的伙伴
                .Set(7, 15)                                      // 死牌
                .Set(5, 45).Set(6, 45).Set(8, 48).Set(9, 48)
                .AddCombo(ComboRule.SumEquals(7, 220, "双手之和为 7：回半血 + 攻防永久 +1"));

            // ---- 剑客（攻 +2）----
            // 运算出 4/6/8 且两手相同 -> 额外追加一次 4/6/8 伤害（无视攻防）。
            Own[CharacterDefinition.SwordsmanId] = new HandValueProfile()
                .Set(4, 68).Set(6, 68).Set(8, 72)
                .Set(5, 45).Set(1, 52).Set(2, 50).Set(3, 50)
                .AddCombo(ComboRule.DoublesOf(130, "双 4/双 6/双 8：追加一次攻击", 4, 6, 8));

            // ---- 忍者（攻 +1）----
            // 攻击拆成两段、伤害减半，**每段各自算攻防加成**。
            // 所以高值受益更大（8 → 4+4 还能各吃加成），4 最亏（2+2 各吃 +1）
            // —— 也就是"攻 +1 被吃了两次"，所以 4 对忍者价值偏低。
            Own[CharacterDefinition.NinjaId] = new HandValueProfile()
                .Set(8, 70).Set(6, 64)
                .Set(4, 35)                       // 两段各吃一次加成，最亏
                .Set(5, 52)
                .AddCombo(ComboRule.ContainsAll(60, "有 8 可打两段高伤", 8));

            // ---- 刺客（攻 +2）----
            // 两只手**都是 7**（且经运算达成）才进潜行：不能被攻击、可跳一回合、+7 伤害。
            // ⚠️ 开局直接摆出双 7 **不算**潜行（见 AssassinLogic）——
            // 但双 7 是"一运算就能维持 7"的最佳起点，所以仍然值钱。
            Own[CharacterDefinition.AssassinId] = new HandValueProfile()
                .Set(7, 90)                       // 潜行的种子
                .Set(8, 62).Set(5, 58)
                .AddCombo(ComboRule.DoublesOf(200, "双 7：一运算即可进入潜行", 7));
        }

        // ============================================================
        // 面对某角色时，我自己的价值表要做的修正
        // ============================================================

        private static void BuildOpponentAdjust()
        {
            // ---- 面对咒术师 ----
            // 他的攻击**无视护盾** -> 我合盾完全白费，5 大幅贬值。
            // 若我是游侠，他的攻击**不可闪避** -> 我的 1 也贬值。
            OpponentAdjust[CharacterDefinition.CurseweaverId] = new List<ValueAdjust>
            {
                new ValueAdjust(5, -45),      // 盾对他无效
                new ValueAdjust(1, -30),      // 游侠的闪避对他无效（对别的角色无害）
            };

            // ---- 面对狼人 ----
            // 他要"双手之和为 7"（回半血 + 攻防永久 +1）-> 别把 7 送到他手上。
            // 注意：这是**对手估值**的修正，不是"我不能选 7"。
            OpponentAdjust[CharacterDefinition.WerewolfId] = new List<ValueAdjust>
            {
                new ValueAdjust(7, -50),
            };

            // ---- 面对游侠 ----
            // 他的 1 能闪避、2 能强化 -> 别给他这两样。
            OpponentAdjust[CharacterDefinition.RangerId] = new List<ValueAdjust>
            {
                new ValueAdjust(1, -45),
                new ValueAdjust(2, -40),
            };

            // ---- 面对剑客 ----
            // 他的双 4/双 6/双 8 会触发追加攻击 -> 别给他成对的机会。
            OpponentAdjust[CharacterDefinition.SwordsmanId] = new List<ValueAdjust>
            {
                new ValueAdjust(4, -35),
                new ValueAdjust(6, -35),
                new ValueAdjust(8, -35),
            };

            // ---- 面对刺客 ----
            // 他的手值 7 是潜行的种子 -> 别给他 7。
            OpponentAdjust[CharacterDefinition.AssassinId] = new List<ValueAdjust>
            {
                new ValueAdjust(7, -45),
            };
        }

        // ============================================================
        // 查询
        // ============================================================

        /// <summary>角色自己的价值表（拿不到角色时为基线）。</summary>
        public static HandValueProfile For(CharacterDefinition definition)
        {
            string id = definition?.Id;
            if (id != null && Own.TryGetValue(id, out HandValueProfile profile))
                return profile;

            return new HandValueProfile();
        }

        /// <summary>
        /// **我**在**面对 <paramref name="opponent"/>** 时该用的价值表。
        ///
        /// = 我角色的表 + 对手带来的修正（例如面对咒术师时我的 5 贬值）。
        /// </summary>
        public static HandValueProfile For(CharacterDefinition me, CharacterDefinition opponent)
        {
            HandValueProfile profile = For(me).Clone();

            string oppId = opponent?.Id;
            if (oppId != null && OpponentAdjust.TryGetValue(oppId, out List<ValueAdjust> adjusts))
            {
                for (int i = 0; i < adjusts.Count; i++)
                    profile.Adjust(adjusts[i].Value, adjusts[i].Delta);
            }

            return profile;
        }

        /// <summary>
        /// **对手**会用哪张表来评估"给他的这个拆分"。
        ///
        /// 用于"两个拆分对我差不多时，挑一个对他更差的"。
        /// </summary>
        public static HandValueProfile ValueToOpponent(CharacterDefinition opponent)
            => For(opponent);

        // ============================================================
        // 给"回合内走子评分"用的紧凑权重
        // ============================================================

        /// <summary>
        /// 权重缩放倍数。
        ///
        /// 开局选点只做**相对比较**，所以用大数字（50/60/85）更好读；
        /// 而回合内走子要把"手值价值"与"效果档位"（4/6/8 攻击 = 44~88）相加，
        /// 所以缩到同量级 —— 否则手值项会完全盖过效果项。
        /// </summary>
        public const int MoveScoreScale = 10;

        private static readonly Dictionary<string, int[]> MoveScoreCache =
            new Dictionary<string, int[]>();

        /// <summary>
        /// 该角色在**回合内走子评分**里用的权重数组（下标 1..9，已按
        /// <see cref="MoveScoreScale"/> 缩放）。
        ///
        /// 只反映"角色自己"的偏好（不含对手修正）—— 走子时面对的是
        /// 已经摆在桌上的手值，不再有"给对手分配"这层语义。
        /// </summary>
        public static int[] MoveScoreWeights(CharacterDefinition definition)
        {
            string id = definition?.Id ?? "";

            if (MoveScoreCache.TryGetValue(id, out int[] cached)) return cached;

            HandValueProfile profile = For(definition);
            var scaled = new int[HandValueProfile.MaxValue + 1];
            for (int v = HandValueProfile.MinValue; v <= HandValueProfile.MaxValue; v++)
                scaled[v] = profile.Weights[v] / MoveScoreScale;

            MoveScoreCache[id] = scaled;
            return scaled;
        }
    }
}
