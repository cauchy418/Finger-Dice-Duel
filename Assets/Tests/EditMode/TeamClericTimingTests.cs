using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class TeamClericTimingTests
    {
        private static GameManager Four(CharacterDefinition role,out PlayerState a,out PlayerState b,out PlayerState c,out PlayerState d,out GreedyChoiceProvider ai)
        {
            a=CharacterFactory.CreatePlayer("A",role);b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank);d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank);
            ai=new GreedyChoiceProvider();var game=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{ai,new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});ai.Game=game;return game;
        }
        private static Hand HealChoice(GameManager g,GreedyChoiceProvider ai,PlayerState a,params PlayerState[] patients)
        {
            var hands=new Hand[patients.Length];for(int i=0;i<hands.Length;i++)hands[i]=patients[i].LeftHand;
            return ai.ChooseHandFor(new ChoiceRequest(HandEffect.LifeDomainHealTarget,"",hands,decider:a,resultValue:9),a);
        }
        [Test] public void All_nine_clerics_rescue_ally_before_lower_health_enemy()
        {
            foreach(var role in new[]{CharacterDefinition.LifeDomain,CharacterDefinition.PurificationDomain,CharacterDefinition.LightDomain,
                CharacterDefinition.WarDomain,CharacterDefinition.NatureDomain,CharacterDefinition.DeathDomain,CharacterDefinition.KnowledgeDomain,
                CharacterDefinition.TrickeryDomain,CharacterDefinition.TempestDomain})
            {
                var g=Four(role,out var a,out var b,out var c,out var d,out var ai);d.SetHealthExact(2);b.SetHealthExact(1);
                Assert.AreEqual(d.LeftHand,HealChoice(g,ai,a,b,a,d),role.ToString());
            }
        }
        [Test] public void Healthy_cleric_values_urgent_teammate_healing_in_move_score()
        {
            var g=Four(CharacterDefinition.TrickeryDomain,out var a,out var b,out var c,out var d,out var ai);
            a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            int healthy=ai.ScoreMoveForTest(move,a,g);d.SetHealthExact(2);int urgent=ai.ScoreMoveForTest(move,a,g);
            Assert.True(urgent>healthy+80,"A full-health healer must value teammate rescue.");
            d.SetHealthExact(20);d.ApplyStatus(StatusEffect.Decay);Assert.True(ai.ScoreMoveForTest(move,a,g)<urgent);
        }
        [Test] public void Purification_avoids_removing_allied_blessings_and_prefers_disabling_cleanse()
        {
            var g=Four(CharacterDefinition.PurificationDomain,out var a,out var b,out var c,out var d,out var ai);
            a.SetHealthExact(16);d.SetHealthExact(14);d.ApplyStatus(StatusEffect.NatureBlessing,2);d.ApplyStatus(StatusEffect.LightBlessing,2);
            Assert.AreEqual(a.LeftHand,HealChoice(g,ai,a,d,a,b));
            d.RemoveStatus(StatusEffect.NatureBlessing);d.RemoveStatus(StatusEffect.LightBlessing);d.ApplyStatus(StatusEffect.Frozen,2);
            Assert.AreEqual(d.LeftHand,HealChoice(g,ai,a,a,d,b));
            d.SetHealthExact(20);a.SetHealthExact(20);Assert.AreEqual(d.LeftHand,HealChoice(g,ai,a,a,d,b));
        }
        [Test] public void Nature_and_light_blessings_do_not_refresh_covered_or_blocked_allies()
        {
            foreach(var role in new[]{CharacterDefinition.NatureDomain,CharacterDefinition.LightDomain})
            {
                var g=Four(role,out var a,out var b,out var c,out var d,out var ai);
                var status=role==CharacterDefinition.NatureDomain?StatusEffect.NatureBlessing:StatusEffect.LightBlessing;
                a.ApplyStatus(status,2);Assert.AreEqual(d.LeftHand,HealChoice(g,ai,a,a,d,b));
                d.ApplyStatus(StatusEffect.HolyLight,2);a.RemoveStatus(status);Assert.AreEqual(a.LeftHand,HealChoice(g,ai,a,d,a,b));
            }
        }
        [Test] public void War_blessed_nine_keeps_heal_for_critical_ally_then_attacks_when_team_recovers()
        {
            var g=Four(CharacterDefinition.WarDomain,out var a,out var b,out var c,out var d,out var ai);
            a.ApplyStatus(StatusEffect.WarBlessing,2);d.SetHealthExact(2);
            Assert.AreEqual(0,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,b,g));
            d.SetHealthExact(20);Assert.AreEqual(1,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,b,g));
            b.SetHealthExact(1);d.SetHealthExact(2);Assert.AreEqual(1,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,b,g));
        }
        [Test] public void Ancients_forced_heal_prefers_ally_then_minimizes_enemy_recovery_when_only_enemies_remain()
        {
            var g=Four(CharacterDefinition.OathOfTheAncients,out var a,out var b,out var c,out var d,out var ai);
            b.SetHealthExact(8);c.SetHealthExact(8);d.SetHealthExact(8);b.ApplyStatus(StatusEffect.Scorch,2);
            Assert.AreEqual(d.LeftHand,ai.ChooseAncientTarget(AncientRoleChoices.AncientHeal,a,new[]{b.LeftHand,c.LeftHand,d.LeftHand},g));
            Assert.AreEqual(b.LeftHand,ai.ChooseAncientTarget(AncientRoleChoices.AncientHeal,a,new[]{c.LeftHand,b.LeftHand},g));
            c.ApplyStatus(StatusEffect.Tinnitus,2);
            Assert.AreEqual(c.LeftHand,ai.ChooseAncientTarget(AncientRoleChoices.AncientHeal,a,new[]{b.LeftHand,c.LeftHand},g));
        }
        private sealed class RangedBlank:CharacterTemplate { public override int BaseAttackRange=>InfiniteAttackRange; }
        [Test] public void Conquest_panic_prefers_enemy_whose_attack_penalty_also_protects_teammate()
        {
            var g=Four(CharacterDefinition.OathOfConquest,out var a,out var b,out var c,out var d,out var ai);
            c.BindCharacter(new RangedBlank());a.ApplyStatus(StatusEffect.WarBlessing,2);
            Assert.True(g.CanReach(b,a));Assert.True(g.CanReach(c,a));Assert.False(g.CanReach(b,d));Assert.True(g.CanReach(c,d));
            Assert.AreEqual(c,ai.ChooseArcaneTarget(DivineChoices.ConquestAura,a,new[]{b,c},g));
            c.ApplyStatus(StatusEffect.Panic,2,a);
            Assert.AreEqual(b,ai.ChooseArcaneTarget(DivineChoices.ConquestAura,a,new[]{c,b},g));
        }
        [Test] public void Actual_board_healing_resolves_selected_teammate_once_and_advances()
        {
            var g=Four(CharacterDefinition.TrickeryDomain,out var a,out var b,out var c,out var d,out var ai);
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider(),ai);
            board.BindPlayer(c,null,new SimpleChoiceProvider());board.BindPlayer(d,null,new SimpleChoiceProvider());
            a.LeftHand.SetValue(1);a.RightHand.SetValue(3);b.LeftHand.SetValue(8);d.SetHealthExact(2);
            board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            for(int i=0;i<12&&g.TurnCount==0;i++)
            {
                if(board.IsHumanChooser)
                {
                    if(board.CanDeclinePendingChoice)board.DeclinePendingChoice();
                    else if(board.ChoiceCandidates.Count>0)board.ClickHand(board.ChoiceCandidates[0]);
                }
                else board.ResolvePendingAiChoice();
            }
            Assert.AreEqual(11,d.Health);Assert.AreEqual(20,a.Health);Assert.AreEqual(20,b.Health);Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void Devotion_shock_finishes_unprotected_enemy_before_false_life_enemy()
        {
            var g=Four(CharacterDefinition.OathOfDevotion,out var a,out var b,out var c,out var d,out var ai);
            b.SetHealthExact(1);b.GainFalseLife(3);c.SetHealthExact(1);
            var request=new ChoiceRequest(HandEffect.OathOfDevotionShockTarget,"",new[]{b.LeftHand,c.LeftHand,d.LeftHand},decider:a);
            Assert.AreEqual(c.LeftHand,ai.ChooseHandFor(request,a));
        }
        [Test] public void Forced_devotion_shock_uses_false_life_before_harming_an_ally()
        {
            var g=Four(CharacterDefinition.OathOfDevotion,out var a,out var b,out var c,out var d,out var ai);
            // Only allies are offered. Equal health makes temporary life the deciding resource.
            a.SetHealthExact(8);d.SetHealthExact(8);d.GainFalseLife(3);
            var req=new ChoiceRequest(HandEffect.OathOfDevotionShockTarget,"",new[]{a.LeftHand,d.LeftHand},decider:a);
            Assert.AreEqual(d.LeftHand,ai.ChooseHandFor(req,a));
            g=Four(CharacterDefinition.OathOfDevotion,out a,out b,out c,out d,out ai);
            a.SetHealthExact(1);a.GainFalseLife(3);d.SetHealthExact(2);
            req=new ChoiceRequest(HandEffect.OathOfDevotionShockTarget,"",new[]{d.LeftHand,a.LeftHand},decider:a);
            Assert.AreEqual(a.LeftHand,ai.ChooseHandFor(req,a));
        }
    }
}
