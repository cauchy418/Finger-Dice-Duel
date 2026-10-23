using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class NewDruidFormsTests
    {
        private static GameManager Game(CharacterDefinition a, CharacterDefinition b,
                                        out PlayerState pA, out PlayerState pB)
        {
            pA = CharacterFactory.CreatePlayer("A", a);
            pB = CharacterFactory.CreatePlayer("B", b);
            return new GameManager(pA, pB, new SimpleChoiceProvider(), new SimpleChoiceProvider());
        }

        [Test]
        public void Moon_enters_bear_on_sum_ten_and_four_becomes_shield()
        {
            GameManager game = Game(CharacterDefinition.CircleOfTheMoon,
                                    CharacterDefinition.Champion,
                                    out PlayerState moon, out _);
            moon.LeftHand.SetValue(4);
            moon.RightHand.SetValue(6);
            game.BeginTurn();

            var logic = (CircleOfTheMoonLogic)moon.Character;
            Assert.AreEqual(CircleOfTheMoonLogic.WildShape.Bear, logic.Form);
            Assert.AreEqual(-1, moon.Character.AttackBonus);
            Assert.AreEqual(2, moon.Character.DefenseBonus);
            Assert.AreEqual(HandEffectType.Shield, logic.DisplayedEffectOf(4));
        }

        [Test]
        public void Moon_can_exit_only_with_equal_hands()
        {
            GameManager game = Game(CharacterDefinition.CircleOfTheMoon,
                                    CharacterDefinition.Champion,
                                    out PlayerState moon, out _);
            moon.LeftHand.SetValue(5);
            moon.RightHand.SetValue(5);
            var logic = (CircleOfTheMoonLogic)moon.Character;
            Assert.IsTrue(logic.TryEnter(CircleOfTheMoonLogic.WildShape.Wolf, moon, game));
            moon.RightHand.SetValue(6);
            Assert.IsFalse(logic.TryExit(moon, game));
            moon.RightHand.SetValue(5);
            Assert.IsTrue(logic.TryExit(moon, game));
            Assert.AreEqual(CircleOfTheMoonLogic.WildShape.None, logic.Form);
        }

        [Test]
        public void Stars_enters_archer_and_attacks_are_radiant()
        {
            GameManager game = Game(CharacterDefinition.CircleOfTheStars,
                                    CharacterDefinition.Champion,
                                    out PlayerState stars, out PlayerState target);
            stars.LeftHand.SetValue(4);
            stars.RightHand.SetValue(6);
            game.BeginTurn();
            var logic = (CircleOfTheStarsLogic)stars.Character;
            Assert.AreEqual(CircleOfTheStarsLogic.StarryForm.Archer, logic.Form);
            var ctx = new AttackContext(stars, target, stars.LeftHand, 4);
            Assert.AreEqual(AttackDamageType.Radiant, logic.AttackTypeOf(ctx));
            Assert.AreEqual(2, stars.Character.AttackBonus);
            Assert.AreEqual(-1, stars.Character.DefenseBonus);
        }
    }
}
