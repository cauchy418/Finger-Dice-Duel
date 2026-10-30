using System.Linq;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class CombatVisualTests
    {
        private static GameManager Match(CharacterDefinition a=null,CharacterDefinition b=null)
        {var g=CharacterFactory.CreateMatch(a??CharacterDefinition.Blank,b??CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.RecordCombatVisuals=true;return g;}
        [Test] public void Frames_are_copied_and_recording_does_not_change_damage()
        {var g=Match();var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);c.Resolve(g.GetChoiceProvider(g.PlayerB),g);var events=g.DrainCombatVisuals();Assert.AreEqual(1,events.Count);Assert.AreEqual(20,events[0].Before.Players[1].Health);Assert.AreEqual(16,events[0].After.Players[1].Health);g.PlayerB.LeftHand.SetValue(8);Assert.AreEqual(1,events[0].Before.Players[1].Left.Value);events[0].Before.Players[1].Left.SetValue(9);Assert.AreEqual(1,events[0].Before.Players[1].Left.Value);Assert.AreEqual(0,g.DrainCombatVisuals().Count);}
        [Test] public void Blocked_then_hit_preserves_segment_and_break_order()
        {var g=Match();g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,8,g){HitCount=2};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);var e=g.DrainCombatVisuals();Assert.AreEqual(3,e.Count);Assert.AreEqual(CombatVisualOutcome.Blocked,e[0].Outcome);Assert.AreEqual(CombatVisualKind.ShieldBreak,e[1].Kind);Assert.AreEqual(CombatVisualOutcome.Hit,e[2].Outcome);Assert.AreEqual(1,e[2].Segment);Assert.AreEqual(4,e[2].Damage);}
        [Test] public void Devotion_protection_and_shock_keep_distinct_targets_and_order()
        {var p=new[]{CharacterFactory.CreatePlayer("paladin",CharacterDefinition.OathOfDevotion),CharacterFactory.CreatePlayer("enemy",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("other",CharacterDefinition.Blank),CharacterFactory.CreatePlayer("ally",CharacterDefinition.Blank)};var g=GameManager.Create2v2(p,new IChoiceProvider[]{new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider(),new SimpleChoiceProvider()});g.RecordCombatVisuals=true;p[0].RightHand.SetShieldState(ShieldState.Formed);g.AnsweredLookupFor=(o,e,v)=>ReferenceEquals(o,p[0])?e==HandEffect.OathOfDevotionRedirect?p[0].LeftHand:e==HandEffect.OathOfDevotionShockTarget?p[1].LeftHand:null:null;var c=new AttackContext(p[1],p[3],p[1].LeftHand,4,g);c.Resolve(g.GetChoiceProvider(p[3]),g);var events=g.DrainCombatVisuals();Assert.AreEqual("protection",events[0].RedirectKind);Assert.AreEqual(p[0],events[0].Target);Assert.AreEqual(p[3],events[0].IntendedTarget);Assert.AreEqual(CombatVisualKind.ShieldBreak,events[1].Kind);Assert.AreEqual("fx_radiant",events[2].Weapon);Assert.AreEqual(2,events[2].Damage);}
        [Test] public void Fey_cancellation_records_its_caster_and_never_deals_damage()
        {var g=Match(b:CharacterDefinition.TheArchfey);g.PlayerB.LeftHand.SetValue(4);var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g);c.Resolve(g.GetChoiceProvider(g.PlayerB),g);var e=g.DrainCombatVisuals();Assert.AreEqual(CombatVisualOutcome.Cancelled,e[0].Outcome);Assert.AreEqual(g.PlayerB,e[0].EvasionActor);Assert.AreEqual(20,g.PlayerB.Health);}
        [Test] public void Physical_and_elemental_carriers_route_without_random_rolls()
        {foreach(var d in new[]{CharacterDefinition.LifeDomain,CharacterDefinition.SchoolOfEvocation,CharacterDefinition.OathOfDevotion,CharacterDefinition.WayOfTheTaiChi}){var g=Match(a:d);var a=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4,g){AttackType=AttackDamageType.Bludgeoning};Assert.AreEqual(d==CharacterDefinition.WayOfTheTaiChi?"weapon_fist":d==CharacterDefinition.OathOfDevotion?"weapon_hammer":"weapon_staff",CombatVisualRouting.Weapon(a,g));a.AttackType=AttackDamageType.Fire;Assert.AreEqual("fx_fire",CombatVisualRouting.Weapon(a,g));a.VisualWeapon="weapon_sword";Assert.AreEqual("weapon_sword",CombatVisualRouting.Weapon(a,g));}}
        [Test] public void Vow_movement_is_a_mark_event_instead_of_an_attack_redirect()
        {var g=Match();g.RecordVowTransfer(g.PlayerA,g.PlayerA,g.PlayerB,g.CaptureVisualFrame());var e=g.DrainCombatVisuals()[0];Assert.AreEqual(CombatVisualKind.VowTransfer,e.Kind);Assert.AreEqual("Effects/vow_of_enmity",e.Weapon);Assert.AreEqual(0,e.Damage);Assert.AreEqual(20,g.PlayerB.Health);}
    }
}
