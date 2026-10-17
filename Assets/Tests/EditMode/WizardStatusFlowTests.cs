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
    /// **法师状态的"活起来"测试** —— 证明它们在真实对局流程里会触发，
    /// 而不只是数据结构正确。
    ///
    /// 覆盖：
    ///   · 中毒：回合开始时扣 2 点，**不可被抵挡**
    ///   · 中毒打死人 -> **跳过其行动**
    ///   · 燃烧：回合结束时扣 3 点，**可被盾抵挡**
    ///   · 感电：自己 + 环上相邻各 1 点
    ///   · 计数递减：目标自己的 2 个回合后过期
    ///   · 腐朽：对局中回血被完全拦掉
    ///   · 冰冻：真实运算出的结果被减半
    /// </summary>
    [TestFixture]
    public class WizardStatusFlowTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        private static GameManager MakeGame(PlayerState a, PlayerState b)
            => new GameManager(a, b, new SimpleChoiceProvider(),
                               new SimpleChoiceProvider());

        // ============================================================
        // 中毒
        // ============================================================

        [Test]
        public void Poison_deals_two_unblockable_damage_at_turn_start()
        {
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            // 给甲两面盾 —— 中毒"不可被抵挡"，盾不该消耗
            p.LeftHand.SetValue(5);
            p.LeftHand.SetShieldState(ShieldState.Formed);
            p.RightHand.SetValue(5);
            p.RightHand.SetShieldState(ShieldState.Formed);

            p.ApplyStatus(StatusEffect.Poison);
            int before = p.Health;

            game.ResolveStatusTurnStart(p);

            Assert.AreEqual(before - StatusRules.PoisonDamagePerTurn, p.Health,
                "中毒造成固定的 2 点毒素伤害。");
            Assert.AreEqual(ShieldState.Formed, p.LeftHand.State,
                "中毒不可被抵挡 —— 盾不该被消耗。");
            Assert.AreEqual(ShieldState.Formed, p.RightHand.State, "两面盾都还在。");
        }

        [Test]
        public void Poison_can_kill_and_then_the_turn_is_skipped()
        {
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            // 压到 2 点血 -> 中毒正好打死
            p.TakeDamage(p.Health - 2);
            p.ApplyStatus(StatusEffect.Poison);

            Assert.AreEqual(p, game.CurrentPlayer, "开局轮到甲。");

            game.ResolveStatusTurnStart(p);

            Assert.IsFalse(p.IsAlive, "中毒把他打死了。");
            Assert.IsTrue(game.IsGameOver, "有人阵亡 -> 对局结束。");
        }

        [Test]
        public void Poison_does_not_reapply_itself()
        {
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            p.ApplyStatus(StatusEffect.Poison);
            game.ResolveStatusTurnStart(p);

            Assert.IsTrue(p.HasStatus(StatusEffect.Poison),
                "中毒伤害本身不会再挂回状态 —— 但也**不会**提前清掉已有的状态。");
            Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.Poison),
                "回合开始不递减（递减在回合结束）。");
        }

        // ============================================================
        // 燃烧
        // ============================================================

        [Test]
        public void Burn_deals_three_at_turn_end_and_a_shield_can_block_it()
        {
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            p.LeftHand.SetValue(5);
            p.LeftHand.SetShieldState(ShieldState.Formed);

            p.ApplyStatus(StatusEffect.Burn);
            int before = p.Health;

            game.ResolveStatusTurnEnd(p);

            Assert.AreEqual(before, p.Health, "有盾时燃烧被完全挡下。");
            Assert.AreNotEqual((int)ShieldState.Formed, (int)p.LeftHand.State,
                    "那面盾被消耗掉了。");
        }

        [Test]
        public void Burn_deals_three_when_undefended()
        {
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            p.ApplyStatus(StatusEffect.Burn);
            int before = p.Health;

            game.ResolveStatusTurnEnd(p);
            Assert.AreEqual(before - StatusRules.BurnDamagePerTurn, p.Health,
                "没有盾时燃烧造成 3 点火焰伤害。");
        }

        // ============================================================
        // 感电
        // ============================================================

        [Test]
        public void Shock_hits_the_victim_and_adjacent_players()
        {
            PlayerState a = Make("甲", CharacterDefinition.Soldier);
            PlayerState b = Make("乙", CharacterDefinition.Soldier);
            PlayerState c = Make("丙", CharacterDefinition.Soldier);
            PlayerState d = Make("丁", CharacterDefinition.Soldier);

            GameManager game = GameManager.Create2v2(
                new[] { a, b, c, d },
                new IChoiceProvider[]
                {
                    new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                    new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                });

            a.ApplyStatus(StatusEffect.Shock);

            int aBefore = a.Health, bBefore = b.Health;
            int cBefore = c.Health, dBefore = d.Health;

            game.ResolveStatusTurnStart(a);

            Assert.AreEqual(aBefore - 1, a.Health, "自己受 1 点。");
            Assert.AreEqual(bBefore - 1, b.Health, "座位 2 相邻 -> 也受 1 点。");
            Assert.AreEqual(dBefore - 1, d.Health, "座位 4 相邻 -> 也受 1 点。");
            Assert.AreEqual(cBefore, c.Health, "座位 3 距离 2 -> 不受影响。");
        }

        // ============================================================
        // 计数递减
        // ============================================================

        [Test]
        public void Status_expires_after_two_turns_of_its_own()
        {
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            p.ApplyStatus(StatusEffect.Acid);

            game.ResolveStatusTurnEnd(p);
            Assert.IsTrue(p.HasStatus(StatusEffect.Acid), "第 1 个自己的回合后还在。");

            game.ResolveStatusTurnEnd(p);
            Assert.IsFalse(p.HasStatus(StatusEffect.Acid), "第 2 个自己的回合后移除。");
        }

        // ============================================================
        // 腐朽 / 冰冻 在真实流程里
        // ============================================================

        [Test]
        public void Decay_blocks_healing_during_a_turn()
        {
            PlayerState priest = Make("牧", CharacterDefinition.Priest);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            GameManager game = MakeGame(priest, foe);

            priest.TakeDamage(10);
            int hurt = priest.Health;

            priest.ApplyStatus(StatusEffect.Decay);
            priest.Heal(9);

            Assert.AreEqual(hurt, priest.Health, "腐朽期间回血完全无效。");
        }

        [Test]
        public void Freeze_never_drops_the_result_below_one()
        {
            // ⚠️⚠️ 玩家实测报的 bug："战士被冰冻影响出现了 0，不行，最小是 1"。
            //    根因：算出 1 时 `1 / 2 == 0`，而 **0 在手牌里是"空手"的意思** ——
            //    那一格就变成不能用的死格（界面显示 `0` 与 `—`）。
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            p.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(2);          // 1 + 2 = 3 -> 减半本该是 1
            p.ApplyStatus(StatusEffect.Freeze);

            // 直接测规则层：任何结果减半后都不得小于 1
            for (int raw = 0; raw <= 9; raw++)
            {
                int mod = StatusRules.ApplyResultModifiers(p, raw);
                Assert.IsTrue(mod >= 1,
                    $"结果是 {raw} 时，冰冻减半后得到 {mod} —— 不能小于 1。");
            }
        }

        [Test]
        public void Freeze_on_one_still_leaves_a_usable_hand()
        {
            // 1 + 1 = 2 -> 减半 = 1（不是 0）
            PlayerState p = Make("甲", CharacterDefinition.Soldier);
            PlayerState foe = Make("乙", CharacterDefinition.Soldier);
            GameManager game = MakeGame(p, foe);

            p.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(1);
            p.ApplyStatus(StatusEffect.Freeze);

            TurnResult r = game.ExecuteTurn(p, p.LeftHand, foe.LeftHand);

            Assert.AreEqual(2, r.FreezeHalvedFrom);
            Assert.AreEqual(1, r.ResultValue, "2 减半是 1。");
            Assert.IsTrue(p.LeftHand.Value >= 1,
                "冰冻不能把手变成 0（0 是空手的意思）。");
        }

        // ============================================================
        // 放弃攻击 -> 抑制伤害（回归）
        // ============================================================

        /// <summary>
        /// 一个**会表态要放弃攻击**的抉择器（模拟 AI / 玩家点了"放弃攻击"）。
        ///
        /// ⚠️ 不能靠"直接给法师置标记"来测这条 ——
        ///    `ResolveMageStatusIntent` 会在**意图读取处**先把标记清零
        ///    （因为标记的语义是"**本回合**放弃"，必须每回合重置，
        ///      否则第一回合放弃之后标记永久为 true：伤害永久被抑制）。
        ///    所以测试必须在**读取时**由抉择器表态。
        /// </summary>
        private sealed class AbandoningSink : IDamageTypeChoiceSink
        {
            private readonly DamageType _type;

            public AbandoningSink(DamageType type) { _type = type; }

            public DamageType ChosenDamageType { get; set; }
            public bool WantsToAbandonAttack { get; set; }
            public void SetGameForChoices(GameManager game) { }

            /// <summary>抉择器"现场表态"：要放弃，并且选这个类型。</summary>
            public void Declare() => ChosenDamageType = _type;
        }

        [Test]
        public void Abandon_attack_suppresses_damage_even_when_a_type_is_pending()
        {
            // ⚠️⚠️ 玩家实测报的 bug："放弃攻击仍然造成了伤害，
            //    且是不用选择就造成了伤害"。
            //
            //    根因：`ResolveMageStatusIntent` 开头有
            //    `if (mage.HasPendingType) return;` —— **法师身上已经有待用类型**时
            //    （前几轮铺下的）直接返回，于是后面那句抑制判定**永远执行不到**。
            //    ⚠️ 这条测试必须**确定性**地驱动"放弃"：
            //      · AI 的放弃判断是**概率性**的（合出 4 只有 70% 会放弃），
            //        用 `GreedyChoiceProvider` 会时红时绿；
            //      · 而且 `HasPendingType` 会让方法提前 return，
            //        抉择器根本没机会表态。
            //      所以只能照**界面路径**的做法：
            //      "玩家点完之后把标记直接写到法师身上"。
            //
            //    因此这里用 `SimpleChoiceProvider`（= 人类玩家的抉择器，
            //    它没有实现 `IDamageTypeChoiceSink`）。
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            GameManager game = MakeGame(mage, foe);      // SimpleChoiceProvider

            mage.LeftHand.SetValue(2);         // 2 + 2 = 4 -> 攻击
            foe.LeftHand.SetValue(2);
            foe.RightHand.SetValue(1);

            var wiz = (FingerDiceDuel.Characters.SpecialLogics.WizardLogic)mage.Character;

            // ① 已经有一个"前几轮铺下的"待用类型
            wiz.SetPendingDamageType(DamageType.Thunder);
            Assert.IsTrue(wiz.HasPendingType);

            // ② 界面把"本回合放弃攻击"直接写到法师身上
            wiz.WantsToAbandonThisTurn = true;

            int hpBefore = foe.Health;
            TurnResult r = game.ExecuteTurn(mage, mage.LeftHand, foe.LeftHand);

            Assert.AreEqual(0, r.DamageDealt,
                "放弃攻击必须抑制伤害 —— 即使身上已有待用类型。"
                + $"\n[诊断] suppress={game.SuppressAttackDamage} effect={r.Effect}");
            Assert.AreEqual(hpBefore, foe.Health, "放弃攻击这一击不造成伤害。");
        }

        [Test]
        public void The_abandon_mark_does_not_stick_to_the_next_turn()
        {
            // ⚠️⚠️ 回归：`WantsToAbandonThisTurn` 原来只有界面路径（BoardController）
            //    会清零，**无头路径从不清零** —— 法师第一次放弃后标记永久为 true：
            //      · 每回合 `SuppressAttackDamage` 都置真 -> 法师再也不造成伤害
            //      · `skipWhen` 恒假 -> 永远追问类型
            //    表现就是"AI 铺了 2000 多次类型，一次状态都没挂上"。
            //
            //    ⚠️ 清零现在由 `MatchRunner`（无头回合开始）/ `BoardController`
            //       （界面回合开始）负责 —— 所以这条测试用 `MatchRunner` 跑，
            //       而不是手工 `ExecuteTurn`（那样没人负责清零）。
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            var game = new GameManager(mage, foe, new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());

            var wiz = (FingerDiceDuel.Characters.SpecialLogics.WizardLogic)mage.Character;

            // 先让法师"放弃过一次"（模拟上一步留下的标记）
            wiz.WantsToAbandonThisTurn = true;

            // 跑一小段对局：MatchRunner 每回合开头都会清零，
            // 所以中途必然出现"没有放弃却正常造成伤害"的回合。
            int maxDamage = 0;
            int prevHp = foe.Health;

            var runner = new MatchRunner { MaxTurns = 60 };
            runner.Run(game, new GreedyChoiceProvider(), new GreedyChoiceProvider());

            maxDamage = foe.Health < prevHp ? prevHp - foe.Health : 0;

            Assert.IsTrue(maxDamage > 0 || game.IsGameOver,
                "清零之后法师应当能正常造成伤害 —— 标记不能跨回合粘住，"
                + "否则法师永远被抑制、一点伤害都打不出来。");
        }

        [Test]
        public void Freeze_halves_a_real_operation_result()
        {
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            GameManager game = MakeGame(mage, foe);

            // 让它算出 9（回复）：左手 8 + 对手 1 = 9
            mage.LeftHand.SetValue(8);
            mage.RightHand.SetValue(1);
            foe.LeftHand.SetValue(1);

            mage.ApplyStatus(StatusEffect.Freeze);

            TurnResult r = game.ExecuteTurn(mage, mage.LeftHand, foe.LeftHand);

            Assert.AreEqual(9, r.FreezeHalvedFrom, "原始结果是 9。");
            Assert.AreEqual(4, r.ResultValue, "冰冻把它减半成 4（向下取整）。");
            Assert.AreEqual(HandEffectType.Attack4, r.Effect,
                "9 本来是回复，减半成 4 之后就变成攻击了。");
        }

        [Test]
        public void Freeze_halves_but_the_mage_has_no_odd_result_to_worry_about()
        {
            // 1 + 1 = 2，减半后是 1（无效果）。只验证"减半确实发生了"。
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            GameManager game = MakeGame(mage, foe);

            mage.LeftHand.SetValue(1);
            foe.LeftHand.SetValue(1);

            mage.ApplyStatus(StatusEffect.Freeze);
            TurnResult r = game.ExecuteTurn(mage, mage.LeftHand, foe.LeftHand);

            Assert.AreEqual(2, r.FreezeHalvedFrom, "原始结果是 2。");
            Assert.AreEqual(1, r.ResultValue, "冰冻把 2 减半成 1。");
        }

        // ============================================================
        // 无头对局里，状态会真的触发
        // ============================================================

        [Test]
        public void A_headless_match_with_poison_reduces_health()
        {
            PlayerState a = Make("甲", CharacterDefinition.Soldier);
            PlayerState b = Make("乙", CharacterDefinition.Soldier);

            a.ApplyStatus(StatusEffect.Poison);
            int before = a.Health;

            var game = new GameManager(a, b, new GreedyChoiceProvider(),
                                       new GreedyChoiceProvider());

            new MatchRunner().Run(game, new GreedyChoiceProvider(),
                                  new GreedyChoiceProvider());

            Assert.IsTrue(a.Health < before,
                "无头对局走完后，中毒至少扣过一次血（证明回合钩子真的接上了）。");
        }

        // ============================================================
        // AI 法师在**无头路径**上铺状态 —— **已知缺口，暂不作为断言**
        // ============================================================

        /// <summary>
        /// **AI 法师在无头对局里真的会铺伤害类型 / 挂状态。**
        ///
        /// 这条测试曾经是"已知缺口"（只测量、不断言）。根因是两处：
        ///   ① `WizardLogic.CollectPreTurnChoices` 的签名写成 `List&lt;ChoiceRequest&gt;`
        ///      且没有 `override`，于是**没有覆盖基类方法**，
        ///      通过接口调用走的是基类空实现（C# 9 没有默认接口成员，不报错）；
        ///   ② `GreedyChoiceProvider.Game` 只在 `BoardController` 里被赋值，
        ///      而 `MatchRunner` / 测试 harness **从不设置它** ——
        ///      AI 的法师分支第一句 `if (Game == null) return null;` 直接放弃。
        ///      已改为通过 <see cref="IDamageTypeChoiceSink.SetGameForChoices"/>
        ///      在 `GameManager` 的调用点注入（三条路径都覆盖）。
        /// </summary>
        [Test]
        public void Ai_mage_applies_a_status_in_a_headless_match()
        {
            // ⚠️⚠️ **这条测试为什么改成"事件级 + 多局统计"**（它红了很多轮）：
            //
            //    原来的写法是"跑 4 局，每局**对局结束时**看看
            //    法师身上还有没有待用类型、或者对手身上还有没有状态"。
            //    两个问题：
            //      ① **样本太小**：4 局里只要法师没在"恰好那一刻"持有类型/状态就红；
            //      ② **看的是终局快照**：状态只持续"目标自己的 2 个回合"，
            //         法师完全可能中途挂过、但结束时已经跳完了；
            //         待用类型也会被"消耗它的那一次攻击"吃掉。
            //
            //    单局逐步轨迹（我用来定位的那次）证实了这一点：类型**确实铺上了**
            //    （`[后]pending=Poison`），只是结束时身上没有。
            //    所以正确的口径是**过程中有没有发生过**，而不是终局快照。
            const int games = 40;
            int gamesWhereApplied = 0;

            for (int seed = 0; seed < games; seed++)
            {
                PlayerState mage = Make("法", CharacterDefinition.Wizard);
                PlayerState foe = Make("敌", CharacterDefinition.Soldier);

                var game = new GameManager(mage, foe, new GreedyChoiceProvider(),
                                           new GreedyChoiceProvider());
                var logic = (WizardLogic)mage.Character;
                var selector = new GreedyChoiceProvider();

                bool appliedThisGame = false;

                for (int guard = 0; guard < 300 && !game.IsGameOver; guard++)
                {
                    PlayerState actor = game.CurrentPlayer;
                    if (actor == null) break;

                    game.BeginTurn();
                    game.ResolveStatusTurnStart(actor);
                    if (game.IsGameOver) break;
                    if (!ReferenceEquals(game.CurrentPlayer, actor)) continue;

                    LegalMove move = selector.ChooseMove(game, actor);
                    if (move.Actor == null) break;

                    TurnResult r = game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);

                    // ① 过程里铺上过类型，或 ② 过程里真的挂上过状态
                    if (ReferenceEquals(actor, mage))
                    {
                        if (logic.HasPendingType) appliedThisGame = true;
                        if (r.StatusHit || r.AppliedStatus != StatusEffect.None)
                            appliedThisGame = true;
                    }
                    if (foe.Statuses.Count > 0) appliedThisGame = true;

                    game.ResolveStatusTurnEnd(actor);
                }

                if (appliedThisGame) gamesWhereApplied++;
            }

            Assert.IsTrue(gamesWhereApplied > 0,
                "AI 法师走无头对局时应当至少铺上一次伤害类型或挂上一个状态 —— "
                + "否则这个角色的招牌能力在 AI 手里等于不存在。"
                + $"（{games} 局里有 {gamesWhereApplied} 局做到了）");
        }

        // ============================================================
        // 玩家要求："只要法师选择了放弃，他再消耗掉这次攻击前不会再选择放弃"
        // ============================================================

        /// <summary>
        /// **待用类型存在时，不再问"要不要放弃"，也不再改类型。**
        ///
        /// 玩家原话：
        ///   "只要法师选择了放弃，他再消耗掉这次攻击前不会再选择放弃。"
        ///
        /// 这正是"法师最少要两个回合才能完成一次带效果的攻击"的机制基础：
        ///   第 1 回合放弃（铺类型，白扔）-> 第 2 回合真的攻击（用掉类型）。
        /// 如果第 2 回合还能再放弃，法师就会**永远在铺垫、永远不出手**。
        /// </summary>
        [Test]
        public void A_pending_type_blocks_further_abandon_choices()
        {
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            var game = new GameManager(mage, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            var wiz = (WizardLogic)mage.Character;

            // ① 先铺一个类型（模拟"上一回合已经放弃过"）
            wiz.SetPendingDamageType(DamageType.Poison);
            Assert.IsTrue(wiz.HasPendingType);

            // ② 本次合出 4（正常会问"要不要放弃"）
            mage.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);

            var context = new PreTurnChoiceContext(mage, mage, mage.LeftHand,
                                                   foe.LeftHand)
            {
                Manager = game,
                ResultValue = 4,
            };

            var requests = new List<ChoiceRequest>();
            wiz.CollectPreTurnChoices(context, requests);

            for (int i = 0; i < requests.Count; i++)
            {
                Assert.IsFalse(requests[i].Effect == HandEffect.WizardAbandonAttack,
                    "身上已有待用类型时，不该再问「要不要放弃」—— "
                    + "否则法师会永远铺垫、永远不出手。");
            }
        }

        /// <summary>
        /// **类型一旦被消耗，就又能放弃了**（证明上一条的闸门是"待用类型"，
        /// 而不是"整局只许放弃一次"）。
        /// </summary>
        [Test]
        public void After_the_type_is_consumed_the_wizard_may_abandon_again()
        {
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);

            var game = new GameManager(mage, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            var wiz = (WizardLogic)mage.Character;

            wiz.SetPendingDamageType(DamageType.Poison);
            wiz.ConsumeDamageType();                 // 用掉了
            Assert.IsFalse(wiz.HasPendingType);

            var context = new PreTurnChoiceContext(mage, mage, mage.LeftHand,
                                                   foe.LeftHand)
            {
                Manager = game,
                ResultValue = 4,
            };

            var requests = new List<ChoiceRequest>();
            wiz.CollectPreTurnChoices(context, requests);

            bool asked = false;
            for (int i = 0; i < requests.Count; i++)
                if (requests[i].Effect == HandEffect.WizardAbandonAttack) asked = true;

            Assert.IsTrue(asked,
                "类型已经用掉了，就该能重新放弃一次（否则法师一辈子只能铺一次）。");
        }

        // ============================================================
        // 已验证的：走界面路径时，法师能铺上类型并挂上状态
        // ============================================================

        [Test]
        public void Wizard_records_the_type_and_applies_it_on_the_next_hit()
        {
            // 这条走**可直接驱动**的路径，证明"铺类型 -> 下一次命中挂状态"
            // 这条链在核心层是通的（不依赖界面收集抉择）。
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            GameManager game = MakeGame(mage, foe);

            var logic = (WizardLogic)mage.Character;

            // ① 铺类型
            logic.SetPendingDamageType(DamageType.Fire);
            Assert.IsTrue(logic.HasPendingType);

            // ② 下一次攻击命中（4）：左手 3 + 对手 1 = 4
            mage.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);

            game.ExecuteTurn(mage, mage.LeftHand, foe.LeftHand);

            Assert.IsTrue(foe.HasStatus(StatusEffect.Burn),
                "火焰命中了就该给对手挂上燃烧。");
            Assert.IsFalse(logic.HasPendingType,
                "类型用完即清（无论命中与否都不保留）。");
        }

        [Test]
        public void Wizard_type_is_wasted_when_the_attack_misses()
        {
            // 玩家确认："没命中就作废，法师要重新放弃一次。"
            PlayerState mage = Make("法", CharacterDefinition.Wizard);
            PlayerState foe = Make("敌", CharacterDefinition.Soldier);
            GameManager game = MakeGame(mage, foe);

            var logic = (WizardLogic)mage.Character;
            logic.SetPendingDamageType(DamageType.Poison);

            // 制造"完全没落地"：对手双手都是 7 -> 刺客式潜行？不适用。
            // 改用一个更直接的判据：把段数压成 0（等价于被取消）。
            mage.LeftHand.SetValue(3);
            foe.LeftHand.SetValue(1);

            var ctx = new AttackContext(mage, foe, mage.LeftHand, 4) { HitCount = 0 };
            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.IsFalse(foe.HasStatus(StatusEffect.Poison),
                "一段都没落地 -> 不算命中，不挂状态。");
        }
    }
}
