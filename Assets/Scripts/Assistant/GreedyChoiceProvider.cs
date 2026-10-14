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
        /// <summary>
        /// "血少到该优先回血"的血量线。
        ///
        /// ⚠️ 调这个数会**同时影响所有能回血的角色**（牧师 / 巨人 / 狼人），
        ///    所以它是**最敏感**的平衡旋钮之一。
        ///
        /// 循环赛实测（每对 40 局，17 角色）：8 时牧师的胜率只有 **30%**、
        /// 而且 640 局里 **226 局是同归于尽**的平局 ——
        /// 因为 ≤8 血时回血（120 分）永远压过攻击（最高 88 分），
        /// 牧师于是"一直回血、打不死别人，最后与对方互换"。
        /// 调到 4 之后牧师明显敢打了。
        /// </summary>
        public int HealPriorityHealth { get; set; } = 4;

        /// <summary>
        /// 牧师认为"自己处于危险值"的血量**百分比**（玩家给的规则）。
        ///
        /// "牧师只需要检查在**危险时刻优先给回血**，否则**全部进攻**"。
        ///
        /// ⚠️ 用百分比而不是绝对值：不同角色的生命上限不同
        ///    （巨人 25、狼人 25、其余 20），绝对阈值在它们之间没有可比性。
        ///
        /// 35% 大致对应"再挨一到两下就没了"。
        /// </summary>
        public int PriestHealHealthPercent { get; set; } = 35;

        /// <summary>
        /// 当前对局（由 <see cref="BoardController"/> 在询问之前注入）。
        ///
        /// ⚠️ 为什么需要：有些抉择的"正确答案"要看**全场信息** ——
        ///    例如牧师该给谁回血（自己这一队里血最低的存活者），
        ///    那要遍历全部参战玩家与队伍关系，`ChooseHandFor` 的入参里没有。
        ///    没有它就只能退回"取第一个候选"（= 永远回自己）。
        /// </summary>
        public GameManager Game { get; set; }

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

        private int ScoreMove(LegalMove move, PlayerState actor, GameManager game)        {
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

            // ---- "别反哺"：这一手会不会让我**下一手更难打出高价值** ----
            //
            // ⚠️ 玩家要求"避免对手下一手能合成更高价值"，但**核实后发现
            //    这个游戏里不存在"把差手值留给对手"这回事**：
            //    `GetLegalMoves` 允许把**任何其他玩家**的手当加数，
            //    而被加数的手值**不会被改变** ——
            //    所以对手的可选集只取决于他自己手上的两只手，与我的留牌无关。
            //
            //    真正的"反哺"是**同一件事的另一面**：我留下的牌决定**我自己**
            //    下一手能打出什么。所以这里评估"我这边的下一手潜力"。
            score += DenyScore(actor, game, result, other.Value);

            // ---- 打队友是坏事 ----            //
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

            // ---- 2v2 的攻击目标优先级（玩家给的 6 级顺序）----
            score += TargetPriorityScore(move, actor, game);

            // ---- **斩杀优先**（玩家要求："在能斩杀对方时，要大幅优先决定攻击"）----
            //
            // 斩杀线 = **本次攻击的最大伤害**，即
            //   基础值 + 攻方攻击加成 − 受方防御加成
            // 再叠加**角色自己的**修正（游侠有 2 时强化、剑客双持追加、
            // 刺客潜行 +7、巨人两段求和…见 <see cref="ILethalThresholdAdvisor"/>）。
            //
            // ⚠️ 必须放在最后、且量级**远大于**其他所有项 ——
            //    能一击带走时，前面那些"目标优先级""屯牌价值"全都不重要了。
            if (IsLethal(move, actor, game, result))
                score += LethalBonus;

            return score;
        }

        /// <summary>
        /// **斩杀赏金**。
        ///
        /// 取 100000 —— 比目标优先级最高档（10000）还大一个数量级，
        /// 因为"能不能赢"压过"打谁更好"。
        /// </summary>
        public int LethalBonus { get; set; } = 100000;

        /// <summary>
        /// 这一手能不能**直接斩杀**目标。
        ///
        /// 伤害估算必须**与真实结算同源**（<see cref="AttackContext.ApplyStatBonuses"/>），
        /// 否则会出现"AI 以为是斩杀、其实差 1 点"。
        /// 角色特有的增伤通过 <see cref="ILethalThresholdAdvisor"/> 叠上来。
        /// </summary>
        private bool IsLethal(LegalMove move, PlayerState actor, GameManager game, int result)
        {
            if (game == null || actor == null) return false;

            HandEffectType effect = Hand.GetEffectType(result);
            bool isAttack = effect == HandEffectType.Attack4
                            || effect == HandEffectType.Attack6
                            || effect == HandEffectType.Attack8;
            if (!isAttack) return false;

            PlayerState victim = game.OwnerOfHand(move.TargetHand);
            if (victim == null || !victim.IsAlive) return false;

            // ⚠️ 打队友不算"斩杀"（那是负收益，另有惩罚）
            if (game.IsTeammate(actor, victim)) return false;

            // 角色特有的增伤 / 额外段数（游侠强化、剑客追加、刺客潜行、巨人两段…）
            // 由 TotalLethalDamage 统一处理（传**原始值**给角色自算）。
            return TotalLethalDamage(move, actor, victim, result) >= victim.Health;
        }

        /// <summary>
        /// 估算这一手对某人的**总伤害**（含角色特有机理）。
        ///
        /// 提取出来是为了让"斩杀判定"与"测试"走同一条路径 ——
        /// 否则测试验证的是一份逻辑、实际用的是另一份。
        /// </summary>
        public int TotalLethalDamage(LegalMove move, PlayerState actor, PlayerState victim,
                                     int result)
        {
            if (actor == null || victim == null) return 0;

            int atkBonus = actor.Character?.AttackBonus ?? 0;
            int defBonus = victim.Character?.DefenseBonus ?? 0;

            var advisor = actor.Character as ILethalThresholdAdvisor;
            if (advisor != null)
            {
                int other = GetOtherHand(actor, move.ActedHand)?.Value ?? 0;
                return advisor.EstimateLethalDamage(
                    new LethalContext(move.ActedHand, move.TargetHand, result, other),
                    result, atkBonus, defBonus);
            }

            return AttackContext.ApplyStatBonuses(result, atkBonus, defBonus,
                                                  ignorePositiveDefense: false);
        }

        /// <summary>
        /// 估算一次攻击对某人的实际伤害（与真实结算同一套加减）。
        ///
        /// 只算**单段**；多段的合计由角色的
        /// <see cref="ILethalThresholdAdvisor"/> 负责。
        /// </summary>
        private static int EstimateAttackDamage(PlayerState attacker, PlayerState victim,
                                                int result)
        {
            return AttackContext.ApplyStatBonuses(result,
                                                  attacker.Character?.AttackBonus ?? 0,
                                                  victim.Character?.DefenseBonus ?? 0,
                                                  ignorePositiveDefense: false);
        }

        // ============================================================
        // 2v2 的**攻击目标优先级**（玩家给的顺序）
        // ============================================================
        //
        // 1. 优先攻击"自己和队友都能打到"的对手
        // 2. 优先攻击血量更低的对手
        // 3. 优先攻击没有防御（护盾 / 闪避 / 太极）的对手
        // 4. 优先攻击防御更低的对手
        // 5. 优先攻击攻击更高的对手
        //
        // ⚠️ 这是**字典序**（先满足第 1 条，同档内才比第 2 条…），
        //    所以权重必须是**递减的若干数量级**，且最高一档
        //    **大于其余所有档之和** —— 否则"低优先级项累加"
        //    会翻掉高优先级的判断。
        //
        //    取基数 10000：最高档 10000 > 1000+100+10+1 = 1111，安全。

        private const int TargetPriorityBase = 10000;

        /// <summary>
        /// 给走子评分（<b>仅供测试</b>）。
        ///
        /// 目标的优先级规则（血量 / 防御 / 攻击）只能从"两个候选之间谁分高"
        /// 来验证，所以需要把评分暴露出来 —— 否则测试只能靠"打很多局统计"
        /// 来间接推断，既慢又不确定。
        /// </summary>
        public int ScoreMoveForTest(LegalMove move, PlayerState actor, GameManager game)
            => ScoreMove(move, actor, game);

        /// <summary>该手牌是否"有防御手段"（护盾 / 太极）。</summary>
        private static bool HasDefense(Hand hand)
            => hand != null && (hand.IsShield || hand.IsTaichi);

        /// <summary>
        /// 把"该打谁"按玩家给的顺序打分。
        ///
        /// 量级远大于效果档位（44~88），所以目标选择**压过**
        /// "这一手打多少" —— 这是刻意的：选错目标比少打两点严重得多。
        /// </summary>
        private int TargetPriorityScore(LegalMove move, PlayerState actor, GameManager game)
        {
            if (game == null || actor == null) return 0;

            PlayerState target = game.OwnerOfHand(move.TargetHand);
            if (target == null) return 0;

            // 只对**对手**算这套优先级（打队友另有惩罚）
            if (ReferenceEquals(target, actor) || game.IsTeammate(actor, target)) return 0;

            int score = 0;

            // ① 自己和队友都能打到 -> 最高优先
            if (BothTeamCanReachActor(actor, target, game))
                score += TargetPriorityBase;

            // ② 血量更低（血越少分越高）
            score += (100 - HealthPercentOf(target)) * (TargetPriorityBase / 100);

            // ③ 没有防御
            bool defended = HasDefense(target.LeftHand) || HasDefense(target.RightHand);
            if (!defended) score += TargetPriorityBase / 100;

            // ④ 防御更低（数值越小=防御越低，越优先）
            int def = target.Character?.DefenseBonus ?? 0;
            score += (10 - Clamp(def + 5, 0, 10)) * 10;

            // ⑤ 攻击更高（威胁更大，先打掉）
            int atk = target.Character?.AttackBonus ?? 0;
            score += Clamp(atk + 5, -10, 10);

            return score;
        }

        private static int Clamp(int v, int lo, int hi)
            => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>目标血量的百分比（0~100，越低越优先）。</summary>
        private static int HealthPercentOf(PlayerState p)
        {
            if (p == null || p.MaximumHealth <= 0) return 100;

            return Clamp(p.Health * 100 / p.MaximumHealth, 0, 100);
        }

        /// <summary>
        /// "我和队友都能打到这个目标吗"。
        ///
        /// 1v1（没有队友）时恒不成立 —— 那是对的：
        /// 没有队友就谈不上"都能打到"。
        /// </summary>
        private static bool BothTeamCanReachActor(PlayerState actor, PlayerState target,
                                                  GameManager game)
        {
            if (actor == null || target == null || game == null) return false;
            if (!CanReach(actor, target, game)) return false;

            bool hasTeammate = false;
            IReadOnlyList<PlayerState> all = game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, actor)) continue;
                if (!game.IsTeammate(actor, p)) continue;

                hasTeammate = true;
                if (!CanReach(p, target, game)) return false;
            }

            return hasTeammate;
        }

        /// <summary>某玩家能否打到另一个玩家（比较攻击距离）。</summary>
        private static bool CanReach(PlayerState from, PlayerState to, GameManager game)
        {
            if (from == null || to == null || game == null) return false;

            return game.DistanceBetween(from, to) <= GameManager.BaseRangeOf(from);
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

        /// <summary>
        /// "这一手之后，我自己**下一手**最好的结果值多少"。
        ///
        /// 玩家要求的"避免反哺"在这款游戏里的**正确形态**：
        ///   · 对手的可选集只取决于他自己手上的牌（我们留什么不影响他），
        ///     所以不存在"把差牌喂给对手"这回事
        ///   · 真正会被反哺的是**自己** —— 留下的两只手决定下一手能打出什么
        ///
        /// 所以这里模拟"下一回合我用留下的两只手之一去加场上任一对手的手"，
        /// 取其中**最好的效果分**（用与 <see cref="ScoreMove"/> 同一套档位），
        /// 再乘一个不大的权重 —— 它只该在"当前这一手差不多"时起决定作用。
        ///
        /// 实现上只枚举**效果档位**，不重复算组合 / 屯牌分，
        /// 避免与 <see cref="HandValueScore"/> 叠加成双份。
        /// </summary>
        private int DenyScore(PlayerState actor, GameManager game,
                              int actedAfter, int otherHandValue)
        {
            if (game == null || actor == null) return 0;

            // ⚠️ **有"签名组合规则"的角色跳过这一项**。
            //
            //    狼人要凑"双手和为 7"、剑客要双 4/6/8、刺客要双 7 ——
            //    这些都是**角色自己的完整计划**，而通用的"下一手潜力"
            //    与它们经常相反（例如 7 对狼人是死牌，但 7 的下一手潜力很高）。
            //    两者一起算会把签名计划压掉
            //    （测试当场抓到：狼人 20 回合都触发不了一次和 7）。
            HandValueProfile profile = HandValueProfiles.For(actor.Definition);
            if (profile != null && profile.Combos.Count > 0) return 0;

            int best = 0;

            // 两种"留下的手"（下一回合可能拿任一只当运算源）
            best = System.Math.Max(best, BestNextEffect(actor, game, actedAfter));
            best = System.Math.Max(best, BestNextEffect(actor, game, otherHandValue));

            return best * NextTurnPotentialWeight / 100;
        }

        /// <summary>用 <paramref name="myHand"/> 当运算源时，下一手最好的效果分。</summary>
        private static int BestNextEffect(PlayerState actor, GameManager game, int myHand)
        {
            int best = 0;

            IReadOnlyList<PlayerState> all = game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, actor)) continue;

                foreach (Hand theirs in p.Hands)
                {
                    int result = Hand.ComputeSum(myHand, theirs.Value);

                    int s;
                    switch (Hand.GetEffectType(result))
                    {
                        case HandEffectType.Attack4: s = 44; break;
                        case HandEffectType.Attack6: s = 66; break;
                        case HandEffectType.Attack8: s = 88; break;
                        case HandEffectType.Shield: s = 50; break;
                        case HandEffectType.Heal: s = 30; break;
                        default: s = 0; break;
                    }

                    if (s > best) best = s;
                }
            }

            return best;
        }

        /// <summary>
        /// "下一手潜力"在总分里的千分比权重。
        ///
        /// ⚠️ 取 120（最高约 +10 分）—— 我第一版取 300（+26）就把
        ///    游侠的"强化射击"压过去了（测试当场抓到：
        ///    它宁可留着 2 也不肯变 3 强化）。
        ///    所以它必须**明显小于**一次效果档位（44~88），
        ///    只在"这一手打什么已经定了"之后做微调。
        /// </summary>
        public int NextTurnPotentialWeight { get; set; } = 120;

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
                        // 锁手该锁**对手最依赖的那只手**，而不是单纯"数值最大"。
                        //
                        // 判据：这只手能给对手带来多好的效果 ——
                        // 用**对手自己的价值表**算（他看重什么，锁掉什么最疼）。
                        //
                        // 为什么不是"数值最大"：数值大不等于有用
                        // （例如对狼人来说 7 反而是死牌、对巨人的 4/6/8 完全无效果）。
                        // 用对手自己的价值表就自动涵盖了这些反直觉的情况。
                        HandValueProfile foeProfile =
                            HandValueProfiles.For(Game?.OwnerOfHand(c[0])?.Definition);

                        Hand best = c[0];
                        int bestScore = int.MinValue;

                        for (int i = 0; i < c.Count; i++)
                        {
                            int s = foeProfile?.WeightOf(c[i].Value) ?? c[i].Value;

                            // 已成型的盾额外加分：锁住它等于让对手少一次免伤
                            if (c[i].IsShield) s += 30;

                            if (s > bestScore) { bestScore = s; best = c[i]; }
                        }

                        return best;
                    }

                case HandEffect.RangerPowerShot:
                    return c[0];       // 每次都给强化，白赚

                case HandEffect.RangerDodge:
                    return c[0];       // 被攻击时换掉一整次攻击，永远划算

                case HandEffect.PriestHealTarget:
                case HandEffect.PriestExtraHealTarget:
                    {
                        // 玩家给的规则：
                        //   只有 2v2 什么时候回给队友要写，
                        //   目前写成**优先给一队血量最低的人回复**。
                        //
                        // ⚠️ 这里**不能**再用"第一个候选就是自己的手"那个假设 ——
                        //    2v2 里队友是 C/D，候选顺序是"自己 -> 其他人"，
                        //    取第一个就等于永远只回自己（玩家实测就是这个症状）。
                        //
                        // 依赖方向：AI（Assistant 层）不能引用 Characters 层，
                        // 所以由角色通过 IHealTargetAdvisor 自报该给谁。
                        var advisor = decider?.Character as IHealTargetAdvisor;
                        PlayerState want = advisor?.BestHealTarget(decider, Game);

                        if (want != null)
                        {
                            for (int i = 0; i < c.Count; i++)
                            {
                                if (Game != null && ReferenceEquals(Game.OwnerOfHand(c[i]), want))
                                    return c[i];
                            }
                        }

                        return c[0];
                    }

                case HandEffect.PriestAttackOrHeal:
                    // 玩家给的规则：
                    //   "牧师只需要检查在**危险时刻优先给回血**，否则**全部进攻**"
                    //
                    // 所以判据是"自己是否处于危险值"（血量百分比），
                    // 而不是一个固定的绝对值 —— 绝对值在不同生命上限的角色
                    // 之间没有可比性（巨人 25、狼人 25、其余 20）。
                    //
                    // 返回的手既表示"要回复"，也表示"回给谁"。
                    // ⚠️ 候选顺序是"自己两只手在前、其他人两只手在后"，
                    //    所以发动时取第一个（= 自己的手）。
                    if (decider == null) return null;

                    bool danger = decider.Health * 100
                                  <= decider.MaximumHealth * PriestHealHealthPercent;

                    if (!danger) return null;      // 不危险 -> 全部进攻（放弃 = 当攻击打）

                    // 危险 -> 优先回血，且回给"自己这一队血最低的人"
                    var healAdvisor = decider.Character as IHealTargetAdvisor;
                    PlayerState healWant = healAdvisor?.BestHealTarget(decider, Game);

                    if (healWant != null && Game != null)
                    {
                        for (int i = 0; i < c.Count; i++)
                        {
                            if (ReferenceEquals(Game.OwnerOfHand(c[i]), healWant)) return c[i];
                        }
                    }

                    return c[0];

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
