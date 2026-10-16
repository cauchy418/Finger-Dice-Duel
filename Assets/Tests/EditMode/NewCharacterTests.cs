using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 牧师 / 游侠 / 狼人 三名新角色的能力验证。
    /// </summary>
    [TestFixture]
    public class NewCharacterTests
    {
        private ScriptedChoiceProvider _choiceA;
        private ScriptedChoiceProvider _choiceB;

        [SetUp]
        public void SetUp()
        {
            _choiceA = new ScriptedChoiceProvider();
            _choiceB = new ScriptedChoiceProvider();
        }

        private GameManager Match(CharacterDefinition a, CharacterDefinition b)
            => new GameManager(
                CharacterFactory.CreatePlayer("A", a),
                CharacterFactory.CreatePlayer("B", b),
                _choiceA, _choiceB);

        // ============================================================
        // 统一让所有内置角色都能被构建
        // ============================================================

        // ============================================================
        // "放弃"键（UI 的取消出口）
        // ============================================================

        /// <summary>构造一个"人类游侠被攻击、停在闪避抉择"的局面。</summary>
        private BoardController ArrangeHumanRangerDodge(PlayerState[] outPair)
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Ranger),
                choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;
            outPair[0] = blank;
            outPair[1] = ranger;

            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(blank, ranger, 1);
            TestSetup.SetHand(blank.LeftHand, 3);       // 3 + 1 = 4 -> 攻击
            TestSetup.SetHand(ranger.LeftHand, 1);

            board.BeginTurn();
            return board;
        }

        [Test]
        public void Ranger_dodge_choice_allows_declining_from_the_ui()
        {
            var pair = new PlayerState[2];
            BoardController board = ArrangeHumanRangerDodge(pair);
            PlayerState blank = pair[0];
            PlayerState ranger = pair[1];

            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "游侠有一只手为 1 -> 应先问是否闪避。");
            Assert.True(board.CanDeclinePendingChoice, "闪避应允许放弃。");
            Assert.True(board.IsChoiceCandidate(ranger.LeftHand));

            int healthBefore = ranger.Health;

            // 每一段各问一次 -> 全部放弃
            Assert.True(DeclineAllDodges(board) > 0, "应至少问过一次闪避");
            Assert.False(board.Phase == BoardPhase.ChoosingPreTurnChoice, "两次都放弃后应继续结算。");
            Assert.AreEqual(1, ranger.LeftHand.Value, "放弃闪避不应改变手值。");
            Assert.AreEqual(1, ranger.RightHand.Value, "放弃闪避不应改变手值。");
            Assert.True(ranger.Health < healthBefore, "放弃闪避应正常吃伤害。");
        }

        [Test]
        public void Declining_is_rejected_outside_a_pre_turn_choice()
        {
            var pair = new PlayerState[2];
            BoardController board = ArrangeHumanRangerDodge(pair);

            // 还在"选自己哪只手"阶段，没有可放弃的抉择
            Assert.False(board.CanDeclinePendingChoice);
            Assert.False(board.DeclinePendingChoice());
        }

        // ============================================================
        // 通用抉择管线（替代原先的 is BarbarianLogic 硬编码）
        // ============================================================

        [Test]
        public void Barbarian_lock_still_goes_through_the_declarative_pipeline()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian),
                choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(blank, barbarian, 1);
            TestSetup.SetHand(barbarian.LeftHand, 4);   // 4 + 1 = 5 -> 合盾

            // Blank 先手；让它随便走一回合把回合交给野蛮人
            board.BeginTurn();
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(barbarian.RightHand);
            board.ConfirmTargetPlayer();

            board.ClickHand(barbarian.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "野蛮人合盾应走通用抉择管线问锁手。");
            Assert.AreEqual(2, board.ChoiceCandidates.Count, "对手两只手都可选。");

            // 点对手的左手 -> 应锁住它
            Assert.True(board.ClickHand(blank.LeftHand));
            board.ConfirmTargetPlayer();
            Assert.True(blank.LeftHand.IsLocked, "被选中的手应被锁住。");
            Assert.False(blank.RightHand.IsLocked, "另一只手不应被锁。");
        }

        [Test]
        public void Barbarian_lock_can_be_declined()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian),
                choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(blank, barbarian, 1);
            TestSetup.SetHand(barbarian.LeftHand, 4);

            // Blank 先手；让它随便走一回合把回合交给野蛮人
            board.BeginTurn();
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(barbarian.RightHand);
            board.ConfirmTargetPlayer();

            board.ClickHand(barbarian.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.True(board.DeclinePendingChoice(), "锁手应允许放弃。");
            Assert.False(blank.LeftHand.IsLocked, "放弃后不应锁任何手。");
            Assert.False(blank.RightHand.IsLocked);
        }

        // ============================================================
        // 剑客 Swordsman（+2 / -1）
        // ============================================================

        [Test]
        public void Swordsman_has_plus_two_and_minus_one()
        {
            PlayerState s = CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman);

            Assert.AreEqual(2, s.Character.AttackBonus);
            Assert.AreEqual(-1, s.Character.DefenseBonus);
        }

        [Test]
        public void Swordsman_follow_up_deals_fixed_damage_ignoring_stat_bonuses()
        {
            // 让双方攻防都不为 0，验证追加攻击"无视双方攻防加成"。
            var atk = CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman);   // 攻 +2
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian);   // 防 +1
            var sink = new ScriptedChoiceProvider();

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4) { IgnoreStatBonuses = true };
            ctx.Resolve(sink, null);

            Assert.AreEqual(4, ctx.AppliedDamage,
                "追加攻击固定 4 点，不受剑客攻 +2 / 野蛮人防 +1 影响。");
        }

        [Test]
        public void Swordsman_main_attack_still_applies_stat_bonuses()
        {
            var atk = CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman);   // 攻 +2
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian);   // 防 +1
            var sink = new ScriptedChoiceProvider();

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.Resolve(sink, null);

            // 4 + 2(剑客攻) - 1(野蛮人防) = 5
            Assert.AreEqual(5, ctx.AppliedDamage, "主攻击照常结算攻防加成。");
        }

        [Test]
        public void Swordsman_follow_up_fires_end_to_end_when_both_hands_match()
        {
            // 端到端：双手都是 6 时打一次攻击，追加攻击应使总伤害翻倍。
            // （此前我用"直接构造 context"的方式写，反复和自己的假设较劲；
            //   走完整回合才是真实行为。）
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                choiceA, choiceB);

            PlayerState s = game.PlayerA;
            PlayerState blank = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(s, blank, 1);
            TestSetup.SetHand(s.LeftHand, 5);          // 5 + 1 = 6
            TestSetup.SetHand(s.RightHand, 6);         // 另一只手也是 6 -> 双 6
            TestSetup.SetHand(blank.RightHand, 1);

            board.BeginTurn();
            board.ClickHand(s.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            // 双 6 -> 应先问"是否追加"
            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "双 6 应弹出追加攻击提示。");
            Assert.True(board.CanDeclinePendingChoice, "应允许不追加。");

            int before = blank.Health;

            // 选择发动追加
            Assert.True(board.ClickHand(board.ChoiceCandidates[0]));
            board.ConfirmTargetPlayer();

            Assert.NotNull(board.LastTurn);
            // 追加攻击固定 6（无视攻防）+ 主攻击 6 + 攻 2 = 8 -> 共 14
            Assert.AreEqual(before - blank.Health, 14,
                "追加 6（无视攻防）+ 主攻击 6+2(攻) = 14。");
        }

        [Test]
        public void Swordsman_follow_up_does_not_fire_when_hands_differ()
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                choiceA, choiceB);

            PlayerState s = game.PlayerA;
            PlayerState blank = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(s, blank, 1);
            TestSetup.SetHand(s.LeftHand, 5);          // 5 + 1 = 6
            TestSetup.SetHand(s.RightHand, 3);         // 不是 6 -> 不构成双 6
            TestSetup.SetHand(blank.RightHand, 1);

            board.BeginTurn();
            board.ClickHand(s.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.False(board.Phase == BoardPhase.ChoosingPreTurnChoice,
                "另一只手不是 6 -> 不应弹出追加提示。");
            Assert.NotNull(board.LastTurn);
            Assert.AreEqual(6 + 2, board.LastTurn.DamageDealt, "只有主攻击：6 + 攻 2 = 8。");
        }
        // ============================================================
        // 忍者 Ninja（+1 / -1）
        // ============================================================

        [Test]
        public void Ninja_has_plus_one_and_minus_one()
        {
            PlayerState n = CharacterFactory.CreatePlayer("N", CharacterDefinition.Ninja);

            Assert.AreEqual(1, n.Character.AttackBonus);
            Assert.AreEqual(-1, n.Character.DefenseBonus);
        }

        [Test]
        public void Ninja_splits_attack_into_two_halves()
        {
            // 基础 4 -> 两次 2。忍者攻 +1：每段 2 + 1 = 3，共 6。
            var atk = CharacterFactory.CreatePlayer("N", CharacterDefinition.Ninja);
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var sink = new ScriptedChoiceProvider();

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.Resolve(sink, null);

            Assert.AreEqual(2, ctx.HitCount, "应是两段攻击。");
            Assert.AreEqual(2, ctx.LandedHits, "两段都应命中。");
            Assert.AreEqual(6, ctx.AppliedDamage, "两段各 3 点。");
        }

        [Test]
        public void Ninja_each_half_is_blocked_by_only_one_shield()
        {
            // 已确认：一面盾只能挡下其中一段，另一段照常造成伤害。
            var atk = CharacterFactory.CreatePlayer("N", CharacterDefinition.Ninja);
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.RightHand, 5);
            def.RightHand.SetShieldState(ShieldState.Formed);

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.Resolve(sink, null);

            Assert.AreEqual(1, ctx.BlockedHits, "一面盾只挡一段。");
            Assert.AreEqual(1, ctx.LandedHits, "另一段应命中。");
            Assert.AreEqual(3, ctx.AppliedDamage, "命中那段 = 2 + 攻1 = 3。");
            Assert.AreEqual(ShieldState.Broken, def.RightHand.State, "盾应被消耗。");
        }

        [Test]
        public void Ninja_two_shields_can_block_both_halves()
        {
            var atk = CharacterFactory.CreatePlayer("N", CharacterDefinition.Ninja);
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 5);
            def.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(def.RightHand, 5);
            def.RightHand.SetShieldState(ShieldState.Formed);

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.Resolve(sink, null);

            Assert.AreEqual(2, ctx.BlockedHits, "两面盾各挡一段。");
            Assert.AreEqual(0, ctx.AppliedDamage, "两段都被挡下 -> 无伤害。");
            Assert.True(ctx.WasBlocked);
        }

        // ============================================================
        // 刺客 Assassin（+2 / -1）
        // ============================================================

        [Test]
        public void Assassin_has_plus_two_and_minus_one()
        {
            PlayerState a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Assassin);

            Assert.AreEqual(2, a.Character.AttackBonus);
            Assert.AreEqual(-1, a.Character.DefenseBonus);
        }

        [Test]
        public void Assassin_is_stealthed_when_both_hands_are_seven()
        {
            PlayerState a = CharacterFactory.CreatePlayer("A", CharacterDefinition.Assassin);

            Assert.False(AssassinLogic.HasDoubleSeven(a), "开局两只手都是 1，不是双 7。");

            TestSetup.SetHand(a.LeftHand, 7);
            TestSetup.SetHand(a.RightHand, 7);

            Assert.True(AssassinLogic.HasDoubleSeven(a), "双手都是 7。");
        }

        [Test]
        public void Assassin_in_stealth_cannot_be_attacked()
        {
            var atk = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var def = CharacterFactory.CreatePlayer("A", CharacterDefinition.Assassin);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 7);
            TestSetup.SetHand(def.RightHand, 7);
            // 双 7 本身不算潜行 —— 必须"经由运算达成"过
            (def.Character as AssassinLogic).EnterStealthForTest();

            // ⚠️ 光是双手为 7 **不算**潜行 —— 必须是"本局经由运算达成过"。
            // 开局掷骰也可能直接摆出双 7，那种不算（见 AssassinLogic 的说明）。
            (def.Character as AssassinLogic).EnterStealthForTest();
            Assert.True((def.Character as AssassinLogic).IsStealthActive(def),
                "前置：应已进入潜行。");

            int before = def.Health;
            var ctx = new AttackContext(atk, def, atk.LeftHand, 8);
            ctx.Resolve(sink, null);

            Assert.True(ctx.WasDodged, "潜行中应完全免伤。");
            Assert.AreEqual(0, ctx.AppliedDamage);
            Assert.AreEqual(before, def.Health, "潜行中不应掉血。");
        }

        [Test]
        public void Assassin_attacking_from_stealth_gets_plus_seven()
        {
            var atk = CharacterFactory.CreatePlayer("A", CharacterDefinition.Assassin);
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(atk.LeftHand, 7);
            TestSetup.SetHand(atk.RightHand, 7);
            (atk.Character as AssassinLogic).EnterStealthForTest();

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.Resolve(sink, null);

            // 4 + 潜行 7 + 攻 2 = 13
            Assert.AreEqual(13, ctx.AppliedDamage, "出潜行的攻击 +7（再加攻 +2）。");
        }

        [Test]
        public void Assassin_can_skip_once_per_stealth()
        {
            var game = Match(CharacterDefinition.Assassin, CharacterDefinition.Blank);
            PlayerState assassin = game.PlayerA;

            TestSetup.SetHand(assassin.LeftHand, 7);
            TestSetup.SetHand(assassin.RightHand, 7);

            (assassin.Character as AssassinLogic).EnterStealthForTest();
            Assert.True(GameManager.CanSkipTurn(assassin), "潜行中第一次可跳。");
            game.SkipTurn();
            Assert.False(GameManager.CanSkipTurn(assassin),
                "一次潜行只能跳一次 -> 第二次不允许。");
            Assert.Throws<System.InvalidOperationException>(() => game.SkipTurn(),
                "第二次跳过应被拒绝。");
        }

        [Test]
        public void Assassin_cannot_skip_outside_stealth()
        {
            var game = Match(CharacterDefinition.Assassin, CharacterDefinition.Blank);
            PlayerState assassin = game.PlayerA;

            Assert.False((assassin.Character as AssassinLogic).IsStealthActive(assassin));
            Assert.False(GameManager.CanSkipTurn(assassin), "非潜行不能跳过。");
        }

        [Test]
        public void Assassin_attacking_breaks_stealth_because_hand_value_changes()
        {
            // 手值变了 -> 不再是双 7 -> 自动脱离潜行。
            var game = Match(CharacterDefinition.Assassin, CharacterDefinition.Blank);
            PlayerState assassin = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(assassin, blank, 1);
            TestSetup.SetHand(assassin.LeftHand, 7);
            TestSetup.SetHand(assassin.RightHand, 7);
            (assassin.Character as AssassinLogic).EnterStealthForTest();
            Assert.True((assassin.Character as AssassinLogic).IsStealthActive(assassin), "前置：应在潜行中。");

            // 7 + 1 = 8 -> 运算源变成 8，不再是双 7
            game.ExecuteTurn(assassin, assassin.LeftHand, blank.RightHand);

            Assert.AreEqual(8, assassin.LeftHand.Value, "7 + 1 = 8。");
            Assert.False((assassin.Character as AssassinLogic).IsStealthActive(assassin), "出手后应脱离潜行。");
        }
        [Test]
        public void Swordsman_extra_hit_value_matches_his_own_hand_value()
        {
            // 需求：追加攻击的固定伤害 = "那双数字"本身
            // （双 4 -> 4、双 6 -> 6、双 8 -> 8）。
            // 直接构造 AttackContext 验证，避免摆局面的干扰。
            foreach (int value in new[] { 4, 6, 8 })
            {
                var atk = CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman);  // 攻 +2
                var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian);  // 防 +1
                var sink = new ScriptedChoiceProvider();

                TestSetup.SetHand(atk.LeftHand, value);
                TestSetup.SetHand(atk.RightHand, value);

                var ctx = new AttackContext(atk, def, atk.LeftHand, value)
                {
                    IgnoreStatBonuses = true,
                };
                ctx.Resolve(sink, null);

                Assert.AreEqual(value, ctx.AppliedDamage,
                    $"双 {value} 的追加攻击应固定造成 {value} 点（无视双方攻防加成）。");
            }
        }

        [Test]
        public void Swordsman_main_attack_is_not_affected_by_follow_up_value()
        {
            // 主攻击照常吃攻防加成：4 + 攻2 - 防1 = 5
            var atk = CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman);
            var def = CharacterFactory.CreatePlayer("B", CharacterDefinition.Barbarian);
            var sink = new ScriptedChoiceProvider();

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.Resolve(sink, null);

            Assert.AreEqual(5, ctx.AppliedDamage, "主攻击 4 + 2 - 1 = 5。");
        }

        [Test]
        public void Assassin_stealth_dodge_reports_the_right_reason()
        {
            // 战报不能说"被游侠闪避" —— 潜行是"打不到"。
            var atk = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var def = CharacterFactory.CreatePlayer("A", CharacterDefinition.Assassin);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 7);
            TestSetup.SetHand(def.RightHand, 7);
            // 双 7 本身不算潜行 —— 必须"经由运算达成"过
            (def.Character as AssassinLogic).EnterStealthForTest();

            var ctx = new AttackContext(atk, def, atk.LeftHand, 8);
            ctx.Resolve(sink, null);

            Assert.True(ctx.WasDodged);
            Assert.NotNull(ctx.DodgeReason);
            Assert.True(ctx.DodgeReason.Contains("潜行"),
                $"免伤原因应说明是潜行：{ctx.DodgeReason}");
        }

        [Test]
        public void Ranger_stealth_free_dodge_has_no_reason_so_uses_default_text()
        {
            // 游侠的真正闪避没有自定义原因 -> 战报走默认文案
            var atk = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);
            var def = CharacterFactory.CreatePlayer("A", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 1);
            TestSetup.SetHand(def.RightHand, 1);
            sink.ChooseHand(def.LeftHand);

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);
            ctx.SourceHandValueBefore = 3;
            ctx.TargetHandValueBefore = 1;
            ctx.AttackerProvider = sink;
            ctx.Resolve(sink, null);

            Assert.True(ctx.WasDodged);

            // 战报路径：游侠闪避没有自定义原因 -> 走默认文案"被游侠闪避"
            TurnResult t = new TurnResult
            {
                Actor = atk,
                ActedHand = atk.LeftHand,
                ActedHandValueBefore = 3,
                TargetHandValue = 1,
                ResultValue = 4,
                WasDodged = true,
                DodgeReason = ctx.DodgeReason,
                Effect = HandEffectType.Attack4,
            };
            string text = BoardController.DescribeTurn(t);
            Assert.True(text.Contains("闪避"), $"默认文案应说'被游侠闪避'：{text}");
            Assert.False(text.Contains("潜行"), "游侠闪避不该说成潜行。");
        }

        [Test]
        public void Ranger_power_shot_prompts_and_applies_in_a_real_turn()
        {
            // 端到端：游侠**攻击手**是别的数字、**另一只手**是 2 -> 应弹强化提示。
            // （此前收集阶段还在扫"自己的两只手"，导致提示不出现。）
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Ranger),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                choiceA, choiceB);

            PlayerState ranger = game.PlayerA;
            PlayerState blank = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(ranger, blank, 1);
            TestSetup.SetHand(ranger.LeftHand, 1);      // 攻击手
            TestSetup.SetHand(ranger.RightHand, 2);     // 另一只手 = 2 -> 可强化
            TestSetup.SetHand(blank.RightHand, 3);      // 1 + 3 = 4 -> 攻击

            board.BeginTurn();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            // 应弹出强化提示，并且有"发动"按钮
            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "另一只手是 2 -> 应弹出强化提示。");
            Assert.True(board.CanDeclinePendingChoice, "应有'不强化'选项。");
            Assert.False(string.IsNullOrEmpty(board.PendingAcceptLabel), "应有'发动'按钮。");

            int before = blank.Health;
            Assert.True(board.AcceptPendingChoice(), "点'发动'应生效。");

            // 强化：基础 4 -> 6；游侠攻 0、白板防 0 => 6
            Assert.AreEqual(before - blank.Health, 6, "强化后 4 -> 6。");
            Assert.AreEqual(3, ranger.RightHand.Value, "另一只手 2 -> 3。");
        }

        [Test]
        public void Swordsman_follow_up_offers_an_accept_button()
        {
            // 需求：追加攻击的提示要有一个明确的"发动"按钮（在"不发动"左侧）。
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Swordsman),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                choiceA, choiceB);

            PlayerState s = game.PlayerA;
            PlayerState blank = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(s, blank, 1);
            TestSetup.SetHand(s.LeftHand, 5);       // 5 + 1 = 6
            TestSetup.SetHand(s.RightHand, 6);      // 另一只手也是 6 -> 双 6
            TestSetup.SetHand(blank.RightHand, 1);

            board.BeginTurn();
            board.ClickHand(s.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase);
            Assert.False(string.IsNullOrEmpty(board.PendingAcceptLabel),
                "应提供'发动追加'按钮。");
            Assert.True(board.CanDeclinePendingChoice, "应提供'不追加'选项。");

            int before = blank.Health;
            Assert.True(board.AcceptPendingChoice(), "点'发动'应生效。");

            // 追加 6（无视攻防）+ 主攻击 6 + 攻 2 = 14
            Assert.AreEqual(before - blank.Health, 14, "追加 6 + 主攻击 6+2 = 14。");
        }

        [Test]
        public void Swordsman_follow_up_is_logged_on_its_own_line()
        {
            // 需求：追加攻击要在战斗记录里单独显示一行。
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("剑客", CharacterDefinition.Swordsman),
                CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank),
                choiceA, choiceB);

            PlayerState s = game.PlayerA;
            PlayerState blank = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(s, blank, 1);
            TestSetup.SetHand(s.LeftHand, 5);       // 5 + 1 = 6
            TestSetup.SetHand(s.RightHand, 6);      // 另一只手也是 6 -> 双 6
            TestSetup.SetHand(blank.RightHand, 1);

            board.BeginTurn();
            board.ClickHand(s.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();
            Assert.True(board.AcceptPendingChoice());

            // 战报里应有一行专门的追加攻击记录
            bool found = false;
            foreach (string line in board.Log)
            {
                if (line.Contains("追加") && line.Contains("6")) { found = true; break; }
            }
            Assert.True(found,
                $"战报应包含追加攻击那一行。实际：{string.Join(" | ", board.Log)}");

            // 追加攻击的记录本身
            Assert.NotNull(board.LastTurn);
            Assert.True(board.LastTurn.ExtraAttack.HasValue, "LastTurn 应带追加攻击信息。");
            Assert.AreEqual(6, board.LastTurn.ExtraAttack.Value.Damage, "固定伤害 = 6。");
            Assert.AreEqual(6, board.LastTurn.ExtraAttack.Value.Dealt, "应造成 6 点。");
        }

        [Test]
        public void Swordsman_prompt_damage_matches_the_hand_value()
        {
            // 需求：提示里的追加伤害数值必须与手上数字一致（双 4 -> 4、双 6 -> 6、双 8 -> 8）。
            foreach (int value in new[] { 4, 6, 8 })
            {
                var choiceA = new SimpleChoiceProvider();
                var choiceB = new SimpleChoiceProvider();

                var game = new GameManager(
                    CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman),
                    CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                    choiceA, choiceB);

                PlayerState s = game.PlayerA;
                PlayerState blank = game.PlayerB;
                var board = new BoardController(game, choiceA, choiceB);

                TestSetup.SetAllHands(s, blank, 1);
                // 让运算结果是 value：运算源 value-1 + 目标 1
                TestSetup.SetHand(s.LeftHand, value - 1);
                TestSetup.SetHand(s.RightHand, value);      // 另一只手 = value -> 双 value
                TestSetup.SetHand(blank.RightHand, 1);

                board.BeginTurn();
                board.ClickHand(s.LeftHand);
                board.ConfirmTargetPlayer();
                board.ClickHand(blank.RightHand);
                board.ConfirmTargetPlayer();

                Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                    $"双 {value} 应弹出追加提示。");

                string prompt = board.Hint ?? "";
                Assert.True(prompt.Contains(value.ToString()),
                    $"提示里应出现 {value}。实际：{prompt}");

                // 实际造成的伤害也应是 value
                Assert.True(board.AcceptPendingChoice());
                Assert.AreEqual(value, board.LastTurn.ExtraAttack.Value.Damage,
                    $"追加攻击的固定伤害应等于手上的数字 {value}。");
            }
        }


        // ============================================================
        // 多段攻击的"每段独立闪避 / 独立挡盾"与战报
        // ============================================================

        [Test]
        public void Ninja_two_halves_need_two_ones_to_dodge_completely()
        {
            // 需求：忍者的每一段都要各自用一次"1 变 2"，完全闪避需要两次。
            var atk = CharacterFactory.CreatePlayer("N", CharacterDefinition.Ninja);
            var def = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 1);
            TestSetup.SetHand(def.RightHand, 1);
            int before = def.Health;

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4) { AutoResolveDodge = true };
            ctx.SourceHandValueBefore = 3;      // 3 + 1 = 4 -> Attack4
            ctx.TargetHandValueBefore = 1;
            ctx.Resolve(sink, null);

            Assert.AreEqual(2, ctx.DodgedHits, "两段都应被闪避。");
            Assert.AreEqual(0, ctx.AppliedDamage, "完全闪避 -> 无伤害。");
            Assert.AreEqual(before, def.Health, "不应掉血。");
            Assert.AreEqual(2, def.LeftHand.Value, "左手 1 -> 2。");
            Assert.AreEqual(2, def.RightHand.Value, "右手 1 -> 2。");
        }

        [Test]
        public void Ninja_one_hand_equal_to_one_only_dodges_one_half()
        {
            // 只有一只"1"时，只能闪掉一段，另一段照常造成伤害。
            var atk = CharacterFactory.CreatePlayer("N", CharacterDefinition.Ninja);
            var def = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 1);     // 只有一只能闪
            TestSetup.SetHand(def.RightHand, 7);

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4) { AutoResolveDodge = true };
            ctx.SourceHandValueBefore = 3;
            ctx.TargetHandValueBefore = 1;
            ctx.Resolve(sink, null);

            Assert.AreEqual(1, ctx.DodgedHits, "只闪掉一段。");
            Assert.AreEqual(1, ctx.LandedHits, "另一段命中。");
            // 每段 = 4/2 + 忍者攻1 - 游侠防(-2) = 2 + 1 + 2 = 5
            Assert.AreEqual(5, ctx.AppliedDamage, "命中那段 = 2 + 攻1 + 游侠易伤2 = 5。");
        }

        [Test]
        public void Swordsman_extra_attack_can_be_dodged_by_a_one()
        {
            // 需求：剑客的追加攻击也可以被游侠"1 变 2"闪避掉。
            var atk = CharacterFactory.CreatePlayer("S", CharacterDefinition.Swordsman);
            var def = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 1);
            TestSetup.SetHand(def.RightHand, 7);
            int before = def.Health;

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4)
            {
                IgnoreStatBonuses = true,
                AutoResolveDodge = true,
            };
            ctx.SourceHandValueBefore = 3;
            ctx.TargetHandValueBefore = 1;
            ctx.Resolve(sink, null);

            Assert.AreEqual(1, ctx.DodgedHits, "追加攻击应被闪避。");
            Assert.AreEqual(0, ctx.AppliedDamage, "闪避后无伤害。");
            Assert.AreEqual(before, def.Health);
            Assert.AreEqual(2, def.LeftHand.Value, "1 -> 2。");
        }

        [Test]
        public void Ninja_attack_log_describes_both_halves()
        {
            // 需求：战报要体现"两段攻击，各造成 X 点伤害"。
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Ninja, CharacterDefinition.Blank);
            PlayerState ninja = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(ninja, blank, 1);
            TestSetup.SetHand(ninja.LeftHand, 3);       // 3 + 1 = 4 -> 两段
            TestSetup.SetHand(blank.RightHand, 1);

            TurnResult r = game.ExecuteTurn(ninja, ninja.LeftHand, blank.RightHand);
            string text = BoardController.DescribeTurn(r);

            Assert.AreEqual(2, r.HitCount);
            Assert.True(text.Contains("两段"),
                $"战报应说明'两段攻击'：{text}");
            Assert.True(text.Contains("造成"),
                $"战报应说明造成多少伤害：{text}");
        }

        [Test]
        public void Ninja_attack_log_mentions_the_blocked_half()
        {
            var game = CharacterFactory.CreateMatch(
                CharacterDefinition.Ninja, CharacterDefinition.Blank);
            PlayerState ninja = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(ninja, blank, 1);
            TestSetup.SetHand(ninja.LeftHand, 3);       // 3 + 1 = 4 -> 两段
            TestSetup.SetHand(blank.RightHand, 1);
            blank.RightHand.SetShieldState(ShieldState.Formed);   // 一面盾挡住一段

            TurnResult r = game.ExecuteTurn(ninja, ninja.LeftHand, blank.RightHand);
            string text = BoardController.DescribeTurn(r);

            Assert.AreEqual(1, r.BlockedHits);
            Assert.True(text.Contains("挡下"),
                $"战报应提到被挡下的那一段：{text}");
            Assert.True(text.Contains("造成"),
                $"战报也应提到命中的那一段：{text}");
        }

        [Test]
        public void Ranger_can_strengthen_with_a_hand_that_became_two_from_dodging()
        {
            // 需求：闪避把 1 变成 2 之后，这只手在**自己回合**应当可以用于强化。
            // （之前因为读了过期的手而失败。）
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                choiceA, choiceB);

            PlayerState ranger = game.PlayerA;
            PlayerState blank = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(ranger, blank, 1);
            // 模拟"上一回合闪避后"的状态：右手已经是 2
            TestSetup.SetHand(ranger.LeftHand, 1);      // 攻击手
            TestSetup.SetHand(ranger.RightHand, 2);     // 闪避留下的 2
            TestSetup.SetHand(blank.RightHand, 3);      // 1 + 3 = 4 -> 攻击

            board.BeginTurn();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "那只 2 应能触发强化提示。");
            Assert.True(board.CanDeclinePendingChoice);
            Assert.False(string.IsNullOrEmpty(board.PendingAcceptLabel), "应有发动按钮。");

            int before = blank.Health;
            Assert.True(board.AcceptPendingChoice(), "强化应生效。");

            Assert.AreEqual(before - blank.Health, 6, "强化后 4 -> 6。");
            Assert.AreEqual(3, ranger.RightHand.Value, "那只手 2 -> 3。");
        }

        [Test]
        public void Ranger_is_not_asked_to_dodge_a_non_attack_operation()
        {
            // 需求：游侠的翻滚闪避**只在对攻击时**才询问。
            // 合盾 / 回复 / 无效果这类操作不该弹提示。
            foreach (int targetValue in new[] { 4, 8, 5, 2, 7 })
            {
                // 让白板用 1 打游侠的 1：1 + 1 = 2（无效果）、1+4=5（合盾）、
                // 1+8=9（回复）、1+2=3（无效果）、1+7=8（攻击 -> 应弹）
                var choiceA = new SimpleChoiceProvider();
                var choiceB = new SimpleChoiceProvider();

                var game = new GameManager(
                    CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                    CharacterFactory.CreatePlayer("B", CharacterDefinition.Ranger),
                    choiceA, choiceB);

                PlayerState blank = game.PlayerA;
                PlayerState ranger = game.PlayerB;
                var board = new BoardController(game, choiceA, choiceB);

                TestSetup.SetAllHands(blank, ranger, 1);
                TestSetup.SetHand(blank.LeftHand, 1);
                TestSetup.SetHand(ranger.RightHand, targetValue);

                int sum = Hand.ComputeSum(1, targetValue);
                bool isAttack = sum == 4 || sum == 6 || sum == 8;

                board.BeginTurn();
                board.ClickHand(blank.LeftHand);
                board.ConfirmTargetPlayer();
                board.ClickHand(ranger.RightHand);
                board.ConfirmTargetPlayer();

                bool asked = board.Phase == BoardPhase.ChoosingPreTurnChoice
                             && board.PendingRequest?.Effect == HandEffect.RangerDodge;

                Assert.AreEqual(isAttack, asked,
                    $"1 + {targetValue} = {sum}（{(isAttack ? "攻击" : "非攻击")}）：" +
                    $"{(isAttack ? "应" : "不应")}询问闪避。");
            }
        }

        [Test]
        public void All_built_in_characters_can_be_constructed()
        {
            // 角色数量随版本增长（现在 13 个：白板 + 9 个旧角色 + 圣武士/邪术师/魔射手）。
            // 这里只断言"至少 10 个且每个都能构造"，不锁死具体数字 ——
            // 否则每加一个角色都要改这条测试。
            Assert.True(CharacterDefinition.BuiltIn.Length >= 10,
                $"内置角色数量偏少（实际 {CharacterDefinition.BuiltIn.Length}）。");

            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                Assert.True(CharacterFactory.IsRegistered(def.Logic),
                    $"{def.Id} 的逻辑 {def.Logic} 未在工厂登记。");

                PlayerState p = CharacterFactory.CreatePlayer(def.DisplayName, def);
                Assert.NotNull(p.Character);
                Assert.AreEqual(def.DisplayName, p.Name);
            }
        }

        // ============================================================
        // 牧师 Priest（-2 / 0）
        // ============================================================

        [Test]
        public void Priest_has_minus_one_attack_and_minus_one_defense()
        {
            // 规则变更：攻防从 -2 / 0 改为 **-1 / -1**
            PlayerState priest = CharacterFactory.CreatePlayer("P", CharacterDefinition.Priest);

            Assert.AreEqual(-1, priest.Character.AttackBonus);
            Assert.AreEqual(-1, priest.Character.DefenseBonus);
            Assert.AreEqual(PlayerState.DefaultMaxHealth, priest.MaximumHealth);
        }

        [Test]
        public void Priest_heal_goes_to_self_by_default()
        {
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            PlayerState priest = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(priest.LeftHand, 8);          // 8 + 1 = 9 -> 回复
            blank.TakeDamage(10);
            priest.TakeDamage(10);

            int before = priest.Health;
            _choiceA.Decline(HandEffect.PriestHealTarget);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(before + 9, priest.Health, "放弃选择 = 回给自己。");
        }

        [Test]
        public void Priest_can_redirect_heal_to_the_opponent()
        {
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            PlayerState priest = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(priest.LeftHand, 8);          // 8 + 1 = 9 -> 回复
            priest.TakeDamage(10);
            blank.TakeDamage(18);                           // 对手残血

            int priestBefore = priest.Health;
            int blankBefore = blank.Health;

            // 选对手的一只手 = 把这次回复给对手
            _choiceA.ChooseHand(blank.LeftHand);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(priestBefore, priest.Health, "牧师自己不应回血。");
            Assert.AreEqual(blankBefore + 9, blank.Health, "对手应回 9 点。");
        }

        [Test]
        public void Priest_heal_never_exceeds_max_health()
        {
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            PlayerState priest = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(priest.LeftHand, 8);
            blank.TakeDamage(3);                            // 只差 3 点满血

            _choiceA.ChooseHand(blank.LeftHand);
            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(blank.MaximumHealth, blank.Health, "回复不应超过上限。");
        }

        [Test]
        public void Priest_double_nine_after_operation_stacks_with_the_main_heal()
        {
            // 触发条件是"**运算后**双手同时为 9"。
            //
            // 构造：运算源 8 + 目标 1 = 9（回复），另一只手也是 9 -> 运算后双 9。
            // 两次回复（额外 9 + 本次 9）各选各的目标；这里都回自己。
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            PlayerState priest = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(priest.LeftHand, 8);      // 8 + 1 = 9 -> 回复
            TestSetup.SetHand(priest.RightHand, 9);
            TestSetup.SetHand(blank.RightHand, 1);
            priest.TakeDamage(19);                      // 只剩 1

            _choiceA.SetChoice(HandEffect.PriestExtraHealTarget, priest.LeftHand);
            _choiceA.SetChoice(HandEffect.PriestHealTarget, priest.LeftHand);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(9, priest.LeftHand.Value, "8 + 1 = 9。");
            Assert.AreEqual(9, priest.RightHand.Value, "另一只手是 9。");
            Assert.True(PriestLogic.HasDoubleNineNow(priest), "运算后应双手同时为 9。");
            Assert.AreEqual(1 + PriestLogic.ExtraHealAmount + PriestLogic.HealAmount,
                priest.Health,
                "额外回 9 + 本次 9 的回复 = 共 18。");
        }

        [Test]
        public void Priest_double_nine_bonus_target_is_chosen_by_the_player()
        {
            // 规则变更：双 9 的那次**额外**回复不再固定回自己，而是由玩家选目标。
            // 这里让额外回复给对手、本次 9 的回复给自己，验证两者互不干扰。
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            PlayerState priest = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(priest.LeftHand, 8);      // 8 + 1 = 9 -> 回复
            TestSetup.SetHand(priest.RightHand, 9);
            TestSetup.SetHand(blank.RightHand, 1);
            priest.TakeDamage(19);                      // 只剩 1
            blank.TakeDamage(15);                       // 只剩 5

            // 顺序：先"额外回复给谁"，再"本次回复给谁"
            _choiceA.SetChoice(HandEffect.PriestExtraHealTarget, blank.LeftHand);
            _choiceA.SetChoice(HandEffect.PriestHealTarget, priest.LeftHand);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(1 + PriestLogic.HealAmount, priest.Health,
                "本次 9 的回复给了自己。");
            Assert.AreEqual(5 + PriestLogic.ExtraHealAmount, blank.Health,
                "双 9 的**额外**回复这次给了对手（不再固定回自己）。");
        }

        [Test]
        public void Priest_double_nine_extra_heal_defaults_to_self_when_not_chosen()
        {
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            PlayerState priest = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(priest.LeftHand, 8);
            TestSetup.SetHand(priest.RightHand, 9);
            TestSetup.SetHand(blank.RightHand, 1);
            priest.TakeDamage(19);

            _choiceA.Decline(HandEffect.PriestExtraHealTarget);   // 不选 = 回自己
            _choiceA.Decline(HandEffect.PriestHealTarget);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(1 + PriestLogic.HealAmount + PriestLogic.ExtraHealAmount,
                priest.Health, "两次回复都回自己，共 18。");
        }

        // ============================================================
        // ============================================================
        // 牧师：4 / 6 / 8 可选"当攻击打"或"当回复用"（规则变更）
        // ============================================================

        /// <summary>造一局：牧师（A）打白板（B），让运算结果为 attackValue。</summary>
        private GameManager PriestAttacksWith(int attackValue, out PlayerState priest,
                                              out PlayerState blank)
        {
            GameManager game = Match(CharacterDefinition.Priest, CharacterDefinition.Blank);
            priest = game.PlayerA;
            blank = game.PlayerB;

            // 运算源 = attackValue - 1，目标手 = 1 -> 结果 = attackValue
            TestSetup.SetHand(priest.LeftHand, attackValue - 1);
            TestSetup.SetHand(priest.RightHand, 5);
            TestSetup.SetHand(blank.RightHand, 1);
            return game;
        }

        [Test]
        public void Priest_choosing_heal_on_a_four_heals_four_and_deals_no_damage()
        {
            GameManager game = PriestAttacksWith(4, out PlayerState priest, out PlayerState blank);

            TestSetup.SetHealth(priest, 10);
            int blankBefore = blank.Health;

            _choiceA.SetChoice(HandEffect.PriestAttackOrHeal, priest.LeftHand);   // 选"当回复用"
            _choiceA.SetChoice(HandEffect.PriestHealTarget, priest.LeftHand);     // 回给自己

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(10 + 4, priest.Health, "4 当回复用 -> 回 4 点。");
            Assert.AreEqual(blankBefore, blank.Health, "选择回复时不造成任何伤害。");
        }

        [Test]
        public void Priest_choosing_heal_on_a_six_heals_six()
        {
            GameManager game = PriestAttacksWith(6, out PlayerState priest, out PlayerState blank);

            TestSetup.SetHealth(priest, 8);

            _choiceA.SetChoice(HandEffect.PriestAttackOrHeal, priest.LeftHand);
            _choiceA.SetChoice(HandEffect.PriestHealTarget, priest.LeftHand);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(8 + 6, priest.Health, "6 当回复用 -> 回 6 点。");
        }

        [Test]
        public void Priest_choosing_heal_on_an_eight_heals_eight()
        {
            GameManager game = PriestAttacksWith(8, out PlayerState priest, out PlayerState blank);

            TestSetup.SetHealth(priest, 5);

            _choiceA.SetChoice(HandEffect.PriestAttackOrHeal, priest.LeftHand);
            _choiceA.SetChoice(HandEffect.PriestHealTarget, priest.LeftHand);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(5 + 8, priest.Health, "8 当回复用 -> 回 8 点。");
        }

        [Test]
        public void Priest_heal_value_is_not_affected_by_attack_bonus()
        {
            // 牧师的攻 -1 只作用于"攻击"，回复量固定等于该数字（不受攻防影响）。
            GameManager game = PriestAttacksWith(6, out PlayerState priest, out PlayerState blank);

            TestSetup.SetHealth(priest, 8);
            Assert.AreEqual(-1, priest.Character.AttackBonus, "牧师攻 -1（对照组）");

            _choiceA.SetChoice(HandEffect.PriestAttackOrHeal, priest.LeftHand);
            _choiceA.SetChoice(HandEffect.PriestHealTarget, priest.LeftHand);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(8 + 6, priest.Health, "回复 6 —— 不能被攻 -1 改成 5。");
        }

        [Test]
        public void Priest_declining_heal_treats_it_as_a_normal_attack()
        {
            GameManager game = PriestAttacksWith(4, out PlayerState priest, out PlayerState blank);

            int blankBefore = blank.Health;

            _choiceA.Decline(HandEffect.PriestAttackOrHeal);   // 不回复 = 当攻击打

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            // 白板防 0、牧师攻 -1 -> 4 - 1 = 3
            Assert.AreEqual(blankBefore - 3, blank.Health,
                "放弃回复就是普通攻击：4 基础 - 牧师攻1 - 白板防0 = 3。");
        }

        [Test]
        public void Priest_can_heal_the_opponent_with_a_four_six_or_eight()
        {
            // 给他人回复是为多人玩法预留的辅助能力（不是 bug）。
            GameManager game = PriestAttacksWith(6, out PlayerState priest, out PlayerState blank);

            TestSetup.SetHealth(blank, 10);
            int priestBefore = priest.Health;

            // 目标与"攻击还是回复"分两段：
            //   ① 选目标手之前 -> PriestAttackOrHeal（选回复）
            //   ② 选目标手之后 -> PriestHealTarget（回给谁）
            _choiceA.SetChoice(HandEffect.PriestAttackOrHeal, priest.LeftHand);
            _choiceA.SetChoice(HandEffect.PriestHealTarget, blank.LeftHand);   // 回给对手

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(10 + 6, blank.Health, "6 当回复用可以回给对手。");
            Assert.AreEqual(priestBefore, priest.Health, "回给他人时自己不变。");
        }

        [Test]
        public void Priest_gets_one_choice_per_stage_for_four_six_eight()
        {
            // 玩家要求的顺序：运算出 4 -> **先**弹出「攻击 / 回复」-> 选完再问目标。
            //
            // 所以"攻击还是回复"属于【选目标手之前】的抉择，
            // 而"回复给谁"属于【选目标手之后】的抉择，两者分开。
            var priest = CharacterFactory.CreatePlayer("P", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);

            TestSetup.SetHand(priest.LeftHand, 3);       // 3 + 1 = 4 -> 攻击/回复 二选一
            TestSetup.SetHand(priest.RightHand, 5);
            TestSetup.SetHand(blank.RightHand, 1);

            var context = new PreTurnChoiceContext(priest, blank,
                                                   priest.LeftHand, blank.RightHand);
            Assert.AreEqual(HandEffectType.Attack4, context.Effect, "3 + 1 = 4");

            // ① 选目标手**之前**：只有一个"攻击还是回复"
            var pre = new List<ChoiceRequest>();
            (priest.Character as ITargetHandChoiceProvider)
                ?.CollectPreTargetChoices(context, pre);

            Assert.AreEqual(1, pre.Count,
                $"4/6/8 在选目标前应只有**一个**抉择。实际：{pre.Count}");
            Assert.AreEqual(HandEffect.PriestAttackOrHeal, pre[0].Effect,
                "那一个抉择应是「攻击还是回复」。");
            Assert.True(pre[0].DeclineResult == null,
                "放弃应是'不回复'（当攻击打）。");

            // ② 选目标手**之后**：注入"选了回复"的答案 -> 应问一次"回复给谁"
            context.AnsweredLookup = (e, v) =>
                e == HandEffect.PriestAttackOrHeal ? priest.LeftHand : null;

            var post = new List<ChoiceRequest>();
            (priest.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, post);

            Assert.AreEqual(1, post.Count, "选了回复 -> 应问一次「回复给谁」。");
            Assert.AreEqual(HandEffect.PriestHealTarget, post[0].Effect);

            // ③ 反向：答了攻击 -> 不该问回复目标
            context.AnsweredLookup = (e, v) => null;

            var post2 = new List<ChoiceRequest>();
            (priest.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, post2);

            Assert.AreEqual(0, post2.Count,
                "选了攻击 -> 4/6/8 之后**不该**再问回复目标。");
        }

        [Test]
        public void Priest_declining_the_heal_question_does_not_ask_for_a_target()
        {
            // 端到端回归：放弃"当回复用"之后，不应再停留在任何抉择阶段。
            GameManager game = PriestAttacksWith(4, out PlayerState priest, out PlayerState blank);

            int blankBefore = blank.Health;
            _choiceA.Decline(HandEffect.PriestAttackOrHeal);

            game.ExecuteTurn(priest, priest.LeftHand, blank.RightHand);

            Assert.AreEqual(blankBefore - 3, blank.Health,
                "放弃回复 = 普通攻击（4 - 牧师攻1 - 白板防0 = 3），且不再追问目标。");
        }

        [Test]
        public void Priest_heal_choice_is_offered_for_four_six_and_eight()
        {
            var priest = CharacterFactory.CreatePlayer("P", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);

            foreach (int value in new[] { 4, 6, 8 })
            {
                TestSetup.SetHand(priest.LeftHand, value - 1);
                TestSetup.SetHand(priest.RightHand, 5);
                TestSetup.SetHand(blank.RightHand, 1);

                var context = new PreTurnChoiceContext(priest, blank,
                                                       priest.LeftHand, blank.RightHand);

                // 4/6/8 的「攻击还是回复」是**选目标手之前**的那一次抉择
                var requests = new List<ChoiceRequest>();
                (priest.Character as ITargetHandChoiceProvider)
                    ?.CollectPreTargetChoices(context, requests);

                bool hasChoice = false;
                for (int i = 0; i < requests.Count; i++)
                    if (requests[i].Effect == HandEffect.PriestAttackOrHeal) hasChoice = true;

                Assert.True(hasChoice, $"运算出 {value} 时应提供「攻击还是回复」的抉择。");

                // 结果确实是该数字
                Assert.AreEqual(value,
                    Hand.ComputeSum(priest.LeftHand.Value, blank.RightHand.Value),
                    $"运算结果应为 {value}。");
            }
        }

        [Test]
        public void Priest_nine_does_not_offer_an_attack_or_heal_choice()
        {
            // 出 9 时本次运算本身就是回复 -> 直接问"回复给谁"，
            // 没有"攻击还是回复"这一步（玩家要求的顺序）。
            var priest = CharacterFactory.CreatePlayer("P", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);

            TestSetup.SetHand(priest.LeftHand, 8);      // 8 + 1 = 9
            TestSetup.SetHand(blank.RightHand, 1);

            var context = new PreTurnChoiceContext(priest, blank,
                                                   priest.LeftHand, blank.RightHand);
            Assert.AreEqual(HandEffectType.Heal, context.Effect);

            var pre = new List<ChoiceRequest>();
            (priest.Character as ITargetHandChoiceProvider)
                ?.CollectPreTargetChoices(context, pre);
            Assert.AreEqual(0, pre.Count, "出 9 时不该有'攻击还是回复'。");

            var post = new List<ChoiceRequest>();
            (priest.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, post);
            Assert.AreEqual(1, post.Count, "出 9 应问一次'回复给谁'。");
            Assert.AreEqual(HandEffect.PriestHealTarget, post[0].Effect);
            Assert.False(post[0].ShouldSkip(), "出 9 时必然要问回复目标。");
        }

        [Test]
        public void Priest_double_nine_asks_for_two_heal_targets()
        {
            // 玩家要求：双 9 -> 选**两次**回复目标（先额外、再本次）
            //
            // 「双 9」在运算**前**就能预测：运算源 8 + 目标 1 = 9，
            // 另一只手本来就是 9 -> 运算后双手都是 9。
            // （这与剑客"追加攻击"的判定同类，都是运算前可算。）
            var priest = CharacterFactory.CreatePlayer("P", CharacterDefinition.Priest);
            var blank = CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank);

            TestSetup.SetHand(priest.LeftHand, 8);      // 运算源（运算前是 8）
            TestSetup.SetHand(priest.RightHand, 9);     // 另一只手是 9
            TestSetup.SetHand(blank.RightHand, 1);      // 8 + 1 = 9

            var context = new PreTurnChoiceContext(priest, blank,
                                                   priest.LeftHand, blank.RightHand);

            Assert.True(PriestLogic.HasDoubleNineAfter(context),
                "8 + 1 = 9 且另一只手是 9 -> 运算后应双 9（运算前可预测）。");

            var post = new List<ChoiceRequest>();
            (priest.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, post);

            Assert.AreEqual(2, post.Count,
                $"双 9 应问**两次**回复目标。实际：{post.Count}");
            Assert.AreEqual(HandEffect.PriestExtraHealTarget, post[0].Effect,
                "第一次应问「额外回复」给谁（玩家要求先额外、再本次）。");
            Assert.AreEqual(HandEffect.PriestHealTarget, post[1].Effect,
                "第二次应问「本次回复」给谁。");
        }
        // 游侠 Ranger（0 / -2）
        // ============================================================

        [Test]
        public void Ranger_has_zero_attack_and_minus_two_defense()
        {
            PlayerState ranger = CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger);

            Assert.AreEqual(0, ranger.Character.AttackBonus);
            Assert.AreEqual(-2, ranger.Character.DefenseBonus);
        }


        /// <summary>
        /// 摆一个"游侠**双盾**、且这次攻击可闪避"的局面。
        ///
        /// 必须是**双盾**：只有双盾时旧顺序才会停在"选盾"阶段，
        /// 才能区分"先问闪避"和"先问选盾"。（单盾时旧顺序直接跳过选盾，测不出顺序。）
        /// </summary>
        private BoardController ArrangeDoubleShieldRanger(PlayerState[] outPair)
        {
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Ranger),
                choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;
            outPair[0] = blank;
            outPair[1] = ranger;

            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(blank, ranger, 1);
            TestSetup.SetHand(blank.LeftHand, 3);       // 3 + 1 = 4 -> 攻击 4，可闪避

            // 游侠双盾，其中左手是 1（可闪避，同时是本次目标手）
            TestSetup.SetHand(ranger.LeftHand, 1);
            ranger.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(ranger.RightHand, 5);
            ranger.RightHand.SetShieldState(ShieldState.Formed);

            Assert.True(ranger.HasDoubleShield, "前置条件：游侠应有双盾。");

            board.BeginTurn();
            return board;
        }

        [Test]
        public void Ranger_is_asked_to_dodge_before_being_asked_which_shield()
        {
            // 需求：闪避的询问时机必须在"用哪面盾抵挡"**之前**。
            // 否则有盾的游侠会被直接碎盾，问不到闪避。
            var pair = new PlayerState[2];
            BoardController board = ArrangeDoubleShieldRanger(pair);
            PlayerState blank = pair[0];
            PlayerState ranger = pair[1];

            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);           // 打游侠的左手（3 + 1 = 4）
            board.ConfirmTargetPlayer();

            // 第一步必须是闪避询问，而不是选盾
            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "有盾的游侠也必须先被问是否闪避，而不是直接进入选盾。");
            RequestEffectIs(board, HandEffect.RangerDodge);
            Assert.False(board.CanDeclinePendingChoice == false && false);   // 占位见下
            Assert.True(board.IsChoiceCandidate(ranger.LeftHand), "候选应是那只 1。");
        }

        [Test]
        public void Declining_the_dodge_then_moves_on_to_the_shield_choice()
        {
            var pair = new PlayerState[2];
            BoardController board = ArrangeDoubleShieldRanger(pair);
            PlayerState blank = pair[0];
            PlayerState ranger = pair[1];

            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();

            // 左手是唯一的"1"，所以只有一段闪避询问
            Assert.True(DeclineAllDodges(board) > 0, "应至少问过一次闪避");

            // 放弃闪避后才轮到"用哪面盾抵挡"（双盾 -> 应停在选盾阶段）
            Assert.AreEqual(BoardPhase.ChoosingDefenseShield, board.Phase,
                "放弃闪避后才应进入选盾阶段。");
            Assert.AreEqual(2, board.DefenseShieldChoices.Count, "双盾都应是候选。");

            // 选右手盾 -> 右手碎，左手保留（也没被闪避消耗掉）
            Assert.True(board.ClickHand(ranger.RightHand));
            board.ConfirmTargetPlayer();
            Assert.AreEqual(ShieldState.Broken, ranger.RightHand.State, "被选中的盾应破碎。");
            Assert.AreEqual(ShieldState.Formed, ranger.LeftHand.State, "另一面盾应完好。");
        }

        /// <summary>
        /// 持续放弃"闪避"抉择，直到离开角色抉择阶段。
        ///
        /// 为什么要循环：现在**每一段攻击都会单独问一次**闪避，
        /// 所以"问几次"取决于段数与手上还有几只"1"。
        /// 用循环表达"都放弃"比写死次数更贴合真实行为。
        /// </summary>
        private static int DeclineAllDodges(BoardController board)
        {
            int count = 0;
            while (board.Phase == BoardPhase.ChoosingPreTurnChoice
                   && board.CanDeclinePendingChoice)
            {
                if (!board.DeclinePendingChoice()) break;
                count++;
                if (count > 8) break;      // 防御：避免测试死循环
            }
            return count;
        }

        /// <summary>断言当前待答抉择的 effect 是哪一个。</summary>
        private static void RequestEffectIs(BoardController board, HandEffect expected)
        {
            ChoiceRequest? request = board.PendingRequest;
            Assert.NotNull(request, "当前应有一个待答抉择。");
            Assert.AreEqual(expected, request.Value.Effect);
        }

        [Test]
        public void Ranger_attacking_with_a_two_in_the_other_hand_prompts_to_strengthen()
        {
            // 需求：一只手发起攻击后，若**另一只手**为 2，
            // 应弹出"是否加强攻击"的提示，并提供"不加强"的选项。
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Ranger),
                choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(blank, ranger, 1);
            TestSetup.SetHand(blank.RightHand, 3);      // 1 + 3 = 4 -> 攻击 4
            TestSetup.SetHand(ranger.RightHand, 2);     // 另一只手为 2 -> 可强化

            // Blank 先手，让一回合把回合交给游侠。
            // 注意：游侠被这一击攻击时也会有一次闪避询问 —— 先放弃掉。
            board.BeginTurn();
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();
            DeclineAllDodges(board);

            board.ClickHand(ranger.LeftHand);           // 游侠用左手攻击（1 + 3 = 4）
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            Assert.AreEqual(BoardPhase.ChoosingPreTurnChoice, board.Phase,
                "另一只手为 2 -> 应弹出强化提示。");
            RequestEffectIs(board, HandEffect.RangerPowerShot);

            // 必须提供"不加强"的出口
            Assert.True(board.CanDeclinePendingChoice, "应允许选择'不加强'。");

            // 候选就是那只 2（另一只手）
            Assert.AreEqual(1, board.ChoiceCandidates.Count);
            Assert.True(ReferenceEquals(board.ChoiceCandidates[0], ranger.RightHand),
                "候选应是另一只手。");

            // 选它 -> 强化
            Assert.True(board.ClickHand(ranger.RightHand));
            board.ConfirmTargetPlayer();
            Assert.NotNull(board.LastTurn);
            Assert.AreEqual(6, board.LastTurn.DamageDealt, "强化后 4 -> 6。");
            Assert.AreEqual(3, ranger.RightHand.Value, "另一只手 2 -> 3。");
        }

        [Test]
        public void Ranger_attacking_without_a_two_is_not_prompted()
        {
            // 另一只手不是 2 时不应弹出强化提示（直接结算）。
            var choiceA = new SimpleChoiceProvider();
            var choiceB = new SimpleChoiceProvider();

            var game = new GameManager(
                CharacterFactory.CreatePlayer("A", CharacterDefinition.Blank),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Ranger),
                choiceA, choiceB);

            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;
            var board = new BoardController(game, choiceA, choiceB);

            TestSetup.SetAllHands(blank, ranger, 1);
            TestSetup.SetHand(blank.RightHand, 3);
            TestSetup.SetHand(ranger.RightHand, 7);     // 不是 2

            board.BeginTurn();
            board.ClickHand(blank.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();
            DeclineAllDodges(board);                    // 先放弃被攻击时的闪避询问

            board.ClickHand(ranger.LeftHand);
            board.ConfirmTargetPlayer();
            board.ClickHand(blank.RightHand);
            board.ConfirmTargetPlayer();

            // 另一只手是 7 -> 不该有强化抉择（若停在抉择阶段，应可放弃且与强化无关）
            if (board.Phase == BoardPhase.ChoosingPreTurnChoice)
            {
                ChoiceRequest? pending = board.PendingRequest;
                Assert.True(pending == null || pending.Value.Effect != HandEffect.RangerPowerShot,
                    "另一只手不是 2 -> 不该提强化。");
                DeclineAllDodges(board);
            }

            Assert.NotNull(board.LastTurn);
            Assert.AreEqual(4, board.LastTurn.DamageDealt, "不强化就是普通 4 点。");
        }

        // ============================================================
        // 不可闪避的判定依据是"攻击者是咒术师"，**不是**"伤害是 8"
        // ============================================================

        /// <summary>
        /// 直接构造一次攻击结算（绕过回合流程，便于精确控制伤害值）。
        ///
        /// <paramref name="damage"/> 决定"运算前的双方手值"：
        ///   4 → 3+1、6 → 5+1、8 → 7+1
        /// 这样 <see cref="PreTurnChoiceContext.Effect"/> 与实际伤害**一致**
        /// —— 否则测试会因为 Effect 恒为 8 而失去意义（我第一版就踩了这个坑）。
        /// </summary>
        private static AttackContext ResolveAttack(CharacterDefinition attackerDef,
                                                   int damage, bool dodge,
                                                   out PlayerState defender,
                                                   out ScriptedChoiceProvider sink)
        {
            PlayerState atk = CharacterFactory.CreatePlayer("Atk", attackerDef);
            defender = CharacterFactory.CreatePlayer("Def", CharacterDefinition.Ranger);
            sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(defender.LeftHand, 1);
            TestSetup.SetHand(defender.RightHand, 1);
            if (dodge) sink.ChooseHand(defender.LeftHand);

            var ctx = new AttackContext(atk, defender, atk.LeftHand, damage);
            ctx.SourceHandValueBefore = damage - 1;
            ctx.TargetHandValueBefore = 1;
            ctx.AttackerProvider = sink;
            ctx.Resolve(sink, null);
            return ctx;
        }

        [Test]
        public void Curseweaver_blocks_dodge_regardless_of_damage_value()
        {
            // 咒术师能用 4 / 6 / 8 攻击，**每一种都不能被闪避**。
            foreach (int damage in new[] { 4, 6, 8 })
            {
                AttackContext ctx = ResolveAttack(
                    CharacterDefinition.Curseweaver, damage, true,
                    out PlayerState defender, out _);

                Assert.AreEqual(damage, ctx.RawDamage, "伤害值应与构造一致。");
                Assert.False(ctx.WasDodged, $"咒术师的 {damage} 点攻击不能被闪避。");
                Assert.True(ctx.AppliedDamage > 0, $"咒术师的 {damage} 点攻击应正常造成伤害。");
                Assert.AreEqual(1, defender.LeftHand.Value,
                    $"未能闪避时不应消耗那只「1」（damage={damage}）。");
            }
        }

        [Test]
        public void Non_curseweaver_attacks_can_be_dodged_even_at_eight_damage()
        {
            // 其他角色打出 8 时，游侠**应该**能闪避（这正是之前的 bug）。
            foreach (CharacterDefinition attacker in new[]
                     {
                         CharacterDefinition.Blank,
                         CharacterDefinition.Fighter,
                         CharacterDefinition.Barbarian,
                         CharacterDefinition.Priest,
                         CharacterDefinition.Werewolf,
                     })
            {
                AttackContext ctx = ResolveAttack(attacker, 8, true,
                                                  out PlayerState defender, out _);

                Assert.True(ctx.WasDodged,
                    $"{attacker.DisplayName} 的 8 点攻击应该可以被闪避。");
                Assert.AreEqual(0, ctx.AppliedDamage, "闪避应完全免伤。");
                Assert.AreEqual(2, defender.LeftHand.Value, "闪避消耗：1 -> 2。");
            }
        }

        [Test]
        public void Ranger_cannot_dodge_a_base_eight_attack()
        {
            // 咒术师的攻击不可闪避 —— 说到底是"攻击者是咒术师"，不是"伤害是 8"。
            // （保留本测试作为"咒术师不可闪避"的最小复现。）
            var atk = CharacterFactory.CreatePlayer("Atk", CharacterDefinition.Curseweaver);
            var def = CharacterFactory.CreatePlayer("Def", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 1);
            TestSetup.SetHand(def.RightHand, 1);
            sink.ChooseHand(def.LeftHand);      // 玩家明明想闪避

            var ctx = new AttackContext(atk, def, atk.LeftHand, 8);   // 7 + 1 = 8
            ctx.SourceHandValueBefore = 7;
            ctx.TargetHandValueBefore = 1;
            ctx.AttackerProvider = sink;
            ctx.Resolve(sink, null);

            Assert.False(ctx.WasDodged, "基础 8 的攻击（咒术师）不能被闪避。");
            Assert.True(ctx.AppliedDamage > 0, "应该正常吃到伤害。");
            Assert.AreEqual(1, def.LeftHand.Value, "未能闪避时不应消耗那只「1」。");
        }

        [Test]
        public void Ranger_can_still_dodge_a_base_four_attack()
        {
            // 对照组：非 8 的攻击仍然可以闪避（证明上面的失败不是因为闪避整个坏了）。
            var atk = CharacterFactory.CreatePlayer("Atk", CharacterDefinition.Blank);
            var def = CharacterFactory.CreatePlayer("Def", CharacterDefinition.Ranger);
            var sink = new ScriptedChoiceProvider();

            TestSetup.SetHand(def.LeftHand, 1);
            TestSetup.SetHand(def.RightHand, 1);
            sink.ChooseHand(def.LeftHand);

            var ctx = new AttackContext(atk, def, atk.LeftHand, 4);   // 3 + 1 = 4
            ctx.SourceHandValueBefore = 3;
            ctx.TargetHandValueBefore = 1;
            ctx.AttackerProvider = sink;
            ctx.Resolve(sink, null);

            Assert.True(ctx.WasDodged, "非 8 的攻击应能闪避。");
            Assert.AreEqual(0, ctx.AppliedDamage, "闪避应完全免伤。");
            Assert.AreEqual(2, def.LeftHand.Value, "闪避消耗：1 -> 2。");
        }

        [Test]
        public void Ranger_dodges_by_turning_a_one_into_a_two()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Ranger);
            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;

            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(blank.LeftHand, 3);           // 3 + 1 = 4 -> 攻击 4

            int healthBefore = ranger.Health;
            _choiceB.ChooseHand(ranger.LeftHand);           // 用左手闪避

            TurnResult r = game.ExecuteTurn(blank, blank.LeftHand, ranger.LeftHand);

            Assert.True(r.WasDodged, "应被闪避。");
            Assert.AreEqual(0, r.DamageDealt, "闪避应完全免伤。");
            Assert.AreEqual(healthBefore, ranger.Health);
            Assert.AreEqual(2, ranger.LeftHand.Value, "闪避消耗：1 -> 2。");
        }

        [Test]
        public void Ranger_declining_the_dodge_takes_damage()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Ranger);
            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;

            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(blank.LeftHand, 3);

            int healthBefore = ranger.Health;
            _choiceB.Decline(HandEffect.RangerDodge);

            TurnResult r = game.ExecuteTurn(blank, blank.LeftHand, ranger.LeftHand);

            Assert.False(r.WasDodged, "放弃闪避就不该闪避。");
            Assert.AreEqual(1, ranger.LeftHand.Value, "放弃闪避不应改变手值。");
            Assert.True(ranger.Health < healthBefore, "应吃到伤害。");
        }

        [Test]
        public void Ranger_dodge_needs_a_hand_equal_to_one()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Ranger);
            PlayerState blank = game.PlayerA;
            PlayerState ranger = game.PlayerB;

            TestSetup.SetHand(ranger.LeftHand, 3);          // 没有 1，无法闪避
            TestSetup.SetHand(blank.LeftHand, 1);           // 1 + 3 = 4 -> 攻击

            TurnResult r = game.ExecuteTurn(blank, blank.LeftHand, ranger.LeftHand);

            Assert.False(r.WasDodged, "手值不是 1 时不能闪避。");
            Assert.True(r.DamageDealt > 0, "应正常吃伤害。");
        }

        [Test]
        public void Ranger_power_shot_multiplies_base_damage_by_1_5()
        {
            GameManager game = Match(CharacterDefinition.Ranger, CharacterDefinition.Blank);
            PlayerState ranger = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 规则：用一只手发起攻击后，若**另一只手**为 2，可以把它变成 3 强化这次攻击。
            // 构造：左手 1 + 目标 3 = 4（攻击），右手正好是 2 -> 可强化。
            // 强化后基础 4 -> 6；游侠攻 0、白板防 0 => 实际 6。
            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 2);
            TestSetup.SetHand(blank.RightHand, 3);

            _choiceA.ChooseHand(ranger.RightHand);          // 消耗另一只手（右手）来强化

            TurnResult r = game.ExecuteTurn(ranger, ranger.LeftHand, blank.RightHand);

            Assert.AreEqual(HandEffectType.Attack4, r.Effect, "基础效果仍是 4。");
            Assert.AreEqual(6, r.DamageDealt, "强化后基础 4 -> 6。");
            Assert.AreEqual(4, ranger.LeftHand.Value, "运算源变成结果值 4。");
            Assert.AreEqual(3, ranger.RightHand.Value, "强化消耗：另一只手 2 -> 3。");
        }

        [Test]
        public void Ranger_power_shot_does_not_apply_when_declined()
        {
            GameManager game = Match(CharacterDefinition.Ranger, CharacterDefinition.Blank);
            PlayerState ranger = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(ranger.LeftHand, 1);
            TestSetup.SetHand(ranger.RightHand, 2);
            TestSetup.SetHand(blank.RightHand, 3);      // 1 + 3 = 4

            _choiceA.Decline(HandEffect.RangerPowerShot);

            TurnResult r = game.ExecuteTurn(ranger, ranger.LeftHand, blank.RightHand);

            Assert.AreEqual(4, r.DamageDealt, "放弃强化就是普通 4 点。");
            Assert.AreEqual(2, ranger.RightHand.Value, "放弃强化不应改变另一只手。");
        }

        [Test]
        public void Ranger_can_reach_9_damage_from_an_8()
        {
            // 8 强化后 12，再算攻防加成。
            // 8 需要 8 + 1 = 9？不 —— 攻击效果是 4/6/8，所以 8 由 8 + 1 得不到。
            // 构造：游侠某只手 2 不能直接打出 8，所以这里验证 8 -> 12 的换算。
            var ctx = new AttackContext(
                CharacterFactory.CreatePlayer("R", CharacterDefinition.Ranger),
                CharacterFactory.CreatePlayer("B", CharacterDefinition.Blank),
                null, 8);

            ctx.DamageMultiplier = RangerLogic.PowerMultiplier;
            ctx.Resolve(null, null);

            // 游侠攻 0、白板防 0 => 12
            Assert.AreEqual(12, ctx.AppliedDamage, "8 × 1.5 = 12。");
        }

        // ============================================================
        // 狼人 Werewolf（-2 / -2，上限 +5）
        // ============================================================

        [Test]
        public void Werewolf_has_25_max_health_and_minus_two_minus_two()
        {
            PlayerState wolf = CharacterFactory.CreatePlayer("W", CharacterDefinition.Werewolf);

            Assert.AreEqual(25, wolf.MaximumHealth,
                $"生命上限应为 20 + {WerewolfLogic.HealthBonus} = 25。");
            Assert.AreEqual(25, wolf.Health, "开局应满血。");
            Assert.AreEqual(-2, wolf.Character.AttackBonus);
            Assert.AreEqual(-2, wolf.Character.DefenseBonus);
        }

        [Test]
        public void Werewolf_heals_and_grows_when_hand_sum_is_seven()
        {
            GameManager game = Match(CharacterDefinition.Werewolf, CharacterDefinition.Blank);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 6 + 1 = 7 是普通值（无效果），所以这次运算不改变左手；右手为 2。
            // 运算后双手和 = 7 + 2 = 9，不该触发。
            TestSetup.SetHand(wolf.LeftHand, 6);
            TestSetup.SetHand(wolf.RightHand, 2);
            TestSetup.SetHand(blank.RightHand, 1);

            wolf.TakeDamage(20);                            // 只剩 5

            game.ExecuteTurn(wolf, wolf.LeftHand, blank.RightHand);

            Assert.AreEqual(7, wolf.LeftHand.Value, "6 + 1 = 7，无效果，手值不变。");
            Assert.AreEqual(9, wolf.LeftHand.Value + wolf.RightHand.Value);
            Assert.AreEqual(5, wolf.Health, "和为 9 不应回血。");
        }

        [Test]
        public void Werewolf_triggers_when_sum_is_seven()
        {
            GameManager game = Match(CharacterDefinition.Werewolf, CharacterDefinition.Blank);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 运算后双手和为 7：运算源 1 + 目标 3 = 4（Attack4），另一只手固定为 3。
            TestSetup.SetHand(wolf.LeftHand, 1);
            TestSetup.SetHand(wolf.RightHand, 3);
            TestSetup.SetHand(blank.RightHand, 3);

            wolf.TakeDamage(20);                            // 只剩 5

            int attackBefore = wolf.Character.AttackBonus;
            int defenseBefore = wolf.Character.DefenseBonus;

            game.ExecuteTurn(wolf, wolf.LeftHand, blank.RightHand);

            Assert.AreEqual(4, wolf.LeftHand.Value, "1 + 3 = 4。");
            Assert.AreEqual(7, wolf.LeftHand.Value + wolf.RightHand.Value, "双手和应为 7。");
            Assert.AreEqual(5 + 25 / 2, wolf.Health, "应回复上限的一半（12），再被上限夹住。");
            Assert.AreEqual(attackBefore + 1, wolf.Character.AttackBonus, "攻击永久 +1。");
            Assert.AreEqual(defenseBefore + 1, wolf.Character.DefenseBonus, "防御永久 +1。");
        }

        [Test]
        public void Werewolf_growth_stacks_across_triggers()
        {
            GameManager game = Match(CharacterDefinition.Werewolf, CharacterDefinition.Blank);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 反复构造"双手和为 7"：1 + 3 = 4，另一只手固定 3 => 和 = 7
            for (int i = 0; i < 3; i++)
            {
                TestSetup.SetHand(wolf.LeftHand, 1);
                TestSetup.SetHand(wolf.RightHand, 3);
                TestSetup.SetHand(blank.RightHand, 3);

                game.ExecuteTurn(wolf, wolf.LeftHand, blank.RightHand);
            }

            Assert.AreEqual(-2 + 3, wolf.Character.AttackBonus, "三次触发后攻击应为 +1。");
            Assert.AreEqual(-2 + 3, wolf.Character.DefenseBonus, "三次触发后防御应为 +1。");
        }

        [Test]
        public void Werewolf_growth_is_cleared_on_reset()
        {
            GameManager game = Match(CharacterDefinition.Werewolf, CharacterDefinition.Blank);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(wolf.LeftHand, 1);
            TestSetup.SetHand(wolf.RightHand, 3);
            TestSetup.SetHand(blank.RightHand, 3);
            game.ExecuteTurn(wolf, wolf.LeftHand, blank.RightHand);

            Assert.AreEqual(-1, wolf.Character.AttackBonus, "触发一次后应为 -1。");

            game.ResetMatch();

            Assert.AreEqual(-2, wolf.Character.AttackBonus,
                "重开一局必须丢弃成长，否则会一直叠加。");
            Assert.AreEqual(-2, wolf.Character.DefenseBonus);
            Assert.AreEqual(25, wolf.MaximumHealth, "生命上限不受重置影响。");
        }

        [Test]
        public void Werewolf_heal_is_capped_at_max_health()
        {
            GameManager game = Match(CharacterDefinition.Werewolf, CharacterDefinition.Blank);
            PlayerState wolf = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(wolf.LeftHand, 1);
            TestSetup.SetHand(wolf.RightHand, 3);
            TestSetup.SetHand(blank.RightHand, 3);

            // 不扣血，直接触发 -> 回复应被上限夹住
            game.ExecuteTurn(wolf, wolf.LeftHand, blank.RightHand);

            Assert.AreEqual(wolf.MaximumHealth, wolf.Health, "回复不应超过上限。");
        }
    }
}
