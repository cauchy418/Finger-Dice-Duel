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

            /// <summary>模拟器被 MaxTurns 强行中断（异常情况，应当为 0）。</summary>
            public readonly bool HitLimit;

            /// <summary>正常打满轮次上限（按队伍总血量判定；相同才平局）。</summary>
            public readonly bool ReachedLimit;

            /// <summary>非空表示这一局出错了（异常信息），其余字段无意义。</summary>
            public readonly string Error;

            public Outcome(bool s14, bool s23, int actions, bool hitLimit,
                           bool reachedLimit, string error)
            {
                Seat14Won = s14;
                Seat23Won = s23;
                Actions = actions;
                HitLimit = hitLimit;
                ReachedLimit = reachedLimit;
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
        /// <param name="turnLimit">
        /// **轮次**上限（一轮 = 所有角色都经过一次行动；2v2 里等于 4 次行动）。
        /// ⚠️ 玩家要求把上限按轮算，这里传的是轮数，不是行动次数。
        /// </param>
        /// <param name="actions">实际进行了多少次行动</param>
        internal static Outcome Play(CharacterDefinition seat1, CharacterDefinition seat2,
                                     CharacterDefinition seat3, CharacterDefinition seat4,
                                     int seed, int turnLimit, out int actions)
            => PlayWith(seat1, seat2, seat3, seat4, seed, turnLimit,
                        null, null, out actions);

        /// <summary>
        /// 可注入抉择器 / 座位初始化钩子的版本（集火对照实验用）。
        ///
        /// 传 <paramref name="providers"/> = null 时与 <see cref="Play"/> 完全相同。
        /// <paramref name="onSeated"/> 在四名玩家创建后、开局的初始手值分配前调用，
        /// 用来注入确定性随机源（术士·狂野魔法的骰子）—— 这样三组实验共享同一批骰子。
        /// </summary>
        internal static Outcome PlayWith(CharacterDefinition seat1, CharacterDefinition seat2,
                                         CharacterDefinition seat3, CharacterDefinition seat4,
                                         int seed, int turnLimit,
                                         IReadOnlyList<IChoiceProvider> providers,
                                         System.Action<IReadOnlyList<PlayerState>> onSeated,
                                         out int actions)
        {
            actions = 0;

            var seated = new List<PlayerState>(4)
            {
                CharacterFactory.CreatePlayer("P1", seat1),
                CharacterFactory.CreatePlayer("P2", seat2),
                CharacterFactory.CreatePlayer("P3", seat3),
                CharacterFactory.CreatePlayer("P4", seat4),
            };

            onSeated?.Invoke(seated);

            // ⚠️ 每个座位一个**自己的**抉择器 —— 这是本测试台的核心。
            if (providers == null)
            {
                providers = new List<IChoiceProvider>(4)
                {
                    new GreedyChoiceProvider { UseLearnedFocus = true },
                    new GreedyChoiceProvider { UseLearnedFocus = true },
                    new GreedyChoiceProvider { UseLearnedFocus = true },
                    new GreedyChoiceProvider { UseLearnedFocus = true },
                };
            }

            if (providers.Count != 4)
                return Fail($"2v2 必须提供 4 个抉择器（实际 {providers.Count} 个）");

            try
            {
                GameManager game = GameManager.Create2v2(seated, providers);
                game.RoundLimit = turnLimit;

                // 与实机 BoardController 对齐：AI 的 GreedyChoiceProvider 需要拿到对局，
                // 法师·塑能学派等角色才会按局面做抉择（原来测试台漏了这一步）。
                for (int i = 0; i < providers.Count; i++)
                    if (providers[i] is GreedyChoiceProvider greedy) greedy.Game = game;

                // 循环上限必须**明显大于**"轮次上限折算成行动数"，
                // 否则循环会先退出、把对局当成"未决"（2v2 一轮 = 4 次行动）。
                //
                // ⚠️ 改成轮次口径后实测：只留 +16 的余量会在循环赛里踩到
                //    AccessViolation（对局已经打满轮次、动作还在推进）。
                //    所以这里留出充足余量。
                int actionCap = turnLimit * 4 + 200;

                // 经典开局：每人掷 1d20 决定初始手值
                for (int i = 0; i < seated.Count; i++)
                    ApplyClassicHands(seated[i], seed * 17 + i * 5 + 3);

                while (!game.IsGameOver && actions < actionCap)
                {
                    PlayerState actor = game.CurrentPlayer;
                    if (actor == null) break;                     // 全部阵亡

                    // ⚠️⚠️ **每个行动开始都要调 `BeginTurn()`。**
                    //
                    //    它是重置"只属于本回合"标记的**唯一**入口：
                    //      · `SuppressAttackDamage`（法师·塑能学派放弃攻击 = 这一击不造成伤害）
                    //      · `SchoolOfEvocationLogic.WantsToAbandonThisTurn`
                    //
                    //    这个测试台原来**一次都没调** —— 后果是：
                    //    法师·塑能学派第一次放弃之后抑制开关**永久为真**，
                    //    之后每回合都在白扔，一局能"放弃" 47 次、自己 0 伤害。
                    //    多人局里就表现为**谁都打不死谁、一直打到轮数上限**。
                    //
                    //    （1v1 的 `MatchRunner` 是调了的，所以 1v1 没这个现象。）
                    game.BeginTurn();
                    // 死灵免费暗蚀可能在回合开始就结束比赛，不能再向选择器要一步。
                    if(game.IsGameOver) break;
                    actor=game.CurrentPlayer;

                    IChoiceProvider choice = game.GetChoiceProvider(actor);
                    if (choice is not IMoveSelector selector)
                        return Fail($"座位 {game.SeatOf(actor)} 没有可用的出招选择器");

                    LegalMove move = selector.ChooseMove(game, actor);
                    if (move.Actor == null)
                    {
                        // 无合法步。**若规则允许跳过，就跳过** ——
                        // 这正是玩家确认的规则：
                        //   · 两只手都被锁 -> 出现跳过按钮，回合被跳过；
                        //   · 两只手都是完整盾 -> 也可以跳过。
                        // 没有这一步时，这种局面会被当成异常（实测 589 局中有 23 处）。
                        if (game.CanSkipTurnFor(actor))
                        {
                            game.SkipTurnAlreadyValidated();
                            actions++;
                            continue;
                        }

                        // 不能跳过却又无手可用：把**现场**带出来。
                        var detail = new System.Text.StringBuilder();
                        detail.Append($"座位 {game.SeatOf(actor)} 无合法步且不能跳过");
                        detail.Append($"（第 {actions + 1} 次行动）");
                        detail.Append($" 左L={actor.LeftHand.IsLocked}左盾={actor.LeftHand.IsShield}");
                        detail.Append($" 右L={actor.RightHand.IsLocked}右盾={actor.RightHand.IsShield}");
                        detail.Append(" 活着=");
                        for (int q = 0; q < seated.Count; q++)
                            if (seated[q].IsAlive) detail.Append($"P{game.SeatOf(seated[q])} ");
                        return Fail(detail.ToString());
                    }

                    // ⚠️ 让"AI 想打谁"真正落到结算上（与 BoardController 第 ③ 步一致）。
                    //    不传的话结算会回退到"被加数手主"—— 游荡者·魔术师自加会打自己，
                    //    而集火/目标优先级算出来的目标也会被丢掉。
                    if (choice is GreedyChoiceProvider greedyChoice)
                        game.DamageTargetPlayer = greedyChoice.IntendedVictim;

                    game.ExecuteTurn(actor, move.ActedHand, move.TargetHand);
                    game.DamageTargetPlayer = null;
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

                return new Outcome(s14, s23, actions, !game.IsGameOver,
                                   game.RoundCount >= game.RoundLimit, null);
            }
            catch (System.Exception ex)
            {
                return Fail($"{ex.GetType().Name} {ex.Message}");
            }
        }

        private static Outcome Fail(string message)
            => new Outcome(false, false, 0, false, false, message);

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

            // 武僧·太极宗的 5 要进入太极（与实机一致）
            WayOfTheTaiChiLogic.ApplyTaichiToFives(player);
        }
    }
}
