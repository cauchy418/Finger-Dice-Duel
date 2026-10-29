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
            Assert.AreEqual(Alignment.ChaoticEvil,CharacterCodex.For(CharacterDefinition.SchoolOfIllusion).Alignment);
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
            board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);Assert.True(board.Hint.Contains("风暴导向"));Assert.AreEqual(g.PlayerA,board.Chooser);Assert.AreEqual(1,g.TurnCount);
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
        private sealed class MirrorPolicy:AbstractChoiceProvider,IIllusionChoicePolicy
        {
            public bool Use;public int Asked;
            public bool RedirectIllusion(IllusionTouch touch,GameManager game){Asked++;return Use;}
        }
        [Test] public void Illusion_choice_is_cached_and_repeated_reads_do_not_ask_again()
        {
            var policy=new MirrorPolicy{Use=true};var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.SchoolOfIllusion);var g=new GameManager(a,b,new SimpleChoiceProvider(),policy);
            b.LeftHand.SetValue(2);b.RightHand.SetValue(7);
            var first=g.PrepareOperationAddend(a,a.LeftHand,b.LeftHand);Assert.AreEqual(b.RightHand,first);Assert.AreEqual(1,policy.Asked);
            policy.Use=false;Assert.AreEqual(first,g.PrepareOperationAddend(a,a.LeftHand,first));Assert.AreEqual(1,policy.Asked);
            g.ClearPreparedOperationAddend();Assert.AreEqual(b.LeftHand,g.PrepareOperationAddend(a,a.LeftHand,b.LeftHand));Assert.AreEqual(2,policy.Asked);
        }
        [Test] public void Illusion_human_can_keep_or_replace_either_selected_hand_before_operation()
        {
            foreach(bool right in new[]{false,true})foreach(bool use in new[]{false,true})
            {
                var board=Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);
                var a=g.PlayerA;var b=g.PlayerB;b.LeftHand.SetValue(2);b.RightHand.SetValue(7);
                var selected=right?b.RightHand:b.LeftHand;board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(selected);
                Assert.AreEqual(b,board.Chooser);Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(2,board.PendingSkillOptions.Count);
                Assert.True(board.Hint.Contains(CombatText.HandName(a,a.LeftHand)+"1"));Assert.True(board.Hint.Contains(CombatText.HandName(b,selected)+selected.Value));
                Assert.AreEqual(1,a.LeftHand.Value);Assert.AreEqual(0,g.TurnCount);
                Assert.True(board.ChooseSkillOption(use?1:0));
                bool attack=right!=use;
                if(attack){Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);Assert.True(board.ClickHand(b.LeftHand));}
                Assert.AreEqual(attack?8:3,a.LeftHand.Value);Assert.AreEqual(attack?11:20,b.Health);Assert.AreEqual(1,g.TurnCount);
                Assert.AreEqual(2,b.LeftHand.Value);Assert.AreEqual(7,b.RightHand.Value);
            }
        }
        [Test] public void Illusion_respects_alternate_hand_legality_and_previews_never_prompt()
        {
            var board=Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);
            g.PlayerB.LeftHand.SetValue(2);g.PlayerB.RightHand.SetValue(7);g.PlayerB.RightHand.SetTargetRestricted(true);
            g.GetLegalMoves(g.PlayerA);g.PreviewOperationResult(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);
            Assert.True(board.PendingWizardChoice==null);board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
            Assert.True(board.PendingWizardChoice==null);Assert.AreEqual(3,g.PlayerA.LeftHand.Value);Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void Illusion_without_two_and_sheep_do_not_offer_redirect()
        {
            foreach(bool sheep in new[]{false,true})
            {
                var board=Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);
                g.PlayerB.LeftHand.SetValue(sheep?2:3);g.PlayerB.RightHand.SetValue(7);
                if(sheep)g.PlayerB.TryPolymorph();board.BeginTurn();board.ClickHand(g.PlayerA.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
                Assert.True(board.PendingWizardChoice==null);
            }
        }
        [Test] public void Illusion_does_not_redirect_own_self_addition_and_reset_invalidates_queued_answer()
        {
            var a=DualGeneralFactory.Create("A",CharacterDefinition.Magician,CharacterDefinition.SchoolOfIllusion);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            a.LeftHand.SetValue(2);a.RightHand.SetValue(7);WizardChoice pending=null;g.DeferWizardChoice=q=>{pending=q;return true;};
            Hand actual=null;Assert.False(g.RequestOperationAddend(a,a.LeftHand,a.RightHand,h=>actual=h));Assert.AreEqual(a.RightHand,actual);Assert.True(pending==null);
            Assert.True(g.RequestOperationAddend(b,b.LeftHand,a.RightHand,h=>actual=h));g.ResetMatch();actual=null;
            pending.ChooseOption(1);Assert.True(actual==null,"重开后的过期选择不能执行旧运算");
        }
        [Test] public void Illusion_ai_prefers_weaker_enemy_result_and_stronger_allied_result()
        {
            var ps=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.SchoolOfIllusion)};
            var g=GameManager.Create2v2(ps,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});
            ps[3].LeftHand.SetValue(2);ps[3].RightHand.SetValue(7);var ai=new GreedyChoiceProvider();
            Assert.True(ai.RedirectIllusion(new IllusionTouch(ps[3],ps[0],ps[0].LeftHand,ps[3].LeftHand,ps[3].RightHand,1,false),g));
            Assert.False(ai.RedirectIllusion(new IllusionTouch(ps[3],ps[1],ps[1].LeftHand,ps[3].LeftHand,ps[3].RightHand,1,false),g));
            Assert.True(ai.RedirectIllusion(new IllusionTouch(ps[3],ps[1],ps[1].LeftHand,ps[3].LeftHand,ps[3].RightHand,6,false),g));
        }
        [Test] public void Illusion_second_addend_has_separate_decision_and_uses_intermediate_source_value()
        {
            var board=Board(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,out var g);var a=g.PlayerA;var b=g.PlayerB;
            b.LeftHand.SetValue(2);b.RightHand.SetValue(7);var qs=new List<WizardChoice>();g.DeferWizardChoice=q=>{qs.Add(q);return true;};
            Hand first=null,second=null;g.RequestOperationAddend(a,a.LeftHand,b.RightHand,h=>first=h);qs[0].ChooseOption(0);
            g.RequestOperationAddend(a,a.LeftHand,b.RightHand,h=>second=h,step:1,sourceValue:8);Assert.AreEqual(2,qs.Count);
            Assert.True(qs[1].Prompt.Contains("左手8"));qs[1].ChooseOption(1);
            Assert.AreEqual(b.RightHand,first);Assert.AreEqual(b.LeftHand,second);Assert.AreEqual(1,a.LeftHand.Value);
            Assert.AreEqual(first,g.PrepareOperationAddend(a,a.LeftHand,first));Assert.AreEqual(second,g.PrepareOperationAddend(a,a.LeftHand,second,step:1));
        }
        [Test] public void Illusion_storm_extra_operation_asks_owner_without_extra_effects_or_turn()
        {
            var board=Board(CharacterDefinition.StormSorcery,CharacterDefinition.SchoolOfIllusion,out var g);var a=g.PlayerA;var b=g.PlayerB;
            board.BeginTurn();board.ChooseSkillOption((int)AttackDamageType.Thunder);board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            b.LeftHand.SetValue(2);b.RightHand.SetValue(7);Assert.True(board.ChooseWizardHand(a.LeftHand));Assert.True(board.ChooseWizardHand(b.RightHand));
            Assert.AreEqual(b,board.Chooser);Assert.True(board.Hint.Contains("幻术默发"));Assert.AreEqual(2,a.LeftHand.Value);
            Assert.True(board.ChooseSkillOption(1));Assert.AreEqual(4,a.LeftHand.Value);Assert.AreEqual(20,b.Health);Assert.AreEqual(1,g.TurnCount);
        }
        private sealed class FixedTouch:IMoveSelector
        {
            public LegalMove Move;public string Describe=>"固定触碰";
            public LegalMove ChooseMove(GameManager game,PlayerState actor)=>Move;
        }
        [Test] public void Illusion_ai_actor_waits_for_the_human_addend_owner()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.SchoolOfIllusion,ca,cb);
            g.PlayerB.LeftHand.SetValue(2);g.PlayerB.RightHand.SetValue(7);
            var ai=new FixedTouch{Move=new LegalMove(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand)};
            var board=new BoardController(g,ca,cb,ai,null);board.BeginTurn();Assert.True(board.MakeAutomaticMove());
            Assert.AreEqual(g.PlayerB,board.Chooser);Assert.True(board.IsHumanChooser);Assert.False(board.ResolvePendingAiChoice());
            Assert.AreEqual(1,g.PlayerA.LeftHand.Value);Assert.True(board.ChooseSkillOption(0));
            Assert.AreEqual(3,g.PlayerA.LeftHand.Value);Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void Illusion_targets_the_correct_human_owner_in_four_and_eight_player_matches()
        {
            foreach(int size in new[]{4,8})
            {
                var players=new PlayerState[size];var sinks=new SimpleChoiceProvider[size];
                for(int i=0;i<size;i++){players[i]=CharacterFactory.CreatePlayer("P"+i,i==size-1?CharacterDefinition.SchoolOfIllusion:CharacterDefinition.Blank);sinks[i]=new SimpleChoiceProvider();}
                var g=size==4?GameManager.Create2v2(players,sinks):GameManager.CreateEight(players,sinks,new[]{0,1,0,1,0,1,0,1});
                var board=new BoardController(g,sinks[0],sinks[1]);for(int i=0;i<size;i++)board.BindPlayer(players[i],null,sinks[i]);
                var owner=players[size-1];owner.LeftHand.SetValue(2);owner.RightHand.SetValue(7);
                board.BeginTurn();board.ClickHand(players[0].LeftHand);board.ClickHand(owner.RightHand);
                Assert.AreEqual(owner,board.Chooser);Assert.True(board.Hint.Contains("P0"));Assert.True(board.ChooseSkillOption(1));
                Assert.AreEqual(3,players[0].LeftHand.Value);Assert.AreEqual(1,g.TurnCount);
            }
        }
        [Test] public void Illusion_dragon_asks_each_addend_once_and_applies_only_the_final_result()
        {
            var board=Board(CharacterDefinition.CircleOfTheStars,CharacterDefinition.SchoolOfIllusion,out var g);
            var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(4);a.RightHand.SetValue(6);b.LeftHand.SetValue(2);b.RightHand.SetValue(7);
            Assert.True(((CircleOfTheStarsLogic)a.Character).TryEnter(CircleOfTheStarsLogic.StarryForm.Dragon,a,g));
            board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.RightHand);Assert.AreEqual(b,board.Chooser);
            Assert.True(board.ChooseSkillOption(1));Assert.AreEqual(a,board.Chooser);Assert.True(board.Hint.Contains("第二个加数"));
            Assert.True(board.ChooseWizardHand(b.RightHand));Assert.AreEqual(b,board.Chooser);Assert.True(board.Hint.Contains("左手6"));
            Assert.AreEqual(4,a.LeftHand.Value);Assert.True(board.ChooseSkillOption(0));
            Assert.AreEqual(3,a.LeftHand.Value);Assert.AreEqual(7,board.LastTurn.SecondAddendValue);Assert.AreEqual(20,b.Health);Assert.AreEqual(1,g.TurnCount);
            Assert.True(board.PendingWizardChoice==null);
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
