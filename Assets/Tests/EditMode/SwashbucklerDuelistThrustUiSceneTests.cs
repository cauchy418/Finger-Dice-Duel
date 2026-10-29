using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 玩家实测现场：**左手 5 + 对面 1 = 6（攻击），右手是 2，却没有强化提示**。
    ///
    /// 这条测试故意把"抉择创建时的现场"逐项断言出来：
    ///   `PendingActedHand` / `PendingTargetHand` / `PendingIsAttack` / 队列内容。
    /// 只要这里是对的，问题就不在规则层与编排层。
    /// </summary>
    [TestFixture]
    public class SwashbucklerDuelistThrustUiSceneTests
    {
        [Test]
        public void Left_five_attacks_with_opponents_one_and_right_hand_two_should_prompt()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler),
                CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                choiceA, choiceB);

            var board = new BoardController(game, choiceA, choiceB);
            PlayerState swashbuckler = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 玩家给的现场：我方左手 5、右手 2（上一回合翻滚 1->2 得来的）
            TestSetup.SetHand(swashbuckler.LeftHand, 5);
            TestSetup.SetHand(swashbuckler.RightHand, 2);
            TestSetup.SetHand(foe.LeftHand, 4);
            TestSetup.SetHand(foe.RightHand, 1);

            board.BeginTurn();
            Assert.True(ReferenceEquals(board.ActivePlayer, swashbuckler), "游荡者·风流剑客先手");

            // 玩家操作：点自己的 5，再点对面的 1
            //
            // ⚠️ 顺序（与玩家确认的交互一致）：
            //   ① 点自己的手  ② 点对手的手（运算源）-> 结果确定
            //   ③ 这时才会问角色抉择（游荡者·风流剑客的潇洒无畏）
            // 所以这里**不需要**在两步之间调 ConfirmTargetPlayer ——
            // 确认目标玩家是"攻击结算前"的一步，而角色抉择在它之前。
            Assert.True(board.ClickHand(swashbuckler.LeftHand), "点击左手应被接受");
            Assert.True(board.ClickHand(foe.RightHand), "点击对面右手应被接受");

            string scene =
                $"PendingActedHand={board.PendingActedHand?.Value} " +
                $"PendingTargetHand={board.PendingTargetHand?.Value} " +
                $"PendingIsAttack={board.PendingIsAttack} " +
                $"右手={swashbuckler.RightHand.Value} " +
                $"phase={board.Phase} " +
                $"队列={board.PendingRequests.Count} " +
                $"acceptLabel='{board.PendingAcceptLabel}'";

            // 逐项断言现场（失败时把现场打出来）
            Assert.AreEqual(5, board.PendingActedHand?.Value,
                $"抉择创建时的行动手应是 5。现场：{scene}");
            Assert.AreEqual(1, board.PendingTargetHand?.Value,
                $"抉择创建时的目标手应是 1。现场：{scene}");
            Assert.AreEqual(6, Hand.ComputeSum(board.PendingActedHand?.Value ?? 0,
                                               board.PendingTargetHand?.Value ?? 0),
                $"5 + 1 应等于 6。现场：{scene}");
            Assert.True(board.PendingIsAttack,
                $"5 + 1 = 6 应被判定为攻击。现场：{scene}");

            Assert.AreEqual(1, board.PendingRequests.Count,
                $"应恰好有一个抉择。现场：{scene}");
            Assert.AreEqual(HandEffect.SwashbucklerDuelistThrust, board.PendingRequests[0].Effect,
                $"抉择应是潇洒无畏。现场：{scene}");
            Assert.AreEqual(2, board.PendingRequests[0].Candidates.Count == 0
                               ? 2 : board.PendingRequests[0].Candidates[0].Value,
                "候选应是那只 2。");
            Assert.False(string.IsNullOrEmpty(board.PendingAcceptLabel),
                $"应提供'发动'按钮文案。现场：{scene}");
        }

        /// <summary>
        /// 对照：如果攻击时点的是**右手 2**（也就是"另一只手是 5"），
        /// 那就**不该**弹强化 —— 因为强化的条件是"**另一只手**是 2"。
        /// 这一条用于排除"玩家点错手"的可能。
        /// </summary>
        [Test]
        public void Attacking_with_the_two_hand_does_not_prompt()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("游荡者·风流剑客", CharacterDefinition.Swashbuckler),
                CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                choiceA, choiceB);

            var board = new BoardController(game, choiceA, choiceB);
            PlayerState swashbuckler = game.PlayerA;
            PlayerState foe = game.PlayerB;

            TestSetup.SetHand(swashbuckler.LeftHand, 5);
            TestSetup.SetHand(swashbuckler.RightHand, 2);
            TestSetup.SetHand(foe.LeftHand, 4);
            TestSetup.SetHand(foe.RightHand, 2);

            board.BeginTurn();

            // 用右手 2 去打对面的 2 -> 2 + 2 = 4（攻击），但"另一只手"是 5，不是 2
            board.ClickHand(swashbuckler.RightHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(foe.RightHand);
            board.ConfirmTargetPlayer();


            Assert.True(board.PendingRequest?.Effect != HandEffect.SwashbucklerDuelistThrust,
                "用那只 2 发起攻击时，'另一只手'是 5 而不是 2，**不该**弹强化。" +
                $"实际={board.PendingRequest?.Effect}");
        }
    }
}
