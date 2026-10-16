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
    /// **双将模式骨架**（玩家确认的合并规则）：
    ///   · 两名必须**不同角色**、**同一阵营序列**（守序 / 中立 / 混乱）
    ///   · 攻/防加成：**相加**
    ///   · 体力上限 / 攻击距离 / 攻击段数：**取最大**
    ///   · 战报只显示**主将**名
    /// </summary>
    [TestFixture]
    public class DualGeneralTests
    {
        [Test]
        public void Two_roles_must_differ()
        {
            bool threw = false;
            try
            {
                DualGeneralFactory.Create("甲", CharacterDefinition.Soldier,
                                          CharacterDefinition.Soldier);
            }
            catch (System.ArgumentException) { threw = true; }

            Assert.True(threw, "同一名角色不能组成双将。");
        }

        [Test]
        public void Two_roles_must_share_the_same_axis()
        {
            // 搜查：守序（战士）+ 混乱（巨人）应被拒绝
            bool threw = false;
            try
            {
                DualGeneralFactory.Create("甲", CharacterDefinition.Fighter,
                                          CharacterDefinition.Giant);
            }
            catch (System.ArgumentException) { threw = true; }

            Assert.True(threw, "不同阵营不能组成双将。");
        }

        [Test]
        public void Same_axis_pair_is_accepted()
        {
            // 守序：战士 + 剑客
            PlayerState p = DualGeneralFactory.Create("甲", CharacterDefinition.Fighter,
                                                      CharacterDefinition.Swordsman);
            Assert.NotNull(p, "同阵营应能组成双将。");
        }

        [Test]
        public void Attack_and_defense_bonuses_are_summed()
        {
            // 圣武士（守序）+ 骑士（守序）
            PlayerState dual = DualGeneralFactory.Create("双", CharacterDefinition.Paladin,
                                                         CharacterDefinition.Knight);
            PlayerState soloA = CharacterFactory.CreatePlayer("a", CharacterDefinition.Paladin);
            PlayerState soloB = CharacterFactory.CreatePlayer("b", CharacterDefinition.Knight);

            Assert.AreEqual(soloA.Character.AttackBonus + soloB.Character.AttackBonus,
                dual.Character.AttackBonus, "攻击加成应相加。");
            Assert.AreEqual(soloA.Character.DefenseBonus + soloB.Character.DefenseBonus,
                dual.Character.DefenseBonus, "防御加成应相加。");
        }

        [Test]
        public void Max_health_takes_the_larger_not_the_sum()
        {
            // 狼人（混乱，有 +5 生命）+ 巨人（混乱，+5）
            PlayerState dual = DualGeneralFactory.Create("双", CharacterDefinition.Werewolf,
                                                         CharacterDefinition.Giant);
            int solo = CharacterFactory.MaxHealthOf(CharacterDefinition.Werewolf);
            int other = CharacterFactory.MaxHealthOf(CharacterDefinition.Giant);
            int expected = solo > other ? solo : other;

            Assert.AreEqual(expected, dual.MaximumHealth,
                "生命上限应**取最大**，不是相加。");
        }

        [Test]
        public void Attack_range_takes_the_larger()
        {
            // 守序：魔射手（射程 3）+ 骑士（近战）
            PlayerState dual = DualGeneralFactory.Create("双", CharacterDefinition.ArcaneArcher,
                                                         CharacterDefinition.Knight);

            Assert.AreEqual(GameManager.BaseRangeOf(
                    CharacterFactory.CreatePlayer("x", CharacterDefinition.ArcaneArcher)),
                GameManager.BaseRangeOf(dual),
                "攻击距离应取较大者。");
        }

        [Test]
        public void Display_name_is_the_primary()
        {
            PlayerState dual = DualGeneralFactory.Create("双", CharacterDefinition.Fighter,
                                                         CharacterDefinition.Swordsman);
            Assert.AreEqual(CharacterDefinition.Fighter.DisplayName,
                ((CharacterTemplate)dual.Character).DisplayName, "战报只显示主将名。");
        }

        [Test]
        public void Partners_exclude_self_and_stay_on_the_same_axis()
        {
            List<CharacterDefinition> partners =
                DualGeneralFactory.PartnersFor(CharacterDefinition.Soldier);

            Assert.True(partners.Count > 0, "应有可搭配的副将。");

            for (int i = 0; i < partners.Count; i++)
            {
                Assert.False(ReferenceEquals(partners[i], CharacterDefinition.Soldier),
                    "副将候选不应包含主将自己。");
                Assert.True(DualGeneralFactory.SameAxis(CharacterDefinition.Soldier, partners[i]),
                    "副将候选必须同阵营。");
            }
        }

        /// <summary>
        /// **没有子角色会改写时，必须返回原效果**。
        ///
        /// 这条测试钉住一个真实线上 bug：
        ///   <c>GameManager</c> 用 <c>rewritten != effect</c> 判“是否被改写”，
        ///   所以返回 <c>None</c> 的含义是**“把效果改成无效果”**
        ///   （巨人的 4/6/8 正是这么用的），而不是“没改写”。
        ///
        ///   我第一版在“两名子角色都不实现改写器”时返回了 None，
        ///   于是**游侠 + 邪术师的 8 变成了无效果**（玩家实测：8 不攻击）。
        /// </summary>
        [Test]
        public void Effect_rewrite_falls_back_to_the_original_when_no_part_rewrites()
        {
            // 游侠与邪术师**都不实现** IHandEffectRewriter
            PlayerState me = DualGeneralFactory.Create("甲", CharacterDefinition.Ranger,
                                                       CharacterDefinition.Warlock);

            var rewriter = me.Character as IHandEffectRewriter;
            Assert.NotNull(rewriter, "容器必须实现 IHandEffectRewriter。");

            int[] values = { 4, 5, 6, 7, 8, 9 };
            for (int i = 0; i < values.Length; i++)
            {
                int v = values[i];
                var hand = new Hand(v);
                hand.SetValue(v);

                HandEffectType expected = Hand.GetEffectType(v);
                HandEffectType got = rewriter.RewriteEffect(me, hand, 5, null);

                Assert.AreEqual(expected, got,
                    $"没有改写器时 v{v} 应保持原效果（实际 {got}）。");
            }
        }

        [Test]
        public void Giant_partner_still_suppresses_its_own_numbers()
        {
            // 巨人的 4/6/8 确实要变成“无效果”，不能被上一条修正误伤。
            PlayerState me = DualGeneralFactory.Create("甲", CharacterDefinition.Giant,
                                                       CharacterDefinition.Assassin);
            var rewriter = (IHandEffectRewriter)me.Character;

            int[] dead = { 4, 6, 8 };
            for (int i = 0; i < dead.Length; i++)
            {
                var hand = new Hand(dead[i]);
                hand.SetValue(dead[i]);
                Assert.AreEqual(HandEffectType.None, rewriter.RewriteEffect(me, hand, 5, null),
                    $"巨人的 {dead[i]} 应为无效果。");
            }

            // 5 变成两段攻击
            var five = new Hand(5);
            five.SetValue(5);
            Assert.AreEqual(HandEffectType.Attack6, rewriter.RewriteEffect(me, five, 5, null),
                "巨人的 5 应为攻击。");
        }

        [Test]
        public void Random_pair_always_shares_the_axis_and_differs()
        {
            // 玩家确认："人机随机搭配不需要给人机选角色"。
            // 随机搭配必须**天然合法**（同阵营、不同角色），不靠重试。
            for (int i = 0; i < 200; i++)
            {
                Assert.True(DualGeneralFactory.TryRandomPair(out CharacterDefinition a,
                                                             out CharacterDefinition b),
                    "应能随机搭配出一对。");
                Assert.NotNull(a, "主将不应为空。");
                Assert.NotNull(b, "副将不应为空。");
                Assert.False(ReferenceEquals(a, b), "两名不能是同一个人。");
                Assert.True(DualGeneralFactory.SameAxis(a, b), "两名必须同阵营。");

                // 而且必须能真的建出来（不只是名字合法）
                Assert.NotNull(DualGeneralFactory.Create("甲", a, b),
                    "随机搭配出的一对必须可用。");
            }
        }

        [Test]
        public void A_dual_general_match_runs_and_resolves()
        {
            // 这是"双将能真正当一局打"的关键验证：
            // 光能建出来不算数，要能进 GameManager 并跑出正常结果。
            // 取各阵营一对代表性组合。
            var pairs = new[]
            {
                new[] { CharacterDefinition.Fighter, CharacterDefinition.Swordsman },
                new[] { CharacterDefinition.Monk, CharacterDefinition.Thief },
                new[] { CharacterDefinition.Giant, CharacterDefinition.Assassin },
            };

            for (int i = 0; i < pairs.Length; i++)
            {
                PlayerState mine = DualGeneralFactory.Create("甲", pairs[i][0], pairs[i][1]);
                PlayerState foe = DualGeneralFactory.Create("乙", pairs[i][1], pairs[i][0]);

                var choiceA = new GreedyChoiceProvider();
                var choiceB = new GreedyChoiceProvider();
                var game = new GameManager(mine, foe, choiceA, choiceB);

                MatchResult r = new MatchRunner().Run(game, choiceA, choiceB);

                Assert.True(r.Turns > 0,
                    $"双将对局应至少走一回合（{pairs[i][0].DisplayName}"
                    + $"+{pairs[i][1].DisplayName}，实际 {r.Turns}）。");
                Assert.False(r.HitTurnLimit,
                    $"双将对局不应撞回合上限（{pairs[i][0].DisplayName}"
                    + $"+{pairs[i][1].DisplayName}）。");
            }
        }

        [Test]
        public void Every_same_axis_pair_can_be_built()
        {
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            int built = 0;
            var failed = new List<string>();

            for (int i = 0; i < all.Length; i++)
            {
                for (int j = 0; j < all.Length; j++)
                {
                    if (i == j) continue;
                    if (!DualGeneralFactory.SameAxis(all[i], all[j])) continue;

                    try
                    {
                        PlayerState p = DualGeneralFactory.Create("双", all[i], all[j]);
                        if (p == null || p.Character == null) failed.Add(all[i].DisplayName + "+" + all[j].DisplayName);
                        else built++;
                    }
                    catch (System.Exception e)
                    {
                        failed.Add(all[i].DisplayName + "+" + all[j].DisplayName + ":" + e.GetType().Name);
                    }
                }
            }

            Assert.True(failed.Count == 0,
                "每个同阵营组合都应能构建：" + string.Join(",", failed));
            Assert.True(built >= 60,
                $"应构建出足够多的组合（实际 {built}）。");
        }
    }
}
