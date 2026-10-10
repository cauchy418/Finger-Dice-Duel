using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 抉择接口：将需要"选择"的时刻抛给上层（UI 或 AI）决定。
    /// 本类型不依赖任何 Unity 类型，可在纯 .NET 下编译与测试。
    /// </summary>
    public interface IChoiceProvider
    {
        /// <summary>
        /// 防守方有多面盾时，询问用哪一面来抵挡本次攻击。
        /// 返回值应为 shieldA 或 shieldB 之一；返回其他值时
        /// <see cref="AttackContext.Resolve"/> 会退回 shieldA。
        /// </summary>
        Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB, AttackContext context);

        /// <summary>
        /// 战士连锁破盾：从候选盾中选一面击碎。
        /// 返回 <c>default(ChainBreakTarget)</c>（其 Shield 为 null）表示放弃发动。
        /// </summary>
        ChainBreakTarget AskWarriorChainBreakTarget(PlayerState warrior, IReadOnlyList<ChainBreakTarget> candidates);

        /// <summary>
        /// 通用抉择入口：回答一个角色提出的 <see cref="ChoiceRequest"/>。
        /// 返回 null 表示"放弃 / 不发动"。
        ///
        /// 牧师的回血目标、游侠的闪避与增伤、野蛮人的锁手都走这里，
        /// 这样新增角色时不需要再往本接口上加方法。
        /// </summary>
        Hand ChooseHandFor(ChoiceRequest request, PlayerState decider);
    }

    /// <summary>战士连锁破盾的候选目标：某玩家某一只处于 Formed 状态的盾。</summary>
    public readonly struct ChainBreakTarget
    {
        public PlayerState Owner { get; }
        public Hand Shield { get; }

        public ChainBreakTarget(PlayerState owner, Hand shield)
        {
            Owner = owner;
            Shield = shield;
        }

        public override string ToString()
            => Owner == null ? "<null target>" : $"{Owner.Name}.{(Owner.LeftHand == Shield ? "L" : "R")}";
    }

    /// <summary>一次合法运算的配对：行动方的手 + 对方的手。</summary>
    public readonly struct LegalMove
    {
        public PlayerState Actor { get; }
        public Hand ActedHand { get; }
        public Hand TargetHand { get; }

        public LegalMove(PlayerState actor, Hand actedHand, Hand targetHand)
        {
            Actor = actor;
            ActedHand = actedHand;
            TargetHand = targetHand;
        }

        public override string ToString()
        {
            if (Actor == null) return "<no move>";
            string a = Actor.LeftHand == ActedHand ? "L" : "R";
            int v = ActedHand == null ? 0 : ActedHand.Value;
            return $"{Actor.Name}: {a}({v}) + 对方({TargetHand?.Value})";
        }
    }
}
