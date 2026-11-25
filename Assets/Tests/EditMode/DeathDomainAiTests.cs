using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class DeathDomainAiTests
    {
        [Test] public void Feigned_actor_scores_only_hand_setup_without_heal_or_attack_bonuses()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider{NextTurnPotentialWeight=0};
            var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);b.RightHand.SetValue(8);
            a.ApplyStatus(StatusEffect.FeignedDeath,2);
            var attack=new LegalMove(a,a.LeftHand,b.LeftHand);var heal=new LegalMove(a,a.LeftHand,b.RightHand);
            int before=ai.ScoreMoveForTest(heal,a,game);a.SetHealthExact(1);
            Assert.AreEqual(before,ai.ScoreMoveForTest(heal,a,game));
            Assert.AreEqual(ai.ScoreMoveForTest(attack,a,game),ai.ScoreMoveForTest(heal,a,game));
            Assert.AreEqual(0,ai.TotalLethalDamage(attack,a,b,4,game));
            Assert.True(AiStatePolicy.Rules.ContainsKey(AiStatePolicy.KeyOf(StatusEffect.FeignedDeath)));
        }
        [Test] public void Feigned_enemy_never_receives_a_lethal_score_and_damage_respects_one_health_floor()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);b.LeftHand.SetValue(7);b.SetHealthExact(1);b.ApplyStatus(StatusEffect.FeignedDeath,2);
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(0,ai.TotalLethalDamage(move,a,b,8,game));
            Assert.True(ai.ScoreMoveForTest(move,a,game)<ai.LethalBonus);
            Assert.AreEqual(1,b.Health);Assert.True(b.IsAlive);
        }
        [Test] public void Death_touch_projects_necrotic_instead_of_physical_affinity()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var corpse=CharacterFactory.CreatePlayer("corpse",CharacterDefinition.Blank);
            var ally=CharacterFactory.CreatePlayer("ally",CharacterDefinition.Blank);
            var game=GameManager.Create2v2(new[]{a,b,ally,corpse},new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});
            corpse.SetHealthExact(0);a.LeftHand.SetValue(1);b.LeftHand.SetValue(7);corpse.LeftHand.SetValue(7);
            b.AddPermanentImmunity(AttackDamageType.Necrotic);
            var ai=new GreedyChoiceProvider();ai.Game=game;
            Assert.True(ai.TotalLethalDamage(new LegalMove(a,a.LeftHand,b.LeftHand),a,b,8,game)>0);
            Assert.AreEqual(0,ai.TotalLethalDamage(new LegalMove(a,a.LeftHand,corpse.LeftHand),a,b,8,game));
        }
        [Test] public void Death_blessing_can_control_a_healthy_enemy_to_save_a_wounded_healer()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(7);a.RightHand.SetValue(7);a.SetHealthExact(2);
            b.LeftHand.SetValue(1);b.RightHand.SetValue(1);
            Assert.True(ai.DeathBlessing(a,b,game));
            var request=new ChoiceRequest(HandEffect.LifeDomainHealTarget,"heal",new[]{a.LeftHand,b.LeftHand},allowDecline:false,decider:a,resultValue:9);
            Assert.AreEqual(b.LeftHand,ai.ChooseHandFor(request,a));
            a.SetHealthExact(a.MaximumHealth);Assert.False(ai.DeathBlessing(a,b,game));
            a.SetHealthExact(2);b.ApplyStatus(StatusEffect.HolyLight);Assert.False(ai.DeathBlessing(a,b,game));
        }
        [Test] public void Death_blessing_does_not_heal_and_protect_a_wounded_last_enemy()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(7);a.RightHand.SetValue(7);a.SetHealthExact(2);
            b.LeftHand.SetValue(1);b.RightHand.SetValue(1);b.SetHealthExact(1);
            Assert.False(ai.DeathBlessing(a,b,game));
        }
        [Test] public void Death_actor_recovers_instead_of_attacking_a_feigned_enemy_at_one_health()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);a.SetHealthExact(10);b.LeftHand.SetValue(7);b.RightHand.SetValue(8);
            b.SetHealthExact(1);b.ApplyStatus(StatusEffect.FeignedDeath,2);
            var attack=new LegalMove(a,a.LeftHand,b.LeftHand);var heal=new LegalMove(a,a.LeftHand,b.RightHand);
            Assert.True(ai.ScoreMoveForTest(heal,a,game)>ai.ScoreMoveForTest(attack,a,game));
        }
        [Test] public void Death_blessing_does_not_sacrifice_actions_when_healing_already_removes_lethal_danger()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(7);a.RightHand.SetValue(7);a.SetHealthExact(2);
            b.LeftHand.SetValue(1);b.RightHand.SetValue(1);
            Assert.True(ai.DeathBlessing(a,a,game));a.Heal(9);
            Assert.False(ai.DeathBlessing(a,a,game));
        }
        [Test] public void Death_blessing_can_disable_a_dangerous_enemy_while_another_enemy_remains()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ally=CharacterFactory.CreatePlayer("ally",CharacterDefinition.Blank);
            var foe=CharacterFactory.CreatePlayer("foe",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=GameManager.Create2v2(new[]{a,b,foe,ally},
                new IChoiceProvider[]{ai,new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});ai.Game=game;
            a.LeftHand.SetValue(7);a.RightHand.SetValue(7);b.LeftHand.SetValue(1);b.RightHand.SetValue(1);
            Assert.True(ai.DeathBlessing(a,b,game));
            var request=new ChoiceRequest(HandEffect.LifeDomainHealTarget,"heal",new[]{a.LeftHand,b.LeftHand,ally.LeftHand,foe.LeftHand},allowDecline:false,decider:a,resultValue:9);
            Assert.AreEqual(b.LeftHand,ai.ChooseHandFor(request,a));
            b.ApplyStatus(StatusEffect.FeignedDeath,2);Assert.False(ai.DeathBlessing(a,b,game));
        }
        [Test] public void Final_feigned_turn_values_setup_for_the_first_active_turn()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(1);a.RightHand.SetValue(2);b.LeftHand.SetValue(3);b.RightHand.SetValue(4);
            var setup=new LegalMove(a,a.LeftHand,b.LeftHand);
            a.ApplyStatus(StatusEffect.FeignedDeath,2);int early=ai.ScoreMoveForTest(setup,a,game);
            a.RemoveStatus(StatusEffect.FeignedDeath);a.ApplyStatus(StatusEffect.FeignedDeath,1);
            Assert.True(ai.ScoreMoveForTest(setup,a,game)>early);
            Assert.AreEqual(0,ai.TotalLethalDamage(setup,a,b,4,game));
        }
        [Test] public void Death_recovery_control_competes_with_an_attack_that_cannot_finish_the_enemy()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            a.LeftHand.SetValue(8);a.RightHand.SetValue(7);a.SetHealthExact(2);
            b.LeftHand.SetValue(1);b.RightHand.SetValue(1);
            var recovery=new LegalMove(a,a.LeftHand,b.LeftHand);var attack=new LegalMove(a,a.RightHand,b.LeftHand);
            Assert.True(ai.ScoreMoveForTest(recovery,a,game)>ai.ScoreMoveForTest(attack,a,game));
        }
        [Test] public void Death_blessing_preserves_a_teammate_only_when_its_actions_cannot_safely_continue()
        {
            var healer=CharacterFactory.CreatePlayer("healer",CharacterDefinition.DeathDomain);
            var enemy=CharacterFactory.CreatePlayer("enemy",CharacterDefinition.TheGreatOldOne);
            var ally=CharacterFactory.CreatePlayer("ally",CharacterDefinition.Blank);
            var foe=CharacterFactory.CreatePlayer("foe",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var game=GameManager.Create2v2(new[]{healer,enemy,foe,ally},
                new IChoiceProvider[]{ai,new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});
            enemy.LeftHand.SetValue(1);enemy.RightHand.SetValue(1);ally.LeftHand.SetValue(7);ally.RightHand.SetValue(7);
            foe.ApplyStatus(StatusEffect.Frozen,2);
            Assert.False(ai.DeathBlessing(healer,ally,game));ally.SetHealthExact(2);
            Assert.True(ai.DeathBlessing(healer,ally,game));
            ally.ApplyStatus(StatusEffect.FeignedDeath,2);Assert.False(ai.DeathBlessing(healer,ally,game));
        }
        [Test] public void Death_blessing_declines_healthy_or_protected_allies_but_preserves_an_endangered_patient()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.DeathDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
            var ai=new GreedyChoiceProvider();var game=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=game;
            b.LeftHand.SetValue(1);b.RightHand.SetValue(1);a.LeftHand.SetValue(7);a.RightHand.SetValue(7);
            Assert.False(ai.DeathBlessing(a,a,game));a.SetHealthExact(2);
            Assert.True(ai.DeathBlessing(a,a,game));
            a.ApplyStatus(StatusEffect.HolyLight);Assert.False(ai.DeathBlessing(a,a,game));
        }
    }
}
