using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class SchoolOfEvocationDeclineKeepsTargetStepTests
    {
        [Test] public void Repeated_choices_reduce_each_segment_once_and_different_choice_resets()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfEvocation);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Breaker);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var logic=(SchoolOfEvocationLogic)a.Character;
            logic.SetPendingDamageType(DamageType.Force);logic.ConsumeDamageType();
            logic.SetPendingDamageType(DamageType.Force);logic.SetPendingDamageType(DamageType.Force);
            Assert.AreEqual(-2,logic.PendingDamagePenalty);int requests=0;
            g.DeferWizardChoice=q=>{requests++;return true;};
            var attack=new AttackContext(a,b,a.LeftHand,8,g){HitCount=2};attack.Resolve(new SimpleChoiceProvider(),g);
            Assert.AreEqual(4,attack.AppliedDamage);Assert.AreEqual(1,requests);
            Assert.IsFalse(logic.HasPendingType);Assert.AreEqual(DamageType.Force,logic.LastChosenDamageType);
            logic.SetPendingDamageType(DamageType.Fire);Assert.AreEqual(0,logic.PendingDamagePenalty);
            Assert.IsTrue(a.HasResistance(AttackDamageType.Fire));logic.ConsumeDamageType();Assert.IsFalse(a.HasResistance(AttackDamageType.Fire));
        }
        [Test] public void Cancelled_attack_consumes_type_without_preparing_another()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfEvocation);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.TheArchfey);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var logic=(SchoolOfEvocationLogic)a.Character;logic.SetPendingDamageType(DamageType.Fire);
            b.LeftHand.SetValue(4);int requests=0;g.DeferWizardChoice=q=>{requests++;return true;};
            var attack=new AttackContext(a,b,a.LeftHand,4,g);attack.Resolve(new SimpleChoiceProvider(),g);
            Assert.IsTrue(attack.WasCancelled);Assert.AreEqual(0,requests);Assert.IsFalse(logic.HasPendingType);
            Assert.AreEqual(DamageType.Fire,logic.LastChosenDamageType);
        }
        [Test] public void Dodged_attack_consumes_old_type_then_offers_one_new_choice()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfEvocation);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Swashbuckler);
            var defender=new SimpleChoiceProvider();var g=new GameManager(a,b,new SimpleChoiceProvider(),defender);
            b.LeftHand.SetValue(1);defender.SetChoice(HandEffect.SwashbucklerDodge,b.LeftHand);
            var logic=(SchoolOfEvocationLogic)a.Character;logic.SetPendingDamageType(DamageType.Fire);
            int requests=0;g.DeferWizardChoice=q=>{requests++;return true;};
            var attack=new AttackContext(a,b,a.LeftHand,4,g);attack.Resolve(defender,g);
            Assert.IsTrue(attack.WasDodged);Assert.IsFalse(logic.HasPendingType);Assert.AreEqual(1,requests);
        }
        [Test] public void Blocked_and_zero_damage_attacks_still_prepare_once()
        {
            foreach(bool blocked in new[]{false,true})
            {
                var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfEvocation);
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());int requests=0;
                g.DeferWizardChoice=q=>{requests++;return true;};
                if(blocked){b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Formed);}
                else b.AddPermanentImmunity(AttackDamageType.Bludgeoning);
                var attack=new AttackContext(a,b,a.LeftHand,4,g);attack.Resolve(new SimpleChoiceProvider(),g);
                Assert.AreEqual(0,attack.AppliedDamage);Assert.AreEqual(1,requests);
            }
        }
        [Test] public void Preparation_is_not_collected_before_an_attack()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfEvocation);
            var requests=new System.Collections.Generic.List<ChoiceRequest>();
            ((SchoolOfEvocationLogic)a.Character).CollectPreTurnChoices(new PreTurnChoiceContext(a,a,a.LeftHand,a.RightHand){ResultValue=4},requests);
            Assert.AreEqual(0,requests.Count);
        }
    }
}
