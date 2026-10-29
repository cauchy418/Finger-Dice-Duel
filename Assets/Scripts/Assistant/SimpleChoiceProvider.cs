using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 抉择器：把"运算前需要玩家决定的事"收集起来交给上层（UI / AI），
    /// 并在结算过程中让角色钩子回读到玩家的选择。
    ///
    /// 关键点：它**同时实现**
    ///   · <see cref="IChoiceProvider"/>（被 GameManager 与角色钩子调用）
    ///   · <see cref="IPreTurnChoiceSink"/>（被角色钩子回读，Core 层）
    ///   · <see cref="IPlayerChoiceSink"/>（被 BoardController 写入，Assistant 层）
    /// 这三个角色必须由**同一个对象**承担 —— 否则收取到的抉择传不回结算流程。
    ///
    /// 放在 Assistant 而不是 UI，是因为它只依赖 Core 类型，这样无头测试可以直接用它
    /// 验证"玩家的选择确实生效"。
    /// </summary>
    public sealed class SimpleChoiceProvider : IChoiceProvider, IPlayerChoiceSink, IPreTurnChoiceSink
    {
        private readonly Dictionary<string, Hand> _choices = new Dictionary<string, Hand>();

        // ============================================================
        // IPlayerChoiceSink —— BoardController 写入玩家的选择
        // ============================================================

        public void SetChoice(HandEffect effect, Hand hand, int variant = 0)
        {
            string key = KeyOf(effect, variant);

            if (hand == null) _choices.Remove(key);   // null = 放弃，等同没选
            else _choices[key] = hand;
        }

        /// <summary>sink 的键：同类抉择按 variant 分开存，多段攻击才不会互相覆盖。</summary>
        internal static string KeyOf(HandEffect effect, int variant)
            => variant == 0 ? effect.ToString() : $"{effect}#{variant}";

        // ============================================================
        // IPreTurnChoiceSink —— 角色钩子回读
        // ============================================================

        /// <summary>清空记录（每个新回合开始时由 BoardController 调用）。</summary>
        public void ClearChoices() => _choices.Clear();

        public Hand GetChoice(HandEffect effect, Hand fallback = null, int variant = 0)
            => _choices.TryGetValue(KeyOf(effect, variant), out Hand h) ? h : fallback;

        /// <summary>
        /// 只返回**玩家显式记录**的答案；没有记录时返回 null（**不套用回退值**）。
        ///
        /// 用于分阶段抉择："玩家选了攻击"（记录为 null）必须能和
        /// "玩家选了回复、默认目标是自己"（记录为一只手）区分开。
        /// 经 <see cref="GetChoice"/> 时后者会拿到非 null 的回退值，
        /// 于是"选了攻击"被误判成"选了回复" —— 真实 bug：
        /// 牧师·生命领域选了攻击仍被追问回复目标。
        /// </summary>
        public Hand GetExplicitChoice(HandEffect effect, int variant = 0)
            => _choices.TryGetValue(KeyOf(effect, variant), out Hand h) ? h : null;

        // ============================================================
        // IChoiceProvider
        // ============================================================

        public Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB, AttackContext context)
            => GetChoice(HandEffect.ShieldBlock, shieldA);

        /// <summary>P1：自动放弃破阵强袭（规则允许放弃）。</summary>
        public ChainBreakTarget AskBreakerChainBreakTarget(PlayerState breaker,
                                                           IReadOnlyList<ChainBreakTarget> candidates)
            => default;

        /// <summary>
        /// 回答角色提出的通用抉择：就是"玩家已经选过的东西"的出口。
        /// 没记录过时退回 <see cref="ChoiceRequest.DeclineResult"/>（与 UI 的取消按钮等价）。
        /// </summary>
        public Hand ChooseHandFor(ChoiceRequest request, PlayerState decider)
            => GetChoice(request.Effect, request.DeclineResult, request.Variant);
    }
}
