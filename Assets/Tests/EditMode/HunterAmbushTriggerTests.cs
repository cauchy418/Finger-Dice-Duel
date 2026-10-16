using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **伏击的判定与触发** —— 以玩家给的例子为准：
    ///
    ///   "指的是我运算后两只手数值相同，例如左手是 9，右手是 2，
    ///    这回合我用 9+3=2，触发伏击。"
    ///
    /// 拆开：左手 9 发起运算、加数是 3 -> 9 + 3 = 12 -> 归一化 **2**，
    /// 于是左手变成 2，而右手本来就是 2 -> 两手同为 2 -> 触发。
    ///
    /// 触发时机与剑客追加攻击**完全一致**（`OnBeforeEffectResolved`）。
    /// </summary>
    [TestFixture]
    public class HunterAmbushTriggerTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        // ---------------------------------------------------------------
        // 玩家给的那个例子
        // ---------------------------------------------------------------

        [Test]
        public void Player_example_nine_plus_three_makes_two_and_triggers()
        {
            // 9 + 3 = 12 -> 归一化 2
            Assert.AreEqual(2, Hand.ComputeSum(9, 3),
                "前提：9 + 3 归一化后应等于 2（玩家例子的算术基础）。");

            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);

            // 左手 9（发起运算的那只）、右手 2
            hunter.LeftHand.SetValue(9);
            hunter.RightHand.SetValue(2);

            // 运算之后：左手已变成结果值 2
            Assert.True(
                HunterLogic.MeetsAmbushCondition(hunter, hunter.LeftHand, 2),
                "左手运算后为 2、右手为 2 -> 应满足伏击条件（玩家给的例子）。");
        }

        [Test]
        public void Player_example_does_not_apply_to_the_other_hand_by_mistake()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            hunter.LeftHand.SetValue(9);
            hunter.RightHand.SetValue(2);

            // 若换成"右手发起运算、结果 2"，那左手（另一只手）是 9 —— 不满足
            Assert.False(
                HunterLogic.MeetsAmbushCondition(hunter, hunter.RightHand, 2),
                "另一只手是 9，不满足（必须是两只手都为 1/2/3 且相同）。");
        }

        [Test]
        public void Condition_requires_both_hands_one_to_three_and_equal()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);

            hunter.LeftHand.SetValue(3);
            hunter.RightHand.SetValue(3);
            Assert.True(HunterLogic.MeetsAmbushCondition(hunter, hunter.LeftHand, 3),
                "结果为 3、另一只手 3 -> 满足。");

            hunter.RightHand.SetValue(4);
            Assert.False(HunterLogic.MeetsAmbushCondition(hunter, hunter.LeftHand, 3),
                "另一只手是 4 -> 超出 1~3，不满足。");

            hunter.RightHand.SetValue(1);
            Assert.False(HunterLogic.MeetsAmbushCondition(hunter, hunter.LeftHand, 3),
                "3 与 1 不同 -> 不满足。");

            hunter.RightHand.SetValue(3);
            Assert.False(HunterLogic.MeetsAmbushCondition(hunter, hunter.LeftHand, 4),
                "结果 4 超出 1~3 -> 不满足。");
        }

        // ---------------------------------------------------------------
        // 与剑客同一个通道：抉择会出现在收集结果里
        // ---------------------------------------------------------------

        [Test]
        public void Ambush_choice_is_collected_through_the_same_channel()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            hunter.LeftHand.SetValue(9);
            hunter.RightHand.SetValue(2);

            // 加数手是敌人的 3 -> 9 + 3 = 2
            foe.LeftHand.SetValue(3);

            var context = new PreTurnChoiceContext(hunter, foe, hunter.LeftHand, foe.LeftHand);
            Assert.AreEqual(2, context.ResultValue, "前提：这次运算的结果应是 2。");
            Assert.False(context.IsAttack, "前提：2 不是攻击效果。");

            var requests = new List<ChoiceRequest>();
            (hunter.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            bool offered = false;
            for (int i = 0; i < requests.Count; i++)
                if (requests[i].Effect == HandEffect.HunterAmbush) offered = true;

            Assert.True(offered,
                "满足条件时应通过抉择通道提供伏击选项（与剑客追加攻击同一条通道）。");
        }

        [Test]
        public void No_ambush_choice_when_the_result_is_not_two_to_three()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            hunter.LeftHand.SetValue(9);
            hunter.RightHand.SetValue(2);
            foe.LeftHand.SetValue(4);   // 9 + 4 = 13 -> 3，与右手 2 不同

            var context = new PreTurnChoiceContext(hunter, foe, hunter.LeftHand, foe.LeftHand);
            Assert.AreNotEqual(2, context.ResultValue, "前提：这次结果不是 2。");

            var requests = new List<ChoiceRequest>();
            (hunter.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                Assert.AreNotEqual((int)HandEffect.HunterAmbush, (int)requests[i].Effect,
                    "结果与另一只手不同时不应提供伏击。");
        }

        [Test]
        public void No_ambush_choice_on_an_attack()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            // 4 + 2 = 6 -> 攻击；且右手 2、结果 6 不同
            hunter.LeftHand.SetValue(4);
            hunter.RightHand.SetValue(2);
            foe.LeftHand.SetValue(2);

            var context = new PreTurnChoiceContext(hunter, foe, hunter.LeftHand, foe.LeftHand);
            Assert.True(context.IsAttack, "前提：这次是攻击。");

            var requests = new List<ChoiceRequest>();
            (hunter.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                Assert.AreNotEqual((int)HandEffect.HunterAmbush, (int)requests[i].Effect,
                    "攻击运算不应提供伏击。");
        }

        [Test]
        public void Other_characters_never_offer_ambush()
        {
            PlayerState soldier = Make("兵", CharacterDefinition.Soldier);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            soldier.LeftHand.SetValue(9);
            soldier.RightHand.SetValue(2);
            foe.LeftHand.SetValue(3);

            var context = new PreTurnChoiceContext(soldier, foe, soldier.LeftHand, foe.LeftHand);
            var requests = new List<ChoiceRequest>();
            (soldier.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                Assert.AreNotEqual((int)HandEffect.HunterAmbush, (int)requests[i].Effect,
                    "只有猎人能伏击。");
        }
    }
}
