using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 落座**：把四名玩家按"座位 1..4"放进环里。
    ///
    /// 玩家确认的座位规则：
    ///   · 四人围成环，**座位 1&4 一队、2&3 一队**
    ///   · 队友在环上**相邻** -> 主视角的队友一定是**上家或下家**
    ///   · 主视角可以在任意座位（角色选择界面里选）
    ///
    /// 这组测试盯的是"主视角看到的四方位置"能否由座位号推出 ——
    /// 四人 UI 完全依赖这个推导。
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoSeatingTests
    {
        /// <summary>造一局 2v2：四人按座位 1..4 坐好，全是白板。</summary>
        private static GameManager Seat4()
        {
            var seated = new List<PlayerState>(4);
            for (int i = 1; i <= 4; i++)
                seated.Add(CharacterFactory.CreatePlayer("P" + i, CharacterDefinition.Blank));

            var providers = new List<IChoiceProvider>(4);
            for (int i = 0; i < 4; i++) providers.Add(new SimpleChoiceProvider());

            return GameManager.Create2v2(seated, providers);
        }

        private static PlayerState Seat(GameManager game, int seat)
            => game.Players[seat - 1];

        [Test]
        public void Four_players_are_seated_one_to_four_in_ring_order()
        {
            GameManager game = Seat4();

            Assert.AreEqual(4, game.Players.Count);
            for (int seat = 1; seat <= 4; seat++)
                Assert.AreEqual(seat, game.SeatOf(Seat(game, seat)),
                    $"座位 {seat} 的玩家应报出座位 {seat}。");
        }

        [Test]
        public void Seats_one_and_four_are_teammates_and_two_and_three_are_teammates()
        {
            GameManager game = Seat4();

            Assert.True(game.IsTeammate(Seat(game, 1), Seat(game, 4)),
                "座位 1 与 4 一队。");
            Assert.True(game.IsTeammate(Seat(game, 2), Seat(game, 3)),
                "座位 2 与 3 一队。");

            Assert.False(game.IsTeammate(Seat(game, 1), Seat(game, 2)),
                "座位 1 与 2 是对手。");
        }

        [Test]
        public void Everyones_teammate_is_a_neighbour_on_the_ring()
        {
            // 这是"队友一定在上家或下家"的引擎保证 ——
            // 四人 UI 的布局规则直接建立在它上面。
            GameManager game = Seat4();

            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState me = Seat(game, seat);
                IReadOnlyList<PlayerState> mates = game.TeammatesOf(me);

                Assert.AreEqual(1, mates.Count, $"座位 {seat} 应有 1 名队友。");

                bool isPrev = ReferenceEquals(mates[0], game.PreviousSeatBefore(me));
                bool isNext = ReferenceEquals(mates[0], game.NextSeatAfter(me));

                Assert.True(isPrev || isNext,
                    $"座位 {seat} 的队友必须是上家或下家（实际既不是）。");
            }
        }

        [Test]
        public void Previous_and_next_neighbours_are_the_two_adjacent_seats()
        {
            GameManager game = Seat4();

            // 座位 1 的上家是 4、下家是 2
            PlayerState p1 = Seat(game, 1);
            Assert.AreSame(Seat(game, 4), game.PreviousSeatBefore(p1), "1 的上家应是 4。");
            Assert.AreSame(Seat(game, 2), game.NextSeatAfter(p1), "1 的下家应是 2。");

            // 座位 2 的上家是 1、下家是 3（此时队友 3 是**下家**）
            PlayerState p2 = Seat(game, 2);
            Assert.AreSame(Seat(game, 1), game.PreviousSeatBefore(p2), "2 的上家应是 1。");
            Assert.AreSame(Seat(game, 3), game.NextSeatAfter(p2), "2 的下家应是 3。");
        }

        [Test]
        public void The_players_team_is_the_previous_seat_when_seated_at_three()
        {
            // 玩家给的例子：这次我是 P3，我的队友是 P2（上家）
            // -> 界面上队友应在**偏左下**。
            GameManager game = Seat4();
            PlayerState me = Seat(game, 3);

            Assert.True(ReferenceEquals(game.PreviousSeatBefore(me), Seat(game, 2)),
                "坐 3 号位时队友 2 是上家（界面放偏左下）。");
            Assert.False(ReferenceEquals(game.NextSeatAfter(me), Seat(game, 2)),
                "队友不该同时是下家。");
        }

        [Test]
        public void The_players_team_is_the_next_seat_when_seated_at_two()
        {
            // 玩家给的例子：这次我是 P2，我的队友是 P3（下家）
            // -> 界面上队友应在**偏右下**。
            GameManager game = Seat4();
            PlayerState me = Seat(game, 2);

            Assert.True(ReferenceEquals(game.NextSeatAfter(me), Seat(game, 3)),
                "坐 2 号位时队友 3 是下家（界面放偏右下）。");
        }

        [Test]
        public void The_two_opponents_fill_the_other_two_positions()
        {
            GameManager game = Seat4();

            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState me = Seat(game, seat);

                IReadOnlyList<PlayerState> foes = game.OpponentsOf(me);
                Assert.AreEqual(2, foes.Count, $"座位 {seat} 应有 2 名对手。");

                // 对手既不是自己，也不是队友
                for (int i = 0; i < foes.Count; i++)
                {
                    Assert.False(ReferenceEquals(foes[i], me));
                    Assert.False(game.IsTeammate(me, foes[i]));
                }
            }
        }

        [Test]
        public void Main_view_is_always_seated_at_the_bottom()
        {
            // 玩家确认：每台机器都把自己的角色放在下方 -> 主视角固定为"自己"。
            // 引擎侧只需保证"任意座位都能当主视角"（座位号与视角无关）。
            // 这里验证四个座位都能正确报出自己的上家/下家/队友/对手。
            GameManager game = Seat4();

            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState me = Seat(game, seat);

                Assert.NotNull(game.PreviousSeatBefore(me), $"座位 {seat} 应有上家。");
                Assert.NotNull(game.NextSeatAfter(me), $"座位 {seat} 应有下家。");
                Assert.AreEqual(1, game.TeammatesOf(me).Count, $"座位 {seat} 应有 1 名队友。");
                Assert.AreEqual(2, game.OpponentsOf(me).Count, $"座位 {seat} 应有 2 名对手。");
            }
        }

        [Test]
        public void Create2v2_rejects_wrong_player_counts()
        {
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };

            Assert.Throws<System.ArgumentException>(
                () => GameManager.Create2v2(new List<PlayerState>(), providers),
                "人数不是 4 应被拒绝。");
        }
    }
}
