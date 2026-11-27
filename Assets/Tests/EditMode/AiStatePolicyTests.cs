using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class AiStatePolicyTests
    {
        private static GameManager Match(out PlayerState a,out PlayerState b,out GreedyChoiceProvider ai)
        {a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;return g;}
        [Test] public void Wet_scores_the_real_six_above_the_raw_eight_that_becomes_seven()
        {
            var g=Match(out var a,out var b,out var ai);a.ApplyStatus(StatusEffect.Wet);a.LeftHand.SetValue(1);a.RightHand.SetValue(3);b.LeftHand.SetValue(6);b.RightHand.SetValue(7);
            Assert.IsTrue(ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.LeftHand),a,g)>ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.RightHand),a,g));
        }
        [Test] public void Decay_keeps_real_healing_available_and_accounts_for_self_damage()
        {
            var g=Match(out var a,out var b,out var ai);a.SetHealthExact(2);a.ApplyStatus(StatusEffect.Decay);a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);b.RightHand.SetValue(4);
            Assert.IsTrue(StatusRules.CanHeal(a));
            Assert.AreEqual(9,AiStatePolicy.OperationSelfDamage(a,9,false));
            Assert.AreEqual(0,AiStatePolicy.OperationSelfDamage(a,4,true));
        }
        [Test] public void Every_current_status_has_an_explicit_rule_and_effect_extension_keys_exist()
        {
            int negatives=0;foreach(var status in StatusNames.AllStatuses){Assert.IsTrue(AiStatePolicy.Rules.ContainsKey(AiStatePolicy.KeyOf(status)),status.ToString());if(StatusNames.IsNegativeStatus(status))negatives++;}
            Assert.AreEqual(27,negatives);
            foreach(var key in new[]{"bladesong","drunken","polymorph","wild_shape","star:1","star:2","star:3","ki","wild_charge","beast_soul","lore_records","portents","element_enchantment","arcane_barrier","false_life","spore_life","hidden_force","stealth","ambush","locked_hand","protected_addend","low_barbarian","summon","hunter_mark","hexblade_curse","vow","resistance","vulnerability","immunity"})
                Assert.IsTrue(AiStatePolicy.Rules.ContainsKey("effect:"+key),key);
        }
        [Test] public void State_hand_weights_are_copies_and_expire_without_leaking_to_other_roles()
        {
            var g=Match(out var a,out var b,out var ai);var basis=HandValueProfiles.For(CharacterDefinition.Blank);int original=basis.WeightOf(5);
            a.ApplyStatus(StatusEffect.KnockedDown);var affected=AiStatePolicy.ApplyHandWeights(basis,a,g);
            Assert.IsTrue(affected.WeightOf(5)>original);Assert.AreEqual(original,basis.WeightOf(5));
            Assert.AreEqual(original,AiStatePolicy.ApplyHandWeights(basis,b,g).WeightOf(5));
            a.RemoveStatus(StatusEffect.KnockedDown);Assert.AreEqual(original,AiStatePolicy.ApplyHandWeights(basis,a,g).WeightOf(5));
        }
        [Test] public void Combined_effects_stack_and_holy_fire_pauses_affinity_policy()
        {
            var g=Match(out var a,out var b,out var ai);a.AddPermanentResistance(AttackDamageType.Fire);
            Assert.IsTrue(AiStatePolicy.Rules["effect:resistance"].Applies(a,g));
            a.ApplyStatus(StatusEffect.HolyLight);Assert.IsFalse(AiStatePolicy.Rules["effect:resistance"].Applies(a,g));
            a.RemoveStatus(StatusEffect.HolyLight);Assert.IsTrue(AiStatePolicy.Rules["effect:resistance"].Applies(a,g));
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);var c=new AiStateContext(a,b,g,move,5,HandEffectType.Shield,0,0,true);
            int initial=AiStatePolicy.ScoreMove(c);a.ApplyStatus(StatusEffect.Acid);int acid=AiStatePolicy.ScoreMove(c);a.ApplyStatus(StatusEffect.KnockedDown);
            Assert.IsTrue(acid>initial);Assert.IsTrue(AiStatePolicy.ScoreMove(c)>acid);
        }
        [Test] public void All_roles_accept_every_negative_modifier_without_changing_the_board()
        {
            foreach(var role in CharacterDefinition.BuiltIn)foreach(var status in StatusNames.AllStatuses)
            {
                if(!StatusNames.IsNegativeStatus(status))continue;
                var a=CharacterFactory.CreatePlayer("A",role);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;
                a.LeftHand.SetValue(3);a.RightHand.SetValue(2);b.LeftHand.SetValue(1);a.ApplyStatus(status);
                int health=a.Health,stacks=a.KnowledgeStacks,ki=a.MonkKi,charges=a.WildCharges,turns=a.StatusTurnsLeft(status);
                var move=new LegalMove(a,a.LeftHand,b.LeftHand);int one=ai.ScoreMoveForTest(move,a,g);int two=ai.ScoreMoveForTest(move,a,g);
                Assert.AreEqual(one,two,role.Id+" "+status);Assert.AreEqual(health,a.Health);Assert.AreEqual(3,a.LeftHand.Value);Assert.AreEqual(2,a.RightHand.Value);
                Assert.AreEqual(stacks,a.KnowledgeStacks);Assert.AreEqual(ki,a.MonkKi);Assert.AreEqual(charges,a.WildCharges);Assert.AreEqual(turns,a.StatusTurnsLeft(status));Assert.AreEqual(0,g.TurnCount);
            }
        }
        [Test] public void Confusion_and_frost_are_applied_once_to_previewed_results()
        {
            var g=Match(out var a,out var b,out var ai);a.ApplyStatus(StatusEffect.Confusion);
            Assert.AreEqual(1,AiStatePolicy.PreviewResult(a,6,8));Assert.AreEqual(8,AiStatePolicy.PreviewResult(a,6,8,true));
            a.RemoveStatus(StatusEffect.Confusion);a.ApplyStatus(StatusEffect.Frost);a.ApplyStatus(StatusEffect.Wet);
            Assert.AreEqual(3,AiStatePolicy.PreviewResult(a,1,7));
            Assert.AreEqual(g.PreviewOperationResult(a,a.LeftHand,b.LeftHand),AiStatePolicy.PreviewResult(a,a.LeftHand.Value,b.LeftHand.Value));
        }
        [Test] public void Tinnitus_discards_heal_and_shield_preferences_and_blindness_changes_target_value()
        {
            var g=Match(out var a,out var b,out var ai);var move=new LegalMove(a,a.LeftHand,b.LeftHand);a.SetHealthExact(4);a.ApplyStatus(StatusEffect.Tinnitus);
            Assert.IsTrue(AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,9,HandEffectType.Heal,0,0,true))<0);
            Assert.IsTrue(AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,5,HandEffectType.Shield,0,0,true))<0);
            a.RemoveStatus(StatusEffect.Tinnitus);a.ApplyStatus(StatusEffect.DarknessBlind);int sighted=AiStatePolicy.ScoreTarget(a,b,g);b.ApplyStatus(StatusEffect.DarknessBlind);
            Assert.IsTrue(AiStatePolicy.ScoreTarget(a,b,g)>sighted);
        }
        [Test] public void Life_cleric_can_still_cleanse_decay_before_its_heal()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LifeDomain);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;
            a.SetHealthExact(2);a.ApplyStatus(StatusEffect.Decay);a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);b.RightHand.SetValue(2);
            Assert.IsTrue(ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.LeftHand),a,g)>ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.RightHand),a,g));
            Assert.IsTrue(a.HasStatus(StatusEffect.Decay));Assert.AreEqual(2,a.Health);
        }
        [Test] public void Life_attack_digits_remain_valuable_cleansing_actions_while_disabled_or_stunned()
        {
            foreach(var status in new[]{StatusEffect.Disabled,StatusEffect.Stunned,StatusEffect.Tinnitus})
            {
                var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LifeDomain);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;
                a.SetHealthExact(2);a.ApplyStatus(status);a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);b.RightHand.SetValue(1);
                Assert.IsTrue(ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.LeftHand),a,g)>ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.RightHand),a,g),status.ToString());
                Assert.IsTrue(a.HasStatus(status));Assert.AreEqual(2,a.Health);
            }
        }
        [Test] public void Life_full_health_holy_fire_still_has_a_real_cleanse_and_overflow_blessing()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.LifeDomain);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;
            a.ApplyStatus(StatusEffect.HolyLight);a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);b.RightHand.SetValue(2);
            Assert.IsTrue(ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.LeftHand),a,g)>ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.RightHand),a,g));Assert.IsTrue(a.HasStatus(StatusEffect.HolyLight));
        }
        [Test] public void Pre_effect_status_survival_cannot_use_future_healing_or_the_operating_shield()
        {
            var g=Match(out var a,out var b,out var ai);a.SetHealthExact(2);a.ApplyStatus(StatusEffect.Decay);
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            Assert.IsTrue(AiStatePolicy.ScoreMove(new AiStateContext(a,a,g,move,9,HandEffectType.Heal,0,9,true))<-400);
            Assert.IsTrue(AiStatePolicy.ScoreMove(new AiStateContext(a,a,g,move,5,HandEffectType.Shield,0,0,true))<-400);
            a.RemoveStatus(StatusEffect.Decay);a.ApplyStatus(StatusEffect.DivinePunishment);
            a.LeftHand.SetValue(5);a.LeftHand.SetShieldState(ShieldState.Formed);
            Assert.IsTrue(AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,4,HandEffectType.Attack4,4,0,true))<-400);
            a.RightHand.SetValue(5);a.RightHand.SetShieldState(ShieldState.Formed);
            Assert.IsTrue(AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,4,HandEffectType.Attack4,4,0,true))>-400);
        }
        [Test] public void Normal_no_effect_digits_trigger_decay_but_suppressed_operations_do_not()
        {
            var g=Match(out var a,out var b,out var ai);a.SetHealthExact(2);a.ApplyStatus(StatusEffect.Decay);
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            int normal=AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,3,HandEffectType.None,0,0,true));
            int pure=AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,3,HandEffectType.None,0,0,true,effectsSuppressed:true));
            Assert.IsTrue(normal<-400);Assert.IsTrue(pure>-400);
        }
        [Test] public void Status_damage_consumes_the_last_shield_before_the_resonance_tick()
        {
            var g=Match(out var a,out var b,out var ai);a.SetHealthExact(1);a.ApplyStatus(StatusEffect.Decay);a.ApplyStatus(StatusEffect.Resonance);
            a.RightHand.SetValue(5);a.RightHand.SetShieldState(ShieldState.Formed);
            var move=new LegalMove(a,a.LeftHand,b.LeftHand);
            int spent=AiStatePolicy.ScoreMove(new AiStateContext(a,b,g,move,3,HandEffectType.None,0,0,true));
            Assert.IsTrue(spent<-400,"The only remaining shield blocks decay, then resonance is lethal.");
            int renewed=AiStatePolicy.ScoreMove(new AiStateContext(a,a,g,move,5,HandEffectType.Shield,0,0,true));
            Assert.IsTrue(renewed>-400,"After surviving decay, the newly formed shield can block resonance.");
            Assert.IsTrue(a.RightHand.IsShield);Assert.AreEqual(1,a.Health);
        }
        [Test] public void Counterplay_suppresses_dizzy_status_damage_until_a_prepared_portent_restores_effects()
        {
            var actor=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfDivination);
            var foe=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var ai=new GreedyChoiceProvider{DenyRatioPercent=100};
            var g=new GameManager(actor,foe,ai,new SimpleChoiceProvider());ai.Game=g;
            actor.LeftHand.SetValue(7);actor.RightHand.SetValue(1);foe.LeftHand.SetValue(2);foe.RightHand.SetValue(2);
            foe.SetHealthExact(1);foe.ApplyStatus(StatusEffect.Dizzy);foe.ApplyStatus(StatusEffect.Decay);
            Assert.IsTrue(ai.ProjectedCounterplayScoreForTest(new LegalMove(actor,actor.LeftHand,foe.LeftHand),actor,g)<0);
            var method=typeof(GreedyChoiceProvider).GetMethod("ProjectedOpportunityValue",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            object[] args={foe,actor,2,3,2,g,foe.LeftHand,actor.LeftHand};
            int suppressed=(int)method.Invoke(ai,args);Assert.IsTrue(suppressed>=0);
            CharacterBehaviors.FirstImplementer<FingerDiceDuel.Characters.SpecialLogics.SchoolOfDivinationLogic>(actor).RecordPortent(3);
            WizardChoice choice=null;g.DeferWizardChoice=c=>{choice=c;return true;};
            g.RequestPortentOperation(foe,foe.LeftHand,actor.LeftHand,()=>{});Assert.IsTrue(choice!=null);choice.ChooseOption(3);
            int prepared=(int)method.Invoke(ai,args);Assert.IsTrue(prepared<suppressed-100);
            Assert.AreEqual(1,foe.Health);Assert.AreEqual(2,foe.LeftHand.Value);
        }
        private sealed class TypedAttacker:CharacterTemplate,IAttackTypeProvider
        {
            private readonly AttackDamageType _type;
            public TypedAttacker(AttackDamageType type){_type=type;}
            public override AttackDamageType AttackTypeOf(AttackContext ctx)=>_type;
        }
        [Test] public void Lethal_forecast_applies_blindness_to_the_matching_damage_type_only()
        {
            foreach(var type in new[]{AttackDamageType.Slashing,AttackDamageType.Fire})
            {
                var a=new PlayerState("A",new TypedAttacker(type));var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;
                var move=new LegalMove(a,a.LeftHand,b.LeftHand);a.ApplyStatus(StatusEffect.Blind);
                Assert.AreEqual(type==AttackDamageType.Fire?3:6,ai.TotalLethalDamage(move,a,b,6,g));
                a.RemoveStatus(StatusEffect.Blind);a.ApplyStatus(StatusEffect.DarknessBlind);
                Assert.AreEqual(type==AttackDamageType.Slashing?3:6,ai.TotalLethalDamage(move,a,b,6,g));
                a.RemoveStatus(StatusEffect.DarknessBlind);a.ApplyStatus(StatusEffect.Annihilation);
                Assert.AreEqual(3,ai.TotalLethalDamage(move,a,b,6,g));
            }
        }
        [Test] public void New_light_dark_states_forecast_operation_damage_without_mutation()
        {
            var g=Match(out var a,out var b,out var ai);a.ApplyStatus(StatusEffect.DivinePunishment);
            Assert.AreEqual(6,AiStatePolicy.OperationSelfDamage(a,6,true));
            Assert.AreEqual(0,AiStatePolicy.OperationSelfDamage(a,9,false));
            a.RemoveStatus(StatusEffect.DivinePunishment);a.ApplyStatus(StatusEffect.Annihilation);
            Assert.AreEqual(9,AiStatePolicy.OperationSelfDamage(a,9,false));
            Assert.AreEqual(4,AiStatePolicy.OperationSelfDamage(a,4,true));Assert.AreEqual(20,a.Health);
        }
        [Test] public void Evocation_prefers_reaction_and_changes_type_when_repeat_cost_is_worse()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfEvocation);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());ai.Game=g;
            var options=new System.Collections.Generic.List<SkillChoiceOption>{new SkillChoiceOption((int)DamageType.Fire,"火",""),new SkillChoiceOption((int)DamageType.Cold,"冷","")};
            b.ApplyStatus(StatusEffect.Wet);
            Assert.AreEqual((int)DamageType.Cold,ai.ChooseSkillOption("evocation",options,a,b,g));
        }
    }
}
