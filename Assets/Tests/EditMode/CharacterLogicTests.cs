using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Assistant;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 三个角色的技能行为：咒术师无视护盾、野蛮人锁手、战士连锁破盾与盾牌修复，
    /// 以及伤害公式（攻击 +1 / 防御 +1）。
    /// 对应 RULES.md 第 6 节与第 8 节。
    ///
    /// 约定：用手值摆局面必须在 <see cref="GameManager"/> 开始跑之前完成
    /// （见 <see cref="TestSetup"/>）。
    /// </summary>
    [TestFixture]
    public class CharacterLogicTests
    {
        private ScriptedChoiceProvider _choiceA;
        private ScriptedChoiceProvider _choiceB;

        [SetUp]
        public void SetUp()
        {
            _choiceA = new ScriptedChoiceProvider();
            _choiceB = new ScriptedChoiceProvider();
        }

        /// <summary>造一局 A / B 各自角色的对局。</summary>
        private GameManager Match(CharacterDefinition a, CharacterDefinition b)
            => new GameManager(
                CharacterFactory.CreatePlayer("A", a),
                CharacterFactory.CreatePlayer("B", b),
                _choiceA, _choiceB);

        // ============================================================
        // 咒术师
        // ============================================================

        [Test]
        public void Curseweaver_attack_ignores_shield()
        {
            // 白板 B 有盾，咒术师 A 去打它
            GameManager game = Match(CharacterDefinition.Curseweaver, CharacterDefinition.Blank);
            PlayerState curseweaver = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(blank.LeftHand, 4);
            game.ExecuteTurn(blank, blank.LeftHand, curseweaver.RightHand);   // B.L: 4+1 = 5 -> 盾
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State, "前置条件：B 应有盾。");

            TestSetup.SetHand(curseweaver.LeftHand, 3);                       // 3+5 = 8 -> 攻击 8
            TurnResult r = game.ExecuteTurn(curseweaver, curseweaver.LeftHand, blank.LeftHand);

            Assert.AreEqual(HandEffectType.Attack8, r.Effect);
            Assert.False(r.WasBlocked, "咒术师的攻击应无视护盾。");
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State, "护盾不应被消耗或破碎。");
            // 8 +（咒术师攻击 -1）= 7
            Assert.AreEqual(7, r.DamageDealt, "咒术师攻击 -1，8 点攻击应造成 7 点伤害。");
            Assert.AreEqual(13, blank.Health);
        }

        [Test]
        public void Curseweaver_ignores_positive_defense_bonus()
        {
            GameManager game = Match(CharacterDefinition.Curseweaver, CharacterDefinition.Barbarian);
            PlayerState curseweaver = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            TestSetup.SetHand(curseweaver.LeftHand, 3);                       // 3+1 = 4 -> 攻击 4
            TurnResult r = game.ExecuteTurn(curseweaver, curseweaver.LeftHand, barbarian.LeftHand);

            Assert.AreEqual(HandEffectType.Attack4, r.Effect);
            // 4（原始）+ (-1)（咒术师攻击）- 0（无视野蛮人 +1 防御）= 3
            Assert.AreEqual(3, r.DamageDealt, "咒术师应无视野蛮人的 +1 防御加成。");
            Assert.AreEqual(17, barbarian.Health);
        }

        // ============================================================
        // 伤害公式
        // ============================================================

        [Test]
        public void Fighter_attack_bonus_adds_one_damage()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetHand(fighter.LeftHand, 3);                           // 3+1 = 4
            TurnResult r = game.ExecuteTurn(fighter, fighter.LeftHand, blank.LeftHand);

            Assert.AreEqual(HandEffectType.Attack4, r.Effect);
            Assert.AreEqual(5, r.DamageDealt, "战士攻击 +1：4 点攻击应造成 5 点伤害。");
            Assert.AreEqual(15, blank.Health);
        }

        [Test]
        public void Barbarian_defense_bonus_reduces_incoming_damage()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            TestSetup.SetHand(blank.LeftHand, 3);                             // 3+1 = 4
            TurnResult r = game.ExecuteTurn(blank, blank.LeftHand, barbarian.LeftHand);

            Assert.AreEqual(HandEffectType.Attack4, r.Effect);
            Assert.AreEqual(3, r.DamageDealt, "野蛮人防御 +1：4 点攻击只造成 3 点伤害。");
            Assert.AreEqual(17, barbarian.Health);
        }

        [Test]
        public void Blank_mirror_match_uses_raw_damage()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Blank);
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            TestSetup.SetHand(a.LeftHand, 3);                                 // 3+1 = 4
            TurnResult r = game.ExecuteTurn(a, a.LeftHand, b.LeftHand);

            Assert.AreEqual(4, r.DamageDealt);
            Assert.AreEqual(16, b.Health);
        }

        [Test]
        public void Damage_never_goes_negative()
        {
            // 极端情况：守方防御加成远高于伤害
            var tanky = new CharacterDefinition("tank", "重装", SpecialLogicType.None, defenseBonus: 99);
            GameManager game = Match(CharacterDefinition.Blank, tanky);

            TestSetup.SetHand(game.PlayerA.LeftHand, 3);                      // 3+1 = 4
            TurnResult r = game.ExecuteTurn(game.PlayerA, game.PlayerA.LeftHand, game.PlayerB.LeftHand);

            Assert.AreEqual(0, r.DamageDealt, "伤害应有 0 保底。");
            Assert.AreEqual(20, game.PlayerB.Health);
        }

        // ============================================================
        // 野蛮人 · 锁手
        // ============================================================

        [Test]
        public void Barbarian_lock_prevents_opponent_from_using_that_hand_as_source()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            TestSetup.SetHand(barbarian.LeftHand, 4);
            _choiceB.LockHand(blank.LeftHand);
            game.ExecuteTurn(barbarian, barbarian.LeftHand, blank.RightHand);  // 4+1 = 5 -> 盾 + 锁手

            Assert.True(blank.LeftHand.IsLocked, "野蛮人合盾时应锁住对手的手。");
            Assert.False(blank.RightHand.IsLocked, "只应锁住一只手。");

            Assert.Throws<System.InvalidOperationException>(
                () => game.ExecuteTurn(blank, blank.LeftHand, barbarian.RightHand),
                "被锁的手不能作为运算源。");
        }

        [Test]
        public void Barbarian_lock_excludes_the_hand_from_legal_moves()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;

            TestSetup.SetHand(game.PlayerB.LeftHand, 4);
            _choiceB.LockHand(blank.LeftHand);
            game.ExecuteTurn(game.PlayerB, game.PlayerB.LeftHand, blank.RightHand);

            var moves = game.GetLegalMoves(blank);
            Assert.AreEqual(2, moves.Count, "被锁一只手后只剩 2 步合法运算。");
            for (int i = 0; i < moves.Count; i++)
                Assert.AreSame(blank.RightHand, moves[i].ActedHand);
        }

        [Test]
        public void Barbarian_lock_is_released_when_the_shield_breaks()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            // 局面：A.L 1、A.R 1、B.L 4、B.R 1
            // （B 用 4 打 A 的 1，才能得到 4+1 = 5 合出盾）
            TestSetup.SetAllHands(blank, barbarian, 1);
            TestSetup.SetHand(barbarian.LeftHand, 4);

            _choiceB.LockHand(blank.LeftHand);
            // 回合 1（B）：B.L 4+1 = 5 -> 合盾 + 锁住 A 的左手
            game.ExecuteTurn(barbarian, barbarian.LeftHand, blank.RightHand);
            Assert.True(blank.LeftHand.IsLocked, "前置条件：A 左手应被锁。");
            Assert.AreEqual(ShieldState.Formed, barbarian.LeftHand.State);
            TestSetup.RequireHand(barbarian.LeftHand, 5, "B 左手");

            // 回合 2（A）：左手被锁，只能用右手 1 打 B 左手那面盾（值为 5）-> 1+5 = 6
            TurnResult r = game.ExecuteTurn(blank, blank.RightHand, barbarian.LeftHand);

            Assert.AreEqual(HandEffectType.Attack6, r.Effect, "1 + 5 = 6");
            Assert.True(r.WasBlocked, "野蛮人的盾应挡住这次攻击。");
            Assert.AreEqual(ShieldState.Broken, barbarian.LeftHand.State);
            Assert.False(blank.LeftHand.IsLocked, "盾碎后锁手应解除。");
        }

        [Test]
        public void Barbarian_does_not_lock_when_already_controlling_one_hand()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            // 局面：B 双手都是 4，可以连续两回合各合一面盾
            TestSetup.SetHand(barbarian.LeftHand, 4);
            TestSetup.SetHand(barbarian.RightHand, 4);

            // 回合 1（B）：B.L 4+1 = 5 -> 合盾 + 锁 A 左手
            _choiceB.LockHand(blank.LeftHand);
            game.ExecuteTurn(barbarian, barbarian.LeftHand, blank.RightHand);
            Assert.True(blank.LeftHand.IsLocked, "第一次合盾应锁住 A 的左手。");

            // 回合 2（A）：让回合（这会改变 A.R 的值）
            game.ExecuteTurn(blank, blank.RightHand, barbarian.RightHand);

            // 回合 3（B）：用右手合盾。此时己方左手（5）已在控制中，本次不应再锁。
            // 先把 A 的手归位，保证 B.R 4 + A.R 1 = 5。
            TestSetup.SetHand(blank.LeftHand, 1);
            TestSetup.SetHand(blank.RightHand, 1);
            TestSetup.SetHand(barbarian.LeftHand, 5);
            barbarian.LeftHand.SetShieldState(ShieldState.Formed);   // 左手仍是那面控制中的盾
            TestSetup.SetHand(barbarian.RightHand, 4);

            _choiceB.LockHand(blank.RightHand);                 // 脚本虽给了目标
            game.ExecuteTurn(barbarian, barbarian.RightHand, blank.RightHand);

            Assert.AreEqual(ShieldState.Formed, barbarian.RightHand.State, "合盾本身仍应生效。");
            Assert.IsFalse(blank.RightHand.IsLocked, "同时最多只能控制对手一只手。");
            Assert.True(blank.LeftHand.IsLocked, "原有的锁应保持。");
        }

        [Test]
        public void Barbarian_can_decline_the_lock()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;

            TestSetup.SetHand(game.PlayerB.LeftHand, 4);
            _choiceB.DeclineLock();
            game.ExecuteTurn(game.PlayerB, game.PlayerB.LeftHand, blank.RightHand);

            Assert.False(blank.LeftHand.IsLocked);
            Assert.False(blank.RightHand.IsLocked);
            Assert.AreEqual(ShieldState.Formed, game.PlayerB.LeftHand.State, "合盾本身仍应生效。");
        }

        [Test]
        public void Barbarian_lock_provider_may_decline_by_returning_an_out_of_range_hand()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;

            TestSetup.SetHand(game.PlayerB.LeftHand, 4);
            _choiceB.LockHand(new Hand(1));                       // 不在候选列表里的手
            game.ExecuteTurn(game.PlayerB, game.PlayerB.LeftHand, blank.RightHand);

            Assert.False(blank.LeftHand.IsLocked, "非候选目标不应被锁。");
            Assert.False(blank.RightHand.IsLocked);
        }

        // ============================================================
        // 战士 · 连锁破盾
        // ============================================================

        [Test]
        public void Fighter_chain_break_destroys_a_chosen_shield()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 开局把 4 只手全部归位，避免依赖默认值
            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(blank.LeftHand, 4);

            // 回合 1（A）：A.L 4+1 = 5 -> 合盾
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State, "A 左手应成盾。");
            TestSetup.RequireHand(fighter.LeftHand, 5, "A 左手");

            // 回合 2（B）：B.R 1+1 = 2 -> 无效果（只是让回合）
            TurnResult pass = game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);
            Assert.AreEqual(HandEffectType.None, pass.Effect);

            // 回合 3（B）：B.L 4+1 = 5 -> 合盾
            game.ExecuteTurn(blank, blank.LeftHand, fighter.RightHand);
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State, "B 左手应成盾。");
            TestSetup.RequireHand(blank.LeftHand, 5, "B 左手");

            // 回合 4（A）：战士用自己那面盾发起运算（5+5=10 -> 1）
            //   -> 运算前先破自己的盾 -> 触发连锁破盾 -> 击碎 B 的盾
            //
            // 注意：连锁破盾的答案现在是**运算前收集**的（与野蛮人锁手同一模式），
            // 所以要在 ExecuteTurn 之前写进 sink。
            _choiceA.SetChoice(HandEffect.FighterChainBreak, blank.LeftHand);
            TurnResult r = game.ExecuteTurn(fighter, fighter.LeftHand, blank.LeftHand);

            Assert.True(r.ShieldBrokenBeforeOp, "作为运算源的盾应先碎。");
            Assert.AreEqual(1, r.ResultValue, "5 + 5 = 10 -> 1");
            Assert.AreEqual(ShieldState.None, fighter.LeftHand.State,
                "5+5=10 -> 1：结果不是 5，作为运算源碎掉的盾不保留标记。");
            Assert.AreEqual(ShieldState.Broken, blank.LeftHand.State, "连锁破盾应击碎选中的目标盾。");
        }

        [Test]
        public void Fighter_can_decline_the_chain_break()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(blank.LeftHand, 4);

            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);   // A.L -> 5 盾
            game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);    // 让回合
            game.ExecuteTurn(blank, blank.LeftHand, fighter.RightHand);     // B.L -> 5 盾
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State);

            _choiceA.DeclineChainBreak();
            // 连锁破盾的答案在运算前收集：DeclineChainBreak 已登记"放弃"
            _choiceA.SetChoice(HandEffect.FighterChainBreak, null);
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.LeftHand);

            Assert.AreEqual(ShieldState.None, fighter.LeftHand.State,
                "5+1=6：结果不是 5，作为运算源碎掉的盾不保留标记。");
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State, "放弃发动时目标盾应完好。");
        }

        [Test]
        public void Fighter_chain_break_triggers_when_attacked()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Fighter);
            PlayerState blank = game.PlayerA;
            PlayerState fighter = game.PlayerB;

            // B（战士）合盾
            TestSetup.SetHand(fighter.LeftHand, 4);
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);        // B.L: 4+1 = 5 盾

            // A 也合一面盾，作为连锁破盾的目标
            game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);         // 让回合
            TestSetup.SetHand(blank.RightHand, 4);
            game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);         // A.R: 4+1 = 5 盾
            Assert.AreEqual(ShieldState.Formed, blank.RightHand.State);

            // A 攻击打碎 B 的盾 -> 触发 B 的连锁破盾 -> 反手击碎 A 的盾
            //
            // 战士 B 是**防守方**，但连锁破盾由他自己决定 ——
            // 抉择在运算前收集（收集阶段会问双方），答案写进 B 的 sink。
            _choiceB.SetChoice(HandEffect.FighterChainBreak, blank.RightHand);
            TestSetup.SetHand(blank.LeftHand, 3);                                // 3+5 = 8
            TurnResult r = game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);

            Assert.True(r.WasBlocked, "B 的盾应挡住这次攻击。");
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State);
            Assert.AreEqual(ShieldState.Broken, blank.RightHand.State,
                "连锁破盾击碎的是 A 的右手盾；A 是白板不会修复它，碎盾保持（手值仍是 5）。");
        }

        [Test]
        public void Fighter_chain_break_does_not_recurse_forever()
        {
            // 双方都是战士且都有盾：破盾会互相触发，必须有防递归保护。
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Fighter);
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            TestSetup.SetAllHands(a, b, 1);
            TestSetup.SetHand(a.LeftHand, 4);
            TestSetup.SetHand(b.LeftHand, 4);

            game.ExecuteTurn(a, a.LeftHand, b.RightHand);                         // A.L -> 5 盾
            game.ExecuteTurn(b, b.LeftHand, a.RightHand);                         // B.L -> 5 盾
            Assert.AreEqual(ShieldState.Formed, a.LeftHand.State);
            Assert.AreEqual(ShieldState.Formed, b.LeftHand.State);

            // 两个抉择器都选择"破对手的盾"，每层都会继续连锁
            _choiceA.SetChoice(HandEffect.FighterChainBreak, b.LeftHand);
            _choiceB.SetChoice(HandEffect.FighterChainBreak, a.LeftHand);

            // 只要不抛栈溢出/死循环，本测试即通过
            game.ExecuteTurn(a, a.LeftHand, b.LeftHand);

            // A 的盾作为运算源在运算前碎掉，结果是 1（5+5=10 -> 1，不是 5）
            //   -> 这只手已经是普通手牌，不保留碎盾标记。
            Assert.AreEqual(ShieldState.None, a.LeftHand.State,
                $"实际 aL={a.LeftHand.Value}/{a.LeftHand.State}");

            // B 的盾是在 A 的回合里被连锁击碎的，而 B 不是本回合的行动方，
            // 所以按规则碎盾标记**保留**到 B 自己行动时（给战士留修复窗口）。
            // B 的盾值仍是 5，标签与数值一致。
            Assert.AreEqual(ShieldState.Broken, b.LeftHand.State,
                $"实际 bL={b.LeftHand.Value}/{b.LeftHand.State}");
            Assert.AreEqual(5, b.LeftHand.Value, "碎盾的手值应仍是 5（与'碎盾'标签一致）。");
        }

        [Test]
        public void Fighter_chain_break_prompts_only_when_other_shields_exist()
        {
            // 玩家要求："破碎后先检查场上是否有其他盾牌，若没有则不需要弹出选择"。
            //
            // 直接观察**是否真的生成了抉择**（而不是只看结算结果）。
            var fighter = CharacterFactory.CreatePlayer("战士", CharacterDefinition.Fighter);
            var blank = CharacterFactory.CreatePlayer("白板", CharacterDefinition.Blank);
            var sinkW = new ScriptedChoiceProvider();
            var sinkB = new ScriptedChoiceProvider();
            var game = new GameManager(fighter, blank, sinkW, sinkB);

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);

            // 让战士合出左手盾（4+1=5）
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);

            // ① 场上除了这面盾之外**没有**其他成型盾 -> 不该生成抉择
            var context = new PreTurnChoiceContext(fighter, blank,
                                                   fighter.LeftHand, blank.LeftHand);
            var requests = new List<ChoiceRequest>();
            (fighter.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            Assert.AreEqual(0, requests.Count,
                $"没有其他完好盾时不该弹选择。实际生成了 {requests.Count} 个。");

            // ② 让对手也合一面盾 -> 这时才该弹
            TestSetup.SetHand(blank.LeftHand, 4);
            game.ExecuteTurn(blank, blank.LeftHand, fighter.RightHand);
            Assert.AreEqual(ShieldState.Formed, blank.LeftHand.State, "对手左手应成盾。");

            var context2 = new PreTurnChoiceContext(fighter, blank,
                                                    fighter.LeftHand, blank.LeftHand);
            var requests2 = new List<ChoiceRequest>();
            (fighter.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context2, requests2);

            Assert.AreEqual(1, requests2.Count,
                $"场上有其他完好盾时**应**弹选择。实际 {requests2.Count} 个。");
            Assert.AreEqual(HandEffect.FighterChainBreak, requests2[0].Effect);
            Assert.True(requests2[0].Candidates.Count > 0, "候选应包含那面完好的盾。");
            Assert.True(requests2[0].AllowDecline, "应允许放弃发动。");
        }

        [Test]
        public void Fighter_chain_break_with_no_targets_is_a_no_op()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 场上只有战士自己这一面盾
            TestSetup.SetHand(fighter.LeftHand, 4);
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);        // A.L -> 5 盾

            // 用这面盾发起运算：它自己碎了，场上再无其他成型盾 -> 无候选
            _choiceA.ChainBreakAt(0);                                            // 即使脚本想发动也没有目标
            TurnResult r = game.ExecuteTurn(fighter, fighter.LeftHand, blank.LeftHand);

            Assert.True(r.ShieldBrokenBeforeOp);
            // 运算结果是 1（5+5=10 -> 1），不是 5，所以这只手就是普通手牌
            Assert.AreEqual(ShieldState.None, fighter.LeftHand.State,
                "5+5=10 -> 1：结果不是 5，该手就是普通手牌，不保留碎盾标记。");
        }

        // ============================================================
        // 战士 · 盾牌修复
        // ============================================================

        [Test]
        public void Fighter_repairs_a_broken_shield_when_forming_a_new_one()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            // 1) 战士左手合盾
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);        // A.L -> 5 盾
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);

            // 2) 白板打碎它
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);           // 3+5 = 8 -> 攻击
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State);

            // 3) 战士用右手合出新盾 -> 自动修复左手的碎盾
            game.ExecuteTurn(fighter, fighter.RightHand, blank.RightHand);       // 4+1 = 5 -> 盾

            Assert.AreEqual(ShieldState.Formed, fighter.RightHand.State, "右手应合出新盾。");
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State, "左手的碎盾应被修复。");
            Assert.True(fighter.HasDoubleShield);
        }

        [Test]
        public void Repair_does_not_change_hand_value()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            // 回合 1（战士）：左手 4+1 = 5 -> 合盾
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);

            // 回合 2（白板）：打碎它
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State);

            // 回合 3（战士）：右手合出新盾 -> 顺手修复左手的碎盾
            game.ExecuteTurn(fighter, fighter.RightHand, blank.RightHand);

            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State, "碎盾应被修复。");
            Assert.AreEqual(5, fighter.LeftHand.Value, "修复只改护盾状态，不应改手值。");
        }

        [Test]
        public void Fighter_repair_does_nothing_when_other_hand_is_normal()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(fighter.RightHand, 4);

            // 回合 1（A）：A.L 4+1 = 5 -> 盾
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);

            // 回合 2（B）：让回合。注意 B 的手值会被改变，所以下一回合前要重新摆好局面。
            game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);

            // 回合 3（A）：重新归位，保证 A.R 4 + B.R 1 = 5 -> 新盾
            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);          // 左手保持成型的盾
            fighter.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(fighter.RightHand, 4);

            game.ExecuteTurn(fighter, fighter.RightHand, blank.RightHand);

            Assert.AreEqual(ShieldState.Formed, fighter.RightHand.State, "右手应合出新盾。");
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State, "左手应保持成型。");
            Assert.AreEqual(5, fighter.LeftHand.Value, "修复逻辑不应改变手值。");
        }

        [Test]
        public void Non_warrior_gets_no_repair()
        {
            // 白板没有修复能力：被对手打碎的盾不会因为自己后来合出新盾而复原。
            //
            // 回合推演（按实际交替顺序）：
            //   回合1 A：A.L 4+1 = 5 -> A 合盾
            //   回合2 B：B.L 3+5 = 8 -> 打碎 A 的盾（A.L 变 Broken）
            //   回合3 B：B.R 1+4 = 5 -> B 也合盾（A.L 仍 Broken，对手回合不清我的）
            //   回合4 A：A.R 4+1 = 5 -> A 合出第二面盾；白板不修复，
            //                        且 A.L 的碎盾在 A 本回合结束时被清理
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Blank);
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            TestSetup.SetAllHands(a, b, 1);
            TestSetup.SetHand(a.LeftHand, 4);
            TestSetup.SetHand(a.RightHand, 4);
            TestSetup.SetHand(b.LeftHand, 3);

            game.ExecuteTurn(a, a.LeftHand, b.RightHand);                        // 回合1
            Assert.AreEqual(ShieldState.Formed, a.LeftHand.State);

            game.ExecuteTurn(b, b.LeftHand, a.LeftHand);                         // 回合2 打碎
            Assert.AreEqual(ShieldState.Broken, a.LeftHand.State);

            game.ExecuteTurn(b, b.RightHand, a.RightHand);                       // 回合3
            Assert.AreEqual(ShieldState.Broken, a.LeftHand.State, "对手回合不清我的碎盾。");

            // 回合3 把 b.R 改成了 5，这里摆回 1，保证回合4 的 4+1 = 5 成立
            TestSetup.SetHand(b.RightHand, 1);
            game.ExecuteTurn(a, a.RightHand, b.RightHand);                       // 回合4 A 合新盾
            Assert.AreEqual(ShieldState.Formed, a.RightHand.State, "A 应合出新盾。");
            Assert.AreEqual(ShieldState.Broken, a.LeftHand.State,
                "白板没有修复能力，碎盾应保持 Broken（手值仍是 5）。");
        }

        // ============================================================
        // 碎盾（Broken）的生命周期 —— 曾经的严重 bug
        //
        // Broken 的语义是"**本回合内刚碎**"，存在的唯一目的是让战士能在同一回合内
        // 修复它。曾经它被跨回合保留，导致：
        //   · 界面上一直显示"碎盾展开"标签（非 5 的手也挂着碎盾）；
        //   · 战士过几回合后随手合个新盾，就把远古的碎盾一起修回来，凭空多一面盾。
        // ============================================================

        [Test]
        public void Broken_shield_persists_through_the_opponents_turn()
        {
            // Broken 只在**轮到持有者自己行动**时才被清除，
            // 这样战士在自己下一次行动时还有机会修复它。
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Fighter);
            PlayerState blank = game.PlayerA;
            PlayerState fighter = game.PlayerB;

            TestSetup.SetAllHands(blank, fighter, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            // 回合 1（战士）：合盾
            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);

            // 回合 2（白板）：打碎它
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "刚被打碎的当回合应标记为 Broken。");

            // 回合 3（白板再次行动）：碎盾标记应保持 —— 还没轮到战士自己
            game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "对手行动时不应清掉我的碎盾（战士还需要这次机会来修复）。");
        }

        [Test]
        public void Broken_shield_persists_indefinitely_while_its_value_stays_five()
        {
            // 按需求：碎盾只要**手值仍然是 5**，就一直处于可修复状态，
            // 无论中间过了多少回合、谁行动过。
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Fighter);
            PlayerState blank = game.PlayerA;
            PlayerState fighter = game.PlayerB;

            TestSetup.SetAllHands(blank, fighter, 1);
            TestSetup.SetHand(fighter.LeftHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            game.ExecuteTurn(fighter, fighter.LeftHand, blank.RightHand);   // 合盾
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);      // 打碎
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State);

            // 空转很多回合（双方交替行动）
            for (int i = 0; i < 8; i++)
            {
                PlayerState actor = game.CurrentPlayer;
                PlayerState other = game.GetOpponent(actor);
                game.ExecuteTurn(actor, actor.RightHand, other.RightHand);
            }

            Assert.AreEqual(5, fighter.LeftHand.Value, "手值应仍是 5。");
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "手值仍是 5 时，碎盾状态应一直保留（可被修复）。");
        }

        [Test]
        public void Broken_shield_on_a_non_five_hand_does_not_persist()
        {
            // 用户报告的 bug 之一：值为 8 的手也挂着"碎盾展开"标签。
            //
            // 原因是"运算前破盾"之后无条件把状态设回 Broken —— 即使运算已经把这只手
            // 变成了别的数字。修法：只有结果仍然是 5 时才保留 Broken。
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Blank);
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            TestSetup.SetAllHands(a, b, 1);
            TestSetup.SetHand(a.LeftHand, 5);
            a.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(b.LeftHand, 3);

            // a 用自己成型的左手发起运算：5 + 3 = 8 -> 运算前破盾，结果不是 5
            TurnResult r = game.ExecuteTurn(a, a.LeftHand, b.LeftHand);

            Assert.True(r.ShieldBrokenBeforeOp, "作为运算源的盾应先碎。");
            Assert.AreEqual(8, r.ResultValue, "5 + 3 = 8");
            Assert.AreEqual(ShieldState.None, a.LeftHand.State,
                "结果不是 5 的手就是普通手牌，**不应**挂着'碎盾'标记。");
            Assert.False(a.LeftHand.IsShield);
            Assert.False(a.HasAnyShield, "碎裂的盾不算有盾。");
        }

        [Test]
        public void Broken_shield_keeps_its_mark_only_while_it_is_still_five()
        {
            // 反面：如果运算结果**仍然是 5**，那这面盾依然是"盾"，只是碎了，
            // 因此保留 Broken（可以被战士修复）。
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);
            fighter.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(blank.LeftHand, 5);

            // 5 + 5 = 10 -> 1…… 结果不是 5。改用能得回 5 的组合：
            // 5 + 0 不存在；这里直接验证"结果是 1 时不留标记"。
            TurnResult r = game.ExecuteTurn(fighter, fighter.LeftHand, blank.LeftHand);

            Assert.AreEqual(1, r.ResultValue, "5 + 5 = 10 -> 1");
            Assert.AreEqual(ShieldState.None, fighter.LeftHand.State,
                "结果不是 5，不应保留碎盾标记。");
        }

        [Test]
        public void Fighter_can_repair_a_shield_broken_several_turns_ago()
        {
            // 按需求：碎盾只要手值仍是 5，就**一直**可被修复，不限回合数。
            // 这里构造"碎盾放了几回合，战士才用另一只手合出新盾"的情形。
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);
            fighter.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            // 回合 1（白板先手）：打碎战士的左盾
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "A.L 5 + 3 = 8，盾被打碎。");
            Assert.AreEqual(5, fighter.LeftHand.Value, "碎盾手值仍是 5。");

            // 空转 6 个回合：碎盾应一直保留
            for (int i = 0; i < 6; i++)
            {
                PlayerState actor = game.CurrentPlayer;
                PlayerState other = game.GetOpponent(actor);
                game.ExecuteTurn(actor, actor.RightHand, other.RightHand);
            }

            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "手值仍是 5 时，碎盾状态应一直保留。");

            // 战士用手合出新盾 -> 修复那面早就碎掉的左盾
            TestSetup.SetHand(fighter.LeftHand, 5);          // 保证左手仍是 5（碎盾）
            fighter.LeftHand.SetShieldState(ShieldState.Broken);
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.RightHand, 1);

            if (!ReferenceEquals(game.CurrentPlayer, fighter))
                game.ExecuteTurn(blank, blank.RightHand, fighter.RightHand);

            game.ExecuteTurn(fighter, fighter.RightHand, blank.RightHand);

            Assert.AreEqual(ShieldState.Formed, fighter.RightHand.State, "右手应合出新盾。");
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State,
                "很久以前碎掉的盾，只要手值还是 5，就应被修复。");
        }

        [Test]
        public void Broken_shield_survives_many_turns_and_stays_repairable()
        {
            // 碎盾的可修复性**只**取决于"手值是否仍是 5"，与过了多少回合无关。
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);
            fighter.LeftHand.SetShieldState(ShieldState.Formed);   // 左手已有盾
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            // 回合 1（白板）：打碎战士的左盾
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "A.L 5 + 3 = 8，盾被打碎。");

            // 空转 6 个回合：碎盾应一直保留（手值仍是 5）
            for (int i = 0; i < 6; i++)
            {
                PlayerState actor = game.CurrentPlayer;
                PlayerState other = game.GetOpponent(actor);
                game.ExecuteTurn(actor, actor.RightHand, other.RightHand);
            }

            Assert.AreEqual(5, fighter.LeftHand.Value, "碎盾手值应仍是 5。");
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "手值仍是 5 时，碎盾状态应一直保留。");
        }

        [Test]
        public void Fighter_cannot_repair_the_shield_he_broke_himself_this_turn()
        {
            // 记录一个**规则边界**（不是 bug，是机制决定）：
            //
            // 战士用自己那面成型的盾发起运算时，该盾运算前先碎。如果这一击又让**另一只手**
            // 变成了 5…… 不可能，因为一回合只有一次运算。
            // 所以"自己弄碎的盾"当回合无法修复，它会在**下一次轮到自己**时被清理掉。
            //
            // 结论：能被修复的只有"**被对手打碎、且还没轮到自己行动过**"的盾。
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);
            fighter.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(fighter.RightHand, 5);   // 另一只手是 5，用来验证不会被"修复"

            // 回合 1（战士）：用左手的盾发起运算 -> 运算前碎 -> 结果 5+1 = 6
            TurnResult r = game.ExecuteTurn(fighter, fighter.LeftHand, blank.LeftHand);

            Assert.True(r.ShieldBrokenBeforeOp);
            Assert.AreEqual(6, r.ResultValue, "5 + 1 = 6");
            Assert.AreEqual(ShieldState.None, fighter.LeftHand.State,
                "结果不是 5，所以这只手就是普通手牌，不保留碎盾标记。");
            Assert.False(fighter.HasAnyShield, "碎裂的盾不算有盾。");
        }

        [Test]
        public void Fighter_can_repair_a_shield_broken_last_turn()
        {
            // 修复机制本身必须仍然可用：战士的盾被对手打碎后，
            // 碎盾会保留到战士自己下一次行动，此时用另一只手合出新盾即可修复。
            //
            // 注意第一次合盾必须用 TestSetup 直接摆成 5/Formed —— 若用一次普通运算合盾，
            // 那只手会停在 5，下一回合再被用来合盾时就会先"运算前破盾"，局面就变了。
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);
            fighter.LeftHand.SetShieldState(ShieldState.Formed);   // 左手已有盾
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            // 回合 1（白板先手）：打碎战士的左盾
            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);
            Assert.AreEqual(ShieldState.Broken, fighter.LeftHand.State,
                "前置条件：左盾应被打碎。");

            // 回合 2（战士）：右手 4 + 1 = 5 -> 合出新盾 -> 修复左手的碎盾
            game.ExecuteTurn(fighter, fighter.RightHand, blank.RightHand);

            Assert.AreEqual(ShieldState.Formed, fighter.RightHand.State, "右手应合出新盾。");
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State,
                "上一回合碎掉的左盾应被修复。");
            Assert.True(fighter.HasDoubleShield, "应变成双盾。");
        }

        [Test]
        public void Fighter_repair_does_not_change_hand_value()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Blank);
            PlayerState fighter = game.PlayerA;
            PlayerState blank = game.PlayerB;

            TestSetup.SetAllHands(fighter, blank, 1);
            TestSetup.SetHand(fighter.LeftHand, 5);
            fighter.LeftHand.SetShieldState(ShieldState.Formed);
            TestSetup.SetHand(fighter.RightHand, 4);
            TestSetup.SetHand(blank.LeftHand, 3);

            game.ExecuteTurn(blank, blank.LeftHand, fighter.LeftHand);     // 打碎
            game.ExecuteTurn(fighter, fighter.RightHand, blank.RightHand);  // 右手合盾 -> 修复

            Assert.AreEqual(5, fighter.LeftHand.Value, "修复只改护盾状态，不应改手值。");
            Assert.AreEqual(ShieldState.Formed, fighter.LeftHand.State);
        }

        // ============================================================
        // 复位（P0 修复项）
        // ============================================================

        [Test]
        public void ResetMatch_clears_barbarian_locks()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Barbarian);
            PlayerState blank = game.PlayerA;
            PlayerState barbarian = game.PlayerB;

            TestSetup.SetHand(barbarian.LeftHand, 4);
            _choiceB.LockHand(blank.LeftHand);
            game.ExecuteTurn(barbarian, barbarian.LeftHand, blank.RightHand);    // 盾 + 锁手
            Assert.True(blank.LeftHand.IsLocked, "前置条件：对手的手应被锁。");

            game.ResetMatch();

            Assert.False(blank.LeftHand.IsLocked, "重开一局必须解除锁手。");
            Assert.False(blank.RightHand.IsLocked);
            Assert.AreEqual(4, game.GetLegalMoves(blank).Count, "锁解除后应恢复 4 步合法运算。");
        }

        [Test]
        public void ResetMatch_restores_health_hands_and_flags()
        {
            GameManager game = Match(CharacterDefinition.Fighter, CharacterDefinition.Barbarian);
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            TestSetup.SetHand(a.LeftHand, 4);
            TestSetup.SetHand(b.LeftHand, 3);

            game.ExecuteTurn(a, a.LeftHand, b.RightHand);        // A.L -> 5 盾
            game.ExecuteTurn(b, b.LeftHand, a.LeftHand);         // 打碎 A 的盾
            a.TakeDamage(7);

            Assert.True(a.Health < 20);

            game.ResetMatch();

            Assert.AreEqual(20, a.Health);
            Assert.AreEqual(20, b.Health);
            Assert.AreEqual(1, a.LeftHand.Value);
            Assert.AreEqual(1, a.RightHand.Value);
            Assert.AreEqual(ShieldState.None, a.LeftHand.State, "碎盾状态也应被清除。");
            Assert.AreEqual(ShieldState.None, b.LeftHand.State);
            Assert.False(a.LeftHand.IsLocked);
            Assert.False(a.RightHand.IsLocked);
            Assert.False(game.IsGameOver);
            Assert.Null(game.Winner);
            Assert.AreEqual(0, game.TurnCount);
            Assert.AreSame(a, game.CurrentPlayer);
        }

        [Test]
        public void ResetMatch_makes_the_game_playable_again_after_game_over()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Blank);
            PlayerState a = game.PlayerA;
            PlayerState b = game.PlayerB;

            TestSetup.SetHand(a.LeftHand, 3);                     // 3+1 = 4
            TestSetup.SetHealth(b, 4);
            game.ExecuteTurn(a, a.LeftHand, b.LeftHand);          // 击杀 B
            Assert.True(game.IsGameOver);
            Assert.AreSame(a, game.Winner);

            game.ResetMatch();

            Assert.False(game.IsGameOver);
            Assert.Null(game.Winner);
            Assert.AreEqual(20, b.Health);
            Assert.AreEqual(2, game.ExecuteTurn(a, a.LeftHand, b.LeftHand).ResultValue);
        }
    }
}
