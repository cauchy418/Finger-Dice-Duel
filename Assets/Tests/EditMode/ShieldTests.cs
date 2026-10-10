using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 护盾机制：合盾、抵挡一次攻击、破碎后的状态、双盾选择、运算前主动破盾。
    /// 对应 RULES.md 第 4 节。
    ///
    /// 约定：
    ///   · 用手值摆局面的操作（<see cref="TestSetup"/>）必须在 <see cref="GameManager"/>
    ///     开始跑之前完成，否则会破坏"只有行动方的手改变"的契约，测试就失去意义。
    ///   · 每个测试开始时 <see cref="SetUp"/> 会把双方的 4 只手统一归位到 1，
    ///     因此各测试之间的局面互不影响。
    /// </summary>
    [TestFixture]
    public class ShieldTests
    {
        private PlayerState _a;
        private PlayerState _b;
        private ScriptedChoiceProvider _choiceA;
        private ScriptedChoiceProvider _choiceB;
        private GameManager _game;

        [SetUp]
        public void SetUp()
        {
            _a = new PlayerState("A");
            _b = new PlayerState("B");
            _choiceA = new ScriptedChoiceProvider();
            _choiceB = new ScriptedChoiceProvider();
            _game = new GameManager(_a, _b, _choiceA, _choiceB);

            // 统一归位，避免依赖 PlayerState 的默认值
            TestSetup.SetAllHands(_a, _b, 1);
        }

        /// <summary>让 A 用左手打出第一回合，合出一面盾（4+1=5）。</summary>
        private void FirstTurnFormsAShield()
        {
            TestSetup.SetHand(_a.LeftHand, 4);
            _game.ExecuteTurn(_a, _a.LeftHand, _b.RightHand);   // 4 + 1 = 5
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "前置条件：A 左手应为盾。");
            Assert.AreEqual(5, _a.LeftHand.Value);
        }

        /// <summary>局面：A 左手 4、A 右手 4。跑三个回合后 A 双手都有盾。</summary>
        private void ArrangeDoubleShieldBeforeStart()
        {
            TestSetup.SetHand(_a.LeftHand, 4);
            TestSetup.SetHand(_a.RightHand, 4);

            _game.ExecuteTurn(_a, _a.LeftHand, _b.RightHand);   // A.L: 4+1 = 5 -> 盾
            _game.ExecuteTurn(_b, _b.LeftHand, _a.RightHand);   // B.L: 1+4 = 5（B 也合盾，不影响断言）
            _game.ExecuteTurn(_a, _a.RightHand, _b.RightHand);  // A.R: 4+1 = 5 -> 盾

            Assert.True(_a.HasDoubleShield, "前置条件：A 应处于双盾状态。");
        }

        [Test]
        public void TestSetup_produces_every_value_in_range()
        {
            for (int v = Hand.MinValue; v <= Hand.MaxValue; v++)
            {
                var hand = new Hand(1);
                TestSetup.SetHand(hand, v);
                Assert.AreEqual(v, hand.Value, $"TestSetup 未能得到 {v}");
                Assert.AreEqual(ShieldState.None, hand.State, $"摆数值后不应残留护盾状态（{v}）");
            }
        }

        [Test]
        public void Forming_a_shield_sets_the_state()
        {
            FirstTurnFormsAShield();

            Assert.True(_a.HasAnyShield);
            Assert.False(_a.HasDoubleShield);
        }

        [Test]
        public void Shield_blocks_one_attack_completely_and_breaks()
        {
            TestSetup.SetHealth(_a, 2);                          // 让"是否挡住"一目了然
            FirstTurnFormsAShield();

            TestSetup.SetHand(_b.LeftHand, 3);                   // 3+1 = 4 -> 攻击
            TurnResult r = _game.ExecuteTurn(_b, _b.LeftHand, _a.RightHand);

            Assert.AreEqual(HandEffectType.Attack4, r.Effect);
            Assert.True(r.WasBlocked, "A 有盾，本次攻击应被抵挡。");
            Assert.AreEqual(0, r.DamageDealt, "被抵挡时不应造成伤害。");
            Assert.AreEqual(2, _a.Health, "护盾应完全免疫这次伤害（2 血仍是 2 血）。");
            Assert.AreEqual(ShieldState.Broken, _a.LeftHand.State, "抵挡后护盾应破碎。");
            Assert.False(_a.HasAnyShield);
        }

        [Test]
        public void Broken_shield_does_not_block_again()
        {
            // 直接构造"场上只有一面碎盾"的局面：
            //   A 的左手是 5、状态 Broken —— 它看起来像盾（数值 5）但已经碎了。
            //   B 的右手 1 打上来：1 + 5 = 6（攻击 6），必须真的掉血，
            //   并且引擎不能把这面碎盾当成可抵挡的盾（见 AttackContext 的 Formed 校验）。
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 5);
            _a.LeftHand.SetShieldState(ShieldState.Broken);
            TestSetup.SetHand(_b.RightHand, 1);

            Assert.False(_a.HasAnyShield, "前置条件：碎盾不算有盾。");

            TurnResult hit = _game.ExecuteTurn(_b, _b.RightHand, _a.LeftHand);

            Assert.AreEqual(HandEffectType.Attack6, hit.Effect, "1 + 5 = 6");
            Assert.False(hit.WasBlocked, "已破碎的盾不应再次抵挡。");
            Assert.AreEqual(6, hit.DamageDealt);
            Assert.AreEqual(14, _a.Health);
        }

        [Test]
        public void Broken_shield_can_still_receive_a_new_shield_state()
        {
            // 碎盾的数值仍然是普通数值，参与运算时按普通手处理
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 5);
            _a.LeftHand.SetShieldState(ShieldState.Broken);

            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.AreEqual(6, r.ResultValue, "5 + 1 = 6");
            Assert.False(r.ShieldBrokenBeforeOp, "碎盾不是成型盾，不应触发'运算前破盾'。");
            Assert.AreEqual(ShieldState.None, _a.LeftHand.State, "结果不是 5，状态回到 None。");
        }

        [Test]
        public void With_two_shields_the_provider_chooses_which_one_breaks()
        {
            ArrangeDoubleShieldBeforeStart();

            _choiceA.BlockWith(_a.RightHand);                    // 指定消耗右手盾

            TestSetup.SetHand(_b.LeftHand, 3);                   // 3+5 = 8 -> 攻击 8
            TurnResult r = _game.ExecuteTurn(_b, _b.LeftHand, _a.LeftHand);

            Assert.AreEqual(HandEffectType.Attack8, r.Effect);
            Assert.True(r.WasBlocked);
            Assert.AreEqual(ShieldState.Formed, _a.LeftHand.State, "左手不应被消耗。");
            Assert.AreEqual(ShieldState.Broken, _a.RightHand.State, "被选中的右手盾应破碎。");
        }

        // 常量：array[0] 在 SetUp 里对应"玩家 B"，array[1] 对应"玩家 A"
        private const int DefenderIndex = 0;
        private const int AttackerIndex = 1;

        /// <summary>
        /// 回归测试：**玩家 B 当防守方**时，选盾结果也必须生效。
        ///
        /// 曾经的 bug：`GameBootstrap` 只把 A 的抉择器当作 sink 传给 BoardController，
        /// 于是 B 当防守方时，选盾结果写进了 A 的抉择器，而 GameManager 问的是 B 的抉择器
        /// → 拿不到选择 → 回退成"第一面盾"，表现为"双盾被攻击时直接破盾、没有询问"。
        ///
        /// 这里同时钉两件事：
        ///   ① 流程会停下来让防守方选；
        ///   ② 防守方选哪面，就碎哪面。
        /// </summary>
        [Test]
        public void Defender_B_can_choose_which_shield_blocks()
        {
            // 构造 B 双盾且轮到 A 出手
            //   回合1（B）：B.L 4 + A.R 1 = 5 -> B.L 成盾
            //   回合2（A）：A.R 1 + B.R 4 = 5 -> A.R 成盾…… 会干扰，改用 A.L
            // 为清晰起见，直接按需摆局：
            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 3);       // A 的运算源，3+5 = 8 -> 攻击 8
            TestSetup.SetHand(_b.LeftHand, 5);
            _b.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(_b.RightHand, 5);
            _b.RightHand.SetShieldState(ShieldState.Formed);
            Assert.True(_b.HasDoubleShield, "前置条件：B 应有双盾。");

            // 让 B 的抉择器把这次选盾录下来（真实 UI 里就是玩家点哪面盾）
            _choiceB.BlockWith(_b.RightHand);

            // A 攻击 B 的左手（3 + 5 = 8）
            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.AreEqual(HandEffectType.Attack8, r.Effect);
            Assert.True(r.WasBlocked, "B 的盾应挡下这次攻击。");
            Assert.AreEqual(ShieldState.Formed, _b.LeftHand.State,
                "B 选的是右手盾，左手应完好。");
            Assert.AreEqual(ShieldState.Broken, _b.RightHand.State,
                "**B 是防守方，B 选的右手盾必须生效**（曾经会错误地碎左手）。");
        }

        [Test]
        public void Defender_B_shield_choice_is_prompted_by_the_controller()
        {
            // 用 BoardController 走一遍：确认流程会**停下来**让 B 选盾，
            // 而不是直接结算破盾。
            // 注意：sink 必须是实现了 IPlayerChoiceSink 的抉择器（SimpleChoiceProvider）。
            var sinkA = new Assistant.SimpleChoiceProvider();
            var sinkB = new Assistant.SimpleChoiceProvider();
            var game = new GameManager(_a, _b, sinkA, sinkB);
            var board = new Assistant.BoardController(game, sinkA, sinkB);

            TestSetup.SetAllHands(_a, _b, 1);
            TestSetup.SetHand(_a.LeftHand, 3);
            TestSetup.SetHand(_b.LeftHand, 5);
            _b.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(_b.RightHand, 5);
            _b.RightHand.SetShieldState(ShieldState.Formed);

            board.BeginTurn();
            Assert.True(board.ClickHand(_a.LeftHand), "A 选自己的手。");
            board.ConfirmTargetPlayer();
            Assert.True(board.ClickHand(_b.LeftHand), "A 选目标。");
            board.ConfirmTargetPlayer();

            Assert.AreEqual(Assistant.BoardPhase.ChoosingDefenseShield, board.Phase,
                "B 有双盾且这次会打出攻击 -> 应先让 B 选盾。");
            Assert.AreSame(_b, board.Defender, "防守方应是 B。");
            Assert.False(board.CanClick(_a.LeftHand), "攻击方 A 的手不应可点。");

            Assert.True(board.ClickHand(_b.RightHand), "B 选右手盾。");
            board.ConfirmTargetPlayer();
            Assert.AreEqual(ShieldState.Broken, _b.RightHand.State, "B 选的右手盾应碎。");
            Assert.AreEqual(ShieldState.Formed, _b.LeftHand.State, "左手应完好。");
        }

        [Test]
        public void Invalid_shield_choice_falls_back_to_the_first_shield()
        {
            ArrangeDoubleShieldBeforeStart();

            _choiceA.BlockInvalid();                             // 上层返回非法 Hand

            TestSetup.SetHand(_b.LeftHand, 3);
            TurnResult r = _game.ExecuteTurn(_b, _b.LeftHand, _a.LeftHand);

            Assert.True(r.WasBlocked, "非法选择不应导致护盾静默失效。");
            Assert.AreEqual(ShieldState.Broken, _a.LeftHand.State, "应回退为消耗第一面盾（左手）。");
            Assert.AreEqual(ShieldState.Formed, _a.RightHand.State);
        }

        [Test]
        public void Using_a_shield_hand_as_the_source_breaks_it_before_the_operation()
        {
            FirstTurnFormsAShield();

            // A 用自己已成盾的左手发起运算（5 + 1 = 6）
            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.True(r.ShieldBrokenBeforeOp, "作为运算源的盾应在运算前先破碎。");
            Assert.AreEqual(6, r.ResultValue, "5 + 1 = 6");
            Assert.AreEqual(HandEffectType.Attack6, r.Effect);
            // 结果不是 5，所以这只手就是普通手牌，不该再挂"碎盾"标记。
            // （只有结果仍然为 5 时才保留 Broken，表示"这面盾碎了但仍可被战士修复"。）
            Assert.AreEqual(ShieldState.None, _a.LeftHand.State,
                "5+1=6：结果不是 5，应回到普通状态。");
            Assert.False(_a.LeftHand.IsShield);
        }

        [Test]
        public void Repaired_broken_shield_can_block_again()
        {
            FirstTurnFormsAShield();

            // 用这面盾发起运算：5 + 1 = 6，结果不是 5 -> 变成普通手牌
            _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);
            Assert.AreEqual(ShieldState.None, _a.LeftHand.State);

            // 模拟"被战士修复"（真实修复由 WarriorLogic.OnShieldFormed 触发）
            _a.LeftHand.SetShieldState(ShieldState.Formed);
            Assert.True(_a.LeftHand.IsShield);

            TestSetup.SetHand(_b.LeftHand, 3);
            TurnResult r = _game.ExecuteTurn(_b, _b.LeftHand, _a.RightHand); // 3+1 = 4

            Assert.True(r.WasBlocked, "被修复为 Formed 的盾应能再次抵挡。");
            Assert.AreEqual(0, r.DamageDealt, "护盾应完全免疫这次伤害。");
        }

        [Test]
        public void No_shield_means_full_damage()
        {
            TestSetup.SetHand(_a.LeftHand, 3);                   // 3+1 = 4
            TurnResult r = _game.ExecuteTurn(_a, _a.LeftHand, _b.LeftHand);

            Assert.False(r.WasBlocked);
            Assert.AreEqual(4, r.DamageDealt);
            Assert.AreEqual(16, _b.Health);
        }

        [Test]
        public void Shield_formed_by_operation_is_ready_for_the_next_turn()
        {
            FirstTurnFormsAShield();

            TestSetup.SetHand(_b.LeftHand, 3);
            TurnResult r = _game.ExecuteTurn(_b, _b.LeftHand, _a.RightHand); // 3+1 = 4

            Assert.True(r.WasBlocked, "上一回合合成的盾在本回合应可用。");
        }
    }
}
