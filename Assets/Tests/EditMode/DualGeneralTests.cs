using System.Collections.Generic;
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
                DualGeneralFactory.Create("甲", CharacterDefinition.Warrior,
                                          CharacterDefinition.Giant);
            }
            catch (System.ArgumentException) { threw = true; }

            Assert.True(threw, "不同阵营不能组成双将。");
        }

        [Test]
        public void Same_axis_pair_is_accepted()
        {
            // 守序：战士 + 剑客
            PlayerState p = DualGeneralFactory.Create("甲", CharacterDefinition.Warrior,
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
            PlayerState dual = DualGeneralFactory.Create("双", CharacterDefinition.Warrior,
                                                         CharacterDefinition.Swordsman);
            Assert.AreEqual(CharacterDefinition.Warrior.DisplayName,
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
