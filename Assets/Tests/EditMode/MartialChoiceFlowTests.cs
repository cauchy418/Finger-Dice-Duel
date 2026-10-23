using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class MartialChoiceFlowTests
    {
        private static BoardController Board(CharacterDefinition def,out GameManager game)
        {
            var a=CharacterFactory.CreatePlayer("A",def);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();game=new GameManager(a,b,ca,cb);
            return new BoardController(game,ca,cb);
        }
        private static void Attack(BoardController board,GameManager game)
        {
            game.PlayerB.LeftHand.SetValue(3);board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            if(board.Phase==BoardPhase.ChoosingTargetPlayer) board.ClickHand(game.PlayerB.LeftHand);
        }
        [Test] public void Open_hand_human_selects_stunned_instead_of_default_knockdown()
        {
            var board=Board(CharacterDefinition.WayOfTheOpenHand,out var game);Attack(board,game);
            Assert.AreEqual(HandEffect.OpenHandTechnique,board.PendingEffect);
            Assert.AreEqual(3,board.PendingSkillOptions.Count);Assert.AreEqual(0,game.TurnCount);
            Assert.IsFalse(board.ChooseSkillOption(12345));Assert.IsFalse(board.AcceptPendingChoice());
            Assert.IsTrue(board.ChooseSkillOption((int)StatusEffect.Stunned));
            Assert.IsTrue(game.PlayerB.HasStatus(StatusEffect.Stunned));Assert.IsFalse(game.PlayerB.HasStatus(StatusEffect.KnockedDown));
            Assert.AreEqual(0,((IKiResource)game.PlayerA.Character).Ki);Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Open_hand_can_keep_ki_and_does_not_apply_default_status()
        {
            var board=Board(CharacterDefinition.WayOfTheOpenHand,out var game);Attack(board,game);board.ChooseSkillOption(0);
            Assert.AreEqual(2,((IKiResource)game.PlayerA.Character).Ki);
            Assert.IsFalse(game.PlayerB.HasStatus(StatusEffect.KnockedDown));Assert.IsFalse(game.PlayerB.HasStatus(StatusEffect.Stunned));
        }
        [Test] public void Four_elements_human_selects_cold_and_spends_only_once()
        {
            var board=Board(CharacterDefinition.WayOfTheFourElements,out var game);Attack(board,game);
            Assert.AreEqual(HandEffect.FourElementsTechnique,board.PendingEffect);Assert.AreEqual(6,board.PendingSkillOptions.Count);
            Assert.IsTrue(board.ChooseSkillOption((int)AttackDamageType.Cold));
            Assert.IsTrue(game.PlayerB.HasStatus(StatusEffect.Frost));Assert.IsFalse(game.PlayerB.HasStatus(StatusEffect.Burn));
            Assert.AreEqual(0,((IKiResource)game.PlayerA.Character).Ki);Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Four_elements_can_save_ki_and_keep_physical_attack()
        {
            var board=Board(CharacterDefinition.WayOfTheFourElements,out var game);Attack(board,game);board.ChooseSkillOption(0);
            Assert.AreEqual(2,((IKiResource)game.PlayerA.Character).Ki);Assert.IsFalse(game.PlayerB.HasStatus(StatusEffect.Burn));
        }
        [Test] public void Draconic_first_turn_requires_one_explicit_element_and_grants_only_that_resistance()
        {
            var board=Board(CharacterDefinition.DraconicBloodline,out var game);
            Assert.IsFalse(game.PlayerA.HasResistance(AttackDamageType.Fire));board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(game.PlayerA,board.Chooser);
            Assert.AreEqual(5,board.PendingSkillOptions.Count);Assert.IsFalse(board.DeclinePendingChoice());
            Assert.IsFalse(board.ChooseWizardHand(game.PlayerB.LeftHand));Assert.IsFalse(board.ChooseSkillOption((int)AttackDamageType.Force));
            Assert.IsTrue(board.ChooseSkillOption((int)AttackDamageType.Cold));
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);Assert.AreEqual(0,game.TurnCount);
            Assert.IsTrue(game.PlayerA.HasResistance(AttackDamageType.Cold));Assert.IsFalse(game.PlayerA.HasResistance(AttackDamageType.Fire));
            ((DraconicBloodlineLogic)game.PlayerA.Character).ChooseElement(AttackDamageType.Poison);
            Assert.AreEqual(AttackDamageType.Cold,((DraconicBloodlineLogic)game.PlayerA.Character).Element);
            board.BeginTurn();Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);
        }
        [Test] public void Draconic_reset_asks_again_and_does_not_keep_old_permanent_resistance()
        {
            var board=Board(CharacterDefinition.DraconicBloodline,out var game);board.BeginTurn();board.ChooseSkillOption((int)AttackDamageType.Acid);
            game.ResetMatch();board.BeginTurn();Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);
            Assert.IsFalse(game.PlayerA.HasResistance(AttackDamageType.Acid));board.ChooseSkillOption((int)AttackDamageType.Poison);
            Assert.IsTrue(game.PlayerA.HasResistance(AttackDamageType.Poison));Assert.IsFalse(game.PlayerA.HasResistance(AttackDamageType.Fire));
        }
        [Test] public void Hunter_seven_waits_for_target_and_supports_decline()
        {
            var board=Board(CharacterDefinition.RangerHunter,out var game);game.PlayerB.LeftHand.SetValue(6);board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(game.PlayerA,board.Chooser);
            Assert.IsTrue(board.PendingWizardChoice.TargetsPlayer);Assert.AreEqual("B",board.WizardHandLabel(game.PlayerB.LeftHand));
            Assert.IsFalse(((IHunterMarkSource)game.PlayerA.Character).IsMarked(game.PlayerB));
            Assert.IsTrue(board.DeclinePendingChoice());Assert.IsFalse(((IHunterMarkSource)game.PlayerA.Character).IsMarked(game.PlayerB));
            Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Hunter_target_selection_applies_to_chosen_character()
        {
            var board=Board(CharacterDefinition.RangerHunter,out var game);game.PlayerB.LeftHand.SetValue(6);board.BeginTurn();
            board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            Assert.IsFalse(board.ChooseWizardHand(game.PlayerA.LeftHand));Assert.IsTrue(board.ChooseWizardHand(game.PlayerB.LeftHand));
            Assert.IsTrue(((IHunterMarkSource)game.PlayerA.Character).IsMarked(game.PlayerB));Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Ai_four_elements_exploits_wet_and_preserves_ki_for_low_health_defense()
        {
            var board=Board(CharacterDefinition.WayOfTheFourElements,out var game);var ai=new GreedyChoiceProvider();
            game.PlayerB.ApplyStatus(StatusEffect.Wet,2,game.PlayerA);
            Assert.AreEqual((int)AttackDamageType.Cold,ai.ChooseSkillOption(MartialSkillChoices.FourElements,MartialSkillChoices.Elements(false),game.PlayerA,game.PlayerB,game));
            game.PlayerB.ClearStatuses();game.PlayerA.SetHealthExact(5);
            Assert.AreEqual(0,ai.ChooseSkillOption(MartialSkillChoices.FourElements,MartialSkillChoices.Elements(false),game.PlayerA,game.PlayerB,game));
        }
        [Test] public void Ai_open_hand_uses_control_avoids_duplicate_and_can_choose_resonance()
        {
            var board=Board(CharacterDefinition.WayOfTheOpenHand,out var game);var ai=new GreedyChoiceProvider();
            var options=MartialSkillChoices.OpenHandOptions(2);
            Assert.AreEqual((int)StatusEffect.Stunned,ai.ChooseSkillOption(MartialSkillChoices.OpenHand,options,game.PlayerA,game.PlayerB,game));
            game.PlayerB.ApplyStatus(StatusEffect.Stunned,2,game.PlayerA);
            Assert.AreEqual(0,ai.ChooseSkillOption(MartialSkillChoices.OpenHand,options,game.PlayerA,game.PlayerB,game));
            game.PlayerB.ClearStatuses();
            Assert.AreEqual(-1,ai.ChooseSkillOption(MartialSkillChoices.OpenHand,MartialSkillChoices.OpenHandOptions(4),game.PlayerA,game.PlayerB,game));
        }
        [Test] public void Ai_draconic_avoids_resisted_element_and_initializes_without_modal()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DraconicBloodline);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=new GameManager(a,b,ca,cb);
            foreach(var type in new[]{AttackDamageType.Fire,AttackDamageType.Lightning,AttackDamageType.Poison,AttackDamageType.Acid}) b.AddPermanentResistance(type);
            var board=new BoardController(game,ca,cb,new GreedyChoiceProvider(),null);board.BeginTurn();
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);Assert.AreEqual(AttackDamageType.Cold,((DraconicBloodlineLogic)a.Character).Element);
            Assert.IsTrue(a.HasResistance(AttackDamageType.Cold));Assert.IsFalse(a.HasResistance(AttackDamageType.Fire));
        }
        [Test] public void Ai_hunter_prefers_wounded_enemy_and_avoids_ally_and_duplicate()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.RangerHunter);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Champion);var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Champion);
            var ai=new GreedyChoiceProvider();var game=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{ai,ai,ai,ai});
            c.TakeDamage(10);d.TakeDamage(15);var candidates=new[]{b,c,d};
            Assert.AreEqual(c,ai.HunterMarkTarget(a,candidates,game));((RangerHunterLogic)a.Character).Mark(c);
            Assert.AreEqual(b,ai.HunterMarkTarget(a,candidates,game));
        }
        [Test] public void Queued_ai_hunter_uses_real_ai_policy_instead_of_human_sink()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.RangerHunter);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=new GameManager(a,b,ca,cb);
            var board=new BoardController(game,ca,cb,new GreedyChoiceProvider(),null);b.LeftHand.SetValue(6);board.BeginTurn();
            board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            Assert.IsTrue(((RangerHunterLogic)a.Character).IsMarked(b));Assert.IsTrue(board.PendingWizardChoice==null);
        }
        [Test] public void Both_monk_components_in_dual_have_independent_options_and_ki()
        {
            var a=new PlayerState("A",new DualGeneralBehavior(new WayOfTheOpenHandLogic(),new WayOfTheFourElementsLogic()));
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=new GameManager(a,b,ca,cb);
            var board=new BoardController(game,ca,cb);Attack(board,game);
            Assert.AreEqual(HandEffect.OpenHandTechnique,board.PendingEffect);board.ChooseSkillOption((int)StatusEffect.Stunned);
            Assert.AreEqual(HandEffect.FourElementsTechnique,board.PendingEffect);board.ChooseSkillOption((int)AttackDamageType.Cold);
            Assert.IsTrue(b.HasStatus(StatusEffect.Stunned));Assert.IsTrue(b.HasStatus(StatusEffect.Frost));
            foreach(var ki in CharacterBehaviors.Implementers<IKiResource>(a)) Assert.AreEqual(0,ki.Ki);
            Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Four_elements_all_five_player_choices_use_matching_damage_and_status()
        {
            foreach(var type in new[]{AttackDamageType.Fire,AttackDamageType.Cold,AttackDamageType.Lightning,AttackDamageType.Thunder,AttackDamageType.Acid})
            {
                var board=Board(CharacterDefinition.WayOfTheFourElements,out var game);Attack(board,game);board.ChooseSkillOption((int)type);
                Assert.AreEqual(type,board.LastTurn.AttackType);
                Assert.IsTrue(game.PlayerB.HasStatus(StatusNames.StatusOf(AttackDamageTypes.ToDamageType(type))),type.ToString());
            }
        }
        [Test] public void Draconic_chosen_element_applies_its_three_turn_status_on_real_attack()
        {
            var board=Board(CharacterDefinition.DraconicBloodline,out var game);board.BeginTurn();board.ChooseSkillOption((int)AttackDamageType.Acid);
            game.PlayerB.LeftHand.SetValue(3);board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            if(board.Phase==BoardPhase.ChoosingTargetPlayer) board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(AttackDamageType.Acid,board.LastTurn.AttackType);Assert.IsTrue(game.PlayerB.HasStatus(StatusEffect.Acid));
        }
        [Test] public void Ai_choice_flow_answers_monk_options_and_advances_once()
        {
            foreach(var def in new[]{CharacterDefinition.WayOfTheOpenHand,CharacterDefinition.WayOfTheFourElements})
            {
                var a=CharacterFactory.CreatePlayer("A",def);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
                var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=new GameManager(a,b,ca,cb);
                var board=new BoardController(game,ca,cb,new GreedyChoiceProvider(),null);Attack(board,game);
                Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(0,board.PendingSkillOptions.Count);
                Assert.AreEqual(0,((IKiResource)a.Character).Ki);
            }
        }
    }
}
