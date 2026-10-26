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
    [TestFixture] public class ArcaneFiveTests
    {
        private static GameManager Match(CharacterDefinition a,CharacterDefinition b)=>CharacterFactory.CreateMatch(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
        private static AttackContext Hit(GameManager g,PlayerState a,PlayerState b,int raw=4,int hits=1,AttackDamageType type=AttackDamageType.None)
        {var c=new AttackContext(a,b,a.LeftHand,raw,g){HitCount=hits,AttackType=type};c.Resolve(g.GetChoiceProvider(b),g);return c;}
        private static List<WizardChoice> Capture(GameManager game)
        {var list=new List<WizardChoice>();game.DeferWizardChoice=c=>{list.Add(c);return true;};return list;}
        private static GameManager Four(CharacterDefinition first,out PlayerState a,out PlayerState b,out PlayerState c,out PlayerState d)
        {
            a=CharacterFactory.CreatePlayer("A",first);b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank);d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank);
            return GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});
        }
        private static HexbladeLogic Curse(GameManager game,PlayerState owner,PlayerState target)
        {var list=Capture(game);owner.LeftHand.SetValue(7);game.ForceActivePlayerForTest(owner);var hex=(HexbladeLogic)owner.Character;hex.OnTurnStarted(owner,game);Assert.AreEqual(1,list.Count);list[0].Choose(target.LeftHand);return hex;}
        private static BoardController SongBoard(out GameManager game,out BladesingingLogic song)
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            game=CharacterFactory.CreateMatch(CharacterDefinition.Bladesinging,CharacterDefinition.Blank,ca,cb);
            game.PlayerA.SetHealthExact(10);game.PlayerA.LeftHand.SetValue(4);game.PlayerA.RightHand.SetValue(6);
            var choices=Capture(game);game.ResolveStatusTurnEnd(game.PlayerA);choices[0].ChooseOption((int)AttackDamageType.Fire);
            song=(BladesingingLogic)game.PlayerA.Character;game.DeferWizardChoice=null;
            return new BoardController(game,ca,cb);
        }
        [Test] public void Real_board_actual_heal_keeps_bladesong()
        {
            var board=SongBoard(out var game,out var song);game.PlayerA.LeftHand.SetValue(8);game.PlayerB.LeftHand.SetValue(1);
            board.BeginTurn();Assert.True(board.ClickHand(game.PlayerA.LeftHand));Assert.True(board.ClickHand(game.PlayerB.LeftHand));
            Assert.AreEqual(19,game.PlayerA.Health);Assert.True(song.Singing);Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Real_board_full_health_or_decay_prevented_heal_does_not_keep_bladesong()
        {
            foreach(bool decay in new[]{false,true})
            {
                var board=SongBoard(out var game,out var song);if(decay)game.PlayerA.ApplyStatus(StatusEffect.Decay,3);else game.PlayerA.SetHealthExact(20);
                game.PlayerA.LeftHand.SetValue(8);game.PlayerB.LeftHand.SetValue(1);board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
                Assert.AreEqual(decay?10:20,game.PlayerA.Health);Assert.False(song.Singing);
            }
        }
        [Test] public void Real_board_attack_and_shield_each_keep_bladesong()
        {
            foreach(int value in new[]{3,4})
            {
                var board=SongBoard(out var game,out var song);game.PlayerA.LeftHand.SetValue(value);game.PlayerB.LeftHand.SetValue(1);
                board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
                if(board.Phase==BoardPhase.ChoosingTargetPlayer)Assert.True(board.ConfirmTargetPlayer());
                Assert.AreEqual(1,game.TurnCount);Assert.True(song.Singing);
            }
        }
        [Test] public void Real_board_knight_active_shield_enchants_this_attack_and_prompts_once()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=CharacterFactory.CreateMatch(CharacterDefinition.EldritchKnight,CharacterDefinition.Blank,ca,cb);
            var board=new BoardController(game,ca,cb);game.PlayerA.LeftHand.SetValue(5);game.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);
            game.PlayerB.LeftHand.SetValue(1);board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            if(board.Phase==BoardPhase.ChoosingTargetPlayer)Assert.True(board.ConfirmTargetPlayer());
            Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(0,game.TurnCount);Assert.True(game.PlayerA.LeftHand.IsShield);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.True(board.ChooseSkillOption((int)AttackDamageType.Fire));Assert.AreEqual(1,game.TurnCount);Assert.AreEqual(6,game.PlayerA.LeftHand.Value);
            Assert.True(game.PlayerB.HasStatus(StatusEffect.Burn));Assert.AreEqual(AttackDamageType.Fire,board.LastTurn.AttackType);
            Assert.AreEqual(AttackDamageType.None,((EldritchKnightLogic)game.PlayerA.Character).Enchantment);Assert.True(board.PendingWizardChoice==null);
        }
        [Test] public void Real_board_knight_nonattack_shield_operation_keeps_enchantment_for_later()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=CharacterFactory.CreateMatch(CharacterDefinition.EldritchKnight,CharacterDefinition.Blank,ca,cb);
            var board=new BoardController(game,ca,cb);game.PlayerA.LeftHand.SetValue(5);game.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);game.PlayerB.LeftHand.SetValue(2);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);
            board.ChooseSkillOption((int)AttackDamageType.Cold);Assert.AreEqual(7,game.PlayerA.LeftHand.Value);Assert.AreEqual(1,game.TurnCount);
            Assert.AreEqual(AttackDamageType.Cold,((EldritchKnightLogic)game.PlayerA.Character).Enchantment);Assert.AreEqual(20,game.PlayerB.Health);
        }
        [Test] public void Knight_ai_also_uses_active_shield_enchantment_in_current_attack()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var game=CharacterFactory.CreateMatch(CharacterDefinition.EldritchKnight,CharacterDefinition.Blank,ca,cb);
            var board=new BoardController(game,ca,cb,new GreedyChoiceProvider(),new GreedyChoiceProvider());game.PlayerA.LeftHand.SetValue(5);game.PlayerA.LeftHand.SetShieldState(ShieldState.Formed);game.PlayerB.LeftHand.SetValue(1);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);if(board.Phase==BoardPhase.ChoosingTargetPlayer)board.ConfirmTargetPlayer();
            while(game.TurnCount==0&&board.ResolvePendingAiChoice()){}
            Assert.AreEqual(1,game.TurnCount);Assert.True(AttackDamageTypes.IsMagic(board.LastTurn.AttackType));Assert.AreEqual(AttackDamageType.None,((EldritchKnightLogic)game.PlayerA.Character).Enchantment);
        }
        [Test] public void Projected_counterplay_is_readonly_and_uses_future_results()
        {
            var game=Match(CharacterDefinition.Champion,CharacterDefinition.Champion);var ai=new GreedyChoiceProvider{DenyRatioPercent=100};
            game.PlayerA.LeftHand.SetValue(1);game.PlayerA.RightHand.SetValue(1);game.PlayerB.LeftHand.SetValue(1);game.PlayerB.RightHand.SetValue(3);game.PlayerB.RightHand.Lock();
            int before=game.PlayerA.LeftHand.Value;
            int a=ai.ProjectedCounterplayScoreForTest(new LegalMove(game.PlayerA,game.PlayerA.LeftHand,game.PlayerB.LeftHand),game.PlayerA,game);
            int b=ai.ProjectedCounterplayScoreForTest(new LegalMove(game.PlayerA,game.PlayerA.LeftHand,game.PlayerB.RightHand),game.PlayerA,game);
            Assert.True(a!=b,"different future digits must change the enemy opportunity score");Assert.AreEqual(before,game.PlayerA.LeftHand.Value);
            game.PlayerB.ApplyStatus(StatusEffect.Frozen,2);Assert.AreEqual(0,ai.ProjectedCounterplayScoreForTest(new LegalMove(game.PlayerA,game.PlayerA.LeftHand,game.PlayerB.RightHand),game.PlayerA,game));
        }
        [Test] public void Validated_tuning_is_mode_specific_and_never_leaks_into_multiplayer()
        {
            var game=Match(CharacterDefinition.Champion,CharacterDefinition.Blank);var ai=new GreedyChoiceProvider{UseValidatedTuning=true};
            ai.ChooseMove(game,game.PlayerA);Assert.True(ai.UseProjectedCounterplay);Assert.AreEqual(100,ai.DenyRatioPercent);
            var four=Four(CharacterDefinition.Champion,out var a,out var b,out var c,out var d);ai.ChooseMove(four,a);Assert.False(ai.UseProjectedCounterplay);Assert.AreEqual(43,ai.DenyRatioPercent);Assert.AreEqual(12,ai.HandValueWeight);
        }
        [Test] public void Immune_poison_does_not_misattribute_bleeding_damage_to_magic()
        {
            var game=Match(CharacterDefinition.SchoolOfEnchantment,CharacterDefinition.EldritchKnight);var choices=Capture(game);
            game.PlayerB.AddPermanentImmunity(AttackDamageType.Poison);game.PlayerB.ApplyStatus(StatusEffect.Poison,2,game.PlayerA);game.PlayerB.ApplyStatus(StatusEffect.Bleed,2,game.PlayerA);
            game.ResolveStatusTurnStart(game.PlayerB);Assert.AreEqual(19,game.PlayerB.Health);Assert.AreEqual(1,choices.Count);Assert.AreEqual(game.PlayerA,choices[0].Owner);
            Assert.AreEqual(AttackDamageType.None,((EldritchKnightLogic)game.PlayerB.Character).Enchantment);
        }
        [Test] public void New_five_roles_registered_profiled_and_coded()
        {
            Assert.AreEqual(52,CharacterDefinition.BuiltIn.Length);
            foreach(var def in new[]{CharacterDefinition.SchoolOfTransmutation,CharacterDefinition.SchoolOfEnchantment,CharacterDefinition.EldritchKnight,CharacterDefinition.Bladesinging,CharacterDefinition.TheHexblade})
            {Assert.True(CharacterFactory.IsRegistered(def.Logic));Assert.True(HandValueProfiles.HasExplicitProfile(def));Assert.NotNull(CharacterCodex.For(def));Assert.AreEqual(def,CharacterDefinition.Find(def.Id));}
        }
        [Test] public void Matching_is_unordered_not_just_equal_sum()
        {var g=Match(CharacterDefinition.SchoolOfTransmutation,CharacterDefinition.Blank);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);g.PlayerB.LeftHand.SetValue(6);g.PlayerB.RightHand.SetValue(4);Assert.True(ArcaneChoices.MatchingHands(g.PlayerA,g.PlayerB));g.PlayerB.LeftHand.SetValue(5);g.PlayerB.RightHand.SetValue(5);Assert.False(ArcaneChoices.MatchingHands(g.PlayerA,g.PlayerB));}
        [Test] public void Transmutation_offers_self_and_matching_players_and_decline()
        {var g=Match(CharacterDefinition.SchoolOfTransmutation,CharacterDefinition.Blank);var list=Capture(g);((SchoolOfTransmutationLogic)g.PlayerA.Character).OnTurnEnded(g.PlayerA,g);Assert.AreEqual(2,list[0].Candidates.Count);Assert.True(list[0].TargetsPlayer);list[0].Choose(null);Assert.False(g.PlayerA.IsPolymorphed);((SchoolOfTransmutationLogic)g.PlayerA.Character).OnTurnEnded(g.PlayerA,g);list[1].Choose(g.PlayerA.LeftHand);Assert.True(g.PlayerA.IsPolymorphed);}
        [Test] public void Sheep_has_independent_health_exact_stats_range_and_no_original_capabilities()
        {var g=Match(CharacterDefinition.PathOfTheGiant,CharacterDefinition.Blank);var p=g.PlayerA;p.SetHealthExact(18);p.AddIdentityBonus(5,5);p.TryPolymorph();Assert.AreEqual(10,p.Health);Assert.AreEqual(10,p.MaximumHealth);Assert.AreEqual(-2,StatusRules.EffectiveAttackBonus(p));Assert.AreEqual(-2,StatusRules.EffectiveDefenseBonus(p));Assert.AreEqual(1,GameManager.BaseRangeOf(p));Assert.False(p.Character is IHandEffectRewriter);Assert.AreEqual("Units/polymorphed_sheep",p.BattleEmblemKey);p.EndPolymorph();Assert.AreEqual(18,p.Health);Assert.True(p.Character is PathOfTheGiantLogic);}
        [Test] public void Sheep_damage_break_restores_original_health_and_only_overflow_hits_it()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerB;p.SetHealthExact(18);p.TryPolymorph();Assert.AreEqual(13,p.TakeDamageResolved(13));Assert.False(p.IsPolymorphed);Assert.AreEqual(15,p.Health);}
        [Test] public void Sheep_expiry_and_refresh_do_not_heal_shell_and_statuses_keep_ticking()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerA;p.SetHealthExact(13);p.ApplyStatus(StatusEffect.Wet,3);p.TryPolymorph();p.TakeDamage(4);p.TryPolymorph();Assert.AreEqual(6,p.Health);p.TickStatuses();Assert.True(p.IsPolymorphed);Assert.AreEqual(2,p.StatusTurnsLeft(StatusEffect.Wet));p.TickStatuses();Assert.False(p.IsPolymorphed);Assert.AreEqual(13,p.Health);Assert.AreEqual(1,p.StatusTurnsLeft(StatusEffect.Wet));}
        [Test] public void Sheep_multihit_recomputes_defense_after_shell_break()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerB;p.SetHealthExact(18);p.TryPolymorph();var ctx=Hit(g,g.PlayerA,p,16,2);Assert.AreEqual(18,ctx.ActualHealthDamage);Assert.AreEqual(10,p.Health);}
        [Test] public void Holy_light_blocks_and_cleanses_polymorph()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerA;p.ApplyStatus(StatusEffect.HolyLight);Assert.False(p.TryPolymorph());p.ClearStatuses();p.TryPolymorph();p.ApplyStatus(StatusEffect.HolyLight);Assert.False(p.IsPolymorphed);}
        [Test] public void Enchantment_prompts_once_after_entire_attack_and_new_effect_survives_that_attack()
        {var g=Match(CharacterDefinition.SchoolOfEnchantment,CharacterDefinition.Blank);var list=Capture(g);Hit(g,g.PlayerA,g.PlayerB,4,2);Assert.AreEqual(1,list.Count);list[0].ChooseOption((int)StatusEffect.Disabled);Assert.True(g.PlayerB.HasStatus(StatusEffect.Disabled));Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Disabled));g.PlayerB.TakeDamage(1);Assert.False(g.PlayerB.HasStatus(StatusEffect.Disabled));}
        [Test] public void Enchantment_received_damage_choice_belongs_to_defender()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.SchoolOfEnchantment);var list=Capture(g);Hit(g,g.PlayerA,g.PlayerB);Assert.AreEqual(g.PlayerB,list[0].Owner);list[0].ChooseOption((int)StatusEffect.Charm);Assert.True(g.PlayerA.HasStatus(StatusEffect.Charm));}
        [Test] public void Enchantment_does_not_clear_other_source_or_break_on_false_life_only()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);var p=g.PlayerB;p.ApplyStatus(StatusEffect.Disabled,4,g.PlayerA);p.ApplyEnchantment(StatusEffect.Disabled,g.PlayerA);p.GainFalseLife(2);p.TakeDamage(1);Assert.AreEqual(4,p.StatusTurnsLeft(StatusEffect.Disabled));p.TakeDamage(2);Assert.True(p.HasStatus(StatusEffect.Disabled));Assert.AreEqual(4,p.StatusTurnsLeft(StatusEffect.Disabled));}
        [Test] public void Enchantment_does_not_trigger_on_shield_zero_damage_or_false_life_only()
        {var g=Match(CharacterDefinition.SchoolOfEnchantment,CharacterDefinition.Blank);var list=Capture(g);g.PlayerB.GainFalseLife(10);Hit(g,g.PlayerA,g.PlayerB);Assert.AreEqual(0,list.Count);}
        [Test] public void Enchantment_can_be_declined_and_expires_in_two_target_turns()
        {var g=Match(CharacterDefinition.SchoolOfEnchantment,CharacterDefinition.Blank);var list=Capture(g);Hit(g,g.PlayerA,g.PlayerB);list[0].ChooseOption(0);Assert.False(g.PlayerB.HasStatus(StatusEffect.Charm));g.PlayerB.ApplyEnchantment(StatusEffect.Charm,g.PlayerA);g.PlayerB.TickStatuses();Assert.True(g.PlayerB.HasStatus(StatusEffect.Charm));g.PlayerB.TickStatuses();Assert.False(g.PlayerB.HasStatus(StatusEffect.Charm));}
        [Test] public void Knight_shield_choice_has_exactly_five_elements_and_decline()
        {var g=Match(CharacterDefinition.EldritchKnight,CharacterDefinition.Blank);var list=Capture(g);var knight=(EldritchKnightLogic)g.PlayerA.Character;knight.OnOwnShieldBroken(g.PlayerA,g.PlayerA.LeftHand,g);Assert.AreEqual(6,list[0].Options.Count);list[0].ChooseOption((int)AttackDamageType.Cold);Assert.AreEqual(AttackDamageType.Cold,knight.Enchantment);}
        [Test] public void Knight_records_all_nonphysical_types_overwrites_and_consumes_at_attack_start()
        {
            foreach(var type in ResistanceNames.All){if(!AttackDamageTypes.IsMagic(type))continue;
                var g=Match(CharacterDefinition.Blank,CharacterDefinition.EldritchKnight);var list=Capture(g);Hit(g,g.PlayerA,g.PlayerB,1,1,type);Assert.True(list.Count>=1);list[list.Count-1].ChooseOption(1);var knight=(EldritchKnightLogic)g.PlayerB.Character;Assert.AreEqual(type,knight.Enchantment);var attack=Hit(g,g.PlayerB,g.PlayerA,1);Assert.AreEqual(type,attack.AttackType);Assert.AreEqual(AttackDamageType.None,knight.Enchantment);}
        }
        [Test] public void Knight_does_not_consume_enchantment_in_preview_or_periodic_damage()
        {var g=Match(CharacterDefinition.EldritchKnight,CharacterDefinition.Blank);var list=Capture(g);var k=(EldritchKnightLogic)g.PlayerA.Character;k.OnOwnShieldBroken(g.PlayerA,g.PlayerA.LeftHand,g);list[0].ChooseOption((int)AttackDamageType.Fire);k.AttackTypeOf(new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g));Assert.AreEqual(AttackDamageType.Fire,k.Enchantment);var dot=new AttackContext(g.PlayerA,g.PlayerA,g.PlayerA.LeftHand,1,g){IsStatusDamage=true,AttackType=AttackDamageType.Fire,IgnoreStatBonuses=true};dot.Resolve(null,g);Assert.AreEqual(AttackDamageType.Fire,k.Enchantment);}
        [Test] public void Bladesong_entry_is_optional_and_survives_entry_turn()
        {var g=Match(CharacterDefinition.Bladesinging,CharacterDefinition.Blank);var p=g.PlayerA;p.SetHealthExact(10);p.LeftHand.SetValue(4);p.RightHand.SetValue(6);var list=Capture(g);g.ResolveStatusTurnEnd(p);Assert.AreEqual(1,list.Count);list[0].ChooseOption((int)AttackDamageType.Acid);var song=(BladesingingLogic)p.Character;Assert.True(song.Singing);Assert.AreEqual(1,p.Character.AttackBonus);Assert.AreEqual(0,p.Character.DefenseBonus);Assert.AreEqual(AttackDamageType.Acid,song.AttackTypeOf(null));}
        [Test] public void Bladesong_requires_effective_action_from_following_turn()
        {var g=Match(CharacterDefinition.Bladesinging,CharacterDefinition.Blank);var p=g.PlayerA;p.SetHealthExact(2);var list=Capture(g);g.ResolveStatusTurnEnd(p);list[0].ChooseOption((int)AttackDamageType.Fire);var song=(BladesingingLogic)p.Character;p.ResetTurnFlags();Hit(g,p,g.PlayerB,0);g.ResolveStatusTurnEnd(p);Assert.True(song.Singing);p.ResetTurnFlags();g.ResolveStatusTurnEnd(p);Assert.False(song.Singing);}
        [Test] public void Hex_curse_is_optional_moves_refreshes_and_ticks_on_target_only()
        {Four(CharacterDefinition.TheHexblade,out var a,out var b,out var c,out var d);var g=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{null,null,null,null});var h=Curse(g,a,b);Assert.AreEqual(2,h.CurseTurns);h.TickCurse(c);Assert.AreEqual(2,h.CurseTurns);h.TickCurse(b);Assert.AreEqual(1,h.CurseTurns);h=Curse(g,a,c);Assert.AreEqual(c,h.CurseTarget);Assert.AreEqual(0,h.BonusAgainst(a,b));Assert.AreEqual(2,h.BonusAgainst(a,c));h.TickCurse(c);h.TickCurse(c);Assert.AreEqual(null,h.CurseTarget);}
        [Test] public void Hex_damage_is_plus_two_each_segment_but_not_periodic()
        {var g=Match(CharacterDefinition.TheHexblade,CharacterDefinition.Blank);Curse(g,g.PlayerA,g.PlayerB);var ctx=Hit(g,g.PlayerA,g.PlayerB,4,2);Assert.AreEqual(10,ctx.ActualHealthDamage);var dot=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,1,g){IsStatusDamage=true,IgnoreStatBonuses=true,AttackType=AttackDamageType.Poison};dot.Resolve(null,g);Assert.AreEqual(1,dot.ActualHealthDamage);}
        [Test] public void Specter_inherits_seat_digits_team_and_permanent_affinities_but_not_victory_life()
        {
            var g=Four(CharacterDefinition.TheHexblade,out var a,out var b,out var c,out var d);Curse(g,a,b);b.LeftHand.SetValue(3);b.RightHand.SetValue(8);b.SetHealthExact(1);Hit(g,a,b);g.RefreshGameOver();Assert.True(b.IsSummon);Assert.AreEqual(a,b.SummonOwner);Assert.AreEqual(3,b.LeftHand.Value);Assert.AreEqual(8,b.RightHand.Value);Assert.AreEqual(10,b.Health);Assert.True(g.IsTeammate(a,b));Assert.AreEqual(3,g.AlivePlayerCount);Assert.AreEqual(2,g.DistanceBetween(a,c));Assert.False(g.CanTakeTurn(b));Assert.True(b.HasImmunity(AttackDamageType.Necrotic));Assert.True(b.HasImmunity(AttackDamageType.Poison));Hit(g,c,b,2,1,AttackDamageType.Fire);Assert.True(b.HasResistance(AttackDamageType.Fire));Assert.True(b.HasImmunity(AttackDamageType.Poison));
        }
        [Test] public void Specter_despawns_on_owner_death_and_game_reset_restores_original_roles()
        {var g=Four(CharacterDefinition.TheHexblade,out var a,out var b,out var c,out var d);Curse(g,a,b);b.SetHealthExact(1);Hit(g,a,b);g.RefreshGameOver();Assert.True(b.IsSummon);a.SetHealthExact(0);g.RefreshGameOver();Assert.False(b.IsSummon);Assert.False(b.IsAlive);Assert.AreEqual(CharacterDefinition.Blank,b.Definition);g.ResetMatch();Assert.AreEqual(CharacterDefinition.TheHexblade,a.Definition);Assert.False(b.IsSummon);}
        [Test] public void Last_opponent_kill_wins_without_specter_delaying_victory()
        {var g=Match(CharacterDefinition.TheHexblade,CharacterDefinition.Blank);Curse(g,g.PlayerA,g.PlayerB);g.PlayerB.SetHealthExact(1);Hit(g,g.PlayerA,g.PlayerB);g.RefreshGameOver();Assert.True(g.IsGameOver);Assert.AreEqual(g.PlayerA,g.Winner);Assert.False(g.PlayerB.IsSummon);}
        [Test] public void New_choice_ai_prefers_enemy_and_never_controls_healthy_ally()
        {var g=Four(CharacterDefinition.SchoolOfTransmutation,out var a,out var b,out var c,out var d);var ai=new GreedyChoiceProvider();Assert.AreEqual(b,ai.ChooseArcaneTarget(ArcaneChoices.Transmute,a,new[]{d,b},g));Assert.AreEqual(null,ai.ChooseArcaneTarget(ArcaneChoices.Transmute,a,new[]{d},g));Assert.AreEqual(b,ai.ChooseArcaneTarget(ArcaneChoices.Hex,a,new[]{d,b},g));}
        [Test] public void Real_board_shows_start_curse_and_end_polymorph_choices_to_human_owner()
        {var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var g=CharacterFactory.CreateMatch(CharacterDefinition.TheHexblade,CharacterDefinition.Blank,ca,cb);var board=new BoardController(g,ca,cb);g.PlayerA.LeftHand.SetValue(7);board.BeginTurn();Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);Assert.AreEqual(g.PlayerA,board.Chooser);Assert.True(board.PendingWizardChoice.TargetsPlayer);Assert.True(board.ChooseWizardHand(g.PlayerB.LeftHand));Assert.AreEqual(g.PlayerB,((HexbladeLogic)g.PlayerA.Character).CurseTarget);}
        [Test] public void Enchantment_normal_status_expiry_does_not_erase_newer_enchanted_overlay()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.Blank);g.PlayerA.ApplyStatus(StatusEffect.Disabled,1);g.PlayerA.ApplyEnchantment(StatusEffect.Disabled,g.PlayerB,2);g.PlayerA.TickStatuses();Assert.True(g.PlayerA.HasStatus(StatusEffect.Disabled));Assert.AreEqual(1,g.PlayerA.StatusTurnsLeft(StatusEffect.Disabled));}
        [Test] public void Poison_and_burning_notify_new_schools_using_the_real_status_source()
        {var g=Match(CharacterDefinition.SchoolOfEnchantment,CharacterDefinition.EldritchKnight);var list=Capture(g);g.PlayerB.ApplyStatus(StatusEffect.Poison,2,g.PlayerA);g.ResolveStatusTurnStart(g.PlayerB);Assert.AreEqual(2,list.Count);Assert.AreEqual(g.PlayerA,list[0].Owner);Assert.AreEqual(g.PlayerB,list[1].Owner);list[1].ChooseOption(1);Assert.AreEqual(AttackDamageType.Poison,((EldritchKnightLogic)g.PlayerB.Character).Enchantment);}
        [Test] public void Knight_force_enchantment_pierces_shield_and_dodge_without_spending_dodge_hand()
        {var g=Match(CharacterDefinition.EldritchKnight,CharacterDefinition.Swashbuckler);var list=Capture(g);var incoming=new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,1,g){AttackType=AttackDamageType.Force};incoming.Resolve(null,g);list[0].ChooseOption(1);g.PlayerB.RightHand.SetShieldState(ShieldState.Formed);var hit=Hit(g,g.PlayerA,g.PlayerB);Assert.False(hit.WasBlocked);Assert.False(hit.WasDodged);Assert.AreEqual(1,g.PlayerB.LeftHand.Value);Assert.True(g.PlayerB.RightHand.IsShield);Assert.AreEqual(AttackDamageType.Force,hit.AttackType);}
        [Test] public void Specter_skips_remainder_of_current_round_then_acts_next_round()
        {var g=Four(CharacterDefinition.TheHexblade,out var a,out var b,out var c,out var d);Curse(g,a,b);b.SetHealthExact(1);Hit(g,a,b);g.RefreshGameOver();g.DeferWizardChoice=null;c.LeftHand.SetValue(1);d.LeftHand.SetValue(1);g.ForceActivePlayerForTest(a);g.ExecuteTurn(a,a.RightHand,c.LeftHand);Assert.AreEqual(c,g.CurrentPlayer);g.ExecuteTurn(c,c.LeftHand,a.RightHand);Assert.AreEqual(d,g.CurrentPlayer);g.ExecuteTurn(d,d.LeftHand,c.LeftHand);Assert.AreEqual(1,g.RoundCount);Assert.AreEqual(a,g.CurrentPlayer);g.ExecuteTurn(a,a.RightHand,c.LeftHand);Assert.AreEqual(b,g.CurrentPlayer);Assert.True(g.CanTakeTurn(b));}
        [Test] public void Summon_team_ui_and_control_follow_owner_even_if_victim_was_human()
        {var g=Four(CharacterDefinition.TheHexblade,out var a,out var b,out var c,out var d);var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());Curse(g,a,b);b.SetHealthExact(1);Hit(g,a,b);int revision=g.SeatRevision;g.RefreshGameOver();Assert.True(g.SeatRevision>revision);Assert.AreEqual(RingRelation.Teammate,IdentityPresentation.Relation(g,d,b));Assert.AreEqual(RingRelation.Opponent,IdentityPresentation.Relation(g,b,a));g.ForceActivePlayerForTest(b);board.BeginTurn();Assert.False(board.IsHumanTurn);}
        [Test] public void Second_cursed_kill_replaces_the_previous_specter_without_changing_original_alive_count()
        {var g=Four(CharacterDefinition.TheHexblade,out var a,out var b,out var c,out var d);Curse(g,a,b);b.SetHealthExact(1);Hit(g,a,b);g.RefreshGameOver();Assert.True(b.IsSummon);Curse(g,a,d);d.SetHealthExact(1);Hit(g,a,d);g.RefreshGameOver();Assert.False(b.IsSummon);Assert.True(d.IsSummon);Assert.AreEqual(2,g.AlivePlayerCount);}
    }
}
