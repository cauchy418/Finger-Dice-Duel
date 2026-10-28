using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class OperationDisplayTests
 {
  private static BoardController Make(CharacterDefinition a,CharacterDefinition b,out GameManager g)
  {var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();g=CharacterFactory.CreateMatch(a,b,ca,cb);return new BoardController(g,ca,cb);}
  [Test] public void Number_changes_visually_before_target_without_premature_damage_or_double_addition()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.Blank,out var g);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);board.BeginTurn();
   board.ClickHand(a.LeftHand);Assert.True(board.PreviewHandValue(a.LeftHand)==null);board.ClickHand(b.LeftHand);
   Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);Assert.AreEqual(4,board.PreviewHandValue(a.LeftHand).Value);Assert.AreEqual(1,a.LeftHand.Value);Assert.AreEqual(20,b.Health);Assert.AreEqual(0,g.TurnCount);
   board.ClickHand(b.LeftHand);Assert.AreEqual(4,a.LeftHand.Value);Assert.AreEqual(16,b.Health);Assert.AreEqual(1,board.LastTurn.ActedHandValueBefore);Assert.True(board.PreviewHandValue(a.LeftHand)==null);}
  [Test] public void Evocation_result_visible_before_abandon_or_element_choice()
  {var board=Make(CharacterDefinition.SchoolOfEvocation,CharacterDefinition.Blank,out var g);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.AreEqual(4,board.PreviewHandValue(g.PlayerA.LeftHand).Value);Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(0,g.TurnCount);}
  [Test] public void Formed_source_shield_projects_new_digit_without_breaking_early()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.Blank,out var g);g.PlayerA.LeftHand.SetValue(5);g.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);g.PlayerB.LeftHand.SetValue(1);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.AreEqual(6,board.PreviewHandValue(g.PlayerA.LeftHand).Value);Assert.True(g.PlayerA.LeftHand.IsShield);board.ClickHand(g.PlayerB.LeftHand);Assert.AreEqual(6,g.PlayerA.LeftHand.Value);Assert.False(g.PlayerA.LeftHand.IsShield);}
  [Test] public void Frozen_result_projection_matches_final_arithmetic()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.Blank,out var g);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(7);g.PlayerA.ApplyStatus(StatusEffect.Frost,2);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.AreEqual(4,board.PreviewHandValue(g.PlayerA.LeftHand).Value);board.ClickHand(g.PlayerB.LeftHand);Assert.AreEqual(4,g.PlayerA.LeftHand.Value);}
  [Test] public void No_legal_attack_and_giveup_keep_visible_result_then_commit_once()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.Assassin,out var g);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(7);g.PlayerB.RightHand.SetValue(7);((FingerDiceDuel.Characters.SpecialLogics.AssassinLogic)g.PlayerB.Character).EnterStealthForTest();board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.AreEqual(8,board.PreviewHandValue(g.PlayerA.LeftHand).Value);Assert.True(board.CanGiveUpAttack);board.GiveUpAttack();Assert.AreEqual(8,g.PlayerA.LeftHand.Value);Assert.AreEqual(20,g.PlayerB.Health);Assert.True(board.PreviewHandValue(g.PlayerA.LeftHand)==null);}
  [Test] public void Trickery_projects_both_exchanged_digits_before_target()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain,out var g);g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(4);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.True(board.PreviewHandValue(g.PlayerA.LeftHand)==null);board.ChooseSkillOption(1);board.ChooseSkillOption(0);
   Assert.AreEqual(4,board.PreviewHandValue(g.PlayerA.LeftHand).Value);Assert.AreEqual(6,board.PreviewHandValue(g.PlayerB.LeftHand).Value);Assert.AreEqual(4,g.PlayerB.LeftHand.Value);
   board.ClickHand(g.PlayerB.LeftHand);Assert.AreEqual(4,g.PlayerA.LeftHand.Value);Assert.AreEqual(6,g.PlayerB.LeftHand.Value);}
  [Test] public void Dragon_waits_for_second_addend_then_projects_final_result()
  {var board=Make(CharacterDefinition.CircleOfTheStars,CharacterDefinition.Blank,out var g);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(4);a.RightHand.SetValue(6);b.LeftHand.SetValue(1);b.RightHand.SetValue(3);
   ((FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic)a.Character).TryEnter(FingerDiceDuel.Characters.SpecialLogics.CircleOfTheStarsLogic.StarryForm.Dragon,a,g);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);Assert.True(board.PreviewHandValue(a.LeftHand)==null);board.ClickHand(b.RightHand);
   Assert.AreEqual(8,board.PreviewHandValue(a.LeftHand).Value);board.ClickHand(b.LeftHand);Assert.AreEqual(8,a.LeftHand.Value);}
 }
}
