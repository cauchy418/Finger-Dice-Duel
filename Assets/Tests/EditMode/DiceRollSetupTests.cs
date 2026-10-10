using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 开局掷骰分配初始手值（1d20）。
    ///
    /// 规则见 <see cref="DiceRollSetup"/> 的类注释。这里逐条钉住：
    ///   · 合法拆分的范围（`max(1, N-9) .. min(9, N-1)`）
    ///   · 特殊点数 1 / 19 / 20
    ///   · 决定顺序（点数小的先决定，相同则同时）
    ///   · **初始手值不触发任何效果**
    /// </summary>
    [TestFixture]
    public class DiceRollSetupTests
    {
        // ============================================================
        // 合法拆分
        // ============================================================

        [Test]
        public void Split_values_are_bounded_by_the_other_hands_maximum()
        {
            // 合法拆分 = 两手都在 1..9 且之和为 N。
            // 因此单手的范围是 max(1, N-9) .. min(9, N-1)，**左右两侧都算**
            // （1/7 与 7/1 是两种不同分配）。
            //
            // 玩家给过的例子（"8 时是 1/7、2/6、3/5、4/4"）只列到 4/4，
            // 那是"左手 ≤ 右手"的一半；镜像同样合法。
            var s8 = DiceRollSetup.LegalSplits(8);
            Assert.AreEqual(7, s8.Count, "8 应有 7 种拆分（1..7 配 7..1）。");
            AssertSplit(s8, 0, 1, 7);
            AssertSplit(s8, 3, 4, 4);
            AssertSplit(s8, 6, 7, 1);      // 镜像也要有

            // 12：下限 3（3+9），上限 9 —— 共 7 种
            var s12 = DiceRollSetup.LegalSplits(12);
            Assert.AreEqual(7, s12.Count, "12 应有 7 种拆分。");
            AssertSplit(s12, 0, 3, 9);
            AssertSplit(s12, 3, 6, 6);
            AssertSplit(s12, 6, 9, 3);

            // 13：下限 4 —— 共 6 种
            var s13 = DiceRollSetup.LegalSplits(13);
            Assert.AreEqual(6, s13.Count, "13 应有 6 种拆分。");
            AssertSplit(s13, 0, 4, 9);
            AssertSplit(s13, 5, 9, 4);

            // 17：只有 8/9 与 9/8
            var s17 = DiceRollSetup.LegalSplits(17);
            Assert.AreEqual(2, s17.Count, "17 只有两种拆分（8/9 与 9/8）。");
            AssertSplit(s17, 0, 8, 9);
            AssertSplit(s17, 1, 9, 8);

            // 18：只有 9/9
            var s18 = DiceRollSetup.LegalSplits(18);
            Assert.AreEqual(1, s18.Count, "18 只有一种拆分。");
            AssertSplit(s18, 0, 9, 9);
        }

        [Test]
        public void Every_legal_split_sums_to_the_roll_and_stays_in_range()
        {
            // 2..18 全部检查：和恰为 N，且两手都在 1..9
            for (int n = 2; n <= 18; n++)
            {
                var list = DiceRollSetup.LegalSplits(n);
                Assert.True(list.Count > 0, $"{n} 应至少有一种拆分。");

                for (int i = 0; i < list.Count; i++)
                {
                    Assert.AreEqual(n, list[i].Total, $"{n}: {list[i]} 之和应等于 {n}。");
                    Assert.True(list[i].Left >= 1 && list[i].Left <= 9,
                        $"{n}: 左手 {list[i].Left} 应在 1..9。");
                    Assert.True(list[i].Right >= 1 && list[i].Right <= 9,
                        $"{n}: 右手 {list[i].Right} 应在 1..9。");
                }
            }
        }

        [Test]
        public void No_candidate_ever_contains_an_illegal_hand_value()
        {
            // 玩家要求："不要让不合法的选项给到玩家"
            for (int n = 1; n <= 20; n++)
            {
                var list = DiceRollSetup.LegalSplits(n);
                for (int i = 0; i < list.Count; i++)
                {
                    Assert.True(list[i].Left >= 1 && list[i].Left <= 9,
                        $"投出 {n} 时给出了非法左手 {list[i].Left}。");
                    Assert.True(list[i].Right >= 1 && list[i].Right <= 9,
                        $"投出 {n} 时给出了非法右手 {list[i].Right}。");
                }
            }
        }

        // ============================================================
        // 特殊点数
        // ============================================================

        [Test]
        public void Nineteen_requires_one_hand_to_be_nine()
        {
            var list = DiceRollSetup.LegalSplits(19);
            Assert.True(list.Count > 0);

            for (int i = 0; i < list.Count; i++)
                Assert.True(list[i].Left == 9 || list[i].Right == 9,
                    $"19 的拆分 {list[i]} 必须有一只手是 9。");

            // "另一只手可以随意" -> 1..9 都应该出现
            bool hasOne = false, hasEight = false;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Left == 1 || list[i].Right == 1) hasOne = true;
                if (list[i].Left == 8 || list[i].Right == 8) hasEight = true;
            }
            Assert.True(hasOne, "19 时另一只手应可以是 1。");
            Assert.True(hasEight, "19 时另一只手应可以是 8。");

            // 19 不受"总和 = 19"的约束（9+1 = 10 是合法的）
            Assert.True(DiceRollSetup.IsLegalSplit(19, new HandSplit(9, 1)),
                "19 时 9+1 应合法（另一只手随意）。");
        }

        [Test]
        public void Twenty_allows_any_legal_split()
        {
            // 玩家选定："任意合法拆分（1+1 到 9+9 都行）"
            Assert.AreEqual(81, DiceRollSetup.LegalSplits(20).Count,
                "20 应有 9×9 = 81 种拆分。");

            Assert.True(DiceRollSetup.IsLegalSplit(20, new HandSplit(1, 1)));
            Assert.True(DiceRollSetup.IsLegalSplit(20, new HandSplit(9, 9)));
            Assert.True(DiceRollSetup.IsLegalSplit(20, new HandSplit(2, 7)));
        }

        [Test]
        public void Critical_failure_still_has_legal_splits_for_the_opponent_to_pick()
        {
            // 投出 1 时由对手指定 —— 对手也必须有合法值可选
            var list = DiceRollSetup.LegalSplits(1);
            Assert.True(list.Count > 0, "大失败时对手也应有合法候选。");
            AssertSplit(list, 0, 1, 1);
        }

        // ============================================================
        // 决定顺序
        // ============================================================

        [Test]
        public void The_lower_roll_decides_first()
        {
            Assert.AreEqual(DecisionOrder.ALower, DiceRollSetup.DecideOrder(7, 15));
            Assert.AreEqual(DecisionOrder.BLower, DiceRollSetup.DecideOrder(15, 7));
        }

        [Test]
        public void Equal_rolls_decide_simultaneously()
        {
            Assert.AreEqual(DecisionOrder.Simultaneous, DiceRollSetup.DecideOrder(12, 12));
            Assert.AreEqual(DecisionOrder.Simultaneous, DiceRollSetup.DecideOrder(1, 1));
        }

        // ============================================================
        // 开局流程
        // ============================================================

        private static MatchStartController Arrange(int rollA, int rollB)
        {
            var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);
            var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            // 抉择器传 null 会抛异常，这里用最简单的
            var game = new GameManager(a, b, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            var start = new MatchStartController(game);
            start.RollWith(rollA, rollB);
            return start;
        }

        [Test]
        public void The_lower_roller_is_asked_first_and_then_the_higher_one()
        {
            var start = Arrange(7, 15);

            // 7 的一方（A）先决定
            Assert.AreEqual(MatchStartPhase.Assigning, start.Phase);
            Assert.AreSame(start.Decider, start.Assignee, "正常点数由自己分配。");
            Assert.AreEqual(2, start.Assignee.LeftHand.Value + start.Assignee.RightHand.Value,
                "还没分配时手值仍是初始的 1+1。");

            // 提交一个合法拆分
            Assert.True(start.Submit(new HandSplit(1, 6)), "1+6 应是 7 的合法拆分。");

            // 现在轮到 15 的一方（B）
            Assert.AreEqual(MatchStartPhase.Assigning, start.Phase);
            Assert.True(start.Decider != null);
            Assert.AreEqual(15, start.Candidates[0].Total, "候选应属于点数 15 的一方。");
        }

        [Test]
        public void An_illegal_split_is_rejected()
        {
            var start = Arrange(8, 12);

            // A 投出 8：1+7 合法，但 2+7=9 不合法
            Assert.True(start.IsCandidate(new HandSplit(1, 7)));
            Assert.False(start.IsCandidate(new HandSplit(2, 7)), "2+7=9，不是 8 的拆分。");
            Assert.False(start.Submit(new HandSplit(2, 7)), "非法拆分应被拒绝。");
        }

        [Test]
        public void Hands_cannot_exceed_nine_or_drop_below_one()
        {
            var start = Arrange(12, 5);

            // 12 的合法下限是 3 —— 2+10 这种"看起来和为 12"的必须被拒
            Assert.False(start.IsCandidate(new HandSplit(2, 10)), "单手不能超过 9。");
            Assert.False(start.IsCandidate(new HandSplit(0, 12)), "单手不能小于 1。");
            Assert.False(start.Submit(new HandSplit(2, 10)));
        }

        [Test]
        public void Critical_failure_lets_the_opponent_assign_your_hands()
        {
            // A 投出 1（大失败），B 投出 15。
            // 1 < 15 -> A 是"点数小的"，先决定；但 A 大失败 -> 由 B 替他指定。
            var start = Arrange(1, 15);

            Assert.AreEqual(MatchStartPhase.Assigning, start.Phase);
            Assert.True(start.IsNamingOpponent, "大失败应由对手指定。");
            Assert.False(ReferenceEquals(start.Decider, start.Assignee), "决策者与承受者不是同一人。");

            // 承受者（投出 1 的那位）的候选必须是 1+1 —— 只有这一种
            AssertSplit(start.Candidates, 0, 1, 1);

            Assert.True(start.Submit(new HandSplit(1, 1)), "1+1 是投出 1 的合法拆分。");
        }

        [Test]
        public void Both_critical_failures_name_each_other()
        {
            var start = Arrange(1, 1);

            Assert.AreEqual(DecisionOrder.Simultaneous, start.Order, "同点数应同时分配。");
            Assert.True(start.IsNamingOpponent, "双方都大失败 -> 互相指定。");

            Assert.True(start.Submit(new HandSplit(1, 1)), "替对手指定 1+1。");
            Assert.AreEqual(MatchStartPhase.Assigning, start.Phase);
            Assert.True(start.IsNamingOpponent, "第二个人同样是替对手指定。");
            Assert.True(start.Submit(new HandSplit(1, 1)));
            Assert.AreEqual(MatchStartPhase.Ready, start.Phase);
        }

        [Test]
        public void The_flow_completes_and_writes_the_chosen_values()
        {
            var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);
            var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var game = new GameManager(a, b, new SimpleChoiceProvider(), new SimpleChoiceProvider());
            var start = new MatchStartController(game);

            start.RollWith(12, 13);

            // A 投 12（先决定），选 6+6
            Assert.True(start.Submit(new HandSplit(6, 6)));
            // B 投 13，选 4+9
            Assert.True(start.Submit(new HandSplit(4, 9)));

            Assert.AreEqual(MatchStartPhase.Ready, start.Phase);
            Assert.True(start.IsComplete);

            Assert.AreEqual(6, a.LeftHand.Value, "A 左手应是 6。");
            Assert.AreEqual(6, a.RightHand.Value, "A 右手应是 6。");
            Assert.AreEqual(4, b.LeftHand.Value, "B 左手应是 4。");
            Assert.AreEqual(9, b.RightHand.Value, "B 右手应是 9。");
        }

        [Test]
        public void Initial_hand_values_do_not_trigger_any_effect()
        {
            // 玩家明确要求："初始手值没有任何初始效果，
            // 如初始的 5 是一面破碎的盾牌，刺客初始出双 7 也不会进潜行"。
            var warrior = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Warrior);
            var assassin = CharacterFactory.CreatePlayer("刺客", CharacterDefinition.Assassin);

            Assert.True(warrior.Character is WarriorLogic, "前置：应是战士逻辑。");
            Assert.True(assassin.Character is AssassinLogic, "前置：应是刺客逻辑。");

            // 直接摆成"开局掷骰就是这样"
            TestSetup.SetHand(warrior.LeftHand, 5);        // 5 = 合盾的数字
            TestSetup.SetHand(assassin.LeftHand, 7);       // 7 = 潜行的数字
            TestSetup.SetHand(assassin.RightHand, 7);

            // ① 初始的 5 **不是**盾
            Assert.AreEqual(ShieldState.None, warrior.LeftHand.State,
                "初始的 5 不是盾（既不是 Formed 也不是 Broken）。");
            Assert.False(warrior.HasDoubleShield, "初始 5 不该算双盾。");
            Assert.False(warrior.LeftHand.IsShield, "初始 5 不该是成型盾。");

            // ② 初始的双 7 **不进潜行**
            Assert.True(AssassinLogic.HasDoubleSeven(assassin), "前置：双手数值是 7/7。");
            Assert.False((assassin.Character as AssassinLogic).IsStealthActive(assassin),
                "初始的双 7 不应进入潜行 —— 潜行必须**经由运算**达成。");
        }

        // ============================================================
        // 工具
        // ============================================================

        private static string[] ToStrings(IReadOnlyList<HandSplit> list)
        {
            var arr = new string[list.Count];
            for (int i = 0; i < list.Count; i++) arr[i] = list[i].ToString();
            return arr;
        }

        private static void AssertSplit(IReadOnlyList<HandSplit> list, int index, int left, int right)
        {
            Assert.True(index < list.Count, $"候选只有 {list.Count} 个，取不到第 {index} 个。");
            Assert.AreEqual(left, list[index].Left, $"第 {index} 个的左手应为 {left}。");
            Assert.AreEqual(right, list[index].Right, $"第 {index} 个的右手应为 {right}。");
        }
    }
}
