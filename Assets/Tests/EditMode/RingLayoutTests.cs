using System.Collections.Generic;
using FingerDiceDuel.Assistant;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **主视角看到的四个位置**（2v2 环形棋盘布局）。
    ///
    /// 玩家确认的规则：
    ///   · 自己在**下方正中**
    ///   · 队友在**左下**（他是上家时）或**右下**（他是下家时）
    ///   · 两名对手占另外两个位置，另一侧的下方位**空着**
    ///   · 位置与环形的对应是"**从自己往左逆时针绕屏一周**"：
    ///       自己 → 上家 → 再上家 → 再上家
    ///
    /// ⚠️ 这条测试是**实机截图暴露问题后补的**：
    ///    我第一版按"座位号升序 = 左上、右上"实现，
    ///    在队友位于左下时把二位/三位画反了。
    ///    只写"两个对手在左上右上"是不够的 —— 必须钉住**哪个在左**。
    /// </summary>
    [TestFixture]
    public class RingLayoutTests
    {
        private static int SeatAt(IReadOnlyList<RingSlot> slots, RingPosition p)
            => RingLayout.SeatAt(slots, p);

        [Test]
        public void Self_is_always_at_the_bottom_centre()
        {
            for (int me = 1; me <= 4; me++)
            {
                IReadOnlyList<RingSlot> slots = RingLayout.Build(me);

                Assert.AreEqual(me, SeatAt(slots, RingPosition.SelfBottom),
                    $"座位 {me} 的主视角应画在下方正中。");
                Assert.AreEqual(RingRelation.Self,
                    RingLayout.RelationAt(slots, RingPosition.SelfBottom));
            }
        }

        [Test]
        public void Teammate_is_on_the_left_when_they_are_the_previous_seat()
        {
            // 坐 1 号位时队友是 4 号位（上家）-> 偏左下
            IReadOnlyList<RingSlot> slots = RingLayout.Build(1);

            Assert.AreEqual(4, SeatAt(slots, RingPosition.LowerLeft),
                "队友（4 号位，上家）应在偏左下。");
            Assert.AreEqual(RingRelation.Teammate,
                RingLayout.RelationAt(slots, RingPosition.LowerLeft));

            // 另一侧下方位空着
            Assert.AreEqual(-1, SeatAt(slots, RingPosition.LowerRight),
                "队友在左下时，右下应空着。");
        }

        [Test]
        public void Teammate_is_on_the_right_when_they_are_the_next_seat()
        {
            // 坐 2 号位时队友是 3 号位（下家）-> 偏右下
            IReadOnlyList<RingSlot> slots = RingLayout.Build(2);

            Assert.AreEqual(3, SeatAt(slots, RingPosition.LowerRight),
                "队友（3 号位，下家）应在偏右下。");
            Assert.AreEqual(-1, SeatAt(slots, RingPosition.LowerLeft),
                "队友在右下时，左下应空着。");
        }

        [Test]
        public void Opponents_follow_counterclockwise_around_the_screen()
        {
            // 玩家给的例子：坐 1 号位、队友在左下 ->
            //   左上 = **三位**、右上 = **二位**
            //（我第一版写反成了"左上 = 二位"，实机截图暴露。）
            IReadOnlyList<RingSlot> slots = RingLayout.Build(1);

            Assert.AreEqual(3, SeatAt(slots, RingPosition.UpperLeft),
                "队友在左下时，左上应是三位。");
            Assert.AreEqual(2, SeatAt(slots, RingPosition.UpperRight),
                "队友在左下时，右上应是二位。");
        }

        [Test]
        public void Opponents_order_flips_when_the_teammate_is_on_the_right()
        {
            // 镜像情况（坐 4 号位、队友在右下）：
            //   左上 = 二位（再下家）、右上 = 三位（再再下家）
            IReadOnlyList<RingSlot> slots = RingLayout.Build(4);

            Assert.AreEqual(2, SeatAt(slots, RingPosition.UpperLeft),
                "队友在右下时，左上应是二位。");
            Assert.AreEqual(3, SeatAt(slots, RingPosition.UpperRight),
                "队友在右下时，右上应是三位。");
        }

        [Test]
        public void All_four_seats_are_placed_exactly_once()
        {
            for (int me = 1; me <= 4; me++)
            {
                IReadOnlyList<RingSlot> slots = RingLayout.Build(me);

                var seats = new List<int>();
                for (int i = 0; i < slots.Count; i++)
                {
                    Assert.True(slots[i].Seat >= 1 && slots[i].Seat <= 4,
                        $"座位号必须在 1..4（实际 {slots[i].Seat}）。");
                    Assert.False(seats.Contains(slots[i].Seat),
                        "同一个座位不能占两个位置。");
                    seats.Add(slots[i].Seat);
                }

                seats.Sort();
                for (int i = 0; i < 4; i++)
                    Assert.AreEqual(i + 1, seats[i],
                        $"四个人应各占一个位置（主视角 {me}）。");
            }
        }

        [Test]
        public void There_are_exactly_two_opponents_and_one_teammate()
        {
            for (int me = 1; me <= 4; me++)
            {
                IReadOnlyList<RingSlot> slots = RingLayout.Build(me);

                int mates = 0, foes = 0, selves = 0;
                for (int i = 0; i < slots.Count; i++)
                {
                    switch (slots[i].Relation)
                    {
                        case RingRelation.Self: selves++; break;
                        case RingRelation.Teammate: mates++; break;
                        default: foes++; break;
                    }
                }

                Assert.AreEqual(1, selves, "只有一个自己。");
                Assert.AreEqual(1, mates, "只有一个队友。");
                Assert.AreEqual(2, foes, "有两名对手。");
            }
        }

        [Test]
        public void Teammate_is_always_a_neighbour_seat_of_the_main_view()
        {
            // 这是"队友必是上家或下家"在界面层的结果
            for (int me = 1; me <= 4; me++)
            {
                IReadOnlyList<RingSlot> slots = RingLayout.Build(me);
                int mate = -1;
                for (int i = 0; i < slots.Count; i++)
                    if (slots[i].Relation == RingRelation.Teammate) mate = slots[i].Seat;

                Assert.True(mate == SeatLayout.PreviousSeat(me)
                            || mate == SeatLayout.NextSeat(me),
                    $"座位 {me} 的队友（{mate}）必须是上家或下家。");
            }
        }

        [Test]
        public void Panel_offsets_keep_every_panel_inside_the_screen()
        {
            // 实机截图暴露过：队友原来用 ±0.92，1920 宽下到 ±883px，
            // 加 560px 面板后超出 960 的屏幕边界（面板被切掉）。
            //
            // ⚠️ 必须用**每个位置真实的尺寸**，不能统一取最大的那个 ——
            //    我第一版用 700（自己那块）去检查队友面板，于是**误报**越界。
            for (int i = 0; i < 5; i++)
            {
                var p = (RingPosition)i;

                Assert.True(RingLayout.FitsOnScreen(p),
                    $"位置 {p} 的面板越出屏幕。");
            }
        }

        [Test]
        public void Self_panel_is_the_widest_because_it_holds_two_hand_cards()
        {
            RingLayout.PanelSize(RingPosition.SelfBottom, out float selfW, out _);
            RingLayout.PanelSize(RingPosition.UpperLeft, out float otherW, out _);

            Assert.True(selfW > otherW,
                "自己那块要放手牌，应该比其余位置更宽。");
        }

        [Test]
        public void Self_is_lower_than_the_teammate_and_opponents()
        {
            // "下方正中"的几何含义：自己的纵坐标必须比其余四个位置都低
            RingLayout.NormalizedOffset(RingPosition.SelfBottom, out _, out float selfY);

            Assert.True(selfY < 0f, "自己在中心线以下。");

            for (int i = 0; i < 5; i++)
            {
                var p = (RingPosition)i;
                if (p == RingPosition.SelfBottom) continue;

                RingLayout.NormalizedOffset(p, out _, out float y);
                Assert.True(selfY < y,
                    $"自己（y={selfY}）应低于位置 {p}（y={y}）。");
            }
        }
    }
}
