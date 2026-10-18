using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// AI 的开局选点策略：价值表 + 选点器。
    ///
    /// 这里逐条钉住**与玩家确认过的机制结论**，避免以后改表时把依据改没：
    ///   · 1/2/3/7 的基线高于 4/5/6/8/9（覆盖面与组合面都更高）
    ///   · 7 对狼人是死牌（"要有 7"与"要和为 7"互斥）
    ///   · 游侠的 1 高于 2；同时拿到 1 和 2 最优
    ///   · 面对咒术师时**我**的 5 贬值（他的攻击无视护盾）、游侠的 1 也贬值（不可闪避）
    ///   · 咒术师**自己**的 5 反而更值钱（他无视别人的盾）
    /// </summary>
    [TestFixture]
    public class DiceSplitAdvisorTests
    {
        private static readonly HandValueProfile Base = new HandValueProfile();

        // ============================================================
        // 基线排名
        // ============================================================

        [Test]
        public void One_two_three_seven_outrank_the_effect_values()
        {
            // 实测：1/2/3/7 的覆盖面 5/5、组合面 5/9；
            // 4/5/6/8/9 都是 4/5、4/9（各缺"自己"）。
            int[] top = { 1, 2, 3, 7 };
            int[] rest = { 4, 6, 8, 9 };

            for (int i = 0; i < top.Length; i++)
                for (int j = 0; j < rest.Length; j++)
                    Assert.True(Base.WeightOf(top[i]) > Base.WeightOf(rest[j]),
                        $"{top[i]} 的基线应高于 {rest[j]}。");
        }

        [Test]
        public void Five_gets_a_small_edge_because_it_is_a_shield()
        {
            // 5 的覆盖/组合面与 4/6/8/9 相同，但它**是护盾**（挡一次伤害），
            // 所以基线略高一点。
            Assert.True(Base.WeightOf(5) > Base.WeightOf(4));
            Assert.True(Base.WeightOf(5) > Base.WeightOf(9));
            Assert.True(Base.WeightOf(1) > Base.WeightOf(5), "但 1 仍高于 5。");
        }

        [Test]
        public void Every_value_can_reach_four_of_the_five_useful_effects_at_least()
        {
            // 硬事实：任意起手 N 与任意目标 T，满足 (N+o)%10 == T 的对手手值 o 唯一，
            // 所以"单个数字凑单个目标"的难度恒定；区分度来自覆盖面。
            int[] useful = { 4, 5, 6, 8, 9 };

            for (int n = 1; n <= 9; n++)
            {
                int hits = 0;
                for (int u = 0; u < useful.Length; u++)
                {
                    for (int o = 1; o <= 9; o++)
                    {
                        if (Hand.ComputeSum(n, o) == useful[u]) { hits++; break; }
                    }
                }

                Assert.True(hits >= 4, $"起手 {n} 至少应能凑到 4 个有用目标，实际 {hits}。");
            }
        }

        [Test]
        public void Nine_cannot_reach_nine_and_five_cannot_reach_five()
        {
            // 9+1=10→1，所以起手 9 **打不出 9**（牧师要特别注意）
            for (int o = 1; o <= 9; o++)
                Assert.False(Hand.ComputeSum(9, o) == 9, $"9+{o} 不该等于 9。");

            // 5+o=5 需要 o=0（不存在）
            for (int o = 1; o <= 9; o++)
                Assert.False(Hand.ComputeSum(5, o) == 5, $"5+{o} 不该等于 5。");

            // 但 8 可以凑到 9（8+1=9）
            Assert.AreEqual(9, Hand.ComputeSum(8, 1));
        }

        // ============================================================
        // 狼人：7 是死牌
        // ============================================================

        [Test]
        public void Seven_is_the_worst_value_for_the_werewolf()
        {
            // 玩家明确说过："对狼人来说 7 是废的，因为要和为 7 而不是有 7"
            HandValueProfile werewolf = HandValueProfiles.For(CharacterDefinition.Werewolf);

            for (int v = 1; v <= 9; v++)
            {
                if (v == 7) continue;
                Assert.True(werewolf.WeightOf(7) < werewolf.WeightOf(v),
                    $"狼人的 7 应低于 {v}。");
            }
        }

        [Test]
        public void Seven_plus_anything_never_returns_to_seven()
        {
            // 这是"7 对狼人是死牌"的数学依据
            for (int o = 1; o <= 9; o++)
                Assert.False(Hand.ComputeSum(7, o) == 7, $"7+{o} 不该等于 7。");
        }

        [Test]
        public void The_werewolf_prefers_splits_that_sum_to_seven()
        {
            HandValueProfile werewolf = HandValueProfiles.For(CharacterDefinition.Werewolf);

            // 3+4 = 7 -> 应明显好过 8+9
            int good = werewolf.Score(new HandSplit(3, 4));
            int bad = werewolf.Score(new HandSplit(8, 9));

            Assert.True(good > bad + 30,
                $"和为 7 的拆分应明显好于其他（{good} vs {bad}）。");
        }

        [Test]
        public void The_werewolf_dislikes_holding_a_seven()
        {
            HandValueProfile werewolf = HandValueProfiles.For(CharacterDefinition.Werewolf);

            // 3+4（和为 7）对狼人应好过 4+7（含 7，且 4+7=11→1 不是 7）
            int sumSeven = werewolf.Score(new HandSplit(3, 4));
            int holdsSeven = werewolf.Score(new HandSplit(4, 7));

            Assert.True(sumSeven > holdsSeven,
                $"狼人应偏好'和为 7'而不是'手里有 7'（{sumSeven} vs {holdsSeven}）。");
        }

        // ============================================================
        // 游侠：1 高于 2
        // ============================================================

        [Test]
        public void The_rangers_one_is_worth_more_than_two()
        {
            // 1 能闪避**并且**变成 2 之后还能强化 —— 两个技能的价值叠加在 1 上
            HandValueProfile ranger = HandValueProfiles.For(CharacterDefinition.Ranger);

            Assert.True(ranger.WeightOf(1) > ranger.WeightOf(2),
                $"游侠的 1 应高于 2（{ranger.WeightOf(1)} vs {ranger.WeightOf(2)}）。");
        }

        [Test]
        public void The_ranger_loves_holding_both_one_and_two()
        {
            HandValueProfile ranger = HandValueProfiles.For(CharacterDefinition.Ranger);

            int both = ranger.Score(new HandSplit(1, 2));
            int onlyOne = ranger.Score(new HandSplit(1, 8));
            int onlyTwo = ranger.Score(new HandSplit(2, 8));

            Assert.True(both > onlyOne, $"同时有 1 和 2 应最好（{both} vs {onlyOne}）。");
            Assert.True(both > onlyTwo, $"同时有 1 和 2 应最好（{both} vs {onlyTwo}）。");
        }

        // ============================================================
        // 咒术师：双向修正
        // ============================================================

        [Test]
        public void The_curseweaver_five_matches_the_soldier_but_attacks_rise()
        {
            // ⚠️ 玩家纠正："咒术师 5 **不必是最高价值**，
            //    **和士兵 5 持平即可**，他的**攻击数字权重应该上升**。"
            //
            //    原来的理由（"他无视别人的盾 -> 自己的盾净赚"）
            //    对他不成立：他自己也只能靠 5 合盾，与别人无异。
            HandValueProfile cw = HandValueProfiles.For(CharacterDefinition.Curseweaver);
            HandValueProfile soldier = HandValueProfiles.For(CharacterDefinition.Soldier);

            Assert.AreEqual(soldier.WeightOf(5), cw.WeightOf(5),
                $"咒术师的 5 应与士兵持平（{cw.WeightOf(5)} vs {soldier.WeightOf(5)}）。");

            // 攻击数字均上升（高于基线）
            Assert.True(cw.WeightOf(8) > Base.WeightOf(8), "咒术师的 8 应高于基线。");
            Assert.True(cw.WeightOf(6) > Base.WeightOf(6), "咒术师的 6 应高于基线。");
            Assert.True(cw.WeightOf(4) > Base.WeightOf(4), "咒术师的 4 应高于基线。");

            // 且按基础价值排序
            Assert.True(cw.WeightOf(8) > cw.WeightOf(6), "8 应高于 6。");
            Assert.True(cw.WeightOf(6) > cw.WeightOf(4), "6 应高于 4。");
        }

        [Test]
        public void Facing_the_curseweaver_my_five_is_worth_less()
        {
            // 他的攻击无视护盾 -> 我合盾完全白费
            HandValueProfile mine = HandValueProfiles.For(CharacterDefinition.Blank,
                                                          CharacterDefinition.Curseweaver);
            HandValueProfile normal = HandValueProfiles.For(CharacterDefinition.Blank,
                                                            CharacterDefinition.Barbarian);

            Assert.True(mine.WeightOf(5) < normal.WeightOf(5),
                $"面对咒术师时我的 5 应贬值（{mine.WeightOf(5)} vs {normal.WeightOf(5)}）。");
        }

        [Test]
        public void Facing_the_curseweaver_a_rangers_one_is_worth_less()
        {
            // 咒术师的攻击不可闪避（IDodgeBlocker.PreventsDodge）-> 游侠的 1 贬值
            HandValueProfile vsCw = HandValueProfiles.For(CharacterDefinition.Ranger,
                                                          CharacterDefinition.Curseweaver);
            HandValueProfile vsOther = HandValueProfiles.For(CharacterDefinition.Ranger,
                                                             CharacterDefinition.Blank);

            Assert.True(vsCw.WeightOf(1) < vsOther.WeightOf(1),
                $"面对咒术师时游侠的 1 应贬值（{vsCw.WeightOf(1)} vs {vsOther.WeightOf(1)}）。");
        }

        [Test]
        public void The_curseweaver_is_the_only_one_that_devalues_my_five()
        {
            // ⚠️ 规则在 2025 年被玩家扩展了，这条测试的**措辞**要跟着改：
            //
            //   原来："只有咒术师无视护盾 -> 只有面对他时 5 才贬值"
            //   现在：**咒术师依然是唯一"让我方盾完全无效"的对手**，
            //        但另有一类新对手会让我方主动**不想**合盾 ——
            //        因为合盾等于**送他们盾**（骑士靠碎盾成长、
            //        野蛮人靠合盾锁我的手、战士的两个技能都以盾为前提）。
            //
            //   所以这条测试现在验证的是**两种不同的贬值原因**：
            //     · 咒术师：盾**无效**（他无视护盾）
            //     · 骑士 / 野蛮人 / 战士：盾**资敌**（别给他凑 5）
            int baseline = HandValueProfiles.For(CharacterDefinition.Blank,
                                                 CharacterDefinition.Blank).WeightOf(5);

            Assert.True(HandValueProfiles.For(CharacterDefinition.Blank,
                                              CharacterDefinition.Curseweaver).WeightOf(5) < baseline);

            // 这一组是"盾完全白费 / 盾没有副作用"的对照组：他们**不该**让 5 贬值
            // ⚠️ 牧师不在这里 —— 他有一条"挡合双 9"的轻贬值（5 减 12），
            //    因为 4+5 / 3+6 / 2+7 / 1+8 都能凑出 9，而 9 是回 9 血。
            // ⚠️ 忍者不在这一组：
            //    玩家要求"针对忍者时**盾的价值相对下降**，
            //    因为他破盾效率高且一般总有一段能打到"。
            CharacterDefinition[] shieldIsFine =
            {
                CharacterDefinition.Ranger,
                CharacterDefinition.Werewolf,
                CharacterDefinition.Swordsman,
                CharacterDefinition.Assassin,
            };
            for (int i = 0; i < shieldIsFine.Length; i++)
            {
                Assert.AreEqual(baseline,
                    HandValueProfiles.For(CharacterDefinition.Blank, shieldIsFine[i]).WeightOf(5),
                    $"面对 {shieldIsFine[i].DisplayName} 时我的 5 不该贬值。");
            }

            // 这一组是"别送他盾"（玩家明确要求：挡合 5 的幅度大于挡 4/6/8）
            CharacterDefinition[] deniesTheirShield =
            {
                CharacterDefinition.Knight, CharacterDefinition.Barbarian,
                CharacterDefinition.Fighter,
            };
            for (int i = 0; i < deniesTheirShield.Length; i++)
            {
                int five = HandValueProfiles.For(CharacterDefinition.Blank,
                                                 deniesTheirShield[i]).WeightOf(5);
                int four = HandValueProfiles.For(CharacterDefinition.Blank,
                                                 deniesTheirShield[i]).WeightOf(4);

                Assert.True(five < baseline,
                    $"面对 {deniesTheirShield[i].DisplayName} 时，我的 5 应贬值（别送他盾）。");

                // 玩家明确要求：挡合 5 的幅度**大于**挡 4/6/8
                Assert.True(five < four,
                    $"面对 {deniesTheirShield[i].DisplayName} 时，" +
                    $"5 要比 4 贬得更多（{five} 应小于 {four}）。");
            }
        }

        // ============================================================
        // 剑客 / 刺客 / 忍者
        // ============================================================

        [Test]
        public void The_swordsman_wants_doubles_of_four_six_eight()
        {
            HandValueProfile fencer = HandValueProfiles.For(CharacterDefinition.Swordsman);

            Assert.True(fencer.Score(new HandSplit(8, 8)) > fencer.Score(new HandSplit(8, 9)),
                "双 8 应优于 8/9。");
            Assert.True(fencer.Score(new HandSplit(4, 4)) > fencer.Score(new HandSplit(4, 5)),
                "双 4 应优于 4/5。");
            Assert.True(fencer.Score(new HandSplit(5, 5)) < fencer.Score(new HandSplit(8, 8)),
                "双 5 不该触发追加攻击。");
        }

        [Test]
        public void The_assassin_values_sevens_because_they_are_the_stealth_seed()
        {
            HandValueProfile assassin = HandValueProfiles.For(CharacterDefinition.Assassin);

            Assert.True(assassin.WeightOf(7) > Base.WeightOf(7),
                "刺客的 7 应高于基线（潜行的种子）。");
            Assert.True(assassin.Score(new HandSplit(7, 7)) > assassin.Score(new HandSplit(8, 9)),
                "双 7 应最好。");
        }

        [Test]
        public void The_ninja_ranks_four_below_eight_but_not_below_baseline()
        {
            // ⚠️ 玩家纠正了我原来的误解：
            //    "忍者……**4 受技能增益其实和 8 一样**，
            //     8 比 4 好仅仅只是因为本身价值 8 就应该比 4 高。"
            //
            //    攻击拆两段后，**两段各自加一次攻防** 的增益
            //    对 4 与 8 是一样的（都是 +1 乘两），
            //    所以 4 不该被压到基线之下 —— 它只是比 8 低。
            HandValueProfile ninja = HandValueProfiles.For(CharacterDefinition.Ninja);

            Assert.True(ninja.WeightOf(4) > Base.WeightOf(4),
                "忍者的 4 不该低于基线（它同样享受两段增益）。");
            Assert.True(ninja.WeightOf(4) < ninja.WeightOf(8), "4 仍应低于 8。");
            Assert.True(ninja.WeightOf(6) < ninja.WeightOf(8), "6 应低于 8。");
            Assert.True(ninja.WeightOf(4) < ninja.WeightOf(6), "4 应低于 6（基础价值排序）。");
        }

        [Test]
        public void Facing_the_assassin_my_seven_is_worth_less()
        {
            HandValueProfile mine = HandValueProfiles.For(CharacterDefinition.Blank,
                                                          CharacterDefinition.Assassin);
            Assert.True(mine.WeightOf(7) < Base.WeightOf(7), "面对刺客时我的 7 应贬值。");
        }

        // ============================================================
        // 选点器：只给合法拆分
        // ============================================================

        [Test]
        public void The_advisor_always_returns_a_legal_split()
        {
            var advisor = new DiceSplitAdvisor();
            CharacterDefinition[] roster = CharacterDefinition.BuiltIn;

            for (int roll = 1; roll <= 20; roll++)
            {
                var legal = DiceRollSetup.LegalSplits(roll);

                for (int i = 0; i < roster.Length; i++)
                {
                    for (int j = 0; j < roster.Length; j++)
                    {
                        HandSplit pick = advisor.Choose(roster[i], roster[j], legal);

                        Assert.True(DiceRollSetup.IsLegalSplit(roll, pick),
                            $"投 {roll} 时 {roster[i].DisplayName} 选了非法拆分 {pick}。");
                    }
                }
            }
        }

        // ============================================================
        // 选点器：具体场景
        // ============================================================

        [Test]
        public void The_werewolf_picks_the_sum_to_seven_split_when_one_exists()
        {
            // ⚠️ 投 11 时**不存在**"和为 7"的拆分
            // （和为 7 只有 {1,6} {2,5} {3,4} 及镜像，它们都不和=11）。
            // 所以这条必须用投 7 来测。
            var advisor = new DiceSplitAdvisor();
            var legal = DiceRollSetup.LegalSplits(7);

            HandSplit pick = advisor.Choose(CharacterDefinition.Werewolf,
                                            CharacterDefinition.Blank, legal);

            Assert.AreEqual(7, Hand.ComputeSum(pick.Left, pick.Right),
                $"狼人应挑能凑 7 的拆分，实际选了 {pick}。");
            // 7 的合法拆分里 3/4 权重最高（3=72, 4=68 优于 1=75/6=45 与 2=72/5=45）
            Assert.AreEqual(3, pick.Left, $"最优应是 3/4，实际 {pick}。");
            Assert.AreEqual(4, pick.Right, $"最优应是 3/4，实际 {pick}。");
        }

        [Test]
        public void The_werewolf_avoids_holding_a_seven_when_no_sum_to_seven_exists()
        {
            // 投 11 时没有和为 7 的拆分，此时"7 是死牌"应主导：
            // 含 7 的 4/7 与 7/4 必须输给 2/9、3/8、8/3、9/2 这些不含 7 的。
            var advisor = new DiceSplitAdvisor();
            var legal = DiceRollSetup.LegalSplits(11);

            HandSplit pick = advisor.Choose(CharacterDefinition.Werewolf,
                                            CharacterDefinition.Blank, legal);

            Assert.False(pick.Left == 7 || pick.Right == 7,
                $"投 11 时狼人不该选含 7 的拆分（死牌），实际选了 {pick}。");

            HandValueProfile w = HandValueProfiles.For(CharacterDefinition.Werewolf);
            Assert.True(w.Score(pick) > w.Score(new HandSplit(4, 7)),
                $"选中的拆分应优于含 7 的 4/7，实际 {pick}。");
        }

        [Test]
        public void The_ranger_picks_a_split_containing_both_one_and_two_when_available()
        {
            var advisor = new DiceSplitAdvisor();
            HandSplit pick = advisor.Choose(CharacterDefinition.Ranger,
                                            CharacterDefinition.Blank,
                                            DiceRollSetup.LegalSplits(3));

            // 投 3 时只有一个合法拆分 1/2 —— 刚好是游侠最想要的
            Assert.AreEqual(1, pick.Left);
            Assert.AreEqual(2, pick.Right);
        }

        [Test]
        public void The_advisor_returns_something_for_every_character_and_roll()
        {
            var advisor = new DiceSplitAdvisor();

            for (int roll = 1; roll <= 20; roll++)
            {
                for (int i = 0; i < CharacterDefinition.BuiltIn.Length; i++)
                {
                    HandSplit pick = advisor.Choose(CharacterDefinition.BuiltIn[i], null,
                                                    DiceRollSetup.LegalSplits(roll));

                    Assert.True(pick.Left >= 1 && pick.Left <= 9, $"{roll}: 左手越界。");
                    Assert.True(pick.Right >= 1 && pick.Right <= 9, $"{roll}: 右手越界。");
                }
            }
        }

        [Test]
        public void My_own_value_dominates_the_opponents_preference()
        {
            // 玩家明确要求：优先最大化自己；只有两者相近时才看对手。
            //
            // 用"白板 vs 战士"构造：战士的 5 权重高达 95，
            // 所以对白板自身价值相近的两个拆分，若其中一个把 5 送给战士，就该避开。
            var advisor = new DiceSplitAdvisor();
            var legal = DiceRollSetup.LegalSplits(11);

            HandValueProfile mine = HandValueProfiles.For(CharacterDefinition.Blank,
                                                          CharacterDefinition.Fighter);
            HandValueProfile fighter = HandValueProfiles.ValueToOpponent(CharacterDefinition.Fighter);

            int bestMine = int.MinValue;
            for (int i = 0; i < legal.Count; i++)
                if (mine.Score(legal[i]) > bestMine) bestMine = mine.Score(legal[i]);

            HandSplit pick = advisor.Choose(CharacterDefinition.Blank,
                                            CharacterDefinition.Fighter, legal);

            // ① 自己因素优先：选中的必须离自己的最好成绩很近
            Assert.True(mine.Score(pick) >= bestMine - advisor.InfoAdvantageBand,
                $"不该为了压制对手而牺牲自己（选中 {pick} 得 {mine.Score(pick)}，" +
                $"最好 {bestMine}）。");

            // ② 相近时才看对手：含 5 的拆分送给战士的价值很高，应被避开
            bool pickHasFive = pick.Left == 5 || pick.Right == 5;
            Assert.False(pickHasFive,
                $"对白板而言 5/6 与 6/5 自身价值相近，但把 5 送给战士对他很有用，" +
                $"所以应避开。实际选了 {pick}（战士得 {fighter.Score(pick)} 分）。");
        }

        // ============================================================
        // 角色数据绑定（AI 选点全靠它查表）
        // ============================================================

        [Test]
        public void Every_created_player_carries_its_character_definition()
        {
            // AI 选点要按 CharacterDefinition.Id 查价值表，
            // 而 PlayerState.Character 只是"行为"，拿不到 Id —— 所以必须绑定 Definition。
            CharacterDefinition[] roster = CharacterDefinition.BuiltIn;

            for (int i = 0; i < roster.Length; i++)
            {
                PlayerState p = CharacterFactory.CreatePlayer("测试", roster[i]);

                Assert.NotNull(p.Definition,
                    $"{roster[i].DisplayName} 的玩家应带上 Definition。");
                Assert.AreEqual(roster[i].Id, p.Definition.Id,
                    $"{roster[i].DisplayName} 的 Definition.Id 应一致。");

                HandValueProfile profile = HandValueProfiles.For(p.Definition);
                Assert.NotNull(profile);
                Assert.True(profile.WeightOf(1) > 0, "价值表应有内容。");
            }
        }

        [Test]
        public void Configure_also_binds_the_definition()
        {
            // CharacterFactory.Configure 是另一条创建路径（用于复用已有 PlayerState）
            var p = new PlayerState("复用");
            CharacterFactory.Configure(p, CharacterDefinition.Assassin);

            Assert.NotNull(p.Definition);
            Assert.AreEqual(CharacterDefinition.AssassinId, p.Definition.Id);
            Assert.True(HandValueProfiles.For(p.Definition).WeightOf(7) > Base.WeightOf(7),
                "刺客的 7 应高于基线（说明查表命中了刺客档案）。");
        }

        // ============================================================
        // 2026 补的三个新角色：法师 / 术士 / 吟游诗人
        // ============================================================

        [Test]
        public void Wizard_values_attack_numbers_and_shield_above_baseline()
        {
            HandValueProfile wizard = HandValueProfiles.For(CharacterDefinition.Wizard);

            Assert.True(wizard.WeightOf(8) > wizard.WeightOf(6), "法师的 8 应高于 6。");
            Assert.True(wizard.WeightOf(6) > wizard.WeightOf(4), "法师的 6 应高于 4。");
            Assert.True(wizard.WeightOf(4) > Base.WeightOf(4), "法师的 4 应高于基线。");
            Assert.True(wizard.WeightOf(5) > Base.WeightOf(5),
                "法师防 −1，5 应高于基线。");
        }

        [Test]
        public void Sorcerer_values_attacks_and_keeps_nine_above_low_values()
        {
            HandValueProfile sorcerer = HandValueProfiles.For(CharacterDefinition.Sorcerer);

            Assert.True(sorcerer.WeightOf(8) > sorcerer.WeightOf(6), "术士的 8 应高于 6。");
            Assert.True(sorcerer.WeightOf(6) > sorcerer.WeightOf(4), "术士的 6 应高于 4。");
            Assert.True(sorcerer.WeightOf(5) > Base.WeightOf(5),
                "术士防 −1，5 应高于基线。");

            // 玩家明确要求：9 是有效保命手段，不能比 1/2/3/7 低。
            foreach (int low in new[] { 1, 2, 3, 7 })
                Assert.True(sorcerer.WeightOf(9) >= sorcerer.WeightOf(low),
                    $"术士的 9 不应低于 {low}（{sorcerer.WeightOf(9)} vs {sorcerer.WeightOf(low)}）。");
        }

        [Test]
        public void Bard_values_nine_as_heal_and_trigger()
        {
            HandValueProfile bard = HandValueProfiles.For(CharacterDefinition.Bard);

            Assert.True(bard.WeightOf(9) > Base.WeightOf(9),
                "吟游诗人的 9 是回血 + 触发窗口，应高于基线。");
            Assert.True(bard.WeightOf(9) >= bard.WeightOf(8),
                "9 至少不应低于 8（回血同时也触发增益）。");
            Assert.True(bard.WeightOf(5) > Base.WeightOf(5),
                "吟游诗人防 −1，5 应高于基线。");
            Assert.True(bard.WeightOf(8) > Base.WeightOf(8),
                "吟游诗人的 8 应高于基线。");
        }
    }
}
