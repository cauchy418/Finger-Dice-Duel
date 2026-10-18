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

        /// <summary>本次攻击**实际挨打的人**（8 人局可能与被加数手主不同）。</summary>
        public PlayerState Defender;

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

        /// <summary>
        /// **本次攻击的伤害类型**（玩家要求："把所有攻击都标记上攻击类型"）。
        ///
        /// 由攻击方角色声明（<see cref="IAttackTypeProvider"/>），
        /// 界面用它给伤害飘字（含"格挡"）上色 —— 颜色与攻击类型图标同源。
        /// 非攻击回合保持 <see cref="AttackDamageType.None"/>。
        /// </summary>
        public AttackDamageType AttackType = AttackDamageType.None;

        /// <summary>本次攻击是否被闪避（游侠 / 刺客潜行）。</summary>
        public bool WasDodged;

        /// <summary>免伤原因（例如"目标在潜行中，无法被攻击"）。供战报显示。</summary>
        public string DodgeReason;

        /// <summary>
        /// 本次攻击是否被**妖术师取消**（玩家要求写进战报：
        /// "可以写被妖术师取消，造成 0 点伤害"）。
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

        // ---------- 法师的状态体系 ----------

        /// <summary>
        /// 被**冰冻**减半前的原始结果（0 = 本次没被减半）。
        ///
        /// 用途：战报要能说清"算出 9 被冰冻减半成 4"。
        /// </summary>
        public int FreezeHalvedFrom;

        /// <summary>
        /// 本次行动为下一次攻击**指定的伤害类型**（法师）。
        ///
        /// 非 <see cref="DamageType.None"/> 时表示"这一次放弃了攻击、只是选了类型"。
        /// </summary>
        public DamageType ChosenDamageType;

        /// <summary>本次攻击**挂到目标身上**的状态（没有则为 None）。</summary>
        public StatusEffect AppliedStatus;

        /// <summary>
        /// 本次伤害**是否命中**（用于决定要不要挂状态）。
        ///
        /// 玩家确认：被盾抵挡 / 太极减免**也算命中**；只有**闪避 / 取消**不算。
        /// </summary>
        public bool StatusHit;

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

        /// <summary>
        /// 这次运算之后猎人是否**进入了伏击**（暗置了一只手）。
        ///
        /// 供战报使用 —— 否则玩家会看到"（无效果）"，而实际上他进了伏击：
        /// 伏击**不改变效果类型**，它改的是那只手的**状态**。
        /// </summary>
        public bool EnteredAmbush;

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

        /// <summary>本次行动是击杀奖惩带来的额外行动（不推进轮次/总行动数）。</summary>
        public bool IsExtraAction;

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

        /// <summary>是否是 8 人阵营混战（只有这个模式启用击杀奖惩）。</summary>
        public bool IsEightPlayerFactionMode { get; private set; }
        public PlayerState Winner { get; private set; }
        public PlayerState CurrentPlayer { get; private set; }
        /// <summary>
        /// 每人的累计行动次数（**原义保留**，冷却与诊断仍在用）。
        /// </summary>
        public int TurnCount { get; private set; }

        /// <summary>
        /// **已完成的轮次数**。
        ///
        /// 玩家规则：一轮 = **所有角色都经过一次行动**。
        ///   · 1v1：从 1 号位行动，到再次回到 1 号位行动，就是新的一轮；
        ///   · 2v2：回到 1 号位行动 = 下一轮；
        ///   · 若 1 号位阵亡，则以"下一个还活着的座位"作为轮的起点。
        ///
        /// ⚠️ 为什么不复用 <see cref="TurnCount"/>：它的语义是"每人一次行动"，
        ///    1v1 里一轮 = 2 个回合、2v2 里一轮 = 4 个回合。
        ///    两者混用会让上限判断与界面显示都差几倍。
        /// </summary>
        public int RoundCount { get; private set; }

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
        /// **所有模式的轮次上限**（玩家确认：统一 50 轮）。
        ///
        /// ⚠️ 语义是**轮**：一轮 = 所有角色都经过一次行动。
        ///    所以 50 轮在 1v1 里等于 100 个回合、在 2v2 里等于 200 个回合。
        /// </summary>
        public const int DefaultRoundLimit = 50;

        /// <summary>本局的**轮次**上限（默认 <see cref="DefaultRoundLimit"/> = 50 轮）。</summary>
        public int RoundLimit { get; set; } = DefaultRoundLimit;

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
        /// <summary>
        /// **所有**不同队伍的存活者 —— **不做任何状态过滤**（残响 / 震慑也照列）。
        ///
        /// 用途：界面显示"场上有哪些敌人"、以及统计类逻辑。
        /// 要拿"**可以打**的目标"请用 <see cref="OpponentsOf"/>。
        /// </summary>
        public List<PlayerState> AllOpponentsOf(PlayerState player)
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

                // ⚠️ **法师状态带来的"打不到"**（残响 / 震慑）。
                //
                //    玩家确认的处理方式："若打出攻击数字且无合法目标就放弃攻击，
                //    和 1v1 面对刺客潜行类似" —— 也就是说这里过滤掉之后，
                //    界面上会变成"放弃攻击"，而不是整局卡住。
                //
                //    · 残响：只能打距离 1 的角色
                //    · 震慑：不能打"给自己挂震慑的那个法师"
                if (!StatusRules.CanAttackTarget(player, p, this)) continue;
                if (StatusRules.IsBlockedByStun(player, p)) continue;

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

        /// <summary>某队还活着的人数（8 人阵营混战 AI 用）。</summary>
        public int AliveTeamCount(int team)
        {
            int count = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (p != null && p.IsAlive && TeamOf(p) == team) count++;
            }

            return count;
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
            RoundCount = 0;

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
            int baseDistance = diff < wrap ? diff : wrap;

            // ⚠️ **邪术师的距离修正**：只影响"他与他命中过的那个人"这一对，
            //    其他玩家之间的距离完全不变（玩家明确要求）。
            return baseDistance + DistancePenaltyBetween(a, b);
        }

        // ============================================================
        // 距离修正层（邪术师）
        // ============================================================

        /// <summary>一条"成对距离修正"。</summary>
        private sealed class DistancePenalty
        {
            public PlayerState A;
            public PlayerState B;
            public int Amount;

            /// <summary>还剩几个"目标自己的回合"。</summary>
            public int TurnsLeft;
        }

        private readonly List<DistancePenalty> _distancePenalties = new List<DistancePenalty>();

        /// <summary>
        /// **邪术师**：令 <paramref name="owner"/> 与 <paramref name="target"/> 之间的距离 +<paramref name="amount"/>。
        ///
        /// 玩家确认的口径：
        ///   · 持续到**目标自己的 2 个回合**结束（与法师状态同一套计时）
        ///   · 再次命中**只刷新时长，不叠加**（+2 封顶）
        ///   · **不封顶**：8 人环距离最大 4，+2 之后就是 6
        /// </summary>
        public void ApplyDistancePenalty(PlayerState owner, PlayerState target, int amount)
        {
            if (owner == null || target == null) return;
            if (ReferenceEquals(owner, target)) return;
            if (amount <= 0) return;

            for (int i = 0; i < _distancePenalties.Count; i++)
            {
                DistancePenalty p = _distancePenalties[i];
                if (!SamePair(p, owner, target)) continue;

                // 刷新：**不叠加**
                p.Amount = amount;
                p.TurnsLeft = PlayerState.StatusDurationTurns;
                return;
            }

            _distancePenalties.Add(new DistancePenalty
            {
                A = owner,
                B = target,
                Amount = amount,
                TurnsLeft = PlayerState.StatusDurationTurns,
            });
        }

        /// <summary>这一对当前的距离修正（没有则为 0）。</summary>
        public int DistancePenaltyBetween(PlayerState a, PlayerState b)
        {
            if (a == null || b == null) return 0;
            for (int i = 0; i < _distancePenalties.Count; i++)
                if (SamePair(_distancePenalties[i], a, b)) return _distancePenalties[i].Amount;
            return 0;
        }

        private static bool SamePair(DistancePenalty p, PlayerState a, PlayerState b)
            => (ReferenceEquals(p.A, a) && ReferenceEquals(p.B, b))
               || (ReferenceEquals(p.A, b) && ReferenceEquals(p.B, a));

        /// <summary>
        /// 递减**属于 <paramref name="owner"/> 这个人的**修正（在他自己的回合结束时调）。
        /// 归零即移除。
        /// </summary>
        private void TickDistancePenalties(PlayerState owner)
        {
            if (owner == null) return;
            for (int i = _distancePenalties.Count - 1; i >= 0; i--)
            {
                DistancePenalty p = _distancePenalties[i];

                // 人已经死了 -> 直接清掉
                if (p.A == null || p.B == null || !p.A.IsAlive || !p.B.IsAlive)
                {
                    _distancePenalties.RemoveAt(i);
                    continue;
                }

                if (!ReferenceEquals(p.B, owner)) continue;      // 记在**目标**身上

                p.TurnsLeft--;
                if (p.TurnsLeft <= 0) _distancePenalties.RemoveAt(i);
            }
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
            => CharacterBehaviors.FirstImplementer<IAttackRangeProvider>(player) is IAttackRangeProvider p
                ? p.BaseAttackRange : 1;

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
        /// <summary>
        /// 一轮的**起点**：名单里第一个还活着的角色（1 号位）。
        ///
        /// 名单顺序就是座位顺序（1v1 是 [A, B]；2v2 由落座决定）。
        /// 若 1 号位阵亡，则顺延到下一个还活着的座位 —— 与玩家规则一致。
        /// </summary>
        public PlayerState RoundAnchor
        {
            get
            {
                for (int i = 0; i < _players.Count; i++)
                    if (_players[i] != null && _players[i].IsAlive) return _players[i];
                return null;
            }
        }

        /// <summary>
        /// 在**某人行动结束、回合已转给下一个人之后**调用，判断是否进入新的一轮。
        ///
        /// 判据：下一个人就是 <see cref="RoundAnchor"/>（第一个还活着的座位）。
        ///   · 1v1：A 是锚点 -> A 行动完下一个人是 B（不是锚点）；
        ///          B 行动完下一个人是 A（是锚点）-> 轮次 +1。✅
        ///   · 2v2：1 号位 -> 2 -> 3 -> 4 -> 回到 1 号位时 +1。✅
        ///   · 1 号位阵亡：锚点顺延到 2 号位。✅
        ///
        /// ⚠️ 必须用**实际出招的 actor** 来推下一个人，不能用 CurrentPlayer ——
        ///    <see cref="ExecuteTurn"/> 允许 actor 与 CurrentPlayer 不一致。
        /// </summary>
        private void AdvanceRoundAfter(PlayerState actor)
        {
            PlayerState next = NextPlayerAfter(actor);
            PlayerState anchor = RoundAnchor;
            if (next != null && ReferenceEquals(next, anchor)) RoundCount++;
        }

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

        /// <summary>
        /// **8 人阵营混战**：按座位 1..8 建局，队伍号由调用方给出
        /// （3 个阵营 = 3 个队伍；具体哪个角色属于哪个阵营由表现层/测试层决定）。
        ///
        /// ⚠️ Core 不认识"阵营"（那是 Characters 层的图鉴数据），
        ///    所以只接收抽象的队伍号；同一队的人可以互相用对方的手当被加数，
        ///    但不能互相攻击（`IsTeammate` 负责）。
        /// </summary>
        public static GameManager CreateEight(IReadOnlyList<PlayerState> seated,
                                              IReadOnlyList<IChoiceProvider> providers,
                                              IReadOnlyList<int> teamIds)
        {
            if (seated == null || seated.Count != 8)
                throw new ArgumentException("8 人局必须正好 8 名玩家（按座位 1..8 排列）。",
                                            nameof(seated));
            if (providers == null || providers.Count != 8)
                throw new ArgumentException("8 人局必须为 8 个座位各提供一个抉择器。",
                                            nameof(providers));
            if (teamIds == null || teamIds.Count != 8)
                throw new ArgumentException("8 人局必须为 8 个座位各提供一个队伍号。",
                                            nameof(teamIds));

            var game = new GameManager(seated[0], seated[1], providers[0], providers[1]);

            // 构造器默认把 A/B 登记成 0/1；8 人局要覆盖成调用方给的阵营号。
            game._team[seated[0]] = teamIds[0];
            game._team[seated[1]] = teamIds[1];

            game.PlayerC = seated[2]; game.ChoiceProviderC = providers[2];
            game.PlayerD = seated[3]; game.ChoiceProviderD = providers[3];
            game.PlayerE = seated[4]; game.ChoiceProviderE = providers[4];
            game.PlayerF = seated[5]; game.ChoiceProviderF = providers[5];
            game.PlayerG = seated[6]; game.ChoiceProviderG = providers[6];
            game.PlayerH = seated[7]; game.ChoiceProviderH = providers[7];

            for (int i = 2; i < 8; i++) game.Register(seated[i], teamIds[i]);

            for (int i = 2; i < 8; i++)
                seated[i].Character?.OnMatchReset(seated[i], game);

            game.NotifyMatchReset();
            game.IsEightPlayerFactionMode = true;
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
        /// （行动方 / 防守方），而**妖术师是场上的第三方** ——
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

            // ⚠️ **法师的负面状态必须一起清掉** —— 否则第二局会带着上一局的
            //    中毒 / 燃烧 / 震慑开局（与"野蛮人的锁手映射"同一类坑）。
            ClearAllStatuses();

            IsGameOver = false;
            Winner = null;
            TurnCount = 0;
            RoundCount = 0;
            CurrentPlayer = PlayerA;

            NotifyMatchReset();
        }

        /// <summary>清空**所有**参战玩家的负面状态（含 2v2 的另外两位）。</summary>
        public void ClearAllStatuses()
        {
            for (int i = 0; i < _players.Count; i++)
                _players[i]?.ClearStatuses();
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
        /// <summary>
        /// 枚举 <paramref name="actor"/> 当前所有合法运算。
        ///
        /// ⚠️ **返回的是副本，不是内部那个复用列表**。
        ///
        ///    原来这里 `return _legalMoves;` —— 而下一步任何一次
        ///    `GetLegalMoves` 都会 `_legalMoves.Clear()`，于是**前一个调用者
        ///    手里的引用会集体变成空 LegalMove**。
        ///    实测表现：2v2 循环赛里 `GreedyChoiceProvider.ScoreMove` 读到
        ///    `move.ActedHand == null`，并**崩溃成 AccessViolation**（原生层直接挂掉）。
        ///
        ///    这个隐患一直存在（"先拿列表、再问一次合法步"就会踩到），
        ///    只是加魔术师的自加选项**改变了列表长度与调用时机**，才让它稳定复现。
        ///    返回副本是最省心的修法：调用方拿到的列表不再与内部状态共享生命周期。
        /// </summary>
        public IReadOnlyList<LegalMove> GetLegalMoves(PlayerState actor)
            => new List<LegalMove>(BuildLegalMoves(actor));

        /// <summary>真正枚举合法步（共用内部复用列表以省分配）。</summary>
        private List<LegalMove> BuildLegalMoves(PlayerState actor)
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
            // 魔术师：**可以拿自己的另一只手当被加数**。
            bool selfAddend = (actor.Character as ISelfAddendProvider)?.AllowsSelfAddend == true;

            foreach (Hand mine in actor.Hands)
            {
                // ⚠️ **被锁与被暗置都不能当运算源**；
                //    而已成型盾 / 太极**可以**（拿它运算会主动破盾）。
                if (!mine.CanBeOperated) continue;

                // 魔术师：先把**自己的另一只手**加进来。
                // 玩家确认：两只手任选一个方向，变的是**先点的那只手**。
                if (selfAddend)
                {
                    foreach (Hand own in actor.Hands)
                    {
                        if (ReferenceEquals(own, mine)) continue;    // 一只手不能加自己
                        if (own.IsConcealed) continue;               // 暗置手不能当被加数

                        // ⚠️ **目盲**（法师的光耀）：只能选**距离 1** 的手当被加数。
                        //    自己的手距离是 0，所以自加**不受目盲限制**。
                        if (!StatusRules.CanUseHandAsAddend(actor, own, this)) continue;

                        _legalMoves.Add(new LegalMove(actor, mine, own));
                    }
                }

                for (int i = 0; i < _players.Count; i++)
                {
                    PlayerState owner = _players[i];
                    if (ReferenceEquals(owner, actor)) continue;

                    // ⚠️ **阵亡玩家不能再当目标**！
                    //
                    //    我第一版漏了这个判断，后果很严重：
                    //    死人的手也会进合法移动列表，于是 AI 会拿它当目标、
                    //    **打一个没有意义的空位**，局面永远推不动。
                    //    （2v2 实测：只剩两个敌对座位时，P3 的 12 步里有 8 步是打死人，
                    //     于是 400 次行动后双方仍然满血。）
                    if (!owner.IsAlive) continue;

                    // ⚠️ **目盲**：只能选距离 1 的手当被加数（这里是别的玩家）。
                    if (!StatusRules.CanUseHandAsAddend(actor, owner.LeftHand, this)
                        && !StatusRules.CanUseHandAsAddend(actor, owner.RightHand, this))
                        continue;

                    foreach (Hand theirs in owner.Hands)
                    {
                        // 玩家确认：暗置的手**不能被当成运算数字**。
                        if (theirs.IsConcealed) continue;

                        if (!StatusRules.CanUseHandAsAddend(actor, theirs, this)) continue;

                        _legalMoves.Add(new LegalMove(actor, mine, theirs));
                    }
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

            // ⚠️ **一只手不能加自己**。
            //
            //    这条以前是**隐含**成立的：加数与被加数必须属于不同玩家，
            //    而一个人只有两只手，所以"同一只手"根本不可能出现。
            //    魔术师放开"可以拿自己的手当被加数"之后，这个隐含前提没了 ——
            //    必须显式挡住（玩家原话："重复点一只手不会生效"）。
            if (ReferenceEquals(actedHand, targetHand))
            {
                reason = "加数与被加数必须是不同的两只手。";
                return false;
            }

            // ⚠️ **法师的状态限制（目盲 / 残响 / 震慑）**。
            //
            //    规则：被加数的手必须在合法范围内（目盲 = 距离 1）；
            //    而"打谁"由**结果效果**决定 —— 若结果是攻击，
            //    挨打的人是当前默认攻击目标，必须通过残响与震慑的检查。
            //
            //    玩家确认的做法："若打出攻击数字且无合法目标就放弃攻击"
            //    —— 与 1v1 面对刺客潜行时同一处理。所以这里不是拒绝运算，
            //    而是**拒绝把它当成攻击**；实际放行由 ResolveTurn 决定。
            if (!StatusRules.CanUseHandAsAddend(actor, targetHand, this))
            {
                reason = "目盲：只能选择与自己距离 1 的手。";
                return false;
            }

            if (ReferenceEquals(addendOwner, actor))
            {
                // ⚠️ 魔术师**例外**：玩家确认他可以拿自己的手当被加数
                //    （左手 3 碰自己的右手 5 凑出 8）。
                //    但仍然**不能一只手加自己**（加数与被加数必须是不同的两只手）——
                //    那条由下面的一般检查挡住。
                bool selfAddend = (actor.Character as ISelfAddendProvider)?.AllowsSelfAddend == true;
                if (!selfAddend)
                {
                    reason = "被加数不能是自己的手（要用别人的数字来改变自己的手）。";
                    return false;
                }
            }

            // ⚠️ **暗置的手不能被当成运算数字**（玩家确认：对手不能碰这只手）。
            //
            //    我第一版只在 `GetLegalMoves` 里过滤了它 —— 于是 AI 挑不到，
            //    但 `IsLegalMove` 仍会放行（测试抓到：校验层期望 false、实际 true）。
            //    两层必须一致：枚举入口过滤是为了性能与 AI，
            //    校验层是**规则本身**，缺了它任何直接调用都能绕过规则。
            if (targetHand.IsConcealed)
            {
                reason = "被暗置的手不能被当成运算数字。";
                return false;
            }

            // ⚠️ **这里故意不拒绝"目标已阵亡"。**
            //
            //    "不能打死人"由 <see cref="GetLegalMoves"/> 过滤掉（那才是 AI 挑招的入口），
            //    而 AI 的平局问题正是那里修好的。
            //
            //    我曾在这里加了一条同样的拒绝，结果 **5 条既有测试变红**：
            //    它们会在"对局已结束 / 双方同时归零"时补一次结算，
            //    期望的是**静默忽略**（判平局）而不是抛异常。
            //    两者语义冲突，而"过滤合法移动"已经足够解决问题，所以不在这里重复拒绝。

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
        public TurnResult ExecuteTurn(PlayerState actor, Hand actedHand, Hand targetHand,
                                      bool extraAction = false)
        {
            if (!IsLegalMove(actor, actedHand, targetHand, out string reason))
                throw new InvalidOperationException($"非法运算：{reason}");

            MarkSelfAddendIfAny(actor, targetHand);
            return ResolveTurn(actor, actedHand, targetHand.Value, targetHand,
                               extraAction: extraAction);
        }

        /// <summary>
        /// **法师：铺伤害类型的统一入口（界面与无头两条路都走它）。**
        ///
        /// 界面路径由 <c>BoardController</c> 在"收集完抉择"之后调用
        /// <c>WizardLogic.ReadFromSink</c>；而无头路径（<c>MatchRunner</c> / 测试）
        /// 直接驱动 GameManager，**从不收集抉择** ——
        /// 于是 AI 法师的"选伤害类型"永远落不到自己身上，
        /// 表现为"AI 法师从不挂状态"（与之前魔术师计数那次同一类坑）。
        ///
        /// 这里兜底：若法师还没回读过选择，就从它的抉择器**当场问一次**。
        /// 已经回读过的不再问（避免覆盖界面玩家的明确选择）。
        /// </summary>
        private void ResolveMageStatusIntent(PlayerState actor, Hand actedHand,
                                               Hand targetHand)
        {
            if (actor?.Character == null) return;

            var mage = actor.Character as IMageState;
            if (mage == null) return;

            // ⚠️⚠️ **"放弃攻击"的伤害抑制必须在所有提前 return 之前判。**
            //
            //    玩家实测报的 bug："放弃攻击仍然造成了伤害，且是不用选择就造成了伤害"。
            //    根因就是下面那句 `if (mage.HasPendingType) return;`：
            //    **法师身上已经有待用类型**时（前几轮铺下的）直接返回，
            //    于是后面那句 `if (mage.WantsToAbandonThisTurn) Suppress...`
            //    **永远执行不到** —— 抑制开关恒为 false，伤害照打。
            //
            //    抑制与"要不要问类型"是**两件事**：前者只取决于标记，
            //    所以必须放在最前面、不受任何提前返回影响。
            //
            //    ⚠️⚠️ 这个标记**不能在这里清零**：
            //       界面路径是"玩家点完之后写标记 -> 结算时读"，
            //       在这里清零会把玩家刚做的选择抹掉。
            //       清零由**回合开始**负责（界面在 `BoardController.SetupFirstStep`、
            //       无头在 `MatchRunner`），这样"每回合重置一次"与"玩家选择"不冲突。
            if (mage.WantsToAbandonThisTurn) SuppressAttackDamage = true;

            // 已经有待用类型 -> 不用再问
            if (mage.HasPendingType) return;

            if (!(GetChoiceProvider(actor) is IDamageTypeChoiceSink sink)) return;

            // 让 AI 就"这一次是不是放弃攻击"表态
            sink.WantsToAbandonAttack = false;
            sink.ChosenDamageType = DamageType.None;

            // ⚠️⚠️ **把当前对局注入抉择器** —— 这是"AI 法师从不在无头对局里
            //    铺状态"的**根因**：
            //    `GreedyChoiceProvider.Game` 原本只在 `BoardController` 里被赋值
            //    （`if (provider is GreedyChoiceProvider g) g.Game = _game;`），
            //    而 `MatchRunner` 与各测试 harness **从不设置它**；
            //    于是 AI 的法师分支第一句 `if (Game == null) return null;`
            //    直接放弃，永远不问、永远不铺。
            //
            //    放在这个调用点：界面 / 无头 / 测试三条路径都会经过这里。
            sink.SetGameForChoices(this);

            // ⚠️ 用 **IChoiceProvider 接口**而不是 AbstractChoiceProvider ——
            //    后者在 Assistant 层，Core 看不到（分层铁律）。
            IChoiceProvider provider = GetChoiceProvider(actor);
            if (provider == null) return;

            // 问一次"要不要放弃攻击"（AI 会在这里设 WantsToAbandonAttack）
            var context = new PreTurnChoiceContext(actor, actor, actedHand,
                                                   targetHand ?? actor.RightHand)
            {
                Manager = this,
                AlivePlayerCount = AlivePlayerCount,

                // ⚠️ **必须显式填结果值** —— 界面路径由 BoardController 填，
                //    而无头路径没人填。`WizardLogic.CollectPreTurnChoices`
                //    正是用 `ResultValue` 判断"结果是不是 4/6/8"，
                //    不填就永远问不出东西（AI 法师从不挂状态 —— 测试抓到过）。
                //    此刻发起运算的那只手**已经是结果值**
                //    （PerformOperation 已执行、效果尚未分派）。
                ResultValue = actedHand?.Value ?? 0,
            };

            var requests = new List<ChoiceRequest>();
            (actor.Character as IPreTurnChoiceProvider)
                ?.CollectPreTurnChoices(context, requests);

            // (临时诊断已移除)

            for (int i = 0; i < requests.Count; i++)
            {
                // ⚠️⚠️ **先问"要不要放弃攻击"，再决定要不要问"选哪种类型"。**
                //
                //    原来第二步的条件是 `sink.WantsToAbandonAttack` ——
                //    但那个属性**只有在 `ChooseHandFor(WizardAbandonAttack)`
                //    被调用之后才会被设置**（AI 与界面都是在那一刻写它的），
                //    而循环的第一轮**正是**在问"要不要放弃"。
                //
                //    于是时序变成"先读答案、再问问题"：
                //    第一步真的执行了、`WantsToAbandonAttack` 也置真了，
                //    但请求列表里没有第二轮，`WizardDamageType` **从来没被问过** ——
                //    表现就是"AI 问了 2000 多次要不要放弃，却一次类型都没选上"。
                //
                //    修法：按**请求的顺序**推进，每一步之后再让后续步骤的判断生效。
                if (requests[i].Effect == HandEffect.WizardAbandonAttack)
                {
                    provider.ChooseHandFor(requests[i], actor);
                }
                else if (requests[i].Effect == HandEffect.WizardDamageType)
                {
                    // 到这里"要不要放弃"已经有答案了（它在 requests 里排在前面）
                    if (sink.WantsToAbandonAttack
                        || (actor.Character as IMageState)?.WantsToAbandonThisTurn == true)
                    {
                        provider.ChooseHandFor(requests[i], actor);
                    }
                }
            }

            

            // ⚠️ **统一把选择落到法师身上**（界面路径也会调同一个方法，
            //    但那是"收集完就落"，这里是"结算时才落" —— 两条路径都幂等）。
            //
            //    漏了这一步的表现：`WantsToAbandonAttack` 已经置真、
            //    但法师自己身上没有类型，于是命中时 `StatusToApplyOnHit`
            //    返回 None，状态挂不上（AI 法师铺了等于没铺）。
            mage.ReadFromSink(sink);

            // ⚠️⚠️ **"放弃攻击"的伤害抑制开关必须在这里设。**
            //
            //    不能放在 `BoardController.ResolveTurn` 的入口：
            //    那里读 `WantsToAbandonThisTurn` 时，结果还没算出来、
            //    `ResolveMageStatusIntent` 还没跑 —— 标记必然是 false，
            //    于是 suppress 永远不成立（实测 24961 次法师回合里一次都没有）。
            //
            //    放在这里（结果已算完、效果尚未分派）时序才对。
            if (mage.WantsToAbandonThisTurn) SuppressAttackDamage = true;
        }

        /// <summary>
        /// 若这次是"拿自己的手当被加数"（魔术师的特色），把被加数那只手标给角色，
        /// **仅供技能发动率统计**。
        ///
        /// \u26a0\ufe0f 规则的判定**不依赖**这个标记 —— 那一条一律从手牌归属直接推出
        ///    （<see cref="CharacterBehaviors.ActorHasSelfAddend"/>）。
        ///    这里只解决"统计要一个累计次数"的问题，
        ///    而且刻意只标记、不参与任何规则分支，避免两处口径不一致。
        /// </summary>
        private static void MarkSelfAddendIfAny(PlayerState actor, Hand targetHand)
        {
            if (actor == null || targetHand == null) return;

            var magician = CharacterBehaviors.FirstImplementer<ISelfAddendProvider>(actor)
                as MagicianLogic;
            if (magician == null) return;

            magician.MarkSelfAddendHand(actor.Owns(targetHand) ? targetHand : null);
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
                AdvanceRoundAfter(actor);
                CheckTurnLimit();
            }

            return result;
        }

        /// <summary>
        /// 最近一次结算的 <see cref="TurnResult"/>（供"偷出的攻击"分两步时续写）。
        /// </summary>
        public TurnResult LastTurnResult { get; private set; }

        // ============================================================
        // 法师的状态：回合开始 / 回合结束结算
        // ============================================================

        /// <summary>
        /// **轮到某人行动之前**的状态结算（中毒 / 感电）。
        ///
        /// · 中毒：2 点毒素伤害，**不可被抵挡**（玩家确认：可能直接把他打死、跳过其行动）
        /// · 感电：1 点闪电伤害，自己 + **环上相邻**的玩家各一次，
        ///         可被盾抵挡 / 游侠闪避 / 太极减免
        ///
        /// ⚠️ 这两种伤害**不会再挂回状态**（玩家明确要求），否则会变成无法摆脱的循环。
        ///
        /// ⚠️ 调用时机由上层（<see cref="Assistant.BoardController"/> /
        ///    <see cref="Assistant.MatchRunner"/>）在"把行动权交给某人之前"触发 ——
        ///    因为只有它们知道"这一位要开始行动了"。
        /// </summary>
        public void ResolveStatusTurnStart(PlayerState actor)
        {
            if (actor == null || !actor.IsAlive) return;

            // ---- 中毒：不可被抵挡 ----
            int poison = StatusRules.TurnStartDamage(actor, out bool unstoppable);
            if (poison > 0 && unstoppable)
            {
                actor.TakeDamage(poison);
                LastStatusNote = $"{actor.Name} 中毒，受到 {poison} 点毒素伤害（不可抵挡）";
                CheckGameOver();
                if (IsGameOver) { SkipIfStatusKilled(actor); return; }
            }

            // ---- 感电：自己 + 相邻 ----
            List<PlayerState> shocked = StatusRules.ShockVictims(actor, this);
            for (int i = 0; i < shocked.Count; i++)
            {
                PlayerState victim = shocked[i];
                if (victim == null || !victim.IsAlive) continue;

                // 走普通伤害流程（可被盾 / 太极抵挡；游侠若满足条件也能闪避）
                var ctx = new AttackContext(actor, victim, actor.LeftHand,
                                            StatusRules.ShockDamagePerTurn)
                {
                    IgnoreStatBonuses = true,      // 闪电伤害是固定 1 点
                };
                ctx.Resolve(GetChoiceProvider(victim), this);

                if (string.IsNullOrEmpty(LastStatusNote))
                    LastStatusNote = $"{victim.Name} 感电，受到 {StatusRules.ShockDamagePerTurn} 点闪电伤害";

                CheckGameOver();
                if (IsGameOver) { SkipIfStatusKilled(actor); return; }
            }

            SkipIfStatusKilled(actor);
        }

        /// <summary>
        /// 回合开始的状态伤害**把当前行动者打死了** -> 跳过他的行动，把回合交给下一位。
        ///
        /// 玩家确认的规则："中毒……且扣血可能直接把他打死（跳过其行动）"。
        ///
        /// ⚠️ 必须用 <see cref="NextPlayerAfter"/>（它只在活人里找），
        ///    否则会把回合交给一个死人、对局再次卡住。
        /// </summary>
        private void SkipIfStatusKilled(PlayerState actor)
        {
            if (actor == null || IsGameOver) return;
            if (actor.IsAlive) return;
            if (!ReferenceEquals(CurrentPlayer, actor)) return;

            PlayerState next = NextPlayerAfter(actor);
            if (next == null) return;      // 所有人都没了 -> 交给 CheckGameOver

            CurrentPlayer = next;
            TurnCount++;
            AdvanceRoundAfter(actor);
            CheckTurnLimit();
        }

        /// <summary>
        /// **某人的回合结束之后**的状态结算（燃烧）与**状态计数递减**。
        ///
        /// · 燃烧：3 点火焰伤害，可被盾抵挡 / 游侠闪避 / 太极减免
        /// · 递减：**目标自己的回合**结束时把所有状态计数 −1，归零即移除
        ///
        /// ⚠️ 递减放在这里（"他自己的回合结束"）而不是"每个回合都减" ——
        ///    玩家确认的持续时间是**目标自己的 2 个回合**，与别人的回合无关。
        /// </summary>
        public void ResolveStatusTurnEnd(PlayerState actor)
        {
            if (actor == null) return;

            // ---- 燃烧 ----
            if (actor.IsAlive)
            {
                int burn = StatusRules.TurnEndDamage(actor);
                if (burn > 0)
                {
                    var ctx = new AttackContext(actor, actor, actor.LeftHand, burn)
                    {
                        IgnoreStatBonuses = true,      // 固定 3 点
                    };
                    ctx.Resolve(GetChoiceProvider(actor), this);

                    LastStatusNote = $"{actor.Name} 燃烧，受到 {burn} 点火焰伤害";
                    CheckGameOver();
                }
            }

            // ---- 状态计数递减（归零即移除）----
            actor.TickStatuses();

            // ---- 距离修正递减（邪术师）：记在**目标**身上，用的也是他自己的回合 ----
            TickDistancePenalties(actor);
        }

        /// <summary>最近一次状态结算的说明（供战报 / 测试；空字符串 = 本次没有）。</summary>
        public string LastStatusNote { get; private set; } = string.Empty;

        /// <summary>
        /// **"放弃攻击"时也要挂状态。**
        ///
        /// 玩家确认：法师放弃攻击是为了给**下一次**攻击铺类型，这一击本就该白扔。
        /// 但"放弃"≠"没命中" —— 那个类型是给**后续**命中用的，
        /// 而这一次的命中同样应该正常消耗类型并挂状态。
        ///
        /// ⚠️ 为什么单独一个方法：`SuppressAttackDamage` 在效果分派处**直接 break**，
        ///    根本不创建 <see cref="AttackContext"/>，而"命中即挂状态"是挂在
        ///    `AttackContext.Resolve` 末尾的 —— 于是"放弃的那一击"永远挂不上状态。
        /// </summary>
        private void ApplyMageStatusOnSuppressedAttack(PlayerState actor, PlayerState victim,
                                                        TurnResult result)
        {
            // ⚠️⚠️ **被放弃的这一击什么都不做。**
            //
            //    玩家确认的机制是"放弃这次攻击，为**下一次**攻击指定伤害类型" ——
            //    所以放弃的这一击：
            //      · 不造成伤害（已由 SuppressAttackDamage 保证）；
            //      · **不消耗类型**（否则类型在这一回合就被吃掉，
            //        永远轮不到"下一次攻击"）；
            //      · **不挂状态**（状态是给下一次命中挂的）。
            //
            //    我第一版在这里 `ConsumeDamageType()` 并挂状态 ——
            //    等于"放弃 = 白扔 + 状态挂在被放弃的这一击上"，
            //    与"类型留给下一次"完全相反（界面流程测试抓到）。
            //
            //    所以这里**直接返回**；保留方法是为了留下这个结论，
            //    并作为"将来若要给放弃的一击加别的副作用"的挂点。
            if (SuppressAttackDamage) return;
            if (actor?.Character is not IStatusApplyingAttack applier) return;

            var probe = new AttackContext(actor, victim, actor.LeftHand,
                                          result?.ResultValue ?? 0);
            StatusEffect status = applier.StatusToApplyOnHit(probe);

            if (actor.Character is Characters.SpecialLogics.WizardLogic mage)
                mage.ConsumeDamageType();

            if (status == StatusEffect.None) return;
            if (victim == null || !victim.IsAlive) return;

            victim.ApplyStatus(status, PlayerState.StatusDurationTurns, actor);
            result.AppliedStatus = status;
            result.StatusHit = true;
        }

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
                                                int roll, Hand targetHand = null,
                                                bool extraAction = false)
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
                               gambleResult: roll, extraAction: extraAction);
        }

        /// <summary>已通过校验的实际结算流程。</summary>
        /// <summary>
        /// **重入守卫**：`ResolveTurn` 正常最多嵌套几层（多段 / 太极 / 追加攻击），
        /// 但一旦失控就会变成无限递归 —— 而无限递归在原生层表现为
        /// `AccessViolationException`（"内存访问违规"），堆栈还被截断，
        /// 极难定位。加这个计数把它变成一条**可读的异常**。
        ///
        /// 阈值取 12：远高于任何正常嵌套，又远低于会把栈打爆的量。
        /// </summary>
        private int _resolveDepth;

        private const int MaxResolveDepth = 12;

        private TurnResult ResolveTurn(PlayerState actor, Hand actedHand, int targetValue,
                                       Hand targetHand, int gambleResult = 0,
                                       bool extraAction = false)
        {
            _resolveDepth++;
            if (_resolveDepth > MaxResolveDepth)
            {
                _resolveDepth = 0;
                throw new InvalidOperationException(
                    "结算重入过深（" + MaxResolveDepth + " 层）：疑似无限递归。" +
                    " 角色=" + (actor?.Definition?.DisplayName ?? "?") +
                    " 手=" + (actedHand?.Value.ToString() ?? "?") +
                    " 目标值=" + targetValue +
                    " 赌博=" + gambleResult);
            }

            try
            {
                return ResolveTurnCore(actor, actedHand, targetValue, targetHand,
                                       gambleResult, extraAction);
            }
            finally
            {
                _resolveDepth--;
            }
        }

        private TurnResult ResolveTurnCore(PlayerState actor, Hand actedHand, int targetValue,
                                           Hand targetHand, int gambleResult, bool extraAction)
        {
            var result = new TurnResult
            {
                Actor = actor,
                TargetHand = targetHand,
                IsExtraAction = extraAction,
            };
            _brokenThisTurn.Clear();

            // ⚠️ "本回合放弃攻击"的标记在 `ResolveMageStatusIntent` 里清零 ——
            //    见那里的详解（无头路径原来从不清零，法师第一次放弃后
            //    标记永久为 true：伤害永久被抑制、类型永远被追问）。

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
            result.Defender = opponent;

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
            if (CharacterBehaviors.FirstImplementer<IHandEffectRewriter>(actor) is IHandEffectRewriter rewriter)
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

            // 2c. **冰冻**（法师的寒冷）：运算得到的结果**减半（向下取整）**。
            //
            //     玩家原话："运算后得到的结果减半（向下取整）。如算出 9 变成 4，
            //     算出 5 变成 2。"
            //
            //     ⚠️ 必须放在**改写之后、效果分派之前**：
            //       · 放太早会被 RewriteEffect 用旧值覆盖；
            //       · 放太晚（效果分派后）就已经先按 9 回过血了。
            //     归一化后的 9 是"回复"，减半成 4 就是"攻击" —— 效果类型要跟着重算。
            if (actor != null && actor.HasStatus(StatusEffect.Freeze))
            {
                int before = actedHand.Value;
                int halved = StatusRules.ApplyResultModifiers(actor, before);

                if (halved != before)
                {
                    actedHand.SetValue(halved);
                    effect = Hand.GetEffectType(halved);

                    // 手牌状态跟随新效果（与 PerformOperation 同一约定）
                    actedHand.SetShieldState(effect == HandEffectType.Shield
                        ? ShieldState.Formed
                        : ShieldState.None);

                    result.FreezeHalvedFrom = before;
                }
            }

            // 2d. **法师：运算出攻击数字时，问"要不要放弃攻击 / 选哪种伤害类型"。**
            //
            //     ⚠️ 必须放在**这里**（结果已经算出来）：
            //        `WizardLogic.CollectPreTurnChoices` 用 `context.ResultValue`
            //        判断"结果是不是 4/6/8"，而在 ExecuteTurn 入口处结果还没算出来。
            //        我第一版放在入口，于是**永远问不出东西**、
            //        AI 法师从不挂状态（新测试立刻抓到）。
            //
            //     与冰冻同一位置，也都在"效果分派之前" —— 因为这一步会决定
            //     本次要不要真的打伤害（放弃 = 不造成伤害）。
            ResolveMageStatusIntent(actor, actedHand, targetHand);

            // ⚠️⚠️ **被放弃的那一击不能吃掉"为下一次准备"的伤害类型。**
            //
            //    玩家确认的机制是"放弃这次攻击，为**下一次**攻击指定伤害类型"。
            //    但这一回合的结算会在攻击分派处走到
            //    `AttackContext.ApplyAttackerStatus`（它无条件
            //    `ConsumeDamageType()`，因为玩家确认"没命中就作废"）——
            //    于是**刚选好的类型会被自己的放弃动作清掉**，
            //    永远轮不到下一次攻击（界面流程测试抓到）。
            //
            //    修法：放弃的那一回合**先把类型取下来**，
            //    结算完再放回去（类型本来就是跨回合的，这样做语义一致）。
            DamageType unspent = DamageType.None;

            if (actor?.Character is Characters.SpecialLogics.WizardLogic mageForStash
                && mageForStash.WantsToAbandonThisTurn)
            {
                unspent = mageForStash.ConsumeDamageType();
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

                            // ⚠️⚠️ **但"放弃攻击"不等于"没命中"。**
                            //
                            //    玩家确认：法师放弃攻击是为了**给下一次攻击铺类型**，
                            //    这一次本来就该白扔。所以这里的语义是
                            //    "命中但不造成伤害" —— 而火焰/毒素等状态挂在
                            //    **下一次**命中的攻击上。
                            //
                            //    ⚠️ 我原来直接 `break`，于是**根本没有创建 AttackContext**，
                            //       而"命中即挂状态"是挂在 `AttackContext.Resolve` 末尾的 ——
                            //       结果就是"AI 法师铺了类型却永远挂不上状态"。
                            //       这里补一次 `ApplyAttackerStatus`，让放弃的那一击
                            //       也能正常消耗类型并挂状态（与普通命中同一套判定）。
                            
                            ApplyMageStatusOnSuppressedAttack(actor, opponent, result);
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

                        // 妖术师的"是否取消"要回读玩家在收集阶段的回答 ——
                        // 与游侠闪避走的是同一条通路。
                        ctx.AnsweredLookupFor = AnsweredLookupFor;
                        ctx.AskedCancelLookup = AskedCancelLookup;

                        // ⚠️ **把本次攻击的距离交给攻击方角色** ——
                        //    牧师 / 妖术师 / 法师按"距离 1 内 / 外"分型
                        //    （玩家规则），而 AttackContext 刻意不持有对局引用。
                        if (actor.Character is CharacterTemplate template)
                            template.LastAttackDistance = DistanceBetween(actor, opponent);

                        ctx.Resolve(oppChoice, this);
                        result.AttackType = ctx.AttackType;
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
                        if (CharacterBehaviors.FirstImplementer<IHealOverride>(actor) is IHealOverride healer)
                        {
                            var sink = GetChoiceProvider(actor) as IPreTurnChoiceSink;
                            PlayerState target = healer.ApplyHeal(actor, this, sink);

                            // 回复量：角色可以自己声明（巨人 = 另一只手点数 × 2），
                            // 否则用牧师那套"按效果类型算"（9 回 9）。
                            //
                            // ⚠️ 必须让角色能接管这一处 —— 巨人的 9 是"两段回复"，
                            //    总量不是固定的 9（玩家规则），写死会让它的回复量永远错。
                            int amount = CharacterBehaviors.FirstImplementer<IHealAmountProvider>(actor) is IHealAmountProvider amountProvider
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
            //    额外行动（击杀奖惩）**不推进轮次，也不增加总行动数**。
            if (!IsGameOver && !extraAction)
            {
                TurnCount++;

                // ⚠️ 必须基于**实际出招的 actor** 轮转，不能用 CurrentPlayer。
                //
                // `ExecuteTurn(actor, ...)` 允许 actor 与 CurrentPlayer 不一致
                // （测试会显式指定某一方，UI 也可能这样调用）。
                // 用 CurrentPlayer 的话，第二次调用会把回合**转回**第一个人 ——
                // 表现为"双方交替错位一轮"（真实 bug，两条盾牌测试抓到）。
                CurrentPlayer = NextPlayerAfter(actor);

                // 5a. **轮次推进**：下一个人是"轮的起点"就算进入新的一轮。
                AdvanceRoundAfter(actor);

                // 5b. **轮次上限**：打满 <see cref="RoundLimit"/> 轮仍未分出胜负 -> 平局。
                //     必须在切换回合之后判断。
                CheckTurnLimit();
                result.GameOver = IsGameOver;
                result.Winner = Winner;
            }

            // ⚠️ **把"放弃攻击"暂存的伤害类型放回去**（见上面取下的地方）。
            //
            //    玩家确认："放弃这次攻击，为下一次攻击指定伤害类型" ——
            //    所以类型必须跨过这次结算、留在法师身上等下一次攻击。
            if (unspent != DamageType.None
                && actor?.Character is Characters.SpecialLogics.WizardLogic mageRestore)
            {
                mageRestore.SetPendingDamageType(unspent);
            }

            return result;
        }

        /// <summary>
        /// 打满**轮次上限**（<see cref="RoundLimit"/> = 50 轮）仍未分出胜负时结算：
        ///
        ///   · **1v1**：血量更多的一方获胜；血量相同才平局。
        ///   · **2v2**：**一队血量相加**更多的一方获胜；两队总和相同才平局。
        ///
        /// ⚠️ 玩家要求：所有模式的上限都按**轮**算，统一 50 轮。
        ///    一轮 = 所有角色都经过一次行动，所以 50 轮在 1v1 里等于 100 个回合、
        ///    在 2v2 里等于 200 个回合。
        ///
        /// 方法名保留 <c>CheckTurnLimit</c> 是为了不动既有调用点；
        /// 判定用的已经是 <see cref="RoundCount"/> / <see cref="RoundLimit"/>。
        /// </summary>
        private void CheckTurnLimit()
        {
            if (IsGameOver) return;
            if (RoundCount < RoundLimit) return;

            IsGameOver = true;
            Winner = DecideWinnerByHealth();
        }

        /// <summary>
        /// 轮次上限后的**血量判定**：返回获胜队伍的代表（同队血量最高的存活者），
        /// 血量相同 / 无法判定时返回 null（平局）。
        ///
        /// 2v2 / 8 人阵营混战用**队伍总血量**（含已阵亡成员的 0）；
        /// 1v1 就是两个人的血量。
        /// </summary>
        private PlayerState DecideWinnerByHealth()
        {
            var teams = new List<int>(2);
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (p == null) continue;
                int team = TeamOf(p);
                if (team < 0 || teams.Contains(team)) continue;
                teams.Add(team);
            }

            // 1v1（2 队）、2v2（2 队）、8 人阵营混战（3 队）都按队伍总血量判定。
            if (teams.Count < 2) return null;

            int bestHealth = int.MinValue;
            int winningTeam = -1;
            bool tied = false;

            for (int i = 0; i < teams.Count; i++)
            {
                int health = TeamHealth(teams[i]);
                if (health > bestHealth)
                {
                    bestHealth = health;
                    winningTeam = teams[i];
                    tied = false;
                }
                else if (health == bestHealth)
                {
                    tied = true;
                }
            }

            if (tied || winningTeam < 0) return null;
            return BestAliveInTeam(winningTeam);
        }

        /// <summary>某队**所有成员**的血量总和（阵亡者按 0 计）。</summary>
        private int TeamHealth(int team)
        {
            int total = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (p == null || TeamOf(p) != team) continue;
                total += p.Health;
            }

            return total;
        }

        /// <summary>某队里血量最高的存活者（同血量取座位靠前的；都阵亡则返回 null）。</summary>
        private PlayerState BestAliveInTeam(int team)
        {
            PlayerState best = null;
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (p == null || !p.IsAlive || TeamOf(p) != team) continue;
                if (best == null || p.Health > best.Health) best = p;
            }

            return best;
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

            // ⚠️ 必须用**实例版**：妖术师的跳过条件要看别人的体力，
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
            AdvanceRoundAfter(CurrentPlayer);
            CheckTurnLimit();   // 跳过同样计入回合数上限
        }

        /// <summary>
        /// **已经校验过条件**的跳过（供"先做副作用、再结束回合"的技能使用）。
        ///
        /// 为什么需要它：妖术师的流程是
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
            AdvanceRoundAfter(CurrentPlayer);
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
            AdvanceRoundAfter(CurrentPlayer);

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
            if (CharacterBehaviors.FirstImplementer<ITurnSkipRule>(player) is ITurnSkipRule rule)
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
        /// ⚠️ 有些角色的跳过条件要**看别人**（妖术师：场上必须有人体力与自己不同），
        ///    没有 manager 就查不到 —— 所以调用方应优先用这个重载。
        ///    静态版保留给无法拿到 manager 的老调用点。
        /// </summary>
        public bool CanSkipTurnFor(PlayerState player)
        {
            if (player == null) return false;

            // ⚠️ **两只手都被锁** -> 无手可用 -> 允许跳过。
            //
            //    玩家确认："两只手都被锁住，右侧出现跳过按钮让该玩家回合被跳过。"
            //    没有这条时，那位玩家会**无法行动也无法跳过** ——
            //    2v2 循环赛里这种局面会出现「无合法步」（实测 589 局中有 23 处）。
            if (player.HasNoUsableHand) return true;

            // ⚠️⚠️ **玩家确认（本轮）："无法合步"也正式算作可以跳过回合。**
            //
            //    触发场景：**目盲 + 邪术师的距离修正**。
            //      · 目盲只允许用"与自己距离正好 1"的人的手当被加数；
            //      · 邪术师命中后把这一对的距离推到 3；
            //      · 于是对方没有任何可用的被加数（自己的手不许自己加）——
            //        一手都走不出来。
            //
            //    玩家选择"保留现状、把无合法步正式定义为可以跳过"，
            //    而不是去改目盲 / 距离的判定。
            if (!HasAnyLegalOperation(player)) return true;

            if (CharacterBehaviors.FirstImplementer<ITurnSkipRule>(player) is ITurnSkipRule rule)
            {
                rule.SyncSkipState(player);
                return rule.CanSkip(player, this);
            }

            return player.HasDoubleShield;
        }

        /// <summary>
        /// 这个人**是否存在任何一步合法运算**（自己的手 + 别人的手）。
        ///
        /// 判据与真实运算一致：自己的手要 `CanBeOperated`，
        /// 被加数要过 <see cref="StatusRules.CanUseHandAsAddend"/>
        /// （目盲 / 锁手 / 暗置 / 阵亡都在那里面判）。
        /// </summary>
        public bool HasAnyLegalOperation(PlayerState actor)
        {
            if (actor == null || !actor.IsAlive) return false;

            Hand[] mine = { actor.LeftHand, actor.RightHand };

            for (int i = 0; i < mine.Length; i++)
            {
                Hand own = mine[i];
                if (own == null || !own.CanBeOperated) continue;

                for (int j = 0; j < _players.Count; j++)
                {
                    PlayerState other = _players[j];
                    if (other == null || ReferenceEquals(other, actor)) continue;
                    if (!other.IsAlive) continue;

                    Hand[] theirs = { other.LeftHand, other.RightHand };
                    for (int k = 0; k < theirs.Length; k++)
                    {
                        Hand h = theirs[k];
                        if (h == null || !h.CanBeOperated) continue;
                        if (!StatusRules.CanUseHandAsAddend(actor, h, this)) continue;

                        return true;      // 找到一步就够
                    }
                }
            }

            return false;
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
        /// **回合开始**：清掉"只属于本回合"的标记。
        ///
        /// ⚠️⚠️ 为什么必须有一个统一入口：
        ///
        ///   `SuppressAttackDamage`（法师放弃攻击 = 这一击不造成伤害）
        ///   与 `WizardLogic.WantsToAbandonThisTurn` 的语义都是
        ///   **"本回合"** —— 但它们原来**都没有每回合重置**：
        ///
        ///     · `BoardController`（界面路径）自己清了；
        ///     · **无头路径（MatchRunner / 测试）从不清**。
        ///
        ///   后果：法师**第一次**放弃攻击之后，`SuppressAttackDamage`
        ///   永久为 true —— **他再也打不出任何伤害**。
        ///   于是"放弃换来的类型"永远用不掉（消耗它的攻击永远不会落地），
        ///   状态也就永远挂不上。表现就是
        ///   "AI 法师问了 7000 多次要不要放弃、铺了 8 万次类型，
        ///     一次状态都没挂上"，而且法师的胜率被人为压得极低。
        ///
        ///   界面路径也走这里 —— 两边一套逻辑，不会再漂移。
        /// </summary>
        public void BeginTurn()
        {
            SuppressAttackDamage = false;

            if (CurrentPlayer?.Character
                is Characters.SpecialLogics.WizardLogic wizard)
            {
                wizard.WantsToAbandonThisTurn = false;
            }

            // ⚠️ **吟游诗人的"本回合受过伤 / 回过血"标记，每次行动开始全部复位。**
            //
            //    为什么是"所有人"而不是"当前行动者"：
            //    触发条件是"本回合我受过伤"，而受伤的往往不是行动者
            //    （对手打他）—— 只在行动者身上复位会让防守方的标记永远留着，
            //    于是技能每回合都能发动。
            for (int i = 0; i < _players.Count; i++)
                _players[i]?.ResetTurnFlags();

            SeedBrokenShields();
        }

        /// <summary>
        /// **开局掷出的 5 要带上"破碎的盾"的状态。**
        ///
        /// 玩家实测 bug："初始的 5 没有盾的词条，导致在这只手没运算过的情况下，
        /// 另一只手生成 5 时战士不会修复这个没有词条的盾。"
        ///
        /// 规则口径（玩家确认）：
        ///   · **5 本身就是盾**的角色 -> 开局那只 5 的手算"**破碎的盾**"
        ///     （所以战士合出新盾时能把它修好）；
        ///   · **5 不是盾**的角色（巨人把 5 变成两段攻击、武僧把 5 变成太极 …）
        ///     -> 不加任何盾状态。
        ///
        /// ⚠️ 只补 `State == None` 的 5 —— 正常运算生成的盾是 `Formed`，
        ///    不能覆盖。所以本方法是**幂等**的，可以每次行动开始都跑一遍。
        /// </summary>
        private void SeedBrokenShields()
        {
            for (int i = 0; i < _players.Count; i++)
            {
                PlayerState p = _players[i];
                if (p == null) continue;

                SeedBrokenShield(p, p.LeftHand);
                SeedBrokenShield(p, p.RightHand);
            }
        }

        private void SeedBrokenShield(PlayerState owner, Hand hand)
        {
            if (hand == null) return;
            if (hand.Value != 5) return;
            if (hand.State != ShieldState.None) return;      // 已经成型的盾不动
            if (!FiveIsShield(owner, hand)) return;

            hand.SetShieldState(ShieldState.Broken);
        }

        /// <summary>
        /// 这个角色的「5」是不是**盾**。
        ///
        /// 判据交给角色自己（`IHandEffectRewriter`）：巨人的 5 是两段攻击、
        /// 武僧的 5 是太极 —— 它们都会把 5 改写成别的东西。
        /// 没有改写者的角色，5 就是标准的合盾。
        /// </summary>
        private bool FiveIsShield(PlayerState owner, Hand hand)
        {
            if (owner?.Character == null) return true;

            // ⚠️ 武僧的 5 是「太极」，不是盾。
            if (owner.Character is MonkLogic) return false;

            if (!(CharacterBehaviors.FirstImplementer<IHandEffectRewriter>(owner)
                  is IHandEffectRewriter rewriter))
            {
                return true;                                 // 没有改写者 -> 5 就是盾
            }

            try
            {
                HandEffectType effect = rewriter.RewriteEffect(owner, hand, 5, this);
                return effect == HandEffectType.Shield;
            }
            catch
            {
                return true;                                 // 判不出来就按"是盾"处理
            }
        }

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
