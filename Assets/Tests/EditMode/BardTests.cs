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
    /// **吟游诗人（Bard）**：受伤 / 实际回血 -> 可发动；四项之一；
    /// 目标数决定强度 ±(4−X)；共用同一项；加在攻防加成层；下次发动替换。
    /// </summary>
    [TestFixture]
    public class BardTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager Match(out PlayerState bard, out PlayerState foe)
        {
            bard = Make("吟", CharacterDefinition.Bard);
            foe = Make("战", CharacterDefinition.Soldier);
            return new GameManager(bard, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        // ============================================================
        // 数值 / 射程 / 伤害类型
        // ============================================================

        [Test]
        public void Bard_stats_range_and_damage_type()
        {
            PlayerState p = Make("吟", CharacterDefinition.Bard);

            Assert.AreEqual(0, p.Character.AttackBonus);
            Assert.AreEqual(-1, p.Character.DefenseBonus);
            Assert.AreEqual(3, GameManager.BaseRangeOf(p), "吟游诗人攻击距离 3。");
            Assert.AreEqual(AttackDamageType.Piercing,
                ((BardLogic)p.Character).AttackTypeOf(null), "伤害类型穿刺。");
        }

        [Test]
        public void Strength_is_four_minus_the_target_count()
        {
            Assert.AreEqual(3, BardLogic.StrengthFor(1));
            Assert.AreEqual(2, BardLogic.StrengthFor(2));
            Assert.AreEqual(1, BardLogic.StrengthFor(3));
        }

        // ============================================================
        // 触发条件
        // ============================================================

        [Test]
        public void Only_damage_or_actual_healing_enables_the_skill()
        {
            GameManager game = Match(out PlayerState bard, out _);

            Assert.IsFalse(BardLogic.CanActivateFor(bard), "什么都没发生，不该能发动。");

            bard.TookDamageThisTurn = true;
            Assert.IsTrue(BardLogic.CanActivateFor(bard), "受过伤害就能发动。");

            bard.ResetTurnFlags();
            bard.HealedThisTurn = true;
            Assert.IsTrue(BardLogic.CanActivateFor(bard), "实际回过血就能发动。");
        }

        [Test]
        public void A_full_health_heal_does_not_enable_the_skill()
        {
            PlayerState bard = Make("吟", CharacterDefinition.Bard);
            bard.ResetTurnFlags();

            Assert.AreEqual(bard.MaximumHealth, bard.Health, "开局是满血。");
            bard.Heal(5);                                  // 满血回复 = 实际回 0

            Assert.IsFalse(bard.HealedThisTurn, "满血回复不算触发。");
            Assert.IsFalse(BardLogic.CanActivateFor(bard));
        }

        [Test]
        public void Taking_damage_flags_the_turn()
        {
            PlayerState bard = Make("吟", CharacterDefinition.Bard);
            bard.ResetTurnFlags();

            bard.TakeDamage(3);
            Assert.IsTrue(bard.TookDamageThisTurn);
        }

        // ============================================================
        // 生效：强度 / 共用同一项 / 加成层
        // ============================================================

        [Test]
        public void One_target_gets_plus_three_attack()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var logic = (BardLogic)bard.Character;

            bard.ResetTurnFlags();
            bard.TookDamageThisTurn = true;

            var targets = new List<PlayerState> { foe };
            int applied = logic.Activate(bard, game, BardLogic.BuffKind.BoostAttack, targets);

            Assert.AreEqual(1, applied);
            Assert.AreEqual(3, foe.BardAttackOffset, "1 个目标 -> 攻击 +3。");
            Assert.AreEqual(0, foe.BardDefenseOffset);

            // 士兵自身攻 +1，再加上吟游诗人的 +3 -> 4
            Assert.AreEqual(4, StatusRules.EffectiveAttackBonus(foe),
                "加成层要能读到（士兵原本 +1）。");
        }

        [Test]
        public void Weakening_defense_uses_the_same_magnitude()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var logic = (BardLogic)bard.Character;

            logic.Activate(bard, game, BardLogic.BuffKind.WeakenDefense,
                           new List<PlayerState> { foe });

            Assert.AreEqual(-3, foe.BardDefenseOffset, "削弱防御 -> 防御 -3。");
        }

        [Test]
        public void Two_targets_share_the_same_option_at_two()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var logic = (BardLogic)bard.Character;

            var targets = new List<PlayerState> { bard, foe };
            logic.Activate(bard, game, BardLogic.BuffKind.BoostDefense, targets);

            Assert.AreEqual(2, bard.BardDefenseOffset, "2 个目标 -> 防御 +2。");
            Assert.AreEqual(2, foe.BardDefenseOffset, "两个目标共用同一项。");
        }

        [Test]
        public void Activating_again_clears_the_previous_offsets()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var logic = (BardLogic)bard.Character;

            logic.Activate(bard, game, BardLogic.BuffKind.BoostAttack,
                           new List<PlayerState> { foe });
            Assert.AreEqual(3, foe.BardAttackOffset);

            // 第二次发动、换目标 -> 旧的全部移除
            logic.Activate(bard, game, BardLogic.BuffKind.WeakenAttack,
                           new List<PlayerState> { bard });

            Assert.AreEqual(0, foe.BardAttackOffset, "上一次给对手的加成应当被清除。");
            Assert.AreEqual(-3, bard.BardAttackOffset);
        }

        [Test]
        public void Clearing_on_death_removes_the_offsets()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var logic = (BardLogic)bard.Character;

            logic.Activate(bard, game, BardLogic.BuffKind.BoostAttack,
                           new List<PlayerState> { foe });

            logic.ClearOffsets(bard, game);

            Assert.AreEqual(0, foe.BardAttackOffset, "死亡时立即清除。");
        }

        [Test]
        public void Eligible_targets_include_self_and_stay_in_range()
        {
            // 2v2 环上：吟游诗人射程 3，四个座位都在范围内
            var seated = new List<PlayerState>
            {
                Make("P1吟", CharacterDefinition.Bard),
                Make("P2战", CharacterDefinition.Soldier),
                Make("P3剑", CharacterDefinition.Swordsman),
                Make("P4巨", CharacterDefinition.Giant),
            };
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(seated, providers);

            var logic = (BardLogic)seated[0].Character;
            List<PlayerState> list = logic.EligibleTargets(seated[0], game);

            Assert.AreEqual(4, list.Count, "射程 3 覆盖环上全部四人。");
            Assert.IsTrue(list.Contains(seated[0]), "自己也在候选里。");
        }

        [Test]
        public void The_offset_shows_up_in_real_damage()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var logic = (BardLogic)bard.Character;

            // 让战士挨打时多受 3 点（削弱防御 3）
            logic.Activate(bard, game, BardLogic.BuffKind.WeakenDefense,
                           new List<PlayerState> { foe });

            bard.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);              // 3 + 1 = 4 -> 4 点伤害
            TurnResult t = game.ExecuteTurn(bard, bard.LeftHand, foe.LeftHand);

            // 吟游诗人攻 0、士兵防 +1；被削弱防御 3 -> 净防御 -2
            // 伤害 = 4 + 0 - (-2) = 6（不削弱时是 4 + 0 - 1 = 3）
            Assert.AreEqual(6, t.DamageDealt,
                "削弱防御 3 之后这一击应当从 3 变成 6。");
        }
    }
}
