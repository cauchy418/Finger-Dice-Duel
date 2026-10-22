using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 一套**手值价值表**：告诉 AI "哪些起手数字对我有用、哪些组合值得凑"。
    ///
    /// ## 为什么需要它
    ///
    /// 开局掷骰后要在若干**合法拆分**里挑一个。挑哪个不是随便的 ——
    /// 每个角色的技能决定了不同的数字有不同的价值。
    ///
    /// ## 价值的两个来源
    ///
    /// **① 数字自身的价值**（<see cref="WeightOf"/>）
    ///
    /// 开局的手值**不会自动产生效果** —— 4 不会立刻打 4 点、5 也不会立刻成盾，
    /// 必须**经过一次运算**（自己的手 + 对手的手）才会触发。
    /// 所以价值 = "这个数字有多容易在后续运算里打出有用的结果"。
    ///
    /// 实测（对任意目标 T，满足 `(N + o) % 10 == T` 的对手手值 o **有且只有一个**，
    /// 所以"单数字凑任何目标"的难度**恒定**），区分度来自两个维度：
    ///
    /// | 维度 | 1/2/3/7 | 4/5/6/8/9 |
    /// |---|---|---|
    /// | 覆盖面：能凑到几个有用目标（4/5/6/8/9） | **5/5** | 4/5（各缺"自己"） |
    /// | 组合面：能与几个伙伴凑出有用结果 | **5/9** | 4/9 |
    ///
    /// 两个维度都判定 **1/2/3/7 高于 4/5/6/8/9**。
    /// 而 4/5/6/8/9 本身**就是效果**（打到就是攻击/盾），所以给它们一点补偿。
    ///
    /// ⚠️ **7 的定位很特殊**：它本身没有任何效果，而且
    /// "要和为 7"（野蛮人·野兽道途）与"要有 7"（游荡者·刺客）是**互斥**的目标 ——
    /// 对野蛮人·野兽道途来说 7 是**死牌**（7 + o 永远回不到 7），所以野蛮人·野兽道途必须把 7 调低。
    ///
    /// **② 两只手之间的组合**（<see cref="Combos"/>）
    ///
    /// 例如野蛮人·野兽道途要"和为 7"、战士·剑士要"双 4/双 6/双 8"、游荡者·风流剑客要"{1, 2} 同持"。
    /// 这些没法用单个数字的权重表达，所以单独列出来。
    ///
    /// ## 怎么调平衡
    ///
    /// 全部是**公开可变字段**且集中在 <see cref="HandValueProfiles"/> 里，
    /// 调平衡只改那张表，不需要动选点逻辑。
    /// </summary>
    public sealed class HandValueProfile
    {
        /// <summary>手值范围。</summary>
        public const int MinValue = DiceRollSetup.MinHandValue;   // 1
        public const int MaxValue = DiceRollSetup.MaxHandValue;   // 9

        /// <summary>
        /// 各数字的权重（下标 0 不用，用 1..9）。
        ///
        /// 默认值来自实测的"覆盖面 + 组合面"综合结论，见类注释。
        /// </summary>
        public readonly int[] Weights = new int[MaxValue + 1];

        /// <summary>组合加成（见 <see cref="ComboRule"/>）。</summary>
        public readonly List<ComboRule> Combos = new List<ComboRule>();

        /// <summary>用一组基线权重建表。</summary>
        public HandValueProfile(int[] weights = null)
        {
            int[] src = weights ?? Baseline;
            for (int i = MinValue; i <= MaxValue; i++)
                Weights[i] = (src != null && i < src.Length) ? src[i] : 50;
        }

        /// <summary>
        /// **默认（白板）权重**。
        ///
        /// | 手值 | 权重 | 覆盖 | 组合 | 自身是效果 |
        /// |---:|---:|---|---|---|
        /// | 1 / 2 / 3 / 7 | 60 | 5/5 | 5/9 | 否 |
        /// | 5 | 55 | 4/5 | 4/9 | 是（护盾） |
        /// | 4 / 6 / 8 / 9 | 50 | 4/5 | 4/9 | 是 |
        /// </summary>
        public static readonly int[] Baseline = { 0, 60, 60, 60, 50, 55, 50, 60, 50, 50 };

        /// <summary>某个手值的权重（越界返回 0）。</summary>
        public int WeightOf(int value)
            => (value >= MinValue && value <= MaxValue) ? Weights[value] : 0;

        /// <summary>调整某个手值的权重（可正可负，会被夹到 0..1000）。</summary>
        public HandValueProfile Adjust(int value, int delta)
        {
            if (value < MinValue || value > MaxValue) return this;
            Weights[value] = Clamp(Weights[value] + delta);
            return this;
        }

        /// <summary>直接设定某个手值的权重。</summary>
        public HandValueProfile Set(int value, int weight)
        {
            if (value < MinValue || value > MaxValue) return this;
            Weights[value] = Clamp(weight);
            return this;
        }

        /// <summary>加一条组合加成。</summary>
        public HandValueProfile AddCombo(ComboRule rule)
        {
            if (rule != null) Combos.Add(rule);
            return this;
        }

        private static int Clamp(int v) => v < 0 ? 0 : (v > 1000 ? 1000 : v);

        // ============================================================
        // 实例化
        // ============================================================

        /// <summary>复制一份（用于在基线上叠加角色 / 对手修正而不污染原表）。</summary>
        public HandValueProfile Clone()
        {
            var copy = new HandValueProfile(Weights);
            copy.Combos.AddRange(Combos);
            return copy;
        }

        // ============================================================
        // 评分
        // ============================================================

        /// <summary>
        /// 给一个拆分打分（越高越想要）。
        ///
        /// = 两只手的数字权重 + 命中的组合加成 + 手值本身就是效果的补偿。
        /// </summary>
        public int Score(HandSplit split)
        {
            int total = WeightOf(split.Left) + WeightOf(split.Right);

            for (int i = 0; i < Combos.Count; i++)
                if (Combos[i].Matches(split.Left, split.Right)) total += Combos[i].Bonus;

            return total;
        }
    }

    /// <summary>组合加成的类型。</summary>
    public enum ComboKind
    {
        /// <summary>两只手的和（十进制归一化）等于 <see cref="ComboRule.Target"/>。</summary>
        SumEquals = 0,

        /// <summary>两只手里**包含**所有 <see cref="ComboRule.RequiredValues"/>。</summary>
        ContainsAll = 1,

        /// <summary>两只手相等，且该值在 <see cref="ComboRule.RequiredValues"/> 里。</summary>
        DoublesOf = 2,
    }

    /// <summary>一条组合加成规则。</summary>
    public sealed class ComboRule
    {
        public ComboKind Kind { get; private set; }

        /// <summary>目标数字（<see cref="ComboKind.SumEquals"/> / <see cref="ComboKind.ReachTarget"/> 用）。</summary>
        public int Target { get; private set; }

        /// <summary>需要的数字集合（<see cref="ComboKind.ContainsAll"/> / <see cref="ComboKind.DoublesOf"/> 用）。</summary>
        public int[] RequiredValues { get; private set; }

        /// <summary>加成分值。</summary>
        public int Bonus { get; private set; }

        /// <summary>说明（调试与文档用）。</summary>
        public string Reason { get; private set; }

        private ComboRule(ComboKind kind, int target, int[] required, int bonus, string reason)
        {
            Kind = kind;
            Target = target;
            RequiredValues = required ?? new int[0];
            Bonus = bonus;
            Reason = reason ?? string.Empty;
        }

        public static ComboRule SumEquals(int target, int bonus, string reason = null)
            => new ComboRule(ComboKind.SumEquals, target, null, bonus, reason);

        public static ComboRule ContainsAll(int bonus, string reason, params int[] values)
            => new ComboRule(ComboKind.ContainsAll, 0, values, bonus, reason);

        public static ComboRule DoublesOf(int bonus, string reason, params int[] values)
            => new ComboRule(ComboKind.DoublesOf, 0, values, bonus, reason);

        /// <summary>这对数字是否命中本规则。</summary>
        public bool Matches(int a, int b)
        {
            switch (Kind)
            {
                case ComboKind.SumEquals:
                    // ⚠️ 注意用的是"十进制归一化"（和=10 变 1），
                    // 与真实运算完全一致 —— 否则野蛮人·野兽道途会以为 3+4 之外的组合也能凑 7。
                    return Hand.ComputeSum(a, b) == Target;

                case ComboKind.ContainsAll:
                    for (int i = 0; i < RequiredValues.Length; i++)
                        if (a != RequiredValues[i] && b != RequiredValues[i]) return false;
                    return true;

                case ComboKind.DoublesOf:
                    if (a != b) return false;
                    for (int i = 0; i < RequiredValues.Length; i++)
                        if (a == RequiredValues[i]) return true;
                    return false;

                default:
                    return false;
            }
        }

        public override string ToString()
            => $"{Kind}(target={Target}, bonus={Bonus}) {Reason}";
    }
}
