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

            // 1. 攻击方钩子介入（如咒术师设置 IgnoreShield / 修改 FinalDamage / 游侠设置倍率）
            Attacker.Character?.OnResolveAttack(this);

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

            IPreTurnChoiceSink sink = defenderChoice as IPreTurnChoiceSink;

            for (int i = 0; i < hits; i++)
            {
                // 2a. 闪避（游侠每次消耗一只"1"；刺客潜行则整次攻击都打不到）
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

                Defender.TakeDamage(perHit);
                AppliedDamage += perHit;
                LandedHits++;
            }
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
        {
            int atkBonus = Attacker.Character?.AttackBonus ?? 0;
            int defBonus = Defender.Character?.DefenseBonus ?? 0;

            // 咒术师特性：无视对方的"正向防御加成"
            if (IgnorePositiveDefense && defBonus > 0)
                defBonus = 0;

            return System.Math.Max(0, damage + atkBonus - defBonus);
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
