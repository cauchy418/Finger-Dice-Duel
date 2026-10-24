using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class WizardChoiceFlowTests
    {
        private static BoardController Board(CharacterDefinition a,CharacterDefinition b,out GameManager game)
        {
            var pa=CharacterFactory.CreatePlayer("A",a);var pb=CharacterFactory.CreatePlayer("B",b);
            var ca=new SimpleChoiceProvider();var cb=new SimpleChoiceProvider();
            game=new GameManager(pa,pb,ca,cb);return new BoardController(game,ca,cb);
        }
        [Test] public void Abjuration_stores_without_target_or_defender_shield_choice()
        {
            var board=Board(CharacterDefinition.SchoolOfAbjuration,CharacterDefinition.Champion,out var game);
            game.PlayerB.LeftHand.SetValue(3);game.PlayerB.RightHand.SetValue(5);
            game.PlayerB.RightHand.SetShieldState(ShieldState.Formed);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(HandEffect.SchoolOfAbjurationBarrier,board.PendingEffect);
            Assert.IsTrue(board.AcceptPendingChoice());
            Assert.AreEqual(4,((IAbjurationSkill)game.PlayerA.Character).BarrierPoints);
            Assert.AreEqual(4,game.PlayerA.LeftHand.Value);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.IsTrue(game.PlayerB.RightHand.IsShield);Assert.IsTrue(board.LastTurn.AttackSuppressed);
            Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Abjuration_selects_living_target_then_magic_type()
        {
            var board=Board(CharacterDefinition.SchoolOfAbjuration,CharacterDefinition.Champion,out var game);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(HandEffect.SchoolOfAbjurationResistanceTarget,board.PendingEffect);
            board.ClickHand(game.PlayerB.RightHand);
            Assert.AreEqual(HandEffect.SchoolOfAbjurationResistanceType,board.PendingEffect);
            Assert.IsFalse(board.AcceptPendingChoice());
            Assert.IsTrue(board.ChooseResistanceType(AttackDamageType.Cold));
            Assert.IsTrue(game.PlayerB.HasResistance(AttackDamageType.Cold));
            Assert.IsFalse(game.PlayerA.HasResistance(AttackDamageType.Cold));
            Assert.AreEqual(1,game.TurnCount);
        }
        [Test] public void Abjuration_rejects_five_forbidden_resistances_in_ui_and_skill_api()
        {
            var board=Board(CharacterDefinition.SchoolOfAbjuration,CharacterDefinition.Champion,out var game);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            board.ClickHand(game.PlayerA.RightHand);
            var skill=(SchoolOfAbjurationLogic)game.PlayerA.Character;
            foreach(var type in new[]{AttackDamageType.Psychic,AttackDamageType.Force,AttackDamageType.Poison,
                AttackDamageType.Radiant,AttackDamageType.Necrotic})
            {
                Assert.IsFalse(board.ChooseResistanceType(type));
                skill.SelectedResistance=type;skill.GrantResistance(game.PlayerA,type);
                Assert.IsFalse(game.PlayerA.HasResistance(type));
                Assert.AreEqual(AttackDamageType.Bludgeoning,skill.SelectedResistance);
            }
            Assert.AreEqual(HandEffect.SchoolOfAbjurationResistanceType,board.PendingEffect);
            Assert.IsTrue(board.ChooseResistanceType(AttackDamageType.Lightning));
            Assert.IsTrue(game.PlayerA.HasResistance(AttackDamageType.Lightning));
        }
        private sealed class AttackRoll : Random { public override int Next(int min,int max) => 4; }
        [Test] public void Gambling_attack_without_legal_target_keeps_roll_and_finishes_once()
        {
            var board=Board(CharacterDefinition.Gambler,CharacterDefinition.Assassin,out var game);
            game.PlayerB.LeftHand.SetValue(7);game.PlayerB.RightHand.SetValue(7);
            ((AssassinLogic)game.PlayerB.Character).EnterStealthForTest();
            board.GambleRng=new AttackRoll();board.BeginTurn();
            Assert.IsTrue(board.ClickHand(game.PlayerA.LeftHand));Assert.IsTrue(board.Gamble());
            Assert.AreEqual(4,game.PlayerA.LeftHand.Value);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.AreEqual(1,game.TurnCount);Assert.IsTrue(board.LastTurn.AttackSuppressed);
            Assert.IsTrue(board.LastIllegalOperation==null);
        }
        [Test] public void Physical_choice_grants_all_three_and_icons_are_distinct()
        {
            var board=Board(CharacterDefinition.SchoolOfAbjuration,CharacterDefinition.Champion,out var game);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.ClickHand(game.PlayerB.LeftHand);
            board.ClickHand(game.PlayerA.RightHand);board.ChooseResistanceType(AttackDamageType.Bludgeoning);
            var icons=CombatStatusIcons.For(game.PlayerA);Assert.AreEqual(3,icons.Count);
            Assert.IsTrue(game.PlayerA.HasResistance(AttackDamageType.Slashing));
            Assert.IsTrue(game.PlayerA.HasResistance(AttackDamageType.Piercing));
            Assert.AreEqual("Resistance/resistance_slashing",icons[0].ResourceKey);
            Assert.AreEqual("Resistance/resistance_piercing",icons[2].ResourceKey);
        }
        [Test] public void All_thirteen_resistances_and_barrier_are_present_in_guide_and_state()
        {
            var p=CharacterFactory.CreatePlayer("A",CharacterDefinition.SchoolOfAbjuration);
            foreach(var t in ResistanceNames.All) p.AddResistance(t);
            ((IAbjurationSkill)p.Character).StoreBarrier(12);
            var icons=CombatStatusIcons.For(p);Assert.AreEqual(14,icons.Count);
            Assert.AreEqual("12",icons[13].Counter);Assert.AreEqual("Effects/arcane_barrier",icons[13].ResourceKey);
            var guide=GuideSheets.Protections();Assert.AreEqual(13,guide.Groups[0].Entries.Count);
            var keys=new HashSet<string>();foreach(var e in guide.Groups[0].Entries) keys.Add(e.ResourceKey);
            Assert.AreEqual(13,keys.Count);
            Assert.AreEqual("Effects/arcane_barrier",GuideSheets.CharacterMarks().Groups[0].Entries[0].ResourceKey);
        }
        [Test] public void Necromancy_human_can_decline_free_attack_without_losing_normal_action()
        {
            var board=Board(CharacterDefinition.SchoolOfNecromancy,CharacterDefinition.Champion,out var game);
            board.BeginTurn();Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);
            Assert.AreEqual(game.PlayerA,board.Chooser);Assert.IsTrue(board.IsHumanChooser);
            Assert.IsTrue(board.DeclinePendingChoice());Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);
            Assert.AreEqual(0,game.TurnCount);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.IsTrue(board.PendingWizardChoice==null);
        }
        [Test] public void Necromancy_auto_activates_one_then_human_selects_only_target()
        {
            var board=Board(CharacterDefinition.SchoolOfNecromancy,CharacterDefinition.Champion,out var game);
            board.BeginTurn();
            Assert.IsTrue(board.PendingWizardChoice.TargetsPlayer);
            Assert.AreEqual("B",board.WizardHandLabel(game.PlayerB.LeftHand));
            Assert.AreEqual("放弃攻击",board.PendingWizardChoice.DeclineLabel);
            Assert.IsTrue(board.ChooseWizardHand(game.PlayerB.LeftHand));
            Assert.AreEqual(19,game.PlayerB.Health);Assert.IsTrue(game.PlayerB.HasStatus(StatusEffect.Decay));
            Assert.AreEqual(1,game.PlayerA.RightHand.Value);Assert.AreEqual(0,game.TurnCount);
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);
        }
        [Test] public void Free_attack_collects_human_necromancy_block_and_resumes_once()
        {
            var board=Board(CharacterDefinition.SchoolOfNecromancy,CharacterDefinition.SchoolOfNecromancy,out var game);
            game.PlayerB.LeftHand.SetValue(2);game.PlayerB.RightHand.SetValue(2);
            board.BeginTurn();board.ChooseWizardHand(game.PlayerB.LeftHand);
            Assert.AreEqual(HandEffect.SchoolOfNecromancyBlock,board.PendingEffect);
            Assert.AreEqual(game.PlayerB,board.Chooser);
            Assert.IsTrue(board.ClickHand(game.PlayerB.RightHand));
            Assert.AreEqual(1,game.PlayerB.RightHand.Value);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.AreEqual(BoardPhase.ChoosingAttackerHand,board.Phase);Assert.AreEqual(0,game.TurnCount);
        }
        [Test] public void Necromancy_block_respects_decline_and_force_exclusion()
        {
            var board=Board(CharacterDefinition.Champion,CharacterDefinition.SchoolOfNecromancy,out var game);
            game.PlayerB.LeftHand.SetValue(2);
            game.AnsweredLookupFor=(p,e,v)=>null;
            var attack=new AttackContext(game.PlayerA,game.PlayerB,game.PlayerA.LeftHand,4)
                {AttackType=AttackDamageType.Slashing,IgnoreStatBonuses=true};
            attack.Resolve(new SimpleChoiceProvider(),game);Assert.AreEqual(4,attack.AppliedDamage);
            Assert.AreEqual(2,game.PlayerB.LeftHand.Value);
            game.AnsweredLookupFor=(p,e,v)=>game.PlayerB.LeftHand;
            var force=new AttackContext(game.PlayerA,game.PlayerB,game.PlayerA.LeftHand,4)
                {AttackType=AttackDamageType.Force,IgnoreStatBonuses=true};
            force.Resolve(new SimpleChoiceProvider(),game);Assert.AreEqual(4,force.AppliedDamage);
            Assert.AreEqual(2,game.PlayerB.LeftHand.Value);
        }
        [Test] public void Death_swap_exchanges_two_pairs_each_hand_once()
        {
            var board=Board(CharacterDefinition.SchoolOfNecromancy,CharacterDefinition.Champion,out var game);
            var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Champion);
            var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Champion);
            game.AddPlayersFor2v2(c,new SimpleChoiceProvider(),d,new SimpleChoiceProvider());
            game.PlayerA.LeftHand.SetValue(3);game.PlayerA.RightHand.SetValue(4);
            c.LeftHand.SetValue(8);c.RightHand.SetValue(9);c.TakeDamage(20);game.RefreshGameOver();
            board.BeginTurn();Assert.AreEqual(BoardPhase.ChoosingWizardChoice,board.Phase);
            board.ChooseWizardHand(game.PlayerA.LeftHand);board.ChooseWizardHand(c.RightHand);
            Assert.AreEqual(9,game.PlayerA.LeftHand.Value);Assert.AreEqual(3,c.RightHand.Value);
            Assert.AreEqual(1,board.PendingWizardChoice.Candidates.Count);
            board.ChooseWizardHand(game.PlayerA.RightHand);board.ChooseWizardHand(c.LeftHand);
            Assert.AreEqual(8,game.PlayerA.RightHand.Value);Assert.AreEqual(4,c.LeftHand.Value);
            Assert.IsTrue(board.PendingWizardChoice==null);Assert.AreEqual(0,game.TurnCount);
        }
        [Test] public void Death_swap_can_end_at_zero_and_match_end_does_not_prompt()
        {
            var board=Board(CharacterDefinition.SchoolOfNecromancy,CharacterDefinition.Champion,out var game);
            var c=CharacterFactory.CreatePlayer("C",CharacterDefinition.Champion);
            var d=CharacterFactory.CreatePlayer("D",CharacterDefinition.Champion);
            game.AddPlayersFor2v2(c,new SimpleChoiceProvider(),d,new SimpleChoiceProvider());
            game.PlayerA.LeftHand.SetValue(3);game.PlayerA.RightHand.SetValue(4);
            c.TakeDamage(20);game.RefreshGameOver();board.BeginTurn();board.DeclinePendingChoice();
            Assert.AreEqual(3,game.PlayerA.LeftHand.Value);Assert.AreEqual(4,game.PlayerA.RightHand.Value);
            game.PlayerB.TakeDamage(20);game.RefreshGameOver();board.BeginTurn();
            Assert.AreEqual(BoardPhase.GameOver,board.Phase);Assert.IsTrue(board.PendingWizardChoice==null);
        }

        [Test] public void Stolen_attack_keeps_eight_and_collects_human_block()
        {
            var board=Board(CharacterDefinition.Thief,CharacterDefinition.SchoolOfNecromancy,out var game);
            game.PlayerA.LeftHand.SetValue(9);game.PlayerB.LeftHand.SetValue(2);game.PlayerB.RightHand.SetValue(8);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.BeginSteal();board.ClickHand(game.PlayerB.RightHand);
            Assert.IsTrue(game.StealAttackAwaitingTarget);board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(HandEffect.SchoolOfNecromancyBlock,board.PendingEffect);
            board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(8,game.PlayerA.LeftHand.Value);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.AreEqual(1,game.TurnCount);Assert.IsFalse(game.StealAttackAwaitingTarget);
        }
        [Test] public void Stolen_attack_without_legal_target_is_abandoned_without_reoperation()
        {
            var board=Board(CharacterDefinition.Thief,CharacterDefinition.Champion,out var game);
            game.PlayerA.LeftHand.SetValue(9);game.PlayerB.LeftHand.SetValue(8);
            game.PlayerA.ApplyStatus(StatusEffect.Panic,2,game.PlayerB);
            board.BeginTurn();board.ClickHand(game.PlayerA.LeftHand);board.BeginSteal();board.ClickHand(game.PlayerB.LeftHand);
            Assert.AreEqual(8,game.PlayerA.LeftHand.Value);Assert.AreEqual(20,game.PlayerB.Health);
            Assert.AreEqual(1,game.TurnCount);Assert.IsFalse(game.StealAttackAwaitingTarget);
        }
        [Test] public void New_school_capabilities_are_discovered_in_both_dual_positions()
        {
            foreach(bool reverse in new[]{false,true})
            {

                var necro=new SchoolOfNecromancyLogic();var diviner=new SchoolOfDivinationLogic();
                var a=new PlayerState("A",new DualGeneralBehavior(reverse?(ICharacterBehavior)diviner:necro,reverse?(ICharacterBehavior)necro:diviner));
                var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
                var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                game.ResetMatch();game.BeginTurn();Assert.AreEqual(19,b.Health);
                Assert.IsTrue(b.HasStatus(StatusEffect.Decay),"双将免费暗蚀保留腐朽");
                Assert.IsTrue(CharacterBehaviors.FirstImplementer<IAttackBlocker>(a)!=null);
                int hooks=0;foreach(var h in CharacterBehaviors.Implementers<ITurnStartHook>(a)) hooks++;
                Assert.AreEqual(2,hooks);
            }
        }
    }
}
