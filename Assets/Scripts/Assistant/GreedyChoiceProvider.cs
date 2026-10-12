using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// AI 难度档位。
    ///
    /// 用**评分抖动**实现：难度越高抖动越小（见
    /// <see cref="GreedyChoiceProvider.ApplyDifficulty"/>）。
    /// 为什么不另外写一套"聪明/笨"的策略：抖动这一条就足以
    /// 让简单档偶尔失误、困难档始终最优，而且不会出现两套逻辑各自漂移。
    /// </summary>
    public enum AiDifficulty
    {
        /// <summary>简单：抖动 ±25，偶尔选明显次优的解。</summary>
        Easy = 0,

        /// <summary>普通：抖动 ±8，大方向稳定。</summary>
        Normal = 1,

        /// <summary>困难：不抖动，始终选最高分。</summary>
        Hard = 2,
    }

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
                // 难度噪声：简单档会随机抖动，于是偶尔选次优解
                // （否则同一个局面永远走同一步，连打两局完全可预判）。
                int noise = _noisy ? _rng.Next(-NoiseAmplitude, NoiseAmplitude + 1) : 0;
                int score = ScoreMove(moves[i], actor, game) + noise;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = moves[i];
                }
            }
            return best;
        }

        /// <summary>
        /// "留下的手值"在评分里占多大比重。
        ///
        /// 效果档位是 44/50/66/88 这个量级，而手值权重已按
        /// <see cref="HandValueProfiles.MoveScoreScale"/> 缩到 1.5~9.5（同量级）。
        /// 取 12 时：游侠把 (1,3) 变成 (2,3) 会掉约
        /// (8.5+4.5 - 2.0-4.5) × 12 ≈ 78 分 —— 高于"多打 2 点伤害"（22 分），
        /// 也就是它会为了保住 1 而放弃一点输出。
        ///
        /// 调大 -> AI 更保守（更愿意屯手值）；调小 -> 更激进。
        /// </summary>
        public int HandValueWeight { get; set; } = 12;

        // ============================================================
        // 难度
        // ============================================================

        /// <summary>
        /// 一手棋评分上叠加的**随机抖动幅度**。
        ///
        /// 为什么需要：不加抖动时 AI 是**完全确定性**的 ——
        /// 同一个局面永远走同一步，连打两局完全可预判，玩起来很机械。
        ///
        /// 同时也是**难度旋钮**：抖动越大，越容易选到次优解。
        /// 效果档位是 44 / 50 / 66 / 88，手值项再叠几十 ——
        /// 所以 ±25 足以让"明显更好的选择"偶尔被换掉，而 ±8 只影响差不多的选项。
        /// </summary>
        public int NoiseAmplitude { get; set; }

        /// <summary>是否启用难度噪声（不启用则评分完全确定）。</summary>
        public bool UseNoise
        {
            get => _noisy;
            set => _noisy = value;
        }

        private bool _noisy;
        private readonly System.Random _rng = new System.Random();

        /// <summary>
        /// 按难度档位配置这个 AI。
        ///
        /// | 档位 | 抖动 | 表现 |
        /// |---|---|---|
        /// | 简单 | ±25 | 明显更好的选择也会偶尔被换掉 -> 像新手 |
        /// | 普通 | ±8 | 大方向稳定，细节有变化 |
        /// | 困难 | 0 | 当前的最优行为（完全不抖） |
        /// </summary>
        public void ApplyDifficulty(AiDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AiDifficulty.Easy:
                    NoiseAmplitude = 25;
                    _noisy = true;
                    break;

                case AiDifficulty.Hard:
                    NoiseAmplitude = 0;
                    _noisy = false;
                    break;

                default:                    // Normal
                    NoiseAmplitude = 8;
                    _noisy = true;
                    break;
            }
        }

        /// <summary>
        /// 一手棋的评分：基础档位 + 角色特性加分 + **留下的手值** + 一些条件次分。
        ///
        /// `HandValueLookup` 是**实例属性**而不是静态注入 ——
        /// 静态注入会在测试之间互相污染（一个测试改了表，另一个测试跟着变）。
        /// </summary>
        /// <summary>
        /// **攻击自己队友**的惩罚。
        ///
        /// 为什么需要：规则的顺序是"打敌人 → 没敌人才打队友 → 连队友都打不到才放弃"，
        /// 而原来的评分**只看效果与手值**，完全不知道目标是谁 ——
        /// 于是 AI 会把队友当成"一个可以用掉废手的靶子"。
        ///
        /// 实测（批量测试）：60 局 2v2 里出现 **616 次**攻击落在队友身上
        /// （玩家也实测报告过："二号位咒术师攻击了自己的队友游侠"）。
        ///
        /// 取值远大于任何正面评分（效果档位 44/66/88、手值项几十），
        /// 所以**只要有敌人可打就绝不会打队友**；但它是惩罚而不是硬禁止 ——
        /// 当队友是唯一合法目标时（规则允许这种情形）仍然打得出去。
        /// </summary>
        public int TeammateAttackPenalty { get; set; } = 10000;

        private int ScoreMove(LegalMove move, PlayerState actor, GameManager game)
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

            // ---- 留下的手值将来能干什么 ----
            //
            // ⚠️ 这一条是**游侠必须有的**：只看"这次运算产出了什么效果"的话，
            // 游侠会拿唯一的 1 去打输出，下回合就闪不了 ——
            // 实测现象："游侠不会主动运算出 1"。
            //
            // 运算后：被操作的手变成**结果值**，另一只手不变。
            score += HandValueScore(actor, result, other.Value);

            // ---- 带条件的次分 ----
            // 打掉对手已成型的盾，比让它继续挡着划算
            if (move.TargetHand.State == ShieldState.Formed) score += 8;

            // 把对手的手打成 2，会让它下一手容易凑出 3（偏弱），略加分
            if (target == 2) score += 5;

            // ---- 打队友是坏事 ----
            //
            // 规则的顺序是"打敌人 → 没敌人才打队友"，但评分原来只看效果与手值，
            // 于是 AI 会把队友当靶子（玩家实测 + 批量测试复现：
            // 60 局 2v2 里 616 次攻击落在队友身上）。
            //
            // 只在**确实会造成攻击伤害**时才罚：用队友的手当被加数、
            // 或对队友合盾 / 治疗，都不是"攻击队友"。
            //
            // ⚠️ 判断"目标手的主人是谁"必须查**全部参战玩家**，
            //    不能用"是不是 PlayerA/PlayerB"那种两人假设。
            if (game != null && actor != null && move.TargetHand != null)
            {
                PlayerState owner = game.OwnerOfHand(move.TargetHand);
                if (owner != null && game.IsTeammate(actor, owner))
                {
                    HandEffectType eff = Hand.GetEffectType(result);
                    if (eff == HandEffectType.Attack4
                        || eff == HandEffectType.Attack6
                        || eff == HandEffectType.Attack8)
                    {
                        score -= TeammateAttackPenalty;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// "运算后留下的两只手值"值多少。
        ///
        /// 两部分，**缩放的待遇不同**：
        ///
        ///   ① **手值权重**（游侠的 1、刺客的 7 之类）：
        ///      这是"屯资源的偏好"，属于次要动机，所以缩到与效果分同量级。
        ///
        ///   ② **组合规则**（狼人凑 7、剑客双持）：
        ///      这是**角色签名能力的触发条件**，不是偏好 ——
        ///      应该像原先硬编码的 +200 那样有决定性。所以**不缩放**。
        ///
        /// ⚠️ 两个踩过的坑：
        ///   · 一开始把两者一起缩放，狼人为了多打 2 点伤害放弃了凑 7。
        ///   · 组合匹配曾用**对手的手值**去算 —— 但狼人的机制是
        ///     `运算后的手 + 另一只手 == 7`（见 WerewolfLogic.ScoreMoveBonus
        ///     的原始写法），对手的手值与此无关，导致凑 7 永远匹配不上。
        ///     正确的两个操作数是"运算后的手"与"自己的另一只手"。
        /// </summary>
        private int HandValueScore(PlayerState actor, int actedAfter, int otherHandValue)
        {
            HandValueProfile profile = HandValueProfiles.For(actor.Definition);
            if (profile == null) return 0;

            // ① 屯手值的偏好（次要，缩放）
            int preference = profile.WeightOf(actedAfter) + profile.WeightOf(otherHandValue);
            int score = preference / HandValueProfiles.MoveScoreScale * HandValueWeight;

            // ② 签名能力的触发（决定性，不缩放）
            for (int i = 0; i < profile.Combos.Count; i++)
            {
                if (profile.Combos[i].Matches(actedAfter, otherHandValue))
                    score += profile.Combos[i].Bonus;
            }

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
