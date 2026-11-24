using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class DeathDomainTests
 {
  private static BoardController Board(out GameManager game,out PlayerState dead)
  {
   var players=new[]{CharacterFactory.CreatePlayer("牧师",CharacterDefinition.DeathDomain),new PlayerState("敌人"),new PlayerState("死者"),new PlayerState("队友")};
   var providers=new[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()};
   game=GameManager.Create2v2(players,providers);dead=players[2];dead.LeftHand.SetValue(3);dead.RightHand.SetValue(8);dead.TakeDamage(99);
   game.PlayerA.LeftHand.SetValue(1);game.PlayerB.LeftHand.SetValue(2);game.PlayerB.RightHand.SetValue(3);
   return new BoardController(game,providers[0],providers[1]);
  }
  [Test] public void Death_domain_is_selectable_in_cleric_and_neutral_evil_filters()
  {
   Assert.True(System.Array.IndexOf(CharacterDefinition.BuiltIn,CharacterDefinition.DeathDomain)>=0);
   Assert.AreEqual("cleric",CharacterDefinition.DeathDomain.Profession.Id);
   Assert.AreEqual(Alignment.NeutralEvil,CharacterCodex.For(CharacterDefinition.DeathDomain).Alignment);
   var p=CharacterFactory.CreatePlayer("死亡",CharacterDefinition.DeathDomain);
   Assert.AreEqual(0,p.Character.AttackBonus);Assert.AreEqual(-1,p.Character.DefenseBonus);
   Assert.True(CharacterSkillText.TryGet(CharacterDefinition.DeathDomainId,out var text));Assert.True(text.Contains("死亡之触"));
  }
  [Test] public void Board_dead_addend_preserves_values_and_hands_off_necrotic_attack_to_live_target()
  {
   var board=Board(out var g,out var dead);g.PlayerB.AddPermanentImmunity(AttackDamageType.Bludgeoning);
   board.BeginTurn();Assert.True(board.ClickHand(g.PlayerA.LeftHand));Assert.True(board.CanClick(dead.LeftHand));
   Assert.True(board.ClickHand(dead.LeftHand));Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);
   Assert.False(board.CanClick(dead.LeftHand));Assert.True(board.ClickHand(g.PlayerB.RightHand));
   Assert.AreEqual(16,g.PlayerB.Health);Assert.AreEqual(4,g.PlayerA.LeftHand.Value);Assert.AreEqual(3,dead.LeftHand.Value);Assert.AreEqual(8,dead.RightHand.Value);
   Assert.AreEqual(AttackDamageType.Necrotic,board.LastTurn.AttackType);
  }
  [Test] public void Board_can_cancel_dead_addend_selection_without_consuming_turn()
  {
   var board=Board(out var g,out var dead);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);
   Assert.True(board.CanClick(dead.LeftHand));Assert.True(board.ClickHand(g.PlayerA.LeftHand));
   Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);Assert.AreEqual(0,g.TurnCount);Assert.AreEqual(1,g.PlayerA.LeftHand.Value);
  }
  [Test] public void Board_healing_blessing_is_optional_player_choice_and_can_be_declined()
  {
   foreach(bool accept in new[]{false,true}){
    var board=Board(out var g,out var dead);g.PlayerA.LeftHand.SetValue(6);g.PlayerB.TakeDamage(10);
    board.BeginTurn();Assert.True(board.ClickHand(g.PlayerA.LeftHand));Assert.True(board.ClickHand(dead.LeftHand));
    Assert.True(board.ClickHand(g.PlayerB.LeftHand),board.Phase+" "+board.Hint);
    Assert.True(board.Hint.Contains("死亡赐福"),board.Phase+" "+board.Hint);Assert.True(board.PendingWizardChoice.TargetsPlayer);
    Assert.AreEqual(19,g.PlayerB.Health);Assert.False(g.PlayerB.HasStatus(StatusEffect.FeignedDeath));
    Assert.True(board.ChooseWizardHand(accept?g.PlayerB.LeftHand:null));Assert.AreEqual(accept,g.PlayerB.HasStatus(StatusEffect.FeignedDeath));
    Assert.AreEqual(1,g.TurnCount);
   }
  }
  [Test] public void Other_roles_cannot_select_dead_or_feigned_addends_but_death_can_select_feigned()
  {
   var board=Board(out var g,out var dead);g.PlayerB.ApplyStatus(StatusEffect.FeignedDeath,2);
   board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);Assert.True(board.CanClick(g.PlayerB.LeftHand));
   var normal=CharacterFactory.CreateMatch(CharacterDefinition.Champion,CharacterDefinition.Blank);
   normal.PlayerB.ApplyStatus(StatusEffect.FeignedDeath,2);var a=new SimpleChoiceProvider();var b=new SimpleChoiceProvider();
   var other=new BoardController(normal,a,b);other.BeginTurn();other.ClickHand(normal.PlayerA.LeftHand);
   Assert.False(other.CanClick(normal.PlayerB.LeftHand));
   g.ForceActivePlayerForTest(g.PlayerB);board.BeginTurn();board.ClickHand(g.PlayerB.LeftHand);Assert.False(board.CanClick(dead.LeftHand));
  }
  [Test] public void Board_feigned_teammate_addend_converts_attack_but_living_addend_keeps_physical_type()
  {
   foreach(bool feigned in new[]{false,true}){
    var board=Board(out var g,out var dead);g.PlayerD.LeftHand.SetValue(3);
    if(feigned)g.PlayerD.ApplyStatus(StatusEffect.FeignedDeath,2);
    board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);Assert.True(board.ClickHand(g.PlayerD.LeftHand));
    Assert.True(board.ClickHand(g.PlayerB.LeftHand));
    Assert.AreEqual(feigned?AttackDamageType.Necrotic:AttackDamageType.Bludgeoning,board.LastTurn.AttackType);
   }
  }
 }
}
