using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class DruidChoiceFlowTests
 {
  private static BoardController Board(CharacterDefinition role,out GameManager game){game=CharacterFactory.CreateMatch(role,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());game.PlayerA.LeftHand.SetValue(4);game.PlayerA.RightHand.SetValue(6);return new BoardController(game,(IPlayerChoiceSink)game.ChoiceProviderA,(IPlayerChoiceSink)game.ChoiceProviderB);}
  [Test] public void Moon_may_decline_without_spending_or_skipping(){var b=Board(CharacterDefinition.CircleOfTheMoon,out var g);b.BeginTurn();Assert.AreEqual(7,b.PendingSkillOptions.Count);b.ChooseSkillOption(0);Assert.AreEqual(0,g.TurnCount);Assert.AreEqual(3,g.PlayerA.WildCharges);Assert.IsFalse(g.PlayerA.IsWildShaped);}
  [Test] public void Stars_require_wild_then_star_and_one_charge(){var b=Board(CharacterDefinition.CircleOfTheStars,out var g);b.BeginTurn();Assert.AreEqual(5,b.PendingSkillOptions.Count);b.ChooseSkillOption(4);Assert.AreEqual(4,b.PendingSkillOptions.Count);b.ChooseSkillOption(1);Assert.AreEqual(1,g.TurnCount);Assert.AreEqual(1,g.PlayerA.WildCharges);Assert.AreEqual(4,g.PlayerA.WildForm);Assert.AreEqual(1,g.PlayerA.WildStar);b.ChooseSkillOption(0);Assert.AreEqual(g.PlayerB,b.ActivePlayer);}
  [Test] public void Cancelling_star_choice_does_not_spend_charge(){var b=Board(CharacterDefinition.CircleOfTheStars,out var g);b.BeginTurn();b.ChooseSkillOption(2);b.ChooseSkillOption(0);Assert.AreEqual(0,g.TurnCount);Assert.AreEqual(2,g.PlayerA.WildCharges);Assert.IsFalse(g.PlayerA.IsWildShaped);}
  [Test] public void Dragon_optional_start_then_mandatory_end_are_separate_from_action(){var b=Board(CharacterDefinition.CircleOfTheStars,out var g);b.BeginTurn();b.ChooseSkillOption(2);b.ChooseSkillOption(3);b.ChooseSkillOption(0);g.ForceActivePlayerForTest(g.PlayerA);b.BeginTurn();Assert.IsTrue(b.Hint.Contains("巨龙座"));Assert.IsTrue(b.PendingWizardChoice.AllowDecline);b.ChooseWizardHand(null);g.PlayerA.LeftHand.SetValue(1);g.PlayerA.RightHand.SetValue(2);g.PlayerB.LeftHand.SetValue(1);b.ClickHand(g.PlayerA.LeftHand);b.ClickHand(g.PlayerB.LeftHand);Assert.IsTrue(b.Hint.Contains("巨龙座"));Assert.IsFalse(b.PendingWizardChoice.AllowDecline);Assert.IsTrue(b.ChooseWizardHand(g.PlayerA.LeftHand));Assert.IsTrue(b.ChooseWizardHand(g.PlayerB.LeftHand));Assert.AreEqual(3,g.PlayerA.LeftHand.Value);Assert.IsTrue(b.Hint.Contains("解除形态"));}
  [Test] public void Chalice_may_select_self(){var b=Board(CharacterDefinition.CircleOfTheStars,out var g);b.BeginTurn();b.ChooseSkillOption(2);b.ChooseSkillOption(2);b.ChooseSkillOption(0);g.ForceActivePlayerForTest(g.PlayerA);g.PlayerA.SetHealthExact(1);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(8);b.BeginTurn();b.ClickHand(g.PlayerA.LeftHand);b.ClickHand(g.PlayerB.LeftHand);Assert.IsTrue(b.PendingWizardChoice.TargetsPlayer);Assert.IsTrue(b.ChooseWizardHand(g.PlayerA.LeftHand));Assert.AreEqual(14,g.PlayerA.Health);}
 }
}
