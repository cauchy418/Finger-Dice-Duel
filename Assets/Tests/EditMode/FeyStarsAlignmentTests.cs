using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class FeyStarsAlignmentTests
    {
        private static BoardController Fey(bool reverse,bool hurt,out GameManager game)
        {
            var a=DualGeneralFactory.Create("妖精刺客",reverse?CharacterDefinition.Assassin:CharacterDefinition.TheArchfey,reverse?CharacterDefinition.TheArchfey:CharacterDefinition.Assassin);
            var b=CharacterFactory.CreatePlayer("对手",CharacterDefinition.Blank);var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            game=new GameManager(a,b,ca,cb);a.LeftHand.SetValue(7);a.RightHand.SetValue(7);
            CharacterBehaviors.FirstImplementer<AssassinLogic>(a).EnterStealthForTest();if(hurt)a.TakeDamage(5);
            var board=new BoardController(game,ca,cb);board.BeginTurn();return board;
        }
        [Test] public void Latest_axes_reject_old_partners_and_allow_new_partners()
        {
            Assert.AreEqual(Alignment.ChaoticEvil,CharacterCodex.For(CharacterDefinition.TheArchfey).Alignment);
            Assert.AreEqual(Alignment.LawfulNeutral,CharacterCodex.For(CharacterDefinition.CircleOfTheStars).Alignment);
            Assert.True(DualGeneralFactory.SameAxis(CharacterDefinition.TheArchfey,CharacterDefinition.Assassin));
            Assert.True(DualGeneralFactory.SameAxis(CharacterDefinition.CircleOfTheStars,CharacterDefinition.LifeDomain));
            Assert.Throws<ArgumentException>(()=>DualGeneralFactory.Create("X",CharacterDefinition.CircleOfTheStars,CharacterDefinition.CircleOfTheMoon));
            Assert.Throws<ArgumentException>(()=>DualGeneralFactory.Create("X",CharacterDefinition.TheArchfey,CharacterDefinition.SchoolOfConjuration));
        }
        [Test] public void Fey_assassin_can_wait_without_rewriting_health_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true}){
                var board=Fey(reverse,true,out var g);Assert.True(board.SkipTurn());Assert.True(board.PendingWizardChoice.AllowDecline);
                Assert.True(board.PendingWizardChoice.TargetsPlayer);Assert.AreEqual("仅潜行等待",board.PendingWizardChoice.DeclineLabel);
                Assert.True(board.ChooseWizardHand(null));Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(1,g.TurnCount);
                Assert.False(CharacterBehaviors.FirstImplementer<AssassinLogic>(g.PlayerA).CanSkip(g.PlayerA,g));
            }
        }
        [Test] public void Fey_assassin_can_rewrite_health_and_advance_once_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true}){
                var board=Fey(reverse,true,out var g);Assert.True(board.SkipTurn());Assert.True(board.ChooseWizardHand(g.PlayerB.LeftHand));
                Assert.AreEqual(15,g.PlayerB.Health);Assert.AreEqual(1,g.TurnCount);
                Assert.False(CharacterBehaviors.FirstImplementer<AssassinLogic>(g.PlayerA).CanSkip(g.PlayerA,g));
            }
        }
        [Test] public void Full_health_fey_assassin_only_waits_and_does_not_gain_an_illegal_rewrite()
        {
            foreach(bool reverse in new[]{false,true}){
                var board=Fey(reverse,false,out var g);g.PlayerB.SetHealthExact(10);
                Assert.True(board.SkipTurn());Assert.True(board.PendingWizardChoice==null);Assert.AreEqual(10,g.PlayerB.Health);Assert.AreEqual(1,g.TurnCount);
            }
        }
    }
}
