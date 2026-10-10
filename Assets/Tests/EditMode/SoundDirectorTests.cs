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
    /// 音效**参数**的验证。
    ///
    /// 实际采样合成需要 UnityEngine 的 AudioClip，无头测试跑不了；
    /// 但"该响哪个音、音高多少、节奏对不对"是纯数据，可以精确验证。
    /// 这也是把 <see cref="SoundDirector"/> 与播放层分开的原因。
    /// </summary>
    [TestFixture]
    public class SoundDirectorTests
    {
        private PlayerState _a;
        private PlayerState _b;

        [SetUp]
        public void SetUp()
        {
            _a = new PlayerState("甲");
            _b = new PlayerState("乙");
        }

        [Test]
        public void A_light_hit_plays_a_higher_note_than_a_heavy_hit()
        {
            // 越重的打击越低沉，这是打击感的常识
            float light = SoundDirector.HitFrequencyFor(HitTier.Light);
            float medium = SoundDirector.HitFrequencyFor(HitTier.Medium);
            float heavy = SoundDirector.HitFrequencyFor(HitTier.Heavy);

            Assert.True(light > medium, $"轻击 {light} 应高于中击 {medium}。");
            Assert.True(medium > heavy, $"中击 {medium} 应高于重击 {heavy}。");
            Assert.AreEqual(0f, SoundDirector.HitFrequencyFor(HitTier.None));
        }

        [Test]
        public void A_heavy_hit_is_louder_than_a_light_hit()
        {
            var light = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack4,
                DamageDealt = 2, LandedHits = 1, HitCount = 1,
            };
            var heavy = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack8,
                DamageDealt = 8, LandedHits = 1, HitCount = 1,
            };

            List<SfxSpec> lightSfx = SoundDirector.Build(light);
            List<SfxSpec> heavySfx = SoundDirector.Build(heavy);

            Assert.AreEqual(SfxKind.HitLight, lightSfx[0].Kind);
            Assert.AreEqual(SfxKind.HitHeavy, heavySfx[0].Kind);
            Assert.True(heavySfx[0].Volume > lightSfx[0].Volume,
                $"重击应更响：{heavySfx[0].Volume} vs {lightSfx[0].Volume}。");
        }

        [Test]
        public void A_blocked_hit_plays_the_block_sound()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack8,
                WasBlocked = true, BlockedHits = 1, HitCount = 1,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(1, sfx.Count);
            Assert.AreEqual(SfxKind.Blocked, sfx[0].Kind);
        }

        [Test]
        public void A_dodge_plays_the_dodge_sound()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack4,
                WasDodged = true, DodgedHits = 1, HitCount = 1,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(1, sfx.Count);
            Assert.AreEqual(SfxKind.Dodged, sfx[0].Kind);
            Assert.True(sfx[0].EndFrequency > sfx[0].StartFrequency,
                "闪避应是上扬音（'嗖'一下躲开）。");
        }

        [Test]
        public void A_heal_plays_a_rising_note()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Heal,
                Healed = 9, HealTarget = _a,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(1, sfx.Count);
            Assert.AreEqual(SfxKind.Heal, sfx[0].Kind);
            Assert.True(sfx[0].EndFrequency > sfx[0].StartFrequency,
                "回复应是上行音（听起来'变好'）。");
        }

        [Test]
        public void A_shield_turn_plays_the_shield_sound()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Shield,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(1, sfx.Count);
            Assert.AreEqual(SfxKind.ShieldUp, sfx[0].Kind);
        }

        [Test]
        public void Multi_hit_attacks_play_one_sound_per_segment_staggered()
        {
            // 忍者两段各 3 点 -> 两声"中击"，依次错开
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack4,
                DamageDealt = 6, LandedHits = 2, HitCount = 2,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(2, sfx.Count, "每段各一声。");
            Assert.AreEqual(SfxKind.HitMedium, sfx[0].Kind);
            Assert.AreEqual(SfxKind.HitMedium, sfx[1].Kind);
            Assert.AreEqual(0f, sfx[0].DelaySeconds);
            Assert.AreEqual(SoundDirector.StaggerSeconds, sfx[1].DelaySeconds);
        }

        [Test]
        public void Swordsman_follow_up_sounds_separately_from_the_main_hit()
        {
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack6,
                ExtraAttack = new ExtraAttackInfo("剑客", 6, 6, false),
                DamageDealt = 8, LandedHits = 1, HitCount = 2,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(2, sfx.Count, "追加算独立一次打击。");
            Assert.AreEqual(SfxKind.HitHeavy, sfx[0].Kind, "追加 6 点是重击。");
            Assert.AreEqual(SfxKind.HitHeavy, sfx[1].Kind, "主攻击 8 点也是重击。");
        }

        [Test]
        public void Dodge_reason_from_stealth_does_not_play_the_dodge_sound()
        {
            // 刺客潜行时是"打不到"，不该响"闪避"音
            var result = new TurnResult
            {
                Actor = _a, Effect = HandEffectType.Attack8,
                WasDodged = true, DodgeReason = "目标在潜行中，无法被攻击",
                DodgedHits = 1, HitCount = 1,
            };

            List<SfxSpec> sfx = SoundDirector.Build(result);

            Assert.AreEqual(0, sfx.Count, "潜行免伤不该响闪避音。");
        }

        [Test]
        public void No_damage_turn_stays_quiet()
        {
            var result = new TurnResult { Actor = _a, Effect = HandEffectType.None };

            Assert.AreEqual(0, SoundDirector.Build(result).Count);
            Assert.AreEqual(0, SoundDirector.Build(null).Count);
        }

        [Test]
        public void Every_spec_has_audible_parameters()
        {
            // 任何一条音效都不能是"零时长 / 零音量"的哑音
            var results = new List<TurnResult>
            {
                new TurnResult { Actor = _a, Effect = HandEffectType.Attack4, DamageDealt = 2, LandedHits = 1 },
                new TurnResult { Actor = _a, Effect = HandEffectType.Attack8, DamageDealt = 8, LandedHits = 1 },
                new TurnResult { Actor = _a, Effect = HandEffectType.Attack8, WasBlocked = true, BlockedHits = 1 },
                new TurnResult { Actor = _a, Effect = HandEffectType.Attack4, WasDodged = true, DodgedHits = 1 },
                new TurnResult { Actor = _a, Effect = HandEffectType.Heal, Healed = 9, HealTarget = _a },
                new TurnResult { Actor = _a, Effect = HandEffectType.Shield },
            };

            foreach (TurnResult r in results)
            {
                foreach (SfxSpec spec in SoundDirector.Build(r))
                {
                    Assert.True(spec.Duration > 0f, $"{spec.Kind} 时长应为正。");
                    Assert.True(spec.Volume > 0f && spec.Volume <= 1f,
                        $"{spec.Kind} 音量应在 (0,1]，实际 {spec.Volume}。");
                    Assert.True(spec.StartFrequency > 0f, $"{spec.Kind} 起始频率应为正。");
                    Assert.True(spec.EndFrequency > 0f, $"{spec.Kind} 结束频率应为正。");
                }
            }
        }

        [Test]
        public void No_sound_is_loud_enough_to_clip()
        {
            // 乘上主音量（0.7）后仍要留出余量，否则重击会"炸"而不是"沉"
            foreach (HitTier tier in new[] { HitTier.Light, HitTier.Medium, HitTier.Heavy })
            {
                float v = SoundDirector.HitVolumeFor(tier);
                Assert.True(v > 0f && v <= 0.75f,
                    $"{tier} 的音量 {v} 过高，乘主音量后可能削波。");
            }

            Assert.AreEqual(0f, SoundDirector.HitVolumeFor(HitTier.None));
        }

        [Test]
        public void Hit_volume_grows_with_the_tier()
        {
            Assert.True(SoundDirector.HitVolumeFor(HitTier.Light)
                        < SoundDirector.HitVolumeFor(HitTier.Medium));
            Assert.True(SoundDirector.HitVolumeFor(HitTier.Medium)
                        < SoundDirector.HitVolumeFor(HitTier.Heavy));
        }

        [Test]
        public void Sound_and_visual_stagger_use_the_same_rhythm()
        {
            // 画面与声音必须同拍，否则会"声画不同步"
            Assert.AreEqual(AnimationDirector.StaggerSeconds, SoundDirector.StaggerSeconds);
        }
    }
}
