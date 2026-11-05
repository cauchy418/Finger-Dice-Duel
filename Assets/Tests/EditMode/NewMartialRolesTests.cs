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
    public class NewMartialRolesTests
    {
        private static GameManager Game(CharacterDefinition a, CharacterDefinition b,
                                        out PlayerState pA, out PlayerState pB)
        {
            pA = CharacterFactory.CreatePlayer("A", a);
            pB = CharacterFactory.CreatePlayer("B", b);
            return new GameManager(pA, pB, new SimpleChoiceProvider(), new SimpleChoiceProvider());
        }

        private sealed class HiddenForcePolicy:AbstractChoiceProvider
        {public override int ChooseSkillOption(string key,System.Collections.Generic.IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game)=>key==MartialSkillChoices.OpenHand?-1:0;}
        [Test] public void Ki_icons_follow_current_resource_including_zero()
        {
            foreach(var def in new[]{CharacterDefinition.WayOfTheOpenHand,CharacterDefinition.WayOfTheFourElements})
            {
                var a=CharacterFactory.CreatePlayer("A",def);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
                var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                var icon=CombatStatusIcons.For(a,game).Find(i=>i.ResourceKey=="Effects/ki");
                Assert.AreEqual("2",icon.Counter);
                b.LeftHand.SetValue(3);game.ExecuteTurn(a,a.LeftHand,b.LeftHand);
                a.TrySpendMonkKi(a.MonkKi);
                Assert.AreEqual("0",CombatStatusIcons.For(a,game).Find(i=>i.ResourceKey=="Effects/ki").Counter);
            }
        }
        [Test] public void Hunter_mark_icons_follow_target_and_actual_consumption()
        {
            var a=CharacterFactory.CreatePlayer("Hunter",CharacterDefinition.RangerHunter);
            var b=CharacterFactory.CreatePlayer("Target",CharacterDefinition.Champion);
            var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
            ((FingerDiceDuel.Characters.SpecialLogics.RangerHunterLogic)a.Character).Mark(b);
            Assert.IsFalse(CombatStatusIcons.For(a,game).Exists(i=>i.ResourceKey=="Effects/hunters_mark"));
            var icon=CombatStatusIcons.For(b,game).Find(i=>i.ResourceKey=="Effects/hunters_mark");
            Assert.IsTrue(icon.Description.Contains("Hunter"));Assert.AreEqual("",icon.Counter);
            b.LeftHand.SetValue(3);game.ExecuteTurn(a,a.LeftHand,b.LeftHand);
            Assert.IsFalse(CombatStatusIcons.For(b,game).Exists(i=>i.ResourceKey=="Effects/hunters_mark"));
        }
        [Test]
        public void Four_new_subclasses_are_registered_with_expected_stats()
        {
            PlayerState draconic = CharacterFactory.CreatePlayer("D", CharacterDefinition.DraconicBloodline);
            PlayerState hunter = CharacterFactory.CreatePlayer("H", CharacterDefinition.RangerHunter);
            PlayerState open = CharacterFactory.CreatePlayer("O", CharacterDefinition.WayOfTheOpenHand);
            PlayerState four = CharacterFactory.CreatePlayer("F", CharacterDefinition.WayOfTheFourElements);

            Assert.AreEqual(0, draconic.Character.AttackBonus);
            Assert.AreEqual(-1, draconic.Character.DefenseBonus);
            Assert.AreEqual(3, GameManager.BaseRangeOf(hunter));
            Assert.AreEqual(0, open.Character.DefenseBonus);
            Assert.AreEqual(0, four.Character.DefenseBonus);
        }

        [Test]
        public void Draconic_bloodline_keeps_permanent_resistance_and_extends_existing_status()
        {
            GameManager game = Game(CharacterDefinition.DraconicBloodline,
                                    CharacterDefinition.Champion,
                                    out PlayerState p, out _);
            var logic = (DraconicBloodlineLogic)p.Character;
            logic.ChooseElement(AttackDamageType.Fire);
            p.AddPermanentResistance(AttackDamageType.Cold);

            Assert.IsTrue(p.HasResistance(AttackDamageType.Cold));
            var ctx = new AttackContext(p, game.PlayerB, p.LeftHand, 4);
            ctx.Resolve(game.GetChoiceProvider(game.PlayerB), game);
            Assert.AreEqual(2, game.PlayerB.StatusTurnsLeft(StatusEffect.Burn));
            new AttackContext(p, game.PlayerB, p.LeftHand, 4).Resolve(game.GetChoiceProvider(game.PlayerB), game);
            Assert.AreEqual(3, game.PlayerB.StatusTurnsLeft(StatusEffect.Burn));
        }

        [Test]
        public void Hunter_mark_uses_the_larger_health_pool_and_is_consumed()
        {
            Game(CharacterDefinition.RangerHunter, CharacterDefinition.Champion,
                 out PlayerState hunter, out PlayerState target);
            var logic = (RangerHunterLogic)hunter.Character;
            foreach (var sample in new[] { (health: 20, bonus: 4), (health: 13, bonus: 2), (health: 5, bonus: 3) })
            {
                logic.Mark(target);
                target.SetHealthExact(sample.health);
                var ctx = new AttackContext(hunter, target, hunter.LeftHand, 4);
                logic.OnResolveAttack(ctx);
                Assert.AreEqual(4 + sample.bonus, ctx.FinalDamage);
                Assert.IsTrue(ctx.IgnorePositiveDefense);
                Assert.IsFalse(logic.IsMarked(target));
            }
        }

        [Test]
        public void Hunter_dual_ai_uses_mark_damage_with_no_other_lethal_advisor()
        {
            var hunter = new PlayerState("游侠", new DualGeneralBehavior(
                CharacterFactory.CreateBehavior(CharacterDefinition.RangerHunter),
                CharacterFactory.CreateBehavior(CharacterDefinition.OathOfTheAncients)));
            var target = CharacterFactory.CreatePlayer("对手", CharacterDefinition.Blank);
            var ai = new GreedyChoiceProvider();
            var game = new GameManager(hunter, target, ai, new SimpleChoiceProvider());
            CharacterBehaviors.FirstImplementer<RangerHunterLogic>(hunter).Mark(target);
            var move = new LegalMove(hunter, hunter.LeftHand, target.LeftHand);

            Assert.AreEqual(8, ai.TotalLethalDamage(move, hunter, target, 4, game));
        }

        [Test]
        public void Open_hand_spends_three_ki_to_apply_hidden_force()
        {
            GameManager game = Game(CharacterDefinition.WayOfTheOpenHand,
                                    CharacterDefinition.Champion,
                                    out PlayerState monk, out PlayerState target);
            var logic = (WayOfTheOpenHandLogic)monk.Character;
            monk.LeftHand.SetValue(4);
            monk.RightHand.SetValue(4);
            MonkRules.AfterAction(monk);
            Assert.AreEqual(3,logic.Ki);
            game.SkillPolicyFor=p=>new HiddenForcePolicy();
            var ctx = new AttackContext(monk, target, monk.LeftHand, 4);
            ctx.Resolve(game.GetChoiceProvider(target),game);
            Assert.IsTrue(target.HasResonance);
            Assert.IsTrue(logic.Ki < 4);
        }

        [Test]
        public void Four_elements_switches_attack_and_consumes_ki_for_resistance()
        {
            GameManager game = Game(CharacterDefinition.WayOfTheFourElements,
                                    CharacterDefinition.Champion,
                                    out PlayerState monk, out PlayerState attacker);
            var logic = (WayOfTheFourElementsLogic)monk.Character;
            logic.ChosenElement = AttackDamageType.Lightning;
            var ctx = new AttackContext(monk, attacker, monk.LeftHand, 4);
            logic.OnResolveAttack(ctx);
            Assert.AreEqual(AttackDamageType.Lightning, ctx.AttackType);
            Assert.AreEqual(0, logic.Ki);

            GameManager game2 = Game(CharacterDefinition.WayOfTheFourElements,
                                     CharacterDefinition.Champion,
                                     out PlayerState monk2, out PlayerState attacker2);
            var logic2 = (WayOfTheFourElementsLogic)monk2.Character;
            var incoming = new AttackContext(attacker2, monk2, attacker2.LeftHand, 4)
            {
                AttackType = AttackDamageType.Fire,
            };
            logic2.OnIncomingElementAttack(incoming, game2);
            Assert.IsTrue(monk2.HasResistance(AttackDamageType.Fire));
            Assert.AreEqual(1, logic2.Ki, "初始 2 气消耗 1 获得抗性。");
        }
    }
}
