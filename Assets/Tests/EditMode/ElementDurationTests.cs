using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class ElementDurationTests
 {
  private static GameManager Game(CharacterDefinition d=null)
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,d??CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.PlayerB.SetMaxHealth(200);g.PlayerB.SetHealthExact(200);return g;}
  private static AttackContext Hit(GameManager g,int hits=1,int duration=2,AttackDamageType type=AttackDamageType.Fire)
  {var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){AttackType=type,IgnoreStatBonuses=true,HitCount=hits,StatusDuration=duration,AttackerStatusOverride=type==AttackDamageType.Fire?StatusEffect.Burn:StatusNames.StatusOf(AttackDamageTypes.ToDamageType(type))};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);return c;}
  private static void Shield(Hand h){h.SetValue(5);h.SetShieldState(ShieldState.Formed);}
  [Test] public void Normal_shield_resistance_and_combination_have_two_one_one_zero_turns()
  {for(int flags=0;flags<4;flags++){var g=Game();if((flags&1)!=0)Shield(g.PlayerB.LeftHand);if((flags&2)!=0)g.PlayerB.AddResistance(AttackDamageType.Fire);
    var c=Hit(g);int expected=2-((flags&1)!=0?1:0)-((flags&2)!=0?1:0);Assert.AreEqual(expected,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));Assert.AreEqual(expected,c.ElementHits[0].Duration(2));}}
  [Test] public void First_blocked_second_landed_refreshes_to_full_duration()
  {var g=Game();Shield(g.PlayerB.LeftHand);var c=Hit(g,2);Assert.AreEqual(1,c.ElementHits[0].Duration(2));Assert.AreEqual(2,c.ElementHits[1].Duration(2));Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Resistance_persists_for_both_segments_even_when_first_is_blocked()
  {var g=Game();Shield(g.PlayerB.LeftHand);g.PlayerB.AddResistance(AttackDamageType.Fire);var c=Hit(g,2);Assert.AreEqual(0,c.ElementHits[0].Duration(2));Assert.AreEqual(1,c.ElementHits[1].Duration(2));Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));Assert.False(g.PlayerB.HasResistance(AttackDamageType.Fire));}
  [Test] public void All_segments_blocked_with_resistance_cannot_apply_state()
  {var g=Game();Shield(g.PlayerB.LeftHand);Shield(g.PlayerB.RightHand);g.PlayerB.AddResistance(AttackDamageType.Fire);Hit(g,2);Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));Assert.True(g.PlayerB.HasResistance(AttackDamageType.Fire));}
  [Test] public void Zero_duration_does_not_remove_or_refresh_existing_state()
  {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,3);Shield(g.PlayerB.LeftHand);g.PlayerB.AddResistance(AttackDamageType.Fire);Hit(g);Assert.AreEqual(3,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Draconic_three_turn_base_reduces_to_two_or_one()
  {var g=Game();Shield(g.PlayerB.LeftHand);Hit(g,1,3);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));Shield(g.PlayerB.LeftHand);g.PlayerB.AddResistance(AttackDamageType.Fire);Hit(g,1,3);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Necromancy_block_is_guarded()
  {var g=Game(CharacterDefinition.SchoolOfNecromancy);g.PlayerB.LeftHand.SetValue(2);var c=Hit(g);Assert.AreEqual(AttackGuard.Necromancy,c.ElementHits[0].Guard);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Ambush_full_mitigation_is_guarded()
  {var g=Game(CharacterDefinition.GloomStalker);g.PlayerB.LeftHand.SetValue(1);((GloomStalkerLogic)g.PlayerB.Character).EnterAmbush(g.PlayerB.LeftHand);var c=Hit(g);Assert.AreEqual(0,c.AppliedDamage);Assert.True((c.ElementHits[0].Guard&AttackGuard.Mitigation)!=0);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Taichi_partial_mitigation_is_guarded()
  {var g=Game(CharacterDefinition.WayOfTheTaiChi);g.PlayerB.LeftHand.SetValue(5);g.PlayerB.LeftHand.SetShieldState(ShieldState.Taichi);((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerB)).SetChoice(HandEffect.WayOfTheTaiChiTaichi,g.PlayerB.LeftHand);var c=Hit(g);Assert.True(c.ElementHits[0].Guard!=AttackGuard.None);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Barrier_partial_or_full_absorption_is_guarded()
  {foreach(int points in new[]{2,20}){var g=Game(CharacterDefinition.SchoolOfAbjuration);((IAbjurationSkill)g.PlayerB.Character).StoreBarrier(points);var c=Hit(g);Assert.True((c.ElementHits[0].Guard&AttackGuard.Barrier)!=0);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}}
  [Test] public void False_life_partial_or_full_absorption_is_guarded()
  {foreach(int hp in new[]{2,10}){var g=Game(CharacterDefinition.TheFiend);g.PlayerB.GainFalseLife(hp);var c=Hit(g);Assert.True((c.ElementHits[0].Guard&AttackGuard.FalseLife)!=0);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}}
  [Test] public void Sheep_independent_pool_is_guarded_even_with_overflow()
  {var g=Game();g.PlayerB.TryPolymorph();var c=Hit(g);Assert.True((c.ElementHits[0].Guard&AttackGuard.Polymorph)!=0);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Multiple_guard_mechanisms_reduce_only_once()
  {var g=Game(CharacterDefinition.SchoolOfAbjuration);g.PlayerB.GainFalseLife(2);((IAbjurationSkill)g.PlayerB.Character).StoreBarrier(2);var c=Hit(g);Assert.True((c.ElementHits[0].Guard&AttackGuard.Barrier)!=0);Assert.True((c.ElementHits[0].Guard&AttackGuard.FalseLife)!=0);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Immunity_always_prevents_element_state()
  {var g=Game();g.PlayerB.AddImmunity(AttackDamageType.Fire);Hit(g);Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));}
  [Test] public void Dodge_has_no_hit_receipt_or_state()
  {var g=Game(CharacterDefinition.Swashbuckler);g.PlayerB.LeftHand.SetValue(1);((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerB)).SetChoice(HandEffect.SwashbucklerDodge,g.PlayerB.LeftHand);var c=Hit(g);Assert.AreEqual(0,c.ElementHits.Count);Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));}
  [Test] public void Sonic_reaction_and_acid_refresh_use_reduced_duration()
  {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Resonance,1);Shield(g.PlayerB.LeftHand);Hit(g,type:AttackDamageType.Lightning);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Tinnitus));Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Shock));
   var h=Game();h.PlayerB.ApplyStatus(StatusEffect.Poison,2);Shield(h.PlayerB.LeftHand);Hit(h,type:AttackDamageType.Acid);Assert.AreEqual(1,h.PlayerB.StatusTurnsLeft(StatusEffect.Poison));Assert.AreEqual(1,h.PlayerB.StatusTurnsLeft(StatusEffect.Acid));}
  [Test] public void Skill_bleed_uses_the_same_guard_duration_rule()
  {var g=Game();Shield(g.PlayerB.LeftHand);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){InflictsBleed=true,AttackType=AttackDamageType.Fire,AttackerStatusOverride=StatusEffect.Burn};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Bleed));}
  private sealed class YesPolicy:ISkillChoicePolicy
  {public int ChooseSkillOption(string key,IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game)=>options[0].Value;
   public PlayerState HunterMarkTarget(PlayerState owner,IReadOnlyList<PlayerState> c,GameManager game)=>null;}
  [Test] public void Lore_and_illusion_whole_attack_defenses_count_as_guard_on_each_segment()
  {foreach(var d in new[]{CharacterDefinition.CollegeOfLore,CharacterDefinition.SchoolOfIllusion}){var g=Game(d);g.SkillPolicyFor=p=>new YesPolicy();var b=g.PlayerB;
    if(b.Character is CollegeOfLoreLogic lore){b.LeftHand.SetValue(8);lore.OnOperationResolved(b,b.LeftHand,1,7,HandEffectType.Attack8,g,null);}
    b.LeftHand.SetValue(1);b.RightHand.SetValue(3);var c=Hit(g,2);Assert.True((c.ElementHits[0].Guard&AttackGuard.WholeAttackReduction)!=0);Assert.AreEqual(1,c.ElementHits[1].Duration(2));Assert.AreEqual(1,b.StatusTurnsLeft(StatusEffect.Burn));}}
  [Test] public void Devotion_cannot_redirect_without_a_complete_shield()
  {var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.OathOfDevotion),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
   var g=GameManager.Create2v2(p,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});g.AnsweredLookupFor=(owner,e,v)=>ReferenceEquals(owner,p[2])&&e==HandEffect.OathOfDevotionRedirect?p[2].LeftHand:null;
   var c=Hit(g);Assert.AreEqual(p[1],c.Defender);Assert.False(c.WasRedirected);Assert.AreEqual(2,p[1].StatusTurnsLeft(StatusEffect.Burn));Assert.False(p[2].HasStatus(StatusEffect.Burn));}
  [Test] public void Oathbreaker_transfer_plus_shield_is_one_guard_penalty()
  {var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("B",CharacterDefinition.Oathbreaker),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
   var g=GameManager.Create2v2(p,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});p[1].LeftHand.SetValue(5);Shield(p[2].LeftHand);g.AnsweredLookupFor=(owner,e,v)=>e==HandEffect.OathbreakerTransfer?p[2].LeftHand:null;
   var c=Hit(g);Assert.AreEqual(p[2],c.Defender);Assert.True((c.ElementHits[0].Guard&AttackGuard.Transfer)!=0);Assert.True((c.ElementHits[0].Guard&AttackGuard.Shield)!=0);Assert.AreEqual(1,p[2].StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Holy_light_role_status_keeps_full_duration_through_guard_and_resistance()
  {for(int flags=0;flags<4;flags++){var g=CharacterFactory.CreateMatch(CharacterDefinition.LightDomain,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());if((flags&1)!=0)Shield(g.PlayerB.LeftHand);if((flags&2)!=0)g.PlayerB.AddResistance(AttackDamageType.Radiant);
    var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4);c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.HolyLight));}}

  [Test] public void Zero_damage_without_guard_keeps_full_status_duration()
  {var g=Game();var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,0){AttackType=AttackDamageType.Fire,AttackerStatusOverride=StatusEffect.Burn};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(0,c.AppliedDamage);Assert.AreEqual(AttackGuard.None,c.ElementHits[0].Guard);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
  [Test] public void Cancelled_lethal_attack_has_no_element_attachment()
  {var g=Game();var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,300){AttackType=AttackDamageType.Fire,AttackerStatusOverride=StatusEffect.Burn,LethalLoyalistSacrifice=(target,damage)=>g.PlayerA};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.True(c.WasCancelled);Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));}

  [Test] public void Bleed_with_shield_and_physical_resistance_has_zero_turns()
  {var g=Game();Shield(g.PlayerB.LeftHand);g.PlayerB.AddResistance(AttackDamageType.Slashing);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){InflictsBleed=true,AttackType=AttackDamageType.Slashing};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.False(g.PlayerB.HasStatus(StatusEffect.Bleed));}
  [Test] public void Physical_immunity_is_guard_but_does_not_immunize_bleed()
  {var g=Game();g.PlayerB.AddImmunity(AttackDamageType.Slashing);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){InflictsBleed=true,AttackType=AttackDamageType.Slashing};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Bleed));}
  [Test] public void Fiend_control_is_attached_after_hit_and_reduced_by_guard()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.TheFiend,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.PlayerA.GainFalseLife(5);Shield(g.PlayerB.LeftHand);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4);c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Disabled));}
  [Test] public void Fiend_control_does_not_attach_to_dodged_attack()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.TheFiend,CharacterDefinition.Swashbuckler,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.PlayerA.GainFalseLife(5);g.PlayerB.LeftHand.SetValue(1);((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerB)).SetChoice(HandEffect.SwashbucklerDodge,g.PlayerB.LeftHand);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4);c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.True(c.WasDodged);Assert.False(g.PlayerB.HasStatus(StatusEffect.Disabled));}
  [Test] public void Battle_master_damage_trigger_ignores_later_blocked_segment()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.BattleMaster,CharacterDefinition.SchoolOfNecromancy,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.PlayerA.RightHand.SetValue(4);g.PlayerB.LeftHand.SetValue(2);g.AnsweredLookupFor=(owner,e,v)=>e==HandEffect.SchoolOfNecromancyBlock&&v==1?g.PlayerB.LeftHand:null;
   var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){HitCount=2};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(2,c.ElementHits.Count);Assert.True(c.ElementHits[0].Damage>0);Assert.AreEqual(0,c.ElementHits[1].Damage);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Limp));}
  [Test] public void Post_attack_control_can_follow_reduced_duration_even_when_chosen_later()
  {var g=Game(CharacterDefinition.SchoolOfAbjuration);((IAbjurationSkill)g.PlayerB.Character).StoreBarrier(1);g.PlayerB.AddResistance(AttackDamageType.Bludgeoning);
   var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){AttackType=AttackDamageType.Bludgeoning};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);c.ApplyAttachedStatus(g.PlayerB,StatusEffect.Charm,2,g.PlayerA,g,"延后惑控",enchantment:true,requireHealthDamage:true);Assert.False(g.PlayerB.HasStatus(StatusEffect.Charm));}

  [Test] public void Multi_hit_skill_bleed_refreshes_from_guarded_one_to_unguarded_two()
  {var g=Game();Shield(g.PlayerB.LeftHand);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){InflictsBleed=true,HitCount=2,AttackType=AttackDamageType.Slashing};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Bleed));}
  [Test] public void Open_hand_control_is_reduced_on_guarded_hit()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.WayOfTheOpenHand,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.SkillPolicyFor=p=>new YesPolicy();Shield(g.PlayerB.LeftHand);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4);c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.KnockedDown));}

  [Test] public void Old_one_distance_effect_keeps_two_turns_despite_guard_and_resistance()
  {for(int flags=0;flags<4;flags++){var g=CharacterFactory.CreateMatch(CharacterDefinition.TheGreatOldOne,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());if((flags&1)!=0)Shield(g.PlayerB.LeftHand);if((flags&2)!=0)g.PlayerB.AddResistance(AttackDamageType.Slashing);
   var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){AttackType=AttackDamageType.Slashing};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);int duration=2;
   Assert.AreEqual(duration>0?2:0,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));for(int turn=0;turn<duration;turn++){Assert.AreEqual(2,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));g.ResolveStatusTurnEnd(g.PlayerB);}Assert.AreEqual(0,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));}}
  [Test] public void Old_one_later_unguarded_hit_refreshes_distance_to_two_turns()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.TheGreatOldOne,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());Shield(g.PlayerB.LeftHand);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){HitCount=2};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);g.ResolveStatusTurnEnd(g.PlayerB);Assert.AreEqual(2,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));g.ResolveStatusTurnEnd(g.PlayerB);Assert.AreEqual(0,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));}

 }
}
