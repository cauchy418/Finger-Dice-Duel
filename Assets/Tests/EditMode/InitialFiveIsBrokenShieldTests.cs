using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **开局掷出的 5 是"破碎的盾"。**
    ///
    /// 玩家确认：
    ///   "初始的 5 除了野蛮人·巨人道途/武僧·太极宗等 5 不是盾的角色外均带上破碎的盾的词条。"
    ///
    /// 为什么重要：修盾 / 抵挡 / 破盾连锁全都以 `ShieldState.Formed` 为前提，
    /// 开局那只 5 若是 `None`，战士·破阵者的新盾就修不到它。
    /// </summary>
    [TestFixture]
    public class InitialFiveIsBrokenShieldTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager Match(CharacterDefinition a, CharacterDefinition b,
                                         out PlayerState pa, out PlayerState pb)
        {
            pa = Make("A", a);
            pb = Make("B", b);
            return new GameManager(pa, pb, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        /// <summary>模拟"开局掷出 5"：直接把手值设成 5（状态仍是 None）。</summary>
        private static void SetOpeningValue(Hand hand, int value)
        {
            hand.Reset();            // 清掉可能残留的状态
            hand.SetValue(value);
        }

        [Test]
        public void An_initial_five_becomes_a_broken_shield()
        {
            GameManager game = Match(CharacterDefinition.Champion,
                                     CharacterDefinition.Breaker,
                                     out PlayerState champion, out _);

            SetOpeningValue(champion.LeftHand, 5);
            Assert.AreEqual(ShieldState.None, champion.LeftHand.State,
                "SetValue 之后还没有盾状态（这正是原来的 bug）。");

            game.BeginTurn();

            Assert.AreEqual(ShieldState.Broken, champion.LeftHand.State,
                "开局掷出的 5 应当是**破碎的盾**。");
            Assert.IsFalse(champion.LeftHand.IsShield,
                "它是破碎的，不是成型盾。");
            Assert.IsFalse(champion.LeftHand.TryBlockAttack(),
                "破碎的盾挡不住攻击。");
        }

        [Test]
        public void A_formed_shield_is_never_downgraded()
        {
            GameManager game = Match(CharacterDefinition.Champion,
                                     CharacterDefinition.Breaker,
                                     out PlayerState champion, out _);

            champion.LeftHand.SetValue(5);
            champion.LeftHand.SetShieldState(ShieldState.Formed);

            game.BeginTurn();

            Assert.AreEqual(ShieldState.Formed, champion.LeftHand.State,
                "已经成型的盾不能被'补词条'覆盖（方法必须是幂等的）。");
        }

        [Test]
        public void The_fighter_can_repair_the_initial_broken_shield()
        {
            GameManager game = Match(CharacterDefinition.Breaker,
                                     CharacterDefinition.Champion,
                                     out PlayerState breaker, out PlayerState foe);

            // 开局：左手 5（破碎的盾），右手 3
            SetOpeningValue(breaker.LeftHand, 5);
            SetOpeningValue(breaker.RightHand, 3);
            game.BeginTurn();
            Assert.AreEqual(ShieldState.Broken, breaker.LeftHand.State);

            // 右手 3 + 对手 2 = 5 -> 合出一面**新盾**；战士·破阵者的规则会把
            // 另一只手的"破碎的盾"顺手修好。
            foe.LeftHand.SetValue(2);
            game.ExecuteTurn(breaker, breaker.RightHand, foe.LeftHand);

            Assert.AreEqual(ShieldState.Formed, breaker.RightHand.State,
                "右手 3+2=5 应当合出新盾。");
            Assert.AreEqual(ShieldState.Formed, breaker.LeftHand.State,
                "战士·破阵者合新盾时应当把原先那只破碎的盾修好。");
        }

        [Test]
        public void PathOfTheGiant_and_monk_fives_are_not_shields()
        {
            GameManager g = Match(CharacterDefinition.PathOfTheGiant,
                                  CharacterDefinition.Champion,
                                  out PlayerState pathOfTheGiant, out _);
            SetOpeningValue(pathOfTheGiant.LeftHand, 5);
            g.BeginTurn();
            Assert.AreEqual(ShieldState.None, pathOfTheGiant.LeftHand.State,
                "野蛮人·巨人道途的 5 是两段攻击，不是盾。");

            GameManager m = Match(CharacterDefinition.WayOfTheTaiChi,
                                  CharacterDefinition.Champion,
                                  out PlayerState wayOfTheTaiChi, out _);
            SetOpeningValue(wayOfTheTaiChi.LeftHand, 5);
            m.BeginTurn();
            Assert.IsFalse(wayOfTheTaiChi.LeftHand.State == ShieldState.Broken,
                "武僧·太极宗的 5 是太极，不该被补成破碎的盾。");
        }
    }
}
