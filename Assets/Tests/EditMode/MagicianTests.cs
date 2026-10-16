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
    /// **魔术师**（混乱善良）的专项测试。
    ///
    /// 玩家给出的规则：
    ///   · 可以拿**自己的手**当被加数（左手 3 碰自己的右手 5 凑出 8）；
    ///   · 两只手**任选一个方向**，变的是**先点的那只**（发起运算的手）；
    ///   · **一只手不能加自己**（重复点同一只手不生效）；
    ///   · 运算出 **2 可以攻击**，吃攻防加成的 2 点伤害；
    ///   · 用掉回合这一点与其他角色一致。
    /// </summary>
    [TestFixture]
    public class MagicianTests
    {
        private static PlayerState Make(string n, CharacterDefinition d)
            => CharacterFactory.CreatePlayer(n, d);

        private static GameManager Match(out PlayerState magician, out PlayerState foe)
        {
            magician = Make("魔", CharacterDefinition.Magician);
            foe = Make("敌", CharacterDefinition.Soldier);
            return new GameManager(magician, foe,
                                   new GreedyChoiceProvider(),
                                   new GreedyChoiceProvider());
        }

        [Test]
        public void UI_allows_clicking_own_other_hand_as_the_addend()
        {
            // 玩家实测的 bug："魔术师的手选择后无法触碰自己的手运算"。
            // 领域层早就放行，但 BoardController.CanClick 的
            // ChoosingTargetHand 分支用的是 `!ActivePlayer.Owns(hand)` ——
            // 把"碰自己的手"一手挡住了。这里把界面这一层也钉住。
            GameManager game = Match(out PlayerState magician, out _);
            // 注意：BoardController 要求抉择器同时实现 IPlayerChoiceSink，
            // 所以这里用 SimpleChoiceProvider（GreedyChoiceProvider 不行）。
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                            new SimpleChoiceProvider());
            board.BeginTurn();

            magician.LeftHand.SetValue(3);
            magician.RightHand.SetValue(5);

            // 第①步：选左手当运算源
            Assert.True(board.ClickHand(magician.LeftHand), "应能选中自己的左手。");

            // 第②步：碰自己的右手（这就是玩家点不动的那一步）
            Assert.True(board.CanClick(magician.RightHand),
                "魔术师必须能点自己的另一只手当被加数。");
            Assert.True(board.ClickHand(magician.RightHand),
                "点击自己的另一只手应当被接受。");
        }

        [Test]
        public void Other_characters_still_cannot_click_their_own_hand_as_addend()
        {
            var soldier = Make("兵", CharacterDefinition.Soldier);
            var foe = Make("敌", CharacterDefinition.Soldier);
            var game = new GameManager(soldier, foe,
                                       new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                            new SimpleChoiceProvider());
            board.BeginTurn();

            soldier.LeftHand.SetValue(3);
            soldier.RightHand.SetValue(5);

            board.ClickHand(soldier.LeftHand);
            Assert.False(board.CanClick(soldier.RightHand),
                "其他角色仍然不能点自己的手当被加数（规则只属于魔术师）。");
        }

        [Test]
        public void Two_from_self_addend_needs_a_target_just_like_four_six_eight()
        {
            // 玩家要求："我需要让 2 和 4/6/8 一样再选择一次目标"。
            // 根因：MagicianLogic 没实现 IResultTargetProvider，
            // 于是 BoardController 走兜底 —— 兜底按 context.Effect 判断，
            // 而 2 的效果类型是 None（不是 Attack4/6/8）-> 不追问目标 -> 自动打人。
            GameManager game = Match(out PlayerState magician, out PlayerState foe);
            magician.LeftHand.SetValue(1);
            magician.RightHand.SetValue(1);
            foe.LeftHand.SetValue(1);      // 1 + 1 = 2（前面写成 4 会得到 5）

            var provider = magician.Character as IResultTargetProvider;
            Assert.NotNull(provider, "魔术师必须实现 IResultTargetProvider。");

            // 自加：1 碰自己的 1 -> 2 -> 是攻击 -> 需要问打谁
            var selfCtx = new PreTurnChoiceContext(magician, foe,
                                                   magician.LeftHand, magician.RightHand);
            Assert.AreEqual(2, selfCtx.ResultValue, "前提：1 + 1 = 2。");
            Assert.True(provider.ResultNeedsTarget(selfCtx),
                "自加凑出的 2 是攻击，必须追问目标。");

            // 非自加：1 碰**对手的** 1 -> 2 -> 同样是攻击 -> 同样要追问
            var foeCtx = new PreTurnChoiceContext(magician, foe,
                                                  magician.LeftHand, foe.LeftHand);
            Assert.AreEqual(2, foeCtx.ResultValue, "前提：1 + 1 = 2。");
            // ⚠️ 玩家的规则是"**运算出 2 可以攻击**"，没有限定必须自加。
            //    我第一版只让"自加凑的 2"追问目标，玩家实测仍旧"自动攻击"
            //    （日志：对方右(1) + 目标1 = 2）。所以口径统一成**只看结果值**。
            Assert.True(provider.ResultNeedsTarget(foeCtx),
                "运算出 2 就是攻击，无论被加数是谁的手 —— 都要追问目标。");
        }

        [Test]
        public void Two_from_self_addend_is_an_attack()
        {
            // 与上一条配套：自加的 2 要真的被改写成攻击
            GameManager game = Match(out PlayerState magician, out _);
            magician.LeftHand.SetValue(1);
            magician.RightHand.SetValue(1);

            var rewriter = magician.Character as IHandEffectRewriter;
            Assert.NotNull(rewriter);

            magician.LeftHand.SetValue(2);
            HandEffectType effect = rewriter.RewriteEffect(
                magician, magician.LeftHand, magician.RightHand.Value, game);

            Assert.AreNotEqual((int)HandEffectType.None, (int)effect,
                "自加的 2 应被改写成攻击。");
        }

        [Test]
        public void Magician_can_use_its_own_hand_as_an_addend()
        {            GameManager game = Match(out PlayerState magician, out _);
            magician.LeftHand.SetValue(3);
            magician.RightHand.SetValue(5);

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(magician);

            bool found = false;
            for (int i = 0; i < moves.Count; i++)
            {
                if (ReferenceEquals(moves[i].ActedHand, magician.LeftHand)
                    && ReferenceEquals(moves[i].TargetHand, magician.RightHand))
                {
                    found = true;
                    break;
                }
            }

            Assert.True(found, "魔术师应能拿自己的左手去碰自己的右手。");
            Assert.True(game.IsLegalMove(magician, magician.LeftHand, magician.RightHand, out _),
                "校验层也应放行自己的手当被加数。");
        }

        [Test]
        public void Other_characters_still_cannot_add_themselves()
        {
            var soldier = Make("兵", CharacterDefinition.Soldier);
            var foe = Make("敌", CharacterDefinition.Soldier);
            var game = new GameManager(soldier, foe,
                                       new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());

            soldier.LeftHand.SetValue(3);
            soldier.RightHand.SetValue(5);

            Assert.False(game.IsLegalMove(soldier, soldier.LeftHand, soldier.RightHand, out _),
                "其他角色不能拿自己的手当被加数（这条规则只属于魔术师）。");
        }

        [Test]
        public void A_hand_cannot_be_added_to_itself()
        {
            GameManager game = Match(out PlayerState magician, out _);
            magician.LeftHand.SetValue(3);

            Assert.False(game.IsLegalMove(magician, magician.LeftHand, magician.LeftHand, out _),
                "一只手不能加自己（即使魔术师也不行）。");

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(magician);
            for (int i = 0; i < moves.Count; i++)
                Assert.False(ReferenceEquals(moves[i].ActedHand, moves[i].TargetHand),
                    "合法步里不应出现加数与被加数相同的手。");
        }

        [Test]
        public void Self_addend_changes_the_acting_hand_not_the_target()
        {
            // 玩家确认："两只手任选一个方向，变的是**先点的那只**"
            // 左手 3 先点、右手 5 当被加数 -> 结果是 8 写在**左手**上
            GameManager game = Match(out PlayerState magician, out _);
            magician.LeftHand.SetValue(3);
            magician.RightHand.SetValue(5);

            Assert.True(game.IsLegalMove(magician, magician.LeftHand, magician.RightHand, out _),
                "前提：这一步合法。");

            game.ExecuteTurn(magician, magician.LeftHand, magician.RightHand);

            Assert.AreEqual(8, magician.LeftHand.Value, "结果 8 应写在先点的左手（发起运算的手）上。");
            Assert.AreEqual(5, magician.RightHand.Value, "被加数（右手）不应改变。");
        }

        [Test]
        public void Two_is_an_attack_dealing_two_point_two_damage()
        {
            PlayerState magician = Make("魔", CharacterDefinition.Magician);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            // 手值 2 -> 魔术师应把它改写成攻击
            magician.LeftHand.SetValue(2);
            var rewriter = magician.Character as IHandEffectRewriter;
            Assert.NotNull(rewriter, "魔术师应实现 IHandEffectRewriter。");

            HandEffectType effect = rewriter.RewriteEffect(magician, magician.LeftHand, 5, null);
            Assert.AreNotEqual((int)HandEffectType.None, (int)effect,
                "运算出 2 对魔术师应是攻击，而不是无效果。");

            // 结算一次 2 的攻击：伤害应为 2 + 攻(0) - 守方防(1) = 1
            int before = foe.Health;
            var ctx = new AttackContext(magician, foe, magician.LeftHand, 2);
            ctx.Resolve(null, null);

            int expected = 2 + magician.Character.AttackBonus - foe.Character.DefenseBonus;
            Assert.AreEqual(expected, before - foe.Health,
                "2 点攻击应造成吃攻防加成后的伤害。");
        }

        [Test]
        public void Base_game_still_treats_two_as_no_effect()
        {
            // 反向对照：别的角色运算出 2 仍然是无效果（规则只给魔术师）
            Assert.AreEqual(HandEffectType.None, Hand.GetEffectType(2),
                "引擎基线里 2 仍然是无效果 —— 魔术师是例外，不是全局改动。");

            var soldier = Make("兵", CharacterDefinition.Soldier);
            var rewriter = soldier.Character as IHandEffectRewriter;
            Assert.True(rewriter == null || rewriter.RewriteEffect(
                    soldier, soldier.LeftHand, 5, null) == HandEffectType.None,
                "士兵的 2 不应变成攻击。");
        }

        [Test]
        public void Magician_has_range_two_and_soft_stats()
        {
            PlayerState magician = Make("魔", CharacterDefinition.Magician);

            Assert.AreEqual(2, GameManager.BaseRangeOf(magician), "魔术师攻击距离 2。");
            Assert.AreEqual(0, magician.Character.AttackBonus, "攻 0。");
            Assert.AreEqual(-1, magician.Character.DefenseBonus, "防 −1。");
        }
    }
}
