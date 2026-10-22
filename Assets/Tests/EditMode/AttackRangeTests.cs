using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **攻击距离**与两个"换射程"技能（战士·破阵者碎盾 / 游荡者·风流剑客强化）。
    ///
    /// 玩家确认的规则：
    ///   · 合法攻击目标 = 处于**攻击范围内**的其他角色（自己除外；队友也是合法目标）
    ///   · 攻击距离 = 座位环上的最短距离（阵亡者从环上摘掉）
    ///   · 白板与战士·破阵者/野蛮人·守望者道途/游荡者·刺客/战士·剑士/武僧·忍宗/野蛮人·野兽道途/游荡者·风流剑客 = 1；**法师·咒法学派 / 牧师·生命领域 = 无限**
    ///   · 战士·破阵者：发起攻击时若另一只手是完整盾牌，可令其破碎 -> 本次攻击距离无限
    ///   · 游荡者·风流剑客：2 变 3 强化攻击时 -> 本次攻击距离无限
    ///
    /// ⚠️ 距离只在 2v2 / 8 人局有意义：1v1 两人相邻，距离恒为 1。
    /// </summary>
    [TestFixture]
    public class AttackRangeTests
    {
        /// <summary>
        /// 造一局 2v2（座位环 1-2-3-4，队伍 {A,D} 与 {B,C}）。
        /// </summary>
        private static GameManager Match2v2(CharacterDefinition defA = null,
                                            CharacterDefinition defB = null,
                                            IChoiceProvider sinkA = null)
        {
            var a = CharacterFactory.CreatePlayer("A", defA ?? CharacterDefinition.Blank);
            var b = CharacterFactory.CreatePlayer("B", defB ?? CharacterDefinition.Blank);
            var c = CharacterFactory.CreatePlayer("C", CharacterDefinition.Blank);
            var d = CharacterFactory.CreatePlayer("D", CharacterDefinition.Blank);

            var game = new GameManager(a, b, sinkA ?? new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            game.AddPlayersFor2v2(c, new SimpleChoiceProvider(),
                                  d, new SimpleChoiceProvider());
            return game;
        }

        // ============================================================
        // 基础攻击距离
        // ============================================================

        [Test]
        public void Melee_characters_have_range_one()
        {
            CharacterDefinition[] melee =
            {
                CharacterDefinition.Blank, CharacterDefinition.Breaker,
                CharacterDefinition.PathOfTheWarden, CharacterDefinition.Assassin,
                CharacterDefinition.Swordsman, CharacterDefinition.WayOfTheNinja,
                CharacterDefinition.PathOfTheBeast, CharacterDefinition.Swashbuckler,
            };

            for (int i = 0; i < melee.Length; i++)
            {
                var p = CharacterFactory.CreatePlayer(melee[i].DisplayName, melee[i]);
                Assert.AreEqual(1, GameManager.BaseRangeOf(p),
                    melee[i].DisplayName + " 的攻击距离应为 1。");
            }
        }

        [Test]
        public void SchoolOfConjuration_and_cleric_have_infinite_range()
        {
            CharacterDefinition[] ranged =
            {
                CharacterDefinition.SchoolOfConjuration, CharacterDefinition.LifeDomain,
            };

            for (int i = 0; i < ranged.Length; i++)
            {
                var p = CharacterFactory.CreatePlayer(ranged[i].DisplayName, ranged[i]);
                Assert.AreEqual(int.MaxValue, GameManager.BaseRangeOf(p),
                    ranged[i].DisplayName + " 的攻击距离应为无限。");
            }
        }

        // ============================================================
        // 距离与可达性
        // ============================================================

        [Test]
        public void CanReach_respects_the_ring_distance()
        {
            GameManager game = Match2v2();

            // 4 人环：1-2-3-4，首尾相邻
            //   A(1) <-> B(2) = 1
            //   A(1) <-> C(3) = 2
            //   A(1) <-> D(4) = 1
            Assert.True(game.CanReach(game.PlayerA, game.PlayerB), "距离 1 应可达。");
            Assert.True(game.CanReach(game.PlayerA, game.PlayerD), "距离 1 应可达（首尾相邻）。");
            Assert.False(game.CanReach(game.PlayerA, game.PlayerC),
                "距离 2 超出基础距离 1 -> 不可达。");
        }

        [Test]
        public void Infinite_range_can_reach_anyone_in_the_ring()
        {
            GameManager game = Match2v2(CharacterDefinition.SchoolOfConjuration,
                                        CharacterDefinition.Blank);

            Assert.True(game.CanReach(game.PlayerA, game.PlayerB));
            Assert.True(game.CanReach(game.PlayerA, game.PlayerC), "无限距离应够到距离 2 的。");
            Assert.True(game.CanReach(game.PlayerA, game.PlayerD));
        }

        [Test]
        public void An_explicit_range_override_extends_reach_for_this_attack_only()
        {
            GameManager game = Match2v2();

            Assert.False(game.CanReach(game.PlayerA, game.PlayerC), "基础距离够不到。");
            Assert.True(game.CanReach(game.PlayerA, game.PlayerC, int.MaxValue),
                "传入无限距离覆盖后应够得到。");
        }

        // ============================================================
        // 攻击目标合法性（BoardController）
        // ============================================================

        [Test]
        public void Board_only_allows_in_range_opponents_as_attack_targets()
        {
            GameManager game = Match2v2();
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                            new SimpleChoiceProvider());
            board.BeginTurn();

            Assert.AreSame(game.PlayerA, board.ActivePlayer, "A 先手（座位 1）。");

            // ⚠️ 2v2 的座位环是 1-2-3-4、**首尾同队**：{A, D} 与 {B, C}。
            //    所以 A 的两名对手是 B(2) 与 C(3)，距离分别是 1 与 2。
            //    => 距离 1 的白板能打到 B；C 够不到；D 是队友，但现在是合法目标。
            Assert.True(board.CanBeAttacked(game.PlayerB), "B 距离 1 且是敌人 -> 可攻击。");
            Assert.False(board.CanBeAttacked(game.PlayerC), "C 距离 2 -> 够不到。");
            Assert.True(board.CanBeAttacked(game.PlayerD), "D 是队友，但队友也是合法目标。");
            Assert.False(board.CanBeAttacked(game.PlayerA), "自己不是攻击目标。");
        }

        [Test]
        public void Melee_range_in_two_versus_two_reaches_only_the_adjacent_enemy()
        {
            // 这是 2v2 的一个**重要推论**：环上首尾同队 =>
            // 队友相邻（距离 1），两名对手都在距离…各为 1 与 2。
            // 所以距离 1 的角色在 2v2 里**只能打到相邻的那一名对手**。
            GameManager game = Match2v2();

            Assert.AreEqual(1, game.DistanceBetween(game.PlayerA, game.PlayerB),
                "A↔B 应为 1（敌人，相邻）。");
            Assert.AreEqual(2, game.DistanceBetween(game.PlayerA, game.PlayerC),
                "A↔C 应为 2（另一名敌人，环上对角）。");
            Assert.AreEqual(1, game.DistanceBetween(game.PlayerA, game.PlayerD),
                "A↔D 应为 1（队友，首尾相邻）。");

            var board = new BoardController(game, new SimpleChoiceProvider(),
                                            new SimpleChoiceProvider());
            board.BeginTurn();

            Assert.True(board.CanBeAttacked(game.PlayerB), "够得到相邻的敌人。");
            Assert.False(board.CanBeAttacked(game.PlayerC), "够不到对角的敌人。");
        }

        [Test]
        public void A_teammate_is_a_legal_attack_target()
        {
            GameManager game = Match2v2();
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                            new SimpleChoiceProvider());
            board.BeginTurn();

            // {A, D} 一队 -> D 虽然是队友，但现在也是合法攻击目标。
            Assert.True(board.CanBeAttacked(game.PlayerD),
                "队友也是攻击目标（距离 1）。");
        }

        // ============================================================
        // 距离不足时的结算
        // ============================================================

        [Test]
        public void Out_of_range_attack_changes_hand_but_deals_no_damage()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 4);          // 4 + 4 = 8 -> 攻击

            int cBefore = game.PlayerC.Health;

            // A 用 C（距离 2）的手当被加数 -> 挨打的是 C，但距离不足
            TurnResult r = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand,
                                            game.PlayerC.LeftHand);

            Assert.AreEqual(8, r.ResultValue, "4+4=8（攻击值）。");
            Assert.AreEqual(8, game.PlayerA.LeftHand.Value, "运算照常发生（手值已改）。");
            Assert.AreEqual(0, r.DamageDealt, "距离不足 -> 不造成伤害。");
            Assert.AreEqual(cBefore, game.PlayerC.Health, "目标不该掉血。");
        }

        [Test]
        public void In_range_attack_deals_damage()
        {
            GameManager game = Match2v2();
            TestSetup.SetAllFourHands(game, 4);

            int bBefore = game.PlayerB.Health;

            TurnResult r = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand,
                                            game.PlayerB.LeftHand);

            Assert.AreEqual(8, r.ResultValue);
            Assert.True(r.DamageDealt > 0, "距离 1 -> 应造成伤害。");
            Assert.True(game.PlayerB.Health < bBefore, "目标应掉血。");
        }

        // ============================================================
        // 战士·破阵者：碎盾换射程
        // ============================================================

        [Test]
        public void Breaker_range_break_is_offered_on_an_attack_with_a_formed_other_hand()
        {
            GameManager game = Match2v2(CharacterDefinition.Breaker,
                                        CharacterDefinition.Blank);
            TestSetup.SetAllFourHands(game, 1);

            PlayerState breaker = game.PlayerA;
            TestSetup.SetHand(breaker.LeftHand, 4);       // 4 + 4 = 8 -> 攻击
            TestSetup.SetHand(breaker.RightHand, 5);      // 另一只手：完整盾牌
            TestSetup.SetHand(game.PlayerB.LeftHand, 4);  // 被加数 4 -> 结果 8
            breaker.RightHand.SetShieldState(ShieldState.Formed);

            var context = new PreTurnChoiceContext(breaker, game.PlayerB,
                                                   breaker.LeftHand, game.PlayerB.LeftHand);

            var requests = new List<ChoiceRequest>();
            (breaker.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            bool offered = false;
            for (int i = 0; i < requests.Count; i++)
                if (requests[i].Effect == HandEffect.BreakerRangeBreak) offered = true;

            Assert.True(offered,
                "攻击且另一只手是完整盾牌时，应提供「碎盾换射程」。" +
                $"现场：isAttack={context.IsAttack} effect={context.Effect} " +
                $"acted={context.ActedHand?.Value} target={context.TargetHand?.Value} " +
                $"other={breaker.RightHand.Value}/{breaker.RightHand.State} " +
                $"requestCount={requests.Count}");
        }

        [Test]
        public void Breaker_range_break_is_not_offered_without_a_formed_other_hand()
        {
            GameManager game = Match2v2(CharacterDefinition.Breaker,
                                        CharacterDefinition.Blank);
            TestSetup.SetAllFourHands(game, 1);

            PlayerState breaker = game.PlayerA;
            TestSetup.SetHand(breaker.LeftHand, 4);       // 攻击
            TestSetup.SetHand(breaker.RightHand, 3);      // 不是盾牌
            TestSetup.SetHand(game.PlayerB.LeftHand, 4);  // 被加数 4 -> 结果 8

            var context = new PreTurnChoiceContext(breaker, game.PlayerB,
                                                   breaker.LeftHand, game.PlayerB.LeftHand);

            var requests = new List<ChoiceRequest>();
            (breaker.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                Assert.False(requests[i].Effect == HandEffect.BreakerRangeBreak,
                    "另一只手不是完整盾牌时不该提供「碎盾换射程」。");
        }

        [Test]
        public void Breaker_range_break_is_not_offered_for_a_non_attack()
        {
            // 非攻击不需要攻击距离 -> 不该打扰玩家
            GameManager game = Match2v2(CharacterDefinition.Breaker,
                                        CharacterDefinition.Blank);
            TestSetup.SetAllFourHands(game, 1);

            PlayerState breaker = game.PlayerA;
            TestSetup.SetHand(breaker.LeftHand, 1);       // 1 + 1 = 2 -> 无效果
            TestSetup.SetHand(breaker.RightHand, 5);
            breaker.RightHand.SetShieldState(ShieldState.Formed);

            var context = new PreTurnChoiceContext(breaker, game.PlayerB,
                                                   breaker.LeftHand, game.PlayerB.LeftHand);

            var requests = new List<ChoiceRequest>();
            (breaker.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
                Assert.False(requests[i].Effect == HandEffect.BreakerRangeBreak,
                    "非攻击时不该提供「碎盾换射程」。");
        }

        [Test]
        public void Breaker_range_break_breaks_the_shield_when_chosen()
        {
            // 玩家选了"发动" -> 那面完整盾牌应被打碎（这是换射程的代价）
            var sink = new ScriptedChoiceProvider();

            GameManager game = Match2v2(CharacterDefinition.Breaker,
                                        CharacterDefinition.Blank, sink);
            TestSetup.SetAllFourHands(game, 1);

            PlayerState breaker = game.PlayerA;
            TestSetup.SetHand(breaker.LeftHand, 4);       // 4 + 4 = 8 -> 攻击
            TestSetup.SetHand(breaker.RightHand, 5);
            TestSetup.SetHand(game.PlayerB.LeftHand, 4);
            breaker.RightHand.SetShieldState(ShieldState.Formed);

            sink.SetChoice(HandEffect.BreakerRangeBreak, breaker.RightHand);

            // 直接跑结算：距离 1 的对手（B），确保攻击走到分派
            game.ExecuteTurn(breaker, breaker.LeftHand, game.PlayerB.LeftHand);

            Assert.AreEqual(ShieldState.Broken, breaker.RightHand.State,
                "选了「碎盾换射程」后，另一只手的盾应被打碎。");
        }

        [Test]
        public void Breaker_range_break_keeps_the_shield_when_declined()
        {
            var sink = new ScriptedChoiceProvider();

            GameManager game = Match2v2(CharacterDefinition.Breaker,
                                        CharacterDefinition.Blank, sink);
            TestSetup.SetAllFourHands(game, 1);

            PlayerState breaker = game.PlayerA;
            TestSetup.SetHand(breaker.LeftHand, 4);
            TestSetup.SetHand(breaker.RightHand, 5);
            TestSetup.SetHand(game.PlayerB.LeftHand, 4);
            breaker.RightHand.SetShieldState(ShieldState.Formed);

            sink.Decline(HandEffect.BreakerRangeBreak);

            game.ExecuteTurn(breaker, breaker.LeftHand, game.PlayerB.LeftHand);

            Assert.AreEqual(ShieldState.Formed, breaker.RightHand.State,
                "放弃换射程时应保留那面盾。");
        }
    }
}
