using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>8 人身份局：身份分配、主公 +5、四类胜负边界。</summary>
    [TestFixture]
    public class IdentityModeTests
    {
        private static GameManager Match(out List<PlayerState> p, out List<IdentityRole> roles)
        {
            p = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                p.Add(CharacterFactory.CreatePlayer("P" + (i + 1), CharacterDefinition.Blank));

            roles = new List<IdentityRole>
            {
                IdentityRole.Lord, IdentityRole.Loyalist, IdentityRole.Loyalist,
                IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Traitor,
            };

            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());
            return GameManager.CreateIdentityEight(p, providers, roles);
        }

        [Test]
        public void Lord_is_seat_one_and_has_ten_extra_health()
        {
            GameManager game = Match(out List<PlayerState> p, out _);
            Assert.AreEqual(IdentityRole.Lord, game.IdentityOf(p[0]));
            Assert.AreEqual(30, p[0].MaximumHealth, "白板 20 + 主公 10。");
            Assert.AreEqual(30, p[0].Health);
            Assert.IsTrue(game.IsIdentityPublic(p[0]));
            Assert.IsFalse(game.IsIdentityPublic(p[1]));
        }

        [Test]
        public void Rebels_win_when_lord_dies_even_if_all_rebels_are_dead()
        {
            GameManager game = Match(out List<PlayerState> p, out _);

            // 反贼全灭；只留主公 + 忠臣。
            for (int i = 3; i <= 6; i++) p[i].SetHealthExact(0);
            p[0].SetHealthExact(0);
            game.RefreshGameOver();

            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(IdentityRole.Rebel, game.WinningIdentity,
                "主公死时有忠臣存活 -> 反贼胜，即使反贼全灭。");
            Assert.True(game.Winner == null);
        }

        [Test]
        public void Lord_side_wins_when_rebels_and_traitor_are_all_dead()
        {
            GameManager game = Match(out List<PlayerState> p, out _);
            for (int i = 3; i <= 7; i++) p[i].SetHealthExact(0);
            game.RefreshGameOver();

            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(IdentityRole.Lord, game.WinningIdentity);
            Assert.AreSame(p[0], game.Winner);
        }

        [Test]
        public void Traitor_wins_when_only_traitor_remains_after_lord_dies()
        {
            GameManager game = Match(out List<PlayerState> p, out _);
            for (int i = 1; i <= 6; i++) p[i].SetHealthExact(0);
            p[0].SetHealthExact(0);
            game.RefreshGameOver();

            Assert.IsTrue(game.IsGameOver);
            Assert.AreEqual(IdentityRole.Traitor, game.WinningIdentity);
            Assert.AreSame(p[7], game.Winner);
        }

        [Test]
        public void Lord_killing_a_rebel_queues_extra_actions_equal_to_remaining_rebels()
        {
            var p = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                p.Add(CharacterFactory.CreatePlayer("P" + (i + 1), CharacterDefinition.Blank));
            var roles = new List<IdentityRole>
            {
                IdentityRole.Lord, IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Loyalist, IdentityRole.Loyalist, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Traitor,
            };
            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());
            GameManager game = GameManager.CreateIdentityEight(p, providers, roles);
            var sink = new SimpleChoiceProvider();
            var board = new BoardController(game, sink);
            for (int i = 1; i < 8; i++)
                board.BindPlayer(p[i], new GreedyChoiceProvider(), sink);
            board.SetFirstPlayer(p[0]);
            board.BeginTurn();

            p[0].LeftHand.SetValue(3);
            p[1].LeftHand.SetValue(1);
            p[1].SetHealthExact(1);

            Assert.IsTrue(board.ClickHand(p[0].LeftHand));
            Assert.IsTrue(board.ClickHand(p[1].LeftHand));
            Assert.IsTrue(board.ClickHand(p[1].LeftHand));

            Assert.IsFalse(p[1].IsAlive);
            Assert.IsTrue(board.IsExtraAction);
            Assert.AreEqual(3, board.ExtraActionsRemaining + 1,
                "击杀后场上还剩 3 名反贼，应额外行动 3 次。");
        }

        [Test]
        public void Attacking_the_lord_marks_the_attacker_for_lord_side_ai()
        {
            var p = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                p.Add(CharacterFactory.CreatePlayer("P" + (i + 1), CharacterDefinition.Blank));
            var roles = new List<IdentityRole>
            {
                IdentityRole.Lord, IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Loyalist, IdentityRole.Loyalist, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Traitor,
            };
            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());
            GameManager game = GameManager.CreateIdentityEight(p, providers, roles);

            p[1].LeftHand.SetValue(3);   // 座位 2，反贼
            p[0].LeftHand.SetValue(1);   // 主公
            game.ExecuteTurn(p[1], p[1].LeftHand, p[0].LeftHand);

            Assert.IsTrue(game.HasAttackedLord(p[1]),
                "攻击过主公的玩家应被主忠方标记。");
            Assert.AreEqual(GameManager.MajorRebelSuspicion,
                game.RebelSuspicionOf(p[1]), "攻击主公应大幅提高反贼怀疑。");
            Assert.IsFalse(game.HasAttackedLord(p[2]));
        }

        [Test]
        public void Ignoring_a_major_suspect_raises_minor_suspicion_and_attacking_them_lowers_it()
        {
            var p = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                p.Add(CharacterFactory.CreatePlayer("P" + (i + 1), CharacterDefinition.Blank));
            var roles = new List<IdentityRole>
            {
                IdentityRole.Lord, IdentityRole.Rebel, IdentityRole.Loyalist,
                IdentityRole.Rebel, IdentityRole.Loyalist, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Traitor,
            };
            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());
            GameManager game = GameManager.CreateIdentityEight(p, providers, roles);

            // p1（座位2）攻击主公 -> 大幅怀疑。
            p[1].LeftHand.SetValue(3);
            p[0].LeftHand.SetValue(1);
            game.ExecuteTurn(p[1], p[1].LeftHand, p[0].LeftHand);
            Assert.IsTrue(game.IsMajorRebelSuspect(p[1]));

            // p2（座位3）攻击 p3（座位4），而范围内有 p1：小幅怀疑。
            p[2].LeftHand.SetValue(3);
            p[3].LeftHand.SetValue(1);
            game.ExecuteTurn(p[2], p[2].LeftHand, p[3].LeftHand);
            Assert.AreEqual(GameManager.MinorRebelSuspicion,
                game.RebelSuspicionOf(p[2]), "放着大幅嫌疑目标不打，应小幅怀疑。");

            // p2 改为攻击 p1，怀疑降低。
            p[2].LeftHand.SetValue(3);
            p[1].LeftHand.SetValue(1);
            game.ExecuteTurn(p[2], p[2].LeftHand, p[1].LeftHand);
            Assert.AreEqual(0, game.RebelSuspicionOf(p[2]),
                "攻击被怀疑对象应降低自身嫌疑。");
        }

        [Test]
        public void Lord_values_shield_and_heal_before_two_rebels_die()
        {
            var p = new List<PlayerState>(8);
            for (int i = 0; i < 8; i++)
                p.Add(CharacterFactory.CreatePlayer("P" + (i + 1), CharacterDefinition.Blank));
            var roles = new List<IdentityRole>
            {
                IdentityRole.Lord, IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Rebel, IdentityRole.Loyalist,
                IdentityRole.Loyalist, IdentityRole.Traitor,
            };
            var providers = new List<IChoiceProvider>(8);
            for (int i = 0; i < 8; i++) providers.Add(new SimpleChoiceProvider());
            GameManager game = GameManager.CreateIdentityEight(p, providers, roles);
            for (int i = 0; i < 8; i++)
                for (int h = 0; h < 2; h++)
                    p[i].Hands[h].SetValue(1);
            p[0].LeftHand.SetValue(3);
            p[0].RightHand.SetValue(8);
            p[2].LeftHand.SetValue(2);   // 3 + 2 = 5，可合盾
            for (int i = 1; i < 8; i++) p[i].SetHealthExact(20);
            p[0].SetHealthExact(10);

            var ai = new GreedyChoiceProvider();
            LegalMove move = ai.ChooseMove(game, p[0]);
            int result = Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value);
            HandEffectType effect = Hand.GetEffectType(result);

            Assert.True(effect == HandEffectType.Shield || effect == HandEffectType.Heal,
                "反贼死亡不足 2 且无法斩杀时，主公应大幅提高合盾/回血权重。");
        }
    }
}
