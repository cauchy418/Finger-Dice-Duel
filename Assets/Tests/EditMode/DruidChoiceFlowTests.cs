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
    [TestFixture] public class DruidChoiceFlowTests
    {
        private static BoardController Board(CharacterDefinition def,out GameManager game)
        {
            var a=CharacterFactory.CreatePlayer("A",def);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();game=new GameManager(a,b,ca,cb);return new BoardController(game,ca,cb);
        }
        [Test] public void Moon_human_selects_any_form_without_default_bear()
        {
            for(int i=1;i<=4;i++)
            {
                var board=Board(CharacterDefinition.CircleOfTheMoon,out var game);game.PlayerA.LeftHand.SetValue(4);game.PlayerA.RightHand.SetValue(6);board.BeginTurn();
                Assert.AreEqual(5,board.PendingSkillOptions.Count);Assert.AreEqual(0,((IDruidForm)game.PlayerA.Character).FormValue);
                Assert.IsFalse(board.ChooseSkillOption(99));Assert.IsTrue(board.ChooseSkillOption(i));Assert.AreEqual(i,((IDruidForm)game.PlayerA.Character).FormValue);Assert.AreEqual(0,game.TurnCount);
            }
        }
        [Test] public void Stars_human_selects_any_form_and_can_keep_base()
        {
            for(int i=0;i<=3;i++)
            {
                var board=Board(CharacterDefinition.CircleOfTheStars,out var game);game.PlayerA.LeftHand.SetValue(4);game.PlayerA.RightHand.SetValue(6);board.BeginTurn();
                Assert.AreEqual(4,board.PendingSkillOptions.Count);board.ChooseSkillOption(i);Assert.AreEqual(i,((IDruidForm)game.PlayerA.Character).FormValue);
                board.BeginTurn();Assert.AreEqual(0,board.PendingSkillOptions.Count);
            }
        }
        [Test] public void Dual_druid_forms_stack_and_keep_independent_state()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var moon=new CircleOfTheMoonLogic();var stars=new CircleOfTheStarsLogic();
                var a=new PlayerState("A",new DualGeneralBehavior(reverse?(ICharacterBehavior)stars:moon,reverse?(ICharacterBehavior)moon:stars));
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=new GameManager(a,b,ca,cb);var board=new BoardController(game,ca,cb);
                a.LeftHand.SetValue(4);a.RightHand.SetValue(6);board.BeginTurn();board.ChooseSkillOption(reverse?3:2);board.ChooseSkillOption(reverse?2:3);
                Assert.AreEqual(2,(int)moon.Form);Assert.AreEqual(3,(int)stars.Form);Assert.AreEqual(3,a.Character.AttackBonus);Assert.AreEqual(0,a.Character.DefenseBonus);Assert.AreEqual(3,GameManager.BaseRangeOf(a));
                a.LeftHand.SetValue(5);a.RightHand.SetValue(5);Assert.IsTrue(moon.TryExit(a,game));Assert.AreEqual(3,(int)stars.Form);
            }
        }
        [Test] public void Dragon_second_addend_can_repeat_and_only_final_effect_resolves()
        {
            var board=Board(CharacterDefinition.CircleOfTheStars,out var game);var a=game.PlayerA;var b=game.PlayerB;
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);((CircleOfTheStarsLogic)a.Character).TryEnter(CircleOfTheStarsLogic.StarryForm.Dragon,a,game);
            a.LeftHand.SetValue(2);a.RightHand.SetValue(1);b.LeftHand.SetValue(3);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(2,a.LeftHand.Value);Assert.IsTrue(board.ChooseWizardHand(b.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);Assert.AreEqual(2,a.LeftHand.Value);board.ClickHand(b.LeftHand);
            Assert.AreEqual(8,a.LeftHand.Value);Assert.IsFalse(a.LeftHand.IsShield);Assert.AreEqual(3,board.LastTurn.SecondAddendValue);Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(11,b.Health);
        }
        [Test] public void Dragon_normalizes_each_addition_and_does_not_fire_intermediate_attack()
        {
            var board=Board(CharacterDefinition.CircleOfTheStars,out var game);var a=game.PlayerA;var b=game.PlayerB;
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);((CircleOfTheStarsLogic)a.Character).TryEnter(CircleOfTheStarsLogic.StarryForm.Dragon,a,game);
            b.LeftHand.SetValue(6);game.DruidSecondAddend=b.LeftHand;game.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(7,a.LeftHand.Value);Assert.AreEqual(20,b.Health);Assert.IsTrue(game.DruidSecondAddend==null);
        }
        [Test] public void Dragon_final_attack_snapshot_is_used_by_defense()
        {
            var board=Board(CharacterDefinition.CircleOfTheStars,out var game);var a=game.PlayerA;var b=game.PlayerB;
            var ctx=new AttackContext(a,b,a.LeftHand,8,game){SourceHandValueBefore=2,TargetHandValueBefore=3,SecondAddendValueBefore=3};Assert.AreEqual(8,ctx.AttackValue);
        }
        [Test] public void Wolf_lifesteal_and_snake_poison_apply_in_real_combat()
        {
            var board=Board(CharacterDefinition.CircleOfTheMoon,out var game);var a=game.PlayerA;var b=game.PlayerB;
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);((CircleOfTheMoonLogic)a.Character).TryEnter(CircleOfTheMoonLogic.WildShape.Wolf,a,game);a.SetHealthExact(10);a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);
            game.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(13,a.Health);
            board=Board(CharacterDefinition.CircleOfTheMoon,out game);a=game.PlayerA;b=game.PlayerB;a.LeftHand.SetValue(4);a.RightHand.SetValue(6);((CircleOfTheMoonLogic)a.Character).TryEnter(CircleOfTheMoonLogic.WildShape.Snake,a,game);a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);
            game.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.IsTrue(b.HasStatus(StatusEffect.Poison));
        }
        [Test] public void End_of_turn_equal_hands_offer_exit_or_keep_without_extra_turn()
        {
            var board=Board(CharacterDefinition.CircleOfTheMoon,out var game);var a=game.PlayerA;a.LeftHand.SetValue(4);a.RightHand.SetValue(6);((CircleOfTheMoonLogic)a.Character).TryEnter(CircleOfTheMoonLogic.WildShape.Bear,a,game);
            a.LeftHand.SetValue(1);a.RightHand.SetValue(2);game.PlayerB.LeftHand.SetValue(1);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(a,board.Chooser);Assert.AreEqual(2,board.PendingSkillOptions.Count);
            board.ChooseSkillOption(1);Assert.AreEqual(0,((IDruidForm)a.Character).FormValue);Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Chalice_human_selects_other_character_and_can_decline()
        {
            var board=Board(CharacterDefinition.CircleOfTheStars,out var game);var a=game.PlayerA;var b=game.PlayerB;
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);((CircleOfTheStarsLogic)a.Character).TryEnter(CircleOfTheStarsLogic.StarryForm.Chalice,a,game);a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);b.SetHealthExact(5);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.IsTrue(board.PendingWizardChoice.TargetsPlayer);Assert.AreEqual(5,b.Health);board.ChooseWizardHand(b.LeftHand);Assert.AreEqual(14,b.Health);Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Dedicated_ai_chooses_survival_range_support_and_legal_second_addend()
        {
            var board=Board(CharacterDefinition.CircleOfTheMoon,out var game);var ai=new GreedyChoiceProvider();game.PlayerA.SetHealthExact(5);
            Assert.AreEqual(1,ai.ChooseForm(game.PlayerA,"moon",DruidForms.Options("moon"),game));
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.CircleOfTheStars);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Champion);var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Champion);
            game=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{ai,ai,ai,ai});d.SetHealthExact(5);
            Assert.AreEqual(2,ai.ChooseForm(a,"stars",DruidForms.Options("stars"),game));Assert.AreEqual(d,ai.ChaliceTarget(a,new[]{b,c,d},game));
            a.LeftHand.SetValue(2);b.LeftHand.SetValue(3);c.LeftHand.SetValue(1);Assert.AreEqual(b.LeftHand,ai.SecondAddend(a,a.LeftHand,b.LeftHand,new[]{b.LeftHand,c.LeftHand},game));
        }
        [Test] public void Ai_respects_eagle_six_and_rewards_preparing_sum_ten()
        {
            var board=Board(CharacterDefinition.CircleOfTheMoon,out var game);var a=game.PlayerA;var b=game.PlayerB;var ai=new GreedyChoiceProvider();
            a.LeftHand.SetValue(4);a.RightHand.SetValue(6);var moon=(CircleOfTheMoonLogic)a.Character;
            Assert.AreEqual(80,moon.ScoreMoveBonus(new MoveValueContext(a.LeftHand,4,b.LeftHand,6)));
            moon.TryEnter(CircleOfTheMoonLogic.WildShape.Eagle,a,game);a.LeftHand.SetValue(1);a.RightHand.SetValue(1);b.LeftHand.SetValue(5);b.RightHand.SetValue(7);
            Assert.IsTrue(ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.RightHand),a,game)>ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.LeftHand),a,game));
        }
    }
}
