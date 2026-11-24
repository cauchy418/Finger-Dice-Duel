using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class NatureLightDomainTests
 {
  private static GameManager Match(CharacterDefinition a,CharacterDefinition b)=>CharacterFactory.CreateMatch(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
  private static AttackContext Hit(GameManager g,AttackDamageType type,int damage=4,int hits=1)
  {var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,damage){AttackType=type,HitCount=hits,IgnoreStatBonuses=true};c.Resolve(g.ChoiceProviderB,g);return c;}
  [Test] public void Nature_and_light_registered_separately_from_purification()
  {Assert.AreEqual(Alignment.NeutralGood,CharacterCodex.For(CharacterDefinition.NatureDomain).Alignment);Assert.AreEqual(Alignment.LawfulGood,CharacterCodex.For(CharacterDefinition.LightDomain).Alignment);
   Assert.AreEqual("cleric_light_domain",CharacterDefinition.LightDomain.Id);Assert.AreEqual("cleric_purification_domain",CharacterDefinition.PurificationDomain.Id);
   Assert.True(CharacterFactory.CreatePlayer("光",CharacterDefinition.LightDomain).Character is RadiantLightDomainLogic);
   Assert.True(CharacterFactory.CreatePlayer("净",CharacterDefinition.PurificationDomain).Character is LightDomainLogic);}
  [Test] public void Nature_suppresses_five_baselines_and_reactions_but_not_skill_bleed()
  {foreach(var type in new[]{AttackDamageType.Acid,AttackDamageType.Cold,AttackDamageType.Fire,AttackDamageType.Lightning,AttackDamageType.Thunder}){
    var g=Match(CharacterDefinition.Blank,CharacterDefinition.NatureDomain);var b=g.PlayerB;
    b.ApplyStatus(StatusEffect.Poison);b.ApplyStatus(StatusEffect.Wet);b.ApplyStatus(StatusEffect.Resonance);b.ApplyStatus(StatusEffect.Burn);
    var c=new AttackContext(g.PlayerA,b,g.PlayerA.LeftHand,2){AttackType=type,AttackerStatusOverride=StatusNames.StatusOf(AttackDamageTypes.ToDamageType(type)),InflictsBleed=true,IgnoreStatBonuses=true};c.Resolve(g.ChoiceProviderB,g);
    Assert.True(b.HasStatus(StatusEffect.Bleed));Assert.False(b.HasStatus(StatusEffect.Frozen));Assert.False(b.HasStatus(StatusEffect.Tinnitus));Assert.False(b.HasStatus(StatusEffect.Scorch));
    Assert.AreEqual(StatusNames.StatusOf(AttackDamageTypes.ToDamageType(type)),((NatureDomainLogic)b.Character).StoredElementStatus);
   }}
  [Test] public void Nature_storage_replaces_and_consumes_on_attack_without_changing_type()
  {var g=Match(CharacterDefinition.Blank,CharacterDefinition.NatureDomain);Hit(g,AttackDamageType.Fire);Hit(g,AttackDamageType.Cold);
   var nature=(NatureDomainLogic)g.PlayerB.Character;Assert.AreEqual(StatusEffect.Frost,nature.StoredElementStatus);
   var c=new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,2){IgnoreStatBonuses=true,HitCount=2};c.Resolve(g.ChoiceProviderA,g);
   Assert.AreEqual(StatusEffect.None,nature.StoredElementStatus);Assert.AreEqual(AttackDamageType.Bludgeoning,c.AttackType);Assert.True(g.PlayerA.HasStatus(StatusEffect.Frost));Assert.False(g.PlayerA.HasStatus(StatusEffect.Burn));}
  [Test] public void Nature_blessing_works_on_noncleric_after_zero_damage_and_shield_whole_attack()
  {foreach(bool shield in new[]{false,true}){var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);g.PlayerB.ApplyStatus(StatusEffect.NatureBlessing);
   if(shield){g.PlayerB.LeftHand.SetValue(5);g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);}
   Hit(g,AttackDamageType.Bludgeoning,0,2);Assert.True(g.PlayerA.HasStatus(StatusEffect.Restrained));Assert.AreEqual(2,g.PlayerA.StatusTurnsLeft(StatusEffect.Restrained));}}
  [Test] public void Blessings_refresh_are_role_states_and_holy_fire_clears_and_blocks()
  {foreach(var status in new[]{StatusEffect.NatureBlessing,StatusEffect.LightBlessing}){var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerA;
   p.ApplyStatus(status);p.TickStatuses();p.ApplyStatus(status);Assert.AreEqual(2,p.StatusTurnsLeft(status));Assert.False(StatusNames.IsNegativeStatus(status));
   p.ApplyStatus(StatusEffect.HolyLight);Assert.False(p.HasStatus(status));p.ApplyStatus(status);Assert.False(p.HasStatus(status));}}
  [Test] public void Nature_and_light_heal_automatically_bless_other_recipient()
  {foreach(var def in new[]{CharacterDefinition.NatureDomain,CharacterDefinition.LightDomain}){var a=new SimpleChoiceProvider();var b=new SimpleChoiceProvider();var g=CharacterFactory.CreateMatch(def,CharacterDefinition.Blank,a,b);var board=new BoardController(g,a,b);
   g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(8);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.True(g.PlayerB.HasStatus(def==CharacterDefinition.NatureDomain?StatusEffect.NatureBlessing:StatusEffect.LightBlessing));}}
  [Test] public void Light_blessing_converts_noncleric_and_blinds()
  {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);g.PlayerA.ApplyStatus(StatusEffect.LightBlessing);var c=Hit(g,AttackDamageType.Bludgeoning);
   Assert.AreEqual(AttackDamageType.Radiant,c.AttackType);Assert.True(g.PlayerB.HasStatus(StatusEffect.Blind));}
  [Test] public void Light_blessing_only_original_radiant_pierces_damage_immunity_and_resistance()
  {foreach(bool originallyRadiant in new[]{false,true}){var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);g.PlayerA.ApplyStatus(StatusEffect.LightBlessing);g.PlayerB.AddPermanentImmunity(AttackDamageType.Radiant);
   var c=Hit(g,originallyRadiant?AttackDamageType.Radiant:AttackDamageType.Fire);Assert.AreEqual(originallyRadiant?4:0,c.AppliedDamage);Assert.False(g.PlayerB.HasStatus(StatusEffect.Blind));}
   var h=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);h.PlayerA.ApplyStatus(StatusEffect.LightBlessing);h.PlayerB.AddPermanentResistance(AttackDamageType.Radiant);
   Assert.AreEqual(4,Hit(h,AttackDamageType.Radiant).AppliedDamage);Assert.AreEqual(1,h.PlayerB.StatusTurnsLeft(StatusEffect.Blind));}
  [Test] public void Dawn_human_choice_pauses_before_damage_cleans_then_applies_blind()
  {var a=new SimpleChoiceProvider();var b=new SimpleChoiceProvider();var g=CharacterFactory.CreateMatch(CharacterDefinition.LightDomain,CharacterDefinition.Blank,a,b);var board=new BoardController(g,a,b);
   g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);g.PlayerB.ApplyStatus(StatusEffect.Decay);g.PlayerB.ApplyStatus(StatusEffect.Bleed);
   board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.True(board.Hint.Contains("黎明曙光"),board.Hint);Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(0,g.TurnCount);
   Assert.True(board.ChooseSkillOption(1));Assert.AreEqual(12,g.PlayerB.Health);Assert.False(g.PlayerB.HasStatus(StatusEffect.Decay));Assert.False(g.PlayerB.HasStatus(StatusEffect.Bleed));Assert.True(g.PlayerB.HasStatus(StatusEffect.Blind));Assert.AreEqual(1,g.TurnCount);}
  [Test] public void Dawn_decline_preserves_negative_statuses()
  {var g=Match(CharacterDefinition.LightDomain,CharacterDefinition.Blank);g.PlayerB.ApplyStatus(StatusEffect.Decay);var c=Hit(g,AttackDamageType.Radiant);Assert.AreEqual(4,c.AppliedDamage);Assert.True(g.PlayerB.HasStatus(StatusEffect.Decay));Assert.True(g.PlayerB.HasStatus(StatusEffect.Blind));}
  [Test] public void Dawn_multihit_pauses_each_hit_and_cleans_before_each_new_blind()
  {var g=Match(CharacterDefinition.LightDomain,CharacterDefinition.Blank);g.PlayerB.ApplyStatus(StatusEffect.Decay);WizardChoice pending=null;int calls=0;bool complete=false;
   g.DeferWizardChoice=request=>{pending=request;calls++;return true;};
   var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){HitCount=2,IgnoreStatBonuses=true};c.Resolve(g.ChoiceProviderB,g,()=>complete=true);
   Assert.False(complete);Assert.AreEqual(1,calls);pending.ChooseOption(1);Assert.False(complete);Assert.AreEqual(2,calls);Assert.AreEqual(16,g.PlayerB.Health);Assert.True(g.PlayerB.HasStatus(StatusEffect.Blind));
   pending.ChooseOption(1);Assert.True(complete);Assert.AreEqual(12,g.PlayerB.Health);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Blind));}
  [Test] public void Dawn_can_cleanse_on_shield_hit_but_not_dodge_or_cancel()
  {var shield=Match(CharacterDefinition.LightDomain,CharacterDefinition.Blank);shield.PlayerB.ApplyStatus(StatusEffect.Decay);shield.PlayerB.LeftHand.SetValue(5);shield.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);
   shield.DeferWizardChoice=c=>{c.ChooseOption(1);return false;};Hit(shield,AttackDamageType.Radiant);Assert.False(shield.PlayerB.HasStatus(StatusEffect.Decay));Assert.AreEqual(20,shield.PlayerB.Health);
   var dodge=Match(CharacterDefinition.LightDomain,CharacterDefinition.Swashbuckler);dodge.PlayerB.ApplyStatus(StatusEffect.NatureBlessing);dodge.PlayerB.ApplyStatus(StatusEffect.Decay);dodge.PlayerB.LeftHand.SetValue(1);
   ((SimpleChoiceProvider)dodge.ChoiceProviderB).SetChoice(HandEffect.SwashbucklerDodge,dodge.PlayerB.LeftHand);int asks=0;dodge.DeferWizardChoice=c=>{asks++;return true;};
   var dodged=Hit(dodge,AttackDamageType.Radiant);Assert.True(dodged.WasDodged);Assert.AreEqual(0,asks);Assert.True(dodge.PlayerA.HasStatus(StatusEffect.Restrained));
   var cancel=Match(CharacterDefinition.LightDomain,CharacterDefinition.TheArchfey);cancel.PlayerB.LeftHand.SetValue(4);cancel.PlayerB.ApplyStatus(StatusEffect.NatureBlessing);cancel.PlayerB.ApplyStatus(StatusEffect.Decay);cancel.DeferWizardChoice=c=>{asks++;return true;};
   var attack=new AttackContext(cancel.PlayerA,cancel.PlayerB,cancel.PlayerA.LeftHand,4){AttackValueOverride=4};attack.Resolve(cancel.ChoiceProviderB,cancel);
   Assert.True(attack.WasCancelled);Assert.AreEqual(0,asks);Assert.False(cancel.PlayerA.HasStatus(StatusEffect.Restrained));}
  [Test] public void Nature_storage_snapshot_survives_multihit_and_cancel_consumes()
  {var g=Match(CharacterDefinition.NatureDomain,CharacterDefinition.Blank);var nature=(NatureDomainLogic)g.PlayerA.Character;nature.StoreElement(AttackDamageType.Cold);
   var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){HitCount=2,IgnoreStatBonuses=true};c.PrepareResolution(g.ChoiceProviderB,g);nature.StoreElement(AttackDamageType.Fire);c.Resolve(g.ChoiceProviderB,g);
   Assert.AreEqual(StatusEffect.Burn,nature.StoredElementStatus);Assert.True(g.PlayerB.HasStatus(StatusEffect.Frost));Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));
   var cancel=Match(CharacterDefinition.NatureDomain,CharacterDefinition.TheArchfey);((NatureDomainLogic)cancel.PlayerA.Character).StoreElement(AttackDamageType.Fire);cancel.PlayerB.LeftHand.SetValue(4);
   var attack=new AttackContext(cancel.PlayerA,cancel.PlayerB,cancel.PlayerA.LeftHand,4){AttackValueOverride=4};attack.Resolve(cancel.ChoiceProviderB,cancel);Assert.True(attack.WasCancelled);Assert.AreEqual(StatusEffect.None,((NatureDomainLogic)cancel.PlayerA.Character).StoredElementStatus);}
 }
}
