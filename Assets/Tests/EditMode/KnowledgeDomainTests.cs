using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class KnowledgeDomainTests
    {
        private static CharacterDefinition Definition=>CharacterDefinition.BuiltIn.FirstOrDefault(d=>d.Id=="cleric_knowledge_domain");
        private const StatusEffect Blessing=(StatusEffect)28,Confusion=(StatusEffect)29;
        [Test] public void Knowledge_domain_is_in_roster()
        {Assert.IsTrue(Definition!=null);}
        [Test] public void Carry_gains_permanent_knowledge_without_preview_side_effects()
        {
            Assert.IsTrue(Definition!=null);var a=CharacterFactory.CreatePlayer("A",Definition);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.LeftHand.SetValue(8);b.LeftHand.SetValue(3);
            g.PreviewOperationResult(a,a.LeftHand,b.LeftHand);Assert.IsFalse(a.HasStatus(Blessing));
            g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.IsTrue(a.HasStatus(Blessing));
            a.TickStatuses();a.TickStatuses();Assert.IsTrue(a.HasStatus(Blessing));
        }
        [Test] public void Confusion_changes_carry_to_one_in_preview_and_execution()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.LeftHand.SetValue(8);b.LeftHand.SetValue(6);a.ApplyStatus(Confusion);
            Assert.AreEqual(1,g.PreviewOperationResult(a,a.LeftHand,b.LeftHand));g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(1,a.LeftHand.Value);Assert.AreEqual(20,b.Health);
        }
        [Test] public void Holy_fire_removes_knowledge_and_confusion()
        {
            var p=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);p.ApplyStatus(Blessing,int.MaxValue);p.ApplyStatus(Confusion);p.ApplyStatus(StatusEffect.HolyLight);
            Assert.IsFalse(p.HasStatus(Blessing));Assert.IsFalse(p.HasStatus(Confusion));
        }
        [Test] public void Shield_blocks_normal_hit_but_not_independent_knowledge_force()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.GainKnowledge();a.GainKnowledge();b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Formed);
            new AttackContext(a,b,a.LeftHand,6,g).Resolve(g.GetChoiceProvider(b),g);
            Assert.AreEqual(18,b.Health);Assert.AreEqual(0,a.KnowledgeStacks);Assert.IsTrue(b.HasStatus(Confusion));
        }
        [Test] public void Multihit_consumes_layers_only_once()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);a.GainKnowledge();a.GainKnowledge();
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());var attack=new AttackContext(a,b,a.LeftHand,6,g){HitCount=2};attack.Resolve(g.GetChoiceProvider(b),g);
            Assert.AreEqual(12,b.Health);Assert.AreEqual(0,a.KnowledgeStacks);
        }
        [Test] public void Force_immunity_blocks_knowledge_damage_and_confusion()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);a.GainKnowledge();b.AddPermanentImmunity(AttackDamageType.Force);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());new AttackContext(a,b,a.LeftHand,4,g).Resolve(g.GetChoiceProvider(b),g);
            Assert.AreEqual(16,b.Health);Assert.IsFalse(b.HasStatus(Confusion));
        }
        [Test] public void Healing_grants_other_role_one_layer_and_cleanse_preserves_it()
        {
            var a=CharacterFactory.CreatePlayer("A",Definition);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var ca=new SimpleChoiceProvider();var g=new GameManager(a,b,ca,new SimpleChoiceProvider());
            a.LeftHand.SetValue(8);b.LeftHand.SetValue(1);ca.SetChoice(HandEffect.LifeDomainHealTarget,b.LeftHand);g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(1,b.KnowledgeStacks);b.ClearNegativeStatuses();Assert.AreEqual(1,b.KnowledgeStacks);
        }
        [Test] public void Dodge_consumes_knowledge_but_prevents_force_and_confusion()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Swashbuckler);var cb=new SimpleChoiceProvider();var g=new GameManager(a,b,new SimpleChoiceProvider(),cb);
            a.GainKnowledge();b.LeftHand.SetValue(1);cb.SetChoice(HandEffect.SwashbucklerDodge,b.LeftHand);
            new AttackContext(a,b,a.LeftHand,4,g).Resolve(cb,g);Assert.AreEqual(20,b.Health);Assert.AreEqual(0,a.KnowledgeStacks);Assert.IsFalse(b.HasStatus(Confusion));
        }
        [Test] public void Independent_force_does_not_count_as_a_second_initiated_attack()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.OathOfConquest);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);
            var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());a.RightHand.SetValue(5);a.GainKnowledge();
            new AttackContext(a,b,a.LeftHand,4,g).Resolve(g.GetChoiceProvider(b),g);
            Assert.AreEqual(1,((FingerDiceDuel.Characters.SpecialLogics.OathOfConquestLogic)a.Character).AttackStacks);
        }
    }
}
