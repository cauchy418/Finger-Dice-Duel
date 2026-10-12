using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 棋盘交互状态机（<see cref="BoardController"/>）。
    ///
    /// 这一层是 P1 新增的"点击 -> 结算 -> 下一回合"流程，它刻意不依赖 Unity，
    /// 所以能在无头测试里完整验证 —— 否则 UI 逻辑只能靠在编辑器里手点。
    /// </summary>
    [TestFixture]
    public class BoardControllerTests
    {
        private PlayerState _a;
        private PlayerState _b;
        private GameManager _game;

        /// <summary>
        /// 用它同时充当 GameManager 的抉择器与 BoardController 的选盾接收者。
        /// 这两者必须是同一个对象，否则选盾结果传不回去（见 SimpleChoiceProvider 的注释）。
        /// </summary>
        private SimpleChoiceProvider _choiceA;
        private SimpleChoiceProvider _choiceB;
        private BoardController _board;

        [SetUp]
        public void SetUp()
        {
            _a = new PlayerState("A");
            _b = new PlayerState("B");
            _choiceA = new SimpleChoiceProvider();
            _choiceB = new SimpleChoiceProvider();
            _game = new GameManager(_a, _b, _choiceA, _choiceB);
            _board = new BoardController(_game, _choiceA, _choiceB);
        }

        // ============================================================
        // 基本流程
        // ============================================================

        [Test]
        public void BeginTurn_starts_by_asking_for_the_actors_hand()
        {
            _board.BeginTurn();

            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, _board.Phase);
            Assert.AreSame(_a, _board.ActivePlayer);
            Assert.True(_board.Hint.Length > 0, "应给出提示文字。");
        }

        // ============================================================
        // 回合数显示（玩家要求：操作的第一个回合就是「第 1 回合」）
        // ============================================================

        [Test]
        public void The_first_turn_the_player_acts_is_displayed_as_turn_one()
        {
            _board.BeginTurn();

            Assert.AreEqual(0, _board.TurnCount,
                "TurnCount 的语义是「已完成回合数」，开局为 0。");
            Assert.AreEqual(1, _board.DisplayTurn,
                "玩家操作的第一个回合应显示为「第 1 回合」。");
        }

        [Test]
        public void DisplayTurn_advances_by_one_after_each_resolved_turn()
        {
            _board.BeginTurn();
            Assert.AreEqual(1, _board.DisplayTurn);

            // 完整走完一个回合：点自己的手 -> 点对方的手 -> 确认目标
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.True(_board.LastTurn != null, "应已结算一个回合。");
            Assert.AreEqual(1, _board.TurnCount, "已打完 1 个回合。");
            Assert.AreEqual(2, _board.DisplayTurn, "下一个回合应显示为第 2 回合。");
        }

        [Test]
        public void DisplayTurn_is_clamped_to_the_turn_limit()
        {
            // 平局瞬间 TurnCount 会等于 TurnLimit，若不收敛就会显示 "51 / 50"。
            //
            // 这里**不模拟整局** —— 那需要一个有界的对局驱动（属于 MatchRunner 的职责），
            // 在单元测试里自己写循环既慢又容易挂住（我第一版就写过无界循环）。
            // 只验证显示值与上限的关系。
            _board.BeginTurn();

            Assert.AreEqual(1, _board.DisplayTurn, "起始应显示第 1 回合。");
            Assert.True(_board.DisplayTurn <= _board.TurnLimit,
                $"显示回合不应超过上限（display={_board.DisplayTurn}, limit={_board.TurnLimit}）。");
            Assert.AreEqual(GameManager.TurnLimit, _board.TurnLimit, "上限应取自 GameManager。");
        }

        [Test]
        public void Clicking_own_hand_then_opponent_hand_resolves_the_turn()
        {
            _board.BeginTurn();

            Assert.True(_board.ClickHand(_a.LeftHand), "点自己的手应被接受。");
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, _board.Phase);
            Assert.AreSame(_a.LeftHand, _board.SelectedHand);

            Assert.True(_board.ClickHand(_b.LeftHand), "点对方的手应被接受。");
            _board.ConfirmTargetPlayer();
            Assert.NotNull(_board.LastTurn, "应产生结算结果。");
            Assert.AreEqual(2, _board.LastTurn.ResultValue, "1 + 1 = 2");
        }

        // ============================================================
        // 新流程："选定参与运算的手" 与 "确认攻击目标" 是两步
        //
        // 背景：运算要用**两只手**（自己的 + 对方的）相加，所以对方那只手
        // 仍然是"加数"；但**挨打的是玩家**，不是手。多人模式下会有多个
        // 可选目标，所以这一步必须显式存在 —— 1v1 也要求点，免得流程变形。
        //
        // ⚠️ 只对**结果会产生指向某人的效果**时才要求确认（攻击 / 回复）。
        //   1+1=2（无效果）与合出护盾（作用于自己）都直接结算，不追问目标 ——
        //   否则每次运算都被追问"打谁"，纯属打扰（玩家实测反馈）。
        // ============================================================

        /// <summary>摆一个"结果一定是攻击"的现场：A.L 3 + B.L 1 = 4（攻击 4）。</summary>
        private void ArrangeAttack()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 3);
            TestSetup.SetHand(_b.LeftHand, 1);
        }

        [Test]
        public void Choosing_the_opponent_hand_asks_to_confirm_the_target_player()
        {
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);

            Assert.True(_board.ClickHand(_b.LeftHand), "点对方的手应被接受。");

            // 不再直接结算 —— 而是停下来确认"打谁"
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, _board.Phase,
                "选定参与运算的手之后，应进入「确认目标玩家」阶段。");
            Assert.Null(_board.LastTurn, "确认之前不应产生结算结果。");
            Assert.AreSame(_b, _board.TargetPlayer, "目标玩家应是 B。");
        }

        [Test]
        public void Target_player_phase_only_accepts_the_target_players_hands()
        {
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.False(_board.CanClick(_a.LeftHand),
                "确认目标阶段不应让玩家再点自己的手。");
            Assert.True(_board.CanClick(_b.LeftHand),
                "目标玩家自己的手可以点（手牌只是指定的载体）。");
        }

        [Test]
        public void The_actor_decides_during_the_target_player_phase()
        {
            // ⚠️ 真实 bug 的回归：玩家实测"战士打战士后第一步就卡住，
            // 显示 AI 正在思考"。
            //
            // 根因：`Chooser` 没覆盖 ChoosingTargetPlayer -> 落到 default
            // -> 返回 null -> IsHumanChooser=false
            // -> UI 的 OnHandClicked 直接 return，**玩家的点击被丢弃** -> 卡死。
            //
            // 这个 bug 之所以没被其它测试抓到：它们用 ConfirmTargetPlayer() 直接调用，
            // 绕过了 IsHumanChooser 这道门。所以这里必须**断言门本身**。
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, _board.Phase);
            Assert.AreSame(_a, _board.Chooser,
                "「确认目标玩家」应由**行动方**决定 —— 不能是 null。");
            Assert.True(_board.IsHumanChooser,
                "行动方是人类时，人类必须能在这一步点击（否则卡死）。");
        }

        [Test]
        public void Confirming_the_target_player_resolves_the_turn()
        {
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.True(_board.ConfirmTargetPlayer(), "确认目标应被接受。");
            Assert.NotNull(_board.LastTurn, "确认后应产生结算结果。");
            Assert.AreEqual(4, _board.LastTurn.ResultValue, "3 + 1 = 4");
        }

        [Test]
        public void Confirm_target_player_is_rejected_outside_the_target_phase()
        {
            ArrangeAttack();
            _board.BeginTurn();

            // 还在选自己的手 -> 不该能确认
            Assert.False(_board.ConfirmTargetPlayer(),
                "不在「确认目标玩家」阶段时，确认应被拒绝（幂等安全）。");

            _board.ClickHand(_a.LeftHand);
            Assert.False(_board.ConfirmTargetPlayer(),
                "还没选参与运算的手时，确认应被拒绝。");
        }

        [Test]
        public void An_operation_without_any_effect_does_not_ask_for_a_target()
        {
            // 玩家实测反馈：战士 1 + 1 运算出 2（**无效果**）也被要求选目标，
            // 产生大量无意义询问。无效果 / 护盾都不该追问。
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            Assert.True(_board.ClickHand(_b.LeftHand), "点对方的手应被接受。");

            Assert.False(_board.Phase == BoardPhase.ChoosingTargetPlayer,
                "1 + 1 = 2 无效果 -> 不应要求指定目标。");
            Assert.NotNull(_board.LastTurn, "应直接结算。");
            Assert.AreEqual(2, _board.LastTurn.ResultValue, "1 + 1 = 2");
        }

        [Test]
        public void Forming_a_shield_does_not_ask_for_a_target()
        {
            // 护盾作用于自己，不需要指定目标
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 4);      // 4 + 1 = 5 -> 合盾

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.False(_board.Phase == BoardPhase.ChoosingTargetPlayer,
                "合出护盾不需要指定目标（它作用于自己）。");
            Assert.NotNull(_board.LastTurn, "应直接结算。");
        }

        [Test]
        public void An_attack_does_ask_for_a_target()
        {
            // 对照组：攻击**必须**问（这是本次重构要保留的行为）
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, _board.Phase,
                "4 是攻击（指向某人）-> 必须确认目标。");
        }

        // ============================================================
        // 放弃攻击（只在面对刺客潜行时出现）
        // ============================================================

        /// <summary>
        /// 造一局：A 打 B，B 是潜行中的刺客。
        ///
        /// ⚠️ 潜行要求刺客**双手都是 7**，所以"参与运算的那只手"固定是 7 ——
        /// 运算结果是 actedValue + 7（测试第一版按 1+3 写，期望值算错了）。
        /// </summary>
        private void ArrangeStealthedAssassinDefender(int actedValue)
        {
            var attacker = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var assassin = CharacterFactory.CreatePlayer("刺客", CharacterDefinition.Assassin);
            var sinkA = new SimpleChoiceProvider();
            var sinkB = new SimpleChoiceProvider();
            var game = new GameManager(attacker, assassin, sinkA, sinkB);
            var board = new BoardController(game, sinkA, sinkB);

            TestSetup.SetAllHands(attacker, assassin, 1);
            TestSetup.SetHand(attacker.LeftHand, actedValue);
            // 潜行 = 双手都为 7
            TestSetup.SetHand(assassin.LeftHand, 7);
            TestSetup.SetHand(assassin.RightHand, 7);

            // ⚠️ 光有双 7 不算潜行 —— 必须"经由运算达成"过（开局掷骰也可能直接摆出双 7）
            (assassin.Character as AssassinLogic).EnterStealthForTest();

            _a = attacker;      // 复用字段，供断言使用
            _b = assassin;
            _game = game;
            _board = board;
        }

        [Test]
        public void Giving_up_an_attack_still_applies_the_operation()
        {
            // ⚠️ 玩家实测 bug：运算出攻击后点"放弃攻击"，**手值回退到运算前**。
            //    运算本身是有效的，不该被撤销 —— 应该保留结果，只是这一击不落实。
            ArrangeStealthedAssassinDefender(actedValue: 1);

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.True(_board.CanGiveUpAttack, "面对潜行的刺客 -> 应提供放弃攻击。");
            Assert.True(_board.GiveUpAttack(), "放弃攻击应被接受。");

            Assert.AreEqual(8, _a.LeftHand.Value,
                "1 + 7 = 8 -> 放弃攻击**不能**把手值退回 1。");
            Assert.AreEqual(20, _b.Health, "放弃攻击 -> 不造成任何伤害。");
            Assert.AreEqual(_b, _board.ActivePlayer, "放弃攻击算用掉本回合，应换对方行动。");
        }

        [Test]
        public void Give_up_attack_is_offered_only_against_a_stealthed_assassin()
        {
            // 玩家实测要求："放弃攻击在 1v1 中应只在面对刺客潜行时会出现"。
            ArrangeAttack();       // 普通对手
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, _board.Phase);
            Assert.False(_board.CanGiveUpAttack,
                "普通对手不该出现「放弃攻击」（否则会被用来规避正常战斗）。");
        }

        [Test]
        public void Give_up_attack_is_rejected_when_the_target_is_not_stealthed()
        {
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.False(_board.GiveUpAttack(), "不在潜行场景时放弃攻击应被拒绝。");
            Assert.Null(_board.LastTurn, "被拒绝时不该结算。");
        }

        [Test]
        public void Every_player_is_a_legal_attack_target_by_default()
        {
            // 判据是"有没有合法的攻击目标"，不是"对手是不是刺客"。
            ArrangeAttack();
            _board.BeginTurn();

            Assert.True(_board.CanBeAttacked(_b), "普通玩家可以被打。");
            Assert.True(_board.HasAnyLegalTarget, "1v1 默认有合法目标。");
            Assert.False(_board.DefenderCannotBeAttacked);
        }

        [Test]
        public void A_stealthed_assassin_removes_the_legal_target()
        {
            // 刺客潜行 = 令**自己**无法成为攻击目标
            ArrangeStealthedAssassinDefender(actedValue: 1);
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.False(_board.CanBeAttacked(_b), "潜行中的刺客不能被选为目标。");
            Assert.False(_board.HasAnyLegalTarget, "1v1 里潜行 -> 没有合法目标。");
            Assert.True(_board.DefenderCannotBeAttacked, "应给出「放弃攻击」。");
        }

        [Test]
        public void The_assassins_hands_cannot_be_clicked_to_confirm_an_attack()
        {
            // "敌方运算出攻击数字无法点击" —— 潜行时刺客的手牌点不动
            ArrangeStealthedAssassinDefender(actedValue: 1);
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.False(_board.CanClick(_b.LeftHand),
                "潜行中的刺客不能被点选为攻击目标。");
        }

        [Test]
        public void A_plain_heal_does_not_ask_for_a_target()
        {
            // 玩家实测要求："除了牧师外，其余角色运算出 9 也是自动回复，无需询问"。
            // 这里用**没有角色**的玩家（等同普通角色）验证默认行为。
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 8);      // 8 + 1 = 9 -> 回复
            _a.TakeDamage(10);

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);

            Assert.False(_board.Phase == BoardPhase.ChoosingTargetPlayer,
                "普通角色运算出 9 -> 自动回复自己，不应询问目标。");
            Assert.NotNull(_board.LastTurn, "应直接结算。");
            Assert.AreEqual(19, _a.Health, "自动回自己 9 点（10 + 9 = 19）。");
        }

        [Test]
        public void The_priest_still_asks_for_a_target_on_a_nine()
        {
            // 对照组：牧师能把回复给对手（IHealOverride），所以必须问。
            // 玩家要求：**运算出 9 时点一次就决定回复对象**，不再先确认目标玩家。
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var choiceP = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, choiceP, choiceB);
            var board = new BoardController(game, choiceP, choiceB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 8);      // 8 + 1 = 9 -> 回复
            TestSetup.SetHealth(priest, 10);

            board.BeginTurn();
            board.ClickHand(priest.LeftHand);           // ① 自己的手（运算源）
            board.ClickHand(blank.LeftHand);            // ② 对手的手 -> 8+1=9

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "运算出 9 后应直接进入「选回复目标」，而不是先确认目标玩家。");
            Assert.NotNull(board.PendingRequest);
            Assert.AreEqual(HandEffect.PriestHealTarget, board.PendingRequest.Value.Effect);

            // 点自己 = 回自己（这就是"删掉回给自己按钮"的原因：点自己本来就是回自己）
            Assert.True(board.ClickHand(priest.RightHand), "点自己的手应表示回自己。");
            Assert.AreEqual(19, priest.Health,
                $"点自己的手应回给自己（10 + 9）。实际 priest={priest.Health} blank={blank.Health}");
            Assert.AreEqual(PlayerState.DefaultMaxHealth, blank.Health,
                "对方是满血，不该被治疗。");
        }

        [Test]
        public void The_priest_heal_on_a_nine_can_also_target_the_opponent()
        {
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var choiceP = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, choiceP, choiceB);
            var board = new BoardController(game, choiceP, choiceB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 8);      // 8 + 1 = 9 -> 回复
            TestSetup.SetHealth(blank, 10);
            TestSetup.SetHealth(priest, 10);

            board.BeginTurn();
            board.ClickHand(priest.LeftHand);
            board.ClickHand(blank.LeftHand);

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase);

            // 点对手的手 = 回给对手（牧师可以把回复给他人）
            Assert.True(board.ClickHand(blank.RightHand), "点对手的手应表示回给他。");
            Assert.AreEqual(19, blank.Health, "回 9 点给对手（10 + 9）。");
            Assert.AreEqual(10, priest.Health, "自己不该被治疗。");
        }

        [Test]
        public void The_priest_asks_attack_or_heal_after_the_target_hand_is_chosen()
        {
            // 玩家确认的顺序：
            //   ① 点自己的手  ② 点对手的手（作为运算源）
            //   ③ 运算出 4/6/8 -> 弹「攻击 / 回复」  ④ 再选攻击/回复的目标
            //
            // ⚠️ 关键：提示**必须晚于**第 ② 步。
            // 因为结果 = 自己那只手 + 对手那只手，运算源没定时结果是不确定的。
            // （曾经在第 ① 步就问，只能用替身猜 —— 实测出现
            //   "该提示不提示" 与 "提示的数值不对" 两类 bug。）
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var choiceP = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, choiceP, choiceB);
            var board = new BoardController(game, choiceP, choiceB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 3);      // 3 + 1 = 4 -> 攻击或回复

            board.BeginTurn();

            // ① 点自己的手：只是选定运算源，此时**还问不出**攻击还是回复
            Assert.True(board.ClickHand(priest.LeftHand), "选自己的手应被接受。");
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, board.Phase,
                "第 ① 步之后应还在选目标手，不能提前问（结果还不确定）。");

            // ② 点对手的手 = 选定运算源 -> 结果确定 -> 这时才问
            Assert.True(board.ClickHand(blank.LeftHand), "选目标手应被接受。");
            Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase,
                "运算源定了（3+1=4）之后才该问攻击还是回复。");
            Assert.NotNull(board.PendingRequest);
            Assert.AreEqual(HandEffect.PriestAttackOrHeal, board.PendingRequest.Value.Effect);
        }

        [Test]
        public void The_priest_can_attack_after_choosing_attack()
        {
            // 选「攻击」-> 再选攻击目标 -> 结算造成伤害
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var choiceP = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, choiceP, choiceB);
            var board = new BoardController(game, choiceP, choiceB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 3);      // 3 + 1 = 4 -> 攻击

            board.BeginTurn();
            board.ClickHand(priest.LeftHand);           // ① 自己的手
            board.ClickHand(blank.LeftHand);            // ② 目标手（运算源）
            Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase);

            // 放弃"当回复用" = 当攻击打
            Assert.True(board.DeclinePendingChoice(), "选「攻击」应被接受。");

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "选完攻击后应进入「确认目标玩家」。");

            Assert.True(board.ConfirmTargetPlayer(), "确认后应结算。");
            Assert.NotNull(board.LastTurn);
            Assert.AreEqual(4, board.LastTurn.ResultValue, "3 + 1 = 4");
            Assert.True(board.LastTurn.DamageDealt > 0, "选攻击应造成伤害。");
        }

        // ============================================================
        // 牧师 4/6/8：提示不能因为"替身的手值"而漏掉（玩家实测报告）
        // ============================================================

        /// <summary>
        /// 提示要问得**准确**：结果 = 自己那只手 + 对手那只手，
        /// 所以必须在"点完对手那只手"之后才问。
        ///
        /// ⚠️ 曾经在第 ① 步（点自己的手）就问，只能用"自己另一只手"当替身猜，
        /// 并假设"换哪只目标手结果一样" —— 那个假设只在自己两只手同值时成立。
        /// 玩家实测因此出现两类错：该提示的不提示、提示的数值不对。
        /// </summary>
        [Test]
        public void Priest_gets_the_attack_or_heal_prompt_after_the_target_hand_is_chosen()
        {
            // 牧师手 5/2、对手手 1/3：
            //   · 用 5 运算、打对手的 1 -> 6（4/6/8）-> 应提示
            //   · 打对手的 3 -> 8（4/6/8）-> 也提示
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);
            var sinkP = new SimpleChoiceProvider();
            var sinkB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, sinkP, sinkB);
            var board = new BoardController(game, sinkP, sinkB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 5);
            TestSetup.SetHand(priest.RightHand, 2);
            TestSetup.SetHand(blank.LeftHand, 1);
            TestSetup.SetHand(blank.RightHand, 3);

            board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, board.Phase);

            // ① 自己的手
            Assert.True(board.ClickHand(priest.LeftHand), "点自己的手应被接受。");
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, board.Phase,
                "第 ① 步之后还没定运算源，不能提前问。");

            // ② 对手的手（运算源）-> 结果 5+1=6 确定 -> 这时才问
            Assert.True(board.ClickHand(blank.LeftHand), "点对手的手应被接受。");
            Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase,
                "5+1=6 在 4/6/8 之列，选定运算源后应问攻击还是回复。");

            string label = board.PendingAcceptLabel;
            Assert.NotNull(label, "应给出「回复 N」的按钮文案。");
            Assert.True(label.Contains("回复"), "文案应说明是回复。实际：" + label);
            Assert.True(label.Contains("6"),
                $"回复量应是真实结果 6（而不是替身算出的别的数）。实际：{label}");
        }

        [Test]
        public void Priest_does_not_prompt_when_the_chosen_target_makes_no_effect()
        {
            // 反方向：选定的运算源打不出 4/6/8 -> 不该提示。
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);
            var sinkP = new SimpleChoiceProvider();
            var sinkB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, sinkP, sinkB);
            var board = new BoardController(game, sinkP, sinkB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 2);
            TestSetup.SetHand(priest.RightHand, 2);
            TestSetup.SetHand(blank.LeftHand, 5);       // 2+5 = 7 -> 无效果
            TestSetup.SetHand(blank.RightHand, 5);

            board.BeginTurn();
            board.ClickHand(priest.LeftHand);
            board.ClickHand(blank.LeftHand);

            Assert.False(board.Phase == BoardPhase.ChoosingPreTargetChoice,
                "2+5=7 不是 4/6/8 -> 不该弹攻击/回复询问。");
        }

        [Test]
        public void Priest_does_not_prompt_on_the_exact_hands_the_player_reported()
        {
            // 玩家实测报告的原话："手是 22，对手手是 13，点自己的手会跳出运算出 4 的错误提示"。
            // 修好替身逻辑后，2+1=3、2+3=5 —— 都不在 4/6/8 里，所以**不该有任何提示**。
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);
            var sinkP = new SimpleChoiceProvider();
            var sinkB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, sinkP, sinkB);
            var board = new BoardController(game, sinkP, sinkB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 2);
            TestSetup.SetHand(priest.RightHand, 2);
            TestSetup.SetHand(blank.LeftHand, 1);
            TestSetup.SetHand(blank.RightHand, 3);

            board.BeginTurn();

            // 点自己的手：应直接进"选目标手"，没有 4 的提示
            Assert.True(board.ClickHand(priest.LeftHand), "点自己的手应被接受。");
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, board.Phase,
                "2+1=3、2+3=5 都不是 4/6/8，不该出现任何攻击/回复提示。");

            // 对手的 1：2+1 = 3（无效果）-> 不该要求确认目标玩家
            Assert.True(board.ClickHand(blank.LeftHand), "点对手的手应被接受。");
            Assert.False(board.Phase == BoardPhase.ChoosingTargetPlayer,
                "2+1=3 无效果，不该问'打谁'。");
            Assert.NotNull(board.LastTurn);
            Assert.AreEqual(3, board.LastTurn.ResultValue, "2 + 1 = 3");
        }

        [Test]
        public void Priest_prompt_shows_the_real_value_on_the_hand_the_player_reported()
        {
            // 玩家实测报告的局面：牧师手 5/8、战士手 6/8。
            //   牧师点自己的 8，再点战士的 8 -> 8+8=16 -> **6**
            //   旧逻辑在"点自己的手"时就问了，只能用替身/候选第一个去猜，
            //   猜出的是 8+6=4 -> 提示写成「回复 4」（错）。
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var warrior = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Warrior);
            var sinkP = new SimpleChoiceProvider();
            var sinkW = new SimpleChoiceProvider();
            var game = new GameManager(priest, warrior, sinkP, sinkW);
            var board = new BoardController(game, sinkP, sinkW);

            TestSetup.SetAllHands(priest, warrior, 1);
            TestSetup.SetHand(priest.LeftHand, 5);
            TestSetup.SetHand(priest.RightHand, 8);     // 运算源
            TestSetup.SetHand(warrior.LeftHand, 6);     // 8+6 = 4
            TestSetup.SetHand(warrior.RightHand, 8);    // 8+8 = 6  <- 玩家点的是这个

            board.BeginTurn();
            board.ClickHand(priest.RightHand);          // ① 自己的 8
            Assert.False(board.Phase == BoardPhase.ChoosingPreTargetChoice,
                "第 ① 步之后运算源未定，不能提前问。");

            board.ClickHand(warrior.RightHand);         // ② 战士的 8 -> 8+8=6

            Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase,
                "8+8=6 在 4/6/8 之列，应问攻击还是回复。");

            string label = board.PendingAcceptLabel;
            Assert.NotNull(label, "应给出「回复 N」。");
            Assert.True(label.Contains("6"),
                $"回复量必须是真实结果 6，不能是另一个候选算出的 4。实际：{label}");
        }

        [Test]
        public void Priest_attack_or_heal_flow_completes_on_the_hand_that_used_to_hide_the_prompt()
        {
            // 端到端：把上面那个"该提示却不提示"的局面**走完**，
            // 确认提示出现后，两条分支（攻击 / 回复）都能正常结算。
            foreach (bool chooseHeal in new[] { false, true })
            {
                var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
                var blank = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);
                var sinkP = new SimpleChoiceProvider();
                var sinkB = new SimpleChoiceProvider();
                var game = new GameManager(priest, blank, sinkP, sinkB);
                var board = new BoardController(game, sinkP, sinkB);

                TestSetup.SetAllHands(priest, blank, 1);
                TestSetup.SetHand(priest.LeftHand, 5);
                TestSetup.SetHand(priest.RightHand, 2);
                TestSetup.SetHand(blank.LeftHand, 1);    // 5+1 = 6
                TestSetup.SetHand(blank.RightHand, 3);
                TestSetup.SetHealth(priest, 10);

                board.BeginTurn();
                board.ClickHand(priest.LeftHand);        // ① 自己的手
                board.ClickHand(blank.LeftHand);         // ② 目标手（运算源）-> 5+1=6 确定

                Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase,
                    "5+1=6 在 4/6/8 之列，选定运算源后应问攻击还是回复。");

                if (chooseHeal) Assert.True(board.AcceptPendingChoice(), "选回复应被接受。");
                else Assert.True(board.DeclinePendingChoice(), "选攻击应被接受。");

                // 运算源已经选过了 -> 直接进入"确认目标玩家"（不再回到选目标手）
                Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                    "选完攻击/回复后应进入「确认目标玩家」。");
                Assert.True(board.ConfirmTargetPlayer(), "确认后应结算。");

                if (chooseHeal)
                {
                    // 回复还有一步：**选回复给谁**（玩家要求的顺序：
                    // 运算出 4 -> 攻击/回复 -> 选回复目标）
                    Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                        "选了回复之后还应问「回复给谁」。");
                    Assert.AreEqual(HandEffect.PriestHealTarget, board.PendingRequest.Value.Effect);

                    Assert.True(board.ClickHand(priest.RightHand), "回给自己应被接受。");
                    Assert.True(priest.Health > 10, $"选回复应回血。实际 HP={priest.Health}。");
                }
                else
                {
                    Assert.NotNull(board.LastTurn);
                    Assert.AreEqual(6, board.LastTurn.ResultValue, "5 + 1 = 6");
                    Assert.True(board.LastTurn.DamageDealt > 0, "选攻击应造成伤害。");
                }
            }
        }


        [Test]
        public void The_priests_pre_target_answer_survives_the_mid_turn_reset()
        {
            // ⚠️ 真实 bug 的回归（这条是本次修复的**核心**）。
            //
            // 牧师的 4/6/8 有两段抉择，中间隔着"确认目标玩家"：
            //   ① 选定运算源之后：「攻击 / 回复」   ← 答案存进 sink
            //   ② 确认目标玩家之后：「回复给谁」/ 结算
            //
            // CommitMove -> BeginCharacterChoices -> SetupFirstStep 会在
            // ① 与 ② 之间跑一次。若那里按"运算源为空"判定回合边界，
            // 就会把同回合刚答的 ① 清掉 —— 结算读到 null，
            // 表现为"选了回复却按攻击结算"。
            //
            // 现在只有**行动方真正变化**时才算回合边界，本答案得以存活。
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var choiceP = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, choiceP, choiceB);
            var board = new BoardController(game, choiceP, choiceB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 3);      // 3 + 1 = 4 -> 可当回复 4
            TestSetup.SetHealth(priest, 10);

            board.BeginTurn();
            board.ClickHand(priest.LeftHand);           // ① 自己的手
            board.ClickHand(blank.LeftHand);            // ② 目标手 -> 结果 4 确定
            Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase);

            board.AcceptPendingChoice();                // 选「回复」—— 答案存进 sink

            // 跨过"确认目标玩家"这一段（中途会跑 SetupFirstStep）
            Assert.True(board.ConfirmTargetPlayer(), "确认目标玩家。");

            // 答案必须还在：所以才会问"回复给谁"
            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "前置答案若被清掉，这里就会直接按攻击结算而**不问**回复目标。");
            Assert.AreEqual(HandEffect.PriestHealTarget, board.PendingRequest.Value.Effect);

            board.ClickHand(priest.RightHand);          // 回给自己
            Assert.AreEqual(14, priest.Health,
                "4 当回复用 -> 回 4 点。若前置答案丢失，这里会是 10。");
        }

        [Test]
        public void The_priest_can_heal_after_choosing_heal()
        {
            // 选「回复」-> 选目标手 -> 确认目标 -> 再问"回复给谁" -> 结算
            var priest = CharacterFactory.CreatePlayer("牧师", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Blank);
            var choiceP = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();
            var game = new GameManager(priest, blank, choiceP, choiceB);
            var board = new BoardController(game, choiceP, choiceB);

            TestSetup.SetAllHands(priest, blank, 1);
            TestSetup.SetHand(priest.LeftHand, 3);      // 3 + 1 = 4 -> 回复 4
            TestSetup.SetHealth(priest, 10);
            int blankBefore = blank.Health;

            board.BeginTurn();
            board.ClickHand(priest.LeftHand);           // ① 自己的手
            board.ClickHand(blank.LeftHand);            // ② 目标手（运算源）-> 结果确定
            Assert.AreEqual(BoardPhase.ChoosingPreTargetChoice, board.Phase);

            // 发动 = 当回复用
            Assert.True(board.AcceptPendingChoice(), "选「回复」应被接受。");

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "选完回复后应进入「确认目标玩家」。");

            Assert.True(board.ConfirmTargetPlayer(), "确认目标玩家。");

            // 确认之后才问"回复给谁"
            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "确认目标后才问「回复给谁」。");
            Assert.NotNull(board.PendingRequest);
            Assert.AreEqual(HandEffect.PriestHealTarget, board.PendingRequest.Value.Effect);

            // 点牧师自己的手 = 回复给自己
            Assert.True(board.ClickHand(priest.RightHand), "点手牌表示回复目标。");
            Assert.NotNull(board.LastTurn, "应已结算。");
            Assert.AreEqual(14, priest.Health, "4 当回复用 -> 回 4 点（10 + 4）。");
            Assert.AreEqual(blankBefore, blank.Health, "选回复不造成伤害。");
        }


        [Test]
        public void Clicking_the_target_hand_itself_also_confirms()
        {
            // 玩家用"点手牌"来表达确认（UI 走这条），与 ConfirmTargetPlayer 等价
            ArrangeAttack();
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.LeftHand);      // 进入确认阶段

            Assert.True(_board.ClickHand(_b.RightHand), "点目标玩家的手牌即确认。");
            Assert.NotNull(_board.LastTurn, "应已结算。");
        }

        [Test]
        public void Ui_flow_reaches_the_defense_choice_when_defender_has_two_shields()
        {
            // ⚠️ 这个测试**故意不调用 ConfirmTargetPlayer** —— 它模拟真实 UI：
            // 玩家点目标玩家的手牌来确认。
            //
            // 场景：B 行动、打 A；A 有双盾 -> 确认目标后应停在"选盾"。
            // 直接把回合设给 B，避免绕一圈（A 先手会停在选盾，回合不会结束）。
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_b.LeftHand, 3);      // 3 + 5 = 8 -> 攻击
            TestSetup.SetHand(_a.LeftHand, 5);
            _a.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(_a.RightHand, 5);
            _a.RightHand.SetShieldState(ShieldState.Formed);

            _game.ForceActivePlayerForTest(_b);
            _board.BeginTurn();

            Assert.AreSame(_b, _board.ActivePlayer, "前置条件：轮到 B 行动。");

            _board.ClickHand(_b.LeftHand);          // 选自己的手（运算源）
            _board.ClickHand(_a.LeftHand);          // 选参与运算的对方手

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, _board.Phase,
                "应先停在「确认目标玩家」。");
            Assert.AreSame(_a, _board.TargetPlayer, "目标玩家应是 A。");

            // UI 路径：再点目标玩家的手牌即确认
            Assert.True(_board.ClickHand(_a.RightHand), "点目标玩家的手牌即确认。");
            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, _board.Phase,
                "A 有双盾 -> 应停下来让 A 选盾（新流程不能把这一步弄丢）。");
        }

        [Test]
        public void Turn_advances_to_the_other_player()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.AreSame(_b, _board.ActivePlayer, "回合应交给 B。");
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, _board.Phase);
            Assert.Null(_board.SelectedHand, "新一轮不应带着上次的选择。");
        }

        [Test]
        public void Cannot_click_opponent_hand_during_attacker_selection()
        {
            _board.BeginTurn();

            Assert.False(_board.CanClick(_b.LeftHand), "第一步只能选自己的手。");
            Assert.False(_board.ClickHand(_b.LeftHand));
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, _board.Phase, "阶段不应改变。");
        }

        [Test]
        public void Cannot_click_own_hand_during_target_selection()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.False(_board.CanClick(_a.RightHand), "第二步只能选对方的手。");
            Assert.False(_board.ClickHand(_a.RightHand));
            _board.ConfirmTargetPlayer();
        }

        [Test]
        public void Locked_hand_cannot_be_selected()
        {
            _a.LeftHand.Lock();
            _board.BeginTurn();

            Assert.False(_board.CanClick(_a.LeftHand), "被锁的手不可选。");
            Assert.True(_board.CanClick(_a.RightHand));
            Assert.True(_board.Hint.Contains("左手被锁"), $"提示应说明锁手情况，实际：{_board.Hint}");
        }

        [Test]
        public void CancelSelection_returns_to_the_first_step()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();

            _board.CancelSelection();

            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, _board.Phase);
            Assert.Null(_board.SelectedHand);
        }

        // ============================================================
        // 再点一次已选的手 = 取消选择
        // ============================================================

        [Test]
        public void Clicking_the_selected_hand_again_deselects_it()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, _board.Phase);
            Assert.True(_board.IsSelected(_a.LeftHand));

            // 再点同一只手 -> 取消，而不是把它当成目标
            Assert.True(_board.ClickHand(_a.LeftHand), "再点已选的手应被接受（表示取消）。");
            _board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingAttackerHand, _board.Phase);
            Assert.Null(_board.SelectedHand);
            Assert.Null(_board.LastTurn, "取消不应产生结算。");
            Assert.AreEqual(0, _game.TurnCount, "取消不应推进回合。");
        }

        [Test]
        public void Can_select_a_different_hand_after_deselecting()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_a.LeftHand);              // 取消
            _board.ConfirmTargetPlayer();

            Assert.True(_board.ClickHand(_a.RightHand), "取消后应能改选另一只手。");
            _board.ConfirmTargetPlayer();
            Assert.AreSame(_a.RightHand, _board.SelectedHand);

            Assert.True(_board.ClickHand(_b.LeftHand));
            _board.ConfirmTargetPlayer();
            Assert.NotNull(_board.LastTurn);
            Assert.AreEqual(2, _board.LastTurn.ResultValue, "1 + 1 = 2");
        }

        [Test]
        public void After_deselect_only_own_hands_are_clickable_again()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_a.LeftHand);              // 取消
            _board.ConfirmTargetPlayer();

            // 取消后回到"选自己的手"阶段，因此对方的手暂时不可点
            Assert.False(_board.CanClick(_b.LeftHand));
            Assert.True(_board.CanClick(_a.LeftHand));
            Assert.True(_board.CanClick(_a.RightHand));
        }

        // ============================================================
        // 野蛮人：自己选锁哪只手
        // ============================================================

        /// <summary>
        /// 局面（全部在开局摆好，过程中不改数值）：
        ///   A = 白板，A.L = 1，A.R = 3
        ///   B = 野蛮人，B.L = 1，B.R = 4
        ///
        /// 回合推演（关键：行动方的手会被改写，所以下一步要用**另一只手**当目标）：
        ///   回合1 A：A.R 3 + B.L 1 = 4 -> 攻击 4（A.R 变 4；A.L 仍是 1，B.R 仍是 4）
        ///   回合2 B：B.R 4 + A.L 1 = 5 -> B 合盾，此时应询问"锁 A 哪只手"
        ///   回合3 A：A.R 4 + B.L 1 = 5 -> A 也合盾（A 是白板，不询问）
        ///   回合4 B：B.L 1 + A.R 5 = 6 -> 攻击（B.L 变 6）
        /// </summary>
        private BoardController ArrangeBarbarianAboutToFormShield(out PlayerState barbarian)
        {
            var blank = CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank);
            barbarian = CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian);

            var sink = new SimpleChoiceProvider();
            var game = new GameManager(blank, barbarian, sink, sink);
            var board = new BoardController(game, sink, sink);

            TestSetup.SetAllHands(blank, barbarian, 1);
            TestSetup.SetHand(blank.RightHand, 3);
            TestSetup.SetHand(barbarian.RightHand, 4);

            board.BeginTurn();
            return board;
        }

        /// <summary>把 <see cref="ArrangeBarbarianAboutToFormShield"/> 推进到"野蛮人已合盾、正等玩家选锁哪只手"。</summary>
        private BoardController ArrangeAtLockPrompt(out PlayerState barbarian, out PlayerState blank)
        {
            BoardController board = ArrangeBarbarianAboutToFormShield(out barbarian);
            blank = board.PlayerA;

            // 回合 1（A）：A.R 3 + B.L 1 = 4 -> 攻击（A.L 保持 1、B.R 保持 4）
            board.ClickHand(blank.RightHand);
            board.ClickHand(barbarian.LeftHand);
            board.ConfirmTargetPlayer();
            Assert.AreEqual(HandEffectType.Attack4, board.LastTurn.Effect, "3 + 1 = 4");

            // 回合 2（B）：B.R 4 + A.L 1 = 5 -> 合盾 -> 询问锁手
            Assert.AreSame(barbarian, board.ActivePlayer, "前置条件：应轮到野蛮人。");
            board.ClickHand(barbarian.RightHand);
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingLockTarget, board.Phase,
                "前置条件：野蛮人合盾后应停在锁手询问。");

            return board;
        }

        [Test]
        public void Barbarian_forming_a_shield_prompts_for_lock_target()
        {
            PlayerState barbarian;
            PlayerState blank;
            BoardController board = ArrangeAtLockPrompt(out barbarian, out blank);

            Assert.AreEqual(2, board.ChoiceCandidates.Count, "对手两只手都可选。");
            Assert.True(board.CanClick(blank.LeftHand));
            Assert.True(board.CanClick(blank.RightHand));
            Assert.False(board.CanClick(barbarian.LeftHand), "自己的手不应可点。");
        }

        [Test]
        public void Barbarian_lock_choice_targets_the_chosen_hand()
        {
            PlayerState barbarian;
            PlayerState blank;
            BoardController board = ArrangeAtLockPrompt(out barbarian, out blank);

            // 显式锁右手（默认策略会锁数值最大的，这里验证"玩家说了算"）
            Assert.True(board.ClickHand(blank.RightHand), "锁手目标应可点。");
            board.ConfirmTargetPlayer();

            Assert.True(blank.RightHand.IsLocked, "被选中的右手应被锁定。");
            Assert.False(blank.LeftHand.IsLocked, "另一只手不应被锁。");
            Assert.AreEqual(ShieldState.Formed, barbarian.RightHand.State, "合盾本身应生效。");
        }

        [Test]
        public void Barbarian_lock_can_target_the_left_hand_too()
        {
            PlayerState barbarian;
            PlayerState blank;
            BoardController board = ArrangeAtLockPrompt(out barbarian, out blank);

            Assert.True(board.ClickHand(blank.LeftHand));
            board.ConfirmTargetPlayer();

            Assert.True(blank.LeftHand.IsLocked, "被选中的左手应被锁定。");
            Assert.False(blank.RightHand.IsLocked, "另一只手不应被锁。");
        }

        [Test]
        public void Locked_hand_cannot_be_used_as_source_next_turn()
        {
            PlayerState barbarian;
            PlayerState blank;
            BoardController board = ArrangeAtLockPrompt(out barbarian, out blank);

            board.ClickHand(blank.LeftHand);           // 锁 A 左手
            board.ConfirmTargetPlayer();

            // 现在轮到 A，A 的左手被锁，只有右手可选
            Assert.AreSame(blank, board.ActivePlayer, "回合应交给 A。");
            Assert.False(board.CanClick(blank.LeftHand), "被锁的手不应可点。");
            Assert.True(board.CanClick(blank.RightHand));

            Assert.False(board.ClickHand(blank.LeftHand), "点击被锁的手应被拒绝。");
            board.ConfirmTargetPlayer();
        }

        [Test]
        public void Non_barbarian_forming_a_shield_does_not_prompt_for_lock()
        {
            // 白板 vs 白板：合盾时不应出现锁手询问。
            //   回合1 A：A.L 1 + B.R 4 = 5 -> A 合盾（A 是白板）
            //   回合2 B：B.L 4 + A.R 1 = 5 -> B 也合盾（B 也是白板）
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_b.RightHand, 4);
            TestSetup.SetHand(_b.LeftHand, 4);

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.RightHand);
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(HandEffectType.Shield, _board.LastTurn.Effect, "前置条件：A 应合盾。");

            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_a.RightHand);
            _board.ConfirmTargetPlayer();

            Assert.False(_board.Phase == BoardPhase.ChoosingLockTarget,
                "只有野蛮人才会触发锁手选择。");
            Assert.NotNull(_board.LastTurn);
            Assert.AreEqual(HandEffectType.Shield, _board.LastTurn.Effect);
        }

        [Test]
        public void Lock_prompt_is_skipped_when_already_controlling_a_hand()
        {
            PlayerState barbarian;
            PlayerState blank;
            BoardController board = ArrangeAtLockPrompt(out barbarian, out blank);

            board.ClickHand(blank.LeftHand);           // 锁住 A 的左手
            board.ConfirmTargetPlayer();
            Assert.True(blank.LeftHand.IsLocked);
            Assert.AreEqual(ShieldState.Formed, barbarian.RightHand.State);

            // 回合 3（A）：A.R 4 + B.L 1 = 5 -> A 也合盾
            Assert.AreSame(blank, board.ActivePlayer);
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(barbarian.LeftHand);
            board.ConfirmTargetPlayer();
            Assert.AreEqual(HandEffectType.Shield, board.LastTurn.Effect, "4 + 1 = 5");

            // 回合 4（B）：B.L 1 + A.R 5 = 6 -> 攻击（B.L 变 6）。
            //                B 的右手（已成型）仍在控制中，因此**不应**再问锁手。
            Assert.AreSame(barbarian, board.ActivePlayer);
            board.ClickHand(barbarian.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.False(board.Phase == BoardPhase.ChoosingLockTarget,
                "已经在控制一只手时不应再询问锁手。");
            Assert.True(blank.LeftHand.IsLocked, "原有的锁应保持。");
            Assert.False(blank.RightHand.IsLocked, "不应锁第二只手。");
        }

        // ============================================================
        // 防守方选盾
        // ============================================================

        /// <summary>
        /// 构造局面：A 双手都是盾，轮到 B 出手，且 B 这次运算会打出攻击。
        /// 因此 <see cref="BoardController"/> 必须先问 A 用哪一面盾抵挡。
        ///
        /// 回合推演（每步之后会自动进入下一回合）：
        ///   回合1 A：A.L 4 + B.R 1 = 5  -> A.L 成盾
        ///   回合2 B：B.R 1 + A.R 4 = 5  -> B.R 变成 5（B 也合盾；A.R 不变，仍为 4）
        ///   回合3 A：B.L 1 + A.R 4 = 5  -> A.R 成盾（注意要打 B 的左手，B.R 已经是 5）
        ///
        /// 方法结束时停在"回合4 轮到 B、还没出手"的状态，交由调用方选择怎么打。
        /// </summary>
        private void ArrangeDoubleShield()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 4);
            TestSetup.SetHand(_a.RightHand, 4);

            // 回合 1（A）：A.L 4 + B.R 1 = 5
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ClickHand(_b.RightHand);
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "回合1 后 A.L 应为盾。");

            // 回合 2（B）：B.R 1 + A.R 4 = 5
            _board.ClickHand(_b.RightHand);
            _board.ClickHand(_a.RightHand);
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(ShieldState.Formed, _b.RightHand.State, "回合2 后 B.R 应为盾。");

            // 回合 3（A）：B.L 1 + A.R 4 = 5 -> A.R 成盾
            TestSetup.SetHand(_a.RightHand, 4);
            _board.ClickHand(_a.RightHand);
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.True(_a.HasDoubleShield, "前置条件：A 应有双盾。");
            Assert.AreSame(_b, _board.ActivePlayer, "前置条件：应轮到 B 出手。");
        }

        /// <summary>在 <see cref="ArrangeDoubleShield"/> 基础上，让 B 打出一记会被盾挡下的攻击。</summary>
        private void ArrangeDoubleShieldThenAttack()
        {
            ArrangeDoubleShield();

            // 回合 4（B）：B.L 1 + A.L 5 = 6 -> 攻击 6，打 A 的左手
            //   A 有两面成型盾 -> 流程会停在"选择用哪面盾抵挡"
            _board.ClickHand(_b.LeftHand);
            _board.ClickHand(_a.LeftHand);
            // 新流程：选定目标手之后还要确认"打谁"（挨打的是玩家）
            _board.ConfirmTargetPlayer();
        }

        [Test]
        public void Two_shields_prompts_the_defender_to_choose()
        {
            ArrangeDoubleShieldThenAttack();

            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, _board.Phase,
                "A 有两面盾，应先问用哪一面。");
            Assert.AreEqual(2, _board.DefenseShieldChoices.Count);

            // LastTurn 的语义是"最近一次已结算的回合"，此时应是回合3 的合盾
            Assert.NotNull(_board.LastTurn);
            Assert.AreEqual(HandEffectType.Shield, _board.LastTurn.Effect);
        }

        [Test]
        public void Only_the_defenders_shields_are_clickable_in_choice_phase()
        {
            ArrangeDoubleShieldThenAttack();

            Assert.True(_board.CanClick(_a.LeftHand));
            Assert.True(_board.CanClick(_a.RightHand));
            Assert.False(_board.CanClick(_b.LeftHand), "进攻方的手不应可点。");
        }

        [Test]
        public void Chosen_shield_is_consumed_and_passed_to_the_sink()
        {
            ArrangeDoubleShieldThenAttack();

            Assert.True(_board.ClickHand(_a.RightHand), "选右手盾应被接受。");
            _board.ConfirmTargetPlayer();

            Assert.NotNull(_board.LastTurn);
            Assert.True(_board.LastTurn.WasBlocked, "应被护盾挡住。");
            Assert.AreEqual(ShieldState.Broken, _a.RightHand.State, "被选中的右手盾应破碎。");
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "未被选中的左手盾应保留。");
        }

        // ============================================================
        // 谁来决定用哪面盾 —— 必须是**防守方**，不是攻击方
        // ============================================================

        [Test]
        public void Only_the_defender_can_pick_the_blocking_shield()
        {
            ArrangeDoubleShieldThenAttack();   // 此刻 B 在攻击，A（双盾）是防守方

            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, _board.Phase);

            // 防守方 = A；攻击方 = B
            Assert.AreSame(_b, _board.ActivePlayer, "此时行动方仍是攻击者 B。");
            Assert.AreSame(_a, _board.Defender, "防守方应是 A。");

            // 候选只能是防守方自己的两面盾
            Assert.AreEqual(2, _board.DefenseShieldChoices.Count,
                "候选应恰好是防守方的两面盾。");
            Assert.True(ReferenceEquals(_board.DefenseShieldChoices[0], _a.LeftHand));
            Assert.True(ReferenceEquals(_board.DefenseShieldChoices[1], _a.RightHand));

            // 攻击方 B 的手一律不可点 —— B 无权选择打碎哪面盾
            Assert.False(_board.CanClick(_b.LeftHand), "攻击方不能参与选盾。");
            Assert.False(_board.CanClick(_b.RightHand), "攻击方不能参与选盾。");

            // 提示文字应明确是防守方在选
            Assert.True(_board.Hint.Contains("轮到") && _board.Hint.Contains(_a.Name),
                $"提示应指明由防守方 {_a.Name} 选择，实际：{_board.Hint}");
        }

        [Test]
        public void Attacker_clicking_its_own_hand_is_ignored_during_shield_choice()
        {
            ArrangeDoubleShieldThenAttack();

            // 攻击方 B 试图"指定"打碎哪面盾 —— 它的手在选盾阶段一律不可点，
            // 点击必须被拒绝，且不产生任何结算。
            Assert.False(_board.CanClick(_b.LeftHand), "攻击方的手不可点。");
            Assert.False(_board.CanClick(_b.RightHand), "攻击方的手不可点。");
            Assert.False(_board.ClickHand(_b.LeftHand));
            _board.ConfirmTargetPlayer();
            Assert.False(_board.ClickHand(_b.RightHand));
            _board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, _board.Phase,
                "非法点击后应仍停在选盾阶段。");
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "不应有任何盾破碎。");
            Assert.AreEqual(ShieldState.Formed, _a.RightHand.State, "不应有任何盾破碎。");

            // LastTurn 仍是"最近一次已结算的回合"（上一回合的合盾），
            // 说明这次攻击**没有被结算** —— 回合计数也没动。
            Assert.AreEqual(HandEffectType.Shield, _board.LastTurn.Effect,
                "不应产生新的攻击结算。");
        }

        [Test]
        public void Only_the_defenders_chosen_shield_breaks()
        {
            // 用"结果状态"来证明选择权确实在防守方：先选左手，左手碎；重开一局再选右手，右手碎。
            ArrangeDoubleShieldThenAttack();
            Assert.True(_board.ClickHand(_a.LeftHand));
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(ShieldState.Broken, _a.LeftHand.State, "防守方选了左手，左手应碎。");
            Assert.AreEqual(ShieldState.Formed, _a.RightHand.State, "右手应完好。");

            // 重新来一局，这次防守方选右手
            SetUp();
            ArrangeDoubleShieldThenAttack();
            Assert.True(_board.ClickHand(_a.RightHand));
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "左手应完好。");
            Assert.AreEqual(ShieldState.Broken, _a.RightHand.State, "防守方选了右手，右手应碎。");
        }

        [Test]
        public void Curseweaver_ignores_shields_so_the_choice_is_harmless()
        {
            // 咒术师无视护盾：即使先问了"用哪面盾"，护盾也不会被消费。
            var curseweaver = CharacterFactory.CreatePlayer("A", CharacterDefinition.Curseweaver);
            var blank = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var ai = new SimpleChoiceProvider();
            var game = new GameManager(curseweaver, blank, ai, new SimpleChoiceProvider());
            var board = new BoardController(game, ai, ai);

            TestSetup.SetAllHands(curseweaver, blank, 1);
            TestSetup.SetHand(blank.LeftHand, 5);
            blank.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(blank.RightHand, 5);
            blank.RightHand.SetShieldState(ShieldState.Formed);
            Assert.True(blank.HasDoubleShield);

            TestSetup.SetHand(curseweaver.LeftHand, 3);   // 3 + 5 = 8 -> 攻击 8
            board.BeginTurn();
            board.ClickHand(curseweaver.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, board.Phase,
                "双盾时仍会询问（无害的多问一次）。");

            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.NotNull(board.LastTurn);
            Assert.False(board.LastTurn.WasBlocked, "咒术师应无视护盾。");
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State, "护盾不应被消费。");
            Assert.AreEqual(ShieldState.Formed, blank.RightHand.State);
        }

        [Test]
        public void Single_shield_does_not_prompt()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 4);

            // 回合 1（A）：4 + 1 = 5 -> 单盾
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.RightHand);
            _board.ConfirmTargetPlayer();

            // 回合 2（B）：B.L 1 打 A 的左手 5 -> 1+5 = 6 -> 攻击 6，被单盾挡下
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.False(_board.Phase == BoardPhase.ChoosingDefenseShield,
                "只有一面盾时不应打断流程。");
            Assert.NotNull(_board.LastTurn);
            Assert.True(_board.LastTurn.WasBlocked, "单盾也应挡下攻击。");
        }

        // ============================================================
        // "现在该谁做决定" —— 曾经失误的守卫
        //
        // UI 曾经用"行动方是不是人类"来放行点击。但进入选盾阶段时行动方是**攻击方**，
        // 该做决定的却是**防守方**。于是"AI 攻击 / 人类防守"时，玩家的选盾点击被
        // 静默丢弃，表现为"双盾被直接破盾、没有询问"。
        //
        // 现在统一走 CanAct()（= IsHumanChooser && CanClick），并且可测。
        // ============================================================

        /// <summary>把 AI 玩家的抉择转发给它的抉择器（模拟 UI 注入的解析器）。</summary>
        private sealed class FakeAiResolver : Assistant.IAiChoiceResolver
        {
            public FakeAiResolver() { }

            public Hand ChooseShieldToBlock(PlayerState defender, IReadOnlyList<Hand> candidates)
                => candidates != null && candidates.Count > 0 ? candidates[0] : null;
        }

        [Test]
        public void Human_defender_can_act_while_ai_is_attacking()
        {
            // 局面：人类持双盾，AI 先手打人类左手（3+5=8 -> 攻击）。
            // 注意 PlayerA 先手，所以把 **AI 放在 PlayerA**，让它先动。
            //
            // 关键：抉择器必须**在建 GameManager 时就传进去** —— GameManager 内部保存的是
            // 自己那份 ChoiceProvider 引用；只把 sink 交给 BoardController 是不够的。
            var aiChoice = new SimpleChoiceProvider();
            var humanChoice = new SimpleChoiceProvider();

            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank, aiChoice, humanChoice);
            PlayerState ai = game.PlayerA;
            PlayerState human = game.PlayerB;
            ai.Rename("AI");
            human.Rename("人类");

            var board = new BoardController(game, aiChoice, humanChoice,
                                            aiForA: new GreedyChoiceProvider());
            board.AiResolver = new FakeAiResolver();

            TestSetup.SetAllHands(human, ai, 1);
            TestSetup.SetHand(human.LeftHand, 5);
            human.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(human.RightHand, 5);
            human.RightHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(ai.LeftHand, 3);      // 3 + 5 = 8 -> 攻击 8

            board.BeginTurn();
            Assert.AreSame(ai, board.ActivePlayer, "前置条件：应轮到 AI(B) 行动。");

            // AI 出招：它会挑一个目标，并自己决定用哪面盾抵挡（AI 作为防守方时）
            board.MakeAutomaticMove();

            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, board.Phase,
                "AI 攻击人类双盾，应停在选盾阶段。");

            // 行动方是 AI，但做决定的是人类（防守方）—— 必须放行
            Assert.False(board.IsHumanTurn, "行动方是 AI。");
            Assert.True(board.IsHumanChooser, "但该做决定的是人类防守方。");
            Assert.AreSame(human, board.Defender, "防守方应是人类。");

            Assert.True(board.CanAct(human.LeftHand), "人类应能点自己的盾。");
            Assert.True(board.CanAct(human.RightHand), "人类应能点自己的盾。");

            // 攻击方（AI）的手一律不可点
            Assert.False(board.CanAct(ai.LeftHand));
            Assert.False(board.CanAct(ai.RightHand));

            // 真点一下：选右手盾 -> 右手碎、左手完好
            // （这条正是原来的 bug：AI 攻击时人类的点击会被 IsHumanTurn 守卫丢弃）
            bool accepted = board.ClickHand(human.RightHand);
            board.ConfirmTargetPlayer();
            Assert.True(accepted, "人类的选盾点击必须被接受。");
            Assert.AreEqual(ShieldState.Broken, human.RightHand.State, "人类选的右手盾应碎。");
            Assert.AreEqual(ShieldState.Formed, human.LeftHand.State, "左手应完好。");
        }

        [Test]
        public void Ai_defender_does_not_interrupt_the_human_attacker()
        {
            // 反面：人类攻击、AI 防守双盾 -> 应自动决定，不打断人类。
            // 抉择器必须在建 GameManager 时就传进去（GameManager 保存自己那份引用）
            var humanChoice = new SimpleChoiceProvider();
            var aiChoice = new SimpleChoiceProvider();

            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank, humanChoice, aiChoice);
            PlayerState human = game.PlayerA;
            PlayerState ai = game.PlayerB;

            var board = new BoardController(game, humanChoice, aiChoice,
                                            aiForB: new GreedyChoiceProvider());
            board.AiResolver = new FakeAiResolver();

            TestSetup.SetAllHands(human, ai, 1);
            TestSetup.SetHand(ai.LeftHand, 5);
            ai.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(ai.RightHand, 5);
            ai.RightHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(human.LeftHand, 3);      // 3 + 5 = 8 -> 攻击

            board.BeginTurn();
            Assert.True(board.IsHumanChooser);

            board.ClickHand(human.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ai.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.False(board.Phase == BoardPhase.ChoosingDefenseShield,
                "防守方是 AI -> 应自动决定，不打断人类。");
            Assert.NotNull(board.LastTurn);
            Assert.True(board.LastTurn.WasBlocked, "AI 的盾应挡下这次攻击。");
        }

        // ============================================================
        // 跳过回合（仅限双盾在手）
        // ============================================================

        /// <summary>把某个玩家摆成双盾。</summary>
        private void GiveDoubleShield(PlayerState p)
        {
            TestSetup.SetHand(p.LeftHand, 5);
            p.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(p.RightHand, 5);
            p.RightHand.SetShieldState(ShieldState.Formed);
        }

        [Test]
        public void Cannot_skip_turn_without_double_shield()
        {
            _board.BeginTurn();
            Assert.False(_board.CanSkipTurn, "没有双盾时不应允许跳过。");
            Assert.False(_board.SkipTurn(), "跳过应被拒绝。");
            Assert.AreEqual(0, _game.TurnCount, "被拒绝的跳过不应推进回合。");
        }

        [Test]
        public void Cannot_skip_turn_with_only_one_shield()
        {
            TestSetup.SetHand(_a.LeftHand, 5);
            _a.LeftHand.SetShieldState(ShieldState.Formed);

            _board.BeginTurn();
            Assert.False(_board.CanSkipTurn, "只有一面盾时不应允许跳过。");
        }

        [Test]
        public void Can_skip_turn_with_double_shield()
        {
            GiveDoubleShield(_a);
            _board.BeginTurn();

            Assert.True(_board.CanSkipTurn, "双盾在手时应允许跳过。");
            Assert.True(_board.SkipTurn(), "跳过应成功。");

            Assert.AreEqual(1, _game.TurnCount, "跳过应消耗一个回合。");
            Assert.AreSame(_b, _game.CurrentPlayer, "回合应交给对方。");
            Assert.AreSame(_b, _board.ActivePlayer);
        }

        [Test]
        public void Skipping_does_not_change_any_hand_or_health()
        {
            GiveDoubleShield(_a);
            TestSetup.SetHand(_b.LeftHand, 3);
            _board.BeginTurn();

            int hpA = _a.Health, hpB = _b.Health;
            _board.SkipTurn();

            Assert.AreEqual(hpA, _a.Health, "跳过不应改变生命。");
            Assert.AreEqual(hpB, _b.Health, "跳过不应改变生命。");
            Assert.AreEqual(5, _a.LeftHand.Value, "跳过不应改变手值。");
            Assert.AreEqual(5, _a.RightHand.Value, "跳过不应改变手值。");
            Assert.AreEqual(3, _b.LeftHand.Value, "跳过不应改变对方手值。");
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "双盾应完好保留。");
            Assert.AreEqual(ShieldState.Formed, _a.RightHand.State, "双盾应完好保留。");
        }

        [Test]
        public void Skipping_is_logged()
        {
            GiveDoubleShield(_a);
            _board.BeginTurn();
            _board.SkipTurn();

            Assert.True(_board.Log.Count > 0);
            Assert.True(_board.Log[_board.Log.Count - 1].Contains("跳过"),
                $"日志应记录跳过，实际：{_board.Log[_board.Log.Count - 1]}");
        }

        [Test]
        public void Cannot_skip_twice_in_a_row()
        {
            GiveDoubleShield(_a);
            _board.BeginTurn();
            Assert.True(_board.SkipTurn());

            // 现在轮到 B（没有双盾）-> 不能再跳
            Assert.False(_board.CanSkipTurn, "B 没有双盾，不应能跳过。");
        }

        [Test]
        public void Cannot_skip_during_shield_choice_phase()
        {
            // 选盾阶段不允许跳过（那时行动方已经把运算交出去了）
            ArrangeDoubleShieldThenAttack();
            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, _board.Phase);
            Assert.False(_board.CanSkipTurn, "选盾阶段不应允许跳过。");
        }

        [Test]
        public void GameManager_skip_turn_validates_the_rule()
        {
            // Core 层的守卫：不满足条件时抛异常，不静默失败
            Assert.Throws<System.InvalidOperationException>(() => _game.SkipTurn());
        }

        // ============================================================
        // AI
        // ============================================================

        [Test]
        public void Automatic_move_is_rejected_for_human_players()
        {
            _board.BeginTurn();
            Assert.False(_board.MakeAutomaticMove(), "没有配置 AI 时不应自动出手。");
        }

        [Test]
        public void Ai_plays_a_full_match_without_human_input()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Warrior, CharacterDefinition.Barbarian);
            var ca = new SimpleChoiceProvider();
            var cb = new SimpleChoiceProvider();
            var board = new BoardController(game, ca, cb,
                                            aiForA: new GreedyChoiceProvider(),
                                            aiForB: new GreedyChoiceProvider());

            const int safety = 2000;
            int steps = 0;
            board.BeginTurn();

            while (board.Phase != BoardPhase.GameOver && steps < safety)
            {
                Assert.True(board.IsHumanTurn == false, "双方都配了 AI，不应等待人类。");
                Assert.True(board.MakeAutomaticMove(), "AI 应能走一步。");
                steps++;
            }

            Assert.True(steps < safety, $"AI 对局未在 {safety} 步内结束。");
            Assert.True(game.IsGameOver);
            Assert.AreEqual(BoardPhase.GameOver, board.Phase);
            Assert.NotNull(board.Hint);
        }

        [Test]
        public void Human_then_ai_alternates_correctly()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Blank, CharacterDefinition.Blank);
            var ca = new SimpleChoiceProvider();
            var cb = new SimpleChoiceProvider();
            var board = new BoardController(game, ca, cb,
                                            aiForB: new GreedyChoiceProvider());

            board.BeginTurn();
            Assert.True(board.IsHumanTurn, "A 是人类。");

            board.ClickHand(game.PlayerA.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(game.PlayerB.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.False(board.IsHumanTurn, "回合已交给 AI 控制的 B。");
            Assert.True(board.MakeAutomaticMove(), "AI 应能出手。");
            Assert.True(board.IsHumanTurn, "回合应回到人类玩家 A。");
        }

        // ============================================================
        // 结束与日志
        // ============================================================

        [Test]
        public void Game_over_blocks_further_clicks()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 3);        // 3 + 1 = 4 -> 攻击 4
            TestSetup.SetHealth(_b, 4);

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.GameOver, _board.Phase);
            Assert.True(_game.IsGameOver);
            Assert.False(_board.CanClick(_a.RightHand), "结束后不应还能点击。");
            Assert.False(_board.ClickHand(_a.RightHand));
            _board.ConfirmTargetPlayer();
            Assert.True(_board.Hint.Contains("获胜"), $"提示应宣布胜者，实际：{_board.Hint}");
        }

        [Test]
        public void Every_resolved_turn_is_appended_to_the_log()
        {
            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.AreEqual(1, _board.Log.Count);
            Assert.True(_board.Log[0].Contains("1 + 1 = 2"), $"日志内容：{_board.Log[0]}");
        }

        [Test]
        public void Describe_turn_mentions_shield_and_blocking()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 4);

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.RightHand);
            _board.ConfirmTargetPlayer();

            Assert.True(_board.Log[0].Contains("护盾"), $"应提到护盾：{_board.Log[0]}");
        }

        [Test]
        public void Describe_turn_mentions_wasted_heal_at_full_health()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 8);         // 8 + 1 = 9 -> 满血回复

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.AreEqual(HandEffectType.Heal, _board.LastTurn.Effect);
            Assert.True(_board.Log[0].Contains("浪费"), $"应说明回合被浪费：{_board.Log[0]}");
        }

        [Test]
        public void Begin_turn_after_game_over_reports_game_over()
        {
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHealth(_b, 1);
            TestSetup.SetHand(_a.LeftHand, 3);

            _board.BeginTurn();
            _board.ClickHand(_a.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_b.LeftHand);
            _board.ConfirmTargetPlayer();
            Assert.AreEqual(BoardPhase.GameOver, _board.Phase);

            _board.BeginTurn();                        // 再调一次不应崩溃
            Assert.AreEqual(BoardPhase.GameOver, _board.Phase);
        }

        [Test]
        public void Constructor_rejects_null_game()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => new BoardController(null, new SimpleChoiceProvider(), new SimpleChoiceProvider()));
        }
    }
}
