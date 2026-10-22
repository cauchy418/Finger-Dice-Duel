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
    /// **吟游诗人·勇气学院的真实触发链路**（不是手工置标记）：
    ///
    ///   真实攻击造成伤害 / 真实 9 回血
    ///     -> `TookDamageThisTurn` / `HealedThisTurn`
    ///     -> `CollegeOfValorLogic.CanActivateFor`
    ///     -> `BoardController.OfferCollegeOfValorChoiceIfAny` 或 `CollegeOfValorAi.TryActivate`
    ///
    /// 专门覆盖"触发"这一层；四步选择本身见 `CollegeOfValorChoiceFlowTests`。
    /// 邪术师·旧日支配者 / 术士·狂野魔法的自动触发不在这里重复测（它们没有可选择的触发窗口）。
    /// </summary>
    [TestFixture]
    public class CollegeOfValorTriggerTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager Match(out PlayerState collegeOfValor, out PlayerState foe)
        {
            collegeOfValor = Make("吟", CharacterDefinition.CollegeOfValor);
            foe = Make("战", CharacterDefinition.Champion);
            return new GameManager(collegeOfValor, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
        }

        [Test]
        public void Real_attack_damage_enables_the_trigger_and_the_board_offer()
        {
            GameManager game = Match(out PlayerState collegeOfValor, out PlayerState foe);
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                             new SimpleChoiceProvider());

            game.BeginTurn();
            collegeOfValor.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(3);              // 战士·破阵者 3 + 1 = 4 攻击；吟游诗人·勇气学院防 −1

            game.ExecuteTurn(foe, foe.LeftHand, collegeOfValor.LeftHand);

            Assert.True(collegeOfValor.Health < collegeOfValor.MaximumHealth, "应当真的挨了伤害。");
            Assert.IsTrue(collegeOfValor.TookDamageThisTurn, "真实伤害必须置本回合受伤标记。");
            Assert.IsTrue(CollegeOfValorLogic.CanActivateFor(collegeOfValor));
            Assert.IsTrue(board.OfferCollegeOfValorChoiceIfAny(), "受伤后应弹出四步选择。");
        }

        [Test]
        public void Real_heal_of_nine_enables_the_trigger_and_ai_activation()
        {
            GameManager game = Match(out PlayerState collegeOfValor, out PlayerState foe);
            collegeOfValor.SetHealthExact(10);
            collegeOfValor.LeftHand.SetValue(4);
            foe.LeftHand.SetValue(5);              // 4 + 5 = 9 -> 回血（目标手必须是对手的）

            game.BeginTurn();
            game.ExecuteTurn(collegeOfValor, collegeOfValor.LeftHand, foe.LeftHand);

            Assert.AreEqual(19, collegeOfValor.Health, "9 回血应实际生效。");
            Assert.IsTrue(collegeOfValor.HealedThisTurn, "实际回血必须置标记。");
            Assert.IsTrue(CollegeOfValorLogic.CanActivateFor(collegeOfValor));
            Assert.IsTrue(CollegeOfValorAi.TryActivate(collegeOfValor, game), "AI 应能接上这次触发。");
            Assert.AreEqual(-3, foe.CollegeOfValorDefenseOffset,
                "1v1 里 AI 默认削弱唯一对手的防御，强度 −3。");
        }

        [Test]
        public void Real_full_health_heal_does_not_enable_the_trigger()
        {
            GameManager game = Match(out PlayerState collegeOfValor, out PlayerState foe);
            collegeOfValor.SetHealthExact(collegeOfValor.MaximumHealth);
            collegeOfValor.LeftHand.SetValue(4);
            foe.LeftHand.SetValue(5);

            game.BeginTurn();
            game.ExecuteTurn(collegeOfValor, collegeOfValor.LeftHand, foe.LeftHand);

            Assert.AreEqual(collegeOfValor.MaximumHealth, collegeOfValor.Health);
            Assert.IsFalse(collegeOfValor.HealedThisTurn, "满血回复不算实际回血。");
            Assert.IsFalse(CollegeOfValorLogic.CanActivateFor(collegeOfValor));
            Assert.IsFalse(CollegeOfValorAi.TryActivate(collegeOfValor, game));
        }

        [Test]
        public void Status_damage_at_turn_start_enables_the_trigger()
        {
            GameManager game = Match(out PlayerState collegeOfValor, out _);
            collegeOfValor.SetHealthExact(10);
            collegeOfValor.ApplyStatus(StatusEffect.Poison, 2);

            game.ResolveStatusTurnStart(collegeOfValor);

            Assert.AreEqual(8, collegeOfValor.Health, "中毒回合开始应受 2 点不可抵挡伤害。");
            Assert.IsTrue(collegeOfValor.TookDamageThisTurn, "状态伤害也要算“本回合受过伤害”。");
            Assert.IsTrue(CollegeOfValorLogic.CanActivateFor(collegeOfValor));
        }

        [Test]
        public void Teammate_damage_does_not_enable_the_bards_trigger()
        {
            var seated = new List<PlayerState>
            {
                Make("P1吟", CharacterDefinition.CollegeOfValor),
                Make("P2术", CharacterDefinition.WildMagic),
                Make("P3战", CharacterDefinition.Champion),
                Make("P4战", CharacterDefinition.Champion),   // P1 的队友
            };
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(seated, providers);

            PlayerState collegeOfValor = seated[0];
            PlayerState teammate = seated[3];
            PlayerState enemy = seated[1];

            // 术士·狂野魔法无限射程，确保能打到对面的 P4；固定 99 = 普攻，避免元素干扰。
            ((WildMagicLogic)enemy.Character).ForcedRoll = 99;
            enemy.LeftHand.SetValue(3);
            teammate.LeftHand.SetValue(1);

            game.BeginTurn();
            game.ExecuteTurn(enemy, enemy.LeftHand, teammate.LeftHand);

            Assert.True(teammate.Health < teammate.MaximumHealth, "队友应真的受伤。");
            Assert.IsTrue(teammate.TookDamageThisTurn);
            Assert.IsFalse(collegeOfValor.TookDamageThisTurn, "队友受伤不应给吟游诗人·勇气学院置触发标记。");
            Assert.IsFalse(CollegeOfValorLogic.CanActivateFor(collegeOfValor));
            Assert.IsFalse(CollegeOfValorAi.TryActivate(collegeOfValor, game));
        }
    }
}
