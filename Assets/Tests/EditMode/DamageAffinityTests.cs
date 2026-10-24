using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class DamageAffinityTests
    {
        private static GameManager Game(CharacterDefinition defender = null)
        {
            var game = CharacterFactory.CreateMatch(CharacterDefinition.Blank, defender ?? CharacterDefinition.Blank,
                new SimpleChoiceProvider(), new SimpleChoiceProvider());
            game.PlayerB.SetMaxHealth(200); game.PlayerB.SetHealthExact(200); return game;
        }
        private static AttackContext Hit(GameManager game, AttackDamageType type, int damage = 5, int hits = 1,
            StatusEffect status = StatusEffect.None)
        {
            var context = new AttackContext(game.PlayerA, game.PlayerB, game.PlayerA.LeftHand, damage)
            { AttackType = type, IgnoreStatBonuses = true, HitCount = hits, AttackerStatusOverride = status };
            context.Resolve(game.ChoiceProviderB, game); return context;
        }

        [Test] public void All_thirteen_vulnerabilities_multiply_real_damage_and_round_up()
        {
            foreach (var type in ResistanceNames.All)
            {
                var g = Game(); g.PlayerB.AddVulnerability(type);
                Assert.AreEqual(8, Hit(g, type).AppliedDamage, type.ToString());
                Assert.IsFalse(g.PlayerB.HasVulnerability(type));
            }
        }
        [Test] public void All_thirteen_immunities_negate_damage_and_magic_status_after_consumption()
        {
            foreach (var type in ResistanceNames.All)
            {
                var g = Game(); g.PlayerB.AddImmunity(type);
                var status = type == AttackDamageType.Psychic ? StatusEffect.Stunned : StatusNames.StatusOf(AttackDamageTypes.ToDamageType(type));
                var hit = Hit(g, type, status: status);
                Assert.AreEqual(0, hit.AppliedDamage, type.ToString()); Assert.IsTrue(hit.WasTypeImmune);
                Assert.IsFalse(g.PlayerB.HasImmunity(type)); Assert.AreEqual(0, g.PlayerB.Statuses.Count);
                Assert.AreEqual(1, hit.LandedHits, "免疫仍为落地攻击，不伪装成闪避");
            }
        }
        [Test] public void Different_types_are_independent_and_any_landed_attack_consumes_all_temporary_affinities()
        {
            var g = Game(); g.PlayerB.AddVulnerability(AttackDamageType.Fire); g.PlayerB.AddImmunity(AttackDamageType.Cold);
            g.PlayerB.AddResistance(AttackDamageType.Lightning); g.PlayerB.AddPermanentResistance(AttackDamageType.Radiant);
            Assert.AreEqual(5, Hit(g, AttackDamageType.Slashing).AppliedDamage);
            Assert.AreEqual(0, g.PlayerB.Vulnerabilities.Count); Assert.AreEqual(0, g.PlayerB.Immunities.Count);
            Assert.IsFalse(g.PlayerB.HasResistance(AttackDamageType.Lightning)); Assert.IsTrue(g.PlayerB.HasResistance(AttackDamageType.Radiant));
        }
        [Test] public void Immunity_has_priority_and_vulnerability_then_resistance_are_both_applied()
        {
            var g = Game(); g.PlayerB.AddVulnerability(AttackDamageType.Fire); g.PlayerB.AddResistance(AttackDamageType.Fire);
            Assert.AreEqual(4, Hit(g, AttackDamageType.Fire).AppliedDamage, "ceil(5×1.5)=8，抗性后4");
            g.PlayerB.AddVulnerability(AttackDamageType.Fire); g.PlayerB.AddResistance(AttackDamageType.Fire); g.PlayerB.AddImmunity(AttackDamageType.Fire);
            Assert.AreEqual(0, Hit(g, AttackDamageType.Fire).AppliedDamage);
        }
        [Test] public void Shield_block_preserves_affinities_and_immunity_also_prevents_on_block_magic_status()
        {
            var g = Game(); var p = g.PlayerB; p.LeftHand.SetValue(5); p.LeftHand.SetShieldState(ShieldState.Formed);
            p.AddImmunity(AttackDamageType.Fire); p.AddVulnerability(AttackDamageType.Cold); p.AddResistance(AttackDamageType.Acid);
            var hit = Hit(g, AttackDamageType.Fire, status: StatusEffect.Burn);
            Assert.IsTrue(hit.WasBlocked); Assert.IsTrue(p.HasImmunity(AttackDamageType.Fire));
            Assert.IsTrue(p.HasVulnerability(AttackDamageType.Cold)); Assert.IsTrue(p.HasResistance(AttackDamageType.Acid));
            Assert.IsFalse(p.HasStatus(StatusEffect.Burn));

            g = Game(CharacterDefinition.Swashbuckler); p = g.PlayerB;
            p.LeftHand.SetValue(1); p.RightHand.SetValue(2); p.ApplyStatus(StatusEffect.Poison, 1);
            p.AddVulnerability(AttackDamageType.Acid); p.AddImmunity(AttackDamageType.Cold);
            ((SimpleChoiceProvider)g.ChoiceProviderB).SetChoice(HandEffect.SwashbucklerDodge, p.LeftHand);
            hit = Hit(g, AttackDamageType.Acid);
            Assert.IsTrue(hit.WasDodged); Assert.IsTrue(p.HasVulnerability(AttackDamageType.Acid));
            Assert.IsTrue(p.HasImmunity(AttackDamageType.Cold)); Assert.AreEqual(1, p.StatusTurnsLeft(StatusEffect.Poison));
            Assert.IsFalse(p.HasStatus(StatusEffect.Acid));
        }
        [Test] public void All_segments_share_affinities_until_the_end_of_one_attack()
        {
            var g = Game(); g.PlayerB.AddVulnerability(AttackDamageType.Fire);
            Assert.AreEqual(10, Hit(g, AttackDamageType.Fire, 6, 2).AppliedDamage, "两段各ceil(3×1.5)=5");
            g.PlayerB.AddImmunity(AttackDamageType.Fire);
            var hit = Hit(g, AttackDamageType.Fire, 6, 2, StatusEffect.Burn);
            Assert.AreEqual(0, hit.AppliedDamage); Assert.AreEqual(2, hit.LandedHits); Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Burn));
        }
        [Test] public void Physical_immunity_does_not_immunize_the_separate_bleed_skill()
        {
            var g = Game(); g.PlayerB.AddImmunity(AttackDamageType.Slashing);
            var ctx = new AttackContext(g.PlayerA, g.PlayerB, g.PlayerA.LeftHand, 6)
            { AttackType = AttackDamageType.Slashing, InflictsBleed = true };
            ctx.Resolve(g.ChoiceProviderB, g); Assert.AreEqual(0, ctx.AppliedDamage); Assert.IsTrue(g.PlayerB.HasStatus(StatusEffect.Bleed));
        }
        [Test] public void Immune_fire_preserves_existing_frost_and_does_not_trigger_reaction()
        {
            var g = Game(); g.PlayerB.ApplyStatus(StatusEffect.Frost, 1); g.PlayerB.AddImmunity(AttackDamageType.Fire);
            Hit(g, AttackDamageType.Fire, status: StatusEffect.Burn);
            Assert.AreEqual(1, g.PlayerB.StatusTurnsLeft(StatusEffect.Frost)); Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Burn));
        }
        [Test] public void Immune_damage_does_not_spend_arcane_ward()
        {
            var g = Game(CharacterDefinition.SchoolOfAbjuration); ((IAbjurationSkill)g.PlayerB.Character).StoreBarrier(12);
            g.PlayerB.AddImmunity(AttackDamageType.Fire); Hit(g, AttackDamageType.Fire);
            Assert.AreEqual(12, ((IAbjurationSkill)g.PlayerB.Character).BarrierPoints);
        }
        [Test] public void Acid_on_poison_and_poison_on_acid_refresh_only_on_hit_and_preserve_source()
        {
            foreach (var type in new[] { AttackDamageType.Acid, AttackDamageType.Poison })
            {
                var g = Game(); var p = g.PlayerB;
                var old = type == AttackDamageType.Acid ? StatusEffect.Poison : StatusEffect.Acid;
                var added = type == AttackDamageType.Acid ? StatusEffect.Acid : StatusEffect.Poison;
                p.ApplyStatus(old, 1, p); var hit = Hit(g, type);
                Assert.AreEqual(5, hit.AppliedDamage); Assert.AreEqual(2, p.StatusTurnsLeft(old)); Assert.AreEqual(2, p.StatusTurnsLeft(added));
                Assert.AreEqual(p, p.StatusSourceOf(old)); Assert.AreEqual(g.PlayerA, p.StatusSourceOf(added));
                Assert.IsTrue(string.Join(" | ", g.TakeSkillLog()).Contains("刷新"));
                p.RemoveStatus(added); p.ApplyStatus(old, 1, p);
                Hit(g, type, 0);
                Assert.AreEqual(2, p.StatusTurnsLeft(old)); Assert.AreEqual(2, p.StatusTurnsLeft(added), "零伤害仍命中时保留反应");
            }
        }
        [Test] public void Acid_poison_reaction_works_through_shield_but_not_immunity()
        {
            var g = Game(); var p = g.PlayerB; p.ApplyStatus(StatusEffect.Poison, 1);
            p.LeftHand.SetValue(5); p.LeftHand.SetShieldState(ShieldState.Formed); Hit(g, AttackDamageType.Acid);
            Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.Poison)); Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.Acid));
            p.RemoveStatus(StatusEffect.Acid); p.ApplyStatus(StatusEffect.Poison, 1); p.AddImmunity(AttackDamageType.Acid);
            Hit(g, AttackDamageType.Acid); Assert.AreEqual(1, p.StatusTurnsLeft(StatusEffect.Poison)); Assert.IsFalse(p.HasStatus(StatusEffect.Acid));
        }
        [Test] public void Preview_of_element_reaction_does_not_refresh_or_add_statuses()
        {
            var g = Game(); g.PlayerB.ApplyStatus(StatusEffect.Poison, 1);
            StatusRules.ApplyDamageTypeInteraction(g.PlayerB, DamageType.Acid, 6, out var status, out var clear);
            Assert.AreEqual(1, g.PlayerB.StatusTurnsLeft(StatusEffect.Poison)); Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Acid));
        }
        [Test] public void Reset_and_cleanse_remove_vulnerability_while_positive_immunity_survives_cleanse()
        {
            var g = Game(); var p = g.PlayerB; p.AddVulnerability(AttackDamageType.Fire); p.AddImmunity(AttackDamageType.Cold);
            p.ClearStatuses(); Assert.IsFalse(p.HasVulnerability(AttackDamageType.Fire)); Assert.IsTrue(p.HasImmunity(AttackDamageType.Cold));
            p.Reset(); Assert.AreEqual(0, p.Immunities.Count); p.AddVulnerability(AttackDamageType.Fire); p.AddImmunity(AttackDamageType.Fire);
            p.ApplyStatus(StatusEffect.HolyLight); Assert.AreEqual(0, p.Vulnerabilities.Count); Assert.AreEqual(0, p.Immunities.Count);
            p.AddVulnerability(AttackDamageType.Cold); p.AddImmunity(AttackDamageType.Cold);
            Assert.AreEqual(0, p.Vulnerabilities.Count); Assert.AreEqual(0, p.Immunities.Count);
        }
        [Test] public void Unstoppable_poison_tick_respects_typed_immunity_and_vulnerability_without_spending_attack_tokens()
        {
            var g = Game(); var p = g.PlayerB; p.ApplyStatus(StatusEffect.Poison); p.AddVulnerability(AttackDamageType.Poison);
            Assert.AreEqual(3, StatusRules.TurnStartDamage(p, out var unstoppable)); Assert.IsTrue(unstoppable);
            p.AddImmunity(AttackDamageType.Poison); Assert.AreEqual(0, StatusRules.TurnStartDamage(p, out unstoppable));
            Assert.IsTrue(p.HasImmunity(AttackDamageType.Poison), "直接状态扣血不是落地攻击");
        }
        [Test] public void Guide_and_combat_icons_cover_thirty_nine_affinities_and_three_separate_marks()
        {
            var p = Game().PlayerB; foreach (var type in ResistanceNames.All) { p.AddResistance(type); p.AddVulnerability(type); p.AddImmunity(type); }
            var icons = CombatStatusIcons.For(p); Assert.AreEqual(39, icons.Count); var keys = new HashSet<string>();
            foreach (var icon in icons) { keys.Add(icon.ResourceKey); Assert.AreEqual("", icon.Counter); }
            Assert.AreEqual(39, keys.Count); var guide = GuideSheets.Protections(); Assert.AreEqual(39, guide.Lines.Count);
            Assert.AreEqual(3, guide.Groups.Count); foreach(var group in guide.Groups) Assert.AreEqual(13, group.Entries.Count);
            Assert.AreEqual("角色标记", GuideSheets.CharacterMarks().TitleZh); Assert.AreEqual(3, GuideSheets.CharacterMarks().Lines.Count);
        }
        [Test] public void Ai_lethal_estimate_respects_immunity_and_vulnerability()
        {
            var g = Game(); var ai = new GreedyChoiceProvider(); var move = new LegalMove(g.PlayerA, g.PlayerA.LeftHand, g.PlayerB.LeftHand);
            g.PlayerB.AddVulnerability(AttackDamageType.Slashing); Assert.AreEqual(9, ai.TotalLethalDamage(move,g.PlayerA,g.PlayerB,6));
            g.PlayerB.AddImmunity(AttackDamageType.Slashing); Assert.AreEqual(0, ai.TotalLethalDamage(move,g.PlayerA,g.PlayerB,6));
            g.PlayerB.ApplyStatus(StatusEffect.Frost); g.PlayerB.AddImmunity(AttackDamageType.Cold);
            var options = new[] { new SkillChoiceOption((int)AttackDamageType.Cold, "寒冷", "Damage/cold"), new SkillChoiceOption((int)AttackDamageType.Fire, "火焰", "Damage/fire") };
            Assert.AreEqual((int)AttackDamageType.Fire, ai.ChooseSkillOption(MartialSkillChoices.FourElements, options, g.PlayerA, g.PlayerB, g),
                "不能为冻结联动奖励选择已免疫的元素");
        }
    }
}
