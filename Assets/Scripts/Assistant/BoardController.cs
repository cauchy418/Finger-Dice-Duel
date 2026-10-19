using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    /// <summary>
    /// 棋盘交互阶段。
    /// </summary>
    public enum BoardPhase
    {
        /// <summary>等待当前玩家选择"用自己哪一只手"。</summary>
        ChoosingAttackerHand = 0,

        /// <summary>等待当前玩家选择"打对方哪一只手"。</summary>
        ChoosingTargetHand = 1,

        /// <summary>
        /// 等待当前玩家确认"打哪位玩家"。
        ///
        /// 为什么需要单独一步（即使 1v1 只有一个人可打）：
        /// **挨打的是玩家，不是手**。手只是"参与运算的那只手"，
        /// 同时也是"玩家用来指定目标"的点击对象。
        /// 现在 1v1 也要求点这一次，是为了**多人模式不去走捷径** ——
        /// 否则多人上线后流程会变，玩家要重新学。
        ///
        /// 若此刻**没有合法攻击目标**（刺客潜行、日后的攻击距离不足等），
        /// 界面改为提供"放弃攻击"（见 <see cref="CanGiveUpAttack"/>）。
        /// </summary>
        ChoosingTargetPlayer = 5,

        /// <summary>防守方有两面盾，需要先决定用哪一面抵挡。</summary>
        ChoosingDefenseShield = 2,

        /// <summary>
        /// 回答角色提出的"运算前抉择"（野蛮人锁手、牧师回血目标、游侠闪避 / 强化射击……）。
        /// 由角色通过 <see cref="IPreTurnChoiceProvider"/> 自己声明，
        /// 控制器不认识任何具体角色类型。
        /// </summary>
        ChoosingPreTurnChoice = 3,

        /// <summary>
        /// 回答"选定目标手**之前**"的角色抉择（牧师的 4/6/8 攻击/回复二选一）。
        ///
        /// 玩家要求的顺序：运算出 4 -> 右侧弹出攻击/回复 -> 选完再点目标手。
        /// </summary>
        ChoosingPreTargetChoice = 6,

        /// <summary>对局结束。</summary>
        GameOver = 4,

        /// <summary>
        /// 等待**妖术师**点一个人的手，把那人的体力改成与自己一致。
        ///
        /// 这是妖术师"跳过回合"技能的目标选择步骤：
        /// 点右侧「跳过回合」进入本相位，再点目标的手发动。
        /// </summary>
        ChoosingHealthSwapTarget = 7,

        /// <summary>
        /// 等待**赌徒**点「开赌」。
        ///
        /// 玩家确认的流程：先点自己一只手 -> 再点右侧「开赌」掷 1d9。
        /// 掷出的点数**直接作为本次行动结果**（4/6/8 攻击、9 回血、5 合盾）。
        /// 赌博**不需要**点他人的手 —— 打谁在结果出来之后再选。
        /// </summary>
        ChoosingGamble = 8,

        /// <summary>
        /// 等待**盗贼**点"要被偷的那只手"。
        ///
        /// 玩家确认的流程：点自己的手 -> 点右侧「偷窃」按钮 -> 点别人的手。
        /// 无限距离、可以偷队友。
        /// </summary>
        ChoosingStealTarget = 9,

        /// <summary>兼容旧名字，与 <see cref="ChoosingPreTurnChoice"/> 等价。</summary>
        ChoosingLockTarget = ChoosingPreTurnChoice,
    }

    /// <summary>
    /// 接收"某一名玩家在回合结算前做出的抉择"的接收者。
    ///
    /// 每个玩家各有一个实现（见 <see cref="SimpleChoiceProvider"/>），
    /// <see cref="BoardController"/> 按**抉择的归属玩家**路由：
    ///   · 选盾 → 路由给**防守方**；
    ///   · 锁手 → 路由给**行动方**（野蛮人自己）。
    ///
    /// 之所以不能只接一个 sink：防守方可能是 A 也可能是 B，
    /// 只有一个通道的话，B 当防守方时选盾结果永远传不进 GameManager
    /// （表现为"双盾被攻击时直接破盾，没有询问"，曾经的真实 bug）。
    /// </summary>
    public interface IPlayerChoiceSink
    {
        /// <summary>记录某个抉择的结果（null 表示放弃）。</summary>
        void SetChoice(HandEffect effect, Hand hand, int variant = 0);

        /// <summary>回读某个抉择的结果；没记录过时返回 <paramref name="fallback"/>。</summary>
        Hand GetChoice(HandEffect effect, Hand fallback = null, int variant = 0);

        /// <summary>
        /// 清空所有记录。**每个新回合开始时必须调用**。
        ///
        /// 为什么关键：`PreTurnChoiceReader.Read` 在抉择器没给出结果时会**回读 sink**。
        /// 而 sink 是跨回合保留的 —— 上一回合留下的旧答案会被当成"本回合的决定"，
        /// 于是 AI（或人类）明明选了闪避/强化，结算时读到的却是过期记录，
        /// 表现为"技能完全不发动"（玩家实测反馈：AI 从不发动技能）。
        /// </summary>
        void ClearChoices();
    }

    /// <summary>
    /// 当"该做决定的人"其实是 AI 时，由它替 AI 做出抉择。
    ///
    /// 为什么需要：进入选盾阶段时，"该做决定的人"可能是**不属于当前行动方**的一方
    /// （例如 AI 攻击、人类防守）。这类抉择没法交给"行动方的 AI"去处理，
    /// 所以由 UI 层在开局时注入一个解析器。返回 null 表示放弃 / 由调用方回退。
    ///
    /// 注：角色自己提出的抉择（锁手 / 回血目标 / 闪避 / 强化）**不走这里** ——
    /// 那些由 AI 自己的 <see cref="IChoiceProvider.ChooseHandFor"/> 直接回答。
    ///
    /// 只依赖 Core 类型，因此 Assistant 层不必引用 UI 程序集。
    /// </summary>
    public interface IAiChoiceResolver
    {
        /// <summary>替 AI 防守方选一面盾抵挡（返回 null 表示放弃选择，由流程回退）。</summary>
        Hand ChooseShieldToBlock(PlayerState defender, IReadOnlyList<Hand> candidates);
    }

    /// <summary>
    /// 棋盘交互控制器：把"点击手牌 -> 结算回合 -> 进入下一回合"这套流程做成
    /// **不依赖任何 Unity 类型**的状态机，因此可以在无头测试里完整验证。
    ///
    /// 分工：
    ///   · 规则执行交给 <see cref="GameManager"/>（Core）；
    ///   · 本类只负责"现在该问玩家什么"以及"这次点击是否合法"；
    ///   · UI（<c>GameBootstrap</c>）只把点击转成本类的调用，并把状态画出来。
    ///
    /// 典型用法：
    ///   1. <see cref="BeginTurn"/> 开始当前玩家的回合；
    ///   2. 人类玩家读 <see cref="Phase"/> / <see cref="Hint"/>，调用 <see cref="ClickHand"/>；
    ///       AI 玩家直接调用 <see cref="MakeAutomaticMove"/>；
    ///   3. 每步之后读 <see cref="Phase"/> / <see cref="LastTurn"/> / <see cref="Log"/> 刷新界面。
    /// </summary>
    public sealed class BoardController
    {
        private readonly GameManager _game;
        private readonly IChoiceProvider _choiceA;
        private readonly IChoiceProvider _choiceB;

        /// <summary>A 玩家的抉择接收者（选盾 / 锁手都写到这里）。</summary>
        private readonly IPlayerChoiceSink _sinkA;

        /// <summary>B 玩家的抉择接收者。</summary>
        private readonly IPlayerChoiceSink _sinkB;

        private readonly IMoveSelector _aiForA;
        private readonly IMoveSelector _aiForB;

        private readonly List<string> _log = new List<string>();
        private readonly List<Hand> _shieldChoices = new List<Hand>(2);

        /// <summary>击杀奖励/惩罚待执行的额外行动（队首优先）。</summary>
        private readonly List<PlayerState> _extraQueue = new List<PlayerState>();
        private bool _extraActionActive;
        private PlayerState _resumePlayer;

        // ---- 通用"运算前抉择"流水线 ----
        private readonly List<ChoiceRequest> _pendingRequests = new List<ChoiceRequest>();
        private readonly List<Hand> _pendingChoiceCandidates = new List<Hand>(4);
        private int _pendingRequestIndex;
        private PlayerState _pendingChoiceDecider;
        private Hand _pendingActedHand;
        private Hand _pendingTargetHand;

        /// <summary>本次运算是否被判定为"攻击"（由 <see cref="BuildChoiceContext"/> 缓存）。</summary>
        private bool _pendingIsAttack;

        private Hand _selectedTarget;
        private Hand _chosenDefenseShield;

        // ---- 赌徒的赌博 ----
        /// <summary>
        /// 本回合已经掷出的赌博点数（0 = 还没掷）。
        ///
        /// 为什么要**存下来**而不是结算时再掷：
        ///   掷出 4/6/8 时先要让玩家**选打谁**，选完才结算 ——
        ///   如果那时才掷，玩家看到的点数与结算用的点数就不是同一次了。
        /// </summary>
        private int _pendingGambleRoll;

        /// <summary>赌博用的随机源（可注入，便于测试确定性复现）。</summary>
        public System.Random GambleRng { get; set; } = new System.Random();

        // ============================================================
        // 本回合的"运算源 / 目标手"的**独立副本**
        //
        // 为什么需要：`SelectedHand` / `_selectedTarget` 会被多处清理
        // （CancelSelection、SetupFirstStep、上一次结算的收尾），
        // 而"选目标手之前的抉择"里 **AI 作答会递归回到
        // AdvanceCharacterChoice**，那条路径上状态可能已被中间的重建清掉 ——
        // 于是结算时运算源为空，抛"未选择运算源"（被 catch 吞掉后极难排查）。
        //
        // 这两个字段只在**回合真正结束时**清空，用来给结算兜底。
        // ============================================================
        private Hand _turnActedHand;
        private Hand _turnTargetHand;

        /// <summary>
        /// 构造棋盘控制器。
        ///
        /// <paramref name="sinkA"/> / <paramref name="sinkB"/> 必须是**两个玩家各自的**
        /// 抉择器，且同时实现 <see cref="IChoiceProvider"/> 与
        /// <see cref="IPlayerChoiceSink"/>（见 <see cref="SimpleChoiceProvider"/>）。
        /// 取舍方式：
        ///   · 选盾 → 防守方那一侧；
        ///   · 锁手 → 行动方那一侧。
        ///
        /// **刻意只接受 sink 而不接受额外的 choiceProvider 参数**：曾经两者分开传，
        /// UI 就漏传了 B 的 sink，导致 B 当防守方时选盾询问被静默跳过。
        /// 两者必须是同一个对象，接口上就不给它分裂的机会。
        /// </summary>
        public BoardController(GameManager game,
                               IPlayerChoiceSink sinkA,
                               IPlayerChoiceSink sinkB,
                               IMoveSelector aiForA = null,
                               IMoveSelector aiForB = null)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _sinkA = sinkA;
            _sinkB = sinkB;

            // 同一个对象本来就兼任 choice provider；这里从 sink 反查，避免两套引用漂移。
            _choiceA = sinkA as IChoiceProvider;
            _choiceB = sinkB as IChoiceProvider;

            if (_choiceA == null)
                throw new ArgumentException(
                    "sinkA 必须同时实现 IChoiceProvider（见 SimpleChoiceProvider）。", nameof(sinkA));
            if (_choiceB == null)
                throw new ArgumentException(
                    "sinkB 必须同时实现 IChoiceProvider（见 SimpleChoiceProvider）。", nameof(sinkB));

            _aiForA = aiForA;
            _aiForB = aiForB;

            // 让结算能读到"本回合先前已问过的答案"（分阶段抉择用）。
            // 必须在构造函数里接上 —— 只在 AI 决策路径赋值的话，
            // 人类玩家的前置抉择就传不到结算（真实 bug：
            // 牧师选了回复却按攻击结算）。
            _game.AnsweredLookup = GetAnsweredChoice;

            // 带**玩家参数**的版本：妖术师是场上的第三方，
            // 取消攻击的抉择不属于行动方也不属于防守方（见 CollectHexerCancelChoices）。
            _game.AnsweredLookupFor = GetAnsweredChoiceFor;

            // "是否问过该玩家取消攻击" —— 用来区分"没问过（AI）"与
            // "问过但选了不发动"（后者必须放行）。
            _game.AskedCancelLookup = WasAskedHexerCancel;
        }

        /// <summary>方便测试：双方共用同一个抉择器对象。</summary>
        public BoardController(GameManager game,
                               IChoiceProvider bothPlayers,
                               IMoveSelector aiForA = null,
                               IMoveSelector aiForB = null)
            : this(game,
                   bothPlayers as IPlayerChoiceSink
                       ?? throw new ArgumentException(
                           "抉择器必须同时实现 IPlayerChoiceSink。", nameof(bothPlayers)),
                   bothPlayers as IPlayerChoiceSink
                       ?? throw new ArgumentException(
                           "抉择器必须同时实现 IPlayerChoiceSink。", nameof(bothPlayers)),
                   aiForA, aiForB)
        {
        }

        // ============================================================
        // 对外状态
        // ============================================================

        public GameManager Game => _game;
        public PlayerState PlayerA => _game.PlayerA;
        public PlayerState PlayerB => _game.PlayerB;

        public BoardPhase Phase { get; private set; } = BoardPhase.ChoosingAttackerHand;

        /// <summary>当前正在行动的玩家。</summary>
        public PlayerState ActivePlayer { get; private set; }

        /// <summary>已经选好的运算源（自己那只手）。</summary>
        public Hand SelectedHand { get; private set; }

        /// <summary>
        /// 最近一次**已结算**的回合结果。会一直保留到下一次结算，
        /// 因此界面上始终能看到"刚才发生了什么"。
        /// </summary>
        public TurnResult LastTurn { get; private set; }

        /// <summary>
        /// 刚才那次结算属于第几回合（<see cref="GameManager.TurnCount"/> 的值）。
        ///
        /// 用途：界面只应在"**本回合刚刚结算过**"时显示上一手摘要。
        /// 否则新回合刚开始时会显示**上一位行动者**的旧结算 ——
        /// 玩家实测看到的正是这个问题：轮到圣武士，摘要却写着"游侠 左手 …"
        /// （那是上一回合游侠打的那一手）。
        /// </summary>
        public int LastTurnNumber { get; private set; } = -1;

        /// <summary>给玩家的一句提示。</summary>
        public string Hint { get; private set; } = "";

        public IReadOnlyList<string> Log => _log;

        /// <summary>当前是否正在执行击杀奖惩的额外行动。</summary>
        public bool IsExtraAction => _extraActionActive;

        /// <summary>额外行动队列中还剩几次。</summary>
        public int ExtraActionsRemaining => _extraQueue.Count;

        /// <summary>需要防守方选盾时，可选的盾（最多 2 面）。</summary>
        public IReadOnlyList<Hand> DefenseShieldChoices => _shieldChoices;

        /// <summary>
        /// 当前待回答的"角色抉择"的可选项（野蛮人锁手、牧师回血目标、游侠闪避 / 强化……）。
        /// 空表示当前不在角色抉择阶段。
        /// </summary>
        public IReadOnlyList<Hand> ChoiceCandidates => _pendingChoiceCandidates;

        /// <summary>当前待回答的抉择本身（用于 UI 显示提示与"是否需要取消键"）。</summary>
        public ChoiceRequest? PendingRequest =>
            IsInChoicePhase && _pendingRequestIndex < _pendingRequests.Count
                ? _pendingRequests[_pendingRequestIndex]
                : (ChoiceRequest?)null;

        /// <summary>
        /// 当前是否停在"回答角色抉择"的阶段。
        ///
        /// ⚠️ 有**两个**这样的阶段：选定目标手**之前**（牧师的攻击/回复）
        /// 与**之后**（闪避 / 强化 / 锁手 / 回复目标）。
        /// 漏掉任何一个都会让 UI 拿不到 <see cref="PendingRequest"/>、
        /// 按钮不出现 —— 新增阶段时必须回来这里补。
        /// </summary>
        private bool IsInChoicePhase
            => Phase == BoardPhase.ChoosingPreTurnChoice
               || Phase == BoardPhase.ChoosingPreTargetChoice;

        /// <summary>当前正在选择的目标手（防守方选的盾）。</summary>
        public Hand PendingChoice => _chosenDefenseShield;

        /// <summary>
        /// 本次运算要打的那位玩家（挨打的是玩家，不是手）。
        ///
        /// ⚠️ 与"被加数的手主"**可能不是同一个人** ——
        /// 玩家确认的交互是：② 点任意人的手当被加数，③ 单独选攻击目标。
        ///
        /// 取值优先级：
        ///   ① 已经确认的目标玩家（<see cref="TargetPlayer"/>）
        ///   ② 退回"第一位对手"（回合刚开始、还没做任何选择时）
        ///
        /// 2v2 / 8 人局里第一位对手**不一定是**想打的人，
        /// 所以凡是在"选完目标之后"才用到的场合，都应该先看 <see cref="TargetPlayer"/>。
        /// 这里直接把它做进返回值，避免二十多处各写一遍 `TargetPlayer ?? Defender`
        /// （那样迟早会漏一处）。
        /// </summary>
        public PlayerState Defender => TargetPlayer ?? _game.GetOpponent(ActivePlayer);

        /// <summary>当前行动方是否由人类操作（没有配置 AI 就是人类）。</summary>
        public bool IsHumanTurn => GetAi(ActivePlayer) == null;

        /// <summary>
        /// 现在这个阶段**该谁做决定**。UI 用它判断"当前是否轮到本机玩家点击"。
        ///
        /// 这一点很关键：进入 <see cref="BoardPhase.ChoosingDefenseShield"/> 时，
        /// <see cref="ActivePlayer"/> 仍然是**攻击方**，但该做决定的是**防守方**。
        /// 如果 UI 用 <see cref="IsHumanTurn"/>（看行动方）来放行点击，
        /// 那么"AI 攻击、人类防守"时玩家的选盾点击会被直接丢弃 —— 曾经的真实 bug。
        /// </summary>
        public PlayerState Chooser
        {
            get
            {
                switch (Phase)
                {
                    case BoardPhase.ChoosingDefenseShield:
                        return Defender;

                    // 角色抉择的归属者**不一定是行动方**（游侠的闪避属于防守方），
                    // 必须用收集阶段定下来的 _pendingChoiceDecider。
                    // 这里曾经错误地返回 ActivePlayer，导致"游侠被攻击时闪避选项点不到"。
                    case BoardPhase.ChoosingPreTurnChoice:
                    // "选目标手之前"的抉择同理，也看 _pendingChoiceDecider
                    case BoardPhase.ChoosingPreTargetChoice:
                        return _pendingChoiceDecider ?? ActivePlayer;

                    case BoardPhase.ChoosingAttackerHand:
                    case BoardPhase.ChoosingTargetHand:
                    // "确认目标玩家"同样由**行动方**决定（他来决定打谁）。
                    // ⚠️ 漏掉这个分支会导致 chooser=null -> IsHumanChooser=false
                    // -> UI 丢弃玩家的点击 -> **游戏在第一步就卡住**（真实 bug：
                    //    玩家实测"战士打战士后第一步就卡住，显示 AI 正在思考"）。
                    case BoardPhase.ChoosingTargetPlayer:
                        return ActivePlayer;

                    // 赌博由行动方自己点「开赌」
                    case BoardPhase.ChoosingGamble:
                        return ActivePlayer;

                    // 妖术师改体力的目标由**行动方自己**点（他发动技能）
                    case BoardPhase.ChoosingHealthSwapTarget:
                        return ActivePlayer;

                    // ⚠️ **盗贼的偷窃目标也由行动方自己点。**
                    //
                    //    我第一版漏了这个分支，于是 chooser=null ->
                    //    IsHumanChooser=false -> UI 把玩家点手牌**全丢了**
                    //    -> 按了偷窃之后**卡死**（玩家实测，诊断里就是
                    //       `phase=ChoosingStealTarget isHumanChooser=False`）。
                    //
                    //    上面 ChoosingTargetPlayer 那段注释写的正是**同一个坑**，
                    //    我又踩了一次 —— 所以新阶段必须在**四个**地方登记：
                    //    ChooserFor、CanClick、ClickHand、以及 Hint 文案。
                    case BoardPhase.ChoosingStealTarget:
                        return ActivePlayer;

                    default:
                        return null;
                }
            }
        }

        public bool IsHumanChooser => Chooser != null && GetAi(Chooser) == null;

        /// <summary>
        /// UI 判断"这张手牌现在能不能被本机玩家点击"的**唯一入口**。
        ///
        /// 语义 = "现在该人类做决定" 且 "这张牌是当前阶段的有效选项"。
        ///
        /// 之所以要把两个条件合并成一个入口：UI 曾经分开判断，用的是
        /// "行动方是不是人类"（<see cref="IsHumanTurn"/>）而不是"该谁决定"
        /// （<see cref="IsHumanChooser"/>），于是"AI 攻击 / 人类防守"时
        /// 玩家的选盾点击被静默丢弃。合成一个可测的方法，就不会再出现这种分裂。
        /// </summary>
        public bool CanAct(Hand hand)
            => IsHumanChooser && CanClick(hand);

        /// <summary>
        /// 由 UI 注入：当"该做决定的人"是 AI 时，用它来代做抉择。
        /// 为 null 时，AI 的选盾/锁手抉择会退化为"默认选择"（见各自的解析方法）。
        /// </summary>
        public IAiChoiceResolver AiResolver { get; set; }

        /// <summary>
        /// 当前是否**可以点「开赌」**。
        ///
        /// 两个时机：
        ///   · <see cref="BoardPhase.ChoosingGamble"/> —— 强制赌博时（上一回合做了正常运算）
        ///   · <see cref="BoardPhase.ChoosingTargetHand"/> —— **非强制**时也允许：
        ///     赌徒点完自己的手后，可以点他人的手走正常运算，
        ///     也可以直接点「开赌」改走赌博（玩家确认"当然也可以继续赌"）
        /// </summary>
        public bool CanGamble
            => !_game.IsGameOver
               && (Phase == BoardPhase.ChoosingGamble
                   || Phase == BoardPhase.ChoosingTargetHand)
               && SelectedHand != null
               && ActivePlayer?.Character is GamblerLogic
               // 强制赌博时"选目标手"这条路本来就不该存在，所以两种相位都要放行；
               // 非强制时两种相位也都放行。此处不再额外区分。
               ;

        /// <summary>
        /// **测试钩子**：临时让赌徒绕过"强制赌博"，好让测试能走一次完整流程
        /// 来验证"正常运算 -> 下一回合强制赌博"这条规则。
        ///
        /// 为什么需要它：控制器对赌徒**总是**走赌博分岔（那是他的正常路径），
        /// 所以从控制器层面根本进不去"正常运算"，也就无法验证它设了强制标志。
        /// 而直接调引擎（GameManager.ExecuteTurn）会绕过控制器，
        /// 同样测不到标志 —— 两头都够不着，才开这个口子。
        ///
        /// ⚠️ 只该被测试使用。正常游玩时不要调用。
        /// </summary>
        public void ForceAllowNormalOperationForTest(bool allow)
        {
            _allowNormalOperationForTest = allow;
        }

        private bool _allowNormalOperationForTest;

        /// <summary>
        /// 本次行动是否可能触发攻击范围内的目标选择（含赌博掷出攻击时）。
        /// </summary>
        public int PendingGambleRoll => _pendingGambleRoll;

        /// <summary>
        /// **最近一次**赌博掷出的点数（结算后仍然保留）。
        ///
        /// 与 <see cref="PendingGambleRoll"/> 的区别：那个是"本回合待结算的点数"，
        /// 回合边界会清零；这个是"刚刚赌出的结果"，供界面/战报显示。
        /// </summary>
        public int LastGambleRoll { get; private set; }

        /// <summary>
        /// 点「开赌」：掷 1d9，并把点数作为本次行动的结果。
        ///
        /// 玩家确认的流程：
        ///   · 掷出 **4/6/8**（攻击）-> 停在"选打谁"（赌博不点他人的手，
        ///     打谁在结果出来后选）
        ///   · 掷出 **5**（合盾）/ **9**（回血）/ 其余（无效果）-> 直接结算
        /// </summary>
        public bool Gamble()
        {
            if (!CanGamble) return false;
            if (SelectedHand == null) return false;

            _pendingGambleRoll = GamblerLogic.Roll(GambleRng);
            LastGambleRoll = _pendingGambleRoll;

            HandEffectType effect = GamblerLogic.EffectOf(_pendingGambleRoll);
            bool isAttack = effect == HandEffectType.Attack4
                            || effect == HandEffectType.Attack6
                            || effect == HandEffectType.Attack8;

            _log.Add($"{ActivePlayer.Name} 开赌：掷出 {_pendingGambleRoll}。");

            if (!isAttack)
            {
                // 不是攻击 -> 不需要选目标，直接结算
                _selectedTarget = SelectedHand;   // 占位：赌博没有"被加数"
                ExecuteResolvedTurn();
                return true;
            }

            // 是攻击 -> 先让玩家选打谁
            Phase = BoardPhase.ChoosingTargetPlayer;

            if (!BeginTargetPlayerChoice())
            {
                // 没有可打的人 -> 放弃这次赌博（算用掉回合）
                NoteGambleGaveUp();
                GiveUpAttack();
            }

            return true;
        }

        /// <summary>赌博没有合法攻击目标时的收尾（战报要说清是赌博）。</summary>
        private void NoteGambleGaveUp()
        {
            _log.Add($"{ActivePlayer.Name} 开赌掷出 {_pendingGambleRoll}，" +
                     $"但没有可攻击的目标 —— 本回合作罢。");
        }

        /// <summary>
        /// 当前行动方是否可以跳过本回合。
        ///
        /// 规则：**只有两只手都是成型护盾时才可以跳过**（见 RULES.md §14）。
        /// UI 用它决定是否显示"跳过回合"按钮。
        /// </summary>
        public bool CanSkipTurn
            => !_game.IsGameOver
               && ActivePlayer != null
               && Phase != BoardPhase.ChoosingDefenseShield
               && Phase != BoardPhase.ChoosingLockTarget
               && Phase != BoardPhase.ChoosingHealthSwapTarget
               && Phase != BoardPhase.GameOver
               // ⚠️ 用带 GameManager 的重载：妖术师的跳过条件要查**别人**的体力，
               //    静态版传 null 会让它永远返回 false（按钮不显示）。
               && _game.CanSkipTurnFor(ActivePlayer);

        /// <summary>
        /// 跳过当前回合（不执行运算，直接把回合交给对方）。
        /// 仅在 <see cref="CanSkipTurn"/> 为 true 时有效。
        ///
        /// ⚠️ 两种角色共用这个按钮，但**流程不同**：
        ///   · 刺客（潜行）/ 双盾在手 -> 直接跳过
        ///   · **妖术师** -> 先进入"选一个人的手"，把那人**体力改成与自己一致**，
        ///     选完才真正跳过（玩家要求的交互：
        ///     "在右侧显示跳过回合，然后点击一个人的手发动技能"）
        /// </summary>
        public bool SkipTurn()
        {
            if (!CanSkipTurn) return false;

            // 妖术师：先选目标，不立刻结束回合
            var hexer = ActivePlayer.Character as HexerLogic;
            if (hexer != null)
            {
                PlayerState victim = FindHealthSwapTarget();
                if (victim != null)
                {
                    Phase = BoardPhase.ChoosingHealthSwapTarget;
                    Hint = $"{ActivePlayer.Name}（妖术师）：点一个人的手，" +
                           $"把那个人的体力改成与自己一致（{ActivePlayer.Health}）。";
                    return true;
                }
                // 找不到目标就退回"直接跳过"（正常不会发生：CanSkipTurn 已判过）
            }

            _log.Add($"{ActivePlayer.Name} 选择跳过本回合。");
            _game.SkipTurn();

            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();

            BeginTurn();
            return true;
        }

        /// <summary>
        /// 妖术师改体力的目标：**场上任意一名体力与自己不同的存活角色**。
        ///
        /// 返回 null 表示没有可改的人（此时技能无意义）。
        /// </summary>
        public PlayerState FindHealthSwapTarget()
        {
            if (ActivePlayer == null) return null;

            IReadOnlyList<PlayerState> all = _game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, ActivePlayer)) continue;
                if (p.Health == ActivePlayer.Health) continue;

                return p;
            }

            return null;
        }

        /// <summary>落实"把某人体力改成与自己一致"并结束本回合（妖术师）。</summary>
        public bool ApplyHealthSwapAndEndTurn(PlayerState target)
        {
            if (target == null || ActivePlayer == null) return false;
            if (Phase != BoardPhase.ChoosingHealthSwapTarget) return false;

            int before = target.Health;

            if (!HexerLogic.ApplyHealthSwap(ActivePlayer, target))
            {
                // 目标体力本来就一样（或已阵亡）-> 退回选择，不浪费回合
                Hint = "那个人的体力已经与你一致，请另选一个人。";
                return false;
            }

            _log.Add($"{ActivePlayer.Name}（妖术师）跳过回合，" +
                     $"把 {target.Name} 的体力从 {before} 改成 {ActivePlayer.Health}。");

            // ⚠️ 用"已校验"的版本：改完体力后"有人体力与我不同"这个条件
            //    就不再成立了，再校验一次会抛"非法跳过"（测试抓到）。
            _game.SkipTurnAlreadyValidated();

            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();
            _preTargetChoiceActive = false;
            _returnToTargetAfterChoice = false;

            BeginTurn();
            return true;
        }

        // ============================================================
        // 多名玩家的查表（2v2 / 8 人局）
        //
        // 1v1 仍然走下面的 A/B 回退分支，所以既有行为完全不变；
        // 2v2 由 UI 调 BindPlayers 把四个人的 AI 与抉择器登记进来。
        //
        // 为什么用字典而不是"再加四个字段"：玩家上限会变（8 人局），
        // 按玩家查表不需要每加一个人就改一轮 if。
        // ============================================================

        private readonly Dictionary<PlayerState, IMoveSelector> _aiByPlayer =
            new Dictionary<PlayerState, IMoveSelector>();
        private readonly Dictionary<PlayerState, IPlayerChoiceSink> _sinkByPlayer =
            new Dictionary<PlayerState, IPlayerChoiceSink>();

        /// <summary>
        /// 登记一名玩家的 AI 与抉择器（2v2 / 8 人局用）。
        ///
        /// <paramref name="ai"/> 传 null 表示这位是**人类**玩家。
        /// </summary>
        public void BindPlayer(PlayerState player, IMoveSelector ai, IPlayerChoiceSink sink)
        {
            if (player == null) return;

            _aiByPlayer[player] = ai;
            if (sink != null) _sinkByPlayer[player] = sink;
        }

        /// <summary>取某玩家对应的 AI；人类玩家返回 null。</summary>
        private IMoveSelector GetAi(PlayerState p)
        {
            if (p == null) return null;
            if (_aiByPlayer.TryGetValue(p, out IMoveSelector ai)) return ai;

            // 1v1 回退（未登记时仍按 A/B 判定）
            return ReferenceEquals(p, PlayerA) ? _aiForA : _aiForB;
        }

        /// <summary>
        /// 取某玩家对应的**抉择器**。
        ///
        /// ⚠️ 这一处曾经是个真实 bug（玩家实测：**AI 从不发动任何技能**）：
        /// 原来它无条件返回 `_choiceA / _choiceB`，也就是 sink 对象
        /// （UI 里是 `SimpleChoiceProvider` —— 只负责**记录**玩家点了什么）。
        /// 于是 AI 的抉择被交给了一个"没有任何策略、只回读记录"的对象，
        /// 而 AI 真正的策略在 <see cref="IMoveSelector"/>（`GreedyChoiceProvider`）里，
        /// 结果永远是 null —— 表现为"AI 有 1 也不闪避、有 2 也不强化"。
        ///
        /// 现在：**AI 玩家优先用它的抉择器**（AI 的 IMoveSelector 同时实现
        /// <see cref="IChoiceProvider"/>）；人类玩家才用 sink。
        /// </summary>
        private IChoiceProvider GetChoice(PlayerState p)
        {
            if (_aiByPlayer.TryGetValue(p, out IMoveSelector ai) && ai is IChoiceProvider fromAi)
                return fromAi;

            if (ReferenceEquals(p, PlayerA))
                return (_aiForA as IChoiceProvider) ?? _choiceA;

            if (ReferenceEquals(p, PlayerB))
                return (_aiForB as IChoiceProvider) ?? _choiceB;

            // 2v2 / 8 人局：登记过的玩家用自己的抉择器（AI）或 sink（人类）
            if (_aiByPlayer.TryGetValue(p, out IMoveSelector anyAi)
                && anyAi is IChoiceProvider anyProvider)
                return anyProvider;

            if (_sinkByPlayer.TryGetValue(p, out IPlayerChoiceSink sink))
                return sink as IChoiceProvider;

            return _game.GetChoiceProvider(p);
        }

        /// <summary>取某玩家对应的抉择接收者（写入玩家选择用）。</summary>
        private IPlayerChoiceSink SinkFor(PlayerState p)
        {
            if (ReferenceEquals(p, PlayerA)) return _sinkA;
            if (ReferenceEquals(p, PlayerB)) return _sinkB;

            if (_sinkByPlayer.TryGetValue(p, out IPlayerChoiceSink sink)) return sink;

            return GetChoice(p) as IPlayerChoiceSink;
        }

        /// <summary>某玩家对应的抉择接收者（读回"已经问过的答案"用，Core 侧接口）。</summary>
        private IPreTurnChoiceSink PreTurnSinkFor(PlayerState p)
            => SinkFor(p) as IPreTurnChoiceSink;

        /// <summary>
        /// 读回"本回合先前已经问过的答案"（供分阶段抉择使用）。
        ///
        /// ⚠️ 必须走 <c>GetExplicitChoice</c>（只读显式记录）。
        /// 用 <c>GetChoice</c> 会套用抉择自身的回退值，于是
        /// "玩家选了攻击"（记录为 null）被误判成"选了回复"（回退值是自己）。
        ///
        /// 先查行动方（牧师的 4/6/8 攻击/回复是他答的），再查防守方。
        /// </summary>
        private Hand GetAnsweredChoice(HandEffect effect, int variant)
        {
            // ⚠️ **先读控制器自己保存的本回合答案**。
            //
            // 牧师的 4/6/8 有两段抉择，中间隔着"选目标手"，
            // 而那段流程会重建抉择队列、并可能在回合边界清空 sink。
            // sink 里的答案因此可能读不到（实测：结算时读到 null，
            // 于是"选了回复"被当成"选攻击"）。
            //
            // 控制器这一份由 RecordAnswer 统一登记，不受 sink 清空影响，所以它优先。
            if (_answeredThisTurn.TryGetValue(AnswerKey(effect, variant), out Hand saved))
                return saved;

            Hand fromActor = PreTurnSinkFor(ActivePlayer)?.GetExplicitChoice(effect, variant);
            if (fromActor != null) return fromActor;

            return PreTurnSinkFor(Defender)?.GetExplicitChoice(effect, variant);
        }

        /// <summary>
        /// 本回合已经答过的抉择（键 = <c>effect#variant</c>）。
        /// </summary>
        private readonly Dictionary<string, Hand> _answeredThisTurn =
            new Dictionary<string, Hand>();

        /// <summary>
        /// 读回"**某位玩家**对某抉择的作答"（带玩家参数）。
        ///
        /// 妖术师是场上的第三方，所以不能复用 <see cref="GetAnsweredChoice"/>
        /// （那个按"行动方 / 防守方"找）。
        ///
        /// ⚠️ 用 <c>_askedHexerCancel</c> 的**键是否存在**表达"问过"，
        ///    这样"问过但选了不发动"（值为 null）与"没问过"能区分开。
        /// </summary>
        private Hand GetAnsweredChoiceFor(PlayerState player, HandEffect effect, int variant)
        {
            if (player != null && effect == HandEffect.HexerCancel
                && _askedHexerCancel.TryGetValue(player, out Hand answer))
            {
                return answer;      // 可能是 null（玩家选了不发动）
            }

            return PreTurnSinkFor(player)?.GetExplicitChoice(effect, variant);
        }

        /// <summary>
        /// 该玩家**是否被问过**"是否取消攻击"。
        ///
        /// 与 <see cref="GetAnsweredChoiceFor"/> 配套：
        /// 返回 true 表示"问过"，此时 null 答案意味着"玩家选了不发动"。
        /// </summary>
        public bool WasAskedHexerCancel(PlayerState player)
            => player != null && _askedHexerCancel.ContainsKey(player);

        /// <summary>
        /// 本回合"**问过谁是否取消攻击**"（键 = 玩家）。
        ///
        /// 为什么需要单独记一份：妖术师的取消是**第三方**的抉择，
        /// 而且必须区分两种"没选"：
        ///   · **没问过**（AI，或条件不成立）-> 走自动取消
        ///   · **问过但选了「不发动」** -> 放行这次攻击
        /// `_answeredThisTurn` 在答案为 null 时是"删除键"，分不出这两者；
        /// 这里用"键是否存在"表达"问过"，值为答案（可为 null）。
        /// </summary>
        private readonly Dictionary<PlayerState, Hand> _askedHexerCancel =
            new Dictionary<PlayerState, Hand>();

        private static string AnswerKey(HandEffect effect, int variant)
            => variant == 0 ? effect.ToString() : $"{effect}#{variant}";

        /// <summary>登记一条"玩家/AI 已经答过"的抉择（本回合有效）。</summary>
        private void RecordAnswer(HandEffect effect, int variant, Hand choice)
        {
            string key = AnswerKey(effect, variant);
            if (choice == null) _answeredThisTurn.Remove(key);
            else _answeredThisTurn[key] = choice;
        }

        // ============================================================
        // 流程
        // ============================================================

        /// <summary>
        /// 开始当前玩家（<see cref="GameManager.CurrentPlayer"/>）的回合。
        /// 开局与每次结算完成后都要调用。
        /// </summary>
        /// <summary>
        /// 指定**谁先出招**（开局掷骰后由 UI 调用：点数大的先行动）。
        ///
        /// 必须在第一次 <see cref="BeginTurn"/> **之前**调用 ——
        /// <see cref="BeginTurn"/> 会读 <see cref="GameManager.CurrentPlayer"/>，
        /// 这里就是把对局对象里的先手改成指定的人。
        ///
        /// 传 null 或与当前一致时不改动（入门模式没有掷骰，走默认先手）。
        /// </summary>
        public void SetFirstPlayer(PlayerState player)
        {
            if (player == null || _game == null) return;
            if (_game.IsGameOver) return;

            _game.ForceActivePlayerForTest(player);
        }

        public void BeginTurn()
        {
            if (_game.IsGameOver)
            {
                Phase = BoardPhase.GameOver;
                Hint = WinMessage();
                return;
            }

            // ⚠️⚠️ **必须调一次 Core 的 `BeginTurn()`。**
            //
            //    它是"只属于本回合的标记"的**唯一复位点**：
            //      · `SuppressAttackDamage`（法师放弃攻击）
            //      · 法师的 `WantsToAbandonThisTurn`
            //      · 吟游诗人的 `TookDamageThisTurn` / `HealedThisTurn`
            //        / **`BardActivatedThisTurn`**
            //
            //    玩家实测 bug："吟游诗人在发动技能后再受伤没有触发面板" ——
            //    因为界面路径一直没调它，`BardActivatedThisTurn` 永远是 true，
            //    发动过一次之后就再也不弹面板。
            //    （无头路径由 `MatchRunner` 每次循环调，所以测试发现不了。）
            _game.BeginTurn();

            // ⚠️ **法师的状态：先结算"上一位的回合结束"**（燃烧 + 状态计数递减）。
            //
            //    为什么放在这里：`BeginTurn` 是**每次换人行动**的唯一入口，
            //    所以"上一位的回合结束"与"这一位的回合开始"天然相邻。
            //
            //    ⚠️ 用**独立哨兵字段**而不是读 `LastTurn` ——
            //       `LastTurn` 的赋值时机在部分路径上是"先 BeginTurn 再赋值"
            //       （盗贼偷窃那两处），读它会漏结算或重复结算。
            //       哨兵保证：同一次行动只结算一次回合结束。
            if (_statusTurnEndFor != null)
            {
                _game.ResolveStatusTurnEnd(_statusTurnEndFor);
                _statusTurnEndFor = null;

                if (_game.IsGameOver)
                {
                    Phase = BoardPhase.GameOver;
                    Hint = WinMessage();
                    return;
                }
            }

            ActivePlayer = _game.CurrentPlayer;

            // 诊断记录每回合清空 —— 它们是跨回合保留的，
            // 不清空会把"上一回合/上一局"的现场当成这一次的（我踩过两次）。
            ResetDiagnostics();

            // ⚠️ **法师的状态：这一位的回合开始**（中毒 / 感电）。
            //    玩家确认：中毒可能**直接把他打死、跳过其行动** ——
            //    所以结算后要重新读一次当前行动者。
            _game.ResolveStatusTurnStart(ActivePlayer);

            if (_game.IsGameOver)
            {
                Phase = BoardPhase.GameOver;
                Hint = WinMessage();
                return;
            }

            if (!ReferenceEquals(_game.CurrentPlayer, ActivePlayer))
            {
                ActivePlayer = _game.CurrentPlayer;
                _game.ResolveStatusTurnStart(ActivePlayer);

                if (_game.IsGameOver)
                {
                    Phase = BoardPhase.GameOver;
                    Hint = WinMessage();
                    return;
                }
            }

            // 注意：这里**不**清空 LastTurn。它的语义是"最近一次已结算的回合"，
            // 要一直保留到下一次结算为止 —— 界面需要在玩家为新回合做选择时，
            // 仍然显示上一回合打出了什么。
            SetupFirstStep();
        }

        /// <summary>
        /// **待结算"回合结束"的那位玩家**（法师的状态体系用）。
        ///
        /// 由"某位玩家的行动刚结算完"的那几处写入，由 <see cref="BeginTurn"/>
        /// 在下一次换人时消费并清空。
        ///
        /// 为什么用独立字段而不是读 <c>LastTurn</c>：
        /// 后者在盗贼偷窃那两条路径上是"先 BeginTurn 再赋值"，读它会错。
        /// </summary>
        private PlayerState _statusTurnEndFor;

        private void SetupFirstStep()
        {
            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();
            TargetPlayer = null;      // 目标玩家每个回合重新确认

            // 盗贼的"想偷"标记也属于"本回合的一次行动"，同样在第一步清掉
            _thiefWantsSteal = false;

            // ⚠️ **法师的"本回合放弃攻击"标记也必须清掉** ——
            //    它的语义是"这一次行动放弃攻击"，
            //    留在身上会让下一回合的普通攻击也被静默抑制（打不出伤害）。
            if (ActivePlayer?.Character
                is FingerDiceDuel.Characters.SpecialLogics.WizardLogic mageReset)
            {
                mageReset.WantsToAbandonThisTurn = false;
            }

            // ⚠️ 赌博点数属于"本回合的一次行动"，**回合第一步**就要清掉。
            //
            //    我第一版放在 BeginCharacterChoices（收集抉择时），
            //    但那条路只有"点了目标手"才会走 —— 测试立刻抓到
            //    "新回合开始时点数还是上一回合的 3"。
            //    放在这里才能保证每个回合开局就干净（诊断字段跨回合残留我踩过多次）。
            _pendingGambleRoll = 0;

            // ⚠️ **法师"放弃攻击"已作答**的标记也必须每回合复位。
            //
            //    它只用来防止"确认目标"时把已经答完的「是否放弃攻击」
            //    再问一遍（玩家实测的无限循环）。跨回合残留会让下一回合
            //    跳过角色抉择，所以这里必须清干净。
            _wizardDeclineAnswered = false;

            // ⚠️ 必须把**抉择队列也复位**。
            //
            // 曾经这里只清了 _shieldChoices，留下 _pendingRequestIndex 不管。
            // 上个回合接受过一个抉择（例如游侠闪避）后 idx 会变成 1；
            // 下个回合 BeginCharacterChoices 把队列清空重建、长度可能只有 1，
            // 而 idx 仍是 1 -> `1 >= 1` 被判成"全部问完了"，
            // 于是**新回合的强化/闪避提示根本不弹**（真实 bug，玩家实测反馈）。
            _pendingRequests.Clear();
            _pendingRequestIndex = 0;
            _pendingChoiceCandidates.Clear();
            _pendingChoiceDecider = null;

            // ⚠️ 只在**真正的回合边界**清空 sink。
            //
            // sink 是跨回合保留的；而 `PreTurnChoiceReader.Read` 在抉择器没给结果时
            // 会回读 sink。于是上一回合的旧答案会被当成"本回合的决定"，
            // 表现为"AI / 玩家明明选了技能，结算却按旧记录走" ——
            // 玩家实测反馈的"AI 从不发动技能"就是这个原因。
            //
            // 但**回合中途不能清**：牧师的 4/6/8 有两段抉择，中间隔着"选目标手" ——
            //   ① 选目标手之前：攻击还是回复（存在 sink 里）
            //   ② 选目标手之后：回复给谁 / 结算
            // 而第 ② 段开头（CommitMove -> BeginCharacterChoices -> 这里）
            // 会把第 ① 段的答案抹掉，结算时 ChoseToHeal() 读到 null，
            // 表现为"牧师选了回复却按攻击结算"（真实 bug）。
            if (IsTurnBoundary)
            {
                _sinkA?.ClearChoices();
                _sinkB?.ClearChoices();
                _answeredThisTurn.Clear();
                MarkTurnBoundaryCrossed();
            }

            // AI 的抉择记录同样只在"本回合"有效
            ClearAiDecisions();

            // ⚠️ 注意：_aiTrace（诊断用）**故意不在这里清空**。
            //
            // 它是排查"AI 不发动技能"的唯一线索，而它记录的时机是
            // "AI 作答"（在 CommitMove 之后、ExecuteResolvedTurn 之前），
            // 紧接着回合边界就会走到这里 —— 一旦清空，
            // 玩家下次刷新界面时看到的就是"（无）"，
            // 会误判成"AI 根本没被问到"（我自己就因此误判了一轮）。
            // 让它跨回合累积，定位完再移除即可。

            IReadOnlyList<LegalMove> moves = _game.GetLegalMoves(ActivePlayer);
            if (moves.Count == 0)
            {
                Phase = BoardPhase.GameOver;
                Hint = "没有可行的运算。";
                return;
            }

            Phase = BoardPhase.ChoosingAttackerHand;
            Hint = $"轮到 {ActivePlayer.Name}：请选择要用自己哪一只手。";
            AppendLockedHandNote();
        }

        private void AppendLockedHandNote()
        {
            var parts = new List<string>(2);
            if (ActivePlayer.LeftHand.IsLocked) parts.Add("左手被锁");
            if (ActivePlayer.RightHand.IsLocked) parts.Add("右手被锁");
            if (parts.Count > 0) Hint += "（" + string.Join("、", parts) + "）";
        }

        /// <summary>这张手牌当前是否可以被点击。</summary>
        public bool CanClick(Hand hand)
        {
            if (hand == null || _game.IsGameOver) return false;
            if (ActivePlayer == null) return false;

            switch (Phase)
            {
                case BoardPhase.ChoosingDefenseShield:
                    return _shieldChoices.Contains(hand);

                case BoardPhase.ChoosingPreTurnChoice:
                // "选目标手之前"的抉择：同样只能点候选手牌
                case BoardPhase.ChoosingPreTargetChoice:
                    return IsChoiceCandidate(hand);

                case BoardPhase.ChoosingAttackerHand:
                    // ⚠️ 必须用 `Hand.CanBeOperated`（= 未锁 **且** 未暗置），
                    //    不能只查 `!IsLocked`。
                    //
                    //    玩家实测的 bug：猎人**用已暗置（伏击）的手主动发起了运算**，
                    //    于是伏击被破除、那只手随即变成合法的被加数，
                    //    接着对手就能拿它当加数了。
                    //
                    //    根因是**两层口径不一致**：
                    //      领域层（GetLegalMoves / IsLegalMove）用的是 `CanBeOperated`；
                    //      界面这层还停在 `!IsLocked` —— 只认得"被锁"。
                    //    所以暗置的手在领域层已被拒绝，**界面上却仍然点得动**。
                    //
                    //    玩家原话："伏击的手不能主动发起运算，看上去和被锁是一样的。"
                    return ActivePlayer.Owns(hand) && hand.CanBeOperated;

                case BoardPhase.ChoosingGamble:
                    // 赌博阶段**只等点「开赌」**，手牌本身不再接受点击。
                    // （运算源已经选好了；再点手牌没有意义，容易误触。）
                    return false;

                case BoardPhase.ChoosingStealTarget:
                    // 盗贼：可以点**任何其他人**的手（无限距离、可以偷队友）。
                    // 自己除外 —— 偷自己没有意义。
                    return !ActivePlayer.Owns(hand);

                case BoardPhase.ChoosingHealthSwapTarget:
                {
                    // 妖术师：可以点**任何其他人**的手（体力与自己不同的才有意义）。
                    // 自己除外 —— "把场上一人的体力改成与自己一致"，自己不在"场上"。
                    PlayerState owner = OwnerOf(hand);
                    if (owner == null) return false;
                    if (ReferenceEquals(owner, ActivePlayer)) return false;

                    return owner.IsAlive && owner.Health != ActivePlayer.Health;
                }

                case BoardPhase.ChoosingTargetHand:
                {
                    // 自己已选的手也允许再点一次（表示取消选择）
                    if (ReferenceEquals(hand, SelectedHand)) return true;

                    // 被加数可以是**任何其他玩家**的手（含队友）——
                    // 玩家确认："A 可以选择 B/C/D 任何一个数字作为被加数"，
                    // 别人的数字不会被改动，只有行动方自己的手变成结果值。
                    //
                    // ⚠️ **不能要求"目标手未被锁定"** ——
                    //    锁定只禁止那只手**发起**运算（`GetLegalMoves` 里过滤运算源），
                    //    它**仍然可以作为被加数**。
                    //
                    //    我上一轮在这里多加了 `!hand.IsLocked`，于是
                    //    AI 选中一只被锁的手时 `ClickHand` 会被拒 ->
                    //    `MakeAutomaticMove` 返回 false -> **对局卡死**
                    //    （玩家实测"经典里战士攻击卡住了"，批量测试复现）。
                    if (!ActivePlayer.Owns(hand)) return true;

                    // ⚠️ **魔术师例外：他可以碰自己的另一只手。**
                    //
                    //    玩家实测的 bug："魔术师的手选择后无法触碰自己的手运算"。
                    //    上面那句 `!ActivePlayer.Owns(hand)` 把它一手挡住了 ——
                    //    领域层（GetLegalMoves / IsLegalMove）早就放行，
                    //    界面这层又漏了一处（与"暗置的手还能点"同一类错误）。
                    //
                    //    仍然不许点**已选的那只**（上面已单独返回），
                    //    所以隐含地保证了"加数与被加数不是同一只手"。
                    var selfAddend = ActivePlayer.Character as ISelfAddendProvider;
                    if (selfAddend != null && selfAddend.AllowsSelfAddend)
                        return hand.CanBeOperated;

                    return false;
                }

                case BoardPhase.ChoosingTargetPlayer:
                {
                    // 第 ③ 步：点**任意可攻击敌人的手**即确认打他。                    //
                    // ⚠️ 不能只接受 `TargetPlayer.Owns(hand)` ——
                    //    TargetPlayer 可能是"被加数的手主"（例如队友），
                    //    那样玩家**没有任何手可点**，对局直接卡死
                    //    （玩家实测："选择和队友运算出攻击数字后，
                    //      再次选择攻击目标会卡死"）。
                    PlayerState owner = OwnerOf(hand);
                    if (owner == null) return false;
                    if (ReferenceEquals(owner, ActivePlayer)) return false;

                    return CanBeAttacked(owner);
                }

                default:
                    return false;
            }
        }

        /// <summary>
        /// 进入"选目标手**之前**的角色抉择"（若角色有这种问题）。
        ///
        /// 目前只有牧师用：运算出 4/6/8 时先问"当攻击打还是当回复用"。
        /// 玩家要求的顺序就是这样 —— 先问，再选目标手。
        ///
        /// 返回 true 表示已经停在 <see cref="BoardPhase.ChoosingPreTargetChoice"/>。
        /// </summary>
        /// <summary>
        /// 进入"运算结果确定后、确认目标玩家之前的角色抉择"（若角色有这种问题）。
        ///
        /// 目前只有牧师用：运算出 4/6/8 时先问"当攻击打还是当回复用"。
        ///
        /// ⚠️ **调用时机**：必须在"运算源（目标手）已经选定"之后。
        /// 因为结果 = 自己那只手 + 对手那只手，运算源没定时结果是不确定的。
        /// 玩家要求的顺序就是：点自己的手 -> 点对手的手 -> 弹攻击/回复 -> 选目标。
        ///
        /// 曾经在第 ① 步（点自己的手）就问，结果只能用替身猜，
        /// 出现"该提示不提示"与"提示的数字不对"两类真实 bug。
        ///
        /// 返回 true 表示已经停在 <see cref="BoardPhase.ChoosingPreTargetChoice"/>。
        /// </summary>
        private bool BeginPreTargetChoicesIfAny()
        {
            var provider = ActivePlayer?.Character as ITargetHandChoiceProvider;
            if (provider == null) return false;
            if (SelectedHand == null || _selectedTarget == null) return false;

            // 运算源已定 -> 用**真实**的两只手构造上下文，结果准确。
            var context = new PreTurnChoiceContext(ActivePlayer, TargetPlayer ?? Defender,
                                                   SelectedHand, _selectedTarget);

            _pendingRequests.Clear();
            _pendingRequestIndex = 0;
            _pendingChoiceCandidates.Clear();
            _pendingChoiceDecider = null;

            provider.CollectPreTargetChoices(context, _pendingRequests);

            // 过滤空候选（牧师 4/6/8 的"攻击还是回复"等）。
            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
                if (_pendingRequests[i].Candidates == null
                    || _pendingRequests[i].Candidates.Count == 0)
                    _pendingRequests.RemoveAt(i);

            if (_pendingRequests.Count == 0) return false;

            _preTargetChoiceActive = true;
            AdvanceCharacterChoice();
            return Phase == BoardPhase.ChoosingPreTargetChoice;
        }

        /// <summary>当前是否停在"选目标手之前的抉择"。</summary>
        private bool _preTargetChoiceActive;

        /// <summary>
        /// 回答完游侠强化 / 战士换射程后，仍需回到"确认攻击目标"。
        /// 与 `_preTargetChoiceActive` 分开：这些抉择本身仍属于普通
        /// `ChoosingPreTurnChoice`（阶段名不改成 PreTarget）。
        /// </summary>
        private bool _returnToTargetAfterChoice;

        /// <summary>
        /// 盗贼已经按过「偷窃」按钮（本回合）。
        ///
        /// ⚠️ 为什么需要一个标记而不是只看 Phase：
        ///    点完自己的手后控制器会**自动推进**到"选加数"，
        ///    那时玩家直接点别人的手也应该算偷窃。
        ///    <see cref="BeginSteal"/> 置位，<c>SetupFirstStep</c> 清掉。
        /// </summary>
        private bool _thiefWantsSteal;

        /// <summary>
        /// 盗贼 AI 走到哪一步了（**独立字段**，不会被别的诊断覆盖）。
        ///
        /// ⚠️ 存在的理由：上一轮我把它写进 LastAiMoveDiagnostic，
        ///    而普通运算的 FAIL-STEP2 会**后写覆盖**它 ——
        ///    读到的永远是最后那一条，等于没打诊断（白查两轮）。
        /// </summary>
        public string ThiefAiTrace => _thiefAiTrace;

        private string _thiefAiTrace = "";

        /// <summary>
        /// 当前是否处在**真正的回合边界**（可以清空"仅本回合有效"的抉择记录）。
        ///
        /// 判据是"**行动方变了**"，而不是"运算源为空"。
        ///
        /// 为什么不用 `_pendingActedHand == null`：那是一个**脆弱的间接判据** ——
        /// 它是否成立取决于调用顺序（`CommitMove` 恰好在 `BeginCharacterChoices`
        /// 之前给 `_pendingActedHand` 赋值）。一旦顺序变化，"回合中途"就会被
        /// 误判成"新回合"，把同回合刚答的抉择清掉，表现为
        /// "牧师选了回复却按攻击结算"。
        ///
        /// 用"行动方是否变化"表达**意图本身**：与调用顺序无关，永远正确。
        /// </summary>
        private bool IsTurnBoundary => !ReferenceEquals(_lastBoundaryPlayer, ActivePlayer);

        /// <summary>上一次记录"回合边界"时的行动方。</summary>
        private PlayerState _lastBoundaryPlayer;

        /// <summary>标记"已跨过回合边界"（在清空之后调用）。</summary>
        private void MarkTurnBoundaryCrossed() => _lastBoundaryPlayer = ActivePlayer;

        /// <summary>这张手牌是否是当前已选中的运算源。</summary>
        public bool IsSelected(Hand hand) => hand != null && ReferenceEquals(hand, SelectedHand);

        // ============================================================
        // 合法攻击目标
        //
        // 判据是"**有没有合法的攻击目标**"，而不是"对手是不是刺客" ——
        // 刺客的潜行只是**当前唯一的**一种"令自己无法成为目标"的原因。
        // 日后的多人模式还有"攻击距离不足"等，都走同一判据。
        // ============================================================

        /// <summary>
        /// 这次攻击目前申请到的**距离覆盖**（-1 = 用角色基础距离）。
        ///
        /// 目前两个来源（都是"本次攻击距离无限"）：
        ///   · 战士碎盾换射程（<see cref="HandEffect.FighterRangeBreak"/>）
        ///   · 游侠强化射击（<see cref="HandEffect.RangerPowerShot"/>）
        ///
        /// 判定必须**同时看 sink 与控制器记录**：
        /// 人类玩家的答案在 sink 里，AI 的答案在 <c>_answeredThisTurn</c> 里 ——
        /// 只看一边会让另一边失效（本项目踩过多次同类坑）。
        ///
        /// ⚠️ 只在**运算结果是攻击**时才放宽距离：
        /// 非攻击（护盾 / 回复 / 无效果）本来就不需要攻击距离。
        /// </summary>
        private int CurrentRangeOverride()
        {
            if (!_pendingIsAttack) return -1;
            if (ActivePlayer == null) return -1;

            if (RangeBreakChosen(HandEffect.FighterRangeBreak)) return int.MaxValue;
            if (RangeBreakChosen(HandEffect.RangerPowerShot)) return int.MaxValue;

            return -1;
        }

        /// <summary>某个"换射程"的抉择是否被选择了"发动"。</summary>
        private bool RangeBreakChosen(HandEffect effect)
        {
            // ① 人类玩家：答案在 sink 里（显式记录，没记录 = 放弃）
            Hand viaSink = PreTurnSinkFor(ActivePlayer)?.GetExplicitChoice(effect);
            if (viaSink != null) return true;

            // ② AI：答案在控制器自己记的"本回合已答"里
            if (_answeredThisTurn.TryGetValue(AnswerKey(effect, 0), out Hand viaAi))
                return viaAi != null;

            return false;
        }

        /// <summary>某位玩家当前是否可以成为攻击目标。
        public bool CanBeAttacked(PlayerState player)
        {
            if (player == null) return false;
            if (ActivePlayer == null) return false;
            if (ReferenceEquals(player, ActivePlayer)) return false;

            // ① 玩家要求：**所有模式队友都是合法攻击目标**（可以打队友）。
            //    1v1 没有队友；2v2 / 8 人阵营混战都可以主动打自己人。

            // ② 必须在**攻击距离**内（咒术师 / 牧师无限，其余 1）。
            //    距离按座位环算，阵亡者从环上摘掉 —— 都在 GameManager 里。
            //
            //    "本次攻击距离无限"由两个抉择提供（战士碎盾 / 游侠强化），
            //    这里用**实时计算**而不是缓存字段 —— 因为玩家的答案随时可能到达
            //    （人类点按钮、AI 被代答），缓存会读到过期值。
            if (!_game.CanReach(ActivePlayer, player, CurrentRangeOverride()))
                return false;

            // ③ 角色可以令自己免疫（刺客潜行 -> IsImmuneToAttack）。
            //    控制器不认识任何具体角色：Assistant 层不能引用 Characters 层。
            if (player.Character is IAttackTargetRule rule && rule.IsImmuneToAttack(player))
                return false;

            return true;
        }

        /// <summary>
        /// 当前是否存在**合法的攻击目标**。
        ///
        /// 1v1 里只有一位对手，所以等价于"对手能不能被打"；
        /// 目前**只有刺客潜行**会导致没有合法目标。
        /// </summary>
        public bool HasAnyLegalTarget
        {
            get
            {
                if (ActivePlayer == null) return false;

                // 遍历**所有**其他玩家（不再只看唯一对手）——
                // 2v2 / 8 人局里可能有多位对手，且距离会筛掉一部分。
                IReadOnlyList<PlayerState> all = _game.Players;
                for (int i = 0; i < all.Count; i++)
                    if (CanBeAttacked(all[i])) return true;

                return false;
            }
        }

        /// <summary>
        /// 界面是否应把**「放弃攻击」**作为出口。
        ///
        /// 修正（玩家确认）：判据是"**有没有任何合法攻击目标**"；
        /// 队友也是合法目标，所以只有所有其他人都不可打时才出现放弃。
        /// 曾经这里只看当前的 <see cref="Defender"/>，2v2 里会因为
        /// "另一位对手在范围内"而漏判 -> 该放弃时不给按钮。
        ///
        /// 所以判据是 <see cref="HasAnyLegalTarget"/>，不是"对手是刺客"。
        /// </summary>
        public bool DefenderCannotBeAttacked
            => Phase == BoardPhase.ChoosingTargetPlayer && !HasAnyLegalTarget;

        // ============================================================
        // 盗贼：偷窃的三步流程
        // ============================================================
        //
        // 玩家给的交互："操作为先点击自己的手，点击右侧按钮偷窃，
        //              再点击他人的手完成偷窃。"

        /// <summary>
        /// 本回合能不能偷窃（上回合发起过就不能）—— UI 据此显示「偷窃」按钮。
        ///
        /// ⚠️ 允许在**两个阶段**都显示按钮：
        ///   · <see cref="BoardPhase.ChoosingAttackerHand"/> —— 刚点完自己的手
        ///   · <see cref="BoardPhase.ChoosingTargetHand"/> —— 控制器点完自己的手后
        ///     **会自动推进**到这里（见 ClickHand 的 ChoosingAttackerHand 分支）
        ///
        /// 我第一版只认前者，于是按钮**永远显示不出来**
        /// （测试当场抓到：phase=ChoosingTargetHand）。
        /// 两个阶段都允许，同时也保留了"再点自己的手可取消"这个既有手势。
        /// </summary>
        public bool CanSteal
            => (Phase == BoardPhase.ChoosingAttackerHand
                || Phase == BoardPhase.ChoosingTargetHand)
               && ActivePlayer?.Character is ThiefLogic
               && SelectedHand != null
               && _game.CanStealOn(ActivePlayer);

        /// <summary>
        /// 玩家点了「偷窃」按钮：进入"选被偷的手"这一步。
        ///
        /// 推文提示里带上"偷来的数字不能大于当前这只手"——
        /// 否则玩家不知道判定规则，点了没反应会以为游戏坏了。
        /// </summary>
        public bool BeginSteal()
        {
            if (!CanSteal) return false;

            // ⚠️ 这个标记必须与阶段**同时**设置：
            //    点完自己的手之后控制器会自动推进到"选加数"，
            //    所以"点别人的手"这件事在**两个阶段**都可能发生。
            //    只靠 Phase 判断会漏掉"自动推进后直接点别人"那条路。
            _thiefWantsSteal = true;

            Phase = BoardPhase.ChoosingStealTarget;
            Hint = $"盗贼：请点一只手来偷（无限距离，可以偷队友）。" +
                   $"偷来的数字不大于 {SelectedHand.Value} 才会生效。";
            return true;
        }

        /// <summary>「偷窃」按钮的入口（与 <see cref="Gamble"/> 对称）。</summary>
        public bool Steal() => BeginSteal();

        /// <summary>
        /// 完成偷窃 —— 在 <see cref="BoardPhase.ChoosingStealTarget"/> 阶段点手牌时调用。
        /// </summary>
        private bool CompleteSteal(Hand target)
        {
            if (Phase != BoardPhase.ChoosingStealTarget) return false;
            if (target == null || SelectedHand == null) return false;

            // 不能偷自己
            if (ActivePlayer != null && ActivePlayer.Owns(target)) return false;

            try
            {
                LastTurn = _game.ExecuteSteal(ActivePlayer, SelectedHand, target);
            _statusTurnEndFor = LastTurn?.Actor;       // 法师状态：换人时结算回合结束

                // ⚠️ 偷出**攻击数字**时，伤害不能在这里结算 ——
                //    玩家要求"要再次点击目标才可以攻击"，
                //    所以先进入「选打谁」，与普通运算的攻击同一条流程。
                //    ResolveSteal 已经把这次攻击登记成"待选目标"。
                if (_game.StealAttackAwaitingTarget)
                {
                    Phase = BoardPhase.ChoosingTargetPlayer;
                    TargetPlayer = null;
                    Hint = $"{ActivePlayer.Name}（盗贼）偷到了 " +
                           $"{_game.PendingStealAttackValue}：请选择攻击目标。";

                    if (!BeginTargetPlayerChoice())
                    {
                        GiveUpAttack();
                        BeginTurn();
                    }
                    return true;
                }

                // ⚠️ 必须调 **BeginTurn()**，不能用 SetupFirstStep()。
                //
                //    `ActivePlayer` 是**缓存字段**，只有 BeginTurn() 会把它
                //    刷成新的 CurrentPlayer（见 BeginTurn 里的赋值）。
                //    普通运算的结算收尾走的也是 BeginTurn()。
                //
                //    我第一版只调了 SetupFirstStep()，于是
                //    `game.CurrentPlayer` 已经换成对手、而 `ActivePlayer`
                //    还停在盗贼身上 —— 表现为**盗贼像连动两个回合**
                //    （玩家实测："偷窃之后会跳过对方回合"）。
                BeginTurn();
                ResolvePendingAiChoices();
            }
            catch (System.InvalidOperationException ex)
            {
                LastIllegalOperation = ex.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// **盗贼 AI**（玩家给的策略）：
        ///
        /// > "能成功偷到并触发**高价值数字**就偷；不能偷则尽量
        /// >   **合成更大点数的高价值数字**，**8 和 9 价值上升**。"
        ///
        /// 所以判据是**两段**：
        ///   ① 存在一个"判定生效（原先 ≥ 偷来的）且偷来的是**高价值数字**"
        ///      的目标 -> 偷其中**最好**的那个
        ///   ② 否则**不偷**，回去走正常运算 ——
        ///      而正常运算的偏好由盗贼的价值表负责
        ///      （8 > 9 > 6 > 5 > 4，见 HandValueProfiles）
        ///
        /// ⚠️ 我第一版是"只要有得分 > 0 的机会就偷"，
        ///    那会为了一个无效果的 3 放弃正常运算 —— 与玩家策略不符。
        ///
        /// 返回 true 表示已经用掉了这个回合。
        /// </summary>
        private bool TryAiThiefSteal()
        {
            // ⚠️ 用**独立字段**记录走到哪一步 —— 上一轮我把它写进
            //    LastAiMoveDiagnostic，结果被后续的普通运算诊断**覆盖**，
            //    等于没打诊断（白查两轮）。所以这里单独存一份。
            _thiefAiTrace = "enter";

            if (!(ActivePlayer?.Character is ThiefLogic))
            {
                _thiefAiTrace = "exit:不是盗贼";
                return false;
            }

            if (!_game.CanStealOn(ActivePlayer))
            {
                _thiefAiTrace = $"exit:冷却 turn={_game.TurnCount} last={_game.LastStealTurn}";
                return false;
            }

            IReadOnlyList<Hand> mine = ActivePlayer.Hands;

            Hand bestMine = null;
            Hand bestTheirs = null;
            int bestScore = -1;
            int scanned = 0;

            IReadOnlyList<PlayerState> all = _game.Players;
            for (int m = 0; m < mine.Count; m++)
            {
                Hand myHand = mine[m];
                if (myHand == null || myHand.IsLocked) continue;

                for (int p = 0; p < all.Count; p++)
                {
                    PlayerState owner = all[p];
                    if (owner == null || !owner.IsAlive) continue;
                    if (ReferenceEquals(owner, ActivePlayer)) continue;   // 偷自己非法

                    IReadOnlyList<Hand> theirs = owner.Hands;
                    for (int t = 0; t < theirs.Count; t++)
                    {
                        Hand theirHand = theirs[t];
                        if (theirHand == null) continue;

                        scanned++;

                        // ① 判定必须生效（原先 ≥ 偷来的）
                        if (myHand.Value < theirHand.Value) continue;

                        // ② 而且偷来的必须是**高价值数字**（4/5/6/8/9）——
                        //    偷一个 3 只会白白用掉这个回合（还没效果）。
                        int score = StealScore(theirHand.Value, myHand.Value);
                        if (score <= 0) continue;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestMine = myHand;
                            bestTheirs = theirHand;
                        }
                    }
                }
            }

            if (bestMine == null)
            {
                _thiefAiTrace = $"exit:没有划算的偷窃 scanned={scanned} " +
                                $"L={ActivePlayer?.LeftHand?.Value} R={ActivePlayer?.RightHand?.Value}";
                return false;
            }

            _thiefAiTrace = $"选了 {bestMine.Value}->{bestTheirs.Value} score={bestScore}";

            if (!ClickHand(bestMine))
            {
                _thiefAiTrace += " / exit:ClickHand 失败 phase=" + Phase;
                return false;
            }

            if (!BeginSteal())
            {
                _thiefAiTrace += " / exit:BeginSteal 失败 phase=" + Phase +
                                 " canSteal=" + CanSteal;
                return false;
            }

            try
            {
                LastTurn = _game.ExecuteSteal(ActivePlayer, bestMine, bestTheirs);
                _statusTurnEndFor = LastTurn?.Actor;   // 法师状态：换人时结算回合结束
                BeginTurn();          // 必须刷新 ActivePlayer（见 CompleteSteal 的注释）
                ResolvePendingAiChoices();
                _thiefAiTrace += " / 偷窃成功";
            }
            catch (System.InvalidOperationException ex)
            {
                _thiefAiTrace += " / exit:ExecuteSteal 抛异常 " + ex.Message;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 偷来这个数字值多少（盗贼 AI 用）。
        ///
        /// **只有高价值数字才有分**（4/5/6/8/9）——
        /// 1/2/3/7 返回 0，表示"不值得为它用掉这个回合"。
        ///
        /// 梯度按玩家要求："**8 和 9 价值上升**"，
        /// 与盗贼价值表里的 8 > 9 > 6 > 5 > 4 保持一致。
        /// </summary>
        private static int StealScore(int stolen)
            => StealScore(stolen, stolen);

        /// <summary>
        /// 偷窃的划算程度（玩家给的两条规则）。
        ///
        /// 规则原话：
        ///   · "盗贼在**没法凑出高价值数字**时，用**小数字偷大数字**的价值**小幅度提升**（偷 8、9）"
        ///   · "在没法凑出高价值数字时用**大数字偷到有效数字**的价值**大幅度提升**"
        ///
        /// 两条都在说同一件事的两端：**这一偷让我的手值提升了多少**。
        ///   · 小数字（1/2/3）本来没有价值 -> 偷到 8/9 是"从废牌变好牌"，
        ///     提升很大，给**小幅**加分（玩家指定"小幅度"）。
        ///   · 大数字（7/8/9）本来就没法凑出高价值（因为它们去运算只会变小），
        ///     用它偷到一个**有效数字**（4/5/6/8/9）= 把"没法用的好牌"变现，
        ///     给**大幅**加分（玩家指定"大幅度"）。
        ///
        /// \u26a0\ufe0f 评分仍然以"偷来的数字本身值多少"为主（<paramref name="stolen"/> 那一栏），
        ///    下面那两项只是**加成**，不会把"偷 3"变成值得做的事
        ///    （`Hand.GetEffectType(3)` 仍是 None -> 基础分 0，加成也只在有效时给）。
        /// </summary>
        private static int StealScore(int stolen, int myCurrent)
        {
            int baseScore;
            switch (Hand.GetEffectType(stolen))
            {
                case HandEffectType.Attack8: baseScore = 92; break;   // 最高攻击
                case HandEffectType.Heal: baseScore = 86; break;      // 回复 9
                case HandEffectType.Attack6: baseScore = 76; break;
                case HandEffectType.Shield: baseScore = 66; break;    // 偷来 5：整盾还是碎盾看原先
                case HandEffectType.Attack4: baseScore = 62; break;
                default: baseScore = 0; break;                        // 1/2/3/7 -> 不值得偷
            }

            if (baseScore == 0) return 0;      // 偷来的是废数字：任何加成都不该救它

            int bonus = 0;

            // ① 用**小数字**去偷**大数字**：小幅提升（玩家给了上界"小幅度"）
            //    1/2/3 本来是废牌，能换到 8/9 这种高价值是纯赚。
            if (myCurrent <= 3 && stolen >= 8) bonus += 8;

            // ② 用**大数字**去偷到**有效数字**：大幅提升
            //    7/8/9 留在手上只能越算越小，换成一个有效数字是"把死牌变现"。
            if (myCurrent >= 7 && baseScore > 0) bonus += 22;

            return baseScore + bonus;
        }

        /// <summary>玩家点击了一张手牌。返回 true 表示这次点击被接受。</summary>
        public bool ClickHand(Hand hand)
        {
            // 诊断：记录每一次点击（含阶段与结果）——
            // "点了没反应"这类问题光看状态字段定位不到是哪一步拒的。
            _advanceTrace.Add($"CLICK h={hand?.Value.ToString() ?? "N"}" +
                              $" phase={Phase}" +
                              $" sel={SelectedHand?.Value.ToString() ?? "N"}" +
                              $" tgt={_selectedTarget?.Value.ToString() ?? "N"}" +
                              $" roll={_pendingGambleRoll}");
            // ⚠️ **强制赌博时点他人的手要明确拒绝并说明原因**（玩家要求：
            //    "弹提示并拒绝"）。
            //
            //    不能静默忽略 —— 玩家会以为"点了没反应 = 游戏坏了"。
            //    也不能允许选中 —— 那等于绕过了强制赌博。
            //
            //    ⚠️⚠️ 但这条守卫**只能在"还没开始行动"时生效**
            //    （ChoosingAttackerHand / ChoosingTargetHand）。
            //
            //    我第一版没限阶段，于是掷出攻击后的"**选打谁**"
            //    （ChoosingTargetPlayer）也被拦了 —— 玩家实测：
            //    "开赌投出 6 后无法点击他人攻击，攻击选择和运算选择他人
            //      一起被锁死"。而选打谁本来就**必须**点他人的手。
            bool stillChoosingOperation =
                Phase == BoardPhase.ChoosingAttackerHand
                || Phase == BoardPhase.ChoosingTargetHand;

            if (hand != null
                && stillChoosingOperation
                && ActivePlayer?.Character is GamblerLogic
                && ActivePlayer.MustGamble
                && !ActivePlayer.Owns(hand))
            {
                Hint = $"{ActivePlayer.Name}（赌徒）上一回合做了正常运算，" +
                       $"本回合必须赌博 —— 不能选别人的手，请点自己的一只手后按「开赌」。";
                _log.Add($"{ActivePlayer.Name} 本回合必须赌博，拒绝了选择他人手牌的点击。");
                return false;
            }

            if (!CanClick(hand)) return false;

            switch (Phase)
            {
                // ---- 盗贼：第三步，点"要被偷的那只手"----
                // 放在最前面，因为它与"选运算源 / 选加数"完全无关
                // （偷窃是**放弃运算**的另一条路）。
                case BoardPhase.ChoosingStealTarget:
                    return CompleteSteal(hand);

                case BoardPhase.ChoosingDefenseShield:
                    ApplyDefenseChoice(hand);
                    // 选盾同时也是一个通用抉择的结果，写进 sink 供 AttackContext 回读
                    SinkFor(Defender)?.SetChoice(HandEffect.ShieldBlock, hand);
                    ExecuteResolvedTurn();
                    return true;

                case BoardPhase.ChoosingPreTurnChoice:
                // "选目标手之前"的抉择：走同一条回答路径
                case BoardPhase.ChoosingPreTargetChoice:
                    ApplyPreTurnChoiceAndContinue(hand);
                    return true;

                case BoardPhase.ChoosingHealthSwapTarget:
                {
                    // 妖术师：点谁的手就把谁的体力改成与自己一致，然后结束回合
                    PlayerState owner = OwnerOf(hand);
                    return ApplyHealthSwapAndEndTurn(owner);
                }

                case BoardPhase.ChoosingAttackerHand:
                    SelectedHand = hand;

                    // ⚠️ **赌徒走另一条路**：点自己的手之后不进"选被加数"，
                    //    而是停在"等点开赌"。
                    //
                    //    玩家确认的流程："先点击自己一只手，然后点击右侧按钮开赌"
                    //    —— 赌博**不需要**点他人的手。
                    // ⚠️ **赌徒的两条路**（玩家确认的规则）：
                    //
                    //   · **本回合必须赌博**（上一回合做了正常运算）
                    //     -> 点自己的手之后直接进赌博阶段，不给别的选择
                    //   · **否则可以自选**：点「开赌」就赌，点他人的手就走正常运算
                    //
                    //   我第一版**无条件**进赌博阶段，于是"已经赌过、本可以正常运算"
                    //   的那些回合看起来也像被强制（玩家实测报告：
                    //   "上一回合已经赌过了但仍被强制赌博"）。
                    //   MustGamble 只改了提示文案，没给玩家另一条路 —— 那是设计缺口。
                    if (ActivePlayer?.Character is GamblerLogic
                        && !_allowNormalOperationForTest
                        && ActivePlayer.MustGamble)
                    {
                        // 诊断：赌徒的强制状态与"上一次结算是不是赌博"一起打出来。
                        // 玩家报"上一回合已经赌过了但仍被强制赌博"——
                        // 光看界面提示分不清是 MustGamble 没被清，
                        // 还是"赌徒本来就总是进赌博流程"（后者是设计）。
                        _advanceTrace.Add(
                            $"GAMBLER-ENTER must={ActivePlayer.MustGamble}" +
                            $" lastWasGamble={LastTurn?.WasGamble}" +
                            $" lastRoll={LastTurn?.GambleRoll}");

                        Phase = BoardPhase.ChoosingGamble;
                        _log.Add($"{ActivePlayer.Name}（赌徒）选定了" +
                                 $"{SideLabel(hand, ActivePlayer)}（{hand.Value}）。" +
                                 $"本回合必须赌博。");
                        Hint = $"{ActivePlayer.Name}（赌徒）：上一回合做了正常运算，" +
                               $"本回合必须赌博 —— 点「开赌」掷 1d9。";
                        return true;
                    }

                    // ⚠️ **不在这里**问"选目标手之前"的问题。
                    //
                    // 玩家要求的顺序是：
                    //   ① 点自己的手  ② 点对手的手（作为运算源）
                    //   ③ 运算出 4/6/8 -> 弹「攻击 / 回复」  ④ 再选攻击/回复的目标
                    //
                    // 关键：**必须等到第 ② 步之后**才问。因为结果取决于运算源，
                    // 而"选目标手"就是选运算源 —— 第 ① 步之后还不知道会出 4 还是 6。
                    //
                    // 曾经在第 ① 步就问，于是"运算出 N"只能用**替身**或**候选里的第一个**
                    // 去猜。玩家实测：牧师手 5/8、战士手 6/8 时点自己的 8，
                    // 打战士的 6 出 4、打 8 出 6 —— 提示却写死成「回复 4」（真实 bug）。
                    Phase = BoardPhase.ChoosingTargetHand;
                    Hint = $"已选择 {SideLabel(hand, ActivePlayer)}（{hand.Value}）。" +
                           $"请选择要打 {Defender.Name} 的哪一只手（再点一次已选的手可取消）。";

                    // ⚠️ 这里**不填 `_pendingChoiceCandidates`**。
                    //
                    //    我曾经为了"让能点的手都亮起来"把这一步的所有可点手
                    //    都填进候选，结果它们全走了**金色（Highlighted）**分支，
                    //    与"选中"撞成同一个颜色。玩家实测反馈：
                    //    "现在可点的高光变成黄色外框了，应该保持：
                    //      选中的手是黄色高光、可以选的手是白色高光"。
                    //
                    //    正确做法是让**两层各自负责**：
                    //      · 候选（金色）= 当前抉择真正在问的那批手；
                    //      · 可点（白色）= `CanClick` 放行的手。
                    //    而"选被加数"这一步的可点手是由 `CanClick` 算出来的，
                    //    不需要候选来重复表达 —— 所以这里留空。
                    _pendingChoiceCandidates.Clear();
                    return true;

                case BoardPhase.ChoosingTargetHand:
                    // ⚠️ **前提守卫：阶段 ② 必须有已选的运算源。**
                    //
                    // `SelectedHand` 可能在中间被清掉（CancelSelection、
                    // SetupFirstStep、上一次结算的收尾），而这条分支后面会
                    // 走到 `BeginPreTargetChoicesIfAny` -> AdvanceCharacterChoice
                    // -> 结算；那时运算源已经没了，于是抛"未选择运算源"
                    // （玩家实测的非法运算日志就是这个）。
                    //
                    // 前提不成立就**回到本回合第一步**重选，而不是继续往下走。
                    if (SelectedHand == null)
                    {
                        _preTargetChoiceActive = false;
                        SetupFirstStep();
                        return false;
                    }

                    // ⚠️ **盗贼**：在"选加数"这一步点**别人的手**时，
                    //    如果已经按过「偷窃」按钮（或他本来就是来偷的），
                    //    就走偷窃而不是加法运算。
                    //
                    //    判定放在"取消选择"之后、"收集抉择"之前 ——
                    //    这样"再点自己的手取消"这个既有手势仍然有效。
                    if (ActivePlayer?.Character is ThiefLogic
                        && !ActivePlayer.Owns(hand)
                        && _game.CanStealOn(ActivePlayer)
                        && (Phase == BoardPhase.ChoosingStealTarget
                            || _thiefWantsSteal))
                    {
                        return CompleteSteal(hand);
                    }

                    // 再次点击已选中的运算源 = 取消选择
                    if (ReferenceEquals(hand, SelectedHand))
                    {
                        CancelSelection();
                        return true;
                    }

                    // 第 ② 步：选定"参与运算的那只手"（加法的一个加数）。
                    _selectedTarget = hand;
                    TargetPlayer = OwnerOf(hand) ?? Defender;

                    // 立刻记下这次运算的两只手 —— 后面的"角色抉择"阶段
                    // （游侠强化、牧师攻击/回复/治疗目标）都要用到它们，
                    // 而这些抉择发生在 ConfirmTargetPlayer（CommitMove）**之前**。
                    //
                    // ⚠️ 曾经只在这里设 `_selectedTarget`，`_pendingActedHand` /
                    // `_pendingTargetHand` / `_pendingIsAttack` 都要等 CommitMove
                    // 才赋值 —— 于是抉择阶段读到的全是 null/false（测试抓到）。
                    _pendingActedHand = SelectedHand;
                    _pendingTargetHand = hand;

                    // 记下"本回合真正要运算的两只手"（副本，供结算兜底）。
                    // 只在回合结束时清 —— 中间的重建不会动它。
                    _turnActedHand = SelectedHand;
                    _turnTargetHand = hand;

                    // 用与"创建抉择"完全同一个判定，避免两处各写一份（会漂移）
                    _pendingIsAttack = new PreTurnChoiceContext(
                        ActivePlayer, TargetPlayer ?? Defender,
                        SelectedHand, hand).IsAttack;

                    // 第 ③ 步：运算源已经定了 -> 结果已知 -> 这时才能问得准确。
                    // 牧师运算出 4/6/8 时先问"当攻击还是当回复"。
                    if (BeginPreTargetChoicesIfAny())
                        return true;

                    // 第 ③' 步：**纯回复**（运算出 9）—— 运算已结束，
                    // 直接让人点一只手选治疗目标（点谁回谁），
                    // 不再要求先确认一次目标玩家（玩家实测反馈多余）。
                    if (TryEnterHealTargetChoice())
                        return true;

                    // 只有**运算结果会产生指向某人的效果**时才需要指定目标。
                    //
                    // 1 + 1 = 2（无效果）、或合出护盾（作用于自己）都不需要 ——
                    // 否则每一次运算都会被追问"打谁"，纯属打扰（玩家实测反馈：
                    // "战士 1+1 运算出 2 都要求选择目标，会出现很多无意义询问"）。
                    if (!NeedsTargetAfterOperation())
                    {
                        // 仍然记下目标玩家（多人模式要用这个概念），但不打断玩家
                        CommitMove(hand);
                        return true;
                    }

                    // 需要指定目标 -> 再走一步
                    // <see cref="BoardPhase.ChoosingTargetPlayer"/>。
                    // 1v1 也只有一个人可打，但仍然要求点这一次，
                    // 免得多人模式上线后流程变形、玩家要重新学。
                    Phase = BoardPhase.ChoosingTargetPlayer;
                    bool hasTarget = BeginTargetPlayerChoice();
                    Hint = !hasTarget
                        ? $"没有可攻击的目标。请点右侧「放弃攻击」。"
                        : $"已选择 {SideLabel(hand, Defender)}（{hand.Value}）参与运算，" +
                          $"结果是 {Hand.ComputeSum(SelectedHand.Value, hand.Value)}。" +
                          $"请再点一次 {TargetPlayer.Name} 的手牌，确认本次的目标是" +
                          $"{TargetPlayer.Name}。";
                    return true;

                case BoardPhase.ChoosingTargetPlayer:
                    // 第 ③ 步：点目标玩家的手牌确认"打他"。
                    // 挨打的是**玩家**；手牌只是指定的载体。
                    //
                    // ⚠️ 也接受"任意**可攻击敌人**的手" —— 因为 TargetPlayer
                    //    可能仍是"被加数的手主"（例如用队友的手当被加数），
                    //    那样只认 TargetPlayer 会让玩家**没有任何手可点** -> 卡死。
                    {
                        PlayerState owner = OwnerOf(hand);
                        if (owner == null) return false;
                        if (ReferenceEquals(owner, ActivePlayer)) return false;
                        if (!CanBeAttacked(owner)) return false;

                        TargetPlayer = owner;
                        // ⚠️ 赌博路径从没设过 `_selectedTarget`（赌博没有"被加数"），
                        //    而 `ConfirmTargetPlayer` 要求它非 null ——
                        //    不补这一句就会**静默返回 false**、`ClickHand` 却返回 true，
                        //    表现成"点了目标没反应、对局卡住"（玩家实测）。
                        //
                        //    用挨打那人的手当占位：结算需要一只"目标手"来定位挨打的人。
                        if (_selectedTarget == null)
                            _selectedTarget = owner.LeftHand;

                        ConfirmTargetPlayer();
                        return true;
                    }

                default:
                    return false;
            }
        }

        /// <summary>
        /// 这次运算的结果是否会产生**指向某人的效果**（攻击 / 回复 / 牧师把 4/6/8 当回复……），
        /// 从而需要玩家"再点一次手牌确认目标"。
        ///
        /// 判断交给角色自己（<see cref="IResultTargetProvider"/>），
        /// 控制器不认识任何具体角色 —— 与"角色抉择"用的是同一套思路。
        ///
        /// 无效果（1/2/3/7）与护盾（5，作用于自己）都不需要，避免无意义询问。
        /// </summary>
        private bool NeedsTargetAfterOperation()
        {
            if (SelectedHand == null || _selectedTarget == null) return false;

            // 用"运算前的快照"构造上下文：此时运算还没执行，
            // 直接读手值会把效果算错（这一点在 PreTurnChoiceContext 里有专门说明）。
            var context = new PreTurnChoiceContext(ActivePlayer, TargetPlayer ?? Defender,
                                                   SelectedHand, _selectedTarget);

            var provider = ActivePlayer?.Character as IResultTargetProvider;
            if (provider != null) return provider.ResultNeedsTarget(context);

            // 没有角色逻辑时（极少数直连场景）用**基础规则**兜底：
            // 攻击指向对手 -> 需要确认目标；9 的回复默认回自己 -> 不需要。
            //
            // ⚠️ 兜底的方向很重要。曾经这里写的是 `return false`，
            //    结果在没有 Character 的场景下攻击**被静默跳过确认步骤**。
            switch (context.Effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 确认"本次的目标玩家"，随后进入角色抉择 / 结算。
        ///
        /// 公开出来是为了让 UI 与测试有一个**明确的一步**（而不是"再点一次手牌"
        /// 这种隐式约定）。人类玩家在 <see cref="BoardPhase.ChoosingTargetPlayer"/>
        /// 阶段点目标玩家的手牌时也会走到这里。
        /// </summary>
        public bool ConfirmTargetPlayer()
        {
            if (Phase != BoardPhase.ChoosingTargetPlayer) return false;
            if (_selectedTarget == null) return false;

            CommitMove(_selectedTarget);
            return true;
        }

        /// <summary>把一只手翻译成它的主人；认不出来时返回 null。</summary>
        /// <summary>
        /// 最近一次"非法运算被拒绝"的现场（phase / 行动方 / 两只手 / 原因）。
        ///
        /// 为什么单独存一份：战报里只有一句话，看不出**当时处在哪个阶段**，
        /// 而"运算源为空"这类问题必须知道阶段才能定位。
        /// </summary>
        public string LastIllegalOperation { get; private set; }

        /// <summary>
        /// 清空诊断记录（在**每个回合开始时**调用）。
        ///
        /// ⚠️ 这些字段会跨回合/跨局保留，不清空会读到**上一次**的残留值 ——
        ///    我为此白查过两轮。
        /// </summary>
        private void ResetDiagnostics()
        {
            LastIllegalOperation = null;
            _advanceTrace.Clear();
        }

        /// <summary>诊断用：AdvanceCharacterChoice 的进入轨迹（只保留最后若干条）。</summary>
        private readonly List<string> _advanceTrace = new List<string>(64);

        /// <summary>
        /// 测试用：当前对局还活着几名玩家。
        ///
        /// 存在意义：角色收集抉择时读的是 `PreTurnChoiceContext.AlivePlayerCount`，
        /// 而**只有控制器能填它** —— 这条测试要能直接断言这个值。
        /// </summary>
        public int GameAlivePlayerCountForTest => _game?.AlivePlayerCount ?? 0;

        /// <summary>
        /// 这张手牌属于谁。
        ///
        /// ⚠️ 必须查**全部参战玩家**，不能只认 A/B ——
        /// 2v2 / 8 人局里 C、D 的手会返回 null，
        /// 于是被当成"第一位对手"（真实 bug：点 C 的手会打 B）。
        ///
        /// 现在的实现交给 <see cref="GameManager.OwnerOfHand"/>（按名单查），
        /// 避免这里再维护一份"谁是玩家"的清单。
        /// </summary>
        private PlayerState OwnerOf(Hand hand)
        {
            if (hand == null) return null;
            return _game.OwnerOfHand(hand);
        }

        /// <summary>
        /// 当前是否"没有合法攻击目标"，应当提供**放弃攻击**。
        ///
        /// **只在面对潜行中的刺客时出现。**
        /// 玩家实测要求："放弃攻击在 1v1 中应只在面对刺客潜行时会出现" ——
        /// 其余情况下不该给这个按钮（否则会被用来规避正常战斗）。
        ///
        /// 日后若加入"攻击距离不足"等规则，在这里一并判断即可。
        /// </summary>
        public bool CanGiveUpAttack
        {
            get
            {
                if (Phase != BoardPhase.ChoosingTargetPlayer) return false;
                if (ActivePlayer == null) return false;

                // 目前唯一场景：目标处于潜行（双手都是 7），此时不能被攻击
                return DefenderCannotBeAttacked;
            }
        }

        /// <summary>
        /// 放弃攻击：**运算照常发生、手值照常改变，但不造成伤害**，
        /// 然后结束本回合（算用掉）。
        ///
        /// ⚠️ 曾经的实现是"直接换对方行动、完全不结算"，
        ///    结果玩家 1 + 3 = 4 之后点放弃攻击，**手值回退到 1** ——
        ///    玩家实测报的 bug。运算本身是有效的，不该被撤销。
        /// </summary>
        public bool GiveUpAttack()
        {
            if (!CanGiveUpAttack) return false;

            _log.Add($"{ActivePlayer.Name} 放弃攻击：运算照常、" +
                     $"但不对 {TargetPlayer?.Name} 造成伤害（本回合作废）。");

            _giveUpAttack = true;

            // 没有确认过目标时直接结算（运算源已选定，加数也已选定）
            if (Phase == BoardPhase.ChoosingTargetPlayer && _selectedTarget != null)
            {
                CommitMove(_selectedTarget);
                return true;
            }

            return false;
        }

        // ============================================================
        // 吟游诗人：**回合结束后的四步抉择**
        // ============================================================
        //
        // 玩家确认的流程：
        //   ① 是否发动（本回合受过伤害 或 实际回复过体力）
        //   ② 增幅攻击 / 增幅防御 / 削弱攻击 / 削弱防御
        //   ③ 目标数 1~3（上限 = 范围内实际人数）-> 强度 ±(4−人数)
        //   ④ 选目标（按**角色**选，同一个人点两只手只算一次；可含自己）
        //
        // ⚠️ 这是**第一个"运算之后才问"**的技能，所以它不走
        //    `CollectPreTurnChoices`（那套是"运算前收集齐"）。
        //    这里只暴露一个**不依赖 BoardPhase** 的小流程：
        //    界面在每次刷新后调 `OfferBardChoiceIfAny()`，
        //    拿到 true 就按 ②③④ 把答案交给 `ApplyBardChoice(...)`。
        //    这样不会碰既有的阶段机（阶段机有四处必须同步登记，改它风险高）。

        /// <summary>当前是否有待答的吟游诗人抉择。</summary>
        public bool BardChoicePending { get; private set; }

        /// <summary>正在等谁做吟游诗人抉择（没有则为 null）。</summary>
        public PlayerState BardActor => _bardActor;

        /// <summary>此刻可选的**角色**候选（含自己；受攻击距离限制）。</summary>
        public IReadOnlyList<PlayerState> BardCandidates
            => _bardCandidates ?? (IReadOnlyList<PlayerState>)System.Array.Empty<PlayerState>();

        private List<PlayerState> _bardCandidates;
        private PlayerState _bardActor;

        /// <summary>
        /// **结算那一刻**快照下来的"可以发动的吟游诗人"。
        /// 不能等到刷新时再读标记 —— 那时已经被下一次 `BeginTurn()` 清零了。
        /// </summary>
        private readonly List<PlayerState> _bardEligible = new List<PlayerState>();

        /// <summary>结算完立刻调用：记下这一刻谁满足吟游诗人的发动条件。</summary>
        private void SnapshotBardEligibility()
        {
            _bardEligible.Clear();
            if (_game == null) return;

            IReadOnlyList<PlayerState> all = _game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (!(p.Character is Characters.SpecialLogics.BardLogic)) continue;
                if (p.BardActivatedThisTurn) continue;
                if (!Characters.SpecialLogics.BardLogic.CanActivateFor(p)) continue;

                _bardEligible.Add(p);
            }
        }

        /// <summary>
        /// 若有吟游诗人可以发动，就开出这个抉择；返回 true 表示**界面该弹面板**了。
        ///
        /// 故意不自动应用 —— 人类要自己选，AI 走 `BardAi.TryActivate`。
        /// </summary>
        public bool OfferBardChoiceIfAny()
        {
            if (BardChoicePending) return true;          // 已经在问了
            if (_game == null || _game.IsGameOver) return false;

            // ① 谁可以发动：用**结算那一刻的快照**
            PlayerState found = null;
            for (int i = 0; i < _bardEligible.Count; i++)
            {
                PlayerState p = _bardEligible[i];
                if (p == null || !p.IsAlive) continue;
                if (p.BardActivatedThisTurn) continue;          // 本回合已经答过了
                found = p;
                break;
            }

            // ⚠️ 回落：快照为空时**实时扫一遍**。
            //    正常界面路径一定有快照（结算那一刻记的）；
            //    但单元测试、以及少数不经过 ExecuteResolvedTurn 的路径没有，
            //    那时按当前标记判断即可。
            if (found == null && _bardEligible.Count == 0)
            {
                IReadOnlyList<PlayerState> all = _game.Players;
                for (int i = 0; i < all.Count; i++)
                {
                    PlayerState p = all[i];
                    if (p == null || !p.IsAlive) continue;
                    if (!(p.Character is Characters.SpecialLogics.BardLogic)) continue;
                    if (p.BardActivatedThisTurn) continue;
                    if (!Characters.SpecialLogics.BardLogic.CanActivateFor(p)) continue;
                    found = p;
                    break;
                }
            }

            if (found == null) return false;

            var logic = (Characters.SpecialLogics.BardLogic)found.Character;
            List<PlayerState> cands = logic.EligibleTargets(found, _game);
            if (cands.Count == 0) return false;

            _bardActor = found;
            _bardCandidates = cands;
            BardChoicePending = true;
            return true;
        }

        /// <summary>
        /// **AI 的吟游诗人由 AI 自己选完**，不要弹玩家面板。
        ///
        /// 界面在 `OfferBardChoiceIfAny()` 返回 true 后先判断归属者：
        /// 人类 -> 弹面板；AI -> 调本方法，让 `BardAi` 直接决策。
        /// 无论 AI 是否真的发动，本回合都标记为"已答"，避免反复询问。
        /// </summary>
        public bool AutoResolveAiBardChoice()
        {
            if (!BardChoicePending) return false;
            PlayerState bard = _bardActor;
            if (bard == null) { ClearBardChoice(); return false; }

            bool activated = Characters.SpecialLogics.BardAi.TryActivate(bard, _game);
            bard.BardActivatedThisTurn = true;
            ClearBardChoice();
            return activated;
        }

        /// <summary>最多能选几个目标（1~3，且不超过候选人数）。</summary>
        public int BardMaxTargetCount
        {
            get
            {
                int n = _bardCandidates?.Count ?? 0;
                if (n > Characters.SpecialLogics.BardLogic.MaxTargets)
                    n = Characters.SpecialLogics.BardLogic.MaxTargets;
                return n;
            }
        }

        /// <summary>按目标数算出的强度（界面上显示 ±N 用）。</summary>
        public static int BardStrengthFor(int targetCount)
            => Characters.SpecialLogics.BardLogic.StrengthFor(targetCount);

        /// <summary>
        /// 落实一次吟游诗人抉择（②③④ 的答案一起交上来）。
        /// </summary>
        /// <returns>实际生效的目标数；0 表示没有合法目标。</returns>
        public int ApplyBardChoice(Characters.SpecialLogics.BardLogic.BuffKind kind,
                                   IReadOnlyList<PlayerState> targets)
        {
            int applied = 0;
            if (_bardActor != null)
            {
                var logic = _bardActor.Character as Characters.SpecialLogics.BardLogic;
                if (logic != null) applied = logic.Activate(_bardActor, _game, kind, targets);

                _bardActor.BardActivatedThisTurn = true;      // 本回合不再问

                if (applied > 0)
                {
                    var sb = new System.Text.StringBuilder();
                    sb.Append(_bardActor.Name).Append(" 发动技能：").Append(kind);
                    sb.Append("（").Append(targets != null ? targets.Count : 0).Append(" 个目标）");
                    _log.Add(sb.ToString());
                }
            }

            ClearBardChoice();
            return applied;
        }

        /// <summary>放弃这次发动（等同于①选"不发动"）。</summary>
        public void DeclineBardChoice()
        {
            if (_bardActor != null) _bardActor.BardActivatedThisTurn = true;   // 答过"不发动"也算
            ClearBardChoice();
        }

        private void ClearBardChoice()
        {
            BardChoicePending = false;
            _bardActor = null;
            _bardCandidates = null;
        }

        /// <summary>本回合是否"放弃攻击"（运算照常、不造成伤害）。</summary>
        private bool _giveUpAttack;

        /// <summary>
        /// 本次运算要打**哪位玩家**（挨打的是玩家，不是手）。
        ///
        /// 多人模式会真正用到它；1v1 里等于 <see cref="Defender"/>，
        /// 但仍然要求玩家点一次确认 —— 免得多人上线后流程变形。
        /// </summary>
        public PlayerState TargetPlayer { get; private set; }

        /// <summary>
        /// 把一只手翻译成"它的主人"的名字（提示文字用）。
        ///
        /// ⚠️ 必须查**全部参战玩家** —— 原来只认 PlayerA/PlayerB，
        /// 2v2 里 C/D 的手会一律显示成 `Defender` 的名字（提示骗人）。
        /// </summary>
        private string OwnerLabel(Hand hand)
        {
            PlayerState owner = OwnerOf(hand);
            if (owner != null) return owner.Name;

            return Defender?.Name ?? "";
        }

        /// <summary>
        /// 第 ③ 步该指向谁时用的**默认目标**。
        ///
        /// ⚠️ 不能用"被加数的手主" —— 被加数可以是**队友**的手，
        ///    而队友不可被攻击，于是那个默认目标点不动 -> **卡死**
        ///    （玩家实测："选择和队友运算出攻击数字后，再次选择攻击目标会卡死"）。
        ///
        /// 规则（玩家确认）：合法攻击目标 = **攻击范围内的敌人**。
        /// 所以默认取第一个可攻击的敌人；一个都没有时（队友是唯一目标）
        /// 才回落到被加数手主 —— 那时界面给的是「放弃攻击」。
        /// </summary>
        private PlayerState DefaultAttackTarget()
        {
            // 队友虽然也是合法目标，但 AI / 默认目标仍**优先敌人**；
            // 只有敌人全部不可打时，才回落到队友。
            IReadOnlyList<PlayerState> foes = _game.OpponentsOf(ActivePlayer);
            for (int i = 0; i < foes.Count; i++)
                if (CanBeAttacked(foes[i])) return foes[i];

            IReadOnlyList<PlayerState> all = _game.Players;
            for (int i = 0; i < all.Count; i++)
                if (CanBeAttacked(all[i])) return all[i];

            return null;
        }

        /// <summary>
        /// AI 的"目标意图"：如果这位是 AI，且
        /// <see cref="GreedyChoiceProvider.IntendedVictim"/> 是当前可攻击的敌人，
        /// 就用它，而不是无脑取第一个对手。
        ///
        /// ⚠️ 这是"集火 / 六级目标优先级"在实机生效的**必要条件**：
        ///    AI 的 <c>ChooseMove</c> 已经按目标打分了，但第 ③ 步如果永远回落到
        ///    <c>OpponentsOf[0]</c>，算出来的目标就被丢掉，等于没选。
        ///    （人类玩家没有 AI，仍走下面的默认目标。）
        /// </summary>
        private PlayerState AiIntendedTarget()
        {
            if (ActivePlayer == null) return null;

            IMoveSelector ai = GetAi(ActivePlayer);
            if (ai is not GreedyChoiceProvider greedy) return null;

            PlayerState intended = greedy.IntendedVictim;
            if (intended == null || ReferenceEquals(intended, ActivePlayer)) return null;
            if (!CanBeAttacked(intended)) return null;

            return intended;
        }

        /// <summary>
        /// 进入第 ③ 步（确认攻击目标）时，把 <see cref="TargetPlayer"/> 设成一个
        /// **点得动的人**。
        ///
        /// 返回 false 表示"没有任何可攻击的敌人"——此时界面只该给「放弃攻击」。
        /// </summary>
        private bool BeginTargetPlayerChoice()
        {
            PlayerState target = AiIntendedTarget()
                                 ?? DefaultAttackTarget()
                                 ?? OwnerOf(_selectedTarget);
            TargetPlayer = target;

            return target != null && CanBeAttacked(target);
        }

        /// <summary>取消目标选择，回到第一步。</summary>
        public void CancelSelection()
        {
            if (Phase != BoardPhase.ChoosingTargetHand) return;

            SelectedHand = null;
            Phase = BoardPhase.ChoosingAttackerHand;
            Hint = $"已取消选择。轮到 {ActivePlayer.Name}：请选择要用自己哪一只手。";
            AppendLockedHandNote();
        }

        private void CommitMove(Hand target)
        {
            _selectedTarget = target;
            _chosenDefenseShield = null;

            // 保存本次运算的手，供后续抉择阶段使用
            _pendingActedHand = SelectedHand;
            _pendingTargetHand = target;

            // ⚠️⚠️ **本回合的角色抉择只收集一次。**
            //
            //    玩家实测的死循环（法师）：
            //      点自己的手 -> 点对手的手 -> 收集到「是否放弃攻击」(idx=0/2)
            //      -> 点「不发动」-> 进入"确认目标" (idx=2/2)
            //      -> 点目标的手 -> 又回到这里 -> **重新收集** -> idx 归 0
            //      -> **又弹「是否放弃攻击」** -> 无限循环。
            //
            // ⚠️⚠️ **法师在「是否放弃攻击」上答过之后，确认目标时不能再收集一遍。**
            //
            //    玩家实测的死循环：
            //      点自己的手 -> 点对手的手 -> 收集到「是否放弃攻击」(idx=0/2)
            //      -> 点「不发动」-> 进入"确认目标" (idx=2/2)
            //      -> 点目标的手 -> 又回到这里 -> **重新收集** -> idx 归 0
            //      -> **又弹「是否放弃攻击」** -> 无限循环。
            //
            //    根因：`BeginCharacterChoices` 会把 `_pendingRequests` 与
            //    `_pendingRequestIndex` **一起复位**，于是"已经把抉择答完"
            //    这个事实被抹掉了，玩家被问了第二遍。
            //
            //    ⚠️ 闸门刻意做得**很窄**（只认"法师放弃攻击已答"）：
            //       正常流程里"确认目标"发生在**收集之前**，所以不能一概跳过收集
            //       —— 那样会把闪避 / 太极 / 妖术师取消全部问不到（我试过，
            //       一次红 17 条测试）。
            if (_wizardDeclineAnswered)
            {
                AdvanceCharacterChoice();
                return;
            }

            // 顺序很重要：**先角色抉择（闪避等），再问选盾**。
            //
            // 因为闪避在结算里优先于护盾（`AttackContext.Resolve` 先判闪避再判盾），
            // 所以询问顺序也必须一致。曾经这里先问选盾、问完直接结算，
            // 结果"有盾的游侠"根本问不到闪避 —— 表现为"优先碎盾而不是询问是否闪避"。
            BeginCharacterChoices(BuildChoiceContext());
        }

        /// <summary>
        /// **法师的「是否放弃攻击」本回合已经答过**（每回合在 <see cref="SetupFirstStep"/> 复位）。
        ///
        /// ⚠️ 存在的唯一理由：防止"确认目标"时把已经答完的这个问题**再收集一遍**，
        ///    那会让玩家被同一个问题问第二次（玩家实测的无限循环）。
        /// </summary>
        private bool _wizardDeclineAnswered;

        /// <summary>
        /// 诊断用：把"抉择收集状态"打印成一行，便于定位"问题被问了两遍"。
        /// 与 `Phase` / `PendingEffect` 一起看就能判断是不是被重新收集了。
        /// </summary>
        public string ChoiceStateProbe
            => $"wizardDeclineAnswered={_wizardDeclineAnswered}"
               + $" idx={_pendingRequestIndex}/{_pendingRequests.Count}"
               + $" preTarget={_preTargetChoiceActive}"
               + $" phase={Phase}"
               + $" sel={SelectedHand != null}"
               + $" tgt={_selectedTarget != null}";

        /// <summary>
        /// 角色抉择都问完之后，进入"用哪面盾抵挡"阶段（若确实需要）。
        /// 返回 true 表示停在了选盾阶段。
        /// </summary>
        private bool BeginShieldChoiceIfNeeded()
        {
            if (!RequiresDefenseChoice(_pendingActedHand, _pendingTargetHand, out List<Hand> shields))
                return false;

            _shieldChoices.Clear();
            _shieldChoices.AddRange(shields);
            Phase = BoardPhase.ChoosingDefenseShield;

            // 措辞要明确"是**防守方**在选"，避免让人以为攻击方在挑要打碎哪面盾。
            Hint = $"轮到 {Defender.Name} 选择：用哪一面护盾抵挡 " +
                   $"{ActivePlayer.Name} 的这次攻击" +
                   $"（{_pendingActedHand.Value} + {_pendingTargetHand.Value} = " +
                   $"{Hand.ComputeSum(_pendingActedHand.Value, _pendingTargetHand.Value)}）？";

            // 防守方若是 AI，立刻替它决定，不要让流程卡在这里
            ResolveDefenseChoiceIfAi();
            return true;
        }

        /// <summary>
        /// 构造"运算前"的局面信息。
        ///
        /// 顺便把 <see cref="PreTurnChoiceContext.IsAttack"/> 缓存下来 ——
        /// 它是所有"攻击类抉择"（游侠强化/闪避、剑客追加）的前置条件，
        /// 请求没生成时第一个要查的就是它，所以在诊断里能直接读。
        /// </summary>
        private PreTurnChoiceContext BuildChoiceContext()
        {
            var context = new PreTurnChoiceContext(ActivePlayer, Defender,
                                                   _pendingActedHand, _pendingTargetHand);
            _pendingIsAttack = context.IsAttack;

            // 让角色能读到"本回合先前已问过的答案"（分阶段抉择需要）：
            // 牧师的 4/6/8 先问攻击/回复（选目标手之前），
            // 之后再据此决定"要不要问回复目标"。
            context.AnsweredLookup = GetAnsweredChoice;

            return context;
        }

        /// <summary>
        /// 收集双方角色提出的抉择并逐个询问。
        ///
        /// 这里是"角色专属 UI 提示"的**通用**入口：控制器不需要认识任何具体角色类型，
        /// 只遍历 <see cref="IPreTurnChoiceProvider"/>。
        /// （此前是 <c>is BarbarianLogic</c> 这种硬编码，每加一个带抉择的角色都要改控制器。）
        /// </summary>
        private void BeginCharacterChoices(PreTurnChoiceContext context)
        {
            _lastCollectInfo = $"actor={context.Actor?.Name}/{context.Actor?.Character?.GetType().Name} " +
                               $"defender={context.Defender?.Name}/{context.Defender?.Character?.GetType().Name} " +
                               $"acted={context.ActedHand?.Value} target={context.TargetHand?.Value} " +
                               $"result={context.ResultValue} isAttack={context.IsAttack}";

            _pendingRequests.Clear();

            // 防御性：索引必须与队列一起复位。
            // 若只清队列不清索引，后面会因 idx 越界而"跳过全部询问"（真实 bug）。
            _pendingRequestIndex = 0;
            _pendingChoiceCandidates.Clear();
            _pendingChoiceDecider = null;
            _preTargetChoiceActive = false;

            // ⚠️ 这里**不**清 `_pendingGambleRoll` —— 清零点在 SetupFirstStep。
            //    我一度两处都放，结果"开赌"掷完点数、进收集阶段时又被清成 0
            //    （测试抓到：期望 3 实际 0）。

            // ⚠️ 每回合清空"问过谁取消攻击" ——
            //    残留会让下一回合变成"问过但没答"，于是玩家的意图被误读
            //    （选了发动却被当成不发动）。我踩过同类坑（诊断字段跨回合残留）。
            _askedHexerCancel.Clear();

            CollectFrom(context.Actor, context);
            CollectFrom(context.Defender, context);

            // 妖术师的"是否取消这次攻击"：取消者是**场上的第三方**，
            // 不属于上面的行动方/防守方，所以单独收集。
            CollectHexerCancelChoices();

            // 记录"这次收集到了什么"以及"AI 怎么答的"。
            // 注意：不能用会被回合边界清空的容器 —— 上一个回合的作答
            // 常常在你看到的那次刷新之前就被清掉了（诊断设计踩过的坑）。
            var collects = new System.Text.StringBuilder();
            collects.Append("actor=").Append(context.Actor?.Name)
                    .Append(" defender=").Append(context.Defender?.Name)
                    .Append(" acted=").Append(context.ActedHand?.Value)
                    .Append(" target=").Append(context.TargetHand?.Value)
                    .Append(" result=").Append(context.ResultValue)
                    .Append(" isAttack=").Append(context.IsAttack)
                    .Append(" 请求=");
            if (_pendingRequests.Count == 0) collects.Append("（无）");
            for (int i = 0; i < _pendingRequests.Count; i++)
            {
                ChoiceRequest cr = _pendingRequests[i];
                collects.Append('[').Append(i).Append(']').Append(cr.Effect)
                        .Append("(v").Append(cr.Variant).Append(",cand")
                        .Append(cr.Candidates.Count)
                        .Append(",decider=").Append(cr.Decider?.Name ?? "行动方").Append(") ");
            }
            _lastCollectInfo = collects.ToString();


            AdvanceCharacterChoice();
        }

        private void CollectFrom(PlayerState player, PreTurnChoiceContext context)
        {
            // 填"场上存活人数" —— "仅剩两人才生效"的规则（野蛮人锁手）需要它。
            // 只有控制器知道完整名单，所以在这里统一填，不依赖各调用点自己记得。
            if (context != null && _game != null)
            {
                context.AlivePlayerCount = _game.AlivePlayerCount;
                // 候选可能要看攻击距离（圣武士的冲击目标）
                context.Manager = _game;
            }

            var provider = player?.Character as IPreTurnChoiceProvider;
            _lastCollectInfo += $" || CollectFrom({player?.Name}, " +
                                $"char={player?.Character?.GetType().Name}, " +
                                $"isProvider={provider != null})";

            if (provider != null)
            {
                int before = _pendingRequests.Count;
                provider.CollectPreTurnChoices(context, _pendingRequests);
            // ⚠️ **法师：把抉择器记下的选择读回自己身上。**
            //    必须在这里做 —— 结算一旦开始就不再问玩家，
            //    而"选了什么伤害类型"只有在结算时才知道要用。
            if (context.Actor?.Character is FingerDiceDuel.Characters.SpecialLogics.WizardLogic mage
                && provider is IDamageTypeChoiceSink dmgSink)
            {
                mage.ReadFromSink(dmgSink);
            }
                _lastCollectInfo += $" added={_pendingRequests.Count - before}";

                // 防御性：角色不该提出空候选的抉择，否则玩家永远点不下去
                for (int i = _pendingRequests.Count - 1; i >= before; i--)
                {
                    if (_pendingRequests[i].Candidates == null || _pendingRequests[i].Candidates.Count == 0)
                    {
                        _lastCollectInfo += " (过滤空候选)";
                        _pendingRequests.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// 这次攻击**将会是**什么数字（运算源 + 目标手的和）。
        ///
        /// 只有"结果会产生指向某人的攻击效果"时才有意义。
        /// </summary>
        public int PredictedAttackValue()
        {
            if (SelectedHand == null || _selectedTarget == null) return 0;

            int sum = Hand.ComputeSum(SelectedHand.Value, _selectedTarget.Value);

            return sum == 4 || sum == 6 || sum == 8 ? sum : 0;
        }

        /// <summary>
        /// 收集"**人类**妖术师是否要取消这次攻击"的抉择。
        ///
        /// 玩家要求的交互：右侧给按钮询问是否发动，然后**点对应数字**触发。
        /// 所以候选就是"与攻击数字相同的那只手" —— 点它即发动。
        ///
        /// ⚠️ 为什么要在**攻击结算之前**问：
        ///    整个回合是一次性跑完的（`ExecuteResolvedTurn` 里同步结算），
        ///    没法在结算中途停下来等玩家点击。所以抉择必须提前收集。
        ///    AI 的妖术师不走这里 —— 它在结算时自动取消（见 AttackContext）。
        ///
        /// ⚠️ 三条限制（都已由玩家确认）：
        ///    · 不能取消**自己**发起的攻击
        ///    · 多人模式里不能取消**队友**发起的攻击
        ///    · 特殊攻击（追加攻击 / 冲击）没有攻击数字，天然不会被收集
        /// </summary>
        private void CollectHexerCancelChoices()
        {
            int attackValue = PredictedAttackValue();
            if (attackValue <= 0) return;
            if (ActivePlayer == null || _game == null) return;

            IReadOnlyList<PlayerState> all = _game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;

                // 只有人类妖术师才问 —— AI 在结算时自动取消
                if (!(p.Character is HexerLogic)) continue;
                if (GetAi(p) != null) continue;

                if (ReferenceEquals(p, ActivePlayer)) continue;              // 自己不算
                if (_game.IsTeammate(p, ActivePlayer)) continue;             // 队友不算

                // 候选 = 与攻击数字相同的那只手
                var candidates = new List<Hand>(2);
                if (p.LeftHand != null && p.LeftHand.Value == attackValue)
                    candidates.Add(p.LeftHand);
                if (p.RightHand != null && p.RightHand.Value == attackValue)
                    candidates.Add(p.RightHand);

                if (candidates.Count == 0) continue;

                // 先登记"**问过这个人**"（值为 null 表示"还没答/答了不发动"）——
                // 结算时靠它区分"问过但选了不发动"与"根本没问过（AI）"。
                _askedHexerCancel[p] = null;

                _pendingRequests.Add(new ChoiceRequest(
                    HandEffect.HexerCancel,
                    $"{ActivePlayer.Name} 发起 {attackValue} 的攻击：" +
                    $"要用手上那个 {attackValue}（+1，永久）取消它吗？",
                    candidates,
                    allowDecline: true,          // 点「不发动」= 放行
                    declineResult: null,
                    decider: p));

                _lastCollectInfo += $" +妖术师取消({p.Name}, 攻击值{attackValue})";
            }
        }

        /// <summary>
        /// 推进到下一个待问的抉择。
        ///
        /// 全部问完后**先**进入"用哪面盾抵挡"阶段（若需要），最后才执行结算。
        /// 这个顺序必须与 <see cref="AttackContext.Resolve"/> 一致（闪避优先于护盾），
        /// 否则会出现"有盾时问不到闪避"。
        /// </summary>
        /// <summary>
        /// 当前这次运算的结果值（仅用于提示文案）。
        ///
        /// 运算还没执行，所以用手值直接算 —— 与结算用的是同一个
        /// <see cref="Hand.ComputeSum"/>，口径一致。
        /// </summary>
        private int LastResultValueForHint()
        {
            if (SelectedHand == null || _selectedTarget == null) return 0;
            return Hand.ComputeSum(SelectedHand.Value, _selectedTarget.Value);
        }

        /// <summary>
        /// 进入"选治疗目标"（牧师）。点一只手 = 治疗那只手的主人。
        ///
        /// ## 为什么这一步存在
        ///
        /// 玩家明确说明：**选回复的时候运算已经结束了，此时不存在"运算源"这种说法**，
        /// 选择的就是**治疗目标** —— 点自己的手给自己回血、点对方的手给对方回血。
        ///
        /// 所以这里**不再需要"回给自己"这种按钮**（点自己就是回自己），
        /// 也不需要先点一次对手的手来确认目标玩家（那一击就是回复本身）。
        ///
        /// ## 两种情况
        ///
        /// ① **纯回复**（运算出 9）：运算源一选定就该问，直接由
        ///    <see cref="BoardController.ClickHand"/> 的选目标手分支调用。
        /// ② **4/6/8 选了回复**：在"攻击/回复"答完之后调用。
        ///
        /// 返回 true 表示已经停在 <see cref="BoardPhase.ChoosingPreTurnChoice"/>。
        /// </summary>
        private bool TryEnterHealTargetChoice()
        {
            var provider = ActivePlayer?.Character as IPreTurnChoiceProvider;
            if (provider == null) return false;
            if (SelectedHand == null || _selectedTarget == null) return false;

            var context = new PreTurnChoiceContext(ActivePlayer, TargetPlayer ?? Defender,
                                                   SelectedHand, _selectedTarget);

            // ⚠️ 必须挂上"本回合已答"的查询 —— 牧师的 4/6/8 靠它读
            // "玩家刚才选了回复还是攻击"（`ChoseToHeal` 的 AnsweredLookup 路径）。
            // 漏了它，牧师会把"选了回复"读成"没选" -> 不问治疗目标 ->
            // 阶段退回 ChoosingTargetPlayer（真实 bug）。
            context.AnsweredLookup = GetAnsweredChoice;

            // ⚠️ 这里**不能**按"结果是不是攻击值（4/6/8）"过滤：
            // 4/6/8 的回复分支里运算结果本身就是 4/6/8，
            // 用 `context.IsAttack` 过滤会把它误判成攻击而直接跳过 ——
            // 表现成"选了回复却还要再点一次对手"（玩家实测 bug）。
            // 该不该进这一步由**调用点**决定（纯 9 或 4/6/8 选了回复）。
            _pendingRequests.Clear();
            _pendingRequestIndex = 0;
            _pendingChoiceCandidates.Clear();
            _pendingChoiceDecider = null;

            // 同样要填"存活人数"（"仅剩两人才生效"的规则依赖它）
            if (_game != null) context.AlivePlayerCount = _game.AlivePlayerCount;

            // 也要挂上 GameManager：有些抉择的候选要**按攻击距离过滤**
            // （圣武士的冲击目标只能在范围内），没有它就算不出距离。
            if (_game != null) context.Manager = _game;

            provider.CollectPreTurnChoices(context, _pendingRequests);


            // ⚠️ **法师：把抉择器记下的选择读回自己身上。**
            //    必须在这里做 —— 结算一旦开始就不再问玩家，
            //    而"选了什么伤害类型"只有在结算时才知道要用。
            if (context.Actor?.Character is FingerDiceDuel.Characters.SpecialLogics.WizardLogic mage
                && provider is IDamageTypeChoiceSink dmgSink)
            {
                mage.ReadFromSink(dmgSink);
            }

            // ⚠️⚠️ **这里只能收"确认目标之前就必须回答"的抉择。**
            //
            //    以前这里不过滤：剑客的追加、游侠的强化、野蛮人的锁手……
            //    全被当成"回复目标"在**确认攻击目标之前**问掉了。
            //    1v1 只有一个目标，所以看不出来；8 人局有多个目标时，
            //    玩家点完加数就直接进这些抉择，目标被默认成"加数手主"，
            //    表现为"剑客主攻击和追加都不让我选目标"（玩家实测）。
            //
            //    保留在这里的只有：
            //      · 回血目标（9 / 牧师）；
            //      · 会改变本次攻击射程的游侠强化 / 战士碎盾换射程；
            //      · 法师放弃攻击 / 选伤害类型；
            //      · 防守方游侠闪避（必须在结算前收集）。
            //    其余（剑客追加、野蛮人锁手、猎人伏击……）留到目标确认后。
            bool hasNonHeal = false;
            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
            {
                ChoiceRequest r = _pendingRequests[i];
                bool keep = r.Effect == HandEffect.PriestHealTarget
                         || r.Effect == HandEffect.PriestExtraHealTarget
                         || r.Effect == HandEffect.RangerPowerShot
                         || r.Effect == HandEffect.RangerDodge
                         || r.Effect == HandEffect.WizardAbandonAttack
                         || r.Effect == HandEffect.WizardDamageType
                         || r.Effect == HandEffect.FighterRangeBreak;
                if (!keep || r.Candidates == null || r.Candidates.Count == 0)
                    _pendingRequests.RemoveAt(i);
                else if (r.Effect == HandEffect.RangerPowerShot
                      || r.Effect == HandEffect.RangerDodge
                      || r.Effect == HandEffect.FighterRangeBreak)
                    hasNonHeal = true;
            }

            if (_pendingRequests.Count == 0) return false;

            // 攻击距离类抉择回答完以后仍要回到"确认目标"；
            // 纯回复目标则直接进入治疗结算。
            // 1v1 只有一个目标，旧交互不再多问一次；多人局必须回到确认目标。
            if (hasNonHeal && _game.Players.Count > 2)
                _returnToTargetAfterChoice = true;

            // 纯回复目标不设 `_preTargetChoiceActive`（答完直接结算）；
            // 攻击距离 / 法师类抉择已在上面的 hasNonHeal 分支置位，
            // 答完后会回到"确认目标"。
            AdvanceCharacterChoice();
            return Phase == BoardPhase.ChoosingPreTurnChoice
                || Phase == BoardPhase.ChoosingPreTargetChoice;
        }

        /// <summary>
        /// 玩家在这次"攻击还是回复"抉择里是否选了**回复**。
        ///
        /// 读的是本回合记录下来的答案 ——
        /// 不能用 sink 的 <c>GetChoice</c>（它会套用回退值，
        /// 把"选了攻击"读成"选了回复"）。
        /// </summary>
        private bool ChoseHealInAttackOrHeal()
        {
            if (!_answeredThisTurn.TryGetValue(AnswerKey(HandEffect.PriestAttackOrHeal, 0),
                                               out Hand answer))
                return false;

            return answer != null;
        }

        private void AdvanceCharacterChoice()
        {
            _advanceTrace.Add($"enter idx={_pendingRequestIndex}/{_pendingRequests.Count}" +
                              $" sel={SelectedHand?.Value.ToString() ?? "N"}" +
                              $" tgt={_selectedTarget?.Value.ToString() ?? "N"}" +
                              $" pre={_preTargetChoiceActive}");

            if (_pendingRequestIndex >= _pendingRequests.Count)
            {
                // ⚠️ **守卫：运算源必须还在。**
                //
                // 走到这里意味着"抉择都问完了，该结算了"，但结算需要
                // `SelectedHand` / `_selectedTarget`。它们可能在别处被清掉
                // （`CancelSelection`、`SetupFirstStep`、上一次结算的收尾），
                // 那样 `ExecuteTurn` 会抛"未选择运算源" ——
                // 被防御性 catch 接住后记进战报，表现为对局里莫名多一条错误日志
                // （玩家实测 + 批量测试都抓到过）。
                //
                // 正确做法不是"硬着头皮结算"，而是**回到本回合第一步**，
                // 让玩家重新选（状态确实已经不完整了）。
                if (SelectedHand == null || _selectedTarget == null)
                {
                    _preTargetChoiceActive = false;
                    SetupFirstStep();
                    return;
                }

                // ①-0 游侠强化 / 战士换射程：回答完先回到"确认目标玩家"。
                if (_returnToTargetAfterChoice)
                {
                    _returnToTargetAfterChoice = false;

                    if (NeedsTargetAfterOperation())
                    {
                        Phase = BoardPhase.ChoosingTargetPlayer;
                        if (SelectedHand == null) SelectedHand = _turnActedHand;
                        bool hasTarget = BeginTargetPlayerChoice();

                        string targetName = TargetPlayer != null
                            ? TargetPlayer.Name
                            : (Defender?.Name ?? "目标");
                        Hint = !hasTarget
                            ? $"运算出 {LastResultValueForHint()}，但没有可攻击的目标。" +
                              $"请点右侧「放弃攻击」。"
                            : $"运算出 {LastResultValueForHint()}。" +
                              $"请确认这次要指向 {targetName}。";
                        return;
                    }
                }

                // ① 这次抉择是"选定运算源之后、确认目标玩家之前"问的。
                //
                // ⚠️ 此时**运算源（目标手）已经选过了**，所以不能回到
                // `ChoosingTargetHand` 让玩家再点一遍同一只手。
                // 玩家确认的顺序是：
                //   点自己的手 -> 点对手的手 -> 弹攻击/回复 -> 确认目标玩家
                // 所以这里直接接"确认目标玩家"（需要时），否则照常结算。
                if (_preTargetChoiceActive)
                {
                    _preTargetChoiceActive = false;

                    // ①-a 牧师 4/6/8 选了「回复」：
                    //   运算**已经结束**，接下来点一只手就是选**治疗目标**
                    //   （点自己的手 = 回自己，点对方的手 = 回对方）。
                    //
                    //   玩家明确要求：选完回复后**那一次点击就该完成回复**，
                    //   不要先让人点一次对手的手（那只是确认目标玩家、纯属多余）。
                    //
                    //   ⚠️ 必须先用**记录下来的答案**确认玩家真的选了回复：
                    //   4/6/8 的运算结果本身就是攻击值，不能靠"结果是不是攻击"
                    //   来判断 —— 那样会把攻击分支也当成回复（玩家实测 bug）。
                    if (ChoseHealInAttackOrHeal() && TryEnterHealTargetChoice())
                    {
                        // 治疗目标选定后仍要结算，所以副本继续保留
                        return;
                    }

                    // ①-b 攻击：仍要确认目标玩家（玩家确认过的交互不变）
                    if (NeedsTargetAfterOperation())
                    {
                        Phase = BoardPhase.ChoosingTargetPlayer;
                        if (SelectedHand == null) SelectedHand = _turnActedHand;
                        bool hasTarget = BeginTargetPlayerChoice();

                        // ⚠️ 没有合法目标时 `TargetPlayer` 仍是 null ——
                        //    直接拼提示会空引用（1v1 循环赛抓到过 NullReferenceException）。
                        string targetName = TargetPlayer != null
                            ? TargetPlayer.Name
                            : (Defender?.Name ?? "目标");

                        Hint = !hasTarget
                            ? $"运算出 {LastResultValueForHint()}，但没有可攻击的目标。" +
                              $"请点右侧「放弃攻击」。"
                            : $"运算出 {LastResultValueForHint()}。" +
                              $"请确认这次要指向 {targetName}。";
                        return;
                    }

                    if (!BeginShieldChoiceIfNeeded()) ExecuteResolvedTurn();
                    return;
                }

                // ② 常规：角色抉择都问完了 -> 选盾 / 结算
                if (!BeginShieldChoiceIfNeeded()) ExecuteResolvedTurn();
                return;
            }

            ChoiceRequest request = _pendingRequests[_pendingRequestIndex];

            // ① 这一次根本不用问（分阶段抉择的后一步）-> 按放弃值落地后继续。
            //
            // 典型：牧师的 4/6/8 要先问"攻击还是回复"，选了攻击就**不该再问**
            // "回复给谁"。后一步是提前收集的（ExecuteTurn 要求运算前收集齐），
            // 靠 ShouldSkip() 在这里被静默跳过 ——
            // 避免"选了攻击却被追问回复目标"（玩家实测 bug）。
            if (request.ShouldSkip())
            {
                SinkFor(DeciderFor(request))?.SetChoice(request.Effect,
                                                         request.DeclineResult,
                                                         request.Variant);
                _pendingRequestIndex++;
                AdvanceCharacterChoice();
                return;
            }

            _pendingChoiceCandidates.Clear();
            _pendingChoiceCandidates.AddRange(request.Candidates);

            // 该由谁回答：优先按角色自己声明的归属判断
            _pendingChoiceDecider = DeciderFor(request);

            // ⚠️ 两种"运算前抉择阶段"必须分清：
            //   · ChoosingPreTargetChoice —— 在**选目标手之前**问（牧师 4/6/8 攻击还是回复）
            //   · ChoosingPreTurnChoice    —— 其余所有（含**治疗目标**）
            //
            // 治疗目标发生在"运算源已定"之后，**不属于**"选目标手之前"。
            // 曾经借用了 `_preTargetChoiceActive` 的标志，阶段就被报成
            // ChoosingPreTargetChoice —— 表现成"点完手却停在选目标手前的阶段"（真实 bug）。
            Phase = _preTargetChoiceActive
                ? BoardPhase.ChoosingPreTargetChoice
                : BoardPhase.ChoosingPreTurnChoice;
            Hint = request.Prompt;

            ResolvePreTurnChoiceIfAi();
        }

        /// <summary>
        /// 角色在玩家答完一个抉择后，按**刚收到的答案**追加"后一步"的问题。
        ///
        /// 为什么需要：`GameManager.ExecuteTurn` 一次跑完整个回合，
        /// 所有抉择都必须在运算前收集齐；但有些问题**依赖前一个答案**
        /// （例如牧师先选"攻击还是回复"，选回复才需要问"回复给谁"）。
        ///
        /// 控制器不参与判断 —— 它只把"刚答完的请求 + 玩家的答案"交给角色，
        /// 由角色自己决定要不要追加。返回 null 表示没有后续问题。
        /// </summary>
        private void AppendFollowUpChoices(ChoiceRequest answered, Hand answer)
        {
            if (answered.Decider == null && ActivePlayer == null) return;

            var provider = ActivePlayer?.Character as IPreTurnChoiceProvider;
            if (provider == null) return;

            int before = _pendingRequests.Count;
            provider.CollectFollowUpChoices(answered, answer, _pendingRequests);

            // 防御性：不要空候选
            for (int i = _pendingRequests.Count - 1; i >= before; i--)
            {
                if (_pendingRequests[i].Candidates == null || _pendingRequests[i].Candidates.Count == 0)
                    _pendingRequests.RemoveAt(i);
            }
        }

        /// <summary>
        /// 判断某个抉择该由谁回答。
        /// 角色可以在收集时用 <see cref="ChoiceRequest.Decider"/> 显式指定
        /// （例如游侠的闪避属于**防守方**）；未指定则由行动方回答。
        /// </summary>
        private PlayerState DeciderFor(ChoiceRequest request)
            => request.Decider ?? ActivePlayer;

        public bool IsChoiceCandidate(Hand hand) => ContainsHand(_pendingChoiceCandidates, hand);

        /// <summary>
        /// 本局**已完成**的回合数（`GameManager.TurnCount` 的原义）。
        /// 内部/测试用；UI 显示请用 <see cref="DisplayTurn"/>。
        /// </summary>
        public int TurnCount => _game.TurnCount;

        /// <summary>
        /// 本局**已完成**的轮次数（一轮 = 所有角色都经过一次行动）。
        /// 内部/测试用；UI 显示请用 <see cref="DisplayRound"/>。
        /// </summary>
        public int RoundCount => _game.RoundCount;

        /// <summary>
        /// **轮次上限**（所有模式统一 50 轮）。
        /// </summary>
        public int RoundLimit => _game?.RoundLimit ?? GameManager.DefaultRoundLimit;

        /// <summary>
        /// UI 显示的"当前是第几轮"（**从 1 开始**）。
        ///
        /// 玩家操作的第一个回合属于**第 1 轮**，而 `RoundCount` 记的是
        /// "已完成的轮次数"（开局为 0），所以显示值要 +1。
        ///
        /// 为什么不在 `GameManager` 里把 `RoundCount` 改成 1 起：
        /// 它的语义（已完成数）被上限判定依赖，改语义的连带风险很大。
        /// **只在显示层换算。**
        /// </summary>
        public int DisplayRound => Math.Min(RoundCount + 1, RoundLimit);

        /// <summary>
        /// UI 显示的"当前是第几回合"（**从 1 开始**）。
        ///
        /// ⚠️ 玩家要求界面改成显示**轮次**，所以这里保留给战报/诊断用，
        ///    牌桌顶栏请用 <see cref="DisplayRound"/>。
        ///
        /// 为什么不在 `GameManager` 里把 `TurnCount` 改成 1 起：
        /// 它的语义（已完成数）被胜负判定与多份测试依赖，
        /// 改语义的收益很小、连带风险很大。**只在显示层换算。**
        ///
        /// 上限处收敛到 `TurnLimit`，避免平局瞬间显示 "51 / 50"。
        /// </summary>
        public int DisplayTurn => Math.Min(TurnCount + 1, TurnLimit);

        public int TurnLimit => _game?.TurnLimit ?? GameManager.DefaultTurnLimit;

        /// <summary>
        /// 诊断用：把"当前停在哪个阶段、该谁决定、候选是什么"打出来。
        /// 卡住时第一时间看这个，比猜快得多。
        /// </summary>
        public string DebugPhaseInfo()
            => $"phase={Phase} actor={ActivePlayer?.Name} chooser={Chooser?.Name} " +
               $"decider={_pendingChoiceDecider?.Name} idx={_pendingRequestIndex}/{_pendingRequests.Count} " +
               $"cands={_pendingChoiceCandidates.Count} isHumanChooser={IsHumanChooser} " +
               $"shields={_shieldChoices.Count}";

        /// <summary>
        /// 诊断用：当前待答的角色抉择队列（只读）。
        ///
        /// 有些问题（例如"为什么只问了一次""为什么第二次问的是别的东西"）
        /// 光看 <see cref="PendingRequest"/> 只能看到队首，必须能看到整个队列。
        /// </summary>
        public IReadOnlyList<ChoiceRequest> PendingRequests => _pendingRequests;

        /// <summary>诊断用：队列里当前问到第几个。</summary>
        public int PendingRequestIndex => _pendingRequestIndex;

        /// <summary>诊断用：本次运算的行动手（抉择创建时用的那只）。</summary>
        public Hand PendingActedHand => _pendingActedHand;

        /// <summary>诊断用：本次运算的目标手。</summary>
        public Hand PendingTargetHand => _pendingTargetHand;

        /// <summary>
        /// 诊断用：本次运算是否会被判定为"攻击"。
        ///
        /// 角色的抉择（游侠强化/闪避、剑客追加……）都以它为前置条件，
        /// 请求没生成时第一步就要看这个值。
        /// </summary>
        public bool PendingIsAttack => _pendingIsAttack;

        private string _lastCollectInfo;

        /// <summary>诊断用：最近一次"收集角色抉择"时的上下文快照。</summary>
        public string LastCollectInfo => _lastCollectInfo;

        /// <summary>回答当前待问的角色抉择。<paramref name="hand"/> 传 null 表示放弃。</summary>
        private void ApplyPreTurnChoiceAndContinue(Hand hand)
        {
            ChoiceRequest request = _pendingRequests[_pendingRequestIndex];

            // 候选校验（放弃时不校验）
            if (hand != null && !IsChoiceCandidate(hand)) return;

            SinkFor(_pendingChoiceDecider)?.SetChoice(request.Effect, hand);

            // 妖术师的"是否取消攻击"：把玩家的回答（可能是"不发动"）也记进
            // `_askedHexerCancel` —— 结算时靠它决定放行还是取消。
            if (request.Effect == HandEffect.HexerCancel && _pendingChoiceDecider != null
                && hand != null && _pendingChoiceDecider.Owns(hand))
            {
                _askedHexerCancel[_pendingChoiceDecider] = hand;
            }

            // 统一定点：控制器自己保存一份本回合的答案。
            //
            // 这样后续阶段与**结算**一定能读到它，完全不依赖 sink 是否被清空。
            // （牧师的 4/6/8 两段抉择之间会走 SetupFirstStep 清 sink，
            //   只靠 sink 回读会导致"选了回复却按攻击结算"。）
            RecordAnswer(request.Effect, request.Variant, hand);

            // 让角色按"刚收到的答案"追加后续问题（例如牧师选了回复才问给谁）。
            // 必须**先记答案再追加** —— 后一步的 SkipWhen 要读得到前一步的答案。
            _pendingRequestIndex++;

            // ⚠️⚠️ **法师在「是否放弃攻击」上点了「不发动」= 照常攻击。**
            //
            //    这时必须重新立起"本次抉择属于选目标手之前"，
            //    让它走 ①-b（`NeedsTargetAfterOperation` -> 确认目标玩家）。
            //    否则会一路走到 `ExecuteResolvedTurn()` ——
            //    **没让玩家确认攻击目标就直接打了**
            //    （玩家实测："法师点击不发动后没有选择攻击目标就自动攻击了"）。
            //
            //    ⚠️ 为什么不能靠原生的 `_preTargetChoiceActive`：
            //       进 `AdvanceCharacterChoice` 的中间步骤会把它清掉，
            //       实测到那一刻它已经是 false，于是走了结算。
            if (request.Effect == HandEffect.WizardAbandonAttack && hand == null)
            {
                _preTargetChoiceActive = true;

                // 记下"这个问题本回合已经答过" —— 确认目标时
                // `CommitMove` 靠它**不再收集一遍**（否则又弹一次 -> 死循环）。
                _wizardDeclineAnswered = true;
            }

            AppendFollowUpChoices(request, hand);

            AdvanceCharacterChoice();
        }

        /// <summary>
        /// 放弃当前待问的角色抉择（等价于点了"不发动"）。
        /// 只在 <see cref="ChoiceRequest.AllowDecline"/> 为 true 时有效。
        /// </summary>
        public bool DeclinePendingChoice()
        {
            if (!IsInChoicePhase) return false;

            // ⚠️⚠️ **阶段守卫：只有"角色抉择"阶段才能用「不发动」。**
            //
            //    玩家实测的死循环：法师「不发动」之后进入 `ChoosingTargetPlayer`
            //    （该点哪位目标），此时界面上**仍留着「放弃攻击」按钮**；
            //    再点一次 -> 走到这里 -> `ApplyPreTurnChoiceAndContinue`
            //    按一个**过期**的 `_pendingRequestIndex` 推进，
            //    结果 `idx` 被弄回 0 -> **重新问「是否放弃攻击」** -> 无限循环。
            //
            //    所以这里必须挡住：确认目标阶段**没有**"不发动"这个操作，
            //    要么点目标的手确认，要么（真的没有合法目标时）用 `GiveUpAttack`。
            if (Phase == BoardPhase.ChoosingTargetPlayer) return false;
            if (Phase == BoardPhase.ChoosingTargetHand) return false;
            if (Phase == BoardPhase.ChoosingAttackerHand) return false;

            if (_pendingRequestIndex >= _pendingRequests.Count) return false;

            ChoiceRequest request = _pendingRequests[_pendingRequestIndex];
            if (!request.AllowDecline) return false;

            ApplyPreTurnChoiceAndContinue(null);
            return true;
        }

        /// <summary>当前待问的抉择是否允许放弃（UI 用来决定是否显示"不发动"按钮）。</summary>
        public bool CanDeclinePendingChoice
            => IsInChoicePhase
               && _pendingRequestIndex < _pendingRequests.Count
               && _pendingRequests[_pendingRequestIndex].AllowDecline;

        /// <summary>
        /// 当前待问的抉择**是哪一种**（UI 用来决定弹哪个面板）。
        ///
        /// 目前主要给法师用：轮到 <see cref="HandEffect.WizardDamageType"/> 时
        /// 要弹出"10 种伤害类型"的选择面板，而不是普通的"发动 / 不发动"。
        /// </summary>
        public HandEffect PendingEffect
            => IsInChoicePhase && _pendingRequestIndex < _pendingRequests.Count
               ? _pendingRequests[_pendingRequestIndex].Effect
               : HandEffect.None;

        /// <summary>
        /// **法师：为下一次攻击记下伤害类型。**
        ///
        /// 由界面在玩家点了类型面板之后调用。记完之后把这一步抉择标为已答，
        /// 流程继续（下一步就是选攻击目标）。
        /// </summary>
        public bool ChooseDamageType(DamageType type)
        {
            if (type == DamageType.None) return false;
            if (PendingEffect != HandEffect.WizardDamageType) return false;

            // ⚠️⚠️ **必须把类型记到法师身上，不能只写进抉择器。**
            //
            //    我第一版写成"先从 `SinkFor(ActivePlayer)` 取 sink（要求实现
            //    `IDamageTypeChoiceSink`），再 `mage.ReadFromSink(sink)`" ——
            //    而**人类玩家的抉择器是 `SimpleChoiceProvider`，
            //    它没有实现那个接口**（只有 AI 的 `GreedyChoiceProvider` 实现了）。
            //    于是 `sink == null` -> 直接返回 false ->
            //    **面板点了没反应（玩家实测的"卡住"）**。
            //
            //    正确的做法：**法师自己就是答案的归属者**，直接写给他。
            //    （抉择器那条路是给 AI 用的，两者互不冲突。）
            var mage = ActivePlayer?.Character as Characters.SpecialLogics.WizardLogic;
            if (mage == null) return false;

            mage.SetPendingDamageType(type);
            mage.WantsToAbandonThisTurn = true;

            // 顺手也写进抉择器（若它支持）—— 让 AI 侧的记录与玩家选择保持一致
            if (SinkFor(ActivePlayer) is IDamageTypeChoiceSink sink)
            {
                sink.ChosenDamageType = type;
                sink.WantsToAbandonAttack = true;
            }

            ApplyPreTurnChoiceAndContinue(null);
            return true;
        }

        /// <summary>
        /// "发动"按钮的文案（空 = 不显示）。
        ///
        /// 有些抉择只有一个候选（剑客的追加攻击、游侠的强化射击），
        /// 让玩家去点手牌很别扭，所以给一个明确的发动按钮。
        /// </summary>
        public string PendingAcceptLabel
            => IsInChoicePhase
               && _pendingRequestIndex < _pendingRequests.Count
               ? _pendingRequests[_pendingRequestIndex].AcceptLabel
               : null;

        /// <summary>
        /// 直接接受当前待问的抉择（等价于点"发动"按钮）。
        /// 只对唯一候选的抉择有意义；多候选时返回 false，要求玩家自己选。
        /// </summary>
        public bool AcceptPendingChoice()
        {
            if (!IsInChoicePhase) return false;
            if (_pendingRequestIndex >= _pendingRequests.Count) return false;

            ChoiceRequest request = _pendingRequests[_pendingRequestIndex];
            if (string.IsNullOrEmpty(request.AcceptLabel)) return false;

            // ⚠️⚠️ **法师：玩家在界面点了"放弃攻击"，要当场把标记落到法师身上。**
            //
            //    否则下一步「选伤害类型」会被 `skipWhen` 跳过 ——
            //    那个条件是 `!WantsToAbandonThisTurn`，而这个标记
            //    原本要等结算时（`ResolveMageStatusIntent`）才被设置，
            //    所以在"选类型"这一刻它还是 false -> 整步被跳过 ->
            //    **界面上什么都不发生（玩家看到的"卡住"）**，
            //    而结算时又因为没走"选类型"这一步而没有类型可选。
            //
            //    ⚠️ 只有**真的被接受**时才标记；被拒绝时不能留下脏标记。
            if (request.Effect == HandEffect.WizardAbandonAttack
                && ActivePlayer?.Character
                   is Characters.SpecialLogics.WizardLogic mage
                && mage.WantsToAbandonThisTurn == false)
            {
                mage.WantsToAbandonThisTurn = true;
            }

            // ⚠️ 只有**一个候选**时，"发动"就是"就选它"，语义明确。
            if (request.Candidates.Count == 1)
            {
                ApplyPreTurnChoiceAndContinue(request.Candidates[0]);
                return true;
            }

            // ⚠️⚠️ **多个候选时也要能用。**
            //
            //    原来的写法是 `if (request.Candidates.Count != 1) return false;`，
            //    于是法师的「放弃攻击」——它是**是非题**（候选是法师自己的两只手，
            //    只用来占位）——点了"发动"按钮会被**静默拒绝**，
            //    界面毫无反应，玩家看到的就是**卡住**
            //    （玩家实测："法师点击放弃攻击后卡住"）。
            //
            //    对"是非题"来说，候选填什么都不重要（真正要选的是
            //    "放弃 / 不放弃"，答案存在 sink 里）。所以取第一个候选即可。
            //
            //    ⚠️ 这不会破坏"多候选必须玩家自己挑"的既有抉择：
            //       锁手 / 治疗目标 / 闪避那几种的 `AcceptLabel` 都是空的，
            //       上一句 `string.IsNullOrEmpty(request.AcceptLabel)` 已经挡住了。
            ApplyPreTurnChoiceAndContinue(request.Candidates[0]);
            return true;
        }

        /// <summary>
        /// AI 已经替某一方做出的抉择（键 = <c>effect#variant</c>）。
        ///
        /// 为什么单独存一份、而不是只写 sink：
        /// `PreTurnChoiceReader.Read` 的回读路径（sink）是**跨回合保留**的，
        /// 上一回合的旧答案会被当成"本回合的决定"（实测过：导致技能完全不发动）。
        /// 这份记录由控制器**在回合边界清空**，读取时**优先于 sink**，
        /// 所以 AI 的决定不会被过期记录顶掉。
        /// </summary>
        private readonly Dictionary<string, Hand> _aiDecisions
            = new Dictionary<string, Hand>();

        private static string DecisionKey(HandEffect effect, int variant)
            => variant == 0 ? effect.ToString() : $"{effect}#{variant}";

        /// <summary>记下 AI 的抉择（供结算时优先读取）。</summary>
        private void RecordAiDecision(HandEffect effect, int variant, Hand hand)
            => _aiDecisions[DecisionKey(effect, variant)] = hand;

        /// <summary>
        /// 取 AI 已经做出的抉择；没有则返回 null。
        ///
        /// 供角色在结算时**优先**查询（见 <see cref="PreTurnChoiceContext.AiDecisionLookup"/>）。
        /// </summary>
        public Hand GetAiDecision(HandEffect effect, int variant = 0)
            => _aiDecisions.TryGetValue(DecisionKey(effect, variant), out Hand h) ? h : null;

        /// <summary>清空 AI 抉择记录（每个新回合开始时调用）。</summary>
        private void ClearAiDecisions() => _aiDecisions.Clear();

        private readonly List<string> _aiTrace = new List<string>(16);

        /// <summary>
        /// 诊断用：本回合 AI 对各个抉择的作答记录（最近 16 条）。
        ///
        /// 排查"AI 不发动技能"时看它：能区分
        ///   ① AI 根本没被问到（没有记录）；
        ///   ② AI 被问到了但答了 null（provider 返回放弃）；
        ///   ③ AI 答了但结果被判定为非法（pick 显示 null/非法）。
        /// </summary>
        public IReadOnlyList<string> AiTrace => _aiTrace;

        /// <summary>
        /// 若当前待答抉择的归属者是 AI，则替它决定。
        /// 人类则什么都不做，等 UI 调用 <see cref="ClickHand"/>。
        /// </summary>
        private void ResolvePreTurnChoiceIfAi()
        {
            if (GetAi(_pendingChoiceDecider) == null)
            {
                _aiTrace.Add($"跳过：decider={_pendingChoiceDecider?.Name ?? "null"} 不是 AI");
                return;   // 人来做决定
            }

            ChoiceRequest request = _pendingRequests[_pendingRequestIndex];
            IChoiceProvider provider = GetChoice(_pendingChoiceDecider);

            // ⚠️ 注入对局引用：有些抉择要看全场信息（牧师该给谁回血），
            //    而 ChooseHandFor 的入参里没有 game。
            if (provider is GreedyChoiceProvider greedy) greedy.Game = _game;

            Hand pick = provider?.ChooseHandFor(request, _pendingChoiceDecider);
            bool legal = IsChoiceCandidate(pick);
            if (!legal) pick = null;          // 非法或放弃

            // 诊断：记录 AI 对每个抉择的作答（含"被判定为非法"的情况）
            _aiTrace.Add($"AI作答 {request.Effect}(v{request.Variant}) " +
                         $"provider={provider?.GetType().Name ?? "null"} " +
                         $"pick={(legal ? pick.Value.ToString() : "null/非法")} " +
                         $"cands={request.Candidates.Count}");

            SinkFor(_pendingChoiceDecider)?.SetChoice(request.Effect, pick);

            // 另外记一份"本轮有效"的答案：结算时优先读它，避免被 sink 里的旧记录顶掉
            RecordAiDecision(request.Effect, request.Variant, pick);
            _game.AiDecisionLookup = GetAiDecision;
            _game.AnsweredLookup = GetAnsweredChoice;

            // 控制器统一保存一份（AI 的答案同样要能被后续阶段读到）
            RecordAnswer(request.Effect, request.Variant, pick);

            _pendingRequestIndex++;
            AdvanceCharacterChoice();
        }

        /// <summary>
        /// AI 走一步。返回是否真的走了。
        ///
        /// 实现上直接复用人类那条路径（<see cref="ClickHand"/>）：AI 用
        /// <see cref="IMoveSelector"/> 选好手之后，把两次"点击"代它做掉。
        /// 这样 AI 与人类走的是**完全相同**的代码路径，不会出现"AI 能走通、
        /// 人类走不通"或反过来的分歧（这类分歧正是之前选盾 bug 的成因）。
        ///
        /// 如果过程中进入了需要 AI 自己抉择的阶段（野蛮人锁手 / 防守方是 AI），
        /// 会同步代它决定，随后流程自然继续。
        /// </summary>
        public bool MakeAutomaticMove()
        {
            IMoveSelector ai = GetAi(ActivePlayer);
            if (ai == null) return false;
            if (_game.IsGameOver) return false;

            // ⚠️ AI 的妖术师要用"跳过回合改体力"这个技能。
            //
            // 人类玩家是"点跳过回合 -> 点一个人的手"，AI 没有点击，
            // 所以要在这里自己走完 —— 否则 AI 永远不用这个技能。
            //
            // 选目标策略：**体力最高的对手**（把他拉低到自己的血量 = 收益最大）。
            if (TryAiHexerSkip()) return true;

            // ---- 盗贼：AI 自己偷（玩家要求"AI 也要会用这个技能"）----
            //
            // ⚠️⚠️ **必须放在普通运算之前**。
            //
            //    我第一版把它放在普通运算的 ClickHand **之后**，
            //    于是普通运算**先执行了**、盗贼那一块**永远走不到** ——
            //    表现为"AI 从不偷"（两条测试失败，而且我加的诊断
            //    一次都没打印，因为方法根本没被调用）。
            //
            //    判据是"值不值得偷"：不值得才回去走正常运算
            //    （正常运算的偏好由盗贼价值表负责合成更大点数）。
            if (Phase == BoardPhase.ChoosingAttackerHand
                && ActivePlayer?.Character is ThiefLogic
                && TryAiThiefSteal())
            {
                return true;
            }

            LegalMove move = ai.ChooseMove(_game, ActivePlayer);
            if (move.Actor == null) return false;

            // ⚠️ **AI 赌徒的策略**（玩家给的规则）：
            //    "在无法运算出有效数字如 4/5/6/8/9 时，赌徒会非常倾向于开赌。"
            //
            //    必须在**点手之前**判断：一旦点了自己的手，
            //    强制状态下就直接进赌博阶段了，就来不及比较"运算 vs 赌博"。
            if (ShouldAiGamble(ActivePlayer))
            {
                _lastAiMoveDiagnostic =
                    $"AI-GAMBLE actor={ActivePlayer?.Name} must={ActivePlayer.MustGamble}";

                // 先点一只手（赌博需要一个运算源），再开赌
                if (!ClickHand(move.ActedHand)) return false;

                // 强制状态下点手即进 ChoosingGamble；非强制时进 ChoosingTargetHand ——
                // 两个阶段都允许开赌（见 CanGamble），所以这里统一处理。
                if (Phase != BoardPhase.ChoosingGamble
                    && Phase != BoardPhase.ChoosingTargetHand)
                {
                    ResolvePendingAiChoices();
                    return true;
                }

                if (!Gamble()) return false;

                if (Phase == BoardPhase.ChoosingTargetPlayer)
                {
                    if (DefenderCannotBeAttacked) GiveUpAttack();
                    else
                    {
                        if (_selectedTarget == null)
                            _selectedTarget = TargetPlayer?.LeftHand ?? Defender?.LeftHand;
                        ConfirmTargetPlayer();
                    }
                }

                ResolvePendingAiChoices();
                return true;
            }

            // 诊断字符串在**每个失败点当场重写** ——
            // 只在开头写一次会留下上一次成功调用的旧值，
            // 于是"显示的 phase 与真正失败的位置不一致"（我为此白查过一轮）。
            if (!ClickHand(move.ActedHand))
            {
                _lastAiMoveDiagnostic =
                    $"第①步失败 phase={Phase} actor={ActivePlayer?.Name}" +
                    $" acted={move.ActedHand?.Value}" +
                    $" own={ActivePlayer?.Owns(move.ActedHand)}";
                return false;
            }

            // 走到这里可能已经停在"选锁手"阶段（合盾且是野蛮人）
            ResolvePendingAiChoices();

            // ⚠️ **AI 赌徒**：点完自己的手会停在"等开赌"，而 AI 不会点按钮 ——
            //    不处理就会**卡死在 ChoosingGamble**（我在实现时预判到的风险）。
            //
            //    这里直接代它点一次「开赌」，并**立刻**处理它可能带来的后续阶段：
            //    · 掷出攻击 -> 停在"选打谁" -> 这里直接确认默认目标
            //    · 其余（合盾 / 回血 / 无效果）-> Gamble 内部已经结算完
            if (Phase == BoardPhase.ChoosingGamble)
            {
                if (!Gamble()) return false;

                if (Phase == BoardPhase.ChoosingTargetPlayer)
                {
                    if (DefenderCannotBeAttacked) GiveUpAttack();
                    else
                    {
                        if (_selectedTarget == null)
                            _selectedTarget = TargetPlayer?.LeftHand ?? Defender?.LeftHand;
                        ConfirmTargetPlayer();
                    }
                }

                ResolvePendingAiChoices();
                return true;
            }

            if (Phase != BoardPhase.ChoosingTargetHand) return true;

            // ⚠️ 诊断必须**在失败点当场重新记录** ——
            //    否则读到的是上一次成功调用留下的旧值（我踩过：
            //    诊断显示的 phase 与实际失败点不一致，白查一轮）。
            _lastAiMoveDiagnostic =
                $"FAIL-STEP2 phase={Phase} actor={ActivePlayer?.Name}" +
                $" current={_game.CurrentPlayer?.Name}" +
                $" acted={move.ActedHand?.Value}" +
                $" target={move.TargetHand?.Value}" +
                $" sameAsSelected={ReferenceEquals(move.TargetHand, SelectedHand)}" +
                $" targetLocked={move.TargetHand?.IsLocked}" +
                $" ownsTarget={ActivePlayer?.Owns(move.TargetHand)}" +
                $" isGameOver={_game.IsGameOver}";

            if (!ClickHand(move.TargetHand))
            {
                // 没有合法目标时 AI 不应静默卡死；有「放弃攻击」出口就直接用。
                if (CanGiveUpAttack)
                {
                    GiveUpAttack();
                    return true;
                }
                return false;
            }

            // 第 ③ 步：AI 也要走完"确认目标玩家"。
            // 没有合法目标（刺客潜行）时直接放弃本回合。
            if (Phase == BoardPhase.ChoosingTargetPlayer)
            {
                if (DefenderCannotBeAttacked) GiveUpAttack();
                else ConfirmTargetPlayer();
            }

            // 可能停在"选盾"阶段（防守方是 AI）
            ResolvePendingAiChoices();
            return true;
        }

        /// <summary>
        /// AI 的妖术师：**安全**且对手体力比自己高 **8 以上**时跳过回合，
        /// 把对手的体力拉低到与自己一致。
        ///
        /// 玩家给的策略："妖术师会在安全的情况下，和对手血量相差 8 以上时
        /// 跳过回合（让对手血量减少）"。
        ///
        /// 三条判据：
        ///   · 对手体力 **> 自己 + 8**（差值小于 8 收益太小，不值得用掉整个回合）
        ///   · **安全**：跳过等于把回合白送给对方，所以自己不能处在"会被一波带走"
        ///     的危险区间 —— 低于 <see cref="AiDangerHealth"/> 时不跳
        ///   · 对手必须比自己血多（否则这一改是**帮对方回血**，见下一个注释）
        /// </summary>
        private bool TryAiHexerSkip()
        {
            if (!(ActivePlayer?.Character is HexerLogic)) return false;
            if (!CanSkipTurn) return false;

            // ⚠️ **安全性**：跳过 = 白送一个回合给对方。
            //    自己血太少时这么干等于自杀，所以宁可正常打（还能合盾 / 输出）。
            if (ActivePlayer.Health < AiDangerHealth) return false;

            // 玩家给的判据："血量比敌人少的值大于等于**自己能打出的最大伤害**"
            // —— 用"最大伤害"而不是固定数字，因为它随角色 / 手值变化。
            int maxDamage = MaxDamageICanDeal(ActivePlayer);

            // ① 优先把**危险的队友**拉上来（玩家要求）：
            //    "队友血量若处于危险值且自己体力比队友多大于 9 则大幅度倾向于拉队友"
            PlayerState teammate = FindDangerousTeammate(ActivePlayer);
            if (teammate != null) return DoHexerSkip(teammate);

            // ② 否则拉一个"血量比自己多至少 maxDamage"的对手
            PlayerState best = null;
            IReadOnlyList<PlayerState> all = _game.Players;
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, ActivePlayer)) continue;
                if (_game.IsTeammate(ActivePlayer, p)) continue;

                if (p.Health - ActivePlayer.Health < maxDamage) continue;

                if (best == null || p.Health > best.Health) best = p;
            }

            if (best == null) return false;

            return DoHexerSkip(best);
        }

        /// <summary>进入相位并发动跳过（复用人类那条路径，避免两套逻辑漂移）。</summary>
        private bool DoHexerSkip(PlayerState target)
        {
            if (target == null) return false;
            if (!SkipTurn()) return false;
            return ApplyHealthSwapAndEndTurn(target);
        }

        /// <summary>
        /// **我这一手能打出的最大伤害**（妖术师的跳过判据用它）。
        ///
        /// 估算方式：手值里最高的那个直接当攻击数字（4/6/8 才是攻击，
        /// 所以取"能凑到的最好的攻击值"），再加攻防。
        ///
        /// ⚠️ 这是**保守估算** —— 宁可高估也不要低估：
        ///    高估只是少跳一次（放着好机会没用），
        ///    低估会在"其实打得死"的时候白送一个回合给对手。
        /// </summary>
        private static int MaxDamageICanDeal(PlayerState me)
        {
            if (me?.Character == null) return 8;

            int atk = me.Character.AttackBonus;

            // 攻击效果只有 4 / 6 / 8，取最大的 8
            // （手上有 8，或者能凑出 8 —— 这里用"攻击距离内的对手手值"不划算，
            //  所以直接按最大攻击档位算，属于保守高估）
            return System.Math.Max(0, 8 + atk);
        }

        /// <summary>
        /// 找一个"处于危险值、且自己血量比它多 9 以上"的队友（玩家给的规则）。
        ///
        /// 危险值沿用 <see cref="AiDangerHealth"/>：低于它就该被拉上来。
        /// </summary>
        private PlayerState FindDangerousTeammate(PlayerState me)
        {
            IReadOnlyList<PlayerState> all = _game.Players;
            PlayerState best = null;

            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null || !p.IsAlive) continue;
                if (ReferenceEquals(p, me)) continue;
                if (!_game.IsTeammate(me, p)) continue;

                // 队友处于危险值
                if (p.Health > AiDangerHealth) continue;

                // 自己比队友多 9 以上（否则拉上来等于把自己拉下去，不划算）
                if (me.Health - p.Health <= HexerTeammatePullGap) continue;

                if (best == null || p.Health < best.Health) best = p;
            }

            return best;
        }

        /// <summary>拉队友的血量差门槛（玩家给的数：9）。</summary>
        public int HexerTeammatePullGap { get; set; } = 9;

        /// <summary>
        /// 妖术师 AI 跳过的**血量差阈值**（玩家给的数：8）。
        ///
        /// 调平衡改这里 —— 调大 = 更保守（更少跳），调小 = 更激进。
        /// </summary>
        public int HexerSkipHealthGap { get; set; } = 8;

        /// <summary>
        /// AI 认为"自己血太少、不该白送回合"的血量线。
        ///
        /// 跳过回合 = 把主动权交给对方，所以自己必须还有本钱。
        /// 默认 8：低于它时优先正常打（合盾 / 输出 / 回复都可能救命）。
        /// </summary>
        public int AiDangerHealth { get; set; } = 8;

        /// <summary>
        /// **赌徒是否应该赌博**（AI 策略）。
        ///
        /// 玩家给的策略："在无法运算出有效数字如 4/5/6/8/9 时，
        /// 赌徒会**非常倾向于**开赌。"
        ///
        /// 判据：枚举当前所有合法运算，看有没有哪一步的结果落在
        /// 有效数字集合 {4,5,6,8,9} 里。**一个都没有** -> 开赌。
        ///
        /// 理由很直白：正常运算既然出不了任何有用的东西，
        /// 还不如掷 1d9 —— 9 个面里有 5 个是有用的（4/5/6/8/9）。
        /// </summary>
        private bool ShouldAiGamble(PlayerState actor)
        {
            if (!(actor?.Character is GamblerLogic)) return false;
            if (actor.MustGamble) return true;      // 强制时没得选

            IReadOnlyList<LegalMove> moves = _game.GetLegalMoves(actor);
            if (moves.Count == 0) return true;      // 连运算都做不了 -> 赌

            // 玩家给的规则："赌徒在**无法运算出高价值点数**时优先赌。"
            //
            // ⚠️ 判据是**高价值**，不是"有效果"。
            //    我原来用的是"有没有产生效果"（4/5/6/8/9 都算），
            //    而那个条件在实战里几乎总成立（4/6/8 攻击、5 合盾、9 回复），
            //    于是赌徒**从不主动开赌** —— 30 局发动率 0%。
            //
            //    高价值 = **能推进战局**的：4/6/8 的攻击。
            //    合盾（5）与纯回复（9）不算 —— 它们不改变局面，
            //    而赌博有 4/9 的概率直接掷出攻击，值得一赌。
            for (int i = 0; i < moves.Count; i++)
            {
                LegalMove m = moves[i];
                if (m.ActedHand == null || m.TargetHand == null) continue;

                int result = Hand.ComputeSum(m.ActedHand.Value, m.TargetHand.Value);
                if (IsHighValueResult(result)) return false;   // 能打出高价值 -> 不赌
            }

            return true;                            // 打不出高价值 -> 开赌
        }

        /// <summary>
        /// 这个数字算不算**高价值**（能推进战局）。
        ///
        /// 只算攻击（4/6/8）—— 合盾与纯回复不改变局面，
        /// 而赌博有 4/9 的概率直接掷出攻击。
        ///
        /// ⚠️ 与 <see cref="IsUsefulGambleResult"/> 的区别：
        ///    那个是"有没有效果"（含合盾与回复），
        ///    这个是"值不值得为它放弃赌博"。两者用途不同，不要合并。
        /// </summary>
        private static bool IsHighValueResult(int result)
            => result == 4 || result == 6 || result == 8;

        /// <summary>
        /// 这个数字算不算"有效结果"（玩家列举：4/5/6/8/9）。
        ///
        /// 1/2/3/7 是"无效果"，所以不在里面。
        /// 直接按**效果类型**判断，与赌博的效果规则共用一套，
        /// 避免"两处各写一份而漂移"（列表变了只要改效果判定）。
        /// </summary>
        private static bool IsUsefulGambleResult(int value)
            => Hand.GetEffectType(value) != HandEffectType.None;

        /// <summary>
        /// 诊断用：最近一次 <see cref="MakeAutomaticMove"/> 里 AI 选出的那一手。
        ///
        /// 排查"AI 出手卡住"时靠它看到底是哪一步失败 ——
        /// 只看"MakeAutomaticMove 返回 false"是定位不到原因的。
        /// </summary>
        public string LastAiMoveDiagnostic => _lastAiMoveDiagnostic;

        private string _lastAiMoveDiagnostic = "";

        /// <summary>如果当前停留在需要 AI 抉择的阶段，代它决定并继续。</summary>
        private void ResolvePendingAiChoices()
        {
            ResolvePendingAiChoice();
        }

        /// <summary>
        /// 让 AI 完成当前阶段需要的那个抉择（选盾 / 角色抉择），然后继续流程。
        ///
        /// 用于"AI 只需要代做一个决定"的场景 —— 典型例子是 **AI 攻击、人类防守**：
        /// 此刻行动方是 AI，但该做决定的是防守方（人类），
        /// 所以既不能由 AI 出招，也不能等 AI 自己动。
        /// 返回是否真的处理了一个抉择。
        /// </summary>
        public bool ResolvePendingAiChoice()
        {
            if (_game.IsGameOver) return false;

            if (IsInChoicePhase)
            {
                if (GetAi(_pendingChoiceDecider) == null) return false;
                ResolvePreTurnChoiceIfAi();
                return true;
            }

            if (Phase == BoardPhase.ChoosingDefenseShield)
            {
                if (GetAi(Defender) == null) return false;
                ResolveDefenseChoiceIfAi();
                return true;
            }

            return false;
        }

        /// <summary>记录防守方选定的盾。供 UI 在 <see cref="BoardPhase.ChoosingDefenseShield"/> 阶段调用。</summary>
        public void ApplyDefenseChoice(Hand shield)
        {
            _chosenDefenseShield = shield;
            SinkFor(Defender)?.SetChoice(HandEffect.ShieldBlock, shield);
        }

        /// <summary>
        /// 若防守方是 AI，则替它选一面盾然后继续结算。
        /// 防守方是人类时什么都不做，等 UI 调用 <see cref="ApplyDefenseChoice"/> + <see cref="ClickHand"/>。
        ///
        /// 注意：这里**不能**再回调 <see cref="BeginCharacterChoices"/> ——
        /// 角色抉择（闪避等）在进入选盾阶段之前就已经问完了，
        /// 回到收集只会无限递归。
        /// </summary>
        private void ResolveDefenseChoiceIfAi()
        {
            if (GetAi(Defender) == null) return;   // 人来做决定

            // ⚠️ 守卫：没有运算源就不该结算。
            //
            // 这里是**第 5 个** `ExecuteResolvedTurn()` 调用点，原来只有它没有守卫 ——
            // 于是 `SelectedHand == null` 时会抛"未选择运算源"，
            // 被防御性 catch 接住后记进战报（玩家实测 + 批量测试都抓到过）。
            //
            // 触发场景：AI 防守时被叫来选盾，但本回合的运算源在别处已被清掉
            // （CancelSelection / 上一次结算的收尾 / SetupFirstStep）。
            if (SelectedHand == null || _selectedTarget == null || _shieldChoices.Count == 0)
            {
                _chosenDefenseShield = null;
                _shieldChoices.Clear();
                SetupFirstStep();
                return;
            }

            Hand pick = AiResolver?.ChooseShieldToBlock(Defender, _shieldChoices);

            ApplyDefenseChoice(ContainsHand(_shieldChoices, pick) ? pick : _shieldChoices[0]);
            ExecuteResolvedTurn();
        }

        /// <summary>
        /// 真正执行一次运算。要求 <see cref="SelectedHand"/> 与 <c>_selectedTarget</c> 已确定，
        /// 且所有需要玩家决定的抉择都已写入 <see cref="IPlayerChoiceSink"/>。
        /// </summary>
        /// <summary>
        /// **最终防线**：状态不完整就不结算。
        ///
        /// 已确认的成因（靠执行时抓 StackTrace 定位，调用栈完整）：
        ///   `MakeAutomaticMove -> ClickHand -> BeginPreTargetChoicesIfAny`
        ///   `-> AdvanceCharacterChoice -> ResolvePreTurnChoiceIfAi`
        ///   `-> AdvanceCharacterChoice -> ExecuteResolvedTurn`
        /// 也就是 **AI 在"选目标手之前的抉择"里作答时，会递归回到
        /// `AdvanceCharacterChoice`**，而那条路径上 `SelectedHand` /
        /// `_selectedTarget` 可能已经被中间的重建（SetupFirstStep 等）清掉。
        ///
        /// 与其把 null 交给 <see cref="GameManager"/>（它会抛"未选择运算源"，
        /// 被 catch 吞掉后只在战报里留一句话，极难排查），
        /// 不如在这里**明确记录并回到本回合第一步**。
        /// </summary>
        /// <summary>
        /// 把一次击杀折算成额外行动队列。
        ///
        ///   · 击杀不同阵营：行动方额外行动 X 次，X = 受害者阵营**击杀后**存活人数；
        ///   · 击杀同阵营：从行动方下家开始，其他阵营每个存活角色各行动一次；
        ///   · 只有直接攻击击杀算（宿主在 ExecuteResolvedTurn 里先做血量快照）。
        /// </summary>
        private void QueueKillRewards(TurnResult result,
                                      Dictionary<PlayerState, int> healthBefore)
        {
            if (!KillRewardPlanner.IsEnabled(_game)) return;

            List<PlayerState> rewards = KillRewardPlanner.Build(_game, result, healthBefore);
            if (rewards.Count == 0) return;

            PlayerState killer = result.Actor;
            PlayerState victim = result.Defender;
            int killerTeam = _game.TeamOf(killer);
            int victimTeam = _game.TeamOf(victim);

            // 嵌套奖励优先于当前队列剩余部分：立即插入队首。
            _extraQueue.InsertRange(0, rewards);

            string why;
            if (_game.IsIdentityMode)
            {
                why = _game.IdentityOf(victim) == IdentityRole.Rebel
                    ? $"击杀反贼，额外行动 {rewards.Count} 次"
                    : "主公击杀忠臣，所有反贼各行动一次";
            }
            else
            {
                why = victimTeam == killerTeam
                    ? $"其他阵营各行动一次（{rewards.Count} 次）"
                    : $"额外行动 {rewards.Count} 次";
            }
            _log.Add($"【击杀奖惩】{killer.Name} 击杀 {victim.Name}：{why}");
        }

        /// <summary>开始队列里的下一次额外行动；队列空则恢复正常的下一回合。</summary>
        private void BeginNextExtraActionOrResume()
        {
            while (_extraQueue.Count > 0)
            {
                PlayerState next = _extraQueue[0];
                _extraQueue.RemoveAt(0);
                if (next == null || !next.IsAlive) continue;

                _extraActionActive = true;
                _game.ForceActivePlayerForTest(next);
                BeginTurn();          // 只重置本回合标记，不推进 TurnCount / RoundCount
                return;
            }

            _extraActionActive = false;
            if (_resumePlayer != null) _game.ForceActivePlayerForTest(_resumePlayer);
            BeginTurn();
        }

        private void ExecuteResolvedTurn()
        {
            // ⚠️ 用**本回合的副本**兜底。
            //
            // `SelectedHand` / `_selectedTarget` 可能在"选目标手之前的抉择"
            // 递归回到 AdvanceCharacterChoice 时被中间的重建清掉
            // （已确认的调用栈：BeginPreTargetChoicesIfAny ->
            //   AdvanceCharacterChoice -> ResolvePreTurnChoiceIfAi ->
            //   AdvanceCharacterChoice -> 这里）。副本只在回合结束时清，
            // 所以这里一定拿得到本回合真正要运算的两只手。
            Hand acted = SelectedHand ?? _turnActedHand;
            Hand target = _selectedTarget ?? _turnTargetHand;

            if (acted == null || target == null)
            {
                LastIllegalOperation =
                    $"BLOCKED-NULL-SOURCE entryPhase={Phase}" +
                    $" actor={ActivePlayer?.Name}" +
                    $" pendingIdx={_pendingRequestIndex}/{_pendingRequests.Count}" +
                    $" preTargetActive={_preTargetChoiceActive}" +
                    $" trace=[{string.Join(" | ", _advanceTrace)}]";

                _log.Add($"结算被拦截：运算源或目标手缺失（行动方=" +
                         $"{ActivePlayer?.Name}）。已回到本回合第一步。");

                SelectedHand = null;
                _selectedTarget = null;
                _chosenDefenseShield = null;
                _shieldChoices.Clear();
                _preTargetChoiceActive = false;
                SetupFirstStep();
                return;
            }

            // 入口就把阶段记下来 —— 异常处理里的 `Phase` 可能已被改动，
            // 只看那一刻会得到**陈旧阶段**（我为此白查两轮）。
            string phaseAtEntry = Phase.ToString();

            // 诊断：入口时把"待答抉择"的状态一起记下来 ——
            // 只有知道 pendingIdx / preTargetActive 才能判断是哪条路径
            // 走到了"运算源缺失"（战报里没有这些信息，定位不了）。
            string resolveStateAtEntry =
                $"pendingIdx={_pendingRequestIndex}/{_pendingRequests.Count}" +
                $" preTargetActive={_preTargetChoiceActive}" +
                $" inChoicePhase={IsInChoicePhase}" +
                $" answered={_answeredThisTurn.Count}";

            // "放弃攻击"：运算照常发生（手值改变），但不造成伤害。
            // 用 GameManager 的开关在分派攻击时短路，而不是跳过整个回合 ——
            // 跳过整个回合会让手值回退（玩家实测报的 bug）。
            //
            // ⚠️ **法师的"放弃攻击"也走这条开关，但它在别处设。**
            //
            //    玩家确认：放弃攻击 = "运算照常、手值照常改变，但不造成伤害"，
            //    只是**多了一个副作用**：为下一次攻击记下伤害类型。
            //
            //    ⚠️ 但**不能在这里读法师的标记** —— 此处是 ResolveTurn 的**入口**，
            //       结果还没算出来、`GameManager.ResolveMageStatusIntent`
            //       还没跑，标记必然是 false（实测 24961 次法师回合里
            //       suppress 一次都不成立）。真正的设置点在
            //       `GameManager.ResolveMageStatusIntent`（结果算完之后）。
            _game.SuppressAttackDamage = _giveUpAttack;

            // 本次攻击的距离覆盖（战士碎盾换射程 / 游侠强化）。
            // 必须与 CanBeAttacked 看到的是同一个值，否则会出现
            // "界面允许选、结算却打不到"的不一致。
            _game.RangeOverride = CurrentRangeOverride();

            // 挨打的是**选定的目标玩家**，不是"被加数的手主"。
            //
            // 玩家确认的交互：② 点任意人的手当被加数，③ 单独选攻击目标。
            // 两者可以是不同的人（例如用距离 2 的 C 的数字、打距离 1 的 B）。
            // 不显式传的话结算会回退到"被加数手主"，于是界面显示打 B、
            // 实际却把伤害算在 C 头上。
            //
            // 选目标阶段（还没确认）用被加数手主兜底，与 CanBeAttacked 的视角一致。
            _game.DamageTargetPlayer = TargetPlayer ?? OwnerOf(_selectedTarget);

            // 击杀判定要比较行动前后的血量；先给全部玩家拍快照。
            var healthBefore = new Dictionary<PlayerState, int>(_game.Players.Count);
            for (int i = 0; i < _game.Players.Count; i++)
            {
                PlayerState p = _game.Players[i];
                if (p != null) healthBefore[p] = p.Health;
            }
            bool wasExtra = _extraActionActive;

            TurnResult result;
            try
            {
                // ⚠️ **赌博走另一条入口，而且绝不能在这里再掷一次**。
                //
                //    点数是"开赌"那一次掷的，存在 `_pendingGambleRoll` 里；
                //    掷出攻击（4/6/8）时流程是"开赌 -> 选打谁 -> 结算"，
                //    掷点与结算**分属两次调用** ——
                //    这里若调 ExecuteGamble 会重新掷，玩家看到的点数
                //    与实际结算的点数就不同了
                //    （玩家实测："先说投出了 4，点战士时又说投出 2 无效果"）。
                result = _pendingGambleRoll > 0
                    ? _game.ExecuteGambleWithRoll(ActivePlayer, acted,
                                                  _pendingGambleRoll, _selectedTarget,
                                                  extraAction: wasExtra)
                    : _game.ExecuteTurn(ActivePlayer, acted, target,
                                        extraAction: wasExtra);

                // ⚠️⚠️ **就在这一刻把"谁可以发动吟游诗人技能"记下来。**
                //
                //    `TookDamageThisTurn` / `HealedThisTurn` 会在**下一次
                //    `BeginTurn()`**（换人时）被清掉，而换人发生在
                //    `ExecuteResolvedTurn` 的收尾 —— 早于界面的 `RefreshView`。
                //    所以刷新时再去读标记，伤害早就被清干净了
                //    （玩家实测："第一次受伤也没跳出提示"）。
                SnapshotBardEligibility();
            }
            catch (InvalidOperationException ex)
            {
                // 防御性：正常流程走不到这里，但绝不能让界面卡死。
                // 一定要把原因带出来 —— 静默吞掉会让"点击没反应"变得极难排查。
                //
                // ⚠️ 现场信息**当场记进字段**（不只是战报），
                //    这样测试能直接断言"有没有出现过非法运算"并看到现场。
                LastIllegalOperation =
                    $"entryPhase={phaseAtEntry} actor={ActivePlayer?.Name}" +
                    $" acted={(acted == null ? "NULL" : acted.Value.ToString())}" +
                    $" target={(target == null ? "NULL" : target.Value.ToString())}" +
                    $" selected={SelectedHand?.Value.ToString() ?? "NULL"}" +
                    $" selectedTarget={_selectedTarget?.Value.ToString() ?? "NULL"}" +
                    $" answer={ex.Message} state=[{resolveStateAtEntry}]" +
                    $" trace=[{string.Join(" | ", _advanceTrace)}]";

                // 战报里也带上**阶段**与两只手的来源 ——
                // 只说"未选择运算源"看不出是哪个阶段出的问题。
                _log.Add($"非法运算被拒绝[{Phase}]：{ex.Message}（行动方=" +
                         $"{ActivePlayer?.Name}，运算源={acted?.Value}，" +
                         $"目标={target?.Value}，SelectedHand=" +
                         $"{SelectedHand?.Value.ToString() ?? "NULL"}，" +
                         $"selectedTarget={_selectedTarget?.Value.ToString() ?? "NULL"}）");
                System.Diagnostics.Debug.WriteLine($"[FingerDiceDuel] 非法运算：{ex}");
                SelectedHand = null;
                _selectedTarget = null;
                _chosenDefenseShield = null;
                _shieldChoices.Clear();
                SetupFirstStep();
                return;
            }
            finally
            {
                // 必须复位，否则会污染后续所有回合
                _game.SuppressAttackDamage = false;
                _game.RangeOverride = -1;
                _game.DamageTargetPlayer = null;
                _giveUpAttack = false;
            }

            LastTurn = result;
            LastTurnNumber = _game.TurnCount;

            // 法师的状态体系：记下"这一位的回合该在换人时结算结束"
            _statusTurnEndFor = result.Actor;

            // ⚠️ **赌徒的强制赌博状态**（玩家确认的规则）：
            //    "若赌徒上一个自己的回合进行了正常的运算（非赌博），
            //     那么这一回合就必须赌博"；"赌完就解除"。
            //
            //    跳过回合**不算**正常运算（不触发强制）——
            //    所以这里只看"有没有产生 TurnResult 且不是赌博"。
            //    放在结算收尾处：这是唯一能确定"这一回合到底做了什么"的时点。
            if (result.Actor?.Character is GamblerLogic)
            {
                result.Actor.MustGamble = !result.WasGamble;
            }
            string extraPrefix = result.IsExtraAction ? "【额外行动】" : "";
            foreach (string line in DescribeTurnLines(result)) _log.Add(extraPrefix + line);

            // 圣武士的冲击不占一次运算，所以不在 TurnResult 里 ——
            // 单独把本回合的冲击记录补进战报（玩家要求能看见它是否发生）。
            foreach (string shock in _game.TakeShockLog()) _log.Add(shock);

            // 回合真正结束 -> 副本可以清了（它只在回合内兜底用）
            _turnActedHand = null;
            _turnTargetHand = null;

            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();

            if (_game.IsGameOver)
            {
                _extraQueue.Clear();
                _extraActionActive = false;
                Phase = BoardPhase.GameOver;
                Hint = WinMessage();
                _log.Add(Hint);
                return;
            }

            // 击杀奖惩：只有直接攻击击杀会进队列。
            QueueKillRewards(result, healthBefore);

            if (wasExtra)
            {
                // 额外行动结束：继续队列，或恢复正常的下一回合。
                BeginNextExtraActionOrResume();
                return;
            }

            // 普通行动：GameManager 已经把 CurrentPlayer 推到下一位；
            // 先记下来，等额外行动队列跑完后恢复。
            _resumePlayer = _game.CurrentPlayer;

            if (_extraQueue.Count > 0)
            {
                BeginNextExtraActionOrResume();
                return;
            }

            BeginTurn();
        }

        // 注：此前这里有一个 RequiresLockChoice()，用 `is BarbarianLogic` 硬编码判断
        //     "野蛮人是否需要额外问一次锁手"。现已改为由角色自己通过
        //     IPreTurnChoiceProvider 声明（见 BeginCharacterChoices），
        //     控制器不再认识任何具体角色类型。

        private static bool ContainsHand(IReadOnlyList<Hand> list, Hand hand)
        {
            if (list == null || hand == null) return false;

            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], hand)) return true;

            return false;
        }

        /// <summary>
        /// 这次运算是否需要先问"用哪面盾挡"。
        /// 只有在"会打出一次攻击"且"防守方两面都是成型盾"时才需要。
        /// 咒术师无视护盾时多问一次是无害的 —— 护盾不会被消费。
        /// </summary>
        private bool RequiresDefenseChoice(Hand acted, Hand target, out List<Hand> shields)
        {
            shields = null;

            if (acted == null || target == null) return false;

            int value = Hand.ComputeSum(acted.Value, target.Value);
            if (!IsAttack(Hand.GetEffectType(value))) return false;

            PlayerState defender = Defender;
            if (!defender.HasDoubleShield) return false;

            shields = new List<Hand>(2) { defender.LeftHand, defender.RightHand };
            return true;
        }

        private static bool IsAttack(HandEffectType effect)
            => effect == HandEffectType.Attack4
               || effect == HandEffectType.Attack6
               || effect == HandEffectType.Attack8;

        private string WinMessage()
        {
            if (!_game.IsGameOver) return "";

            if (_game.Winner == null) return "平局 —— 双方同时倒下。";
            return $"{_game.Winner.Name} 获胜！";
        }

        // ============================================================
        // 文本
        // ============================================================

        private static string SideLabel(Hand hand, PlayerState owner)
            => owner != null && ReferenceEquals(hand, owner.LeftHand) ? "左手" : "右手";

        /// <summary>
        /// 把一次结算结果转成人话（可能**多行**，例如剑客的追加攻击单独一行）。
        /// 由 <see cref="ExecuteResolvedTurn"/> 逐行写进战报。
        /// </summary>
        public static IReadOnlyList<string> DescribeTurnLines(TurnResult t)
        {
            var lines = new List<string>(2);
            if (t == null || t.Actor == null) return lines;

            // 追加攻击发生在主攻击之前，所以先写它
            if (t.ExtraAttack.HasValue)
            {
                ExtraAttackInfo extra = t.ExtraAttack.Value;
                lines.Add(extra.WasBlocked
                    ? $"{extra.ActorName}：【追加】{extra.Damage} 点固定伤害（无视攻防），被护盾挡下"
                    : $"{extra.ActorName}：【追加】{extra.Damage} 点固定伤害（无视攻防），造成 {extra.Dealt} 点伤害");
            }

            lines.Add(DescribeTurn(t));
            return lines;
        }

        /// <summary>
        /// 多段攻击的结果描述（忍者）。
        ///
        /// 例："两段攻击：一段被护盾挡下，另一段造成 3 点伤害"
        ///     "两段攻击：两段各造成 3 点伤害"
        /// </summary>
        private static string DescribeMultiHit(TurnResult t)
        {
            int hits = t.HitCount;
            int landed = t.LandedHits;
            int blocked = t.BlockedHits;
            int dodged = t.DodgedHits;

            var parts = new List<string>(3);
            if (blocked > 0) parts.Add($"{CountWord(blocked)}被护盾挡下");
            if (dodged > 0) parts.Add($"{CountWord(dodged)}被闪避");
            if (landed > 0) parts.Add($"{CountWord(landed)}造成 {t.DamageDealt} 点伤害");

            string result = parts.Count == 0 ? "全部落空" : string.Join("，", parts);
            return $"{CountWord(hits)}攻击：{result}";
        }

        /// <summary>把段数说成"一段/两段/三段"。</summary>
        private static string CountWord(int n)
        {
            switch (n)
            {
                case 1: return "一段";
                case 2: return "两段";
                case 3: return "三段";
                case 4: return "四段";
                default: return $"{n} 段";
            }
        }

        /// <summary>把一次结算结果转成一句人话（单行，不含追加攻击）。</summary>
        public static string DescribeTurn(TurnResult t)
        {
            if (t == null || t.Actor == null) return "";

            string side = SideLabel(t.ActedHand, t.Actor);

            // ⚠️ **赌博不是运算**，不能写成 "N + M = K" —— 那会让玩家以为
            //    自己算了一手（赌博时根本没点他人的手）。
            //    玩家要求战报里能看出这是赌博。
            string head;
            if (t.WasGamble)
            {
                head = $"{t.Actor.Name} 开赌：掷出 {t.GambleRoll}";
            }
            else
            {
                // ⚠️⚠️ **算式必须自洽**：`A + B = C` 里的 C 要等于 A、B 归一化后的和。
                //
                //    玩家实测看到过 `1 + 4 = 2（无效果）` —— 1+4 应当是 5。
                //    根因是"运算源 / 被加数在快照之后又被改过"（某个效果改了手值），
                //    于是 `ResultValue` 与两个操作数对不上。
                //
                //    战报是对玩家最直接的反馈，**不能自相矛盾**：
                //    这里用快照的两个操作数**重算**一次显示用的结果；
                //    与真实结果不一致时把真实结果也标出来，
                //    这样既能读懂、又能保留"实际发生了什么"。
                int shownResult = Hand.ComputeSum(t.ActedHandValueBefore, t.TargetHandValue);

                string sum = shownResult == t.ResultValue
                    ? shownResult.ToString()
                    : $"{shownResult}（实际结算为 {t.ResultValue} —— 手值在运算中被改过）";

                head = $"{t.Actor.Name} {side}：{t.ActedHandValueBefore} + {t.TargetHandValue} = {sum}";
            }

            switch (t.Effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
                    // ⚠️ **被妖术师取消**要最先说 —— 玩家要求：
                    //    "可以写被妖术师取消，造成 0 点伤害"。
                    //    放在最前面，否则会被"造成 0 点伤害"那类分支抢先描述。
                    if (t.WasCancelled)
                    {
                        string by = string.IsNullOrEmpty(t.CancelledBy)
                            ? "妖术师"
                            : t.CancelledBy;
                        return head + $"（攻击，被{by}取消，造成 0 点伤害）";
                    }

                    // ⚠️ 牧师可以把 4/6/8 **当回复用**。
                    // 那种情况下 `Effect` 仍然是 Attack4/6/8（运算结果没变），
                    // 只是 `HealTarget` 被设上了 —— 所以必须**先**判回复，
                    // 否则战斗记录会永远写成"攻击，造成 0 点伤害"（玩家实测 bug）。
                    if (t.HealTarget != null)
                    {
                        int amount = t.HealAmount;

                        if (ReferenceEquals(t.HealTarget, t.Actor))
                            return head + $"（回复自己 {amount} 点生命）";

                        return head + $"（回复 {t.HealTarget.Name} {amount} 点生命）";
                    }

                    if (t.WasDodged)
                    {
                        // 闪避与"潜行免伤"共用同一条通道，所以要说清是哪一种
                        string reason = string.IsNullOrEmpty(t.DodgeReason)
                            ? "被游侠闪避"
                            : t.DodgeReason;
                        return head + $"（攻击，{reason}）";
                    }

                    // 多段攻击（忍者）：说清"发动了几段、各是什么结果"
                    if (t.HitCount > 1) return head + "（" + DescribeMultiHit(t) + "）";

                    return t.WasBlocked
                        ? head + "（攻击，被护盾挡下）"
                        : head + $"（攻击，造成 {t.DamageDealt} 点伤害）";

                case HandEffectType.Shield:
                    return head + "（合拢手掌，形成护盾）";

                case HandEffectType.Heal:
                    {
                        // 牧师可以把 9 回给别人
                        bool toOther = t.HealTarget != null
                                       && !ReferenceEquals(t.HealTarget, t.Actor);

                        if (toOther)
                            return head + $"（回复 {t.HealTarget.Name} {t.HealAmount} 点生命）";

                        return t.Healed > 0
                            ? head + $"（回复 {t.Healed} 点生命）"
                            : head + $"（回复 {t.HealAmount} 点，但已满血 —— 本回合被浪费）";
                    }

                default:
                    // 猎人伏击：效果类型本身没变（仍是无效果），
                    // 但他把那只手**暗置**了 —— 战报必须说出来。
                    if (t.EnteredAmbush) return head + "（进入伏击）";
                    return head + "（无效果）";
            }
        }
    }
}
