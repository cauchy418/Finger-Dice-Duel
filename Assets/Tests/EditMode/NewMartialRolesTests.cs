using FingerDiceDuel.Assistant;
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

        [Test] public void Ki_icons_follow_current_resource_including_zero()
        {
            foreach(var def in new[]{CharacterDefinition.WayOfTheOpenHand,CharacterDefinition.WayOfTheFourElements})
            {
                var a=CharacterFactory.CreatePlayer("A",def);var b=CharacterFactory.CreatePlayer("B",CharacterDefinition.Champion);
                var game=new GameManager(a,b,new SimpleChoiceProvider(),new SimpleChoiceProvider());
                var icon=CombatStatusIcons.For(a,game).Find(i=>i.ResourceKey=="Effects/ki");
                Assert.AreEqual("2",icon.Counter);
                b.LeftHand.SetValue(3);game.ExecuteTurn(a,a.LeftHand,b.LeftHand);
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
        public void Draconic_bloodline_keeps_permanent_resistance_and_three_turn_status()
        {
            GameManager game = Game(CharacterDefinition.DraconicBloodline,
                                    CharacterDefinition.Champion,
                                    out PlayerState p, out _);
            var logic = (DraconicBloodlineLogic)p.Character;
            logic.ChooseElement(AttackDamageType.Cold);
            p.AddPermanentResistance(AttackDamageType.Cold);

            Assert.IsTrue(p.HasResistance(AttackDamageType.Cold));
            var ctx = new AttackContext(p, game.PlayerB, p.LeftHand, 4);
            logic.OnResolveAttack(ctx);
            Assert.AreEqual(3, ctx.StatusDuration, "龙族血脉的该元素状态持续 3 回合。");
        }

        [Test]
        public void Hunter_mark_adds_lost_health_damage_and_is_consumed()
        {
            GameManager game = Game(CharacterDefinition.RangerHunter,
                                    CharacterDefinition.Champion,
                                    out PlayerState hunter, out PlayerState target);
            var logic = (RangerHunterLogic)hunter.Character;
            logic.Mark(target);
            target.SetHealthExact(15); // 已损 5 -> +1
            var ctx = new AttackContext(hunter, target, hunter.LeftHand, 4)
            {
                AttackType = AttackDamageType.Piercing,
                IgnoreStatBonuses = true,
            };
            logic.OnResolveAttack(ctx);
            Assert.AreEqual(5, ctx.FinalDamage);
            Assert.IsTrue(ctx.IgnorePositiveDefense);
            Assert.IsFalse(logic.IsMarked(target));
        }

        [Test]
        public void Open_hand_spends_four_ki_to_apply_resonance()
        {
            GameManager game = Game(CharacterDefinition.WayOfTheOpenHand,
                                    CharacterDefinition.Champion,
                                    out PlayerState monk, out PlayerState target);
            var logic = (WayOfTheOpenHandLogic)monk.Character;
            monk.LeftHand.SetValue(4);
            monk.RightHand.SetValue(4);
            logic.OnOperationResolved(monk, monk.LeftHand, 4, 4, HandEffectType.None, game, null);
            logic.OnOperationResolved(monk, monk.LeftHand, 4, 4, HandEffectType.None, game, null);
            Assert.IsTrue(logic.Ki >= 4);

            var ctx = new AttackContext(monk, target, monk.LeftHand, 4);
            logic.OnResolveAttack(ctx);
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
