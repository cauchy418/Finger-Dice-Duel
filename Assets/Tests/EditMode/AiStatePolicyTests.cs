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
        [Test] public void Decay_does_not_prefer_a_zero_heal_over_a_new_shield()
        {
            var g=Match(out var a,out var b,out var ai);a.SetHealthExact(2);a.ApplyStatus(StatusEffect.Decay);a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);b.RightHand.SetValue(4);
            Assert.IsTrue(ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.RightHand),a,g)>ai.ScoreMoveForTest(new LegalMove(a,a.LeftHand,b.LeftHand),a,g));
        }
        [Test] public void Every_current_status_has_an_explicit_rule_and_effect_extension_keys_exist()
        {
            int negatives=0;foreach(var status in StatusNames.AllStatuses){Assert.IsTrue(AiStatePolicy.Rules.ContainsKey(AiStatePolicy.KeyOf(status)),status.ToString());if(StatusNames.IsNegativeStatus(status))negatives++;}
            Assert.AreEqual(25,negatives);
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
        [Test] public void Evocation_abandon_generator_reaches_the_full_hundred_point_range()
        {
            var ai=new GreedyChoiceProvider();var roll=typeof(GreedyChoiceProvider).GetMethod("SchoolOfEvocationRoll",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);var values=new System.Collections.Generic.HashSet<int>();
            for(int i=0;i<2000;i++){int n=(int)roll.Invoke(ai,null);Assert.IsTrue(n>=0&&n<100);values.Add(n);}
            Assert.IsTrue(values.Count>90);
        }
    }
}
