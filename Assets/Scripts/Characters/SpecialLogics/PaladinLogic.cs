using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **圣武士**（攻 0 / 防 +1，攻击距离 1）。
    ///
    /// 玩家确认的规则：
    ///   · **4 与 6 不再攻击**，改为**合出护盾**
    ///   · **合出盾、主动碎盾、被动被打碎盾**时，
    ///     对**攻击范围内一名角色**（**含队友**）造成 **2 点真实伤害**
    ///     —— **强制发动**（不能放弃），但**目标由玩家选**
    ///   · 攻击范围内的角色被攻击时，**可以把目标改成自己**
    ///
    /// "真实伤害"= 直接扣血、**经过护盾才算挡**？不 —— 规则说的是"真实伤害"，
    /// 所以**无视护盾与攻防加成**，直接扣 2 点。
    /// </summary>
    public sealed class PaladinLogic : CharacterTemplate, IPreTurnChoiceProvider,
                                       FingerDiceDuel.Core.IHandEffectRewriter,
                                       IAttackRangeProvider,
                                       FingerDiceDuel.Core.IAttackRedirector
    {
        public override string DisplayName => "圣武士";

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 1;

        /// <summary>攻击距离 1（与白板相同）。</summary>
        public override int BaseAttackRange => 1;

        /// <summary>冲击造成的真实伤害。</summary>
        public const int ShockDamage = 2;

        /// <summary>把 4 / 6 改写成护盾。</summary>
        public HandEffectType RewriteEffect(PlayerState actor, Hand actedHand,
                                            int targetValue, GameManager manager)
        {
            // 4 与 6 的结果值：PerformOperation 已经把 actedHand 写成结果值
            int result = actedHand?.Value ?? 0;

            if (result == 4 || result == 6) return HandEffectType.Shield;

            return Hand.GetEffectType(result);
        }

        // ============================================================
        // 冲击
        // ============================================================

        /// <summary>
        /// 合出盾牌时：冲击范围内一名角色（强制发动，目标由玩家选）。
        /// </summary>
        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            TriggerShock(owner, shieldHand, "合出盾牌", manager);
        }

        /// <summary>
        /// **主动碎盾**（自己用一面成型的盾发起运算）时：冲击。
        /// </summary>
        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            TriggerShock(owner, brokenHand, "主动碎盾", manager);
        }

        /// <summary>
        /// **被动被打碎盾**时：冲击。
        ///
        /// 由 <see cref="GameManager.BreakShieldAndNotify"/> 在盾被攻击打碎时调用。
        /// </summary>
        public override void OnOtherShieldBroken(PlayerState owner, Hand brokenHand,
                                                GameManager manager)
        {
            TriggerShock(owner, brokenHand, "盾被打碎", manager);
        }

        /// <summary>冲击的候选：**攻击范围内**除自己以外的所有角色（含队友）。</summary>
        private static List<PlayerState> ShockCandidates(PlayerState owner, GameManager manager)
        {
            var list = new List<PlayerState>(4);
            if (owner == null || manager == null) return list;

            IReadOnlyList<PlayerState> all = manager.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || ReferenceEquals(p, owner)) continue;
                if (!p.IsAlive) continue;

                // 必须在攻击距离内（圣武士是 1）
                if (!manager.CanReach(owner, p)) continue;

                list.Add(p);
            }

            return list;
        }

        /// <summary>
        /// 落实一次冲击：选一个范围内的人，直接扣 2 点（无视护盾与攻防加成）。
        ///
        /// ⚠️ 只**回读已记录的答案**，不在结算里索取输入 ——
        ///    结算一旦开始就一次跑完（本项目反复踩过的坑）。
        /// </summary>
        private void TriggerShock(PlayerState owner, Hand sourceHand, string reason,
                                  GameManager manager)
        {
            List<PlayerState> candidates = ShockCandidates(owner, manager);
            if (candidates.Count == 0) return;      // 范围内没人 -> 静默跳过

            PlayerState picked = ReadShockTarget(owner, sourceHand, candidates, reason, manager);
            if (picked == null) picked = candidates[0];   // 强制发动 -> 兜底第一个

            ApplyTrueDamage(picked, ShockDamage);
        }

        /// <summary>读回玩家选的冲击目标（收集阶段问过；没答过就返回 null）。</summary>
        private PlayerState ReadShockTarget(PlayerState owner, Hand sourceHand,
                                            List<PlayerState> candidates, string reason,
                                            GameManager manager)
        {
            var handCandidates = new List<Hand>(candidates.Count * 2);
            for (int i = 0; i < candidates.Count; i++)
            {
                handCandidates.Add(candidates[i].LeftHand);
                handCandidates.Add(candidates[i].RightHand);
            }

            var request = new ChoiceRequest(
                HandEffect.PaladinShockTarget,
                $"圣武士：{reason}，对攻击范围内一名角色造成 {ShockDamage} 点真实伤害。",
                handCandidates,
                allowDecline: false,          // 强制发动 -> 不能放弃
                declineResult: null,
                decider: owner);

            Hand chosen = PreTurnChoiceReader.Read(
                manager.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                manager.GetChoiceProvider(owner), request, owner);

            if (chosen == null) return null;

            return manager.OwnerOfHand(chosen);
        }

        /// <summary>真实伤害：直接扣血，**不经护盾、不受攻防加成影响**。</summary>
        private static void ApplyTrueDamage(PlayerState target, int amount)
        {
            if (target == null || amount <= 0) return;
            if (!target.IsAlive) return;

            target.TakeDamage(amount);
        }

        // ============================================================
        // 把目标改成自己
        // ============================================================

        /// <summary>
        /// 攻击范围内的角色被攻击时，圣武士可以把目标改成自己。
        ///
        /// 判据（我的理解，已按此实现）：
        ///   · 攻击者必须在圣武士的**攻击距离内**（他能拦住这一击）
        ///   · 原本挨打的**不是自己**（否则没有意义）
        ///
        /// ⚠️ 这也是"要不要发动"的抉择 —— 由玩家决定，允许放弃。
        /// </summary>
        public bool TryRedirectToSelf(PlayerState owner, PlayerState attacker,
                                      PlayerState intendedTarget, GameManager manager)
        {
            if (owner == null || attacker == null || intendedTarget == null) return false;
            if (ReferenceEquals(owner, intendedTarget)) return false;   // 本来就是自己
            if (!owner.IsAlive) return false;

            // 攻击者必须在自己攻击距离内
            if (!manager.CanReach(owner, attacker)) return false;

            var request = new ChoiceRequest(
                HandEffect.PaladinRedirect,
                $"{intendedTarget.Name} 正被 {attacker.Name} 攻击：要把目标改成自己吗？",
                new List<Hand>(2) { owner.LeftHand, owner.RightHand },
                allowDecline: true,
                declineResult: null,             // 放弃 = 不改
                decider: owner);

            Hand chosen = PreTurnChoiceReader.Read(
                manager.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                manager.GetChoiceProvider(owner), request, owner);

            return chosen != null;               // 选了任一只手 = 同意改成自己
        }

        // ============================================================
        // 运算前收集
        // ============================================================

        /// <summary>
        /// 把"冲击目标"与"是否代挡"两个抉择**在运算前收集齐**。
        ///
        /// 为什么必须在运算前：结算一旦开始就一次跑完，
        /// 没法中途问人类玩家（本项目反复踩过的坑）。
        ///
        /// 什么时候需要问：
        ///   · 自己的运算可能**合出盾或碎盾**（即结果会是 4/6/5，或用成型盾运算）
        ///   · 自己是**防守方**（可能被打碎盾），或者可能替别人代挡
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;

            // ① 这次运算会合出盾或碎盾吗？（只在自己行动时）
            if (ReferenceEquals(context.Actor?.Character, this))
            {
                bool willFormShield = WillFormShield(context);
                bool willBreakOwnShield = context.ActedHand != null
                                          && context.ActedHand.State == ShieldState.Formed;

                if ((willFormShield || willBreakOwnShield)
                    && context.ActedHand != null && context.Actor.Owns(context.ActedHand))
                {
                    AddShockRequest(context, requests);
                }
            }
        }

        /// <summary>这次运算会不会合出盾（4 / 6 -> 护盾）。</summary>
        private static bool WillFormShield(PreTurnChoiceContext context)
        {
            if (context?.ActedHand == null || context.TargetHand == null) return false;

            int result = Hand.ComputeSum(context.ActedHand.Value, context.TargetHand.Value);
            return result == 4 || result == 6;
        }

        private void AddShockRequest(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            List<PlayerState> candidates = ShockCandidates(context.Actor, null);
            // 没有 manager 时无法按距离过滤，退化为"所有其他角色"
            if (candidates.Count == 0)
            {
                candidates = new List<PlayerState>(4);
                PlayerState owner = context.Actor;
                if (context.Defender != null && !ReferenceEquals(context.Defender, owner))
                    candidates.Add(context.Defender);
            }

            var hands = new List<Hand>(candidates.Count * 2);
            for (int i = 0; i < candidates.Count; i++)
            {
                hands.Add(candidates[i].LeftHand);
                hands.Add(candidates[i].RightHand);
            }

            if (hands.Count == 0) return;

            requests.Add(new ChoiceRequest(
                HandEffect.PaladinShockTarget,
                $"圣武士：这次运算会合出护盾，随后对攻击范围内一名角色" +
                $"造成 {ShockDamage} 点真实伤害。请选目标。",
                hands,
                allowDecline: false,             // 强制发动
                declineResult: null,
                decider: context.Actor));
        }
    }
}
