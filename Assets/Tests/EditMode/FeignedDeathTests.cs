using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class FeignedDeathTests
    {
        private const StatusEffect Feign = (StatusEffect)30;
        private static GameManager Game() => CharacterFactory.CreateMatch(CharacterDefinition.Blank, CharacterDefinition.Blank,
            new SimpleChoiceProvider(), new SimpleChoiceProvider());

        [Test] public void Resistance_to_all_thirteen_types_persists_after_repeated_hits()
        {
            foreach(var type in ResistanceNames.All)
            {
                var g=Game();var p=g.PlayerB;p.ApplyStatus(Feign);
                for(int n=0;n<2;n++)
                {
                    var hit=new AttackContext(g.PlayerA,p,g.PlayerA.LeftHand,6){AttackType=type,IgnoreStatBonuses=true};
                    hit.Resolve(g.ChoiceProviderB,g);
                    Assert.AreEqual(3,hit.AppliedDamage,type.ToString());
                    Assert.IsTrue(p.HasResistance(type));
                }
            }
        }
        [Test] public void Direct_damage_and_health_costs_cannot_kill_until_status_expires()
        {
            var g=Game();var p=g.PlayerA;p.ApplyStatus(Feign);p.TakeDamage(1000);
            Assert.AreEqual(1,p.Health);Assert.IsTrue(p.IsAlive);
            p.SetHealthExact(-20);Assert.AreEqual(1,p.Health);
            p.TickStatuses();Assert.IsTrue(p.HasStatus(Feign));
            p.TickStatuses();Assert.IsFalse(p.HasStatus(Feign));
            p.TakeDamage(1);Assert.IsTrue(p.IsDead);
        }
        [Test] public void Holy_fire_clears_and_blocks_feigned_death()
        {
            var p=Game().PlayerA;p.ApplyStatus(Feign);p.ApplyStatus(StatusEffect.HolyLight);
            Assert.IsFalse(p.HasStatus(Feign));p.ApplyStatus(Feign);Assert.IsFalse(p.HasStatus(Feign));
            p.TakeDamage(1000);Assert.IsTrue(p.IsDead);
        }
        [Test] public void Pure_operation_never_attacks_heals_or_forms_shield()
        {
            foreach(int sum in new[]{4,5,9})
            {
                var g=Game();var p=g.PlayerA;p.SetHealthExact(10);p.ApplyStatus(Feign);
                p.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(sum-1);
                Assert.IsTrue(g.WillSuppressOperationEffects(p));
                var result=g.ExecuteTurn(p,p.LeftHand,g.PlayerB.LeftHand);
                Assert.AreEqual(sum,p.LeftHand.Value);Assert.AreEqual(HandEffectType.None,result.Effect);
                Assert.IsFalse(p.LeftHand.IsShield);Assert.AreEqual(10,p.Health);Assert.AreEqual(20,g.PlayerB.Health);
                Assert.AreEqual(1,g.TurnCount);Assert.AreSame(g.PlayerB,g.CurrentPlayer);
                g.ResolveStatusTurnEnd(p);Assert.AreEqual(1,p.StatusTurnsLeft(Feign));
            }
        }
        private sealed class Probe : CharacterTemplate
        {
            public int Calls;
            public override void OnBeforeEffectResolved(PlayerState p,Hand h,int v,HandEffectType e,GameManager g,IPreTurnChoiceSink s){Calls++;}
            public override void OnOperationResolved(PlayerState p,Hand h,int before,int v,HandEffectType e,GameManager g,IPreTurnChoiceSink s){Calls++;}
            public override void OnOwnShieldBroken(PlayerState p,Hand h,GameManager g){Calls++;}
        }
        [Test] public void Operation_does_not_trigger_character_hooks_even_when_spending_a_shield()
        {
            var g=Game();var p=g.PlayerA;var probe=new Probe();p.BindCharacter(probe);
            p.LeftHand.SetValue(5);p.LeftHand.SetShieldState(ShieldState.Formed);g.PlayerB.LeftHand.SetValue(3);p.ApplyStatus(Feign);
            g.ExecuteTurn(p,p.LeftHand,g.PlayerB.LeftHand);
            Assert.AreEqual(8,p.LeftHand.Value);Assert.AreEqual(0,probe.Calls);
        }
        [Test] public void Status_metadata_classifies_character_status_and_uses_dedicated_icons()
        {
            Assert.IsFalse(StatusNames.IsNegativeStatus(Feign));Assert.AreEqual("假死",StatusNames.Of(Feign));
            Assert.AreEqual("Status/feigned_death",StatusNames.ResourceKeyOf(Feign));
            Assert.AreEqual("生命祝福",StatusNames.Of(StatusEffect.BlessedHealing));
            Assert.AreEqual("Status/life_blessing",StatusNames.ResourceKeyOf(StatusEffect.BlessedHealing));
        }
        [Test] public void Healing_cleanse_preserves_character_status_and_persistent_resistance_help()
        {
            var p=Game().PlayerA;p.ApplyStatus(Feign);p.ApplyStatus(StatusEffect.Poison);p.ClearNegativeStatuses();
            Assert.AreEqual(2,p.StatusTurnsLeft(Feign));Assert.IsFalse(p.HasStatus(StatusEffect.Poison));
            foreach(var icon in CombatStatusIcons.For(p))
                if(icon.ResourceKey==ResistanceNames.Key(AttackDamageType.Fire))
                    Assert.IsTrue(icon.Description.Contains("假死"));
        }
        [Test] public void Board_pure_operation_finishes_without_attack_or_character_choices()
        {
            var g=Game();var p=g.PlayerA;p.ApplyStatus(Feign);p.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(3);
            var board=new BoardController(g,(SimpleChoiceProvider)g.ChoiceProviderA,(SimpleChoiceProvider)g.ChoiceProviderB);
            board.BeginTurn();Assert.IsTrue(board.ClickHand(p.LeftHand));Assert.IsTrue(board.ClickHand(g.PlayerB.LeftHand));
            Assert.AreEqual(4,p.LeftHand.Value);Assert.AreEqual(20,g.PlayerB.Health);Assert.AreEqual(1,g.TurnCount);
            Assert.AreEqual(1,p.StatusTurnsLeft(Feign));Assert.AreSame(g.PlayerB,board.ActivePlayer);
        }
        [Test] public void Attack_true_damage_and_status_damage_leave_one_health_and_never_end_match()
        {
            var g=Game();var p=g.PlayerB;p.SetHealthExact(2);p.ApplyStatus(Feign);
            var hit=new AttackContext(g.PlayerA,p,g.PlayerA.LeftHand,20)
                {AttackType=AttackDamageType.Force,IgnoreStatBonuses=true,ExtraTrueDamage=20};
            hit.Resolve(g.ChoiceProviderB,g);Assert.AreEqual(1,p.Health);Assert.AreEqual(1,hit.ActualHealthDamage);
            p.ApplyStatus(StatusEffect.Poison);p.ApplyStatus(StatusEffect.Burn);g.ResolveStatusTurnStart(p);g.ResolveStatusTurnEnd(p);
            Assert.AreEqual(1,p.Health);Assert.IsTrue(p.IsAlive);Assert.IsFalse(g.IsGameOver);
        }
        [Test] public void Other_roles_cannot_choose_feigned_hands_and_cleanse_keeps_same_source()
        {
            var g=Game();var p=g.PlayerB;p.ApplyStatus(Feign,2,g.PlayerA);
            Assert.IsFalse(g.IsLegalMove(g.PlayerA,g.PlayerA.LeftHand,p.LeftHand,out _));
            foreach(var move in g.GetLegalMoves(g.PlayerA))Assert.IsFalse(p.Owns(move.TargetHand));
            p.ClearNegativeStatuses();Assert.AreSame(g.PlayerA,p.StatusSourceOf(Feign));
        }
        [Test] public void Pure_operation_does_not_trigger_gamblers_next_turn_obligation()
        {
            var g=CharacterFactory.CreateMatch(CharacterDefinition.Gambler,CharacterDefinition.Blank,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            var p=g.PlayerA;p.ApplyStatus(Feign);p.LeftHand.SetValue(1);g.PlayerB.LeftHand.SetValue(1);
            var board=new BoardController(g,(SimpleChoiceProvider)g.ChoiceProviderA,(SimpleChoiceProvider)g.ChoiceProviderB);
            board.BeginTurn();board.ClickHand(p.LeftHand);board.ClickHand(g.PlayerB.LeftHand);
            Assert.AreEqual(1,g.TurnCount);Assert.IsFalse(p.MustGamble);
        }
    }
}


