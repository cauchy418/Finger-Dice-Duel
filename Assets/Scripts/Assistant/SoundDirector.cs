using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>音效种类。</summary>
    public enum SfxKind
    {
        /// <summary>轻打击。</summary>
        HitLight = 0,

        /// <summary>中打击。</summary>
        HitMedium = 1,

        /// <summary>重打击。</summary>
        HitHeavy = 2,

        /// <summary>被护盾挡下。</summary>
        Blocked = 3,

        /// <summary>闪避。</summary>
        Dodged = 4,

        /// <summary>回复。</summary>
        Heal = 5,

        /// <summary>合拢手掌形成护盾。</summary>
        ShieldUp = 6,

        /// <summary>回合切换。</summary>
        TurnSwitch = 7,
    }

    /// <summary>
    /// 一条音效的**合成参数**（纯数据）。
    ///
    /// 为什么把参数独立出来：这样"该播哪个音、音高多少"可以在无头测试里验证，
    /// 而实际采样合成（需要 UnityEngine 的 AudioClip）留在 UI 层。
    /// 与飘字 / 血条 / 打击感是同一套分层思路。
    /// </summary>
    public readonly struct SfxSpec
    {
        public SfxKind Kind { get; }

        /// <summary>起始频率（Hz）。</summary>
        public float StartFrequency { get; }

        /// <summary>结束频率（Hz）。与起始不同即为滑音，听感更"有动作"。</summary>
        public float EndFrequency { get; }

        /// <summary>时长（秒）。</summary>
        public float Duration { get; }

        /// <summary>音量（0~1）。</summary>
        public float Volume { get; }

        /// <summary>相对触发时刻的延迟（秒），用于多段攻击依次响。</summary>
        public float DelaySeconds { get; }

        public SfxSpec(SfxKind kind, float startFrequency, float endFrequency,
                       float duration, float volume, float delaySeconds = 0f)
        {
            Kind = kind;
            StartFrequency = startFrequency;
            EndFrequency = endFrequency;
            Duration = duration;
            Volume = volume;
            DelaySeconds = delaySeconds;
        }

        public override string ToString()
            => $"{Kind} {StartFrequency:0}->{EndFrequency:0}Hz {Duration:0.00}s vol{Volume:0.00} +{DelaySeconds:0.00}s";
    }

    /// <summary>
    /// 把一次结算翻译成"该响哪些音效"。
    ///
    /// 与 <see cref="AnimationDirector"/> 平行：那边管画面，这边管声音，
    /// 两者共用同一份 <see cref="TurnResult"/>，所以节奏天然一致。
    /// </summary>
    public static class SoundDirector
    {
        /// <summary>同一次结算里，相邻音效的错开间隔（与飘字、打击感一致）。</summary>
        public const float StaggerSeconds = AnimationDirector.StaggerSeconds;

        /// <summary>各档打击音的音高：越重越低沉。</summary>
        public static float HitFrequencyFor(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light: return 420f;
                case HitTier.Medium: return 320f;
                case HitTier.Heavy: return 220f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 为一次结算生成音效序列。
        ///
        /// 规则：
        ///   · 每段命中的伤害 -> 一次打击音，音量与音高按伤害分档
        ///   · 被盾挡 / 被闪避 -> 各自的音
        ///   · 回复 -> 上行音（+9 用）
        ///   · 合盾 -> 闷响
        ///   · 多段之间依次错开 <see cref="StaggerSeconds"/> 秒，与画面同步
        /// </summary>
        public static System.Collections.Generic.List<SfxSpec> Build(TurnResult result)
        {
            var list = new System.Collections.Generic.List<SfxSpec>(4);
            if (result == null) return list;

            // ---- 追加攻击（剑客）：算独立一次 ----
            if (result.ExtraAttack.HasValue)
            {
                ExtraAttackInfo extra = result.ExtraAttack.Value;
                if (extra.WasBlocked) Add(list, SfxKind.Blocked);
                else if (extra.Dealt > 0) AddHit(list, extra.Dealt);
            }

            // ---- 闪避：每段各一声 ----
            if (result.WasDodged && string.IsNullOrEmpty(result.DodgeReason))
            {
                int dodged = result.DodgedHits > 0 ? result.DodgedHits : 1;
                for (int i = 0; i < dodged; i++) Add(list, SfxKind.Dodged);
            }

            // ---- 格挡：每段各一声 ----
            for (int i = 0; i < result.BlockedHits; i++) Add(list, SfxKind.Blocked);

            // ---- 伤害：每段各一声，音高按**单段**伤害分档 ----
            if (result.LandedHits > 0 && result.DamageDealt > 0)
            {
                int perHit = result.DamageDealt / result.LandedHits;
                for (int i = 0; i < result.LandedHits; i++) AddHit(list, perHit);
            }

            // ---- 回复 ----
            if (result.Healed > 0) Add(list, SfxKind.Heal);

            // ---- 合盾（没有攻击/回复时才是本回合的主效果）----
            if (list.Count == 0 && result.Effect == HandEffectType.Shield)
                Add(list, SfxKind.ShieldUp);

            return list;
        }

        /// <summary>
        /// 各档打击音的音量。
        ///
        /// 用查表而不是公式：公式版（0.35 + 0.20 * 档位）会给出 0.95，
        /// 乘上主音量后逼近削波，重击会"炸"而不是"沉"。
        /// </summary>
        public static float HitVolumeFor(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light: return 0.36f;
                case HitTier.Medium: return 0.52f;
                case HitTier.Heavy: return 0.68f;
                default: return 0f;
            }
        }

        private static void AddHit(System.Collections.Generic.List<SfxSpec> into, int damage)
        {
            HitTier tier = AnimationDirector.TierFor(damage);
            if (tier == HitTier.None) return;

            SfxKind kind = tier == HitTier.Light ? SfxKind.HitLight
                         : tier == HitTier.Medium ? SfxKind.HitMedium
                         : SfxKind.HitHeavy;

            float freq = HitFrequencyFor(tier);
            float duration = AnimationDirector.ShakeDurationFor(tier);

            Add(into, kind, freq, freq * 0.55f, duration, HitVolumeFor(tier));
        }

        private static void Add(System.Collections.Generic.List<SfxSpec> into, SfxKind kind)
            => Add(into, kind, 0f, 0f, 0f, 0f);

        private static void Add(System.Collections.Generic.List<SfxSpec> into, SfxKind kind,
                                float startFreq, float endFreq, float duration, float volume)
        {
            // 未指定频率的音效走各自的默认参数
            if (startFreq <= 0f)
            {
                switch (kind)
                {
                    case SfxKind.Blocked:
                        startFreq = 150f; endFreq = 110f; duration = 0.12f; volume = 0.30f;
                        break;
                    case SfxKind.Dodged:
                        startFreq = 700f; endFreq = 1100f; duration = 0.10f; volume = 0.22f;
                        break;
                    case SfxKind.Heal:
                        startFreq = 520f; endFreq = 900f; duration = 0.28f; volume = 0.30f;
                        break;
                    case SfxKind.ShieldUp:
                        startFreq = 240f; endFreq = 180f; duration = 0.16f; volume = 0.28f;
                        break;
                    case SfxKind.TurnSwitch:
                        startFreq = 600f; endFreq = 760f; duration = 0.09f; volume = 0.18f;
                        break;
                    default:
                        startFreq = 400f; endFreq = 300f; duration = 0.12f; volume = 0.25f;
                        break;
                }
            }

            float delay = into.Count == 0 ? 0f : StaggerSeconds;
            into.Add(new SfxSpec(kind, startFreq, endFreq, duration, volume, delay));
        }
    }
}
