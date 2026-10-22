using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **闪避排在太极之前**（玩家要求）。
    ///
    /// 以前：太极在逐段循环**之前**询问 —— 一旦发动，本次攻击**根本不再判闪避**；
    /// 现在：循环里**每段先闪避**，只有"没被闪掉的段"才吃太极。
    ///
    /// 这条测试钉住这个差别：防守方**同时**有闪避资源与太极时，
    /// 闪避必须**先被消耗**（那张"1"变成 2）。
    /// </summary>
    [TestFixture]
    public class DodgeBeforeTaichiTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        [Test]
        public void Dodge_is_consumed_before_taichi_takes_over()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            // 照拄现有的 ArrangeHumanSwashbucklerDodge：攻方用 Blank
            var game = new GameManager(Make("A", CharacterDefinition.Blank),
                                       Make("B", CharacterDefinition.Swashbuckler),
                                       choiceA, choiceB);
            var board = new BoardController(game, choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState swashbuckler = game.PlayerB;

            TestSetup.SetAllHands(blank, swashbuckler, 1);
            TestSetup.SetHand(blank.LeftHand, 3);        // 3 + 1 = 4 -> 攻击

            // 游荡者·风流剑客：**左手是 1（闪避资源）**、右手是太极。
            // 闪避消耗后左手变成 2 —— 可直接断言。
            TestSetup.SetHand(swashbuckler.LeftHand, 1);
            swashbuckler.RightHand.SetValue(5);
            swashbuckler.RightHand.SetShieldState(ShieldState.Taichi);

            board.BeginTurn();
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(swashbuckler.LeftHand);
            board.ConfirmTargetPlayer();

            // 收集阶段：先回答闪避（点那张 1），再回答太极（点右手）
            for (int guard = 0; guard < 6 && board.Phase == BoardPhase.ChoosingPreTurnChoice; guard++)
            {
                if (board.PendingRequest?.Effect == HandEffect.SwashbucklerDodge)
                    board.ClickHand(swashbuckler.LeftHand);
                else
                    board.ClickHand(swashbuckler.RightHand);
            }

            // 闪避先消耗 -> 那张 1 变成 2（游荡者·风流剑客闪避规则：1 变 2）
            Assert.AreEqual(2, swashbuckler.LeftHand.Value,
                "闪避应先于太极生效并消耗掉那张 1"
                + $" （实际 {swashbuckler.LeftHand.Value}, phase={board.Phase}）。");
        }
    }
}
