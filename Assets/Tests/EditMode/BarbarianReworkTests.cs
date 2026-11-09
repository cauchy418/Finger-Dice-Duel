using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture] public class BarbarianReworkTests
    {
        private static CharacterDefinition[] Roles=>new[]{CharacterDefinition.PathOfTheWarden,CharacterDefinition.PathOfTheBeast,CharacterDefinition.PathOfTheGiant,CharacterDefinition.PathOfTheBerserker};
        [Test] public void All_barbarians_have_the_shared_base_panel_and_slashing_type()
        {
            foreach(var d in Roles)
            {
                var p=CharacterFactory.CreatePlayer("A",d);
                Assert.AreEqual(-1,d.AttackBonus);Assert.AreEqual(-1,d.DefenseBonus);Assert.AreEqual(25,p.MaximumHealth);
                Assert.AreEqual(AttackDamageType.Slashing,((IAttackTypeProvider)p.Character).AttackTypeOf(new AttackContext(p,p,p.LeftHand,4)));
            }
        }
        [Test] public void Missing_health_bonus_changes_immediately_when_damage_or_healing_changes_it()
        {
            foreach(var d in Roles)
            {
                var p=CharacterFactory.CreatePlayer("A",d);p.SetHealthExact(15);
                Assert.AreEqual(1,StatusRules.EffectiveAttackBonus(p));Assert.AreEqual(1,StatusRules.EffectiveDefenseBonus(p));
                p.SetHealthExact(5);Assert.AreEqual(3,StatusRules.EffectiveAttackBonus(p));
                p.Heal(11);Assert.AreEqual(-1,StatusRules.EffectiveAttackBonus(p));
            }
        }
        private static GameManager Match(CharacterDefinition d,out PlayerState a,out PlayerState b)
        {a=CharacterFactory.CreatePlayer("A",d);b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);return new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());}
        [Test] public void Double_barbarian_has_one_health_bonus_and_one_missing_health_bonus()
        {
            var a=DualGeneralFactory.Create("A",CharacterDefinition.PathOfTheWarden,CharacterDefinition.PathOfTheGiant);
            Assert.AreEqual(25,a.MaximumHealth);int baseline=StatusRules.EffectiveAttackBonus(a);a.SetHealthExact(15);
            Assert.AreEqual(baseline+2,StatusRules.EffectiveAttackBonus(a));
        }
        [Test] public void Warden_oath_clears_attached_statuses_but_keeps_independent_knowledge_damage()
        {
            var g=Match(CharacterDefinition.PathOfTheWarden,out var w,out var source);w.SetHealthExact(15);w.LeftHand.SetValue(5);w.LeftHand.SetShieldState(ShieldState.Formed);source.GainKnowledge();source.GainKnowledge();
            g.DeferWizardChoice=r=>{r.ChooseOption(1);return true;};
            var attack=new AttackContext(source,w,source.LeftHand,4,g){AttackType=AttackDamageType.Fire,AttackerStatusOverride=StatusEffect.Burn,InflictsBleed=true};attack.Resolve(g.GetChoiceProvider(w),g);
            Assert.AreEqual(AttackDamageType.Force,attack.AttackType);Assert.AreEqual(9,w.Health);Assert.IsTrue(w.LeftHand.IsShield);
            Assert.IsFalse(w.HasStatus(StatusEffect.Burn));Assert.IsFalse(w.HasStatus(StatusEffect.Bleed));Assert.IsTrue(w.HasStatus(StatusEffect.Confusion));
        }
        [Test] public void Beast_accumulates_soul_then_heals_and_grows_at_turn_end()
        {
            var g=Match(CharacterDefinition.PathOfTheBeast,out var a,out var b);a.SetHealthExact(15);a.LeftHand.SetValue(1);a.RightHand.SetValue(5);b.LeftHand.SetValue(1);
            var beast=(FingerDiceDuel.Characters.SpecialLogics.PathOfTheBeastLogic)a.Character;g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.AreEqual(1,beast.BeastSouls);Assert.AreEqual(0,beast.TriggerCount);Assert.AreEqual(15,a.Health);
            g.ResolveStatusTurnEnd(a);Assert.AreEqual(0,beast.BeastSouls);Assert.AreEqual(1,beast.TriggerCount);Assert.AreEqual(20,a.Health);Assert.AreEqual(0,a.Character.AttackBonus);
        }
        [Test] public void Giant_high_health_has_ordinary_numbers_and_low_health_has_double_five_attack()
        {
            var g=Match(CharacterDefinition.PathOfTheGiant,out var a,out var b);a.LeftHand.SetValue(1);b.LeftHand.SetValue(3);
            Assert.AreEqual(3,g.ExecuteTurn(a,a.LeftHand,b.LeftHand).DamageDealt);
            g=Match(CharacterDefinition.PathOfTheGiant,out a,out b);a.SetHealthExact(15);a.LeftHand.SetValue(2);a.RightHand.SetValue(3);b.LeftHand.SetValue(3);
            var r=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(2,r.HitCount);Assert.AreEqual(8,r.DamageDealt);Assert.IsFalse(a.LeftHand.IsShield);
        }
        [Test] public void Giant_nine_heals_twice_and_keeps_threshold_snapshot_between_heals()
        {
            var g=Match(CharacterDefinition.PathOfTheGiant,out var a,out var b);a.SetHealthExact(15);a.LeftHand.SetValue(8);a.RightHand.SetValue(3);b.LeftHand.SetValue(1);a.ApplyStatus(StatusEffect.Scorch);
            var r=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(6,r.ActualHealed);Assert.AreEqual(17,a.Health);
        }
        [Test] public void Berserker_low_health_five_attacks_and_leech_uses_actual_health_damage()
        {
            var g=Match(CharacterDefinition.PathOfTheBerserker,out var a,out var b);a.SetHealthExact(15);a.LeftHand.SetValue(2);b.LeftHand.SetValue(3);b.SetHealthExact(3);
            var r=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.IsTrue(r.DamageDealt>0);Assert.AreEqual(16,a.Health);
        }
        [Test] public void Giant_blade_pays_health_without_buffers_and_is_consumed_by_cancel()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.PathOfTheGiant);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.TheArchfey);var cb=new SimpleChoiceProvider();var g=new GameManager(a,b,new SimpleChoiceProvider(),cb);
            a.GainSporeLife();a.GainFalseLife(4);g.DeferWizardChoice=r=>{r.ChooseOption((int)AttackDamageType.Fire);return true;};
            ((PathOfTheGiantLogic)a.Character).OnTurnStarted(a,g);Assert.AreEqual(20,a.Health);Assert.AreEqual(10,a.SporeLife);Assert.AreEqual(4,a.FalseLife);
            b.RightHand.SetValue(4);cb.SetChoice(HandEffect.TheArchfeyCancel,b.RightHand);var attack=new AttackContext(a,b,a.LeftHand,4,g);attack.Resolve(cb,g);
            Assert.IsTrue(attack.WasCancelled);Assert.AreEqual(AttackDamageType.None,((PathOfTheGiantLogic)a.Character).Enchantment);
        }
        [Test] public void Bound_low_health_displays_update_without_a_match_or_reset()
        {
            foreach(var d in new[]{CharacterDefinition.PathOfTheGiant,CharacterDefinition.PathOfTheBerserker})
            {
                var p=CharacterFactory.CreatePlayer("A",d);
                Assert.AreEqual(HandEffectType.Shield,CharacterBehaviors.DisplayedEffectOf(p,5));
                p.SetHealthExact(15);Assert.IsTrue(CharacterBehaviors.DisplayedEffectOf(p,5)!=HandEffectType.Shield);
                p.Heal(1);Assert.AreEqual(HandEffectType.Shield,CharacterBehaviors.DisplayedEffectOf(p,5));
            }
        }
        [Test] public void Warden_human_board_asks_after_target_and_resumes_once()
        {
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.Blank);var w=CharacterFactory.CreatePlayer("W",CharacterDefinition.PathOfTheWarden);
            var g=new GameManager(a,w,ca,cb);var board=new BoardController(g,ca,cb);
            w.SetHealthExact(15);a.LeftHand.SetValue(3);w.LeftHand.SetValue(1);
            board.BeginTurn();Assert.IsTrue(board.ClickHand(a.LeftHand));Assert.IsTrue(board.ClickHand(w.LeftHand));
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer,board.Phase);board.ConfirmTargetPlayer();
            Assert.IsTrue(board.Hint.Contains("守望誓约"));Assert.AreEqual(2,board.PendingSkillOptions.Count);
            Assert.AreEqual(0,g.TurnCount);board.ChooseSkillOption(1);
            Assert.AreEqual(1,g.TurnCount);Assert.AreEqual(11,w.Health);
        }
        [Test] public void Beast_waits_for_enough_souls_and_growth_uses_missing_health_not_maximum()
        {
            var g=Match(CharacterDefinition.PathOfTheBeast,out var a,out var b);a.SetHealthExact(5);
            var beast=(FingerDiceDuel.Characters.SpecialLogics.PathOfTheBeastLogic)a.Character;
            a.LeftHand.SetValue(4);a.RightHand.SetValue(3);
            for(int i=0;i<2;i++){beast.OnOperationResolved(a,a.LeftHand,1,3,HandEffectType.Attack4,g,null);beast.OnTurnEnded(a,g);}
            Assert.AreEqual(2,beast.BeastSouls);Assert.AreEqual(5,a.Health);Assert.AreEqual(0,beast.TriggerCount);
            beast.OnOperationResolved(a,a.LeftHand,1,3,HandEffectType.Attack4,g,null);beast.OnTurnEnded(a,g);
            Assert.AreEqual(15,a.Health);Assert.AreEqual(0,beast.BeastSouls);Assert.AreEqual(1,beast.TriggerCount);
        }
        [Test] public void Giant_cannot_pay_the_last_five_health_for_element_blade()
        {
            var g=Match(CharacterDefinition.PathOfTheGiant,out var a,out var b);a.SetHealthExact(5);int prompts=0;
            g.DeferWizardChoice=r=>{prompts++;r.ChooseOption((int)AttackDamageType.Fire);return true;};
            ((PathOfTheGiantLogic)a.Character).OnTurnStarted(a,g);
            Assert.AreEqual(0,prompts);Assert.AreEqual(5,a.Health);
        }
        [Test] public void Warden_oath_also_clears_untimed_attack_markers()
        {
            var g=Match(CharacterDefinition.PathOfTheWarden,out var w,out var source);w.SetHealthExact(15);
            g.DeferWizardChoice=r=>{r.ChooseOption(1);return true;};
            var attack=new AttackContext(source,w,source.LeftHand,4,g);
            attack.ApplyUntimedOnHit(()=>w.SetHiddenForce(source));attack.Resolve(g.GetChoiceProvider(w),g);
            Assert.IsFalse(w.HasResonance);
        }
        [Test] public void Warden_reflection_waits_for_human_oath_before_damage_and_turn_end()
        {
            var a=CharacterFactory.CreatePlayer("A",CharacterDefinition.PathOfTheWarden);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.WayOfTheTaiChi);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var g=new GameManager(a,b,ca,cb);var board=new BoardController(g,ca,cb);
            a.SetHealthExact(15);a.LeftHand.SetValue(3);b.LeftHand.SetValue(1);b.RightHand.SetValue(5);b.RightHand.SetShieldState(ShieldState.Taichi);
            board.BeginTurn();board.ClickHand(a.LeftHand);board.ClickHand(b.LeftHand);board.ConfirmTargetPlayer();
            Assert.IsTrue(board.ClickHand(b.RightHand));if(board.PendingSkillOptions.Count>0&&!board.Hint.Contains("守望誓约"))board.ChooseSkillOption(0);
            Assert.IsTrue(board.Hint.Contains("守望誓约"));Assert.AreEqual(15,a.Health);Assert.AreEqual(0,g.TurnCount);
            board.ChooseSkillOption(1);Assert.AreEqual(13,a.Health);Assert.AreEqual(1,g.TurnCount);
        }
        [Test] public void Giant_blade_takes_priority_over_dual_wild_magic_in_both_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=DualGeneralFactory.Create("A",reverse?CharacterDefinition.WildMagic:CharacterDefinition.PathOfTheGiant,reverse?CharacterDefinition.PathOfTheGiant:CharacterDefinition.WildMagic);
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                a.SetHealthExact(15);g.DeferWizardChoice=r=>{r.ChooseOption((int)AttackDamageType.Fire);return true;};
                CharacterBehaviors.FirstImplementer<PathOfTheGiantLogic>(a).OnTurnStarted(a,g);
                CharacterBehaviors.FirstImplementer<FingerDiceDuel.Characters.SpecialLogics.WildMagicLogic>(a).ForcedRoll=72;
                a.LeftHand.SetValue(2);a.RightHand.SetValue(3);b.LeftHand.SetValue(3);
                var hit=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                Assert.AreEqual(2,hit.HitCount);Assert.AreEqual(AttackDamageType.Fire,hit.SegmentTypes[0]);Assert.AreEqual(AttackDamageType.Fire,hit.SegmentTypes[1]);
                Assert.IsTrue(b.HasStatus(StatusEffect.Burn));Assert.AreEqual(AttackDamageType.None,CharacterBehaviors.FirstImplementer<PathOfTheGiantLogic>(a).Enchantment);
            }
        }
    }
}
