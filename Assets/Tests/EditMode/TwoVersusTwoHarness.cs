using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **2v2 批量对局的测试台**（重写版）。
    ///
    /// ⚠️ 为什么不复用 <see cref="MatchRunner"/>：
    ///    它的接口只有**两个**出招选择器，分配规则是
    ///    "行动方是 PlayerA 就用第一个，否则用第二个"。
    ///    四人局里这会把 **座位 3 / 4 也交给座位 2 那一方的 AI** ——
    ///    于是**只有两人的 AI 在真正出招**，局面根本推不动。
    ///    （我第一版就是这么跑的：612 局里 458 局平局、平均 298 回合。）
    ///
    /// ✅ 本测试台改成**按座位分派**：每一步都问
    ///    <see cref="GameManager.GetChoiceProvider"/> 要"这个行动方自己的"抉择器，
    ///    与实机（BoardController.MakeAutomaticMove）走的是同一条路。
    ///
    /// 另外两处与 1v1 不同的地方，也都写在这里以免再踩：
    ///   · **回合上限**：2v2 里每个玩家每次行动都算 1 回合（`TurnCount++`），
    ///     所以默认的 50 相当于每人只有约 12 次行动 —— 根本打不死人。
    ///     这里默认给 400。
    ///   · **胜负按队伍判定**：<c>MatchResult.WinnerIsPlayerA/B</c> 只认两个玩家，
    ///     四人局要用 <see cref="GameManager.TeamOf"/> 判阵营。
    /// </summary>
    internal static class TwoVersusTwoHarness
    {
        /// <summary>一局的结果。</summary>
        internal readonly struct Outcome
        {
            /// <summary>获胜阵营是"座位 1 与 4"这一队。</summary>
            public readonly bool Seat14Won;

            /// <summary>获胜阵营是"座位 2 与 3"这一队。</summary>
            public readonly bool Seat23Won;

            /// <summary>这次尝试里 count 递增了几次（每次 = 一个玩家的一次行动）。</summary>
            public readonly int Actions;

            /// <summary>是否因为达到回合上限而结束（平局）。</summary>
            public readonly bool HitLimit;

            /// <summary>非空表示这一局出错了（异常信息），其余字段无意义。</summary>
            public readonly string Error;

            public Outcome(bool s14, bool s23, int actions, bool hitLimit, string error)
            {
                Seat14Won = s14;
                Seat23Won = s23;
                Actions = actions;
                HitLimit = hitLimit;
                Error = error;
            }

            public bool IsDraw => !Seat14Won && !Seat23Won;
        }

        /// <summary>
        /// 跑一局 2v2。
        /// </summary>
        /// <param name="seat1">座位 1（与座位 4 同队）</param>
        /// <param name="seat2">座位 2（与座位 3 同队）</param>
        /// <param name="seat3">座位 3</param>
        /// <param name="seat4">座位 4</param>
        /// <param name="seed">初始手值的种子</param>
        /// <param name="turnLimit">回合上限（每次行动算 1）</param>
        /// <param name="actions">实际进行了多少次行动</param>
        internal static Outcome Play(CharacterDefinition seat1, CharacterDefinition seat2,
                                     CharacterDefinition seat3, CharacterDefinition seat4,
                                     int seed, int turnLimit, out int actions)
        {
            actions = 0;

            var seated = new List<PlayerState>(4)
            {
                CharacterFactory.CreatePlayer("P1", seat1),
                CharacterFactory.CreatePlayer("P2", seat2),
                CharacterFactory.CreatePlayer("P3", seat3),
                CharacterFactory.CreatePlayer("P4", seat4),
            };

            // ⚠️ 每个座位一个**自己的**抉择器 —— 这是本测试台的核心。
            var providers = new List<IChoiceProvider>(4)
            {
                new GreedyChoiceProvider(), new GreedyChoiceProvider(),
                new GreedyChoiceProvider(), new GreedyChoiceProvider(),
            };

            try
            {
                GameManager game = GameManager.Create2v2(seated, providers);
                game.TurnLimit = turnLimit;

                // 经典开局：每人掷 1d20 决定初始手值
                for (int i = 0; i < seated.Count; i++)
                    ApplyClassicHands(seated[i], seed * 17 + i * 5 + 3);

                while (!game.IsGameOver && actions < turnLimit)
                {
                    PlayerState actor = game.CurrentPlayer;
                    if (actor == null) break;                     // 全部阵亡

                    IChoiceProvider choice = game.GetChoiceProvider(actor);
                    if (choice is not IMoveSelector selector)
                        return Fail($"座位 {game.SeatOf(actor)} 没有可用的出招选择器");

                    LegalMove move = selector.ChooseMove(game, actor);
                    if (move.Actor == null)
                    {
                        // 无合法步。把**现场**一并带出来（否则只能知道"出错了"）。
                        var detail = new System.Text.StringBuilder();
                        detail.Append($"座位 {game.SeatOf(actor)} 无合法步");
                        detail.Append($"（第 {actions + 1} 次行动）");
                        detail.Append(" 活着=");
                        for (int k = 0; k < seated.Count; k++)
                            if (seated[k].IsAlive) detail.Append($"P{game.SeatOf(seated[k])} ");
                        detail.Append("| 各对手可达=");
                        for (int k = 0; k < seated.Count; k++)
                        {
                            if (ReferenceEquals(seated[k], actor)) continue;
                            detail.Append($"[P{game.SeatOf(seated[k])}"
                                         + $"活={(seated[k].IsAlive ? 1 : 0)}"
                                         + $"距={game.DistanceBetween(actor, seated[k])}"
                                         + $"可打={(game.CanReach(actor, seated[k]) ? 1 : 0)}] ");
                        }
                        return Fail(detail.ToString());
                    }

                    game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
                    actions++;
                }

                // 胜负按**队伍**判定（不能用 WinnerIsPlayerA/B）
                PlayerState w = game.Winner;
                bool s14 = false, s23 = false;
                if (w != null)
                {
                    int seat = game.SeatOf(w);
                    s14 = seat == 1 || seat == 4;
                    s23 = seat == 2 || seat == 3;
                }

                return new Outcome(s14, s23, actions, !game.IsGameOver, null);
            }
            catch (System.Exception ex)
            {
                return Fail($"{ex.GetType().Name} {ex.Message}");
            }
        }

        private static Outcome Fail(string message)
            => new Outcome(false, false, 0, false, message);

        /// <summary>
        /// 按 1d20 的点数给某玩家分配初始手值 —— 与 1v1 循环赛**同一套逻辑**，
        /// 这样两个模式的统计口径一致。
        /// </summary>
        internal static void ApplyClassicHands(PlayerState player, int seed)
        {
            var rng = new System.Random(seed);
            int roll = DiceRollSetup.Roll(rng);

            List<HandSplit> splits = DiceRollSetup.DistinctSplits(roll);
            if (splits.Count == 0) return;

            // 取"两手最接近"的那个拆分（最稳，也最不容易制造极端局面）
            HandSplit best = splits[0];
            int bestGap = int.MaxValue;
            for (int i = 0; i < splits.Count; i++)
            {
                int gap = System.Math.Abs(splits[i].Left - splits[i].Right);
                if (gap < bestGap) { bestGap = gap; best = splits[i]; }
            }

            TestSetup.SetHand(player.LeftHand, best.Left);
            TestSetup.SetHand(player.RightHand, best.Right);

            // 武僧的 5 要进入太极（与实机一致）
            MonkLogic.ApplyTaichiToFives(player);
        }
    }
}
