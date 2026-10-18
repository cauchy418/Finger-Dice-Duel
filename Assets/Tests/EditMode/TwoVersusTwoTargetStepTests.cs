using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **第 ③ 步（确认攻击目标）不能卡死**。
    ///
    /// 玩家实测的卡死场景：
    ///   "玩家在选择和**队友**运算出攻击数字后，再次选择攻击目标会卡死"
    ///
    /// 根因：第 ② 步把 `TargetPlayer` 设成了**被加数的手主** ——
    /// 而队友也可以是"被加数的手主"。于是进第 ③ 步后，
    /// 只接受"TargetPlayer 自己的手"就变成"只接受队友的手"，
    /// 可队友**不可被攻击** -> 没有任何手点得动 -> 卡死。
    ///
    /// 修法：第 ③ 步接受**任意可攻击敌人的手**，并在进入时把目标默认到
    /// 一个真正打得动的人身上。
    /// </summary>
    [TestFixture]
    public class TwoVersusTwoTargetStepTests
    {
        /// <summary>
        /// 2v2：四个白板。队伍 {P1,P4} 与 {P2,P3}。
        /// P1 是人类视角，其余三人配 AI（本测试只手动驱动 P1）。
        /// </summary>
        private static BoardController Arrange(out GameManager game,
                                               out List<PlayerState> seated)
        {
            seated = new List<PlayerState>(4)
            {
                CharacterFactory.CreatePlayer("P1", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("P2", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("P3", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("P4", CharacterDefinition.Blank),
            };

            var providers = new List<IChoiceProvider>(4);
            for (int i = 0; i < 4; i++) providers.Add(new SimpleChoiceProvider());

            game = GameManager.Create2v2(seated, providers);

            var board = new BoardController(game,
                providers[0] as IPlayerChoiceSink, providers[1] as IPlayerChoiceSink,
                aiForB: new GreedyChoiceProvider());

            board.BindPlayer(seated[2], new GreedyChoiceProvider(),
                             providers[2] as IPlayerChoiceSink);
            board.BindPlayer(seated[3], new GreedyChoiceProvider(),
                             providers[3] as IPlayerChoiceSink);

            board.BeginTurn();
            return board;
        }

        [Test]
        public void Using_a_teammates_hand_then_confirming_the_target_does_not_stall()
        {
            BoardController board = Arrange(out GameManager game,
                                            out List<PlayerState> seated);

            PlayerState me = seated[0];        // P1（1 号位）
            PlayerState mate = seated[3];      // P4（4 号位）—— 与 P1 同队

            Assert.True(game.IsTeammate(me, mate), "前置：P1 与 P4 是队友。");

            // 让"用队友的手当被加数"能凑出攻击值：
            //   我左手 4 + 队友左手 1 = 5 -> 护盾（不是攻击）
            //   我左手 7 + 队友右手 2 = 9 -> 回复
            //   我左手 6 + 队友右手 2 = 8 -> **攻击**
            TestSetup.SetHand(me.LeftHand, 6);
            TestSetup.SetHand(me.RightHand, 1);
            TestSetup.SetHand(mate.LeftHand, 3);
            TestSetup.SetHand(mate.RightHand, 2);

            // 第 ① 步：点自己的手
            Assert.True(board.ClickHand(me.LeftHand), "第①步应可点。");
            Assert.AreEqual(BoardPhase.ChoosingTargetHand, board.Phase);

            // 第 ② 步：点**队友**的手当被加数 -> 6 + 2 = 8（攻击）
            Assert.True(board.ClickHand(mate.RightHand),
                "第②步：队友的手可以作为被加数。");

            // 第 ② 步之后应当进到"确认目标玩家"（因为结果是攻击）
            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase,
                "运算出攻击数字后应进入确认目标玩家的阶段。");

            // 关键断言：**必须有手点得动** —— 这就是原来卡死的地方
            bool anyClickable = false;
            for (int i = 0; i < seated.Count; i++)
            {
                if (board.CanClick(seated[i].LeftHand)) anyClickable = true;
                if (board.CanClick(seated[i].RightHand)) anyClickable = true;
            }

            Assert.True(anyClickable,
                "第③步必须至少有一只手可以点（否则对局卡死）。" +
                $"当前阶段={board.Phase}，提示={board.Hint}");

            // 而且点下去必须真的推进（不能"看着能点、点了没反应"）
            bool advanced = false;
            for (int i = 0; i < seated.Count && !advanced; i++)
            {
                if (board.CanClick(seated[i].LeftHand))
                    advanced = board.ClickHand(seated[i].LeftHand);
                if (!advanced && board.CanClick(seated[i].RightHand))
                    advanced = board.ClickHand(seated[i].RightHand);
            }

            Assert.True(advanced, "点下可点的手之后必须真的推进对局。");
        }

        [Test]
        public void A_teammate_is_offered_as_a_legal_attack_target()
        {
            BoardController board = Arrange(out GameManager game,
                                            out List<PlayerState> seated);

            PlayerState me = seated[0];
            PlayerState mate = seated[3];

            TestSetup.SetHand(me.LeftHand, 6);
            TestSetup.SetHand(mate.RightHand, 2);

            board.ClickHand(me.LeftHand);
            board.ClickHand(mate.RightHand);

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);

            // 队友的手现在**可以**点（他是合法攻击目标）
            Assert.True(board.CanClick(mate.LeftHand),
                "队友现在可以成为攻击目标。");
            Assert.True(board.CanClick(mate.RightHand),
                "队友现在可以成为攻击目标。");
        }

        [Test]
        public void The_default_target_is_an_attackable_enemy()
        {
            BoardController board = Arrange(out GameManager game,
                                            out List<PlayerState> seated);

            PlayerState me = seated[0];
            PlayerState mate = seated[3];

            TestSetup.SetHand(me.LeftHand, 6);
            TestSetup.SetHand(mate.RightHand, 2);

            board.ClickHand(me.LeftHand);
            board.ClickHand(mate.RightHand);

            Assert.AreEqual(BoardPhase.ChoosingTargetPlayer, board.Phase);

            // P1 在 1 号位：敌人是 P2(2 号位，距离 1) 与 P3(3 号位，距离 2)。
            // 白板攻击距离 1 -> 只有 P2 可打。
            Assert.True(board.CanClick(seated[1].LeftHand),
                "距离 1 的敌人（P2）应可点。");
            Assert.False(board.CanClick(seated[2].LeftHand),
                "距离 2 的敌人（P3）超出攻击距离，不该可点。");
        }
    }
}
