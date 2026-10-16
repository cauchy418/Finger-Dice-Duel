using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 脚本化抉择器（测试替身）：按预设脚本依次给出抉择。
    ///
    /// 队列耗尽时回退为默认行为（放弃发动 / 用第一面盾），而不是抛异常，
    /// 这样测试只需为"关心的那一次抉择"排一个脚本。
    /// </summary>
    public sealed class ScriptedChoiceProvider : AbstractChoiceProvider, IPreTurnChoiceSink
    {
        private readonly Queue<Func<PlayerState, Hand, Hand, AttackContext, Hand>> _blockChoices
            = new Queue<Func<PlayerState, Hand, Hand, AttackContext, Hand>>();

        private readonly Queue<Func<PlayerState, IReadOnlyList<ChainBreakTarget>, ChainBreakTarget>> _chainBreaks
            = new Queue<Func<PlayerState, IReadOnlyList<ChainBreakTarget>, ChainBreakTarget>>();


        // ---------- Builder ----------

        /// <summary>下一次"两面盾选一"时，使用 shieldA。</summary>
        public ScriptedChoiceProvider BlockFirst()
        {
            _blockChoices.Enqueue((d, a, b, c) => a);
            return this;
        }

        /// <summary>下一次"两面盾选一"时，使用 shieldB。</summary>
        public ScriptedChoiceProvider BlockSecond()
        {
            _blockChoices.Enqueue((d, a, b, c) => b);
            return this;
        }

        /// <summary>下一次"两面盾选一"时，返回一个非法结果（用于测试防御性回退）。</summary>
        public ScriptedChoiceProvider BlockInvalid()
        {
            _blockChoices.Enqueue((d, a, b, c) => new Hand(1));
            return this;
        }

        /// <summary>下一次"两面盾选一"时，用指定的那面盾。</summary>
        public ScriptedChoiceProvider BlockWith(Hand shield)
        {
            _blockChoices.Enqueue((d, a, b, c) => shield);
            return this;
        }

        /// <summary>下一次战士连锁破盾时，放弃发动。</summary>
        public ScriptedChoiceProvider DeclineChainBreak()
        {
            _chainBreaks.Enqueue((w, list) => default);
            return this;
        }

        /// <summary>下一次战士连锁破盾时，选择候选列表中的第 index 项。</summary>
        public ScriptedChoiceProvider ChainBreakAt(int index)
        {
            _chainBreaks.Enqueue((w, list) => (index >= 0 && index < list.Count) ? list[index] : default);
            return this;
        }

        /// <summary>下一次战士连锁破盾时，选择满足条件的第一项。</summary>
        public ScriptedChoiceProvider ChainBreakWhere(Func<ChainBreakTarget, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            _chainBreaks.Enqueue((w, list) =>
            {
                for (int i = 0; i < list.Count; i++)
                    if (predicate(list[i])) return list[i];
                return default;
            });
            return this;
        }

        /// <summary>下一次野蛮人锁手时，放弃发动。（等价于 <see cref="Decline"/>。）</summary>
        public ScriptedChoiceProvider DeclineLock()
            => Decline(HandEffect.BarbarianLockTarget);

        /// <summary>下一次野蛮人锁手时，锁定候选列表中的第 index 项。</summary>
        public ScriptedChoiceProvider LockAt(int index)
        {
            _choices.Enqueue((r, d) =>
                (index >= 0 && index < r.Candidates.Count) ? r.Candidates[index] : null);
            return this;
        }

        /// <summary>下一次野蛮人锁手时，锁定指定那只手。</summary>
        public ScriptedChoiceProvider LockHand(Hand hand)
        {
            _choices.Enqueue((r, d) => hand);
            return this;
        }

        // ---------- IChoiceProvider ----------

        public override Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB,
                                                   AttackContext context)
        {
            return _blockChoices.Count > 0
                ? _blockChoices.Dequeue()(defender, shieldA, shieldB, context)
                : base.AskWhichShieldToBlock(defender, shieldA, shieldB, context);
        }

        public override ChainBreakTarget AskWarriorChainBreakTarget(PlayerState fighter,
                                                                    IReadOnlyList<ChainBreakTarget> candidates)
        {
            return _chainBreaks.Count > 0
                ? _chainBreaks.Dequeue()(fighter, candidates)
                : default;
        }

        public override Hand ChooseHandFor(ChoiceRequest request, PlayerState decider)
        {
            // ① 若调用方用 SetChoice 显式预设过这次抉择，就以它为准。
            //
            // 为什么需要：`PreTurnChoiceReader.Read` 是**抉择器优先**的，
            // 而本方法在队列耗尽时会回退到 `request.DeclineResult` ——
            // 那会把测试里 `SetChoice(...)` 预设的答案**覆盖掉**，
            // 于是测试拿到的永远是"放弃值"，无法验证"玩家选了谁"。
            Hand preset = GetChoice(request.Effect, null, request.Variant);
            if (preset != null) return preset;

            Hand pick = _choices.Count > 0
                ? _choices.Dequeue()(request, decider)
                : base.ChooseHandFor(request, decider);

            // 记录到 sink：脚本抉择器同时充当"抉择接收者"，
            // 这样角色钩子能像真实 UI 一样回读（真实 UI 里是 SimpleChoiceProvider 兼任两者）。
            SetChoice(request.Effect, pick);
            return pick;
        }

        // ---------- IPreTurnChoiceSink ----------

        private readonly Dictionary<string, Hand> _recorded = new Dictionary<string, Hand>();

        public void SetChoice(HandEffect effect, Hand hand, int variant = 0)
        {
            string key = SimpleChoiceProvider.KeyOf(effect, variant);

            if (hand == null) _recorded.Remove(key);
            else _recorded[key] = hand;
        }

        /// <summary>清空记录（每个新回合开始时由 BoardController 调用）。</summary>
        public void ClearChoices() => _recorded.Clear();

        public Hand GetChoice(HandEffect effect, Hand fallback = null, int variant = 0)
            => _recorded.TryGetValue(SimpleChoiceProvider.KeyOf(effect, variant), out Hand h) ? h : fallback;

        /// <summary>只返回显式记录过的答案；没有记录时返回 null（不套用回退值）。</summary>
        public Hand GetExplicitChoice(HandEffect effect, int variant = 0)
            => _recorded.TryGetValue(SimpleChoiceProvider.KeyOf(effect, variant), out Hand h) ? h : null;

        // ---------- 通用抉择脚本 ----------

        private readonly Queue<Func<ChoiceRequest, PlayerState, Hand>> _choices
            = new Queue<Func<ChoiceRequest, PlayerState, Hand>>();

        /// <summary>下一次遇到该抉择时，放弃发动。</summary>
        public ScriptedChoiceProvider Decline(HandEffect effect)
        {
            _choices.Enqueue((r, d) => r.Effect == effect ? r.DeclineResult : r.DeclineResult);
            return this;
        }

        /// <summary>下一次遇到该抉择时，选候选里的第 index 项。</summary>
        public ScriptedChoiceProvider ChooseAt(int index)
        {
            _choices.Enqueue((r, d) =>
                (index >= 0 && index < r.Candidates.Count) ? r.Candidates[index] : r.DeclineResult);
            return this;
        }

        /// <summary>下一次遇到该抉择时，选指定的那只手。</summary>
        public ScriptedChoiceProvider ChooseHand(Hand hand)
        {
            _choices.Enqueue((r, d) => hand);
            return this;
        }

        /// <summary>下一次遇到该抉择时，选满足条件的第一项。</summary>
        public ScriptedChoiceProvider ChooseWhere(Func<ChoiceRequest, Hand, bool> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            _choices.Enqueue((r, d) =>
            {
                for (int i = 0; i < r.Candidates.Count; i++)
                    if (predicate(r, r.Candidates[i])) return r.Candidates[i];
                return r.DeclineResult;
            });
            return this;
        }
    }
}
