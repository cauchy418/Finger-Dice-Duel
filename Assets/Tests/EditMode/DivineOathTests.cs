using System;
using System.Collections.Generic;
using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class DivineOathTests
    {
        private static GameManager Match(CharacterDefinition a,CharacterDefinition b)=>CharacterFactory.CreateMatch(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
        private static List<WizardChoice> Capture(GameManager g){var list=new List<WizardChoice>();g.DeferWizardChoice=q=>{list.Add(q);return true;};return list;}
        private static AttackContext Hit(GameManager g,PlayerState a,PlayerState b,int damage=4,int hits=1)
        {var attack=new AttackContext(a,b,a.LeftHand,damage,g){HitCount=hits};attack.Resolve(g.GetChoiceProvider(b),g);return attack;}
        private static GameManager Four(CharacterDefinition role)
        {var players=new[]{CharacterFactory.CreatePlayer("A",role),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank)};return GameManager.Create2v2(players,players.Select(p=>(IChoiceProvider)new SimpleChoiceProvider()).ToArray());}
        [Test] public void New_oaths_have_stats_identities_alignments_and_explicit_profiles()
        {
            foreach(var role in new[]{CharacterDefinition.OathOfConquest,CharacterDefinition.OathOfVengeance})
            {var p=CharacterFactory.CreatePlayer("P",role);Assert.AreEqual(0,p.Character.AttackBonus);Assert.AreEqual(0,p.Character.DefenseBonus);Assert.AreEqual(1,GameManager.BaseRangeOf(p));Assert.AreEqual("paladin",role.Profession.Id);Assert.True(HandValueProfiles.For(role).WeightOf(5)>0);}
            Assert.AreEqual(Alignment.LawfulEvil,CharacterCodex.For(CharacterDefinition.OathOfConquest).Alignment);
            Assert.AreEqual(Alignment.LawfulNeutral,CharacterCodex.For(CharacterDefinition.OathOfVengeance).Alignment);
        }
        [Test] public void War_zero_attack_digits_gives_no_prompt_and_9_without_blessing_does_not_count()
        {var g=Match(CharacterDefinition.WarDomain,CharacterDefinition.Blank);g.PlayerA.LeftHand.SetValue(9);g.PlayerA.RightHand.SetValue(1);var qs=Capture(g);g.BeginTurn();Assert.AreEqual(0,qs.Count);}
        [Test] public void War_blessed_nines_count_as_two_attacks_and_second_target_is_optional()
        {
            var g=Four(CharacterDefinition.WarDomain);var a=g.PlayerA;a.LeftHand.SetValue(9);a.RightHand.SetValue(9);a.ApplyStatus(StatusEffect.WarBlessing,1,a);
            var qs=Capture(g);g.BeginTurn();Assert.AreEqual(1,qs.Count);Assert.True(qs[0].Choose(a.LeftHand));Assert.AreEqual(2,qs.Count);
            Assert.False(qs[1].Candidates.Contains(a.LeftHand));Assert.True(qs[1].Choose(null));Assert.AreEqual(2,a.StatusTurnsLeft(StatusEffect.WarBlessing));Assert.AreEqual(1,((WarDomainLogic)a.Character).SkillUseCount);
        }
        [Test] public void War_can_decline_all_blessings_without_prompting_a_second_time()
        {var g=Four(CharacterDefinition.WarDomain);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);var qs=Capture(g);g.BeginTurn();Assert.True(qs[0].Choose(null));Assert.AreEqual(1,qs.Count);Assert.False(g.PlayerA.HasStatus(StatusEffect.WarBlessing));}
        [Test] public void War_blessing_refreshes_without_stacking_and_expires_normally()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerA;p.ApplyStatus(StatusEffect.WarBlessing,2);p.ApplyStatus(StatusEffect.WarBlessing,2);Assert.AreEqual(1,StatusRules.EffectiveAttackBonus(p));Assert.AreEqual(1,StatusRules.EffectiveDefenseBonus(p));g.ResolveStatusTurnEnd(p);Assert.AreEqual(1,p.StatusTurnsLeft(StatusEffect.WarBlessing));g.ResolveStatusTurnEnd(p);Assert.AreEqual(0,StatusRules.EffectiveAttackBonus(p));}
        [Test] public void Blessing_nine_attack_is_a_real_human_choice_and_uses_nine_base_damage()
        {
            var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var a=g.PlayerA;var b=g.PlayerB;a.ApplyStatus(StatusEffect.WarBlessing,2);a.LeftHand.SetValue(8);b.LeftHand.SetValue(1);
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);
            Assert.True(board.PendingWizardChoice!=null);Assert.AreEqual(2,board.PendingSkillOptions.Count);Assert.True(board.ChooseSkillOption(1));Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);
            Assert.True(board.ConfirmTargetPlayer());Assert.AreEqual(9,a.LeftHand.Value);Assert.AreEqual(10,board.LastTurn.DamageDealt);Assert.AreEqual(10,b.Health);Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void Blessing_nine_can_keep_healing_and_does_not_ask_for_an_attack_target()
        {
            var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var a=g.PlayerA;var b=g.PlayerB;a.SetHealthExact(5);a.ApplyStatus(StatusEffect.WarBlessing,2);a.LeftHand.SetValue(8);b.LeftHand.SetValue(1);
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);Assert.True(board.ChooseSkillOption(0));
            Assert.AreEqual(14,a.Health);Assert.AreEqual(20,b.Health);Assert.AreEqual(1,g.TurnCount);Assert.True(board.PendingWizardChoice==null);
        }
        [Test] public void Holy_light_clears_and_prevents_both_song_and_blessing()
        {
            var g=Match(CharacterDefinition.Bladesinging,CharacterDefinition.Blank);var a=g.PlayerA;a.LeftHand.SetValue(2);a.RightHand.SetValue(3);a.SetHealthExact(5);
            var qs=Capture(g);((BladesingingLogic)a.Character).OnTurnEnded(a,g);qs[0].ChooseOption((int)AttackDamageType.Cold);a.ApplyStatus(StatusEffect.WarBlessing,2);
            Assert.True(((IBladesong)a.Character).Singing);a.ApplyStatus(StatusEffect.HolyLight,2);Assert.False(((IBladesong)a.Character).Singing);Assert.False(a.HasStatus(StatusEffect.WarBlessing));
            a.ApplyStatus(StatusEffect.WarBlessing,2);Assert.False(a.HasStatus(StatusEffect.WarBlessing));((BladesingingLogic)a.Character).OnTurnEnded(a,g);Assert.AreEqual(1,qs.Count);
        }
        [Test] public void Life_heal_cleanses_negative_statuses_but_preserves_blessing()
        {var g=Match(CharacterDefinition.LifeDomain,CharacterDefinition.Blank);var b=g.PlayerB;b.ApplyStatus(StatusEffect.WarBlessing,2);b.ApplyStatus(StatusEffect.Poison,2);b.SetHealthExact(5);LifeDomainLogic.CleanseAndHeal(g.PlayerA,b,9,g);Assert.True(b.HasStatus(StatusEffect.WarBlessing));Assert.False(b.HasStatus(StatusEffect.Poison));Assert.AreEqual(14,b.Health);}
        [Test] public void Conquest_grows_after_attack_and_fear_uses_starting_bonuses()
        {
            var g=Match(CharacterDefinition.OathOfConquest,CharacterDefinition.Blank);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(5);
            var first=Hit(g,a,b);Assert.AreEqual(4,first.AppliedDamage);Assert.AreEqual(1,((OathOfConquestLogic)a.Character).AttackStacks);Assert.False(b.HasStatus(StatusEffect.Panic));
            var second=Hit(g,a,b);Assert.AreEqual(5,second.AppliedDamage);Assert.AreEqual(2,((OathOfConquestLogic)a.Character).AttackStacks);Assert.True(b.HasStatus(StatusEffect.Panic));Assert.AreEqual(a,b.StatusSourceOf(StatusEffect.Panic));
        }
        [Test] public void Conquest_has_no_growth_without_five_and_broken_five_still_counts()
        {var g=Match(CharacterDefinition.OathOfConquest,CharacterDefinition.Blank);var a=g.PlayerA;Hit(g,a,g.PlayerB);Assert.AreEqual(0,((OathOfConquestLogic)a.Character).AttackStacks);a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Broken);Hit(g,a,g.PlayerB);Assert.AreEqual(1,((OathOfConquestLogic)a.Character).AttackStacks);}
        [Test] public void Conquest_defense_grows_once_after_whole_multi_hit_even_when_shield_blocks()
        {
            var g=Match(CharacterDefinition.Blank,CharacterDefinition.OathOfConquest);var b=g.PlayerB;b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Formed);
            var hit=Hit(g,g.PlayerA,b,8,2);Assert.AreEqual(1,hit.BlockedHits);Assert.AreEqual(1,((OathOfConquestLogic)b.Character).DefenseStacks);
            Assert.AreEqual(4,hit.AppliedDamage); // 新防御不提前减少第二段。
        }
        [Test] public void Conquest_defensive_fear_compares_defense_not_attack()
        {var g=Match(CharacterDefinition.OathOfConquest,CharacterDefinition.Blank);var a=g.PlayerA;a.LeftHand.SetValue(5);Hit(g,g.PlayerB,a);Assert.False(g.PlayerB.HasStatus(StatusEffect.Panic));Hit(g,g.PlayerB,a);Assert.True(g.PlayerB.HasStatus(StatusEffect.Panic));}
        [Test] public void Conquest_decay_removes_only_one_owned_stack_and_never_negative()
        {
            var g=Match(CharacterDefinition.OathOfConquest,CharacterDefinition.Blank);var a=g.PlayerA;a.LeftHand.SetValue(5);Hit(g,a,g.PlayerB);Hit(g,a,g.PlayerB);Hit(g,g.PlayerB,a);Hit(g,g.PlayerB,a);
            g.ForceActivePlayerForTest(a);g.BeginTurn();g.ResolveStatusTurnEnd(a);var skill=(OathOfConquestLogic)a.Character;Assert.AreEqual(1,skill.AttackStacks);Assert.AreEqual(2,skill.DefenseStacks);
            a.LeftHand.SetValue(4);g.ResolveStatusTurnEnd(a);Assert.AreEqual(0,skill.AttackStacks);Assert.AreEqual(1,skill.DefenseStacks);g.ResolveStatusTurnEnd(a);g.ResolveStatusTurnEnd(a);Assert.AreEqual(0,skill.DefenseStacks);
        }
        [Test] public void Vengeance_initial_target_is_required_and_cannot_be_self()
        {var g=Four(CharacterDefinition.OathOfVengeance);var qs=Capture(g);g.BeginTurn();Assert.False(qs[0].AllowDecline);Assert.False(qs[0].Choose(null));Assert.False(qs[0].Candidates.Contains(g.PlayerA.LeftHand));qs[0].Choose(g.PlayerB.LeftHand);Assert.AreEqual(g.PlayerB,((IVowOfEnmity)g.PlayerA.Character).VowTarget);}
        [Test] public void Vengeance_bonus_applies_to_each_segment_and_outside_target_is_minus_one()
        {var g=Four(CharacterDefinition.OathOfVengeance);var qs=Capture(g);g.BeginTurn();qs[0].Choose(g.PlayerB.LeftHand);var marked=Hit(g,g.PlayerA,g.PlayerB,8,2);Assert.AreEqual(10,marked.AppliedDamage);var other=Hit(g,g.PlayerA,g.PlayerC,8,2);Assert.AreEqual(6,other.AppliedDamage);}
        [Test] public void Vengeance_human_can_transfer_before_incoming_attack()
        {
            var g=Four(CharacterDefinition.OathOfVengeance);var qs=Capture(g);g.BeginTurn();qs[0].Choose(g.PlayerC.LeftHand);
            var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(3);b.LeftHand.SetValue(1);g.ForceActivePlayerForTest(b);
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());board.BeginTurn();board.ClickHand(b.LeftHand);board.ClickHand(a.LeftHand);board.ConfirmTargetPlayer();
            Assert.AreEqual(HandEffect.VengeanceRetarget,board.PendingEffect);Assert.True(board.ChooseSkillOption(1));Assert.AreEqual(b,((IVowOfEnmity)a.Character).VowTarget);
        }
        [Test] public void Vengeance_prepared_transfer_does_not_apply_when_attack_is_cancelled()
        {
            var players=new[]{CharacterFactory.CreatePlayer("A",CharacterDefinition.OathOfVengeance),CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("D",CharacterDefinition.TheArchfey)};
            var g=GameManager.Create2v2(players,players.Select(p=>(IChoiceProvider)new SimpleChoiceProvider()).ToArray());
            var qs=Capture(g);g.BeginTurn();qs[0].Choose(players[2].LeftHand);
            var skill=(OathOfVengeanceLogic)players[0].Character;players[1].LeftHand.SetValue(1);players[0].LeftHand.SetValue(3);players[3].LeftHand.SetValue(4);
            var context=new PreTurnChoiceContext(players[1],players[0],players[1].LeftHand,players[0].LeftHand){Manager=g};
            var requests=new List<ChoiceRequest>();skill.CollectPreTurnChoices(context,requests);Assert.AreEqual(1,requests.Count);
            Assert.True(requests[0].ChooseOption(1));Assert.AreEqual(players[2],skill.VowTarget,"预选不应提前转移");
            var attack=Hit(g,players[1],players[0]);Assert.True(attack.WasCancelled);Assert.AreEqual(players[2],skill.VowTarget,"取消的攻击不转移仇敌");
        }
        [Test] public void Vengeance_marked_enemy_break_gives_optional_pure_operation_without_turn_advance()
        {
            var g=Match(CharacterDefinition.OathOfVengeance,CharacterDefinition.Blank);var a=g.PlayerA;var b=g.PlayerB;g.BeginTurn();Assert.AreEqual(b,((IVowOfEnmity)a.Character).VowTarget);
            a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);b.RightHand.SetValue(3);var qs=Capture(g);Hit(g,b,a);
            Assert.AreEqual(1,qs.Count);Assert.False(qs[0].TargetsPlayer);Assert.True(qs[0].AllowDecline);qs[0].Choose(a.LeftHand);Assert.AreEqual(2,qs.Count);qs[1].Choose(b.RightHand);
            Assert.AreEqual(8,a.LeftHand.Value);Assert.AreEqual(20,b.Health);Assert.AreEqual(0,g.TurnCount);Assert.AreEqual(20,a.Health);
        }
        [Test] public void Vengeance_every_broken_shield_counts_and_other_breakers_do_not_trigger()
        {
            var g=Four(CharacterDefinition.OathOfVengeance);var a=g.PlayerA;var qs=Capture(g);g.BeginTurn();qs[0].Choose(g.PlayerB.LeftHand);qs.Clear();
            foreach(var h in a.Hands){h.SetValue(5);h.SetShieldState(ShieldState.Formed);}g.BreakShieldAndNotify(a,a.LeftHand,g.PlayerB);g.BreakShieldAndNotify(a,a.RightHand,g.PlayerB);Assert.AreEqual(2,qs.Count);
            foreach(var q in qs.ToArray())q.Choose(null);qs.Clear();a.LeftHand.SetShieldState(ShieldState.Formed);g.BeginTurn();g.BreakShieldAndNotify(a,a.LeftHand,g.PlayerC);Assert.AreEqual(0,qs.Count);
        }
        [Test] public void Vengeance_dead_mark_offers_optional_new_living_target()
        {var g=Four(CharacterDefinition.OathOfVengeance);var qs=Capture(g);g.BeginTurn();qs[0].Choose(g.PlayerB.LeftHand);qs.Clear();g.PlayerB.SetHealthExact(0);g.RefreshGameOver();Assert.AreEqual(1,qs.Count);Assert.True(qs[0].AllowDecline);Assert.False(qs[0].Candidates.Contains(g.PlayerB.LeftHand));qs[0].Choose(g.PlayerD.LeftHand);Assert.AreEqual(g.PlayerD,((IVowOfEnmity)g.PlayerA.Character).VowTarget);}
        [Test] public void State_guide_separates_three_character_states_and_marker_page_has_no_song_duplicate()
        {var sheet=GuideSheets.Statuses();Assert.AreEqual(2,sheet.Groups.Count);Assert.AreEqual(21,sheet.Groups[0].Entries.Count);Assert.AreEqual(4,sheet.Groups[1].Entries.Count);Assert.True(sheet.Groups[1].Entries.Any(e=>e.ResourceKey=="Effects/bladesong"));Assert.False(GuideSheets.CharacterMarks().Lines.Any(line=>line.StartsWith("剑歌：")));}
        [Test] public void New_oath_states_and_vow_are_present_in_combat_icon_list()
        {var g=Match(CharacterDefinition.OathOfVengeance,CharacterDefinition.Blank);g.BeginTurn();g.PlayerA.ApplyStatus(StatusEffect.WarBlessing,2);Assert.True(CombatStatusIcons.For(g.PlayerA,g).Any(i=>i.ResourceKey=="Status/war_blessing"));Assert.True(CombatStatusIcons.For(g.PlayerB,g).Any(i=>i.ResourceKey=="Effects/vow_of_enmity"));}
        [Test] public void Ai_blesses_known_teammates_and_marks_enemies()
        {
            foreach(var role in new[]{CharacterDefinition.WarDomain,CharacterDefinition.OathOfVengeance})
            {var g=Four(role);var ai=new GreedyChoiceProvider();var target=ai.ChooseArcaneTarget(role==CharacterDefinition.WarDomain?DivineChoices.Blessing:DivineChoices.Vow,g.PlayerA,g.Players,g);Assert.True(target!=null);Assert.AreEqual(role==CharacterDefinition.WarDomain,g.IsKnownAlly(g.PlayerA,target));}
        }
        [Test] public void War_and_oath_hooks_work_in_either_dual_position()
        {
            foreach(bool reverse in new[]{false,true})
            {var war=new WarDomainLogic();var oath=new OathOfConquestLogic();var a=new PlayerState("A",new DualGeneralBehavior(reverse?(ICharacterBehavior)war:oath,reverse?(ICharacterBehavior)oath:war));var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.LeftHand.SetValue(5);a.RightHand.SetValue(4);var qs=Capture(g);g.BeginTurn();Assert.AreEqual(1,qs.Count);qs[0].Choose(a.LeftHand);Hit(g,a,b);Assert.AreEqual(1,oath.AttackStacks);Assert.AreEqual(1,war.SkillUseCount);}
        }
        [Test] public void Blessing_nine_gamble_asks_human_before_selecting_attack_target()
        {
            var g=Match(CharacterDefinition.Gambler,CharacterDefinition.Blank);var a=g.PlayerA;var b=g.PlayerB;a.ApplyStatus(StatusEffect.WarBlessing,2);
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            for(int seed=0;seed<200;seed++)if(GamblerLogic.Roll(new Random(seed))==9){board.GambleRng=new Random(seed);break;}
            board.BeginTurn();Assert.True(board.ClickHand(a.LeftHand));Assert.True(board.Gamble(),"gamble entry");Assert.True(board.PendingWizardChoice!=null,"gamble nine prompt roll="+board.LastGambleRoll);Assert.True(board.ChooseSkillOption(1),"gamble nine choose");
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);Assert.True(board.ClickHand(b.LeftHand),"confirm "+board.Phase+" "+board.Hint);Assert.AreEqual(9,a.LeftHand.Value);Assert.True(board.LastTurn.DamageDealt>=9,"gamble damage="+board.LastTurn.DamageDealt);
        }
        [Test] public void Blessing_nine_effective_theft_asks_human_and_defers_attack_normally()
        {
            var g=Match(CharacterDefinition.Thief,CharacterDefinition.Blank);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(9);b.LeftHand.SetValue(9);a.ApplyStatus(StatusEffect.WarBlessing,2);
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());board.BeginTurn();board.ClickHand(a.LeftHand);Assert.True(board.BeginSteal(),"steal entry");Assert.True(board.ClickHand(b.LeftHand),"steal target");
            Assert.True(board.PendingWizardChoice!=null,"steal nine prompt phase="+board.Phase);Assert.True(board.ChooseSkillOption(1),"steal choose");Assert.True(g.StealAttackAwaitingTarget,"steal pending phase="+board.Phase);Assert.AreEqual(20,b.Health);Assert.True(board.ClickHand(b.LeftHand),"confirm "+board.Phase+" "+board.Hint);Assert.True(b.Health<20,"steal hit hp="+b.Health);
        }
        [Test] public void Ai_blessed_nine_prefers_emergency_heal_and_attacks_at_full_health()
        {
            var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var a=g.PlayerA;var ai=new GreedyChoiceProvider();a.ApplyStatus(StatusEffect.WarBlessing,2);a.SetHealthExact(2);
            Assert.AreEqual(0,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,g.PlayerB,g));a.SetHealthExact(20);Assert.AreEqual(1,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,g.PlayerB,g));
        }
        [Test] public void Ai_vow_retargets_from_distant_enemy_but_never_to_known_ally()
        {
            var g=Four(CharacterDefinition.OathOfVengeance);var qs=Capture(g);g.BeginTurn();var owner=g.PlayerA;
            var enemies=g.Players.Where(p=>!g.IsKnownAlly(owner,p)).ToList();qs[0].Choose(enemies[0].LeftHand);
            var options=NewSubclassChoices.YesNo("转移","Effects/vow_of_enmity","保留");var ai=new GreedyChoiceProvider();var ally=g.Players.First(p=>p!=owner&&g.IsKnownAlly(owner,p));
            Assert.AreEqual(0,ai.ChooseSkillOption(DivineChoices.Retarget,options,owner,ally,g));
        }
        [Test] public void New_roles_advance_through_real_ai_controllers_in_all_battle_sizes()
        {
            foreach(var role in new[]{CharacterDefinition.WarDomain,CharacterDefinition.OathOfConquest,CharacterDefinition.OathOfVengeance})
            foreach(int size in new[]{2,4,8})for(int seed=0;seed<3;seed++)
            {
                var players=Enumerable.Range(0,size).Select(i=>CharacterFactory.CreatePlayer("P"+i,i==0?role:i%3==0?CharacterDefinition.OathOfVengeance:i%3==1?CharacterDefinition.WarDomain:CharacterDefinition.OathOfConquest)).ToArray();
                var sinks=players.Select(p=>new SimpleChoiceProvider()).ToArray();var providers=sinks.Cast<IChoiceProvider>().ToArray();
                var g=size==2?new GameManager(players[0],players[1],providers[0],providers[1]):size==4?GameManager.Create2v2(players,providers):GameManager.CreateEight(players,providers,Enumerable.Range(0,size).Select(i=>i%2).ToArray());
                var ais=players.ToDictionary(p=>p,p=>new GreedyChoiceProvider{Game=g,UseLearnedFocus=true,UseValidatedTuning=true});
                var board=new BoardController(g,sinks[0],sinks[1]);foreach(var p in players){ais[p].ApplyDifficulty(AiDifficulty.Hard);board.BindPlayer(p,ais[p],sinks[Array.IndexOf(players,p)]);}
                for(int i=0;i<size;i++){players[i].LeftHand.SetValue(1+(i+seed)%9);players[i].RightHand.SetValue(1+(i+seed*2+3)%9);}
                board.BeginTurn();int steps=0;
                while(!g.IsGameOver&&steps++<5000)
                {
                    bool ok=board.Phase==BoardPhase.ChoosingAttackerHand||board.Phase==BoardPhase.ChoosingGamble?board.MakeAutomaticMove():board.ResolvePendingAiChoice();
                    Assert.True(ok||g.IsGameOver,"AI卡住 "+role.Id+"/"+size+"/"+board.Phase+" "+board.Hint);
                }
                Assert.True(g.IsGameOver,"应在正常规则限制内结束 "+role.Id+"/"+size);
            }
        }
        [Test] public void Blessed_nine_attack_in_life_domain_dual_does_not_offer_heal_twice()
        {
            var a=new PlayerState("A",new DualGeneralBehavior(new LifeDomainLogic(),new OathOfConquestLogic()));var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var g=new GameManager(a,b,ca,cb);a.ApplyStatus(StatusEffect.WarBlessing,2);a.LeftHand.SetValue(8);a.RightHand.SetValue(1);a.SetHealthExact(10);b.LeftHand.SetValue(1);
            var board=new BoardController(g,ca,cb);board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);Assert.True(board.ChooseSkillOption(1));Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);
            Assert.True(board.ClickHand(b.LeftHand));Assert.AreEqual(10,a.Health);Assert.AreEqual(11,b.Health);Assert.True(board.PendingWizardChoice==null);Assert.False(board.PendingRequest.HasValue);
        }
        [Test] public void Conquest_zero_damage_counts_but_cancel_follows_the_common_rule()
        {
            var disabled=Match(CharacterDefinition.OathOfConquest,CharacterDefinition.Blank);disabled.PlayerA.LeftHand.SetValue(5);disabled.PlayerA.ApplyStatus(StatusEffect.Disabled,2);
            var zero=Hit(disabled,disabled.PlayerA,disabled.PlayerB);Assert.AreEqual(0,zero.AppliedDamage);Assert.AreEqual(1,((OathOfConquestLogic)disabled.PlayerA.Character).AttackStacks);
            var g=Match(CharacterDefinition.OathOfConquest,CharacterDefinition.TheArchfey);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(5);Hit(g,a,b);
            Assert.AreEqual(1,((OathOfConquestLogic)a.Character).AttackStacks);g.BeginTurn();b.LeftHand.SetValue(4);
            ((SimpleChoiceProvider)g.GetChoiceProvider(b)).SetChoice(HandEffect.TheArchfeyCancel,b.LeftHand);
            var cancelled=Hit(g,a,b);Assert.True(cancelled.WasCancelled);Assert.AreEqual(1,((OathOfConquestLogic)a.Character).AttackStacks);Assert.False(b.HasStatus(StatusEffect.Panic));
            g.ResolveStatusTurnEnd(a);Assert.AreEqual(0,((OathOfConquestLogic)a.Character).AttackStacks);
        }
    }
}
