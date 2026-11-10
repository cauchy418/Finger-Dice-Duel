using System;
using FingerDiceDuel.Assistant;
using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class SecondaryAnimationTests
    {
        private static GameManager Match(CharacterDefinition a=null,CharacterDefinition b=null,bool visuals=true)
        {var g=CharacterFactory.CreateMatch(a??CharacterDefinition.Blank,b??CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.RecordCombatVisuals=visuals;return g;}
        [Test] public void Life_healing_records_cleanse_before_actual_heal_at_the_real_target()
        {var g=Match(CharacterDefinition.LifeDomain);g.PlayerB.SetHealthExact(7);g.PlayerB.ApplyStatus(StatusEffect.Decay,2);LifeDomainLogic.CleanseAndHeal(g.PlayerA,g.PlayerB,9,g);var cues=g.DrainCombatVisuals();Assert.AreEqual(2,cues.Count);Assert.AreEqual("神佑医者",cues[0].RedirectKind);Assert.AreEqual("生命门徒",cues[1].RedirectKind);Assert.AreEqual(g.PlayerB,cues[1].Target);Assert.AreEqual(9,cues[1].SkillData.Value);Assert.AreEqual(16,g.PlayerB.Health);Assert.AreEqual(20,g.PlayerA.Health);}
        [Test] public void Full_health_heal_never_claims_nonzero_recovery()
        {var g=Match(CharacterDefinition.LifeDomain);LifeDomainLogic.CleanseAndHeal(g.PlayerA,g.PlayerB,9,g);var cues=g.DrainCombatVisuals();Assert.AreEqual(0,cues.Single(e=>e.RedirectKind=="生命门徒").SkillData.Value);Assert.IsTrue(cues.Any(e=>e.RedirectKind=="神佑医者"&&e.SkillData.Status==StatusEffect.BlessedHealing));}
        [Test] public void Blocked_or_rejected_maneuvers_do_not_emit_success_animation()
        {foreach(bool holy in new[]{false,true}){var g=Match();if(holy)g.PlayerB.ApplyStatus(StatusEffect.HolyLight,2);else{g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);g.PlayerB.AddResistance(AttackDamageType.Slashing);}var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){AttackType=AttackDamageType.Slashing};attack.ApplyAttachedStatus(g.PlayerB,StatusEffect.Limp,2,g.PlayerA,g,"卓越战技");attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);Assert.IsFalse(g.PlayerB.HasStatus(StatusEffect.Limp));Assert.AreEqual(0,g.DrainCombatVisuals().Count(e=>e.RedirectKind=="卓越战技"));}}
        [Test] public void Ki_upkeep_records_once_and_uses_actual_shared_resource_counts()
        {var g=Match(CharacterDefinition.WayOfTheDrunkenMaster);var monk=CharacterBehaviors.FirstImplementer<WayOfTheDrunkenMasterLogic>(g.PlayerA);Assert.IsTrue(monk.TryEnter(g.PlayerA));monk.OnTurnStarted(g.PlayerA,g);monk.OnTurnStarted(g.PlayerA,g);var ki=g.DrainCombatVisuals().Where(e=>e.RedirectKind=="气").ToList();Assert.AreEqual(1,ki.Count);Assert.AreEqual(2,ki[0].SkillData.Previous);Assert.AreEqual(1,ki[0].SkillData.Value);Assert.AreEqual(1,g.PlayerA.MonkKi);}
        [Test] public void Ki_gain_and_snapshot_count_are_immutable_and_cap_does_not_flash()
        {var g=Match(CharacterDefinition.WayOfTheNinja);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(4);var frame=g.CaptureVisualFrame();MonkRules.AfterAction(g.PlayerA,g);var e=g.DrainCombatVisuals()[0];Assert.AreEqual(2,e.SkillData.Previous);Assert.AreEqual(3,e.SkillData.Value);Assert.AreEqual(2,frame.Players[0].Icons.First(x=>x.ResourceKey=="Effects/ki").Value);g.PlayerA.GainMonkKi(9);MonkRules.AfterAction(g.PlayerA,g);Assert.AreEqual(0,g.DrainCombatVisuals().Count);}
        [Test] public void Current_reworked_skills_have_dedicated_semantic_profiles()
        {foreach(var name in new[]{"岁月知识","知识赐福","知识祝福","困惑","净化圣焰","圣焰赐福","灼伤"})Assert.IsTrue(SecondarySkillAnimation.Find(name)!=null,name);}
        [Test] public void Knowledge_aura_exposes_stack_value_to_the_renderer()
        {var g=Match();g.PlayerA.GainKnowledge();g.PlayerA.GainKnowledge();Assert.AreEqual(2,CombatStatusIcons.For(g.PlayerA,g).Single(i=>i.ResourceKey=="Status/knowledge_blessing").Value);}
        [Test] public void Beast_gain_and_growth_have_distinct_payloads()
        {var g=Match(CharacterDefinition.PathOfTheBeast);var beast=CharacterBehaviors.FirstImplementer<PathOfTheBeastLogic>(g.PlayerA);g.PlayerA.LeftHand.SetValue(3);g.PlayerA.RightHand.SetValue(4);beast.OnOperationResolved(g.PlayerA,g.PlayerA.LeftHand,1,2,HandEffectType.None,g,null);beast.OnTurnEnded(g.PlayerA,g);var cues=g.DrainCombatVisuals().Where(e=>e.RedirectKind=="野兽形态").ToList();Assert.AreEqual("gain",cues[0].SkillData.Variant);Assert.AreEqual("consume",cues[1].SkillData.Variant);Assert.AreEqual(1,cues[1].SkillData.Previous);Assert.AreEqual(0,cues[1].SkillData.Value);}
        [Test] public void Confusion_carry_visual_uses_the_real_sum_and_operating_hand()
        {var g=Match();g.PlayerA.RightHand.SetValue(8);g.PlayerB.LeftHand.SetValue(6);g.PlayerA.ApplyStatus(StatusEffect.Confusion,2);g.ExecuteTurn(g.PlayerA,g.PlayerA.RightHand,g.PlayerB.LeftHand);var cue=g.DrainCombatVisuals().Single(e=>e.RedirectKind=="困惑");Assert.AreEqual(14,cue.SkillData.Previous);Assert.AreEqual(1,cue.SkillData.Value);Assert.AreEqual(g.PlayerA.RightHand,cue.SkillData.Source);}
        [Test] public void Knowledge_spend_and_successful_confusion_are_separate_visual_events()
        {var g=Match();g.PlayerA.GainKnowledge();new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.RightHand,4,g).Resolve(g.GetChoiceProvider(g.PlayerB),g);var cues=g.DrainCombatVisuals();var consume=cues.Single(e=>e.RedirectKind=="知识祝福"&&e.SkillData.Variant=="consume");Assert.AreEqual(1,consume.SkillData.Previous);Assert.AreEqual(0,consume.SkillData.Value);Assert.IsTrue(cues.Any(e=>e.RedirectKind=="困惑"&&e.SkillData.Variant=="apply"&&e.Target==g.PlayerB));}
        [Test] public void Giant_recovery_reports_healing_before_scorch_damage()
        {var g=Match(CharacterDefinition.PathOfTheGiant);g.PlayerA.SetHealthExact(10);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2);g.PlayerA.LeftHand.SetValue(8);g.PlayerA.RightHand.SetValue(3);g.PlayerB.LeftHand.SetValue(1);g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);var cue=g.DrainCombatVisuals().Single(e=>e.RedirectKind=="巨人恢复");Assert.AreEqual(6,cue.SkillData.Value);Assert.AreEqual(12,g.PlayerA.Health);}
        [Test] public void Warden_force_conversion_has_a_real_incoming_element_payload()
        {var g=Match(CharacterDefinition.Blank,CharacterDefinition.PathOfTheWarden);g.PlayerB.SetHealthExact(15);g.DeferWizardChoice=r=>{r.ChooseOption(1);return true;};new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.RightHand,4,g){AttackType=AttackDamageType.Fire}.Resolve(g.GetChoiceProvider(g.PlayerB),g);var cue=g.DrainCombatVisuals().Single(e=>e.RedirectKind=="守望誓约");Assert.IsTrue(SecondarySkillAnimation.Find(cue.RedirectKind)!=null);Assert.AreEqual((int)AttackDamageType.Fire,cue.SkillData.Previous);Assert.AreEqual(AttackDamageType.Force,cue.SkillData.Element);Assert.AreEqual(g.PlayerB,cue.Target);}
        [Test] public void Transmutation_extra_operation_reports_the_completed_source_and_result_once()
        {var g=Match(CharacterDefinition.SchoolOfTransmutation);var a=g.PlayerA;var b=g.PlayerB;a.LeftHand.SetValue(8);a.RightHand.SetValue(6);b.LeftHand.SetValue(6);b.RightHand.SetValue(4);var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);board.ConfirmTargetPlayer();board.ChooseWizardHand(b.LeftHand);board.ChooseSkillOption(1);g.ClearCombatVisuals();board.ClickHand(a.LeftHand);board.ClickHand(b.RightHand);board.ConfirmTargetPlayer();var cue=g.DrainCombatVisuals().Single(e=>e.RedirectKind=="变化莫测");Assert.IsTrue(SecondarySkillAnimation.Find(cue.RedirectKind)!=null);Assert.AreEqual(a.LeftHand,cue.SkillData.Source);Assert.AreEqual(4,cue.SkillData.Previous);Assert.AreEqual(8,cue.SkillData.Value);}
        [Test] public void Magician_emits_the_current_trickery_skill_name()
        {var g=Match(CharacterDefinition.Magician);g.PlayerA.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);g.ExecuteTurn(g.PlayerA,g.PlayerA.LeftHand,g.PlayerB.LeftHand);var cue=g.DrainCombatVisuals().Single(e=>e.RedirectKind=="魔术扑克");Assert.IsTrue(SecondarySkillAnimation.Find(cue.RedirectKind)!=null);Assert.AreEqual(2,cue.SkillData.Value);}
        [Test] public void Wolf_lifesteal_reports_actual_healing()
        {var g=Match(CharacterDefinition.CircleOfTheMoon);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);Assert.IsTrue(g.PlayerA.EnterWild(2,0,g));g.PlayerA.SetHealthExact(5);g.ClearCombatVisuals();new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerB),g);var cue=g.DrainCombatVisuals().Single(e=>e.RedirectKind=="狼形态");Assert.AreEqual(2,cue.SkillData.Value);}
        [Test] public void Every_literal_skill_event_has_a_registered_route_or_explicit_existing_presentation()
        {
            // These names are prose companions of already-recorded non-Skill events or snapshots.
            var alternatives=new System.Collections.Generic.HashSet<string>(new[]{
                "仇敌誓言","守护之誓","憎恨灵光","妖精步伐·取消攻击","梦幻舞步", // VowTransfer / Attack redirect / Attack dodge
                "恐惧打击","惧怖收割","次级咒术","潇洒无畏","灭族者", // real Attack, including free/extra attacks
                "双九回复", // CleanseAndHeal records 维持生命 with the actual recipient and healing
                "伤害免疫","伤害易伤","施加状态","附加抵挡","耳鸣","油腻","附魔", // Attack/frame/status icons, not extra successes
                "刻印","回廊重赋","折光外壳","推开路障","操演护持","整装迎战","架盾","架盾试验","注水铭刻","演练调律","稳弦","符文换装","训练整备" // campaign UI, hands, shields and frames
            });
            var root=System.IO.Path.GetFullPath("Assets/Scripts");
            foreach(var file in System.IO.Directory.GetFiles(root,"*.cs",System.IO.SearchOption.AllDirectories))
            {
                if(file.Contains(System.IO.Path.DirectorySeparatorChar+"UI"+System.IO.Path.DirectorySeparatorChar))continue;
                string source=System.IO.File.ReadAllText(file);
                foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(source,@"(?:NoteSkill|RecordSkillVisual)\([^
]*?,\s*""([^""]+)"""))
                {string name=match.Groups[1].Value;Assert.IsTrue(SkillVisualProfiles.Key(name)!=null||alternatives.Contains(name),name+" in "+file);}
            }
        }
        [Test] public void Scorched_wolf_heal_records_separate_real_fire_damage()
        {var g=Match(CharacterDefinition.CircleOfTheMoon);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);Assert.IsTrue(g.PlayerA.EnterWild(2,0,g));g.PlayerA.SetHealthExact(5);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2,g.PlayerB);g.ClearCombatVisuals();new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerB),g);var cues=g.DrainCombatVisuals();Assert.AreEqual(2,cues.Single(e=>e.RedirectKind=="狼形态").SkillData.Value);Assert.AreEqual(1,cues.Single(e=>e.RedirectKind=="灼伤").SkillData.Value);Assert.AreEqual(6,g.PlayerA.Health);HealBeforeScorch(cues,cues.Any(e=>e.RedirectKind=="狼形态")?"狼形态":"黑暗强运");}
        [Test] public void Scorched_harmony_uses_gross_healing_and_records_fire_damage()
        {var g=Match(CharacterDefinition.WayOfTheOpenHand);g.PlayerA.SetHealthExact(2);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2,g.PlayerB);Assert.IsTrue(CharacterBehaviors.FirstImplementer<WayOfTheOpenHandLogic>(g.PlayerA).TryHarmonyBody(g.PlayerA,g));var cues=g.DrainCombatVisuals();Assert.AreEqual(4,cues.Single(e=>e.RedirectKind=="混元体").SkillData.Value);Assert.AreEqual(2,cues.Single(e=>e.RedirectKind=="灼伤").SkillData.Value);Assert.AreEqual(4,g.PlayerA.Health);HealBeforeScorch(cues,"混元体");}
        [Test] public void Scorched_fiend_drain_uses_gross_healing_and_records_fire_damage()
        {var g=Match(CharacterDefinition.TheFiend);g.PlayerA.SetHealthExact(5);g.PlayerB.SetHealthExact(10);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2,g.PlayerB);new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerB),g);var cues=g.DrainCombatVisuals();Assert.AreEqual(3,cues.Single(e=>e.RedirectKind=="黑暗强运").SkillData.Value);Assert.AreEqual(g.PlayerA,cues.Single(e=>e.RedirectKind=="黑暗强运").Target);Assert.AreEqual(2,cues.Single(e=>e.RedirectKind=="灼伤").SkillData.Value);Assert.AreEqual(6,g.PlayerA.Health);HealBeforeScorch(cues,cues.Any(e=>e.RedirectKind=="狼形态")?"狼形态":"黑暗强运");}
        [Test] public void Scorched_chalice_heal_records_full_heal_then_actual_fire_damage()
        {var g=Match(CharacterDefinition.CircleOfTheStars);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);Assert.IsTrue(g.PlayerA.EnterWild(2,2,g));g.PlayerB.SetHealthExact(5);g.PlayerB.ApplyStatus(StatusEffect.Scorch,2,g.PlayerA);g.DeferWizardChoice=r=>{r.Choose(g.PlayerB.LeftHand);return true;};g.PlayerA.LeftHand.SetValue(9);g.PlayerA.Character.OnOperationResolved(g.PlayerA,g.PlayerA.LeftHand,8,1,HandEffectType.Heal,g,null);var cues=g.DrainCombatVisuals();Assert.AreEqual(9,cues.Single(e=>e.RedirectKind=="圣杯座").SkillData.Value);Assert.AreEqual(5,cues.Single(e=>e.RedirectKind=="灼伤").SkillData.Value);Assert.AreEqual(9,g.PlayerB.Health);HealBeforeScorch(cues,"圣杯座");}
        [Test] public void Life_domain_removes_scorch_before_healing()
        {var g=Match(CharacterDefinition.LifeDomain);g.PlayerB.SetHealthExact(5);g.PlayerB.ApplyStatus(StatusEffect.Scorch,2);LifeDomainLogic.CleanseAndHeal(g.PlayerA,g.PlayerB,9,g);Assert.AreEqual(14,g.PlayerB.Health);Assert.IsFalse(g.DrainCombatVisuals().Any(e=>e.RedirectKind=="灼伤"));}
        private static void HealBeforeScorch(System.Collections.Generic.IReadOnlyList<CombatVisualEvent> cues,string skill)
        {int heal=-1,scorch=-1;for(int i=0;i<cues.Count;i++){if(cues[i].RedirectKind==skill)heal=i;if(cues[i].RedirectKind=="灼伤")scorch=i;}Assert.IsTrue(heal>=0&&scorch>heal,skill+" must precede Scorch");}
        [Test] public void Scorched_ancient_self_heal_keeps_existing_reaction_count_and_records_order()
        {var g=Match(CharacterDefinition.OathOfTheAncients);g.PlayerA.SetHealthExact(10);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2,g.PlayerB);new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,4,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerA),g);var cues=g.DrainCombatVisuals();Assert.AreEqual(7,g.PlayerA.Health);Assert.AreEqual(1,cues.Count(e=>e.RedirectKind=="不灭哨卫"));Assert.AreEqual(2,cues.Single(e=>e.RedirectKind=="不灭哨卫").SkillData.Value);Assert.AreEqual(1,cues.Single(e=>e.RedirectKind=="灼伤").SkillData.Value);HealBeforeScorch(cues,"不灭哨卫");}
        [Test] public void Scorched_ancient_ally_heal_does_not_enable_reciprocal_gameplay_hooks()
        {foreach(bool visuals in new[]{false,true}){var g=Match(CharacterDefinition.OathOfTheAncients,CharacterDefinition.OathOfTheAncients,visuals);g.PlayerA.SetHealthExact(19);g.PlayerB.SetHealthExact(19);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2);g.PlayerB.ApplyStatus(StatusEffect.Scorch,2);int choices=0;g.DeferWizardChoice=r=>{choices++;r.Choose(g.PlayerB.LeftHand);return true;};new AttackContext(g.PlayerB,g.PlayerA,g.PlayerB.LeftHand,1,g){IgnoreStatBonuses=true}.Resolve(g.GetChoiceProvider(g.PlayerA),g);Assert.AreEqual(18,g.PlayerA.Health);Assert.AreEqual(19,g.PlayerB.Health);Assert.AreEqual(1,choices);var cues=g.DrainCombatVisuals();if(visuals){Assert.AreEqual(1,cues.Single(e=>e.RedirectKind=="不灭哨卫").SkillData.Value);HealBeforeScorch(cues,"不灭哨卫");}else Assert.AreEqual(0,cues.Count);}}
        [Test] public void Heal_visual_callback_reports_zero_when_blocked_or_full()
        {var g=Match();int actual=-1;g.PlayerA.Heal(3,onHealed:n=>actual=n,visualManager:g);Assert.AreEqual(0,actual);g.PlayerA.SetHealthExact(5);g.PlayerA.ApplyStatus(StatusEffect.Decay,2);g.PlayerA.ApplyStatus(StatusEffect.Scorch,2);actual=-1;g.PlayerA.Heal(3,onHealed:n=>actual=n,visualManager:g);Assert.AreEqual(0,actual);Assert.AreEqual(5,g.PlayerA.Health);Assert.AreEqual(0,g.DrainCombatVisuals().Count);}
        private sealed class CountRandom:Random
        {public int Calls;public override int Next(int maxValue){Calls++;return Calls==1?40:56;}}
        [Test] public void Wild_magic_visuals_do_not_roll_again_or_change_per_hit_results()
        {int calls=0,hp=0;AttackDamageType[] expected=null;foreach(bool visuals in new[]{false,true}){var g=Match(CharacterDefinition.WildMagic,visuals:visuals);var random=new CountRandom();CharacterBehaviors.FirstImplementer<WildMagicLogic>(g.PlayerA).Rng=random;var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8,g){HitCount=2};attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);if(!visuals){calls=random.Calls;hp=g.PlayerB.Health;expected=attack.SegmentTypes.ToArray();}else{Assert.AreEqual(calls,random.Calls);Assert.AreEqual(hp,g.PlayerB.Health);Assert.AreEqual(expected[0],attack.SegmentTypes[0]);Assert.AreEqual(expected[1],attack.SegmentTypes[1]);var cues=g.DrainCombatVisuals().Where(e=>e.RedirectKind=="狂野魔法浪涌").ToList();Assert.AreEqual(2,cues.Count);Assert.AreEqual(expected[0],cues[0].SkillData.Element);Assert.AreEqual(expected[1],cues[1].SkillData.Element);}}}
        [Test] public void Critical_visual_power_is_captured_without_becoming_a_damage_multiplier()
        {var g=Match(CharacterDefinition.Champion);g.PlayerA.LeftHand.SetValue(4);g.PlayerA.RightHand.SetValue(6);var attack=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){SourceHandValueBefore=1};attack.Resolve(g.GetChoiceProvider(g.PlayerB),g);var e=g.DrainCombatVisuals().First(x=>x.Kind==CombatVisualKind.Attack);Assert.AreEqual(1.35f,e.Power);Assert.AreEqual(7,attack.AppliedDamage);}
        [Test] public void Tactician_visual_records_the_subtraction_result()
        {
            var g=Match(CharacterDefinition.Tactician);var a=g.PlayerA;var b=g.PlayerB;
            var board=new BoardController(g,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            a.LeftHand.SetValue(4);b.LeftHand.SetValue(6);board.BeginTurn();
            Assert.True(board.ClickHand(a.LeftHand));Assert.True(board.ClickHand(b.LeftHand));
            Assert.True(board.ChooseSkillOption(1));Assert.True(board.ConfirmTargetPlayer());
            var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="战术眼光");
            Assert.AreEqual(8,e.SkillData.Value);Assert.AreEqual(4,e.SkillData.Previous);
        }
        [Test] public void Right_hand_taichi_formation_keeps_the_real_source_hand()
        {var g=Match(CharacterDefinition.WayOfTheTaiChi);var logic=CharacterBehaviors.FirstImplementer<WayOfTheTaiChiLogic>(g.PlayerA);g.PlayerA.RightHand.SetValue(5);logic.OnShieldFormed(g.PlayerA,g.PlayerA.RightHand,g);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="阴阳流转");Assert.AreEqual(g.PlayerA.RightHand,e.SourceHand);Assert.AreEqual(g.PlayerA.RightHand,e.SkillData.Source);Assert.AreEqual(ShieldState.Taichi,g.PlayerA.RightHand.State);}
        [Test] public void War_blessing_nine_conversion_uses_the_operating_right_hand()
        {var g=Match(CharacterDefinition.WarDomain);g.PlayerA.RightHand.SetValue(8);g.PlayerB.LeftHand.SetValue(1);g.PlayerA.ApplyStatus(StatusEffect.WarBlessing,2);g.WarBlessingNineAttack=true;g.ExecuteTurn(g.PlayerA,g.PlayerA.RightHand,g.PlayerB.LeftHand);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="战争祝福");Assert.AreEqual(g.PlayerA.RightHand,e.SourceHand);Assert.AreEqual(9,e.SkillData.Value);}
        [Test] public void Both_locked_hands_have_distinct_presentation_targets()
        {var g=Match();g.PlayerA.LeftHand.Lock();g.PlayerA.RightHand.Lock();var icons=CombatStatusIcons.For(g.PlayerA,g).Where(i=>i.ResourceKey=="Overlay/locked").ToList();Assert.AreEqual(2,icons.Count);Assert.AreEqual(g.PlayerA.LeftHand,icons[0].TargetHand);Assert.AreEqual(g.PlayerA.RightHand,icons[1].TargetHand);}
        [Test] public void Distance_shield_expiry_reports_the_real_pair_after_two_target_turns()
        {var g=Match(CharacterDefinition.TheGreatOldOne);g.ApplyDistancePenalty(g.PlayerA,g.PlayerB,2);g.ResolveStatusTurnEnd(g.PlayerB);Assert.AreEqual(0,g.DrainCombatVisuals().Count);g.ResolveStatusTurnEnd(g.PlayerB);var e=g.DrainCombatVisuals().First(x=>x.RedirectKind=="思维之盾");Assert.AreEqual("expire",e.SkillData.Variant);Assert.AreEqual(g.PlayerA,e.Actor);Assert.AreEqual(g.PlayerB,e.Target);Assert.AreEqual(0,g.DistancePenaltyBetween(g.PlayerA,g.PlayerB));}
        [Test] public void Profiles_stay_silent_when_visual_recording_is_disabled()
        {var g=Match(visuals:false);foreach(var p in SecondarySkillAnimation.Profiles)g.RecordSkillVisual(g.PlayerA,p.Skill,g.PlayerB,new SkillVisualData(value:3));Assert.AreEqual(0,g.DrainCombatVisuals().Count);}
    }
}
