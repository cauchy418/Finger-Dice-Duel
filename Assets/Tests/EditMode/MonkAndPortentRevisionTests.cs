using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Assistant;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class MonkAndPortentRevisionTests
    {
        private sealed class Policy:AbstractChoiceProvider
        {
            public readonly Dictionary<string,int> Answers=new Dictionary<string,int>();
            public override int ChooseSkillOption(string key,IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game)
                =>Answers.TryGetValue(key,out int value)?value:base.ChooseSkillOption(key,options,owner,target,game);
        }
        private static PlayerState P(CharacterDefinition role)=>CharacterFactory.CreatePlayer(role.DisplayName,role);
        private static GameManager Match(CharacterDefinition role,out PlayerState a,out PlayerState b,Policy policy=null)
        {a=P(role);b=P(CharacterDefinition.Blank);return new GameManager(a,b,(IChoiceProvider)policy??new SimpleChoiceProvider(),new SimpleChoiceProvider());}
        [Test] public void All_five_monks_share_base_stats_and_initial_resource()
        {
            foreach(var role in CharacterDefinition.BuiltIn)if(role.Profession.Id=="monk")
            {var a=P(role);Assert.AreEqual(0,a.Character.AttackBonus);Assert.AreEqual(0,a.Character.DefenseBonus);Assert.AreEqual(1,GameManager.BaseRangeOf(a));Assert.AreEqual(2,a.MonkKi);Assert.AreEqual(AttackDamageType.Bludgeoning,((IAttackTypeProvider)a.Character).AttackTypeOf(null));Assert.IsTrue(CharacterCodex.For(role).UsesNormalizedSkills);}
        }
        [Test] public void Dual_monks_share_one_pool_and_one_icon_and_gain_once_after_action()
        {
            var a=DualGeneralFactory.Create("双僧",CharacterDefinition.WayOfTheOpenHand,CharacterDefinition.WayOfTheNinja);var b=P(CharacterDefinition.Blank);
            var policy=new Policy();policy.Answers[MartialSkillChoices.OpenHand]=0;policy.Answers[MonkRules.Ninja]=0;
            var g=new GameManager(a,b,policy,new SimpleChoiceProvider());a.LeftHand.SetValue(1);a.RightHand.SetValue(4);b.LeftHand.SetValue(3);
            g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(3,a.MonkKi);
            foreach(var ki in CharacterBehaviors.Implementers<IKiResource>(a))Assert.AreEqual(3,ki.Ki);
            Assert.AreEqual(1,CombatStatusIcons.For(a,g).FindAll(i=>i.ResourceKey=="Effects/ki").Count);
        }
        [Test] public void Ninja_can_decline_or_spend_one_without_borrowing_future_gain()
        {
            foreach(int yes in new[]{0,1})
            {var p=new Policy();p.Answers[MonkRules.Ninja]=yes;var g=Match(CharacterDefinition.WayOfTheNinja,out var a,out var b,p);a.LeftHand.SetValue(1);a.RightHand.SetValue(4);b.LeftHand.SetValue(3);var r=g.ExecuteTurn(a,a.LeftHand,b.LeftHand);Assert.AreEqual(yes==1?2:1,r.HitCount);Assert.AreEqual(yes==1?2:3,a.MonkKi);Assert.AreEqual(4,r.DamageDealt);}
            var game=Match(CharacterDefinition.WayOfTheNinja,out var n,out var t);n.TrySpendMonkKi(2);n.LeftHand.SetValue(1);n.RightHand.SetValue(4);t.LeftHand.SetValue(3);var result=game.ExecuteTurn(n,n.LeftHand,t.LeftHand);Assert.AreEqual(1,result.HitCount);Assert.AreEqual(1,n.MonkKi);
        }
        [Test] public void Harmony_is_free_and_removes_all_ki()
        {
            var g=Match(CharacterDefinition.WayOfTheOpenHand,out var a,out var b);a.SetHealthExact(2);Assert.IsTrue(((WayOfTheOpenHandLogic)a.Character).TryHarmonyBody(a,g));Assert.AreEqual(6,a.Health);Assert.AreEqual(0,a.MonkKi);Assert.AreEqual(0,g.TurnCount);Assert.AreEqual(a,g.CurrentPlayer);
        }
        [Test] public void Hidden_force_requires_placer_and_subsequent_hit_and_counts_shield()
        {
            var policy=new Policy();policy.Answers[MartialSkillChoices.OpenHand]=0;
            var g=Match(CharacterDefinition.WayOfTheOpenHand,out var a,out var b,policy);b.SetHiddenForce(a);b.LeftHand.SetValue(5);b.LeftHand.SetShieldState(ShieldState.Formed);
            var attack=new AttackContext(a,b,a.LeftHand,6){IgnoreStatBonuses=true};attack.Resolve(g.GetChoiceProvider(b),g);Assert.AreEqual(3,attack.ExtraTrueDamage);Assert.AreEqual(17,b.Health);Assert.IsFalse(b.HasResonance);
            b.SetHiddenForce(a);var other=P(CharacterDefinition.Blank);var wrong=new AttackContext(other,b,other.LeftHand,4){IgnoreStatBonuses=true};wrong.Resolve(g.GetChoiceProvider(b),g);Assert.IsTrue(b.HasResonance);
        }
        [Test] public void Hidden_force_does_not_detonate_on_the_hit_that_places_it()
        {
            var policy=new Policy();policy.Answers[MartialSkillChoices.OpenHand]=-1;var g=Match(CharacterDefinition.WayOfTheOpenHand,out var a,out var b,policy);a.GainMonkKi();var hit=new AttackContext(a,b,a.LeftHand,4);hit.Resolve(g.GetChoiceProvider(b),g);Assert.AreEqual(16,b.Health);Assert.IsTrue(b.HasResonance);Assert.AreEqual(a,b.HiddenForceSource);Assert.AreEqual(0,a.MonkKi);
        }
        [Test] public void Four_elements_defense_is_optional_and_cannot_grant_psychic()
        {
            foreach(int yes in new[]{0,1})
            {var policy=new Policy();policy.Answers[MonkRules.Defense]=yes;var a=P(CharacterDefinition.Blank);var b=P(CharacterDefinition.WayOfTheFourElements);var g=new GameManager(a,b,new SimpleChoiceProvider(),policy);var hit=new AttackContext(a,b,a.LeftHand,6){AttackType=AttackDamageType.Fire};hit.Resolve(policy,g);Assert.AreEqual(yes==1?17:14,b.Health);Assert.AreEqual(yes==1?1:2,b.MonkKi);}
            var game=Match(CharacterDefinition.WayOfTheFourElements,out var monk,out var foe);var incoming=new AttackContext(foe,monk,foe.LeftHand,4){AttackType=AttackDamageType.Psychic};((WayOfTheFourElementsLogic)monk.Character).OnIncomingElementAttack(incoming,game);Assert.AreEqual(2,monk.MonkKi);Assert.IsFalse(monk.HasResistance(AttackDamageType.Psychic));
        }
        [Test] public void Drunken_enters_after_sober_heal_and_all_digits_become_attacks()
        {
            var policy=new Policy();policy.Answers[MonkRules.Drunken]=1;var g=Match(CharacterDefinition.WayOfTheDrunkenMaster,out var a,out var b,policy);a.SetHealthExact(10);a.LeftHand.SetValue(1);b.LeftHand.SetValue(8);g.ExecuteTurn(a,a.LeftHand,b.LeftHand);var drunk=(WayOfTheDrunkenMasterLogic)a.Character;Assert.AreEqual(19,a.Health);Assert.IsTrue(drunk.Drunken);
            for(int value=0;value<=9;value++){a.LeftHand.SetValue(value);Assert.AreEqual(HandEffectType.Attack4,drunk.RewriteEffect(a,a.LeftHand,1,g));Assert.IsFalse(a.LeftHand.IsShield);}
        }
        [Test] public void Drunken_rolls_per_segment_before_protection_and_leaves_status_damage_unchanged()
        {
            var g=Match(CharacterDefinition.WayOfTheDrunkenMaster,out var a,out var b);var drunk=(WayOfTheDrunkenMasterLogic)a.Character;drunk.TryEnter(a);drunk.ForcedOutgoing=10;drunk.ForcedIncoming=5;
            var hit=new AttackContext(a,b,a.LeftHand,4){HitCount=2};hit.Resolve(g.GetChoiceProvider(b),g);Assert.AreEqual(20,hit.AppliedDamage);
            b.SetHealthExact(20);a.AddResistance(AttackDamageType.Fire);var incoming=new AttackContext(b,a,b.LeftHand,8){AttackType=AttackDamageType.Fire};incoming.Resolve(g.GetChoiceProvider(a),g);Assert.AreEqual(3,incoming.AppliedDamage);
            var dot=new AttackContext(b,a,b.LeftHand,2){IsStatusDamage=true,IgnoreStatBonuses=true};dot.Resolve(g.GetChoiceProvider(a),g);Assert.AreEqual(2,dot.AppliedDamage);
            a.ApplyStatus(StatusEffect.HolyLight);Assert.IsFalse(drunk.Drunken);Assert.IsFalse(drunk.TryEnter(a));
        }
        [Test] public void Drunken_upkeep_exits_when_empty_and_pure_operation_never_gains_ki()
        {
            var g=Match(CharacterDefinition.WayOfTheDrunkenMaster,out var a,out var b);var drunk=(WayOfTheDrunkenMasterLogic)a.Character;drunk.TryEnter(a);a.TrySpendMonkKi(2);drunk.OnTurnStarted(a,g);Assert.IsFalse(drunk.Drunken);
            a.LeftHand.SetValue(1);a.RightHand.SetValue(4);b.LeftHand.SetValue(3);Assert.IsTrue(g.ApplyEffectlessOperation(a,a.LeftHand,b.LeftHand,"测试"));Assert.AreEqual(0,a.MonkKi);
        }
        [Test] public void Sustained_abjuration_recasts_and_caster_death_do_not_remove_other_sources()
        {
            var g=Match(CharacterDefinition.SchoolOfAbjuration,out var a,out var b);var caster=(SchoolOfAbjurationLogic)a.Character;caster.GrantResistance(b,AttackDamageType.Fire);b.AddPermanentResistance(AttackDamageType.Cold);
            for(int i=0;i<2;i++){var hit=new AttackContext(a,b,a.LeftHand,4){AttackType=AttackDamageType.Fire};hit.Resolve(g.GetChoiceProvider(b),g);Assert.IsTrue(b.HasResistance(AttackDamageType.Fire));}
            caster.GrantResistance(a,AttackDamageType.Bludgeoning);Assert.IsFalse(b.HasResistance(AttackDamageType.Fire));Assert.IsTrue(b.HasResistance(AttackDamageType.Cold));Assert.IsTrue(a.HasResistance(AttackDamageType.Piercing));
            caster.GrantResistance(b,AttackDamageType.Fire);a.SetHealthExact(0);Assert.IsFalse(b.HasResistance(AttackDamageType.Fire));Assert.IsTrue(b.HasResistance(AttackDamageType.Cold));
        }
        [Test] public void Evocation_pending_type_protects_until_consumption_or_holy_light()
        {
            var g=Match(CharacterDefinition.SchoolOfEvocation,out var a,out var b);var mage=(SchoolOfEvocationLogic)a.Character;Assert.AreEqual(int.MaxValue,GameManager.BaseRangeOf(a));mage.SetPendingDamageType(DamageType.Fire);Assert.IsTrue(a.HasResistance(AttackDamageType.Fire));a.ClearTemporaryDamageAffinities();Assert.IsTrue(a.HasResistance(AttackDamageType.Fire));mage.ConsumeDamageType();Assert.IsFalse(a.HasResistance(AttackDamageType.Fire));mage.SetPendingDamageType(DamageType.Cold);a.ApplyStatus(StatusEffect.HolyLight);Assert.IsFalse(a.HasResistance(AttackDamageType.Cold));
        }
        [Test] public void Portent_records_unique_digits_persist_and_new_skill_no_longer_invalidates()
        {
            var g=Match(CharacterDefinition.SchoolOfDivination,out var a,out var b);var book=(SchoolOfDivinationLogic)a.Character;a.LeftHand.SetValue(7);a.RightHand.SetValue(0);book.OnOperationResolved(a,a.LeftHand,3,4,HandEffectType.None,g,null);book.OnOperationResolved(a,a.LeftHand,3,4,HandEffectType.None,g,null);Assert.AreEqual(1,book.Portents.Count);Assert.IsFalse(g.WillSuppressOperationEffects(b));Assert.IsTrue(book.ConsumePortent(0));Assert.IsFalse(book.ConsumePortent(0));
        }
        [Test] public void Portent_replacement_changes_effect_without_reapplying_result_modifiers()
        {
            var policy=new Policy();policy.Answers[DivinationRules.Key]=5;var g=Match(CharacterDefinition.SchoolOfDivination,out var a,out var b,policy);var book=(SchoolOfDivinationLogic)a.Character;book.RecordPortent(5);a.LeftHand.SetValue(7);b.LeftHand.SetValue(1);b.ApplyStatus(StatusEffect.Frost);var result=g.ExecuteTurn(b,b.LeftHand,a.LeftHand);Assert.AreEqual(5,b.LeftHand.Value);Assert.IsTrue(b.LeftHand.IsShield);Assert.AreEqual(HandEffectType.Shield,result.Effect);Assert.AreEqual(0,book.Portents.Count);Assert.IsFalse(result.ActionInvalidated);
        }
        [Test] public void Portent_cannot_fire_without_seven_and_decline_preserves_record()
        {
            var policy=new Policy();policy.Answers[DivinationRules.Key]=5;var g=Match(CharacterDefinition.SchoolOfDivination,out var a,out var b,policy);var book=(SchoolOfDivinationLogic)a.Character;book.RecordPortent(5);a.LeftHand.SetValue(3);b.LeftHand.SetValue(1);g.ExecuteTurn(b,b.LeftHand,a.LeftHand);Assert.AreEqual(4,b.LeftHand.Value);Assert.AreEqual(1,book.Portents.Count);
            policy.Answers[DivinationRules.Key]=-1;a.LeftHand.SetValue(7);b.LeftHand.SetValue(1);g.ExecuteTurn(b,b.LeftHand,a.LeftHand);Assert.AreEqual(8,b.LeftHand.Value);Assert.AreEqual(1,book.Portents.Count);
        }
        [Test] public void Tai_chi_human_boost_options_change_only_the_accepted_segment()
        {
            for(int mode=0;mode<=2;mode++)
            {
                var a=P(CharacterDefinition.Champion);var b=P(CharacterDefinition.WayOfTheTaiChi);var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();var g=new GameManager(a,b,ca,cb);var board=new BoardController(g,ca,cb);
                a.LeftHand.SetValue(3);a.RightHand.SetValue(3);b.LeftHand.SetValue(1);b.RightHand.SetValue(5);b.RightHand.SetShieldState(ShieldState.Taichi);
                board.BeginTurn();Assert.IsTrue(board.ClickHand(a.LeftHand));Assert.IsTrue(board.ClickHand(b.LeftHand));Assert.IsTrue(board.ConfirmTargetPlayer());Assert.IsTrue(board.ClickHand(b.RightHand));
                Assert.AreEqual(HandEffect.TaichiKiBoost,board.PendingEffect);Assert.IsTrue(board.ChooseSkillOption(mode));
                Assert.AreEqual(mode==1?20:17,b.Health);Assert.AreEqual(mode==2?15:18,a.Health);Assert.AreEqual(mode==0?2:1,b.MonkKi);Assert.AreEqual(0,b.RightHand.Value);Assert.AreEqual(1,g.TurnCount);
            }
        }
        [Test] public void Ai_saves_portents_and_uses_them_to_help_known_allies()
        {
            var g=Match(CharacterDefinition.SchoolOfDivination,out var a,out var b);var book=(SchoolOfDivinationLogic)a.Character;var ai=new GreedyChoiceProvider();g.SkillPolicyFor=p=>ai;book.RecordPortent(1);a.LeftHand.SetValue(7);b.LeftHand.SetValue(1);
            var hit=g.ExecuteTurn(b,b.LeftHand,a.LeftHand);Assert.AreEqual(1,b.LeftHand.Value);Assert.AreEqual(20,a.Health);Assert.AreEqual(0,book.Portents.Count);
            book.RecordPortent(8);a.LeftHand.SetValue(7);b.LeftHand.SetValue(4);g.ExecuteTurn(b,b.LeftHand,a.LeftHand);Assert.AreEqual(1,b.LeftHand.Value);Assert.AreEqual(1,book.Portents.Count);
        }
        [Test] public void Drunken_sampling_repeats_with_the_same_seed()
        {
            var a=CharacterBalanceTests.RunOne(CharacterDefinition.WayOfTheDrunkenMaster,CharacterDefinition.SchoolOfDivination,true,500,out string ea);
            var b=CharacterBalanceTests.RunOne(CharacterDefinition.WayOfTheDrunkenMaster,CharacterDefinition.SchoolOfDivination,true,500,out string eb);
            Assert.IsTrue(ea==null&&eb==null,ea??eb);Assert.AreEqual(a.Turns,b.Turns);Assert.AreEqual(a.WinnerIsPlayerA,b.WinnerIsPlayerA);
        }
        [Test] public void Drunken_digit_rewrite_overrides_giant_heal_in_both_legal_dual_orders()
        {
            foreach(bool reverse in new[]{false,true})
            {
                var a=DualGeneralFactory.Create("醉巨人",reverse?CharacterDefinition.WayOfTheDrunkenMaster:CharacterDefinition.PathOfTheGiant,reverse?CharacterDefinition.PathOfTheGiant:CharacterDefinition.WayOfTheDrunkenMaster);
                var b=P(CharacterDefinition.Blank);var g=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                ((WayOfTheDrunkenMasterLogic)CharacterBehaviors.FirstImplementer<IDrunkenStance>(a)).TryEnter(a);
                Assert.AreEqual(HandEffectType.Attack4,CharacterBehaviors.DisplayedEffectOf(a,9));Assert.AreEqual(HandEffectType.Attack4,CharacterBehaviors.DisplayedEffectOf(a,5));
            }
        }
    }
}
