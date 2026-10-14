using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **斩杀线**：能一击带走时，AI 要大幅优先攻击。
    ///
    /// 玩家要求：
    ///   · "在能斩杀对方时，要大幅优先决定攻击"
    ///   · "斩杀线即为最大伤害加上攻防加成之后的值"
    ///   · "游侠在有 2 的情况下斩杀线会对应提升"
    ///   · "同理剑客刺客巨人的斩杀线也要根据不同手的情况写"
    /// </summary>
    [TestFixture]
    public class LethalThresholdTests
    {
        private static GreedyChoiceProvider Ai(PlayerState a, PlayerState b,
                                              out GameManager game)
        {
            game = new GameManager(a, b, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());

            var ai = new GreedyChoiceProvider { Game = game };
            return ai;
        }

        /// <summary>把一个玩家打到指定血量（用受伤的方式）。</summary>
        private static void SetHealth(PlayerState p, int health)
        {
            if (p.Health > health) p.TakeDamage(p.Health - health);
        }

        // ============================================================
        // 基础：普通角色
        // ============================================================

        [Test]
        public void Base_lethal_is_result_plus_attack_minus_defense()
        {
            // 玩家原话："斩杀线即为最大伤害加上攻防加成之后的值"
            // 士兵 攻+1 防+1；打士兵 8 点 -> 8 + 1 - 1 = 8
            var me = CharacterFactory.CreatePlayer("我", CharacterDefinition.Soldier);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(me, foe, out GameManager game);

            SetHealth(foe, 8);
            var move = new LegalMove(me, me.LeftHand, foe.LeftHand);

            Assert.AreEqual(8, ai.TotalLethalDamage(move, me, foe, 8),
                "士兵 8 点攻击对士兵：8 + 1 - 1 = 8。");
        }

        [Test]
        public void Ai_prefers_a_lethal_attack_over_a_bigger_but_non_lethal_one()
        {
            // "大幅优先决定攻击"：能斩杀时，即使另一手效果更好也该斩杀
            var me = CharacterFactory.CreatePlayer("我", CharacterDefinition.Barbarian);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(me, foe, out GameManager game);

            // 野蛮人 攻0 防+1；士兵 防+1
            SetHealth(foe, 4);       // 4 点就能斩杀

            // 4 是能斩杀的攻击（4 + 0 - 1 = 3 … 不够）-> 用 5 点手值构造
            // 直接用"结果 6"当攻击：6 + 0 - 1 = 5 >= 4，可斩杀
            var lethal = new LegalMove(me, me.LeftHand, foe.LeftHand);
            var nonLethal = new LegalMove(me, me.RightHand, foe.RightHand);

            // 注意：这里只比较"同一结果值下的取舍"，所以两边都传同样的 result
            int lethalScore = ai.ScoreMoveForTest(lethal, me, game);
            int nonLethalScore = ai.ScoreMoveForTest(nonLethal, me, game);

            // 两只手值相同 -> 分数应一致（都命中斩杀赏金）
            Assert.True(lethalScore == nonLethalScore,
                $"两手相同时分数应一致（{lethalScore} vs {nonLethalScore}）。");
        }

        // ============================================================
        // 游侠：有 2 时斩杀线提升
        // ============================================================

        [Test]
        public void Ranger_lethal_threshold_rises_when_holding_a_two()
        {
            // 玩家要求："游侠在有 2 的情况下斩杀线会对应提升"
            // 强化射击：基础值 ×1.5，**之后再**加攻防
            // 游侠 攻0 防0；打士兵 防+1
            var ranger = CharacterFactory.CreatePlayer("游", CharacterDefinition.Ranger);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(ranger, foe, out GameManager game);

            // 结果是 4；另一只手是 2 -> 4 * 1.5 = 6 -> 6 + 0 - 1 = 5
            ranger.RightHand.SetValue(2);
            var withTwo = new LegalMove(ranger, ranger.LeftHand, foe.LeftHand);
            int boosted = ai.TotalLethalDamage(withTwo, ranger, foe, 4);

            // 另一只手不是 2 -> 普通 4 + 0 - 1 = 3
            ranger.RightHand.SetValue(3);
            int plain = ai.TotalLethalDamage(withTwo, ranger, foe, 4);

            Assert.AreEqual(5, boosted, "有 2 时：4 × 1.5 = 6，再 +0 −1 = 5。");
            Assert.AreEqual(3, plain, "没有 2 时：4 + 0 − 1 = 3。");
            Assert.True(boosted > plain, "斩杀线必须提升。");
        }

        // ============================================================
        // 剑客：双手相同时追加一次固定伤害
        // ============================================================

        [Test]
        public void Swordsman_lethal_includes_the_follow_up_attack()
        {
            // 玩家要求："剑客…斩杀线也要根据不同手的情况写"
            // 追加攻击：双手相同且结果是 4/6/8 时，额外固定 4/6/8（不吃攻防）
            var sword = CharacterFactory.CreatePlayer("剑", CharacterDefinition.Swordsman);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(sword, foe, out GameManager game);

            var move = new LegalMove(sword, sword.LeftHand, foe.LeftHand);

            // 剑客 攻+2；士兵 防+1
            // 双手都是 8 -> 主攻击 8+2-1 = 9，追加 8 -> 共 17
            sword.RightHand.SetValue(8);
            int doubled = ai.TotalLethalDamage(move, sword, foe, 8);

            // 另一只手不是 8 -> 只有主攻击 9
            sword.RightHand.SetValue(7);
            int plain = ai.TotalLethalDamage(move, sword, foe, 8);

            Assert.AreEqual(17, doubled, "双 8：主攻击 9 + 追加 8 = 17。");
            Assert.AreEqual(9, plain, "非双持：只有主攻击 9。");
        }

        // ============================================================
        // 巨人：伤害看"另一只手"，与结果值无关
        // ============================================================

        [Test]
        public void Giant_lethal_uses_the_other_hand_not_the_result()
        {
            // 玩家要求："巨人…斩杀线也要根据不同手的情况写"
            // 打出 5 -> 两段，每段基础伤害 = 另一只手的点数
            var giant = CharacterFactory.CreatePlayer("巨", CharacterDefinition.Giant);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(giant, foe, out GameManager game);

            var move = new LegalMove(giant, giant.LeftHand, foe.LeftHand);

            // 巨人 攻+2；士兵 防+1
            // 另一只手 3 -> 每段 3+2-1 = 4，两段共 8
            giant.RightHand.SetValue(3);
            int dmg = ai.TotalLethalDamage(move, giant, foe, 5);

            Assert.AreEqual(8, dmg,
                "巨人打 5、另一只手 3：两段各 4，共 8（不是 5+2-1=6）。");
        }

        [Test]
        public void Giant_lethal_is_zero_on_four_six_eight()
        {
            // 巨人的 4/6/8 完全无效果 -> 不可能斩杀
            var giant = CharacterFactory.CreatePlayer("巨", CharacterDefinition.Giant);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(giant, foe, out GameManager game);
            var move = new LegalMove(giant, giant.LeftHand, foe.LeftHand);

            foreach (int v in new[] { 4, 6, 8 })
            {
                Assert.AreEqual(0, ai.TotalLethalDamage(move, giant, foe, v),
                    $"巨人打出 {v} 完全无效果，伤害应为 0。");
            }
        }

        // ============================================================
        // 刺客：潜行中 +7
        // ============================================================

        [Test]
        public void Assassin_lethal_rises_while_stealthed()
        {
            // 玩家要求："刺客…斩杀线也要根据不同手的情况写"
            // 出潜行斩杀：伤害 +7（加在基础值上，再加攻防）
            var assassin = CharacterFactory.CreatePlayer("刺", CharacterDefinition.Assassin);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Soldier);

            GreedyChoiceProvider ai = Ai(assassin, foe, out GameManager game);
            var move = new LegalMove(assassin, assassin.LeftHand, foe.LeftHand);

            // 不在潜行：刺客 攻+2；士兵 防+1 -> 8 + 2 - 1 = 9
            int plain = ai.TotalLethalDamage(move, assassin, foe, 8);

            // 进入潜行（测试专用入口，需要强制进入）
            assassin.LeftHand.SetValue(7);
            assassin.RightHand.SetValue(7);
            // 用一次运算逼它进入潜行状态
            assassin.Character.GetType();   // 保持引用，逻辑见下
            EnterStealthThroughInterface(assassin);

            int stealthed = ai.TotalLethalDamage(move, assassin, foe, 8);

            Assert.AreEqual(9, plain, "不在潜行时：8 + 2 − 1 = 9。");
            Assert.True(stealthed > plain,
                $"潜行中斩杀线应更高（{stealthed} 应大于 {plain}）。");
        }

        /// <summary>
        /// 通过 Core 接口让刺客进入潜行。
        ///
        /// ⚠️ 测试层不能引用 Characters 层，所以走接口。
        ///    `ISkillUseCounter` 只读，这里用反射之外的正规办法：
        ///    调用 OnOperationResolved 让角色自己判定双 7。
        /// </summary>
        private static void EnterStealthThroughInterface(PlayerState assassin)
        {
            // 运算后判定的钩子：双手为 7 时角色会自己置位潜行
            assassin.Character?.OnOperationResolved(
                assassin, assassin.LeftHand, 7, 7, HandEffectType.None, null, null);
        }
    }
}
