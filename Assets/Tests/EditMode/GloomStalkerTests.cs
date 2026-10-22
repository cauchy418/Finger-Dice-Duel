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
    /// **游侠·幽域追踪者**（守序邪恶）的专项测试。
    ///
    /// 玩家给出的规则：
    ///   · 运算后若**两只手同时是 1/2/3 且数字相同**，可以（主动选择）
    ///     把**参与运算的那只手**暗置，进入伏击；
    ///   · 暗置的手不能发起运算、也不能被当成运算数字，但**数值可见**；
    ///   · 伏击时**攻击 +1**、**攻击距离 3**；
    ///   · 即将受到伤害时若在伏击，**恢复暗置的手并抵挡此次伤害**，
    ///     触发时机在**武僧·太极宗太极之后**；
    ///   · **法师·咒法学派穿透伏击**：既挡不住，也**不会解除**伏击。
    /// </summary>
    [TestFixture]
    public class GloomStalkerTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GloomStalkerLogic GloomStalkerOf(PlayerState p) => p.Character as GloomStalkerLogic;

        // ---------------------------------------------------------------
        // 伏击条件
        // ---------------------------------------------------------------

        [Test]
        public void Ambush_requires_both_hands_equal_and_within_one_to_three()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);

            // 2 + 2 -> 两手都是 2（且都用 1/2/3）-> 满足
            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(2);
            Assert.True(GloomStalkerLogic.MeetsAmbushCondition(gloomStalker), "双 2 应满足伏击条件。");

            // 3 + 3 -> 满足
            gloomStalker.LeftHand.SetValue(3);
            gloomStalker.RightHand.SetValue(3);
            Assert.True(GloomStalkerLogic.MeetsAmbushCondition(gloomStalker), "双 3 应满足伏击条件。");

            // 2 + 3 -> 不相等 -> 不满足
            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(3);
            Assert.False(GloomStalkerLogic.MeetsAmbushCondition(gloomStalker), "不同数字不应满足。");

            // 4 + 4 -> 超过 3 -> 不满足
            gloomStalker.LeftHand.SetValue(4);
            gloomStalker.RightHand.SetValue(4);
            Assert.False(GloomStalkerLogic.MeetsAmbushCondition(gloomStalker), "4 超出 1~3，不应满足。");
        }

        // ---------------------------------------------------------------
        // 暗置：两边都不能碰
        // ---------------------------------------------------------------

        [Test]
        public void Concealed_hand_cannot_be_operated_nor_be_an_addend()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);
            PlayerState foe = Make("敌", CharacterDefinition.Champion);

            gloomStalker.LeftHand.SetValue(3);
            gloomStalker.RightHand.SetValue(3);

            var game = new GameManager(gloomStalker, foe,
                                       new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());

            // 进入伏击：暗置左手
            GloomStalkerOf(gloomStalker).EnterAmbush(gloomStalker.LeftHand);

            Assert.True(gloomStalker.LeftHand.IsConcealed, "左手应已暗置。");
            Assert.False(gloomStalker.LeftHand.CanBeOperated, "暗置的手不能当运算源。");

            // 数值仍然可见
            Assert.AreEqual(3, gloomStalker.LeftHand.Value, "暗置后数值不应改变（对手看得到）。");

            // ① 不能当运算源
            IReadOnlyList<LegalMove> mine = game.GetLegalMoves(gloomStalker);
            for (int i = 0; i < mine.Count; i++)
                Assert.False(ReferenceEquals(mine[i].ActedHand, gloomStalker.LeftHand),
                    "暗置的手不应出现在自己的合法运算源里。");

            // ② 对手也不能碰它
            IReadOnlyList<LegalMove> theirs = game.GetLegalMoves(foe);
            for (int i = 0; i < theirs.Count; i++)
                Assert.False(ReferenceEquals(theirs[i].TargetHand, gloomStalker.LeftHand),
                    "暗置的手不应成为对手的被加数。");

            // ③ 校验层也要拒绝
            Assert.False(game.IsLegalMove(foe, foe.LeftHand, gloomStalker.LeftHand, out _),
                "校验层也应拒绝把暗置手当被加数。");
        }

        // ---------------------------------------------------------------
        // 伏击加成
        // ---------------------------------------------------------------

        [Test]
        public void Ambush_grants_attack_bonus_and_range_three()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);
            var logic = GloomStalkerOf(gloomStalker);

            Assert.AreEqual(0, gloomStalker.Character.AttackBonus, "未伏击时攻击加成为 0。");
            Assert.AreEqual(1, GameManager.BaseRangeOf(gloomStalker), "未伏击时距离 1。");

            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(2);
            logic.EnterAmbush(gloomStalker.LeftHand);

            Assert.AreEqual(GloomStalkerLogic.AmbushAttackBonus, gloomStalker.Character.AttackBonus,
                "伏击时攻击 +1。");
            Assert.AreEqual(GloomStalkerLogic.AmbushRange, GameManager.BaseRangeOf(gloomStalker),
                "伏击时攻击距离 3。");
        }

        // ---------------------------------------------------------------
        // 抵伤
        // ---------------------------------------------------------------

        /// <summary>跑一次攻击结算，返回防御方掉了多少血。</summary>
        private static int Attack(PlayerState attacker, PlayerState defender, int attackValue)
        {
            var ctx = new AttackContext(attacker, defender, attacker.LeftHand, attackValue);
            ctx.Resolve(null, null);
            return ctx.AppliedDamage;
        }

        [Test]
        public void Ambush_blocks_one_attack_and_is_consumed()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);
            PlayerState foe = Make("敌", CharacterDefinition.Champion);
            var logic = GloomStalkerOf(gloomStalker);

            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(2);
            logic.EnterAmbush(gloomStalker.LeftHand);

            int before = gloomStalker.Health;
            int dealt = Attack(foe, gloomStalker, 8);

            Assert.AreEqual(0, dealt, "伏击应完全挡下这次伤害。");
            Assert.AreEqual(before, gloomStalker.Health, "生命不应减少。");
            Assert.False(logic.IsAmbushing, "抵挡后伏击应被消耗。");
            Assert.False(gloomStalker.LeftHand.IsConcealed, "被暗置的手应恢复。");
            Assert.True(gloomStalker.LeftHand.CanBeOperated, "恢复后应能再次发起运算。");
        }

        [Test]
        public void Ambush_does_not_block_a_second_attack()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);
            PlayerState foe = Make("敌", CharacterDefinition.Champion);
            var logic = GloomStalkerOf(gloomStalker);

            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(2);
            logic.EnterAmbush(gloomStalker.LeftHand);

            Attack(foe, gloomStalker, 8);          // 第一下被挡
            int second = Attack(foe, gloomStalker, 8);   // 第二下应打实

            Assert.True(second > 0, "伏击只有一次，第二下应造成伤害。");
        }

        [Test]
        public void SchoolOfConjuration_pierces_the_ambush_without_releasing_it()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);
            PlayerState weaver = Make("咒", CharacterDefinition.SchoolOfConjuration);
            var logic = GloomStalkerOf(gloomStalker);

            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(2);
            logic.EnterAmbush(gloomStalker.LeftHand);

            int dealt = Attack(weaver, gloomStalker, 8);

            Assert.True(dealt > 0, "咒术攻击应穿透伏击造成伤害。");
            Assert.True(logic.IsAmbushing, "咒术攻击不应解除伏击。");
            Assert.True(gloomStalker.LeftHand.IsConcealed, "暗置状态应保持。");
        }

        [Test]
        public void Ambush_is_released_on_match_reset()
        {
            PlayerState gloomStalker = Make("猎", CharacterDefinition.GloomStalker);
            var logic = GloomStalkerOf(gloomStalker);

            gloomStalker.LeftHand.SetValue(2);
            gloomStalker.RightHand.SetValue(2);
            logic.EnterAmbush(gloomStalker.LeftHand);
            Assert.True(gloomStalker.LeftHand.IsConcealed, "前提：已暗置。");

            var game = new GameManager(gloomStalker, Make("敌", CharacterDefinition.Champion),
                                       new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());
            game.ResetMatch();

            Assert.False(gloomStalker.LeftHand.IsConcealed,
                "重开一局必须清掉暗置状态，否则第二局开局就有手不能碰。");
            Assert.False(logic.IsAmbushing, "重开一局伏击状态应清空。");
        }
    }
}
