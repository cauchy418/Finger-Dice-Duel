using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class SporesTests
 {
  private static GameManager Match()=>CharacterFactory.CreateMatch(CharacterDefinition.CircleOfSpores,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());
  [Test] public void Human_symbiosis_can_be_declined_and_consumes_one_charge_when_accepted()
  {var g=Match();var b=new BoardController(g,(IPlayerChoiceSink)g.ChoiceProviderA,(IPlayerChoiceSink)g.ChoiceProviderB);b.BeginTurn();Assert.IsTrue(b.Hint.Contains("一体共生"));b.ChooseSkillOption(0);Assert.AreEqual(0,g.PlayerA.SporeLife);Assert.AreEqual(2,g.PlayerA.WildCharges);g.ForceActivePlayerForTest(g.PlayerA);b.BeginTurn();b.ChooseSkillOption(1);Assert.AreEqual(10,g.PlayerA.SporeLife);Assert.AreEqual(1,g.PlayerA.WildCharges);Assert.AreEqual(0,g.TurnCount);}
  [Test] public void Spore_buffer_precedes_false_life_and_transfers_overflow()
  {var g=Match();var p=g.PlayerA;p.GainSporeLife();p.GainFalseLife(4);Assert.AreEqual(0,p.TakeDamageResolved(12));Assert.AreEqual(0,p.SporeLife);Assert.AreEqual(2,p.FalseLife);Assert.AreEqual(20,p.Health);Assert.AreEqual(3,p.TakeDamageResolved(5));Assert.AreEqual(17,p.Health);}
  [Test] public void Ai_drops_finishing_bonus_when_spore_life_absorbs_the_blow()
  {
      var g=Match();var target=g.PlayerA;var attacker=g.PlayerB;
      target.SetHealthExact(3);target.LeftHand.SetValue(3);
      var move=new LegalMove(attacker,attacker.LeftHand,target.LeftHand);
      var ai=new GreedyChoiceProvider{Game=g};
      int exposed=ai.ScoreMoveForTest(move,attacker,g);
      target.GainSporeLife();
      Assert.AreEqual(0,ai.TotalLethalDamage(move,attacker,target,4));
      Assert.IsTrue(exposed-ai.ScoreMoveForTest(move,attacker,g)>ai.LethalBonus/2);
      Assert.AreEqual(10,target.SporeLife);
  }
  [Test] public void Halo_requires_spore_life_ignores_health_comparison_and_only_runs_on_own_turn()
  {var g=Match();g.PlayerA.SetHealthExact(3);g.PlayerA.GainSporeLife();g.PlayerA.LeftHand.SetValue(1);g.PlayerA.RightHand.SetValue(2);g.BeginTurn();Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Decay));g.PlayerB.RemoveStatus(StatusEffect.Decay);g.ForceActivePlayerForTest(g.PlayerB);g.BeginTurn();Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Decay));}
  [Test] public void Halo_does_not_trigger_without_spore_life_even_against_lower_health()
  {var g=Match();g.PlayerB.SetHealthExact(1);g.PlayerA.LeftHand.SetValue(1);g.PlayerA.RightHand.SetValue(2);g.BeginTurn();Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Decay));}
  [Test] public void Halo_affects_adjacent_allies_and_enemies_but_not_distant_seats()
  {
   var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.CircleOfSpores),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
   var g=GameManager.Create2v2(p,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});p[0].GainSporeLife();g.BeginTurn();
   Assert.AreEqual(2,p[1].StatusTurnsLeft(StatusEffect.Decay));Assert.AreEqual(2,p[3].StatusTurnsLeft(StatusEffect.Decay));Assert.IsFalse(p[2].HasStatus(StatusEffect.Decay));Assert.IsFalse(p[0].HasStatus(StatusEffect.Decay));
  }
  [Test] public void Death_restores_charge_once_even_at_match_end_but_sheep_break_does_not()
  {var g=Match();var p=g.PlayerA;p.SpendWildCharge();g.PlayerB.TryPolymorph();g.PlayerB.TakeDamage(5);g.RefreshGameOver();Assert.AreEqual(1,p.WildCharges);g.PlayerB.TakeDamage(99);g.RefreshGameOver();Assert.AreEqual(2,p.WildCharges);g.RefreshGameOver();Assert.AreEqual(2,p.WildCharges);}
  [Test] public void Death_proximity_uses_pre_death_ring_and_obstacles()
  {var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.CircleOfSpores),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};var g=GameManager.Create2v2(p,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});p[0].SpendWildCharge();p[2].TakeDamage(99);g.RefreshGameOver();Assert.AreEqual(1,p[0].WildCharges);g=Match();g.ConfigureSeatObstacles(1,1);g.PlayerA.SpendWildCharge();g.PlayerB.TakeDamage(99);g.RefreshGameOver();Assert.AreEqual(1,g.PlayerA.WildCharges);}
  [Test] public void Poison_type_ends_when_spore_life_is_exhausted()
  {var g=Match();var p=g.PlayerA;p.GainSporeLife();var attack=new AttackContext(p,g.PlayerB,p.LeftHand,4,g);Assert.AreEqual(AttackDamageType.Poison,((IAttackTypeProvider)p.Character).AttackTypeOf(attack));p.TakeDamage(10);Assert.AreEqual(AttackDamageType.Bludgeoning,((IAttackTypeProvider)p.Character).AttackTypeOf(attack));}
  [Test] public void Holy_light_blocks_halo_but_not_independent_spore_buffer()
  {var g=Match();g.PlayerB.SetHealthExact(19);g.PlayerB.ApplyStatus(StatusEffect.HolyLight,2,g.PlayerA);g.PlayerA.ApplyStatus(StatusEffect.HolyLight,2,g.PlayerB);g.BeginTurn();Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Decay));Assert.AreEqual(10,g.PlayerA.SporeLife);}
  [Test] public void Dual_druids_share_initial_four_charge_cap_and_ai_can_activate()
  {var p=DualGeneralFactory.Create("双德",CharacterDefinition.CircleOfTheLand,CharacterDefinition.CircleOfSpores);var g=new GameManager(p,CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new GreedyChoiceProvider(),new SimpleChoiceProvider());Assert.AreEqual(4,p.WildCharges);for(int i=0;i<6;i++)p.RestoreWildCharge();Assert.AreEqual(4,p.WildCharges);g.BeginTurn();Assert.AreEqual(10,p.SporeLife);Assert.AreEqual(3,p.WildCharges);Assert.AreEqual(Alignment.TrueNeutral,CharacterCodex.For(CharacterDefinition.CircleOfSporesId).Alignment);}
 }
}
