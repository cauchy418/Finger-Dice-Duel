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
    [TestFixture] public class LoreStormIllusionTests
    {
        private static BoardController Board(CharacterDefinition a,CharacterDefinition b,out GameManager game)
        {var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();game=CharacterFactory.CreateMatch(a,b,ca,cb);return new BoardController(game,ca,cb);}
        private static void Record(CollegeOfLoreLogic lore,PlayerState owner,GameManager game,int value)
        {owner.LeftHand.SetValue(value);lore.OnOperationResolved(owner,owner.LeftHand,1,1,Hand.GetEffectType(value),game,new SimpleChoiceProvider());}
        [Test] public void New_roles_have_correct_stats_alignments_profiles_and_aliases()
        {
            foreach(var def in new[]{CharacterDefinition.CollegeOfLore,CharacterDefinition.StormSorcery,CharacterDefinition.SchoolOfIllusion}){
                var p=CharacterFactory.CreatePlayer("A",def);Assert.AreEqual(0,p.Character.AttackBonus);Assert.AreEqual(-1,p.Character.DefenseBonus);Assert.True(HandValueProfiles.HasExplicitProfile(def));Assert.AreEqual(def,CharacterDefinition.Find(CharacterIdentity.Find(def.Id).LegacyId));
            }
            Assert.AreEqual(Alignment.LawfulNeutral,CharacterCodex.For(CharacterDefinition.CollegeOfLore).Alignment);
            Assert.AreEqual(Alignment.ChaoticNeutral,CharacterCodex.For(CharacterDefinition.StormSorcery).Alignment);
            Assert.AreEqual(Alignment.ChaoticNeutral,CharacterCodex.For(CharacterDefinition.SchoolOfIllusion).Alignment);
        }
        [Test] public void Lore_records_unique_actual_results_but_not_initial_hands()
        {
            Board(CharacterDefinition.CollegeOfLore,CharacterDefinition.Blank,out var g);var lore=(CollegeOfLoreLogic)g.PlayerA.Character;
            Assert.AreEqual(0,lore.RecordCount);Record(lore,g.PlayerA,g,3);Record(lore,g.PlayerA,g,3);Record(lore,g.PlayerA,g,0);Assert.AreEqual(1,lore.RecordCount);Assert.True(lore.HasRecord(3));
        }
        [Test] public void Lore_growth_is_optional_clears_records_and_stacks_permanent_stats()
        {
            Board(CharacterDefinition.CollegeOfLore,CharacterDefinition.Blank,out var g);var a=g.PlayerA;var lore=(CollegeOfLoreLogic)a.Character;WizardChoice choice=null;g.DeferWizardChoice=c=>{choice=c;return true;};
            for(int n=1;n<=9;n++)Record(lore,a,g,n);lore.OnTurnEnded(a,g);choice.ChooseOption(0);Assert.AreEqual(9,lore.RecordCount);Assert.AreEqual(0,lore.LoreLevel);
            lore.OnTurnEnded(a,g);choice.ChooseOption(1);Assert.AreEqual(0,lore.RecordCount);Assert.AreEqual(1,lore.AttackBonus);Assert.AreEqual(0,lore.DefenseBonus);Assert.AreEqual(2,lore.BaseAttackRange);
            for(int n=1;n<=9;n++)Record(lore,a,g,n);lore.OnTurnEnded(a,g);choice.ChooseOption(1);Assert.AreEqual(2,lore.LoreLevel);Assert.AreEqual(3,lore.BaseAttackRange);
        }
        [Test] public void Lore_human_defense_uses_matching_record_and_respects_decline()
        {
            foreach(bool use in new[]{true,false}){
                var board=Board(CharacterDefinition.Blank,CharacterDefinition.CollegeOfLore,out var g);var b=g.PlayerB;var lore=(CollegeOfLoreLogic)b.Character;Record(lore,b,g,4);b.LeftHand.SetValue(3);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(b.LeftHand);board.ClickHand(b.LeftHand);
                Assert.AreEqual(HandEffect.CollegeOfLoreDefense,board.PendingEffect);Assert.AreEqual(b,board.Chooser);board.ChooseSkillOption(use?1:0);Assert.AreEqual(use?17:15,b.Health);Assert.AreEqual(!use,lore.HasRecord(4));
            }
        }
        [Test] public void Lore_cannot_defend_an_unrecorded_number_and_reset_clears_growth()
        {
            Board(CharacterDefinition.Blank,CharacterDefinition.CollegeOfLore,out var g);var lore=(CollegeOfLoreLogic)g.PlayerB.Character;Record(lore,g.PlayerB,g,6);
            new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g).Resolve(new SimpleChoiceProvider(),g);Assert.True(lore.HasRecord(6));g.ResetMatch();Assert.AreEqual(0,lore.RecordCount);Assert.AreEqual(0,lore.LoreLevel);
        }
        [Test] public void Storm_offers_both_resistances_at_initialization_and_human_picks_thunder()
        {
            var board=Board(CharacterDefinition.StormSorcery,CharacterDefinition.Blank,out var g);board.BeginTurn();Assert.AreEqual(2,board.PendingSkillOptions.Count);Assert.False(g.PlayerA.HasResistance(AttackDamageType.Lightning));
            board.ChooseSkillOption((int)AttackDamageType.Thunder);Assert.True(g.PlayerA.HasResistance(AttackDamageType.Thunder));Assert.False(g.PlayerA.HasResistance(AttackDamageType.Lightning));
            board.BeginTurn();Assert.AreEqual(0,board.PendingSkillOptions.Count);
        }
        [Test] public void Storm_initializes_even_before_its_first_active_turn_and_resets()
        {
            var board=Board(CharacterDefinition.Blank,CharacterDefinition.StormSorcery,out var g);board.BeginTurn();Assert.AreEqual(g.PlayerB,board.Chooser);board.ChooseSkillOption((int)AttackDamageType.Lightning);
            Assert.True(g.PlayerB.HasResistance(AttackDamageType.Lightning));g.ResetMatch();Assert.False(g.PlayerB.HasResistance(AttackDamageType.Lightning));
        }
        [Test] public void Storm_random_boundaries_are_thirty_thirty_forty_and_preview_does_not_roll()
        {
            foreach(int roll in new[]{0,29,30,59,60,99}){
                Board(CharacterDefinition.StormSorcery,CharacterDefinition.Blank,out var g);var storm=(StormSorceryLogic)g.PlayerA.Character;storm.ForcedRoll=roll;
                var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);Assert.AreEqual(AttackDamageType.Bludgeoning,storm.AttackTypeOf(c));c.Resolve(new SimpleChoiceProvider(),g);
                Assert.AreEqual(roll<30?AttackDamageType.Lightning:roll<60?AttackDamageType.Thunder:AttackDamageType.Bludgeoning,c.AttackType);
                if(roll<60)Assert.True(g.PlayerB.HasStatus(StatusNames.StatusOf(AttackDamageTypes.ToDamageType(c.AttackType))));
            }
        }
        [Test] public void Storm_extra_operation_is_optional_and_changes_only_hand_without_healing()
        {
            var board=Board(CharacterDefinition.StormSorcery,CharacterDefinition.Blank,out var g);board.BeginTurn();board.ChooseSkillOption((int)AttackDamageType.Thunder);g.PlayerA.SetHealthExact(12);
            board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);Assert.True(board.Hint.Contains("风暴余势"));Assert.AreEqual(g.PlayerA,board.Chooser);Assert.AreEqual(1,g.TurnCount);
            board.ChooseWizardHand(g.PlayerA.LeftHand);g.PlayerB.LeftHand.SetValue(7);board.ChooseWizardHand(g.PlayerB.LeftHand);Assert.AreEqual(9,g.PlayerA.LeftHand.Value);Assert.AreEqual(12,g.PlayerA.Health);Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(1,g.TurnCount);Assert.True(board.PendingWizardChoice==null);
        }
        [Test] public void Storm_extra_operation_can_be_declined_at_each_step()
        {
            foreach(bool source in new[]{true,false}){
                var board=Board(CharacterDefinition.StormSorcery,CharacterDefinition.Blank,out var g);board.BeginTurn();board.ChooseSkillOption((int)AttackDamageType.Lightning);board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
                if(!source)board.ChooseWizardHand(g.PlayerA.LeftHand);Assert.True(board.ChooseWizardHand(null));Assert.AreEqual(2,g.PlayerA.LeftHand.Value);Assert.AreEqual(1,g.TurnCount);
            }
        }
        [Test] public void Extra_operation_never_calls_lore_recording_or_numeric_effects_in_a_raw_container()
        {
            var storm=new StormSorceryLogic();var lore=new CollegeOfLoreLogic();var a=new PlayerState("历史容器",new DualGeneralBehavior(storm,lore));var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            a.LeftHand.SetValue(1);b.LeftHand.SetValue(7);Assert.True(g.ApplyStormFreeOperation(a,a.LeftHand,b.LeftHand));Assert.AreEqual(8,a.LeftHand.Value);Assert.AreEqual(20,b.Health);Assert.AreEqual(0,lore.RecordCount);Assert.AreEqual(0,g.TurnCount);
        }
        [Test] public void Illusion_fifty_percent_boundaries_and_cached_choice_are_deterministic()
        {
            Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);var mage=(SchoolOfIllusionLogic)g.PlayerB.Character;g.PlayerB.LeftHand.SetValue(2);g.PlayerB.RightHand.SetValue(7);mage.ForcedRoll=49;
            var first=g.PrepareOperationAddend(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);Assert.AreEqual(g.PlayerB.RightHand,first);mage.ForcedRoll=99;Assert.AreEqual(first,g.PrepareOperationAddend(g.PlayerA,g.PlayerA.LeftHand,first));
            g.ClearPreparedOperationAddend();Assert.AreEqual(g.PlayerB.LeftHand,g.PrepareOperationAddend(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand));
        }
        [Test] public void Illusion_respects_alternate_hand_legality_and_preview_never_rolls()
        {
            Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);var mage=(SchoolOfIllusionLogic)g.PlayerB.Character;mage.ForcedRoll=0;g.PlayerB.LeftHand.SetValue(2);g.PlayerB.RightHand.SetValue(7);g.PlayerB.RightHand.SetTargetRestricted(true);
            g.GetLegalMoves(g.PlayerA);Assert.AreEqual(g.PlayerB.LeftHand,g.PrepareOperationAddend(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand));
        }
        [Test] public void Illusion_controller_uses_redirected_sum_for_target_prompt_and_actual_attack()
        {
            var board=Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);var mage=(SchoolOfIllusionLogic)g.PlayerB.Character;mage.ForcedRoll=0;g.PlayerB.LeftHand.SetValue(2);g.PlayerB.RightHand.SetValue(7);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);board.ClickHand(g.PlayerB.LeftHand);Assert.AreEqual(8,g.PlayerA.LeftHand.Value);Assert.AreEqual(11,g.PlayerB.Health);Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void Illusion_defense_asks_owner_and_changes_only_smaller_hand_when_accepted()
        {
            foreach(bool use in new[]{true,false}){
                var board=Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);g.PlayerA.LeftHand.SetValue(3);g.PlayerB.LeftHand.SetValue(1);g.PlayerB.RightHand.SetValue(3);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
                Assert.AreEqual(HandEffect.SchoolOfIllusionDefense,board.PendingEffect);Assert.AreEqual(g.PlayerB,board.Chooser);board.ChooseSkillOption(use?1:0);Assert.AreEqual(use?2:1,g.PlayerB.LeftHand.Value);Assert.AreEqual(3,g.PlayerB.RightHand.Value);Assert.AreEqual(use?17:15,g.PlayerB.Health);
            }
        }
        [Test] public void Whole_attack_defenses_halve_stat_bonuses_in_the_actual_controller()
        {
            foreach(var role in new[]{CharacterDefinition.CollegeOfLore,CharacterDefinition.SchoolOfIllusion}){
                var board=Board(CharacterDefinition.Blank,role,out var g);var a=g.PlayerA;var b=g.PlayerB;
                a.SetDynamicBonuses(2,0);a.LeftHand.SetValue(3);
                if(b.Character is CollegeOfLoreLogic lore)Record(lore,b,g,4);
                b.LeftHand.SetValue(1);b.RightHand.SetValue(3);board.BeginTurn();
                board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);board.ClickHand(b.LeftHand);board.ChooseSkillOption(1);
                Assert.AreEqual(16,b.Health,"(4 + 攻2 - 防(-1)) / 2 向上取整应为4。");
            }
        }
        [Test] public void Whole_attack_defenses_apply_to_every_segment_after_stat_bonuses()
        {
            foreach(var role in new[]{CharacterDefinition.CollegeOfLore,CharacterDefinition.SchoolOfIllusion}){
                var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",role);
                var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.SetDynamicBonuses(2,0);
                if(b.Character is CollegeOfLoreLogic lore)Record(lore,b,g,8);
                b.LeftHand.SetValue(1);b.RightHand.SetValue(3);
                var attack=new AttackContext(a,b,a.LeftHand,8,g){HitCount=2};attack.Resolve(new SimpleChoiceProvider(),g);
                Assert.AreEqual(8,attack.AppliedDamage,"每段(4+2+1)/2向上取整，共8。");
            }
        }
        [Test] public void Status_damage_does_not_spend_lore_records_or_change_illusion_hands()
        {
            foreach(var role in new[]{CharacterDefinition.CollegeOfLore,CharacterDefinition.SchoolOfIllusion}){
                var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",role);
                var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                if(b.Character is CollegeOfLoreLogic lore)Record(lore,b,g,4);
                b.LeftHand.SetValue(1);b.RightHand.SetValue(3);
                var attack=new AttackContext(a,b,a.LeftHand,4,g){IsStatusDamage=true,IgnoreStatBonuses=true};attack.Resolve(new SimpleChoiceProvider(),g);
                Assert.AreEqual(4,attack.AppliedDamage);Assert.AreEqual(1,b.LeftHand.Value);
                if(b.Character is CollegeOfLoreLogic book)Assert.True(book.HasRecord(4));
            }
        }
        [Test] public void New_resource_icon_displays_actual_recorded_digits_and_count()
        {
            Board(CharacterDefinition.CollegeOfLore,CharacterDefinition.Blank,out var g);var lore=(CollegeOfLoreLogic)g.PlayerA.Character;Record(lore,g.PlayerA,g,2);Record(lore,g.PlayerA,g,8);
            var icon=CombatStatusIcons.For(g.PlayerA)[0];Assert.AreEqual("2/9",icon.Counter);Assert.True(icon.Description.Contains("2、8"));
        }
        [Test] public void Ai_extra_operation_chooses_only_beneficial_preparation_and_preserves_shields()
        {
            Board(CharacterDefinition.StormSorcery,CharacterDefinition.Blank,out var g);var ai=new GreedyChoiceProvider();var a=g.PlayerA;a.LeftHand.SetValue(1);a.RightHand.SetValue(5);a.RightHand.SetShieldState(ShieldState.Formed);g.PlayerB.LeftHand.SetValue(7);g.PlayerB.RightHand.SetValue(5);
            var source=ai.ChooseNewSubclassHand(NewSubclassChoices.StormSource,a,a.Hands,g);Assert.AreEqual(a.LeftHand,source);
            var target=ai.ChooseNewSubclassHand(NewSubclassChoices.StormAddend,a,g.PlayerB.Hands,g);Assert.True(target!=null);Assert.True(g.ApplyStormFreeOperation(a,source,target));Assert.True(a.RightHand.IsShield);Assert.AreEqual(20,g.PlayerB.Health);
        }
        [Test] public void Ai_preserves_a_record_when_a_single_hit_will_be_shielded()
        {
            Board(CharacterDefinition.Blank,CharacterDefinition.CollegeOfLore,out var g);var a=g.PlayerB;a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);var ai=new GreedyChoiceProvider();
            var options=NewSubclassChoices.YesNo("减伤","Subclasses/bard_college_of_lore");Assert.AreEqual(0,ai.ChooseSkillOption(NewSubclassChoices.LoreDefense,options,a,g.PlayerA,g));
            a.LeftHand.SetValue(1);Assert.AreEqual(1,ai.ChooseSkillOption(NewSubclassChoices.LoreDefense,options,a,g.PlayerA,g));
        }
        [Test] public void Ai_damage_preview_accounts_for_new_defenses_without_consuming_records_or_changing_hands()
        {
            foreach(var role in new[]{CharacterDefinition.CollegeOfLore,CharacterDefinition.SchoolOfIllusion}){
                Board(CharacterDefinition.Blank,role,out var g);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(3);b.LeftHand.SetValue(1);b.RightHand.SetValue(3);
                if(b.Character is CollegeOfLoreLogic lore)Record(lore,b,g,4);b.LeftHand.SetValue(1);
                var ai=new GreedyChoiceProvider();var move=new LegalMove(a,a.LeftHand,b.LeftHand);ai.ScoreMoveForTest(move,a,g);Assert.AreEqual(3,ai.TotalLethalDamage(move,a,b,4));Assert.AreEqual(1,b.LeftHand.Value);Assert.AreEqual(20,b.Health);
                if(b.Character is CollegeOfLoreLogic book)Assert.True(book.HasRecord(4));
            }
        }
        [Test] public void Storm_board_ai_uses_the_real_policy_and_never_leaves_a_human_modal()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var g=CharacterFactory.CreateMatch(CharacterDefinition.StormSorcery,CharacterDefinition.Blank,ca,cb);var board=new BoardController(g,ca,cb,new GreedyChoiceProvider(),new GreedyChoiceProvider());
            g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);board.BeginTurn();Assert.True(board.PendingWizardChoice==null);board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);Assert.True(board.PendingWizardChoice==null);Assert.AreEqual(1,g.TurnCount);
        }
    }
}
