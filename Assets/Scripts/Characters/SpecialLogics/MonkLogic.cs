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
        /// <param name="segmentDamage">这一段当前的伤害量。</param>
        /// <param name="stages">实际发动了几次减免（有几个太极就能发动几次）。</param>
        /// <param name="totalReduced">被减免掉的总量。</param>
        /// <param name="totalReflect">要反弹回去的总量。</param>
        public bool TryMitigate(PlayerState owner, AttackContext context, int segmentDamage,
                                int stagesUsed,
                                out int stages, out int totalReduced, out int totalReflect)
        {
            stages = 0;
            totalReduced = 0;
            totalReflect = 0;

            if (owner == null || context == null) return false;
            if (segmentDamage <= 0) return false;

            // ⚠️ 咒术师的攻击：**不减半、不反弹、5 也不消耗**
            // 咒术师的攻击：**不减半、不反弹、5 也不消耗**。
            //
            // ⚠️ 改用接口而不是 `is CurseweaverLogic`：
            //    现在**猎人也要判同一件事**（伏击同样挡不住咒术），
            //    再硬编码一次就是“两个角色各自认识咒术师”。
            if (PiercesMitigation(context)) return false;

            // ⚠⚠ **必须在改动任何手之前，把（手, variant）固定下来。**
            //
            //    实测确认的根因：第一次发动把一只太极手设成 0 之后，
            //    <see cref="TaichiHandsOf"/> 只剩 1 只 —— **它的索引从 1 变成 0**。
            //    于是再问 variant 0，又去读那只已经是 0 的手的答案 → 失败。
            //    （现象：双太极只减半一次。）
            //
            //    所以先把"答应发动的太极手"按 variant 顺序固定成列表，
            //    之后用 <paramref name="stagesUsed"/> **直接取第几只**，不再重新索引。
            List<Hand> all = TaichiHandsOf(owner);
            if (all.Count == 0) return false;

            // ⚠⚠ **按索引挑手**，而不是每次重建一个"答应的手"列表。
            //
            //    我试过重建，但那会错位：第一次发动把一只手设成 0 后，
            //    <see cref="TaichiHandsOf"/> 只剩 1 只，重建出来的列表里它是**索引 0**，
            //    于是又去问 variant 0（那只已经是 0 的手）→ 读不到有效答案。
            //
            //    正确做法：只问**当前该用的那一只**（索引 = stagesUsed），
            //    而"最多能发动几次"由 <see cref="TryMitigateByTaichi"/>
            //    里已经验证过的计数来限制。
            // ⚠️ **不能用 `stagesUsed >= all.Count` 来判断"没有更多太极"！**
            //
            //    `all` 是每次现查的，而**上一只太极发动后会被设成 0**，
            //    于是它从列表里消失、`all.Count` 缩小（实测：2 -> 1）。
            //    我加过这句守卫，结果把"还有第二个太极"误判成"没有了"。
            //    （实测：call1 ok=True；call2 因 stagesUsed=1 >= all.Count=1 而返回 false。）
            //
            //    正确做法：**只问当前该用的那一只**（origin 索引 = stagesUsed），
            //    而"最多能发动几次"由**外层**（AttackContext 的 taichiRemaining）限制。
            //    若该索引上已经没有可用的手，下面会自然拿不到。
            if (all.Count == 0) return false;

            Hand taichi = all[stagesUsed < all.Count ? stagesUsed : all.Count - 1];
            if (!WantsToUseTaichi(owner, context, taichi, stagesUsed)) return false;

            int taichiAvailable = all.Count > stagesUsed + 1 ? all.Count : stagesUsed + 1;

            // 太极发动：那只 5 变成 0（此后它只是一只普通的 0，
            // 而且"被他人触碰会变 1"—— 见 OnTouchedAsAddend）
            taichi.SetValue(TaichiBrokenValue);

            // ⚠️ **一个太极只对付一段**（玩家明确确认的规则）：
            //
            //    "武僧一个太极只能弹一段伤害是对的，不要改成能一次挡多段伤害；
            //     例如巨人的攻击打到太极上，一个太极只能减免反弹一段伤害，
            //     另一段伤害是要吃的。"
            //
            //    但**两个太极可以叠在同一段上**（逐次折半）。
            //    现在是**逐段调用**（闪避已排到太极之前），
            //    所以只算"**新增那一次**"的递减。
            // ⚠⚠ **这里只算"这一轮新增的减免"，不能累加。**
            //
            //    玩家指出的错误：段伤害 9、两个太极时，
            //      第一轮 9 -> 弹 4 受 5   ✅
            //      第二轮 5 -> **应为弹 2 受 3**，但实际返回了 1（受 4）❌
            //
            //    原因：外层（AttackContext）已经在累加反弹与减免，
            //    而本方法又把"已经发生过的减免"累加进了 totalReduced ——
            //    于是外层拿到的是**累计值减去上一轮的**，而不是本轮的。
            //
            //    **正确语义：out 参数 = 本次调用新增的那一份。**
            //    所以这里只算"当前 remaining 的一半"，不再回溯累加。
            if (segmentDamage <= 0) return false;

            int reflect = segmentDamage / 2;      // 这一轮反弹（下取整）
            int taken = segmentDamage - reflect;  // 这一轮受方仍要承受

            totalReduced = reflect;
            totalReflect = reflect;
            stages = stagesUsed + 1;

            // taken 不用回传：外层用"perHit - 累计减免"自己算。
            _ = taken;

            return stages > 0;
        }

        /// <summary>
        /// 攻击方是否**穿透减免**（咒术师）。
        ///
        /// 抽成共用判定：武僧的太极与猎人的伏击都走这里。
        /// </summary>
        internal static bool PiercesMitigation(AttackContext context)
        {
            var pierce = context?.Attacker?.Character as IPiercesMitigation;
            return pierce != null && pierce.PiercesMitigation;
        }

        /// <summary>
        /// 回读"玩家是否选择发动太极"。
        ///
        /// <see cref="PreTurnChoiceReader.Read"/> 优先问抉择器（AI），
        /// 再回读 sink（人类在收集阶段点的）。没有答案时返回 true（默认发动）。
        /// </summary>
        private static bool WantsToUseTaichi(PlayerState owner, AttackContext context,
                                             Hand taichi, int variant)
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

            // ⚠️ **每个太极各发一个请求**（用 variant 区分）。
            //
            //    玩家确认："若武僧有两个太极，他对一段攻击是能都弹的。"
            //    但原来只发**一个**请求（ChoiceCandidates 只有 1 个），
            //    所以玩家**根本没机会发动第二个** ——
            //    表现为"只减半一次"。
            //
            //    sink 的键是 effect + variant，所以多个请求不会互相覆盖。
            List<Hand> mine = TaichiHandsOf(context.Defender);
            for (int i = 0; i < mine.Count; i++)
            {
                requests.Add(new ChoiceRequest(
                    HandEffect.MonkTaichi,
                    $"武僧被攻击：要发动太极吗？（把这张 {TaichiValue} 变成 0，" +
                    $"只受一半伤害并把剩余的反弹给攻击方）",
                    new List<Hand>(1) { mine[i] },
                    allowDecline: true,
                    declineResult: null,
                    decider: context.Defender,
                    acceptLabel: "发动太极",
                    variant: i));
            }
        }

        /// <summary>武僧当前处于太极形态的**全部**手（最多两只）。</summary>
        public static List<Hand> TaichiHandsOf(PlayerState player)
        {
            var list = new List<Hand>();
            if (player == null) return list;

            if (player.LeftHand != null && player.LeftHand.IsTaichi) list.Add(player.LeftHand);
            if (player.RightHand != null && player.RightHand.IsTaichi) list.Add(player.RightHand);

            return list;
        }

        /// <summary>
        /// 武僧当前有**几个太极**（0 / 1 / 2）。
        ///
        /// 玩家确认：两个太极可以**叠在同一段伤害上**，
        /// 第一次弹一半、第二次弹剩余伤害的一半。
        /// </summary>
        public static int TaichiCountOf(PlayerState player)
        {
            if (player == null) return 0;

            int n = 0;
            if (player.LeftHand != null && player.LeftHand.IsTaichi) n++;
            if (player.RightHand != null && player.RightHand.IsTaichi) n++;

            return n;
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
