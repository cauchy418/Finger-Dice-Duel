using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **盗贼**的偷窃。
    ///
    /// 规则全部由玩家逐条确认（含 9 个澄清问题的答复）：
    ///   · 双方点数**与状态**互换，但只有盗贼可能触发效果
    ///   · **偷窃前的点数 ≥ 偷来的点数**才生效
    ///   · 效果由**偷来的数字**决定（4 攻击、5 合盾、9 回复）
    ///   · 偷来 5 一律合成**完整盾**（不管那 5 之前是什么状态）
    ///   · 被偷者只被改点数，**不形成盾**
    ///   · 上回合**发起过**（点过按钮）则本回合不能再偷
    ///   · 无限距离、可以偷队友
    /// </summary>
    [TestFixture]
    public class ThiefTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match(out PlayerState thief, out PlayerState foe)
        {
            thief = Make("贼", CharacterDefinition.Thief);
            foe = Make("敌", CharacterDefinition.Soldier);

            return new GameManager(thief, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        [Test]
        public void Thief_stats_and_range()
        {
            PlayerState t = Make("贼", CharacterDefinition.Thief);

            Assert.AreEqual(0, t.Character.AttackBonus);
            Assert.AreEqual(-1, t.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(t), "盗贼攻击距离 1。");
        }

        [Test]
        public void Steal_swaps_both_values()
        {
            // 玩家确认："双方点数互换"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.AreEqual(4, thief.LeftHand.Value, "盗贼应拿到偷来的 4。");
            Assert.AreEqual(9, foe.LeftHand.Value, "被偷者应拿到盗贼原来的 9。");
        }

        [Test]
        public void Steal_is_effective_when_original_is_not_smaller()
        {
            // 玩家给的判定："偷窃前的数字**大于等于**偷窃后的数字即生效"
            // 例：原 9 偷 4 -> 生效 -> 4 是攻击
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            int before = foe.Health;
            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.True(r.StealEffective, "9 ≥ 4，应生效。");
            Assert.AreEqual(HandEffectType.Attack4, r.Effect, "偷来的 4 -> 攻击。");
            Assert.True(foe.Health < before, "应造成伤害。");
        }

        [Test]
        public void Steal_is_ineffective_when_original_is_smaller()
        {
            // 玩家给的例子："盗贼原先手里有个 2，拿这个 2 去偷了别人的 8，
            //               2 比 8 小，所以不会发起 8 的攻击"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 2);
            TestSetup.SetHand(foe.LeftHand, 8);

            int before = foe.Health;
            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.False(r.StealEffective, "2 < 8，不生效。");
            Assert.AreEqual(8, thief.LeftHand.Value,
                "但交换照做（玩家确认：交换照做，只是没有效果）。");
            Assert.AreEqual(2, foe.LeftHand.Value);
            Assert.AreEqual(before, foe.Health, "不生效 -> 不该造成伤害。");
        }

        [Test]
        public void Steal_of_five_always_makes_a_whole_shield()
        {
            // 玩家给的例子："盗贼原先手里有个 7，拿 7 去偷了别人的 5，
            //               7 比 5 大，**无论之前这个 5 是什么状态**，
            //               盗贼都会合成一面盾牌"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            // 把那 5 弄成"破碎的盾"，验证它仍然合成**完整**盾
            TestSetup.SetHand(foe.LeftHand, 5);
            foe.LeftHand.SetShieldState(ShieldState.Broken);

            TestSetup.SetHand(thief.LeftHand, 7);

            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.True(r.StealEffective, "7 ≥ 5，应生效。");
            Assert.AreEqual(HandEffectType.Shield, r.Effect, "偷来的 5 -> 合盾。");
            Assert.AreEqual(ShieldState.Formed, thief.LeftHand.State,
                "偷来的 5 一律合成**完整**盾（不管它之前是什么状态）。");
        }

        [Test]
        public void The_victim_does_not_form_a_shield()
        {
            // 玩家确认："被偷窃的人仅仅是被变换了点数，**不产生效果**"，
            // 而且"**不形成盾**（因为他没合盾）"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);     // 9 -> 盗贼的原值
            TestSetup.SetHand(foe.LeftHand, 5);       // 5 -> 会被换给被偷者？

            // 这里用"盗贼拿 9 偷 5"：盗贼得到 5 合盾，被偷者得到 9
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.AreEqual(9, foe.LeftHand.Value, "被偷者拿到盗贼原来的 9。");
            Assert.False(foe.LeftHand.IsShield,
                "被偷者不该因为拿到数字而形成盾（他没合盾）。");
        }

        [Test]
        public void Steal_states_swap_together_with_the_values()
        {
            // 玩家确认："盗贼以**完整的盾**去换别人**完整的盾**，
            //            会换来一面完整的盾而对面的盾破碎"
            //
            // 也就是"完整 vs 破碎"时，状态**跟着数值一起交换**：
            // 盗贼把自己那面完整盾给出去，换来对面那面碎的。
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            // ⚠️ 必须用**不同数值**才能观察状态交换 ——
            //    如果两边都是 5，那么"偷来的 5 一律合成完整盾"会生效，
            //    盗贼那边永远是完整盾，看不出交换
            //    （我第一版就是这么写的，白查一轮）。
            //
            //    玩家给的语义：盗贼以**完整的盾**去换别人**完整/破碎的盾**时，
            //    状态**跟着数值一起走**。
            TestSetup.SetHand(thief.LeftHand, 5);
            thief.LeftHand.SetShieldState(ShieldState.Formed);     // 盗贼：完整 5

            TestSetup.SetHand(foe.LeftHand, 3);
            foe.LeftHand.SetShieldState(ShieldState.Broken);       // 对面：破碎的 3

            // 5 >= 3 -> 判定生效（但偷来的 3 本身无效果）
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.AreEqual(3, thief.LeftHand.Value, "盗贼拿到 3。");
            Assert.AreEqual(5, foe.LeftHand.Value, "对面拿到盗贼原来的 5。");

            // 玩家确认："状态只在数值仍是 5 时保留" -> 3 不是盾，状态清掉
            Assert.AreEqual(ShieldState.None, thief.LeftHand.State,
                "3 不是盾，所以不保留任何盾状态。");

            // 玩家确认：被偷的人拿到完整盾时它**变成碎盾**
            //（"因为他没合盾"）
            Assert.AreEqual(ShieldState.Broken, foe.LeftHand.State,
                "对面拿到完整的 5 -> 它变成碎盾。");
        }

        [Test]
        public void Cannot_steal_twice_in_a_row()
        {
            // 玩家规则："若上回合**发起了**偷窃，本回合不能继续偷窃"，
            // 且"**只要点了偷窃就算'发起过'**"
            //
            // ⚠️ 用"同一回合号内再偷一次"来验证冷却 ——
            //    CanStealOn 判的是"上次偷窃的回合号 == 当前回合号"，
            //    所以同一回合内再偷同样被拦住
            //    （这正是"上回合偷过 -> 本回合不能偷"的最小复现）。
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            Assert.True(game.CanStealOn(thief), "还没偷过 -> 可以偷。");

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.False(game.CanStealOn(thief),
                "偷过之后（回合号相同）不能再偷。");

            Assert.Throws<System.InvalidOperationException>(
                () => game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand),
                "再偷应被拒绝并说明原因。");
        }

        [Test]
        public void Ineffective_steal_still_counts_as_initiated()
        {
            // 玩家确认："只要点了偷窃就算'发起过'"
            // 所以"偷了但判定不生效"也要进入冷却
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 8);       // 1 < 8 -> 不生效

            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);
            Assert.False(r.StealEffective, "前置：这次偷窃不生效。");

            Assert.False(game.CanStealOn(thief),
                "不生效也算'发起过'，同样要进冷却。");
        }

        [Test]
        public void Steal_of_four_attacks_the_victim()
        {
            // 玩家给的例子："盗贼原先手里有个 9，拿这个 9 去偷了别人的 4，
            //               9 比 4 大，所以会正常发起 4 的攻击"
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 4);

            int before = foe.Health;
            game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            // 盗贼 攻0、士兵 防+1 -> 4 - 1 = 3
            Assert.AreEqual(3, before - foe.Health, "4 点攻击 - 士兵防御 1 = 3 点伤害。");
        }

        [Test]
        public void No_effect_when_the_stolen_number_is_blank()
        {
            // 玩家确认："1/2/3/7 无效果（就跟平时打出 1/2/3/7 一样）"
            // 原 9 偷 3 -> 9 ≥ 3 生效，但偷来的 3 本身无效果
            GameManager game = Match(out PlayerState thief, out PlayerState foe);

            TestSetup.SetHand(thief.LeftHand, 9);
            TestSetup.SetHand(foe.LeftHand, 3);

            int before = foe.Health;
            TurnResult r = game.ExecuteSteal(thief, thief.LeftHand, foe.LeftHand);

            Assert.True(r.StealEffective, "9 ≥ 3，判定生效。");
            Assert.AreEqual(HandEffectType.None, r.Effect, "但偷来的 3 无效果。");
            Assert.AreEqual(before, foe.Health, "不该造成伤害。");
        }
    }
}
