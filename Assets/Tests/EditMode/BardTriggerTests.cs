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
    /// **吟游诗人的真实触发链路**（不是手工置标记）：
    ///
    ///   真实攻击造成伤害 / 真实 9 回血
    ///     -> `TookDamageThisTurn` / `HealedThisTurn`
    ///     -> `BardLogic.CanActivateFor`
    ///     -> `BoardController.OfferBardChoiceIfAny` 或 `BardAi.TryActivate`
    ///
    /// 专门覆盖"触发"这一层；四步选择本身见 `BardChoiceFlowTests`。
    /// 邪术师 / 术士的自动触发不在这里重复测（它们没有可选择的触发窗口）。
    /// </summary>
    [TestFixture]
    public class BardTriggerTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager Match(out PlayerState bard, out PlayerState foe)
        {
            bard = Make("吟", CharacterDefinition.Bard);
            foe = Make("战", CharacterDefinition.Soldier);
            return new GameManager(bard, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        [Test]
        public void Real_attack_damage_enables_the_trigger_and_the_board_offer()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                             new SimpleChoiceProvider());

            game.BeginTurn();
            bard.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);              // 战士 3 + 1 = 4 攻击；吟游诗人防 −1

            game.ExecuteTurn(foe, foe.LeftHand, bard.LeftHand);

            Assert.True(bard.Health < bard.MaximumHealth, "应当真的挨了伤害。");
            Assert.IsTrue(bard.TookDamageThisTurn, "真实伤害必须置本回合受伤标记。");
            Assert.IsTrue(BardLogic.CanActivateFor(bard));
            Assert.IsTrue(board.OfferBardChoiceIfAny(), "受伤后应弹出四步选择。");
        }

        [Test]
        public void Real_heal_of_nine_enables_the_trigger_and_ai_activation()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            bard.SetHealthExact(10);
            bard.LeftHand.SetValue(4);
            foe.LeftHand.SetValue(5);              // 4 + 5 = 9 -> 回血（目标手必须是对手的）

            game.BeginTurn();
            game.ExecuteTurn(bard, bard.LeftHand, foe.LeftHand);

            Assert.AreEqual(19, bard.Health, "9 回血应实际生效。");
            Assert.IsTrue(bard.HealedThisTurn, "实际回血必须置标记。");
            Assert.IsTrue(BardLogic.CanActivateFor(bard));
            Assert.IsTrue(BardAi.TryActivate(bard, game), "AI 应能接上这次触发。");
            Assert.AreEqual(-3, foe.BardDefenseOffset,
                "1v1 里 AI 默认削弱唯一对手的防御，强度 −3。");
        }

        [Test]
        public void Real_full_health_heal_does_not_enable_the_trigger()
        {
            GameManager game = Match(out PlayerState bard, out PlayerState foe);
            bard.SetHealthExact(bard.MaximumHealth);
            bard.LeftHand.SetValue(4);
            foe.LeftHand.SetValue(5);

            game.BeginTurn();
            game.ExecuteTurn(bard, bard.LeftHand, foe.LeftHand);

            Assert.AreEqual(bard.MaximumHealth, bard.Health);
            Assert.IsFalse(bard.HealedThisTurn, "满血回复不算实际回血。");
            Assert.IsFalse(BardLogic.CanActivateFor(bard));
            Assert.IsFalse(BardAi.TryActivate(bard, game));
        }

        [Test]
        public void Status_damage_at_turn_start_enables_the_trigger()
        {
            GameManager game = Match(out PlayerState bard, out _);
            bard.SetHealthExact(10);
            bard.ApplyStatus(StatusEffect.Poison, 2);

            game.ResolveStatusTurnStart(bard);

            Assert.AreEqual(8, bard.Health, "中毒回合开始应受 2 点不可抵挡伤害。");
            Assert.IsTrue(bard.TookDamageThisTurn, "状态伤害也要算“本回合受过伤害”。");
            Assert.IsTrue(BardLogic.CanActivateFor(bard));
        }

        [Test]
        public void Teammate_damage_does_not_enable_the_bards_trigger()
        {
            var seated = new List<PlayerState>
            {
                Make("P1吟", CharacterDefinition.Bard),
                Make("P2术", CharacterDefinition.Sorcerer),
                Make("P3战", CharacterDefinition.Soldier),
                Make("P4战", CharacterDefinition.Soldier),   // P1 的队友
            };
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(seated, providers);

            PlayerState bard = seated[0];
            PlayerState teammate = seated[3];
            PlayerState enemy = seated[1];

            // 术士无限射程，确保能打到对面的 P4；固定 99 = 普攻，避免元素干扰。
            ((SorcererLogic)enemy.Character).ForcedRoll = 99;
            enemy.LeftHand.SetValue(3);
            teammate.LeftHand.SetValue(1);

            game.BeginTurn();
            game.ExecuteTurn(enemy, enemy.LeftHand, teammate.LeftHand);

            Assert.True(teammate.Health < teammate.MaximumHealth, "队友应真的受伤。");
            Assert.IsTrue(teammate.TookDamageThisTurn);
            Assert.IsFalse(bard.TookDamageThisTurn, "队友受伤不应给吟游诗人置触发标记。");
            Assert.IsFalse(BardLogic.CanActivateFor(bard));
            Assert.IsFalse(BardAi.TryActivate(bard, game));
        }
    }
}
