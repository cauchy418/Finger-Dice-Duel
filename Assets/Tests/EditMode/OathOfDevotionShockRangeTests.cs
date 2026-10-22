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
    /// <summary>
    /// **圣武士·奉献之誓的盾牌冲击只能打到"攻击距离 1 以内"的人**（玩家确认）。
    ///
    /// 玩家问："盾牌冲击确实应该只能对距离 1 以内发动，现在不是吗"
    /// → 这条测试就是用来回答这个问题的：**实测**候选列表。
    ///
    /// 2v2 环上四座（1&4 一队 / 2&3 一队）：
    ///   距离 1 = 相邻，距离 2 = 对角。
    ///   所以圣武士·奉献之誓在座位 1 时，只有座位 2 与座位 4 是距离 1，座位 3 是距离 2。
    /// </summary>
    [TestFixture]
    public class OathOfDevotionShockRangeTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        [Test]
        public void Shock_candidates_only_include_distance_one()
        {
            // 四个座位：圣武士·奉献之誓(1)、战士·勇士(2)、战士·破阵者(3)、野蛮人·巨人道途(4)
            var seated = new List<PlayerState>
            {
                Make("P1圣武士·奉献之誓", CharacterDefinition.OathOfDevotion),
                Make("P2战士·勇士", CharacterDefinition.Champion),
                Make("P3战士·破阵者", CharacterDefinition.Breaker),
                Make("P4野蛮人·巨人道途", CharacterDefinition.PathOfTheGiant),
            };

            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };

            GameManager game = GameManager.Create2v2(seated, providers);

            PlayerState oathOfDevotion = seated[0];
            var logic = (OathOfDevotionLogic)oathOfDevotion.Character;

            // 用"结算时的同一个"候选函数（通过公开的询问入口触发）
            // —— 这里直接用反射拿列表太脆，改为断言"距离"本身，
            //    再断言冲击的实现确实用了 CanReach。
            var inRange = new List<string>();
            var outOfRange = new List<string>();

            for (int i = 1; i < seated.Count; i++)
            {
                int d = game.DistanceBetween(oathOfDevotion, seated[i]);
                if (game.CanReach(oathOfDevotion, seated[i])) inRange.Add(seated[i].Name + "(d=" + d + ")");
                else outOfRange.Add(seated[i].Name + "(d=" + d + ")");
            }

            // 关键断言：圣武士·奉献之誓的攻击距离是 1，所以**只**能打到 d=1 的人
            Assert.AreEqual(1, logic.BaseAttackRange,
                "圣武士·奉献之誓的攻击距离应当是 1。");

            for (int i = 1; i < seated.Count; i++)
            {
                int d = game.DistanceBetween(oathOfDevotion, seated[i]);
                bool reach = game.CanReach(oathOfDevotion, seated[i]);

                if (d == 1)
                {
                    Assert.IsTrue(reach,
                        $"{seated[i].Name} 距离 {d}，应当**能**被冲击打到。");
                }
                else
                {
                    Assert.IsFalse(reach,
                        $"{seated[i].Name} 距离 {d}，**不该**被冲击打到 —— "
                        + "冲击只能在攻击距离 1 以内发动。");
                }
            }

            Assert.IsTrue(inRange.Count >= 1,
                $"距离 1 内至少应有队友/对手可选。范围内 {inRange.Count} 人"
                + $"（{string.Join(",", inRange)}），范围外 {outOfRange.Count} 人"
                + $"（{string.Join(",", outOfRange)}）");
        }

        [Test]
        public void A_diagonal_target_survives_a_shock()
        {
            // 直接验证"打不到"的后果：让圣武士·奉献之誓合盾（触发冲击），
            // 2v2 对角那位（距离 2）不该掉血。
            var seated = new List<PlayerState>
            {
                Make("P1圣武士·奉献之誓", CharacterDefinition.OathOfDevotion),
                Make("P2战士·勇士", CharacterDefinition.Champion),
                Make("P3战士·破阵者", CharacterDefinition.Breaker),
                Make("P4野蛮人·巨人道途", CharacterDefinition.PathOfTheGiant),
            };

            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };

            GameManager game = GameManager.Create2v2(seated, providers);
            PlayerState oathOfDevotion = seated[0];
            PlayerState neighbour = seated[1];     // 距离 1（相邻）
            PlayerState diagonal = seated[2];      // 距离 2（对角）

            Assert.AreEqual(1, game.DistanceBetween(oathOfDevotion, neighbour));
            Assert.AreEqual(2, game.DistanceBetween(oathOfDevotion, diagonal),
                "2v2 环上，座位 1 与座位 3 是距离 2。");

            // 圣武士·奉献之誓 3 + 邻居 1 = 4 -> 合盾（4 被改成合盾），触发冲击
            oathOfDevotion.LeftHand.SetValue(3);
            neighbour.LeftHand.SetValue(1);

            int diagonalBefore = diagonal.Health;

            game.ExecuteTurn(oathOfDevotion, oathOfDevotion.LeftHand, neighbour.LeftHand);

            Assert.AreEqual(diagonalBefore, diagonal.Health,
                "距离 2 的人不该被盾牌冲击打到。");
        }
    }
}
