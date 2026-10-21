using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class NewThreeCharacterTests
    {
        [Test]
        public void New_characters_are_in_the_built_in_roster_with_english_names()
        {
            bool oath = false, berserk = false, wander = false;
            for (int i = 0; i < CharacterDefinition.BuiltIn.Length; i++)
            {
                CharacterDefinition d = CharacterDefinition.BuiltIn[i];
                if (d.Id == CharacterDefinition.OathbreakerId)
                { oath = d.EnglishName == "Oathbreaker"; }
                if (d.Id == CharacterDefinition.BerserkerId)
                { berserk = d.EnglishName == "Berserker"; }
                if (d.Id == CharacterDefinition.WandererId)
                { wander = d.EnglishName == "Wanderer"; }
            }
            Assert.IsTrue(oath && berserk && wander);
            Assert.AreEqual(CharacterDefinition.ClericId, CharacterDefinition.PriestId,
                "旧 Priest Id 必须兼容到 Cleric。");
        }

        [Test]
        public void Oathbreaker_five_becomes_attack_and_berserker_scales_with_missing_health()
        {
            PlayerState oath = CharacterFactory.CreatePlayer(
                "破誓者", CharacterDefinition.Oathbreaker);
            oath.LeftHand.SetValue(5);
            Assert.AreEqual(HandEffectType.Attack4,
                new OathbreakerLogic().RewriteEffect(oath, oath.LeftHand, 1, null));
            Assert.AreEqual(2, oath.Character.AttackBonus);

            PlayerState berserk = CharacterFactory.CreatePlayer(
                "狂战士", CharacterDefinition.Berserker);
            Assert.AreEqual(25, berserk.MaximumHealth, "狂战士上限 +5。");
            berserk.SetHealthExact(2);
            var logic = (BerserkerLogic)berserk.Character;
            logic.RefreshDynamicStats(berserk, null);
            Assert.AreEqual(4, berserk.DynamicAttackBonus);
            Assert.AreEqual(4, berserk.DynamicDefenseBonus);
            Assert.AreEqual(-1 + 4, StatusRules.EffectiveAttackBonus(berserk));
        }
    }
}
