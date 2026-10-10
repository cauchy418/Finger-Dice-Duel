using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 随机 AI：所有抉择都随机（可设种子复现）。
    /// 主要用途是"批量模拟对局"以评估平衡性，也是 P0 阶段唯一能自动跑完整局的对手。
    ///
    /// 设计取舍：
    /// - 锁手一定发动（对野蛮人有利，没有理由放弃）。
    /// - 连锁破盾默认放弃，可通过 <see cref="ChainBreakChance"/> 调整（破盾有风险也有收益）。
    /// </summary>
    public sealed class RandomChoiceProvider : AbstractChoiceProvider, IMoveSelector
    {
        private readonly Random _rng;

        /// <summary>战士连锁破盾的发动概率，0 = 从不发动，1 = 总是发动。</summary>
        public double ChainBreakChance { get; set; }

        public RandomChoiceProvider() : this(Environment.TickCount) { }

        public RandomChoiceProvider(int seed)
        {
            _rng = new Random(seed);
        }

        public string Describe => $"随机AI(seed-based, chainBreak={ChainBreakChance:P0})";

        /// <summary>随机选出一步合法运算；无合法步时返回 null。</summary>
        public LegalMove ChooseMove(GameManager game, PlayerState actor)
        {
            if (game == null || actor == null) return default;

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(actor);
            if (moves.Count == 0) return default;

            return moves[_rng.Next(moves.Count)];
        }

        /// <summary>随机选一面盾抵挡（永远返回合法结果）。</summary>
        public override Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB,
                                                   AttackContext context)
            => _rng.Next(2) == 0 ? shieldA : shieldB;

        public override ChainBreakTarget AskWarriorChainBreakTarget(PlayerState warrior,
                                                                    IReadOnlyList<ChainBreakTarget> candidates)
        {
            if (candidates == null || candidates.Count == 0) return default;
            if (_rng.NextDouble() >= ChainBreakChance) return default;

            return candidates[_rng.Next(candidates.Count)];
        }

        /// <summary>通用角色抉择：在候选里随机选一个（可放弃的抉择按 50% 概率放弃）。</summary>
        public override Hand ChooseHandFor(ChoiceRequest request, PlayerState decider)
        {
            IReadOnlyList<Hand> c = request.Candidates;
            if (c == null || c.Count == 0) return request.DeclineResult;

            // 可放弃时给一半概率放弃，让随机对局也能覆盖"不发动"的分支
            if (request.AllowDecline && _rng.Next(2) == 0) return request.DeclineResult;

            return c[_rng.Next(c.Count)];
        }
    }
}
