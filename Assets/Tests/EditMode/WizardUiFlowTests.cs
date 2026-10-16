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
    /// **法师的完整界面流程**测试。
    ///
    /// 起因：玩家实测「法师点击放弃攻击后卡住」。
    ///
    /// 两个根因（都在**界面层**）：
    ///   ① `GameBootstrap.OnAcceptClicked` 写成
    ///      `if (AcceptPendingChoice()) RefreshView();` ——
    ///      被拒绝时**界面一点都不刷新**，看起来就是卡住；
    ///   ② `BoardController.AcceptPendingChoice` 要求**唯一候选**，
    ///      而法师的「放弃攻击」是**是非题**（候选是他自己的两只手，
    ///      只用来占位）—— 于是点"发动"按钮被**静默拒绝**。
    ///
    /// 本测试把这两条钉在**控制器层**（界面层不好单测）。
    /// </summary>
    [TestFixture]
    public class WizardUiFlowTests
    {
        private static BoardController MakeBoard(out PlayerState mage, out PlayerState foe,
                                                 out GameManager game)
        {
            mage = CharacterFactory.CreatePlayer("法", CharacterDefinition.Wizard);
            foe = CharacterFactory.CreatePlayer("战", CharacterDefinition.Fighter);

            game = new GameManager(mage, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());

            // ⚠️ BoardController 要求抉择器同时实现 IPlayerChoiceSink
            return new BoardController(game, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
        }

        [Test]
        public void Abandon_attack_button_is_accepted_even_with_multiple_candidates()
        {
            BoardController board = MakeBoard(out PlayerState mage, out PlayerState foe,
                                              out _);
            board.BeginTurn();

            // 摆一个"算出 4"的局面：左手 3 + 对手 1 = 4
            mage.LeftHand.SetValue(3);
            mage.RightHand.SetValue(1);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(1);

            // ① 点自己的手 -> ② 点对手的手 -> 进入"结算前抉择"
            board.ClickHand(mage.LeftHand);
            board.ClickHand(foe.LeftHand);

            // 到了"是否放弃攻击"这一步
            Assert.AreEqual(HandEffect.WizardAbandonAttack, board.PendingEffect,
                "算出攻击数字后应当先问「是否放弃攻击」。");
            Assert.IsTrue(board.CanDeclinePendingChoice,
                "这一步允许放弃（放弃 = 不放弃攻击 = 照常打）。");

            // ⚠️ 关键：候选是法师自己的两只手（是非题），
            //    但"发动"按钮必须**能用** —— 这就是玩家卡死的那一步。
            Assert.IsTrue(board.AcceptPendingChoice(),
                "「放弃攻击」有多个候选时，点发动按钮也必须被接受"
                + "（否则界面毫无反应 = 玩家看到的卡死）。");

            // ③ 下一步应当是"选伤害类型"
            Assert.AreEqual(HandEffect.WizardDamageType, board.PendingEffect,
                "选了放弃之后，下一步应当是 10 选 1 的伤害类型。");
        }

        [Test]
        public void Choosing_a_damage_type_completes_the_turn()
        {
            BoardController board = MakeBoard(out PlayerState mage, out PlayerState foe,
                                              out GameManager game);
            board.BeginTurn();

            mage.LeftHand.SetValue(3);
            mage.RightHand.SetValue(1);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(1);

            board.ClickHand(mage.LeftHand);
            board.ClickHand(foe.LeftHand);
            board.AcceptPendingChoice();          // 放弃攻击

            Assert.AreEqual(HandEffect.WizardDamageType, board.PendingEffect);

            TurnResult before = board.LastTurn;
            bool ok = board.ChooseDamageType(DamageType.Fire);
            Assert.IsTrue(ok,
                "选了伤害类型之后流程必须继续 —— 否则就是卡住。"
                + $"[phase={board.Phase} pending={board.PendingEffect} "
                + $"canDecline={board.CanDeclinePendingChoice} "
                + $"actor={board.ActivePlayer?.Name}]");

            // 这一步不该还停在"选伤害类型"
            Assert.AreNotEqual((int)HandEffect.WizardDamageType, (int)board.PendingEffect,
                "回答完之后不该还停在「选伤害类型」。");

            // ⚠️ 类型**必须留下来**：玩家确认的机制是
            //    "放弃这次攻击，为**下一次**攻击指定伤害类型" ——
            //    所以被放弃的这一击不能把它吃掉。
            var logic = (WizardLogic)mage.Character;
            Assert.AreEqual(DamageType.Fire, logic.PendingDamageType,
                "被放弃的那一击不该消耗类型 —— 类型要留给下一次攻击。");
        }

        [Test]
        public void Choosing_a_damage_type_records_it_before_resolving()
        {
            // 与上一条互补：**在选择动作本身**这一步，
            // 类型必须真的写进法师身上（不能只是因为被立刻消耗掉而看不到）。
            BoardController board = MakeBoard(out PlayerState mage, out PlayerState foe,
                                              out _);
            board.BeginTurn();

            mage.LeftHand.SetValue(3);
            mage.RightHand.SetValue(1);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(1);

            board.ClickHand(mage.LeftHand);
            board.ClickHand(foe.LeftHand);
            board.AcceptPendingChoice();

            Assert.AreEqual(HandEffect.WizardDamageType, board.PendingEffect,
                "放弃之后应当进入「选伤害类型」。");

            bool ok = board.ChooseDamageType(DamageType.Psychic);
            Assert.IsTrue(ok, "选择伤害类型必须被接受。");

            // ⚠️ 玩家确认的机制是"放弃这次攻击，为**下一次**攻击指定类型" ——
            //    所以放弃的这一击**不能消耗掉类型**，它必须留下来等下一次。
            var logic = (WizardLogic)mage.Character;
            Assert.AreEqual(DamageType.Psychic, logic.PendingDamageType,
                "被放弃的那一击不该消耗类型 —— 类型要留给下一次攻击"
                + $"（现在={logic.PendingDamageType}）。");
        }

        [Test]
        public void Declining_the_abandon_keeps_the_normal_attack()
        {
            BoardController board = MakeBoard(out PlayerState mage, out PlayerState foe,
                                              out _);
            board.BeginTurn();

            mage.LeftHand.SetValue(3);
            mage.RightHand.SetValue(1);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(1);

            board.ClickHand(mage.LeftHand);
            board.ClickHand(foe.LeftHand);

            // 点"不放弃" = 照常攻击
            Assert.IsTrue(board.DeclinePendingChoice());

            var logic = (WizardLogic)mage.Character;
            Assert.IsFalse(logic.HasPendingType,
                "没放弃就不该记下任何伤害类型。");
        }
    }
}
