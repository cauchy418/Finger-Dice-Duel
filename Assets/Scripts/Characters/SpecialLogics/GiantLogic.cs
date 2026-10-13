namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **巨人**（攻 +2 / 防 +2，攻击距离 1）。
    ///
    /// 玩家确认的规则：
    ///   · **4 / 6 / 8 无法攻击**（**完全无效果**，与 1/2/3/7 一样）
    ///   · **5 无法合盾**，改为**发起两段攻击**：
    ///     每段的**基础伤害 = 另一只手的点数**（之后**各自**再加攻防加成）
    ///   · **9 改为两段回复**，每段回复量 = **另一只手的点数**（都回自己）
    ///
    /// 玩家补充的两点关键说明：
    ///   · "另一只手在本次运算前后都不会变化" —— 所以取哪个时刻的值都一样，
    ///     实现上直接用那只手的当前值即可
    ///   · "**其余结算和忍者类似**" —— 即两段各自独立判盾 / 判闪避、
    ///     各自结算攻防加成（复用 <see cref="AttackContext.HitCount"/> 那条通路）
    ///   · "邪术师可以通过 5 来一次性取消巨人的两段攻击" ——
    ///     因为攻击数字就是 5，取消发生在逐段循环之前，所以天然一次抵消两段
    /// </summary>
    public sealed class GiantLogic : CharacterTemplate, IHandEffectRewriter,
                                      IMultiHitAttack
    {
        public override string DisplayName => "巨人";

        public override int DefaultAttackBonus => 2;
        public override int DefaultDefenseBonus => 2;

        /// <summary>攻击距离 1。</summary>
        public override int BaseAttackRange => 1;

        /// <summary>打出 5 时的攻击段数。</summary>
        public const int HitCount = 2;

        /// <summary>打出 9 时的回复段数。</summary>
        public const int HealCount = 2;

        /// <summary>需要多段时提前告诉上层段数（每段各问一次闪避）。</summary>
        public int MaxHits => HitCount;

        /// <summary>
        /// 改写效果：
        ///   · **4 / 6 / 8 -> 无效果**（不能攻击）
        ///   · **5 -> 攻击**（两段，伤害由"另一只手"决定，见 OnResolveAttack）
        ///   · **9 -> 回复**（两段，见 GameManager 的回复分支）
        /// </summary>
        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
        {
            int result = actedHand?.Value ?? 0;

            switch (result)
            {
                // 4 / 6 / 8 完全无效果
                case 4:
                case 6:
                case 8:
                    return HandEffectType.None;

                // 5 不再合盾，改为"两段攻击"——
                // 效果类型借 Attack6（任何攻击类型都会走攻击分派），
                // 真正的伤害与段数在 OnResolveAttack 里设定。
                case 5:
                    return HandEffectType.Attack6;

                default:
                    return Hand.GetEffectType(result);
            }
        }

        /// <summary>
        /// 打 5 时：把基础伤害改成**另一只手的点数**，并拆成两段。
        ///
        /// ⚠️ 顺序要点：这段逻辑在 <see cref="AttackContext"/> 的
        ///    "先剥掉攻防加成、再分段、最后每段各自加回去"之前生效，
        ///    所以"基础伤害 = 另一只手点数"是**每段**的基础值，
        ///    之后每段各自叠加攻防（与忍者同一套）。
        /// </summary>
        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;
            if (context.Attacker?.Character != this) return;

            // 只有"打出 5"这条路需要改写（4/6/8 已被改成无效果）
            int result = context.Attacker.LeftHand == context.SourceHand
                ? context.Attacker.RightHand?.Value ?? 0
                : context.Attacker.LeftHand?.Value ?? 0;

            // 另一只手的点数即基础伤害（可能是 0 —— 武僧的太极会把 5 变 0）
            context.FinalDamage = result;
            context.HitCount = HitCount;
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }
    }
}
