using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 击杀奖惩：
    ///   · 不同阵营击杀 -> 行动方额外行动 X 次（X = 受害者阵营剩余存活人数）；
    ///   · 同阵营击杀   -> 其他阵营每个存活角色各行动一次；
    ///   · 奖惩行动不推进 TurnCount / RoundCount，但战报要记录。
    /// </summary>
    [TestFixture]
    public class KillRewardTests
    {
        private static GameManager MatchEight(out List<PlayerState> seated)
        {
            seated = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                seated.Add(CharacterFactory.CreatePlayer("P" + (i + 1),
                                                         CharacterDefinition.Blank));

            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());

            return GameManager.CreateEight(seated, providers,
                new List<int> { 0, 0, 0, 1, 1, 1, 2, 2 });
        }

        private static BoardController Controller(GameManager game,
                                                  IReadOnlyList<PlayerState> seated)
        {
            var sink = new SimpleChoiceProvider();
            var controller = new BoardController(game, sink);
            for (int i = 0; i < seated.Count; i++)
                controller.BindPlayer(seated[i], new GreedyChoiceProvider(), sink);
            controller.SetFirstPlayer(seated[0]);
            controller.BeginTurn();
            return controller;
        }

        [Test]
        public void Extra_action_does_not_advance_turn_or_round_counters()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);
            game.BeginTurn();

            seated[0].LeftHand.SetValue(3);
            seated[1].LeftHand.SetValue(1);

            int turns = game.TurnCount;
            int rounds = game.RoundCount;
            PlayerState current = game.CurrentPlayer;

            TurnResult result = game.ExecuteTurn(seated[0], seated[0].LeftHand,
                                                 seated[1].LeftHand,
                                                 extraAction: true);

            Assert.IsTrue(result.IsExtraAction);
            Assert.AreEqual(turns, game.TurnCount, "额外行动不增加总行动数。");
            Assert.AreEqual(rounds, game.RoundCount, "额外行动不推进轮次。");
            Assert.AreSame(current, game.CurrentPlayer, "额外行动不切换正常的下一位。");
        }

        [Test]
        public void Killing_an_enemy_queues_extra_actions_equal_to_remaining_faction()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);
            BoardController controller = Controller(game, seated);

            seated[0].LeftHand.SetValue(3);
            seated[0].RightHand.SetValue(1);
            for (int i = 1; i < seated.Count; i++)
            {
                seated[i].SetHealthExact(1);
                seated[i].LeftHand.SetValue(1);
                seated[i].RightHand.SetValue(1);
            }

            Assert.IsTrue(controller.MakeAutomaticMove(), "AI 应能完成一次攻击。");
            Assert.True(controller.LastTurn != null, "应产生结算结果。");
            PlayerState victim = controller.LastTurn.Defender;
            Assert.True(victim != null, "应记录真实受击者。");
            Assert.IsFalse(victim.IsAlive, "测试前提：这一击必须击杀目标。");
            Assert.AreNotEqual(0, game.TeamOf(victim), "击杀的应是敌方阵营。");

            int expected = game.AliveTeamCount(game.TeamOf(victim));
            int pending = controller.ExtraActionsRemaining
                          + (controller.IsExtraAction ? 1 : 0);
            Assert.AreEqual(expected, pending,
                "额外行动次数应等于受害者阵营击杀后的剩余人数（含已经开始的那一次）。");
            Assert.IsTrue(controller.IsExtraAction);
            Assert.AreEqual(1, game.TurnCount);

            int turns = game.TurnCount;
            int rounds = game.RoundCount;
            Assert.IsTrue(controller.MakeAutomaticMove(), "AI 应自动完成额外行动。");
            Assert.AreEqual(turns, game.TurnCount, "额外行动不能增加总行动数。");
            Assert.AreEqual(rounds, game.RoundCount, "额外行动不能推进轮次。");

            bool logged = false;
            for (int i = 0; i < controller.Log.Count; i++)
                if (controller.Log[i].Contains("【额外行动】")) logged = true;
            Assert.IsTrue(logged, "战报必须反映额外行动。");
        }

        [Test]
        public void Killing_a_teammate_queues_other_factions_from_the_next_seat()
        {
            GameManager game = MatchEight(out List<PlayerState> seated);
            BoardController controller = Controller(game, seated);

            seated[0].LeftHand.SetValue(3);      // 战士·破阵者（守序）
            seated[1].LeftHand.SetValue(1);      // 队友牧师·生命领域（守序）
            seated[1].SetHealthExact(1);

            Assert.IsTrue(controller.CanBeAttacked(seated[1]));
            Assert.IsTrue(controller.ClickHand(seated[0].LeftHand));
            Assert.IsTrue(controller.ClickHand(seated[1].LeftHand));
            Assert.IsTrue(controller.ClickHand(seated[1].LeftHand));

            Assert.IsFalse(seated[1].IsAlive);
            Assert.IsTrue(controller.IsExtraAction);
            Assert.AreSame(seated[3], controller.ActivePlayer,
                "同阵营击杀后，从击杀者下家开始，第一位其他阵营角色行动。");

            int pending = controller.ExtraActionsRemaining
                          + (controller.IsExtraAction ? 1 : 0);
            Assert.AreEqual(5, pending, "中立 3 人 + 混乱 2 人，各行动一次。");
        }

        [Test]
        public void Kill_rewards_are_disabled_outside_eight_player_mode()
        {
            var seated = new List<PlayerState>
            {
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("C", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("D", CharacterDefinition.Blank),
            };
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            GameManager game = GameManager.Create2v2(seated, providers);
            var sink = new SimpleChoiceProvider();
            var controller = new BoardController(game, sink);
            for (int i = 0; i < seated.Count; i++)
                controller.BindPlayer(seated[i], new GreedyChoiceProvider(), sink);
            controller.SetFirstPlayer(seated[0]);
            controller.BeginTurn();

            seated[0].LeftHand.SetValue(3);
            seated[3].LeftHand.SetValue(1);   // D 是 A 的队友（首尾相邻）
            seated[3].SetHealthExact(1);

            Assert.IsTrue(controller.ClickHand(seated[0].LeftHand));
            Assert.IsTrue(controller.ClickHand(seated[3].LeftHand));
            Assert.IsTrue(controller.ClickHand(seated[3].LeftHand));

            Assert.IsFalse(seated[3].IsAlive);
            Assert.IsFalse(controller.IsExtraAction,
                "击杀奖惩只在 8 人阵营混战启用。");
            Assert.AreEqual(0, controller.ExtraActionsRemaining);
        }
    }
}
