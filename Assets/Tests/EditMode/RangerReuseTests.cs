using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 游侠技能**可重复发动**的验证。
    ///
    /// 为什么单独写一组：代码原本的注释写着"条件一旦用掉就消失 —— 这是设计意图，
    /// 用掉就没了"，读起来像"整局只能发动一次"。而真实规则是：
    ///
    ///   · 技能**不是消耗次数**，只要**当前**满足条件就能发动；
    ///   · "永久改变手值"只是改数值（1→2、2→3），不构成"额度用尽"；
    ///   · 所以那只手后来又变回 1 / 2 时，技能**又能发动**。
    ///
    /// 而原有的 20 个游侠测试**没有一个覆盖"反复发动"** ——
    /// 也就是说这条规则此前从未被验证过。这一组就是补这个缺口。
    /// </summary>
    [TestFixture]
    public class RangerReuseTests
    {
        /// <summary>造一次"白板攻击游侠"的结算，返回结果供断言。</summary>
        private static AttackContext AttackRanger(PlayerState ranger, ScriptedChoiceProvider sink,
                                                  int attackerHandValue, int rangerHandValue)
        {
            var attacker = CharacterFactory.CreatePlayer("Atk", CharacterDefinition.Blank);

            var ctx = new AttackContext(attacker, ranger, attacker.LeftHand, 4)
            {
                // 收集阶段的"运算前快照"：判定 4 点攻击
                SourceHandValueBefore = attackerHandValue,
                TargetHandValueBefore = rangerHandValue,
                AttackerProvider = sink,
            };

            ctx.Resolve(sink, null);
            return ctx;
        }

        // ============================================================
        // 闪避可以反复发动
        // ============================================================

        [Test]
        public void First_dodge_turns_a_one_into_a_two()
        {
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 5);
            sink.ChooseHand(ranger.LeftHand);

            AttackContext ctx = AttackRanger(ranger, sink, 3, 1);

            Assert.True(ctx.WasDodged, "第一次应当能闪避。");
            Assert.AreEqual(2, ranger.LeftHand.Value, "闪避把 1 变成 2。");

            _ = ctx;
        }

        [Test]
        public void Dodge_can_be_used_again_after_the_hand_returns_to_one()
        {
            // 这是本组测试的核心：
            //   左手 1 --翻滚--> 2 --（后续运算把它变回 1）--> 又能翻滚。
            // 若技能是"用掉就没了"，第二次就不会闪避。
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);

            // 第一次：1 -> 2
            var sink1 = new ScriptedChoiceProvider();
            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 5);
            sink1.ChooseHand(ranger.LeftHand);

            AttackContext first = AttackRanger(ranger, sink1, 3, 1);
            Assert.True(first.WasDodged, "第一次应闪避。");
            Assert.AreEqual(2, ranger.LeftHand.Value);

            // 模拟"后续回合把这只手变回 1"（例如 2 + 9 = 11 -> 1）
            TestSetup.SetHand(ranger.LeftHand, 1);

            // 第二次：应当还能闪避
            var sink2 = new ScriptedChoiceProvider();
            sink2.ChooseHand(ranger.LeftHand);

            AttackContext second = AttackRanger(ranger, sink2, 3, 1);

            Assert.True(second.WasDodged, "手值变回 1 之后应该**再次**能闪避（技能不消耗次数）。");
            Assert.AreEqual(2, ranger.LeftHand.Value, "第二次同样把 1 变成 2。");
        }

        [Test]
        public void The_other_hand_can_dodge_even_though_the_first_one_already_did()
        {
            // 双 1：第一次用左手，第二次（手值恢复后）用右手 —— 证明不是"只有一只手能用一次"
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);

            var sink1 = new ScriptedChoiceProvider();
            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 1);
            sink1.ChooseHand(ranger.LeftHand);

            AttackContext first = AttackRanger(ranger, sink1, 3, 1);
            Assert.True(first.WasDodged);
            Assert.AreEqual(2, ranger.LeftHand.Value, "左手用掉。");
            Assert.AreEqual(1, ranger.RightHand.Value, "右手应保留 1。");

            // 左手恢复成 1
            TestSetup.SetHand(ranger.LeftHand, 1);

            var sink2 = new ScriptedChoiceProvider();
            sink2.ChooseHand(ranger.RightHand);       // 这次换右手

            AttackContext second = AttackRanger(ranger, sink2, 3, 1);

            Assert.True(second.WasDodged, "换一只 1 也应能闪避。");
            Assert.AreEqual(2, ranger.RightHand.Value, "这次消耗的是右手。");
        }

        [Test]
        public void Dodge_is_impossible_when_neither_hand_is_one()
        {
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(ranger.LeftHand, 2);      // 用掉之后的状态
            TestSetup.SetHand(ranger.RightHand, 5);

            AttackContext ctx = AttackRanger(ranger, sink, 3, 2);

            Assert.False(ctx.WasDodged, "没有 1 时不能闪避 —— 条件不满足，而不是次数用尽。");
        }

        // ============================================================
        // 强化射击可以反复发动
        // ============================================================

        [Test]
        public void Power_shot_turns_a_two_into_a_three()
        {
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var attacker = CharacterFactory.CreatePlayer("Atk", CharacterDefinition.Blank);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(ranger.RightHand, 2);     // 另一只手

            var ctx = new AttackContext(ranger, attacker, ranger.LeftHand, 4)
            {
                SourceHandValueBefore = 3,              // 3 + 1 = 4 -> 攻击 4
                TargetHandValueBefore = 1,
                AttackerProvider = sink,
            };
            sink.ChooseHand(ranger.RightHand);

            ctx.Resolve(sink, null);

            Assert.AreEqual(3, ranger.RightHand.Value, "强化把 2 变成 3。");
            Assert.True(ctx.DamageMultiplier > 1f, "强化应提高倍率。");
        }

        [Test]
        public void Power_shot_can_be_used_again_after_the_hand_returns_to_two()
        {
            // 核心：2 --强化--> 3 --（后续运算变回 2）--> 又能强化
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var attacker = CharacterFactory.CreatePlayer("Atk", CharacterDefinition.Blank);

            // 第一次
            var sink1 = new ScriptedChoiceProvider();
            TestSetup.SetHand(ranger.RightHand, 2);

            var first = new AttackContext(ranger, attacker, ranger.LeftHand, 4)
            {
                SourceHandValueBefore = 3,
                TargetHandValueBefore = 1,
                AttackerProvider = sink1,
            };
            sink1.ChooseHand(ranger.RightHand);
            first.Resolve(sink1, null);

            Assert.AreEqual(3, ranger.RightHand.Value, "第一次强化：2 -> 3。");
            Assert.True(first.DamageMultiplier > 1f);

            // 模拟后续回合把这只手变回 2
            TestSetup.SetHand(ranger.RightHand, 2);

            // 第二次
            var sink2 = new ScriptedChoiceProvider();
            sink2.ChooseHand(ranger.RightHand);

            var second = new AttackContext(ranger, attacker, ranger.LeftHand, 4)
            {
                SourceHandValueBefore = 3,
                TargetHandValueBefore = 1,
                AttackerProvider = sink2,
            };
            second.Resolve(sink2, null);

            Assert.AreEqual(3, ranger.RightHand.Value, "第二次强化同样 2 -> 3。");
            Assert.True(second.DamageMultiplier > 1f,
                "手值变回 2 之后应该**再次**能强化（技能不消耗次数）。");
        }

        [Test]
        public void Power_shot_is_impossible_when_the_other_hand_is_not_two()
        {
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var attacker = CharacterFactory.CreatePlayer("Atk", CharacterDefinition.Blank);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(ranger.RightHand, 3);     // 用掉之后的状态

            var ctx = new AttackContext(ranger, attacker, ranger.LeftHand, 4)
            {
                SourceHandValueBefore = 3,
                TargetHandValueBefore = 1,
                AttackerProvider = sink,
            };
            ctx.Resolve(sink, null);

            Assert.AreEqual(3, ranger.RightHand.Value, "没有 2 时不能被强化。");
            Assert.AreEqual(1f, ctx.DamageMultiplier, "倍率应保持 1。");
        }

        // ============================================================
        // 两个技能互不干扰
        // ============================================================

        [Test]
        public void Dodging_does_not_consume_the_power_shot_charge()
        {
            // 用闪避把一只 1 变成 2 —— 这时"另一只手是 2"的条件**恰好被满足**，
            // 所以紧接着自己攻击时应当能强化。证明两个技能各按自己的条件判断，
            // 没有共享"额度"。
            var ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);

            var dodgeSink = new ScriptedChoiceProvider();
            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 5);
            dodgeSink.ChooseHand(ranger.LeftHand);

            AttackContext dodge = AttackRanger(ranger, dodgeSink, 3, 1);
            Assert.True(dodge.WasDodged);
            Assert.AreEqual(2, ranger.LeftHand.Value, "闪避后左手 = 2。");

            // 现在轮到游侠攻击：左手是 2，右手作为运算源
            TestSetup.SetHand(ranger.RightHand, 4);     // 运算源
            var powerSink = new ScriptedChoiceProvider();
            powerSink.ChooseHand(ranger.LeftHand);      // 强化那只 2

            var attack = new AttackContext(ranger, CharacterFactory.CreatePlayer("X", CharacterDefinition.Blank),
                                           ranger.RightHand, 4)
            {
                SourceHandValueBefore = 4,
                TargetHandValueBefore = 1,
                AttackerProvider = powerSink,
            };
            attack.Resolve(powerSink, null);

            Assert.AreEqual(3, ranger.LeftHand.Value,
                "闪避留下的那只 2 应能用于强化（1 -> 2 -> 3，一路用下去）。");
            Assert.True(attack.DamageMultiplier > 1f, "强化应生效。");
        }
    }
}
