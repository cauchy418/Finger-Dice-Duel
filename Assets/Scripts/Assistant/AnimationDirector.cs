using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>飘字的种类（决定文案与颜色）。</summary>
    public enum FloatKind
    {
        /// <summary>造成伤害："-8"，红色。</summary>
        Damage = 0,

        /// <summary>被护盾挡下："格挡"，灰白。</summary>
        Blocked = 1,

        /// <summary>游荡者·风流剑客闪避："闪避"，青色。</summary>
        Dodged = 2,

        /// <summary>回复："+9"，绿色。</summary>
        Heal = 3,
    }

    /// <summary>
    /// 一条飘字指令（纯数据，不依赖 UnityEngine，因此可以在无头测试里验证）。
    /// </summary>
    public readonly struct FloatTextSpec
    {
        /// <summary>飘在哪位玩家的**名字那一行**（血条以后也放这里）。</summary>
        public PlayerState Target { get; }

        /// <summary>显示的文字。</summary>
        public string Text { get; }

        public FloatKind Kind { get; }

        /// <summary>
        /// **本条飘字的颜色覆盖**（0 = 不覆盖，仍按 <see cref="Kind"/> 取默认色）。
        ///
        /// ⚠️ 玩家要求："每次攻击造成的伤害的颜色（包括被盾牌抵挡）
        ///    都由本次攻击的类型决定（和攻击类型图标一致）"。
        ///    所以伤害与格挡那几条会带上攻击类型的颜色 ——
        ///    颜色只在 Core 的 <see cref="AttackDamageTypes.ColorOf"/> 里有一份，
        ///    与图标同源，结构上保证一致。
        /// </summary>
        public int ColorRgb { get; }

        /// <summary>相对前一条飘字的延迟（秒）。同一次结算里的飘字会依次错开。</summary>
        public float DelaySeconds { get; }

        public FloatTextSpec(PlayerState target, string text, FloatKind kind, float delaySeconds)
            : this(target, text, kind, delaySeconds, 0)
        {
        }

        public FloatTextSpec(PlayerState target, string text, FloatKind kind,
                             float delaySeconds, int colorRgb)
        {
            Target = target;
            Text = text ?? "";
            Kind = kind;
            DelaySeconds = delaySeconds;
            ColorRgb = colorRgb;
        }

        public override string ToString()
            => $"{Target?.Name} \"{Text}\" ({Kind}) +{DelaySeconds:0.00}s";
    }

    /// <summary>
    /// 一次结算要播放的完整表现（目前只有飘字）。
    /// </summary>
    public sealed class AnimationPlan
    {
        public IReadOnlyList<FloatTextSpec> Floats { get; }

        /// <summary>打击感指令（受击闪烁 + 屏幕震动），按段依次错开。</summary>
        public IReadOnlyList<HitImpactSpec> Impacts { get; }

        /// <summary>本次结算建议的"回合间停顿"（秒），按伤害量动态 0.25~0.5。</summary>
        public float PauseSeconds { get; }

        /// <summary>
        /// 生成这个计划的原始结算结果。
        ///
        /// 音效层（<see cref="SoundDirector"/>）需要它来生成音效序列 ——
        /// 画面与声音共用同一份 <see cref="TurnResult"/>、同一套错开常量，
        /// 所以节奏天然一致，不会出现"声音比画面早半拍"。
        /// </summary>
        public TurnResult SoundSource { get; }

        public AnimationPlan(IReadOnlyList<FloatTextSpec> floats,
                             IReadOnlyList<HitImpactSpec> impacts = null,
                             float pauseSeconds = 0f,
                             TurnResult soundSource = null)
        {
            Floats = floats ?? new List<FloatTextSpec>(0);
            Impacts = impacts ?? new List<HitImpactSpec>(0);
            PauseSeconds = pauseSeconds;
            SoundSource = soundSource;
        }

        public bool IsEmpty => Floats.Count == 0 && Impacts.Count == 0;

        /// <summary>便于测试与排查：把播放计划打印成多行。</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"停顿 {PauseSeconds:0.00}s | 飘字 {Floats.Count} 条 | 打击感 {Impacts.Count} 条");

            for (int i = 0; i < Floats.Count; i++) sb.Append("\n  ").Append(Floats[i]);
            for (int i = 0; i < Impacts.Count; i++) sb.Append("\n  ").Append(Impacts[i]);

            return sb.ToString();
        }
    }

    /// <summary>打击感强度分档。</summary>
    public enum HitTier
    {
        /// <summary>没有命中（被挡 / 被闪 / 无伤害）。</summary>
        None = 0,

        /// <summary>轻：1~3 点。</summary>
        Light = 1,

        /// <summary>中：4~5 点。</summary>
        Medium = 2,

        /// <summary>重：6 点以上。</summary>
        Heavy = 3,
    }

    /// <summary>
    /// 一条"打击感"指令：哪一方的牌要闪、屏幕震多厉害、延迟多久。
    ///
    /// 与飘字一样是**纯数据**，所以"该震几级、按什么节奏"可以在无头测试里验证。
    /// </summary>
    public readonly struct HitImpactSpec
    {
        /// <summary>被击中的一方（它的手牌要闪）。</summary>
        public PlayerState Target { get; }

        public HitTier Tier { get; }

        /// <summary>屏幕震动幅度（像素）。0 表示不震。</summary>
        public float ShakeMagnitude { get; }

        /// <summary>相对前一条的延迟（秒）。多段之间依次错开。</summary>
        public float DelaySeconds { get; }

        public HitImpactSpec(PlayerState target, HitTier tier, float shakeMagnitude, float delaySeconds)
        {
            Target = target;
            Tier = tier;
            ShakeMagnitude = shakeMagnitude;
            DelaySeconds = delaySeconds;
        }

        public override string ToString()
            => $"{Target?.Name} {Tier} 震动 {ShakeMagnitude:0.#}px +{DelaySeconds:0.00}s";
    }

    /// <summary>
    /// 把一次结算（<see cref="TurnResult"/>）翻译成"要播哪些表现"。
    ///
    /// 为什么单独一层：
    ///   · UI 的 <c>Refresh</c> 只画**最终状态**，没有任何"从 A 到 B 的过程"，
    ///     动画无处下手；
    ///   · 动画逻辑一旦塞进 MonoBehaviour，就只能靠肉眼验证。
    /// 把"该飘什么字、飘在谁头上、间隔多久"抽成**纯数据**后，
    /// 这一层可以用与规则层同一套秒级测试来验证。
    ///
    /// 它不引用 UnityEngine，符合项目的依赖方向（Assistant → Core）。
    /// </summary>
    public static class AnimationDirector
    {
        /// <summary>同一次结算里，相邻两条飘字的间隔（秒）。</summary>
        public const float StaggerSeconds = 0.25f;

        /// <summary>
        /// 为一次结算生成播放计划。
        ///
        /// 飘字规则（已与需求方确认）：
        ///   · 伤害 → "-3"（红），飘在**受击者**名字行；**每一段各飘一条**
        ///   · 被盾挡 → "格挡"（灰白），**每一段各飘一条**
        ///   · 游荡者·风流剑客闪避 → "闪避"（青），**每一段各飘一条**
        ///   · 回复 → "+9"（绿），飘在**被回复者**名字行（牧师·生命领域可能回给对手）
        ///   · 同一次结算的多条飘字依次错开 <see cref="StaggerSeconds"/> 秒
        ///
        /// 为什么要按**段数**而不是按总伤害生成：武僧·忍宗一次攻击是两段、
        /// 战士·剑士的追加算独立一次 —— 玩家需要看出"打了几下"。
        /// 如果按总数只飘一条 "-6"，就看不出那是两段各 3 点。
        ///
        /// 顺序约定：闪避 → 格挡 → 伤害。多段之间"哪一段被挡、哪一段命中"
        /// 的原始顺序没有单独记录，所以用这个固定顺序，保证结果稳定可测。
        /// </summary>
        /// <param name="result">本次结算结果（可为 null，返回空计划）。</param>
        /// <param name="defender">受击方（用来确定飘字位置）。</param>
        public static AnimationPlan Build(TurnResult result, PlayerState defender)
            => Build(result, defender, AttackDamageType.None);

        /// <summary>
        /// 同上，但带上**本次攻击的伤害类型** —— 伤害与"格挡"两条飘字
        /// 会用该类型的颜色（与攻击类型图标一致）。
        ///
        /// ⚠️ 玩家要求："每次攻击造成的伤害的颜色（包括被盾牌抵挡）
        ///    都由本次攻击的类型决定（和攻击类型图标一致）"。
        /// </summary>
        public static AnimationPlan Build(TurnResult result, PlayerState defender,
                                          AttackDamageType attackType)
        {
            // 0 = 不覆盖颜色（未知类型时保留旧的默认色，避免整片变灰）
            int typeColor = attackType == AttackDamageType.None
                ? 0 : AttackDamageTypes.ColorOf(attackType);

            var floats = new List<FloatTextSpec>(4);
            if (result == null || result.Actor == null) return new AnimationPlan(floats);

            if (defender == null) defender = result.Actor;

            // ---- 追加攻击（战士·剑士）：算独立一次，先播 ----
            if (result.ExtraAttack.HasValue)
            {
                ExtraAttackInfo extra = result.ExtraAttack.Value;
                Add(floats, defender,
                    extra.WasBlocked ? "格挡" : "-" + extra.Dealt,
                    extra.WasBlocked ? FloatKind.Blocked : FloatKind.Damage,
                    typeColor);
            }

            // ---- 闪避：每段各一条 ----
            // 游荡者·刺客的潜行免伤复用同一条通道，但那是"打不到"，不该说成"闪避"
            if (result.WasDodged && string.IsNullOrEmpty(result.DodgeReason))
            {
                int dodged = result.DodgedHits > 0 ? result.DodgedHits : 1;
                for (int i = 0; i < dodged; i++) Add(floats, defender, "闪避", FloatKind.Dodged);
            }

            // ---- 格挡：每段各一条 ----
            for (int i = 0; i < result.BlockedHits; i++)
                Add(floats, defender, "格挡", FloatKind.Blocked, typeColor);

            // ---- 伤害：每段各一条，数值是**单段**伤害 ----
            if (result.LandedHits > 0 && result.DamageDealt > 0)
            {
                int perHit = result.DamageDealt / result.LandedHits;
                for (int i = 0; i < result.LandedHits; i++)
                    Add(floats, defender, "-" + perHit, FloatKind.Damage, typeColor);
            }

            // ---- 回复：飘在**被回复者**头上 ----
            if (result.Healed > 0)
            {
                PlayerState healed = result.HealTarget ?? result.Actor;
                Add(floats, healed, "+" + result.Healed, FloatKind.Heal);
            }
            else if (result.Effect == HandEffectType.Heal && result.HealTarget != null)
            {
                // 满血回复：没有实际回血，但要让玩家看见"这一回合被浪费了"
                Add(floats, result.HealTarget, "+0", FloatKind.Heal);
            }

            // ---- 打击感：与飘字一样**按段**生成，多段之间依次错开 ----
            var impacts = new List<HitImpactSpec>(4);

            if (result.ExtraAttack.HasValue)
            {
                ExtraAttackInfo extra = result.ExtraAttack.Value;
                if (!extra.WasBlocked && extra.Dealt > 0)
                    AddImpact(impacts, defender, extra.Dealt);
            }

            if (result.LandedHits > 0 && result.DamageDealt > 0)
            {
                int perHit = result.DamageDealt / result.LandedHits;
                for (int i = 0; i < result.LandedHits; i++) AddImpact(impacts, defender, perHit);
            }

            return new AnimationPlan(floats, impacts, PauseFor(result.DamageDealt), result);
        }

        /// <summary>
        /// 打击感强度分档：按**单段**伤害量。
        ///   1~2 → 轻、3~5 → 中、6+ → 重。
        /// </summary>
        public static HitTier TierFor(int damage)
        {
            if (damage <= 0) return HitTier.None;
            if (damage <= 2) return HitTier.Light;
            if (damage <= 5) return HitTier.Medium;
            return HitTier.Heavy;
        }

        /// <summary>
        /// 各档的屏幕震动幅度（像素）。
        ///
        /// 第一版是 6 / 12 / 20，实机反馈"不明显"，所以整体放大到 12 / 24 / 40。
        /// </summary>
        public static float ShakeMagnitudeFor(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light: return 12f;
                case HitTier.Medium: return 24f;
                case HitTier.Heavy: return 40f;
                default: return 0f;
            }
        }

        /// <summary>震动持续时长（秒）。档位越高震得越久。</summary>
        public static float ShakeDurationFor(HitTier tier)
        {
            switch (tier)
            {
                case HitTier.Light: return 0.14f;
                case HitTier.Medium: return 0.20f;
                case HitTier.Heavy: return 0.28f;
                default: return 0f;
            }
        }

        /// <summary>
        /// 回合间停顿：按伤害量动态 0.25 ~ 0.5 秒。
        ///
        /// 为什么需要：伤害是瞬间算完的，画面会"啪"一下跳到结果。
        /// 停这么一下，玩家才看得清震屏与飘字，而不是还没反应就轮到对面了。
        /// </summary>
        public static float PauseFor(int damageDealt)
        {
            const float min = 0.25f;
            const float max = 0.5f;

            // 4 点伤害对应 0.35s（(min+max)/2），大致线性
            float t = damageDealt / 8f;
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;

            return min + (max - min) * t;
        }

        /// <summary>
        /// 加一条打击感。延迟与同批的其他条目错开（与飘字同一节奏）。
        /// 没有命中（伤害 0）时不加 —— 被挡 / 被闪不该震屏。
        /// </summary>
        private static void AddImpact(List<HitImpactSpec> into, PlayerState target, int damage)
        {
            HitTier tier = TierFor(damage);
            if (tier == HitTier.None) return;

            float delay = into.Count == 0 ? 0f : StaggerSeconds;
            into.Add(new HitImpactSpec(target, tier, ShakeMagnitudeFor(tier), delay));
        }

        /// <summary>
        /// 血量比例（0..1），夹紧到合法范围。
        ///
        /// 上限为 0 的边界、超出上限的回复等都要在这里归一，
        /// 否则血条会画成负宽度或溢出底槽。
        /// </summary>
        public static float HealthRatio(int health, int maxHealth)
        {
            if (maxHealth <= 0) return 0f;
            if (health <= 0) return 0f;

            float ratio = (float)health / maxHealth;
            return ratio > 1f ? 1f : ratio;
        }

        /// <summary>血条填充宽度 = 满宽 × 血量比例。</summary>
        public static float HealthBarFillWidth(float fullWidth, int health, int maxHealth)
            => fullWidth * HealthRatio(health, maxHealth);

        /// <summary>
        /// 把"画布本地坐标"（左下角为原点、向上为正）换算成
        /// "以画布中心为原点的 anchoredPosition"。
        ///
        /// 为什么放在这里：UI 层的同名方法需要 <c>UnityEngine.Rect</c>，
        /// 而无头测试编译不过 UnityEngine。**飘字落点对不对**是值得验证的事，
        /// 所以核心换算放在这个不依赖 Unity 的层，UI 只做一层薄包装。
        /// </summary>
        public static void CanvasLocalToAnchored(float canvasWidth, float canvasHeight,
                                                 float localX, float localY,
                                                 out float anchoredX, out float anchoredY)
        {
            anchoredX = localX - canvasWidth * 0.5f;
            anchoredY = localY - canvasHeight * 0.5f;
        }

        private static void Add(List<FloatTextSpec> into, PlayerState target, string text, FloatKind kind)
            => Add(into, target, text, kind, 0);

        private static void Add(List<FloatTextSpec> into, PlayerState target, string text,
                                FloatKind kind, int colorRgb)
        {
            // 第一条不延迟，之后每条再错开 StaggerSeconds
            float delay = into.Count == 0 ? 0f : StaggerSeconds;
            into.Add(new FloatTextSpec(target, text, kind, delay, colorRgb));
        }
    }
}
