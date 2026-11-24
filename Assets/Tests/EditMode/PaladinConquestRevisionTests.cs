using System.Collections.Generic;
using System.Linq;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class PaladinConquestRevisionTests
    {
        private static GameManager Match()=>CharacterFactory.CreateMatch(CharacterDefinition.OathOfConquest,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());
        private static AttackContext Hit(GameManager game,PlayerState a,PlayerState b)
        {var attack=new AttackContext(a,b,a.RightHand,4,game);attack.Resolve(game.GetChoiceProvider(b),game);return attack;}
        private static List<WizardChoice> Capture(GameManager game)
        {var choices=new List<WizardChoice>();game.DeferWizardChoice=q=>{choices.Add(q);return true;};return choices;}
        private static void Shield(Hand hand){hand.SetValue(5);hand.SetShieldState(ShieldState.Formed);}
        [Test] public void Conquest_attacks_only_grow_without_automatically_inflicting_panic()
        {
            var game=Match();var a=game.PlayerA;var b=game.PlayerB;a.LeftHand.SetValue(5);
            Hit(game,a,b);Hit(game,a,b);
            Assert.AreEqual(2,((OathOfConquestLogic)a.Character).AttackStacks);Assert.False(b.HasStatus(StatusEffect.Panic));
        }
        [Test] public void Conquest_aura_requires_strict_corresponding_effective_bonus_and_has_no_range_limit()
        {
            var players=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.OathOfConquest),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
            var game=GameManager.Create2v2(players,players.Select(p=>(IChoiceProvider)new SimpleChoiceProvider()).ToArray());var owner=players[0];
            foreach(var target in players)target.SetDynamicBonuses(0,0);
            Assert.AreEqual(0,OathOfConquestLogic.AuraCandidates(owner,game).Count);
            players[2].SetDynamicBonuses(1,-1);Assert.False(game.CanReach(owner,players[2]));
            Shield(owner.LeftHand);var choices=Capture(game);game.BreakActiveShieldAndNotify(owner,owner.LeftHand);
            Assert.AreEqual(1,choices.Count);Assert.True(choices[0].AllowDecline);Assert.AreEqual(1,choices[0].Candidates.Count);
            Assert.True(choices[0].Choose(players[2].LeftHand));Assert.AreEqual(2,players[2].StatusTurnsLeft(StatusEffect.Panic));Assert.AreSame(owner,players[2].StatusSourceOf(StatusEffect.Panic));
        }
        [Test] public void Conquest_passive_break_prompts_once_per_shield_and_can_decline()
        {
            var game=Match();var owner=game.PlayerA;owner.SetDynamicBonuses(1,0);Shield(owner.LeftHand);Shield(owner.RightHand);
            var choices=Capture(game);game.BreakShieldAndNotify(owner,owner.LeftHand,game.PlayerB);game.BreakShieldAndNotify(owner,owner.RightHand,game.PlayerB);
            Assert.AreEqual(2,choices.Count);Assert.True(choices[0].Choose(null));Assert.False(game.PlayerB.HasStatus(StatusEffect.Panic));
            Assert.True(choices[1].Choose(game.PlayerB.LeftHand));Assert.AreEqual(2,game.PlayerB.StatusTurnsLeft(StatusEffect.Panic));
        }
        [Test] public void Conquest_panic_from_blocked_attack_keeps_two_turn_skill_duration()
        {
            var game=Match();var owner=game.PlayerA;owner.SetDynamicBonuses(1,0);Shield(owner.LeftHand);Shield(game.PlayerB.LeftHand);
            var choices=Capture(game);Hit(game,game.PlayerB,owner);Assert.AreEqual(1,choices.Count);
            choices[0].Choose(game.PlayerB.LeftHand);Assert.AreEqual(2,game.PlayerB.StatusTurnsLeft(StatusEffect.Panic));
        }
        [Test] public void Conquest_idle_decay_reduces_both_stacks_even_while_holding_five()
        {
            var game=Match();var owner=game.PlayerA;owner.LeftHand.SetValue(5);Hit(game,owner,game.PlayerB);Hit(game,game.PlayerB,owner);
            var skill=(OathOfConquestLogic)owner.Character;game.ForceActivePlayerForTest(owner);game.BeginTurn();
            skill.OnTurnEnded(owner,game);Assert.AreEqual(0,skill.AttackStacks);Assert.AreEqual(0,skill.DefenseStacks);
            skill.OnTurnEnded(owner,game);Assert.AreEqual(0,skill.AttackStacks);Assert.AreEqual(0,skill.DefenseStacks);
        }
        [Test] public void Conquest_attack_preserves_both_stacks_even_without_five_at_turn_end()
        {
            var game=Match();var owner=game.PlayerA;owner.LeftHand.SetValue(5);Hit(game,game.PlayerB,owner);game.BeginTurn();Hit(game,owner,game.PlayerB);
            owner.LeftHand.SetValue(3);var skill=(OathOfConquestLogic)owner.Character;skill.OnTurnEnded(owner,game);
            Assert.AreEqual(1,skill.AttackStacks);Assert.AreEqual(1,skill.DefenseStacks);
        }
        [Test] public void Vengeance_bonus_and_ai_lethal_estimate_use_two_on_marked_target()
        {
            var game=CharacterFactory.CreateMatch(CharacterDefinition.OathOfVengeance,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());game.BeginTurn();
            var owner=game.PlayerA;var target=game.PlayerB;var ai=new GreedyChoiceProvider{Game=game};
            Assert.AreEqual(2,((OathOfVengeanceLogic)owner.Character).BonusAgainst(owner,target));
            Assert.AreEqual(6,ai.TotalLethalDamage(new LegalMove(owner,owner.LeftHand,target.LeftHand),owner,target,4,game));
            var attack=Hit(game,owner,target);Assert.AreEqual(6,attack.AppliedDamage);
        }
        [Test] public void Ai_conquest_aura_declines_allies_and_prefers_enemy_without_existing_panic()
        {
            var players=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.OathOfConquest),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
            var game=GameManager.Create2v2(players,players.Select(p=>(IChoiceProvider)new SimpleChoiceProvider()).ToArray());var owner=players[0];owner.SetDynamicBonuses(1,1);
            var ai=new GreedyChoiceProvider{Game=game};var ally=players.First(p=>p!=owner&&game.IsKnownAlly(owner,p));
            Assert.True(ai.ChooseArcaneTarget(DivineChoices.ConquestAura,owner,new[]{ally},game)==null);
            var enemies=players.Where(p=>!game.IsKnownAlly(owner,p)).ToArray();enemies[0].ApplyStatus(StatusEffect.Panic,2,owner);
            Assert.AreSame(enemies[1],ai.ChooseArcaneTarget(DivineChoices.ConquestAura,owner,OathOfConquestLogic.AuraCandidates(owner,game),game));
        }
        [Test] public void Ai_guardian_can_accept_with_plain_five_and_no_formed_shield()
        {
            foreach(var role in new[]{CharacterDefinition.OathOfTheCrown,CharacterDefinition.OathOfTheAncients,CharacterDefinition.Oathbreaker,CharacterDefinition.OathOfConquest,CharacterDefinition.OathOfVengeance,CharacterDefinition.OathOfDevotion})
            {
                var players=new[]{CharacterFactory.CreatePlayer("A",role),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
                var game=GameManager.Create2v2(players,players.Select(p=>(IChoiceProvider)new SimpleChoiceProvider()).ToArray());var owner=players[0];owner.LeftHand.SetValue(5);
                var ally=players.First(p=>p!=owner&&game.IsKnownAlly(owner,p));ally.SetHealthExact(1);var foe=players.First(p=>!game.IsKnownAlly(owner,p));
                var request=PaladinGuardianAura.Request(owner,foe,ally,game,4);var ai=new GreedyChoiceProvider{Game=game};Assert.True(ai.ChooseHandFor(request,owner)!=null,role.Id);
            }
        }
        [Test] public void Ai_devotion_guardian_accepts_plain_four_and_six()
        {
            foreach(int value in new[]{4,6})
            {
                var players=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.OathOfDevotion),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};
                var game=GameManager.Create2v2(players,players.Select(p=>(IChoiceProvider)new SimpleChoiceProvider()).ToArray());var owner=players[0];owner.LeftHand.SetValue(value);owner.RightHand.SetValue(2);
                var ally=players.First(p=>p!=owner&&game.IsKnownAlly(owner,p));ally.SetHealthExact(1);var foe=players.First(p=>!game.IsKnownAlly(owner,p));
                var ai=new GreedyChoiceProvider{Game=game};Assert.True(ai.ChooseHandFor(PaladinGuardianAura.Request(owner,foe,ally,game,4),owner)!=null);
            }
        }
    }
}
