using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class TeamSupportAiTests
    {
        private sealed class RangedBlank:CharacterTemplate{public override int BaseAttackRange=>InfiniteAttackRange;}
        private static GameManager Four(out PlayerState paladin,out PlayerState enemy,out PlayerState enemy2,out PlayerState human,out GreedyChoiceProvider ai)
        {
            paladin=CharacterFactory.CreatePlayer("圣武士AI",CharacterDefinition.OathOfDevotion);enemy=CharacterFactory.CreatePlayer("敌人",CharacterDefinition.Blank);
            enemy2=CharacterFactory.CreatePlayer("敌人2",CharacterDefinition.Blank);human=CharacterFactory.CreatePlayer("玩家队友",CharacterDefinition.Blank);ai=new GreedyChoiceProvider();
            return GameManager.Create2v2(new[]{paladin,enemy,enemy2,human},new IChoiceProvider[]{ai,new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});
        }
        [Test] public void Shock_prefers_enemy_even_when_human_teammate_is_first_candidate()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);ai.Game=g;var req=new ChoiceRequest(HandEffect.OathOfDevotionShockTarget,"",new[]{d.LeftHand,b.LeftHand},decider:a);Assert.AreEqual(b.LeftHand,ai.ChooseHandFor(req,a));}
        [Test] public void Shield_form_and_active_break_both_shock_enemy_instead_of_first_ally()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);var skill=(OathOfDevotionLogic)a.Character;skill.OnShieldFormed(a,a.LeftHand,g);Assert.AreEqual(18,b.Health);Assert.AreEqual(20,d.Health);skill.OnOwnShieldBroken(a,a.LeftHand,g);Assert.AreEqual(16,b.Health);Assert.AreEqual(20,d.Health);}
        [Test] public void Protect_accepts_when_ally_unshielded_and_ai_has_a_shield()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);ai.Game=g;a.LeftHand.SetShieldState(ShieldState.Formed);var r=new ChoiceRequest(HandEffect.OathOfDevotionRedirect,"",new[]{a.LeftHand},true,decider:a,resultValue:8,relatedTarget:d,relatedActor:b);Assert.AreEqual(a.LeftHand,ai.ChooseHandFor(r,a));}
        [Test] public void Protect_declines_for_enemy_or_safe_shielded_ally_or_own_lethal_damage()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);ai.Game=g;var candidates=new[]{a.LeftHand};a.SetHealthExact(1);var r=new ChoiceRequest(HandEffect.OathOfDevotionRedirect,"",candidates,true,decider:a,resultValue:8,relatedTarget:d,relatedActor:b);Assert.AreEqual(null,ai.ChooseHandFor(r,a));a.SetHealthExact(20);d.LeftHand.SetShieldState(ShieldState.Formed);Assert.AreEqual(null,ai.ChooseHandFor(r,a));r=new ChoiceRequest(HandEffect.OathOfDevotionRedirect,"",candidates,true,decider:a,resultValue:8,relatedTarget:c,relatedActor:d);Assert.AreEqual(null,ai.ChooseHandFor(r,a));}
        [Test] public void Direct_attack_ai_paladin_redirects_and_preserves_human_health()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);a.LeftHand.SetShieldState(ShieldState.Formed);var ctx=new AttackContext(b,d,b.LeftHand,4,g);ctx.Resolve(g.GetChoiceProvider(d),g);Assert.True(ctx.WasRedirected);Assert.AreEqual(a,ctx.Defender);Assert.AreEqual(20,d.Health);Assert.True(ctx.WasBlocked);}
        [Test] public void Real_controller_collects_third_party_ai_protection_for_ordinary_attack()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider(),ai);board.BindPlayer(c,null,new SimpleChoiceProvider());board.BindPlayer(d,null,new SimpleChoiceProvider());g.ForceActivePlayerForTest(b);b.BindCharacter(new RangedBlank());a.LeftHand.SetShieldState(ShieldState.Formed);b.LeftHand.SetValue(1);d.LeftHand.SetValue(3);board.BeginTurn();board.ClickHand(b.LeftHand);board.ClickHand(d.LeftHand);board.ClickHand(d.LeftHand);for(int i=0;i<12&&board.Phase!=BoardPhase.ChoosingAttackerHand;i++){if(board.IsHumanChooser){if(board.CanDeclinePendingChoice)board.DeclinePendingChoice();else if(board.ChoiceCandidates.Count>0)board.ClickHand(board.ChoiceCandidates[0]);}else board.ResolvePendingAiChoice();}Assert.AreEqual(20,d.Health);Assert.True(a.LeftHand.State!=ShieldState.Formed);}
        [Test] public void Chain_break_never_selects_allied_shield_while_enemy_shield_available()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);ai.Game=g;ai.UseChainBreak=true;d.LeftHand.SetShieldState(ShieldState.Formed);b.RightHand.SetShieldState(ShieldState.Formed);var r=new ChoiceRequest(HandEffect.BreakerChainBreak,"",new[]{d.LeftHand,b.RightHand},true,decider:a);Assert.AreEqual(b.RightHand,ai.ChooseHandFor(r,a));}
        [Test] public void Abjuration_and_chalice_choose_injured_human_ally_not_wounded_enemy()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);d.SetHealthExact(5);b.SetHealthExact(1);Assert.AreEqual(d,ai.ResistanceTarget(a,g));Assert.AreEqual(d,ai.ChaliceTarget(a,new[]{b,d},g));}
        [Test] public void Ordinary_attack_selection_does_not_choose_human_ally_as_damage_target()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);ai.Game=g;var move=ai.ChooseMove(g,a);var target=ai.IntendedVictim;Assert.True(target==null||!g.IsTeammate(a,target));}
        [Test] public void Eight_faction_ai_support_and_shock_respect_all_actual_teammates()
        {
            var p=new PlayerState[8];var providers=new IChoiceProvider[8];var teams=new[]{0,1,1,2,2,0,1,0};var ai=new GreedyChoiceProvider();
            for(int i=0;i<8;i++){p[i]=CharacterFactory.CreatePlayer("P"+i,i==0?CharacterDefinition.OathOfDevotion:CharacterDefinition.Blank);providers[i]=i==0?ai:new SimpleChoiceProvider();}
            var g=GameManager.CreateEight(p,providers,teams);ai.Game=g;p[7].SetHealthExact(3);Assert.AreEqual(p[7],ai.ResistanceTarget(p[0],g));
            var req=new ChoiceRequest(HandEffect.OathOfDevotionShockTarget,"",new[]{p[7].LeftHand,p[1].LeftHand},decider:p[0]);Assert.AreEqual(p[1].LeftHand,ai.ChooseHandFor(req,p[0]));
            p[0].LeftHand.SetShieldState(ShieldState.Formed);req=new ChoiceRequest(HandEffect.OathOfDevotionRedirect,"",new[]{p[0].LeftHand},true,decider:p[0],relatedTarget:p[7],relatedActor:p[1],resultValue:8);Assert.AreEqual(p[0].LeftHand,ai.ChooseHandFor(req,p[0]));
        }
        [Test] public void Identity_support_uses_public_lord_allegiance_and_does_not_read_hidden_roles()
        {
            var p=new PlayerState[8];var providers=new IChoiceProvider[8];for(int i=0;i<8;i++){p[i]=CharacterFactory.CreatePlayer("P"+i,CharacterDefinition.Blank);providers[i]=new GreedyChoiceProvider();}
            var roles=new[]{IdentityRole.Lord,IdentityRole.Loyalist,IdentityRole.Loyalist,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Rebel,IdentityRole.Traitor};
            var g=GameManager.CreateIdentityEight(p,providers,roles);Assert.True(g.IsKnownAlly(p[1],p[0]));Assert.False(g.IsKnownAlly(p[0],p[1]));Assert.False(g.IsKnownAlly(p[3],p[0]));p[0].SetHealthExact(2);
            Assert.AreEqual(p[0],((GreedyChoiceProvider)providers[1]).ResistanceTarget(p[1],g));
        }
        [Test] public void Healthy_life_cleric_selects_healing_to_rescue_or_cleanse_human_ally()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);CharacterFactory.Configure(a,CharacterDefinition.LifeDomain);ai.Game=g;d.SetHealthExact(2);var r=new ChoiceRequest(HandEffect.LifeDomainAttackOrHeal,"",new[]{a.LeftHand},true,decider:a);Assert.AreEqual(a.LeftHand,ai.ChooseHandFor(r,a));Assert.AreEqual(d,LifeDomainLogic.BestHealTargetFor(a,g));d.SetHealthExact(20);d.ApplyStatus(StatusEffect.Frozen);Assert.AreEqual(a.LeftHand,ai.ChooseHandFor(r,a));Assert.AreEqual(d,LifeDomainLogic.BestHealTargetFor(a,g));}
        [Test] public void Ai_protection_also_handles_reactive_special_attacks_without_precollected_answers()
        {var g=Four(out var a,out var b,out var c,out var d,out var ai);var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider(),ai);a.LeftHand.SetShieldState(ShieldState.Formed);var ctx=new AttackContext(b,d,b.LeftHand,2,g){IsSpecialAttack=true,IgnoreStatBonuses=true};ctx.Resolve(g.GetChoiceProvider(d),g);Assert.AreEqual(a,ctx.Defender);Assert.True(ctx.WasBlocked);Assert.AreEqual(20,d.Health);}
        [Test] public void Dual_valor_ai_can_activate_its_bard_part_in_both_primary_orders()
        {foreach(bool reverse in new[]{false,true}){var a=FingerDiceDuel.Characters.DualGeneralFactory.Create("A",reverse?CharacterDefinition.EldritchKnight:CharacterDefinition.CollegeOfValor,reverse?CharacterDefinition.CollegeOfValor:CharacterDefinition.EldritchKnight);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new GreedyChoiceProvider(),new GreedyChoiceProvider());a.TookDamageThisTurn=true;Assert.True(CollegeOfValorAi.TryActivate(a,g));Assert.True(b.CollegeOfValorDefenseOffset<0||a.CollegeOfValorAttackOffset>0);}}
    }
}
