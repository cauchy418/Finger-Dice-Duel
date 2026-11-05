using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class ShadowMagicTests
    {
        private static GameManager Match()=>CharacterFactory.CreateMatch(CharacterDefinition.ShadowMagic,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());
        [Test] public void Ai_counts_unspent_grave_strength_before_marking_a_finish()
        {
            var game=Match();var target=game.PlayerA;var attacker=game.PlayerB;
            target.SetHealthExact(1);target.LeftHand.SetValue(3);
            var move=new LegalMove(attacker,attacker.LeftHand,target.LeftHand);
            var ai=new GreedyChoiceProvider{Game=game};
            Assert.IsTrue(ai.ScoreMoveForTest(move,attacker,game)<ai.LethalBonus);
            target.TakeDamage(4);
            Assert.IsTrue(ai.ScoreMoveForTest(move,attacker,game)>=ai.LethalBonus);
        }
        [Test] public void Grave_strength_survives_once_even_with_decay_and_resets_each_match()
        {var g=Match();var p=g.PlayerA;p.ApplyStatus(StatusEffect.Decay);p.TakeDamage(100);Assert.AreEqual(1,p.Health);Assert.IsTrue(((ShadowMagicLogic)p.Character).GraveSpent);p.TakeDamage(1);Assert.AreEqual(0,p.Health);g.ResetMatch();p.TakeDamage(100);Assert.AreEqual(1,p.Health);}
        [Test] public void Immunity_is_only_darkness_blind_and_status_is_negative_and_expires()
        {var g=Match();g.PlayerA.ApplyStatus(StatusEffect.DarknessBlind);Assert.IsFalse(g.PlayerA.HasStatus(StatusEffect.DarknessBlind));g.PlayerA.ApplyStatus(StatusEffect.Blind);Assert.IsTrue(g.PlayerA.HasStatus(StatusEffect.Blind));g.PlayerB.ApplyStatus(StatusEffect.DarknessBlind);Assert.IsTrue(StatusNames.IsNegativeStatus(StatusEffect.DarknessBlind));Assert.AreEqual("darkness_blind",StatusNames.IconKeyOf(StatusEffect.DarknessBlind));g.PlayerB.TickStatuses();g.PlayerB.TickStatuses();Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.DarknessBlind));}
        [Test] public void Blinded_attack_halves_after_stats_only_against_non_blinded_target()
        {var g=Match();g.PlayerB.ApplyStatus(StatusEffect.DarknessBlind);new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,4,g).Resolve(new SimpleChoiceProvider(),g);Assert.AreEqual(17,g.PlayerA.Health);}
        [Test] public void Shadow_eyes_adds_two_and_changes_damage_to_necrotic()
        {var g=Match();g.PlayerB.ApplyStatus(StatusEffect.DarknessBlind);var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);attack.Resolve(new SimpleChoiceProvider(),g);Assert.AreEqual(14,g.PlayerB.Health);Assert.AreEqual(AttackDamageType.Necrotic,attack.AttackType);Assert.IsTrue(g.PlayerB.HasStatus(StatusEffect.Decay));}
        [Test] public void Darkness_has_a_real_human_end_turn_choice_and_can_be_declined()
        {foreach(int use in new[]{0,1}){var g=Match();g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);WizardChoice choice=null;g.DeferWizardChoice=c=>{choice=c;return true;};((ShadowMagicLogic)g.PlayerA.Character).OnTurnEnded(g.PlayerA,g);Assert.IsTrue(choice!=null);choice.ChooseOption(use);Assert.AreEqual(use==1,g.PlayerB.HasStatus(StatusEffect.DarknessBlind));Assert.IsFalse(g.PlayerA.HasStatus(StatusEffect.DarknessBlind));}}
        [Test] public void Catalog_and_profile_are_registered()
        {var g=Match();Assert.AreEqual(Alignment.NeutralEvil,CharacterCodex.For(CharacterDefinition.ShadowMagic).Alignment);Assert.AreEqual(0,g.PlayerA.Character.AttackBonus);Assert.AreEqual(-1,g.PlayerA.Character.DefenseBonus);Assert.AreEqual(int.MaxValue,GameManager.BaseRangeOf(g.PlayerA));Assert.IsTrue(HandValueProfiles.HasExplicitProfile(CharacterDefinition.ShadowMagic));}
        [Test] public void Human_board_exposes_darkness_after_the_normal_operation()
        {var g=Match();var b=new BoardController(g,(SimpleChoiceProvider)g.ChoiceProviderA,(SimpleChoiceProvider)g.ChoiceProviderB);g.PlayerA.LeftHand.SetValue(1);g.PlayerA.RightHand.SetValue(6);g.PlayerB.LeftHand.SetValue(3);b.BeginTurn();Assert.IsTrue(b.ClickHand(g.PlayerA.LeftHand));Assert.IsTrue(b.ClickHand(g.PlayerB.LeftHand));Assert.IsTrue(b.ClickHand(g.PlayerB.LeftHand));Assert.IsTrue(b.PendingWizardChoice!=null);Assert.IsTrue(b.ChooseSkillOption(1));Assert.IsTrue(g.PlayerB.HasStatus(StatusEffect.DarknessBlind));}
        [Test] public void Darkness_hits_near_allies_but_not_far_players_and_ai_weighs_friendly_fire()
        {
            var p=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.ShadowMagic),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
            var c=new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()};var g=GameManager.Create2v2(p,c);p[0].LeftHand.SetValue(4);p[0].RightHand.SetValue(6);((ShadowMagicLogic)p[0].Character).OnTurnEnded(p[0],g);
            Assert.IsTrue(p[1].HasStatus(StatusEffect.DarknessBlind));Assert.IsTrue(p[3].HasStatus(StatusEffect.DarknessBlind));Assert.IsFalse(p[2].HasStatus(StatusEffect.DarknessBlind));
            g.ConfigureSeatObstacles(2,0,0,0);var options=new System.Collections.Generic.List<SkillChoiceOption>{new SkillChoiceOption(1,"发动",""),new SkillChoiceOption(0,"不发动","")};Assert.AreEqual(0,new GreedyChoiceProvider().ChooseSkillOption(ShadowRules.Darkness,options,p[0],null,g));
        }
        [Test] public void Blindness_on_both_sides_does_not_reduce_damage_and_holy_light_cleanses_it()
        {var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.ApplyStatus(StatusEffect.DarknessBlind);b.ApplyStatus(StatusEffect.DarknessBlind);new AttackContext(a,b,a.LeftHand,4,g).Resolve(new SimpleChoiceProvider(),g);Assert.AreEqual(16,b.Health);b.ApplyStatus(StatusEffect.HolyLight);Assert.IsFalse(b.HasStatus(StatusEffect.DarknessBlind));b.ApplyStatus(StatusEffect.DarknessBlind);Assert.IsFalse(b.HasStatus(StatusEffect.DarknessBlind));}
        [Test] public void Two_hit_attack_can_kill_after_grave_strength_and_dual_order_does_not_hide_it()
        {foreach(bool reverse in new[]{false,true}){var a=DualGeneralFactory.Create("A",reverse?CharacterDefinition.Champion:CharacterDefinition.ShadowMagic,reverse?CharacterDefinition.ShadowMagic:CharacterDefinition.Champion);var enemy=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(enemy,a,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.SetHealthExact(1);var hit=new AttackContext(enemy,a,enemy.LeftHand,4,g){HitCount=2};hit.Resolve(new SimpleChoiceProvider(),g);Assert.AreEqual(0,a.Health);Assert.IsTrue(CharacterBehaviors.FirstImplementer<IGraveStrength>(a).GraveSpent);}}
    }
}
