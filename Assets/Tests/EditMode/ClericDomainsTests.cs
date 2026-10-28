using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class ClericDomainsTests
 {
  private sealed class Policy:IClericDomainPolicy
  {public bool Swap=true,Charm=true,Counter=true;public int CounterCalls;public AttackDamageType Type=AttackDamageType.Lightning;
   public bool SwapTrickery(TrickeryTouch t,GameManager g)=>Swap;public bool CharmTrickery(TrickeryTouch t,GameManager g)=>Charm;
   public Hand TempestHand(PlayerState o,PlayerState a,IReadOnlyList<Hand> h,GameManager g){CounterCalls++;return Counter?h[0]:null;}
   public Hand TempestAddend(PlayerState o,Hand s,IReadOnlyList<Hand> h,GameManager g)=>h[0];
   public AttackDamageType TempestType(PlayerState o,PlayerState t,GameManager g)=>Type;}
  private static BoardController Board(CharacterDefinition a,CharacterDefinition b,out GameManager g)
  {var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();g=CharacterFactory.CreateMatch(a,b,ca,cb);return new BoardController(g,ca,cb);}
  private static AttackContext Hit(GameManager g,AttackDamageType type,int damage=4)
  {var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,damage){AttackType=type};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);return c;}
  [Test] public void New_domains_registered_with_profiles_codex_and_alignment()
  {foreach(var def in new[]{CharacterDefinition.TrickeryDomain,CharacterDefinition.TempestDomain}){
    var p=CharacterFactory.CreatePlayer("a",def);Assert.AreEqual(-1,p.Character.DefenseBonus);Assert.AreEqual(0,p.Character.AttackBonus);
    Assert.True(HandValueProfiles.HasExplicitProfile(def));Assert.AreEqual(Alignment.ChaoticNeutral,CharacterCodex.For(def).Alignment);
    Assert.AreEqual("cleric",def.Profession.Id);Assert.True(((CharacterTemplate)p.Character).BaseAttackRange>100);}}
  [Test] public void Trickery_all_three_conditions_apply_before_numeric_effects()
  {foreach(var pair in new[]{new[]{2,4},new[]{3,2},new[]{8,4}}){
    var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);g.ClericPolicyFor=p=>new Policy();
    var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(pair[0]);b.LeftHand.SetValue(pair[1]);
    var r=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(pair[1],a.LeftHand.Value);Assert.AreEqual(Hand.ComputeSum(pair[0],pair[1]),b.LeftHand.Value);
    Assert.True(a.HasStatus(StatusEffect.Charm));Assert.AreEqual(pair[1],r.ResultValue);
  }}
  [Test] public void Trickery_decline_has_no_charm_and_success_can_decline_charm()
  {foreach(bool swap in new[]{false,true}){var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);g.ClericPolicyFor=p=>new Policy{Swap=swap,Charm=false};
    g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(4);g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);
    Assert.AreEqual(swap?4:6,g.PlayerA.LeftHand.Value);Assert.False(g.PlayerA.HasStatus(StatusEffect.Charm));}}
  [Test] public void Trickery_nontrigger_and_self_operation_do_not_swap()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);g.ClericPolicyFor=p=>new Policy();
   g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(4,g.PlayerA.LeftHand.Value);Assert.AreEqual(3,g.PlayerB.LeftHand.Value);}
  [Test] public void Trickery_human_swap_and_charm_are_separate_before_target_selection()
  {var board=Board(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain,out var g);g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(4);g.PlayerB.RightHand.SetValue(3);
   board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);Assert.True(board.Hint.Contains("诡术祝福"));Assert.AreEqual(2,g.PlayerA.LeftHand.Value);
   Assert.True(board.ChooseSkillOption(1));Assert.True(board.Hint.Contains("魅惑"));Assert.AreEqual(0,g.TurnCount);
   Assert.True(board.ChooseSkillOption(0));Assert.False(g.PlayerA.HasStatus(StatusEffect.Charm));
   Assert.AreEqual(4,g.PreviewOperationResult(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand));
   if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ClickHand(g.PlayerB.LeftHand);
   Assert.AreEqual(4,g.PlayerA.LeftHand.Value);Assert.AreEqual(6,g.PlayerB.LeftHand.Value);Assert.AreEqual(1,g.TurnCount);}
  [Test] public void Trickery_exchanged_shield_and_heal_dispatch_actual_digit()
  {foreach(int add in new[]{5,9}){var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);g.ClericPolicyFor=p=>new Policy{Charm=false};
    g.PlayerA.TakeDamage(10);g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(add);
    var r=g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(add,g.PlayerA.LeftHand.Value);
    if(add==5)Assert.True(g.PlayerA.LeftHand.IsShield);else Assert.AreEqual(19,g.PlayerA.Health);
    Assert.AreEqual(Hand.ComputeSum(2,add),g.PlayerB.LeftHand.Value);}}
  [Test] public void Trickery_frost_applies_once_to_original_result_not_swapped_digit()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);g.ClericPolicyFor=p=>new Policy{Charm=false};
   g.PlayerA.LeftHand.SetValue(2);g.PlayerA.ApplyStatus(StatusEffect.Frost,2);g.PlayerB.LeftHand.SetValue(4);
   var r=g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(4,r.ResultValue);Assert.AreEqual(3,g.PlayerB.LeftHand.Value);Assert.True(BattleTurnText.Describe(r).Contains("诡术祝福"));}
  [Test] public void Trickery_pure_operation_does_not_heal_or_attack_or_advance()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);g.ClericPolicyFor=p=>new Policy();g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(9);g.PlayerA.TakeDamage(10);
   Assert.True(g.ApplyEffectlessOperation(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand,"测试"));Assert.AreEqual(9,g.PlayerA.LeftHand.Value);Assert.AreEqual(10,g.PlayerA.Health);Assert.AreEqual(0,g.TurnCount);}
  [Test] public void Tempest_human_counter_precedes_damage_then_same_hand_must_operate()
  {var board=Board(CharacterDefinition.Blank,CharacterDefinition.TempestDomain,out var g);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(1);a.RightHand.SetValue(3);b.LeftHand.SetValue(4);b.RightHand.SetValue(3);
   board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.RightHand);if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ClickHand(b.LeftHand);
   Assert.True(board.Hint.Contains("风暴之怒"),board.Phase+" "+board.Hint);Assert.AreEqual(20,b.Health);Assert.AreEqual(0,g.TurnCount);
   Assert.True(board.ClickHand(b.LeftHand),"pick4 "+board.Phase+board.Hint);Assert.True(board.ChooseSkillOption((int)AttackDamageType.Lightning),"picktype "+board.Phase+board.Hint);
   Assert.AreEqual(16,a.Health);Assert.AreEqual(20,b.Health);Assert.AreEqual(4,b.LeftHand.Value);Assert.False(board.PendingWizardChoice.AllowDecline);
   Assert.True(board.ClickHand(a.RightHand),"addend "+board.Phase+board.Hint);Assert.AreEqual(7,b.LeftHand.Value);Assert.AreEqual(15,b.Health);Assert.AreEqual(1,g.TurnCount);}
  [Test] public void Tempest_counter_kill_aborts_original_attack()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TempestDomain);g.ClericPolicyFor=p=>new Policy();g.PlayerB.LeftHand.SetValue(4);g.PlayerA.TakeDamage(17);
   var c=Hit(g,AttackDamageType.Bludgeoning);Assert.False(g.PlayerA.IsAlive);Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(0,c.AppliedDamage);Assert.True(c.WasCancelled);}
  [Test] public void Tempest_decline_and_nonrecursive_counter()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.TempestDomain,CharacterDefinition.TempestDomain);g.ClericPolicyFor=p=>new Policy();g.PlayerA.LeftHand.SetValue(4);g.PlayerB.LeftHand.SetValue(4);
   Hit(g,AttackDamageType.Bludgeoning);Assert.AreEqual(15,g.PlayerA.Health);Assert.AreEqual(15,g.PlayerB.Health);
   var h=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TempestDomain);h.ClericPolicyFor=p=>new Policy{Counter=false};h.PlayerB.LeftHand.SetValue(4);Hit(h,AttackDamageType.Bludgeoning);Assert.AreEqual(20,h.PlayerA.Health);}
  [Test] public void Tempest_turn_start_operation_is_optional_and_only_changes_digits()
  {var board=Board(CharacterDefinition.TempestDomain,CharacterDefinition.Blank,out var g);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(8);g.PlayerA.TakeDamage(5);board.BeginTurn();
   Assert.True(board.Hint.Contains("风暴之心"));board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);Assert.AreEqual(9,g.PlayerA.LeftHand.Value);Assert.AreEqual(15,g.PlayerA.Health);Assert.AreEqual(0,g.TurnCount);}
  [Test] public void Tinnitus_amplifies_attack_and_periodic_damage_and_prevents_heal_shield()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.Blank);var b=g.PlayerB;b.ApplyStatus(StatusEffect.Tinnitus,2);Assert.AreEqual(6,Hit(g,AttackDamageType.Fire).AppliedDamage);
   b.Heal(9);Assert.AreEqual(14,b.Health);b.LeftHand.SetValue(2);g.PlayerA.LeftHand.SetValue(3);var r=g.ExecuteTurn(b,b.LeftHand,g.PlayerA.LeftHand);Assert.False(b.LeftHand.IsShield);Assert.AreEqual(HandEffectType.None,r.Effect);
   b.ApplyStatus(StatusEffect.Bleed,2);Assert.AreEqual(2,StatusRules.TurnStartDamage(b,out _));}
  [Test] public void Tinnitus_thunder_immunity_and_holy_light_cleansing()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.Blank);g.PlayerB.ApplyStatus(StatusEffect.Tinnitus,2);Assert.AreEqual(0,Hit(g,AttackDamageType.Thunder).AppliedDamage);Assert.False(g.PlayerB.HasStatus(StatusEffect.Resonance));
   g.PlayerB.ApplyStatus(StatusEffect.HolyLight,2);Assert.False(g.PlayerB.HasStatus(StatusEffect.Tinnitus));Assert.False(g.PlayerB.HasImmunity(AttackDamageType.Thunder));g.PlayerB.ApplyStatus(StatusEffect.Tinnitus,2);Assert.False(g.PlayerB.HasStatus(StatusEffect.Tinnitus));}
  [Test] public void Cross_sonic_reactions_refresh_original_and_apply_normal_status()
  {foreach(var type in new[]{AttackDamageType.Lightning,AttackDamageType.Thunder}){
    var g=CharacterFactory.CreateMatch(CharacterDefinition.TempestDomain,CharacterDefinition.Blank);var old=type==AttackDamageType.Lightning?StatusEffect.Resonance:StatusEffect.Shock;g.PlayerB.ApplyStatus(old,1,g.PlayerA);
    Hit(g,type);Assert.True(g.PlayerB.HasStatus(StatusEffect.Tinnitus));Assert.True(g.PlayerB.HasStatus(old));Assert.True(g.PlayerB.HasStatus(type==AttackDamageType.Lightning?StatusEffect.Shock:StatusEffect.Resonance));}}
  [Test] public void Sonic_reaction_applies_through_shield_but_not_type_immunity()
  {foreach(bool immune in new[]{false,true}){var g=CharacterFactory.CreateMatch(CharacterDefinition.TempestDomain,CharacterDefinition.Blank);g.PlayerB.ApplyStatus(StatusEffect.Resonance,2);
    if(immune)g.PlayerB.AddImmunity(AttackDamageType.Lightning);else{g.PlayerB.LeftHand.SetValue(5);g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);}Hit(g,AttackDamageType.Lightning);Assert.AreEqual(!immune,g.PlayerB.HasStatus(StatusEffect.Tinnitus));}}
  [Test] public void Ai_does_not_counter_known_ally_and_chooses_nonimmune_damage()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.TempestDomain,CharacterDefinition.Blank);var ai=new GreedyChoiceProvider();g.PlayerB.AddPermanentImmunity(AttackDamageType.Lightning);
   Assert.AreEqual(AttackDamageType.Thunder,ai.TempestType(g.PlayerA,g.PlayerB,g));}
  [Test] public void Tinnitus_is_enumerated_rendered_and_expires_normally()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerA;p.ApplyStatus(StatusEffect.Tinnitus,2);
   Assert.True(System.Linq.Enumerable.Contains(p.Statuses,StatusEffect.Tinnitus));Assert.True(CombatStatusIcons.For(p).Exists(icon=>icon.ResourceKey=="Status/tinnitus"));
   p.TickStatuses();Assert.AreEqual(1,p.StatusTurnsLeft(StatusEffect.Tinnitus));p.TickStatuses();Assert.False(p.HasStatus(StatusEffect.Tinnitus));Assert.False(p.HasImmunity(AttackDamageType.Thunder));}

  [Test] public void Trickery_reset_invalidates_queued_answers()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain);WizardChoice request=null;g.DeferWizardChoice=c=>{request=c;return true;};
   g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(4);bool completed=false;
   Assert.True(g.RequestTrickeryOperation(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand,()=>completed=true));g.ResetMatch();request.ChooseOption(1);Assert.False(completed);}
  [Test] public void Dragon_second_addend_can_offer_trickery_independently()
  {var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.CircleOfTheStars),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.TrickeryDomain),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
   var g=GameManager.Create2v2(p,new IChoiceProvider[4]);g.ClericPolicyFor=o=>new Policy{Charm=false};p[0].LeftHand.SetValue(4);p[0].RightHand.SetValue(6);
   ((FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic)p[0].Character).TryEnter(FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic.StarryForm.Dragon,p[0],g);
   p[1].LeftHand.SetValue(4);p[2].LeftHand.SetValue(2);g.DruidSecondAddend=p[2].LeftHand;
   var result=g.ExecuteTurn(p[0],p[0].LeftHand,p[1].LeftHand);Assert.AreEqual(2,result.ResultValue);Assert.AreEqual(1,p[2].LeftHand.Value);Assert.AreEqual(4,p[1].LeftHand.Value);}
  [Test] public void Counter_kill_returns_board_gameover_with_completed_turn_log()
  {var board=Board(CharacterDefinition.Blank,CharacterDefinition.TempestDomain,out var g);var a=g.PlayerA;var b=g.PlayerB;a.TakeDamage(17);a.LeftHand.SetValue(1);b.LeftHand.SetValue(4);b.RightHand.SetValue(3);
   board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.RightHand);if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ClickHand(b.LeftHand);
   board.ClickHand(b.LeftHand);board.ChooseSkillOption((int)AttackDamageType.Lightning);
   Assert.False(a.IsAlive);Assert.AreEqual(20,b.Health);Assert.AreEqual(BoardPhase.GameOver,board.Phase);Assert.True(board.LastTurn!=null);Assert.False(g.PendingClericResolution);}
  [Test] public void Counter_occurs_once_for_whole_multihit_attack()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.WayOfTheNinja,CharacterDefinition.TempestDomain);var policy=new Policy();g.ClericPolicyFor=o=>policy;g.PlayerB.LeftHand.SetValue(4);
   Hit(g,AttackDamageType.Slashing,8);Assert.AreEqual(1,policy.CounterCalls);}
  [Test] public void Counter_can_be_blocked_and_still_requires_followup_operation()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TempestDomain);g.ClericPolicyFor=o=>new Policy();g.PlayerA.LeftHand.SetValue(5);g.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);g.PlayerB.LeftHand.SetValue(4);
   Hit(g,AttackDamageType.Bludgeoning);Assert.AreEqual(20,g.PlayerA.Health);Assert.False(g.PlayerA.LeftHand.IsShield);Assert.True(g.PlayerB.LeftHand.Value!=4);}
  [Test] public void Tinnitus_does_not_modify_fixed_light_damage_and_life_heals_after_cleanse()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.LightDomain,CharacterDefinition.Blank);g.PlayerB.ApplyStatus(StatusEffect.Tinnitus,2);Assert.AreEqual(4,Hit(g,AttackDamageType.Radiant).AppliedDamage);
   var h=CharacterFactory.CreateMatch(CharacterDefinition.LifeDomain,CharacterDefinition.Blank);h.PlayerB.TakeDamage(10);h.PlayerB.ApplyStatus(StatusEffect.Tinnitus,2);FingerDiceDuel.Characters.SpecialLogics.LifeDomainLogic.CleanseAndHeal(h.PlayerA,h.PlayerB,9,h);Assert.AreEqual(19,h.PlayerB.Health);Assert.False(h.PlayerB.HasStatus(StatusEffect.Tinnitus));}
  [Test] public void Tempest_ai_does_not_counter_known_2v2_teammate()
  {var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.TempestDomain),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
   var g=GameManager.Create2v2(p,new IChoiceProvider[4]);p[0].LeftHand.SetValue(4);Assert.True(g.IsKnownAlly(p[0],p[3]));Assert.True(new GreedyChoiceProvider().TempestHand(p[0],p[3],new[]{p[0].LeftHand},g)==null);}

  [Test] public void Swordsman_followup_waits_for_counter_before_its_main_attack()
  {var board=Board(CharacterDefinition.Swordsman,CharacterDefinition.TempestDomain,out var g);var a=g.PlayerA;var b=g.PlayerB;
   a.LeftHand.SetValue(1);a.RightHand.SetValue(4);b.LeftHand.SetValue(4);b.RightHand.SetValue(3);board.BeginTurn();
   board.ClickHand(a.LeftHand);board.ClickHand(b.RightHand);
   for(int i=0;i<5&&board.PendingWizardChoice==null;i++){
    if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ClickHand(b.LeftHand);
    else if(board.PendingEffect==HandEffect.SwordsmanFollowUp)board.AcceptPendingChoice();else break;
   }
   Assert.True(board.Hint.Contains("风暴之怒"),board.Phase+board.Hint);Assert.AreEqual(20,b.Health);
   board.ClickHand(b.LeftHand);board.ChooseSkillOption((int)AttackDamageType.Lightning);Assert.AreEqual(20,b.Health);
   board.ClickHand(a.RightHand);Assert.AreEqual(1,g.TurnCount);Assert.True(board.LastTurn!=null);Assert.True(board.LastTurn.ExtraAttack.HasValue);
   Assert.True(b.Health<16);Assert.False(g.PendingClericResolution);}

  [Test] public void Tempest_counter_counts_as_guard_for_negative_duration_only()
  {for(int flags=0;flags<4;flags++){var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.TempestDomain);g.ClericPolicyFor=p=>new Policy{Counter=(flags&1)!=0};g.PlayerB.LeftHand.SetValue(4);if((flags&2)!=0)g.PlayerB.AddResistance(AttackDamageType.Fire);
   var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){AttackType=AttackDamageType.Fire,AttackerStatusOverride=StatusEffect.Burn,IgnoreStatBonuses=true};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);
   Assert.AreEqual((flags&1)!=0,c.TempestCountered);Assert.AreEqual(2-((flags&1)!=0?1:0)-((flags&2)!=0?1:0),g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}}

 }
}
