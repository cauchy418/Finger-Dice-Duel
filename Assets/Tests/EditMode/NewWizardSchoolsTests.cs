using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class NewWizardSchoolsTests
    {
        private static GameManager Game(CharacterDefinition a, CharacterDefinition b,
                                        out PlayerState pA, out PlayerState pB,
                                        out SimpleChoiceProvider ca, out SimpleChoiceProvider cb)
        {
            pA = CharacterFactory.CreatePlayer("A", a);
            pB = CharacterFactory.CreatePlayer("B", b);
            ca = new SimpleChoiceProvider();
            cb = new SimpleChoiceProvider();
            return new GameManager(pA, pB, ca, cb);
        }

        [Test]
        public void Wizard_schools_have_the_confirmed_base_stats()
        {
            CharacterDefinition[] defs =
            {
                CharacterDefinition.SchoolOfDivination,
                CharacterDefinition.SchoolOfNecromancy,
                CharacterDefinition.SchoolOfAbjuration,
            };
            foreach (CharacterDefinition def in defs)
            {
                Assert.AreEqual(0, def.AttackBonus, def.Id);
                Assert.AreEqual(-1, def.DefenseBonus, def.Id);
                Assert.AreEqual(CharacterProfession.Find("wizard"), def.Profession, def.Id);
                PlayerState p = CharacterFactory.CreatePlayer("P", def);
                Assert.AreEqual(CharacterTemplate.InfiniteAttackRange,
                    ((IAttackRangeProvider)p.Character).BaseAttackRange, def.Id);
            }
        }

        [Test]
        public void Necromancy_two_blocks_one_segment_and_is_consumed()
        {
            GameManager game = Game(CharacterDefinition.Champion,
                                    CharacterDefinition.SchoolOfNecromancy,
                                    out PlayerState attacker,
                                    out PlayerState necro,
                                    out _, out _);
            necro.LeftHand.SetValue(2);
            int before = necro.Health;
            var ctx = new AttackContext(attacker, necro, attacker.LeftHand, 4)
            {
                AttackType = AttackDamageType.Slashing,
                IgnoreStatBonuses = true,
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);
            Assert.AreEqual(0, ctx.AppliedDamage);
            Assert.AreEqual(before, necro.Health);
            Assert.AreEqual(1, necro.LeftHand.Value);
        }

        [Test]
        public void Necromancy_start_attack_is_free_and_applies_decay()
        {
            GameManager game = Game(CharacterDefinition.SchoolOfNecromancy,
                                    CharacterDefinition.Champion,
                                    out PlayerState necro,
                                    out PlayerState target,
                                    out _, out _);
            necro.LeftHand.SetValue(1);
            int before = target.Health;
            game.BeginTurn();
            Assert.AreEqual(before - 1, target.Health);
            Assert.IsTrue(target.HasStatus(StatusEffect.Decay));
            Assert.AreEqual(1, necro.LeftHand.Value, "免费暗蚀不消耗 1。");
        }

        [Test]
        public void Abjuration_resistance_rounds_up_and_barrier_absorbs_after_it()
        {
            GameManager game = Game(CharacterDefinition.Champion,
                                    CharacterDefinition.SchoolOfAbjuration,
                                    out PlayerState attacker,
                                    out PlayerState abjurer,
                                    out _, out _);
            abjurer.AddResistance(AttackDamageType.Bludgeoning);
            var abj = (SchoolOfAbjurationLogic)abjurer.Character;
            abj.StoreBarrier(10);

            var ctx = new AttackContext(attacker, abjurer, attacker.LeftHand, 5)
            {
                AttackType = AttackDamageType.Bludgeoning,
                IgnoreStatBonuses = true,
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);
            Assert.AreEqual(0, ctx.AppliedDamage, "5 抗性后向上取整为 3，屏障 10 全部吸收。");
            Assert.AreEqual(0, abj.BarrierPoints);
            Assert.IsFalse(abjurer.HasResistance(AttackDamageType.Bludgeoning),
                "一次实际伤害后抗性消耗。");
        }
    }
}
