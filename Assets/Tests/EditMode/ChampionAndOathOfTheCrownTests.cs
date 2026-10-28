using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **战士·勇士 / 圣武士·王冠之誓**的行为。
    ///
    /// 战士·勇士：精通重击，攻+1 / 防0。
    /// 圣武士·王冠之誓：自己的盾碎后攻击距离永久 +1，且**攻击加成恒等于距离**。
    /// </summary>
    [TestFixture]
    public class ChampionAndOathOfTheCrownTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager Match(CharacterDefinition defA, CharacterDefinition defB,
                                         out SimpleChoiceProvider sinkA,
                                         out SimpleChoiceProvider sinkB)
        {
            sinkA = new SimpleChoiceProvider();
            sinkB = new SimpleChoiceProvider();

            return new GameManager(Make("A", defA), Make("B", defB), sinkA, sinkB);
        }

        // ============================================================
        // 战士·勇士
        // ============================================================

        [Test]
        public void Champion_has_plus_one_attack_and_zero_defense()
        {
            PlayerState s = Make("士", CharacterDefinition.Champion);

            Assert.AreEqual(1, s.Character.AttackBonus);
            Assert.AreEqual(0, s.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(s), "战士·勇士攻击距离 1。");
        }

        [Test]
        public void Champion_replaces_the_blank_in_the_built_in_roster()
        {
            // 玩家要求："将现在的白板更换为战士·勇士"
            bool hasChampion = false;
            bool hasBlank = false;

            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                if (def.Id == CharacterDefinition.ChampionId) hasChampion = true;
                if (def.Id == CharacterDefinition.BlankId) hasBlank = true;
            }

            Assert.True(hasChampion, "角色列表里应有战士·勇士。");
            Assert.False(hasBlank, "白板不该再出现在角色列表里（已被战士·勇士取代）。");
        }

        // ============================================================
        // 圣武士·王冠之誓
        // ============================================================

        [Test]
        public void OathOfTheCrown_starts_with_range_one_and_bonus_one()
        {
            PlayerState k = Make("骑", CharacterDefinition.OathOfTheCrown);

            Assert.AreEqual(-1, k.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(k), "圣武士·王冠之誓初始攻击距离 1。");
            Assert.AreEqual(1, k.Character.AttackBonus,
                "圣武士·王冠之誓初始攻击加成 = 攻击距离 = 1（不是 0）。");
        }

        [Test]
        public void OathOfTheCrown_gains_range_and_bonus_after_its_own_shield_breaks()
        {
            // 玩家确认："破碎一次盾牌后攻击距离和攻击加成都变为 2"
            GameManager game = Match(CharacterDefinition.OathOfTheCrown, CharacterDefinition.Champion,
                                     out _, out _);
            PlayerState oathOfTheCrown = game.PlayerA;
            var logic = oathOfTheCrown.Character as OathOfTheCrownLogic;
            Assert.NotNull(logic, "圣武士·王冠之誓应实现 OathOfTheCrownLogic。");

            // 给圣武士·王冠之誓一面成型的盾，然后让它自己用掉（主动碎盾）
            oathOfTheCrown.RightHand.SetValue(5);
            oathOfTheCrown.RightHand.SetShieldState(ShieldState.Formed);

            Assert.AreEqual(0, logic.ShieldBreakCount, "前置：还没碎过盾。");

            // 用成型盾当运算源 -> 主动碎盾
            oathOfTheCrown.LeftHand.SetValue(1);
            game.PlayerB.LeftHand.SetValue(2);
            game.ExecuteTurn(oathOfTheCrown, oathOfTheCrown.RightHand, game.PlayerB.LeftHand);

            Assert.AreEqual(1, logic.ShieldBreakCount, "应记录一次碎盾。");
            Assert.AreEqual(2, GameManager.BaseRangeOf(oathOfTheCrown),
                "碎一次盾后攻击距离应为 2。");
            Assert.AreEqual(2, oathOfTheCrown.Character.AttackBonus,
                "攻击加成恒等于距离 -> 也应为 2。");
        }

        [Test]
        public void OathOfTheCrown_attack_bonus_always_equals_its_range()
        {
            // "加成恒等于距离"是**结构上**保证的（两者接同一个计数），
            // 这条测试把它钉住，防止以后有人给加成另开一个计数器。
            GameManager game = Match(CharacterDefinition.OathOfTheCrown, CharacterDefinition.Champion,
                                     out _, out _);
            PlayerState oathOfTheCrown = game.PlayerA;
            var logic = oathOfTheCrown.Character as OathOfTheCrownLogic;

            for (int breaks = 0; breaks <= 2; breaks++)
            {
                Assert.AreEqual(GameManager.BaseRangeOf(oathOfTheCrown), oathOfTheCrown.Character.AttackBonus,
                    $"碎盾 {breaks} 次后，加成应仍等于距离。");

                if (breaks == 2) break;

                // 造一次主动碎盾
                oathOfTheCrown.RightHand.SetValue(5);
                oathOfTheCrown.RightHand.SetShieldState(ShieldState.Formed);
                oathOfTheCrown.LeftHand.SetValue(1);
                game.PlayerB.LeftHand.SetValue(2);
                game.ExecuteTurn(oathOfTheCrown, oathOfTheCrown.RightHand, game.PlayerB.LeftHand);
            }

            Assert.AreEqual(3, GameManager.BaseRangeOf(oathOfTheCrown), "碎两次后距离应为 3。");
            Assert.AreEqual(3, oathOfTheCrown.Character.AttackBonus, "加成同样为 3。");
        }

        [Test]
        public void OathOfTheCrown_resets_its_growth_on_a_new_match()
        {
            // 成长是跨回合保留的，但**新开一局必须清零**
            // （角色对象会被复用，不清零会带着上一局的加成开局）
            GameManager game = Match(CharacterDefinition.OathOfTheCrown, CharacterDefinition.Champion,
                                     out _, out _);
            PlayerState oathOfTheCrown = game.PlayerA;
            var logic = oathOfTheCrown.Character as OathOfTheCrownLogic;

            oathOfTheCrown.RightHand.SetValue(5);
            oathOfTheCrown.RightHand.SetShieldState(ShieldState.Formed);
            oathOfTheCrown.LeftHand.SetValue(1);
            game.PlayerB.LeftHand.SetValue(2);
            game.ExecuteTurn(oathOfTheCrown, oathOfTheCrown.RightHand, game.PlayerB.LeftHand);

            Assert.AreEqual(1, logic.ShieldBreakCount);

            game.ResetMatch();

            Assert.AreEqual(0, logic.ShieldBreakCount, "新开一局应清零碎盾计数。");
            Assert.AreEqual(1, GameManager.BaseRangeOf(oathOfTheCrown), "距离回到 1。");
        }

        [Test]
        public void OathOfTheCrown_does_not_gain_from_a_teammates_broken_shield()
        {
            // 玩家确认："仅自己的盾碎才算"
            var oathOfTheCrown = Make("骑", CharacterDefinition.OathOfTheCrown);
            var mate = Make("友", CharacterDefinition.Champion);
            var foe1 = Make("敌1", CharacterDefinition.Champion);
            var foe2 = Make("敌2", CharacterDefinition.Champion);

            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(
                new List<PlayerState> { oathOfTheCrown, mate, foe1, foe2 }, providers);

            var logic = oathOfTheCrown.Character as OathOfTheCrownLogic;

            // 让队友的盾被打破（通过公共入口，模拟被打碎）
            mate.RightHand.SetValue(5);
            mate.RightHand.SetShieldState(ShieldState.Formed);
            game.BreakShieldAndNotify(mate, mate.RightHand);

            Assert.AreEqual(0, logic.ShieldBreakCount,
                "队友的盾碎不该给圣武士·王冠之誓加成长。");
            Assert.AreEqual(1, GameManager.BaseRangeOf(oathOfTheCrown), "距离应仍是 1。");
        }
    }
}
