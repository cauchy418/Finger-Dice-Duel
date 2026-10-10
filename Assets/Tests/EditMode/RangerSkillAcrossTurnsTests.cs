using System.Collections.Generic;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 游侠抉择**生成条件**的反复性验证（直接测单位行为）。
    ///
    /// 玩家反馈：**一只手发动过技能之后就再也发动不了**，条件满足也不弹按钮。
    ///
    /// ⚠️ 为什么不用 BoardController 驱动：
    ///   我试过两次靠"推回合"来模拟，都因为
    ///   ① 每回合的运算会改手值；② `_pendingRequests` 的清理时机
    ///   而读到**残留队列**，断言看到的不是自己设的现场（踩了两次坑）。
    ///   改成直接测生成条件 —— 这才是真正要回答的问题：
    ///   "条件满足时，抉择还会不会被生成？"
    /// </summary>
    [TestFixture]
    public class RangerSkillAcrossTurnsTests
    {
        private PlayerState _ranger;
        private PlayerState _warrior;

        [SetUp]
        public void SetUp()
        {
            _ranger = CharacterFactory.CreatePlayer("游侠", CharacterDefinition.Ranger);
            _warrior = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Warrior);
        }

        /// <summary>模拟"战士攻击游侠"，收集游侠这一侧提出的抉择。</summary>
        private List<ChoiceRequest> CollectDodgeChoices(int rangerLeft, int rangerRight,
                                                        int warriorActed, int warriorTarget)
        {
            TestSetup.SetHand(_ranger.LeftHand, rangerLeft);
            TestSetup.SetHand(_ranger.RightHand, rangerRight);
            TestSetup.SetHand(_warrior.LeftHand, warriorActed);
            TestSetup.SetHand(_warrior.RightHand, warriorTarget);

            // 运算：战士的左手 打 游侠的左手 -> 结果 = acted + target
            var context = new PreTurnChoiceContext(
                _warrior, _ranger, _warrior.LeftHand, _ranger.LeftHand);

            var requests = new List<ChoiceRequest>();
            (_ranger.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            return requests;
        }

        /// <summary>模拟"游侠攻击"，收集游侠这一侧提出的抉择。</summary>
        private List<ChoiceRequest> CollectPowerShotChoices(int rangerActed, int rangerOther,
                                                            int warriorTarget)
        {
            TestSetup.SetHand(_ranger.LeftHand, rangerActed);
            TestSetup.SetHand(_ranger.RightHand, rangerOther);
            TestSetup.SetHand(_warrior.LeftHand, warriorTarget);
            TestSetup.SetHand(_warrior.RightHand, 1);

            // 注意：结果必须是攻击（4/6/8）才会问强化。
            // 6 + 2 = 8 ✔；而 6 + 1 = 7 不是攻击，就不会生成（曾因此误判）。
            var context = new PreTurnChoiceContext(
                _ranger, _warrior, _ranger.LeftHand, _warrior.LeftHand);

            var requests = new List<ChoiceRequest>();
            (_ranger.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            return requests;
        }

        private static bool Has(List<ChoiceRequest> list, HandEffect effect)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].Effect == effect) return true;

            return false;
        }

        // ============================================================
        // 翻滚
        // ============================================================

        [Test]
        public void Dodge_is_generated_when_a_hand_is_one()
        {
            List<ChoiceRequest> requests = CollectDodgeChoices(1, 5, 3, 1);

            Assert.True(Has(requests, HandEffect.RangerDodge),
                "手上有 1 且这次是攻击 -> 应生成翻滚抉择。");
        }

        [Test]
        public void Dodge_is_still_generated_after_a_previous_dodge_happened()
        {
            // 核心：真的发动过一次（1 -> 2），之后把手值还原成 1，
            // 抉择**必须还能被生成**。
            List<ChoiceRequest> first = CollectDodgeChoices(1, 5, 3, 1);
            Assert.True(Has(first, HandEffect.RangerDodge), "第一次应生成。");

            TestSetup.SetHand(_ranger.LeftHand, RangerLogic.DodgeToValue);   // 1 -> 2
            Assert.AreEqual(2, _ranger.LeftHand.Value);

            // 把那只手变回 1（模拟后续回合的运算）
            TestSetup.SetHand(_ranger.LeftHand, RangerLogic.DodgeFromValue);

            List<ChoiceRequest> second = CollectDodgeChoices(1, 5, 3, 1);

            Assert.True(Has(second, HandEffect.RangerDodge),
                "手值重新变成 1 之后，**必须还能生成翻滚抉择**。" +
                "若失败，就是玩家反馈的\"用过一次就再也不能发动\"。");
        }

        [Test]
        public void Dodge_is_not_generated_when_neither_hand_is_one()
        {
            List<ChoiceRequest> requests = CollectDodgeChoices(2, 5, 3, 1);

            Assert.False(Has(requests, HandEffect.RangerDodge),
                "没有 1 时不该生成翻滚抉择。");
        }

        // ============================================================
        // 强化
        // ============================================================

        [Test]
        public void Power_shot_is_generated_when_the_other_hand_is_two()
        {
            List<ChoiceRequest> requests = CollectPowerShotChoices(6, 2, 2);

            Assert.True(Has(requests, HandEffect.RangerPowerShot),
                "另一只手是 2 且自己发起攻击 -> 应生成强化抉择。");
        }

        [Test]
        public void Power_shot_is_still_generated_after_a_previous_shot_happened()
        {
            // 核心：真的强化过一次（2 -> 3），之后把那只手变回 2，
            // 抉择**必须还能被生成**。
            List<ChoiceRequest> first = CollectPowerShotChoices(6, 2, 2);
            Assert.True(Has(first, HandEffect.RangerPowerShot), "第一次应生成。");

            TestSetup.SetHand(_ranger.RightHand, RangerLogic.PowerToValue);   // 2 -> 3
            Assert.AreEqual(3, _ranger.RightHand.Value);

            TestSetup.SetHand(_ranger.RightHand, RangerLogic.PowerFromValue); // 变回 2

            List<ChoiceRequest> second = CollectPowerShotChoices(6, 2, 2);

            Assert.True(Has(second, HandEffect.RangerPowerShot),
                "手值重新变成 2 之后，**必须还能生成强化抉择**。" +
                "若失败，就是玩家反馈的\"用过一次就再也不能发动\"。");
        }

        [Test]
        public void Power_shot_is_not_generated_when_the_other_hand_is_not_two()
        {
            List<ChoiceRequest> requests = CollectPowerShotChoices(6, 3, 2);

            Assert.False(Has(requests, HandEffect.RangerPowerShot),
                "另一只手不是 2 时不该生成强化抉择。");
        }

        // ============================================================
        // 玩家的精确场景：左手 5 + 对面 1 = 6 攻击，右手是 2
        // ============================================================

        [Test]
        public void Players_exact_scene_left_five_plus_one_equals_six_with_right_two()
        {
            // 玩家实测：第五步左手 5、右手 2（上一回合翻滚 1->2 得到的），
            // 用 5 打对面的 1 -> 5 + 1 = 6 -> 攻击，此时应弹强化，但实际没有。
            List<ChoiceRequest> requests = CollectPowerShotChoices(5, 2, 1);

            Assert.True(Has(requests, HandEffect.RangerPowerShot),
                "左手 5 + 对面 1 = 6（攻击），另一只手是 2 -> 应生成强化抉择。" +
                "若失败，就是玩家反馈的问题。");
        }

        [Test]
        public void Left_six_plus_two_equals_eight_with_right_two_also_prompts()
        {
            // 对照：另一种能凑出攻击的组合，也应生成
            List<ChoiceRequest> requests = CollectPowerShotChoices(6, 2, 2);

            Assert.True(Has(requests, HandEffect.RangerPowerShot),
                "6 + 2 = 8（攻击），另一只手是 2 -> 应生成强化抉择。");
        }

        [Test]
        public void All_attacks_with_a_two_in_the_other_hand_prompt()
        {
            // 穷举：左手值 1..9、对面手值 1..9，只要结果是 4/6/8 且另一只手是 2，
            // 就必须生成强化抉择。这样不会再漏掉某个组合。
            var missing = new List<string>();

            for (int left = 1; left <= 9; left++)
            {
                for (int target = 1; target <= 9; target++)
                {
                    int result = Hand.ComputeSum(left, target);
                    HandEffectType effect = Hand.GetEffectType(result);

                    bool isAttack = effect == HandEffectType.Attack4
                                    || effect == HandEffectType.Attack6
                                    || effect == HandEffectType.Attack8;

                    List<ChoiceRequest> requests = CollectPowerShotChoices(left, 2, target);
                    bool prompted = Has(requests, HandEffect.RangerPowerShot);

                    if (isAttack != prompted)
                    {
                        missing.Add($"左手{left}+对面{target}={result}" +
                                    $"（{(isAttack ? "攻击" : "非攻击")}）-> " +
                                    $"生成={prompted}");
                    }
                }
            }

            Assert.AreEqual(0, missing.Count,
                "以下组合的强化生成与'是否攻击'不一致：\n" + string.Join("\n", missing));
        }
        // ============================================================
        // 反复生成 10 次，确认没有"一次性标记"
        // ============================================================

        [Test]
        public void Ranger_logic_has_no_one_shot_state()
        {
            for (int i = 1; i <= 10; i++)
            {
                List<ChoiceRequest> requests = CollectDodgeChoices(1, 5, 3, 1);
                Assert.True(Has(requests, HandEffect.RangerDodge),
                    $"第 {i} 次生成失败 —— 说明有条件被\"用掉\"了。");

                // 用掉再还原
                TestSetup.SetHand(_ranger.LeftHand, RangerLogic.DodgeToValue);
                TestSetup.SetHand(_ranger.LeftHand, RangerLogic.DodgeFromValue);
            }
        }
    }
}
