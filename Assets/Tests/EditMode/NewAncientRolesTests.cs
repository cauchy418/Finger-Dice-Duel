using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class NewAncientRolesTests
    {
        private static GameManager Game(CharacterDefinition a, CharacterDefinition b,
                                        out PlayerState pA, out PlayerState pB)
        {
            pA = CharacterFactory.CreatePlayer("A", a);
            pB = CharacterFactory.CreatePlayer("B", b);
            return new GameManager(pA, pB, new SimpleChoiceProvider(), new SimpleChoiceProvider());
        }

        [Test]
        public void Land_restricts_a_hand_as_addend_only()
        {
            GameManager game = Game(CharacterDefinition.CircleOfTheLand,
                                    CharacterDefinition.Champion,
                                    out PlayerState land, out PlayerState foe);
            var logic = (CircleOfTheLandLogic)land.Character;
            Hand hand = foe.LeftHand;
            Assert.IsTrue(logic.RestrictHand(hand, game));
            Assert.IsFalse(StatusRules.CanUseHandAsAddend(land, hand, game));
            Assert.IsTrue(hand.CanBeOperated, "封锁目标不等于封锁运算源。");
            Assert.AreEqual(1, land.Character.DefenseBonus + 0);
        }

        [Test]
        public void Paladin_heals_itself_when_below_ten_after_damage()
        {
            GameManager game = Game(CharacterDefinition.OathOfTheAncients,
                                    CharacterDefinition.Champion,
                                    out PlayerState paladin, out PlayerState foe);
            paladin.SetHealthExact(8);
            var logic = (OathOfTheAncientsLogic)paladin.Character;
            var ctx = new AttackContext(foe, paladin, foe.LeftHand, 2)
            {
                AttackType = AttackDamageType.Bludgeoning,
                IgnoreStatBonuses = true,
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);
            Assert.AreEqual(8, paladin.Health, "受伤后 6，再自愈 2。");
        }

        [Test]
        public void Fiend_healing_overflow_becomes_false_life_and_absorbs_damage()
        {
            GameManager game = Game(CharacterDefinition.TheFiend,
                                    CharacterDefinition.Champion,
                                    out PlayerState fiend, out _);
            fiend.SetHealthExact(18);
            fiend.Heal(9); // 实际回 2，溢出 7
            Assert.AreEqual(20, fiend.Health);
            Assert.AreEqual(7, fiend.FalseLife);

            fiend.TakeDamage(5);
            Assert.AreEqual(20, fiend.Health);
            Assert.AreEqual(2, fiend.FalseLife);
        }

        [Test]
        public void Fiend_attack_spends_false_life_for_disabled()
        {
            GameManager game = Game(CharacterDefinition.TheFiend,
                                    CharacterDefinition.Champion,
                                    out PlayerState fiend, out PlayerState foe);
            fiend.GainFalseLife(5);
            var logic = (TheFiendLogic)fiend.Character;
            var ctx = new AttackContext(fiend, foe, fiend.LeftHand, 4);
            ctx.Resolve(game.GetChoiceProvider(foe),game);
            Assert.AreEqual(0, fiend.FalseLife);
            Assert.IsTrue(foe.HasStatus(StatusEffect.Disabled));
        }
    }
}
