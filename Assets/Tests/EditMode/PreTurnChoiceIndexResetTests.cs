using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 回归：**接受过一个抉择之后，下个回合的抉择会被静默跳过**。
    ///
    /// 玩家实测的现象：游荡者·风流剑客第五步（左手 5 + 对面 1 = 6，右手是 2）
    /// 应该弹强化，但**提示条里连文字都没有**。
    ///
    /// 根因（由实机诊断日志 `idx=1/0` 定位）：
    ///   `SetupFirstStep()` 每个新回合只清了 `_shieldChoices`，
    ///   **没有复位 `_pendingRequestIndex`**。
    ///   上个回合接受过一次抉择（例如闪避）后 idx 变成 1；
    ///   下个回合 `BeginCharacterChoices` 清空并重建队列（长度 1），
    ///   而 idx 仍是 1 → `1 >= 1` 被判成"全部问完" → **直接结算，不弹提示**。
    ///
    /// 这组测试的价值：它复现的是**跨回合的状态残留**，
    /// 而之前所有单回合测试都是"新建 board、只问一次"，天然看不到这个问题。
    /// </summary>
    [TestFixture]
    public class PreTurnChoiceIndexResetTests
    {
        private GameManager _game;
        private BoardController _board;
        private PlayerState _ranger;      // A：游荡者·风流剑客（人类）
        private PlayerState _foe;         // B：白板（人类，便于手动驱动）

        private void Arrange()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            _game = new GameManager(
                CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler),
                CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                choiceA, choiceB);

            _ranger = _game.PlayerA;
            _foe = _game.PlayerB;

            // 两边都是人类：可以手动驱动，不掺 AI
            _board = new BoardController(_game, choiceA, choiceB);
        }

        /// <summary>
        /// 核心回归：先在第 1 回合**接受**一个抉择（让 idx 前进），
        /// 再在第 2 回合验证强化提示仍然会出现。
        /// </summary>
        [Test]
        public void After_accepting_a_choice_the_next_turn_still_asks_its_own_choice()
        {
            Arrange();

            // ================= 第 1 回合：游荡者·风流剑客被攻击 -> 接受闪避 =================
            // 白板先手（A 是游荡者·风流剑客，这里改成让 B 攻击，所以先让 A 走一步）
            TestSetup.SetHand(_ranger.LeftHand, 5);
            TestSetup.SetHand(_ranger.RightHand, 1);     // 会被翻滚用掉
            TestSetup.SetHand(_foe.LeftHand, 4);
            TestSetup.SetHand(_foe.RightHand, 1);

            _board.BeginTurn();                          // A 的回合
            Assert.True(ReferenceEquals(_board.ActivePlayer, _ranger));

            // A 随便走一步让出回合（用右手 1 打对手右手 1 -> 2，无效果）
            _board.ClickHand(_ranger.RightHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_foe.RightHand);
            _board.ConfirmTargetPlayer();
            while (_board.Phase == BoardPhase.ChoosingPreTurnChoice)
                if (!_board.DeclinePendingChoice()) break;

            // 现在轮到 B（白板），让它攻击游荡者·风流剑客 -> 应问游荡者·风流剑客翻滚
            TestSetup.SetHand(_ranger.LeftHand, 5);
            TestSetup.SetHand(_ranger.RightHand, 1);
            TestSetup.SetHand(_foe.LeftHand, 3);         // 3 + 5 = 8 -> 攻击
            TestSetup.SetHand(_foe.RightHand, 1);

            _board.BeginTurn();
            Assert.True(ReferenceEquals(_board.ActivePlayer, _foe), "应轮到白板");

            _board.ClickHand(_foe.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_ranger.LeftHand);
            _board.ConfirmTargetPlayer();

            Assert.AreEqual(HandEffect.SwashbucklerDodge, _board.PendingRequest?.Effect,
                "白板攻击游荡者·风流剑客 -> 应问翻滚");

            // **接受**（这一步会让 _pendingRequestIndex 前进到 1）
            Assert.True(_board.AcceptPendingChoice(), "接受翻滚应成功");
            Assert.AreEqual(2, _ranger.RightHand.Value, "翻滚把 1 变成 2");

            // ================= 第 2 回合：游荡者·风流剑客攻击 -> 必须弹强化 =================
            TestSetup.SetHand(_ranger.LeftHand, 5);
            TestSetup.SetHand(_ranger.RightHand, 2);     // 翻滚留下的 2
            TestSetup.SetHand(_foe.LeftHand, 4);
            TestSetup.SetHand(_foe.RightHand, 1);

            _board.BeginTurn();

            Assert.True(ReferenceEquals(_board.ActivePlayer, _ranger), "应轮到游荡者·风流剑客");

            // 左手 5 打对面 1 -> 6（攻击）
            _board.ClickHand(_ranger.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_foe.RightHand);
            _board.ConfirmTargetPlayer();

            string scene =
                $"phase={_board.Phase} idx={_board.PendingRequestIndex}/" +
                $"{_board.PendingRequests.Count} " +
                $"pending={_board.PendingRequest?.Effect.ToString() ?? "null"} " +
                $"游荡者·风流剑客手 L{_ranger.LeftHand.Value} R{_ranger.RightHand.Value}";

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, _board.Phase,
                $"下一回合的抉择**不能**被跳过。现场：{scene}");
            Assert.AreEqual(HandEffect.SwashbucklerDuelistThrust, _board.PendingRequest?.Effect,
                $"左手 5 + 对面 1 = 6（攻击），另一只手是 2 -> 应弹强化。现场：{scene}");
        }

        /// <summary>
        /// 更直接地钉住不变量：任何"轮到某人选牌"的时候，
        /// 抉择索引都必须与队列一致（不能出现 idx &gt; 0 而队列为空/长度 1）。
        /// </summary>
        [Test]
        public void Starting_a_turn_resets_the_choice_queue_index()
        {
            Arrange();

            // 先制造一次"接受抉择"以推进 idx
            TestSetup.SetHand(_ranger.LeftHand, 5);
            TestSetup.SetHand(_ranger.RightHand, 1);
            TestSetup.SetHand(_foe.LeftHand, 4);
            TestSetup.SetHand(_foe.RightHand, 2);

            _board.BeginTurn();
            _board.ClickHand(_ranger.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_foe.RightHand);
            _board.ConfirmTargetPlayer();
            while (_board.Phase == BoardPhase.ChoosingPreTurnChoice)
                if (!_board.DeclinePendingChoice()) break;

            TestSetup.SetHand(_ranger.LeftHand, 5);
            TestSetup.SetHand(_ranger.RightHand, 1);
            TestSetup.SetHand(_foe.LeftHand, 3);
            TestSetup.SetHand(_foe.RightHand, 1);

            _board.BeginTurn();
            _board.ClickHand(_foe.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_ranger.LeftHand);
            _board.ConfirmTargetPlayer();

            if (_board.PendingRequest?.Effect == HandEffect.SwashbucklerDodge)
                _board.AcceptPendingChoice();

            // 走过一个回合边界后再检查
            _board.BeginTurn();

            Assert.AreEqual(0, _board.PendingRequestIndex,
                "新回合开始时，抉择索引必须复位为 0（否则会跳过本回合的询问）。");
            Assert.AreEqual(0, _board.PendingRequests.Count,
                "新回合开始时，上一回合的抉择队列必须被清空。");
        }
    }
}
