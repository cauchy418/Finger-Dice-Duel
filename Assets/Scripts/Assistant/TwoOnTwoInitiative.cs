using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **2v2 经典玩法：掷骰先攻**（四人各投 1d20）。
    ///
    /// 座位规则（玩家确认，**不是**"按点数从高到低排 1/2/3/4"）：
    ///   · 一队两人的点数里较大者称为该队的"大数"，较小者称为"小数"
    ///   · **大数较大**的那一队去坐 **1 号位与 4 号位**；大数相同则比**小数**
    ///     （小数大的那队去 1&amp;4）；两者都相同则**随机**选一队
    ///   · 去 1&amp;4 的那队：大数 = **1 号位**，小数 = **4 号位**（队友关系）
    ///   · 另一队：大数 = **2 号位**，小数 = **3 号位**（队友关系）
    ///
    /// 于是队伍恒为 `{1,4}` 与 `{2,3}`，与引擎的座位规则一致。
    ///
    /// ⚠️ 曾经的错误推导：以为"四人点数从高到低依次是 1234"。
    ///    反例（玩家给的）：一队 16/12、二队 16/8 ->
    ///    1号=16、2号=16、**4号=12**、**3号=8** —— 12 坐 4 号的理由是
    ///    "它是 1 号的队友"，**不是**"它比 8 大"。
    ///    两队的大数相同（都是 16）时才比较小数（12 vs 8）来决定**哪一队**去 1&amp;4。
    /// </summary>
    public static class TwoOnTwoInitiative
    {
        /// <summary>一名玩家的投掷结果（含大失败 / 大成功判断）。</summary>
        public struct Roll
        {
            public int PlayerIndex;      // 0..3，调用方用来对应到具体玩家
            public int Value;            // 1..20

            public bool IsCriticalFailure => DiceRollSetup.IsCriticalFailure(Value);

            /// <summary>大成功（20）：最后一起、各自决定自己的点数。</summary>
            public bool IsMajorSuccess => Value >= DiceRollSetup.MajorSuccess;

            public Roll(int playerIndex, int value)
            {
                PlayerIndex = playerIndex;
                Value = value;
            }
        }

        /// <summary>落座结果：座位号 1..4 -> 玩家下标（0..3）。</summary>
        public sealed class SeatAssignment
        {
            /// <summary><c>SeatToPlayer[座位号]</c>，下标 0 不用（座位从 1 起算）。</summary>
            public int[] SeatToPlayer = new int[5];

            /// <summary>两人一队，两队。</summary>
            public int[] TeamOfPlayer = new int[4];

            /// <summary>是否需要靠随机才分出 1&amp;4 归属（用于测试与说明）。</summary>
            public bool DecidedByRandom;

            public int PlayerAtSeat(int seat) => SeatToPlayer[seat];

            public int TeamOfSeat(int seat) => TeamOfPlayer[SeatToPlayer[seat]];
        }

        /// <summary>
        /// 按四人的点数定座位。
        ///
        /// <paramref name="valueOfPlayer"/> 是四人的点数（下标 0..3）。
        /// <paramref name="teamOfPlayer"/> 是四人的队伍（必须两队各两人）。
        /// <paramref name="rng"/> 只在"大数、小数都相同"时才用到
        /// （玩家确认：仍无法决定则随机选一队去 1&amp;4）。
        /// </summary>
        public static SeatAssignment AssignSeats(IReadOnlyList<int> valueOfPlayer,
                                                 IReadOnlyList<int> teamOfPlayer,
                                                 Random rng)
        {
            if (valueOfPlayer == null || valueOfPlayer.Count != 4)
                throw new ArgumentException("必须正好 4 人的点数。", nameof(valueOfPlayer));
            if (teamOfPlayer == null || teamOfPlayer.Count != 4)
                throw new ArgumentException("必须正好 4 人的队伍。", nameof(teamOfPlayer));

            var result = new SeatAssignment();

            // 收集两队各自的成员
            var team0 = new List<int>(2);
            var team1 = new List<int>(2);
            for (int i = 0; i < 4; i++)
                (teamOfPlayer[i] == 0 ? team0 : team1).Add(i);

            if (team0.Count != 2 || team1.Count != 2)
                throw new ArgumentException("两队必须各两人。", nameof(teamOfPlayer));

            int max0 = Max(valueOfPlayer, team0);
            int max1 = Max(valueOfPlayer, team1);

            bool team0TakesFirstFour;

            if (max0 != max1)
            {
                team0TakesFirstFour = max0 > max1;
            }
            else
            {
                int min0 = Min(valueOfPlayer, team0);
                int min1 = Min(valueOfPlayer, team1);

                if (min0 != min1)
                {
                    team0TakesFirstFour = min0 > min1;
                }
                else
                {
                    // 大数、小数都相同 -> 随机选一队去 1&4
                    // （玩家确认：座位顺序仍按大小定，所以只随机"哪一队"）
                    result.DecidedByRandom = true;
                    team0TakesFirstFour = rng != null && rng.Next(2) == 0;
                }
            }

            List<int> firstFour = team0TakesFirstFour ? team0 : team1;
            List<int> secondThree = team0TakesFirstFour ? team1 : team0;

            // 去 1&4 的队：大者 = 1 号位，小者 = 4 号位
            SortByValueDesc(valueOfPlayer, firstFour, rng);
            result.SeatToPlayer[1] = firstFour[0];
            result.SeatToPlayer[4] = firstFour[1];

            // 另一队：大者 = 2 号位，小者 = 3 号位
            SortByValueDesc(valueOfPlayer, secondThree, rng);
            result.SeatToPlayer[2] = secondThree[0];
            result.SeatToPlayer[3] = secondThree[1];

            for (int seat = 1; seat <= 4; seat++)
            {
                int player = result.SeatToPlayer[seat];
                result.TeamOfPlayer[player] = teamOfPlayer[player];
            }

            return result;
        }

        private static int Max(IReadOnlyList<int> values, List<int> members)
        {
            int best = int.MinValue;
            for (int i = 0; i < members.Count; i++)
                if (values[members[i]] > best) best = values[members[i]];
            return best;
        }

        private static int Min(IReadOnlyList<int> values, List<int> members)
        {
            int best = int.MaxValue;
            for (int i = 0; i < members.Count; i++)
                if (values[members[i]] < best) best = values[members[i]];
            return best;
        }

        /// <summary>
        /// 按点数从大到小排序；**点数相同时随机决定先后**
        /// （玩家确认："若两人点数相同则随机决定座位"）。
        ///
        /// 用 Fisher–Yates 先打乱、再按点数稳定排序 —— 这样"同分者的先后"
        /// 完全由 <paramref name="rng"/> 决定，而不是某个固定的下标关系。
        ///
        /// ⚠️ 第一版这里写的是 `(x & 1).CompareTo(y & 1)`，那**不是随机**，
        ///    而是恒定按"偶数下标优先" —— 会让同分时总是同一个座位号先，
        ///    与玩家要求不符。
        /// </summary>
        private static void SortByValueDesc(IReadOnlyList<int> values, List<int> members,
                                            Random rng)
        {
            if (members.Count > 1 && rng != null)
            {
                for (int i = members.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int tmp = members[i];
                    members[i] = members[j];
                    members[j] = tmp;
                }
            }

            // List.Sort 不稳定，但上面已经用随机顺序打乱过，
            // 所以同分者的相对次序仍然来自 rng。
            members.Sort((x, y) => values[y].CompareTo(values[x]));
        }

        // ============================================================
        // 决定顺序
        // ============================================================

        /// <summary>一个"决定点数"的步骤：谁决定、给谁决定。</summary>
        public struct DecisionStep
        {
            /// <summary>被决定点数的人（座位号 1..4）。</summary>
            public int OwnerSeat;

            /// <summary>实际做决定的人（座位号 1..4）。</summary>
            public int DeciderSeat;

            /// <summary>这一步是否处理"大失败"（大失败最先处理）。</summary>
            public bool IsCriticalFailure;
        }

        /// <summary>
        /// **配对的对手座位**：大失败时由他去替对方决定点数。
        ///
        /// 玩家确认："**大失败是把决定权送给对手**" ——
        /// 所以配对是互为对手的相邻座位：`4↔3`、`2↔1`。
        ///
        /// 注意这与"队友"正好相反：队友是 `1↔4`、`2↔3`。
        /// </summary>
        public static int OpponentSeatOf(int seat)
        {
            switch (seat)
            {
                case 1: return 2;
                case 2: return 1;
                case 3: return 4;
                case 4: return 3;
                default: return -1;
            }
        }

        /// <summary>队友座位（`1↔4`、`2↔3`），与引擎的座位规则一致。</summary>
        public static int TeammateSeatOf(int seat) => SeatLayout.TeammateSeat(seat);

        /// <summary>
        /// 算出"决定点数"的完整顺序。
        ///
        /// 玩家确认的规则：
        ///   ① **大失败最先处理**：由**配对的对手**替他决定（对手完全自由地选两手）
        ///   ② 处理完大失败后，**剩余顺序仍是 4 → 3 → 2 → 1** 各自决定
        ///   ③ **大成功（20）全部放到最后**，各自决定自己的
        ///
        /// 配对双方同时大失败时**同时互相决定**，所以它们属于同一批（顺序不限）。
        ///
        /// <paramref name="valueBySeat"/> 是"该座位投出的点数"（下标 1..4）。
        /// </summary>
        public static List<DecisionStep> BuildDecisionOrder(IReadOnlyList<int> valueBySeat)
        {
            if (valueBySeat == null || valueBySeat.Count < 5)
                throw new ArgumentException("需要下标 1..4 的点数。", nameof(valueBySeat));

            var steps = new List<DecisionStep>(4);

            bool IsFail(int seat) => DiceRollSetup.IsCriticalFailure(valueBySeat[seat]);
            bool IsMajor(int seat) => valueBySeat[seat] >= DiceRollSetup.MajorSuccess;

            // ① 大失败：按座位从大到小（4→3→2→1），由配对的对手决定。
            //
            //    双方同时大失败 -> 互相决定（两条都进这里，天然成对）。
            for (int seat = 4; seat >= 1; seat--)
            {
                if (!IsFail(seat)) continue;

                steps.Add(new DecisionStep
                {
                    OwnerSeat = seat,
                    DeciderSeat = OpponentSeatOf(seat),
                    IsCriticalFailure = true,
                });
            }

            // ② 正常顺序 4→3→2→1，各自决定自己；
            //    跳过已处理的大失败与留到最后的大成功。
            for (int seat = 4; seat >= 1; seat--)
            {
                if (IsFail(seat)) continue;
                if (IsMajor(seat)) continue;

                steps.Add(new DecisionStep
                {
                    OwnerSeat = seat,
                    DeciderSeat = seat,
                    IsCriticalFailure = false,
                });
            }

            // ③ 大成功全部最后，各自决定自己的。
            for (int seat = 4; seat >= 1; seat--)
            {
                if (!IsMajor(seat)) continue;

                steps.Add(new DecisionStep
                {
                    OwnerSeat = seat,
                    DeciderSeat = seat,
                    IsCriticalFailure = false,
                });
            }

            return steps;
        }
    }
}
