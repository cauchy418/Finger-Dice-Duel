using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class BreakerAiCounterplayTests
    {
        private static void RunShieldEight(bool enabled)
        {
            var ai=new GreedyChoiceProvider();if(enabled)ai.UseChainBreak=true;
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Breaker,CharacterDefinition.Blank,ca,cb);
            g.PlayerA.LeftHand.SetValue(5);g.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);g.PlayerA.RightHand.SetValue(1);
            g.PlayerB.LeftHand.SetValue(3);g.PlayerB.RightHand.SetValue(5);g.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            var board=new BoardController(g,ca,cb,ai);board.BeginTurn();
            Assert.True(board.ClickHand(g.PlayerA.LeftHand));Assert.True(board.ClickHand(g.PlayerB.LeftHand));if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ConfirmTargetPlayer();
            Assert.AreEqual(1,g.TurnCount,string.Join(";",board.AiTrace));
            Assert.AreEqual(1,((BreakerLogic)g.PlayerA.Character).ChainBreakCount,string.Join(";",board.AiTrace));
            Assert.False(board.LastTurn.WasBlocked);Assert.AreEqual(9,board.LastTurn.DamageDealt);
        }
        [Test] public void Enabled_chain_breaks_before_five_plus_three_attack(){RunShieldEight(true);}
        [Test] public void Default_ai_breaks_before_five_plus_three_attack(){RunShieldEight(false);}
        private static int CounterScore(GameManager g,PlayerState a,int result)
        {
            var method=typeof(GreedyChoiceProvider).GetMethod("SpecialistCounterplayScore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            return (int)method.Invoke(null,new object[]{new LegalMove(a,a.LeftHand,g.PlayerB.LeftHand),a,result,g});
        }
        [Test] public void Beast_counterplay_denies_new_sum_seven_soul_and_recovery()
        {
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.PathOfTheBeast,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.RightHand.SetValue(3);g.PlayerB.SetHealthExact(10);
            Assert.True(CounterScore(g,g.PlayerA,3)<CounterScore(g,g.PlayerA,1));
            Assert.AreEqual(10,g.PlayerB.Health);Assert.AreEqual(0,((FingerDiceDuel.Characters.SpecialLogics.PathOfTheBeastLogic)g.PlayerB.Character).BeastSouls);
        }
        [Test] public void Audited_attack_roles_deny_new_attack_opportunities()
        {
            foreach(var role in new[]{CharacterDefinition.Swordsman,CharacterDefinition.TheGreatOldOne,CharacterDefinition.PathOfTheBerserker,CharacterDefinition.DraconicBloodline,CharacterDefinition.SchoolOfIllusion,CharacterDefinition.OathOfConquest})
            {
                var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,role,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.RightHand.SetValue(role==CharacterDefinition.Swordsman?4:role==CharacterDefinition.SchoolOfIllusion?2:1);g.PlayerB.SetHealthExact(14);
                Assert.True(CounterScore(g,g.PlayerA,3)<CounterScore(g,g.PlayerA,1),role.Id);
                Assert.AreEqual(1,g.PlayerA.LeftHand.Value);Assert.AreEqual(0,g.TurnCount);
            }
        }
        [Test] public void Devotion_counterplay_denies_new_shield_and_shock()
        {
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.OathOfDevotion,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.RightHand.SetValue(1);
            Assert.True(CounterScore(g,g.PlayerA,3)<CounterScore(g,g.PlayerA,1));
        }

        [Test] public void Forced_gambling_suppresses_operand_denial_for_non_gamblers()
        {
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.PathOfTheBeast,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.RightHand.SetValue(3);
            g.DeferWizardChoice=choice=>{choice.Choose(g.PlayerB.LeftHand);return true;};
            g.OfferCrowdGambling(g.PlayerA,g.PlayerA.LeftHand);
            Assert.True(g.HasCrowdGambling(g.PlayerB));
            Assert.AreEqual(0,CounterScore(g,g.PlayerA,3));
            var ai=new GreedyChoiceProvider();
            Assert.AreEqual(0,ai.ProjectedCounterplayScoreForTest(new LegalMove(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand),g.PlayerA,g));
            Assert.True(AiStatePolicy.Rules["effect:crowd_gambling"].Applies(g.PlayerB,g));
            Assert.False(AiStatePolicy.Rules["effect:crowd_gambling"].Applies(g.PlayerA,g));
        }
        [Test] public void Unchanged_operand_that_already_grants_beast_soul_is_not_new_risk()
        {
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.PathOfTheBeast,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            g.PlayerA.LeftHand.SetValue(1);g.PlayerA.RightHand.SetValue(3);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.RightHand.SetValue(3);
            Assert.AreEqual(0,CounterScore(g,g.PlayerA,3));
        }
    }
}
