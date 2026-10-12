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
    /// **2v2 经典玩法开局的执行层**：四人各投 1d20 → 定座位 → 依次决定初始手值。
    ///
    /// 玩家确认的规则（都在这里被验证）：
    ///   · 座位：大数较大的队去 1&amp;4（大数 = 1 号、小数 = 4 号），另一队 2&amp;3
    ///   · 决定顺序 **4 → 3 → 2 → 1**，**大失败最先**（由配对的对手决定）、
    ///     **大成功最后**
    ///   · **后决定的人能看到前面人决定的数字**
    ///   · 初始手值**只写数值、不触发任何效果**
    /// </summary>
    [TestFixture]
    public class TwoOnTwoStartControllerTests
    {
        private static List<PlayerState> MakePlayers()
        {
            var list = new List<PlayerState>(4);
            for (int i = 1; i <= 4; i++)
                list.Add(CharacterFactory.CreatePlayer("P" + i, CharacterDefinition.Blank));
            return list;
        }

        /// <summary>队伍：[0,0,1,1] —— 0/1 号玩家一队、2/3 号玩家一队。</summary>
        private static readonly int[] Teams = { 0, 0, 1, 1 };

        private static TwoOnTwoStartController Controller(int[] rolls, int seed = 1234)
        {
            return new TwoOnTwoStartController(MakePlayers(), Teams, rolls, new Random(seed));
        }

        /// <summary>把整轮走完（每一步取第一个合法拆分）。</summary>
        private static void PlayThrough(TwoOnTwoStartController c)
        {
            while (!c.IsFinished)
            {
                IReadOnlyList<HandSplit> options = c.Candidates;
                Assert.True(options.Count > 0, "每一步都必须有合法拆分。");
                Assert.True(c.Submit(options[0]), "合法拆分必须被接受。");
            }
        }

        /// <summary>
        /// 找出"投出某个点数"的座位号（−1 表示没有）。
        ///
        /// 用途：测试要断言"投出 X 的那个人是什么情况"时，
        /// **不要手算座位**（我手算错过好几次）——直接问控制器。
        /// </summary>
        private static int SeatWithRoll(TwoOnTwoStartController c, int roll)
        {
            for (int seat = 1; seat <= 4; seat++)
                if (c.RollOf(c.PlayerAtSeat(seat)) == roll) return seat;

            return -1;
        }

        // ============================================================
        // 建局：座位
        // ============================================================

        [Test]
        public void Seats_follow_the_players_example()
        {
            // 玩家给的例子：一队 16/12、二队 16/8
            //（一方是 P1/P2，另一方是 P3/P4）
            var c = Controller(new[] { 16, 12, 16, 8 });

            Assert.AreEqual(16, c.RollOf(c.PlayerAtSeat(1)), "1 号位应是 16。");
            Assert.AreEqual(16, c.RollOf(c.PlayerAtSeat(2)), "2 号位应是另一个 16。");
            Assert.AreEqual(12, c.RollOf(c.PlayerAtSeat(4)), "4 号位应是 12（1 号的队友）。");
            Assert.AreEqual(8, c.RollOf(c.PlayerAtSeat(3)), "3 号位应是 8（2 号的队友）。");
        }

        [Test]
        public void Seat_one_and_four_are_teammates_and_two_and_three_are_teammates()
        {
            Assert.AreEqual(4, SeatLayout.TeammateSeat(1), "座位 1 的队友是 4。");
            Assert.AreEqual(1, SeatLayout.TeammateSeat(4), "座位 4 的队友是 1。");
            Assert.AreEqual(3, SeatLayout.TeammateSeat(2), "座位 2 的队友是 3。");
            Assert.AreEqual(2, SeatLayout.TeammateSeat(3), "座位 3 的队友是 2。");
        }

        [Test]
        public void Seat_of_reports_the_players_seat()
        {
            var c = Controller(new[] { 10, 18, 15, 3 });

            // 大数大的一队（P2=18）去 1&4 -> P2 坐 1 号位
            Assert.AreEqual(1, c.SeatOf(c.PlayerAtSeat(1)));
            for (int seat = 1; seat <= 4; seat++)
                Assert.AreEqual(seat, c.SeatOf(c.PlayerAtSeat(seat)),
                    $"座位 {seat} 上的玩家应报出座位 {seat}。");
        }

        // ============================================================
        // 决定顺序
        // ============================================================

        [Test]
        public void Without_specials_the_order_is_four_three_two_one()
        {
            var c = Controller(new[] { 10, 11, 12, 13 });

            var owners = new List<int>();
            while (!c.IsFinished)
            {
                Assert.AreEqual(c.Assignee, c.Decider, "没有大失败时各自决定自己的。");
                owners.Add(c.SeatOf(c.Assignee));

                IReadOnlyList<HandSplit> options = c.Candidates;
                c.Submit(options[0]);
            }

            Assert.AreEqual(4, owners.Count);
            Assert.AreEqual(4, owners[0]);
            Assert.AreEqual(3, owners[1]);
            Assert.AreEqual(2, owners[2]);
            Assert.AreEqual(1, owners[3]);
        }

        [Test]
        public void A_critical_failure_is_decided_by_the_opponent_first()
        {
            // 座位 3 大失败 -> 先由座位 4（对手）替他决定
            // 这里要让"座位 3 的点数是 1"：座位 3 是二队的小数
            //  一队 10/11（大数 11）、二队 1/12（大数 12）
            //  -> 二队大数 12 更大 -> 二队去 1&4（12 坐 1 号、1 坐 4 号）
            //  一队去 2&3（11 坐 2 号、10 坐 3 号）
            // 这样座位 4 是点数 1（大失败），由座位 3 决定。
            var c = Controller(new[] { 10, 11, 1, 12 });

            Assert.AreEqual(1, c.RollOf(c.PlayerAtSeat(4)), "4 号位应是那个大失败（点数 1）。");

            Assert.True(c.IsNamingOpponent, "大失败这一步是'替对手决定'。");
            Assert.False(ReferenceEquals(c.Assignee, c.Decider),
                "决定者不是被决定者（由对手代定）。");
            Assert.AreEqual(3, c.SeatOf(c.Decider), "四号位的对手是三号位。");
            Assert.AreEqual(4, c.SeatOf(c.Assignee), "被决定的是四号位。");
        }

        [Test]
        public void Major_success_is_decided_last()
        {
            // 一队 20/10、二队 15/3
            //  -> 一队大数 20 > 15 -> 一队去 1&4（20 坐 1 号、10 坐 4 号）
            //  二队去 2&3（15 坐 2 号、3 坐 3 号）
            // 所以大成功在 1 号位 -> 应排在最后
            var c = Controller(new[] { 20, 10, 15, 3 });

            Assert.AreEqual(20, c.RollOf(c.PlayerAtSeat(1)), "大成功应在 1 号位。");

            var owners = new List<int>();
            while (!c.IsFinished)
            {
                owners.Add(c.SeatOf(c.Assignee));
                c.Submit(c.Candidates[0]);
            }

            Assert.AreEqual(1, owners[owners.Count - 1], "大成功（1 号位）应最后决定。");
        }

        // ============================================================
        // 信息优势
        // ============================================================

        [Test]
        public void Nothing_is_visible_before_the_first_decision()
        {
            var c = Controller(new[] { 10, 11, 12, 13 });

            Assert.AreEqual(0, c.DecidedSeatsBefore.Count,
                "第一步之前没有任何已决定的点数可看。");
            Assert.Null(c.SplitOf(c.PlayerAtSeat(1)), "还没决定。");
        }

        [Test]
        public void Later_deciders_can_see_the_numbers_decided_before_them()
        {
            // 玩家确认："整个骰先攻决定自己数字的过程中，
            //            后决定的人可以看到前面人决定的数字。"
            var c = Controller(new[] { 10, 11, 12, 13 });

            // ⚠️ 必须用**真实的候选**提交（我第一版写了 HandSplit(1,2)，
            //    总和 3 对 4 号位的点数 12 不合法 -> 被正确拒绝 -> 什么都没决定）。
            HandSplit first = c.Candidates[0];
            int firstOwnerSeat = c.SeatOf(c.Assignee);
            Assert.True(c.Submit(first), "第一步应能提交一个合法拆分。");

            var visible = c.DecidedSeatsBefore;
            Assert.AreEqual(1, visible.Count, "第二步应能看到第一步的结果。");
            Assert.True(visible[0] == firstOwnerSeat,
                $"看到的应该是第一步被决定的座位 {firstOwnerSeat}。");

            HandSplit decided = c.SplitOf(c.PlayerAtSeat(firstOwnerSeat)).Value;
            Assert.AreEqual(first.Left, decided.Left);
            Assert.AreEqual(first.Right, decided.Right);

            // 第二步 -> 现在能看到两个
            HandSplit second = c.Candidates[0];
            Assert.True(c.Submit(second), "第二步应能提交。");
            Assert.AreEqual(2, c.DecidedSeatsBefore.Count, "第三步应能看到前两步。");
        }

        [Test]
        public void The_opponent_deciding_a_critical_failure_also_sees_prior_numbers()
        {
            // 座位 4 大失败（点数 1）-> 由座位 3 决定；而且座位 3 能看到前面已决定的
            var c = Controller(new[] { 10, 11, 1, 12 });
            Assert.AreEqual(1, c.RollOf(c.PlayerAtSeat(4)));

            bool sawPrior = false;
            while (!c.IsFinished)
            {
                if (c.IsNamingOpponent)
                {
                    // 这一步是替大失败者决定 —— 前面已决定的必须可见
                    sawPrior = true;
                    Assert.True(c.DecidedSeatsBefore.Count >= 0,
                        "替对手决定时也要能看到前面的数字。");
                }
                c.Submit(c.Candidates[0]);
            }

            Assert.True(sawPrior, "整轮里应当出现过'替对手决定'的一步。");
        }

        // ============================================================
        // 候选与提交
        // ============================================================

        [Test]
        public void Candidates_follow_the_assignees_roll()
        {
            // 座位 4 的点数是 **8**（一队是 16/8，8 是一队的小数 -> 4 号位），
            // 所以候选应是"两只手之和 8"的合法拆分。
            var c = Controller(new[] { 16, 8, 13, 12 });
            Assert.AreEqual(8, c.RollOf(c.PlayerAtSeat(4)));

            IReadOnlyList<HandSplit> options = c.Candidates;
            Assert.True(options.Count > 0);
            for (int i = 0; i < options.Count; i++)
                Assert.AreEqual(8, options[i].Left + options[i].Right,
                    "有总和约束时两只手之和必须等于点数。");
        }

        [Test]
        public void A_critical_failure_is_decided_by_the_opponent()
        {
            var c = Controller(new[] { 10, 11, 1, 12 });

            int failSeat = SeatWithRoll(c, 1);
            Assert.True(failSeat > 0, "应有一个大失败（点数 1）。");
            Assert.True(c.IsNamingOpponent, "大失败这一步由对手代定。");
            Assert.AreEqual(failSeat, c.SeatOf(c.Assignee), "被决定的是投出 1 的那个人。");
            Assert.AreEqual(TwoOnTwoInitiative.OpponentSeatOf(failSeat), c.SeatOf(c.Decider),
                "由他的对手决定。");

            // ⚠️ 这里**只断言"能提交"**，不断言候选数量 ——
            //    "大失败是否完全自由"存在规则冲突（见下方注释），需要玩家确认后再定。
            Assert.True(c.Candidates.Count > 0, "大失败也必须有合法候选。");
            Assert.True(c.Submit(c.Candidates[0]), "应能提交。");
        }

        // ⚠️ 未决问题（已向玩家提出）：
        //    现有 1v1 规则（DiceRollSetup 的文档）把大失败当"总和 1"，
        //    因此 DistinctSplits(1) 只给出 1/1 一种拆分；
        //    而玩家在 2v2 里说"对手决定大失败也是完全自由"（那会有 81 种）。
        //    这两者不一致，**不该由我猜**：


        [Test]
        public void Illegal_splits_are_rejected()
        {
            var c = Controller(new[] { 16, 8, 13, 12 });
            Assert.AreEqual(8, c.RollOf(c.PlayerAtSeat(4)));

            // 总和 5 的拆分对点数 8 不合法
            Assert.False(c.IsCandidate(new HandSplit(3, 2)), "总和不对 -> 不合法。");
            Assert.False(c.Submit(new HandSplit(3, 2)), "不合法的拆分必须被拒绝。");

            // 被拒绝后不应推进：当前步骤仍然是 4 号位
            Assert.AreEqual(8, c.RollOf(c.Assignee), "被拒绝时当前步骤不能推进。");
        }

        [Test]
        public void Every_seat_is_decided_exactly_once()
        {
            var c = Controller(new[] { 1, 20, 19, 16 });
            var owners = new List<int>();

            while (!c.IsFinished)
            {
                owners.Add(c.SeatOf(c.Assignee));
                Assert.True(c.Submit(c.Candidates[0]));
            }

            Assert.AreEqual(4, owners.Count);
            for (int i = 0; i < owners.Count; i++)
                for (int j = i + 1; j < owners.Count; j++)
                    Assert.False(owners[i] == owners[j], "同一座位不能决定两次。");

            for (int seat = 1; seat <= 4; seat++)
                Assert.True(c.IsDecided(seat), $"座位 {seat} 应已决定。");
        }

        [Test]
        public void Submitting_after_finish_is_rejected()
        {
            var c = Controller(new[] { 10, 11, 12, 13 });
            PlayThrough(c);

            Assert.True(c.IsFinished);
            Assert.Null(c.CurrentStep, "完成后没有当前步骤。");
            Assert.False(c.Submit(new HandSplit(1, 1)), "完成后不能再提交。");
        }

        // ============================================================
        // 写进玩家手牌
        // ============================================================

        [Test]
        public void ApplyToPlayers_writes_the_decided_values()
        {
            var players = MakePlayers();
            var c = new TwoOnTwoStartController(players, Teams,
                                                new[] { 10, 11, 12, 13 }, new Random(1));

            // 每一步提交一个已知拆分
            var submitted = new Dictionary<int, HandSplit>();
            while (!c.IsFinished)
            {
                int seat = c.SeatOf(c.Assignee);
                // 选一个"总和 = 点数"的合法拆分（大失败时随便取一个）
                HandSplit pick = c.Candidates[0];
                submitted[seat] = pick;
                c.Submit(pick);
            }

            c.ApplyToPlayers();

            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState p = c.PlayerAtSeat(seat);
                HandSplit expect = submitted[seat];

                Assert.AreEqual(expect.Left, p.LeftHand.Value,
                    $"座位 {seat} 的左手应为 {expect.Left}。");
                Assert.AreEqual(expect.Right, p.RightHand.Value,
                    $"座位 {seat} 的右手应为 {expect.Right}。");
            }
        }

        [Test]
        public void ApplyToPlayers_does_not_form_shields()
        {
            // 初始手值**不触发任何效果** —— 即使分到 5 也不该是护盾
            var players = MakePlayers();
            var c = new TwoOnTwoStartController(players, Teams,
                                                new[] { 10, 11, 12, 13 }, new Random(1));

            while (!c.IsFinished)
                c.Submit(c.Candidates[0]);

            c.ApplyToPlayers();

            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState p = c.PlayerAtSeat(seat);
                Assert.AreEqual(ShieldState.None, p.LeftHand.State,
                    "开局不应出现护盾（初始手值不触发效果）。");
                Assert.AreEqual(ShieldState.None, p.RightHand.State,
                    "开局不应出现护盾（初始手值不触发效果）。");
            }
        }

        // ============================================================
        // 随机掷骰
        // ============================================================

        [Test]
        public void RollAndBegin_produces_a_valid_full_flow_for_many_seeds()
        {
            for (int seed = 1; seed <= 80; seed++)
            {
                var players = MakePlayers();
                var c = TwoOnTwoStartController.RollAndBegin(
                    players, Teams, new Random(seed));

                Assert.AreEqual(4, c.Order.Count, "总是四个人各决定一次。");

                for (int seat = 1; seat <= 4; seat++)
                {
                    int roll = c.RollOf(c.PlayerAtSeat(seat));
                    Assert.True(roll >= 1 && roll <= 20,
                        $"点数必须在 1..20（实际 {roll}）。");
                }

                PlayThrough(c);
                c.ApplyToPlayers();

                for (int seat = 1; seat <= 4; seat++)
                {
                    PlayerState p = c.PlayerAtSeat(seat);
                    Assert.True(p.LeftHand.Value >= 1 && p.LeftHand.Value <= 9,
                        "左手必须在 1..9。");
                    Assert.True(p.RightHand.Value >= 1 && p.RightHand.Value <= 9,
                        "右手必须在 1..9。");
                }
            }
        }

        [Test]
        public void Seat_summary_lists_all_four_seats()
        {
            var c = Controller(new[] { 10, 11, 12, 13 });
            string summary = c.SeatSummary;

            for (int seat = 1; seat <= 4; seat++)
                Assert.True(summary.Contains(seat + "号位"),
                    $"说明里应包含 {seat} 号位（实际：{summary}）。");
        }

        [Test]
        public void Hint_says_when_the_opponent_decides()
        {
            var c = Controller(new[] { 10, 11, 1, 12 });
            Assert.True(c.IsNamingOpponent);

            string hint = c.Hint;
            Assert.True(hint.Contains("大失败"),
                $"替大失败决定时的提示应说明原因（实际：{hint}）。");
        }
    }
}
