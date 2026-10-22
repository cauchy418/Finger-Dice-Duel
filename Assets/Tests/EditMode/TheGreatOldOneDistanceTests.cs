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
    /// **邪术师·旧日支配者（TheGreatOldOne）**：命中（闪避/取消除外）令自己与目标的距离 +2，
    /// 持续目标自己的 2 个回合；刷新不叠加、不封顶；只影响这一对。
    /// </summary>
    [TestFixture]
    public class TheGreatOldOneDistanceTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        [Test]
        public void TheGreatOldOne_stats_and_infinite_range()
        {
            PlayerState p = Make("邪", CharacterDefinition.TheGreatOldOne);
            Assert.AreEqual(0, p.Character.AttackBonus);
            Assert.AreEqual(-1, p.Character.DefenseBonus);
            Assert.AreEqual(CharacterTemplate.InfiniteAttackRange,
                GameManager.BaseRangeOf(p));
        }

        [Test]
        public void TheGreatOldOnes_normal_attack_is_slashing_near_and_piercing_far()
        {
            PlayerState w = Make("邪", CharacterDefinition.TheGreatOldOne);
            var logic = (TheGreatOldOneLogic)w.Character;

            ((CharacterTemplate)w.Character).LastAttackDistance = 1;
            Assert.AreEqual(AttackDamageType.Slashing, logic.AttackTypeOf(null));

            ((CharacterTemplate)w.Character).LastAttackDistance = 3;
            Assert.AreEqual(AttackDamageType.Piercing, logic.AttackTypeOf(null));
        }

        [Test]
        public void A_hit_pushes_the_pair_distance_up_by_two()
        {
            PlayerState w = Make("邪", CharacterDefinition.TheGreatOldOne);
            PlayerState foe = Make("战", CharacterDefinition.Champion);
            var game = new GameManager(w, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            Assert.AreEqual(1, game.DistanceBetween(w, foe), "1v1 初始距离 1。");

            w.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);              // 1 + 3 = 4 -> 攻击
            game.ExecuteTurn(w, w.LeftHand, foe.LeftHand);

            Assert.AreEqual(3, game.DistanceBetween(w, foe),
                "命中之后这一对的距离应当变成 3（+2）。");
        }

        [Test]
        public void The_locked_target_can_no_longer_reach_the_warlock()
        {
            PlayerState w = Make("邪", CharacterDefinition.TheGreatOldOne);
            PlayerState foe = Make("战", CharacterDefinition.Champion);   // 射程 1
            var game = new GameManager(w, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            w.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);
            game.ExecuteTurn(w, w.LeftHand, foe.LeftHand);

            Assert.IsFalse(game.CanReach(foe, w),
                "距离 3 > 战士·勇士射程 1，他这两个回合打不到邪术师·旧日支配者。");
        }

        [Test]
        public void The_penalty_expires_after_two_of_the_targets_turns()
        {
            PlayerState w = Make("邪", CharacterDefinition.TheGreatOldOne);
            PlayerState foe = Make("战", CharacterDefinition.Champion);
            var game = new GameManager(w, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            w.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);
            game.ExecuteTurn(w, w.LeftHand, foe.LeftHand);
            Assert.AreEqual(3, game.DistanceBetween(w, foe));

            // ⚠️ 回合边界要**显式**推进：`ExecuteTurn` 只跑运算，
            //    状态 / 距离的计时由 `ResolveStatusTurnEnd` 在"换人"时推进
            //    （界面路径由 BoardController 调，无头路径由 MatchRunner 调）。
            //    这里直连 `ExecuteTurn`，所以必须自己调 —— 之前漏了这一步，
            //    看起来像"计时不生效"，其实是测试没走真实路径。

            // 目标自己的回合 ①
            foe.LeftHand.SetValue(1);
            w.LeftHand.SetValue(1);
            game.ExecuteTurn(foe, foe.LeftHand, w.LeftHand);
            game.ResolveStatusTurnEnd(foe);
            Assert.AreEqual(3, game.DistanceBetween(w, foe), "第 1 个回合后还在。");

            // 目标自己的回合 ②
            game.ExecuteTurn(foe, foe.LeftHand, w.LeftHand);
            game.ResolveStatusTurnEnd(foe);

            Assert.AreEqual(0, game.DistancePenaltyBetween(w, foe),
                "第 2 个回合结束后修正应当清零。");
            Assert.AreEqual(1, game.DistanceBetween(w, foe),
                "第 2 个回合结束后距离恢复成 1。");
        }

        [Test]
        public void Hitting_again_refreshes_but_does_not_stack()
        {
            PlayerState w = Make("邪", CharacterDefinition.TheGreatOldOne);
            PlayerState foe = Make("战", CharacterDefinition.Champion);
            var game = new GameManager(w, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            w.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);
            game.ExecuteTurn(w, w.LeftHand, foe.LeftHand);
            Assert.AreEqual(3, game.DistanceBetween(w, foe));

            // 再命中一次 —— 依然是 3（+2 封顶），不是 5
            game.ExecuteTurn(w, w.LeftHand, foe.LeftHand);
            Assert.AreEqual(3, game.DistanceBetween(w, foe),
                "重复命中只刷新时长，不叠加。");
        }

        [Test]
        public void The_penalty_does_not_affect_other_pairs()
        {
            // 2v2：邪术师·旧日支配者打 A 之后，A 与队友之间的距离不该变
            var seated = new List<PlayerState>
            {
                Make("P1邪", CharacterDefinition.TheGreatOldOne),
                Make("P2战", CharacterDefinition.Champion),
                Make("P3剑", CharacterDefinition.Swordsman),
                Make("P4巨", CharacterDefinition.PathOfTheGiant),
            };
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(seated, providers);

            PlayerState w = seated[0];
            PlayerState enemy = seated[1];
            PlayerState ally = seated[3];

            int allyToEnemyBefore = game.DistanceBetween(ally, enemy);

            w.LeftHand.SetValue(1);
            enemy.LeftHand.SetValue(3);
            game.ExecuteTurn(w, w.LeftHand, enemy.LeftHand);

            Assert.AreEqual(3, game.DistanceBetween(w, enemy));
            Assert.AreEqual(allyToEnemyBefore, game.DistanceBetween(ally, enemy),
                "距离修正只影响邪术师·旧日支配者与目标这一对。");
        }

        [Test]
        public void A_dodged_attack_does_not_raise_the_distance()
        {
            PlayerState w = Make("邪", CharacterDefinition.TheGreatOldOne);
            PlayerState swashbuckler = Make("游", CharacterDefinition.Swashbuckler);
            var game = new GameManager(w, swashbuckler, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            // 游荡者·风流剑客用一张 1 闪避
            swashbuckler.LeftHand.SetValue(1);
            swashbuckler.RightHand.SetValue(1);
            w.LeftHand.SetValue(1);
            swashbuckler.LeftHand.SetValue(3);            // 1 + 3 = 4

            TurnResult t = game.ExecuteTurn(w, w.LeftHand, swashbuckler.LeftHand);

            if (t.WasDodged)
                Assert.AreEqual(1, game.DistanceBetween(w, swashbuckler),
                    "被闪避不算命中，距离不该变。");
            else
                Assert.AreEqual(3, game.DistanceBetween(w, swashbuckler));
        }
    }
}
