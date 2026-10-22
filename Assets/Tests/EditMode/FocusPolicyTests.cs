using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 集火策略**（<see cref="GreedyChoiceProvider.FocusPolicy"/>）。
    ///
    /// 这一组测试钉住三件事：
    ///   ① 车道解析：Self = 离自己最近的对手，Mate = 离队友最近的对手；
    ///   ② 集火只加在**攻击**上，且量级要压过六级目标优先级；
    ///   ③ 近战打不到队友车道时，Mate **不能**偷偷退化成 Self。
    /// </summary>
    [TestFixture]
    public class FocusPolicyTests
    {
        private static GameManager Make2v2(CharacterDefinition d1, CharacterDefinition d2,
                                           CharacterDefinition d3, CharacterDefinition d4,
                                           out List<PlayerState> seated,
                                           out List<GreedyChoiceProvider> ais)
        {
            seated = new List<PlayerState>(4)
            {
                CharacterFactory.CreatePlayer("P1", d1),
                CharacterFactory.CreatePlayer("P2", d2),
                CharacterFactory.CreatePlayer("P3", d3),
                CharacterFactory.CreatePlayer("P4", d4),
            };

            ais = new List<GreedyChoiceProvider>(4)
            {
                new GreedyChoiceProvider(), new GreedyChoiceProvider(),
                new GreedyChoiceProvider(), new GreedyChoiceProvider(),
            };

            var providers = new List<IChoiceProvider>(4)
            {
                ais[0], ais[1], ais[2], ais[3],
            };

            GameManager game = GameManager.Create2v2(seated, providers);
            for (int i = 0; i < ais.Count; i++) ais[i].Game = game;

            return game;
        }

        private static void SetAllHands(List<PlayerState> seated, int left, int right)
        {
            for (int i = 0; i < seated.Count; i++)
            {
                seated[i].LeftHand.SetValue(left);
                seated[i].RightHand.SetValue(right);
            }
        }

        [Test]
        public void Self_and_mate_resolve_to_the_two_different_lanes()
        {
            GameManager game = Make2v2(
                CharacterDefinition.SchoolOfConjuration, CharacterDefinition.Champion,
                CharacterDefinition.Champion, CharacterDefinition.Champion,
                out List<PlayerState> seated, out _);

            PlayerState me = seated[0];        // 座位 1，1&4 一队
            PlayerState selfLane = seated[1];  // 座位 2：离我最近
            PlayerState mateLane = seated[2];  // 座位 3：离队友（座位 4）最近

            var ai = new GreedyChoiceProvider { Game = game, Focus = GreedyChoiceProvider.FocusPolicy.Self };
            Assert.AreSame(selfLane, ai.ResolveFocusTarget(game, me),
                "Self 应指向离自己最近的对手（座位 2）。");

            ai.Focus = GreedyChoiceProvider.FocusPolicy.Mate;
            Assert.AreSame(mateLane, ai.ResolveFocusTarget(game, me),
                "Mate 应指向离队友最近的对手（座位 3）。");

            ai.Focus = GreedyChoiceProvider.FocusPolicy.None;
            Assert.Null(ai.ResolveFocusTarget(game, me),
                "None 不应解析出任何集火目标。");
        }

        [Test]
        public void Focus_bonus_goes_only_to_attacks_on_the_focus_target()
        {
            GameManager game = Make2v2(
                CharacterDefinition.SchoolOfConjuration, CharacterDefinition.Champion,
                CharacterDefinition.Champion, CharacterDefinition.Champion,
                out List<PlayerState> seated, out List<GreedyChoiceProvider> ais);

            PlayerState me = seated[0];
            PlayerState focus = seated[1];
            PlayerState other = seated[2];
            SetAllHands(seated, 3, 1);          // 3 + 3 = 6 -> 攻击

            var baseline = ais[0];
            baseline.Focus = GreedyChoiceProvider.FocusPolicy.None;

            var focused = new GreedyChoiceProvider
            {
                Game = game,
                Focus = GreedyChoiceProvider.FocusPolicy.Self,
            };

            int baseFocus = baseline.ScoreMoveForTest(
                new LegalMove(me, me.LeftHand, focus.LeftHand), me, game);
            int baseOther = baseline.ScoreMoveForTest(
                new LegalMove(me, me.LeftHand, other.LeftHand), me, game);

            int withFocus = focused.ScoreMoveForTest(
                new LegalMove(me, me.LeftHand, focus.LeftHand), me, game);
            int withOther = focused.ScoreMoveForTest(
                new LegalMove(me, me.LeftHand, other.LeftHand), me, game);

            // 目标优先级用的是"相对最优目标"的分差（见 BestTargetPriority），
            // 所以集火目标自己拿到的是 0 分基准；关键是它**必须高于**其他目标。
            Assert.True(withFocus > withOther,
                $"集火目标的攻击应高于非集火目标（{withFocus} vs {withOther}）。");
            Assert.True(withFocus > baseFocus || withOther < baseOther,
                "集火应让目标选择偏向集火目标。");
        }

        [Test]
        public void Focus_bonus_does_not_apply_to_shields()
        {
            GameManager game = Make2v2(
                CharacterDefinition.SchoolOfConjuration, CharacterDefinition.Champion,
                CharacterDefinition.Champion, CharacterDefinition.Champion,
                out List<PlayerState> seated, out List<GreedyChoiceProvider> ais);

            PlayerState me = seated[0];
            PlayerState focus = seated[1];
            SetAllHands(seated, 4, 1);          // 4 + 1 = 5 -> 合盾

            var baseline = ais[0];
            var focused = new GreedyChoiceProvider
            {
                Game = game,
                Focus = GreedyChoiceProvider.FocusPolicy.Self,
            };

            int baseScore = baseline.ScoreMoveForTest(
                new LegalMove(me, me.LeftHand, focus.RightHand), me, game);
            int focusScore = focused.ScoreMoveForTest(
                new LegalMove(me, me.LeftHand, focus.RightHand), me, game);

            Assert.AreEqual(0, focusScore - baseScore,
                "合盾不该被集火带偏（否则 AI 会为了贪分去做防守动作）。");
        }

        [Test]
        public void Focus_bonus_outweighs_the_six_level_target_priority()
        {
            // 反例：原来的 +40 在"双方都能打到 + 血低"约 20000 分面前完全无效。
            // 这里让队友车道的敌人掉到 10 血（基线一定打他，但还没进斩杀线），
            // 集火 Self 仍应改打自己车道那个满血的敌人。
            GameManager game = Make2v2(
                CharacterDefinition.SchoolOfConjuration, CharacterDefinition.Champion,
                CharacterDefinition.Champion, CharacterDefinition.Champion,
                out List<PlayerState> seated, out List<GreedyChoiceProvider> ais);

            PlayerState me = seated[0];
            PlayerState selfLane = seated[1];
            PlayerState weakMateLane = seated[2];

            SetAllHands(seated, 3, 1);
            weakMateLane.TakeDamage(weakMateLane.MaximumHealth - 10);

            var baseline = ais[0];
            baseline.Focus = GreedyChoiceProvider.FocusPolicy.None;
            LegalMove baseMove = baseline.ChooseMove(game, me);

            var focused = new GreedyChoiceProvider
            {
                Game = game,
                Focus = GreedyChoiceProvider.FocusPolicy.Self,
            };
            LegalMove focusMove = focused.ChooseMove(game, me);

            Assert.AreSame(weakMateLane,
                game.OwnerOfHand(baseMove.TargetHand),
                "前置：基线应优先打血少的队友车道敌人。");
            Assert.AreSame(selfLane,
                game.OwnerOfHand(focusMove.TargetHand),
                "集火 Self 应压过六级优先级，改打自己车道。");
            Assert.AreSame(selfLane, focused.IntendedVictim,
                "IntendedVictim 应跟集火目标一致。");
        }

        [Test]
        public void Mate_is_inert_for_a_melee_actor_who_cannot_reach_the_mate_lane()
        {
            GameManager game = Make2v2(
                CharacterDefinition.Champion, CharacterDefinition.Champion,
                CharacterDefinition.Champion, CharacterDefinition.Champion,
                out List<PlayerState> seated, out List<GreedyChoiceProvider> ais);

            PlayerState me = seated[0];        // 战士·勇士：射程 1
            PlayerState mateLane = seated[2];  // 座位 3：离队友最近，但离我距离 2

            SetAllHands(seated, 3, 1);

            Assert.False(game.CanReach(me, mateLane),
                "前置：近战前排打不到队友车道的敌人。");

            var baseline = ais[0];
            baseline.Focus = GreedyChoiceProvider.FocusPolicy.None;
            LegalMove baseMove = baseline.ChooseMove(game, me);

            var focused = new GreedyChoiceProvider
            {
                Game = game,
                Focus = GreedyChoiceProvider.FocusPolicy.Mate,
            };
            LegalMove focusMove = focused.ChooseMove(game, me);

            Assert.AreSame(baseMove.ActedHand, focusMove.ActedHand,
                "打不到 Mate 车道时，集火不应改变运算源。");
            Assert.AreSame(baseMove.TargetHand, focusMove.TargetHand,
                "打不到 Mate 车道时，集火不应改变被加数手。");
            Assert.AreSame(seated[1], focused.IntendedVictim,
                "打不到 Mate 车道时，实际目标应仍是自己车道的敌人。");
        }
    }
}
