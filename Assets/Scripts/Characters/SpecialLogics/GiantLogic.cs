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
                                      IMultiHitAttack, IHealOverride, IHealAmountProvider,
                                      IHandEffectDisplay
    {
        public override string DisplayName => "巨人";

        public override int DefaultAttackBonus => 2;
        /// <summary>
        /// 本次运算"**另一只手**"的点数（由 <see cref="RewriteEffect"/> 记下）。
        ///
        /// ⚠️ 为什么要在那里记：`RewriteEffect` 是**唯一**同时知道
        ///    "运算源是哪只手"与"结果值"的时点 —— 一旦结算继续，
        ///    运算源已经变成结果值，就再也分辨不出哪只是运算源了。
        ///
        ///    我第一版想用启发式（"等于 5 或 9 的那只是结果"）来事后推断，
        ///    但那只手完全可能本来就是 5 或 9，会推断错。
        /// </summary>
        private int _otherHandValueOfThisOperation;

        /// <summary>本次运算的另一只手点数（供测试与战报读取）。</summary>
        public int OtherHandValueThisOperation => _otherHandValueOfThisOperation;

        public override int DefaultDefenseBonus => 2;

        /// <summary>生命上限 +5（玩家要求："同时巨人最大体力上限 +5"）。</summary>
        public const int HealthBonus = 5;

        public override int MaxHealthBonus => HealthBonus;

        /// <summary>
        /// **巨人是否需要"点一次目标"**。
        ///
        /// 玩家实测："巨人 4/6/8 还需要点击一次目标，实际上不应该点；
        /// 而 5 则默认攻击运算目标了"。
        ///
        /// 为什么不能沿用基类：基类按 <c>context.Effect</c> 判断，
        /// 而巨人的 4/6/8 在**基础效果**层面本来就是 `None`
        /// （`Hand.GetEffectType(4)` = Attack4，但巨人要把它变成"无效果"）——
        /// 所以必须按**结果值**判断：
        ///
        ///   · 4 / 6 / 8 -> 无效果 -> **不点目标**
        ///   · 5        -> 两段攻击 -> **要点目标**（它是真的会打人）
        ///   · 9        -> 回自己 -> 不点目标
        ///
        /// ⚠️ 5 必须保留"点目标"这一步：它虽然改写成 Attack6，
        ///    但那是**两段真攻击**，不指定打谁就说不过去。
        /// </summary>
        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context == null) return false;

            int result = context.ResultValue;

            // 5 -> 两段攻击，必须选目标
            if (result == 5) return true;

            // 4 / 6 / 8 被改写成无效果 -> 不要目标
            // 9 回自己 -> 不要目标
            // 其余（1/2/3/7）本来就无效果
            return false;
        }

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

            // 记下"另一只手"的点数 —— 此刻还能分辨哪只是运算源。
            // 玩家说明："另一只手在本次运算前后都不会变化"，
            // 所以此刻读到的值与结算时一致。
            _otherHandValueOfThisOperation = OtherHandOf(actor, actedHand)?.Value ?? 0;

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

            // ⚠️ 用 RewriteEffect 记下的"另一只手点数"，**不要在这里重新推断**：
            //    此时运算源已经变成结果值（5），再想分辨哪只是运算源
            //    就只能靠猜 —— 而另一只手完全可能本来就是 5。
            //
            // ⚠️⚠️ 必须**乘以段数**：AttackContext 的多段语义是
            //     "把**总伤害**平摊到各段"（忍者就是靠这个：8 -> 4+4）。
            //     而巨人的规则是"**每段**基础伤害 = 另一只手点数"，
            //     所以要先把总伤害设成「另一只手点数 × 段数」，
            //     平摊之后每段才正好是"另一只手点数"。
            //
            //     我第一版直接设成"另一只手点数"，于是 3 被平摊成 1+2，
            //     每段只有 1 点基础伤害（测试当场抓到）。
            context.FinalDamage = _otherHandValueOfThisOperation * HitCount;
            context.HitCount = HitCount;
        }

        /// <summary>
        /// 打出 9 时：**两段回复**，每段回复量 = **另一只手的点数**，都回自己。
        ///
        /// 玩家确认："打出 9 时进行两段回复，回复量为另一只手的点数"、
        /// "都回**自己**"。
        ///
        /// 实现上把"两段"折成**总量 = 另一只手点数 × 2** ——
        /// 因为都回自己，与分两次回同一人是等价的（满血截断也一样：
        /// 先回一半再回一半的总量，等于一次回总量，都不会超过上限）。
        /// </summary>
        public PlayerState ApplyHeal(PlayerState owner, GameManager manager,
                                     IPreTurnChoiceSink choiceSink)
        {
            return owner;      // 都回自己
        }

        /// <summary>本次回复总量 = 另一只手点数 × 2（两段各一次）。</summary>
        public int HealAmountOf(PlayerState owner, GameManager manager)
            => _otherHandValueOfThisOperation * HealCount;

        /// <summary>取"另一只手"（不是本次运算源的那只）；认不出来时返回 null。</summary>
        private static Hand OtherHandOf(PlayerState owner, Hand actedHand)
        {
            if (owner == null || actedHand == null) return null;
            if (ReferenceEquals(owner.LeftHand, actedHand)) return owner.RightHand;
            if (ReferenceEquals(owner.RightHand, actedHand)) return owner.LeftHand;

            return null;
        }

        /// <summary>
        /// 界面上这张手该显示成什么效果。
        ///
        /// 玩家实测："巨人 4/6/8 在 UI 上不要显示红色的攻击，要让 5 显示红色的攻击。"
        ///
        /// 通用规则是 5 = 护盾（蓝色）、4/6/8 = 攻击（红色），
        /// 而**巨人正好相反** —— 所以必须把改写后的结果告诉界面，
        /// 否则玩家会看着红色以为"这张能打人"。
        ///
        /// 直接复用 <see cref="RewriteEffect"/> 的同一套判定，
        /// 保证"显示"与"实际"不可能漂移。
        /// </summary>
        public HandEffectType DisplayedEffectOf(int resultValue)
        {
            switch (resultValue)
            {
                // 4 / 6 / 8 完全无效果 -> 显示成普通（不红）
                case 4:
                case 6:
                case 8:
                    return HandEffectType.None;

                // 5 是两段攻击 -> 显示成攻击（红）
                case 5:
                    return HandEffectType.Attack6;

                default:
                    return Hand.GetEffectType(resultValue);
            }
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            _otherHandValueOfThisOperation = 0;
        }
    }
}
