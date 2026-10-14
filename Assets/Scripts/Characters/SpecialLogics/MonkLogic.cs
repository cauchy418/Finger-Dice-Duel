namespace FingerDiceDuel.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// **武僧**（攻 0 / 防 0，攻击距离 1）。
    ///
    /// 玩家确认的规则：
    ///   · **5 不合盾**，而是进入 **太极形态**
    ///   · 被攻击时，若自己有太极形态的手：**把那只 5 变成 0**，
    ///     只受**本次攻击应有伤害的一半**，**剩下的一半反弹**给攻击方
    ///   · 反弹**无视距离**、**可以被盾挡下**、**可以被游侠闪避**，
    ///     但**无法被邪术师取消**
    ///   · 对**咒术师**的攻击：**不减半、不反弹，且 5 不消耗**
    ///     （咒术师无视护盾，太极也无从借力）
    ///   · **0 被他人触碰**（别人把它当**加数**用）后**立即变成 1**
    ///
    /// ## 关于"减半"的取整
    ///
    /// 玩家没有明确说奇数怎么办。这里按**向上取整给挨打方**（即受 `ceil(N/2)`、
    /// 反弹 `floor(N/2)`）—— 理由：太极是防守方自己的技能，
    /// 不该因为取整而让它比"四舍五入"更亏；而且这样**总伤害守恒**（受 + 反弹 = N）。
    /// </summary>
    public sealed class MonkLogic : CharacterTemplate, IHandEffectRewriter,
                                     IDamageMitigator, IPreTurnChoiceProvider
    {
        public override string DisplayName => "武僧";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 0;

        /// <summary>攻击距离 1。</summary>
        public override int BaseAttackRange => 1;

        /// <summary>太极形态的手值（与护盾同为 5）。</summary>
        public const int TaichiValue = 5;

        /// <summary>太极触发后那只手变成的值。</summary>
        public const int TaichiBrokenValue = 0;

        // ============================================================
        // 5 -> 太极形态（而不是护盾）
        // ============================================================

        /// <summary>
        /// 把**形成护盾**改写为**太极形态**。
        ///
        /// 5 与圣武士改写来的 4/6 都会走到"效果 = Shield"这一步，
        /// 所以在这里统一改成 Taichi 状态即可覆盖全部来源。
        ///
        /// ⚠️ 效果类型**仍然返回 Shield** —— 太极在"效果类型"这一层
        ///    与护盾同类（都是"合拢手掌"），区别只在**手牌状态**与后续结算。
        ///    这样"是否需要选目标""战报怎么写"这些既有判断都不用改。
        /// </summary>
        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
        {
            HandEffectType effect = Hand.GetEffectType(actedHand?.Value ?? 0);

            if (effect == HandEffectType.Shield)
            {
                // 用 Taichi 状态覆盖稍后会被设成 Formed 的护盾状态。
                // GameManager 在效果分派处会调 OnShieldFormed —— 见下面那个钩子。
                actedHand?.SetShieldState(ShieldState.Taichi);
            }

            return effect;
        }

        /// <summary>
        /// 合出"盾"的钩子里把状态**改回太极** ——
        /// 因为 GameManager 的分派路径会在 Shield 分支里把手设成 Formed。
        ///
        /// ⚠️ 顺序上 RewriteEffect 先跑、分派后跑，所以这里必须再纠正一次，
        ///    否则太极会被覆盖成护盾（那就变成"能挡一次"而不是"减半反弹"了）。
        /// </summary>
        public override void OnShieldFormed(PlayerState owner, Hand shieldHand,
                                           GameManager manager)
        {
            if (shieldHand == null) return;
            if (shieldHand.Value != TaichiValue) return;

            shieldHand.SetShieldState(ShieldState.Taichi);
        }

        // ============================================================
        // 太极化解
        // ============================================================

        /// <summary>
        /// 挨打时尝试太极化解。
        ///
        /// 判据：
        ///   · 自己有一只手处于**太极形态**
        ///   · **不是咒术师的攻击**（咒术师无视护盾，太极也无从借力）
        /// </summary>
        public bool TryMitigate(PlayerState owner, AttackContext context, int incomingDamage,
                                out int reducedDamage, out int reflectDamage)
        {
            reducedDamage = 0;
            reflectDamage = 0;

            if (owner == null || context == null) return false;
            if (incomingDamage <= 0) return false;

            // ⚠️ 咒术师的攻击：**不减半、不反弹、5 也不消耗**
            if (context.Attacker?.Character is CurseweaverLogic) return false;

            Hand taichi = TaichiHandOf(owner);
            if (taichi == null) return false;

            // ⚠️ **是否发动由玩家决定**（玩家要求"也要给按钮选择是否发动"）。
            //
            //    走与游侠闪避同一条通路：收集阶段问过、答案记进 sink，
            //    这里回读。读不到答案（AI / 没问过）时**默认发动** ——
            //    减半天生比吃满伤害好，不发动只是白白吃亏。
            if (!WantsToUseTaichi(owner, context, taichi)) return false;

            // 太极发动：那只 5 变成 0（此后它只是一只普通的 0，
            // 而且"被他人触碰会变 1"—— 见 OnTouchedAsAddend）
            taichi.SetValue(TaichiBrokenValue);

            // 减半：受上取整、反弹下取整（总和守恒）
            reducedDamage = (incomingDamage + 1) / 2;
            reflectDamage = incomingDamage / 2;

            return true;
        }

        /// <summary>
        /// 回读"玩家是否选择发动太极"。
        ///
        /// <see cref="PreTurnChoiceReader.Read"/> 优先问抉择器（AI），
        /// 再回读 sink（人类在收集阶段点的）。没有答案时返回 true（默认发动）。
        /// </summary>
        private static bool WantsToUseTaichi(PlayerState owner, AttackContext context,
                                             Hand taichi)
        {
            GameManager manager = context?.Manager;
            if (manager == null) return true;      // 拿不到上下文 -> 默认发动

            var request = new ChoiceRequest(
                HandEffect.MonkTaichi,
                $"武僧被攻击：要发动太极吗？（把这张 {TaichiValue} 变成 0，" +
                $"只受一半伤害并把剩余的反弹给攻击方）",
                new List<Hand>(1) { taichi },
                allowDecline: true,
                declineResult: null,               // 放弃 = 不发动，正常吃满伤害
                decider: owner,
                acceptLabel: "发动太极");

            Hand chosen = PreTurnChoiceReader.Read(
                manager.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                manager.GetChoiceProvider(owner), request, owner);

            // 选了候选（那只太极手）= 发动；null（明确放弃）= 不发动。
            return chosen != null;
        }

        /// <summary>
        /// 收集"是否发动太极"—— 必须在**结算之前**问，否则结算中途没有机会。
        ///
        /// 只在"我是这次攻击的防守方"时问（太极是防守姿态）。
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (!ReferenceEquals(context.Defender?.Character, this)) return;
            if (!context.IsAttack) return;

            Hand taichi = TaichiHandOf(context.Defender);
            if (taichi == null) return;

            requests.Add(new ChoiceRequest(
                HandEffect.MonkTaichi,
                $"武僧被攻击：要发动太极吗？（把这张 {TaichiValue} 变成 0，" +
                $"只受一半伤害并把剩余的反弹给攻击方）",
                new List<Hand>(1) { taichi },
                allowDecline: true,
                declineResult: null,
                decider: context.Defender,
                acceptLabel: "发动太极"));
        }

        /// <summary>找出处于太极形态的那只手；没有则返回 null。</summary>
        public static Hand TaichiHandOf(PlayerState player)
        {
            if (player == null) return null;
            if (player.LeftHand != null && player.LeftHand.IsTaichi) return player.LeftHand;
            if (player.RightHand != null && player.RightHand.IsTaichi) return player.RightHand;

            return null;
        }

        /// <summary>
        /// 把武僧手上所有**值为 5** 的手变成太极形态。
        ///
        /// 玩家要求："武僧初始有 5 的话这个 5 是可以发动的，处于太极状态" ——
        /// 即**开局就是 5**（掷骰决定的手值 / 测试直接摆的局面）也算太极，
        /// 不必先用运算"打出来"。
        ///
        /// ⚠️ 为什么需要单独一步：`ShieldState` 只在**运算产生 Shield 效果**时
        ///    才会被设成 Taichi（见 <see cref="RewriteEffect"/>）。
        ///    开局直接摆出来的 5 没有经过那一步，状态还是 None ——
        ///    于是"有 5 却不能发动太极"。
        ///
        /// 由上层在**开局设定手值之后**调用（见
        /// <c>GameManager.ApplyInitialTaichiForm</c>）。
        /// </summary>
        public static void ApplyTaichiToFives(PlayerState player)
        {
            if (player == null) return;
            if (!(player.Character is MonkLogic)) return;

            ApplyTo(player.LeftHand);
            ApplyTo(player.RightHand);

            void ApplyTo(Hand hand)
            {
                if (hand == null) return;
                if (hand.Value != TaichiValue) return;

                // 只动"没有任何护盾状态"的手 —— 已成型的盾 / 已碎的盾不该被改写
                if (hand.State != ShieldState.None) return;

                hand.SetShieldState(ShieldState.Taichi);
            }
        }

        // ============================================================
        // 0 被他人触碰 -> 立即变成 1
        // ============================================================

        /// <summary>
        /// 别人把武僧的 **0** 当作**加数**使用时，那只手立即变成 **1**。
        ///
        /// 玩家确认："被别人把 0 当加数用" —— 且**当成 1 参与本次运算**
        /// （手值当场就变，所以那次运算的结果也跟着变）。
        ///
        /// 由 <see cref="GameManager"/> 在选定加数后、结算前调用
        /// （见 <c>GameManager.TriggerMonkZeroTouch</c>）。
        /// </summary>
        public static void OnTouchedAsAddend(Hand hand)
        {
            if (hand == null) return;
            if (hand.Value != TaichiBrokenValue) return;   // 只对 0 生效

            hand.SetValue(1);
        }

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }
    }
}
