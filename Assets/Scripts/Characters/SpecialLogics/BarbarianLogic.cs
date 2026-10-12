using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 野蛮人：
    /// - 新合成一面盾时，可控制对手一只手（Lock），直到该盾破碎时解除（Unlock）。
    /// - 同时最多只能控制对手一只手：若己方另一只手已处于"控制中"，本次不能发动。
    /// - 防御 +1。
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
            if (context.Actor?.Character != this) return;          // 只有自己行动时才发动
            if (context.Effect != HandEffectType.Shield) return;   // 只有合出护盾才发动

            // ⚠️ 必须确认**运算源是自己的手**。
            //    规则允许"被加数是任何其他人的手"，而 context.ActedHand
            //    在某些调用点承载的是参与运算的那只手（可能是别人的）——
            //    直接 GetOtherHand 会抛"传入的 Hand 不属于该玩家"
            //    （真实 bug：批量测试里 2v2 对局直接抛异常中断）。
            if (context.ActedHand == null || !context.Actor.Owns(context.ActedHand)) return;

            // 己方另一只手若已在控制中，本次不能发动
            Hand otherOwnHand = context.Actor.GetOtherHand(context.ActedHand);
            if (_lockMap.ContainsKey(otherOwnHand)) return;

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

            // 己方另一只手若已在控制中，则不能再发动
            Hand otherOwnHand = owner.GetOtherHand(shieldHand);
            if (_lockMap.ContainsKey(otherOwnHand)) return;

            PlayerState opponent = manager.GetOpponent(owner);
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
        }

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
    }
}
