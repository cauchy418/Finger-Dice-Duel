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

                // ⚠️⚠️ **回合开始：清掉"只属于本回合"的标记。**
                //
                //    `SuppressAttackDamage`（法师放弃攻击 = 这一击不造成伤害）
                //    与 `WizardLogic.WantsToAbandonThisTurn` 原来都**没有每回合重置**：
                //    界面路径自己清了，无头路径从不清 ——
                //    后果是法师第一次放弃之后**永久**被抑制、再也打不出伤害，
                //    于是"放弃换来的类型"永远用不掉，状态永远挂不上。
                //
                //    统一走 `GameManager.BeginTurn()`，界面与无头一套逻辑。
                game.BeginTurn();

                // ⚠️ **法师的状态：轮到某人之前先结算他的"回合开始"**
                //    （中毒 / 感电）。中毒可能直接把他打死、跳过其行动，
                //    所以结算后要重新读一次当前行动者。
                //
                //    ⚠️ `ResolveStatusTurnEnd` 不在这里调 —— 无头路径没有"换人事件"，
                //       所以**在调用 ExecuteTurn 之后**立刻结算（见下面的收尾段）。
                game.ResolveStatusTurnStart(actor);
                if (game.IsGameOver) break;

                if (!ReferenceEquals(game.CurrentPlayer, actor))
                {
                    // 被中毒打死了 -> 行动权已经交给下一位，本圈重来
                    continue;
                }

                LegalMove move = selector.ChooseMove(game, actor);
                if (move.Actor == null)
                {
                    // ⚠️ **没有合法步时，先问"这一位能不能跳过回合"，而不是直接判平局。**
                    //
                    //    为什么必须在这里处理：`SkipTurn` 的入口只存在于
                    //    **界面层**（BoardController 的跳过按钮）。无头路径
                    //    （MatchRunner / 各测试 harness）直接驱动 GameManager，
                    //    于是"两手都不能运算"的玩家会让整局**卡在这里**。
                    //
                    //    实测触发场景（玩家早就指出过）：
                    //      猎人进**伏击**会暗置一只手；若另一只手随后被锁
                    //      （野蛮人锁手），他就两只手都不能运算 -> 无合法步。
                    //      我一度把"AI 自动接受伏击"打开，于是：
                    //        全角色循环赛 5 处卡死、双将交叉对战失败、
                    //        "hunter vs barbarian 应在回合上限内结束"失败。
                    //    补上这一段之后，那些局面就会**正常跳过**而不是卡住。
                    if (game.CanSkipTurnFor(actor))
                    {
                        game.SkipTurn();          // 内部会把回合交给下一位
                        result.Turns++;

                        TurnResult skipped = game.LastTurnResult;
                        if (skipped != null)
                        {
                            if (RecordHistory) result.History.Add(skipped);
                            OnTurnResolved?.Invoke(skipped);
                        }
                        continue;
                    }

                    // 真的无路可走（且规则也不允许跳过）：判定为平局，避免死循环。
                    result.HitTurnLimit = true;
                    break;
                }

                TurnResult turn = game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
                result.Turns++;

                if (RecordHistory) result.History.Add(turn);
                OnTurnResolved?.Invoke(turn);

                // ⚠️ **法师的状态：这一位的"回合结束"**（燃烧 + 状态计数递减）。
                //    无头路径没有"换人事件"，所以在行动结算之后**立刻**结算 ——
                //    下一次循环开头就会轮到下一位，时序与界面路径一致。
                game.ResolveStatusTurnEnd(actor);

                // ⚠️ **吟游诗人：本回合受过伤 / 实际回过血 -> 这一手结算完立刻发动。**
                //
                //    玩家确认的时机是"每次行动结算完检查"，所以放在
                //    `ResolveStatusTurnEnd` **之后**（燃烧那种回合结束伤害也要算进"受过伤"）。
                //    对**双方**都试一次 —— 受伤的往往不是行动者。
                //    标记在 `GameManager.BeginTurn()` 里统一复位，所以不会重复触发。
                Characters.SpecialLogics.BardAi.TryActivate(actor, game);
                Characters.SpecialLogics.BardAi.TryActivate(game.GetOpponent(actor), game);
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
