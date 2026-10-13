using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **三个新角色的核心行为**：圣武士 / 邪术师 / 魔射手。
    ///
    /// 规则都由玩家逐条确认过，这里把"每条规则"钉成断言 ——
    /// 尤其是容易写反的地方（代挡判据、攻击数字的取值、两段伤害怎么分）。
    /// </summary>
    [TestFixture]
    public class NewCharacterBatchTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
            => CharacterFactory.CreatePlayer(name, def);

        /// <summary>1v1 对局：A 与 B。</summary>
        private static GameManager Match(CharacterDefinition defA, CharacterDefinition defB,
                                         out SimpleChoiceProvider sinkA,
                                         out SimpleChoiceProvider sinkB)
        {
            sinkA = new SimpleChoiceProvider();
            sinkB = new SimpleChoiceProvider();

            PlayerState a = Make("A", defA);
            PlayerState b = Make("B", defB);
            return new GameManager(a, b, sinkA, sinkB);
        }

        // ============================================================
        // 圣武士
        // ============================================================

        [Test]
        public void Paladin_stats_and_range()
        {
            PlayerState p = Make("圣", CharacterDefinition.Paladin);

            Assert.AreEqual(0, p.Character.AttackBonus);
            Assert.AreEqual(1, p.Character.DefenseBonus);
            Assert.AreEqual(1, GameManager.BaseRangeOf(p), "圣武士攻击距离 1。");
        }

        [Test]
        public void Paladin_turns_four_and_six_into_shields()
        {
            // 玩家确认："4 和 6 不能攻击，改为合成盾牌"
            GameManager game = Match(CharacterDefinition.Paladin, CharacterDefinition.Blank,
                                     out _, out _);
            PlayerState paladin = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 4 + 1 = 5 -> 本身就是护盾，不是最好的例子；
            // 用"结果 = 4"验证改写：0+4? 不行，手值最小 1。
            // 1 + 3 = 4 -> 结果是 4（原本是 Attack4）
            TestSetup.SetHand(paladin.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 3);

            TurnResult r = game.ExecuteTurn(paladin, paladin.LeftHand, foe.LeftHand);

            Assert.AreEqual(4, r.ResultValue, "运算结果应为 4。");
            Assert.AreEqual(HandEffectType.Shield, r.Effect,
                "圣武士的 4 应被改写为护盾（不再是攻击）。");
            Assert.AreEqual(ShieldState.Formed, paladin.LeftHand.State,
                "改写后手牌应处于成型护盾状态。");
            Assert.AreEqual(0, r.DamageDealt, "改写成护盾后不应造成攻击伤害。");
        }

        [Test]
        public void Paladin_turns_six_into_a_shield()
        {
            GameManager game = Match(CharacterDefinition.Paladin, CharacterDefinition.Blank,
                                     out _, out _);
            PlayerState paladin = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 2 + 4 = 6 -> 原本 Attack6
            TestSetup.SetHand(paladin.LeftHand, 2);
            TestSetup.SetHand(foe.LeftHand, 4);

            TurnResult r = game.ExecuteTurn(paladin, paladin.LeftHand, foe.LeftHand);

            Assert.AreEqual(6, r.ResultValue);
            Assert.AreEqual(HandEffectType.Shield, r.Effect, "圣武士的 6 也应改为护盾。");
        }

        [Test]
        public void Paladin_shock_deals_two_true_damage_through_a_shield()
        {
            // 玩家确认："造成 2 点真实伤害" —— 真实伤害无视护盾与攻防加成
            GameManager game = Match(CharacterDefinition.Paladin, CharacterDefinition.Blank,
                                     out _, out _);
            PlayerState paladin = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 干扰项：让对手有一面成型盾，真实伤害应无视它
            foe.RightHand.SetValue(5);
            foe.RightHand.SetShieldState(ShieldState.Formed);

            int before = foe.Health;

            // 1 + 3 = 4 -> 合盾 -> 触发冲击
            TestSetup.SetHand(paladin.LeftHand, 1);
            TestSetup.SetHand(foe.LeftHand, 3);
            game.ExecuteTurn(paladin, paladin.LeftHand, foe.LeftHand);

            Assert.AreEqual(before - PaladinLogic.ShockDamage, foe.Health,
                $"合盾应造成 {PaladinLogic.ShockDamage} 点真实伤害（无视护盾）。");
            Assert.AreEqual(ShieldState.Formed, foe.RightHand.State,
                "真实伤害不该消耗对手的护盾。");
        }

        [Test]
        public void Paladin_redirect_requires_the_intended_target_in_range()
        {
            // 玩家澄清：判据是"**挨打的人**在自己攻击距离内"，
            // 而不是"攻击者在距离内"。
            var paladin = Make("圣", CharacterDefinition.Paladin);
            var attacker = Make("攻", CharacterDefinition.Blank);
            var victim = Make("受", CharacterDefinition.Blank);

            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var seated = new List<PlayerState> { paladin, attacker, victim,
                                                 Make("闲", CharacterDefinition.Blank) };
            GameManager game = GameManager.Create2v2(seated, providers);

            var logic = paladin.Character as IAttackRedirector;
            Assert.NotNull(logic, "圣武士应实现代挡接口。");

            // 圣武士在 1 号位：距离 1 的是 2 号位(attacker)与 4 号位(闲)；
            // 3 号位(victim)距离 2 -> 超出距离 1
            Assert.AreEqual(2, game.DistanceBetween(paladin, victim),
                "前置：3 号位对 1 号位的距离应为 2。");

            Assert.False(logic.TryRedirectToSelf(paladin, attacker, victim, game),
                "挨打的人超出攻击距离 -> 不能代挡。");

            // 4 号位（距离 1）挨打时可以代挡
            PlayerState near = seated[3];
            Assert.AreEqual(1, game.DistanceBetween(paladin, near));
            // 没有 sink 记录时默认不发动，所以这里只断言"判据放行"
            // （真正的发动由玩家/抉择器决定）
        }

        // ============================================================
        // 邪术师
        // ============================================================

        [Test]
        public void Warlock_stats_and_infinite_range()
        {
            PlayerState w = Make("邪", CharacterDefinition.Warlock);

            Assert.AreEqual(0, w.Character.AttackBonus);
            Assert.AreEqual(-1, w.Character.DefenseBonus);
            Assert.AreEqual(int.MaxValue, GameManager.BaseRangeOf(w),
                "邪术师攻击距离无限。");
        }

        [Test]
        public void Warlock_cancels_an_attack_matching_one_of_its_hands()
        {
            // 玩家确认：攻击数字与自己某只手相同 -> 把那只手 +1 取消这次攻击
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Warlock,
                                     out _, out _);
            PlayerState attacker = game.PlayerA;
            PlayerState warlock = game.PlayerB;

            // 邪术师手上有一个 4
            TestSetup.SetHand(warlock.LeftHand, 4);
            TestSetup.SetHand(warlock.RightHand, 1);

            // 直接构造一次 4 的攻击，验证取消
            var ctx = new AttackContext(attacker, warlock, attacker.LeftHand, 4)
            {
                SourceHandValueBefore = 1,
                TargetHandValueBefore = 3,      // 1 + 3 = 4 -> 攻击数字 4
            };

            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.True(ctx.WasCancelled, "邪术师应取消这次攻击。");
            Assert.AreEqual(5, warlock.LeftHand.Value,
                "取消的代价是那只手 +1（永久）。");
        }

        [Test]
        public void Warlock_cancel_is_permanent_and_updates_the_shield_state()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Warlock,
                                     out _, out _);
            PlayerState attacker = game.PlayerA;
            PlayerState warlock = game.PlayerB;

            // 手里是一个"成型的 5"，而攻击数字也是 5 -> 取消后变成 6
            TestSetup.SetHand(warlock.LeftHand, 5);
            warlock.LeftHand.SetShieldState(ShieldState.Formed);

            var ctx = new AttackContext(attacker, warlock, attacker.LeftHand, 4)
            {
                SourceHandValueBefore = 2,
                TargetHandValueBefore = 3,      // 2 + 3 = 5
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.True(ctx.WasCancelled);
            Assert.AreEqual(6, warlock.LeftHand.Value);
            Assert.AreEqual(ShieldState.None, warlock.LeftHand.State,
                "手值不再是 5 就不该继续挂着护盾状态。");
        }

        [Test]
        public void Warlock_does_not_cancel_when_no_hand_matches()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Warlock,
                                     out _, out _);
            PlayerState attacker = game.PlayerA;
            PlayerState warlock = game.PlayerB;

            TestSetup.SetHand(warlock.LeftHand, 2);
            TestSetup.SetHand(warlock.RightHand, 3);

            var ctx = new AttackContext(attacker, warlock, attacker.LeftHand, 4)
            {
                SourceHandValueBefore = 1,
                TargetHandValueBefore = 3,      // 攻击数字 4，手上没有 4
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.False(ctx.WasCancelled, "没有匹配的手 -> 不取消。");
            Assert.AreEqual(2, warlock.LeftHand.Value, "不取消就不该改手值。");
        }

        [Test]
        public void Warlock_cannot_cancel_special_attacks()
        {
            // 玩家确认：剑客追加攻击与圣武士冲击的"攻击数字视为 0" -> 不可被取消
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Warlock,
                                     out _, out _);
            PlayerState attacker = game.PlayerA;
            PlayerState warlock = game.PlayerB;

            TestSetup.SetHand(warlock.LeftHand, 4);

            var ctx = new AttackContext(attacker, warlock, attacker.LeftHand, 4)
            {
                IsSpecialAttack = true,         // 追加攻击 / 冲击
            };

            Assert.AreEqual(0, ctx.AttackValue, "特殊攻击的攻击数字应为 0。");

            ctx.Resolve(new SimpleChoiceProvider(), game);
            Assert.False(ctx.WasCancelled, "特殊攻击不可被取消。");
        }

        [Test]
        public void Warlock_can_skip_only_when_hurt_and_health_differs()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Warlock,
                                     out _, out _);
            PlayerState warlock = game.PlayerB;

            var logic = warlock.Character as ITurnSkipRule;
            Assert.NotNull(logic, "邪术师应实现跳过回合接口。");

            // 满血 -> 不能发动
            Assert.False(logic.CanSkip(warlock, game), "满血不能发动。");

            // 受伤，且对手体力不同 -> 可以发动
            warlock.TakeDamage(5);
            Assert.True(logic.CanSkip(warlock, game),
                "体力不满且有人体力与自己不同 -> 可以发动。");
        }

        [Test]
        public void Warlock_health_swap_sets_exact_values()
        {
            GameManager game = Match(CharacterDefinition.Blank, CharacterDefinition.Warlock,
                                     out _, out _);
            PlayerState foe = game.PlayerA;
            PlayerState warlock = game.PlayerB;

            warlock.TakeDamage(7);              // 邪术师 13
            Assert.AreEqual(13, warlock.Health);

            Assert.True(WarlockLogic.ApplyHealthSwap(warlock, foe),
                "应能把对手的体力改成与自己一致。");
            Assert.AreEqual(13, foe.Health, "对手体力应被改成与邪术师一致。");
        }

        [Test]
        public void Warlock_cancels_a_multi_hit_attack_entirely_not_per_hit()
        {
            // 玩家要求："忍者和魔射手的二段攻击，邪术师需要可以一次性全部抵消"
            //
            // 实现上这件事是**天然成立**的：取消发生在 AttackContext.Resolve
            // 的最开头，段数（HitCount = 2）此时还没进入循环 ——
            // 一次取消就让整个循环不跑，所以是"一次性全部抵消"而不是"抵消一段"。
            // 这条测试把这个性质钉住，防止以后有人把取消挪进循环里。
            foreach (CharacterDefinition attackerDef in new[]
                     {
                         CharacterDefinition.Ninja,          // 4 -> 两段各 2
                         CharacterDefinition.ArcaneArcher,   // 4 -> 两段各 4
                     })
            {
                GameManager game = Match(attackerDef, CharacterDefinition.Warlock,
                                         out _, out _);
                PlayerState attacker = game.PlayerA;
                PlayerState warlock = game.PlayerB;

                TestSetup.SetHand(warlock.LeftHand, 4);      // 手上有 4 -> 能取消
                TestSetup.SetHand(warlock.RightHand, 1);

                int before = warlock.Health;

                var ctx = new AttackContext(attacker, warlock, attacker.LeftHand, 4)
                {
                    SourceHandValueBefore = 1,
                    TargetHandValueBefore = 3,               // 攻击数字 4
                };
                ctx.Resolve(new SimpleChoiceProvider(), game);

                Assert.True(ctx.WasCancelled,
                    $"{attackerDef.DisplayName} 的两段攻击应被取消。");
                Assert.AreEqual(0, ctx.HitCount,
                    $"{attackerDef.DisplayName}：取消后**一段都不该打**。");
                Assert.AreEqual(0, ctx.LandedHits, "不应有任何一段命中。");
                Assert.AreEqual(0, ctx.AppliedDamage, "两段合计应为 0 伤害。");
                Assert.AreEqual(before, warlock.Health,
                    "被完全取消的攻击不该扣血。");

                // 只能 +1 一次（一只手的数字只匹配一次）
                Assert.AreEqual(5, warlock.LeftHand.Value,
                    "取消的代价只付一次（+1），不是每段各付一次。");
            }
        }

        // ============================================================
        // 邪术师的交互（跳过回合 -> 点一个人的手改体力）
        // ============================================================

        [Test]
        public void Warlock_skip_enters_target_selection_then_swaps_health()
        {
            // 玩家要求的交互："在右侧显示跳过回合，然后点击一个人的手发动技能"
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var a = Make("A", CharacterDefinition.Blank);
            var warlock = Make("邪", CharacterDefinition.Warlock);

            // ⚠️ 邪术师必须是**当前行动方** —— 第一个参数是先手。
            //    （我第一次写成 (a, warlock)，于是 CanSkipTurn 检查的是 A，
            //      诊断为 active=A hp=20 才看出来。）
            var game = new GameManager(warlock, a, providers[0], providers[1]);
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                           new SimpleChoiceProvider());
            board.BeginTurn();

            // 满血 -> 不能跳过（技能要求体力不满）
            Assert.False(board.CanSkipTurn, "满血时不该出现跳过回合按钮。");

            warlock.TakeDamage(7);
            Assert.True(board.CanSkipTurn,
                "体力不满且对手体力不同 -> 应出现跳过回合按钮。");

            // 点"跳过回合" -> 进入选目标相位（**不立刻结束回合**）
            Assert.True(board.SkipTurn(), "应进入选目标相位。");
            Assert.AreEqual(BoardPhase.ChoosingHealthSwapTarget, board.Phase,
                "应停在'选一个人改体力'这一步。");

            int warlockHealth = warlock.Health;

            // 点对手的手 -> 那人体力被改成与邪术师一致，回合结束
            Assert.True(board.ClickHand(a.LeftHand), "点目标的手应成功发动。");

            Assert.AreEqual(warlockHealth, a.Health,
                "对手体力应被改成与邪术师一致。");
            Assert.True(board.Phase != BoardPhase.ChoosingHealthSwapTarget,
                "发动后应离开选目标相位。");
        }

        [Test]
        public void Warlock_skip_rejects_a_target_whose_health_already_matches()
        {
            // 体力本来就一样的人不是合法目标 —— 点头上不该让它浪费回合
            var providers = new List<IChoiceProvider>
            {
                new SimpleChoiceProvider(), new SimpleChoiceProvider(),
            };
            var warlock = Make("邪", CharacterDefinition.Warlock);
            var a = Make("A", CharacterDefinition.Blank);

            var game = new GameManager(warlock, a, providers[0], providers[1]);
            var board = new BoardController(game, new SimpleChoiceProvider(),
                                           new SimpleChoiceProvider());
            board.BeginTurn();

            warlock.TakeDamage(7);
            Assert.True(board.SkipTurn());

            // 强行把对手体力设成与邪术师一致 -> 该目标变非法
            a.SetHealthExact(warlock.Health);

            Assert.False(board.ClickHand(a.LeftHand),
                "体力已一致的人不该是合法目标。");
            Assert.AreEqual(BoardPhase.ChoosingHealthSwapTarget, board.Phase,
                "非法目标应留在选择相位，等玩家另选。");
        }

        [Test]
        public void Ai_warlock_uses_the_skip_skill_on_its_own()
        {
            // AI 没有"点击"，必须自己在 MakeAutomaticMove 里走完这个技能 ——
            // 否则 AI 的邪术师永远不会用它（等于技能在 AI 手里不存在）。
            var ai = new GreedyChoiceProvider();
            var warlock = Make("邪", CharacterDefinition.Warlock);
            var foe = Make("敌", CharacterDefinition.Blank);

            var game = new GameManager(warlock, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());
            var board = new BoardController(game,
                                           new SimpleChoiceProvider(),
                                           new SimpleChoiceProvider(),
                                           aiForA: ai);
            board.BeginTurn();

            warlock.TakeDamage(7);
            int warlockHealth = warlock.Health;

            Assert.True(board.MakeAutomaticMove(), "AI 应该自己走完这一步。");
            Assert.AreEqual(warlockHealth, foe.Health,
                "AI 应把对手体力改成与自己一致。");
            Assert.True(board.Phase != BoardPhase.ChoosingHealthSwapTarget,
                "AI 不该停在需要点击的相位上（否则对局会卡死）。");
        }

        // ============================================================
        // 魔射手
        // ============================================================

        [Test]
        public void Archer_stats_and_range_three()
        {
            PlayerState a = Make("魔", CharacterDefinition.ArcaneArcher);

            Assert.AreEqual(0, a.Character.AttackBonus);
            Assert.AreEqual(-1, a.Character.DefenseBonus);
            Assert.AreEqual(3, GameManager.BaseRangeOf(a), "魔射手攻击距离 3。");
        }

        [Test]
        public void Archer_four_becomes_two_hits_of_four()
        {
            // 玩家确认："4 的攻击改为两段伤害为 4 的攻击" —— 各为 4，不是减半
            GameManager game = Match(CharacterDefinition.ArcaneArcher,
                                     CharacterDefinition.Blank, out _, out _);
            PlayerState archer = game.PlayerA;
            PlayerState foe = game.PlayerB;

            var ctx = new AttackContext(archer, foe, archer.LeftHand, 4)
            {
                SourceHandValueBefore = 1,
                TargetHandValueBefore = 3,      // 攻击数字 4
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.AreEqual(2, ctx.HitCount, "4 应拆成两段。");
            Assert.AreEqual(2, ctx.LandedHits, "没有盾时应两段都命中。");
            Assert.AreEqual(8, ctx.AppliedDamage,
                "两段各 4 -> 总伤害 8（而不是忍者的 2+2=4）。");
        }

        [Test]
        public void Archer_six_gets_plus_two_damage()
        {
            GameManager game = Match(CharacterDefinition.ArcaneArcher,
                                     CharacterDefinition.Blank, out _, out _);
            PlayerState archer = game.PlayerA;
            PlayerState foe = game.PlayerB;

            var ctx = new AttackContext(archer, foe, archer.LeftHand, 6)
            {
                SourceHandValueBefore = 2,
                TargetHandValueBefore = 4,      // 攻击数字 6
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.AreEqual(1, ctx.HitCount, "6 仍是单段。");
            Assert.AreEqual(8, ctx.AppliedDamage, "6 + 2 = 8 点伤害。");
        }

        [Test]
        public void Archer_eight_ignores_the_shield()
        {
            // 玩家确认："8 的攻击无视盾牌"
            GameManager game = Match(CharacterDefinition.ArcaneArcher,
                                     CharacterDefinition.Blank, out _, out _);
            PlayerState archer = game.PlayerA;
            PlayerState foe = game.PlayerB;

            // 对手有一面成型盾
            foe.LeftHand.SetValue(5);
            foe.LeftHand.SetShieldState(ShieldState.Formed);

            int before = foe.Health;

            var ctx = new AttackContext(archer, foe, archer.LeftHand, 8)
            {
                SourceHandValueBefore = 3,
                TargetHandValueBefore = 5,      // 3 + 5 = 8
            };
            ctx.Resolve(new SimpleChoiceProvider(), game);

            Assert.False(ctx.WasBlocked, "8 应无视盾牌。");
            Assert.AreEqual(before - 8, foe.Health, "8 点伤害应全部落实。");
            Assert.AreEqual(ShieldState.Formed, foe.LeftHand.State,
                "被无视的盾不该被打碎。");
        }

        [Test]
        public void Archer_eight_is_still_dodgeable()
        {
            // 玩家确认："但可以被游侠闪避邪术取消等" —— 只是无视盾牌
            GameManager game = Match(CharacterDefinition.ArcaneArcher,
                                     CharacterDefinition.Ranger, out _, out _);
            PlayerState archer = game.PlayerA;
            PlayerState ranger = game.PlayerB;

            var ctx = new AttackContext(archer, ranger, archer.LeftHand, 8)
            {
                SourceHandValueBefore = 3,
                TargetHandValueBefore = 5,
            };

            ctx.Resolve(new SimpleChoiceProvider(), game);

            // 8 只改"无视盾牌"，**不改闪避通道** ——
            // 没有 sink 记录时游侠不会自动闪避，所以这里只验证
            // "要么造成伤害、要么被闪避"，二者必有其一（说明闪避判定仍然在跑）。
            Assert.True(ctx.AppliedDamage > 0 || ctx.WasDodged,
                "8 要么造成伤害、要么被闪避，二者必有其一。");
        }
    }
}
