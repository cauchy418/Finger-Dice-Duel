using System;
using FingerDiceDuel.Assistant;
using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class SecondaryAnimationTests
    {
        private static GameManager Match(CharacterDefinition a=null,CharacterDefinition b=null,bool visuals=true)
        {var g=CharacterFactory.CreateMatch(a??CharacterDefinition.Blank,b??CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.RecordCombatVisuals=visuals;return g;}
        [Test] public void Life_healing_records_cleanse_before_actual_heal_at_the_real_target()
        {var g=Match(CharacterDefinition.LifeDomain);g.PlayerB.SetHealthExact(7);g.PlayerB.ApplyStatus(StatusEffect.Decay,2);LifeDomainLogic.CleanseAndHeal(g.PlayerA,g.PlayerB,9,g);var cues=g.DrainCombatVisuals();Assert.AreEqual(2,cues.Count);Assert.AreEqual("净化",cues[0].RedirectKind);Assert.AreEqual("生命门徒",cues[1].RedirectKind);Assert.AreEqual(g.PlayerB,cues[1].Target);Assert.AreEqual(9,cues[1].SkillData.Value);Assert.AreEqual(16,g.PlayerB.Health);Assert.AreEqual(20,g.PlayerA.Health);}
        [Test] public void Full_health_heal_never_claims_nonzero_recovery()
        {var g=Match(CharacterDefinition.LifeDomain);LifeDomainLogic.CleanseAndHeal(g.PlayerA,g.PlayerB,9,g);Assert.AreEqual(0,g.DrainCombatVisuals()[0].SkillData.Value);}
        [Test] public void Blocked_or_rejected_maneuvers_do_not_emit_success_animation()
        {foreach(bool holy in new[]{false,true}){var g=Match();if(holy)g.PlayerB.ApplyStatus(StatusEffect.HolyLight,2);else{g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);g.PlayerB.AddResistance(AttackDamageType.Slashing);}var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){AttackType=AttackDamageType.Slashing};attack.ApplyAttachedStatus(g.PlayerB,StatusEffect.Limp,2,g.PlayerA,g,"卓越战技");attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Limp));Assert.AreEqual(0,g.DrainCombatVisuals().Count(e=>e.RedirectKind=="卓越战技"));}}
        [Test] public void Ki_upkeep_records_once_and_uses_actual_shared_resource_counts()
        {var g=Match(CharacterDefinition.WayOfTheDrunkenMaster);var monk=CharacterBehaviors.FirstImplementer<WayOfTheDrunkenMasterLogic>(g.PlayerA);Assert.IsTrue(monk.TryEnter(g.PlayerA));monk.OnTurnStarted(g.PlayerA,g);monk.OnTurnStarted(g.PlayerA,g);var ki=g.DrainCombatVisuals().Where(e=>e.RedirectKind=="气").ToList();Assert.AreEqual(1,ki.Count);Assert.AreEqual(2,ki[0].SkillData.Previous);Assert.AreEqual(1,ki[0].SkillData.Value);Assert.AreEqual(1,g.PlayerA.MonkKi);}
        [Test] public void Ki_gain_and_snapshot_count_are_immutable_and_cap_does_not_flash()
        {var g=Match(CharacterDefinition.WayOfTheNinja);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(4);var frame=g.CaptureVisualFrame();MonkRules.AfterAction(g.PlayerA,g);var e=g.DrainCombatVisuals()[0];Assert.AreEqual(2,e.SkillData.Previous);Assert.AreEqual(3,e.SkillData.Value);Assert.AreEqual(2,frame.Players[0].Icons.First(x=>x.ResourceKey=="Effects/ki").Value);g.PlayerA.GainMonkKi(9);MonkRules.AfterAction(g.PlayerA,g);Assert.AreEqual(0,g.DrainCombatVisuals().Count);}
        private sealed class CountRandom:Random
        {public int Calls;public override int Next(int maxValue){Calls++;return Calls==1?40:56;}}
        [Test] public void Wild_magic_visuals_do_not_roll_again_or_change_per_hit_results()
        {int calls=0,hp=0;AttackDamageType[] expected=null;foreach(bool visuals in new[]{false,true}){var g=Match(CharacterDefinition.WildMagic,visuals:visuals);var random=new CountRandom();CharacterBehaviors.FirstImplementer<WildMagicLogic>(g.PlayerA).Rng=random;var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8,g){HitCount=2};attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);if(!visuals){calls=random.Calls;hp=g.PlayerB.Health;expected=attack.SegmentTypes.ToArray();}else{Assert.AreEqual(calls,random.Calls);Assert.AreEqual(hp,g.PlayerB.Health);Assert.AreEqual(expected[0],attack.SegmentTypes[0]);Assert.AreEqual(expected[1],attack.SegmentTypes[1]);var cues=g.DrainCombatVisuals().Where(e=>e.RedirectKind=="狂野魔法浪涌").ToList();Assert.AreEqual(2,cues.Count);Assert.AreEqual(expected[0],cues[0].SkillData.Element);Assert.AreEqual(expected[1],cues[1].SkillData.Element);}}}
        [Test] public void Critical_visual_power_is_captured_without_becoming_a_damage_multiplier()
        {var g=Match(CharacterDefinition.Champion);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);var e=g.DrainCombatVisuals().First(x=>x.Kind==CombatVisualKind.Attack);Assert.AreEqual(1.35f,e.Power);Assert.AreEqual(7,attack.AppliedDamage);}
        [Test] public void Tactician_captures_real_health_spread_before_the_attack()
        {var g=Match(CharacterDefinition.Tactician);g.PlayerB.SetHealthExact(8);var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="战术优势");Assert.AreEqual(12,e.SkillData.Value);Assert.AreEqual(4,e.SkillData.Previous);}
        [Test] public void Right_hand_taichi_formation_keeps_the_real_source_hand()
        {var g=Match(CharacterDefinition.WayOfTheTaiChi);var logic=CharacterBehaviors.FirstImplementer<WayOfTheTaiChiLogic>(g.PlayerA);g.PlayerA.RightHand.SetValue(5);logic.OnShieldFormed(g.PlayerA,g.PlayerA.RightHand,g);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="阴阳流转");Assert.AreEqual(g.PlayerA.RightHand,e.SourceHand);Assert.AreEqual(g.PlayerA.RightHand,e.SkillData.Source);Assert.AreEqual(ShieldState.Taichi,g.PlayerA.RightHand.State);}
        [Test] public void War_blessing_nine_conversion_uses_the_operating_right_hand()
        {var g=Match(CharacterDefinition.WarDomain);g.PlayerA.RightHand.SetValue(8);g.PlayerB.LeftHand.SetValue(1);g.PlayerA.ApplyStatus(StatusEffect.WarBlessing,2);g.WarBlessingNineAttack=true;g.ExecuteTurn(g.PlayerA,g.PlayerA.RightHand,g.PlayerB.LeftHand);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="战争祝福");Assert.AreEqual(g.PlayerA.RightHand,e.SourceHand);Assert.AreEqual(9,e.SkillData.Value);}
        [Test] public void Both_locked_hands_have_distinct_presentation_targets()
        {var g=Match();g.PlayerA.LeftHand.Lock();g.PlayerA.RightHand.Lock();var icons=CombatStatusIcons.For(g.PlayerA,g).Where(i=>i.ResourceKey=="Overlay/locked").ToList();Assert.AreEqual(2,icons.Count);Assert.AreEqual(g.PlayerA.LeftHand,icons[0].TargetHand);Assert.AreEqual(g.PlayerA.RightHand,icons[1].TargetHand);}
        [Test] public void Distance_shield_expiry_reports_the_real_pair_after_two_target_turns()
        {var g=Match(CharacterDefinition.TheGreatOldOne);g.ApplyDistancePenalty(g.PlayerA,g.PlayerB,2);g.ResolveStatusTurnEnd(g.PlayerB);Assert.AreEqual(0,g.DrainCombatVisuals().Count);g.ResolveStatusTurnEnd(g.PlayerB);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="思维之盾");Assert.AreEqual("expire",e.SkillData.Variant);Assert.AreEqual(g.PlayerA,e.Actor);Assert.AreEqual(g.PlayerB,e.Target);Assert.AreEqual(0,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));}
        [Test] public void Profiles_stay_silent_when_visual_recording_is_disabled()
        {var g=Match(visuals:false);foreach(var p in SecondarySkillAnimation.Profiles)g.RecordSkillVisual(g.PlayerA,p.Skill,g.PlayerB,new SkillVisualData(value:3));Assert.AreEqual(0,g.DrainCombatVisuals().Count);}
    }
}
