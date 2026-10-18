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
    /// **术士（Sorcerer）**：每次攻击各 8% 概率改写成 10 种元素之一（共 80%），
    /// 余 20% 普攻；改写后挂对应状态；力场走穿盾；普攻 1 内钝击、1 外穿刺。
    /// </summary>
    [TestFixture]
    public class SorcererTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager Match(out PlayerState sorc, out PlayerState foe)
        {
            sorc = Make("术", CharacterDefinition.Sorcerer);
            foe = Make("战", CharacterDefinition.Soldier);
            return new GameManager(sorc, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        // ============================================================
        // 数值与射程
        // ============================================================

        [Test]
        public void Sorcerer_stats_and_infinite_range()
        {
            PlayerState p = Make("术", CharacterDefinition.Sorcerer);

            Assert.AreEqual(0, p.Character.AttackBonus);
            Assert.AreEqual(-1, p.Character.DefenseBonus);
            Assert.AreEqual(CharacterTemplate.InfiniteAttackRange,
                GameManager.BaseRangeOf(p), "术士射程无限。");
        }

        // ============================================================
        // 掷骰映射：10 种各 8% + 20% 普攻
        // ============================================================

        [Test]
        public void Each_element_occupies_an_eight_point_band()
        {
            // 0~7 雷鸣、8~15 强酸 …… 72~79 力场
            for (int i = 0; i < SorcererLogic.ElementCount; i++)
            {
                int lo = i * SorcererLogic.ElementChancePercent;
                int hi = lo + SorcererLogic.ElementChancePercent - 1;

                Assert.AreEqual(SorcererLogic.DamageTypesForRoll[i],
                    SorcererLogic.ElementOfRoll(lo), $"第 {i} 段的起点");
                Assert.AreEqual(SorcererLogic.DamageTypesForRoll[i],
                    SorcererLogic.ElementOfRoll(hi), $"第 {i} 段的终点");
            }
        }

        [Test]
        public void The_last_twenty_percent_stays_a_plain_attack()
        {
            for (int roll = 80; roll < 100; roll++)
            {
                Assert.AreEqual(DamageType.None, SorcererLogic.ElementOfRoll(roll),
                    $"掷出 {roll} 应当是普攻。");
            }
        }

        [Test]
        public void Bands_total_eighty_percent()
        {
            int hits = 0;
            for (int roll = 0; roll < SorcererLogic.RollFaces; roll++)
                if (SorcererLogic.ElementOfRoll(roll) != DamageType.None) hits++;

            Assert.AreEqual(80, hits, "10 种 × 8% = 80% 应当落在元素上。");
        }

        [Test]
        public void All_ten_elements_are_reachable()
        {
            var seen = new System.Collections.Generic.List<DamageType>();
            for (int i = 0; i < SorcererLogic.ElementCount; i++)
                seen.Add(SorcererLogic.DamageTypesForRoll[i]);

            Assert.AreEqual(10, seen.Count, "应当正好 10 种元素。");
            Assert.IsTrue(seen.Contains(DamageType.Thunder));
            Assert.IsTrue(seen.Contains(DamageType.Force), "力场必须在列表里。");
            Assert.IsTrue(seen.Contains(DamageType.Psychic));
        }

        // ============================================================
        // 结算：改写类型 / 挂状态 / 力场穿透
        // ============================================================

        [Test]
        public void A_forced_roll_rewrites_the_attack_type_and_status()
        {
            GameManager game = Match(out PlayerState sorc, out PlayerState foe);
            var logic = (SorcererLogic)sorc.Character;

            logic.ForcedRoll = 8;                 // 强酸
            sorc.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);             // 3 + 1 = 4 -> 攻击

            TurnResult t = game.ExecuteTurn(sorc, sorc.LeftHand, foe.LeftHand);

            Assert.AreEqual(AttackDamageType.Acid, t.AttackType,
                "掷到强酸时攻击类型应当是强酸。");
            Assert.IsTrue(foe.HasStatus(StatusEffect.Acid),
                "命中后应当挂上强酸状态。");
        }

        [Test]
        public void A_plain_roll_leaves_the_normal_attack_and_no_status()
        {
            GameManager game = Match(out PlayerState sorc, out PlayerState foe);
            var logic = (SorcererLogic)sorc.Character;

            logic.ForcedRoll = 95;                // 20% 普攻区
            sorc.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);

            TurnResult t = game.ExecuteTurn(sorc, sorc.LeftHand, foe.LeftHand);

            // 1v1 距离 1 -> 钝击
            Assert.AreEqual(AttackDamageType.Bludgeoning, t.AttackType,
                "没改写时，距离 1 以内应当是钝击。");
            Assert.IsFalse(foe.HasStatus(StatusEffect.Acid));
            Assert.IsFalse(foe.HasStatus(StatusEffect.Poison));
        }

        [Test]
        public void A_force_roll_pierces_the_shield()
        {
            GameManager game = Match(out PlayerState sorc, out PlayerState foe);
            var logic = (SorcererLogic)sorc.Character;

            logic.ForcedRoll = 72;                // 力场
            sorc.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);             // 4

            // 给对手一面成型的盾，正常会被挡下
            foe.RightHand.SetValue(5);
            foe.RightHand.SetShieldState(ShieldState.Formed);

            TurnResult t = game.ExecuteTurn(sorc, sorc.LeftHand, foe.LeftHand);

            Assert.AreEqual(AttackDamageType.Force, t.AttackType,
                "掷到力场时攻击类型应当是力场。");
            Assert.IsFalse(t.WasBlocked, "力场穿盾，不该被挡下。");
            Assert.IsTrue(t.DamageDealt > 0, "力场应当造成伤害。");
        }

        [Test]
        public void Force_does_not_apply_a_status()
        {
            GameManager game = Match(out PlayerState sorc, out PlayerState foe);
            var logic = (SorcererLogic)sorc.Character;

            logic.ForcedRoll = 79;                // 力场段的终点
            sorc.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);

            game.ExecuteTurn(sorc, sorc.LeftHand, foe.LeftHand);

            // 力场只有穿透，不挂状态 —— 检查那 9 种状态一个都没上
            StatusEffect[] all =
            {
                StatusEffect.Resonance, StatusEffect.Acid, StatusEffect.Poison,
                StatusEffect.Blind, StatusEffect.Decay, StatusEffect.Burn,
                StatusEffect.Freeze, StatusEffect.Shock, StatusEffect.Stun,
            };
            for (int i = 0; i < all.Length; i++)
                Assert.IsFalse(foe.HasStatus(all[i]),
                    $"力场不该挂 {all[i]}。");
        }

        [Test]
        public void Force_cannot_be_dodged()
        {
            GameManager game = Match(out PlayerState sorc, out PlayerState foe);
            var logic = (SorcererLogic)sorc.Character;

            // 对手换成游侠并给他一张 1 用来闪避
            PlayerState ranger = Make("游", CharacterDefinition.Ranger);
            ranger.LeftHand.SetValue(1);
            ranger.RightHand.SetValue(1);
            game = new GameManager(sorc, ranger, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());

            logic.ForcedRoll = 72;                // 力场
            sorc.LeftHand.SetValue(3);
            ranger.LeftHand.SetValue(1);          // 3 + 1 = 4

            TurnResult t = game.ExecuteTurn(sorc, sorc.LeftHand, ranger.LeftHand);

            Assert.IsFalse(t.WasDodged, "力场不可被闪避（与咒术师同源）。");
        }
    }
}
