using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class SchoolOfEvocationUiFlowTests
    {
        private static BoardController Board(out GameManager game,out PlayerState mage,out PlayerState foe)
        {
            mage=CharacterFactory.CreatePlayer("Mage",CharacterDefinition.SchoolOfEvocation);
            foe=CharacterFactory.CreatePlayer("Foe",CharacterDefinition.Breaker);
            game=new GameManager(mage,foe,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var board=new BoardController(game,new SimpleChoiceProvider(),new SimpleChoiceProvider());board.BeginTurn();return board;
        }
        private static void Attack(BoardController board,PlayerState mage,PlayerState foe)
        {
            mage.LeftHand.SetValue(3);mage.RightHand.SetValue(1);foe.LeftHand.SetValue(1);foe.RightHand.SetValue(1);
            board.ClickHand(mage.LeftHand);board.ClickHand(foe.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);
            Assert.AreEqual(HandEffect.None,board.PendingEffect);
            board.ClickHand(foe.LeftHand);
        }
        [Test] public void Current_attack_lands_before_human_next_type_choice()
        {
            var b=Board(out var g,out var mage,out var foe);int hp=foe.Health;Attack(b,mage,foe);
            Assert.IsTrue(foe.Health<hp);Assert.AreEqual(BoardPhase.ChoosingWizardChoice,b.Phase);
            Assert.AreEqual(mage,b.Chooser);Assert.AreEqual(11,b.PendingSkillOptions.Count);
            Assert.IsFalse(foe.HasStatus(StatusEffect.Burn));
            Assert.IsTrue(b.ChooseDamageType(DamageType.Fire));
            Assert.AreEqual(DamageType.Fire,((SchoolOfEvocationLogic)mage.Character).PendingDamageType);
            var next=new AttackContext(mage,foe,mage.LeftHand,4,g);next.Resolve(new SimpleChoiceProvider(),g);
            Assert.AreEqual(AttackDamageType.Fire,next.AttackType);Assert.IsTrue(foe.HasStatus(StatusEffect.Burn));
        }
        [Test] public void Multiplayer_dual_mages_keep_the_human_post_attack_choice()
        {
            foreach(int count in new[]{4,8}) foreach(bool reverse in new[]{false,true})
            {
                var players=new PlayerState[count];var providers=new IChoiceProvider[count];var teams=new int[count];
                players[0]=DualGeneralFactory.Create("Mage",reverse?CharacterDefinition.Champion:CharacterDefinition.SchoolOfEvocation,reverse?CharacterDefinition.SchoolOfEvocation:CharacterDefinition.Champion);
                for(int i=0;i<count;i++){if(i>0)players[i]=CharacterFactory.CreatePlayer("P"+i,CharacterDefinition.Blank);providers[i]=new SimpleChoiceProvider();teams[i]=i%2;}
                var g=count==4?GameManager.Create2v2(players,providers):GameManager.CreateEight(players,providers,teams);
                var b=new BoardController(g,(IPlayerChoiceSink)providers[0],(IPlayerChoiceSink)providers[1]);
                for(int i=0;i<count;i++)b.BindPlayer(players[i],null,(IPlayerChoiceSink)providers[i]);
                b.BeginTurn();Attack(b,players[0],players[1]);
                Assert.AreEqual(BoardPhase.ChoosingWizardChoice,b.Phase);Assert.AreEqual(players[0],b.Chooser);
                Assert.IsTrue(b.ChooseSkillOption((int)DamageType.Fire));
                Assert.AreEqual(DamageType.Fire,CharacterBehaviors.FirstImplementer<SchoolOfEvocationLogic>(players[0]).PendingDamageType);
            }
        }
        [Test] public void Declining_post_attack_does_not_erase_last_choice()
        {
            var b=Board(out var g,out var mage,out var foe);var logic=(SchoolOfEvocationLogic)mage.Character;
            logic.SetPendingDamageType(DamageType.Cold);Attack(b,mage,foe);
            Assert.IsTrue(b.ChooseSkillOption(0));Assert.IsFalse(logic.HasPendingType);
            Assert.AreEqual(DamageType.Cold,logic.LastChosenDamageType);
            logic.SetPendingDamageType(DamageType.Cold);Assert.AreEqual(-2,logic.PendingDamagePenalty);
        }
    }
}
