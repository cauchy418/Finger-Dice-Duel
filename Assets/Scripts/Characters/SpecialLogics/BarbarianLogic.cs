using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 野蛮人：
    /// - 新合成一面盾时，可控制对手一只手（Lock），直到该盾破碎时解除（Unlock）。
    /// - 防御 +1。
    ///
    /// ## "同时只能控制一只手"的限制**只在场上仅剩两人时**生效
    ///
    /// 玩家确认的规则：多人局（2v2 / 8 人局）里，野蛮人已经锁住某人的一只手后，
    /// **新合成一面盾可以再锁同一个人的另一只手**；
    /// 只有当**场上仅剩两名玩家**时（本来就 1v1，或多人局打到只剩两人），
    /// 才不能"两只手两面盾锁同一个人的两只手"。
    ///
    /// 原因（我的理解）：1v1 里把对手两只手全锁住等于让他完全无法行动，
    /// 是压倒性的；多人局里还有队友能解围，所以允许。
    ///
    /// 锁手目标在**运算前**收集（结算开始后无法再问 UI），
    /// 在护盾成型时（<see cref="OnShieldFormed"/>）回读并施加。
    /// </summary>
    public class BarbarianLogic : CharacterTemplate, IPreTurnChoiceProvider
    {
        /// <summary>默认防御 +1（可被 CharacterDefinition.DefenseBonus 覆盖）。</summary>
        public override int DefaultAttackBonus => 0;
        public override int DefaultDefenseBonus => 1;

        public override string DisplayName => "野蛮人";

        // 记录：自己哪一面盾 -> 锁住了对手哪只手
        private readonly Dictionary<Hand, Hand> _lockMap = new Dictionary<Hand, Hand>();

        // ============================================================
        // 运算前的抉择：问要锁对手哪只手
        // ============================================================

        public override void CollectPreTurnChoices(PreTurnChoiceContext context, IList<ChoiceRequest> requests)
        {
            if (context == null || requests == null) return;
            if (!CharacterBehaviors.ActorHas(context.Actor, this)) return;          // 只有自己行动时才发动
            if (context.Effect != HandEffectType.Shield) return;   // 只有合出护盾才发动

            // ⚠️ 必须确认**运算源是自己的手**。
            //    规则允许"被加数是任何其他人的手"，而 context.ActedHand
            //    在某些调用点承载的是参与运算的那只手（可能是别人的）——
            //    直接 GetOtherHand 会抛"传入的 Hand 不属于该玩家"
            //    （真实 bug：批量测试里 2v2 对局直接抛异常中断）。
            if (context.ActedHand == null || !context.Actor.Owns(context.ActedHand)) return;

            // 己方另一只手若已在控制中，本次不能发动。
            //
            // ⚠️ **这条限制只在场上仅剩两名玩家时生效**（玩家确认）：
            //    1v1 里把对手双手都锁住等于让他完全无法行动，太强；
            //    多人局里还有队友能解围，所以允许再锁同一个人的另一只手。
            if (context.OnlyTwoPlayersLeft)
            {
                Hand otherOwnHand = context.Actor.GetOtherHand(context.ActedHand);
                if (_lockMap.ContainsKey(otherOwnHand)) return;
            }

            // 候选：对手尚未被锁住的手。
            // 注意这里用的是"运算**后**"的锁定状态 —— 运算本身不改变锁手，所以两者等价。
            var candidates = new List<Hand>(2);
            if (!context.Defender.LeftHand.IsLocked) candidates.Add(context.Defender.LeftHand);
            if (!context.Defender.RightHand.IsLocked) candidates.Add(context.Defender.RightHand);
            if (candidates.Count == 0) return;

            requests.Add(new ChoiceRequest(
                HandEffect.BarbarianLockTarget,
                $"野蛮人合出护盾：要锁住 {context.Defender.Name} 的哪一只手？" +
                "（被锁的手不能作为运算源）",
                candidates,
                allowDecline: true,
                declineResult: null));      // 放弃 = 不锁
        }

        // ============================================================
        // 结算：护盾成型时施加锁定
        // ============================================================

        public override void OnShieldFormed(PlayerState owner, Hand shieldHand, GameManager manager)
        {
            if (manager == null || shieldHand == null) return;

            // 己方另一只手若已在控制中，则不能再发动。
            //
            // ⚠️ 与收集阶段同一条限制：**只在场上仅剩两人时生效**。
            //    必须与 <see cref="CollectPreTurnChoices"/> 的判定一致 ——
            //    一边放行、另一边拦住会表现为"问了却不生效"（本项目踩过多次）。
            if (manager.AlivePlayerCount <= 2)
            {
                Hand otherOwnHand = owner.GetOtherHand(shieldHand);
                if (_lockMap.ContainsKey(otherOwnHand)) return;
            }

            // ⚠️ 目标必须是**本次运算真正要打的人**，不能用
            //    `manager.GetOpponent(owner)`（那是"第一位对手"）——
            //    2v2 里第 ③ 步可以选第二位对手，用 GetOpponent 会把锁手
            //    给到另一个人身上。
            //
            //    `DamageTargetPlayer` 在结算时由上层设好（与主攻击同一个目标）。
            PlayerState opponent = manager.DamageTargetPlayer ?? manager.GetOpponent(owner);
            if (opponent == null) return;

            // 候选：对手未被锁住的手
            var candidates = new List<Hand>(2);
            if (!opponent.LeftHand.IsLocked) candidates.Add(opponent.LeftHand);
            if (!opponent.RightHand.IsLocked) candidates.Add(opponent.RightHand);
            if (candidates.Count == 0) return;

            IChoiceProvider provider = manager.GetChoiceProvider(owner);
            var sink = provider as IPreTurnChoiceSink;

            // 先回读 sink（正常流程），没有记录则当场询问抉择器（直连 ExecuteTurn 的场景）
            Hand target = PreTurnChoiceReader.Read(
                sink, provider, BuildRequest(owner, opponent, candidates), owner);

            if (target == null) return;                    // 玩家放弃
            if (!opponent.Owns(target)) return;            // 非法目标
            if (target.IsLocked) return;                   // 已被锁，避免重复

            target.Lock();
            _lockMap[shieldHand] = target;
            LockCount++;
        }

        /// <summary>
        /// 本局**锁手**发动过几次（合盾时控制对手一只手）。
        ///
        /// ⚠️ 为什么要计数器：锁手的效果在终局可能已经解除了
        ///    （盾碎即解锁），只看终局状态测不出"AI 到底用没用这个技能"。
        /// </summary>
        public int LockCount { get; private set; }

        private static ChoiceRequest BuildRequest(PlayerState owner, PlayerState opponent,
                                                  List<Hand> candidates)
            => new ChoiceRequest(
                HandEffect.BarbarianLockTarget,
                $"野蛮人合出护盾：要锁住 {opponent.Name} 的哪一只手？",
                candidates,
                allowDecline: true,
                declineResult: null,
                decider: owner);

        public override void OnOwnShieldBroken(PlayerState owner, Hand brokenHand, GameManager manager)
        {
            if (brokenHand == null) return;

            if (_lockMap.TryGetValue(brokenHand, out Hand lockedOpponentHand))
            {
                lockedOpponentHand.Unlock();
                _lockMap.Remove(brokenHand);
            }
        }

        /// <summary>
        /// 整场比赛开始时清空锁手状态。
        /// 缺少本方法会导致重开一局时对手的手被永久锁死（P0 修复项）。
        /// </summary>
        public override void OnMatchReset(PlayerState owner, GameManager manager)
        {
            foreach (Hand locked in _lockMap.Values)
                locked?.Unlock();

            _lockMap.Clear();
        }
    
        /// <summary>野蛮人：**钝击伤害**（玩家指定）。</summary>
        public override AttackDamageType AttackTypeOf(AttackContext context)
            => AttackDamageType.Bludgeoning;

}
}
