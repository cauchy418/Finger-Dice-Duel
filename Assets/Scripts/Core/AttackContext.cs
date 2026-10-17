using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 单次攻击的结算上下文。角色钩子可以修改本对象来影响结算（如无视护盾、增伤等）。
    /// 本类型不依赖任何 Unity 类型，可在纯 .NET 下编译与测试。
    /// </summary>
    public class AttackContext
    {
        public PlayerState Attacker { get; }

        /// <summary>
        /// 本次攻击**实际承受伤害的人**。
        ///
        /// 默认就是构造时的目标，但可以在 <see cref="Resolve"/> 开头被改写 ——
        /// 圣武士的"范围内的角色被攻击时把目标改成自己"就是这么做的。
        /// </summary>
        public PlayerState Defender { get; private set; }
        public Hand SourceHand { get; }

        /// <summary>原始伤害（来自 HandEffectType 的数值，4/6/8）。</summary>
        public int RawDamage { get; }

        /// <summary>
        /// 最终伤害。角色钩子可修改此值（在 OnResolveAttack 中修改即为基础伤害修正）。
        /// 结算时会在此基础上叠加攻防加成。
        /// </summary>
        public int FinalDamage { get; set; }

        /// <summary>
        /// 基础伤害倍率，默认 1.0。角色钩子可修改（游侠强化后为 1.5）。
        /// 作用顺序：**先乘倍率、再叠加攻防加成**（见 RULES.md §6）。
        /// </summary>
        public float DamageMultiplier { get; set; } = 1f;

        /// <summary>本次攻击是否被闪避（游侠）。闪避完全免伤，且不消耗护盾。</summary>
        public bool WasDodged { get; private set; }

        /// <summary>免伤原因（例如"目标在潜行中，无法被攻击"）。供战报显示。</summary>
        public string DodgeReason { get; private set; }

        /// <summary>
        /// 攻击方的抉择接收者，由 <see cref="GameManager"/> 在结算前注入。
        /// 角色的攻击钩子（如游侠的强化射击）用它回读玩家在运算前的选择。
        /// </summary>
        public IPreTurnChoiceSink AttackerChoiceSink { get; set; }

        /// <summary>攻击方的抉择器（无收集步骤时用于当场询问）。</summary>
        public IChoiceProvider AttackerProvider { get; set; }

        /// <summary>
        /// 被攻击的那只防守方手在运算**之前**的数值。
        ///
        /// 必须显式保存：运算源已经被改写成结果值，无法再由它反推这次运算的效果。
        /// （曾经在闪避判定里把运算源当成目标手用，导致 Effect 算成 None、
        /// 游侠的闪避被整个跳过 —— 真实 bug。）
        /// </summary>
        public int TargetHandValueBefore { get; set; }

        /// <summary>
        /// 攻击方运算**之前**那只运算源手值。
        ///
        /// 重要：<see cref="GameManager.ExecuteTurn"/> 会先把运算源改成运算结果，
        /// 之后才调用攻击钩子。因此钩子里读 <c>SourceHand.Value</c> 已经是"运算后"的值，
        /// 想判断"运算前手里有没有 2"这类条件必须用本属性。
        /// </summary>
        public int SourceHandValueBefore { get; set; }

        /// <summary>是否无视护盾（咒术师）。</summary>
        public bool IgnoreShield { get; set; }

        /// <summary>是否无视对方的正向防御加成（咒术师）。负向加成仍然生效。</summary>
        public bool IgnorePositiveDefense { get; set; }

        /// <summary>
        /// 本次攻击是否**完全无视双方的攻防加成**（剑客的追加攻击）。
        /// 为 true 时最终伤害就等于 RawDamage（经倍率与分段），不叠加任何攻防修正。
        /// </summary>
        public bool IgnoreStatBonuses { get; set; }

        /// <summary>本次攻击是否被护盾抵挡。</summary>
        public bool WasBlocked { get; private set; }

        /// <summary>被消耗的护盾（如有）。</summary>
        public Hand ConsumedShield { get; private set; }

        /// <summary>本次结算实际造成的生命损失（多段攻击为各段之和；被抵挡时为 0）。</summary>
        public int AppliedDamage { get; private set; }

        /// <summary>
        /// 本次攻击的段数（默认 1）。忍者把它设为 2：伤害平均分配，每段独立结算。
        /// </summary>
        public int HitCount { get; set; } = 1;

        /// <summary>
        /// **本次攻击的伤害类型**（由攻击方角色声明）。
        ///
        /// ⚠️ 玩家要求："把所有攻击都标记上攻击类型"，并让伤害飘字的颜色
        ///    （含被盾牌抵挡）由它决定、与攻击类型图标一致。
        ///    结算开始时填一次，`GameManager` 结算完再回填到
        ///    <see cref="TurnResult.AttackType"/> 供界面使用。
        /// </summary>
        public AttackDamageType AttackType { get; private set; } = AttackDamageType.None;

        /// <summary>
        /// 结算过程中询问玩家的回调。**由上层（BoardController）注入**。
        ///
        /// 为 null 时角色会退化成"自动选第一个可用选项"（单元测试的直连场景）。
        /// </summary>
        public IChoiceGatherer ChoiceGatherer { get; set; }

        /// <summary>
        /// 查询"AI 已经替某一方做出的抉择"（由上层注入）。
        /// 会透传给闪避上下文，让角色优先采用它，而不是回读可能过期的 sink。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AiDecisionLookup { get; set; }

        /// <summary>查询 AI 抉择（便捷封装，未注入时返回 null）。</summary>
        public Hand GetAiDecision(HandEffect effect, int variant = 0)
            => AiDecisionLookup?.Invoke(effect, variant);

        /// <summary>
        /// true 表示这次攻击的闪避**不询问玩家**、自动选第一个可用选项。
        /// 没有 <see cref="ChoiceGatherer"/> 时由构造函数自动置为 true。
        /// </summary>
        public bool AutoResolveDodge { get; set; }

        /// <summary>被护盾挡下的段数。</summary>
        public int BlockedHits { get; private set; }

        /// <summary>实际造成伤害的段数。</summary>
        public int LandedHits { get; private set; }

        /// <summary>被闪避掉的段数（游侠每段消耗一只"1"）。</summary>
        public int DodgedHits { get; private set; }

        public AttackContext(PlayerState attacker, PlayerState defender, Hand sourceHand, int rawDamage)
        {
            Attacker = attacker;
            Defender = defender;
            SourceHand = sourceHand;
            RawDamage = rawDamage;
            FinalDamage = rawDamage;
            IgnoreShield = false;
            IgnorePositiveDefense = false;

            // 默认**不**自动结算：走 sink / 抉择器那条正常路径。
            // 直接构造 AttackContext 且没有 sink 的场景，调用方自行置 true。
            AutoResolveDodge = false;
        }

        /// <summary>
        /// 执行结算流程：调用攻击方钩子 -> 处理护盾 -> 应用伤害。
        /// </summary>
        public void Resolve(IChoiceProvider defenderChoice, GameManager manager)
        {
            Manager = manager;
            // 0. **最早结算：取消攻击**（邪术师）。
            //
            //    必须排在攻击方钩子**之前** —— 规则确认它是"最早结算"的，
            //    所以能取消掉咒术师那种"在钩子里改写伤害"的攻击。
            //
            //    ⚠️ 取消者不是挨打的人，而是**场上的第三方**，所以要遍历全部参战玩家。
            //    ⚠️ 追加攻击与冲击的"攻击数字视为 0"（见 AttackValue），
            //       任何手值都不等于 0，所以它们天然无法被取消 —— 不需要特判。
            if (TryCancelAttack(manager))
            {
                // 取消 = 完全无效：不扣血、不碎盾、不触发后续效果。
                // 段数设为 0 让后面的循环一次都不跑（比 early-return 更省事，
                // 且保留"这次攻击发生过"的记录供战报使用）。
                HitCount = 0;
                return;
            }

            // 1. 攻击方钩子介入（如咒术师设置 IgnoreShield / 修改 FinalDamage / 游侠设置倍率）
            Attacker.Character?.OnResolveAttack(this);

            // 1a. **本次攻击的伤害类型**（玩家要求：所有攻击都要标记类型）。
            //
            //     ⚠️ 必须在 `OnResolveAttack` **之后**取：
            //        游侠的"强化射击"就是在那个钩子里把 `DamageMultiplier` 设成 1.5，
            //        而"普通挥砍 / 强化穿刺"正是按这个区分（见 RangerLogic）。
            if (Attacker.Character is IAttackTypeProvider typeProvider)
                AttackType = typeProvider.AttackTypeOf(this);

            // 1b. 攻击方的**伤害规则**改写（魔射手：6 伤害 +2、8 无视护盾）。
            //
            //     与 OnResolveAttack 分开是因为它需要**攻击数字**，
            //     而那个数字要用"运算前的快照"算（SourceHandValueBefore + TargetHandValueBefore），
            //     写成独立接口可以让规则更清晰地声明"我只按数字办事"。
            if (Attacker.Character is IAttackDamageRule rule)
                rule.ApplyAttackRule(this, AttackValue, manager);

            FinalDamage = ApplyBonuses(FinalDamage);

            // ⚠️ 顺序要点：
            //   "把目标改成自己"必须排在**攻击方钩子之后**（伤害已定），
            //   但排在**闪避与护盾判定之前** —— 因为那两件事都取决于"谁来承受"。
            //
            //   本来想放在最前面，但攻击方钩子可能改写 IgnoreShield 之类的标记，
            //   而代挡者应当承受**最终确定下来**的那一击。
            TryRedirectDefender(manager);

            // 2~4. 逐段结算：每一段都**独立**判定闪避、独立判定护盾、独立扣血、
            //      独立结算攻防加成。
            //
            // 为什么连闪避也要逐段：规则已确认 ——
            // 忍者的每一段都要各自用一次"1 变 2"才能完全闪过，
            // 剑客的追加攻击也算**独立的一次攻击**（可以单独被闪避/挡盾）。
            //
            // 注意这里先把加成从基础伤害里剥掉，再分段，最后每段各自加回去：
            //   忍者基础 4、攻 +1 -> 每段 (4/2) + 1 = 3，共 6（而不是 (4+1)/2 = 2，共 4）。
            int hits = HitCount < 1 ? 1 : HitCount;

            // 基础伤害用 **FinalDamage**（钩子可能改过它，例如刺客的出潜行 +7），
            // 而不是 RawDamage —— 否则钩子对伤害的修改会被静默丢弃。
            int perHitBase = SplitDamage(FinalDamage, hits);
            int perHit = IgnoreStatBonuses ? perHitBase : ApplyStatBonuses(perHitBase);

            // 2°. **太极化解**（武僧）：在逐段结算之前询问。
            //
            //     与闪避 / 护盾的区别：那两种是"整段免掉"，
            //     太极是"**减半**，并把剩下的一半**反弹**给攻击方"。
            //
            //     所以它作用在**整次攻击的总伤害**上（而不是逐段），
            //     并把结果折成每段的伤害。
            //
            //     ⚠️ 它排在闪避 / 护盾**之前**：因为太极一旦发动，
            //        那只手会从 5 变成 0，本次攻击就按"减半 + 反弹"走；
            //        若排在后面，玩家可能既闪避又太极（规则上只应发生一种）。
            int reflectTotal = 0;

            // ⚠️ **太极不在这里问了** —— 玩家要求把**闪避移到太极之前**：
            //    "先闪避再太极" —— 没闪掉的那一段才吃太极。
            //
            //    为什么不能只挪一行调用：
            //      · <see cref="TryDodgeThisHit"/> **有副作用**（游侠的闪避会**消耗一只"1"**），
            //        所以不能"先预判一遍、再正式问一遍"；
            //      · 太极原本在循环**之前**询问，而闪避在循环**里** —— 顺序天然是太极在前。
            //
            //    所以改成：**在循环里、每段先闪避，未被闪避的段才吃太极**。
            int taichiRemaining = 2;      // 最多两只手；能用几次由逐次询问决定
            int taichiStagesUsed = 0;
            int perHitReduction = 0;
            bool taichiUsed = false;

            IPreTurnChoiceSink sink = defenderChoice as IPreTurnChoiceSink;

            for (int i = 0; i < hits; i++)
            {
                // 2a. **闪避**（游侠每次消耗一只"1"；刺客潜行则整次攻击都打不到）
                //
                //     现在它排在**太极之前**（玩家要求）：
                //     闪掉的段不再吃太极，太极留给后面真正落地的段。
                if (sink != null && TryDodgeThisHit(sink, manager, i))
                {
                    WasDodged = true;
                    DodgedHits++;
                    continue;
                }

                // 2b. 护盾：一面盾只能挡下这一段
                if (TryConsumeShield(defenderChoice, manager))
                {
                    WasBlocked = true;
                    BlockedHits++;
                    continue;
                }

                // 2c. **太极化解**（武僧）——
                //     未被闪避、未被盾挡的段，若还有太极可用，就减半并累加反弹。
                //
                //     ⚠⚠ **同一段上要把太极问到用完** —— 这是个内层循环，
                //     不是"每段只询问一次"。
                //     玩家确认："两个太极对一段攻击是能都弹的：
                //     第一次弹一半，第二次弹剩余伤害的一半。"
                int dealt = perHit;
                int reductionThisSegment = 0;

                while (taichiRemaining > 0)
                {
                    // 传**该段当前剩余的伤害**（dealt）——
                    // 这正是玩家说的"第二个太极的目标是减免过的伤害"。
                    if (!TryMitigateByTaichi(manager, dealt, taichiStagesUsed,
                                             out int reduced, out int reflect))
                    {
                        break;
                    }

                    taichiUsed = true;
                    taichiStagesUsed++;
                    taichiRemaining--;
                    reductionThisSegment += reduced;
                    reflectTotal += reflect;

                    // 该段当前剩余 = 原值 - 累计被减免的量
                    dealt = perHit - reductionThisSegment;
                    if (dealt <= 0) break;
                }

                if (i == 0) perHitReduction = reductionThisSegment;

                Defender.TakeDamage(dealt);
                AppliedDamage += dealt;
                LandedHits++;
            }

            // 保持旧行为：第一段的最终伤害写入属性（供诊断与测试读）
            if (taichiUsed) MitigatedPerHit = perHit - perHitReduction;

            // 3. **太极反弹**：把化解下来的那部分伤害打回给攻击方。
            //
            //    玩家确认的规则：
            //      · **无视距离**（不走 CanReach）
            //      · **可以被盾挡下、可以被游侠闪避**
            //      · **无法被邪术师取消**
            //
            //    所以这里发起的是**一次新的攻击**（同一套结算），
            //    但它是**由挨打方发起、目标是攻击方**，而且用
            //    <see cref="IsSpecialAttack"/> 关掉"取消"那条路
            //    （与剑客的追加攻击同类——它们的"攻击数字"不是一次正常运算）。
            if (WasMitigated && reflectTotal > 0 && Attacker != null && Attacker.IsAlive
                && manager != null)
            {
                var reflectCtx = new AttackContext(Defender, Attacker,
                                                   Defender.LeftHand ?? Defender.RightHand,
                                                   reflectTotal)
                {
                    // "攻击数字视为 0" -> 邪术师取消不了（玩家明确要求）。
                    // 用反射产生的攻击，只能取消真正的攻击，不能取消反弹。
                    IsSpecialAttack = true,

                    // 反弹是"已经算好的固定伤害"，不该再叠一次攻防加成
                    IgnoreStatBonuses = true,

                    // 玩家允许"可循环"：反弹仍可被对方的太极再弹回来
                    IsReflected = true,
                    ReflectDepth = ReflectDepth + 1,
                };

                reflectCtx.Resolve(manager.GetChoiceProvider(Attacker), manager);
                ReflectedDealt = reflectCtx.AppliedDamage;
            }

            // 4. **法师：命中即挂状态**。
            //
            //    玩家确认的命中口径：
            //      · 被盾**抵挡**（BlockedHits）、被太极**减免**（WasMitigated）
            //        -> **仍算命中**，照样挂状态；
            //      · 被**闪避**导致一段都没落地、或被**取消**（HitCount == 0）
            //        -> **不算命中**（不挂，且那次的"放弃"作废）。
            //
            //    ⚠️ 挂状态的是**攻击方**的角色（法师），挂到**被实际承受伤害的人**身上
            //       （圣武士可以把目标改成自己 —— 那就挂给代挡者，符合"谁挨打谁中招"）。
            ApplyAttackerStatus(manager);
        }

        /// <summary>
        /// 把攻击方角色声明的状态挂到**实际承受伤害的人**身上（法师体系）。
        ///
        /// 没命中就什么都不做 —— 但**注意**：法师的"放弃换来的类型"该不该作废
        /// 由 <c>WizardLogic</c> 在使用后自行清空决定（见 <c>WizardLogic.ConsumeDamageType</c>），
        /// 这里只负责"命中才挂"。
        /// </summary>
        private void ApplyAttackerStatus(GameManager manager)
        {
            if (Attacker == null) return;
            if (!(Attacker.Character is IStatusApplyingAttack applier)) return;

            StatusEffect status = applier.StatusToApplyOnHit(this);

            // ⚠️ 先取走类型（无论命中与否）—— 玩家确认"没命中就作废，要重新放弃一次"。
            if (Attacker.Character is Characters.SpecialLogics.WizardLogic mage)
                mage.ConsumeDamageType();

            if (status == StatusEffect.None) return;

            // 取消 / 一段都没落地 -> 不算命中
            if (HitCount == 0) return;
            if (LandedHits == 0 && BlockedHits == 0) return;      // 全被闪避

            PlayerState victim = Defender;
            if (victim == null || !victim.IsAlive) return;

            victim.ApplyStatus(status, PlayerState.StatusDurationTurns, Attacker);
        }

        /// <summary>
        /// 本次攻击是不是**别人反弹过来的**。
        ///
        /// 玩家确认："**可以再被反弹（可循环）**" —— 所以反弹被打回时，
        /// 对方若也有太极，仍可再化解并弹回来。
        /// </summary>
        public bool IsReflected { get; set; }

        /// <summary>反弹实际造成的伤害（0 = 被挡下 / 被闪避 / 没反弹）。</summary>
        public int ReflectedDealt { get; private set; }

        /// <summary>
        /// 反弹的嵌套层数（最初那次攻击是 0）。
        ///
        /// ⚠️ 玩家允许"可循环"，但**没有任何终止条件**的互相反弹会变成
        ///    无限递归（两个武僧各自 5，挡一下弹回去、再挡一下弹回来…）。
        ///    所以这里设一个**硬上限**，超过就不再反弹 ——
        ///    正常对局远远到不了（每次反弹伤害都减半，几层之后就是 0）。
        /// </summary>
        public int ReflectDepth { get; set; }

        /// <summary>反弹的最大嵌套层数（防御性上限，防无限递归）。</summary>
        public const int MaxReflectDepth = 8;

        /// <summary>本次攻击的伤害是否被**太极化解**（武僧）。</summary>
        public bool WasMitigated { get; private set; }

        /// <summary>
        /// 当前对局（在 <see cref="Resolve"/> 时记下）。
        ///
        /// 用途：角色在结算途中需要回读"玩家在收集阶段的作答"
        /// （武僧的"是否发动太极"），那需要 <see cref="GameManager.GetChoiceProvider"/>。
        /// </summary>
        public GameManager Manager { get; private set; }

        /// <summary>被太极**反弹**给攻击方的伤害（0 = 没反弹）。</summary>
        public int ReflectedDamage { get; private set; }

        /// <summary>太极化解后**每段**的伤害（仅 <see cref="WasMitigated"/> 为 true 时有意义）。</summary>
        public int MitigatedPerHit { get; private set; }

        /// <summary>
        /// 这一段上实际发动了**几次**减免（武僧有两个太极时会是 2）。
        /// </summary>
        public int MitigationStages { get; private set; }

        /// <summary>
        /// 询问挨打的人是否用**太极**化解这次攻击（武僧）。
        ///
        /// 只有**挨打的人自己**能化解（不像代挡那样遍历全场）——
        /// 太极是"以柔克刚"的自身姿态。
        ///
        /// <paramref name="totalIncoming"/> 是本次攻击的**总伤害**
        /// （已含攻防加成、已按段数合计）。
        ///
        /// ⚠️ **不要用 out 参数**：C# 要求 out 在提前 return 前也赋值，
        ///    于是"没发动"时会返回 0 并把伤害抹平 —— 我踩过。
        ///    结果写在 <see cref="MitigatedPerHit"/> / <see cref="ReflectedDamage"/> 上，
        ///    调用方只在返回 true 时读。
        /// </summary>
        /// <summary>
        /// 询问守方是否发动减免（武僧的太极）。
        ///
        /// ⚠️ **只处理第一段后的结果**：
        ///    玩家确认"一个太极只对付一段"，
        ///    但**两个太极可以叠在同一段上**逐次折半。
        ///    多段攻击时，应该只有**第一段**被这里减免；
        ///    其余各段后续如需再改（目前规则下不需要）。
        /// </summary>
        private bool TryMitigateByTaichi(GameManager manager, int segmentDamage,
                                        int stagesUsed,
                                        out int reduced, out int reflect)
        {
            reduced = 0;
            reflect = 0;

            // 反弹达到嵌套上限就不再弹（防无限递归）
            if (ReflectDepth >= MaxReflectDepth) return false;

            if (Defender?.Character is not IDamageMitigator mitigator) return false;

            if (!mitigator.TryMitigate(Defender, this, segmentDamage, stagesUsed,
                                      out _, out int totalReduced, out int totalReflect))
            {
                return false;
            }

            reduced = totalReduced;
            reflect = totalReflect;

            WasMitigated = true;
            MitigationStages = stagesUsed + 1;
            ReflectedDamage = totalReflect;

            return true;
        }

        /// <summary>
        /// 询问场上是否有角色愿意**代挡**这一击（圣武士：把目标改成自己）。
        ///
        /// 遍历全部参战玩家（不能只看 A/B —— 2v2 里有四个人），
        /// 第一个答应的就成为新的挨打目标。
        ///
        /// ⚠️ "代挡者自己不会代挡自己"由
        ///    <see cref="IAttackRedirector.TryRedirectToSelf"/> 内部判掉。
        /// </summary>
        private void TryRedirectDefender(GameManager manager)
        {
            if (manager == null) return;

            PlayerState original = Defender;
            if (original == null) return;

            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, original)) continue;
                if (ReferenceEquals(p, Attacker)) continue;

                if (p.Character is IAttackRedirector redirector
                    && redirector.TryRedirectToSelf(p, Attacker, original, manager))
                {
                    Defender = p;
                    WasRedirected = true;
                    return;
                }
            }
        }

        /// <summary>本次攻击是否被"代挡"改过目标。</summary>
        public bool WasRedirected { get; private set; }

        /// <summary>本次攻击是否被**取消**（邪术师）。</summary>
        public bool WasCancelled { get; private set; }

        /// <summary>取消者的名字（供战报显示）。</summary>
        public string CancelledBy { get; private set; }

        /// <summary>
        /// **攻击数字**：发起攻击时用的那个数字（4 / 6 / 8）。
        ///
        /// ⚠️ 必须用**运算前的快照**算：运算源在钩子阶段已经是结果值，
        ///    直接用当前值会把数字算错（本项目在"游侠闪避"上踩过同类坑）。
        ///
        /// ⚠️ **剑客的追加攻击与圣武士的冲击视为 0** —— 它们不是"发起攻击"，
        ///    所以邪术师无法取消（任何手值都不会等于 0，因此天然拦不住）。
        /// </summary>
        public int AttackValue
        {
            get
            {
                if (IsSpecialAttack) return 0;

                // 快照是 int（默认 0 表示"没设"）。两者都有值时才算真数字 ——
                // 0 不是合法手值，所以可以安全地用它当"未设置"的哨兵。
                if (SourceHandValueBefore <= 0 || TargetHandValueBefore <= 0)
                {
                    // 没有快照（直接构造 AttackContext 的测试场景）：
                    // 退化为 RawDamage（构造时由效果类型给出 4/6/8）。
                    return RawDamage;
                }

                return Hand.ComputeSum(SourceHandValueBefore, TargetHandValueBefore);
            }
        }

        /// <summary>
        /// 本次攻击是不是"特殊攻击"（追加攻击 / 冲击）——
        /// 它们**没有攻击数字**，因此不可被取消。
        /// </summary>
        public bool IsSpecialAttack { get; set; }

        /// <summary>
        /// 查询"**某位玩家**对某个抉择的作答"（由 <see cref="GameManager"/> 注入）。
        ///
        /// 邪术师的"是否取消"要在**结算时**读回玩家在收集阶段的回答 ——
        /// 没有它就只能自动取消（那是 AI 的行为）。
        ///
        /// ⚠️ 用带玩家参数的版本：邪术师是**场上的第三方**，
        ///    既不是行动方也不是防守方。
        /// </summary>
        public Func<PlayerState, HandEffect, int, Hand> AnsweredLookupFor { get; set; }

        /// <summary>
        /// 查询"**是否问过**该玩家是否取消攻击"。
        ///
        /// 必须能区分两种"没选"：
        ///   · 没问过（AI）-> 自动取消
        ///   · 问过但选了「不发动」-> 放行
        /// 只有 BoardController 知道答案，所以由它注入。
        /// </summary>
        public Func<PlayerState, bool> AskedCancelLookup { get; set; }

        /// <summary>
        /// 询问场上有没有角色能**取消**这次攻击（邪术师）。
        ///
        /// 遍历全部参战玩家（取消者是第三方，不是挨打的人）。
        /// 第一个答应的就取消成功。
        /// </summary>
        private bool TryCancelAttack(GameManager manager)
        {
            if (manager == null) return false;

            int attackValue = AttackValue;
            if (attackValue <= 0) return false;      // 特殊攻击（视为 0）不可取消

            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;

                // ⚠️ **不能取消自己发起的攻击**（玩家实测抓到：
                //    邪术师取消了自己的攻击 —— 设计上他只能取消**他人**的攻击）。
                if (ReferenceEquals(p, Attacker)) continue;

                // ⚠️ 多人模式里**不能取消队友的攻击**。
                //    队友在攻击敌人，取消掉等于自伤。
                if (manager.IsTeammate(p, Attacker)) continue;

                if (p.Character is WarlockLogic warlock)
                {
                    // 人类的邪术师走"询问式"：按玩家在收集阶段的回答决定。
                    // AI 的邪术师没被问过 -> wasAsked = false -> 自动取消。
                    bool wasAsked = AskedCancelLookup != null && AskedCancelLookup(p);
                    Hand answer = AnsweredLookupFor?.Invoke(p, HandEffect.WarlockCancel, 0);

                    if (warlock.TryCancelAttackWithAnswer(p, attackValue, wasAsked, answer))
                    {
                        WasCancelled = true;
                        CancelledBy = p.Name;
                        return true;
                    }

                    continue;
                }

                if (p.Character is IAttackCanceller canceller
                    && canceller.TryCancelAttack(p, Attacker, Defender, attackValue, manager))
                {
                    WasCancelled = true;
                    CancelledBy = p.Name;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 询问防守方是否用一次免伤挡下**这一段**攻击。
        ///
        /// 判断与消耗都由角色自己在 <see cref="CharacterTemplate.TryDodge"/> 里完成
        /// （游侠把一只"1"变成 2；刺客潜行则不需要消耗）。
        /// </summary>
        private bool TryDodgeThisHit(IPreTurnChoiceSink sink, GameManager manager, int hitIndex)
        {
            var dodgeCtx = new PreTurnChoiceContext(
                Attacker, Defender, Defender.LeftHand, Defender.RightHand)
            {
                Provider = sink as IChoiceProvider,
                ActedHandValueBefore = SourceHandValueBefore,
                TargetHandValueBefore = TargetHandValueBefore,
                AutoResolveChoice = AutoResolveDodge,
                Gatherer = ChoiceGatherer,
                AiDecisionLookup = AiDecisionLookup,
            };

            Defender.Character?.TryDodge(dodgeCtx, sink, hitIndex);
            if (!dodgeCtx.Dodged) return false;

            // 潜行这类"整次攻击都打不到"的免伤，用原因标记出来
            if (!string.IsNullOrEmpty(dodgeCtx.DodgeReason)) DodgeReason = dodgeCtx.DodgeReason;
            return true;
        }

        /// <summary>原始伤害经过倍率后的值（尚未叠加攻防加成）。</summary>
        private int RawDamageScaled()
            => (int)System.Math.Round(
                RawDamage * DamageMultiplier, System.MidpointRounding.AwayFromZero);

        /// <summary>给一段伤害叠加攻防修正。</summary>
        private int ApplyStatBonuses(int damage)
            => ApplyStatBonuses(damage,
                                StatusRules.EffectiveAttackBonus(Attacker),
                                StatusRules.EffectiveDefenseBonus(Defender),
                                IgnorePositiveDefense);

        /// <summary>
        /// **公开静态版**：给一段伤害叠加攻防修正（供 AI 估算斩杀线）。
        ///
        /// ⚠️ 必须是**同一份实现** —— 否则 AI 的斩杀线会与真实结算漂移，
        ///    表现为"AI 以为是斩杀、其实差 1 点"（或反过来该杀不杀）。
        ///
        /// <paramref name="ignorePositiveDefense"/> 对应咒术师的
        /// "无视对方正向防御加成"。
        /// </summary>
        public static int ApplyStatBonuses(int damage, int attackBonus, int defenseBonus,
                                           bool ignorePositiveDefense)
        {
            if (ignorePositiveDefense && defenseBonus > 0) defenseBonus = 0;

            return System.Math.Max(0, damage + attackBonus - defenseBonus);
        }

        /// <summary>
        /// 把总伤害平均分成 <paramref name="hits"/> 段（向下取整，余数补给前几段）。
        /// 例：4 -> 2+2、6 -> 3+3、8 -> 4+4。
        /// </summary>
        public static int SplitDamage(int total, int hits)
        {
            if (hits < 1) hits = 1;
            return total / hits;
        }

        /// <summary>
        /// 尝试消耗防守方一面成型护盾来挡下**这一段**攻击。
        /// 返回 true 表示这一段被挡下。
        /// </summary>
        private bool TryConsumeShield(IChoiceProvider defenderChoice, GameManager manager)
        {
            if (IgnoreShield) return false;

            Hand shieldA = Defender.LeftHand.IsShield ? Defender.LeftHand : null;
            Hand shieldB = Defender.RightHand.IsShield ? Defender.RightHand : null;
            if (shieldA == null && shieldB == null) return false;

            Hand chosen;
            if (shieldA != null && shieldB != null && defenderChoice != null)
            {
                chosen = defenderChoice.AskWhichShieldToBlock(Defender, shieldA, shieldB, this);

                // 防御性校验：上层（UI/AI）可能返回非法结果。
                // 此时退回"优先消耗第一面盾"，而不是静默地让护盾失效。
                if (!ReferenceEquals(chosen, shieldA) && !ReferenceEquals(chosen, shieldB))
                    chosen = shieldA;
            }
            else
            {
                chosen = shieldA ?? shieldB;
            }

            // 只有仍处于 Formed 的盾才能抵挡。
            // （例如上层返回了一面不在候选里的碎盾，或状态在同一回合内被改变。）
            if (chosen == null || chosen.State != ShieldState.Formed || !chosen.TryBlockAttack())
                return false;

            ConsumedShield = chosen;
            manager?.BreakShieldAndNotify(Defender, chosen);
            return true;
        }

        /// <summary>
        /// 计算最终伤害，顺序是：
        ///   1. 基础伤害 × 倍率（游侠强化）
        ///   2. 取整（四舍五入，远离零）
        ///   3. 叠加 攻方.AttackBonus - 守方.DefenseBonus
        ///   4. 下限 0
        ///
        /// 例：游侠强化后 4 -> 6、6 -> 9、8 -> 12；
        ///     咒术师（攻 -1）无视野蛮人（防 +1）打出 4，无倍率时是 3。
        /// </summary>
        /// <summary>
        /// 记录"经倍率、但**尚未叠加攻防加成**"的伤害。
        ///
        /// 真正的攻防加成在逐段结算时施加（每段各自结算，见 <see cref="Resolve"/>）。
        /// 这里**绝不能**把加成算进去 —— 否则会被叠加两次
        /// （我加多段攻击时就犯过这个错，6 个既有测试同时失败）。
        /// </summary>
        private int ApplyBonuses(int baseDamage)
            => (int)System.Math.Round(
                baseDamage * DamageMultiplier, System.MidpointRounding.AwayFromZero);
    }
}
