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
    /// **猎人**（守序邪恶）的专项测试。
    ///
    /// 玩家给出的规则：
    ///   · 运算后若**两只手同时是 1/2/3 且数字相同**，可以（主动选择）
    ///     把**参与运算的那只手**暗置，进入伏击；
    ///   · 暗置的手不能发起运算、也不能被当成运算数字，但**数值可见**；
    ///   · 伏击时**攻击 +1**、**攻击距离 3**；
    ///   · 即将受到伤害时若在伏击，**恢复暗置的手并抵挡此次伤害**，
    ///     触发时机在**武僧太极之后**；
    ///   · **咒术师穿透伏击**：既挡不住，也**不会解除**伏击。
    /// </summary>
    [TestFixture]
    public class HunterTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static HunterLogic HunterOf(PlayerState p) => p.Character as HunterLogic;

        // ---------------------------------------------------------------
        // 伏击条件
        // ---------------------------------------------------------------

        [Test]
        public void Ambush_requires_both_hands_equal_and_within_one_to_three()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);

            // 2 + 2 -> 两手都是 2（且都用 1/2/3）-> 满足
            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            Assert.True(HunterLogic.MeetsAmbushCondition(hunter), "双 2 应满足伏击条件。");

            // 3 + 3 -> 满足
            hunter.LeftHand.SetValue(3);
            hunter.RightHand.SetValue(3);
            Assert.True(HunterLogic.MeetsAmbushCondition(hunter), "双 3 应满足伏击条件。");

            // 2 + 3 -> 不相等 -> 不满足
            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(3);
            Assert.False(HunterLogic.MeetsAmbushCondition(hunter), "不同数字不应满足。");

            // 4 + 4 -> 超过 3 -> 不满足
            hunter.LeftHand.SetValue(4);
            hunter.RightHand.SetValue(4);
            Assert.False(HunterLogic.MeetsAmbushCondition(hunter), "4 超出 1~3，不应满足。");
        }

        // ---------------------------------------------------------------
        // 暗置：两边都不能碰
        // ---------------------------------------------------------------

        [Test]
        public void Concealed_hand_cannot_be_operated_nor_be_an_addend()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            hunter.LeftHand.SetValue(3);
            hunter.RightHand.SetValue(3);

            var game = new GameManager(hunter, foe,
                                       new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());

            // 进入伏击：暗置左手
            HunterOf(hunter).EnterAmbush(hunter.LeftHand);

            Assert.True(hunter.LeftHand.IsConcealed, "左手应已暗置。");
            Assert.False(hunter.LeftHand.CanBeOperated, "暗置的手不能当运算源。");

            // 数值仍然可见
            Assert.AreEqual(3, hunter.LeftHand.Value, "暗置后数值不应改变（对手看得到）。");

            // ① 不能当运算源
            IReadOnlyList<LegalMove> mine = game.GetLegalMoves(hunter);
            for (int i = 0; i < mine.Count; i++)
                Assert.False(ReferenceEquals(mine[i].ActedHand, hunter.LeftHand),
                    "暗置的手不应出现在自己的合法运算源里。");

            // ② 对手也不能碰它
            IReadOnlyList<LegalMove> theirs = game.GetLegalMoves(foe);
            for (int i = 0; i < theirs.Count; i++)
                Assert.False(ReferenceEquals(theirs[i].TargetHand, hunter.LeftHand),
                    "暗置的手不应成为对手的被加数。");

            // ③ 校验层也要拒绝
            Assert.False(game.IsLegalMove(foe, foe.LeftHand, hunter.LeftHand, out _),
                "校验层也应拒绝把暗置手当被加数。");
        }

        // ---------------------------------------------------------------
        // 伏击加成
        // ---------------------------------------------------------------

        [Test]
        public void Ambush_grants_attack_bonus_and_range_three()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            var logic = HunterOf(hunter);

            Assert.AreEqual(0, hunter.Character.AttackBonus, "未伏击时攻击加成为 0。");
            Assert.AreEqual(1, GameManager.BaseRangeOf(hunter), "未伏击时距离 1。");

            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            logic.EnterAmbush(hunter.LeftHand);

            Assert.AreEqual(HunterLogic.AmbushAttackBonus, hunter.Character.AttackBonus,
                "伏击时攻击 +1。");
            Assert.AreEqual(HunterLogic.AmbushRange, GameManager.BaseRangeOf(hunter),
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
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            var logic = HunterOf(hunter);

            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            logic.EnterAmbush(hunter.LeftHand);

            int before = hunter.Health;
            int dealt = Attack(foe, hunter, 8);

            Assert.AreEqual(0, dealt, "伏击应完全挡下这次伤害。");
            Assert.AreEqual(before, hunter.Health, "生命不应减少。");
            Assert.False(logic.IsAmbushing, "抵挡后伏击应被消耗。");
            Assert.False(hunter.LeftHand.IsConcealed, "被暗置的手应恢复。");
            Assert.True(hunter.LeftHand.CanBeOperated, "恢复后应能再次发起运算。");
        }

        [Test]
        public void Ambush_does_not_block_a_second_attack()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            var logic = HunterOf(hunter);

            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            logic.EnterAmbush(hunter.LeftHand);

            Attack(foe, hunter, 8);          // 第一下被挡
            int second = Attack(foe, hunter, 8);   // 第二下应打实

            Assert.True(second > 0, "伏击只有一次，第二下应造成伤害。");
        }

        [Test]
        public void Curseweaver_pierces_the_ambush_without_releasing_it()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            PlayerState weaver = Make("咒", CharacterDefinition.Curseweaver);
            var logic = HunterOf(hunter);

            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            logic.EnterAmbush(hunter.LeftHand);

            int dealt = Attack(weaver, hunter, 8);

            Assert.True(dealt > 0, "咒术攻击应穿透伏击造成伤害。");
            Assert.True(logic.IsAmbushing, "咒术攻击不应解除伏击。");
            Assert.True(hunter.LeftHand.IsConcealed, "暗置状态应保持。");
        }

        [Test]
        public void Ambush_is_released_on_match_reset()
        {
            PlayerState hunter = Make("猎", CharacterDefinition.Hunter);
            var logic = HunterOf(hunter);

            hunter.LeftHand.SetValue(2);
            hunter.RightHand.SetValue(2);
            logic.EnterAmbush(hunter.LeftHand);
            Assert.True(hunter.LeftHand.IsConcealed, "前提：已暗置。");

            var game = new GameManager(hunter, Make("敌", CharacterDefinition.Soldier),
                                       new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());
            game.ResetMatch();

            Assert.False(hunter.LeftHand.IsConcealed,
                "重开一局必须清掉暗置状态，否则第二局开局就有手不能碰。");
            Assert.False(logic.IsAmbushing, "重开一局伏击状态应清空。");
        }
    }
}
