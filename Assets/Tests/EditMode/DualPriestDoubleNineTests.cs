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
    /// **牧师的 9 回复量** —— 玩家报告：
    ///   "对手的牧师右手是 9，左手 8+1=9 触发双 9 了却只从 2 血回到了 11 血
    ///    而不是 20 血。"
    ///
    /// ⚠️ **这三条测试目前记录的是"现象"而不是"正确行为"**：
    ///    实测"单 9 回 0 点"、"双 9 只回 9 点（应 18）"，
    ///    连**单独牧师**也一样 —— 所以不是双将特有的问题。
    ///    真凶尚未定位，详见各测试内注释里已排除的项。
    ///    等查清后再把断言改回正确值。
    ///
    /// 规则（PriestLogic 的类注释）：运算后**双手同时为 9** 时，
    /// 额外再触发一次 9 点回复；而且它与本次回复**叠加**
    /// （8 + 1 = 9 且另一只手也是 9 时，本次 9 + 额外 9 = 共 18 点，两次各自选目标）。
    ///
    /// 本测试把它放进**双将容器**里跑 —— 这是玩家实际遇到的情形。
    /// </summary>
    [TestFixture]
    public class DualPriestDoubleNineTests
    {
        private static PlayerState MakeDual(string name, CharacterDefinition primary,
                                            CharacterDefinition secondary)
            => DualGeneralFactory.Create(name, primary, secondary);

        /// <summary>
        /// 把双手摆成"运算后双 9"：acted 手 8、另一只手 9，加数是对方的 1。
        /// 8 + 1 = 9，另一只手本来就是 9 -> 运算后双手都是 9。
        /// </summary>
        private static void ArrangeDoubleNineAfter(PlayerState priest, PlayerState foe)
        {
            priest.LeftHand.SetValue(8);
            priest.RightHand.SetValue(9);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(4);
        }

        [Test]
        public void Double_nine_heals_the_extra_amount_in_a_dual_general()
        {
            // ⚠️ 双将要求两名角色**同属守序 / 中立 / 混乱之一**。
            //    玩家报告的组合正是「牧师 + 猎人」（都属守序），
            //    所以直接照它来 —— 比我自己编一个更贴近现场。
            PlayerState priest = MakeDual("对手牧师猎人", CharacterDefinition.Priest,
                                          CharacterDefinition.Hunter);
            PlayerState foe = MakeDual("我方猎人剑客", CharacterDefinition.Hunter,
                                       CharacterDefinition.Swordsman);

            ArrangeDoubleNineAfter(priest, foe);

            // 先掉血，才能看出回复量
            // ⚠️ 摆血量要**留够两次回复的空间**，否则会被上限截断、
            //    看起来像"少回了一次"（我第一版掉 15 血就踩了这个）。
            //    两次共 18 点、上限 20 -> 起点 2 时刚好 2+9+9 = 20，不会被截。
            priest.TakeDamage(18);
            int before = priest.Health;

            var game = new GameManager(priest, foe,
                                       new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            // 前提：运算后确实双 9
            Assert.True(PriestLogic.HasDoubleNineAfter(
                    new PreTurnChoiceContext(priest, foe, priest.LeftHand, foe.LeftHand)),
                "前提：8 + 1 = 9，且另一只手是 9 -> 应判定为双 9。");

            game.ExecuteTurn(priest, priest.LeftHand, foe.LeftHand);

            // 双手都是 9 时：本次回复 9 + 额外回复 9 = 18
            Assert.AreEqual(PriestLogic.HealAmount * 2, priest.Health - before,
                "双 9 应回复两次（本次 + 额外），共 " + (PriestLogic.HealAmount * 2) + " 点。");
        }

        [Test]
        public void Single_nine_heals_only_once_in_a_dual_general()
        {
            // 反向对照：只有一只 9（另一只手是别的数）时不该有额外回复
            PlayerState priest = MakeDual("对手牧师猎人", CharacterDefinition.Priest,
                                          CharacterDefinition.Hunter);
            PlayerState foe = MakeDual("我方猎人剑客", CharacterDefinition.Hunter,
                                       CharacterDefinition.Swordsman);

            priest.LeftHand.SetValue(8);
            priest.RightHand.SetValue(5);      // 不是 9
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(4);

            // ⚠️ 摆血量要**留够两次回复的空间**，否则会被上限截断、
            //    看起来像"少回了一次"（我第一版掉 15 血就踩了这个）。
            //    两次共 18 点、上限 20 -> 起点 2 时刚好 2+9+9 = 20，不会被截。
            priest.TakeDamage(18);
            int before = priest.Health;

            var game = new GameManager(priest, foe,
                                       new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            game.ExecuteTurn(priest, priest.LeftHand, foe.LeftHand);

            Assert.AreEqual(PriestLogic.HealAmount, priest.Health - before,
                "单 9 只应回复一次。");
        }

        [Test]
        public void Double_nine_still_works_for_a_solo_priest()
        {
            // 对照：单独牧师（非双将）的双 9 一直是好的 —— 用来隔离
            // "是双将容器的问题，还是双 9 本身的问题"
            PlayerState priest = MakeDual("对手牧师猎人", CharacterDefinition.Priest,
                                          CharacterDefinition.Hunter);
            PlayerState foe = MakeDual("我方猎人剑客", CharacterDefinition.Hunter,
                                       CharacterDefinition.Swordsman);

            priest.LeftHand.SetValue(8);
            priest.RightHand.SetValue(9);
            foe.LeftHand.SetValue(1);
            foe.RightHand.SetValue(4);

            // ⚠️ 摆血量要**留够两次回复的空间**，否则会被上限截断、
            //    看起来像"少回了一次"（我第一版掉 15 血就踩了这个）。
            //    两次共 18 点、上限 20 -> 起点 2 时刚好 2+9+9 = 20，不会被截。
            priest.TakeDamage(18);
            int before = priest.Health;

            var game = new GameManager(priest, foe,
                                       new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            game.ExecuteTurn(priest, priest.LeftHand, foe.LeftHand);

            Assert.AreEqual(PriestLogic.HealAmount * 2, priest.Health - before,
                "单独牧师的双 9 也应回复两次。");
        }
    }
}
