using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 经典玩法的掷骰先攻**：四人各投 1d20 -> 定座位 -> 依次决定点数。
    ///
    /// 玩家确认的规则：
    ///   · 一队里较大的点叫"大数"、较小的叫"小数"
    ///   · **大数大**的那一队去坐 **1 号位（大数）与 4 号位（小数）**
    ///   · 大数相同 -> 比**小数**（小数大的队去 1&amp;4）
    ///   · 都相同 -> **随机**选一队去 1&amp;4
    ///   · 另一队：大数 = 2 号位，小数 = 3 号位
    ///   · 决定顺序：**大失败最先**（由配对的对手替他决定，完全自由），
    ///     然后剩余按 **4→3→2→1** 各自决定，**大成功全部最后**
    ///
    /// ⚠️ 反例（玩家给的）：一队 16/12、二队 16/8 ->
    ///    1号=16、2号=16、**4号=12**、**3号=8**。
    ///    12 坐 4 号的理由是"他是 1 号的队友"，**不是**"他比 8 大" ——
    ///    所以**不能**把四人点数排序后直接对应 1/2/3/4。
    /// </summary>
    [TestFixture]
    public class TwoOnTwoInitiativeTests
    {
        /// <summary>两人一队：0/1 一队，2/3 一队。</summary>
        private static readonly int[] Teams = { 0, 0, 1, 1 };

        private static TwoOnTwoInitiative.SeatAssignment Seat(int v0, int v1, int v2, int v3,
                                                              int seed = 12345)
        {
            return TwoOnTwoInitiative.AssignSeats(
                new[] { v0, v1, v2, v3 }, Teams, new Random(seed));
        }

        // ============================================================
        // 座位分配
        // ============================================================

        [Test]
        public void The_players_example_seats_twelve_at_four_and_eight_at_three()
        {
            // 一队 16/12，二队 16/8
            //  -> 1号=16(队0)、4号=12(队0)、2号=16(队1)、3号=8(队1)
            var seats = Seat(16, 12, 16, 8);

            Assert.AreEqual(0, seats.PlayerAtSeat(1), "1 号位应是一队的 16。");
            Assert.AreEqual(1, seats.PlayerAtSeat(4), "4 号位应是一队的 12（1 号的队友）。");
            Assert.AreEqual(2, seats.PlayerAtSeat(2), "2 号位应是二队的 16。");
            Assert.AreEqual(3, seats.PlayerAtSeat(3), "3 号位应是二队的 8（2 号的队友）。");
        }

        [Test]
        public void The_bigger_team_takes_seats_one_and_four()
        {
            // 一队大数 18 > 二队大数 15 -> 一队去 1&4
            var seats = Seat(10, 18, 15, 3);

            Assert.AreEqual(1, seats.PlayerAtSeat(1), "一队的 18 应坐 1 号位。");
            Assert.AreEqual(0, seats.PlayerAtSeat(4), "一队的 10 应坐 4 号位。");
            Assert.AreEqual(2, seats.PlayerAtSeat(2), "二队的 15 应坐 2 号位。");
            Assert.AreEqual(3, seats.PlayerAtSeat(3), "二队的 3 应坐 3 号位。");
        }

        [Test]
        public void When_the_big_numbers_tie_the_smaller_numbers_decide_the_team()
        {
            // 两队大数都是 16；一队小数 12 > 二队小数 8 -> 一队去 1&4
            var seats = Seat(16, 12, 16, 8);

            Assert.True(seats.TeamOfSeat(1) == seats.TeamOfSeat(4),
                "1 号与 4 号必须是同队。");
            Assert.True(seats.TeamOfSeat(2) == seats.TeamOfSeat(3),
                "2 号与 3 号必须是同队。");
            Assert.True(seats.TeamOfSeat(1) != seats.TeamOfSeat(2),
                "两队必须是不同队伍。");

            // 一队（下标 0/1）去了 1&4
            Assert.AreEqual(0, seats.TeamOfSeat(1));
        }

        [Test]
        public void The_other_team_takes_seats_too_when_its_small_number_is_bigger()
        {
            // 两队大数都是 16；二队小数 14 > 一队小数 5 -> 二队去 1&4
            var seats = Seat(16, 5, 16, 14);

            Assert.AreEqual(1, seats.TeamOfSeat(1), "二队（team 1）应去 1&4。");
            Assert.AreEqual(2, seats.PlayerAtSeat(1), "二队的 16 坐 1 号位。");
            Assert.AreEqual(3, seats.PlayerAtSeat(4), "二队的 14 坐 4 号位。");
        }

        [Test]
        public void Seat_order_within_a_team_is_by_value()
        {
            // 一队去 1&4：队内大的坐 1、小的坐 4
            var seats = Seat(12, 19, 3, 2);

            Assert.AreEqual(1, seats.PlayerAtSeat(1), "一队里 19 更大 -> 1 号位。");
            Assert.AreEqual(0, seats.PlayerAtSeat(4), "一队里 12 较小 -> 4 号位。");
        }

        [Test]
        public void All_tied_values_are_broken_randomly_and_still_form_two_teams()
        {
            // 四人全部 16 -> 只能随机选一队去 1&4
            var seenTeamAtSeat1 = new List<int>();
            for (int seed = 1; seed <= 40; seed++)
            {
                var seats = Seat(16, 16, 16, 16, seed);

                Assert.True(seats.DecidedByRandom, "四人同点应标记为随机决定。");
                Assert.AreEqual(seats.TeamOfSeat(1), seats.TeamOfSeat(4), "1&4 同队。");
                Assert.AreEqual(seats.TeamOfSeat(2), seats.TeamOfSeat(3), "2&3 同队。");
                Assert.True(seats.TeamOfSeat(1) != seats.TeamOfSeat(2), "两队不同。");

                if (!seenTeamAtSeat1.Contains(seats.TeamOfSeat(1)))
                    seenTeamAtSeat1.Add(seats.TeamOfSeat(1));
            }

            Assert.AreEqual(2, seenTeamAtSeat1.Count,
                "全同点时两队都应该有机会坐到 1 号位（随机生效）。");
        }

        [Test]
        public void Seats_always_hold_the_four_distinct_players()
        {
            var rng = new Random(7);
            for (int trial = 0; trial < 200; trial++)
            {
                int v0 = rng.Next(1, 21), v1 = rng.Next(1, 21);
                int v2 = rng.Next(1, 21), v3 = rng.Next(1, 21);

                var seats = Seat(v0, v1, v2, v3, trial + 1);

                var seen = new List<int>();
                for (int seat = 1; seat <= 4; seat++)
                {
                    int p = seats.PlayerAtSeat(seat);
                    Assert.False(seen.Contains(p), "同一玩家不能占两个座位。");
                    seen.Add(p);
                }
                Assert.AreEqual(4, seen.Count, "四个座位应坐满四个人。");
            }
        }

        [Test]
        public void Seat_one_is_the_highest_of_the_team_that_takes_one_and_four()
        {
            var rng = new Random(99);
            for (int trial = 0; trial < 200; trial++)
            {
                int v0 = rng.Next(1, 21), v1 = rng.Next(1, 21);
                int v2 = rng.Next(1, 21), v3 = rng.Next(1, 21);
                int[] values = { v0, v1, v2, v3 };

                var seats = Seat(v0, v1, v2, v3, trial + 1);

                // 1 号位必须是他所在队里点数较大的那个（或同分）
                int seat1 = seats.PlayerAtSeat(1);
                int seat4 = seats.PlayerAtSeat(4);
                Assert.True(values[seat1] >= values[seat4],
                    "1 号位的点数应不小于自己的队友（4 号位）。");

                // 2 号位同理
                int seat2 = seats.PlayerAtSeat(2);
                int seat3 = seats.PlayerAtSeat(3);
                Assert.True(values[seat2] >= values[seat3],
                    "2 号位的点数应不小于自己的队友（3 号位）。");
            }
        }

        // ============================================================
        // 决定顺序
        // ============================================================

        /// <summary>把点数列转成"下标 1..4"的形式（座位 1..4 的点数）。</summary>
        private static int[] BySeat(int s1, int s2, int s3, int s4)
            => new[] { 0, s1, s2, s3, s4 };

        [Test]
        public void Without_any_special_rolls_the_order_is_four_three_two_one()
        {
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(10, 11, 12, 13));

            Assert.AreEqual(4, order.Count);
            Assert.AreEqual(4, order[0].OwnerSeat);
            Assert.AreEqual(3, order[1].OwnerSeat);
            Assert.AreEqual(2, order[2].OwnerSeat);
            Assert.AreEqual(1, order[3].OwnerSeat);

            for (int i = 0; i < order.Count; i++)
                Assert.AreEqual(order[i].OwnerSeat, order[i].DeciderSeat,
                    "没有大失败时各自决定自己的点数。");
        }

        [Test]
        public void A_critical_failure_at_seat_three_is_decided_by_seat_four_first()
        {
            // 玩家给的例子：三号位大失败
            //  -> 先由四号位给三号位决定，然后四号位、二号位、一号位依次决定自己
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(10, 11, 1, 13));

            Assert.AreEqual(4, order.Count, "四个人各决定一次。");

            Assert.AreEqual(3, order[0].OwnerSeat, "第一步是替三号位决定。");
            Assert.AreEqual(4, order[0].DeciderSeat, "由四号位（对手）替他决定。");
            Assert.True(order[0].IsCriticalFailure);

            Assert.AreEqual(4, order[1].OwnerSeat, "然后是四号位决定自己。");
            Assert.AreEqual(4, order[1].DeciderSeat);

            Assert.AreEqual(2, order[2].OwnerSeat, "然后二号位。");
            Assert.AreEqual(1, order[3].OwnerSeat, "最后是一号位。");
        }

        [Test]
        public void A_critical_failure_at_seat_four_is_decided_by_seat_three()
        {
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(10, 11, 12, 1));

            Assert.AreEqual(4, order[0].OwnerSeat, "第一步仍是四号位（大失败最先）。");
            Assert.AreEqual(3, order[0].DeciderSeat, "由三号位（对手）替他决定。");
            Assert.True(order[0].IsCriticalFailure);

            // 剩下的仍是 3 -> 2 -> 1
            Assert.AreEqual(3, order[1].OwnerSeat);
            Assert.AreEqual(2, order[2].OwnerSeat);
            Assert.AreEqual(1, order[3].OwnerSeat);
        }

        [Test]
        public void A_critical_failure_at_seat_one_is_decided_by_seat_two()
        {
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(1, 11, 12, 13));

            // 大失败最先 -> 一号位（由二号位决定）排在最前
            Assert.AreEqual(1, order[0].OwnerSeat);
            Assert.AreEqual(2, order[0].DeciderSeat, "一号位的对手是二号位。");

            // 然后剩下的 4 -> 3 -> 2
            Assert.AreEqual(4, order[1].OwnerSeat);
            Assert.AreEqual(3, order[2].OwnerSeat);
            Assert.AreEqual(2, order[3].OwnerSeat);
        }

        [Test]
        public void Mutual_critical_failures_at_three_and_four_are_both_handled_first()
        {
            // 玩家确认："三四同时大失败则三四同时给对方决定"
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(10, 11, 1, 1));

            Assert.AreEqual(4, order.Count);
            Assert.True(order[0].IsCriticalFailure);
            Assert.True(order[1].IsCriticalFailure, "两条大失败都应最先处理。");

            // 两条互相决定：4->3 与 3->4
            bool hasFourDecidesThree = false;
            bool hasThreeDecidesFour = false;
            for (int i = 0; i < 2; i++)
            {
                if (order[i].OwnerSeat == 3 && order[i].DeciderSeat == 4) hasFourDecidesThree = true;
                if (order[i].OwnerSeat == 4 && order[i].DeciderSeat == 3) hasThreeDecidesFour = true;
            }

            Assert.True(hasFourDecidesThree, "四号位应给三号位决定。");
            Assert.True(hasThreeDecidesFour, "三号位应给四号位决定。");

            // 然后才是 2 -> 1
            Assert.AreEqual(2, order[2].OwnerSeat);
            Assert.AreEqual(1, order[3].OwnerSeat);
        }

        [Test]
        public void Mutual_critical_failures_at_one_and_two_are_both_handled_first()
        {
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(1, 1, 12, 13));

            Assert.True(order[0].IsCriticalFailure);
            Assert.True(order[1].IsCriticalFailure);

            bool hasTwoDecidesOne = false;
            bool hasOneDecidesTwo = false;
            for (int i = 0; i < 2; i++)
            {
                if (order[i].OwnerSeat == 1 && order[i].DeciderSeat == 2) hasTwoDecidesOne = true;
                if (order[i].OwnerSeat == 2 && order[i].DeciderSeat == 1) hasOneDecidesTwo = true;
            }

            Assert.True(hasTwoDecidesOne, "二号位应给一号位决定。");
            Assert.True(hasOneDecidesTwo, "一号位应给二号位决定。");

            Assert.AreEqual(4, order[2].OwnerSeat);
            Assert.AreEqual(3, order[3].OwnerSeat);
        }

        [Test]
        public void All_four_critical_failures_are_handled_in_the_failure_phase()
        {
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(1, 1, 1, 1));

            Assert.AreEqual(4, order.Count);
            for (int i = 0; i < 4; i++)
                Assert.True(order[i].IsCriticalFailure, "四条都应在大失败最先阶段处理。");

            // 每一对互相决定
            for (int i = 0; i < 4; i++)
                Assert.AreEqual(TwoOnTwoInitiative.OpponentSeatOf(order[i].OwnerSeat),
                                order[i].DeciderSeat, "大失败一律由对手决定。");
        }

        [Test]
        public void Major_successes_are_all_decided_last()
        {
            // 座位 4 与 2 是大成功（20）-> 都排到最后
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(10, 20, 12, 20));

            Assert.AreEqual(4, order.Count);

            // 前面是 3 -> 1（各自决定）
            Assert.AreEqual(3, order[0].OwnerSeat);
            Assert.AreEqual(1, order[1].OwnerSeat);

            // 最后两条是大成功（4 与 2），各自决定自己
            var lastTwo = new List<int> { order[2].OwnerSeat, order[3].OwnerSeat };
            Assert.True(lastTwo.Contains(4), "大成功的 4 号位应在最后。");
            Assert.True(lastTwo.Contains(2), "大成功的 2 号位应在最后。");

            for (int i = 2; i < 4; i++)
                Assert.AreEqual(order[i].OwnerSeat, order[i].DeciderSeat,
                    "大成功由自己决定。");
        }

        [Test]
        public void Critical_failures_come_before_normal_and_major_success_comes_last()
        {
            // 座位 1 大失败、座位 3 大成功、其余正常
            var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(1, 11, 20, 13));

            // ① 大失败最先
            Assert.AreEqual(1, order[0].OwnerSeat);
            Assert.True(order[0].IsCriticalFailure);

            // ② 正常 4 -> 2（跳过已处理的大失败 1 与最后的大成功 3）
            Assert.AreEqual(4, order[1].OwnerSeat);
            Assert.AreEqual(2, order[2].OwnerSeat);

            // ③ 大成功最后
            Assert.AreEqual(3, order[3].OwnerSeat);
            Assert.False(order[3].IsCriticalFailure);
        }

        [Test]
        public void Every_seat_is_decided_exactly_once()
        {
            var rng = new Random(4242);
            var specials = new[] { 1, 19, 20, 10 };

            for (int trial = 0; trial < 300; trial++)
            {
                int s1 = specials[rng.Next(specials.Length)];
                int s2 = specials[rng.Next(specials.Length)];
                int s3 = specials[rng.Next(specials.Length)];
                int s4 = specials[rng.Next(specials.Length)];

                var order = TwoOnTwoInitiative.BuildDecisionOrder(BySeat(s1, s2, s3, s4));

                Assert.AreEqual(4, order.Count, $"每个座位都应被决定一次（{s1}/{s2}/{s3}/{s4}）。");

                var owners = new List<int>();
                for (int i = 0; i < order.Count; i++)
                {
                    Assert.False(owners.Contains(order[i].OwnerSeat),
                        "同一个座位不能决定两次。");
                    owners.Add(order[i].OwnerSeat);

                    // 大失败由**对手**决定；其余由自己决定
                    if (order[i].IsCriticalFailure)
                        Assert.AreEqual(TwoOnTwoInitiative.OpponentSeatOf(order[i].OwnerSeat),
                                        order[i].DeciderSeat);
                    else
                        Assert.AreEqual(order[i].OwnerSeat, order[i].DeciderSeat);
                }
            }
        }

        [Test]
        public void Opponent_pairs_are_the_adjacent_seats_and_teammates_are_the_other_pair()
        {
            // 大失败送给对手：4<->3 与 2<->1
            Assert.AreEqual(3, TwoOnTwoInitiative.OpponentSeatOf(4));
            Assert.AreEqual(4, TwoOnTwoInitiative.OpponentSeatOf(3));
            Assert.AreEqual(1, TwoOnTwoInitiative.OpponentSeatOf(2));
            Assert.AreEqual(2, TwoOnTwoInitiative.OpponentSeatOf(1));

            // 队友是另一对：1<->4 与 2<->3
            Assert.AreEqual(4, TwoOnTwoInitiative.TeammateSeatOf(1));
            Assert.AreEqual(1, TwoOnTwoInitiative.TeammateSeatOf(4));
            Assert.AreEqual(3, TwoOnTwoInitiative.TeammateSeatOf(2));
            Assert.AreEqual(2, TwoOnTwoInitiative.TeammateSeatOf(3));

            // 对手与队友必须不同（否则大失败会送给队友）
            for (int seat = 1; seat <= 4; seat++)
                Assert.True(TwoOnTwoInitiative.OpponentSeatOf(seat)
                            != TwoOnTwoInitiative.TeammateSeatOf(seat),
                    $"座位 {seat} 的对手与队友不能是同一人。");
        }
    }
}
