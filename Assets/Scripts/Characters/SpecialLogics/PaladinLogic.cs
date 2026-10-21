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
                                       FingerDiceDuel.Core.IHandEffectRewriter, IHandEffectDisplay,
                                       IAttackRangeProvider,
                                       FingerDiceDuel.Core.IAttackRedirector,
                                       FingerDiceDuel.Core.IAiMoveAdvisor,
                                       FingerDiceDuel.Core.ISkillUseCounter
    {
        public override string DisplayName => "圣武士";

        /// <summary>
        /// 本局**合出盾牌**几次（4/5/6 都会走到这里）。
        ///
        /// ⚠️ 为什么需要它：<c>ShockLog</c> 只在**真正打出冲击**时才写，
        ///    而没有候选目标时会静默跳过（见 TriggerShock 的 early return）——
        ///    所以"圣武士一次都没合盾"与"合了盾但冲击没打出去"从 ShockLog
        ///    上分不出来。用它把两件事区分开。
        /// </summary>
        public int ShieldFormCount { get; private set; }

        /// <summary>本局发动招牌技能（合盾触发冲击）几次 —— <see cref="ISkillUseCounter"/>。</summary>
        public int SkillUseCount => ShieldFormCount;

        /// <summary>
        /// 让 AI **主动去合盾**（玩家指出："圣武士类似 [武僧与战士]，都是 5 高价值"）。
        ///
        /// 依据：圣武士的 4 / 5 / 6 都通向"合盾 + 2 点冲击"，
        /// 而冲击是**白赚的额外伤害**。价值表已经把 4/5/6 抬到 80，
        /// 但那只影响"屯牌偏好"；这里再给一层**直接奖励**，
        /// 让它真的把这一手打出去。
        ///
        /// ⚠️ 为什么不只靠价值表：价值表的权重会被缩放（除以 10 再乘 12），
        ///    与一次攻击档位（44~88）相比太小，
        ///    所以"屯着 5"和"打出 5 合盾"可能分不出高下。
        /// </summary>
        public override int ScoreMoveBonus(MoveValueContext context)
        {
            int after = context.ActedValueAfter;

            // 4 / 5 / 6 都能合盾（4 与 6 由 RewriteEffect 改写而来）
            if (after != 4 && after != 5 && after != 6) return 0;

            // ⚠️ **玩家特别要求的一条**：
            //    "当可以从一面完整的盾牌合成另一面完整盾牌
            //      （如完整的 4 主动去碰 2 合成 6）要**大幅度**去做"
            //
            //    理由：圣武士的冲击是**每次合盾白赚的固定伤害**。
            //    已经有一面完整的盾时，拿它当运算源去碰出 4/5/6，
            //    等于"用一次运算换一次免费伤害" —— 比单纯屯盾强得多。
            //
            //    所以给这一手**远大于**普通合盾的奖励（400 而不是 30）——
            //    量级足以压过一次攻击档位（44~88），
            //    因为这确实是更好的选择。
            //
            //    判据：运算源本身就是一面**完整的盾**。
            if (context.ActedHand != null
                && context.ActedHand.State == ShieldState.Formed)
            {
                return 400;
            }

            return 30;
        }

        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 1;

        /// <summary>攻击距离 1（与白板相同）。</summary>
        public override int BaseAttackRange => 1;

        public HandEffectType DisplayedEffectOf(int resultValue)
            => resultValue == 4 || resultValue == 6 ? HandEffectType.Shield : Hand.GetEffectType(resultValue);

        /// <summary>4 / 6 合盾只询问冲击目标，不再额外确认一次普通攻击目标。</summary>
        public override bool ResultNeedsTarget(PreTurnChoiceContext context)
        {
            if (context == null) return false;
            if (context.ResultValue == 4 || context.ResultValue == 6) return false;
            return base.ResultNeedsTarget(context);
        }

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
        /// **variant 0 = 合出盾牌触发的冲击**。
        ///
        /// 与"碎盾冲击"（variant 1）分开，是因为**一次运算可能同时发生两件事**
        /// （用一面成型盾去合出另一面盾：旧的碎、新的成）——
        /// 玩家要求这时**提示两次**，分别选目标。用 variant 才能各读各的答案。
        /// </summary>
        public const int ShockVariantForm = 0;

        /// <summary>**variant 1 = 碎盾（主动或被动）触发的冲击**。</summary>
        public const int ShockVariantBreak = 1;

        /// <summary>
        /// 合出盾牌时：冲击范围内一名角色（强制发动，目标由玩家选）。
        /// </summary>
        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            ShieldFormCount++;
            TriggerShock(owner, shieldHand, "合出盾牌", manager, ShockVariantForm);
        }

        /// <summary>
        /// **主动碎盾**（自己用一面成型的盾发起运算）时：冲击。
        /// </summary>
        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            TriggerShock(owner, brokenHand, "主动碎盾", manager, ShockVariantBreak);
        }

        /// <summary>
        /// **被动被打碎盾**时：冲击。
        ///
        /// 由 <see cref="GameManager.BreakShieldAndNotify"/> 在盾被攻击打碎时调用。
        /// </summary>
        public override void OnOtherShieldBroken(PlayerState owner, Hand brokenHand,
                                                GameManager manager)
        {
            TriggerShock(owner, brokenHand, "盾被打碎", manager, ShockVariantBreak);
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
                                  GameManager manager, int variant)
        {
            List<PlayerState> candidates = ShockCandidates(owner, manager);
            if (candidates.Count == 0) return;      // 范围内没人 -> 静默跳过

            PlayerState picked = ReadShockTarget(owner, sourceHand, candidates, reason,
                                                 manager, variant);
            if (picked == null) picked = candidates[0];   // 强制发动 -> 兜底第一个

            int before = picked.Health;
            var attack = ResolveShockDamage(owner, sourceHand, picked, manager);
            int dealt = before - picked.Health;

            // 写进战报：这样"冲击到底有没有发生、扣了多少"一眼可见。
            // 光看血条分不清"被盾挡了"还是"根本没触发"（玩家实测报告过疑惑）。
            manager?.NoteShock(owner, picked, reason, dealt, attack?.WasBlocked == true ? "被护盾挡下"
                : attack?.WasDodged == true ? "被闪避" : attack?.WasCancelled == true ? "被取消" : null);
        }

        /// <summary>
        /// **落实一次冲击**：对范围内一个人造成 2 点伤害。
        ///
        /// ⚠️ 伤害**走 <see cref="AttackContext"/>**，所以：
        ///   · **可以被盾牌挡下**（消耗一面盾）
        ///   · **可以被游侠闪避**
        ///
        /// 这是玩家明确要求的平衡性改动：
        /// "我需要让圣武士的攻击可以被盾牌挡下或者被游侠闪避，
        ///  不然圣武士冲击强度太高了"。
        ///
        /// 但仍然标记为 <see cref="AttackContext.IsSpecialAttack"/>：
        /// 它的"攻击数字视为 0"，所以**妖术师无法取消它**（玩家此前确认过）。
        /// </summary>
        private AttackContext ResolveShockDamage(PlayerState owner, Hand sourceHand,
                                        PlayerState target, GameManager manager)
        {
            if (owner == null || target == null || !target.IsAlive) return null;

            Hand hand = sourceHand ?? owner.LeftHand;
            if (hand == null) return null;

            var ctx = new AttackContext(owner, target, hand, ShockDamage)
            {
                // 冲击**走正常攻击结算**，所以能被盾挡、能被闪避
                //（玩家要求的平衡性改动）。
                IsSpecialAttack = true,

                // ⚠️ 但伤害是**固定 2 点**，不叠加攻防加成（玩家确认）。
                //
                //    没有这一行的话，打游侠（防 −2）会变成 2 + 2 = 4 点 ——
                //    测试抓到过这个副作用。剑客的追加攻击用的是同一个开关。
                IgnoreStatBonuses = true,
            };

            ctx.Resolve(manager?.GetChoiceProvider(target), manager);
            return ctx;
        }

        /// <summary>读回玩家选的冲击目标（收集阶段问过；没答过就返回 null）。</summary>
        private PlayerState ReadShockTarget(PlayerState owner, Hand sourceHand,
                                            List<PlayerState> candidates, string reason,
                                            GameManager manager, int variant)
        {
            var handCandidates = new List<Hand>(candidates.Count * 2);
            for (int i = 0; i < candidates.Count; i++)
            {
                handCandidates.Add(candidates[i].LeftHand);
                handCandidates.Add(candidates[i].RightHand);
            }

            var request = new ChoiceRequest(
                HandEffect.PaladinShockTarget,
                $"圣武士：{reason}，对攻击范围内一名角色造成 {ShockDamage} 点伤害。",
                handCandidates,
                allowDecline: false,          // 强制发动 -> 不能放弃
                declineResult: null,
                decider: owner,
                variant: variant);            // 合盾 / 碎盾 各一份答案

            Hand chosen = PreTurnChoiceReader.Read(
                manager.GetChoiceProvider(owner) as IPreTurnChoiceSink,
                manager.GetChoiceProvider(owner), request, owner);

            if (chosen == null) return null;

            return manager.OwnerOfHand(chosen);
        }

        // ============================================================
        // 把目标改成自己
        // ============================================================

        /// <summary>破誓者转移确定后，先收集圣武士是否愿意代挡，不在结算中弹窗。</summary>
        public void CollectRedirectChoice(PlayerState owner, PlayerState attacker, PlayerState target,
            GameManager manager, IList<ChoiceRequest> requests)
        {
            if (owner == null || !owner.IsAlive || ReferenceEquals(owner, attacker)
                || ReferenceEquals(owner, target) || !manager.CanReach(owner, target)) return;
            requests.Add(new ChoiceRequest(HandEffect.PaladinRedirect,
                $"圣武士【代挡】：攻击已转移至 {target.Name}，是否由自己承受？",
                new List<Hand> { owner.LeftHand, owner.RightHand }, true, null, owner));
        }

        /// <summary>
        /// 攻击范围内的角色被攻击时，圣武士可以把目标改成自己。
        ///
        /// 判据（玩家确认）：**挨打的人**必须在圣武士的**攻击距离内** ——
        /// 也就是"范围内的角色被攻击时"。
        ///
        /// ⚠️ 我第一版错误地判成"攻击者在距离内"，已按玩家澄清改正。
        ///    两者的区别在 2v2 里会实际体现（对手可能在远处打你的队友）。
        ///
        /// 这也是"要不要发动"的抉择 —— 由玩家决定，允许放弃。
        /// </summary>
        public bool TryRedirectToSelf(PlayerState owner, PlayerState attacker,
                                      PlayerState intendedTarget, GameManager manager)
        {
            if (owner == null || attacker == null || intendedTarget == null) return false;
            if (ReferenceEquals(owner, intendedTarget)) return false;   // 本来就是自己
            if (!owner.IsAlive) return false;

            // **挨打的人**必须在自己攻击距离内（这才是"范围内的角色"）
            if (!manager.CanReach(owner, intendedTarget)) return false;

            var request = new ChoiceRequest(
                HandEffect.PaladinRedirect,
                $"队友 {intendedTarget.Name} 正被 {attacker.Name} 攻击：" +
                $"要把目标改成自己吗？",
                new List<Hand>(2) { owner.LeftHand, owner.RightHand },
                allowDecline: true,
                declineResult: null,             // 放弃 = 不改
                decider: owner);

            // 控制器的答案仅本回合有效，不能读取第三方 sink 里上一回合留下的同意。
            Hand chosen = manager.AnsweredLookupFor != null
                ? manager.AnsweredLookupFor(owner, HandEffect.PaladinRedirect, 0)
                : PreTurnChoiceReader.Read(
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
        /// 什么时候需要问冲击目标：
        ///   · **自己行动且这次会合出盾 / 主动碎盾** -> 一定会触发
        ///   · **对手行动且自己有一面成型盾** -> 可能被打碎（玩家要求
        ///     "盾牌破碎时跳出提示，点击目标的手发动"）
        /// </summary>
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,
                                                   IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;

            // ① 自己行动：这次运算会合出盾 / 主动碎盾
            if (CharacterBehaviors.ActorHas(context.Actor, this))
            {
                bool willFormShield = WillFormShield(context);
                bool willBreakOwnShield = context.ActedHand != null
                                          && context.ActedHand.State == ShieldState.Formed;

                if (context.Actor.LeftHand == null) return;

                // ⚠️ **可能同时发生两件事**（玩家要求这时提示两次）：
                //    用一面成型盾去合出另一面盾 -> 旧的碎（碎盾冲击）、新的成（合盾冲击）。
                //    所以两条抉择各加一次，用 variant 区分，各读各的答案。
                if (willBreakOwnShield)
                {
                    AddShockRequest(context.Actor, context.Defender, requests,
                                    context.Manager, ShockVariantBreak, "主动碎盾");
                }

                if (willFormShield)
                {
                    AddShockRequest(context.Actor, context.Defender, requests,
                                    context.Manager, ShockVariantForm, "合出盾牌");
                }

                return;
            }

            // ② **对手行动**，而我是可能被打碎盾的防守方
            //
            //    ⚠️ 这里**只能按"有没有成型盾"预判**，因为收集发生在结算之前，
            //       还不知道这次运算会不会真的打碎它。
            //
            //       但这会造成**幽灵提问**：对手运算出 9（回复）或 2（无效果）时
            //       根本没碰盾，却已经问过玩家 —— 玩家实测报过
            //       "战士运算出 9 时让圣武士错误提示盾牌被破坏"。
            //
            //       所以真正的**发问门槛**在结算侧：只有
            //       OnOtherShieldBroken 真的被触发时才会用到这份答案
            //       （TriggerShock 由碎盾钩子调用）。
            //       收集侧的预判只是"提前把选择收上来"，本身不会造成冲击。
            if (CharacterBehaviors.ActorHas(context.Defender, this))
            {
                if (HasFormedShield(context.Defender) && context.Defender.LeftHand != null)
                {
                    AddShockRequest(context.Defender, context.Actor, requests,
                                    context.Manager, ShockVariantBreak, "盾被打碎");
                }
            }
        }

        /// <summary>该玩家是否有一面**成型的盾**（可能被打碎 -> 触发冲击）。</summary>
        private static bool HasFormedShield(PlayerState player)
            => player != null
               && ((player.LeftHand != null && player.LeftHand.State == ShieldState.Formed)
                   || (player.RightHand != null && player.RightHand.State == ShieldState.Formed));

        /// <summary>
        /// 这次运算会不会**合出护盾**。
        ///
        /// ⚠️ **结果是 5 也算合盾**（手值 5 本身就是"成型护盾"的数值）——
        ///    我第一版只判了 4 和 6，于是"1+4=5 合盾"不收集冲击目标，
        ///    结算时读不到答案、兜底打了 candidates[0]，
        ///    表现成"生成 5 的盾牌冲击没有提示选择，而是自动造成了伤害"
        ///    （玩家实测报告）。
        ///
        /// 与 <see cref="HandEffectType.Shield"/> 的判定保持一致：
        /// 4 / 6 是圣武士改写来的，5 本身就是护盾值。
        /// </summary>
        private static bool WillFormShield(PreTurnChoiceContext context)
        {
            if (context?.ActedHand == null || context.TargetHand == null) return false;

            int result = Hand.ComputeSum(context.ActedHand.Value, context.TargetHand.Value);

            // 4 / 6：圣武士的改写规则；5：手值本身就是护盾
            return result == 4 || result == 5 || result == 6;
        }

        /// <summary>
        /// 加一条"选冲击目标"的抉择。
        ///
        /// <paramref name="owner"/> 是圣武士（抉择的归属者），
        /// <paramref name="other"/> 是另一位参战者（拿不到完整名单时的兜底候选）。
        /// </summary>
        private static void AddShockRequest(PlayerState owner, PlayerState other,
                                           IList<ChoiceRequest> requests,
                                           GameManager manager, int variant, string reason)
        {
            if (owner == null || requests == null) return;

            // ⚠️ 候选**必须与结算时用同一个规则**（距离过滤），
            //    否则玩家能选到一个打不到的人，结算时又被迫改成 candidates[0]
            //    —— 表现成"我选的人没被打"。
            List<PlayerState> candidates = manager != null
                ? ShockCandidates(owner, manager)
                : new List<PlayerState>(1);

            if (other != null && !ReferenceEquals(other, owner) && other.IsAlive
                && !candidates.Contains(other))
            {
                candidates.Add(other);      // 兜底：至少让对面那位可选
            }

            var hands = new List<Hand>(candidates.Count * 2);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].LeftHand != null) hands.Add(candidates[i].LeftHand);
                if (candidates[i].RightHand != null) hands.Add(candidates[i].RightHand);
            }

            if (hands.Count == 0) return;

            requests.Add(new ChoiceRequest(
                HandEffect.PaladinShockTarget,
                $"圣武士（{reason}）：请选攻击范围内一名角色，" +
                $"造成 {ShockDamage} 点伤害（可被护盾挡下或闪避）。",
                hands,
                allowDecline: false,             // 强制发动（玩家只选目标）
                declineResult: null,
                decider: owner,
                variant: variant));              // 合盾 / 碎盾 各一份答案
        }

        /// <summary>
        /// 圣武士：攻击与**盾牌冲击**都是**钝击**（玩家指定）。
        /// </summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;

}
}
