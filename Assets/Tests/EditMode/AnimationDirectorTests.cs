using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 飘字（伤害/格挡/闪避/回复）的计划生成验证。
    ///
    /// 这一层是纯数据，所以能用与规则层同一套秒级测试验证 ——
    /// UI 外观看不到，但"该飘什么字、飘在谁头上、间隔多久"是可以精确断言的。
    /// </summary>
    [TestFixture]
    public class AnimationDirectorTests
    {
        private PlayerState _a;
        private PlayerState _b;

        [SetUp]
        public void SetUp()
        {
            _a = new PlayerState("甲");
            _b = new PlayerState("乙");
        }

        private TurnResult Attack(int damage, int landed = 1, int blocked = 0)
            => new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Attack8,
                DamageDealt = damage,
                LandedHits = landed,
                BlockedHits = blocked,
                HitCount = landed + blocked,
                WasBlocked = blocked > 0,
            };

        // ============================================================
        // 基本四类
        // ============================================================

        [Test]
        public void Damage_shows_on_the_defender_as_a_minus_number()
        {
            AnimationPlan plan = AnimationDirector.Build(Attack(8), _b);

            Assert.AreEqual(1, plan.Floats.Count);
            Assert.AreEqual("乙", plan.Floats[0].Target.Name, "应飘在受击者头上。");
            Assert.AreEqual("-8", plan.Floats[0].Text);
            Assert.AreEqual(FloatKind.Damage, plan.Floats[0].Kind);
            Assert.AreEqual(0f, plan.Floats[0].DelaySeconds, "第一条不延迟。");
        }

        [Test]
        public void Blocked_hit_shows_the_word_block()
        {
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Attack8,
                WasBlocked = true,
                BlockedHits = 1,
                HitCount = 1,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(1, plan.Floats.Count);
            Assert.AreEqual("格挡", plan.Floats[0].Text);
            Assert.AreEqual(FloatKind.Blocked, plan.Floats[0].Kind);
            Assert.AreEqual("乙", plan.Floats[0].Target.Name);
        }

        [Test]
        public void Swashbuckler_dodge_shows_the_word_dodge()
        {
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Attack4,
                WasDodged = true,
                DodgedHits = 1,
                HitCount = 1,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(1, plan.Floats.Count);
            Assert.AreEqual("闪避", plan.Floats[0].Text);
            Assert.AreEqual(FloatKind.Dodged, plan.Floats[0].Kind);
        }

        [Test]
        public void Stealth_immunity_does_not_show_the_word_dodge()
        {
            // 游荡者·刺客潜行时是"打不到"，不该说成"闪避"
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Attack8,
                WasDodged = true,
                DodgeReason = "目标在潜行中，无法被攻击",
                DodgedHits = 1,
                HitCount = 1,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(0, plan.Floats.Count, "潜行免伤不该飘'闪避'。");
        }

        [Test]
        public void Heal_shows_on_the_healed_player_not_the_actor()
        {
            // 牧师·生命领域可以把 9 回给对手 —— 必须飘在**被回复者**头上
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Heal,
                Healed = 9,
                HealTarget = _b,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(1, plan.Floats.Count);
            Assert.AreEqual("乙", plan.Floats[0].Target.Name, "应飘在被回复者头上。");
            Assert.AreEqual("+9", plan.Floats[0].Text);
            Assert.AreEqual(FloatKind.Heal, plan.Floats[0].Kind);
        }

        [Test]
        public void Wasted_full_health_heal_shows_plus_zero()
        {
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Heal,
                Healed = 0,
                HealTarget = _a,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(1, plan.Floats.Count);
            Assert.AreEqual("+0", plan.Floats[0].Text, "满血回复也应让玩家看见。");
        }

        // ============================================================
        // 错开
        // ============================================================

        [Test]
        public void Multiple_floats_are_staggered_by_a_quarter_second()
        {
            // 武僧·忍宗两段 + 战士·剑士追加可能一次冒出多条 -> 依次错开
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Attack8,
                ExtraAttack = new ExtraAttackInfo("甲", 6, 6, false),
                DamageDealt = 6,
                LandedHits = 2,
                HitCount = 3,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            // 追加 1 段 + 主攻击 2 段 = 3 条飘字
            Assert.AreEqual(3, plan.Floats.Count, "追加一次 + 主攻击两段。");
            Assert.AreEqual(0f, plan.Floats[0].DelaySeconds, "第一条立即播。");
            Assert.AreEqual(AnimationDirector.StaggerSeconds, plan.Floats[1].DelaySeconds,
                "第二条错开 0.25 秒。");
            Assert.AreEqual(AnimationDirector.StaggerSeconds, plan.Floats[2].DelaySeconds,
                "第三条也错开 0.25 秒。");
        }

        [Test]
        public void Mixed_results_are_ordered_and_staggered()
        {
            // 追击被挡 + 主攻击命中 + 自身回复（构造一个混合结算）
            var result = new TurnResult
            {
                Actor = _a,
                Effect = HandEffectType.Attack8,
                ExtraAttack = new ExtraAttackInfo("甲", 6, 0, true),
                DamageDealt = 8,
                LandedHits = 1,
                BlockedHits = 0,
                HitCount = 1,
                Healed = 9,
                HealTarget = _a,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(3, plan.Floats.Count);
            Assert.AreEqual("格挡", plan.Floats[0].Text, "追加被挡先播。");
            Assert.AreEqual("-8", plan.Floats[1].Text, "再播主攻击伤害。");
            Assert.AreEqual("+9", plan.Floats[2].Text, "最后播回复。");

            // 延迟是"相对前一条"的累加值
            Assert.AreEqual(0f, plan.Floats[0].DelaySeconds);
            Assert.AreEqual(0.25f, plan.Floats[1].DelaySeconds);
            Assert.AreEqual(0.25f, plan.Floats[2].DelaySeconds);

            // 前两条飘在受击者头上、最后一条飘回自己头上
            Assert.AreEqual("乙", plan.Floats[0].Target.Name);
            Assert.AreEqual("乙", plan.Floats[1].Target.Name);
            Assert.AreEqual("甲", plan.Floats[2].Target.Name);
        }

        // ============================================================
        // 打击感：分档、错开、停顿
        // ============================================================

        [Test]
        public void Impact_tier_follows_the_per_hit_damage()
        {
            Assert.AreEqual(HitTier.None, AnimationDirector.TierFor(0));
            Assert.AreEqual(HitTier.Light, AnimationDirector.TierFor(2));
            Assert.AreEqual(HitTier.Medium, AnimationDirector.TierFor(3));
            Assert.AreEqual(HitTier.Medium, AnimationDirector.TierFor(5));
            Assert.AreEqual(HitTier.Heavy, AnimationDirector.TierFor(6));
            Assert.AreEqual(HitTier.Heavy, AnimationDirector.TierFor(8));
        }

        [Test]
        public void Shake_grows_with_the_tier()
        {
            float light = AnimationDirector.ShakeMagnitudeFor(HitTier.Light);
            float medium = AnimationDirector.ShakeMagnitudeFor(HitTier.Medium);
            float heavy = AnimationDirector.ShakeMagnitudeFor(HitTier.Heavy);
            float none = AnimationDirector.ShakeMagnitudeFor(HitTier.None);

            Assert.AreEqual(0f, none, "没命中就不该震。");
            Assert.True(light < medium, $"{light} 应小于 {medium}");
            Assert.True(medium < heavy, $"{medium} 应小于 {heavy}");
        }

        [Test]
        public void No_impact_when_the_attack_was_blocked()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack8,
                WasBlocked = true, BlockedHits = 1, HitCount = 1,
                DamageDealt = 0,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(0, plan.Impacts.Count, "被挡下不该震屏。");
        }

        [Test]
        public void No_impact_when_the_attack_was_dodged()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack4,
                WasDodged = true, DodgedHits = 1, HitCount = 1,
                DamageDealt = 0,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(0, plan.Impacts.Count, "被闪避不该震屏。");
        }

        [Test]
        public void Heavy_hit_produces_a_heavy_shake()
        {
            AnimationPlan plan = AnimationDirector.Build(Attack(8), _b);

            Assert.AreEqual(1, plan.Impacts.Count);
            Assert.AreEqual(HitTier.Heavy, plan.Impacts[0].Tier);
            Assert.AreEqual(AnimationDirector.ShakeMagnitudeFor(HitTier.Heavy),
                plan.Impacts[0].ShakeMagnitude);
            Assert.AreEqual("乙", plan.Impacts[0].Target.Name);
        }

        [Test]
        public void Multi_hit_attacks_shake_once_per_segment()
        {
            // 武僧·忍宗两段各 3 点 -> 两次"中"档震动，依次错开
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack4,
                DamageDealt = 6, LandedHits = 2, HitCount = 2,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(2, plan.Impacts.Count, "每段各震一次。");
            Assert.AreEqual(0f, plan.Impacts[0].DelaySeconds);
            Assert.AreEqual(AnimationDirector.StaggerSeconds, plan.Impacts[1].DelaySeconds);
        }

        [Test]
        public void Swordman_follow_up_shakes_separately_from_the_main_hit()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack6,
                ExtraAttack = new ExtraAttackInfo("战士·剑士", 6, 6, false),
                DamageDealt = 8, LandedHits = 1, HitCount = 2,
            };

            AnimationPlan plan = AnimationDirector.Build(result, _b);

            Assert.AreEqual(2, plan.Impacts.Count, "追加算独立一次打击。");
            Assert.AreEqual(HitTier.Heavy, plan.Impacts[0].Tier, "追加 6 点是重击。");
            Assert.AreEqual(HitTier.Heavy, plan.Impacts[1].Tier, "主攻击 8 点也是重击。");
        }

        [Test]
        public void Pause_is_dynamic_between_a_quarter_and_a_half_second()
        {
            Assert.AreEqual(0.25f, AnimationDirector.PauseFor(0), "无伤害取最短。");
            Assert.AreEqual(0.5f, AnimationDirector.PauseFor(8), "8 点伤害取最长。");
            Assert.AreEqual(0.5f, AnimationDirector.PauseFor(20), "超过上限也取最长。");

            // 中间值应严格位于两端之间，且随伤害单调
            float p3 = AnimationDirector.PauseFor(3);
            float p6 = AnimationDirector.PauseFor(6);
            Assert.True(p3 > 0.25f && p3 < 0.5f, $"3 点应在中间：{p3}");
            Assert.True(p6 > p3, $"伤害越高停越久：{p6} vs {p3}");
        }

        [Test]
        public void Plan_carries_pause_and_impacts_together()
        {
            AnimationPlan plan = AnimationDirector.Build(Attack(8), _b);

            Assert.AreEqual(0.5f, plan.PauseSeconds);
            Assert.AreEqual(1, plan.Floats.Count);
            Assert.AreEqual(1, plan.Impacts.Count);
            Assert.True(plan.Describe().Contains("停顿"), plan.Describe());
        }
        [Test]
        public void Empty_result_produces_no_floats()
        {
            Assert.True(AnimationDirector.Build(null, _b).IsEmpty);
            Assert.True(AnimationDirector.Build(new TurnResult(), _b).IsEmpty);
        }

        [Test]
        public void Describe_lists_the_plan_for_inspection()
        {
            // 便于人工核对：把计划打印出来看
            AnimationPlan plan = AnimationDirector.Build(Attack(8), _b);
            string text = plan.Describe();

            Assert.True(text.Contains("乙"), $"应包含目标名：{text}");
            Assert.True(text.Contains("-8"), $"应包含飘字内容：{text}");
        }
    }
}
