using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **复现**：法师在"是否放弃攻击"上点「不发动」之后，
    /// 必须在**确认攻击目标**这一步停下来，而不是直接结算。
    ///
    /// 玩家实测（附日志）：
    ///   "法师点击不发动后没有选择攻击目标就自动攻击了"
    /// </summary>
    [TestFixture]
    public class WizardDeclineKeepsTargetStepTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        [Test]
        public void Declining_the_abandon_still_asks_for_the_attack_target()
        {
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("法", CharacterDefinition.Wizard),
                                       Make("战", CharacterDefinition.Fighter),
                                       providers[0], providers[1]);
            var board = new BoardController(game, providers[0], providers[1]);
            board.BeginTurn();       // ⚠️ 必须先开始回合，否则 ClickHand 会被拒

            PlayerState mage = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 法师右手 9 + 战士 5 = 14 -> 4（攻击）
            mage.LeftHand.SetValue(8);
            mage.RightHand.SetValue(9);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(5);

            var log = new System.Text.StringBuilder();


            // ① 选自己的右手
            Assert.IsTrue(board.ClickHand(mage.RightHand), "应当能点自己的手。");
            log.Append("afterOwnHand=").Append(board.Phase).Append(' ');

            // ② 点对手的右手（5）作为加数
            Assert.IsTrue(board.ClickHand(foe.RightHand), "应当能点对手的手。");
            log.Append("afterTargetHand=").Append(board.Phase).Append(' ');

            // ③ 此时应当停在"选目标手之前的抉择"（法师的「是否放弃攻击」）

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "算出攻击数字后应当先问法师「是否放弃攻击」。[" + log + "]");
            Assert.AreEqual(HandEffect.WizardAbandonAttack, board.PendingEffect,
                "当前待问的应当是「是否放弃攻击」。[" + log + "]");

            // ④ 点「不发动」= 照常攻击
            Assert.IsTrue(board.DeclinePendingChoice(), "「不发动」应当被接受。");


            log.Append("afterDecline=").Append(board.Phase).Append(' ');
            log.Append("hint=").Append(board.Hint ?? "null").Append(' ');
            log.Append("selected=").Append(board.SelectedHand?.Value.ToString() ?? "null").Append(' ');

            // ⑤ **关键断言**：必须停下来确认攻击目标
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "点「不发动」之后必须**停下来确认攻击目标**，"
                + "而不是直接结算（玩家实测：不选目标就自动打了）。[" + log + "]");

            // ⑥ 对手此时还不该掉血
            Assert.AreEqual(foe.MaximumHealth, foe.Health,
                "还没确认目标，就不该已经造成伤害。[" + log + "]");
        }
    }
}
