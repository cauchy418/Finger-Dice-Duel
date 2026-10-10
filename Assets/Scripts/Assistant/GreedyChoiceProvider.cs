using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 贪心 AI：用一组启发式评分选招。
    /// 目的不是"很强"，而是"合理且确定"，用于冒烟测试与基准对局。
    ///
    /// 评分 = 结果效果的固定档位 + **当前角色的特性加分**。
    ///
    /// 基础档位（数值可调，改这里即可调整风格）：
    ///   4 → 44    6 → 66    8 → 88
    ///   5 → 50（合盾）
    ///   9 → 90，但仅在生命低于阈值时才优先（否则不浪费回复）
    ///   其他 → 0
    ///
    /// ⚠️ 特性加分是后加的：早期版本只认识白板 / 战士，
    /// 牧师、游侠、狼人、剑客、忍者、刺客的 AI **完全不会用技能**。
    /// 现在每个角色都有对应的评分与抉择策略。
    /// </summary>
    public sealed class GreedyChoiceProvider : AbstractChoiceProvider, IMoveSelector
    {
        /// <summary>
        /// 生命低于等于该值时，回复的评分提高。
        ///
        /// 重要：这个阈值必须显著低于"最大可造成伤害"，否则会出现"双方都优先回血、
        /// 谁也打不死谁"的僵局。实测野蛮人镜像在阈值 ≥12 时会 100% 打成僵局，
        /// 阈值 ≤8 时 21 回合内正常结束。默认值 8 是据此选定的。
        /// </summary>
        public int HealPriorityHealth { get; set; } = 8;

        /// <summary>是否发动战士连锁破盾。默认放弃（避免自作自受地破自己的盾）。</summary>
        public bool UseChainBreak { get; set; }

        /// <summary>
        /// 低于该伤害就不发动剑客的追加攻击。
        /// 追加会把"另一只手"改写成主攻击结果，太弱的追加不值得换掉原来的数字。
        /// </summary>
        public int SwordsmanFollowUpMinDamage { get; set; } = 3;


        public string Describe => $"贪心AI(chainBreak={UseChainBreak})";

        public LegalMove ChooseMove(GameManager game, PlayerState actor)
        {
            if (game == null || actor == null) return default;

            IReadOnlyList<LegalMove> moves = game.GetLegalMoves(actor);
            if (moves.Count == 0) return default;

            LegalMove best = moves[0];
            int bestScore = int.MinValue;

            for (int i = 0; i < moves.Count; i++)
            {
                int score = ScoreMove(moves[i], actor);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = moves[i];
                }
            }
            return best;
        }

        /// <summary>
        /// 一手棋的评分：基础档位 + 角色特性加分 + 一些带条件的次分。
        /// </summary>
        private int ScoreMove(LegalMove move, PlayerState actor)
        {
            int acted = move.ActedHand.Value;
            int target = move.TargetHand.Value;
            int result = Hand.ComputeSum(acted, target);

            int score;
            switch (Hand.GetEffectType(result))
            {
                case HandEffectType.Attack4: score = 44; break;
                case HandEffectType.Attack6: score = 66; break;
                case HandEffectType.Attack8: score = 88; break;

                case HandEffectType.Shield: score = 50; break;

                case HandEffectType.Heal:
                    // 满血时回复毫无价值，压低评分
                    score = actor.Health >= actor.MaximumHealth
                        ? 5
                        : (actor.Health <= HealPriorityHealth ? 120 : 30);
                    break;

                default: score = 0; break;
            }

            // ---- 角色特性加分 ----
            // 反转依赖：不是"AI 认识狼人"，而是"角色告诉 AI 这一手值多少"。
            // 这样 Assistant 层不必引用 Characters 层（项目约定）。
            Hand other = GetOtherHand(actor, move.ActedHand);

            if (actor.Character is IAiMoveAdvisor advisor)
            {
                score += advisor.ScoreMoveBonus(new MoveValueContext(
                    move.ActedHand, result, move.TargetHand, other.Value));
            }

            // ---- 带条件的次分 ----
            // 打掉对手已成型的盾，比让它继续挡着划算
            if (move.TargetHand.State == ShieldState.Formed) score += 8;

            // 把对手的手打成 2，会让它下一手容易凑出 3（偏弱），略加分
            if (target == 2) score += 5;

            return score;
        }

        private static Hand GetOtherHand(PlayerState actor, Hand acted)
            => ReferenceEquals(acted, actor.LeftHand) ? actor.RightHand : actor.LeftHand;

        /// <summary>用数值较低的那面盾去挡，尽量保留高数字的手。</summary>
        public override Hand AskWhichShieldToBlock(PlayerState defender, Hand shieldA, Hand shieldB,
                                                   AttackContext context)
            => shieldA.Value <= shieldB.Value ? shieldA : shieldB;

        public override ChainBreakTarget AskWarriorChainBreakTarget(PlayerState warrior,
                                                                    IReadOnlyList<ChainBreakTarget> candidates)
        {
            if (!UseChainBreak || candidates == null || candidates.Count == 0) return default;

            // 优先破对手的盾
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Owner != warrior) return candidates[i];

            return default;
        }

        /// <summary>
        /// 通用角色抉择的贪心策略（按角色特性逐条处理）：
        ///
        ///   · 锁手（野蛮人）：锁对手更大的那只手，限制其最强的一招。
        ///   · 强化射击（游侠）：一律发动 —— 白赚 50% 伤害，代价只是把一只 2 变成 3。
        ///   · 闪避（游侠）：一律用掉一只 1。被攻击时一只 1 本身毫无战斗力，
        ///     换掉一次完整攻击是纯赚（比"被盾挡下"还彻底）。
        ///   · 回血目标（牧师）：**永远回给自己**，绝不回给对手。
        ///   · 追加攻击（剑客）：伤害太低时不换（保留原来那手数字）。
        /// </summary>
        public override Hand ChooseHandFor(ChoiceRequest request, PlayerState decider)
        {
            IReadOnlyList<Hand> c = request.Candidates;
            if (c == null || c.Count == 0) return request.DeclineResult;

            switch (request.Effect)
            {
                case HandEffect.BarbarianLockTarget:
                    {
                        Hand best = c[0];
                        for (int i = 1; i < c.Count; i++)
                            if (c[i].Value > best.Value) best = c[i];
                        return best;
                    }

                case HandEffect.RangerPowerShot:
                    return c[0];       // 每次都给强化，白赚

                case HandEffect.RangerDodge:
                    return c[0];       // 被攻击时换掉一整次攻击，永远划算

                case HandEffect.PriestHealTarget:
                    // 候选顺序是"自己两只手在前、对手两只手在后"
                    // （见 PriestLogic.AddOwnerHands 的调用顺序），
                    // 所以取第一个候选就一定是自己的手 —— 不需要额外查询归属。
                    return c[0];

                case HandEffect.PriestExtraHealTarget:
                    // 双 9 的额外回复同理：给自己
                    return c[0];

                case HandEffect.PriestAttackOrHeal:
                    // 4/6/8 是"当攻击打"还是"当回复用"。
                    // 血量偏低时优先回复（回复量 = 该数字，不受攻防影响），
                    // 否则正常当攻击 —— 否则双方都只顾回血会打成僵局。
                    //
                    // 目标已经**合并进这一次抉择**（点哪只手 = 回给谁），
                    // 所以这里返回的手既表示"要回复"，也表示"回给谁"。
                    //
                    // ⚠️ 候选顺序是"自己两只手在前、对手两只手在后"
                    // （见 PriestLogic.AddOwnerHands 的调用顺序）。
                    // AI 只能回给自己 —— 1v1 里把回复给对手是资敌。
                    return decider != null && decider.Health <= HealPriorityHealth
                        ? c[0]      // 发动 = 当回复用，且回给自己
                        : null;     // 放弃 = 当攻击打

                case HandEffect.SwordsmanFollowUp:
                    {
                        int damage = FollowUpDamageLookup?.Invoke() ?? int.MaxValue;
                        return damage >= SwordsmanFollowUpMinDamage ? c[0] : null;
                    }

                case HandEffect.WarriorChainBreak:
                    {
                        if (!UseChainBreak || c.Count == 0) return null;   // 默认放弃

                        // 优先破**对手**的盾：候选里自己的手在前、对手的在后，
                        // 所以挑第一个不属于自己的。
                        for (int i = 0; i < c.Count; i++)
                            if (decider == null || !decider.Owns(c[i])) return c[i];

                        return null;    // 只有自己的盾可破 -> 没必要（自伤）
                    }

                default:
                    return request.DeclineResult;
            }
        }

        /// <summary>
        /// 由调用方注入："剑客这次追加攻击会造成多少伤害"。
        /// 这是一次结算过程中的临时信息，只有正在跑的那次运算知道。
        /// 未注入时按"值得发动"处理（保守起见不打折）。
        /// </summary>
        public Func<int> FollowUpDamageLookup { get; set; }
    }
}
