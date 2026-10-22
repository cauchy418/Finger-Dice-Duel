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
    /// **法师·塑能学派（SchoolOfEvocation）的状态体系**测试。
    ///
    /// 覆盖玩家确认的每一条规则：
    ///   · 10 种伤害类型 -&gt; 9 种状态（力场不挂）
    ///   · 持续时间 = **目标自己的 2 个回合**（计数式）
    ///   · 同种状态**刷新**而不叠加强度
    ///   · 数值修正：强酸 防 −2、震慑 攻 −2、冰冻 结果减半
    ///   · 腐朽不能回血
    ///   · 中毒不可抵挡 / 燃烧可被盾挡 / 感电伤自己和相邻
    ///   · 命中口径：被盾挡仍算命中，被闪避不算
    ///   · 连续放弃只保留最新选择；没命中就作废
    /// </summary>
    [TestFixture]
    public class SchoolOfEvocationStatusTests
    {
        private static PlayerState Make(string name, CharacterDefinition def)
        {
            PlayerState p = CharacterFactory.CreatePlayer(name, def);
            return p;
        }

        // ============================================================
        // 类型 -> 状态 的映射
        // ============================================================

        [Test]
        public void Every_damage_type_maps_to_its_status()
        {
            Assert.AreEqual(StatusEffect.Resonance, StatusNames.StatusOf(DamageType.Thunder));
            Assert.AreEqual(StatusEffect.Acid, StatusNames.StatusOf(DamageType.Acid));
            Assert.AreEqual(StatusEffect.Poison, StatusNames.StatusOf(DamageType.Poison));
            Assert.AreEqual(StatusEffect.Blind, StatusNames.StatusOf(DamageType.Radiant));
            Assert.AreEqual(StatusEffect.Decay, StatusNames.StatusOf(DamageType.Necrotic));
            Assert.AreEqual(StatusEffect.Burn, StatusNames.StatusOf(DamageType.Fire));
            Assert.AreEqual(StatusEffect.Freeze, StatusNames.StatusOf(DamageType.Cold));
            Assert.AreEqual(StatusEffect.Shock, StatusNames.StatusOf(DamageType.Lightning));
            Assert.AreEqual(StatusEffect.Stun, StatusNames.StatusOf(DamageType.Psychic));
        }

        [Test]
        public void Force_does_not_apply_any_status()
        {
            Assert.AreEqual(StatusEffect.None, StatusNames.StatusOf(DamageType.Force),
                "玩家确认：力场只有穿透这一条，不附带任何状态。");
        }

        // ============================================================
        // 持续时间：目标自己的 2 个回合
        // ============================================================

        [Test]
        public void Status_lasts_exactly_two_of_the_targets_own_turns()
        {
            PlayerState p = Make("敌", CharacterDefinition.Champion);
            p.ApplyStatus(StatusEffect.Poison);

            Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.Poison));

            p.TickStatuses();
            Assert.IsTrue(p.HasStatus(StatusEffect.Poison), "第 1 个自己的回合结束后还在。");

            p.TickStatuses();
            Assert.IsFalse(p.HasStatus(StatusEffect.Poison),
                "第 2 个自己的回合结束后应当移除（玩家确认的时长）。");
        }

        [Test]
        public void Applying_the_same_status_again_refreshes_instead_of_stacking()
        {
            PlayerState p = Make("敌", CharacterDefinition.Champion);
            p.ApplyStatus(StatusEffect.Burn);
            p.TickStatuses();
            Assert.AreEqual(1, p.StatusTurnsLeft(StatusEffect.Burn));

            p.ApplyStatus(StatusEffect.Burn);      // 再挂一次
            Assert.AreEqual(2, p.StatusTurnsLeft(StatusEffect.Burn),
                "玩家确认：同一种效果只刷新持续时间，不叠加强度。");
        }

        [Test]
        public void Different_statuses_coexist()
        {
            PlayerState p = Make("敌", CharacterDefinition.Champion);
            p.ApplyStatus(StatusEffect.Poison);
            p.ApplyStatus(StatusEffect.Blind);

            Assert.AreEqual(2, p.Statuses.Count);
            Assert.IsTrue(p.HasStatus(StatusEffect.Poison));
            Assert.IsTrue(p.HasStatus(StatusEffect.Blind));
        }

        [Test]
        public void Reset_match_clears_all_statuses()
        {
            PlayerState a = Make("甲", CharacterDefinition.SchoolOfEvocation);
            PlayerState b = Make("乙", CharacterDefinition.Champion);
            var game = new GameManager(a, b, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            b.ApplyStatus(StatusEffect.Poison);
            b.ApplyStatus(StatusEffect.Burn);
            Assert.AreEqual(2, b.Statuses.Count);

            game.ResetMatch();
            Assert.AreEqual(0, b.Statuses.Count,
                "重开一局不能带着上一局的中毒 / 燃烧。");
        }

        // ============================================================
        // 数值修正
        // ============================================================

        [Test]
        public void Acid_reduces_defense_by_two()
        {
            PlayerState p = Make("甲", CharacterDefinition.Breaker);   // 防 0
            int before = StatusRules.EffectiveDefenseBonus(p);

            p.ApplyStatus(StatusEffect.Acid);
            Assert.AreEqual(before - StatusRules.AcidDefensePenalty,
                StatusRules.EffectiveDefenseBonus(p));
        }

        [Test]
        public void Stun_reduces_attack_by_two()
        {
            PlayerState p = Make("甲", CharacterDefinition.Swordsman);   // 攻 +2
            int before = StatusRules.EffectiveAttackBonus(p);

            p.ApplyStatus(StatusEffect.Stun);
            Assert.AreEqual(before - StatusRules.StunAttackPenalty,
                StatusRules.EffectiveAttackBonus(p));
        }

        [Test]
        public void Freeze_halves_the_result_rounding_down()
        {
            PlayerState p = Make("甲", CharacterDefinition.SchoolOfEvocation);

            // 玩家给的例子：9 变 4、5 变 2
            Assert.AreEqual(9, StatusRules.ApplyResultModifiers(p, 9),
                "没有冰冻时结果不变。");

            p.ApplyStatus(StatusEffect.Freeze);
            Assert.AreEqual(4, StatusRules.ApplyResultModifiers(p, 9), "9 -> 4（向下取整）");
            Assert.AreEqual(2, StatusRules.ApplyResultModifiers(p, 5), "5 -> 2（向下取整）");
        }

        [Test]
        public void Decay_blocks_healing_entirely()
        {
            PlayerState p = Make("甲", CharacterDefinition.Champion);
            p.TakeDamage(10);
            int hurt = p.Health;

            p.Heal(9);
            Assert.AreEqual(hurt + 9, p.Health, "正常情况下 9 点回复生效。");

            p.ApplyStatus(StatusEffect.Decay);
            p.Heal(9);
            Assert.AreEqual(hurt + 9, p.Health,
                "腐朽期间完全不能回血（玩家确认：不能回复生命值）。");
        }

        // ============================================================
        // 合法性过滤
        // ============================================================

        [Test]
        public void Resonance_restricts_attacks_to_distance_one()
        {
            // 4 人环：座位 1 与座位 3 距离 2
            PlayerState a = Make("甲", CharacterDefinition.Champion);
            PlayerState b = Make("乙", CharacterDefinition.Champion);
            PlayerState c = Make("丙", CharacterDefinition.Champion);
            PlayerState d = Make("丁", CharacterDefinition.Champion);

            GameManager game = GameManager.Create2v2(
                new[] { a, b, c, d },
                new IChoiceProvider[]
                {
                    new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                    new SimpleChoiceProvider(), new SimpleChoiceProvider(),
                });

            Assert.AreEqual(1, game.DistanceBetween(a, b), "相邻座位距离 1。");
            Assert.AreEqual(2, game.DistanceBetween(a, c), "对角座位距离 2。");
            Assert.IsTrue(StatusRules.CanAttackTarget(a, c, game), "没有残响时可以打。");

            a.ApplyStatus(StatusEffect.Resonance);
            Assert.IsFalse(StatusRules.CanAttackTarget(a, c, game),
                "残响：只能攻击与自己距离 1 的角色（距离 2 打不到）。");
            Assert.IsTrue(StatusRules.CanAttackTarget(a, b, game),
                "残响：距离 1 的仍然能打。");
        }

        [Test]
        public void Stun_blocks_attacking_the_source_only()
        {
            PlayerState mage = Make("法", CharacterDefinition.SchoolOfEvocation);
            PlayerState other = Make("乙", CharacterDefinition.Champion);

            PlayerState victim = Make("丙", CharacterDefinition.Champion);
            victim.ApplyStatus(StatusEffect.Stun, PlayerState.StatusDurationTurns, mage);

            Assert.IsTrue(StatusRules.IsBlockedByStun(victim, mage),
                "被震慑者不能攻击给他挂震慑的法师·塑能学派。");
            Assert.IsFalse(StatusRules.IsBlockedByStun(victim, other),
                "但不是所有人都打不到 —— 只有施加者不能成为其目标。");
        }

        // ============================================================
        // 法师·塑能学派自己的机制
        // ============================================================

        [Test]
        public void SchoolOfEvocation_keeps_only_the_latest_damage_type()
        {
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            var mage = p.Character as SchoolOfEvocationLogic;
            Assert.NotNull(mage, "法师·塑能学派应绑定 SchoolOfEvocationLogic。");

            mage.SetPendingDamageType(DamageType.Fire);
            mage.SetPendingDamageType(DamageType.Cold);

            Assert.AreEqual(DamageType.Cold, mage.PendingDamageType,
                "玩家确认：连续放弃时只保留最新的选择。");
        }

        [Test]
        public void Consuming_the_type_clears_it()
        {
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            var mage = (SchoolOfEvocationLogic)p.Character;

            mage.SetPendingDamageType(DamageType.Psychic);
            Assert.AreEqual(DamageType.Psychic, mage.ConsumeDamageType());
            Assert.AreEqual(DamageType.None, mage.PendingDamageType,
                "取走之后类型作废 —— 玩家确认没命中就要重新放弃一次。");
        }

        [Test]
        public void Force_attack_pierces_shield_and_cannot_be_dodged()
        {
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            var mage = (SchoolOfEvocationLogic)p.Character;

            Assert.IsFalse(mage.PreventsDodge, "不是力场时可以闪避。");

            mage.SetPendingDamageType(DamageType.Force);
            Assert.IsTrue(mage.IsForceAttack);
            Assert.IsTrue(mage.PreventsDodge, "力场同法师·咒法学派：不可被闪避。");
        }

        [Test]
        public void Only_force_makes_the_mage_undodgeable()
        {
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            var mage = (SchoolOfEvocationLogic)p.Character;

            mage.SetPendingDamageType(DamageType.Fire);
            Assert.IsFalse(mage.PreventsDodge,
                "除力场外的 9 种伤害都能被闪避（玩家确认）。");
        }

        [Test]
        public void SchoolOfEvocation_is_registered_as_a_built_in_character()
        {
            CharacterDefinition def = null;
            for (int i = 0; i < CharacterDefinition.BuiltIn.Length; i++)
                if (CharacterDefinition.BuiltIn[i].Id == CharacterDefinition.SchoolOfEvocationId)
                    def = CharacterDefinition.BuiltIn[i];

            Assert.NotNull(def, "法师·塑能学派应当在内置角色表里。");
            Assert.AreEqual(0, def.AttackBonus);
            Assert.AreEqual(-1, def.DefenseBonus);
            Assert.AreEqual(Alignment.TrueNeutral,
                CharacterCodex.For(def).Alignment, "玩家最新指定：绝对中立。");
        }

        [Test]
        public void Match_reset_clears_the_pending_type()
        {
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            PlayerState foe = Make("敌", CharacterDefinition.Champion);
            var game = new GameManager(p, foe, new SimpleChoiceProvider(),
                                       new SimpleChoiceProvider());

            var mage = (SchoolOfEvocationLogic)p.Character;
            mage.SetPendingDamageType(DamageType.Acid);
            game.ResetMatch();

            Assert.AreEqual(DamageType.None, mage.PendingDamageType,
                "重开一局不能带着上一局选的伤害类型。");
        }

        // ============================================================
        // 回归：人类玩家点"放弃攻击"却仍造成伤害
        // ============================================================

        /// <summary>
        /// 模拟**人类玩家的抉择器**：它没有实现 <see cref="IDamageTypeChoiceSink"/>，
        /// 所以 `WantsToAbandonAttack` 恒为 false、`ChosenDamageType` 恒为 None。
        ///
        /// 界面（`BoardController`）会把"放弃"与"类型"**直接写到法师·塑能学派身上**。
        /// </summary>
        private sealed class HumanLikeSink : IDamageTypeChoiceSink
        {
            public DamageType ChosenDamageType { get; set; }
            public bool WantsToAbandonAttack { get; set; }
            public void SetGameForChoices(GameManager game) { }
        }

        [Test]
        public void Human_abandon_is_not_undone_by_a_silent_sink()
        {
            // ⚠️⚠️ 玩家实测报的 bug："法师·塑能学派选择放弃攻击仍造成了伤害"。
            //
            //    根因：`SchoolOfEvocationLogic.ReadFromSink` 原来**无条件**用
            //    `sink.WantsToAbandonAttack` 覆盖本回合标记。而人类抉择器
            //    恒报 false —— 于是界面刚写下的 true 被**读回 false**，
            //    `SuppressAttackDamage` 永远不成立，伤害照打。
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            var mage = (SchoolOfEvocationLogic)p.Character;

            // 界面直接写到法师·塑能学派身上（BoardController 就是这么做的）
            mage.SetPendingDamageType(DamageType.Thunder);
            mage.WantsToAbandonThisTurn = true;

            // 结算时 GameManager 会再读一次抉择器
            mage.ReadFromSink(new HumanLikeSink());

            Assert.IsTrue(mage.WantsToAbandonThisTurn,
                "抉择器没表态时，不能把界面已确认的「放弃攻击」读回 false —— "
                + "否则放弃的一击仍会造成伤害。");
            Assert.AreEqual(DamageType.Thunder, mage.PendingDamageType,
                "界面选的类型也不能被抹掉。");
        }

        [Test]
        public void Abandon_flag_can_still_be_cleared_when_nothing_was_chosen()
        {
            // 反向保护：**什么都没选**时，抉择器的 false 仍应清掉残留标记，
            // 否则上一回合的"放弃"会粘到下一回合（那样会白扔一击）。
            PlayerState p = Make("法", CharacterDefinition.SchoolOfEvocation);
            var mage = (SchoolOfEvocationLogic)p.Character;

            mage.WantsToAbandonThisTurn = true;      // 上一回合的残留

            mage.ReadFromSink(new HumanLikeSink());

            Assert.IsFalse(mage.WantsToAbandonThisTurn,
                "没有待用类型时，残留的放弃标记应当被清掉。");
        }
    }
}
