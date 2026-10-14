using System;
using System.Collections.Generic;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 单回合结算结果，供 UI 展示与测试断言使用。
    ///
    /// 注意：<see cref="ActedHand"/> / <see cref="TargetHand"/> 是**活动引用**。
    /// 同一只手会在后续回合继续变化，因此事后回看历史记录时，这两个引用反映的是
    /// "最终状态"而不是"当时状态"。需要当时数值时，一律使用
    /// <see cref="ActedHandValueBefore"/> / <see cref="ActedHandValueAfter"/> /
    /// <see cref="TargetHandValue"/> 这三个**快照**。
    /// </summary>
    public struct ExtraAttackInfo
    {
        /// <summary>发起者名字（战报用）。</summary>
        public string ActorName { get; }

        /// <summary>固定伤害值（无视攻防）。</summary>
        public int Damage { get; }

        /// <summary>实际造成的伤害（被护盾挡下时为 0）。</summary>
        public int Dealt { get; }

        /// <summary>是否被护盾挡下。</summary>
        public bool WasBlocked { get; }

        public ExtraAttackInfo(string actorName, int damage, int dealt, bool wasBlocked)
        {
            ActorName = actorName ?? "";
            Damage = damage;
            Dealt = dealt;
            WasBlocked = wasBlocked;
        }
    }

    /// <summary>
    /// 一次运算的结算结果。所有字段都是**快照**，不持有 Hand 的实时引用
    /// （这样 UI 显示历史战报时不会因为手值后续变化而前后不一致）。
    /// </summary>
    public class TurnResult
    {
        public PlayerState Actor;
        public Hand ActedHand;
        public Hand TargetHand;

        /// <summary>运算前行动方那只手的数值（快照）。</summary>
        public int ActedHandValueBefore;

        /// <summary>运算后行动方那只手的数值（快照，等于 <see cref="ResultValue"/>）。</summary>
        public int ActedHandValueAfter;

        /// <summary>运算时对方那只手的数值（快照）。</summary>
        public int TargetHandValue;

        public int ResultValue;
        public HandEffectType Effect;
        public bool ShieldBrokenBeforeOp;
        public int DamageDealt;
        public bool WasBlocked;

        /// <summary>本次攻击是否被闪避（游侠 / 刺客潜行）。</summary>
        public bool WasDodged;

        /// <summary>免伤原因（例如"目标在潜行中，无法被攻击"）。供战报显示。</summary>
        public string DodgeReason;

        /// <summary>
        /// 本次攻击是否被**邪术师取消**（玩家要求写进战报：
        /// "可以写被邪术师取消，造成 0 点伤害"）。
        /// </summary>
        public bool WasCancelled;

        /// <summary>取消者的名字（战报里要写清是谁取消的）。</summary>
        public string CancelledBy;

        /// <summary>本次行动是不是**赌博**（赌徒）。</summary>
        public bool WasGamble;

        // ---------- 盗贼的偷窃 ----------

        /// <summary>本次行动是不是**偷窃**（盗贼）。</summary>
        public bool StealUsed;

        /// <summary>偷窃前盗贼那只手的点数（"原先"）。</summary>
        public int StealOriginal;

        /// <summary>偷来的点数（"偷窃后"）。</summary>
        public int StealTaken;

        /// <summary>这次偷窃算不算**生效**（原先 ≥ 偷来的）。</summary>
        public bool StealEffective;

        /// <summary>赌博掷出的点数（1d9）。<see cref="WasGamble"/> 为 true 时有效。</summary>
        public int GambleRoll;

        public int Healed;

        /// <summary>
        /// 本次回复**效果本身的数值**（牧师 9 -> 9、4/6/8 当回复 -> 该数字）。
        ///
        /// 与 <see cref="Healed"/> 的区别：`Healed` 是"行动方实际回了多少血"
        /// （回给别人时是 0），而本字段是**这次回复的量**，供战报显示。
        /// </summary>
        public int HealAmount;

        /// <summary>本次回复效果实际作用到谁（牧师可以回给对手）。非回复效果时为 null。</summary>
        public PlayerState HealTarget;

        /// <summary>本次运算**之前**发生的追加攻击（剑客）。没有则为 null，供战报单独显示一行。</summary>
        public ExtraAttackInfo? ExtraAttack;

        /// <summary>本次攻击的段数（忍者 = 2，其余 = 1）。战报用它说明"两段攻击"。</summary>
        public int HitCount = 1;

        /// <summary>造成伤害的段数。</summary>
        public int LandedHits;

        /// <summary>被护盾挡下的段数。</summary>
        public int BlockedHits;

        /// <summary>被闪避掉的段数。</summary>
        public int DodgedHits;

        public bool GameOver;
        public PlayerState Winner;

        /// <summary>便于日志：例如 "A: 3 + 6 = 9 (Heal)"。</summary>
        public override string ToString()
            => $"{Actor?.Name}: {ActedHandValueBefore} + {TargetHandValue} = {ResultValue} ({Effect})";
    }

    /// <summary>
    /// 流程控制器：负责回合驱动、结算触发、胜负判定、连锁破盾防递归、整场复位。
    ///
    /// 回合契约（详见 RULES.md 第 5 节）：
    ///   行动方选自己的 1 只手 + 对方的 1 只手；
    ///   **只有行动方那只手的数值会改变**，对方的手值不变；
    ///   效果类型由行动方手的新值决定。
    /// </summary>
    public class GameManager
    {
        public PlayerState PlayerA { get; }
        public PlayerState PlayerB { get; }

        public IChoiceProvider ChoiceProviderA { get; set; }
        public IChoiceProvider ChoiceProviderB { get; set; }

        public bool IsGameOver { get; private set; }
        public PlayerState Winner { get; private set; }
        public PlayerState CurrentPlayer { get; private set; }
        public int TurnCount { get; private set; }

        /// <summary>本回合已破碎护盾集合，防止连锁破盾递归死循环。</summary>
        private readonly HashSet<Hand> _brokenThisTurn = new HashSet<Hand>();

        /// <summary>
        /// 由角色在结算过程中写入的"本次追加攻击"记录（剑客用）。
        /// <see cref="ResolveTurn"/> 结束时收进 <see cref="TurnResult.ExtraAttack"/> 并清空。
        ///
        /// 之所以要中转一下：追加攻击发生在 <c>OnBeforeEffectResolved</c> 里，
        /// 那时还没有 <see cref="TurnResult"/> 可以写。
        /// </summary>
        public ExtraAttackInfo? PendingExtraAttack;

        /// <summary>
        /// true 表示当前正在结算"追加攻击"（剑客）。
        ///
        /// 这类攻击发生在主攻击之前，玩家还没做过闪避选择，
        /// 所以它的闪避要**自动结算**（有几只"1"就闪几段），不弹提示。
        /// </summary>
        public bool ResolvingExtraAttack;

        /// <summary>
        /// **默认**回合数上限（1v1）。达到上限时按**平局**结束（见 RULES.md §7）。
        ///
        /// 这保证了对局一定会在有限步内结束，不会出现"双方都只顾回复"的死循环。
        /// </summary>
        public const int DefaultTurnLimit = 50;

        /// <summary>
        /// 本局的回合数上限。
        ///
        /// ⚠️ 从 `const` 改成**实例属性**的原因：多人局的回合数需要更长 ——
        ///    2v2 每"回合"只有一个人行动，同样的 50 回合只够 1v1 的四分之一，
        ///    所以玩家要求 2v2 用 **100**。
        ///
        /// 默认仍是 <see cref="DefaultTurnLimit"/>，所以 1v1 与既有测试不受影响；
        /// 平局判定与显示都读这个属性（不是常量）。
        /// </summary>
        public int TurnLimit { get; set; } = DefaultTurnLimit;

        private readonly List<LegalMove> _legalMoves = new List<LegalMove>(4);

        // ============================================================
        // 名单与队伍（2v2 用）
        //
        // ⚠️ 设计取舍：**保留 PlayerA / PlayerB 属性不动**。
        //
        // 它们是"两人对偶"写法，但全项目（含 465 项测试）都依赖它们；
        // 直接改成"只有名单"会让改动扩散到每一处。所以这里**在它们之上**
        // 加一层名单 + 队伍：
        //   · 1v1：名单 = [A, B]，队伍 = {A:0, B:1}
        //   · 2v2：名单 = [A, B, C, D]，队伍 = {A:0, B:1, C:0, D:1}
        //     （A/C 一队，B/D 一队 —— 交错排列，便于 UI 左右各两人）
        //
        // 于是"对手"从"另一个人"变成"所有不同队伍的人"，其余规则不变。
        // ============================================================

        /// <summary>参战玩家（按加入顺序）。1v1 时为 2 人，2v2 时为 4 人。</summary>
        public IReadOnlyList<PlayerState> Players => _players;

        /// <summary>每人的队伍号（0 / 1）。1v1 时 A=0、B=1。</summary>
        public int TeamOf(PlayerState player)
        {
            if (player == null) return -1;
            return _team.TryGetValue(player, out int team) ? team : -1;
        }

        /// <summary>这两位是不是队友（同一队伍）。</summary>
        public bool IsTeammate(PlayerState a, PlayerState b)
        {
            int ta = TeamOf(a);
            return ta >= 0 && ta == TeamOf(b);
        }

        /// <summary>
        /// **所有仍在场的对手**（不同队伍且活着）。
        ///
        /// 1v1 时恰好一个人；2v2 时最多两个。
        /// 排除了阵亡者 —— 否则界面会把死人列成可选目标。
        /// </summary>
        public IReadOnlyList<PlayerState> OpponentsOf(PlayerState player)
        {
            var result = new List<PlayerState>(2);
            if (player == null) return result;

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (ReferenceEquals(p, player)) continue;
                if (IsTeammate(p, player)) continue;
                if (!p.IsAlive) continue;

                result.Add(p);
            }

            return result;
        }

        /// <summary>某人的队友（不含自己），含阵亡者 —— 用于界面显示。</summary>
        public IReadOnlyList<PlayerState> TeammatesOf(PlayerState player)
        {
            var result = new List<PlayerState>(2);
            if (player == null) return result;

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (ReferenceEquals(p, player)) continue;
                if (!IsTeammate(p, player)) continue;

                result.Add(p);
            }

            return result;
        }

        /// <summary>
        /// **场上还活着几名玩家**（1v1 里可能变成 1，2v2 里可以是 1..4）。
        ///
        /// 用途：有些规则**只在仅剩两人时**生效 ——
        /// 例如野蛮人的"同时只能控制对手一只手"（见 <c>BarbarianLogic</c>）。
        /// </summary>
        public int AlivePlayerCount
        {
            get
            {
                int alive = 0;
                for (int i = 0; i < _players.Count; i++)
                    if (_players[i].IsAlive) alive++;

                return alive;
            }
        }

        /// <summary>某队是否已全灭（队伍里所有人都阵亡）。</summary>
        public bool IsTeamWiped(int team)
        {
            bool any = false;

            for (int i = 0; i < _players.Count; i++)
            {
                if (TeamOf(_players[i]) != team) continue;

                any = true;
                if (_players[i].IsAlive) return false;
            }

            // 该队一个人都没有时不算"全灭"（避免判定误伤）
            return any;
        }

        private readonly List<PlayerState> _players = new List<PlayerState>(4);
        private readonly Dictionary<PlayerState, int> _team =
            new Dictionary<PlayerState, int>();

        /// <summary>登记一名参战玩家及其队伍（构造时用；也供 2v2 扩展）。</summary>
        private void Register(PlayerState player, int team)
        {
            if (player == null) return;

            _players.Add(player);
            _team[player] = team;
        }

        public GameManager(PlayerState a, PlayerState b,
                           IChoiceProvider providerA, IChoiceProvider providerB)
        {
            PlayerA = a ?? throw new ArgumentNullException(nameof(a));
            PlayerB = b ?? throw new ArgumentNullException(nameof(b));
            ChoiceProviderA = providerA;
            ChoiceProviderB = providerB;

            Register(PlayerA, 0);
            Register(PlayerB, 1);

            CurrentPlayer = a;
            TurnCount = 0;

            // 开局即通知角色复位，保证内部状态与首局状态一致。
            NotifyMatchReset();
        }

        /// <summary>
        /// 2v2：再加两名玩家（C / D）。
        ///
        /// 队伍按玩家确认的**环形座位**分配：座位 `1-2-3-4`，**首尾同队**
        /// → `{A, D}` 与 `{B, C}`（A=1、B=2、C=3、D=4）。
        ///
        /// 这样每队两人**彼此相邻**（环上距离 1），而两名对手相隔 2 ——
        /// 于是"攻击距离 1"在 2v2 里是有意义的（详见 RULES.md）。
        ///
        /// 必须在**对局开始前**调用（第一回合之后加入会让回合顺序错乱）。
        /// </summary>
        public void AddPlayersFor2v2(PlayerState c, IChoiceProvider providerC,
                                     PlayerState d, IChoiceProvider providerD)
        {
            if (c == null || d == null)
                throw new ArgumentNullException(nameof(c), "2v2 的第三/第四名玩家不能为空。");

            PlayerC = c;
            PlayerD = d;
            ChoiceProviderC = providerC;
            ChoiceProviderD = providerD;

            Register(c, 1);                  // C（座位 3）与 B（座位 2）同队
            Register(d, 0);                  // D（座位 4）与 A（座位 1）同队（首尾相邻）

            c.Character?.OnMatchReset(c, this);
            d.Character?.OnMatchReset(d, this);
        }

        /// <summary>
        /// 8 人局：再加六名玩家（E~J 共 6 人，合计 8 人）。
        ///
        /// 每人**各自一队**（8 人局是混战），队伍号依次为 2..7。
        /// 队伍号只需"不同"，胜负判定按"存活队伍数"判断。
        /// </summary>
        public void AddPlayersFor8(PlayerState e, IChoiceProvider providerE,
                                   PlayerState f, IChoiceProvider providerF,
                                   PlayerState g, IChoiceProvider providerG,
                                   PlayerState h, IChoiceProvider providerH)
        {
            if (e == null || f == null || g == null || h == null)
                throw new ArgumentNullException(nameof(e), "8 人局的玩家不能为空。");

            PlayerE = e; PlayerF = f; PlayerG = g; PlayerH = h;
            ChoiceProviderE = providerE;
            ChoiceProviderF = providerF;
            ChoiceProviderG = providerG;
            ChoiceProviderH = providerH;

            Register(e, 2);
            Register(f, 3);
            Register(g, 4);
            Register(h, 5);

            e.Character?.OnMatchReset(e, this);
            f.Character?.OnMatchReset(f, this);
            g.Character?.OnMatchReset(g, this);
            h.Character?.OnMatchReset(h, this);
        }

        /// <summary>8 人局的第 5~8 名玩家（未加入时为 null）。</summary>
        public PlayerState PlayerE { get; private set; }
        public PlayerState PlayerF { get; private set; }
        public PlayerState PlayerG { get; private set; }
        public PlayerState PlayerH { get; private set; }

        public IChoiceProvider ChoiceProviderE { get; set; }
        public IChoiceProvider ChoiceProviderF { get; set; }
        public IChoiceProvider ChoiceProviderG { get; set; }
        public IChoiceProvider ChoiceProviderH { get; set; }


        /// <summary>2v2 的第三名玩家（1v1 时为 null）。</summary>
        public PlayerState PlayerC { get; private set; }

        /// <summary>2v2 的第四名玩家（1v1 时为 null）。</summary>
        public PlayerState PlayerD { get; private set; }

        public IChoiceProvider ChoiceProviderC { get; set; }
        public IChoiceProvider ChoiceProviderD { get; set; }

        /// <summary>
        /// **座位距离**（环形最短步数）。
        ///
        /// 玩家确认的规则：
        ///   · 座位围成一圈（4 人局 `1-2-3-4` 成环，8 人局同理）
        ///   · 距离 = 环上最短步数 = `min(|i-j|, n-|i-j|)`
        ///   · **阵亡的玩家从环上摘掉**（环收缩），所以活着的人会彼此靠近
        ///   · 自己到自己 = 0（不合法目标）
        ///
        /// 实测例子（与玩家给的数字一致）：
        ///   4 人：1↔2=1、1↔3=2、1↔4=1
        ///   8 人：1↔4=3、1↔5=4、1↔6=3
        /// </summary>
        public int DistanceBetween(PlayerState a, PlayerState b)
        {
            if (a == null || b == null) return -1;
            if (ReferenceEquals(a, b)) return 0;

            // 只把**活着的人**排成环（阵亡者不计入距离计算）
            var ring = new List<PlayerState>(4);
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].IsAlive) ring.Add(_players[i]);

            int ia = ring.IndexOf(a);
            int ib = ring.IndexOf(b);

            // 有人不在环上（已阵亡）-> 无法计算
            if (ia < 0 || ib < 0) return -1;

            int n = ring.Count;
            if (n < 2) return -1;

            int diff = ia > ib ? ia - ib : ib - ia;
            int wrap = n - diff;
            return diff < wrap ? diff : wrap;
        }

        /// <summary>
        /// <paramref name="actor"/> 能否打到 <paramref name="target"/>（只看距离）。
        ///
        /// 规则（玩家确认）：合法攻击目标 = **处于攻击范围内的其他角色**；
        /// 攻击距离由角色自己声明（<see cref="IAttackRangeProvider"/>），
        /// 咒术师与牧师无限，其余为 1。
        ///
        /// <paramref name="rangeOverride"/> 用于"本次攻击距离无限"的效果
        /// （战士碎盾、游侠强化）——传 <see cref="CharacterTemplate.InfiniteAttackRange"/>。
        /// </summary>
        public bool CanReach(PlayerState actor, PlayerState target, int rangeOverride = -1)
        {
            if (actor == null || target == null) return false;
            if (ReferenceEquals(actor, target)) return false;

            int distance = DistanceBetween(actor, target);
            if (distance < 0) return false;          // 有人已阵亡 -> 打不到

            int range = rangeOverride >= 0
                ? rangeOverride
                : BaseRangeOf(actor);

            return distance <= range;
        }

        /// <summary>某人的基础攻击距离（没实现接口时按 1 处理）。</summary>
        public static int BaseRangeOf(PlayerState player)
            => player?.Character is IAttackRangeProvider p ? p.BaseAttackRange : 1;

        /// <summary>这张手牌属于谁（在**全部**参战玩家里查）。
        ///
        /// 2v2 需要它：运算目标是"某位对手的一只手"，
        /// 而那位对手不一定是 <see cref="GetOpponent"/> 返回的第一位。
        /// </summary>
        public PlayerState OwnerOfHand(Hand hand)
        {
            if (hand == null) return null;

            for (int i = 0; i < _players.Count; i++)
                if (_players[i].Owns(hand)) return _players[i];

            return null;
        }

        /// <summary>
        /// 下一位该行动的玩家：按名单顺序轮转，**跳过已阵亡者**。
        ///
        /// 为什么不能沿用"切换成对手"：那是 1v1 写法 ——
        /// 4 个人时会让 A↔B 互相打、C/D 永远轮不到。
        ///
        /// 名单顺序是刻意排成"队伍交错"的（A 队0、B 队1、C 队0、D 队1），
        /// 所以轮转天然形成两队的交替出手。
        /// </summary>
        private PlayerState NextPlayerAfter(PlayerState current)
        {
            int count = _players.Count;
            if (count == 0) return null;

            int start = _players.IndexOf(current);
            if (start < 0) start = 0;

            // 至多绕一圈：若所有人都阵亡则不返回任何人
            for (int step = 1; step <= count; step++)
            {
                PlayerState candidate = _players[(start + step) % count];
                if (candidate.IsAlive) return candidate;
            }

            return null;
        }

        /// <summary>
        /// **2v2 落座**：按玩家选定的座位把四人放进环里。
        ///
        /// 玩家确认的座位规则：
        ///   · 四人围成环，**座位 1&4 一队、2&3 一队**
        ///   · 队友在环上**相邻**（所以主视角的队友一定是上家或下家）
        ///   · 主视角可以在**任意座位**（角色选择界面里选）
        ///
        /// <paramref name="seated"/> 必须正好 4 个人、**按座位 1..4 排列**；
        /// 本方法负责把它们映射到底层 A/B/C/D 槽位
        /// （A=座位1、B=座位2、C=座位3、D=座位4）。
        /// </summary>
        public static GameManager Create2v2(IReadOnlyList<PlayerState> seated,
                                            IReadOnlyList<IChoiceProvider> providers)
        {
            if (seated == null || seated.Count != 4)
                throw new ArgumentException("2v2 必须正好 4 名玩家（按座位 1..4 排列）。",
                                            nameof(seated));
            if (providers == null || providers.Count != 4)
                throw new ArgumentException("2v2 必须为 4 个座位各提供一个抉择器。",
                                            nameof(providers));

            var game = new GameManager(seated[0], seated[1], providers[0], providers[1]);
            game.AddPlayersFor2v2(seated[2], providers[2], seated[3], providers[3]);
            return game;
        }

        /// <summary>靠"环形座位"取名次：座位号 1..4（1 起算，便于与玩家沟通）。</summary>
        public int SeatOf(PlayerState player)
        {
            int index = _players.IndexOf(player);
            return index < 0 ? -1 : index + 1;
        }

        /// <summary>
        /// 某位玩家的**下家**（环上下一个座位，跳过阵亡者）。
        ///
        /// ⚠️ 与"回合顺序"是同一件事：<see cref="NextPlayerAfter"/> 就是按座位轮转的。
        /// 界面用它判断"队友是我的上家还是下家"。
        /// </summary>
        public PlayerState NextSeatAfter(PlayerState player) => NextPlayerAfter(player);

        /// <summary>
        /// 某位玩家的**上家**（环上前一个座位，跳过阵亡者）。
        ///
        /// 与"下家"对称实现，避免两处各写一套轮转逻辑。
        /// </summary>
        public PlayerState PreviousSeatBefore(PlayerState player)
        {
            int count = _players.Count;
            if (count == 0) return null;

            int start = _players.IndexOf(player);
            if (start < 0) start = 0;

            for (int step = 1; step <= count; step++)
            {
                int idx = ((start - step) % count + count) % count;
                if (_players[idx].IsAlive) return _players[idx];
            }

            return null;
        }

        /// <summary>兼容旧接口：取"某一个"对手。
        ///
        /// ⚠️ 1v1 之外请改用 <see cref="OpponentsOf"/>（可能不止一个）。
        ///
        /// ⚠️ 这里**不按存活过滤**：它是"取另一个人"的通用工具，
        /// 结算流程（含"双方同归于尽"这种边界）需要它总能给出一个结果。
        /// 按存活过滤会把"双方都阵亡"变成 null -> 结算里 NRE（真实 bug）。
        /// 需要"可攻击目标"请用 <see cref="OpponentsOf"/>。
        /// </summary>
        public PlayerState GetOpponent(PlayerState p)
        {
            if (p == null) return null;

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState other = _players[i];
                if (ReferenceEquals(other, p)) continue;
                if (IsTeammate(other, p)) continue;

                return other;
            }

            return null;
        }

        /// <summary>
        /// 查询"AI 已经替某一方做出的抉择"（由上层 BoardController 注入）。
        ///
        /// 为什么需要：`PreTurnChoiceReader.Read` 会回读 sink，而 sink 跨回合保留，
        /// 旧答案会被误当成"本回合的决定"，导致 AI 的技能不发动。
        /// 这份记录由上层在回合边界清空，所以只反映本回合。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AiDecisionLookup { get; set; }

        /// <summary>
        /// 查询"本回合**先前已经问过**的抉择的答案"（由上层 BoardController 注入）。
        ///
        /// 与 <see cref="AiDecisionLookup"/> 的区别：这个是"玩家已经答过的"。
        /// 用于分阶段抉择 —— 牧师的 4/6/8 先问"攻击还是回复"（选目标手之前），
        /// 之后再据此决定"要不要回复"。
        ///
        /// ⚠️ 不能只靠 sink 回读：两段抉择之间会重建队列、sink 可能被清空，
        /// 于是"选了回复"会读成 null 而被当成"选攻击"（真实 bug）。
        /// </summary>
        public System.Func<HandEffect, int, Hand> AnsweredLookup { get; set; }

        /// <summary>
        /// "**某位玩家**对某个抉择的作答"（带玩家参数）。
        ///
        /// 与 <see cref="AnsweredLookup"/> 的区别：那个已经绑定到"当前记录者"
        /// （行动方 / 防守方），而**邪术师是场上的第三方** ——
        /// 取消攻击的抉择不属于行动方也不属于防守方，所以要按人查。
        ///
        /// 由 <see cref="Assistant.BoardController"/> 注入。
        /// </summary>
        public System.Func<PlayerState, HandEffect, int, Hand> AnsweredLookupFor { get; set; }

        /// <summary>
        /// 查询"**是否问过**某玩家是否取消攻击"（由 <see cref="Assistant.BoardController"/> 注入）。
        ///
        /// 用于区分"没问过"（AI -> 自动取消）与"问过但选了不发动"（放行）。
        /// </summary>
        public System.Func<PlayerState, bool> AskedCancelLookup { get; set; }

        /// <summary>结算过程中询问玩家的回调（由上层注入）。</summary>
        public IChoiceGatherer ChoiceGatherer { get; set; }

        /// <summary>
        /// 测试辅助：直接把行动方指定为某人。
        ///
        /// 为什么需要：测试若靠"反复推回合"来等轮到自己，每回合的运算都会改手值，
        /// 测试设定的现场很快被推走 —— 断言看到的不是自己设的东西（踩过两次）。
        /// 直接指定行动方后，每一步的手值都完全可控。
        /// </summary>
        public void ForceActivePlayerForTest(PlayerState player)
        {
            if (player == null) return;
            CurrentPlayer = player;
        }

        /// <summary>
        /// 取某人的抉择器。
        ///
        /// 2v2 有 4 个人，所以按身份逐个匹配 ——
        /// 不能用"A 就是 providerA、其余都是 providerB"这种二元写法。
        /// </summary>
        public IChoiceProvider GetChoiceProvider(PlayerState p)
        {
            if (ReferenceEquals(p, PlayerA)) return ChoiceProviderA;
            if (ReferenceEquals(p, PlayerB)) return ChoiceProviderB;
            if (ReferenceEquals(p, PlayerC)) return ChoiceProviderC;
            if (ReferenceEquals(p, PlayerD)) return ChoiceProviderD;
            if (ReferenceEquals(p, PlayerE)) return ChoiceProviderE;
            if (ReferenceEquals(p, PlayerF)) return ChoiceProviderF;
            if (ReferenceEquals(p, PlayerG)) return ChoiceProviderG;
            if (ReferenceEquals(p, PlayerH)) return ChoiceProviderH;

            return null;
        }

        // ============================================================
        // 整场复位
        // ============================================================

        /// <summary>
        /// 复位整场比赛：生命、双手数值、护盾状态、锁定状态、回合计数、胜负标记，
        /// 并通知每个角色清空自身跨回合状态（<see cref="ICharacterBehavior.OnMatchReset"/>）。
        ///
        /// 重开一局必须走这里，而不是直接调用 PlayerState.Reset()，
        /// 否则野蛮人的锁手会残留到下一局。
        /// </summary>
        public void ResetMatch()
        {
            _brokenThisTurn.Clear();
            PlayerA.Reset();
            PlayerB.Reset();

            IsGameOver = false;
            Winner = null;
            TurnCount = 0;
            CurrentPlayer = PlayerA;

            NotifyMatchReset();
        }

        private void NotifyMatchReset()
        {
            PlayerA.Character?.OnMatchReset(PlayerA, this);
            PlayerB.Character?.OnMatchReset(PlayerB, this);
        }

        /// <summary>
        /// 把场上所有武僧手上**值为 5** 的手变成**太极形态**。
        ///
        /// 玩家要求："武僧初始有 5 的话这个 5 是可以发动的，处于太极状态。"
        ///
        /// 为什么需要单独一步：`ShieldState.Taichi` 原本只在**运算产生 Shield 效果**
        /// 时才被设上；而开局直接摆出来的 5（掷骰决定的手值、测试直接设的手值）
        /// 没经过那一步，状态还是 None —— 于是"有 5 却不能发动太极"。
        ///
        /// 由上层在**开局手值确定之后**调用（掷骰分配完成 / 直接开局）。
        /// 幂等：已经是太极或被锁定 / 已成型的手不会被改写。
        /// </summary>
        public void ApplyInitialTaichiForm()
        {
            for (int i = 0; i < _players.Count; i++)
                MonkLogic.ApplyTaichiToFives(_players[i]);
        }

        // ============================================================
        // 合法运算枚举与校验
        // ============================================================

        /// <summary>
        /// 枚举 <paramref name="actor"/> 当前所有合法运算（每人最多 2×2 = 4 种）。
        /// 被锁定的手不能作为运算源，但仍可作为运算目标。
        /// </summary>
        public IReadOnlyList<LegalMove> GetLegalMoves(PlayerState actor)
        {
            _legalMoves.Clear();
            if (actor == null || IsGameOver) return _legalMoves;

            // ⚠️ 用"所有对手"而不是"某一个对手" ——
            // 2v2 里 A 可以打 B 或 D，只列第一个会漏掉另一个。
            //
            // ⚠️ 另外：**被加数可以是任何其他玩家**（含队友）——
            // 玩家确认的规则是"A 可以选择 B/C/D 任何一个数字作为被加数"，
            // 别人的数字不会被改动，只有行动方自己的手变成结果值。
            // 所以这里枚举"除自己以外的全部玩家"，不过滤队友。
            foreach (Hand mine in actor.Hands)
            {
                if (mine.IsLocked) continue;

                for (int i = 0; i < _players.Count; i++)
                {
                    PlayerState owner = _players[i];
                    if (ReferenceEquals(owner, actor)) continue;

                    foreach (Hand theirs in owner.Hands)
                        _legalMoves.Add(new LegalMove(actor, mine, theirs));
                }
            }

            return _legalMoves;
        }

        /// <summary>
        /// 校验一次运算是否合法。不合法时通过 <paramref name="reason"/> 返回原因。
        /// </summary>
        public bool IsLegalMove(PlayerState actor, Hand actedHand, Hand targetHand, out string reason)
        {
            reason = null;

            if (IsGameOver) { reason = "比赛已结束。"; return false; }
            if (actor == null) { reason = "行动方为空。"; return false; }
            if (actedHand == null) { reason = "未选择运算源。"; return false; }
            if (targetHand == null) { reason = "未选择运算目标。"; return false; }

            if (!actor.Owns(actedHand)) { reason = "运算源必须是行动方自己的手。"; return false; }

            // ⚠️ 判据是"**是不是队友**"，不是"是不是那唯一一个对手"。
            //
            // 被加数可以是**任何其他玩家**的手（含队友）。
            //
            // 玩家确认的规则："A 可以选择 B/C/D 任何一个数字作为被加数" ——
            // 别人的数字**不会被改动**，只有 A 自己的手变成结果值。
            // 所以这里只排除"自己的手"，不排除队友。
            PlayerState addendOwner = OwnerOfHand(targetHand);
            if (addendOwner == null)
            {
                reason = "被加数必须属于某位参战玩家。";
                return false;
            }

            if (ReferenceEquals(addendOwner, actor))
            {
                reason = "被加数不能是自己的手（要用别人的数字来改变自己的手）。";
                return false;
            }

            if (actedHand.IsLocked) { reason = "被锁定的手不能作为运算源。"; return false; }

            return true;
        }

        // ============================================================
        // 回合执行
        // ============================================================

        /// <summary>
        /// 执行一次回合操作（推荐入口，参数不可混淆）。
        /// </summary>
        /// <exception cref="InvalidOperationException">运算不合法时抛出（不静默失败）。</exception>
        public TurnResult ExecuteTurn(PlayerState actor, Hand actedHand, Hand targetHand)
        {
            if (!IsLegalMove(actor, actedHand, targetHand, out string reason))
                throw new InvalidOperationException($"非法运算：{reason}");

            return ResolveTurn(actor, actedHand, targetHand.Value, targetHand);
        }

        /// <summary>
        /// 执行一次回合操作（索引 + 数值形式）。
        /// otherHandValue 是**对方**参与运算的那只手的数值。
        /// </summary>
        /// <exception cref="InvalidOperationException">运算不合法时抛出（不静默失败）。</exception>
        public TurnResult ExecuteTurn(PlayerState actor, int actedHandIndex, int otherHandValue)
        {
            if (IsGameOver)
                throw new InvalidOperationException("非法运算：比赛已结束。");
            if (actor == null)
                throw new InvalidOperationException("非法运算：行动方为空。");

            Hand actedHand = actor.GetHand(actedHandIndex);
            if (actedHand.IsLocked)
                throw new InvalidOperationException("非法运算：被锁定的手不能作为运算源。");

            return ResolveTurn(actor, actedHand, otherHandValue, null);
        }

        /// <summary>
        /// 最近一次"武僧的 0 被触碰变成 1"的说明（供战报显示）；没有则为 null。
        ///
        /// 只在一次结算内有效。
        /// </summary>
        public string MonkZeroTouchNote { get; private set; }

        // ============================================================
        // 盗贼：偷窃
        // ============================================================

        /// <summary>
        /// 盗贼上一次**发起**偷窃是在第几回合（−1 = 还没偷过）。
        ///
        /// 玩家规则："若上回合**发起了**偷窃，本回合不能继续偷窃"，
        /// 且"**只要点了偷窃就算'发起过'**"（哪怕判定不生效）。
        ///
        /// ⚠️ 记的是**回合号**而不是布尔值 —— 布尔值在"跳过回合"等
        ///    不推进回合数的路径上会残留（本项目在诊断字段上踩过这个坑）。
        /// </summary>
        private int _lastStealTurn = -1;

        /// <summary>上一次发起偷窃的是谁（配合 <see cref="_lastStealTurn"/> 用）。</summary>
        private PlayerState _lastStealActor;

        /// <summary>
        /// **盗贼自己的行动次数**（每结算一次他的行动 +1）。
        ///
        /// 为什么要单独计数，而不是用 `TurnCount`：
        ///   `TurnCount` 每过一个回合都会变，于是"回合号 != 上次回合号"
        ///   **永远成立** —— 冷却形同虚设，盗贼可以**连续偷**
        ///   （玩家实测："盗贼现在可以连续偷"）。
        ///
        ///   正确判据是"他**上一次行动**偷过吗"，所以需要一个
        ///   只在**他自己行动时**递增的计数器。
        /// </summary>
        private readonly Dictionary<PlayerState, int> _ownTurnOrdinal =
            new Dictionary<PlayerState, int>();

        /// <summary>某玩家**当前**的行动序号（没有记录时返回 0）。</summary>
        private int CurrentOwnTurnOrdinal(PlayerState player)
        {
            if (player == null) return -1;
            _ownTurnOrdinal.TryGetValue(player, out int cur);
            return cur;
        }

        /// <summary>
        /// **推进某人的行动序号**（他"这次行动"结束了）。
        /// </summary>
        ///
        /// 由 <see cref="ResolveSteal"/> 在偷窃收尾时调用 ——
        /// 与普通运算的 <see cref="ResolveTurn"/> 末尾对应。
        /// 有了它，"他上一次行动偷过吗"才有正确的时间基准。
        /// </summary>
        private void BumpOwnTurnOrdinal(PlayerState player)
        {
            if (player == null) return;

            _ownTurnOrdinal.TryGetValue(player, out int cur);
            _ownTurnOrdinal[player] = cur + 1;
        }

        /// <summary>盗贼本回合能不能偷窃（他自己上一次行动偷过就不能）。</summary>
        public bool CanStealOn(PlayerState thief)
        {
            if (thief == null || IsGameOver) return false;
            if (!(thief.Character is ThiefLogic)) return false;

            // ⚠️ 判据：**这个人上一次自己的行动**是不是偷窃。
            //
            //    我踩过两次：
            //      ① 用 `_lastStealTurn != TurnCount` —— TurnCount 在结算后才自增，
            //         于是刚偷完就判成"可以偷"。
            //      ② 改成"记录偷完之后的回合号"仍然不对 ——
            //         因为 TurnCount **每回合都会变**，
            //         等再次轮到他时回合号当然又不同了，所以**永远可以偷**。
            //
            //    真正要比的是"他自己的两次行动之间有没有别的行动"，
            //    所以用 <see cref="_ownTurnOrdinal"/> 这个按人计数的序号。
            if (!ReferenceEquals(_lastStealActor, thief)) return true;
            if (!_ownTurnOrdinal.TryGetValue(thief, out int serial)) return true;

            return _lastStealTurn != serial;
        }

        /// <summary>
        /// **偷窃**：交换盗贼与被偷者的两只手（点数与状态），
        /// 若"偷窃前的点数 ≥ 偷来的点数"则按**偷来的数字**触发效果。
        ///
        /// 玩家给的完整规则见 <see cref="ThiefLogic"/> 的类注释。
        ///
        /// 消耗整个回合。
        /// </summary>
        /// <exception cref="InvalidOperationException">校验失败时抛出。</exception>
        public TurnResult ExecuteSteal(PlayerState actor, Hand myHand, Hand theirHand)
        {
            if (IsGameOver)
                throw new InvalidOperationException("非法偷窃：比赛已结束。");
            if (actor == null)
                throw new InvalidOperationException("非法偷窃：行动方为空。");
            if (!(actor.Character is ThiefLogic))
                throw new InvalidOperationException("非法偷窃：该角色不是盗贼。");
            if (myHand == null || !actor.Owns(myHand))
                throw new InvalidOperationException("非法偷窃：必须用自己的一只手。");
            if (myHand.IsLocked)
                throw new InvalidOperationException("非法偷窃：被锁定的手不能用来偷窃。");
            if (theirHand == null)
                throw new InvalidOperationException("非法偷窃：没有指定被偷的手。");
            if (ReferenceEquals(theirHand, myHand))
                throw new InvalidOperationException("非法偷窃：不能偷自己。");
            if (!CanStealOn(actor))
                throw new InvalidOperationException("非法偷窃：上回合已经偷过，本回合不能再偷。");

            // 标记"发起过" —— 玩家明确：只要点了偷窃就算，哪怕判定不生效
            _lastStealActor = actor;

            ThiefLogic.StealOutcome outcome = ThiefLogic.Steal(myHand, theirHand);

            // ⚠️ **武僧的 0 被触碰就变 1**（玩家补充规则）。
            //
            //    "盗贼偷武僧的 0 只能偷到 1，目前只有武僧能持有 0。"
            //
            //    这是"0 被他人触碰后立即变成 1"那条规则在偷窃路径上的延伸 ——
            //    ⚠️ 我原来的 `ResolveTurn` 里只处理了"被当**加数**用"，
            //       而偷窃**不走运算**，所以那条守卫覆盖不到这里
            //       （这是"两条路径各写一份而漂移"的典型案例）。
            //
            //    所以两边都要纠正：手值 0 在任何人手上都只可能是武僧的太极产物，
            //    一旦经由偷窃换手，就按"被触碰"处理。
            if (myHand.Value == ThiefLogic.MonksZeroBecomesOne)
                myHand.SetValue(1);            // 盗贼只能偷到 1

            if (theirHand.Value == ThiefLogic.MonksZeroBecomesOne)
                theirHand.SetValue(1);         // 武僧那只 0 也被触碰了

            // 被偷者若是武僧，他收到的 5 会变成**太极**（玩家最新规则）
            ThiefLogic.ApplyVictimShieldSemantics(OwnerOfHand(theirHand), theirHand);

            // 结算：只有**生效**时才按偷来的数字触发效果
            return ResolveSteal(actor, myHand, theirHand, outcome);
        }

        /// <summary>偷窃的结算：判定生效则按偷来的数字走效果分派。</summary>
        private TurnResult ResolveSteal(PlayerState actor, Hand myHand, Hand theirHand,
                                        ThiefLogic.StealOutcome outcome)
        {
            var result = new TurnResult
            {
                Actor = actor,
                TargetHand = theirHand,
                StealUsed = true,
                StealOriginal = outcome.Original,
                StealTaken = outcome.Stolen,
                StealEffective = outcome.Effective,
            };

            // ⚠️ 无论生效与否，交换**已经发生**（在 ThiefLogic.Steal 里），
            //    所以这里只负责"要不要触发效果"。
            if (!outcome.Effective)
            {
                _shockLog.Clear();
                CurrentPlayer = NextPlayerAfter(actor);
                TurnCount++;
                // ⚠️ 顺序很重要：**先递增、再记录**。
                //    回忆一下判据：`_lastStealTurn != CurrentOwnTurnOrdinal(thief)`
                //    -> 可以偷。
                //    若先记录 0 再递增到 1，那么下一次行动时
                //    读到的序号已经是 1，与记录的 0 不等 -> 又能偷了（冷却失效）。
                //    正确做法：先把他这一次行动算完，
                //    再记下"他这一次行动的序号"。
                BumpOwnTurnOrdinal(actor);
                _lastStealTurn = CurrentOwnTurnOrdinal(actor);
                CheckTurnLimit();
                return result;
            }

            int stolen = outcome.Stolen;

            // 偷来的 5 -> **合成一面完整的盾**（玩家明确要求：
            // "无论之前这个 5 是什么状态，盗贼都会合成一面盾牌"）。
            // 所以这里**不受**"已碎"影响，直接设成完整盾。
            HandEffectType effect = ThiefLogic.EffectOfStolen(stolen);

            result.ResultValue = stolen;
            result.Effect = effect;

            if (effect == HandEffectType.Shield)
            {
                myHand.SetShieldState(ShieldState.Formed);
                actor.Character?.OnShieldFormed(actor, myHand, this);
            }
            else if (effect == HandEffectType.Heal)
            {
                int before = actor.Health;
                actor.Heal(Hand.HealAmount);
                result.Healed = actor.Health - before;
                result.HealAmount = Hand.HealAmount;
                result.HealTarget = actor;
            }
            else if (effect == HandEffectType.Attack4
                     || effect == HandEffectType.Attack6
                     || effect == HandEffectType.Attack8)
            {
                // ⚠️ **不在这里结算伤害**（玩家实测要求）：
                //    "盗贼偷窃出攻击数字要**再次点击目标**才可以攻击，
                //      现在是默认攻击。"
                //
                //    所以这里只**登记**"偷出了一个待结算的攻击"，
                //    由上层（BoardController）走与普通运算一样的
                //    「选打谁」那一步，玩家确认后才真的打。
                //
                //    这与"攻击目标单独确认"的既有规则一致 ——
                //    偷窃只是换了个方式得到攻击数字，目标确认不该被跳过。
                PendingStealAttackValue = stolen;
                PendingStealAttackHand = myHand;
                StealAttackAwaitingTarget = true;
                return result;                  // 把收尾交给上层
            }

            actor.Character?.OnOperationResolved(actor, myHand, outcome.Original, stolen,
                                                 effect, this, null);

            CheckGameOver();

            if (!IsGameOver)
            {
                TurnCount++;
                // ⚠️ 顺序很重要：**先递增、再记录**。
                //    回忆一下判据：`_lastStealTurn != CurrentOwnTurnOrdinal(thief)`
                //    -> 可以偷。
                //    若先记录 0 再递增到 1，那么下一次行动时
                //    读到的序号已经是 1，与记录的 0 不等 -> 又能偷了（冷却失效）。
                //    正确做法：先把他这一次行动算完，
                //    再记下"他这一次行动的序号"。
                BumpOwnTurnOrdinal(actor);
                _lastStealTurn = CurrentOwnTurnOrdinal(actor);      // 他的"这次行动"结束了
                CurrentPlayer = NextPlayerAfter(actor);
                CheckTurnLimit();
            }

            return result;
        }

        // ============================================================
        // 盗贼：偷出攻击时的"待确认目标"
        // ============================================================

        /// <summary>是否正在等"偷出的攻击"选定目标。</summary>
        public bool StealAttackAwaitingTarget { get; private set; }

        /// <summary>偷出来的那个攻击数字（4/6/8）。</summary>
        public int PendingStealAttackValue { get; private set; }

        /// <summary>偷窃时用的是哪只手（结算伤害时要用它当运算源）。</summary>
        public Hand PendingStealAttackHand { get; private set; }

        /// <summary>
        /// **结算"偷出的攻击"**（上层在选定目标之后调用）。
        ///
        /// 与普通运算的攻击走同一套 <see cref="AttackContext"/>，
        /// 所以闪避 / 护盾 / 太极 / 取消都照常生效。
        /// </summary>
        public TurnResult ResolvePendingStealAttack(PlayerState actor, PlayerState victim)
        {
            StealAttackAwaitingTarget = false;

            TurnResult result = LastTurnResult ?? new TurnResult();
            if (actor == null || victim == null || PendingStealAttackHand == null)
                return result;

            if (!SuppressAttackDamage)
            {
                var ctx = new AttackContext(actor, victim, PendingStealAttackHand,
                                            PendingStealAttackValue);
                ctx.Resolve(GetChoiceProvider(victim), this);

                result.DamageDealt = ctx.AppliedDamage;
                result.HitCount = ctx.HitCount;
            }

            result.TargetHand = PendingStealAttackHand;
            LastTurnResult = result;

            PendingStealAttackHand = null;
            PendingStealAttackValue = 0;

            CheckGameOver();

            if (!IsGameOver)
            {
                TurnCount++;
                CurrentPlayer = NextPlayerAfter(actor);
                CheckTurnLimit();
            }

            return result;
        }

        /// <summary>
        /// 最近一次结算的 <see cref="TurnResult"/>（供"偷出的攻击"分两步时续写）。
        /// </summary>
        public TurnResult LastTurnResult { get; private set; }

        /// <summary>
        /// 盗贼**发起过偷窃**是在第几回合（−1 = 没偷过）。
        /// </summary>
        public int LastStealTurn => _lastStealTurn;

        /// <summary>已通过校验的实际结算流程。</summary>
        /// <summary>已通过校验的实际结算流程。</summary>
        /// <summary>已通过校验的实际结算流程。</summary>
        private TurnResult ResolveTurn(PlayerState actor, Hand actedHand, int targetValue, Hand targetHand)
            => ResolveTurn(actor, actedHand, targetValue, targetHand, gambleResult: 0);

        /// <summary>
        /// **赌博结算**：掷 1d9，**结果直接作为本次行动的结果**。
        ///
        /// 玩家确认的规则：
        ///   · 4/6/8 攻击、9 回血、5 合盾 —— 与普通运算**同一套**效果规则
        ///   · 赌博**不需要**点他人的手；打谁在结果出来之后再选
        ///
        /// <paramref name="targetHand"/> 只用于"指定挨打的人"（结果出来后才定），
        /// 可以传 null —— 那就走默认的"打第一位对手"。
        /// </summary>
        /// <exception cref="InvalidOperationException">不是赌徒、或比赛已结束时抛出。</exception>
        public TurnResult ExecuteGamble(PlayerState actor, Hand actedHand, Random rng,
                                        Hand targetHand = null)
        {
            if (rng == null) rng = new Random();
            return ExecuteGambleWithRoll(actor, actedHand, GamblerLogic.Roll(rng), targetHand);
        }

        /// <summary>
        /// 用**已经掷好的点数**结算赌博。
        ///
        /// ⚠️ 为什么必须有这个重载：掷出攻击（4/6/8）时流程是
        ///    "开赌掷点 -> 选打谁 -> 结算"，也就是**掷点与结算分属两次调用**。
        ///    如果结算时又调 <see cref="ExecuteGamble"/>，就会**再掷一次**，
        ///    玩家看到的点数与实际结算的点数不同
        ///    （玩家实测："先说投出了 4，点战士时又说投出 2 无效果"）。
        ///
        ///    所以点数只能由**开赌那一次**决定，之后一路传递。
        /// </summary>
        public TurnResult ExecuteGambleWithRoll(PlayerState actor, Hand actedHand,
                                                int roll, Hand targetHand = null)
        {
            if (IsGameOver)
                throw new InvalidOperationException("非法赌博：比赛已结束。");
            if (actor == null)
                throw new InvalidOperationException("非法赌博：行动方为空。");
            if (!(actor.Character is GamblerLogic))
                throw new InvalidOperationException("非法赌博：该角色不是赌徒。");
            if (actedHand == null || !actor.Owns(actedHand))
                throw new InvalidOperationException("非法赌博：运算源必须是自己的手。");
            if (actedHand.IsLocked)
                throw new InvalidOperationException("非法赌博：被锁定的手不能作为运算源。");

            return ResolveTurn(actor, actedHand, targetValue: 0, targetHand,
                               gambleResult: roll);
        }

        /// <summary>已通过校验的实际结算流程。</summary>
        private TurnResult ResolveTurn(PlayerState actor, Hand actedHand, int targetValue,
                                       Hand targetHand, int gambleResult)
        {
            var result = new TurnResult { Actor = actor, TargetHand = targetHand };
            _brokenThisTurn.Clear();

            // 0. 说明：本方法**不**清理 ShieldState.Broken。
            //
            //    Broken 表示"这面盾碎了、但它的手值仍然是 5，所以还能被修复"。
            //    只要这只手还是 5，碎盾状态就一直保留，**无论过多少回合**；
            //    一旦它不再是 5（被运算改写），它就成了普通手牌，状态自然回到 None。
            //    这两条由第 3 步的值判断保证，因此不需要按回合清理。
            // ⚠️ 被加数的手主**不一定**是挨打的人。
            //
            // 玩家确认的规则：第 ③ 步**单独选攻击目标**，可以与被加数的手主不同。
            // 所以挨打的默认取被加数手主（保持 1v1 与既有测试不变），
            // 但 <see cref="DamageTargetPlayer"/> 可以覆盖它。
            PlayerState addendOwner = OwnerOfHand(targetHand);
            PlayerState opponent = DamageTargetPlayer ?? addendOwner ?? GetOpponent(actor);

            // ⚠️ **武僧的 0 被他人触碰 -> 立即变 1**（玩家确认的规则）。
            //
            //    触发条件：**别人把那只见习武僧的手当作加数用**。
            //    且"当成 1 参与本次运算" —— 所以必须在这里（结算前）改，
            //    而不是结算后（那样本次运算的结果就不对了）。
            //
            //    注意"别人"：自己用自己的 0 不算触碰。
            if (targetHand != null
                && addendOwner != null
                && !ReferenceEquals(addendOwner, actor)
                && addendOwner.Character is MonkLogic)
            {
                int before = targetHand.Value;
                MonkLogic.OnTouchedAsAddend(targetHand);

                if (targetHand.Value != before)
                {
                    // 手值变了 -> 参与运算的数字也要跟着变
                    targetValue = targetHand.Value;
                    result.TargetHandValue = targetValue;
                    MonkZeroTouchNote = $"{addendOwner.Name}（武僧）的手被触碰，" +
                                        $"{before} 变成 {targetHand.Value}";
                }
            }

            IChoiceProvider oppChoice = GetChoiceProvider(opponent);

            result.ActedHand = actedHand;
            result.ActedHandValueBefore = actedHand.Value;
            result.TargetHandValue = targetValue;

            // 1. 运算前破盾（自己用一面已成型的盾发起运算时，该盾先碎）
            result.ShieldBrokenBeforeOp = actedHand.BreakShieldBeforeOperation();
            if (result.ShieldBrokenBeforeOp)
                NotifyOwnShieldBroken(actor, actedHand);

            // 2. 执行运算：只有行动方的手会改变
            //
            //    ⚠️ 赌博（gambleResult > 0）**不走运算** ——
            //       玩家确认"1d9 的结果就作为本次行动结果"，
            //       所以直接把骰子点数写进运算源，效果规则与普通运算共用
            //       （Hand.GetEffectType），避免两处各写一份而漂移。
            HandEffectType effect;
            if (gambleResult > 0)
            {
                actedHand.SetValue(gambleResult);      // 骰子点数即手值
                effect = Hand.GetEffectType(gambleResult);
                result.WasGamble = true;
                result.GambleRoll = gambleResult;

                // ⚠️ `SetValue` 会**清空护盾状态**（它本来的设计就是
                //    "绕过运算直接设值，状态归 None"）。所以赌出 5 时
                //    必须自己把状态补成成型护盾，否则会出现
                //    "效果是护盾、但手牌状态是 None"（测试抓到）。
                if (effect == HandEffectType.Shield)
                    actedHand.SetShieldState(ShieldState.Formed);
            }
            else
            {
                effect = actedHand.PerformOperation(targetValue);
            }

            // 2b. 角色可以**改写效果类型**（圣武士的 4/6 改为合出护盾）。
            //
            //     必须在这里做：手值已经变成结果值（盾就是 5），
            //     但护盾状态还没设 —— 改写后要由我们补上状态，
            //     否则会出现"看起来是盾、实际不是盾"。
            if (actor.Character is IHandEffectRewriter rewriter)
            {
                HandEffectType rewritten = rewriter.RewriteEffect(
                    actor, actedHand, targetValue, this);

                if (rewritten != effect)
                {
                    effect = rewritten;

                    // 改写结果决定手牌状态（与 Hand.PerformOperation 同一约定）。
                    //
                    // ⚠️ **不要在这里调 OnShieldFormed** —— 效果分派处
                    //    （后面那段按 effect 分发的代码）已经会调一次，
                    //    这里再调就变成"合一次盾触发两次"（圣武士的冲击
                    //    因此造成 4 点而不是 2 点，测试抓到）。
                    actedHand.SetShieldState(effect == HandEffectType.Shield
                        ? ShieldState.Formed
                        : ShieldState.None);
                }
            }

            // 3. 运算前碎掉的盾：只有**结果仍然是 5** 时才保留 Broken 标记。
            //
            //    Broken 的语义是"这面盾碎了，但它仍然是一面（值为 5 的）盾，可以被修复"。
            //    如果运算把这只手变成了别的数字（例如 5+3=8），它就已经是普通手牌，
            //    不该再挂"碎盾"标签 —— 否则界面会出现"值为 8 的手显示碎盾展开"。
            //
            //    反过来，只要手值还是 5，Broken 就一直保留（可跨回合、可无限期），
            //    战士随时都能用另一只手合出新盾把它修好。
            if (result.ShieldBrokenBeforeOp
                && actedHand.State == ShieldState.None
                && actedHand.Value == 5)
            {
                actedHand.SetShieldState(ShieldState.Broken);
            }

            result.ResultValue = actedHand.Value;
            result.ActedHandValueAfter = actedHand.Value;
            result.Effect = effect;

            // 3. 效果分派前的"**双 9 抢跑**"阶段。
            //
            //    牧师的规则要求：运算出双 9 时，**先自动回自己 9 点**，
            //    然后再结算"运算出 9"那次回复。所以这一步必须排在效果分派之前。
            //
            //    判定用的是"运算后的双手值"：运算源已经是结果值，
            //    另一只手在运算中不变，所以两值之和为 18 即双 9。
            //    ⚠️ 赌博时 `targetHand` 是 **null**（赌博不点他人的手）——
            //       所以不能直接 `targetHand.Value`，否则空引用（测试抓到）。
            //       赌博没有"另一只手"，用运算源自己的值兜底：
            //       那只手此刻已经是骰子点数，所以"双手和"这类条件不会被误判成成立
            //       （双 X 需要两只手同值，而骰子点数不会因此凑巧翻倍）。
            int otherHandValue = targetHand != null ? targetHand.Value : actedHand.Value;

            actor.Character?.OnBeforeEffectResolved(
                actor, actedHand, otherHandValue, effect, this,
                GetChoiceProvider(actor) as IPreTurnChoiceSink);

            // 3b. 分派效果
            int healthBefore = actor.Health;

            // 牧师可以把 4/6/8 **当回复用**（回复量就是该数字，不受攻防影响）。
            // 这一步在分派**之前**完成，因为"选了回复"就完全不走攻击分支。
            //
            // 返回 null 表示"玩家选的是攻击" -> 走正常攻击路径；
            // 返回某位玩家表示"选的是回复，目标是这位"。
            //
            // 两步走（与交互顺序一致）：
            //   ① 前置抉择（**选目标手之前**）里玩家选的是"攻击"还是"回复"
            //   ② 若选了回复，再解析"回复给谁"（**选目标手之后**才问）
            PlayerState priestHealTarget = null;
            if (actor.Character is PriestLogic && isAttackEffect(effect))
            {
                var sink = GetChoiceProvider(actor) as IPreTurnChoiceSink;

                if (PriestLogic.ChoseToHeal(actor, this, sink, AnsweredLookup))
                    priestHealTarget = PriestLogic.ResolveTarget(actor, this, sink,
                                                                 HandEffect.PriestHealTarget);
            }

            switch (effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                    {
                        // ① 牧师选择"当回复用" -> 不造成伤害，改为回复
                        if (priestHealTarget != null)
                        {
                            int amount = PriestLogic.HealAmountFor(effect);
                            PlayerState target = priestHealTarget;

                            int before = target?.Health ?? 0;
                            target?.Heal(amount);

                            result.Healed = ReferenceEquals(target, actor)
                                ? actor.Health - before
                                : 0;   // 回给了别人，行动方自己没变
                            result.HealAmount = amount;
                            result.HealTarget = target;
                            break;
                        }

                        // ② "放弃攻击"：运算已经生效（手值已改），但**不造成伤害**。
                        //
                        //    为什么不能整回合跳过：跳过会让手值回退到运算前 ——
                        //    玩家实测报的 bug（1 + 3 = 4 放弃攻击后手值退回 1）。
                        //    运算本身是有效的，只是这一击不落实。
                        if (SuppressAttackDamage)
                        {
                            result.WasBlocked = false;
                            result.WasDodged = false;
                            result.DamageDealt = 0;
                            break;
                        }

                        // ③ 正常攻击
                        //
                        // ⚠️ 顺序很重要：**先让角色做"换射程"的副作用**（战士碎盾），
                        //    再检查距离 —— 否则"碎盾换无限距离"永远来不及生效。
                        //
                        // 只有结果是攻击时才需要这一步：非攻击不需要攻击距离。
                        (actor.Character as IRangeBreakEffect)?.ApplyRangeBreak(actor, actedHand, this);
                        //
                        // 攻击距离：只有"本次攻击在范围内"才落实伤害。
                        //   距离 1 的角色（战士等）在 2v2 里够不到环上对角的对手；
                        //   咒术师 / 牧师无限；战士碎盾 / 游侠强化可让本次无限。
                        //
                        // ⚠️ 距离不足时**保留运算结果**（手值照常改变）但不造成伤害 ——
                        //    与"放弃攻击"同一处理方式，避免手值回退（踩过的坑）。
                        bool inRange = CanReach(actor, opponent, RangeOverride);

                        if (!inRange)
                        {
                            result.WasBlocked = false;
                            result.WasDodged = false;
                            result.DamageDealt = 0;
                            break;
                        }

                        var ctx = new AttackContext(actor, opponent, actedHand, (int)effect);
                        ctx.SourceHandValueBefore = result.ActedHandValueBefore;
                        ctx.TargetHandValueBefore = result.TargetHandValue;
                        ctx.AttackerChoiceSink = GetChoiceProvider(actor) as IPreTurnChoiceSink;
                        ctx.AttackerProvider = GetChoiceProvider(actor);

                        // 透传"AI 已做的抉择"查询与结算中询问回调：
                        // 让角色优先采用 AI 的决定，而不是回读可能过期的 sink
                        ctx.AiDecisionLookup = AiDecisionLookup;
                        ctx.ChoiceGatherer = ChoiceGatherer;

                        // 邪术师的"是否取消"要回读玩家在收集阶段的回答 ——
                        // 与游侠闪避走的是同一条通路。
                        ctx.AnsweredLookupFor = AnsweredLookupFor;
                        ctx.AskedCancelLookup = AskedCancelLookup;

                        ctx.Resolve(oppChoice, this);
                        result.WasBlocked = ctx.WasBlocked;
                        result.WasDodged = ctx.WasDodged;
                        result.DodgeReason = ctx.DodgeReason;
                        result.WasCancelled = ctx.WasCancelled;
                        result.CancelledBy = ctx.CancelledBy;
                        result.HitCount = ctx.HitCount;
                        result.LandedHits = ctx.LandedHits;
                        result.BlockedHits = ctx.BlockedHits;
                        result.DodgedHits = ctx.DodgedHits;
                        result.DamageDealt = ctx.AppliedDamage;
                        break;
                    }

                case HandEffectType.Shield:
                    actor.Character?.OnShieldFormed(actor, actedHand, this);
                    break;

                case HandEffectType.Heal:
                    {
                        // 默认：回复行动方自己。
                        // 但角色可以"接管"这一步（例如牧师的 9 能回给对手），
                        // 接管时不再执行默认治疗，避免一次 9 回两次。
                        if (actor.Character is IHealOverride healer)
                        {
                            var sink = GetChoiceProvider(actor) as IPreTurnChoiceSink;
                            PlayerState target = healer.ApplyHeal(actor, this, sink);

                            // 回复量：角色可以自己声明（巨人 = 另一只手点数 × 2），
                            // 否则用牧师那套"按效果类型算"（9 回 9）。
                            //
                            // ⚠️ 必须让角色能接管这一处 —— 巨人的 9 是"两段回复"，
                            //    总量不是固定的 9（玩家规则），写死会让它的回复量永远错。
                            int amount = actor.Character is IHealAmountProvider amountProvider
                                ? amountProvider.HealAmountOf(actor, this)
                                : PriestLogic.HealAmountFor(effect);
                            int before = target?.Health ?? 0;
                            target?.Heal(amount);

                            result.Healed = ReferenceEquals(target, actor)
                                ? actor.Health - before
                                : 0;   // 回给了别人，行动方自己没变
                            result.HealAmount = amount;
                            result.HealTarget = target;
                        }
                        else
                        {
                            int before = actor.Health;
                            actor.Heal(Hand.HealAmount);
                            result.Healed = actor.Health - before;
                            result.HealAmount = Hand.HealAmount;
                            result.HealTarget = actor;
                        }
                        break;
                    }
            }

            // 3c. "运算后"钩子（狼人的双手和为 7 等）。
            //     放在效果分派之后、"胜负判定"之前，这样钩子造成的回血/成长能立刻算进判定。
            //     只允许读取玩家**在运算前**已经做好的选择，结算中不能再向 UI 索取输入。
            actor.Character?.OnOperationResolved(
                actor, actedHand, result.ActedHandValueBefore, otherHandValue, effect, this,
                GetChoiceProvider(actor) as IPreTurnChoiceSink);

            // 3d. 收下角色在结算中登记的"追加攻击"（剑客），供战报单独显示一行。
            result.ExtraAttack = PendingExtraAttack;
            PendingExtraAttack = null;

            // 4. 胜负判定
            CheckGameOver();
            result.GameOver = IsGameOver;
            result.Winner = Winner;

            // 5. 切换回合
            if (!IsGameOver)
            {
                TurnCount++;

                // ⚠️ 必须基于**实际出招的 actor** 轮转，不能用 CurrentPlayer。
                //
                // `ExecuteTurn(actor, ...)` 允许 actor 与 CurrentPlayer 不一致
                // （测试会显式指定某一方，UI 也可能这样调用）。
                // 用 CurrentPlayer 的话，第二次调用会把回合**转回**第一个人 ——
                // 表现为"双方交替错位一轮"（真实 bug，两条盾牌测试抓到）。
                CurrentPlayer = NextPlayerAfter(actor);

                // 5b. 回合数上限：打满 <see cref="TurnLimit"/> 回合仍未分出胜负 -> 平局。
                //     必须在切换回合之后判断（TurnCount 记的是"已完成的回合数"）。
                CheckTurnLimit();
                result.GameOver = IsGameOver;
                result.Winner = Winner;
            }

            return result;
        }

        /// <summary>打满回合上限仍未分胜负时，按平局结束对局。</summary>
        private void CheckTurnLimit()
        {
            if (IsGameOver) return;
            if (TurnCount < TurnLimit) return;

            IsGameOver = true;
            Winner = null;   // 平局
        }

        // ============================================================
        // 破盾（统一入口）
        // ============================================================

        /// <summary>
        /// 跳过当前回合：不执行任何运算，直接把回合交给对方。
        ///
        /// 规则：**只有当前行动方两只手都处于成型护盾状态时才允许跳过**，
        /// 由调用方（<see cref="Assistant.BoardController"/>）先用
        /// <see cref="CanSkipTurn"/> 判断，UI 再决定是否显示"跳过回合"按钮。
        /// 本方法自身也做校验，不满足条件时抛异常（不静默失败）。
        /// </summary>
        /// <exception cref="InvalidOperationException">当前不满足跳过条件时抛出。</exception>
        public void SkipTurn()
        {
            if (IsGameOver)
                throw new InvalidOperationException("非法跳过：比赛已结束。");

            // ⚠️ 必须用**实例版**：邪术师的跳过条件要看别人的体力，
            //    静态版传 null 会让它永远判非法（测试抓到：
            //    "非法跳过：当前不满足跳过回合的条件"）。
            if (!CanSkipTurnFor(CurrentPlayer))
                throw new InvalidOperationException(
                    "非法跳过：当前不满足跳过回合的条件。");

            PlayerState opponent = GetOpponent(CurrentPlayer);
            _brokenThisTurn.Clear();

            // 通知角色"跳过已发生"（刺客用它消耗掉潜行的那次跳过机会）
            (CurrentPlayer.Character as ITurnSkipRule)?.OnTurnSkipped(CurrentPlayer, this);

            TurnCount++;
            CurrentPlayer = NextPlayerAfter(CurrentPlayer);
            CheckTurnLimit();   // 跳过同样计入回合数上限
        }

        /// <summary>
        /// **已经校验过条件**的跳过（供"先做副作用、再结束回合"的技能使用）。
        ///
        /// 为什么需要它：邪术师的流程是
        /// "校验可发动 -> 改某人体力 -> 结束回合"，
        /// 而**改完体力之后条件就不成立了**（所有人血量被改成一致），
        /// 再走一次校验会抛"非法跳过"（测试抓到）。
        ///
        /// ⚠️ 调用方必须自己保证进入时条件成立。
        /// </summary>
        public void SkipTurnAlreadyValidated()
        {
            if (IsGameOver) return;

            _brokenThisTurn.Clear();
            (CurrentPlayer.Character as ITurnSkipRule)?.OnTurnSkipped(CurrentPlayer, this);

            TurnCount++;
            CurrentPlayer = NextPlayerAfter(CurrentPlayer);
            CheckTurnLimit();
        }
        ///
        /// 与 <see cref="SkipTurn"/> 的区别：后者是角色的技能（双盾 / 潜行），
        /// 有严格的前置条件；而"放弃攻击"是玩家在任何时候都可以做的选择
        /// （例如目标不可被攻击时）—— 所以这里**不校验**跳过条件。
        ///
        /// 规则确认：放弃攻击 = **算用掉本回合**。
        /// </summary>
        public void GiveUpTurn()
        {
            if (IsGameOver) return;

            PlayerState opponent = GetOpponent(CurrentPlayer);
            _brokenThisTurn.Clear();

            TurnCount++;
            CurrentPlayer = NextPlayerAfter(CurrentPlayer);

            CheckTurnLimit();
        }

        /// <summary>
        /// 该玩家当前是否可以跳过回合。
        ///
        /// 默认规则：两只手都是成型护盾（见 RULES.md §14）。
        /// 但角色可以自己接管（<see cref="ITurnSkipRule"/>），例如刺客的潜行。
        /// </summary>
        public static bool CanSkipTurn(PlayerState player)
        {
            if (player == null) return false;

            // 角色可以自己接管跳过规则（例如刺客的潜行）
            if (player.Character is ITurnSkipRule rule)
            {
                rule.SyncSkipState(player);
                return rule.CanSkip(player, null);
            }

            return player.HasDoubleShield;
        }

        /// <summary>
        /// 记录一次圣武士的**冲击**，供战报显示。
        ///
        /// 为什么必须写进战报：冲击是"合盾 / 碎盾的副产品"，
        /// 不占一次运算，所以它**不会**出现在 TurnResult 里。
        /// 光看血条无法区分"被盾挡住了"和"根本没触发"（玩家实测报告过疑惑）。
        ///
        /// ⚠️ 冲击现在**可以被盾牌挡下 / 被游侠闪避**（玩家要求的平衡性改动），
        ///    所以文案要反映实际结果，不能一律说"造成 N 点"。
        /// </summary>
        public void NoteShock(PlayerState source, PlayerState target,
                              string reason, int damage)
        {
            if (source == null || target == null) return;

            string result = damage > 0
                ? $"造成 {damage} 点伤害"
                : "被挡下（护盾或闪避）";

            _shockLog.Add($"{source.Name}（圣武士 {reason}）冲击 {target.Name}：{result}");
        }

        private readonly List<string> _shockLog = new List<string>();

        /// <summary>本回合发生的冲击记录（结算后清空）。</summary>
        public IReadOnlyList<string> ShockLog => _shockLog;

        /// <summary>取出并清空冲击记录（由战报在所有条目汇总完后调用）。</summary>
        public List<string> TakeShockLog()
        {
            var copy = new List<string>(_shockLog);
            _shockLog.Clear();
            return copy;
        }

        /// <summary>
        /// 该玩家当前是否可以跳过回合（**带 GameManager** 的版本）。
        ///
        /// ⚠️ 有些角色的跳过条件要**看别人**（邪术师：场上必须有人体力与自己不同），
        ///    没有 manager 就查不到 —— 所以调用方应优先用这个重载。
        ///    静态版保留给无法拿到 manager 的老调用点。
        /// </summary>
        public bool CanSkipTurnFor(PlayerState player)
        {
            if (player == null) return false;

            if (player.Character is ITurnSkipRule rule)
            {
                rule.SyncSkipState(player);
                return rule.CanSkip(player, this);
            }

            return player.HasDoubleShield;
        }

        /// <summary>
        /// 破除某玩家一面护盾，并通知其角色钩子（供战士连锁破盾等使用）。
        /// 使用 _brokenThisTurn 防止递归。
        /// </summary>
        public void BreakShieldAndNotify(PlayerState owner, Hand shield)
        {
            if (owner == null || shield == null) return;
            if (_brokenThisTurn.Contains(shield)) return;

            _brokenThisTurn.Add(shield);
            shield.ForceBreakShield();

            // ⚠️ 这条路径是"**被动被打碎**"（被攻击消耗、或被连锁破盾打碎）。
            //    它与"自己主动破盾"（`NotifyOwnShieldBroken`）**不是同一件事** ——
            //    圣武士要区分它们：两种都触发冲击，但语义不同、以后可能分叉。
            NotifyOwnShieldBroken(owner, shield);
            owner.Character?.OnOtherShieldBroken(owner, shield, this);
        }

        /// <summary>
        /// 通知角色"自己的盾碎了"。
        /// 注意：<see cref="BreakShieldAndNotify"/> 走的是"防递归"路径，
        /// 而运算前主动破盾走的是本方法（那面盾并未登记进 _brokenThisTurn，
        /// 因为它是由行动方自己消耗的，仍需触发角色钩子）。
        /// </summary>
        private void NotifyOwnShieldBroken(PlayerState owner, Hand shield)
        {
            owner.Character?.OnOwnShieldBroken(owner, shield, this);
        }

        // ============================================================
        // 胜负
        // ============================================================

        /// <summary>
        /// 本次结算是否**抑制攻击伤害**（"放弃攻击"用）。
        ///
        /// 与"跳过回合"完全不同：运算照常发生、手值照常改变，
        /// 只是这一击不落实到目标身上。调用方必须在结算后复位。
        /// </summary>
        public bool SuppressAttackDamage { get; set; }

        /// <summary>
        /// **本次结算的攻击目标玩家**（可覆盖"被加数的手主"）。
        ///
        /// 为什么需要：玩家确认的交互是
        ///   ① 点自己的手（运算源）② 点**任意人**的手（被加数）③ 单独选攻击目标
        /// 第 ③ 步的目标可以与第 ② 步的手主不同（例如用距离 2 的 C 的数字，
        /// 但打距离 1 的 B）。
        ///
        /// 为 null 时回退到"被加数的手主" —— 保持 1v1 与既有调用不变。
        /// 调用方必须在结算后复位。
        /// </summary>
        public PlayerState DamageTargetPlayer { get; set; }

        /// <summary>
        /// **本次攻击的距离覆盖**（-1 = 用角色自己的基础距离）。
        ///
        /// 由上层（BoardController）在收完"换射程"的抉择后设定 ——
        /// 目前两个来源：战士碎盾换射程、游侠强化射击。
        /// 调用方必须在结算后复位。
        /// </summary>
        public int RangeOverride { get; set; } = -1;

        /// <summary>这个效果类型是不是"攻击"。</summary>
        private static bool isAttackEffect(HandEffectType effect)
            => effect == HandEffectType.Attack4
               || effect == HandEffectType.Attack6
               || effect == HandEffectType.Attack8;

        private void CheckGameOver()
        {
            // 只要**还有两个以上队伍**活着，对局就继续。
            //
            // 用"存活队伍数"而不是"A/B 对偶" —— 这样 1v1（2 队）、
            // 2v2（2 队）、8 人混战（8 队）用同一套判定。
            var aliveTeams = new HashSet<int>();

            for (int i = 0; i < _players.Count; i++)
            {
                int team = TeamOf(_players[i]);
                if (team < 0) continue;

                if (_players[i].IsAlive) aliveTeams.Add(team);
            }

            // 队伍数（含已全灭的）—— 只有一支队伍时不必判负
            var allTeams = new HashSet<int>();
            for (int i = 0; i < _players.Count; i++)
            {
                int team = TeamOf(_players[i]);
                if (team >= 0) allTeams.Add(team);
            }

            if (aliveTeams.Count > 1) return;
            if (allTeams.Count <= 1) return;      // 只有一队时不算"分出胜负"

            IsGameOver = true;

            if (aliveTeams.Count == 0)
            {
                Winner = null;      // 全灭 -> 平局
                return;
            }

            // 赢家取"还活着的那一队里第一个还活着的人"（供战报显示）
            int winningTeam = 0;
            foreach (int t in aliveTeams) { winningTeam = t; break; }

            Winner = FirstAliveInTeam(winningTeam);
        }

        /// <summary>某队里第一个还活着的人；都阵亡则返回 null。</summary>
        private PlayerState FirstAliveInTeam(int team)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (TeamOf(_players[i]) != team) continue;
                if (_players[i].IsAlive) return _players[i];
            }

            return null;
        }

        /// <summary>赢下这一局的是哪一队（无赢家时为 -1）。</summary>
        public int WinningTeam => Winner == null ? -1 : TeamOf(Winner);
    }
}
