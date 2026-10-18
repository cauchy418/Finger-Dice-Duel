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
    ///   "初始的 5 除了巨人/武僧等 5 不是盾的角色外均带上破碎的盾的词条。"
    ///
    /// 为什么重要：修盾 / 抵挡 / 破盾连锁全都以 `ShieldState.Formed` 为前提，
    /// 开局那只 5 若是 `None`，战士的新盾就修不到它。
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
            GameManager game = Match(CharacterDefinition.Soldier,
                                     CharacterDefinition.Fighter,
                                     out PlayerState soldier, out _);

            SetOpeningValue(soldier.LeftHand, 5);
            Assert.AreEqual(ShieldState.None, soldier.LeftHand.State,
                "SetValue 之后还没有盾状态（这正是原来的 bug）。");

            game.BeginTurn();

            Assert.AreEqual(ShieldState.Broken, soldier.LeftHand.State,
                "开局掷出的 5 应当是**破碎的盾**。");
            Assert.IsFalse(soldier.LeftHand.IsShield,
                "它是破碎的，不是成型盾。");
            Assert.IsFalse(soldier.LeftHand.TryBlockAttack(),
                "破碎的盾挡不住攻击。");
        }

        [Test]
        public void A_formed_shield_is_never_downgraded()
        {
            GameManager game = Match(CharacterDefinition.Soldier,
                                     CharacterDefinition.Fighter,
                                     out PlayerState soldier, out _);

            soldier.LeftHand.SetValue(5);
            soldier.LeftHand.SetShieldState(ShieldState.Formed);

            game.BeginTurn();

            Assert.AreEqual(ShieldState.Formed, soldier.LeftHand.State,
                "已经成型的盾不能被'补词条'覆盖（方法必须是幂等的）。");
        }

        [Test]
        public void The_fighter_can_repair_the_initial_broken_shield()
        {
            GameManager game = Match(CharacterDefinition.Fighter,
                                     CharacterDefinition.Soldier,
                                     out PlayerState fighter, out PlayerState foe);

            // 开局：左手 5（破碎的盾），右手 3
            SetOpeningValue(fighter.LeftHand, 5);
            SetOpeningValue(fighter.RightHand, 3);
            game.BeginTurn();
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State);

            // 右手 3 + 对手 2 = 5 -> 合出一面**新盾**；战士的规则会把
            // 另一只手的"破碎的盾"顺手修好。
            foe.LeftHand.SetValue(2);
            game.ExecuteTurn(fighter, fighter.RightHand, foe.LeftHand);

            Assert.AreEqual(ShieldState.Formed, fighter.RightHand.State,
                "右手 3+2=5 应当合出新盾。");
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State,
                "战士合新盾时应当把原先那只破碎的盾修好。");
        }

        [Test]
        public void Giant_and_monk_fives_are_not_shields()
        {
            GameManager g = Match(CharacterDefinition.Giant,
                                  CharacterDefinition.Soldier,
                                  out PlayerState giant, out _);
            SetOpeningValue(giant.LeftHand, 5);
            g.BeginTurn();
            Assert.AreEqual(ShieldState.None, giant.LeftHand.State,
                "巨人的 5 是两段攻击，不是盾。");

            GameManager m = Match(CharacterDefinition.Monk,
                                  CharacterDefinition.Soldier,
                                  out PlayerState monk, out _);
            SetOpeningValue(monk.LeftHand, 5);
            m.BeginTurn();
            Assert.IsFalse(monk.LeftHand.State == ShieldState.Broken,
                "武僧的 5 是太极，不该被补成破碎的盾。");
        }
    }
}
