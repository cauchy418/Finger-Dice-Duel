using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class AncientRoleChoiceFlowTests
    {
        private static BoardController Board(CharacterDefinition role,out GameManager game)
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            game=CharacterFactory.CreateMatch(role,CharacterDefinition.Blank,ca,cb);
            return new BoardController(game,ca,cb);
        }
        private static AttackContext Hit(GameManager g,PlayerState attacker,PlayerState defender,int damage)
        {
            var c=new AttackContext(attacker,defender,attacker.LeftHand,damage,g){IgnoreStatBonuses=true};
            c.Resolve(g.GetChoiceProvider(defender),g);return c;
        }
        [Test] public void Land_three_offers_target_then_mandatory_end_seal_without_default_selection()
        {
            var board=Board(CharacterDefinition.CircleOfTheLand,out var g);var a=g.PlayerA;var b=g.PlayerB;
            b.LeftHand.SetValue(2);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.True(board.Hint.Contains("大地束缚"));
            Assert.False(b.HasStatus(StatusEffect.Restrained));Assert.True(board.PendingWizardChoice.TargetsPlayer);
            Assert.True(board.ChooseWizardHand(b.LeftHand));Assert.True(b.HasStatus(StatusEffect.Restrained));
            Assert.True(board.Hint.Contains("大地封锁"));Assert.False(board.PendingWizardChoice.AllowDecline);
            Assert.False(board.ChooseWizardHand(null));Assert.True(board.ChooseWizardHand(a.RightHand));
            Assert.True(a.RightHand.IsTargetRestricted);Assert.True(a.RightHand.CanBeOperated);
            Assert.AreEqual(1,g.TurnCount);Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);
            Assert.False(board.CanAct(a.RightHand));
        }
        [Test] public void Land_three_can_be_declined_but_end_seal_still_occurs()
        {
            var board=Board(CharacterDefinition.CircleOfTheLand,out var g);g.PlayerB.LeftHand.SetValue(2);
            board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
            Assert.True(board.ChooseWizardHand(null));Assert.False(g.PlayerB.HasStatus(StatusEffect.Restrained));
            Assert.True(board.Hint.Contains("大地封锁"));board.ChooseWizardHand(g.PlayerB.RightHand);
            Assert.True(g.PlayerB.RightHand.IsTargetRestricted);
        }
        [Test] public void Land_end_hook_runs_after_current_player_has_already_changed()
        {
            var board=Board(CharacterDefinition.CircleOfTheLand,out var g);var a=g.PlayerA;
            board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
            Assert.AreEqual(g.PlayerB,g.CurrentPlayer);Assert.True(board.Hint.Contains("大地封锁"));
            board.ChooseWizardHand(a.LeftHand);g.ForceActivePlayerForTest(a);g.BeginTurn();Assert.False(a.LeftHand.IsTargetRestricted);
        }
        [Test] public void Multiple_land_seals_have_independent_sources_and_death_clears_own_source()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.CircleOfTheLand);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.CircleOfTheLand);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var x=(CircleOfTheLandLogic)a.Character;var y=(CircleOfTheLandLogic)b.Character;
            x.RestrictHand(a.LeftHand,g);y.RestrictHand(a.LeftHand,g);g.BeginTurn();
            Assert.True(a.LeftHand.IsTargetRestricted,"A解除自己的来源，B仍封锁");
            b.TakeDamage(99);y.OnTurnStarted(b,g);Assert.False(a.LeftHand.IsTargetRestricted);
        }
        [Test] public void Seal_rejects_foreign_hands_and_reset_cleans_all_sources()
        {
            Board(CharacterDefinition.CircleOfTheLand,out var g);var a=g.PlayerA;var skill=(CircleOfTheLandLogic)a.Character;
            Assert.False(skill.RestrictHand(new Hand(3),g));skill.RestrictHand(a.LeftHand,g);g.ResetMatch();
            Assert.False(a.LeftHand.IsTargetRestricted);
        }
        [Test] public void Ancient_passive_break_queues_one_optional_player_target_in_actual_range()
        {
            Board(CharacterDefinition.OathOfTheAncients,out var g);var a=g.PlayerA;var b=g.PlayerB;
            var requests=new List<WizardChoice>();g.DeferWizardChoice=r=>{requests.Add(r);return true;};
            a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);Hit(g,b,a,4);
            Assert.AreEqual(1,requests.Count);Assert.True(requests[0].AllowDecline);Assert.True(requests[0].TargetsPlayer);
            Assert.False(b.HasStatus(StatusEffect.Restrained));requests[0].Choose(b.LeftHand);Assert.True(b.HasStatus(StatusEffect.Restrained));
        }
        [Test] public void Ancient_active_break_uses_human_target_choice_after_attack()
        {
            var board=Board(CharacterDefinition.OathOfTheAncients,out var g);var a=g.PlayerA;var b=g.PlayerB;
            a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);
            board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);board.ClickHand(b.LeftHand);
            Assert.True(board.Hint.Contains("古贤束缚"));Assert.False(b.HasStatus(StatusEffect.Restrained));
            Assert.True(board.ChooseWizardHand(null));Assert.False(b.HasStatus(StatusEffect.Restrained));
        }
        [Test] public void Ancient_high_health_healing_is_a_target_choice_not_an_automatic_enemy_heal()
        {
            Board(CharacterDefinition.OathOfTheAncients,out var g);g.PlayerB.SetHealthExact(12);
            var requests=new List<WizardChoice>();g.DeferWizardChoice=r=>{requests.Add(r);return true;};
            Hit(g,g.PlayerB,g.PlayerA,2);Assert.AreEqual(18,g.PlayerA.Health);Assert.AreEqual(12,g.PlayerB.Health);
            Assert.AreEqual(1,requests.Count);Assert.False(requests[0].AllowDecline);Assert.True(requests[0].TargetsPlayer);
            requests[0].Choose(g.PlayerB.LeftHand);Assert.AreEqual(14,g.PlayerB.Health);
        }
        [Test] public void Ancient_threshold_is_after_damage_and_lethal_damage_does_not_revive()
        {
            Board(CharacterDefinition.OathOfTheAncients,out var g);g.PlayerA.SetHealthExact(10);Hit(g,g.PlayerB,g.PlayerA,2);
            Assert.AreEqual(10,g.PlayerA.Health,"受伤后8，自动回复2");
            g.PlayerA.SetHealthExact(2);Hit(g,g.PlayerB,g.PlayerA,3);Assert.AreEqual(0,g.PlayerA.Health);
        }
        [Test] public void Ancient_direct_poison_damage_also_runs_the_after_damage_hook()
        {
            Board(CharacterDefinition.OathOfTheAncients,out var g);g.PlayerA.SetHealthExact(8);g.PlayerA.ApplyStatus(StatusEffect.Poison);
            g.ResolveStatusTurnStart(g.PlayerA);Assert.AreEqual(8,g.PlayerA.Health);
        }
        [Test] public void Fiend_human_can_pick_either_status_or_keep_false_life()
        {
            foreach(int choice in new[]{0,(int)StatusEffect.Disabled,(int)StatusEffect.Panic})
            {
                var board=Board(CharacterDefinition.TheFiend,out var g);var a=g.PlayerA;var b=g.PlayerB;
                a.GainFalseLife(5);b.LeftHand.SetValue(3);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);board.ClickHand(b.LeftHand);
                Assert.AreEqual(HandEffect.TheFiendPact,board.PendingEffect);Assert.AreEqual(3,board.PendingSkillOptions.Count);
                Assert.AreEqual(5,a.FalseLife);Assert.False(board.ChooseSkillOption(999));Assert.True(board.ChooseSkillOption(choice));
                Assert.AreEqual(choice==0?5:0,a.FalseLife);Assert.AreEqual(choice!=0,b.HasStatus((StatusEffect)choice));
                Assert.AreEqual(1,g.TurnCount);
            }
        }
        [Test] public void Fiend_full_health_and_dual_healing_overflow_grants_false_life_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=DualGeneralFactory.Create("A",reverse?CharacterDefinition.LifeDomain:CharacterDefinition.TheFiend,reverse?CharacterDefinition.TheFiend:CharacterDefinition.LifeDomain);
                a.Heal(9);Assert.AreEqual(9,a.FalseLife);a.Heal(5);Assert.AreEqual(10,a.FalseLife);
                a.TakeDamage(12);Assert.AreEqual(18,a.Health);Assert.AreEqual(0,a.FalseLife);
            }
        }
        [Test] public void Ancient_dual_after_damage_hook_works_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=DualGeneralFactory.Create("A",reverse?CharacterDefinition.Champion:CharacterDefinition.OathOfTheAncients,reverse?CharacterDefinition.OathOfTheAncients:CharacterDefinition.Champion);
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                a.SetHealthExact(8);Hit(g,b,a,2);Assert.AreEqual(8,a.Health);
            }
        }
        [Test] public void Fiend_killing_a_low_health_target_still_heals_and_can_overflow()
        {
            Board(CharacterDefinition.TheFiend,out var g);g.PlayerB.SetHealthExact(2);Hit(g,g.PlayerA,g.PlayerB,4);
            Assert.AreEqual(3,g.PlayerA.FalseLife);Assert.AreEqual(0,g.PlayerB.Health);
            foreach(bool reverse in new[]{false,true})
            {
                var a=DualGeneralFactory.Create("A",reverse?CharacterDefinition.LifeDomain:CharacterDefinition.TheFiend,reverse?CharacterDefinition.TheFiend:CharacterDefinition.LifeDomain);
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                b.SetHealthExact(6);Hit(g,a,b,4);Assert.AreEqual(3,a.FalseLife,"双将中仍能汲取并转化溢出");
            }
        }
        [Test] public void False_life_absorbs_without_triggering_actual_health_damage_hooks_and_AI_subtracts_it()
        {
            Board(CharacterDefinition.OathOfTheAncients,out var g);g.PlayerA.SetHealthExact(8);g.PlayerA.GainFalseLife(5);
            Hit(g,g.PlayerB,g.PlayerA,4);Assert.AreEqual(8,g.PlayerA.Health);Assert.AreEqual(1,g.PlayerA.FalseLife);
            var ai=new GreedyChoiceProvider();ai.SetGameForChoices(g);var move=new LegalMove(g.PlayerB,g.PlayerB.LeftHand,g.PlayerA.LeftHand);
            int before=ai.TotalLethalDamage(move,g.PlayerB,g.PlayerA,4);g.PlayerA.ClearFalseLife();
            Assert.AreEqual(before+1,ai.TotalLethalDamage(move,g.PlayerB,g.PlayerA,4));
        }
        [Test] public void Ancient_AI_controls_enemies_heals_allies_and_fiend_preserves_emergency_buffer()
        {
            var roles=new[]{CharacterDefinition.OathOfTheAncients,CharacterDefinition.Blank,CharacterDefinition.Blank,CharacterDefinition.Blank};
            var ps=new List<PlayerState>();foreach(var role in roles)ps.Add(CharacterFactory.CreatePlayer(role.DisplayName,role));
            var ai=new GreedyChoiceProvider();var g=GameManager.Create2v2(ps,new IChoiceProvider[]{ai,ai,ai,ai});ps[3].SetHealthExact(2);
            var hands=new List<Hand>{ps[1].LeftHand,ps[2].LeftHand,ps[3].LeftHand};
            Assert.AreSame(ps[3].LeftHand,ai.ChooseAncientTarget(AncientRoleChoices.AncientHeal,ps[0],hands,g));
            Assert.False(ReferenceEquals(ps[3].LeftHand,ai.ChooseAncientTarget(AncientRoleChoices.AncientRestrain,ps[0],hands,g)));
            ps[0].GainFalseLife(8);ps[0].SetHealthExact(6);
            Assert.AreEqual(0,ai.ChooseSkillOption(AncientRoleChoices.FiendPact,AncientRoleChoices.PactOptions(),ps[0],ps[1],g));
        }
        [Test] public void Fiend_periodic_damage_neither_spends_pact_buffer_nor_triggers_life_drain()
        {
            foreach(int buffer in new[]{0,5}){
                var a=CharacterFactory.CreatePlayer("妖魔",CharacterDefinition.TheFiend);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                a.SetHealthExact(8);a.GainFalseLife(buffer);a.ApplyStatus(StatusEffect.Burn,2);game.ResolveStatusTurnEnd(a);
                Assert.AreEqual(buffer==0?5:8,a.Health);Assert.AreEqual(buffer==0?0:2,a.FalseLife);
                Assert.False(a.HasStatus(StatusEffect.Disabled),"持续伤害不是妖魔对自己发起攻击。");
            }
        }
        [Test] public void False_life_marker_and_manual_show_points_not_turns_and_reset_removes_it()
        {
            Board(CharacterDefinition.TheFiend,out var g);g.PlayerA.GainFalseLife(7);
            var icons=CombatStatusIcons.For(g.PlayerA);Assert.AreEqual(1,icons.Count);Assert.AreEqual("7",icons[0].Counter);
            Assert.AreEqual("Effects/false_life",icons[0].ResourceKey);Assert.AreEqual(8,GuideSheets.CharacterMarks().Lines.Count);
            Assert.True(GuideSheets.CharacterMarks().Lines[3].Contains("绿色"));g.ResetMatch();Assert.AreEqual(0,CombatStatusIcons.For(g.PlayerA).Count);
        }
    }
}
