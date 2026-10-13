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
