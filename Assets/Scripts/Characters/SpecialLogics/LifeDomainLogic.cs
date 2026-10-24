using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>
    /// 牧师·生命领域 LifeDomain（攻防 **-1 / -1**）
    ///
    /// 技能：
    ///   1. **9 = 回复**：可以选择把这次回复给自己或对手（回复量 9）。
    ///   2. **4 / 6 / 8 二选一**：可以当**攻击**打，也可以当**回复**用。
    ///      选回复时回复量就是该数字本身（4 回 4、6 回 6、8 回 8），
    ///      **不受攻击加成影响**（不像攻击那样加/减攻防）。
    ///      目标同样可以是自己或对手（给他人回复是为多人玩法预留的辅助能力）。
    ///   3. **双 9 额外回复**：运算后双手同时为 9 时，额外再触发一次 9 点回复，
    ///      这次**也由玩家选择目标**（不再固定回自己）。
    ///
    /// 抉择顺序（都在运算前收集，结算时只回读）：
    ///   · 4/6/8 -> 先问"攻击还是回复"，再问"回复给谁"；
    ///   · 9     -> 只问"回复给谁"（本次运算已经出了 9，不存在"选攻击"这一步）；
    ///   · 双 9  -> 先问"**额外**回复给谁"，再问"本次回复给谁"。
    ///
    /// 本角色没有跨回合状态。
    /// </summary>
    public sealed class LifeDomainLogic : CharacterTemplate, IPreTurnChoiceProvider,
                                      IHealOverride, IHealTargetAdvisor
    {
        /// <summary>9 的回复量（= 数字 9）。</summary>
        public const int HealAmount = Hand.HealAmount;

        /// <summary>双手同时为该值时触发额外回复。</summary>
        public const int DoubleNineValue = 9;

        public override string DisplayName => "牧师·生命领域";

        public override int DefaultAttackBonus => -1;
        public override int DefaultDefenseBonus => -1;

        /// <summary>
        /// **攻击距离无限**（玩家确认）：牧师·生命领域不受座位距离限制。
        /// </summary>
        public override int BaseAttackRange => InfiniteAttackRange;

        /// <summary>
        /// **牧师·生命领域是唯一需要为回复指定目标的角色。**
        ///
        /// 其余角色的 9 都是"自动回自己"（`GameManager` 的默认治疗），
        /// 所以基类默认不对 9 提问 —— 玩家实测要求："除了牧师·生命领域外，
        /// 其余角色运算出 9 也是自动回复，无需询问"。
        ///
        /// 牧师·生命领域能把回复给对手（实现了 <see cref="IHealOverride"/>），
        /// 所以两种情况都要选人：
        ///   · 4/6/8 -> 不管选攻击还是回复，都要指定对象；
        ///   · 9     -> 回复要指定回给谁。
        ///
        /// 护盾 5 / 无效果仍然不问。
        /// </summary>
        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context == null) return false;

            switch (context.Effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                case HandEffectType.Heal:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 这次运算**可选的回复量**：
        ///   9 -> 9；4/6/8 -> 该数字本身；其他 -> 0（不可回复）。
        /// </summary>
        public static int HealAmountFor(HandEffectType effect)
        {
            switch (effect)
            {
                case HandEffectType.Heal: return HealAmount;
                case HandEffectType.Attack4: return 4;
                case HandEffectType.Attack6: return 6;
                case HandEffectType.Attack8: return 8;
                default: return 0;
            }
        }

        /// <summary>双 9 额外回复的固定量。</summary>
        public const int ExtraHealAmount = HealAmount;

        // ============================================================
        // 运算前的抉择
        // ============================================================

        /// <summary>
        /// 收集牧师·生命领域需要的抉择。顺序很重要：
        /// 先"额外回复（双 9）"，再"本次回复给谁"，最后"4/6/8 攻击还是回复"。
        ///
        /// ⚠️ 判定统一用 <see cref="PreTurnChoiceContext.IsAttack"/>
        /// （它内部用"运算前的快照"重算，所以不会把 9 误判成攻击）。
        /// </summary>
        // ============================================================
        // 选定目标手**之前**的抉择：4/6/8 攻击还是回复
        // ============================================================

        /// <summary>
        /// 运算出 4/6/8 时，在**选目标手之前**先问"当攻击打还是当回复用"。
        ///
        /// 玩家要求的顺序：
        /// ```
        /// 运算出 4 -> 右侧弹出「攻击 / 回复」
        ///           -> 选攻击：再点手牌选攻击目标
        ///           -> 选回复：再点手牌选回复目标
        /// ```
        /// 候选用牧师·生命领域**自己的一只手**当"发动"的载体（点它 = 选回复）。
        ///
        /// ⚠️ 这一步的答案要跨越"选目标手"才能到达结算，而那段流程会调用
        /// <see cref="Assistant.BoardController"/> 的回合边界清理。
        /// 之前的实现被清掉了答案（表现为"选了回复却按攻击结算"）；
        /// 现在控制器只在**行动方真正变化**时清理，本答案得以保留。
        /// </summary>
        public override void CollectPreTargetChoices(PreTurnChoiceContext context,
                                                     IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (!context.IsAttack) return;      // 只有 4/6/8 有这一步（9 直接问回复目标）

            int heal = HealAmountFor(context.Effect);
            if (heal <= 0) return;

            requests.Add(new ChoiceRequest(
                HandEffect.LifeDomainAttackOrHeal,
                $"牧师·生命领域运算出 {heal}：当攻击打（{heal} 点基础伤害，再算攻防），" +
                $"还是当回复用（回复 {heal} 点，不受攻防影响）？",
                new List<Hand>(1) { context.Actor.LeftHand },
                allowDecline: true,                 // 放弃 = 当攻击打（默认）
                declineResult: null,
                decider: context.Actor,
                acceptLabel: $"回复 {heal}"));
        }

        /// <summary>
        /// 玩家是否把这次 4/6/8 **当回复用**了。
        ///
        /// 只认**显式记录**的答案（<see cref="IPreTurnChoiceSink.GetExplicitChoice"/>）：
        ///   · 选了回复 -> 记录了一只手
        ///   · 选了攻击 -> 记录为 null（放弃值就是 null）
        ///
        /// ⚠️ 不能用 <see cref="IPreTurnChoiceSink.GetChoice"/> —— 它会套用
        /// 抉择自身的回退值，于是"选了攻击"会被误判成"选了回复"。
        /// </summary>
        public static bool ChoseToHeal(PlayerState owner, GameManager manager,
                                       IPreTurnChoiceSink sink) 
            => ChoseToHeal(owner, manager, sink, null);

        /// <summary>
        /// 同上，但可以额外传一个"先前答案查询"（
        /// <see cref="GameManager.AnsweredLookup"/>）。
        ///
        /// ⚠️ 为什么要它：两段抉择之间会重建队列、sink 可能被清空，
        /// 于是"选了回复"会读成 null 而被当成"选攻击"（真实 bug）。
        /// 上层抄下来的那份不受清空影响，所以优先读它。
        /// </summary>
        public static bool ChoseToHeal(PlayerState owner, GameManager manager,
                                       IPreTurnChoiceSink sink,
                                       System.Func<HandEffect, int, Hand> answeredLookup)
        {
            Hand viaLookup = answeredLookup?.Invoke(HandEffect.LifeDomainAttackOrHeal, 0);
            if (viaLookup != null) return true;

            Hand recorded = sink?.GetExplicitChoice(HandEffect.LifeDomainAttackOrHeal);
            if (recorded != null) return true;

            // 没有记录时问一下抉择器（AI 的答案走这条）
            var candidates = new List<Hand>(1);
            if (owner != null) candidates.Add(owner.LeftHand);

            var probe = new ChoiceRequest(
                HandEffect.LifeDomainAttackOrHeal,
                "牧师·生命领域：这次当回复用吗？",
                candidates,
                allowDecline: true,
                declineResult: null,
                decider: owner);

            IChoiceProvider provider = manager?.GetChoiceProvider(owner);
            return provider?.ChooseHandFor(probe, owner) != null;
        }

        // ============================================================
        // 选定目标手**之后**的抉择：回复给谁
        // ============================================================

        /// <summary>
        /// 收集回复目标。顺序按玩家要求：
        ///   · 双 9 -> **两次**（先额外回复、再本次回复）
        ///   · 9    -> 一次（本次回复）
        ///   · 4/6/8 选了回复 -> 一次（这次回复给谁）
        ///
        /// ⚠️ 判定统一用 <see cref="PreTurnChoiceContext.IsAttack"/>
        /// （它内部用"运算前的快照"重算，所以不会把 9 误判成攻击）。
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;

            // ① 双 9 的额外回复：先问目标（**必选**，玩家要求不能放弃）
            //
            // 用**预测**（运算后是否双 9），因为收集发生在运算之前。
            // 这与战士·剑士"追加攻击"的判定同类：都是运算前可算的。
            if (HasDoubleNineAfter(context))
                requests.Add(BuildTargetRequest(context, HandEffect.LifeDomainExtraHealTarget,
                    $"牧师·生命领域双手都是 {DoubleNineValue}：额外回复 {ExtraHealAmount} 点给谁？"));

            // ② 本次运算出 9 -> 问这次回复给谁（**必选**）
            if (context.Effect == HandEffectType.Heal)
            {
                requests.Add(BuildTargetRequest(context, HandEffect.LifeDomainHealTarget,
                    $"牧师·生命领域打出 {HealAmount}：这次回复给谁？"));
                return;
            }

            // ③ 4/6/8：**前置抉择里选了回复**才需要问"回复给谁"（**必选**）。
            //
            // 「攻击还是回复」发生在选定运算源之后、本方法之前，所以这里读得到答案。
            if (context.IsAttack)
            {
                if (!context.ChoseToHeal()) return;      // 选了攻击 -> 直接打，不问目标

                requests.Add(BuildTargetRequest(context, HandEffect.LifeDomainHealTarget,
                    $"牧师·生命领域这次回复 {HealAmountFor(context.Effect)} 点给谁？"));
            }
        }

        /// <summary>
        /// 把**所有其他玩家**的手都加进候选。
        ///
        /// ⚠️ 不能用 `manager.GetOpponent(owner)` —— 那只给"第一位对手"。
        ///    玩家确认的规则是"回复可以给任何人"：
        ///    2v2 里有队友 + 两名对手，只列第一位对手会让玩家**点不到其他人**。
        /// </summary>
        private static void AddOtherPlayersHands(List<Hand> candidates,
                                                 PlayerState owner, GameManager manager)
        {
            if (candidates == null || manager == null) return;

            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive || ReferenceEquals(p, owner)) continue;

                AddOwnerHands(candidates, p);
            }
        }

        /// <summary>
        /// 把"牧师·生命领域在攻击/回复抉择里选的那只手"翻译成回复目标。
        ///
        /// 没选 / 选了攻击 → 返回 null（表示"不回复，正常攻击"）。
        /// </summary>
        public static PlayerState ResolveHealChoiceTarget(PlayerState owner, GameManager manager,
                                                          IPreTurnChoiceSink sink)
        {
            var candidates = new List<Hand>(8);
            AddOwnerHands(candidates, owner);
            AddOtherPlayersHands(candidates, owner, manager);

            var request = new ChoiceRequest(
                HandEffect.LifeDomainAttackOrHeal,
                "牧师·生命领域：这次当回复用吗？",
                candidates,
                allowDecline: true,
                declineResult: null,        // 放弃 = 当攻击打
                decider: owner);

            IChoiceProvider provider = manager?.GetChoiceProvider(owner);
            Hand chosen = PreTurnChoiceReader.Read(sink, provider, request, owner);
            if (chosen == null) return null;

            return OwnerOf(chosen, owner, manager);
        }

        /// <summary>把一只手翻译成它的主人；认不出来时返回 <paramref name="fallback"/>。</summary>
        private static PlayerState OwnerOf(Hand hand, PlayerState owner, GameManager manager)
        {
            return manager?.OwnerOfHand(hand) ?? owner;
        }

        /// <summary>
        /// 构造一个"回复给谁"的抉择。
        ///
        /// ⚠️ **不允许放弃**（玩家明确要求）：选了回复就必须回复、
        /// 运算出 9 必须回复、双 9 的两段回复都必须回复。
        /// 所以没有"不回复"按钮 —— 点一只手即可，点自己的手就是回自己。
        /// </summary>
        private static ChoiceRequest BuildTargetRequest(PreTurnChoiceContext context,
                                                        HandEffect effect, string prompt,
                                                        System.Func<bool> skipWhen = null)
        {
            var candidates = new List<Hand>(4);
            AddOwnerHands(candidates, context.Actor);
            if (context.Manager != null) AddOtherPlayersHands(candidates, context.Actor, context.Manager);
            else AddOwnerHands(candidates, context.Defender);

            return new ChoiceRequest(
                effect,
                prompt + "（点任意一只手代表那位玩家）",
                candidates,
                allowDecline: false,
                declineResult: null,
                decider: context.Actor,
                skipWhen: skipWhen);
        }

        private static void AddOwnerHands(List<Hand> into, PlayerState owner)
        {
            if (owner == null) return;
            into.Add(owner.LeftHand);
            into.Add(owner.RightHand);
        }

        // ============================================================
        // 结算：主回复
        // ============================================================

        /// <summary>
        /// 接管回复效果的结算（<see cref="IHealOverride"/>）。返回**实际被回复的玩家**。
        ///
        /// 回复量由 <see cref="GameManager"/> 用 <see cref="HealAmountFor"/> 算好后传入，
        /// 因此"9 回 9、4 回 4"这类差异不需要在这里判断。
        /// </summary>
        public PlayerState ApplyHeal(PlayerState owner, GameManager manager, IPreTurnChoiceSink sink)
        {
            PlayerState target = ResolveTarget(owner, manager, sink, HandEffect.LifeDomainHealTarget);
            return target;
        }

        /// <summary>每段牧师·生命领域回复先净化再回血；满血也净化，腐朽清除后不再阻止本次回复。</summary>
        public static void CleanseAndHeal(PlayerState owner, PlayerState target, int amount, GameManager manager)
        {
            if (target == null) return;
              int removed = target.Statuses.Count + target.Vulnerabilities.Count;
            target.ClearStatuses();
            target.Heal(amount);
            if (removed > 0) manager?.NoteSkill(owner, "净化", $"{target.Name} 清除 {removed} 种负面状态");
        }

        // ============================================================
        // 结算：运算后（双手同为 9 -> 额外回复）
        // ============================================================

        /// <summary>
        /// 运算出双 9 时的**额外**回复：同样由玩家选目标（不再固定回自己），
        /// 且与主效果叠加。
        ///
        /// ⚠️ 叠加是规则明确的：8 + 1 = 9 且另一只手也是 9 时，
        /// 本次回复 9 + 额外回复 9 = 共 18 点（两次各自选目标）。
        ///
        /// 判定用"运算**后**的双手值"（运算源已是结果值，另一只手未变）。
        /// </summary>
        public override void OnBeforeEffectResolved(PlayerState owner, Hand actedHand,
                                                    int targetValue, HandEffectType effect,
                                                    GameManager manager, IPreTurnChoiceSink choiceSink)
        {
            if (owner == null) return;
            if (!HasDoubleNineNow(owner)) return;
            // 额外回复的目标由玩家在运算前选好
            PlayerState target = ResolveTarget(owner, manager, choiceSink,
                                               HandEffect.LifeDomainExtraHealTarget);
            int before = target?.Health ?? 0;
            CleanseAndHeal(owner, target, ExtraHealAmount, manager);
            manager?.NoteSkill(owner, "双九回复", $"{target?.Name} 回复 {(target?.Health ?? 0) - before}");
        }

        /// <summary>当前（运算后）是否双手同时为 9。</summary>
        public static bool HasDoubleNineNow(PlayerState owner)
            => owner != null
               && owner.LeftHand.Value == DoubleNineValue
               && owner.RightHand.Value == DoubleNineValue;

        /// <summary>
        /// **预测**：这次运算完成后，该玩家是否双手同时为 9。
        ///
        /// 为什么需要预测：抉择全部要在**运算前**收集（`ExecuteTurn` 一次跑完），
        /// 而双 9 的判定按"运算后"的手值 —— 但它在运算前**就能算出来**：
        /// 运算源会被改写成结果值，另一只手不变。
        ///
        /// 例：运算源 8 + 目标 1 = 9，另一只手本来就是 9
        ///     -> 运算后双手都是 9，双 9 成立。
        ///
        /// 这与战士·剑士"追加攻击"的判定是同一类做法
        /// （4/6/8 且另一只手同值 —— 也是运算前可算）。
        ///
        /// ⚠️ 用**运算前的快照**（<see cref="PreTurnChoiceContext.ActedHandValueBefore"/>），
        ///    不能用 <c>ActedHand.Value</c> —— 结算钩子里那只手已经是结果值了。
        /// </summary>
        public static bool HasDoubleNineAfter(PreTurnChoiceContext context)
        {
            if (context?.Actor == null || context.ActedHand == null) return false;

            int actedBefore = context.ActedHandValueBefore ?? context.ActedHand.Value;
            int targetValue = context.TargetHandValueBefore ?? (context.TargetHand?.Value ?? 0);

            // 运算后：运算源 = 结果值，另一只手不变
            int actedAfter = Hand.ComputeSum(actedBefore, targetValue);

            PlayerState owner = context.Actor;
            Hand other = ReferenceEquals(context.ActedHand, owner.LeftHand)
                ? owner.RightHand
                : owner.LeftHand;

            return actedAfter == DoubleNineValue && other.Value == DoubleNineValue;
        }

        // ============================================================
        // 工具
        // ============================================================

        /// <summary>
        /// 把"玩家选的那只手"翻译成"那位玩家"。没选过 / 选了无效的手时回退为自己。
        ///
        /// 走 <see cref="PreTurnChoiceReader"/>：优先读抉择器 / sink（正常流程），
        /// 都没有则用放弃值。
        /// </summary>
        public static PlayerState ResolveTarget(PlayerState owner, GameManager manager,
                                                IPreTurnChoiceSink sink, HandEffect effect)
        {
            var candidates = new List<Hand>(8);
            AddOwnerHands(candidates, owner);
            AddOtherPlayersHands(candidates, owner, manager);

            var request = new ChoiceRequest(
                effect,
                "牧师·生命领域：这次回复给谁？",
                candidates,
                allowDecline: true,
                declineResult: owner?.LeftHand,
                decider: owner);

            IChoiceProvider provider = manager?.GetChoiceProvider(owner);
            Hand chosen = PreTurnChoiceReader.Read(sink, provider, request, owner);
            if (chosen == null) return owner;

            // ⚠️ 归属判断必须查**全部参战玩家**，不能用 PlayerA/PlayerB ——
            //    2v2 里 A 的队友是 C/D，用两人假设会把队友错判成"不是任何人的手"，
            //    于是回复落到默认的自己身上（玩家实测就是这个问题）。
            PlayerState byOwner = manager?.OwnerOfHand(chosen);
            if (byOwner != null) return byOwner;

            return owner;
        }

        /// <summary>
        /// **AI 该把回复给谁**（玩家给的规则）：
        ///
        /// > 只有 2v2 什么时候回给队友要写，目前写成**优先给一队血量最低的人回复**
        ///
        /// 顺序：
        ///   ① 2v2（有队友）时：**自己这一队里血量最低的存活者**（含自己）
        ///   ② 队友都满血 / 只有自己：回自己
        ///
        /// ⚠️ 为什么不是"挑血最少的那个"（连敌人也算）—— 敌人血少是**好事**，
        ///    给他回血是自伤。玩家明确说的是"**一队**血量最低的人"。
        /// </summary>
        public static PlayerState BestHealTargetFor(PlayerState owner, GameManager manager)
        {
            if (owner == null || manager == null) return owner;

            PlayerState best = null;
            IReadOnlyList<PlayerState> all = manager.Players;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;

                // 只看"自己这一队"（自己也算）
                if (!ReferenceEquals(p, owner) && !manager.IsTeammate(owner, p)) continue;

                if (best == null || p.Health < best.Health) best = p;
            }

            return best ?? owner;
        }

        /// <summary>AI 的回复目标 —— 见 <see cref="IHealTargetAdvisor"/>。</summary>
        public PlayerState BestHealTarget(PlayerState owner, GameManager manager)
            => BestHealTargetFor(owner, manager);

        /// <summary>兼容旧调用：默认解析"本次回复"的目标。</summary>
        public static PlayerState ResolveTarget(PlayerState owner, GameManager manager,
                                                IPreTurnChoiceSink sink)
            => ResolveTarget(owner, manager, sink, HandEffect.LifeDomainHealTarget);

        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>
        /// 牧师·生命领域：对**攻击距离 1** 的是钝击，**距离 1 以外**的是穿刺（玩家指定）。
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => ByDistance(AttackDamageType.Bludgeoning, AttackDamageType.Piercing);

}
}
