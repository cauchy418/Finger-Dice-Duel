using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// **2v2 经典玩法的开局执行层**：四人各投 1d20 → 定座位 → 依次决定初始手值。
    ///
    /// 玩家确认的完整规则：
    ///   ① 四人各投 **1d20**
    ///   ② 按点数定座位（见 <see cref="TwoOnTwoInitiative.AssignSeats"/>）：
    ///      大数较大的队去 1&amp;4 号位（大数 = 1 号、小数 = 4 号），
    ///      另一队去 2&amp;3 号位；大数相同比小数，都相同则随机
    ///   ③ 按座位 **4 → 3 → 2 → 1** 依次决定自己的点数
    ///      · **大失败（1）最先**处理：由**配对的对手**替他决定（完全自由）
    ///        —— 配对是互为对手的相邻座位 `4↔3`、`2↔1`
    ///      · **大成功（20）全部最后**，各自决定自己的
    ///   ④ **后决定的人可以看到前面人决定的数字**
    ///
    /// 本类只管"谁在什么时候替谁选"，**不管怎么显示** —— 界面与 AI 都通过它取信息。
    /// 所有状态都在这里，所以可以无头测试整条流程。
    /// </summary>
    public class TwoOnTwoStartController
    {
        /// <summary>一名玩家的投掷 + 落座 + 已决定的点数。</summary>
        private class Slot
        {
            public PlayerState Player;
            public int Roll;

            /// <summary>决定完成的点数（未决定时为 null）。</summary>
            public HandSplit? Split;
        }

        private readonly PlayerState[] _playersBySeat = new PlayerState[5];   // 下标 1..4
        private readonly Slot[] _slotBySeat = new Slot[5];
        private readonly Dictionary<PlayerState, int> _seatOfPlayer =
            new Dictionary<PlayerState, int>();

        private readonly List<TwoOnTwoInitiative.DecisionStep> _order;
        private int _stepIndex;

        // ============================================================
        // 构造：掷骰（或注入指定的点数）后立即定座位
        // ============================================================

        /// <summary>
        /// 用给定的点数建局（测试与"重开一局"都用它；UI 用 <see cref="RollAndBegin"/>）。
        /// </summary>
        /// <param name="players">四人，顺序任意（队伍由 <paramref name="teamOfPlayer"/> 决定）。</param>
        /// <param name="teamOfPlayer">与 players 一一对应的队伍号（必须两队各两人）。</param>
        /// <param name="rollOfPlayer">与 players 一一对应的 1d20 点数。</param>
        public TwoOnTwoStartController(IReadOnlyList<PlayerState> players,
                                      IReadOnlyList<int> teamOfPlayer,
                                      IReadOnlyList<int> rollOfPlayer,
                                      Random rng)
        {
            if (players == null || players.Count != 4)
                throw new ArgumentException("2v2 先攻需要正好 4 名玩家。", nameof(players));

            var assignment = TwoOnTwoInitiative.AssignSeats(rollOfPlayer, teamOfPlayer, rng);

            // ⚠️ 这里必须用"**players 列表里的下标**"去取玩家。
            //
            // `TwoOnTwoInitiative.AssignSeats` 接收的是"下标 0..3"，
            // 它的 SeatToPlayer[seat] 返回的就是那个**列表下标**。
            // 第一版我写成了 `players[assignment.PlayerAtSeat(seat)]` ——
            // 把"下标"当成"座位号"再查一次，只有 players 的顺序恰好与座位
            // 重合时才对得上（真实 bug：SplitOf / DecidedSeatsBefore 全错）。
            var slotOfIndex = new Slot[players.Count];
            for (int i = 0; i < players.Count; i++)
                slotOfIndex[i] = new Slot { Player = players[i], Roll = rollOfPlayer[i] };

            for (int seat = 1; seat <= 4; seat++)
            {
                int index = assignment.PlayerAtSeat(seat);
                PlayerState p = players[index];

                _playersBySeat[seat] = p;
                _slotBySeat[seat] = slotOfIndex[index];
                _seatOfPlayer[p] = seat;
            }

            Seats = assignment;
            _order = TwoOnTwoInitiative.BuildDecisionOrder(RollBySeatArray());
            _stepIndex = 0;
        }

        /// <summary>四人各投一次 1d20 并建局。</summary>
        public static TwoOnTwoStartController RollAndBegin(
            IReadOnlyList<PlayerState> players,
            IReadOnlyList<int> teamOfPlayer,
            Random rng,
            Func<int> rollDie = null)
        {
            if (players == null || players.Count != 4)
                throw new ArgumentException("2v2 先攻需要正好 4 名玩家。", nameof(players));

            Func<int> die = rollDie ?? (() => DiceRollSetup.Roll(rng));
            var rolls = new List<int>(4);
            for (int i = 0; i < 4; i++) rolls.Add(die());

            return new TwoOnTwoStartController(players, teamOfPlayer, rolls, rng);
        }

        private int[] RollBySeatArray()
        {
            var arr = new int[5];
            for (int seat = 1; seat <= 4; seat++) arr[seat] = _slotBySeat[seat].Roll;
            return arr;
        }

        // ============================================================
        // 落座结果
        // ============================================================

        public TwoOnTwoInitiative.SeatAssignment Seats { get; }

        /// <summary>决定顺序（含大失败 / 大成功的排序）。</summary>
        public IReadOnlyList<TwoOnTwoInitiative.DecisionStep> Order => _order;

        /// <summary>座位号 1..4 上的玩家。</summary>
        public PlayerState PlayerAtSeat(int seat) => _playersBySeat[seat];

        /// <summary>某玩家的座位号（不在局中返回 −1）。</summary>
        public int SeatOf(PlayerState p)
            => p != null && _seatOfPlayer.TryGetValue(p, out int seat) ? seat : -1;

        /// <summary>某玩家投出的点数（不在局中返回 0）。</summary>
        public int RollOf(PlayerState p)
        {
            int seat = SeatOf(p);
            return seat < 0 ? 0 : _slotBySeat[seat].Roll;
        }

        /// <summary>某玩家最终决定的点数；还没决定则为 null。</summary>
        public HandSplit? SplitOf(PlayerState p)
        {
            int seat = SeatOf(p);
            return seat < 0 ? null : _slotBySeat[seat].Split;
        }

        /// <summary>某座位的点数是否已决定。</summary>
        public bool IsDecided(int seat)
        {
            if (seat < 1 || seat > 4) return false;
            return _slotBySeat[seat].Split.HasValue;
        }

        /// <summary>整轮决定是否已完成。</summary>
        public bool IsFinished => _stepIndex >= _order.Count;

        // ============================================================
        // 当前这一步
        // ============================================================

        /// <summary>当前步骤；已完成时为 null。</summary>
        public TwoOnTwoInitiative.DecisionStep? CurrentStep
            => IsFinished ? (TwoOnTwoInitiative.DecisionStep?)null : _order[_stepIndex];

        /// <summary>当前要决定点数的人（点数归属者）。</summary>
        public PlayerState Assignee
        {
            get
            {
                var step = CurrentStep;
                return step.HasValue ? _playersBySeat[step.Value.OwnerSeat] : null;
            }
        }

        /// <summary>当前实际做决定的人（大失败时是**对手**）。</summary>
        public PlayerState Decider
        {
            get
            {
                var step = CurrentStep;
                return step.HasValue ? _playersBySeat[step.Value.DeciderSeat] : null;
            }
        }

        /// <summary>当前这一步是不是"替大失败的对手决定点数"。</summary>
        public bool IsNamingOpponent
        {
            get
            {
                var step = CurrentStep;
                return step.HasValue && step.Value.IsCriticalFailure;
            }
        }

        /// <summary>
        /// 当前这一步**是否有人类参与**。
        ///
        /// 大失败时是"对手替他决定"，所以要看**决定者**是不是人类。
        /// </summary>
        public bool IsHumanStep(Func<PlayerState, bool> isHuman)
        {
            PlayerState decider = Decider;
            if (decider == null) return false;

            return isHuman == null || isHuman(decider);
        }

        /// <summary>
        /// **信息优势**：后决定的人能看到前面人决定的数字。
        ///
        /// 玩家确认：大失败由对手决定时，**对手看得到**（他替谁定、以及前面已定的）。
        /// 所以这里返回"已经决定完成的那些座位"，界面据此展示。
        /// </summary>
        public IReadOnlyList<int> DecidedSeatsBefore
        {
            get
            {
                var result = new List<int>(4);
                for (int seat = 1; seat <= 4; seat++)
                    if (seat != (CurrentStep?.OwnerSeat ?? -1) && IsDecided(seat))
                        result.Add(seat);

                return result;
            }
        }

        // ============================================================
        // 候选与提交
        // ============================================================

        /// <summary>
        /// 当前这一步允许的拆分（**按被决定者的点数**算）。
        ///
        /// ⚠️ 大失败时用被决定者的点数（1），也就是"完全自由" ——
        /// 因为总和 1 无法拆成两只 1..9 的手，规则上对手是自由分配的。
        /// 这由 <see cref="DiceRollSetup.DistinctSplits"/> 自然给出
        /// （大失败返回全部合法拆分）。
        /// </summary>
        public IReadOnlyList<HandSplit> Candidates
        {
            get
            {
                var step = CurrentStep;
                if (!step.HasValue) return new List<HandSplit>(0);

                int roll = _slotBySeat[step.Value.OwnerSeat].Roll;
                return DiceRollSetup.DistinctSplits(roll);
            }
        }

        /// <summary>这个拆分在当前这一步是否合法。</summary>
        public bool IsCandidate(HandSplit split)
        {
            IReadOnlyList<HandSplit> all = Candidates;
            for (int i = 0; i < all.Count; i++)
                if (all[i].Left == split.Left && all[i].Right == split.Right) return true;

            return false;
        }

        /// <summary>
        /// 提交当前这一步的点数，推进到下一步。
        ///
        /// 返回 false 表示"当前没有待决定的步骤或拆分不合法"（调用方不应静默忽略）。
        /// </summary>
        public bool Submit(HandSplit split)
        {
            var step = CurrentStep;
            if (!step.HasValue) return false;
            if (!IsCandidate(split)) return false;

            _slotBySeat[step.Value.OwnerSeat].Split = split;
            _stepIndex++;
            return true;
        }

        /// <summary>
        /// 把已决定的点数写进四名玩家的手牌。
        ///
        /// 由调用方在"整轮完成后、正式开局前"执行一次。
        /// 未决定的座位按 1/1 兜底（正常流程不会发生）。
        /// </summary>
        public void ApplyToPlayers()
        {
            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState p = _playersBySeat[seat];
                if (p == null) continue;

                HandSplit split = _slotBySeat[seat].Split ?? new HandSplit(1, 1);

                // ⚠️ **只写数值**。初始手值不触发任何效果，所以绝不写护盾状态
                //    （与 1v1 的 MatchStartController.Assign 同一约定）。
                p.LeftHand.SetValue(split.Left);
                p.RightHand.SetValue(split.Right);
            }
        }

        // ============================================================
        // 界面文案
        // ============================================================

        /// <summary>当前这一步的提示（UI 直接显示，不要在界面里自己拼）。</summary>
        public string Hint
        {
            get
            {
                var step = CurrentStep;
                if (!step.HasValue) return "";

                PlayerState owner = _playersBySeat[step.Value.OwnerSeat];
                PlayerState decider = _playersBySeat[step.Value.DeciderSeat];

                if (step.Value.IsCriticalFailure)
                {
                    return $"{owner.Name}（{step.Value.OwnerSeat} 号位）投出大失败，" +
                           $"由对手 {decider.Name}（{step.Value.DeciderSeat} 号位）" +
                           $"决定他的初始手值（完全自由）。";
                }

                return $"{owner.Name}（{step.Value.OwnerSeat} 号位）投出 " +
                       $"{_slotBySeat[step.Value.OwnerSeat].Roll}，请决定初始手值。";
            }
        }

        /// <summary>先攻结果与座位的一句话说明。</summary>
        public string SeatSummary
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                for (int seat = 1; seat <= 4; seat++)
                {
                    if (seat > 1) sb.Append("　");
                    sb.Append(seat).Append("号位=")
                      .Append(_playersBySeat[seat]?.Name)
                      .Append('(').Append(_slotBySeat[seat].Roll).Append(')');
                }

                return sb.ToString();
            }
        }

        /// <summary>谁先出招：1 号位（玩家确认的座位顺序就是行动顺序的起点）。</summary>
        public PlayerState FirstToAct => _playersBySeat[1];

        // ============================================================
        // AI 代做
        // ============================================================

        /// <summary>
        /// 让 AI 代做当前这一步（或整轮直到轮到人类为止）。
        ///
        /// <paramref name="pickSplit"/> 由调用方提供（通常是 AI 的选招逻辑）；
        /// 返回 null 表示"没得选"，此时用第一个合法候选兜底，
        /// 保证流程一定推进（否则界面会卡在等待 AI）。
        ///
        /// 返回**实际被代做的步数**。
        /// </summary>
        /// <param name="isHuman">
        /// 判断"这位玩家是不是人类"。返回 false 的都会被 AI 代做。
        /// 传 null 表示"全部当人类"（即不代做任何步骤）。
        /// </param>
        /// <param name="maxSteps">最多代做几步（防御性上限，避免意外死循环）。</param>
        public int AutoResolve(Func<PlayerState, bool> isHuman,
                              Func<TwoOnTwoStartController, PlayerState, HandSplit> pickSplit,
                              int maxSteps = 8)
        {
            int done = 0;

            while (!IsFinished && done < maxSteps)
            {
                PlayerState decider = Decider;
                if (decider == null) break;

                // 人类的那一步就停下，交给界面
                if (isHuman != null && isHuman(decider)) break;

                HandSplit pick = pickSplit != null
                    ? pickSplit(this, decider)
                    : default;

                if (!IsCandidate(pick))
                {
                    // 兜底：保证推进（AI 返回了非法值也不该让整局卡住）
                    IReadOnlyList<HandSplit> options = Candidates;
                    if (options.Count == 0) break;
                    pick = options[0];
                }

                if (!Submit(pick)) break;
                done++;
            }

            return done;
        }

        /// <summary>
        /// 默认的 AI 分配策略：**优先把两只手摊平**（避免出现 9/1 这种极端），
        /// 并在"替大失败者决定"时避免给对手留下双 9（那太强）。
        ///
        /// 保持简单：这一层的目标是"能跑通且不完全随机"，
        /// 真正的策略等 2v2 平衡性测试时再调。
        /// </summary>
        public static HandSplit DefaultAiSplit(TwoOnTwoStartController c, PlayerState decider)
        {
            IReadOnlyList<HandSplit> options = c.Candidates;
            if (options.Count == 0) return default;

            bool decidingForOpponent = c.IsNamingOpponent;
            HandSplit best = options[0];
            int bestScore = int.MinValue;

            for (int i = 0; i < options.Count; i++)
            {
                HandSplit s = options[i];
                int lo = s.Left < s.Right ? s.Left : s.Right;
                int hi = s.Left > s.Right ? s.Left : s.Right;

                // 摊平：两手差距越小越好
                int score = -(hi - lo);

                // 替对手决定 -> 尽量**不要**给他 9（9 是强力资源）
                if (decidingForOpponent && hi >= 9) score -= 10;

                // 替自己决定 -> 高值略有加分（9 更好）
                if (!decidingForOpponent && hi >= 9) score += 2;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }

            return best;
        }
    }
}
