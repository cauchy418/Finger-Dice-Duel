using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class DruidReworkTests
    {
        private static GameManager Match(CharacterDefinition role=null)
            =>CharacterFactory.CreateMatch(role??CharacterDefinition.CircleOfTheMoon,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());
        private static void Enter(GameManager g,int form,int star=0)
        {g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);Assert.IsTrue(g.PlayerA.EnterWild(form,star,g));g.SkipTurnAlreadyValidated();g.ForceActivePlayerForTest(g.PlayerA);}
        [Test] public void Ai_distinguishes_breaking_shape_from_defeating_original()
        {
            var game=Match();var target=game.PlayerA;var attacker=game.PlayerB;
            Enter(game,2);
            target.SetHealthExact(2);
            target.LeftHand.SetValue(3);
            var move=new LegalMove(attacker,attacker.LeftHand,target.LeftHand);
            var ai=new GreedyChoiceProvider{Game=game};
            Assert.AreEqual(22,target.HealthAcrossForms);
            Assert.IsTrue(ai.ScoreMoveForTest(move,attacker,game)<ai.LethalBonus);
            target.EndWild();
            target.SetHealthExact(2);
            Assert.IsTrue(ai.ScoreMoveForTest(move,attacker,game)>=ai.LethalBonus);
        }
        [Test] public void All_forms_replace_stats_health_type_and_restore_original()
        {
            int[] hp={0,12,10,8,8,16,10},atk={0,-1,0,1,0,0,2},def={0,1,-1,-1,0,2,0};
            for(int form=1;form<=6;form++)
            {var g=Match();var p=g.PlayerA;p.SetHealthExact(13);Enter(g,form);Assert.AreEqual(hp[form],p.Health);Assert.AreEqual(hp[form],p.MaximumHealth);Assert.AreEqual(atk[form],p.Character.AttackBonus);Assert.AreEqual(def[form],p.Character.DefenseBonus);Assert.IsTrue(p.BattleEmblemKey.StartsWith("Forms/"));p.EndWild();Assert.AreEqual(13,p.Health);Assert.AreEqual(20,p.MaximumHealth);}
        }
        [Test] public void Charges_are_shared_additive_and_not_refunded_by_exit()
        {
            var g=Match();Assert.AreEqual(3,g.PlayerA.WildCharges);Enter(g,5);Assert.AreEqual(1,g.PlayerA.WildCharges);g.PlayerA.EndWild();Assert.IsFalse(g.PlayerA.EnterWild(5,0,g));Assert.AreEqual(1,g.PlayerA.WildCharges);g.ResetMatch();Assert.AreEqual(3,g.PlayerA.WildCharges);
            var p=new PlayerState("双德",new DualGeneralBehavior(new CircleOfTheMoonLogic(),new CircleOfTheStarsLogic()));g=new GameManager(p,CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());Assert.AreEqual(5,p.WildCharges);p.LeftHand.SetValue(4);p.RightHand.SetValue(6);Assert.IsTrue(p.EnterWild(6,3,g));Assert.AreEqual(3,p.WildCharges);Assert.AreEqual(3,p.Character.AttackBonus);Assert.AreEqual(1,p.Character.DefenseBonus);Assert.AreEqual(3,GameManager.BaseRangeOf(p));
        }
        [Test] public void Sheep_and_wild_nested_pools_transfer_overflow_once()
        {
            var g=Match();var p=g.PlayerA;p.SetHealthExact(18);Enter(g,1);p.TryPolymorph();Assert.AreEqual(8,p.Health);Assert.AreEqual(22,p.TakeDamageResolved(22));Assert.IsFalse(p.IsPolymorphed);Assert.IsFalse(p.IsWildShaped);Assert.AreEqual(16,p.Health);
        }
        [Test] public void Bear_four_is_shield_and_eagle_six_has_no_effect()
        {var g=Match();Enter(g,1);Assert.AreEqual(HandEffectType.Shield,CharacterBehaviors.DisplayedEffectOf(g.PlayerA,4));g.PlayerA.EndWild();Enter(g,3);Assert.AreEqual(HandEffectType.None,CharacterBehaviors.DisplayedEffectOf(g.PlayerA,6));}
        [Test] public void Wolf_heals_actual_health_damage_and_panther_bleeds()
        {var g=Match();Enter(g,2);g.PlayerA.SetHealthExact(4);new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,6,g).Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(7,g.PlayerA.Health);g=Match();Enter(g,6);new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g).Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.IsTrue(g.PlayerB.HasStatus(StatusEffect.Bleed));}
        [Test] public void Owlbear_adds_distance_before_defense()
        {var g=Match();g.ConfigureSeatObstacles(1,1);Enter(g,5);var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.AreEqual(14,g.PlayerB.Health);}
        [Test] public void Stars_stack_with_wild_and_archer_overrides_poison()
        {var g=Match(CharacterDefinition.CircleOfTheStars);Enter(g,4,1);Assert.AreEqual(2,g.PlayerA.Character.AttackBonus);Assert.AreEqual(-1,g.PlayerA.Character.DefenseBonus);Assert.AreEqual(AttackDamageType.Radiant,((IAttackTypeProvider)g.PlayerA.Character).AttackTypeOf(new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g)));g=Match(CharacterDefinition.CircleOfTheStars);Enter(g,1,2);Assert.AreEqual(16,g.PlayerA.MaximumHealth);}
        [Test] public void Transformation_consumes_turn_and_allows_unequal_hands_exit()
        {var g=Match();var b=new BoardController(g,(IPlayerChoiceSink)g.ChoiceProviderA,(IPlayerChoiceSink)g.ChoiceProviderB);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);b.BeginTurn();Assert.IsTrue(b.ChooseSkillOption(1));Assert.AreEqual(1,g.TurnCount);Assert.AreEqual(g.PlayerA,b.Chooser);Assert.IsTrue(b.ChooseSkillOption(1));Assert.IsFalse(g.PlayerA.IsWildShaped);Assert.AreEqual(2,g.PlayerA.WildCharges);}
        [Test] public void Original_dual_skills_are_suspended_and_return_after_exit()
        {var p=new PlayerState("双将",new DualGeneralBehavior(new CircleOfTheMoonLogic(),new SchoolOfNecromancyLogic()));var g=new GameManager(p,CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());p.LeftHand.SetValue(4);p.RightHand.SetValue(6);Assert.IsTrue(p.EnterWild(1,0,g));Assert.AreEqual(null,CharacterBehaviors.FirstImplementer<SchoolOfNecromancyLogic>(p));p.EndWild();Assert.IsTrue(CharacterBehaviors.FirstImplementer<SchoolOfNecromancyLogic>(p)!=null);}
        [Test] public void Land_seals_only_own_hand_when_no_three_and_can_decline()
        {var g=Match(CharacterDefinition.CircleOfTheLand);var p=g.PlayerA;var land=(CircleOfTheLandLogic)p.Character;Assert.IsFalse(land.RestrictHand(g.PlayerB.LeftHand,g));p.LeftHand.SetValue(3);Assert.IsFalse(land.RestrictHand(p.RightHand,g));p.LeftHand.SetValue(2);Assert.IsTrue(land.RestrictHand(p.RightHand,g));land.OnTurnStarted(p,g);Assert.IsFalse(p.RightHand.IsTargetRestricted);Assert.AreEqual(2,p.WildCharges);}
        [Test] public void Dual_monks_begin_with_four_and_still_cap_at_six()
        {var p=new PlayerState("双僧",new DualGeneralBehavior(new WayOfTheNinjaLogic(),new WayOfTheTaiChiLogic()));var g=new GameManager(p,CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());Assert.AreEqual(4,p.MonkKi);p.GainMonkKi(9);Assert.AreEqual(6,p.MonkKi);g.ResetMatch();Assert.AreEqual(4,p.MonkKi);}
        [Test] public void Archer_panther_applies_both_magic_status_and_skill_bleed()
        {var p=new PlayerState("双德",new DualGeneralBehavior(new CircleOfTheMoonLogic(),new CircleOfTheStarsLogic()));var g=new GameManager(p,CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),new SimpleChoiceProvider(),new SimpleChoiceProvider());p.LeftHand.SetValue(4);p.RightHand.SetValue(6);Assert.IsTrue(p.EnterWild(6,1,g));new AttackContext(p,g.PlayerB,p.LeftHand,4,g).Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.IsTrue(g.PlayerB.HasStatus(StatusEffect.Blind));Assert.IsTrue(g.PlayerB.HasStatus(StatusEffect.Bleed));}
        [Test] public void Shape_is_an_independent_pool_not_a_status_cleared_by_holy_light()
        {var g=Match();g.PlayerA.ApplyStatus(StatusEffect.HolyLight,2,g.PlayerB);Enter(g,1);Assert.IsTrue(g.PlayerA.IsWildShaped);Assert.IsTrue(g.PlayerA.HasStatus(StatusEffect.HolyLight));g.PlayerA.TickStatuses();Assert.AreEqual(1,g.PlayerA.StatusTurnsLeft(StatusEffect.HolyLight));}
    }
}
