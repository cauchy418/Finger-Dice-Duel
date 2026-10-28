using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 游荡者·风流剑客 Swashbuckler（攻防 0 / -2）
    ///
    /// 技能（两者都是"把某只手改大 1 点"的代价型能力）：
    ///   1. **闪避**：被攻击时，若有一只手为 1，可把它变成 2 来完全闪避这次攻击。
    ///      两只手都是 1 时可以自己选用哪只手；也可以放弃闪避（照常吃伤害）。
    ///   2. **潇洒气质**：自己发起攻击时，若**另一只手**为 2，可把它变成 3，
    ///      让这次攻击的**基础数值 ×1.5**（4→6、6→9、8→12），之后再算攻防加成。
    ///
    /// ⚠️ **两个技能都可以反复发动，没有"次数"概念。**
    /// 它们不是消耗品，而是"**当前**满足条件就能用"：
    ///   · 每次被攻击时，只要此刻手上有 1，就能翻滚；
    ///   · 每次自己攻击时，只要此刻另一只手是 2，就能强化。
    ///
    /// "永久改变手值"（1→2、2→3）只是**改变数值本身**，不构成"额度用尽"：
    /// 那只手后来又被运算改回 1 / 2 时，技能**照样能再次发动**
    /// （例如 1 -翻滚-> 2，之后某回合 2 + 9 = 11 → 1，就又能翻滚）。
    ///
    /// 早期这里的注释写成"条件一旦用掉就消失 —— 用掉就没了"，
    /// 读起来像"整局只能发动一次"，与实现不符，已修正。
    /// 反复发动的行为由 <c>SwashbucklerReuseTests</c> 覆盖。
    /// </summary>
    public sealed class SwashbucklerLogic : CharacterTemplate, IPreTurnChoiceProvider, IDodgeCapability,
                                      ILethalThresholdAdvisor
    {
        /// <summary>可以拿来闪避的手值。</summary>
        public const int DodgeFromValue = 1;

        /// <summary>闪避后变成的值。</summary>
        public const int DodgeToValue = 2;

        /// <summary>可以拿来强化攻击的手值。</summary>
        public const int PowerFromValue = 2;

        /// <summary>强化后变成的值。</summary>
        public const int PowerToValue = 3;

        /// <summary>强化后的基础伤害倍率。</summary>
        public const float PowerMultiplier = 1.5f;

        /// <summary>
        /// **游荡者·风流剑客的斩杀线**：手上有 **2** 时，潇洒气质让基础值 ×1.5，
        /// 所以斩杀线要相应提高（玩家要求）。
        ///
        /// ⚠️ 顺序必须与实际结算一致：**先 ×1.5，再加攻防** ——
        ///    真实实现是 `RawDamage * DamageMultiplier` 之后再 ApplyStatBonuses，
        ///    所以不能写成 `(结果 + 攻 - 防) * 1.5`。
        ///    4 → 6、6 → 9、8 → 12（与注释里写的一致）。
        /// </summary>
        public int EstimateLethalDamage(LethalContext context, int resultValue,
                                       int attackBonus, int defenseBonus)
        {
            int baseValue = resultValue;

            // 有 2 才能强化（2 是潇洒气质的原料，见 GetDuelistThrustCandidate）
            if (HasDuelistThrustFuel(context))
            {
                baseValue = (int)System.Math.Round(
                    resultValue * PowerMultiplier, System.MidpointRounding.AwayFromZero);
            }

            return AttackContext.ApplyStatBonuses(baseValue, attackBonus, defenseBonus,
                                                  ignorePositiveDefense: false);
        }

        /// <summary>手上是否有"2"可用来强化（运算源之外的那只手）。</summary>
        private static bool HasDuelistThrustFuel(LethalContext context)
            => context.OtherHandValue == PowerFromValue;

        /// <summary>
        /// 这次攻击是否可以被闪避。
        ///
        /// 判定依据是**攻击者是否声明"我的攻击不可闪避"**（<see cref="IDodgeBlocker"/>），
        /// 而不是伤害数值 —— 法师·咒法学派同样会打出 4 / 6，
        /// 而其他角色打出 8 时游荡者·风流剑客**应该**能闪避。
        ///
        /// （曾经按 `Effect == Attack8` 判定，导致所有角色的 8 伤害都无法闪避 —— 真实 bug。）
        /// </summary>
        public static bool IsDodgeable(PreTurnChoiceContext context)
        {
            if (context == null || !context.IsAttack) return false;
            if (CharacterBehaviors.FirstImplementer<IPerHitAttackType>(context.Actor) != null) return true;

            var blocker = context.Actor?.Character as IDodgeBlocker;
            return blocker == null || !blocker.PreventsDodge;
        }

        public override string DisplayName => "游荡者·风流剑客";

        // ============================================================
        // 运算前的抉择
        // ============================================================

        /// <summary>
        /// 由 <see cref="Assistant.BoardController"/> 对**双方**各调用一次，
        /// 所以这里必须自己判断"我是行动方还是防守方"：
        /// 作为防守方时问闪避，作为行动方时问潇洒气质。
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (!context.IsAttack) return;      // 只有攻击才涉及闪避 / 强化

            // ① 我是防守方 -> 问闪避（法师·咒法学派的攻击不可闪避，见 IsDodgeable）
            if (CharacterBehaviors.ActorHas(context.Defender, this))
            {
                if (IsDodgeable(context)) AddDodgeRequests(context, requests);
                return;                          // 同一次运算不会既闪避又强化
            }

            // ② 我是行动方 -> 问潇洒气质
            if (CharacterBehaviors.ActorHas(context.Actor, this))
            {
                AddDuelistThrustRequest(context, requests);
            }
        }

        /// <summary>
        /// 为**每一段攻击**各生成一次闪避询问。
        ///
        /// 为什么需要多份：一次攻击可能是多段（武僧·忍宗两段、战士·剑士追加算独立一次），
        /// 而 <c>GameManager.ExecuteTurn</c> 一旦开始就一次跑完，无法中途再问玩家。
        /// 所以必须**在运算前**把每一段的询问都准备好，
        /// 结算时第 N 段取第 N 个答案（见 <see cref="ChoiceRequest.Variant"/>）。
        ///
        /// ⚠️ 这段逻辑原来写在 <c>BoardController.AppendDodgeChoices</c> 里，
        /// 与这里重复生成 —— 结果同一次攻击出现两个闪避槽位（variant 0 与 1），
        /// 而实际只有 hitIndex=0 一段，真正读的槽位答不上来，技能就"发不动"。
        /// 现在统一由角色自己生成，控制器不再插手。
        /// </summary>
        private static void AddDodgeRequests(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            PlayerState defender = context.Defender;
            if (defender == null) return;

            int slots = CountDodgeSlots(context);
            if (slots <= 0) return;

            for (int i = 0; i < slots; i++)
            {
                // 每次重新枚举：同一位候选不会被两个槽位重复占用
                List<Hand> candidates = FindUsableHands(defender, DodgeFromValue);
                if (candidates.Count == 0) break;

                requests.Add(new ChoiceRequest(
                    HandEffect.SwashbucklerDodge,
                    slots > 1
                        ? $"游荡者·风流剑客被攻击（第 {i + 1} 段，共 {slots} 段）：" +
                          $"用一只「1」变成 {DodgeToValue} 闪避这一段？"
                        : $"游荡者·风流剑客被攻击：可以用一只「1」变成 {DodgeToValue} 来完全闪避这次攻击。要用哪只手？",
                    candidates,
                    allowDecline: true,
                    declineResult: null,             // 放弃 = 不闪避，正常吃伤害
                    decider: defender,               // 闪避是**防守方**自己的决定
                    acceptLabel: candidates.Count == 1 ? $"潇洒无畏（变 {DodgeToValue}）" : null,
                    variant: i));
            }
        }

        /// <summary>
        /// 这次攻击最多能闪几段 = min(攻击段数, 手上可用的「1」的个数)。
        ///
        /// 攻击段数：主攻击段数（武僧·忍宗 2、其余 1）+ 战士·剑士的追加攻击（独立一次）。
        /// </summary>
        private static int CountDodgeSlots(PreTurnChoiceContext context)
        {
            int segments = CharacterFactory.MaxAttackHits(context.Actor);

            if ((context.Actor?.Character as IExtraAttackProvider)?.WillExtraAttack(context) == true)
                segments++;

            int available = FindUsableHands(context.Defender, DodgeFromValue).Count;
            return segments < available ? segments : available;
        }

        private static void AddDuelistThrustRequest(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            Hand other = GetDuelistThrustCandidate(context.Actor, context.ActedHand);
            if (other == null) return;

            requests.Add(new ChoiceRequest(
                HandEffect.SwashbucklerDuelistThrust,
                $"游荡者·风流剑客攻击：另一只手是 2，可以把它变成 {PowerToValue}，" +
                $"让这次攻击的基础值 ×{PowerMultiplier}（4→6、6→9、8→12），" +
                $"并使本次攻击距离∞，命中附流血2回合。要发动吗？",
                new List<Hand>(1) { other },
                allowDecline: true,
                declineResult: null,             // 放弃 = 不加强
                decider: context.Actor,
                acceptLabel: $"强化（变 {PowerToValue}）"));
        }

        /// <summary>
        /// 找出可用于潇洒气质的那只手（**另一只手**，不是运算源）。
        ///
        /// 规则：用一只手发起攻击后，若**另一只手**为 2，可以把它变成 3 强化这次攻击。
        /// 运算源就是攻击手（值 4/6/8），本来就不可能是 2。
        ///
        /// 收集阶段与结算阶段**必须用同一个判定**，否则会像之前那样
        /// "结算认得另一只手、收集却还在扫运算源"，导致提示永远不出现。
        /// </summary>
        private static Hand GetDuelistThrustCandidate(PlayerState actor, Hand actedHand)
        {
            if (actor == null || actedHand == null) return null;

            Hand other = ReferenceEquals(actedHand, actor.LeftHand)
                ? actor.RightHand
                : actor.LeftHand;

            if (other == null || other.IsLocked) return null;
            return other.Value == PowerFromValue ? other : null;
        }

        /// <summary>供上层查询"这只手能不能拿来闪避"。</summary>
        public List<Hand> GetDodgeHands(PlayerState owner) => FindUsableHands(owner, DodgeFromValue);

        /// <summary>供上层查询"这一击要不要问闪避"。</summary>
        public bool CanDodgeAgainst(PreTurnChoiceContext context) => IsDodgeable(context);

        /// <summary>找出主人手上值等于 <paramref name="value"/>、且未被锁住的手。</summary>
        private static List<Hand> FindUsableHands(PlayerState owner, int value)
        {
            var result = new List<Hand>(2);
            if (owner == null) return result;

            foreach (Hand hand in owner.Hands)
            {
                if (hand.Value == value && !hand.IsLocked) result.Add(hand);
            }

            return result;
        }

        // ============================================================
        // 结算：闪避
        // ============================================================

        public override void TryDodge(PreTurnChoiceContext context, IPreTurnChoiceSink sink, int hitIndex)
        {
            if (context == null) return;
            if (!context.IsAttack) return;

            PlayerState self = context.Defender;
            if (!CharacterBehaviors.ActorHas(self, this)) return;

            // 攻击者若声明"我的攻击不可闪避"（法师·咒法学派），直接跳过。
            // 判定看**攻击者身份**，不看伤害数值。
            if (!IsDodgeable(context)) return;

            Hand chosen = ReadDodgeChoice(self, context, sink, hitIndex);
            if (chosen == null) return;                      // 玩家放弃闪避
            if (!self.Owns(chosen)) return;                  // 非法结果，视为放弃
            if (chosen.IsLocked) return;
            if (chosen.Value != DodgeFromValue) return;      // 手值已经变了，条件不成立

            // 消耗：1 -> 2
            chosen.SetValue(DodgeToValue);

            context.Dodged = true;
            context.ConsumedHand = chosen;
            context.Manager?.NoteSkill(self, "潇洒无畏", $"{CombatText.HandName(self, chosen)} 1→2，闪避第 {hitIndex + 1} 段");
        }

        /// <summary>
        /// 取这一次闪避要消耗的那只手。
        ///
        /// 每段攻击调用一次，因此**每次都重新枚举候选** —— 上一段已经用掉的那只"1"
        /// 这时已经变成 2、不在候选里了，所以不会被重复选中。
        ///
        /// 优先级：
        ///   ① 上层回调（<see cref="PreTurnChoiceContext.Gatherer"/>）—— 正常对局走这条，
        ///      会把问题**在结算过程中**抛给玩家，所以每一段都能单独询问；
        ///   ② 自动选第一个可用的（没有回调的单元测试场景，避免卡住）。
        /// </summary>
        private static Hand ReadDodgeChoice(PlayerState self, PreTurnChoiceContext context,
                                            IPreTurnChoiceSink sink, int hitIndex)
        {
            List<Hand> dodgers = FindUsableHands(self, DodgeFromValue);
            if (dodgers.Count == 0) return null;

            var request = new ChoiceRequest(
                HandEffect.SwashbucklerDodge,
                "游荡者·风流剑客被攻击：可以用一只「1」变成 2 来完全闪避这一段攻击。要用哪只手？",
                dodgers,
                allowDecline: true,
                declineResult: null,
                decider: self,
                variant: hitIndex);          // 第几段就用第几个答案

            // ① 显式要求自动（直接构造 AttackContext 的单元测试）：挑一只可用的
            if (context.AutoResolveChoice) return dodgers[0];

            // ② AI 已经替自己决定过这一段 -> 直接采用。
            //    必须**优先于回读 sink**：sink 跨回合保留，旧答案会被误当成本回合的决定，
            //    表现为"AI 从不发动技能"（玩家实测）。
            Hand aiPick = context.GetAiDecision(HandEffect.SwashbucklerDodge, hitIndex);
            if (aiPick != null) return ContainsHand(dodgers, aiPick) ? aiPick : null;

            // ② 上层回调：把问题**在结算过程中**抛给玩家，所以每一段都能单独询问
            if (context.Gatherer != null)
            {
                Hand picked = context.Gatherer.GatherChoice(request, self);
                if (picked == null) return null;                        // 玩家放弃这一段
                return ContainsHand(dodgers, picked) ? picked : null;    // 非法结果视为放弃
            }

            // ③ 都没有：回退到抉择器 / sink
            return PreTurnChoiceReader.Read(sink, context.Provider, request, self);
        }

        private static bool ContainsHand(List<Hand> list, Hand hand)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], hand)) return true;

            return false;
        }

        // ============================================================
        // 结算：潇洒气质
        // ============================================================

        public override void OnResolveAttack(AttackContext context)
        {
            if (context == null) return;

            // 强化只对"自己发起的攻击"生效
            if (!CharacterBehaviors.ActorHas(context.Attacker, this)) return;

            Hand chosen = ReadDuelistThrustChoice(context);
            if (chosen == null) return;                      // 玩家放弃强化
            if (!context.Attacker.Owns(chosen)) return;      // 非法结果，视为放弃
            if (chosen.IsLocked) return;
            if (chosen.Value != PowerFromValue) return;      // 另一只手已经变了，条件不成立

            // 消耗：2 -> 3
            chosen.SetValue(PowerToValue);

            // 先乘倍率、再算攻防加成（由 AttackContext.ApplyBonuses 保证顺序）
            context.DamageMultiplier = PowerMultiplier;
            context.InflictsBleed = true;
            context.Manager?.NoteSkill(context.Attacker, "潇洒气质", $"{CombatText.HandName(context.Attacker, chosen)} 2→3，基础伤害×1.5、距离∞");
        }

        /// <summary>
        /// 回读强化抉择：优先读 sink（正常流程），没有记录则当场问抉择器。
        ///
        /// 判定复用 <see cref="GetDuelistThrustCandidate"/>，与收集阶段**完全一致**。
        /// </summary>
        private static Hand ReadDuelistThrustChoice(AttackContext context)
        {
            Hand other = GetDuelistThrustCandidate(context.Attacker, context.SourceHand);
            if (other == null) return null;

            // AI 已经决定过 -> 直接采用，避免被 sink 里的旧记录顶掉
            Hand aiPick = context.GetAiDecision(HandEffect.SwashbucklerDuelistThrust);
            if (aiPick != null)
            {
                return ReferenceEquals(aiPick, other) ? aiPick : null;
            }

            var request = new ChoiceRequest(
                HandEffect.SwashbucklerDuelistThrust,
                $"游荡者·风流剑客攻击：另一只手是 2，可以把它变成 {PowerToValue}，" +
                $"让这次攻击的基础值 ×{PowerMultiplier}（4→6、6→9、8→12），" +
                $"并使本次攻击距离∞，命中附流血2回合。要发动吗？",
                new List<Hand>(1) { other },
                allowDecline: true,
                declineResult: null,             // 放弃 = 不强化
                decider: context.Attacker,
                acceptLabel: $"强化（变 {PowerToValue}）");

            return PreTurnChoiceReader.Read(
                context.AttackerChoiceSink, context.AttackerProvider, request, context.Attacker);
        }

        /// <summary>
        /// 找出可用于潇洒气质的手。
        ///
        /// 规则：**用一只手发起攻击后，若另一只手为 2**，可以把它变成 3 来强化这次攻击。
        /// 所以候选只看"另一只手"，**不是**运算源 ——
        /// 运算源是攻击手（值 4/6/8），本来就不可能是 2。
        ///
        /// 判定统一走 <see cref="GetDuelistThrustCandidate"/>，收集与结算共用一个实现。
        /// （我曾把这两处分别实现，结果"结算认得另一只手、收集还在扫运算源"，
        /// 提示永远不出现 —— 真实 bug。）

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>
        /// 游荡者·风流剑客：普通是**挥砍**；用 2 强化的攻击是**穿刺**（玩家指定）。
        ///
        /// ⚠️ 判据用 `context.DamageMultiplier` —— 潇洒气质就是把它设成 1.5。
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            if (context != null && context.DamageMultiplier > 1f)
                return AttackDamageType.Piercing;

            return AttackDamageType.Slashing;
        }

}
}
