using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 回归：**AI 的抉择必须交给 AI 的抉择器，而不是交给 sink**。
    ///
    /// 玩家实测 bug：AI 使用新角色时**从不发动任何技能**
    /// （AI 游荡者·风流剑客有 1 不闪避、有 2 不强化、战士·剑士不追加……）。
    ///
    /// 根因（由实机诊断日志定位）：
    ///   `BoardController.GetChoice(player)` 原来**无条件**返回
    ///   `_choiceA / _choiceB`（来自 sink 对象，UI 里是 `SimpleChoiceProvider`
    ///   —— 它只负责**记录**玩家点了什么，没有任何策略）。
    ///   于是 AI 的抉择被交给它，永远返回 null -> 判定非法 -> 放弃发动。
    ///
    ///   诊断日志里的铁证：
    ///   `AI作答 SwashbucklerDodge(v0) provider=SimpleChoiceProvider pick=null/非法 cands=1`
    ///                                            ^^^^^^^^^^^^^^^^^^^^ 应该是 GreedyChoiceProvider
    ///
    /// 修法：AI 玩家优先用它的 <see cref="IMoveSelector"/>（它同时实现
    /// <see cref="IChoiceProvider"/>）；人类玩家才用 sink。
    /// </summary>
    [TestFixture]
    public class AiChoiceProviderRoutingTests
    {
        private GameManager _game;
        private BoardController _board;
        private PlayerState _human;   // A
        private PlayerState _ai;      // B
        private GreedyChoiceProvider _aiChoice;

        private void Arrange(CharacterDefinition aiChar)
        {
            var humanChoice = new SimpleChoiceProvider();
            var aiSink = new SimpleChoiceProvider();
            _aiChoice = new GreedyChoiceProvider();

            _game = new GameManager(
                CharacterFactory.CreatePlayer("人类", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("AI", aiChar),
                humanChoice, aiSink);

            _human = _game.PlayerA;
            _ai = _game.PlayerB;

            // UI 的接线方式：B 的 sink 是 SimpleChoiceProvider，
            // 而 B 的策略在 GreedyChoiceProvider 里 —— 两者是不同的对象。
            _board = new BoardController(_game, humanChoice, aiSink, aiForB: _aiChoice);
        }

        private string AiTraceText()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _board.AiTrace.Count; i++)
                sb.Append(_board.AiTrace[i]).Append(" | ");
            return sb.ToString();
        }

        // ============================================================
        // ① AI 作为防守方：翻滚
        // ============================================================

        [Test]
        public void Ai_defender_uses_its_own_choice_provider_to_dodge()
        {
            Arrange(CharacterDefinition.Swashbuckler);

            // 人类先手：3 打 AI 的 5 -> 8（攻击）；AI 右手是 1，可翻滚
            TestSetup.SetHand(_human.LeftHand, 3);
            TestSetup.SetHand(_human.RightHand, 1);
            TestSetup.SetHand(_ai.LeftHand, 5);
            TestSetup.SetHand(_ai.RightHand, 1);

            _board.BeginTurn();
            Assert.True(ReferenceEquals(_board.ActivePlayer, _human), "A 先手");

            _board.ClickHand(_human.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_ai.LeftHand);
            _board.ConfirmTargetPlayer();

            // 关键断言：AI 的抉择器被调用了（provider 名字能证明路由正确）
            Assert.True(AiTraceText().Contains("GreedyChoiceProvider"),
                "AI 的抉择应交给 GreedyChoiceProvider，而不是 SimpleChoiceProvider。" +
                $"实际记录：{AiTraceText()}");

            Assert.AreEqual(2, _ai.RightHand.Value,
                $"AI 游荡者·风流剑客应翻滚（1 -> 2）。记录：{AiTraceText()}");
            Assert.AreEqual(20, _ai.Health,
                $"翻滚应完全免伤。记录：{AiTraceText()}");
        }

        // ============================================================
        // ② AI 作为行动方：乘风突刺
        // ============================================================

        [Test]
        public void Ai_attacker_uses_its_own_choice_provider_to_power_shot()
        {
            Arrange(CharacterDefinition.Swashbuckler);

            // 让它轮到 AI：人类先走一步
            TestSetup.SetHand(_human.LeftHand, 1);
            TestSetup.SetHand(_human.RightHand, 1);
            TestSetup.SetHand(_ai.LeftHand, 1);
            TestSetup.SetHand(_ai.RightHand, 1);

            _board.BeginTurn();
            _board.ClickHand(_human.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.ClickHand(_ai.LeftHand);
            _board.ConfirmTargetPlayer();
            _board.BeginTurn();

            Assert.True(ReferenceEquals(_board.ActivePlayer, _ai), "应轮到 AI");

            // 现场：AI 左手 6 打人类右手 2 -> 8（攻击）；AI 右手是 2，可强化
            TestSetup.SetHand(_ai.LeftHand, 6);
            TestSetup.SetHand(_ai.RightHand, 2);
            TestSetup.SetHand(_human.LeftHand, 1);
            TestSetup.SetHand(_human.RightHand, 2);

            _board.MakeAutomaticMove();

            Assert.True(AiTraceText().Contains("GreedyChoiceProvider"),
                "AI 的强化抉择应交给 GreedyChoiceProvider。" +
                $"实际记录：{AiTraceText()}");

            Assert.AreEqual(3, _ai.RightHand.Value,
                $"AI 游荡者·风流剑客应强化（2 -> 3）。记录：{AiTraceText()}");
        }
    }
}
