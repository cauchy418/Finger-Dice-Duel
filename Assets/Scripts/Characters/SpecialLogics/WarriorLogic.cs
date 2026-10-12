using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 战士（Warrior）特殊逻辑：
    ///
    /// 【技能一 · 连锁破盾】
    ///   当战士自身的一面盾牌破碎时（无论是被攻击破碎，还是运算前主动破碎已有盾牌），
    ///   战士可以选择场上任意一面【完整（Formed）】的盾牌，使其立即破碎。
    ///
    ///   ⚠️ **必须在运算前收集这个抉择。**
    ///   `GameManager.ExecuteTurn` 一旦开始就一次跑完，没有"暂停 / 恢复"机制；
    ///   在结算钩子里直接问 provider 的话，**人类玩家永远看不到弹窗**
    ///   （AI 能当场作答，所以表现为"有时生效"）。这与野蛮人锁手是同一模式。
    ///
    ///   判定"自己的盾这次会不会碎"只需两个事实（都在运算前可知）：
    ///     · 自己当行动方，且运算源是 `Formed` 的盾 → 运算前会主动破碎；
    ///     · 对手当行动方，且这次会打出攻击、又选中了自己的 `Formed` 盾 → 会被打碎。
    ///
    /// 【技能二 · 盾牌修复】
    ///   当战士新运算出 5、在某只手上合成一面盾牌时，
    ///   若其【另一只手】为破碎（Broken）状态的盾牌，则将其修复为 Formed。
    ///
    /// 依赖：
    ///   - `IPreTurnChoiceProvider`：运算前声明"要不要发动连锁破盾、打碎哪面盾"。
    ///   - `GameManager.BreakShieldAndNotify(...)`：统一破盾入口，保证连锁链正确触发。
    /// </summary>
    public class WarriorLogic : CharacterTemplate, IPreTurnChoiceProvider
    {
        public override string DisplayName => "Warrior";

        /// <summary>
        /// 运算前收集"要不要发动连锁破盾、打碎哪面盾"。
        ///
        /// 只在**自己的盾这次会碎**时才提问（见类注释里的两条判定）。
        /// 场上没有其他完好盾时**静默跳过**，不弹选择。
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (context.Actor?.Character != this) return;   // 只有自己行动时才会"自己的盾碎"

            Hand breaking = OwnShieldThatWillBreak(context);
            if (breaking == null) return;

            var candidates = CollectFormedShields(context.Actor, context.Defender, exclude: breaking);
            if (candidates.Count == 0) return;              // 没有其他完好的盾 -> 不问

            requests.Add(new ChoiceRequest(
                HandEffect.WarriorChainBreak,
                $"战士的盾已碎：要连带打碎场上一面完好的盾吗？" +
                $"（同意后点那面盾牌；被打碎的可能是自己的，也可能是对手的）",
                ToHands(candidates),
                allowDecline: true,
                declineResult: null,                        // 放弃 = 不连锁
                decider: context.Actor,
                acceptLabel: "发动连锁破盾"));
        }

        /// <summary>
        /// 判断"行动方这次运算会让**自己的哪一面盾**破碎"；没有则返回 null。
        ///
        /// 两种情况（都在运算前可知）：
        ///   ① 运算源本身是 `Formed` 的盾 —— 运算前会主动破碎；
        ///   ② 对手打出攻击、且攻击目标选中了自己的 `Formed` 盾 —— 会被打碎。
        /// </summary>
        private static Hand OwnShieldThatWillBreak(PreTurnChoiceContext context)
        {
            PlayerState me = context.Actor;
            PlayerState foe = context.Defender;

            // ① 主动破盾：运算源是成型盾
            Hand acted = context.ActedHand;
            if (acted != null && acted.State == ShieldState.Formed && me.Owns(acted))
                return acted;

            // ② 被打碎：运算结果是攻击，且目标手是自己那面成型盾
            if (context.IsAttack)
            {
                Hand target = context.TargetHand;
                if (target != null && target.State == ShieldState.Formed && me.Owns(target))
                    return target;
            }

            return null;
        }

        /// <summary>把候选（含所有者）摊平成手牌列表 —— 抉择的 Candidates 只接受 Hand。</summary>
        private static List<Hand> ToHands(List<ChainBreakTarget> targets)
        {
            var hands = new List<Hand>(targets.Count);
            for (int i = 0; i < targets.Count; i++) hands.Add(targets[i].Shield);
            return hands;
        }

        /// <summary>
        /// 结算：自己的盾破碎时，把"运算前收集到的答案"施加出去。
        ///
        /// ⚠️ 这里**不能**再向 provider 索要输入 —— 结算中无法暂停问人类玩家。
        /// 只回读 sink / 抉择器里**已经记录**的答案。
        /// </summary>
        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            if (manager == null || owner == null || brokenHand == null) return;

            var candidates = CollectFormedShields(manager.PlayerA, manager.PlayerB, exclude: brokenHand);
            if (candidates.Count == 0) return;              // 没有其他完好的盾 -> 无事可做

            var provider = manager.GetChoiceProvider(owner);
            if (provider == null) return;

            Hand picked = PreTurnChoiceReader.Read(
                provider as IPreTurnChoiceSink, provider,
                BuildReadRequest(owner, candidates), owner);

            if (picked == null) return;                     // 放弃

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!ReferenceEquals(candidates[i].Shield, picked)) continue;
                if (candidates[i].Shield.State != ShieldState.Formed) return;   // 防御性
                manager.BreakShieldAndNotify(candidates[i].Owner, candidates[i].Shield);
                return;
            }
            // 选了一个不在候选里 / 已经碎掉的盾 -> 视为放弃
        }

        /// <summary>回读答案用的请求（候选与收集阶段保持一致）。</summary>
        private static ChoiceRequest BuildReadRequest(PlayerState owner, List<ChainBreakTarget> candidates)
            => new ChoiceRequest(
                HandEffect.WarriorChainBreak,
                "战士：要连带打碎哪一面完好的盾？",
                ToHands(candidates),
                allowDecline: true,
                declineResult: null,
                decider: owner);

        /// <summary>收集场上所有 `Formed` 的盾（排除 <paramref name="exclude"/> 那一面）。</summary>
        private static List<ChainBreakTarget> CollectFormedShields(PlayerState a, PlayerState b, Hand exclude)
        {
            var list = new List<ChainBreakTarget>(4);
            if (a != null)
            {
                AddIfFormed(list, a, a.LeftHand, exclude);
                AddIfFormed(list, a, a.RightHand, exclude);
            }
            if (b != null)
            {
                AddIfFormed(list, b, b.LeftHand, exclude);
                AddIfFormed(list, b, b.RightHand, exclude);
            }
            return list;
        }



        /// <summary>
        /// 合成新盾时触发：如果另一只手是“破碎”盾，则修复为完整盾。
        /// 注意：这里的修复是被动、自动的（规则表述并未强调“可选”），
        /// 如需改为“可选”，把下面 if 内改为通过 IChoiceProvider 询问即可。
        /// </summary>
        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            if (owner == null || shieldHand == null) return;

            var otherHand = owner.GetOtherHand(shieldHand);
            if (otherHand != null && otherHand.State == ShieldState.Broken)
            {
                otherHand.SetShieldState(ShieldState.Formed);
            }
        }

        /// <summary>
        /// 整场比赛开始时清空状态。战士的技能不持有跨回合状态，
        /// 但显式声明"已复位"便于后续扩展（例如记录本局连锁次数）。
        /// </summary>
        public override void OnMatchReset(PlayerState owner, GameManager manager) { }

        /// <summary>默认攻击 +1（可被 CharacterDefinition.AttackBonus 覆盖）。</summary>
        public override int DefaultAttackBonus => 1;
        public override int DefaultDefenseBonus => 0;


        private static void AddIfFormed(List<ChainBreakTarget> list, PlayerState owner, Hand h, Hand exclude)
        {
            if (h == exclude) return;
            if (h.State == ShieldState.Formed) list.Add(new ChainBreakTarget(owner, h));
        }
    }
}
