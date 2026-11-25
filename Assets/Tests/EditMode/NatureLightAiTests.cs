using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class NatureLightAiTests
    {
        [Test] public void Light_prepares_a_missing_blessing_instead_of_firing_into_radiant_immunity()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LightDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);a.RightHand.SetValue(2);b.LeftHand.SetValue(7);b.RightHand.SetValue(8);
            b.AddPermanentImmunity(AttackDamageType.Radiant);
            var heal=new LegalMove(a,a.LeftHand,b.RightHand);var blocked=new LegalMove(a,a.LeftHand,b.LeftHand);
            Assert.True(ai.ScoreMoveForTest(heal,a,game)>ai.ScoreMoveForTest(blocked,a,game));
        }
        [Test] public void Dawn_cleanses_control_when_the_extra_damage_alone_finishes_an_unprotected_enemy()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LightDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());
            b.ApplyStatus(StatusEffect.Frozen,2);b.SetHealthExact(2);
            Assert.True(ai.CleanseLightAttack(a,b,game));
            b.ApplyStatus(StatusEffect.FeignedDeath,2);Assert.False(ai.CleanseLightAttack(a,b,game));
            b.RemoveStatus(StatusEffect.FeignedDeath);b.RightHand.SetValue(1);b.RightHand.PerformOperation(4);
            Assert.False(ai.CleanseLightAttack(a,b,game));
        }
        [Test] public void All_current_roles_and_new_blessings_have_explicit_policy_registration()
        {
            foreach(var role in CharacterDefinition.BuiltIn)Assert.True(HandValueProfiles.HasExplicitProfile(role),role.Id);
            Assert.True(AiStatePolicy.Rules.ContainsKey(AiStatePolicy.KeyOf(StatusEffect.NatureBlessing)));
            Assert.True(AiStatePolicy.Rules.ContainsKey(AiStatePolicy.KeyOf(StatusEffect.LightBlessing)));
            Assert.True(AiStatePolicy.Rules.ContainsKey("effect:stored_nature_element"));
        }
        [Test] public void Full_health_healing_values_new_blessings_only_when_sacred_fire_allows_them()
        {
            foreach(var role in new[]{CharacterDefinition.NatureDomain,CharacterDefinition.LightDomain})
            {
                var a=CharacterFactory.CreatePlayer("A",role);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
                a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);var move=new LegalMove(a,a.LeftHand,b.LeftHand);
                int allowed=ai.ScoreMoveForTest(move,a,game);a.ApplyStatus(StatusEffect.HolyLight,2);
                Assert.True(allowed>ai.ScoreMoveForTest(move,a,game),role.Id);
                Assert.AreEqual(a.MaximumHealth,a.Health);Assert.AreEqual(0,game.TurnCount);
            }
        }
        [Test] public void Dawn_cleanse_compares_damage_with_losing_enemy_control()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LightDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());
            b.ApplyStatus(StatusEffect.Frozen,2);Assert.False(ai.CleanseLightAttack(a,b,game));
            b.RemoveStatus(StatusEffect.Frozen);b.ApplyStatus(StatusEffect.Blind,1);
            Assert.True(ai.CleanseLightAttack(a,b,game));
            Assert.True(b.HasStatus(StatusEffect.Blind));Assert.AreEqual(20,b.Health);
        }
        [Test] public void Light_blessing_converts_damage_but_only_innate_radiant_pierces_immunity()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LightDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());
            b.AddPermanentImmunity(AttackDamageType.Radiant);var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(0,ai.TotalLethalDamage(move,a,b,8,game));
            a.ApplyStatus(StatusEffect.LightBlessing,2);Assert.True(ai.TotalLethalDamage(move,a,b,8,game)>0);
            var physical=CharacterFactory.CreatePlayer("physical",CharacterDefinition.Champion);
            physical.ApplyStatus(StatusEffect.LightBlessing,2);
            Assert.AreEqual(0,ai.TotalLethalDamage(new LegalMove(physical,physical.LeftHand,b.LeftHand),physical,b,8,game));
        }
        [Test] public void Nature_stored_element_changes_attack_opportunity_without_spending_it()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.NatureDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);b.LeftHand.SetValue(7);var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            int before=ai.ScoreMoveForTest(move,a,game);var nature=CharacterBehaviors.FirstImplementer<NatureDomainLogic>(a);
            nature.StoreElement(AttackDamageType.Fire);Assert.True(ai.ScoreMoveForTest(move,a,game)>before);
            Assert.AreEqual(StatusEffect.Burn,nature.StoredElementStatus);Assert.AreEqual(1,a.LeftHand.Value);Assert.AreEqual(0,game.TurnCount);
        }
        [Test] public void Nature_blessing_retaliation_reduces_attack_value_even_with_target_shield()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Champion);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);b.LeftHand.SetValue(7);b.RightHand.SetValue(5);
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);int before=ai.ScoreMoveForTest(move,a,game);
            b.ApplyStatus(StatusEffect.NatureBlessing,2);Assert.True(ai.ScoreMoveForTest(move,a,game)<before);
        }
    }
}
