using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
 [TestFixture] public class OperationDisplayTests
 {
  [Test] public void Tactician_chooses_before_preview_and_uses_status_adjusted_difference()
  {
   var board=Make(CharacterDefinition.Tactician,CharacterDefinition.Blank,out var g);
   var actor=g.PlayerA;var defender=g.PlayerB;
   actor.LeftHand.SetValue(4);defender.LeftHand.SetValue(6);actor.ApplyStatus(StatusEffect.Frost,2);board.BeginTurn();
   Assert.True(board.ClickHand(actor.LeftHand));Assert.True(board.ClickHand(defender.LeftHand));
   Assert.IsTrue(board.PendingWizardChoice.Prompt.Contains("战术眼光"));
   Assert.True(board.PreviewHandValue(actor.LeftHand)==null);
   Assert.True(board.ChooseSkillOption(1));
   Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);
   Assert.AreEqual(4,board.PreviewHandValue(actor.LeftHand).Value);
   Assert.True(board.ConfirmTargetPlayer());Assert.True(board.LastTurn.WasSubtraction);
   Assert.AreEqual(4,board.LastTurn.DamageDealt);
   Assert.IsTrue(BattleTurnText.Describe(board.LastTurn).Contains("4 − 6 = 8"));
  }

  [Test] public void Tactician_ai_can_choose_the_only_attack_available_by_subtraction()
  {
   var a=CharacterFactory.CreatePlayer("战术家",CharacterDefinition.Tactician);
   var b=CharacterFactory.CreatePlayer("对手",CharacterDefinition.Blank);
   var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());
   a.LeftHand.SetValue(4);a.RightHand.SetValue(4);b.LeftHand.SetValue(6);b.RightHand.SetValue(6);
   var move=ai.ChooseMove(g,a);
   var result=g.ExecuteTurn(a,move.ActedHand,move.TargetHand);
   Assert.True(result.WasSubtraction);Assert.AreEqual(8,result.DamageDealt);
   Assert.AreEqual(1,Hand.ComputeDifference(4,4));Assert.AreEqual(9,Hand.ComputeDifference(1,2));
  }
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

  [Test] public void Reduced_attack_digit_skips_attack_and_skill_targeting()
  {
   foreach (var status in new[] { StatusEffect.Frost, StatusEffect.Wet })
   {
    var board = Make(CharacterDefinition.SchoolOfEvocation, CharacterDefinition.Blank, out var game);
    var actor = game.PlayerA;
    var defender = game.PlayerB;
    actor.LeftHand.SetValue(1);
    defender.LeftHand.SetValue(3);
    actor.ApplyStatus(status, 2);
    board.BeginTurn();

    Assert.True(board.ClickHand(actor.LeftHand));
    Assert.True(board.ClickHand(defender.LeftHand));

    Assert.AreEqual(1, game.TurnCount);
    Assert.AreEqual(status == StatusEffect.Frost ? 2 : 3, actor.LeftHand.Value);
    Assert.AreEqual(HandEffectType.None, board.LastTurn.Effect);
    Assert.AreEqual(20, defender.Health);
    Assert.False(board.CanGiveUpAttack);
   }
  }

  [Test] public void Wet_can_create_an_attack_that_waits_for_target_confirmation()
  {
   var board = Make(CharacterDefinition.Blank, CharacterDefinition.Blank, out var game);
   var actor = game.PlayerA;
   var defender = game.PlayerB;
   actor.LeftHand.SetValue(2);
   defender.LeftHand.SetValue(3);
   actor.ApplyStatus(StatusEffect.Wet, 2);
   board.BeginTurn();

   Assert.True(board.ClickHand(actor.LeftHand));
   Assert.True(board.ClickHand(defender.LeftHand));
   Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
   Assert.AreEqual(4, board.PredictedAttackValue());
   Assert.AreEqual(4, board.PreviewHandValue(actor.LeftHand).Value);
   Assert.AreEqual(20, defender.Health);

   Assert.True(board.ConfirmTargetPlayer());
   Assert.AreEqual(4, actor.LeftHand.Value);
   Assert.AreEqual(16, defender.Health);
  }

  [Test] public void Portent_final_digit_determines_whether_to_choose_an_attack_target()
  {
   foreach (int portent in new[] { 2, 4 })
   {
    var board = Make(CharacterDefinition.Blank, CharacterDefinition.SchoolOfDivination, out var game);
    var actor = game.PlayerA;
    var defender = game.PlayerB;
    actor.LeftHand.SetValue(portent == 2 ? 1 : 9);
    actor.ApplyStatus(StatusEffect.Frost, 2);
    defender.LeftHand.SetValue(7);
    ((SchoolOfDivinationLogic)defender.Character).RecordPortent(portent);
    board.BeginTurn();

    Assert.True(board.ClickHand(actor.LeftHand));
    Assert.True(board.ClickHand(defender.LeftHand));
    Assert.AreEqual(0, game.TurnCount);
    Assert.False(board.Phase == BoardPhase.ChoosingTargetPlayer);
    Assert.True(board.ChooseSkillOption(portent));

    if (portent == 4)
    {
     Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);
     Assert.AreEqual(4, board.PreviewHandValue(actor.LeftHand).Value);
     Assert.True(board.ConfirmTargetPlayer());
    }
    Assert.AreEqual(1, game.TurnCount);
    Assert.AreEqual(portent, actor.LeftHand.Value);
    Assert.AreEqual(portent == 2 ? HandEffectType.None : HandEffectType.Attack4, board.LastTurn.Effect);
   }
  }

  [Test] public void Final_digit_is_interpreted_by_the_subclass_before_choosing_target()
  {
   var board = Make(CharacterDefinition.OathOfDevotion, CharacterDefinition.Blank, out var game);
   var actor = game.PlayerA;
   actor.LeftHand.SetValue(1);
   actor.ApplyStatus(StatusEffect.Frost, 2);
   game.PlayerB.LeftHand.SetValue(7);
   board.BeginTurn();

   Assert.True(board.ClickHand(actor.LeftHand));
   Assert.True(board.ClickHand(game.PlayerB.LeftHand));
   Assert.AreEqual(1, game.TurnCount);
   Assert.AreEqual(4, actor.LeftHand.Value);
   Assert.AreEqual(HandEffectType.Shield, board.LastTurn.Effect);
   Assert.True(actor.LeftHand.IsShield);
  }
  [Test] public void No_legal_attack_and_giveup_keep_visible_result_then_commit_once()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.Assassin,out var g);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(7);g.PlayerB.RightHand.SetValue(7);((FingerDiceDuel.Characters.SpecialLogics.AssassinLogic)g.PlayerB.Character).EnterStealthForTest();board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.AreEqual(8,board.PreviewHandValue(g.PlayerA.LeftHand).Value);Assert.True(board.CanGiveUpAttack);board.GiveUpAttack();Assert.AreEqual(8,g.PlayerA.LeftHand.Value);Assert.AreEqual(20,g.PlayerB.Health);Assert.True(board.PreviewHandValue(g.PlayerA.LeftHand)==null);}
  [Test] public void Trickery_projects_both_exchanged_digits_before_target()
  {var board=Make(CharacterDefinition.Blank,CharacterDefinition.TrickeryDomain,out var g);g.PlayerA.LeftHand.SetValue(2);g.PlayerB.LeftHand.SetValue(4);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
   Assert.True(board.PreviewHandValue(g.PlayerA.LeftHand)==null);board.ChooseSkillOption(1);board.ChooseSkillOption(0);
   Assert.AreEqual(4,board.PreviewHandValue(g.PlayerA.LeftHand).Value);Assert.AreEqual(6,board.PreviewHandValue(g.PlayerB.LeftHand).Value);Assert.AreEqual(4,g.PlayerB.LeftHand.Value);
   board.ClickHand(g.PlayerB.LeftHand);Assert.AreEqual(4,g.PlayerA.LeftHand.Value);Assert.AreEqual(6,g.PlayerB.LeftHand.Value);}

  [Test] public void Trickery_can_remove_an_attack_before_asking_for_a_target()
  {
   var board = Make(CharacterDefinition.Blank, CharacterDefinition.TrickeryDomain, out var game);
   var actor = game.PlayerA;
   var defender = game.PlayerB;
   actor.LeftHand.SetValue(2);
   defender.LeftHand.SetValue(2);
   board.BeginTurn();

   Assert.True(board.ClickHand(actor.LeftHand));
   Assert.True(board.ClickHand(defender.LeftHand));
   Assert.True(board.ChooseSkillOption(1));
   Assert.True(board.ChooseSkillOption(0));

   Assert.AreEqual(1, game.TurnCount);
   Assert.AreEqual(2, actor.LeftHand.Value);
   Assert.AreEqual(4, defender.LeftHand.Value);
   Assert.AreEqual(20, defender.Health);
   Assert.AreEqual(HandEffectType.None, board.LastTurn.Effect);
  }

 }
}
