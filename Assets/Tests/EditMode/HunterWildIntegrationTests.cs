using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class HunterWildIntegrationTests
 {
  private sealed class Rolls : Random
  {private readonly Queue<int> values; public int Calls; public Rolls(params int[] sequence){values=new Queue<int>(sequence);}public override int Next(int max){Calls++;return values.Count==0?99:values.Dequeue();}}
  private static void Shield(Hand h){h.SetValue(5);h.SetShieldState(ShieldState.Formed);}
  private static GameManager Wild(out Rolls rng,params int[] rolls)
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.WildMagic,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());rng=new Rolls(rolls);((WildMagicLogic)g.PlayerA.Character).Rng=rng;g.PlayerB.SetMaxHealth(100);g.PlayerB.SetHealthExact(100);return g;}
  private static AttackContext Hit(GameManager g,int count=2)
  {var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){HitCount=count,IgnoreStatBonuses=true};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);return c;}
  [Test] public void Wild_rolls_each_segment_once_and_keeps_both_statuses()
  {var g=Wild(out var rng,40,56);var c=Hit(g);Assert.AreEqual(2,rng.Calls);Assert.AreEqual(AttackDamageType.Fire,c.SegmentTypes[0]);Assert.AreEqual(AttackDamageType.Lightning,c.SegmentTypes[1]);Assert.AreEqual(AttackDamageType.Fire,c.ElementHits[0].AttackType);Assert.True(g.PlayerB.HasStatus(StatusEffect.Burn));Assert.True(g.PlayerB.HasStatus(StatusEffect.Shock));Assert.AreEqual(8,c.AppliedDamage);}
  [Test] public void Force_then_plain_does_not_leak_shield_or_defense_bypass()
  {var g=Wild(out var rng,72,99);Shield(g.PlayerB.LeftHand);var c=Hit(g);Assert.AreEqual(4,c.AppliedDamage);Assert.AreEqual(1,c.BlockedHits);Assert.False(c.IgnoreShield);Assert.False(c.IgnorePositiveDefense);Assert.AreEqual(AttackDamageType.Bludgeoning,c.SegmentTypes[1]);}
  [Test] public void Plain_then_force_only_bypasses_on_the_force_segment()
  {var g=Wild(out var rng,99,72);Shield(g.PlayerB.LeftHand);var c=Hit(g);Assert.AreEqual(4,c.AppliedDamage);Assert.AreEqual(1,c.BlockedHits);Assert.AreEqual(AttackGuard.Shield,c.ElementHits[0].Guard);Assert.AreEqual(AttackGuard.None,c.ElementHits[1].Guard);}
  [Test] public void Independent_elements_use_corresponding_resistance_snapshot()
  {var g=Wild(out var rng,40,56);g.PlayerB.AddResistance(AttackDamageType.Fire);var c=Hit(g);Assert.AreEqual(6,c.AppliedDamage);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Shock));Assert.True(c.ElementHits[0].Resisted);Assert.False(c.ElementHits[1].Resisted);Assert.False(g.PlayerB.HasResistance(AttackDamageType.Fire));}
  [Test] public void Immunity_does_not_leak_to_later_different_element()
  {var g=Wild(out var rng,40,56);g.PlayerB.AddImmunity(AttackDamageType.Fire);var c=Hit(g);Assert.AreEqual(4,c.AppliedDamage);Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));Assert.True(g.PlayerB.HasStatus(StatusEffect.Shock));Assert.True(c.ElementHits[0].Immune);Assert.False(c.ElementHits[1].Immune);}
  [Test] public void Second_element_reacts_to_status_from_the_first_segment()
  {var g=Wild(out var rng,48,40);var c=Hit(g);Assert.AreEqual(10,c.AppliedDamage);Assert.False(g.PlayerB.HasStatus(StatusEffect.Frost));Assert.False(g.PlayerB.HasStatus(StatusEffect.Burn));}
  [Test] public void Later_plain_segment_uses_defense_changed_by_acid()
  {var g=Wild(out var rng,8,99);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){HitCount=2};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(10,c.AppliedDamage);}
  [Test] public void No_roll_for_segments_after_target_death()
  {var g=Wild(out var rng,40,56);g.PlayerB.SetHealthExact(1);Hit(g);Assert.AreEqual(1,rng.Calls);}
  [Test] public void Reflection_keeps_the_mitigated_segment_type()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.WildMagic,CharacterDefinition.WayOfTheTaiChi,new SimpleChoiceProvider(),new SimpleChoiceProvider());((WildMagicLogic)g.PlayerA.Character).Rng=new Rolls(40,56);g.PlayerB.LeftHand.SetValue(5);g.PlayerB.LeftHand.SetShieldState(ShieldState.Taichi);((SimpleChoiceProvider)g.GetChoiceProvider(g.PlayerB)).SetChoice(HandEffect.WayOfTheTaiChiTaichi,g.PlayerB.LeftHand);var c=Hit(g);Assert.AreEqual(AttackDamageType.Fire,c.ReflectedAttackType);Assert.AreEqual(2,c.ReflectedDealt);}
  [Test] public void Legal_dual_giant_wild_magic_has_two_independent_types_in_both_orders()
  {foreach(bool reverse in new[]{false,true}){var a=DualGeneralFactory.Create("dual",reverse?CharacterDefinition.WildMagic:CharacterDefinition.PathOfTheGiant,reverse?CharacterDefinition.PathOfTheGiant:CharacterDefinition.WildMagic);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());var wild=CharacterBehaviors.FirstImplementer<WildMagicLogic>(a);var rng=new Rolls(40,56);wild.Rng=rng;a.LeftHand.SetValue(2);a.RightHand.SetValue(4);b.LeftHand.SetValue(3);var turn=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(2,rng.Calls);Assert.AreEqual(2,turn.HitCount);Assert.True(BattleTurnText.Describe(turn).Contains("逐段：火焰／闪电"));Assert.True(b.HasStatus(StatusEffect.Burn));Assert.True(b.HasStatus(StatusEffect.Shock));}}
  private static GameManager Hunter(out PlayerState[] p)
  {p=new[]{CharacterFactory.CreatePlayer("H",CharacterDefinition.RangerHunter),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};return GameManager.Create2v2(p,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});}
  [Test] public void Hunter_extra_attack_consumes_mark_and_uses_full_attack_pipeline()
  {var g=Hunter(out var p);var h=(RangerHunterLogic)p[0].Character;h.Mark(p[2]);p[2].TakeDamage(5);var c=new AttackContext(p[0],p[1],p[0].LeftHand,4);c.Resolve(g.GetChoiceProvider(p[1]),g);Assert.AreEqual(16,p[1].Health);Assert.AreEqual(10,p[2].Health);Assert.False(h.IsMarked(p[2]));Assert.AreEqual(0,g.TurnCount);}
  [Test] public void Hunter_does_not_attack_a_marked_primary_target_twice()
  {var g=Hunter(out var p);var h=(RangerHunterLogic)p[0].Character;h.Mark(p[1]);new AttackContext(p[0],p[1],p[0].LeftHand,4).Resolve(g.GetChoiceProvider(p[1]),g);Assert.AreEqual(16,p[1].Health);}
  [Test] public void Hunter_ai_never_volley_attacks_a_known_teammate()
  {var g=Hunter(out var p);g.SkillPolicyFor=o=>new GreedyChoiceProvider();var h=(RangerHunterLogic)p[0].Character;h.Mark(p[3]);new AttackContext(p[0],p[1],p[0].LeftHand,4).Resolve(g.GetChoiceProvider(p[1]),g);Assert.True(g.IsKnownAlly(p[0],p[3]));Assert.AreEqual(20,p[3].Health);Assert.True(h.IsMarked(p[3]));}
  [Test] public void Hunter_player_can_decline_without_consuming_extra_mark()
  {var g=Hunter(out var p);var h=(RangerHunterLogic)p[0].Character;h.Mark(p[2]);WizardChoice pending=null;g.DeferWizardChoice=q=>{pending=q;return true;};new AttackContext(p[0],p[1],p[0].LeftHand,4).Resolve(g.GetChoiceProvider(p[1]),g);Assert.True(pending.TargetsPlayer);Assert.True(pending.AllowDecline);Assert.True(pending.Choose(null));Assert.True(h.IsMarked(p[2]));Assert.AreEqual(20,p[2].Health);}
  [Test] public void Hunter_selected_extra_attack_collects_defender_choices_and_does_not_recurse()
  {var g=Hunter(out var p);var h=(RangerHunterLogic)p[0].Character;h.Mark(p[2]);WizardChoice pending=null;AttackContext extra=null;Action resolve=null;g.DeferWizardChoice=q=>{pending=q;return true;};g.DeferWizardAttack=(c,go)=>{extra=c;resolve=go;return true;};new AttackContext(p[0],p[1],p[0].LeftHand,4).Resolve(g.GetChoiceProvider(p[1]),g);Assert.True(pending.Choose(p[2].LeftHand));Assert.True(extra.IsHunterVolley);Assert.AreEqual(4,extra.AttackValue);Assert.AreEqual(20,p[2].Health);Shield(p[2].LeftHand);resolve();Assert.AreEqual(20,p[2].Health);Assert.False(h.IsMarked(p[2]));Assert.True(extra.WasBlocked);}
  [Test] public void Hunter_real_board_queues_extra_player_selection_before_turn_changes()
  {var g=Hunter(out var p);var board=new BoardController(g,g.GetChoiceProvider(p[0]) as IPlayerChoiceSink,g.GetChoiceProvider(p[1]) as IPlayerChoiceSink);((RangerHunterLogic)p[0].Character).Mark(p[2]);p[0].LeftHand.SetValue(1);p[1].LeftHand.SetValue(3);board.BeginTurn();board.ClickHand(p[0].LeftHand);board.ClickHand(p[1].LeftHand);if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ClickHand(p[1].LeftHand);Assert.True(board.PendingWizardChoice!=null,board.Phase+board.Hint);Assert.True(board.PendingWizardChoice.TargetsPlayer);Assert.True(board.ChooseWizardHand(p[2].LeftHand));Assert.AreEqual(16,p[2].Health);Assert.AreEqual(1,g.TurnCount);}
  [Test] public void Hunter_eight_player_volley_hits_each_legal_mark_only_once()
  {var p=new List<PlayerState>();var providers=new List<IChoiceProvider>();for(int i=0;i<8;i++){p.Add(CharacterFactory.CreatePlayer("P"+i,i==0?CharacterDefinition.RangerHunter:CharacterDefinition.Blank));providers.Add(new SimpleChoiceProvider());}var g=GameManager.CreateEight(p,providers,new[]{0,1,2,3,4,5,6,7});var h=(RangerHunterLogic)p[0].Character;foreach(int i in new[]{1,3,4,7})h.Mark(p[i]);new AttackContext(p[0],p[1],p[0].LeftHand,4).Resolve(providers[1],g);foreach(int i in new[]{1,3,7}){Assert.AreEqual(16,p[i].Health);Assert.False(h.IsMarked(p[i]));}Assert.AreEqual(20,p[4].Health);Assert.True(h.IsMarked(p[4]));Assert.AreEqual(0,g.TurnCount);}
  [Test] public void Force_then_plain_keeps_human_necromancy_block_choice_for_plain_hit()
  {var g=CharacterFactory.CreateMatch(CharacterDefinition.WildMagic,CharacterDefinition.SchoolOfNecromancy,new SimpleChoiceProvider(),new SimpleChoiceProvider());((WildMagicLogic)g.PlayerA.Character).Rng=new Rolls(72,99);g.PlayerB.RightHand.SetValue(2);g.AnsweredLookupFor=(owner,e,v)=>e==HandEffect.SchoolOfNecromancyBlock&&v==0?g.PlayerB.RightHand:null;var c=Hit(g);Assert.AreEqual(1,c.BlockedHits);Assert.AreEqual(1,g.PlayerB.RightHand.Value);Assert.AreEqual(4,c.AppliedDamage);}
  [Test] public void Force_then_plain_reenables_positive_defense_for_plain_hit()
  {var g=Wild(out var rng,72,99);g.PlayerB.ApplyStatus(StatusEffect.WarBlessing,2);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8){HitCount=2};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(7,c.AppliedDamage);}
  [Test] public void Force_then_plain_real_board_preserves_human_dodge_for_plain_hit()
  {var a=DualGeneralFactory.Create("A",CharacterDefinition.PathOfTheGiant,CharacterDefinition.WildMagic);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Swashbuckler);var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var g=new GameManager(a,b,ca,cb);var board=new BoardController(g,ca,cb);CharacterBehaviors.FirstImplementer<WildMagicLogic>(a).Rng=new Rolls(72,99);a.LeftHand.SetValue(2);a.RightHand.SetValue(4);b.LeftHand.SetValue(3);b.RightHand.SetValue(1);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);bool asked=false;for(int i=0;i<12&&g.TurnCount==0;i++){if(board.PendingEffect==HandEffect.SwashbucklerDodge){asked=true;board.ClickHand(b.RightHand);}else if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ClickHand(b.LeftHand);else if(board.CanDeclinePendingChoice)board.DeclinePendingChoice();else break;}Assert.True(asked);Assert.AreEqual(1,g.TurnCount,board.Phase+board.Hint);Assert.AreEqual(1,board.LastTurn.DodgedHits);Assert.AreEqual(2,b.RightHand.Value);}
  [Test] public void All_codex_entries_use_normalized_chinese_skill_labels()
  {int count=0;foreach(var d in CharacterDefinition.BuiltIn){var entry=CharacterCodex.For(d);count++;Assert.True(entry.UsesNormalizedSkills,d.DisplayName);Assert.True(entry.DetailSkill.StartsWith(d.Profession.NameZh+"｜职业："),d.DisplayName);Assert.False(entry.DetailSkill.Contains("被动技能（"));Assert.False(entry.DetailSkill.Contains("待核对"));Assert.False(entry.DetailSkill.Contains("School of")||entry.DetailSkill.Contains("Arcane Volley"));}Assert.AreEqual(55,count);}
  [Test] public void Renamed_and_merged_skills_have_one_canonical_codex_entry()
  {var archer=CharacterCodex.For(CharacterDefinition.ArcaneArcher).DetailSkill;Assert.True(archer.Contains("奥术射击｜被动技能"));Assert.True(archer.Contains("魔能齐射")&&archer.Contains("强力射击")&&archer.Contains("穿透箭"));Assert.False(archer.Contains("连射｜"));Assert.True(CharacterCodex.For(CharacterDefinition.EldritchKnight).DetailSkill.Contains("奥法武器｜主动技能"));Assert.True(CharacterCodex.For(CharacterDefinition.BattleMaster).DetailSkill.Contains("卓越战技｜主动技能"));var hunter=CharacterCodex.For(CharacterDefinition.RangerHunter).DetailSkill;Assert.True(hunter.Contains("猎人印记｜主动技能"));Assert.True(hunter.Contains("巨像屠夫｜被动技能"));Assert.True(hunter.Contains("灭族者｜主动技能"));}

 }
}
