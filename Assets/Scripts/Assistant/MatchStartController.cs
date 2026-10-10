using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 开局"掷骰分配初始手值"这一步的状态机。
    ///
    /// 与 <see cref="BoardController"/> 一样，**刻意不依赖 Unity** ——
    /// 这样整套开局流程都能在无头测试里跑完。
    ///
    /// ## 流程
    ///
    /// ```
    /// ① 掷骰        双方各 1d20
    /// ② 依次分配    点数小的先决定；点数相同则同时
    /// ③ 校验        必须是该点数的**合法拆分**（19 / 20 例外见 DiceRollSetup）
    /// ④ 开始对局    把结果写进双方的手（**不触发任何效果**）
    /// ```
    ///
    /// ## 大失败（投出 1）
    ///
    /// 不自己分配 —— 由**对手**替你指定。所以这一轮的"决策者"是对手，
    /// 但被写入的是**你**的手。
    ///
    /// | 情况 | 决策者 | 被分配者 |
    /// |---|---|---|
    /// | 正常 | 自己 | 自己 |
    /// | 自己大失败 | 对手 | 自己 |
    /// | 双方都是 1 | 对手 | 自己（两边**同时**互相指定）|
    ///
    /// ## 重要语义
    ///
    /// 初始手值**不触发任何效果** —— 只写数值，绝不写护盾状态。
    /// 初始 5 不是盾、刺客初始双 7 不进潜行。
    /// </summary>
    public sealed class MatchStartController
    {
        private readonly GameManager _game;
        private readonly Random _rng;

        /// <summary>本次可选的合法拆分（按点数算好后缓存）。</summary>
        private readonly List<HandSplit> _candidates = new List<HandSplit>();

        public MatchStartController(GameManager game, Random rng = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _rng = rng ?? new Random();
        }

        // ============================================================
        // 状态
        // ============================================================

        /// <summary>当前阶段。</summary>
        public MatchStartPhase Phase { get; private set; } = MatchStartPhase.Rolling;

        /// <summary>A 的投掷点数（未掷为 0）。</summary>
        public int RollA { get; private set; }

        /// <summary>B 的投掷点数（未掷为 0）。</summary>
        public int RollB { get; private set; }

        /// <summary>双方谁先分配。</summary>
        public DecisionOrder Order { get; private set; } = DecisionOrder.Simultaneous;

        /// <summary>当前该做决定的玩家（分配/指定）。</summary>
        public PlayerState Decider { get; private set; }

        /// <summary>当前这次决定，要把值写给谁。</summary>
        public PlayerState Assignee { get; private set; }

        /// <summary>当前这次决定是在**替对手指定**（大失败）。</summary>
        public bool IsNamingOpponent { get; private set; }

        /// <summary>提示文字（UI 直接显示）。</summary>
        public string Hint { get; private set; } = "";

        /// <summary>本次决定的合法候选（UI 用来校验点击）。</summary>
        public IReadOnlyList<HandSplit> Candidates => _candidates;

        /// <summary>已经分配完毕的玩家（用于 UI 显示进度）。</summary>
        private readonly HashSet<PlayerState> _done = new HashSet<PlayerState>();

        /// <summary>开局是否已全部完成（可以进入对局）。</summary>
        public bool IsComplete => Phase == MatchStartPhase.Ready;

        // ============================================================
        // ① 掷骰
        // ============================================================

        /// <summary>掷骰并进入分配阶段。返回 A / B 的点数。</summary>
        public void Roll()
        {
            RollA = DiceRollSetup.Roll(_rng);
            RollB = DiceRollSetup.Roll(_rng);
            Order = DiceRollSetup.DecideOrder(RollA, RollB);

            _done.Clear();
            Phase = MatchStartPhase.Assigning;

            // 点数小的先决定
            if (Order == DecisionOrder.ALower) BeginDecision(PlayerA, PlayerB);
            else if (Order == DecisionOrder.BLower) BeginDecision(PlayerB, PlayerA);
            else
            {
                // 同时：先让 A 决定（UI 会依次呈现，但**双方都不基于对方的选择**，
                // 因为同时决定意味着信息对称 —— 这里按 A→B 的顺序收集，语义等价）
                BeginDecision(PlayerA, PlayerB);
            }
        }

        /// <summary>A 方（UI 用；让它不必自己猜谁是谁）。</summary>
        public PlayerState PlayerA => _game.PlayerA;

        /// <summary>B 方。</summary>
        public PlayerState PlayerB => _game.PlayerB;

        /// <summary>
        /// 进入"由 <paramref name="decider"/> 决定"的一步。
        ///
        /// 正常情况下 decider 就是 assignee；大失败时 decider 是对手。
        /// </summary>
        private void BeginDecision(PlayerState decider, PlayerState assignee)
        {
            Decider = decider;
            Assignee = assignee;
            IsNamingOpponent = !ReferenceEquals(decider, assignee);

            int roll = RollOf(assignee);
            _candidates.Clear();
            _candidates.AddRange(DiceRollSetup.LegalSplits(roll));

            Hint = BuildHint(roll);
        }

        private int RollOf(PlayerState p) => ReferenceEquals(p, PlayerA) ? RollA : RollB;

        /// <summary>
        /// **顺序说明**（UI 直接显示）。
        ///
        /// ⚠️ 必须由控制器提供：界面自己拼名字时很容易把 decider 的名字
        /// 两边都用上，于是"点数小的先分配"会指向错误的人（真实 bug）。
        /// </summary>
        public string OrderText
        {
            get
            {
                if (PlayerA == null || PlayerB == null) return "";
                return DiceRollText.OrderNote(Order, PlayerA.Name, PlayerB.Name);
            }
        }

        /// <summary>
        /// **本次抉择的说明**（UI 直接显示）。
        ///
        /// 同样必须由控制器提供 —— 它才知道"是不是替对手指定"、点数是多少、
        /// 以及该点数有什么特殊规则（19 小成功 / 20 大成功）。
        /// </summary>
        public string DecisionText
        {
            get
            {
                if (Assignee == null || Decider == null) return "";

                int roll = RollOf(Assignee);
                string note = DiceRollText.SpecialNote(roll);

                if (IsNamingOpponent)
                    return $"{Decider.Name} 投出 {roll}（大失败）：由你来替 " +
                           $"{Assignee.Name} 指定初始手值。";

                string head = $"{Decider.Name} 投出 {roll}";
                if (!string.IsNullOrEmpty(note)) head += $"（{note}）";
                return head + "：请选择两只手的初始点数。";
            }
        }

        /// <summary>点数行的名字（供 UI 显示 `A 17 vs B 19`）。</summary>
        public string NameA => PlayerA?.Name ?? "玩家 A";
        public string NameB => PlayerB?.Name ?? "玩家 B";

        private PlayerState Other(PlayerState p)
            => ReferenceEquals(p, PlayerA) ? PlayerB : PlayerA;

        private string BuildHint(int roll)
        {
            string who = Assignee?.Name ?? "?";
            StringBuilder sb = new StringBuilder();

            if (IsNamingOpponent)
            {
                // decider 就是那个"投出 1 的人"（大失败者），这里要写清楚
                sb.Append($"{Decider.Name} 投出 **{roll}**（大失败）：由你来替 ")
                  .Append($"{who} 指定初始手值。");
            }
            else
            {
                sb.Append($"{Decider.Name} 投出 **{roll}**");
                if (DiceRollSetup.IsMinorSuccess(roll)) sb.Append("（小成功：一只手必须是 9）");
                else if (DiceRollSetup.IsMajorSuccess(roll)) sb.Append("（大成功：两手随意）");
                sb.Append($"：请选择两只手的初始点数（两手之和 = {roll}）。");
            }

            sb.Append($"　可选：{_candidates.Count} 种。");
            return sb.ToString();
        }

        // ============================================================
        // ② 提交一次分配
        // ============================================================

        /// <summary>
        /// 提交当前这次决定。<paramref name="split"/> 必须在该点数的合法候选里。
        ///
        /// 返回 false 表示输入不合法（UI 不应放过它）。
        /// </summary>
        public bool Submit(HandSplit split)
        {
            if (Phase != MatchStartPhase.Assigning) return false;
            if (Assignee == null) return false;
            if (!IsCandidate(split)) return false;

            Assign(Assignee, split);
            _done.Add(Assignee);

            Advance();
            return true;
        }

        /// <summary>这个拆分是否是当前候选之一（UI 用来判断"能不能点"）。</summary>
        public bool IsCandidate(HandSplit split)
        {
            for (int i = 0; i < _candidates.Count; i++)
                if (_candidates[i].Left == split.Left && _candidates[i].Right == split.Right)
                    return true;
            return false;
        }

        /// <summary>
        /// 把拆分写进玩家的手。
        ///
        /// ⚠️ **只写数值**。初始手值不触发任何效果，所以绝不写护盾状态
        /// （`TestSetup` / 开局流程都必须遵守这一点）。
        /// </summary>
        private static void Assign(PlayerState player, HandSplit split)
        {
            player.LeftHand.SetValue(split.Left);
            player.RightHand.SetValue(split.Right);
        }

        /// <summary>推进到下一步 / 完成。</summary>
        private void Advance()
        {
            PlayerState pending = PendingPlayer();
            if (pending == null)
            {
                Phase = MatchStartPhase.Ready;
                Decider = null;
                Assignee = null;
                IsNamingOpponent = false;
                _candidates.Clear();
                Hint = $"开局完成：{PlayerA.Name} {Describe(PlayerA)}　|　" +
                       $"{PlayerB.Name} {Describe(PlayerB)}";
                return;
            }

            // 谁是这一轮的"决定者"？若 pending 大失败，则由对手替他指定。
            BeginDecision(DeciderFor(pending), pending);
        }

        /// <summary>还没分配完的那位（都分配完则返回 null）。</summary>
        private PlayerState PendingPlayer()
        {
            if (!_done.Contains(PlayerA) && !_done.Contains(PlayerB))
            {
                // 第一轮：按点数顺序取先手方；同时则先 A
                if (Order == DecisionOrder.BLower) return PlayerB;
                return PlayerA;
            }
            if (!_done.Contains(PlayerA)) return PlayerA;
            if (!_done.Contains(PlayerB)) return PlayerB;
            return null;
        }

        /// <summary>该由谁来替 <paramref name="assignee"/> 决定（大失败时是对手）。</summary>
        private PlayerState DeciderFor(PlayerState assignee)
        {
            int roll = RollOf(assignee);
            if (DiceRollSetup.IsCriticalFailure(roll))
                return ReferenceEquals(assignee, PlayerA) ? PlayerB : PlayerA;

            return assignee;
        }

        private static string Describe(PlayerState p)
            => p == null ? "?" : $"{p.LeftHand.Value}+{p.RightHand.Value}";

        // ============================================================
        // 一次性便捷入口（测试 / 单机快速开局用）
        // ============================================================

        /// <summary>
        /// 用给定的点数开局（不掷骰），便于测试与"重开"。
        /// </summary>
        public void RollWith(int rollA, int rollB)
        {
            RollA = rollA;
            RollB = rollB;
            Order = DiceRollSetup.DecideOrder(rollA, rollB);

            _done.Clear();
            Phase = MatchStartPhase.Assigning;

            if (Order == DecisionOrder.BLower) BeginDecision(PlayerB, PlayerB);
            else BeginDecision(PlayerA, PlayerA);

            if (DiceRollSetup.IsCriticalFailure(RollOf(Assignee)))
                BeginDecision(DeciderFor(Assignee), Assignee);
        }
    }

    /// <summary>开局掷骰流程的阶段。</summary>
    public enum MatchStartPhase
    {
        /// <summary>还没掷骰。</summary>
        Rolling = 0,

        /// <summary>正在分配 / 指定初始手值。</summary>
        Assigning = 1,

        /// <summary>分配完毕，可以开始对局。</summary>
        Ready = 2,
    }

}
