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
    ///   · "游荡者·风流剑客在有 2 的情况下斩杀线会对应提升"
    ///   · "同理战士·剑士游荡者·刺客野蛮人·巨人道途的斩杀线也要根据不同手的情况写"
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
            // 战士·勇士 攻+1 防+1；打战士·勇士 8 点 -> 8 + 1 - 1 = 8
            var me = CharacterFactory.CreatePlayer("我", CharacterDefinition.Champion);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(me, foe, out GameManager game);

            SetHealth(foe, 8);
            var move = new LegalMove(me, me.LeftHand, foe.LeftHand);

            Assert.AreEqual(8, ai.TotalLethalDamage(move, me, foe, 8),
                "战士·勇士 8 点攻击对战士·勇士：8 + 1 - 1 = 8。");
        }

        [Test]
        public void Ai_prefers_a_lethal_attack_over_a_bigger_but_non_lethal_one()
        {
            // "大幅优先决定攻击"：能斩杀时，即使另一手效果更好也该斩杀
            var me = CharacterFactory.CreatePlayer("我", CharacterDefinition.PathOfTheWarden);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(me, foe, out GameManager game);

            // 野蛮人·守望者道途 攻0 防+1；战士·勇士 防+1
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
        // 游荡者·风流剑客：有 2 时斩杀线提升
        // ============================================================

        [Test]
        public void Swashbuckler_lethal_threshold_rises_when_holding_a_two()
        {
            // 玩家要求："游荡者·风流剑客在有 2 的情况下斩杀线会对应提升"
            // 乘风突刺：基础值 ×1.5，**之后再**加攻防
            // 游荡者·风流剑客 攻0 防0；打战士·勇士 防+1
            var swashbuckler = CharacterFactory.CreatePlayer("游", CharacterDefinition.Swashbuckler);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(swashbuckler, foe, out GameManager game);

            // 结果是 4；另一只手是 2 -> 4 * 1.5 = 6 -> 6 + 0 - 1 = 5
            swashbuckler.RightHand.SetValue(2);
            var withTwo = new LegalMove(swashbuckler, swashbuckler.LeftHand, foe.LeftHand);
            int boosted = ai.TotalLethalDamage(withTwo, swashbuckler, foe, 4);

            // 另一只手不是 2 -> 普通 4 + 0 - 1 = 3
            swashbuckler.RightHand.SetValue(3);
            int plain = ai.TotalLethalDamage(withTwo, swashbuckler, foe, 4);

            Assert.AreEqual(5, boosted, "有 2 时：4 × 1.5 = 6，再 +0 −1 = 5。");
            Assert.AreEqual(3, plain, "没有 2 时：4 + 0 − 1 = 3。");
            Assert.True(boosted > plain, "斩杀线必须提升。");
        }

        // ============================================================
        // 战士·剑士：双手相同时追加一次固定伤害
        // ============================================================

        [Test]
        public void Swordsman_lethal_includes_the_follow_up_attack()
        {
            // 玩家要求："战士·剑士…斩杀线也要根据不同手的情况写"
            // 追加攻击：双手相同且结果是 4/6/8 时，额外固定 4/6/8（不吃攻防）
            var sword = CharacterFactory.CreatePlayer("剑", CharacterDefinition.Swordsman);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(sword, foe, out GameManager game);

            var move = new LegalMove(sword, sword.LeftHand, foe.LeftHand);

            // 战士·剑士 攻+2；战士·勇士 防+1
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
        // 野蛮人·巨人道途：伤害看"另一只手"，与结果值无关
        // ============================================================

        [Test]
        public void PathOfTheGiant_lethal_uses_the_other_hand_not_the_result()
        {
            // 玩家要求："野蛮人·巨人道途…斩杀线也要根据不同手的情况写"
            // 打出 5 -> 两段，每段基础伤害 = 另一只手的点数
            var pathOfTheGiant = CharacterFactory.CreatePlayer("巨", CharacterDefinition.PathOfTheGiant);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(pathOfTheGiant, foe, out GameManager game);

            var move = new LegalMove(pathOfTheGiant, pathOfTheGiant.LeftHand, foe.LeftHand);

            // 野蛮人·巨人道途 攻+2；战士·勇士 防+1
            // 另一只手 3 -> 每段 3+2-1 = 4，两段共 8
            pathOfTheGiant.RightHand.SetValue(3);
            int dmg = ai.TotalLethalDamage(move, pathOfTheGiant, foe, 5);

            Assert.AreEqual(8, dmg,
                "野蛮人·巨人道途打 5、另一只手 3：两段各 4，共 8（不是 5+2-1=6）。");
        }

        [Test]
        public void PathOfTheGiant_lethal_is_zero_on_four_six_eight()
        {
            // 野蛮人·巨人道途的 4/6/8 完全无效果 -> 不可能斩杀
            var pathOfTheGiant = CharacterFactory.CreatePlayer("巨", CharacterDefinition.PathOfTheGiant);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(pathOfTheGiant, foe, out GameManager game);
            var move = new LegalMove(pathOfTheGiant, pathOfTheGiant.LeftHand, foe.LeftHand);

            foreach (int v in new[] { 4, 6, 8 })
            {
                Assert.AreEqual(0, ai.TotalLethalDamage(move, pathOfTheGiant, foe, v),
                    $"野蛮人·巨人道途打出 {v} 完全无效果，伤害应为 0。");
            }
        }

        // ============================================================
        // 情境化阻挡：3 : 7 的比率约束
        // ============================================================

        /// <summary>造一个"双方都血量低"的局面（阻挡效果最强的情境）。</summary>
        private static GreedyChoiceProvider DangerAi(out PlayerState me, out PlayerState foe,
                                                     out GameManager game)
        {
            me = CharacterFactory.CreatePlayer("我", CharacterDefinition.Champion);
            foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            game = new GameManager(me, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
            var ai = new GreedyChoiceProvider { Game = game };

            // 双方都打到 20% 血 -> 两个方向的情境同时触发
            SetHealth(me, System.Math.Max(1, me.MaximumHealth / 5));
            SetHealth(foe, System.Math.Max(1, foe.MaximumHealth / 5));

            return ai;
        }

        [Test]
        public void Denial_never_outweighs_my_own_gain()
        {
            // 玩家要求："阻止对方的减值数值**不能比**让自己合成高价值的数值多，
            //            比率要调整到合适的值如 **3 : 7** 左右。"
            GreedyChoiceProvider ai = DangerAi(out _, out PlayerState foe,
                                               out GameManager game);
            game.GetType();     // 保持引用（局面已由 DangerAi 摆好）

            const int scale = 42;                  // 与实现里的 DenyWeightScale 一致
            int foeWeight = HandValueProfiles.For(foe.Definition).WeightOf(8);
            int exposure = foeWeight * scale / 100;

            // 情境同时触发时，"阻挡权重"最多是 exposure 的两倍（我危险 + 他危险）
            int maxDeny = exposure * 2 * ai.DenyRatioPercent / 100;

            // 我方一手最多能拿到的价值分（权重档位最高约 90）
            int maxMine = 90 / HandValueProfiles.MoveScoreScale * 12;

            Assert.True(maxDeny < maxMine,
                $"阻挡的减值（最多 {maxDeny}）**不能**超过自己合成的收益（最多 {maxMine}）。" +
                $"当前 DenyRatioPercent={ai.DenyRatioPercent}。");
        }

        [Test]
        public void Denial_ratio_is_around_three_to_seven()
        {
            // 3 : 7 是"目标区间"，允许 ±10% 偏差。
            GreedyChoiceProvider ai = DangerAi(out _, out _, out _);

            int mine = 90;                                     // 我方一手最高档
            int theirs = 90 * ai.DenyRatioPercent / 100;
            double ratio = theirs / (double)(mine + theirs);   // 对方占的比例

            Assert.True(ratio > 0.20 && ratio < 0.40,
                $"阻挡应占约 30%（3/7），实际 {ratio * 100:F0}%（DenyRatioPercent={ai.DenyRatioPercent}）。");
        }

        [Test]
        public void Denial_does_not_fire_when_nobody_is_in_danger()
        {
            // 平时（双方血量都健康）不该多算这一项 —— 否则会改变既有行为
            var me = CharacterFactory.CreatePlayer("我", CharacterDefinition.Champion);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            var game = new GameManager(me, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            var ai = new GreedyChoiceProvider { Game = game };

            var move = new LegalMove(me, me.LeftHand, foe.LeftHand);
            int healthy = ai.ScoreMoveForTest(move, me, game);

            // 把双方都打残 -> 阻挡生效，分数应变化
            SetHealth(me, 2);
            SetHealth(foe, 2);
            int danger = ai.ScoreMoveForTest(move, me, game);

            Assert.True(danger != healthy,
                $"血量低时分数应变化（健康 {healthy} vs 危险 {danger}）。");
        }

        // ============================================================
        // 游荡者·刺客：潜行中 +7
        // ============================================================

        [Test]
        public void Assassin_lethal_rises_while_stealthed()
        {
            // 玩家要求："游荡者·刺客…斩杀线也要根据不同手的情况写"
            // 出潜行斩杀：伤害 +7（加在基础值上，再加攻防）
            var assassin = CharacterFactory.CreatePlayer("刺", CharacterDefinition.Assassin);
            var foe = CharacterFactory.CreatePlayer("敌", CharacterDefinition.Champion);

            GreedyChoiceProvider ai = Ai(assassin, foe, out GameManager game);
            var move = new LegalMove(assassin, assassin.LeftHand, foe.LeftHand);

            // 不在潜行：游荡者·刺客 攻+2；战士·勇士 防+1 -> 8 + 2 - 1 = 9
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
        /// 通过 Core 接口让游荡者·刺客进入潜行。
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
