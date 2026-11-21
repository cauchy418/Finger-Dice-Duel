using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class BurningReactionTests
    {
        private static GameManager Game()
        {var g=CharacterFactory.CreateMatch(CharacterDefinition.Blank,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());g.PlayerB.SetMaxHealth(200);g.PlayerB.SetHealthExact(200);return g;}
        private static AttackContext Fire(GameManager g,int hits=1)
        {var c=new AttackContext(g.PlayerA,g.PlayerB,g.PlayerA.LeftHand,4){AttackType=AttackDamageType.Fire,IgnoreStatBonuses=true,HitCount=hits,AttackerStatusOverride=StatusEffect.Burn};c.Resolve(g.GetChoiceProvider(g.PlayerB),g);return c;}
        [Test] public void Burning_deals_two_fire_at_turn_end()
        {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,2,g.PlayerA);g.ResolveStatusTurnEnd(g.PlayerB);Assert.AreEqual(198,g.PlayerB.Health);Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
        [Test] public void Fire_on_burning_applies_existing_scorch_and_refreshes_burn()
        {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,1,g.PlayerA);Fire(g);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Scorch));Assert.AreEqual(g.PlayerA,g.PlayerB.StatusSourceOf(StatusEffect.Scorch));Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
        [Test] public void First_fire_hit_only_burns()
        {var g=Game();Fire(g);Assert.True(g.PlayerB.HasStatus(StatusEffect.Burn));Assert.False(g.PlayerB.HasStatus(StatusEffect.Scorch));}
        [Test] public void Scorch_duration_obeys_guard_and_resistance()
        {for(int flags=0;flags<4;flags++){var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,2);if((flags&1)!=0){g.PlayerB.LeftHand.SetValue(5);g.PlayerB.LeftHand.SetShieldState(ShieldState.Formed);}if((flags&2)!=0)g.PlayerB.AddResistance(AttackDamageType.Fire);Fire(g);Assert.AreEqual(2-((flags&1)!=0?1:0)-((flags&2)!=0?1:0),g.PlayerB.StatusTurnsLeft(StatusEffect.Scorch));}}
        [Test] public void Fire_immunity_suppresses_reaction_even_when_consumed()
        {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,1);g.PlayerB.AddImmunity(AttackDamageType.Fire);Fire(g);Assert.False(g.PlayerB.HasStatus(StatusEffect.Scorch));Assert.AreEqual(1,g.PlayerB.StatusTurnsLeft(StatusEffect.Burn));}
        [Test] public void Status_fire_damage_does_not_react_with_burning()
        {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,2);g.ResolveStatusTurnEnd(g.PlayerB);Assert.False(g.PlayerB.HasStatus(StatusEffect.Scorch));}
        [Test] public void Repeated_reaction_refreshes_scorch_instead_of_stacking()
        {var g=Game();g.PlayerB.ApplyStatus(StatusEffect.Burn,2);g.PlayerB.ApplyStatus(StatusEffect.Scorch,1);Fire(g);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Scorch));}
        [Test] public void Second_fire_segment_reacts_with_first_segments_burning()
        {var g=Game();Fire(g,2);Assert.AreEqual(2,g.PlayerB.StatusTurnsLeft(StatusEffect.Scorch));}
        [Test] public void Element_ai_rewards_new_scorch_but_not_reapplication_or_immunity()
        {
            var g=Game();var method=typeof(GreedyChoiceProvider).GetMethod("ElementScore",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            System.Func<double> score=()=>(double)method.Invoke(null,new object[]{(int)AttackDamageType.Fire,g.PlayerA,g.PlayerB,g});
            g.PlayerB.ApplyStatus(StatusEffect.Burn,2);g.PlayerB.ApplyStatus(StatusEffect.Scorch,2);double existingScorch=score();
            g.PlayerB.RemoveStatus(StatusEffect.Scorch);Assert.AreEqual(existingScorch+6,score());
            g.PlayerB.AddImmunity(AttackDamageType.Fire);Assert.AreEqual(-100d,score());
        }
    }
}
