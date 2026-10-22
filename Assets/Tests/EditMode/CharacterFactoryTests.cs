using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 角色装配层：CharacterDefinition / CharacterFactory 的正确性。
    /// 对应 RULES.md 第 10 节。
    /// </summary>
    [TestFixture]
    public class CharacterFactoryTests
    {
        [Test]
        public void Factory_maps_logic_type_to_behavior()
        {
            Assert.True(CharacterFactory.CreateBehavior(SpecialLogicType.None) is DataDrivenCharacter);
            Assert.True(CharacterFactory.CreateBehavior(SpecialLogicType.Breaker) is BreakerLogic);
            Assert.True(CharacterFactory.CreateBehavior(SpecialLogicType.SchoolOfConjuration) is SchoolOfConjurationLogic);
            Assert.True(CharacterFactory.CreateBehavior(SpecialLogicType.PathOfTheWarden) is PathOfTheWardenLogic);
        }

        [Test]
        public void Every_registered_logic_type_is_covered()
        {
            foreach (SpecialLogicType logic in System.Enum.GetValues(typeof(SpecialLogicType)))
            {
                Assert.True(CharacterFactory.IsRegistered(logic),
                    $"SpecialLogicType.{logic} 未在 CharacterFactory 注册，会退化成白板。");
            }
        }

        [Test]
        public void BuiltIn_definitions_all_map_to_a_real_behavior()
        {
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                Assert.NotNull(def, "内置角色表不应有 null 项。");
                Assert.True(def.MaxHealth > 0, $"{def.Id} 的最大生命应大于 0。");
                Assert.NotNull(CharacterFactory.CreateBehavior(def), $"{def.Id} 未能创建行为实例。");
            }
        }

        [Test]
        public void Definition_stats_are_injected_into_the_behavior()
        {
            var breaker = (CharacterTemplate)CharacterFactory.CreateBehavior(CharacterDefinition.Breaker);
            Assert.AreEqual(1, breaker.AttackBonus, "战士·破阵者攻击 +1");
            Assert.AreEqual(0, breaker.DefenseBonus);

            var schoolOfConjuration = (CharacterTemplate)CharacterFactory.CreateBehavior(CharacterDefinition.SchoolOfConjuration);
            Assert.AreEqual(-1, schoolOfConjuration.AttackBonus, "法师·咒法学派攻击 -1");

            var pathOfTheWarden = (CharacterTemplate)CharacterFactory.CreateBehavior(CharacterDefinition.PathOfTheWarden);
            Assert.AreEqual(0, pathOfTheWarden.AttackBonus);
            Assert.AreEqual(1, pathOfTheWarden.DefenseBonus, "野蛮人·守望者道途防御 +1");

            var blank = (CharacterTemplate)CharacterFactory.CreateBehavior(CharacterDefinition.Blank);
            Assert.AreEqual(0, blank.AttackBonus);
            Assert.AreEqual(0, blank.DefenseBonus);
        }

        [Test]
        public void Definition_overrides_the_code_default()
        {
            // 数据层可以把战士·破阵者改成攻击 +3，而无需改 BreakerLogic
            var tuned = new CharacterDefinition("fighter_plus3", "超级战士·破阵者",
                SpecialLogicType.Breaker, attackBonus: 3);

            var behavior = (CharacterTemplate)CharacterFactory.CreateBehavior(tuned);
            Assert.AreEqual(3, behavior.AttackBonus, "数据层应覆盖代码里的默认值。");
        }

        [Test]
        public void Default_stats_are_used_when_no_definition_is_applied()
        {
            // 直接 new 出来的角色走代码默认值，方便单测角色逻辑
            Assert.AreEqual(1, new BreakerLogic().AttackBonus);
            Assert.AreEqual(-1, new SchoolOfConjurationLogic().AttackBonus);
            Assert.AreEqual(1, new PathOfTheWardenLogic().DefenseBonus);
        }

        [Test]
        public void Max_health_comes_from_the_definition()
        {
            var def = new CharacterDefinition("tank", "坦克", SpecialLogicType.None, maxHealth: 30);
            PlayerState player = CharacterFactory.CreatePlayer("坦克", def);

            Assert.AreEqual(30, player.MaximumHealth);
            Assert.AreEqual(30, player.Health);
        }

        [Test]
        public void Default_max_health_is_twenty()
        {
            var player = new PlayerState("A");
            Assert.AreEqual(20, player.MaximumHealth);
            Assert.AreEqual(20, PlayerState.MaxHealth);
            Assert.AreEqual(20, PlayerState.DefaultMaxHealth);
        }

        [Test]
        public void Empty_definition_id_is_rejected()
        {
            Assert.Throws<System.ArgumentException>(
                () => new CharacterDefinition("", "无名", SpecialLogicType.None));
        }

        [Test]
        public void CreateMatch_builds_a_ready_game()
        {
            GameManager game = CharacterFactory.CreateMatch(
                CharacterDefinition.Breaker, CharacterDefinition.PathOfTheWarden);

            Assert.AreEqual(20, game.PlayerA.Health);
            Assert.AreEqual(20, game.PlayerB.Health);
            Assert.True(game.PlayerA.Character is BreakerLogic);
            Assert.True(game.PlayerB.Character is PathOfTheWardenLogic);
            Assert.False(game.IsGameOver);
            Assert.AreEqual(4, game.GetLegalMoves(game.PlayerA).Count);
        }
    }
}
