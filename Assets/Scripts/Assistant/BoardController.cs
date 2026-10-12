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

        /// <summary>给玩家的一句提示。</summary>
        public string Hint { get; private set; } = "";

        public IReadOnlyList<string> Log => _log;

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

        /// <summary>防守方（与 <see cref="ActivePlayer"/> 相对）。</summary>
        public PlayerState Defender => _game.GetOpponent(ActivePlayer);

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
               && Phase != BoardPhase.GameOver
               && GameManager.CanSkipTurn(ActivePlayer);

        /// <summary>
        /// 跳过当前回合（不执行运算，直接把回合交给对方）。
        /// 仅在 <see cref="CanSkipTurn"/> 为 true 时有效。
        /// </summary>
        public bool SkipTurn()
        {
            if (!CanSkipTurn) return false;

            _log.Add($"{ActivePlayer.Name} 选择跳过本回合（双盾在手）。");
            _game.SkipTurn();

            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();

            BeginTurn();
            return true;
        }

        private IMoveSelector GetAi(PlayerState p) => ReferenceEquals(p, PlayerA) ? _aiForA : _aiForB;

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
            if (ReferenceEquals(p, PlayerA))
                return (_aiForA as IChoiceProvider) ?? _choiceA;

            return (_aiForB as IChoiceProvider) ?? _choiceB;
        }

        /// <summary>取某玩家对应的抉择接收者（写入玩家选择用）。</summary>
        private IPlayerChoiceSink SinkFor(PlayerState p)
            => ReferenceEquals(p, PlayerA) ? _sinkA : _sinkB;

        /// <summary>某玩家对应的抉择接收者（读回"已经问过的答案"用，Core 侧接口）。</summary>
        private IPreTurnChoiceSink PreTurnSinkFor(PlayerState p)
            => ReferenceEquals(p, PlayerA)
                ? _sinkA as IPreTurnChoiceSink
                : _sinkB as IPreTurnChoiceSink;

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

        /// <summary>本回合已经答过的抉择（键 = <c>effect#variant</c>）。</summary>
        private readonly Dictionary<string, Hand> _answeredThisTurn =
            new Dictionary<string, Hand>();

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

            ActivePlayer = _game.CurrentPlayer;

            // 注意：这里**不**清空 LastTurn。它的语义是"最近一次已结算的回合"，
            // 要一直保留到下一次结算为止 —— 界面需要在玩家为新回合做选择时，
            // 仍然显示上一回合打出了什么。
            SetupFirstStep();
        }

        private void SetupFirstStep()
        {
            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();
            TargetPlayer = null;      // 目标玩家每个回合重新确认

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
                    return ActivePlayer.Owns(hand) && !hand.IsLocked;

                case BoardPhase.ChoosingTargetHand:
                    // 自己已选的手也允许再点一次（表示取消选择）
                    return Defender.Owns(hand) || ReferenceEquals(hand, SelectedHand);

                case BoardPhase.ChoosingTargetPlayer:
                    // 点目标玩家的手牌即确认；只接受目标玩家自己的手。
                    //
                    // 目标无法被攻击（刺客潜行）时**没有可点的目标** ——
                    // 这正是"没有合法攻击目标"的表现，界面此时只给「放弃攻击」。
                    if (TargetPlayer == null || !TargetPlayer.Owns(hand)) return false;

                    // 攻击数字才能确认攻击；无效果/护盾本来也不会进这个阶段
                    return CanBeAttacked(TargetPlayer);

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

            // 过滤空候选
            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
                if (_pendingRequests[i].Candidates == null || _pendingRequests[i].Candidates.Count == 0)
                    _pendingRequests.RemoveAt(i);

            if (_pendingRequests.Count == 0) return false;

            _preTargetChoiceActive = true;
            AdvanceCharacterChoice();
            return Phase == BoardPhase.ChoosingPreTargetChoice;
        }

        /// <summary>当前是否停在"选目标手之前的抉择"。</summary>
        private bool _preTargetChoiceActive;

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

        /// <summary>某位玩家当前是否可以成为攻击目标。</summary>
        public bool CanBeAttacked(PlayerState player)
        {
            if (player == null) return false;

            // 角色可以令自己免疫（刺客潜行 -> IsImmuneToAttack）。
            // 控制器不认识任何具体角色：Assistant 层不能引用 Characters 层。
            if (player.Character is IAttackTargetRule rule && rule.IsImmuneToAttack(player))
                return false;

            // 日后的"攻击距离不足"等规则加在这里。

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

                // 1v1：唯一的对手。多人模式时改为遍历所有其他玩家。
                return CanBeAttacked(Defender);
            }
        }

        /// <summary>
        /// 界面是否应把**「放弃攻击」**作为出口 ——
        /// 即"当前没有合法攻击目标"。
        ///
        /// 玩家要求："放弃攻击按钮是在没有合法的攻击目标时弹出"。 
        /// 所以判据是 <see cref="HasAnyLegalTarget"/>，不是"对手是刺客"。
        /// </summary>
        public bool DefenderCannotBeAttacked
            => Phase == BoardPhase.ChoosingTargetPlayer && !HasAnyLegalTarget;

        /// <summary>玩家点击了一张手牌。返回 true 表示这次点击被接受。</summary>
        public bool ClickHand(Hand hand)
        {

            if (!CanClick(hand)) return false;

            switch (Phase)
            {
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

                case BoardPhase.ChoosingAttackerHand:
                    SelectedHand = hand;

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
                    return true;

                case BoardPhase.ChoosingTargetHand:
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
                    Hint = DefenderCannotBeAttacked
                        ? $"{TargetPlayer.Name} 当前无法被攻击（潜行中）。请点右侧「放弃攻击」。"
                        : $"已选择 {SideLabel(hand, Defender)}（{hand.Value}）参与运算，" +
                          $"结果是 {Hand.ComputeSum(SelectedHand.Value, hand.Value)}。" +
                          $"请再点一次 {TargetPlayer.Name} 的手牌，确认本次的目标是" +
                          $"{TargetPlayer.Name}。";
                    return true;

                case BoardPhase.ChoosingTargetPlayer:
                    // 第 ③ 步：点目标玩家的手牌确认"打他"。
                    // 挨打的是**玩家**；手牌只是指定的载体。
                    if (!TargetPlayer.Owns(hand)) return false;

                    ConfirmTargetPlayer();
                    return true;

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
        private PlayerState OwnerOf(Hand hand)
        {
            if (hand == null) return null;
            if (_game.PlayerA != null && _game.PlayerA.Owns(hand)) return _game.PlayerA;
            if (_game.PlayerB != null && _game.PlayerB.Owns(hand)) return _game.PlayerB;
            return null;
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

        /// <summary>本回合是否"放弃攻击"（运算照常、不造成伤害）。</summary>
        private bool _giveUpAttack;

        /// <summary>
        /// 本次运算要打**哪位玩家**（挨打的是玩家，不是手）。
        ///
        /// 多人模式会真正用到它；1v1 里等于 <see cref="Defender"/>，
        /// 但仍然要求玩家点一次确认 —— 免得多人上线后流程变形。
        /// </summary>
        public PlayerState TargetPlayer { get; private set; }

        /// <summary>把一只手翻译成"它的主人"的名字（提示文字用）。</summary>
        private string OwnerLabel(Hand hand)
        {
            if (_game.PlayerA != null && _game.PlayerA.Owns(hand)) return _game.PlayerA.Name;
            if (_game.PlayerB != null && _game.PlayerB.Owns(hand)) return _game.PlayerB.Name;
            return Defender.Name;
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

            // 顺序很重要：**先角色抉择（闪避等），再问选盾**。
            //
            // 因为闪避在结算里优先于护盾（`AttackContext.Resolve` 先判闪避再判盾），
            // 所以询问顺序也必须一致。曾经这里先问选盾、问完直接结算，
            // 结果"有盾的游侠"根本问不到闪避 —— 表现为"优先碎盾而不是询问是否闪避"。
            BeginCharacterChoices(BuildChoiceContext());
        }

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

            CollectFrom(context.Actor, context);
            CollectFrom(context.Defender, context);

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
            var provider = player?.Character as IPreTurnChoiceProvider;
            _lastCollectInfo += $" || CollectFrom({player?.Name}, " +
                                $"char={player?.Character?.GetType().Name}, " +
                                $"isProvider={provider != null})";

            if (provider != null)
            {
                int before = _pendingRequests.Count;
                provider.CollectPreTurnChoices(context, _pendingRequests);
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

            provider.CollectPreTurnChoices(context, _pendingRequests);

            // 过滤空候选
            for (int i = _pendingRequests.Count - 1; i >= 0; i--)
                if (_pendingRequests[i].Candidates == null || _pendingRequests[i].Candidates.Count == 0)
                    _pendingRequests.RemoveAt(i);

            if (_pendingRequests.Count == 0) return false;

            // ⚠️ 这里**不设** `_preTargetChoiceActive`。
            // 治疗目标发生在"运算源已定"之后，不属于"选目标手之前"的抉择 ——
            // 借用那个标志会让阶段被报成 ChoosingPreTargetChoice（真实 bug）。
            AdvanceCharacterChoice();
            return Phase == BoardPhase.ChoosingPreTurnChoice;
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
            if (_pendingRequestIndex >= _pendingRequests.Count)
            {
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
                    if (ChoseHealInAttackOrHeal() && TryEnterHealTargetChoice()) return;

                    // ①-b 攻击：仍要确认目标玩家（玩家确认过的交互不变）
                    if (NeedsTargetAfterOperation())
                    {
                        Phase = BoardPhase.ChoosingTargetPlayer;
                        Hint = $"运算出 {LastResultValueForHint()}。" +
                               $"请确认这次要指向 {TargetPlayer?.Name ?? Defender.Name}。";
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
        /// UI 显示的"当前是第几回合"（**从 1 开始**）。
        ///
        /// 玩家操作的第一个回合就是**第 1 回合**，而 `TurnCount` 记的是
        /// "已完成的回合数"（开局为 0），所以显示值要 +1。
        ///
        /// 为什么不在 `GameManager` 里把 `TurnCount` 改成 1 起：
        /// 它的语义（已完成数）被胜负判定与多份测试依赖，
        /// 改语义的收益很小、连带风险很大。**只在显示层换算。**
        ///
        /// 上限处收敛到 `TurnLimit`，避免平局瞬间显示 "51 / 50"。
        /// </summary>
        public int DisplayTurn => Math.Min(TurnCount + 1, TurnLimit);

        public int TurnLimit => GameManager.TurnLimit;

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

            // 统一定点：控制器自己保存一份本回合的答案。
            //
            // 这样后续阶段与**结算**一定能读到它，完全不依赖 sink 是否被清空。
            // （牧师的 4/6/8 两段抉择之间会走 SetupFirstStep 清 sink，
            //   只靠 sink 回读会导致"选了回复却按攻击结算"。）
            RecordAnswer(request.Effect, request.Variant, hand);

            // 让角色按"刚收到的答案"追加后续问题（例如牧师选了回复才问给谁）。
            // 必须**先记答案再追加** —— 后一步的 SkipWhen 要读得到前一步的答案。
            _pendingRequestIndex++;
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
            if (request.Candidates.Count != 1) return false;

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

            LegalMove move = ai.ChooseMove(_game, ActivePlayer);
            if (move.Actor == null) return false;

            if (!ClickHand(move.ActedHand)) return false;

            // 走到这里可能已经停在"选锁手"阶段（合盾且是野蛮人）
            ResolvePendingAiChoices();
            if (Phase != BoardPhase.ChoosingTargetHand) return true;

            if (!ClickHand(move.TargetHand)) return false;

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

            Hand pick = AiResolver?.ChooseShieldToBlock(Defender, _shieldChoices);

            ApplyDefenseChoice(ContainsHand(_shieldChoices, pick) ? pick : _shieldChoices[0]);
            ExecuteResolvedTurn();
        }

        /// <summary>
        /// 真正执行一次运算。要求 <see cref="SelectedHand"/> 与 <c>_selectedTarget</c> 已确定，
        /// 且所有需要玩家决定的抉择都已写入 <see cref="IPlayerChoiceSink"/>。
        /// </summary>
        private void ExecuteResolvedTurn()
        {
            Hand acted = SelectedHand;
            Hand target = _selectedTarget;

            // "放弃攻击"：运算照常发生（手值改变），但不造成伤害。
            // 用 GameManager 的开关在分派攻击时短路，而不是跳过整个回合 ——
            // 跳过整个回合会让手值回退（玩家实测报的 bug）。
            _game.SuppressAttackDamage = _giveUpAttack;

            TurnResult result;
            try
            {
                result = _game.ExecuteTurn(ActivePlayer, acted, target);
            }
            catch (InvalidOperationException ex)
            {
                // 防御性：正常流程走不到这里，但绝不能让界面卡死。
                // 一定要把原因带出来 —— 静默吞掉会让"点击没反应"变得极难排查。
                _log.Add($"非法运算被拒绝：{ex.Message}（行动方=" +
                         $"{ActivePlayer?.Name}，运算源={acted?.Value}，目标={target?.Value}）");
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
                _giveUpAttack = false;
            }

            LastTurn = result;
            foreach (string line in DescribeTurnLines(result)) _log.Add(line);

            SelectedHand = null;
            _selectedTarget = null;
            _chosenDefenseShield = null;
            _shieldChoices.Clear();

            if (_game.IsGameOver)
            {
                Phase = BoardPhase.GameOver;
                Hint = WinMessage();
                _log.Add(Hint);
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
            string head = $"{t.Actor.Name} {side}：{t.ActedHandValueBefore} + {t.TargetHandValue} = {t.ResultValue}";

            switch (t.Effect)
            {
                case HandEffectType.Attack4:
                case HandEffectType.Attack6:
                case HandEffectType.Attack8:
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
                    return head + "（无效果）";
            }
        }
    }
}
