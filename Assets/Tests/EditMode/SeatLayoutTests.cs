using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **座位关系推导**（2v2 四人 UI 的地基）。
    ///
    /// 玩家确认的规则：
    ///   · 四人围成环，**第一个行动的与第四个行动的是一队**（`1↔4`），
    ///     第二个与第三个是一队（`2↔3`）
    ///   · 队友在环上**相邻** -> 队友一定是我的**上家或下家**
    ///   · 主视角可以在任意座位
    ///
    /// 四人 UI 完全由这几个函数推出四个位置，所以这里穷举四个座位逐个验证。
    ///
    /// ⚠️ 我第一版把队友公式写成了 `+2 / -2`（得到 `1↔3`、`2↔4`）——
    ///    用"第一个和第四个一队"一核验就能发现错。这条测试就是那个核验。
    /// </summary>
    [TestFixture]
    public class SeatLayoutTests
    {
        [Test]
        public void Teammates_are_seats_one_and_four_and_seats_two_and_three()
        {
            Assert.AreEqual(4, SeatLayout.TeammateSeat(1), "座位 1 的队友是座位 4。");
            Assert.AreEqual(1, SeatLayout.TeammateSeat(4), "座位 4 的队友是座位 1。");
            Assert.AreEqual(3, SeatLayout.TeammateSeat(2), "座位 2 的队友是座位 3。");
            Assert.AreEqual(2, SeatLayout.TeammateSeat(3), "座位 3 的队友是座位 2。");
        }

        [Test]
        public void Teammate_relation_is_symmetric()
        {
            for (int seat = 1; seat <= SeatLayout.SeatCount; seat++)
            {
                int mate = SeatLayout.TeammateSeat(seat);
                Assert.AreEqual(seat, SeatLayout.TeammateSeat(mate),
                    $"座位 {seat} 与 {mate} 应互为队友。");
            }
        }

        [Test]
        public void Every_teammate_is_either_previous_or_next_seat()
        {
            // 这是四人 UI 布局规则的前提：队友一定在"上家"或"下家"那一侧。
            for (int seat = 1; seat <= SeatLayout.SeatCount; seat++)
            {
                int mate = SeatLayout.TeammateSeat(seat);
                bool isPrev = mate == SeatLayout.PreviousSeat(seat);
                bool isNext = mate == SeatLayout.NextSeat(seat);

                Assert.True(isPrev || isNext,
                    $"座位 {seat} 的队友（{mate}）必须是上家或下家。");
            }
        }

        [Test]
        public void The_players_examples_both_hold()
        {
            // 玩家给的例子 1：我是 P2，队友 P3 在我之后行动（下家）
            Assert.AreEqual(3, SeatLayout.TeammateSeat(2));
            Assert.True(SeatLayout.TeammateSeat(2) == SeatLayout.NextSeat(2),
                "坐 2 号位时队友是**下家** -> 界面偏右下。");

            // 玩家给的例子 2：我是 P3，队友 P2 在我上家
            Assert.AreEqual(2, SeatLayout.TeammateSeat(3));
            Assert.True(SeatLayout.TeammateSeat(3) == SeatLayout.PreviousSeat(3),
                "坐 3 号位时队友是**上家** -> 界面偏左下。");
        }

        [Test]
        public void Two_opponents_fill_the_remaining_seats()
        {
            for (int seat = 1; seat <= SeatLayout.SeatCount; seat++)
            {
                int[] foes = SeatLayout.OpponentSeats(seat);

                Assert.AreEqual(2, foes.Length, $"座位 {seat} 应有 2 名对手。");
                Assert.False(foes[0] == seat || foes[1] == seat, "对手不该包含自己。");
                Assert.False(foes[0] == SeatLayout.TeammateSeat(seat)
                             || foes[1] == SeatLayout.TeammateSeat(seat),
                    "对手不该包含队友。");

                // 四个座位 = 自己 + 队友 + 2 对手，不重不漏
                var all = new List<int> { seat, SeatLayout.TeammateSeat(seat), foes[0], foes[1] };
                all.Sort();
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(i + 1, all[i], $"座位 {seat} 的四个人应是 1..4 不重不漏。");
            }
        }

        [Test]
        public void Previous_and_next_seats_wrap_around_the_ring()
        {
            Assert.AreEqual(4, SeatLayout.PreviousSeat(1), "座位 1 的上家是 4（环回绕）。");
            Assert.AreEqual(1, SeatLayout.NextSeat(4), "座位 4 的下家是 1（环回绕）。");
        }

        [Test]
        public void Seat_numbers_are_normalized_into_the_ring()
        {
            Assert.AreEqual(1, SeatLayout.Normalize(1));
            Assert.AreEqual(4, SeatLayout.Normalize(4));
            Assert.AreEqual(1, SeatLayout.Normalize(5), "5 应回绕成 1。");
            Assert.AreEqual(4, SeatLayout.Normalize(0), "0 应回绕成 4。");
        }
    }
}
