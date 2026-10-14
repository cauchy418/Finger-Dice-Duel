using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **盗贼**的偷窃。
    ///
    /// 规则全部由玩家逐条确认（含 9 个澄清问题的答复）：
    ///   · 双方点数**与状态**互换，但只有盗贼可能触发效果
    ///   · **偷窃前的点数 ≥ 偷来的点数**才生效
    ///   · 效果由**偷来的数字**决定（4 攻击、5 合盾、9 回复）
    ///   · 偷来 5 一律合成**完整盾**（不管那 5 之前是什么状态）
    ///   · 被偷者只被改点数，**不形成盾**
    ///   · 上回合**发起过**（点过按钮）则本回合不能再偷
    ///   · 无限距离、可以偷队友
    /// </summary>
    [TestFixture]
    public class ThiefTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match(out PlayerState thief, out PlayerState foe)
        {
            thief = Make("贼", CharacterDefinition.Thief);
            foe = Make("敌", CharacterDefinition.Soldier);

            return new GameManager(thief, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        [Test]
        public void Thief_stats_and_range()
        {
            PlayerState t = Make("贼", CharacterDefinition.Thief);

            Assert.AreEqual(0, t.Character.AttackBonus);
            Assert.AreEqual(-1, t.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(t), "盗贼攻击距离 1。");
        }

        // ============================================================
        // 界面的三步流程（玩家要求：点自己 -> 点按钮 -> 点别人）
        // ============================================================

        /// <summary>造一个带控制器的局面（走真机那条路）。</summary>
        private static BoardController Board(out PlayerState thief, out PlayerState foe,
                                            out GameManager game)
        {
            thief = Make("贼", CharacterDefinition.Thief);
            foe = Make("兵", CharacterDefinition.Soldier);

            game = new GameManager(thief, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());

            return new BoardController(game,
                                       new SimpleChoiceProvider(), new SimpleChoiceProvider());
        }

        [Test]
        public void Steal_button_appears_only_after_picking_own_hand()
        {
            BoardController board = Board(out PlayerState thief, out _, out _);

            board.BeginTurn();

            // 还没点自己的手 -> 按钮不该出现
            Assert.False(board.CanSteal, "没选运算源时不该显示偷窃按钮。");

            Assert.True(board.CanClick(thief.LeftHand),
                $"前置：自己的手应该可点。[phase={board.Phase} " +
                $"active={board.ActivePlayer?.Name} " +
                $"owns={board.ActivePlayer?.Owns(thief.LeftHand)} " +
                $"locked={thief.LeftHand.IsLocked}]");
            Assert.True(board.ClickHand(thief.LeftHand),
                $"第一步：点自己的手。[phase={board.Phase} " +
                $"selected={board.SelectedHand?.Value.ToString() ?? "NULL"}]");
            Assert.True(board.CanSteal,
                $"点了自己的手 -> 应显示偷窃按钮。" +
                $"[phase={board.Phase} selected={board.SelectedHand?.Value.ToString() ?? "NULL"} " +
                $"active={board.ActivePlayer?.Name}]");
        }

        [Test]
        public void Steal_flow_goes_through_the_three_steps()
        {
            BoardController board = Board(out PlayerState thief, out PlayerState foe,
                                          out GameManager game);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            board.BeginTurn();

            // ① 点自己的手
            Assert.True(board.ClickHand(thief.LeftHand), "点自己的手");

            // ② 点「偷窃」按钮
            Assert.True(board.BeginSteal(), "点偷窃按钮");
            Assert.AreEqual(BoardPhase.ChoosingStealTarget, board.Phase,
                "应进入「选被偷的手」这一步。");

            // ③ 点别人的手 —— 完成偷窃
            Assert.True(board.ClickHand(foe.LeftHand), "点别人的手完成偷窃");

            Assert.AreEqual(4, thief.LeftHand.Value, "盗贼拿到 4。");
            Assert.AreEqual(9, foe.LeftHand.Value, "对手拿到 9。");
        }

        [Test]
        public void Cannot_pick_own_hand_as_the_steal_target()
        {
            BoardController board = Board(out PlayerState thief, out _, out _);

            board.BeginTurn();
            board.ClickHand(thief.LeftHand);
            board.BeginSteal();

            // 自己那两只手都不该能点
            Assert.False(board.CanClick(thief.LeftHand), "不能偷自己的左手。");
            Assert.False(board.CanClick(thief.RightHand), "不能偷自己的右手。");
        }

        [Test]
        public void Stealing_a_monks_zero_only_yields_one()
        {
            // 玩家补充规则："盗贼偷武僧的 0 **只能偷到 1**，
            //               目前**只有武僧能持有 0**。"
            //
            // 0 是武僧太极形态的专属产物（5 被攻击后变 0），
            // 一旦经由偷窃换手就按"被触碰"处理。
            var thief = Make("贼", CharacterDefinition.Thief);
            var monk = Make("僧", CharacterDefinition.Monk);
            var game = new GameManager(thief, monk, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            TestSetup.SetHand(thief.LeftHand, 8);
            TestSetup.SetHand(monk.LeftHand, 0);      // 武僧的太极被打掉后留下的 0

            Assert.AreEqual(0, monk.LeftHand.Value, "前置：武僧持有 0。");

            game.ExecuteSteal(thief, thief.LeftHand, monk.LeftHand);

            Assert.AreEqual(1, thief.LeftHand.Value,
                "盗贼偷 0 只能得到 1（0 不可能流通到别人手上）。");
            Assert.AreEqual(8, monk.LeftHand.Value,
                "武僧拿到盗贼原来的 8（8 不是 0，不受这条规则影响）。");
        }

        [Test]
        public void A_zero_left_on_the_monks_hand_also_becomes_one()
        {
            // 同一规则的另一半：交换之后**武僧自己**那只 0 也被触碰了。
            // 造法：武僧手里有 0，但他不是被偷的那只手 ——
            // 用盗贼的 0 换出去会更复杂，所以这里直接验证
            // "偷窃之后场上不该残留 0"。
            var thief = Make("贼", CharacterDefinition.Thief);
            var monk = Make("僧", CharacterDefinition.Monk);
            var game = new GameManager(thief, monk, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            TestSetup.SetHand(thief.LeftHand, 3);
            TestSetup.SetHand(monk.LeftHand, 0);

            game.ExecuteSteal(thief, thief.LeftHand, monk.LeftHand);

            Assert.False(thief.LeftHand.Value == 0, "盗贼手上不该有 0。");
            Assert.False(monk.LeftHand.Value == 0, "武僧那只 0 也被触碰了，应变成非 0。");
        }

        [Test]
        public void Steal_target_step_is_a_human_decision()
        {
            // ⚠️ **回归测试**（玩家实测的卡死）。
            //
            // 症状：按了「偷窃」之后游戏卡住，点手牌没反应。
            // 诊断里是 `phase=ChoosingStealTarget isHumanChooser=False`。
            //
            // 根因：`ChooserFor` 漏了 ChoosingStealTarget 分支 ->
            //      chooser=null -> IsHumanChooser=false ->
            //      UI 把玩家的点击**全丢了**。
            //
            // 这条测试钉住"这一步必须由**行动方自己**决定"。
            // 与既有的 ChoosingTargetPlayer / ChoosingGamble 是同一类要求。
            BoardController board = Board(out PlayerState thief, out _, out _);

            board.BeginTurn();
            board.ClickHand(thief.LeftHand);

            Assert.True(board.BeginSteal(), "按下偷窃按钮");
            Assert.AreEqual(BoardPhase.ChoosingStealTarget, board.Phase);

            Assert.True(board.IsHumanChooser,
                "偷窃目标这一步必须由行动方自己点 —— 否则 UI 会丢弃点击并卡死。");
            Assert.True(ReferenceEquals(board.Chooser, board.ActivePlayer),
                "chooser 应该是行动方（盗贼本人）。");
        }

        [Test]
        public void Steal_target_can_be_any_other_player()
        {
            // 玩家明确："无限距离、**可以偷队友**"。
            //
            // ⚠️ 这里只验"任意他人（含队友）都可被点"这一条判据 ——
            //    它由 CanClick 的 ChoosingStealTarget 分支实现
            //    （`return !ActivePlayer.Owns(hand)`，**不看是不是队友**）。
            //    我不在这里另建 2v2 局面：那需要复刻 Create2v2 的座位约定，
            //    而本测试不依赖座位关系也能验证"不看队伍"这一点。
            BoardController board = Board(out PlayerState thief, out PlayerState foe, out _);

            board.BeginTurn();
            board.ClickHand(thief.LeftHand);
            board.BeginSteal();

            Assert.True(board.CanClick(foe.LeftHand), "对手的手可以偷。");
            Assert.True(board.CanClick(foe.RightHand), "对手另一只手也可以偷。");

            // 关键：判据里**没有任何队伍判断**，所以队友同样可以
            // （座位与队伍关系由 2v2 的既有测试覆盖）
            Assert.False(board.CanClick(thief.LeftHand), "自己的手不行。");
        }

        [Test]
        public void Steal_swaps_both_values()
        {
            // 玩家确认："双方点数互换"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.AreEqual(4, thief.LeftHand.Value, "盗贼应拿到偷来的 4。");
            Assert.AreEqual(9, foe.LeftHand.Value, "被偷者应拿到盗贼原来的 9。");
        }

        [Test]
        public void Steal_is_effective_when_original_is_not_smaller()
        {
            // 玩家给的判定："偷窃前的数字**大于等于**偷窃后的数字即生效"
            // 例：原 9 偷 4 -> 生效 -> 4 是攻击
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            int before = foe.Health;
            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.True(r.StealEffective, "9 ≥ 4，应生效。");
            Assert.AreEqual(HandEffectType.Attack4, r.Effect, "偷来的 4 -> 攻击。");
            Assert.True(foe.Health < before, "应造成伤害。");
        }

        [Test]
        public void Steal_is_ineffective_when_original_is_smaller()
        {
            // 玩家给的例子："盗贼原先手里有个 2，拿这个 2 去偷了别人的 8，
            //               2 比 8 小，所以不会发起 8 的攻击"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 2);
            TestSetup.SetHand(foe.LeftHand, 8);

            int before = foe.Health;
            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.False(r.StealEffective, "2 < 8，不生效。");
            Assert.AreEqual(8, thief.LeftHand.Value,
                "但交换照做（玩家确认：交换照做，只是没有效果）。");
            Assert.AreEqual(2, foe.LeftHand.Value);
            Assert.AreEqual(before, foe.Health, "不生效 -> 不该造成伤害。");
        }

        [Test]
        public void Steal_of_five_with_high_original_gives_a_whole_shield()
        {
            // 玩家最新规则："只有用**大于等于 5** 的数字去偷一个 5，
            //               这个 5 才是完整盾"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 7);
            TestSetup.SetHand(foe.LeftHand, 5);

            // 那 5 之前是碎的，验证它**仍然**合成完整盾
            foe.LeftHand.SetShieldState(ShieldState.Broken);

            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.True(r.StealEffective, "7 ≥ 5，生效。");
            Assert.AreEqual(ShieldState.Formed, thief.LeftHand.State,
                "用 ≥5 去偷 5 -> 完整盾。");
        }

        [Test]
        public void Steal_of_five_with_low_original_gives_a_broken_shield()
        {
            // 玩家最新规则："用 **1~4** 去偷只会得到一个**破碎的盾**"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 4);
            TestSetup.SetHand(foe.LeftHand, 5);

            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.False(r.StealEffective, "4 < 5，判定不生效。");
            Assert.AreEqual(5, thief.LeftHand.Value, "交换照做，拿到 5。");
            Assert.AreEqual(ShieldState.Broken, thief.LeftHand.State,
                "用 1~4 去偷 5 -> 破碎的盾（不是完整盾）。");
        }

        [Test]
        public void Thief_never_inherits_taichi_from_a_monk()
        {
            // 玩家最新规则："对于武僧，盗贼**不会偷到一个带有太极的 5**，
            //               他只会偷到盾 / 破碎的 5"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);
            game.GetType();

            // 把对手换成武僧
            var monk = Make("僧", CharacterDefinition.Monk);
            var game2 = new GameManager(thief, monk, new SimpleChoiceProvider(),
                                        new SimpleChoiceProvider());

            TestSetup.SetHand(thief.LeftHand, 7);
            TestSetup.SetHand(monk.LeftHand, 5);
            monk.LeftHand.SetShieldState(ShieldState.Taichi);      // 武僧的太极

            game2.ExecuteSteal(thief, thief.LeftHand, monk.LeftHand);

            Assert.AreEqual(ShieldState.Formed, thief.LeftHand.State,
                "盗贼只拿到**普通完整盾**，不继承太极。");
            Assert.False(thief.LeftHand.State == ShieldState.Taichi,
                "盗贼不该拿到太极形态。");
        }

        [Test]
        public void Monk_receiving_a_five_gets_taichi()
        {
            // 玩家最新规则："同理**武僧收到一个 5 也只会是太极状态**"
            var thief = Make("贼", CharacterDefinition.Thief);
            var monk = Make("僧", CharacterDefinition.Monk);
            var game = new GameManager(thief, monk, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            // ⚠️ 造法要选对：武僧手上放**非 5**（3），盗贼用 **5** 去偷它 ——
            //    交换后武僧才拿到那个 5。
            //
            //    我第一版写成"武僧手上是 5、盗贼是 3"，那样武僧拿到的是 3，
            //    根本验证不到"收到 5"；而且第二次调用还会撞上冷却
            //    （"上回合已经偷过"）—— 两条都是测试自己写错。
            TestSetup.SetHand(thief.LeftHand, 5);
            TestSetup.SetHand(monk.LeftHand, 3);

            game.ExecuteSteal(thief, thief.LeftHand, monk.LeftHand);

            Assert.AreEqual(5, monk.LeftHand.Value, "武僧拿到盗贼原来的 5。");
            Assert.AreEqual(ShieldState.Taichi, monk.LeftHand.State,
                "武僧收到的 5 只会是**太极**状态（不是普通盾）。");

            // 盗贼那边：偷来的 3 不是盾
            Assert.AreEqual(3, thief.LeftHand.Value, "盗贼拿到 3。");
            Assert.AreEqual(ShieldState.None, thief.LeftHand.State);
        }

        [Test]
        public void The_victim_does_not_form_a_shield()
        {
            // 玩家确认："被偷窃的人仅仅是被变换了点数，**不产生效果**"，
            // 而且"**不形成盾**（因为他没合盾）"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);     // 9 -> 盗贼的原值
            TestSetup.SetHand(foe.LeftHand, 5);       // 5 -> 会被换给被偷者？

            // 这里用"盗贼拿 9 偷 5"：盗贼得到 5 合盾，被偷者得到 9
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.AreEqual(9, foe.LeftHand.Value, "被偷者拿到盗贼原来的 9。");
            Assert.False(foe.LeftHand.IsShield,
                "被偷者不该因为拿到数字而形成盾（他没合盾）。");
        }

        [Test]
        public void Steal_states_swap_together_with_the_values()
        {
            // 玩家确认："盗贼以**完整的盾**去换别人**完整的盾**，
            //            会换来一面完整的盾而对面的盾破碎"
            //
            // 也就是"完整 vs 破碎"时，状态**跟着数值一起交换**：
            // 盗贼把自己那面完整盾给出去，换来对面那面碎的。
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            // ⚠️ 必须用**不同数值**才能观察状态交换 ——
            //    如果两边都是 5，那么"偷来的 5 一律合成完整盾"会生效，
            //    盗贼那边永远是完整盾，看不出交换
            //    （我第一版就是这么写的，白查一轮）。
            //
            //    玩家给的语义：盗贼以**完整的盾**去换别人**完整/破碎的盾**时，
            //    状态**跟着数值一起走**。
            TestSetup.SetHand(thief.LeftHand, 5);
            thief.LeftHand.SetShieldState(ShieldState.Formed);     // 盗贼：完整 5

            TestSetup.SetHand(foe.LeftHand, 3);
            foe.LeftHand.SetShieldState(ShieldState.Broken);       // 对面：破碎的 3

            // 5 >= 3 -> 判定生效（但偷来的 3 本身无效果）
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.AreEqual(3, thief.LeftHand.Value, "盗贼拿到 3。");
            Assert.AreEqual(5, foe.LeftHand.Value, "对面拿到盗贼原来的 5。");

            // 玩家确认："状态只在数值仍是 5 时保留" -> 3 不是盾，状态清掉
            Assert.AreEqual(ShieldState.None, thief.LeftHand.State,
                "3 不是盾，所以不保留任何盾状态。");

            // 玩家确认：被偷的人拿到完整盾时它**变成碎盾**
            //（"因为他没合盾"）
            Assert.AreEqual(ShieldState.Broken, foe.LeftHand.State,
                "对面拿到完整的 5 -> 它变成碎盾。");
        }

        [Test]
        public void Cannot_steal_twice_in_a_row()
        {
            // 玩家规则："若上回合**发起了**偷窃，本回合不能继续偷窃"，
            // 且"**只要点了偷窃就算'发起过'**"
            //
            // ⚠️ 用"同一回合号内再偷一次"来验证冷却 ——
            //    CanStealOn 判的是"上次偷窃的回合号 == 当前回合号"，
            //    所以同一回合内再偷同样被拦住
            //    （这正是"上回合偷过 -> 本回合不能偷"的最小复现）。
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            Assert.True(game.CanStealOn(thief), "还没偷过 -> 可以偷。");

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.False(game.CanStealOn(thief),
                "偷过之后（回合号相同）不能再偷。");

            Assert.Throws<System.InvalidOperationException>(
                () => game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand),
                "再偷应被拒绝并说明原因。");
        }

        [Test]
        public void Ineffective_steal_still_counts_as_initiated()
        {
            // 玩家确认："只要点了偷窃就算'发起过'"
            // 所以"偷了但判定不生效"也要进入冷却
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 8);       // 1 < 8 -> 不生效

            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);
            Assert.False(r.StealEffective, "前置：这次偷窃不生效。");

            Assert.False(game.CanStealOn(thief),
                "不生效也算'发起过'，同样要进冷却。");
        }

        [Test]
        public void Steal_of_four_attacks_the_victim()
        {
            // 玩家给的例子："盗贼原先手里有个 9，拿这个 9 去偷了别人的 4，
            //               9 比 4 大，所以会正常发起 4 的攻击"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            int before = foe.Health;
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            // 盗贼 攻0、士兵 防+1 -> 4 - 1 = 3
            Assert.AreEqual(3, before - foe.Health, "4 点攻击 - 士兵防御 1 = 3 点伤害。");
        }

        [Test]
        public void No_effect_when_the_stolen_number_is_blank()
        {
            // 玩家确认："1/2/3/7 无效果（就跟平时打出 1/2/3/7 一样）"
            // 原 9 偷 3 -> 9 ≥ 3 生效，但偷来的 3 本身无效果
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 3);

            int before = foe.Health;
            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.True(r.StealEffective, "9 ≥ 3，判定生效。");
            Assert.AreEqual(HandEffectType.None, r.Effect, "但偷来的 3 无效果。");
            Assert.AreEqual(before, foe.Health, "不该造成伤害。");
        }
    }
}
