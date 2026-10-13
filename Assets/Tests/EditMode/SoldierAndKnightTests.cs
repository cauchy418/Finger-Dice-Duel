using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **士兵 / 骑士**的行为。
    ///
    /// 士兵：取代白板的基础角色（攻防 +1）。
    /// 骑士：自己的盾碎后攻击距离永久 +1，且**攻击加成恒等于距离**。
    /// </summary>
    [TestFixture]
    public class SoldierAndKnightTests
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
        // 士兵
        // ============================================================

        [Test]
        public void Soldier_has_plus_one_attack_and_defense()
        {
            PlayerState s = Make("士", CharacterDefinition.Soldier);

            Assert.AreEqual(1, s.Character.AttackBonus);
            Assert.AreEqual(1, s.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(s), "士兵攻击距离 1。");
        }

        [Test]
        public void Soldier_replaces_the_blank_in_the_built_in_roster()
        {
            // 玩家要求："将现在的白板更换为士兵"
            bool hasSoldier = false;
            bool hasBlank = false;

            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                if (def.Id == CharacterDefinition.SoldierId) hasSoldier = true;
                if (def.Id == CharacterDefinition.BlankId) hasBlank = true;
            }

            Assert.True(hasSoldier, "角色列表里应有士兵。");
            Assert.False(hasBlank, "白板不该再出现在角色列表里（已被士兵取代）。");
        }

        // ============================================================
        // 骑士
        // ============================================================

        [Test]
        public void Knight_starts_with_range_one_and_bonus_one()
        {
            PlayerState k = Make("骑", CharacterDefinition.Knight);

            Assert.AreEqual(-1, k.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(k), "骑士初始攻击距离 1。");
            Assert.AreEqual(1, k.Character.AttackBonus,
                "骑士初始攻击加成 = 攻击距离 = 1（不是 0）。");
        }

        [Test]
        public void Knight_gains_range_and_bonus_after_its_own_shield_breaks()
        {
            // 玩家确认："破碎一次盾牌后攻击距离和攻击加成都变为 2"
            GameManager game = Match(CharacterDefinition.Knight, CharacterDefinition.Soldier,
                                     out _, out _);
            PlayerState knight = game.PlayerA;
            var logic = knight.Character as KnightLogic;
            Assert.NotNull(logic, "骑士应实现 KnightLogic。");

            // 给骑士一面成型的盾，然后让它自己用掉（主动碎盾）
            knight.RightHand.SetValue(5);
            knight.RightHand.SetShieldState(ShieldState.Formed);

            Assert.AreEqual(0, logic.ShieldBreakCount, "前置：还没碎过盾。");

            // 用成型盾当运算源 -> 主动碎盾
            knight.LeftHand.SetValue(1);
            game.PlayerB.LeftHand.SetValue(2);
            game.ExecuteTurn(knight, knight.RightHand, game.PlayerB.LeftHand);

            Assert.AreEqual(1, logic.ShieldBreakCount, "应记录一次碎盾。");
            Assert.AreEqual(2, GameManager.BaseRangeOf(knight),
                "碎一次盾后攻击距离应为 2。");
            Assert.AreEqual(2, knight.Character.AttackBonus,
                "攻击加成恒等于距离 -> 也应为 2。");
        }

        [Test]
        public void Knight_attack_bonus_always_equals_its_range()
        {
            // "加成恒等于距离"是**结构上**保证的（两者接同一个计数），
            // 这条测试把它钉住，防止以后有人给加成另开一个计数器。
            GameManager game = Match(CharacterDefinition.Knight, CharacterDefinition.Soldier,
                                     out _, out _);
            PlayerState knight = game.PlayerA;
            var logic = knight.Character as KnightLogic;

            for (int breaks = 0; breaks <= 2; breaks++)
            {
                Assert.AreEqual(GameManager.BaseRangeOf(knight), knight.Character.AttackBonus,
                    $"碎盾 {breaks} 次后，加成应仍等于距离。");

                if (breaks == 2) break;

                // 造一次主动碎盾
                knight.RightHand.SetValue(5);
                knight.RightHand.SetShieldState(ShieldState.Formed);
                knight.LeftHand.SetValue(1);
                game.PlayerB.LeftHand.SetValue(2);
                game.ExecuteTurn(knight, knight.RightHand, game.PlayerB.LeftHand);
            }

            Assert.AreEqual(3, GameManager.BaseRangeOf(knight), "碎两次后距离应为 3。");
            Assert.AreEqual(3, knight.Character.AttackBonus, "加成同样为 3。");
        }

        [Test]
        public void Knight_resets_its_growth_on_a_new_match()
        {
            // 成长是跨回合保留的，但**新开一局必须清零**
            // （角色对象会被复用，不清零会带着上一局的加成开局）
            GameManager game = Match(CharacterDefinition.Knight, CharacterDefinition.Soldier,
                                     out _, out _);
            PlayerState knight = game.PlayerA;
            var logic = knight.Character as KnightLogic;

            knight.RightHand.SetValue(5);
            knight.RightHand.SetShieldState(ShieldState.Formed);
            knight.LeftHand.SetValue(1);
            game.PlayerB.LeftHand.SetValue(2);
            game.ExecuteTurn(knight, knight.RightHand, game.PlayerB.LeftHand);

            Assert.AreEqual(1, logic.ShieldBreakCount);

            game.ResetMatch();

            Assert.AreEqual(0, logic.ShieldBreakCount, "新开一局应清零碎盾计数。");
            Assert.AreEqual(1, GameManager.BaseRangeOf(knight), "距离回到 1。");
        }

        [Test]
        public void Knight_does_not_gain_from_a_teammates_broken_shield()
        {
            // 玩家确认："仅自己的盾碎才算"
            var knight = Make("骑", CharacterDefinition.Knight);
            var mate = Make("友", CharacterDefinition.Soldier);
            var foe1 = Make("敌1", CharacterDefinition.Soldier);
            var foe2 = Make("敌2", CharacterDefinition.Soldier);

            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(
                new List<PlayerState> { knight, mate, foe1, foe2 }, providers);

            var logic = knight.Character as KnightLogic;

            // 让队友的盾被打破（通过公共入口，模拟被打碎）
            mate.RightHand.SetValue(5);
            mate.RightHand.SetShieldState(ShieldState.Formed);
            game.BreakShieldAndNotify(mate, mate.RightHand);

            Assert.AreEqual(0, logic.ShieldBreakCount,
                "队友的盾碎不该给骑士加成长。");
            Assert.AreEqual(1, GameManager.BaseRangeOf(knight), "距离应仍是 1。");
        }
    }
}
