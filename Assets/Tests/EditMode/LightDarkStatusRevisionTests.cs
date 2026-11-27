using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class LightDarkStatusRevisionTests
 {
  private static GameManager Game(CharacterDefinition actor=null,CharacterDefinition defender=null)
   =>CharacterFactory.CreateMatch(actor??CharacterDefinition.Blank,defender??CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());
  private static void Shield(Hand h){h.SetValue(5);h.SetShieldState(ShieldState.Formed);}
  private static AttackContext Hit(GameManager g,AttackDamageType type,int damage=3,int hits=1,bool status=false)
  {var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,damage){AttackType=type,IgnoreStatBonuses=true,HitCount=hits,IsStatusDamage=status};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);return c;}
  [Test] public void Blind_only_halves_magic_against_unblinded_defenders()
  {foreach(var type in new[]{AttackDamageType.Fire,AttackDamageType.Force,AttackDamageType.Slashing}){var g=Game();g.PlayerA.ApplyStatus(StatusEffect.Blind);Assert.AreEqual(type==AttackDamageType.Slashing?3:2,Hit(g,type).AppliedDamage);}
   var h=Game();h.PlayerA.ApplyStatus(StatusEffect.Blind);h.PlayerB.ApplyStatus(StatusEffect.Blind);Assert.AreEqual(3,Hit(h,AttackDamageType.Fire).AppliedDamage);}
  [Test] public void Darkness_only_halves_physical_against_non_dark_defenders()
  {foreach(var type in new[]{AttackDamageType.Necrotic,AttackDamageType.Bludgeoning,AttackDamageType.Piercing}){var g=Game();g.PlayerA.ApplyStatus(StatusEffect.DarknessBlind);Assert.AreEqual(type==AttackDamageType.Necrotic?3:2,Hit(g,type).AppliedDamage);}
   var h=Game();h.PlayerA.ApplyStatus(StatusEffect.DarknessBlind);h.PlayerB.ApplyStatus(StatusEffect.DarknessBlind);Assert.AreEqual(3,Hit(h,AttackDamageType.Slashing).AppliedDamage);}
  [Test] public void Status_damage_is_not_reduced_by_outgoing_debuffs_or_given_new_states()
  {var g=Game();g.PlayerA.ApplyStatus(StatusEffect.Annihilation);g.PlayerA.ApplyStatus(StatusEffect.Disabled);Assert.AreEqual(3,Hit(g,AttackDamageType.Fire,status:true).AppliedDamage);Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));}
  [Test] public void Element_statuses_and_same_element_reactions_use_guard_and_resistance_duration()
  {foreach(bool dark in new[]{false,true})for(int flags=0;flags<4;flags++){var g=Game();var type=dark?AttackDamageType.Necrotic:AttackDamageType.Radiant;var old=dark?StatusEffect.DarknessBlind:StatusEffect.Blind;var reaction=dark?StatusEffect.Decay:StatusEffect.DivinePunishment;
    g.PlayerB.ApplyStatus(old,3);if((flags&1)!=0)Shield(g.PlayerB.LeftHand);if((flags&2)!=0)g.PlayerB.AddResistance(type);Hit(g,type);
    int n=2-((flags&1)!=0?1:0)-((flags&2)!=0?1:0);Assert.AreEqual(n,g.PlayerB.StatusTurnsLeft(reaction));Assert.True(g.PlayerB.HasStatus(old));}}
  [Test] public void Fresh_necrotic_adds_darkness_not_decay()
  {var g=Game();Hit(g,AttackDamageType.Necrotic);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.DarknessBlind));Assert.False(g.PlayerB.HasStatus(StatusEffect.Decay));}
  [Test] public void Cross_reactions_clear_all_four_states_and_persist_until_expiry()
  {foreach(bool dark in new[]{false,true}){var g=Game();var b=g.PlayerB;b.ApplyStatus(dark?StatusEffect.Blind:StatusEffect.DarknessBlind);b.ApplyStatus(StatusEffect.DivinePunishment);b.ApplyStatus(StatusEffect.Decay);
    Hit(g,dark?AttackDamageType.Necrotic:AttackDamageType.Radiant);Assert.AreEqual(2,b.StatusTurnsLeft(StatusEffect.Annihilation));
    foreach(var st in new[]{StatusEffect.Blind,StatusEffect.DarknessBlind,StatusEffect.DivinePunishment,StatusEffect.Decay}){Assert.False(b.HasStatus(st));b.ApplyStatus(st);Assert.False(b.HasStatus(st));}
    for(int i=0;i<2;i++){Assert.AreEqual(0,Hit(g,AttackDamageType.Radiant).AppliedDamage);Assert.AreEqual(0,Hit(g,AttackDamageType.Necrotic).AppliedDamage);}
    b.TickStatuses();Assert.True(b.HasImmunity(AttackDamageType.Radiant));b.TickStatuses();Assert.False(b.HasImmunity(AttackDamageType.Radiant));}}
  [Test] public void Zero_duration_cross_reaction_preserves_existing_states()
  {var g=Game();var b=g.PlayerB;b.ApplyStatus(StatusEffect.DarknessBlind,3);Shield(b.LeftHand);b.AddResistance(AttackDamageType.Radiant);Hit(g,AttackDamageType.Radiant);Assert.False(b.HasStatus(StatusEffect.Annihilation));Assert.AreEqual(3,b.StatusTurnsLeft(StatusEffect.DarknessBlind));}
  [Test] public void Temporary_immunity_blocks_reaction_then_is_consumed()
  {var g=Game();var b=g.PlayerB;b.ApplyStatus(StatusEffect.DarknessBlind,3);b.AddImmunity(AttackDamageType.Radiant);Assert.AreEqual(0,Hit(g,AttackDamageType.Radiant).AppliedDamage);Assert.False(b.HasStatus(StatusEffect.Annihilation));Assert.AreEqual(3,b.StatusTurnsLeft(StatusEffect.DarknessBlind));Assert.False(b.HasImmunity(AttackDamageType.Radiant));Hit(g,AttackDamageType.Radiant);Assert.True(b.HasStatus(StatusEffect.Annihilation));}
  [Test] public void Annihilation_halves_all_attack_types_but_not_status_damage()
  {foreach(var type in ResistanceNames.All){var g=Game();g.PlayerA.ApplyStatus(StatusEffect.Annihilation);Assert.AreEqual(2,Hit(g,type).AppliedDamage);}}
  [Test] public void Pure_effectless_operations_do_not_trigger_any_self_damage()
  {foreach(var st in new[]{StatusEffect.DivinePunishment,StatusEffect.Decay,StatusEffect.Annihilation}){var g=Game();g.PlayerA.ApplyStatus(st);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(2);Assert.True(g.ApplyStormFreeOperation(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand));Assert.AreEqual(20,g.PlayerA.Health);}}
  [Test] public void Divine_punishment_uses_final_frost_modified_attack_number()
  {var g=Game();var a=g.PlayerA;a.ApplyStatus(StatusEffect.DivinePunishment);a.ApplyStatus(StatusEffect.Frost);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(8);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(4,r.ResultValue);Assert.AreEqual(16,a.Health);Assert.AreEqual(4,r.DamageDealt);Assert.False(a.HasStatus(StatusEffect.Blind));}
  [Test] public void Decay_no_longer_blocks_healing_and_self_damage_precedes_heal()
  {var g=Game();var a=g.PlayerA;a.SetHealthExact(10);a.ApplyStatus(StatusEffect.Decay);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(8);Assert.True(StatusRules.CanHeal(a));g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(10,a.Health);Assert.False(a.HasStatus(StatusEffect.DarknessBlind));}
  [Test] public void Role_effect_rewrite_changes_whether_attack_number_triggers_self_damage()
  {var g=Game(CharacterDefinition.OathOfDevotion);var a=g.PlayerA;a.ApplyStatus(StatusEffect.DivinePunishment);a.ApplyStatus(StatusEffect.Decay);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(HandEffectType.Shield,r.Effect);Assert.AreEqual(16,a.Health);Assert.AreEqual(0,r.DamageDealt);}
  [Test] public void Fatal_self_damage_stops_attack_and_heal_effects()
  {foreach(var st in new[]{StatusEffect.DivinePunishment,StatusEffect.Decay,StatusEffect.Annihilation}){var g=Game();var a=g.PlayerA;a.SetHealthExact(2);a.ApplyStatus(st);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(st==StatusEffect.Decay?8:3);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.False(a.IsAlive);Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(0,r.Healed);Assert.True(g.IsGameOver);}}
  [Test] public void Self_damage_obeys_resistance_immunity_shield_and_barrier()
  {var g=Game();var a=g.PlayerA;a.ApplyStatus(StatusEffect.DivinePunishment);a.AddResistance(AttackDamageType.Radiant);g.ResolveOperationStatusDamage(a,5,HandEffectType.Attack4);Assert.AreEqual(17,a.Health);a.AddImmunity(AttackDamageType.Radiant);g.ResolveOperationStatusDamage(a,4,HandEffectType.Attack4);Assert.AreEqual(17,a.Health);Shield(a.RightHand);g.ResolveOperationStatusDamage(a,4,HandEffectType.Attack4);Assert.AreEqual(17,a.Health);
   var h=Game(CharacterDefinition.SchoolOfAbjuration);h.PlayerA.ApplyStatus(StatusEffect.Decay);((IAbjurationSkill)h.PlayerA.Character).StoreBarrier(10);h.ResolveOperationStatusDamage(h.PlayerA,5,HandEffectType.Shield);Assert.AreEqual(20,h.PlayerA.Health);}
  [Test] public void Resonance_ticks_near_remote_and_self_turns_before_expiring()
  {var players=new List<PlayerState>();var providers=new List<IChoiceProvider>();for(int i=0;i<4;i++){players.Add(CharacterFactory.CreatePlayer("p"+i,CharacterDefinition.Blank));providers.Add(new SimpleChoiceProvider());}var g=GameManager.Create2v2(players,providers);var a=players[0];a.ApplyStatus(StatusEffect.Resonance);g.ResolveStatusTurnEnd(players[2]);Assert.AreEqual(20,a.Health);g.ResolveStatusTurnEnd(players[1]);Assert.AreEqual(19,a.Health);Assert.AreEqual(2,a.StatusTurnsLeft(StatusEffect.Resonance));g.ResolveStatusTurnEnd(a);Assert.AreEqual(18,a.Health);Assert.AreEqual(1,a.StatusTurnsLeft(StatusEffect.Resonance));g.ResolveStatusTurnEnd(a);Assert.AreEqual(17,a.Health);Assert.False(a.HasStatus(StatusEffect.Resonance));Assert.False(a.HasStatus(StatusEffect.Dizzy));}
  [Test] public void Holy_fire_keeps_punishment_and_annihilation_but_clears_decay_and_darkness()
  {var g=Game();var a=g.PlayerA;foreach(var st in new[]{StatusEffect.Burn,StatusEffect.Scorch,StatusEffect.Blind,StatusEffect.DivinePunishment,StatusEffect.DarknessBlind,StatusEffect.Decay})a.ApplyStatus(st);a.ApplyStatus(StatusEffect.HolyLight);Assert.True(a.HasStatus(StatusEffect.DivinePunishment));Assert.True(a.HasStatus(StatusEffect.Blind));Assert.False(a.HasStatus(StatusEffect.DarknessBlind));Assert.False(a.HasStatus(StatusEffect.Decay));a.ApplyStatus(StatusEffect.Annihilation);Assert.True(a.HasStatus(StatusEffect.Annihilation));Assert.True(a.HasImmunity(AttackDamageType.Radiant));Assert.True(a.HasImmunity(AttackDamageType.Necrotic));a.ApplyStatus(StatusEffect.Decay);Assert.False(a.HasStatus(StatusEffect.Decay));}
  [Test] public void Ordered_statuses_group_elements_without_duplicates_and_include_new_icons()
  {var seen=new HashSet<StatusEffect>();foreach(var s in StatusNames.OrderedStatuses)Assert.True(seen.Add(s));Assert.AreEqual(34,seen.Count);Assert.AreEqual("Status/divine_punishment",StatusNames.ResourceKeyOf(StatusEffect.DivinePunishment));Assert.AreEqual("Status/annihilation",StatusNames.ResourceKeyOf(StatusEffect.Annihilation));}
  [Test] public void Real_gallery_storm_and_shadow_lessons_demonstrate_current_status_rules()
  {foreach(int level in new[]{11,17}){var l=CampaignLesson.CreateV3(level);l.BeginTutorial();l.Board.BeginTurn();bool observed=false;
    for(int i=0;i<250&&!l.TrialConfirmationPending&&!l.Game.IsGameOver;i++){int cursor=l.Cursor;int hp=l.Seats[1].Health;Assert.True(CampaignFlowTests.ClickGuide(l),CampaignFlowTests.Diagnostic(l));if(cursor==l.Cursor)continue;
      if(level==11&&cursor==1){Assert.AreEqual(hp-1,l.Seats[1].Health);Assert.AreEqual(1,l.Seats[1].StatusTurnsLeft(StatusEffect.Resonance));observed=true;}
      if(level==17&&cursor==3){Assert.True(l.Seats[1].HasStatus(StatusEffect.Decay));Assert.True(l.Seats[1].HasStatus(StatusEffect.DarknessBlind));}
      if(level==17&&cursor==4){Assert.AreEqual(hp,l.Seats[1].Health,"腐朽先扣9再回复9");observed=true;}
    }Assert.True(observed,CampaignFlowTests.Diagnostic(l));Assert.True(l.TrialConfirmationPending,CampaignFlowTests.Diagnostic(l));}}
  [Test] public void Same_type_multihit_reacts_to_the_previous_segments_base_status()
  {foreach(bool dark in new[]{false,true}){var g=Game();Hit(g,dark?AttackDamageType.Necrotic:AttackDamageType.Radiant,4,2);Assert.True(g.PlayerB.HasStatus(dark?StatusEffect.DarknessBlind:StatusEffect.Blind));Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(dark?StatusEffect.Decay:StatusEffect.DivinePunishment));}}
  [Test] public void Prepared_self_damage_is_applied_once_and_fatal_result_still_finalizes()
  {foreach(bool fatal in new[]{false,true}){var g=Game();var a=g.PlayerA;a.ApplyStatus(StatusEffect.DivinePunishment);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);if(fatal)a.SetHealthExact(2);g.PrepareOperationStatusDamage(a,a.LeftHand,g.PlayerB.LeftHand,4,HandEffectType.Attack4);g.PrepareOperationStatusDamage(a,a.LeftHand,g.PlayerB.LeftHand,4,HandEffectType.Attack4);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(fatal?0:16,a.Health);Assert.AreEqual(4,r.ResultValue);Assert.AreEqual(4,a.LeftHand.Value);Assert.AreEqual(fatal?0:4,r.DamageDealt);}}
  [Test] public void Normal_effectless_numbers_still_trigger_decay_and_annihilation()
  {foreach(var st in new[]{StatusEffect.Decay,StatusEffect.Annihilation}){var g=Game();var a=g.PlayerA;a.ApplyStatus(st);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(HandEffectType.None,r.Effect);Assert.AreEqual(2,r.ResultValue);Assert.AreEqual(18,a.Health);}}
  [Test] public void Prepared_role_effect_is_frozen_before_status_damage_changes_health_threshold()
  {var g=Game(CharacterDefinition.PathOfTheGiant);var a=g.PlayerA;a.SetHealthExact(17);a.ApplyStatus(StatusEffect.DivinePunishment);a.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);g.PrepareOperationStatusDamage(a,a.LeftHand,g.PlayerB.LeftHand,4,HandEffectType.Attack4);Assert.AreEqual(13,a.Health);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(HandEffectType.Attack4,r.Effect);Assert.True(r.DamageDealt>0);}
  [Test] public void Giant_heal_amount_and_segment_mode_are_frozen_before_decay_changes_threshold()
  {foreach(bool prepared in new[]{false,true}){var g=Game(CharacterDefinition.PathOfTheGiant);var a=g.PlayerA;a.SetHealthExact(17);a.ApplyStatus(StatusEffect.Decay);a.LeftHand.SetValue(1);a.RightHand.SetValue(3);g.PlayerB.LeftHand.SetValue(8);if(prepared)g.PrepareOperationStatusDamage(a,a.LeftHand,g.PlayerB.LeftHand,9,HandEffectType.Heal);var r=g.ExecuteTurn(a,a.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(9,r.HealAmount);Assert.AreEqual(17,a.Health);}}
 }
}
