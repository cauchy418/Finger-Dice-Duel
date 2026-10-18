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

        /// <summary>
        /// **回归：在"确认目标"阶段再点「不发动」，不能把流程打回"是否放弃攻击"。**
        ///
        /// 玩家实测的死循环：
        ///   法师「不发动」-> 进入 `ChoosingTargetPlayer`（界面上还留着「放弃攻击」按钮）
        ///   -> 再点 -> `DeclinePendingChoice` 按**过期**的 idx 推进
        ///   -> idx 被弄回 0 -> **重新问「是否放弃攻击」** -> 无限循环。
        /// </summary>
        [Test]
        public void Declining_at_the_target_step_does_not_loop_back()
        {
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("法", CharacterDefinition.Wizard),
                                       Make("战", CharacterDefinition.Fighter),
                                       providers[0], providers[1]);
            var board = new BoardController(game, providers[0], providers[1]);
            board.BeginTurn();

            PlayerState mage = game.PlayerA;
            PlayerState foe = game.PlayerB;

            mage.LeftHand.SetValue(1);
            mage.RightHand.SetValue(9);
            foe.LeftHand.SetValue(5);
            foe.RightHand.SetValue(1);

            board.ClickHand(mage.LeftHand);
            board.ClickHand(foe.LeftHand);          // 1 + 5 = 6 -> 攻击

            Assert.AreEqual(HandEffect.WizardAbandonAttack, board.PendingEffect);

            // 「不发动」-> 停在确认目标
            Assert.IsTrue(board.DeclinePendingChoice());
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "「不发动」之后应当停在确认目标。");

            // 这一步**不该**允许再「不发动」（否则会打回上一步造成死循环）
            Assert.IsFalse(board.DeclinePendingChoice(),
                "确认目标阶段不能再「不发动」—— 那会把流程打回"
                + "「是否放弃攻击」，玩家实测会无限循环。");

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "被拒绝后阶段必须保持不变（仍等玩家确认目标）。");
        }

        /// <summary>
        /// **回归：确认目标之后，不能把「是否放弃攻击」再问一遍。**
        ///
        /// 玩家实测的循环（第二次）：
        ///   「不发动」-> 停在确认目标 -> 点目标的手 ->
        ///   `CommitMove` -> `BeginCharacterChoices` **重新收集** ->
        ///   `idx` 归 0 -> **又弹「是否放弃攻击」** -> 无限循环。
        ///
        /// 修法：`CommitMove` 在"本回合抉择已收集过"时**不再收集**。
        /// </summary>
        [Test]
        public void Confirming_the_target_does_not_ask_the_abandon_again()
        {
            var providers = new List<SimpleChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var game = new GameManager(Make("法", CharacterDefinition.Wizard),
                                       Make("战", CharacterDefinition.Fighter),
                                       providers[0], providers[1]);
            var board = new BoardController(game, providers[0], providers[1]);
            board.BeginTurn();

            PlayerState mage = game.PlayerA;
            PlayerState foe = game.PlayerB;

            mage.LeftHand.SetValue(2);
            mage.RightHand.SetValue(5);
            foe.LeftHand.SetValue(5);
            foe.RightHand.SetValue(6);

            int foeHpBefore = foe.Health;

            board.ClickHand(mage.LeftHand);        // 2
            board.ClickHand(foe.RightHand);        // 2 + 6 = 8 -> 攻击

            Assert.AreEqual(HandEffect.WizardAbandonAttack, board.PendingEffect);

            // 「不发动」-> 停在确认目标
            Assert.IsTrue(board.DeclinePendingChoice());
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);

            string afterDeclineProbe = board.ChoiceStateProbe;

            // 点目标的手确认 —— **不该**又回到「是否放弃攻击」
            Assert.IsTrue(board.ConfirmTargetPlayer(),
                "确认目标应当被接受。");

            string trace = $"afterConfirm={board.Phase} idxPending={board.PendingEffect} "
                + $"hint={board.Hint}\n      afterDecline[{afterDeclineProbe}]"
                + $"\n      afterConfirm[{board.ChoiceStateProbe}]";

            Assert.IsFalse(board.Phase == BoardPhase.ChoosingPreTurnChoice,
                "确认目标之后**不能**又回到「选目标前的抉择」—— "
                + "那会把已经答完的「是否放弃攻击」再问一遍（玩家实测的无限循环）。"
                + trace);

            Assert.IsFalse(board.PendingEffect == HandEffect.WizardAbandonAttack,
                "确认目标之后不该再问「是否放弃攻击」。");

            // 而且这一击应当真的打出去了
            Assert.IsTrue(foe.Health < foeHpBefore,
                $"确认目标后应当造成伤害（{foeHpBefore} -> {foe.Health}）。");
        }
    }
}
