using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class PurificationDomainTests
    {
        private const StatusEffect Scorch = (StatusEffect)27;
        private static GameManager Match(out PlayerState a,out PlayerState b)
        {
            a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LightDomain);
            b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            return new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
        }
        [Test] public void Ordinary_attack_is_fixed_five_fire_and_attaches_both_statuses()
        {
            var g=Match(out var a,out var b);a.LeftHand.SetValue(2);b.LeftHand.SetValue(2);
            var r=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(5,r.DamageDealt);Assert.AreEqual(AttackDamageType.Fire,r.AttackType);
            Assert.IsTrue(b.HasStatus(StatusEffect.HolyLight));Assert.AreEqual(2,b.StatusTurnsLeft(Scorch));
        }
        [Test] public void Fire_immunity_is_pierced_for_damage_but_prevents_attached_statuses()
        {
            var g=Match(out var a,out var b);b.AddPermanentImmunity(AttackDamageType.Fire);
            var attack=new AttackContext(a,b,a.LeftHand,8,g);attack.Resolve(new SimpleChoiceProvider(),g);
            Assert.AreEqual(15,b.Health);Assert.IsFalse(b.HasStatus(StatusEffect.HolyLight));Assert.IsFalse(b.HasStatus(Scorch));
        }
        [Test] public void Holy_fire_keeps_fire_and_radiant_states_and_blocks_other_states()
        {
            Match(out _,out var b);b.ApplyStatus(StatusEffect.Burn);b.ApplyStatus(StatusEffect.Blind);b.ApplyStatus(Scorch);b.ApplyStatus(StatusEffect.Poison);
            b.ApplyStatus(StatusEffect.HolyLight);
            Assert.IsTrue(b.HasStatus(StatusEffect.Burn));Assert.IsTrue(b.HasStatus(StatusEffect.Blind));Assert.IsTrue(b.HasStatus(Scorch));Assert.IsFalse(b.HasStatus(StatusEffect.Poison));
            b.ApplyStatus(StatusEffect.Poison);Assert.IsFalse(b.HasStatus(StatusEffect.Poison));b.ApplyStatus(StatusEffect.Blind,3);Assert.AreEqual(3,b.StatusTurnsLeft(StatusEffect.Blind));
        }
        [Test] public void Holy_fire_suspends_and_restores_all_three_affinities()
        {
            Match(out _,out var b);b.AddResistance(AttackDamageType.Fire);b.AddImmunity(AttackDamageType.Cold);b.AddVulnerability(AttackDamageType.Poison);
            b.ApplyStatus(StatusEffect.HolyLight);
            Assert.IsFalse(b.HasResistance(AttackDamageType.Fire));Assert.IsFalse(b.HasImmunity(AttackDamageType.Cold));Assert.IsFalse(b.HasVulnerability(AttackDamageType.Poison));
            b.RemoveStatus(StatusEffect.HolyLight);
            Assert.IsTrue(b.HasResistance(AttackDamageType.Fire));Assert.IsTrue(b.HasImmunity(AttackDamageType.Cold));Assert.IsTrue(b.HasVulnerability(AttackDamageType.Poison));
        }
        [Test] public void Holy_fire_no_longer_halves_healing()
        {Match(out _,out var b);b.SetHealthExact(5);b.ApplyStatus(StatusEffect.HolyLight);b.Heal(9);Assert.AreEqual(14,b.Health);}
        [Test] public void Scorch_uses_actual_healing_and_rounds_up_without_overflow_damage()
        {Match(out _,out var b);b.SetHealthExact(17);b.ApplyStatus(Scorch);b.Heal(9);Assert.AreEqual(18,b.Health);b.SetHealthExact(20);b.Heal(9);Assert.AreEqual(20,b.Health);}
        [Test] public void Nine_can_heal_a_selected_other_character_and_apply_holy_fire()
        {
            var g=Match(out var a,out var b);a.LeftHand.SetValue(8);b.LeftHand.SetValue(1);b.SetHealthExact(5);
            ((SimpleChoiceProvider)g.GetChoiceProvider(a)).SetChoice(HandEffect.LifeDomainHealTarget,b.LeftHand);
            g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(14,b.Health);Assert.IsTrue(b.HasStatus(StatusEffect.HolyLight));Assert.AreEqual(20,a.Health);
        }
        [Test] public void Affinity_lifetime_continues_while_suppressed()
        {
            Match(out _,out var b);b.ApplyStatus(StatusEffect.PhysicalVulnerability,1);b.ApplyStatus(StatusEffect.HolyLight,2);
            b.TickStatuses();b.TickStatuses();Assert.IsFalse(b.HasVulnerability(AttackDamageType.Slashing));
        }
        [Test] public void War_healing_targets_another_role_and_adds_blessing()
        {
            var g=new GameManager(CharacterFactory.CreatePlayer("A",CharacterDefinition.WarDomain),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(8);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.SetHealthExact(5);
            ((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerA)).SetChoice(HandEffect.LifeDomainHealTarget,g.PlayerB.LeftHand);
            g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);
            Assert.AreEqual(14,g.PlayerB.Health);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.WarBlessing));
        }
        [Test] public void Trickery_heal_hand_choice_holds_the_turn_until_answered()
        {
            var g=new GameManager(CharacterFactory.CreatePlayer("A",CharacterDefinition.TrickeryDomain),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(8);g.PlayerB.LeftHand.SetValue(1);
            ((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerA)).SetChoice(HandEffect.LifeDomainHealTarget,g.PlayerB.LeftHand);
            WizardChoice pending=null;g.DeferWizardChoice=r=>{pending=r;return true;};
            g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);
            Assert.IsTrue(g.PendingClericResolution);Assert.AreEqual(g.PlayerA,g.CurrentPlayer);
            Assert.IsTrue(pending.Choose(g.PlayerB.RightHand));Assert.AreEqual(2,g.PlayerB.RightHand.Value);
            Assert.IsFalse(g.PendingClericResolution);Assert.AreEqual(g.PlayerB,g.CurrentPlayer);
        }
        [Test] public void Tempest_healing_offers_the_target_an_optional_pure_operation()
        {
            var g=new GameManager(CharacterFactory.CreatePlayer("A",CharacterDefinition.TempestDomain),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(8);g.PlayerB.LeftHand.SetValue(1);
            ((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerA)).SetChoice(HandEffect.LifeDomainHealTarget,g.PlayerB.LeftHand);
            WizardChoice pending=null;g.DeferWizardChoice=r=>{pending=r;return true;};
            g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);
            Assert.IsTrue(pending!=null);Assert.IsTrue(g.PendingClericResolution);
            Assert.IsTrue(pending.Choose(null));Assert.IsFalse(g.PendingClericResolution);
        }
        [Test] public void Holy_fire_suspension_keeps_affinities_through_incoming_attacks()
        {
            var g=Match(out var a,out var b);b.AddImmunity(AttackDamageType.Cold);b.ApplyStatus(StatusEffect.HolyLight);
            new AttackContext(a,b,a.LeftHand,4,g).Resolve(new SimpleChoiceProvider(),g);
            b.RemoveStatus(StatusEffect.HolyLight);Assert.IsTrue(b.HasImmunity(AttackDamageType.Cold));
        }
        [Test] public void Dual_life_war_applies_blessing_to_both_double_nine_healing_targets()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var life=CharacterFactory.CreatePlayer("life",CharacterDefinition.LifeDomain).Character;
                var war=CharacterFactory.CreatePlayer("war",CharacterDefinition.WarDomain).Character;
                var a=new PlayerState("A",new DualGeneralBehavior(reverse?war:life,reverse?life:war));
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var choices=new SimpleChoiceProvider();
                var g=new GameManager(a,b,choices,new SimpleChoiceProvider());a.LeftHand.SetValue(8);a.RightHand.SetValue(9);b.LeftHand.SetValue(1);
                choices.SetChoice(HandEffect.LifeDomainExtraHealTarget,a.LeftHand);choices.SetChoice(HandEffect.LifeDomainHealTarget,b.LeftHand);
                g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                Assert.AreEqual(2,a.StatusTurnsLeft(StatusEffect.WarBlessing));Assert.AreEqual(2,b.StatusTurnsLeft(StatusEffect.WarBlessing));
            }
        }
    }
}
