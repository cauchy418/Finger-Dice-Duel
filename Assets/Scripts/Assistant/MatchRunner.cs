using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>整场比赛的模拟结果。</summary>
    public sealed class MatchResult
    {
        /// <summary>获胜者；平局时为 null。</summary>
        public PlayerState Winner { get; internal set; }

        /// <summary>是否由 PlayerA 获胜（统计胜率用，避免依赖 Winner 的对象比较）。</summary>
        public bool WinnerIsPlayerA { get; internal set; }

        /// <summary>是否由 PlayerB 获胜。</summary>
        public bool WinnerIsPlayerB { get; internal set; }

        public bool IsDraw => Winner == null;

        /// <summary>是否因为达到回合上限而中止（说明该对局可能陷入循环）。</summary>
        public bool HitTurnLimit { get; internal set; }

        /// <summary>
        /// 是否**打满了对局的轮次上限**（<see cref="GameManager.RoundLimit"/>），
        /// 即规则内的平局。
        ///
        /// ⚠️ 玩家已把上限改成按**轮**算（一轮 = 所有角色都经过一次行动），
        ///    所以这里判的是 <see cref="GameManager.RoundCount"/> 而不是回合数。
        ///
        /// ⚠️ 与 <see cref="HitTurnLimit"/> 的区别：
        ///    · <see cref="HitTurnLimit"/> = 模拟器被 <c>MaxTurns</c> **强行中断**（异常情况）；
        ///    · 本字段 = 对局**正常打满**规则上限（规则内的平局）。
        ///
        /// 统计胜率时**必须**区分这两者：把打满上限的局当成"负"会严重误导 ——
        /// 实测"狼人+巨人"在 50 回合上限下 96% 都是打满，被算成半负后排倒数第一；
        /// 把上限放开到 300，它是 98.8% 胜。
        /// </summary>
        public bool ReachedTurnLimit { get; internal set; }

        /// <summary>实际执行的回合数。</summary>
        public int Turns { get; internal set; }

        public List<TurnResult> History { get; } = new List<TurnResult>();

        public override string ToString()
        {
            string who = IsDraw ? "平局" : Winner.Name;
            string flag = HitTurnLimit ? "（达到回合上限）" : string.Empty;
            return $"{who} 获胜，共 {Turns} 回合{flag}";
        }
    }

    /// <summary>
    /// 整场比赛驱动器：把"谁来出招、谁来选盾"接到 <see cref="GameManager"/> 上，
    /// 自动跑完一整局。这是 P0 阶段把纯逻辑变成"可运行游戏"的关键一环，
    /// 也是接入 UI 前的平衡性验证工具。
    /// </summary>
    public sealed class MatchRunner
    {
        /// <summary>回合数安全上限，防止双方都打不动对方时死循环。</summary>
        public int MaxTurns { get; set; } = 500;

        /// <summary>是否在 History 中保留每回合明细（批量模拟时可关闭以省内存）。</summary>
        public bool RecordHistory { get; set; } = true;

        /// <summary>每回合结束后回调，便于观察/打日志。</summary>
        public Action<TurnResult> OnTurnResolved { get; set; }

        /// <summary>
        /// 跑完一整局。
        /// </summary>
        /// <param name="game">已组装好的对局。</param>
        /// <param name="movesForA">A 方的出招选择器。</param>
        /// <param name="movesForB">B 方的出招选择器。</param>
        public MatchResult Run(GameManager game, IMoveSelector movesForA, IMoveSelector movesForB)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (movesForA == null) throw new ArgumentNullException(nameof(movesForA));
            if (movesForB == null) throw new ArgumentNullException(nameof(movesForB));

            var result = new MatchResult();

            while (!game.IsGameOver && result.Turns < MaxTurns)
            {
                PlayerState actor = game.CurrentPlayer;
                IMoveSelector selector = ReferenceEquals(actor, game.PlayerA) ? movesForA : movesForB;

                LegalMove move = selector.ChooseMove(game, actor);
                if (move.Actor == null)
                {
                    // 双方都无手可用（理论上不会发生）：判定为平局，避免死循环。
                    result.HitTurnLimit = true;
                    break;
                }

                TurnResult turn = game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
                result.Turns++;

                if (RecordHistory) result.History.Add(turn);
                OnTurnResolved?.Invoke(turn);
            }

            // ⚠️ `HitTurnLimit` **保持原义**：模拟器被 `MaxTurns` 强行中断
            //    （`IsGameOver` 仍为 false）。既有测试依赖它，不能改。
            if (!game.IsGameOver) result.HitTurnLimit = true;

            // 另立一个字段表示"打满了 GameManager 的回合上限" ——
            // 那是**规则内的平局**（`CheckTurnLimit` 置了 IsGameOver / Winner=null），
            // 与"模拟器被迫中断"是两回事。
            //
            // 为什么需要区分：统计胜率时，把这种局当成"负"会严重误导 ——
            // 实测"狼人+巨人"在 50 回合上限下 96% 都是这种局，
            // 被算成半负后排倒数第一；把上限放开到 300，它是 98.8% 胜。
            result.ReachedTurnLimit = game.RoundCount >= game.RoundLimit;

            result.Winner = game.Winner;
            result.WinnerIsPlayerA = ReferenceEquals(game.Winner, game.PlayerA);
            result.WinnerIsPlayerB = ReferenceEquals(game.Winner, game.PlayerB);
            return result;
        }

        /// <summary>跑多局并返回每局结果（用于胜率统计）。</summary>
        public List<MatchResult> RunMany(int count,
                                         Func<GameManager> matchFactory,
                                         Func<int, IMoveSelector> selectorAFactory,
                                         Func<int, IMoveSelector> selectorBFactory)
        {
            if (matchFactory == null) throw new ArgumentNullException(nameof(matchFactory));
            if (selectorAFactory == null) throw new ArgumentNullException(nameof(selectorAFactory));
            if (selectorBFactory == null) throw new ArgumentNullException(nameof(selectorBFactory));

            var results = new List<MatchResult>(count);
            for (int i = 0; i < count; i++)
                results.Add(Run(matchFactory(), selectorAFactory(i), selectorBFactory(i)));

            return results;
        }

        /// <summary>把一局结果汇总成可读文本（胜率、平均回合数）。</summary>
        public static string Summarize(IReadOnlyList<MatchResult> results)
        {
            if (results == null || results.Count == 0) return "（无对局数据）";

            int aWins = 0, bWins = 0, draws = 0, limitHits = 0, totalTurns = 0;
            for (int i = 0; i < results.Count; i++)
            {
                MatchResult r = results[i];
                totalTurns += r.Turns;
                if (r.HitTurnLimit) limitHits++;

                if (r.WinnerIsPlayerA) aWins++;
                else if (r.WinnerIsPlayerB) bWins++;
                else draws++;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"对局数 {results.Count}");
            sb.AppendLine($"A 胜 {aWins}（{aWins * 100.0 / results.Count:F1}%） / " +
                          $"B 胜 {bWins}（{bWins * 100.0 / results.Count:F1}%） / 平局 {draws}");
            sb.AppendLine($"平均回合数 {totalTurns / (double)results.Count:F1}");
            if (limitHits > 0) sb.AppendLine($"达到回合上限的对局 {limitHits}（可能存在僵局）");
            return sb.ToString();
        }
    }
}
