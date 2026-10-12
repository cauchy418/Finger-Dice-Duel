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
        public void Real_roll_path_never_names_the_opponent_unless_a_one_was_rolled()
        {
            // ⚠️ 真实 bug 的回归（玩家实机报告）：
            // 「战士 7 vs 战士 11」却显示「战士 投出 11（大失败）：由你来替 战士 指定初始手值」
            // ——11 不是大失败，而且**不该由玩家替对手指定**。
            //
            // 根因：Roll() 把 BeginDecision 的第二个参数（被分配者）写成了对手，
            // 于是 IsNamingOpponent 恒为 true、候选按对手的点数算。
            // RollWith() 是对的，所以只测 RollWith 的用例全都漏掉了它 ——
            // 所以这条测试**必须走真实的 Roll() 路径**（真随机，扫多个种子）。
            for (int seed = 0; seed < 300; seed++)
            {
                var a = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Warrior);
                var b = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Warrior);
                var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                           new SimpleChoiceProvider());
                var start = new MatchStartController(game, new System.Random(seed));

                start.Roll();

                int steps = 0;
                while (!start.IsComplete && steps++ < 5)
                {
                    string st = start.DebugState();
                    int assigneeRoll = ReferenceEquals(start.Assignee, a) ? start.RollA : start.RollB;

                    if (start.IsNamingOpponent)
                    {
                        // 只有"承受者投出 1"才允许替对手指定
                        Assert.True(DiceRollSetup.IsCriticalFailure(assigneeRoll),
                            $"seed={seed}：承受者投出 {assigneeRoll} 却出现'替对手指定'。状态：{st}");
                        Assert.True(start.DecisionText.Contains("大失败"),
                            $"seed={seed}：替对手指定时应说明是大失败。状态：{st}");
                    }
                    else
                    {
                        Assert.False(start.DecisionText.Contains("大失败"),
                            $"seed={seed}：非大失败不该出现'大失败'字样。状态：{st}");
                        // 决策者就是承受者
                        Assert.True(ReferenceEquals(start.Decider, start.Assignee),
                            $"seed={seed}：非大失败时决策者应是被分配者本人。状态：{st}");
                    }

                    // 普通点数的候选之和必须等于**被分配者**的点数
                    if (start.Candidates.Count > 0
                        && !DiceRollSetup.IgnoresTotal(assigneeRoll)
                        && !DiceRollSetup.IsCriticalFailure(assigneeRoll))
                    {
                        Assert.AreEqual(assigneeRoll, start.Candidates[0].Total,
                            $"seed={seed}：候选之和应等于承受者的点数。状态：{st}");
                    }

                    if (!start.Submit(start.Candidates[0])) break;
                }

                Assert.True(start.IsComplete, $"seed={seed}：流程应能走完。");
            }
        }

        [Test]
        public void Reported_case_seven_vs_eleven_lets_the_lower_roller_choose_himself()
        {
            // 玩家实机报告的具体一局：战士 7 vs 战士 11。
            // 7 更小 -> 7 的那位**自己**选；11 不该被当成大失败。
            var s = Arrange(7, 11);

            Assert.AreEqual(DecisionOrder.ALower, s.Order, "7 < 11，A 先分配。");
            Assert.AreSame(s.PlayerA, s.Decider, "应由 A 自己决定。");
            Assert.AreSame(s.PlayerA, s.Assignee, "被分配者是 A 本人。");
            Assert.False(s.IsNamingOpponent, "没人投出 1 -> 不该替对手指定。");
            Assert.False(s.DecisionText.Contains("大失败"),
                "11 不参与这里的判定；7 也不是大失败。实际：" + s.DecisionText);
            Assert.True(s.DecisionText.Contains("7"), "说明里应是 7。实际：" + s.DecisionText);
            Assert.AreEqual(7, s.Candidates[0].Total, "候选之和应为 7。");
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
        // 掷骰动画时间轴（纯数据，可无头验证）
        // ============================================================

        [Test]
        public void Dice_animation_ends_on_the_real_roll()
        {
            for (int roll = 1; roll <= 20; roll++)
            {
                var plan = new DiceRollAnimationPlan(roll, new System.Random(roll));
                Assert.AreEqual(roll, plan.FinalValue);
                Assert.AreEqual(roll, plan.FaceAt(plan.Duration), $"{roll}: 定格应为真实点数。");
                Assert.AreEqual(roll, plan.FaceAt(plan.Duration + 10f), "超过时长后应一直是真实点数。");
                Assert.True(plan.IsFinished(plan.Duration));
                Assert.False(plan.IsFinished(0f));
            }
        }

        [Test]
        public void Dice_animation_face_is_deterministic_for_the_same_time()
        {
            // ⚠️ 关键：UI 每帧会多次询问"现在显示几"，
            // 若那里现抽随机数，同一帧的不同绘制点会看到不同值（闪错）。
            var plan = new DiceRollAnimationPlan(14, new System.Random(7));

            for (float t = 0f; t < plan.Duration; t += 0.037f)
            {
                int a = plan.FaceAt(t);
                int b = plan.FaceAt(t);
                Assert.AreEqual(a, b, $"t={t} 时两次询问应得到同一个数。");
                Assert.True(a >= 1 && a <= 20, $"滚动期间的点数应在 1..20，实际 {a}。");
            }
        }

        [Test]
        public void Dice_animation_frame_index_is_monotonic()
        {
            var plan = new DiceRollAnimationPlan(9, new System.Random(3));

            int last = -1;
            for (float t = 0f; t < plan.Duration + 0.5f; t += 0.02f)
            {
                int idx = plan.IndexAt(t);
                Assert.True(idx >= last, $"帧号不该回退（t={t} 时 {idx} < {last}）。");
                last = idx;
            }

            Assert.AreEqual(plan.FrameCount - 1, plan.IndexAt(plan.Duration),
                "结束后应停在最后一帧（定格帧）。");
        }

        [Test]
        public void Dice_animation_slows_down_over_time()
        {
            // 换号间隔必须**递增**（开头快、结尾慢），这是"减速定格"观感的核心
            var plan = new DiceRollAnimationPlan(12, new System.Random(1));

            float first = plan.IntervalAt(0f);
            float mid = plan.IntervalAt(0.5f);
            float last = plan.IntervalAt(1f);

            Assert.True(first < mid, $"起步间隔应小于中段（{first} vs {mid}）。");
            Assert.True(mid < last, $"中段间隔应小于收尾（{mid} vs {last}）。");

            // 端点应等于配置值（用缩放成整数来比，shim 没有 delta 重载）
            Assert.AreEqual((int)(plan.FastInterval * 10000), (int)(first * 10000),
                "起步间隔应等于 FastInterval。");
            Assert.AreEqual((int)(plan.SlowInterval * 10000), (int)(last * 10000),
                "收尾间隔应等于 SlowInterval。");
        }

        [Test]
        public void Dice_animation_scale_settles_at_one()
        {
            var plan = new DiceRollAnimationPlan(5, new System.Random(2));

            Assert.True(plan.ScaleAt(0f) > 1f, "开头应略微放大。");
            Assert.AreEqual(10000, (int)(plan.ScaleAt(plan.Duration) * 10000), "结尾应回落到 1。");
            Assert.AreEqual(10000, (int)(plan.ScaleAt(plan.Duration + 5f) * 10000), "之后保持 1。");
        }

        [Test]
        public void Order_text_names_the_lower_roller_as_going_first()
        {
            // ⚠️ 真实 bug 的回归：界面曾经自己拼这行文字、
            // 把 decider 的名字两边都用上，于是"点数小的先分配"指向了错的人。
            // 现在文案由控制器提供，这里直接钉住内容。
            var a17b19 = Arrange(17, 19);
            Assert.AreEqual(DecisionOrder.ALower, a17b19.Order);
            Assert.True(a17b19.OrderText.Contains(a17b19.PlayerA.Name),
                $"点数小的是 A，应先提到 A。实际：{a17b19.OrderText}");
            Assert.True(a17b19.OrderText.Contains(a17b19.PlayerB.Name),
                $"也应提到 B。实际：{a17b19.OrderText}");

            var bLower = Arrange(15, 7);
            Assert.AreEqual(DecisionOrder.BLower, bLower.Order);
            Assert.True(bLower.OrderText.Contains(bLower.PlayerB.Name), "点数小的是 B。");

            var tie = Arrange(12, 12);
            Assert.AreEqual(DecisionOrder.Simultaneous, tie.Order);
            Assert.True(tie.OrderText.Contains("同时"), $"平手应说明同时分配。实际：{tie.OrderText}");
        }

        [Test]
        public void Only_a_roll_of_one_is_a_critical_failure()
        {
            // ⚠️ 真实 bug 的回归：19 被误当成大失败。
            Assert.True(DiceRollSetup.IsCriticalFailure(1));
            for (int roll = 2; roll <= 20; roll++)
                Assert.False(DiceRollSetup.IsCriticalFailure(roll),
                    $"{roll} 不是大失败。");

            // 19 是小成功、20 是大成功
            Assert.True(DiceRollSetup.IsMinorSuccess(19));
            Assert.True(DiceRollSetup.IsMajorSuccess(20));
            Assert.False(DiceRollSetup.IsMinorSuccess(20));
            Assert.False(DiceRollSetup.IsMajorSuccess(19));
        }

        [Test]
        public void Decision_text_only_mentions_critical_failure_on_a_roll_of_one()
        {
            // 19 点的一方应看到"小成功"，绝不能看到"大失败"。
            // A 投 19、B 投 5 -> 5 < 19，所以**先决定的是 B**。
            // 要检查"投 19 那位"的说明，得先让 B 分配完，再轮到 A。
            var s19 = Arrange(19, 5);
            Assert.AreEqual(DecisionOrder.BLower, s19.Order, "点数小的是 B，B 先分配。");
            Assert.True(s19.Submit(new HandSplit(1, 4)), "B 投 5 -> 1+4 合法。");

            // 现在轮到 A（投 19）
            Assert.False(s19.IsNamingOpponent, "19 不是大失败，不该由对手指定。");
            Assert.False(s19.DecisionText.Contains("大失败"),
                "19 的说明不该出现大失败。实际：" + s19.DecisionText);
            Assert.True(s19.DecisionText.Contains("小成功"),
                "19 应说明是小成功。实际：" + s19.DecisionText);
            // ⚠️ 用 IsLegalSplit（对两个朝向都成立，与游戏语义一致）而不是
            // IsCandidate —— 后者是**界面候选**，已按镜像去重（19 时只留 1/9）。
            Assert.True(DiceRollSetup.IsLegalSplit(19, new HandSplit(9, 1)),
                "19 时 9+1 应合法（另一只手随意）。");
            Assert.True(DiceRollSetup.IsLegalSplit(19, new HandSplit(1, 9)),
                "19 时 1+9 也合法（同一对，只是朝向不同）。");

            // 真正的大失败：投出 1 的人由对手指定
            var c1 = Arrange(1, 15);
            Assert.True(c1.IsNamingOpponent, "投出 1 应由对手指定。");
            Assert.True(c1.DecisionText.Contains("大失败"),
                "投出 1 应说明是大失败。实际：" + c1.DecisionText);

            // 普通点数既不是大失败也不是成功
            var plain = Arrange(8, 12);
            Assert.False(plain.DecisionText.Contains("大失败"));
            Assert.False(plain.DecisionText.Contains("小成功"));
            Assert.False(plain.DecisionText.Contains("大成功"));
        }

        [Test]
        public void Only_the_critical_failure_makes_the_opponent_decide()
        {
            // 大失败以外，一律由**自己**分配。
            // （玩家实测反馈的 bug："现在不管投出什么都是点数大的给点数小的决定数字"）
            for (int a = 1; a <= 20; a++)
            {
                for (int b = 1; b <= 20; b++)
                {
                    var s = Arrange(a, b);

                    int assigneeRoll = ReferenceEquals(s.Assignee, s.PlayerA) ? a : b;

                    if (DiceRollSetup.IsCriticalFailure(assigneeRoll))
                    {
                        Assert.True(s.IsNamingOpponent,
                            $"A={a} B={b}：投出 1 的那位应由对手指定。");
                    }
                    else
                    {
                        Assert.False(s.IsNamingOpponent,
                            $"A={a} B={b}：非大失败应由自己分配。");
                    }

                    Assert.True(ReferenceEquals(s.Decider, s.Assignee) == !s.IsNamingOpponent,
                        $"A={a} B={b}：非大失败时决策者就是承受者。");
                }
            }
        }

        // ============================================================
        // 信息优势：后决定的一方看得到先手方的选择
        // ============================================================

        [Test]
        public void The_first_decider_sees_nothing()
        {
            // 先手方（点数小）先决定，此时还没有人分配过 -> 看不到任何东西
            var s = Arrange(7, 15);          // 7 小 -> A 先

            Assert.True(s.HasInfoAdvantage, "正常分配应享有信息优势。");
            Assert.True(ReferenceEquals(s.PreviouslyAssigned, null),
                "还没人分配过 -> 不该有'已分配'的人。");
            Assert.False(s.PreviousSplit.HasValue,
                "先手方不该看到任何已选的拆分。");
        }

        [Test]
        public void The_second_decider_sees_the_first_choice()
        {
            // 这就是新规则：点数大的后决定，但能看到点数小的选了什么
            var s = Arrange(7, 15);          // A 投 7 先决定

            Assert.True(s.Submit(new HandSplit(3, 4)), "7 -> 3+4 合法。");

            // 现在轮到 B（投 15，后决定）
            Assert.True(ReferenceEquals(s.Assignee, s.PlayerB), "应轮到 B 分配。");
            Assert.True(s.HasInfoAdvantage, "后决定者享有信息优势。");
            Assert.True(ReferenceEquals(s.PreviouslyAssigned, s.PlayerA),
                "已分配的人应是 A。");

            Assert.True(s.PreviousSplit.HasValue, "B 应能看到 A 的选择。");
            Assert.AreEqual(3, s.PreviousSplit.Value.Left, "A 选了 3+4。");
            Assert.AreEqual(4, s.PreviousSplit.Value.Right, "A 选了 3+4。");
        }

        [Test]
        public void The_second_decider_sees_the_first_choice_regardless_of_who_rolled_lower()
        {
            var s = Arrange(15, 7);          // B 投 7 先决定
            Assert.True(s.Submit(new HandSplit(1, 6)), "7 -> 1+6 合法。");

            Assert.True(ReferenceEquals(s.Assignee, s.PlayerA), "应轮到 A 分配。");
            Assert.True(ReferenceEquals(s.PreviouslyAssigned, s.PlayerB));
            Assert.AreEqual(1, s.PreviousSplit.Value.Left);
            Assert.AreEqual(6, s.PreviousSplit.Value.Right);
        }

        [Test]
        public void Two_critical_failures_do_not_grant_the_info_advantage()
        {
            // ⚠️ 双大失败时两人都在"替对手指定"。
            // 若这时还能看到对方的选择，就变成互相追逐的串行博弈，
            // 与规则里"同时替对手指定"矛盾 —— 所以不给优势。
            var s = Arrange(1, 1);

            Assert.AreEqual(DecisionOrder.Simultaneous, s.Order);
            Assert.True(s.IsNamingOpponent, "双方都大失败 -> 互相指定。");
            Assert.False(s.HasInfoAdvantage, "双大失败不该给信息优势。");

            Assert.True(s.Submit(new HandSplit(1, 1)));
            Assert.True(s.IsNamingOpponent, "第二个人同样是替对手指定。");
            Assert.False(s.HasInfoAdvantage, "第二次指定同样不该给优势。");
            Assert.False(s.PreviousSplit.HasValue,
                "第二次指定时不该看到第一个人的选择。");
        }

        [Test]
        public void An_unassigned_player_is_not_reported_as_a_previous_choice()
        {
            // 手牌还停在初始 1/1 时，不能把它当成"对方选了 1/1"
            var s = Arrange(7, 15);
            Assert.False(s.PreviousSplit.HasValue);

            s.Submit(new HandSplit(1, 6));   // A 真的选了 1/6
            Assert.True(s.PreviousSplit.HasValue);
            Assert.AreEqual(1, s.PreviousSplit.Value.Left);
            Assert.AreEqual(6, s.PreviousSplit.Value.Right);
        }

        [Test]
        public void A_simultaneous_decision_still_shows_the_first_choice()
        {
            // 点数相同（同时分配）时也按 A -> B 的顺序收集，
            // 所以 B 看得到 A 的选择（信息对称的语义仍成立）
            var s = Arrange(12, 12);
            Assert.AreEqual(DecisionOrder.Simultaneous, s.Order);

            Assert.True(s.Submit(new HandSplit(5, 7)));
            Assert.True(s.HasInfoAdvantage, "同时分配的第二位也享有优势。");
            Assert.True(s.PreviousSplit.HasValue);
            Assert.AreEqual(5, s.PreviousSplit.Value.Left);
            Assert.AreEqual(7, s.PreviousSplit.Value.Right);
        }

        // ============================================================
        // 去重（镜像只留一个）与"谁先行动"
        // ============================================================

        [Test]
        public void Distinct_splits_keep_only_one_of_each_mirror_pair()
        {
            // 玩家要求：1/5 与 5/1 只保留 1/5。
            // 理由：目前左右手作用完全相同，镜像纯属重复；
            // 大成功有 81 种，不去重界面放不下。
            for (int roll = 2; roll <= 20; roll++)
            {
                var all = DiceRollSetup.LegalSplits(roll);
                var distinct = DiceRollSetup.DistinctSplits(roll);

                // 只留"左手 ≤ 右手"
                for (int i = 0; i < distinct.Count; i++)
                {
                    Assert.True(distinct[i].Left <= distinct[i].Right,
                        $"投 {roll}：去重结果 {distinct[i]} 应满足左手 ≤ 右手。");
                }

                // 数量 = (全部 + 对角线条数) / 2
                int diagonal = 0;
                for (int i = 0; i < all.Count; i++)
                    if (all[i].Left == all[i].Right) diagonal++;

                Assert.AreEqual((all.Count + diagonal) / 2, distinct.Count,
                    $"投 {roll}：去重后数量应为 (全部 + 对角线) / 2。");

                // 每个去重结果都合法
                for (int i = 0; i < distinct.Count; i++)
                {
                    Assert.True(DiceRollSetup.IsLegalSplit(roll, distinct[i]),
                        $"投 {roll}：去重结果 {distinct[i]} 应仍合法。");
                }
            }
        }

        [Test]
        public void Distinct_splits_halve_the_major_success_list()
        {
            // 20（大成功）原本 81 种，去重后应剩 45 种（9 个对角 + 36 对的一半）
            Assert.AreEqual(81, DiceRollSetup.LegalSplits(20).Count);
            Assert.AreEqual(45, DiceRollSetup.DistinctSplits(20).Count,
                "大成功去重后应是 45 种（界面才放得下）。");
        }

        [Test]
        public void The_controller_offers_only_distinct_splits()
        {
            // 界面只列 controller.Candidates —— 它必须是去重后的。
            // 注意"点数小的先决定"：要让投 20 的人先决定，就得让**对方**点数更小，
            // 所以这里是 (5, 20) 而不是 (20, 5)。
            var s = Arrange(5, 20);

            // A 投 5（点数小）先决定 —— 此刻候选是 A 的（5 有 2 种去重拆分）
            Assert.AreSame(s.PlayerA, s.Assignee, "点数小的 A 先决定。");
            Assert.AreEqual(2, s.Candidates.Count, "投 5 去重后有 2 种。");

            // 让 A 决定完，才轮到 B（投 20 = 大成功）
            Assert.True(s.Submit(s.Candidates[0]));
            Assert.AreSame(s.PlayerB, s.Assignee, "轮到 B。");

            Assert.AreEqual(45, s.Candidates.Count,
                "大成功时界面候选应是去重后的 45 种。");
        }

        [Test]
        public void The_higher_roller_acts_first_both_ways()
        {
            // 玩家要求：点数**大**的先进行回合。
            // 这与"点数小的先决定初始手值"是两件事（互补，两头平衡）。
            var aHigher = Arrange(15, 7);
            Assert.AreSame(aHigher.PlayerA, aHigher.FirstToAct, "A 点数大 -> A 先行动。");

            var bHigher = Arrange(7, 15);
            Assert.AreSame(bHigher.PlayerB, bHigher.FirstToAct, "B 点数大 -> B 先行动。");
        }

        [Test]
        public void Equal_rolls_let_a_act_first()
        {
            var tie = Arrange(12, 12);
            Assert.AreSame(tie.PlayerA, tie.FirstToAct, "平手 -> A 先行动。");
        }

        [Test]
        public void First_to_act_is_unknown_before_rolling()
        {
            var a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);
            var b = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            var start = new MatchStartController(game);

            Assert.Null(start.FirstToAct, "还没掷骰时应为 null（调用方回退默认先手）。");
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
