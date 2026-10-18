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
    /// **吟游诗人的四步抉择（控制器层）**：
    /// ① 是否发动 ② 四项之一 ③ 目标数 ④ 选目标。
    ///
    /// 这是第一个"运算之后才问"的技能，所以走的是
    /// `OfferBardChoiceIfAny` / `ApplyBardChoice` 这套**不依赖 BoardPhase** 的流程。
    /// </summary>
    [TestFixture]
    public class BardChoiceFlowTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static BoardController MakeBoard(out PlayerState bard, out PlayerState foe,
                                                 out GameManager game)
        {
            bard = Make("吟", CharacterDefinition.Bard);
            foe = Make("战", CharacterDefinition.Soldier);
            game = new GameManager(bard, foe, new SimpleChoiceProvider(),
                                   new SimpleChoiceProvider());
            return new BoardController(game, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
        }

        [Test]
        public void No_damage_means_no_offer()
        {
            BoardController board = MakeBoard(out PlayerState bard, out _, out _);

            Assert.IsFalse(board.OfferBardChoiceIfAny(),
                "本回合没受伤也没回血 -> 不该弹面板。");
            Assert.IsFalse(board.BardChoicePending);
        }

        [Test]
        public void Taking_damage_offers_the_choice_with_self_and_foe_as_candidates()
        {
            BoardController board = MakeBoard(out PlayerState bard, out PlayerState foe,
                                              out _);

            bard.TakeDamage(3);                       // 本回合受伤

            Assert.IsTrue(board.OfferBardChoiceIfAny(), "受伤后应当弹面板。");
            Assert.IsTrue(board.BardChoicePending);

            Assert.AreEqual(2, board.BardCandidates.Count,
                "1v1 里候选 = 自己 + 对手。");
            Assert.AreEqual(2, board.BardMaxTargetCount, "最多可选 2 个（受候选数限制）。");
        }

        [Test]
        public void Step_two_three_four_apply_the_offset()
        {
            BoardController board = MakeBoard(out PlayerState bard, out PlayerState foe,
                                              out _);
            bard.TakeDamage(3);
            board.OfferBardChoiceIfAny();

            // ② 削弱防御  ③ 只选 1 个（强度 3） ④ 选对手
            int applied = board.ApplyBardChoice(BardLogic.BuffKind.WeakenDefense,
                                                new List<PlayerState> { foe });

            Assert.AreEqual(1, applied);
            Assert.AreEqual(-3, foe.BardDefenseOffset, "1 个目标 -> 防御 -3。");
            Assert.IsFalse(board.BardChoicePending, "答完之后要把待答状态清掉。");
        }

        [Test]
        public void Choosing_two_targets_lowers_the_strength()
        {
            BoardController board = MakeBoard(out PlayerState bard, out PlayerState foe,
                                              out _);
            bard.TakeDamage(3);
            board.OfferBardChoiceIfAny();

            Assert.AreEqual(2, BoardController.BardStrengthFor(2));

            board.ApplyBardChoice(BardLogic.BuffKind.BoostAttack,
                                  new List<PlayerState> { bard, foe });

            Assert.AreEqual(2, bard.BardAttackOffset, "2 个目标 -> 强度 2。");
            Assert.AreEqual(2, foe.BardAttackOffset, "两个目标共用同一项。");
        }

        [Test]
        public void Declining_leaves_everyone_untouched()
        {
            BoardController board = MakeBoard(out PlayerState bard, out PlayerState foe,
                                              out _);
            bard.TakeDamage(3);
            board.OfferBardChoiceIfAny();

            board.DeclineBardChoice();

            Assert.IsFalse(board.BardChoicePending);
            Assert.AreEqual(0, foe.BardAttackOffset);
            Assert.AreEqual(0, foe.BardDefenseOffset);
        }
    }
}
