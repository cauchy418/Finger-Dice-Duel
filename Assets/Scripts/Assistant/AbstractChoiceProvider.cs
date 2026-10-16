using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 抉择器基类：把 <see cref="IChoiceProvider"/> 的三个"可选发动"抉择
    /// 统一实现为"默认不发动"，子类只重写自己关心的部分。
    ///
    /// 这样 UI 层和 AI 层都可以只实现需要的抉择，避免每个实现都被迫写空方法。
    /// </summary>
    public abstract class AbstractChoiceProvider : IChoiceProvider
    {
        /// <summary>防守方有两面盾时用哪一面抵挡。默认返回 shieldA（第一面）。</summary>
        public virtual Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB,
                                                  AttackContext context)
            => shieldA;

        /// <summary>战士连锁破盾。默认放弃发动。</summary>
        public virtual ChainBreakTarget AskWarriorChainBreakTarget(PlayerState fighter,
                                                                   IReadOnlyList<ChainBreakTarget> candidates)
            => default;

        /// <summary>
        /// 通用角色抉择（锁手 / 回血目标 / 闪避 / 强化射击 ……）。
        /// 默认"放弃发动"，子类只重写自己关心的 <see cref="HandEffect"/>。
        /// </summary>
        public virtual Hand ChooseHandFor(ChoiceRequest request, PlayerState decider)
            => request.DeclineResult;
    }
}
