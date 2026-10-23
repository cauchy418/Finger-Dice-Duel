using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class WizardAiPolicyTests
    {
        [Test] public void Every_current_role_has_an_explicit_value_profile()
        {
            foreach(var def in CharacterDefinition.BuiltIn) Assert.IsTrue(HandValueProfiles.HasExplicitProfile(def),def.Id);
            Assert.IsTrue(HandValueProfiles.For(CharacterDefinition.SchoolOfNecromancy).WeightOf(2)>
                HandValueProfiles.For(CharacterDefinition.SchoolOfNecromancy).WeightOf(9));
        }
        [Test] public void Resistance_targets_injured_ally_without_granting_forbidden_force()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfAbjuration);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.SchoolOfConjuration);
            var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Champion);
            var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Champion);d.TakeDamage(12);

            var ai=new GreedyChoiceProvider();var g=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{ai,ai,ai,ai});
            c.ApplyStatus(StatusEffect.Frozen,2);
            Assert.AreEqual(d,ai.ResistanceTarget(a,g));
            Assert.IsTrue(ResistanceNames.CanGrant(ai.ResistanceType(a,d,g)));
            Assert.AreEqual(6,ResistanceNames.Choices.Length);
        }
        [Test] public void Headless_abjuration_uses_policy_and_does_not_store_a_lethal_attack()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfAbjuration);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.SchoolOfConjuration);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,ai);
            g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.IsFalse(a.HasResistance(AttackDamageType.Force));
            Assert.IsTrue(a.HasResistance(AttackDamageType.Bludgeoning));
            g.ResetMatch();a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);b.TakeDamage(17);
            g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.IsTrue(!b.IsAlive);
            Assert.AreEqual(0,((IAbjurationSkill)a.Character).BarrierPoints);
        }
        [Test] public void Necrotic_attack_never_selects_an_ally_and_exchange_uses_skill_value()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfNecromancy);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Champion);
            var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Champion);d.TakeDamage(19);
            var ai=new GreedyChoiceProvider();var g=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{ai,ai,ai,ai});
            Assert.IsFalse(ReferenceEquals(d,ai.NecroticTarget(a,g)));
            Assert.IsTrue(ai.ExchangeValue(a,2)>ai.ExchangeValue(a,9),"不会因为9更大就丢掉抵挡用的2");
        }
        [Test] public void Lethal_estimate_accounts_for_resistance_barrier_and_current_stat_offsets_without_consumption()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Champion);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.SchoolOfAbjuration);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,ai);ai.SetGameForChoices(g);
            b.AddResistance(AttackDamageType.Slashing);((IAbjurationSkill)b.Character).StoreBarrier(2);
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(3,ai.TotalLethalDamage(move,a,b,8),"(8+1-(-1))/2-2=3");
            Assert.IsTrue(b.HasResistance(AttackDamageType.Slashing));Assert.AreEqual(2,((IAbjurationSkill)b.Character).BarrierPoints);
        }

        [Test] public void Ai_forced_skip_resolves_archfey_health_target()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.TheArchfey);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=new GameManager(a,b,ca,cb);var board=new BoardController(g,ca,cb,new GreedyChoiceProvider(),new GreedyChoiceProvider());
            a.TakeDamage(10);a.LeftHand.Lock();a.RightHand.Lock();board.BeginTurn();
            Assert.IsTrue(board.MakeAutomaticMove());
            Assert.IsTrue(board.ResolvePendingAiChoice());
            Assert.AreEqual(10,b.Health);Assert.AreEqual(1,g.TurnCount);
        }

        [Test] public void Magician_uses_own_locked_other_hand_as_addend()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Magician);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=new GameManager(a,b,ca,cb);var board=new BoardController(g,ca,cb);
            a.LeftHand.SetValue(1);a.RightHand.SetValue(2);a.RightHand.Lock();board.BeginTurn();
            Assert.IsTrue(board.ClickHand(a.LeftHand));Assert.IsTrue(board.CanClick(a.RightHand));
            Assert.IsTrue(board.ClickHand(a.RightHand));Assert.AreEqual(3,a.LeftHand.Value);
            Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void All_roles_three_difficulties_and_three_board_sizes_progress_without_human_prompts()
        {
            foreach(var difficulty in new[]{AiDifficulty.Easy,AiDifficulty.Normal,AiDifficulty.Hard})
            foreach(int count in new[]{2,4,8})
            foreach(var def in CharacterDefinition.BuiltIn)
            {
                var players=new PlayerState[count];var providers=new IChoiceProvider[count];
                var sinks=new SimpleChoiceProvider[count];var ais=new GreedyChoiceProvider[count];var teams=new int[count];
                for(int i=0;i<count;i++)
                {
                    players[i]=CharacterFactory.CreatePlayer("P"+i,i==0?def:CharacterDefinition.BuiltIn[(i*3+2)%CharacterDefinition.BuiltIn.Length]);
                    ais[i]=new GreedyChoiceProvider();ais[i].ApplyDifficulty(difficulty);sinks[i]=new SimpleChoiceProvider();providers[i]=sinks[i];teams[i]=i%3;
                }
                var g=count==2?new GameManager(players[0],players[1],providers[0],providers[1])
                    :count==4?GameManager.Create2v2(players,providers):GameManager.CreateEight(players,providers,teams);
                var board=new BoardController(g,sinks[0],sinks[1],ais[0],ais[1]);
                for(int i=0;i<count;i++) board.BindPlayer(players[i],ais[i],sinks[i]);
                board.BeginTurn();
                int guard=0;
                while(!g.IsGameOver && g.TurnCount<24 && guard++<180)
                {
                    board.AutoResolveAiCollegeOfValorChoice();
                    bool progressed=board.Phase==BoardPhase.ChoosingAttackerHand?board.MakeAutomaticMove():board.ResolvePendingAiChoice();
                    Assert.IsTrue(progressed || g.IsGameOver,def.Id+" "+difficulty+" "+count+" "+board.DebugPhaseInfo());
                    Assert.IsTrue(board.PendingWizardChoice==null,"AI不等待人类输入");
                }
                Assert.IsTrue(g.IsGameOver || g.TurnCount>=24,def.Id+" "+difficulty+" "+count+" turn="+g.TurnCount+" round="+g.RoundCount+" "+board.DebugPhaseInfo());
            }
        }
    }
}
