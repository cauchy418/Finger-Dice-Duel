using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 开局掷骰分配初始手值（1d20）。
    ///
    /// ## 规则
    ///
    /// 双方各投 **1d20**，点数 = **两只手初始点数之和**（19 / 20 例外，见下）。
    /// 玩家在**合法拆分**里自选。
    ///
    /// **合法拆分**：两只手都在 `1..9`，且 `左 + 右 == N`。
    /// 因此单手的取值范围是 `max(1, N-9) .. min(9, N-1)`。
    ///
    /// | N | 单手下限 | 合法拆分 |
    /// |---|---|---|
    /// | 8 | 1 | 1+7 / 2+6 / 3+5 / 4+4 |
    /// | 12 | 3 | 3+9 / 4+8 / 5+7 / 6+6 |
    /// | 13 | 4 | 4+9 / 5+8 / 6+7 |
    /// | 18 | 9 | 9+9 |
    ///
    /// **特殊点数**：
    ///   · **1（大失败）**：不自己分配 —— 由**对手**替你指定，
    ///     而且对手是**完全自由**地指定两手（1..9 各自任选，不受总和约束）。
    ///   · **19（小成功）**：**一只手固定为 9**，另一只手任意（1..9）。
    ///     不受总和约束（9 + x 允许 10..18）。
    ///   · **20（大成功）**：两只手**完全随意**（1..9 各自任选），不受总和约束。
    ///
    /// **决定顺序**：点数**小**的玩家先决定，点数**大**的后决定
    /// （后决定者能看着对方的分配来选）。
    /// 点数**相同**则**同时**决定；两边都是大失败时**同时替对手指定**。
    ///
    /// ## 重要语义
    ///
    /// **初始手值不触发任何效果。**
    /// 初始 5 **不是**成型盾（更不是碎盾）、游荡者·刺客初始双 7 **不进入潜行**。
    /// 因此本类只写手的数值，绝不写护盾状态。
    /// </summary>
    public static class DiceRollSetup
    {
        /// <summary>骰子面数。</summary>
        public const int DieFaces = 20;

        /// <summary>单手最小 / 最大点数。</summary>
        public const int MinHandValue = 1;
        public const int MaxHandValue = 9;

        /// <summary>大失败：由对手替你分配。</summary>
        public const int CriticalFailure = 1;

        /// <summary>小成功：一只手必须是 9，另一只手任意。</summary>
        public const int MinorSuccess = 19;

        /// <summary>大成功：两手完全随意。</summary>
        public const int MajorSuccess = 20;

        public static bool IsCriticalFailure(int roll) => roll == CriticalFailure;
        public static bool IsMinorSuccess(int roll) => roll == MinorSuccess;
        public static bool IsMajorSuccess(int roll) => roll == MajorSuccess;

        /// <summary>
        /// 这次投掷是否**不受总和约束**。
        ///
        /// 包含三种情况：
        ///   · **1（大失败）**：由对手**完全自由**地指定两手
        ///   · **19（小成功）**：一只手必须是 9，另一手任意
        ///   · **20（大成功）**：两手完全随意
        ///
        /// ⚠️ 大失败原本不在其中（当时把它当成"总和 1"）——
        ///    玩家确认"1v1 大失败也是完全自由的"，所以已并入。
        /// </summary>
        public static bool IgnoresTotal(int roll)
            => roll >= MinorSuccess || IsCriticalFailure(roll);

        // ============================================================
        // 合法拆分
        // ============================================================

        /// <summary>
        /// 列出某次投掷的所有**合法拆分**（左手值由小到大）。
        ///
        /// 返回值**保证非空** —— 19 / 20 与 2..18 都至少有一种拆分；
        /// 若真出现无解（不应发生），返回单个"双方都取能取的值"的兜底。
        /// </summary>
        public static List<HandSplit> LegalSplits(int roll)
        {
            var list = new List<HandSplit>(9);

            if (IsMajorSuccess(roll) || IsCriticalFailure(roll))
            {
                // 大成功与大失败：两手**完全随意**（1..9 各自任选），不受总和约束。
                //
                // ⚠️ 大失败原本被当成"总和 1"，于是只给出 1/1 一种拆分 ——
                //    **那是错的**（玩家确认："1v1 大失败也是完全自由的"）。
                //    语义上大失败是"决定权送给对手"，对手本来就该能自由指定两手。
                for (int left = MinHandValue; left <= MaxHandValue; left++)
                    for (int right = MinHandValue; right <= MaxHandValue; right++)
                        list.Add(new HandSplit(left, right));
                return list;
            }

            if (IsMinorSuccess(roll))
            {
                // 小成功：一只手必须是 9，另一只手任意（两手都允许是 9）
                for (int other = MinHandValue; other <= MaxHandValue; other++)
                {
                    list.Add(new HandSplit(MaxHandValue, other));   // 左 9
                    if (other != MaxHandValue)
                        list.Add(new HandSplit(other, MaxHandValue)); // 右 9
                }
                return list;
            }

            return SplitsSummingTo(roll);
        }

        /// <summary>标准拆分：两手 1..9 且之和恰为 <paramref name="total"/>。</summary>
        private static List<HandSplit> SplitsSummingTo(int total)
        {
            var list = new List<HandSplit>(9);
            int lo = Math.Max(MinHandValue, total - MaxHandValue);
            int hi = Math.Min(MaxHandValue, total - MinHandValue);

            for (int left = lo; left <= hi; left++)
                list.Add(new HandSplit(left, total - left));

            if (list.Count == 0)
            {
                // 兜底（理论上到不了）：给一个两手都取下限的组合
                int v = Math.Max(MinHandValue, Math.Min(MaxHandValue, total / 2));
                list.Add(new HandSplit(v, Math.Max(MinHandValue, total - v)));
            }
            return list;
        }

        /// <summary>
        /// 列出**去重后**的合法拆分：镜像只保留一个（左手 ≤ 右手）。
        ///
        /// 例如 `1/5` 与 `5/1` 只保留 `1/5`。
        ///
        /// 为什么可以这么显示：**目前左右手的作用完全相同** ——
        /// 换哪只手只是把同一对数换个位置，游戏结果没有任何区别。
        /// 大成功的 81 种里有整整一半是镜像，全列出来界面根本放不下。
        ///
        /// 玩家若确实想按 `5/1` 那样摆，界面上有**翻转**按钮 ——
        /// 那只是显示顺序，不影响任何规则。
        /// </summary>
        public static List<HandSplit> DistinctSplits(int roll)
        {
            List<HandSplit> all = LegalSplits(roll);
            var result = new List<HandSplit>(all.Count);

            for (int i = 0; i < all.Count; i++)
            {
                // 每对 (a,b) 与 (b,a) 恰好有一个满足"左手 ≤ 右手"
                if (all[i].Left <= all[i].Right) result.Add(all[i]);
            }

            return result;
        }

        /// <summary>某个拆分是否对这次投掷合法。</summary>
        public static bool IsLegalSplit(int roll, HandSplit split)
        {
            var legal = LegalSplits(roll);
            for (int i = 0; i < legal.Count; i++)
                if (legal[i].Left == split.Left && legal[i].Right == split.Right) return true;
            return false;
        }

        // ============================================================
        // 投掷与先后顺序
        // ============================================================

        /// <summary>投一次 1d20。</summary>
        public static int Roll(Random rng)
            => (rng ?? new Random()).Next(1, DieFaces + 1);

        /// <summary>
        /// 决定"谁先分配自己的初始手值"。
        ///
        /// 规则：点数**小**的先决定，点数**大**的后决定；点数**相同**则同时。
        /// </summary>
        public static DecisionOrder DecideOrder(int rollA, int rollB)
        {
            if (rollA == rollB) return DecisionOrder.Simultaneous;
            return rollA < rollB ? DecisionOrder.ALower : DecisionOrder.BLower;
        }
    }

    /// <summary>一次合法的初始手值拆分。</summary>
    public readonly struct HandSplit
    {
        public readonly int Left;
        public readonly int Right;

        public HandSplit(int left, int right)
        {
            Left = left;
            Right = right;
        }

        public int Total => Left + Right;

        public override string ToString() => $"{Left} / {Right}";
    }

    /// <summary>
    /// 把"开局掷骰"的结果翻译成**界面文案**。
    ///
    /// 放在 Assistant（不依赖 Unity）而不是 UI 里，是为了让文案也能被测试 ——
    /// 否则"选项写错 / 少写"只能靠肉眼在编辑器里看。
    /// </summary>
    public static class DiceRollText
    {
        /// <summary>一个拆分的按钮文案，形如 `4 / 8`。</summary>
        public static string SplitLabel(HandSplit split) => $"{split.Left} / {split.Right}";

        /// <summary>掷骰结果的标题，形如 `战士·破阵者 12　vs　游荡者·风流剑客 15`。</summary>
        public static string RollLine(string nameA, int rollA, string nameB, int rollB)
            => $"{nameA} {rollA}　vs　{nameB} {rollB}";

        /// <summary>某个点数的特殊说明（没有则返回空串）。</summary>
        public static string SpecialNote(int roll)
        {
            if (DiceRollSetup.IsCriticalFailure(roll))
                return "大失败：由对手替你指定初始手值";
            if (DiceRollSetup.IsMinorSuccess(roll))
                return "小成功：一只手必须是 9，另一只手随意";
            if (DiceRollSetup.IsMajorSuccess(roll))
                return "大成功：两只手完全随意";
            return "";
        }

        /// <summary>
        /// 分配阶段的提示：
        ///   · 该谁决定、给谁分配、这次点数是多少、有什么特殊规则。
        /// </summary>
        public static string AssignHint(string deciderName, string assigneeName,
                                        int roll, bool namingOpponent)
        {
            var sb = new System.Text.StringBuilder();

            if (namingOpponent)
                sb.Append($"{deciderName} 投出 {roll}（大失败）：由你来替 {assigneeName} 指定初始手值");
            else
                sb.Append($"{assigneeName} 投出 {roll}");

            string note = SpecialNote(roll);
            if (!string.IsNullOrEmpty(note)) sb.Append("　—　").Append(note);

            if (!namingOpponent && !DiceRollSetup.IgnoresTotal(roll))
                sb.Append($"　（两手之和 = {roll}）");
            else if (!namingOpponent && DiceRollSetup.IsMinorSuccess(roll))
                sb.Append("　（9 + 任意 1..9）");

            return sb.ToString();
        }

        /// <summary>先后顺序的说明（投掷结果出来后显示）。</summary>
        public static string OrderNote(DecisionOrder order, string nameA, string nameB)
        {
            switch (order)
            {
                case DecisionOrder.ALower:
                    return $"{nameA} 点数更小，先分配；{nameB} 后分配。";
                case DecisionOrder.BLower:
                    return $"{nameB} 点数更小，先分配；{nameA} 后分配。";
                default:
                    return "双方点数相同 —— 同时分配。";
            }
        }

        /// <summary>
        /// **谁先行动**的说明（跟在顺序说明后面，避免玩家以为"先分配的也先动"）。
        ///
        /// 规则：点数**大**的先出招 —— 与"点数小的先决定手值"互补。
        /// </summary>
        public static string FirstToActNote(string firstName) =>
            $"点数大的 {firstName} 先行动。";
    }

    /// <summary>谁先分配初始手值。</summary>
    public enum DecisionOrder
    {
        /// <summary>点数相同 —— 双方同时分配。</summary>
        Simultaneous = 0,

        /// <summary>A 点数小，A 先分配。</summary>
        ALower = 1,

        /// <summary>B 点数小，B 先分配。</summary>
        BLower = 2,
    }

    /// <summary>
    /// 2v2 的**座位关系**：给定"我坐第几号位"，算出队友与对手分别坐哪几号位。
    ///
    /// 规则（玩家确认）：
    ///   · 四人围成环，**座位 1&4 一队、2&3 一队**
    ///   · 队友在环上**相邻** —— 所以队友一定是我的**上家或下家**
    ///
    /// 于是主视角的界面布局可以完全由座位号推出：
    ///   · 自己 → 下方正中
    ///   · 上家 → 偏左上（若是队友则改为偏左下）
    ///   · 下家 → 偏右上（若是队友则改为偏右下）
    ///   · 另外两位 → 剩下两个位置（都是对手）
    ///
    /// 纯函数、不依赖任何状态，所以能在无头测试里穷举验证。
    /// </summary>
    public static class SeatLayout
    {
        /// <summary>2v2 的座位数。</summary>
        public const int SeatCount = 4;

        /// <summary>把座位号规范到 1..4（环形回绕）。</summary>
        public static int Normalize(int seat)
        {
            int n = SeatCount;
            return ((seat - 1) % n + n) % n + 1;
        }

        /// <summary>我的**上家**座位号（环上前一个）。</summary>
        public static int PreviousSeat(int seat) => Normalize(seat - 1);

        /// <summary>我的**下家**座位号（环上后一个）。</summary>
        public static int NextSeat(int seat) => Normalize(seat + 1);

        /// <summary>
        /// 我的**队友座位号**。
        ///
        /// 规则（玩家确认）：**第一个行动的与第四个行动的是一队**、
        /// 第二个与第三个是一队 -> `1↔4`、`2↔3`。
        ///
        /// 即：座位 1 / 3 的队友是 **−1**（上家），座位 2 / 4 的队友是 **+1**（下家）。
        /// 结果必然是上家或下家之一（这正是"队友相邻"）。
        ///
        /// ⚠️ 我一开始写成了 `+2 / -2`，那会得到 `1↔3`、`2↔4` —— **错了**
        /// （用"第一个和第四个一队"核验即可发现）。
        /// </summary>
        public static int TeammateSeat(int seat)
        {
            int me = Normalize(seat);
            return me % 2 == 1 ? PreviousSeat(me) : NextSeat(me);
        }

        /// <summary>队友是我的**上家**吗（而不是下家）。</summary>
        public static bool TeammateIsPrevious(int seat)
            => TeammateSeat(seat) == PreviousSeat(seat);

        /// <summary>
        /// 两名**对手**的座位号（按座位号从小到大，便于界面稳定排布）。
        /// </summary>
        public static int[] OpponentSeats(int seat)
        {
            int me = Normalize(seat);
            int mate = TeammateSeat(me);
            var result = new List<int>(2);

            for (int s = 1; s <= SeatCount; s++)
                if (s != me && s != mate) result.Add(s);

            return result.ToArray();
        }
    }
}
