using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class RoleRefreshAiTests
    {
        [Test] public void Blessed_nine_keeps_healing_against_immune_target()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.WarDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());
            a.ApplyStatus(StatusEffect.WarBlessing,2);a.SetHealthExact(14);
            b.AddPermanentImmunity(AttackDamageType.Bludgeoning);
            Assert.AreEqual(0,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,b,g));
        }
        [Test] public void Blessed_nine_does_not_trade_healing_for_cancelled_attack()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.WarDomain);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.TheArchfey);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());
            a.ApplyStatus(StatusEffect.WarBlessing,2);a.SetHealthExact(16);b.LeftHand.SetValue(9);
            Assert.AreEqual(0,ai.ChooseSkillOption(DivineChoices.Nine,DivineChoices.NineOptions(),a,b,g));
            Assert.AreEqual(9,b.LeftHand.Value);Assert.AreEqual(0,((TheArchfeyLogic)b.Character).CancelCount);
        }
        [Test] public void Giant_preserves_five_health_without_elemental_upgrade()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.PathOfTheGiant);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());
            Assert.AreEqual(0,ai.ChooseSkillOption("giant_element_blade",ArcaneChoices.Elements(),a,b,g));
            Assert.AreEqual(25,a.Health);Assert.AreEqual(AttackDamageType.None,((PathOfTheGiantLogic)a.Character).Enchantment);
        }
        [Test] public void Giant_buys_element_to_bypass_physical_immunity()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.PathOfTheGiant);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());
            b.AddPermanentImmunity(AttackDamageType.Slashing);
            Assert.IsTrue(ai.ChooseSkillOption("giant_element_blade",ArcaneChoices.Elements(),a,b,g)!=0);
        }
        [Test] public void Diviner_keeps_portent_when_enemy_attack_is_disabled()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfDivination);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var ai=new GreedyChoiceProvider();var g=new GameManager(a,b,ai,new SimpleChoiceProvider());
            g.SkillPolicyFor=p=>ai;
            var book=(FingerDiceDuel.Characters.SpecialLogics.SchoolOfDivinationLogic)a.Character;
            book.RecordPortent(1);a.LeftHand.SetValue(7);b.LeftHand.SetValue(1);b.ApplyStatus(StatusEffect.Disabled);
            g.ExecuteTurn(b,b.LeftHand,a.LeftHand);
            Assert.AreEqual(1,book.Portents.Count);Assert.AreEqual(8,b.LeftHand.Value);Assert.AreEqual(20,a.Health);
        }
        [Test] public void Element_preview_does_not_advance_psychic_status_random()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);
            var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var method=typeof(GreedyChoiceProvider).GetMethod("ElementDamageScore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            StatusRules.ResetInteractionRandomForTest(137);
            var expected=new StatusEffect[12];
            for(int i=0;i<expected.Length;i++)StatusRules.ApplyDamageTypeInteraction(b,DamageType.Psychic,6,out expected[i],out _);
            StatusRules.ResetInteractionRandomForTest(137);
            method.Invoke(null,new object[]{AttackDamageType.Psychic,a,b,false});
            for(int i=0;i<expected.Length;i++)
            {
                StatusRules.ApplyDamageTypeInteraction(b,DamageType.Psychic,6,out var actual,out _);
                Assert.AreEqual(expected[i],actual);
            }
        }
        [Test] public void Ordinary_cleric_does_not_value_cleansing_it_cannot_perform()
        {
            foreach(var definition in new[]{CharacterDefinition.WarDomain,CharacterDefinition.TrickeryDomain,CharacterDefinition.TempestDomain,CharacterDefinition.KnowledgeDomain,CharacterDefinition.LightDomain})
            {
                var a=CharacterFactory.CreatePlayer("A",definition);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
                var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Blank);var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Blank);
                var g=GameManager.Create2v2(new[]{a,b,c,d},new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});
                a.ApplyStatus(StatusEffect.Frozen);a.ApplyStatus(StatusEffect.Disabled);d.SetHealthExact(18);
                var advisor=(IHealTargetAdvisor)a.Character;
                Assert.AreEqual(definition==CharacterDefinition.LightDomain?a:d,advisor.BestHealTarget(a,g));
                Assert.IsTrue(a.HasStatus(StatusEffect.Frozen));Assert.AreEqual(18,d.Health);
            }
        }
    }
}
