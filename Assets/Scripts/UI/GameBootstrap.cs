using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters;   // 图鉴资料（表现层入口是唯一允许看到它的地方）
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 游戏入口。负责：
    ///   1. 用代码搭出"角色选择"和"棋盘"两套界面（不依赖预制体）；
    ///   2. 组装 <see cref="GameManager"/> + 角色 + 抉择器 + <see cref="BoardController"/>；
    ///   3. 在人类玩家与 AI 之间转发操作。
    ///
    /// 规则与交互逻辑都不在这里：规则在 Core，交互状态机在 Assistant.BoardController
    /// （那一层可以在无头测试里完整验证）。本类只是"胶水 + 表现"。
    ///
    /// 启动方式：<see cref="AutoStart"/> 用 <c>[RuntimeInitializeOnLoadMethod]</c> 在进入
    /// 运行时自动创建本组件，因此**不需要任何场景配置**（打开模板场景直接 Play 即可）。
    /// 如果你想手动控制，把本组件挂到场景物体上并关掉 AutoStart 行为即可
    /// （重复挂载有 <c>_autoInstance</c> 保护，不会创建两份界面）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        [Header("对局设置")]
        [Tooltip("玩家 A 使用的角色（默认白板）")]
        public SpecialLogicType PlayerACharacter = SpecialLogicType.Breaker;

        [Tooltip("玩家 B 使用的角色（默认白板）")]
        public SpecialLogicType PlayerBCharacter = SpecialLogicType.SchoolOfConjuration;

        [Tooltip("玩家 B 是否由 AI 控制")]
        public bool PlayerBIsAI = true;

        [Tooltip("AI 难度：贪心 AI 会打得比较合理，随机 AI 只用于测试")]
        public bool UseGreedyAI = true;

        [Tooltip("AI 出手前的等待秒数（让玩家看清上一回合的结果）")]
        public float AiThinkSeconds = 2f;

        /// <summary>
        /// 玩法模式。
        ///
        /// **入门**：没有掷骰先攻 —— 双方用默认初始手值（1/1）开局，直接对局。
        /// **经典**：开局掷 1d20 决定初始手值与先手。
        /// </summary>
        public enum PlayMode
        {
            /// <summary>入门：不掷骰先攻。</summary>
            Intro = 0,

            /// <summary>经典：掷 1d20 决定初始手值与先手。</summary>
            Classic = 1,
        }

        /// <summary>当前选择的玩法（进入 1v1 后先选）。</summary>
        public PlayMode Mode = PlayMode.Classic;

        /// <summary>
        /// AI 难度（**只在入门玩法的人机对战里可选**）。
        ///
        /// 经典玩法保持不变：AI 始终按当前最优评分走。
        /// </summary>
        public AiDifficulty Difficulty = AiDifficulty.Normal;

        [Header("调试")]
        [Tooltip("勾选后打印每回合的结算日志")]
        /// <summary>
        /// **详细日志**（默认开启）。
        ///
        /// 玩家要求"把 console 的日志强开，日后出现问题可以直接给到" ——
        /// 这类偶发问题（空运算源、结算被拦截、技能不发动）
        /// 人工很难复现，所以让日志**一直开着**，出问题时直接复制 Console 即可。
        ///
        /// 内容：每回合的结算描述、抉择现场（队列 / 收集 / AI 作答）、
        /// 以及"结算被拦截"这类异常情况的完整现场。
        ///
        /// 想安静时把它关掉即可（Inspector 里取消勾选）。
        /// </summary>
        public bool VerboseLogging = true;

        /// <summary>
        /// 详细诊断：把每次刷新时"待答抉择 + 按钮显隐判定"打到 Console。
        ///
        /// 用途：实机排查"为什么没有弹出强化/闪避按钮"。
        /// 这类问题的判定全在 UI 层，而无头测试跑不到 UI，
        /// 所以必须让玩家能一键拿到现场数据（待答队列、候选数、按钮判定结果）。
        ///
        /// 默认关闭。排查时改成 true，或勾上 Inspector 的 <c>Verbose Logging</c>。
        /// （曾靠它定位到"接受过一次抉择后 idx 不复位，导致后续提示被静默跳过"。）
        /// </summary>
        private static readonly bool DiagnoseChoices = false;

        // ---- 运行时对象 ----
        private Font _font;
        private GameBoardUI _board;
        private BoardController _controller;

        /// <summary>已经播过表现的那次结算（避免 RefreshView 被多次调用时重复播放）。</summary>
        private TurnResult _animatedTurn;

        /// <summary>回合间停顿：在这个时间点之前不接受点击、也不推进 AI。</summary>
        private float _pauseUntil;
        private GameManager _game;
        private SimpleChoiceProvider _choiceA;
        private SimpleChoiceProvider _choiceB;

        private RectTransform _selectRoot;
        private CharacterRosterView _rosterViewA;
        private CharacterRosterView _rosterViewB;
        private Text _detailA;
        private Text _detailB;
        private Text _selectTitle;

        // ---- 界面流程 ----
        // ---- 玩家要求的三大模式层级 ----
        /// <summary>冒险模式（章节教学与预留挑战入口）。</summary>
        private ScreenGroup _screenAdventure;
        private ScreenGroup _screenStoryChapter;
        private ScreenGroup _screenStoryBattles;
        private Text _storyFirstStatus;
        private Text _storySecondStatus;
        private readonly Text[] _storyStatuses = new Text[AdventureChapter.BattleCount];
        /// <summary>竞技模式（原 1v1 / 2v2 / 8 人）。</summary>
        private ScreenGroup _screenCompetition;
        /// <summary>娱乐模式（现只有双将）。</summary>
        private ScreenGroup _screenCasual;
        /// <summary>双将模式子菜单。</summary>
        private ScreenGroup _screenDualGeneral;
        /// <summary>双将对决（1v1）。</summary>
        private ScreenGroup _screenDualGeneralMenu;
        /// <summary>阵营混战（8 人）：竞技入口。</summary>
        private ScreenGroup _screenFactionBattleMenu;

        /// <summary>娱乐 → 双将 → 双将阵营混战菜单。</summary>
        private ScreenGroup _screenDualFactionMenu;

        /// <summary>8 人身份局：人机 / 真人菜单。</summary>
        private ScreenGroup _screenIdentityBattleMenu;

        /// <summary>8 人身份局选将。</summary>
        private ScreenGroup _screenIdentitySelect;

        // ---- 8 人身份局状态 ----
        private bool _isIdentity;
        private PlayerState _eightHumanPlayer;
        private bool _humanSacrificeArmed;
        private CharacterRosterView _idRoster;
        private Text _idStatus;
        private Text _idSeats;
        private Text _idPicked;
        private Button _idStart;
        private int _idIndex;
        private IdentityRole[] _idRoles;
        private string[] _idNames;
        private int _idHumanSeat;
        private CharacterDefinition _idLordPick;
        private CharacterDefinition _idHumanPick;
        private System.Random _idRng;

        /// <summary>双将选将界面。</summary>
        private ScreenGroup _screenDualSelect;

        /// <summary>8 人模式子菜单。</summary>
        private ScreenGroup _screenEightPlayerMenu;

        /// <summary>8 人阵营混战的单将选角界面。</summary>
        private ScreenGroup _screenFactionSelect;

        // ---- 8 人阵营混战的状态 ----
        private CharacterRosterView _fbRoster;
        private Text _fbDetail;
        private Text _fbPicked;
        private Button _fbStart;
        private int _fbIndex;
        private bool _isEightPlayer;
        private List<PlayerState> _eightSeated;

        // ---- 双将选将的状态 ----
        /// <summary>已选的**主将**（null = 还没选）。</summary>
        private CharacterDefinition _dgPrimary;
        /// <summary>已选的**副将**（null = 还没选）。</summary>
        private CharacterDefinition _dgSecondary;

        /// <summary>双将的角色卡列表。</summary>
        private CharacterRosterView _dgRoster;
        /// <summary>双将的详情面板。</summary>
        private Text _dgDetail;
        /// <summary>双将的状态提示（"请选择主将 / 请选择副将" 之类）。</summary>
        private Text _dgStatus;
        /// <summary>双将的开始按钮（两名都选好才可点）。</summary>
        private Button _dgStart;

        /// <summary>选将界面标题（1v1 / 8 人双将共用）。</summary>
        private Text _dgTitle;

        /// <summary>本次选将是否用于 8 人双将阵营混战。</summary>
        private bool _dualFactionMode;

        /// <summary>8 人双将选将的返回目标。</summary>
        private GameScreen _dualFactionReturnScreen = GameScreen.DualGeneral;

        /// <summary>本局是不是**双将对局**。</summary>
        private bool _isDualGeneral;

        /// <summary>双将：**人机对手**的主将 / 副将（随机搭配）。</summary>
        private CharacterDefinition _dgFoePrimary;
        private CharacterDefinition _dgFoeSecondary;

        /// <summary>
        /// 取**同阵营的副将候选**。
        ///
        /// ⚠️ 用委托注入而不直接调：
        ///    "同阵营"的判定在 <c>FingerDiceDuel.Characters</c> 层，
        ///    而 UI 层**不能引用 Characters**（分层铁律）。
        ///    所以由入口（本类，唯一允许同时看到两层的地方）注入。
        /// </summary>
        private System.Func<CharacterDefinition, IReadOnlyList<CharacterDefinition>> _dgPartnersOf;

        private ScreenGroup _screenMainMenu;
        private ScreenGroup _screenOneOnOne;

        /// <summary>2v2 子菜单（人机练习 / 真人对决）。</summary>
        private ScreenGroup _screenTwoOnTwo;

        /// <summary>本次对局是不是 2v2（决定棋盘与角色选择走哪条路）。</summary>
        private bool _isTwoOnTwo;

        /// <summary>2v2 角色选择界面。</summary>
        private ScreenGroup _screenTwoOnTwoCharacter;

        /// <summary>2v2 经典玩法的掷骰先攻界面。</summary>
        private TwoOnTwoInitiativeScreen _screenTwoOnTwoInitiative;

        /// <summary>先攻阶段用的四人名单（按"掷骰前"的顺序），用于反查"我在哪"。</summary>
        private List<PlayerState> _twoOnTwoRoster;

        /// <summary>2v2 的建局参数（我 / 队友 / 两名随机对手 + 我的座位）。</summary>
        private class TwoOnTwoSetup
        {
            public int Seat;
            public CharacterDefinition DefMe;
            public CharacterDefinition DefMate;
            public CharacterDefinition DefFoe1;
            public CharacterDefinition DefFoe2;
        }

        private TwoOnTwoSetup _pendingTwoOnTwo;

        /// <summary>2v2 经典玩法的先攻执行层（掷骰 -> 定座位 -> 依次决定）。</summary>
        private TwoOnTwoStartController _twoOnTwoStart;

        /// <summary>我在 <see cref="_twoOnTwoSeated"/> 里的下标（0 = 1 号位）。</summary>
        private int _twoOnTwoMeIndex;

        /// <summary>
        /// 2v2 落座后的四人，**按座位 1..4 排列**（下标 0 = 1 号位）。
        ///
        /// 环形界面直接用它，不必再反查座位。
        /// </summary>
        private List<PlayerState> _twoOnTwoSeated;

        /// <summary>
        /// 2v2 里"我"坐在几号位（1..4）。
        ///
        /// ⚠️ 这个值**只作展示兜底**。真正的座位由 <see cref="RingLayout"/> 给出 ——
        ///    我一度用它去推算每格的座位文案，结果与环形布局的推导不一致
        ///    （实机截图暴露：二三号位标反）。
        ///    现在标签直接用 `RingSlot.Seat`，不再二次推算。
        /// </summary>
        private int _twoOnTwoMySeat = 1;

        /// <summary>2v2 的回合上限（玩家要求 100；1v1 仍是 50）。</summary>
        private const int TwoOnTwoTurnLimit = 100;

        /// <summary>2v2 的第三 / 第四位抉择器（C / D 槽位）。</summary>
        private IChoiceProvider _choiceC;
        private IChoiceProvider _choiceD;

        /// <summary>2v2：我选的座位（1..4）。</summary>
        private int _seat = 1;

        /// <summary>
        /// 2v2 **经典**玩法：座位由掷骰先攻决定，所以界面上**不提供座位选择**。
        /// </summary>
        private bool _classicSeatByInitiative;

        /// <summary>2v2：队友角色索引（对手随机，不由玩家选）。</summary>
        private int _indexMate = 2;

        /// <summary>2v2：两名对手的随机角色索引（进入界面时抽一次）。</summary>
        private int _indexFoe1;
        private int _indexFoe2;

        private CharacterRosterView _rosterView2Me;
        private CharacterRosterView _rosterView2Mate;
        private Text _detail2Me;
        private Text _detail2Mate;
        private Text _seatSummary;
        private Text _foeSummary;
        private Button[] _seatButtons;

        /// <summary>座位选择整块（经典玩法下隐藏）。</summary>
        private GameObject _seatPickerRoot;
        private ScreenGroup _screenMode;
        private ScreenGroup _screenDifficulty;
        private ScreenGroup _screenCharacter;
        private DiceRollScreen _diceRoll;
        private ModalPanel _modal;

        /// <summary>掷骰完成后要用的角色（等掷骰结束才真正开始对局）。</summary>
        private CharacterDefinition _pendingDefA;
        private CharacterDefinition _pendingDefB;

        /// <summary>当前所在界面。</summary>
        private GameScreen _screen = GameScreen.MainMenu;

        /// <summary>两位玩家当前选中的角色索引（对应 <see cref="_roster"/>）。</summary>
        private int _indexA;
        private int _indexB = 1;

        private readonly List<CharacterDefinition> _roster = new List<CharacterDefinition>();

        /// <summary>自动创建出来的那个实例（避免重复创建界面）。</summary>
        private static GameBootstrap _autoInstance;

        /// <summary>AI 出手前的倒计时。</summary>
        private float _aiDelayTimer;

        /// <summary>冒险剧情教学的关卡导演；非冒险对局为 null。</summary>
        private AdventureLesson _adventure;

        /// <summary>冒险关卡胜利要点卡是否已弹过（一关只弹一次）。</summary>
        private bool _adventureVictoryShown;

        // ============================================================
        // 自动启动
        // ============================================================

        /// <summary>
        /// 进入运行时（Play）后自动创建入口对象。
        ///
        /// 这样做是为了**不依赖场景文件**：手工编辑场景 YAML 很容易出错
        /// （脚本 GUID 在团结引擎里是加密格式，手写几乎不可能正确），
        /// 而用代码自动启动则不需要在场景里引用任何脚本。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
            if (_autoInstance != null) return;

            EnsureEventSystem();

            var go = new GameObject("~FingerDiceDuel");
            _autoInstance = go.AddComponent<GameBootstrap>();
            DontDestroyOnLoad(go);
        }

        /// <summary>
        /// 确保场景里有 uGUI 的事件系统。
        ///
        /// **没有 EventSystem，界面画得出来但完全不响应点击**（而且不报错，很难查）。
        /// 本项目入口靠自动启动，不依赖任何场景，所以必须在这里自己把它建出来。
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                Debug.Log($"[FingerDiceDuel] 使用场景已有的 EventSystem（{EventSystem.current.GetType().Name}）。");
                return;
            }
            if (FindObjectOfType<EventSystem>() != null) return;

            // 本项目 activeInputHandler = 0（旧 Input Manager），且未安装 com.unity.inputsystem，
            // 因此 StandaloneInputModule 是正确的输入模块。
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);

            Debug.Log("[FingerDiceDuel] 场景缺少 EventSystem（没有它界面不响应点击），已自动创建。");
        }

        /// <summary>
        /// 替 AI 做"回合内抉择"（选盾 / 锁手）。
        ///
        /// 之所以要单独一个解析器：这些抉择的归属者**可能不是当前行动方**
        /// （典型：AI 攻击、人类防守时，选盾的是人类；反过来人类攻击、AI 防守时，
        /// 选盾的是 AI 防守方，而行动方是人类）。Assistant 层不认识 Unity，
        /// 所以由 UI 层注入这个适配器，把 AI 玩家的抉择转发给它的抉择器。
        /// </summary>
        private sealed class AiChoiceResolver : Assistant.IAiChoiceResolver
        {
            private readonly Func<PlayerState, SimpleChoiceProvider> _lookup;

            public AiChoiceResolver(Func<PlayerState, SimpleChoiceProvider> lookup)
            {
                _lookup = lookup;
            }

            public Hand ChooseShieldToBlock(PlayerState defender, IReadOnlyList<Hand> candidates)
            {
                if (candidates == null || candidates.Count == 0) return null;

                SimpleChoiceProvider provider = _lookup(defender);
                if (provider == null) return candidates[0];

                Hand pick = provider.AskWhichShieldToBlock(defender, candidates[0], candidates[1], null);
                return pick;
            }
        }

        /// <summary>
        /// 取某位玩家的**人类抉择器**（AI 代做抉择时写回答案用）。
        ///
        /// ⚠️ 2v2 有 4 个人，不能再用"A 就是 _choiceA、其余都是 _choiceB" ——
        /// 那会让 C/D 的答案写到 B 的 sink 里。
        /// </summary>
        /// <summary>这个玩家是不是本地人类主视角（8 人 / 2v2 / 1v1）。</summary>
        private bool IsHumanPlayer(PlayerState p)
        {
            if (p == null) return false;

            if (_isEightPlayer && _eightHumanPlayer != null)
                return ReferenceEquals(p, _eightHumanPlayer);
            if (_isEightPlayer && _eightSeated != null && _eightSeated.Count > 0)
                return ReferenceEquals(p, _eightSeated[0]);

            if (_isTwoOnTwo && _twoOnTwoSeated != null && _twoOnTwoSeated.Count > 0)
                return ReferenceEquals(p,
                    _twoOnTwoSeated[Mathf.Clamp(_twoOnTwoMeIndex, 0, _twoOnTwoSeated.Count - 1)]);

            return ReferenceEquals(p, _game?.PlayerA);
        }

        private SimpleChoiceProvider ProviderFor(PlayerState p)
        {
            if (p != null && _sinkByPlayer.TryGetValue(p, out SimpleChoiceProvider found))
                return found;

            return ReferenceEquals(p, _game?.PlayerA) ? _choiceA : _choiceB;
        }

        /// <summary>按玩家登记的"人类抉择器"（2v2 用）。</summary>
        private readonly Dictionary<PlayerState, SimpleChoiceProvider> _sinkByPlayer =
            new Dictionary<PlayerState, SimpleChoiceProvider>();

        // ============================================================
        // 生命周期
        // ============================================================

        private void Awake()
        {
            if (_autoInstance == null) _autoInstance = this;

            _font = LoadFont();

            BuildRoster();
            BuildSelectCanvas();
            BuildMainMenuScreen();
            BuildOneOnOneScreen();
            BuildTwoOnTwoScreen();

            // 玩家要求的三大模式层级
            BuildAdventureScreen();
            BuildStoryChapterScreen();
            BuildAdventureFinaleScreen();
            BuildCompetitionScreen();
            BuildCasualScreen();
            BuildDualGeneralScreen();
            BuildDualGeneralMenuScreen();
            BuildDualFactionMenuScreen();
            BuildEightPlayerMenuScreen();
            BuildFactionBattleMenuScreen();
            BuildIdentityBattleMenuScreen();
            BuildIdentitySelectScreen();
            BuildDualSelectScreen();
            BuildFactionSelectScreen();
            BuildModeScreen();
            BuildDifficultyScreen();
            // 双将的"同阵营副将" 过滤：由本类注入（UI 层不能引用 Characters）。
            _dgPartnersOf = DualGeneralFactory.PartnersFor;

            BuildCharacterScreen();
            BuildTwoOnTwoCharacterScreen();
            BuildInitiativeScreen();
            ConfigureFrontEndSelections();

            _board = gameObject.AddComponent<GameBoardUI>();
            _board.Build();
            _board.HandClicked += OnHandClicked;
            _board.RestartClicked += OnRestartClicked;
            _board.SkipTurnClicked += OnSkipTurnClicked;
            _board.SeatSwapClicked += OnSeatSwapClicked;
            _board.GambleClicked += OnGambleClicked;
            _board.StealClicked += OnStealClicked;
            _board.DeclineClicked += OnDeclineClicked;
            _board.AcceptClicked += OnAcceptClicked;
            _board.SacrificeClicked += OnSacrificeClicked;

            // 法师·塑能学派：伤害类型面板上选了某一种
            _board.DamageTypePicked += OnDamageTypePicked;
            _board.ExitClicked += OnExitClicked;
            _board.SetVisible(false);

            // 初始界面：主菜单。
            // 必须在所有分组建好之后调用 —— 否则 1v1 子菜单与角色选择会一起显示出来。
            ShowScreen(GameScreen.MainMenu);

            Debug.Log($"[FingerDiceDuel] 界面已就绪。角色：{_roster.Count} 个。");
        }

        private void BuildRoster()
        {
            _roster.Clear();

            // 只暴露"有实际技能实现"的角色，避免玩家选到一个没有 AI 逻辑的枚举值。
            foreach (CharacterDefinition def in CharacterDefinition.BuiltIn)
            {
                if (CharacterFactory.IsRegistered(def.Logic)) _roster.Add(def);
            }

            // 兜底角色：白板已被战士·勇士取代（玩家要求），所以这里用战士·勇士。
            if (_roster.Count == 0) _roster.Add(CharacterDefinition.Champion);
        }

        // ============================================================
        // 界面流程：画布 / 主菜单 / 1v1 子菜单 / 角色选择
        // ============================================================

        /// <summary>
        /// 建立一个供所有界面共用的画布，并创建弹窗。
        ///
        /// 共用一个画布而不是每屏一个 Canvas：切屏就是显隐分组，
        /// 不必反复创建销毁 Canvas（那样很容易漏删、或让 EventSystem 失灵）。
        /// </summary>
        private void BuildSelectCanvas()
        {
            var canvasGo = new GameObject("UiCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            _selectRoot = canvasGo.GetComponent<RectTransform>();
            StretchFull(_selectRoot);

            // 背景（所有界面共用，不属于任何分组，因此始终显示）
            var bg = new GameObject("Bg", typeof(Image));
            bg.transform.SetParent(_selectRoot, false);
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.09f, 0.10f, 0.13f);
            bgImg.raycastTarget = false;
            BuildFrontEndDecoration(bgImg);

            _modal = new ModalPanel();
            _modal.Build(_selectRoot, UiFont.Regular, UiFont.Bold,
                         size: new Vector2(1080f, 720f));

            // 开局掷骰界面（自带协程动画，所以是独立组件）
            _diceRoll = gameObject.AddComponent<DiceRollScreen>();
            _diceRoll.Build(_selectRoot, UiFont.Regular, UiFont.Bold);
            _diceRoll.Finished += OnDiceRollFinished;
        }

        /// <summary>右上角的问号按钮：打开完整规则说明。</summary>
        private Button MakeHelpButton(RectTransform parent)
        {
            Button help = MakeButton(parent, "Help", new Vector2(880f, 470f),
                                     new Vector2(64f, 64f), "?");
            help.onClick.AddListener(OnHelpClicked);
            return help;
        }

        private void OnHelpClicked()
        {
            ShowModalWithScrollReset("游戏规则", RuleBook.Full(_roster));
        }

        /// <summary>
        /// 显示弹窗，并在**下一帧**把正文滚回顶部。
        ///
        /// 为什么要等一帧：<see cref="ContentSizeFitter"/> 的高度要等布局算完才知道，
        /// 那一刻设 <c>verticalNormalizedPosition</c> 会被随后的布局刷新覆盖掉。
        /// </summary>
        private void ShowModalWithScrollReset(string title, string body)
        {
            if (_modal == null) return;

            _modal.Show(title, body);
            StartCoroutine(ResetModalScrollNextFrame());
        }

        private System.Collections.IEnumerator ResetModalScrollNextFrame()
        {
            yield return null;              // 等布局刷新
            Canvas.ForceUpdateCanvases();
            _modal?.ScrollToTop();
        }

        private void ShowComingSoon(string feature)
        {
            ShowModalWithScrollReset("敬请期待", RuleBook.ComingSoon(feature));
        }

        // ---- 主菜单 ----

        private void BuildMainMenuScreen() => BuildSkinnedMainMenu();

        private Button MakeSubModeButton(RectTransform parent, string name,
                                         Vector2 position, string title,
                                         string subtitle, System.Action onClick)
            => MakeModeButton(parent, name, position, title, subtitle, onClick);

        /// <summary>
        /// 子菜单共用的“返回”按钮。
        ///
        /// <paramref name="y"/> 必须按“上面有几个按钮”给：
        /// 按钮是 116 高、间距 156，所以**三个按钮时返回会压在第三个上**。
        /// （玩家实测：竞技模式里返回与 8 人身份局重叠。）
        /// </summary>
        private Button MakeBackButton(RectTransform parent, GameScreen target,
                                      float y = -300f)
        {
            Button back = MakeButton(parent, "Back", new Vector2(0f, y),
                                     new Vector2(240f, 64f), "返回");
            back.onClick.AddListener(() => ShowScreen(target));
            return back;
        }

        /// <summary>子菜单共用的大标题。</summary>
        private Text MakeSubTitle(RectTransform parent, string name, string text)
        {
            Text title = MakeText(parent, name, new Vector2(0f, 240f),
                                  new Vector2(1000f, 70f), 52,
                                  TextAnchor.MiddleCenter, new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = text;
            return title;
        }

        /// <summary>冒险入口：剧情十场训练已接入，挑战关卡预留。</summary>
        private void BuildAdventureScreen()
        {
            _screenAdventure = new ScreenGroup();
            _screenAdventure.Add(MakeSubTitle(_selectRoot, "AdvTitle", "冒险模式").gameObject);

            Button pve = MakeSubModeButton(_selectRoot, "AdvPve", new Vector2(0f, 60f),
                                           "剧情冒险", "第一章：边境哨站 · 十场训练",
                                           () => ShowScreen(GameScreen.StoryChapterSelect));
            Button chal = MakeSubModeButton(_selectRoot, "AdvChallenge", new Vector2(0f, -96f),
                                            "挑战关卡", "即将开放",
                                            () => ShowComingSoon("挑战关卡"));

            _screenAdventure.Add(pve.gameObject);
            _screenAdventure.Add(chal.gameObject);
            _screenAdventure.Add(MakeBackButton(_selectRoot, GameScreen.MainMenu).gameObject);
        }

        private void BuildStoryChapterScreen()
        {
            _screenStoryChapter = new ScreenGroup();
            _screenStoryChapter.Add(MakeSubTitle(_selectRoot, "StoryChaptersTitle", "剧情冒险").gameObject);
            _screenStoryChapter.Add(MakeSubModeButton(_selectRoot, "StoryChapter01", new Vector2(0f, 60f),
                "第一章：边境哨站", "初战至终战 · 老兵的指尖骰术指导",
                () => ShowScreen(GameScreen.StoryBattleSelect)).gameObject);
            _screenStoryChapter.Add(MakeBackButton(_selectRoot, GameScreen.Adventure).gameObject);

            _screenStoryBattles = new ScreenGroup();
            var heading = MakeSubTitle(_selectRoot, "StoryChapterTitle", "第一章：边境哨站");
            heading.rectTransform.anchoredPosition = new Vector2(0, 400);
            _screenStoryBattles.Add(heading.gameObject);
            var scroll = FrontEndArt.Scroll(_selectRoot, "StoryBattleScroll", new Vector2(0, -10),
                new Vector2(880, 680), out RectTransform content);
            content.sizeDelta = new Vector2(880, AdventureChapter.BattleCount * 148 + 20);
            string[] names = { "StoryFirst", "StorySecond", "StoryThird", "StoryFourth", "StoryFifth", "StorySixth", "StorySeventh", "StoryEighth", "StoryNinth", "StoryFinale" };
            for (int i = 0; i < AdventureChapter.BattleCount; i++)
            {
                int number = i + 1;
                Button battle = MakeSubModeButton(content, names[i], new Vector2(0, -76 - i * 148),
                    "边境哨站·" + AdventureBattleName(number), AdventureChapter.Hint(number), () => StartAdventureLevel(number));
                battle.GetComponent<RectTransform>().anchorMin = battle.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 1);
                _storyStatuses[i] = battle.transform.Find("Sub").GetComponent<Text>();
            }
            _storyFirstStatus = _storyStatuses[0]; _storySecondStatus = _storyStatuses[1];
            _screenStoryBattles.Add(scroll.gameObject);
            _screenStoryBattles.Add(MakeBackButton(_selectRoot, GameScreen.StoryChapterSelect, -425f).gameObject);
        }

        private static string AdventureBattleName(int number)
            => AdventureChapter.BattleName(number);

        private void RefreshStoryProgress()
        {
            for (int i = 0; i < _storyStatuses.Length; i++)
                if (_storyStatuses[i] != null)
                    _storyStatuses[i].text = AdventureChapter.Hint(i + 1) + "  ·  " +
                        (PlayerPrefs.GetInt("FingerDiceDuel.Adventure.Level" + (i + 1) + ".Completed", 0) != 0 ? "已通关" : i == 0 ? "推荐先学" : "待练习");
        }

        // ============================================================
        // 冒险模式 · 剧情模式 · 第一章：边境哨站
        // ============================================================

        /// <summary>
        /// 教学关入口：勇士 vs 关卡脚本NPC（初战生命12，次战生命18）。
        /// 士兵不走 MakeAI，而是关卡剧本（ScriptedMoveSelector + 收官保护）。
        /// </summary>
        private void StartAdventureLevel1() => StartAdventureLevel(1);
        private void StartAdventureLevel2() => StartAdventureLevel(2);

        private void StartAdventureLevel(int levelNumber, bool launchFinale = false)
        {
            if (levelNumber < 1 || levelNumber > AdventureChapter.BattleCount) throw new ArgumentOutOfRangeException(nameof(levelNumber));
            CleanupMatch();
            if (levelNumber == 10 && !launchFinale) { ShowScreen(GameScreen.AdventureFinaleSelect); return; }
            _pendingDefA = levelNumber == 10 ? AdventureChapter.FinaleRoster[Mathf.Clamp(_finaleIndex, 0, AdventureChapter.FinaleRoster.Count - 1)] : AdventureChapter.PlayerDefinition(levelNumber);
            _pendingDefB = levelNumber == 10 ? AdventureChapter.Knight : CharacterDefinition.Blank;

            _choiceA = new SimpleChoiceProvider();
            _choiceB = new SimpleChoiceProvider();
            _game = CharacterFactory.CreateMatch(_pendingDefA, _pendingDefB, _choiceA, _choiceB);

            _controller = new BoardController(_game, _choiceA, _choiceB);
            _controller.AiResolver = new AiChoiceResolver(ProviderFor);

            switch (levelNumber)
            {
                case 1: AdventureLevel1.PrepareSoldier(_game.PlayerB); _adventure = AdventureLevel1.Attach(_game, _controller); break;
                case 2: AdventureLevel2.Prepare(_game); _adventure = AdventureLevel2.Attach(_game, _controller); break;
                case 3: AdventureLevel3.Prepare(_game); _adventure = AdventureLevel3.Attach(_game, _controller); break;
                case 4: AdventureLevel4.Prepare(_game); _adventure = AdventureLevel4.Attach(_game, _controller); break;
                case 5: _game.PlayerB.Rename(AdventureChapter.InstructorName); _adventure = AdventureLevel5.Attach(_game, _controller); break;
                case 6: AdventureLevel6.Prepare(_game); _adventure = AdventureLevel6.Attach(_game, _controller); break;
                case 7: AdventureLevel7.Prepare(_game); _adventure = AdventureLevel7.Attach(_game, _controller); break;
                case 8: AdventureLevel8.Prepare(_game); _adventure = AdventureLevel8.Attach(_game, _controller); break;
                case 9: AdventureLevel9.Prepare(_game); _adventure = AdventureLevel9.Attach(_game, _controller); break;
                case 10: _adventure = AdventureLevel10.Attach(_game, _controller); break;
                default: throw new ArgumentOutOfRangeException(nameof(levelNumber));
            }
            _adventure.Bind(_controller);
            _adventureVictoryShown = false;

            // 与 PrepareMatch 相同的残留清理：本关固定 1v1，其余模式标志全部复位。
            _isTwoOnTwo = false;
            _twoOnTwoSeated = null;
            _isEightPlayer = false;
            _eightSeated = null;
            _isDualGeneral = false;

            StartPreparedMatch(beginTurn: !_adventure.UsesClassicSetup);
            _board.SetTitle(_adventure.DisplayTitle);
            _board.SetBattleBackground(_adventure.BattleBackgroundKey);
            _board.ShowAdventureObstacles(_adventure.LevelNumber == 4 || _adventure.LevelNumber == 7, _adventure.LevelNumber == 7 ? 2 : 1);

            // 开场剧情（模态）——确认后才进入教学拍；「稍后再来」返回章内战斗列表。
            _board.HideAdventureNarration();
            _board.ShowAdventureIntro(_adventure.DisplayTitle,
                _adventure.OpeningText, AdventureLevel1.GuideAvatarKey,
                AdventureLevel1.GuideName, OnAdventureIntroConfirmed, ReturnToAdventure,
                _adventure.AllowsTutorialSkip && PlayerPrefs.GetInt(_adventure.CompletionKey, 0) != 0 ? (System.Action)OnAdventureSkipTutorial : null,
                _adventure.UsesClassicSetup ? "学习先攻" : "跟随指引");
        }

        /// <summary>开场剧情确认：进入第一教学拍。</summary>
        private void OnAdventureIntroConfirmed()
        {
            if (_adventure == null) return;
            if (_adventure.UsesClassicSetup)
            {
                _board.SetVisible(false);
                _diceRoll.Begin(_game);
                _diceRoll.SetLesson("边境哨站·" + AdventureBattleName(_adventure.LevelNumber) + " · 经典先攻", ReturnToAdventure);
                ShowScreen(GameScreen.DiceRoll);
                return;
            }
            _adventure.BeginTutorial();
            RefreshView();
        }

        private void OnAdventureSkipTutorial()
        {
            if (_adventure == null) return;
            _adventure.SkipTutorial();
            RefreshView();
        }

        /// <summary>胜利要点卡上的「重打本关」。</summary>
        private void OnAdventureReplay()
        {
            StartAdventureLevel(_adventure?.LevelNumber ?? 1, true);
        }

        /// <summary>从冒险关卡返回章节页。</summary>
        private void ReturnToAdventure()
        {
            CleanupMatch();
            ShowScreen(GameScreen.StoryBattleSelect);
        }

        /// <summary>
        /// 竞技模式：**原本主菜单上的 1v1 / 2v2 / 8 人全部收在这里**。
        /// </summary>
        private void BuildCompetitionScreen()
        {
            _screenCompetition = new ScreenGroup();
            _screenCompetition.Add(MakeSubTitle(_selectRoot, "CompTitle", "竞技模式").gameObject);

            Button one = MakeSubModeButton(_selectRoot, "Comp1v1", new Vector2(0f, 90f),
                                           "1 v 1", "单挑对决", OnOneOnOneClicked);
            Button two = MakeSubModeButton(_selectRoot, "Comp2v2", new Vector2(0f, -66f),
                                           "2 v 2", "双人组队", OnTwoOnTwoClicked);
            Button eight = MakeSubModeButton(_selectRoot, "Comp8p", new Vector2(0f, -222f),
                                             "8 人模式", "阵营混战 / 身份对局",
                                             () => ShowScreen(GameScreen.EightPlayerMenu));

            _screenCompetition.Add(one.gameObject);
            _screenCompetition.Add(two.gameObject);
            _screenCompetition.Add(eight.gameObject);

            // 三个按钮（-222 为最下）—— 返回必须放到它下面。
            _screenCompetition.Add(
                MakeBackButton(_selectRoot, GameScreen.MainMenu, y: -370f).gameObject);
        }

        /// <summary>娱乐模式：目前只有双将。</summary>
        private void BuildCasualScreen()
        {
            _screenCasual = new ScreenGroup();
            _screenCasual.Add(MakeSubTitle(_selectRoot, "CasualTitle", "娱乐模式").gameObject);

            Button dual = MakeSubModeButton(_selectRoot, "CasualDual", new Vector2(0f, 60f),
                                            "双将模式", "同阵营两名角色同时上阵",
                                            () => ShowScreen(GameScreen.DualGeneral));

            _screenCasual.Add(dual.gameObject);
            _screenCasual.Add(MakeBackButton(_selectRoot, GameScreen.MainMenu).gameObject);
        }

        /// <summary>双将模式子菜单：双将对决（1v1）与阵营混战（8 人）。</summary>
        private void BuildDualGeneralScreen()
        {
            _screenDualGeneral = new ScreenGroup();
            _screenDualGeneral.Add(MakeSubTitle(_selectRoot, "DualTitle", "双将模式").gameObject);

            Button duel = MakeSubModeButton(_selectRoot, "DualDuel", new Vector2(0f, 60f),
                                            "双将对决", "1 v 1：各自两名同阵营角色",
                                            () => ShowScreen(GameScreen.DualGeneralMenu));
            Button faction = MakeSubModeButton(_selectRoot, "DualFaction", new Vector2(0f, -96f),
                                               "双将阵营混战", "8 人 · 双将 · 三阵营",
                                               () => ShowScreen(GameScreen.DualFactionMenu));

            _screenDualGeneral.Add(duel.gameObject);
            _screenDualGeneral.Add(faction.gameObject);
            _screenDualGeneral.Add(MakeBackButton(_selectRoot, GameScreen.Casual).gameObject);
        }

        /// <summary>双将阵营混战：AI 练习 / 真人对战（真人占位）。</summary>
        private void BuildDualFactionMenuScreen()
        {
            _screenDualFactionMenu = new ScreenGroup();
            _screenDualFactionMenu.Add(MakeSubTitle(_selectRoot, "DualFactionTitle",
                                                     "双将阵营混战").gameObject);

            Button ai = MakeSubModeButton(_selectRoot, "DualFactionAi", new Vector2(0f, 60f),
                                          "AI 练习", "1 名人类 + 7 名 AI，8 人双将",
                                          () =>
                                          {
                                              _dualFactionMode = true;
                                              _dualFactionReturnScreen = GameScreen.DualFactionMenu;
                                              ResetDualSelection();
                                              ShowScreen(GameScreen.DualGeneralSelect);
                                          });
            Button pvp = MakeSubModeButton(_selectRoot, "DualFactionPvp", new Vector2(0f, -96f),
                                           "真人对战", "联网对战玩家（待开放）",
                                           () => ShowComingSoon("双将阵营混战（真人）"));

            _screenDualFactionMenu.Add(ai.gameObject);
            _screenDualFactionMenu.Add(pvp.gameObject);
            _screenDualFactionMenu.Add(MakeBackButton(_selectRoot, GameScreen.DualGeneral).gameObject);
        }

        /// <summary>双将对决（1v1）：人机 / 真人。</summary>
        private void BuildDualGeneralMenuScreen()
        {
            _screenDualGeneralMenu = new ScreenGroup();
            _screenDualGeneralMenu.Add(MakeSubTitle(_selectRoot, "DualDuelTitle", "双将对决").gameObject);

            Button ai = MakeSubModeButton(_selectRoot, "DualAi", new Vector2(0f, 60f),
                                          "人机对战", "与 AI 对战",
                                          () =>
                                          {
                                              _dualFactionMode = false;
                                              ShowScreen(GameScreen.DualGeneralSelect);
                                          });
            Button pvp = MakeSubModeButton(_selectRoot, "DualPvp", new Vector2(0f, -96f),
                                           "真人对战", "联网对战玩家",
                                           () => ShowComingSoon("双将对决（真人）"));

            _screenDualGeneralMenu.Add(ai.gameObject);
            _screenDualGeneralMenu.Add(pvp.gameObject);
            _screenDualGeneralMenu.Add(MakeBackButton(_selectRoot, GameScreen.DualGeneral).gameObject);
        }

        // ============================================================
        // 8 人阵营混战（单将）
        // ============================================================

        private void OnFactionBattleClicked() => ShowScreen(GameScreen.FactionSelect);

        private void BuildFactionSelectScreen()
        {
            _screenFactionSelect = new ScreenGroup();

            Text title = MakeText(_selectRoot, "FbTitle", new Vector2(0f, 272f),
                                  new Vector2(1200f, 64f), 44, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "8 人阵营混战 · 选将";
            _screenFactionSelect.Add(title.gameObject);

            Text status = MakeText(_selectRoot, "FbStatus", new Vector2(0f, 208f),
                                   new Vector2(1120f, 36f), 24, TextAnchor.MiddleCenter,
                                   new Color(0.86f, 0.90f, 0.96f));
            status.text = "选一名角色；AI 补齐 8 个不重复角色，三个阵营按 3 / 3 / 2 分队";
            _screenFactionSelect.Add(status.gameObject);

            _fbRoster = new CharacterRosterView();
            _fbRoster.Build(_selectRoot, "FbRoster", new Vector2(0f, 132f),
                            _roster, UiFont.Regular, 0, SkillHintText);
            _fbRoster.SelectionChanged += OnFactionSelectionChanged;
            _screenFactionSelect.Add(_fbRoster.Root.gameObject);

            _fbDetail = MakeDetailPanel("FbDetail", new Vector2(0f, 62f));
            _screenFactionSelect.Add(_fbDetail.transform.parent.gameObject);

            _fbPicked = MakeText(_selectRoot, "FbPicked", new Vector2(0f, 8f),
                                 new Vector2(1120f, 32f), 21, TextAnchor.MiddleCenter,
                                 new Color(0.72f, 0.80f, 0.90f));
            _screenFactionSelect.Add(_fbPicked.gameObject);

            _fbStart = MakeButton(_selectRoot, "FbStart", new Vector2(150f, -52f),
                                  new Vector2(300f, 68f), "开始对局");
            _fbStart.onClick.AddListener(OnFactionStartClicked);
            _screenFactionSelect.Add(_fbStart.gameObject);

            Button back = MakeButton(_selectRoot, "FbBack", new Vector2(-460f, -52f),
                                     new Vector2(220f, 68f), "返回");
            back.onClick.AddListener(() => ShowScreen(GameScreen.Competition));
            _screenFactionSelect.Add(back.gameObject);

            _screenFactionSelect.Add(MakeHelpButton(_selectRoot).gameObject);

            RefreshFactionSelect();
        }

        private void OnFactionSelectionChanged(int index)
        {
            _fbIndex = index;
            RefreshFactionSelect();
        }

        private void RefreshFactionSelect()
        {
            if (_roster == null || _roster.Count == 0) return;
            _fbIndex = Mathf.Clamp(_fbIndex, 0, _roster.Count - 1);
            CharacterDefinition def = _roster[_fbIndex];

            if (_fbDetail != null) RefreshDetail(_fbDetail, def);
            if (_fbPicked != null)
                _fbPicked.text = $"已选：{def.DisplayName}（{FactionNameOf(def)}）";
            if (_fbStart != null) _fbStart.interactable = true;
        }

        private static int FactionOf(CharacterDefinition def)
        {
            CodexEntry entry = CharacterCodex.For(def);
            string axis = entry == null ? "中立" : Alignments.LawAxisOf(entry.Alignment);
            if (axis == "守序") return 0;
            if (axis == "混乱") return 2;
            return 1;
        }

        private static string FactionNameOf(CharacterDefinition def)
        {
            int f = FactionOf(def);
            return f == 0 ? "守序" : f == 2 ? "混乱" : "中立";
        }

        private static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        private static void ApplyEightOpeningHands(PlayerState player, System.Random rng)
        {
            int roll = DiceRollSetup.Roll(rng);
            IReadOnlyList<HandSplit> splits = DiceRollSetup.DistinctSplits(roll);
            if (splits.Count == 0) return;

            HandSplit split = new DiceSplitAdvisor().Choose(player.Definition, null, splits);
            player.LeftHand.SetValue(split.Left);
            player.RightHand.SetValue(split.Right);
            WayOfTheTaiChiLogic.ApplyTaichiToFives(player);
        }

        private void OnFactionStartClicked()
        {
            if (_roster == null || _roster.Count == 0) return;
            CharacterDefinition human = _roster[Mathf.Clamp(_fbIndex, 0, _roster.Count - 1)];
            var rng = new System.Random();

            int humanFaction = FactionOf(human);
            int weak = rng.Next(3);                 // 2 人的阵营
            int[] counts = { 3, 3, 3 };
            counts[weak] = 2;

            var pools = new List<CharacterDefinition>[3];
            for (int f = 0; f < 3; f++) pools[f] = new List<CharacterDefinition>();
            foreach (CharacterDefinition d in _roster)
            {
                int f = FactionOf(d);
                if (ReferenceEquals(d, human)) continue;
                pools[f].Add(d);
            }

            var aiDefs = new List<CharacterDefinition>(7);
            for (int f = 0; f < 3; f++)
            {
                Shuffle(pools[f], rng);
                int need = counts[f] - (f == humanFaction ? 1 : 0);
                for (int k = 0; k < need && k < pools[f].Count; k++)
                    aiDefs.Add(pools[f][k]);
            }
            Shuffle(aiDefs, rng);

            var seated = new List<PlayerState>(8);
            var providers = new List<IChoiceProvider>(8);
            var teams = new List<int>(8);

            seated.Add(CharacterFactory.CreatePlayer("我", human));
            providers.Add(new SimpleChoiceProvider());
            teams.Add(humanFaction);
            _sinkByPlayer[seated[0]] = (SimpleChoiceProvider)providers[0];

            for (int i = 0; i < aiDefs.Count; i++)
            {
                PlayerState p = CharacterFactory.CreatePlayer("AI" + (i + 1), aiDefs[i]);
                var sink = new SimpleChoiceProvider();
                seated.Add(p);
                providers.Add(sink);
                teams.Add(FactionOf(aiDefs[i]));
                _sinkByPlayer[p] = sink;
            }

            for (int i = 0; i < 8; i++) ApplyEightOpeningHands(seated[i], rng);

            _game = GameManager.CreateEight(seated, providers, teams);
            _game.RoundLimit = GameManager.DefaultRoundLimit;

            _choiceA = (SimpleChoiceProvider)providers[0];
            _choiceB = (SimpleChoiceProvider)providers[1];
            _controller = new BoardController(_game, _choiceA, _choiceB,
                aiForA: null, aiForB: MakeAI());

            for (int i = 2; i < 8; i++)
                _controller.BindPlayer(seated[i], MakeAI(), (SimpleChoiceProvider)providers[i]);

            _controller.AiResolver = new AiChoiceResolver(ProviderFor);
            _controller.SetFirstPlayer(seated[0]);

            _isEightPlayer = true;
            _isTwoOnTwo = false;
            _isDualGeneral = false;
            _isIdentity = false;
            _eightSeated = seated;
            _eightHumanPlayer = seated[0];
            _pendingDefA = human;
            _pendingDefB = seated[1].Definition;

            StartPreparedMatch();
        }

        /// <summary>阵营混战（8 人）：人机 / 真人。⚠️ 等 8 人模式完成后再套用。</summary>
        /// <summary>8 人模式子菜单：阵营混战 / 身份对局。</summary>
        private void BuildEightPlayerMenuScreen()
        {
            _screenEightPlayerMenu = new ScreenGroup();
            _screenEightPlayerMenu.Add(MakeSubTitle(_selectRoot, "EightTitle", "8 人模式").gameObject);

            Button faction = MakeSubModeButton(_selectRoot, "EightFaction", new Vector2(0f, 60f),
                                               "阵营混战", "单将 · 三阵营 · 8 人",
                                               () => ShowScreen(GameScreen.FactionBattleMenu));
            Button identity = MakeSubModeButton(_selectRoot, "EightIdentity", new Vector2(0f, -96f),
                                                "身份对局", "主公 / 忠臣 / 反贼 / 内奸",
                                                () => ShowScreen(GameScreen.IdentityBattleMenu));

            _screenEightPlayerMenu.Add(faction.gameObject);
            _screenEightPlayerMenu.Add(identity.gameObject);
            _screenEightPlayerMenu.Add(MakeBackButton(_selectRoot, GameScreen.Competition).gameObject);
        }

        /// <summary>阵营混战：AI 练习 / 真人对战。</summary>
        private void BuildFactionBattleMenuScreen()
        {
            _screenFactionBattleMenu = new ScreenGroup();
            _screenFactionBattleMenu.Add(MakeSubTitle(_selectRoot, "FactionTitle",
                                                       "阵营混战").gameObject);

            Button ai = MakeSubModeButton(_selectRoot, "FactionAi", new Vector2(0f, 60f),
                                          "AI 练习", "1 名人类 + 7 名 AI，8 人单将",
                                          () =>
                                          {
                                              _dualFactionMode = false;
                                              ShowScreen(GameScreen.FactionSelect);
                                          });
            Button pvp = MakeSubModeButton(_selectRoot, "FactionPvp", new Vector2(0f, -96f),
                                           "真人对战", "联网对战玩家（待开放）",
                                           () => ShowComingSoon("阵营混战（真人）"));

            _screenFactionBattleMenu.Add(ai.gameObject);
            _screenFactionBattleMenu.Add(pvp.gameObject);
            _screenFactionBattleMenu.Add(MakeBackButton(_selectRoot, GameScreen.EightPlayerMenu).gameObject);
        }

        // ============================================================
        // 8 人身份局
        // ============================================================

        private void BuildIdentityBattleMenuScreen()
        {
            _screenIdentityBattleMenu = new ScreenGroup();
            _screenIdentityBattleMenu.Add(MakeSubTitle(_selectRoot, "IdentityMenuTitle",
                                                        "8 人身份对局").gameObject);

            Button ai = MakeSubModeButton(_selectRoot, "IdentityAi", new Vector2(0f, 60f),
                                          "AI 练习", "随机身份 · 1 名人类 + 7 名 AI",
                                          () =>
                                          {
                                              StartIdentityLobby();
                                              ShowScreen(GameScreen.IdentitySelect);
                                          });
            Button pvp = MakeSubModeButton(_selectRoot, "IdentityPvp", new Vector2(0f, -96f),
                                           "真人对战", "联网对战玩家（待开放）",
                                           () => ShowComingSoon("8 人身份对局（真人）"));

            _screenIdentityBattleMenu.Add(ai.gameObject);
            _screenIdentityBattleMenu.Add(pvp.gameObject);
            _screenIdentityBattleMenu.Add(MakeBackButton(_selectRoot, GameScreen.EightPlayerMenu).gameObject);
        }

        private void BuildIdentitySelectScreen()
        {
            _screenIdentitySelect = new ScreenGroup();

            Text title = MakeText(_selectRoot, "IdTitle", new Vector2(0f, 272f),
                                  new Vector2(1200f, 64f), 42, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "8 人身份局 · 选将";
            _screenIdentitySelect.Add(title.gameObject);

            _idStatus = MakeText(_selectRoot, "IdStatus", new Vector2(0f, 214f),
                                 new Vector2(1500f, 34f), 22, TextAnchor.MiddleCenter,
                                 new Color(0.86f, 0.90f, 0.96f));
            _screenIdentitySelect.Add(_idStatus.gameObject);

            _idSeats = MakeText(_selectRoot, "IdSeats", new Vector2(0f, 166f),
                                new Vector2(1650f, 34f), 18, TextAnchor.MiddleCenter,
                                new Color(0.78f, 0.84f, 0.92f));
            _screenIdentitySelect.Add(_idSeats.gameObject);

            _idRoster = new CharacterRosterView();
            _idRoster.Build(_selectRoot, "IdRoster", new Vector2(0f, 52f),
                            _roster, UiFont.Regular, 0, SkillHintText);
            _idRoster.SelectionChanged += index =>
            {
                IReadOnlyList<CharacterDefinition> list = CurrentIdentityList();
                if (index < 0 || index >= list.Count) return;
                _idIndex = index;
                _idHumanPick = list[index];
                RefreshIdentitySelect();
            };
            _screenIdentitySelect.Add(_idRoster.Root.gameObject);

            _idPicked = MakeText(_selectRoot, "IdPicked", new Vector2(0f, -118f),
                                 new Vector2(1200f, 32f), 21, TextAnchor.MiddleCenter,
                                 new Color(0.72f, 0.80f, 0.90f));
            _screenIdentitySelect.Add(_idPicked.gameObject);

            _idStart = MakeButton(_selectRoot, "IdStart", new Vector2(150f, -176f),
                                  new Vector2(300f, 64f), "开始对局");
            _idStart.onClick.AddListener(StartIdentityGame);
            _screenIdentitySelect.Add(_idStart.gameObject);

            Button back = MakeButton(_selectRoot, "IdBack", new Vector2(-460f, -176f),
                                     new Vector2(220f, 64f), "返回");
            back.onClick.AddListener(() => ShowScreen(GameScreen.IdentityBattleMenu));
            _screenIdentitySelect.Add(back.gameObject);
            _screenIdentitySelect.Add(MakeHelpButton(_selectRoot).gameObject);
        }

        private static void ShuffleRoles(List<IdentityRole> roles, System.Random rng)
        {
            for (int i = roles.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                IdentityRole t = roles[i]; roles[i] = roles[j]; roles[j] = t;
            }
        }

        private void StartIdentityLobby()
        {
            _idRng = new System.Random();
            _idRoles = new IdentityRole[8];
            _idRoles[0] = IdentityRole.Lord;
            var rest = new List<IdentityRole>(7)
            {
                IdentityRole.Loyalist, IdentityRole.Loyalist,
                IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Rebel, IdentityRole.Rebel,
                IdentityRole.Traitor,
            };
            ShuffleRoles(rest, _idRng);
            for (int i = 0; i < 7; i++) _idRoles[i + 1] = rest[i];

            _idHumanSeat = _idRng.Next(8);
            _idNames = new string[8];
            for (int i = 0; i < 8; i++)
                _idNames[i] = i == _idHumanSeat ? "我" : "AI" + (i + 1);

            _idLordPick = null;
            _idHumanPick = null;
            _idIndex = 0;

            if (_idHumanSeat != 0)
            {
                var pool = new List<CharacterDefinition>(_roster);
                _idLordPick = pool[_idRng.Next(pool.Count)];
            }

            RefreshIdentitySelect();
        }

        private IReadOnlyList<CharacterDefinition> CurrentIdentityList()
        {
            var list = new List<CharacterDefinition>(8);
            if (_idRng == null) return list;
            foreach (CharacterDefinition d in _roster)
            {
                if (_idHumanSeat != 0 && ReferenceEquals(d, _idLordPick)) continue;
                list.Add(d);
            }
            return list;
        }

        private void RefreshIdentitySelect()
        {
            if (_idRoster == null || _idRoles == null) return;
            IReadOnlyList<CharacterDefinition> list = CurrentIdentityList();
            _idRoster.SetRoster(list, list.Count > 0 ? Mathf.Clamp(_idIndex, 0, list.Count - 1) : -1);
            _idRoster.ScrollToSelected();

            bool humanLord = _idHumanSeat == 0;
            IdentityRole myRole = _idRoles[_idHumanSeat];
            _idStatus.text = humanLord
                ? "你是主公（1 号位），请先选将。基础体力上限 +5、攻防 +1/+1；守序/中立/混乱分别触发 明主/霸王/暴君。"
                : $"你是{IdentityRoleInfo.DisplayName(myRole)}（{_idHumanSeat + 1} 号位）；" +
                  $"主公 AI 已选 {_idLordPick.DisplayName}，请选择你的角色。";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if (i > 0) sb.Append("　");
                string role = i == 0
                    ? "主公"
                    : (i == _idHumanSeat ? IdentityRoleInfo.DisplayName(_idRoles[i]) : "？");
                sb.Append($"{i + 1}号 {_idNames[i]}（{role}）");
            }
            _idSeats.text = sb.ToString();

            _idPicked.text = _idHumanPick == null
                ? "尚未选择角色"
                : $"已选：{_idHumanPick.DisplayName}";
            _idStart.interactable = _idHumanPick != null;
        }

        private void StartIdentityGame()
        {
            if (_idHumanPick == null || _idRoles == null) return;

            var used = new HashSet<CharacterDefinition>();
            used.Add(_idHumanPick);
            if (_idHumanSeat != 0 && _idLordPick != null) used.Add(_idLordPick);

            var defs = new CharacterDefinition[8];
            defs[_idHumanSeat] = _idHumanPick;
            defs[0] = _idHumanSeat == 0 ? _idHumanPick : _idLordPick;

            var pool = new List<CharacterDefinition>(_roster.Count);
            foreach (CharacterDefinition d in _roster)
                if (!used.Contains(d)) pool.Add(d);
            Shuffle(pool, _idRng);
            int cursor = 0;
            for (int i = 1; i < 8; i++)
            {
                if (i == _idHumanSeat) continue;
                defs[i] = pool[cursor++];
            }

            var seated = new List<PlayerState>(8);
            var providers = new List<IChoiceProvider>(8);
            var selectors = new List<IMoveSelector>(8);
            var sinks = new List<SimpleChoiceProvider>(8);
            for (int i = 0; i < 8; i++)
            {
                PlayerState p = CharacterFactory.CreatePlayer(_idNames[i], defs[i]);
                var sink = new SimpleChoiceProvider();
                var ai = i == _idHumanSeat ? null : new GreedyChoiceProvider();
                seated.Add(p);
                providers.Add(ai != null ? (IChoiceProvider)ai : sink);
                selectors.Add(ai);
                sinks.Add(sink);
                _sinkByPlayer[p] = sink;
            }

            _game = GameManager.CreateIdentityEight(seated, providers, _idRoles);
            _game.RoundLimit = GameManager.IdentityRoundLimit;
            for (int i = 0; i < 8; i++) ApplyEightOpeningHands(seated[i], _idRng);

            var sinkA = sinks[0];
            var sinkB = sinks[1];
            _controller = new BoardController(_game, sinkA, sinkB);
            for (int i = 0; i < 8; i++)
                _controller.BindPlayer(seated[i], selectors[i], sinks[i]);

            _controller.AiResolver = new AiChoiceResolver(ProviderFor);
            _controller.SetFirstPlayer(seated[0]);

            _isIdentity = true;
            _humanSacrificeArmed = false;
            _isEightPlayer = true;
            _isTwoOnTwo = false;
            _isDualGeneral = false;
            _eightSeated = seated;
            _eightHumanPlayer = seated[_idHumanSeat];
            _pendingDefA = defs[0];
            _pendingDefB = defs[1];
            StartPreparedMatch();
        }

        // ============================================================
        // 双将的选将界面
        // ============================================================

        /// <summary>
        /// **双将选将**：先选**主将**，选完后副将候选**只剩同阵营**。
        ///
        /// 玩家确认的规则：
        ///   · 两名必须**不同角色**
        ///   · 必须**同一阵营序列**（守序 / 中立 / 混乱）
        ///
        /// 为什么用"一条列表 + 两步选" 而不是两条列表：
        ///   副将候选**取决于主将**，两条并列列表会让人以为它们互相独立；
        ///   而且屏幕高度只够两条，详情面板就没地方了。
        /// </summary>
        private void BuildDualSelectScreen()
        {
            _screenDualSelect = new ScreenGroup();

            Text title = MakeText(_selectRoot, "DgTitle", new Vector2(0f, 272f),
                                  new Vector2(1200f, 64f), 44, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "双将选将";
            _dgTitle = title;
            _screenDualSelect.Add(title.gameObject);

            // 状态提示：告诉玩家现在该选什么
            _dgStatus = MakeText(_selectRoot, "DgStatus", new Vector2(0f, 208f),
                                 new Vector2(1120f, 36f), 24, TextAnchor.MiddleCenter,
                                 new Color(0.86f, 0.90f, 0.96f));
            _screenDualSelect.Add(_dgStatus.gameObject);

            // 角色卡列表
            _dgRoster = new CharacterRosterView();
            _dgRoster.Build(_selectRoot, "DgRoster", new Vector2(0f, 132f),
                            _roster, UiFont.Regular, 0, SkillHintText);
            _dgRoster.SelectionChanged += OnDualSelectionChanged;
            _screenDualSelect.Add(_dgRoster.Root.gameObject);

            // 详情（显示当前选中的角色）
            _dgDetail = MakeDetailPanel("DgDetail", new Vector2(0f, 62f));
            _screenDualSelect.Add(_dgDetail.transform.parent.gameObject);

            // 已选的主/副将摘要
            Text picked = MakeText(_selectRoot, "DgPicked", new Vector2(0f, 8f),
                                   new Vector2(1120f, 32f), 21, TextAnchor.MiddleCenter,
                                   new Color(0.72f, 0.80f, 0.90f));
            _dgPicked = picked;
            _screenDualSelect.Add(picked.gameObject);

            _dgStart = MakeButton(_selectRoot, "DgStart", new Vector2(150f, -52f),
                                  new Vector2(300f, 68f), "开始对局");
            _dgStart.onClick.AddListener(OnDualStartClicked);
            _screenDualSelect.Add(_dgStart.gameObject);

            Button reset = MakeButton(_selectRoot, "DgReset", new Vector2(-180f, -52f),
                                      new Vector2(240f, 68f), "重选");
            reset.onClick.AddListener(ResetDualSelection);
            _screenDualSelect.Add(reset.gameObject);

            Button back = MakeButton(_selectRoot, "DgBack", new Vector2(-460f, -52f),
                                     new Vector2(220f, 68f), "返回");
            back.onClick.AddListener(() => ShowScreen(_dualFactionMode
                ? _dualFactionReturnScreen : GameScreen.DualGeneralMenu));
            _screenDualSelect.Add(back.gameObject);

            _screenDualSelect.Add(MakeHelpButton(_selectRoot).gameObject);

            ResetDualSelection();
        }

        /// <summary>"已选主/副将"的摘要文本。</summary>
        private Text _dgPicked;

        /// <summary>清空选择，回到"请选主将"。</summary>
        private void ResetDualSelection()
        {
            _dgPrimary = null;
            _dgSecondary = null;
            RefreshDualSelect();
        }

        /// <summary>
        /// 根据当前状态刷新整个选将界面。
        ///
        /// 三种状态：
        ///   1 还没选主将 -> 列表是**全部角色**
        ///   2 选了主将、没选副将 -> 列表只剩**同阵营**（且不含主将）
        ///   3 两名都选了 -> 列表回到全部（方便重选），开始按钮可点
        /// </summary>
        private void RefreshDualSelect()
        {
            if (_dgRoster == null) return;

            bool pickingSecondary = _dgPrimary != null && _dgSecondary == null;

            IReadOnlyList<CharacterDefinition> list = _roster;
            if (pickingSecondary)
            {
                list = _dgPartnersOf != null
                    ? _dgPartnersOf(_dgPrimary)
                    : (IReadOnlyList<CharacterDefinition>)new List<CharacterDefinition>();
            }

            _dgRoster.SetRoster(list, -1);
            _dgRoster.ScrollToSelected();

            if (list.Count > 0) RefreshDetail(_dgDetail, list[0]);
            else if (_dgDetail != null) _dgDetail.text = "没有可选的副将。";

            if (_dgTitle != null)
                _dgTitle.text = _dualFactionMode ? "双将阵营混战 · 选将" : "双将选将";
            if (_dgStart != null)
            {
                Text label = _dgStart.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = _dualFactionMode ? "开始 8 人对局" : "开始对局";
            }

            if (_dgStatus != null)
            {
                if (_dgPrimary == null)
                    _dgStatus.text = "第一步：选择主将";
                else if (_dgSecondary == null)
                    _dgStatus.text = "第二步：选择副将"
                                   + "（只能选与主将同阵营的角色）";
                else
                    _dgStatus.text = _dualFactionMode
                        ? "选好了：AI 会补齐 8 名双将，三阵营 3 / 3 / 2"
                        : "选好了，可以开始";
            }

            if (_dgPicked != null)
            {
                string a = _dgPrimary != null ? _dgPrimary.DisplayName : "—";
                string b = _dgSecondary != null ? _dgSecondary.DisplayName : "—";
                _dgPicked.text = $"主将：{a}    副将：{b}";
            }

            if (_dgStart != null) _dgStart.interactable = _dgPrimary != null && _dgSecondary != null;
        }

        /// <summary>
        /// 选中某张卡。
        ///
        /// 注意：**点卡片就是“选定”** ——
        /// 第一次点选主将，之后点选副将（此时列表已只剩同阵营）。
        /// </summary>
        private void OnDualSelectionChanged(int index)
        {
            IReadOnlyList<CharacterDefinition> list = CurrentDualList();
            if (index < 0 || index >= list.Count) return;

            CharacterDefinition def = list[index];

            // 玩家点了哪张卡，详情面板就要跟着变（RefreshDualSelect 只在重建列表时刷新详情）
            RefreshDetail(_dgDetail, def);

            if (_dgPrimary == null)
            {
                _dgPrimary = def;
                RefreshDualSelect();
                return;
            }

            if (_dgSecondary == null)
            {
                // 同一个人不能当自己的副将（列表已过滤，这里再保一层）
                if (ReferenceEquals(def, _dgPrimary)) return;

                _dgSecondary = def;
                RefreshDualSelect();
                return;
            }

            // 两名都选了：再点就重新开始（把这张当主将）
            _dgPrimary = def;
            _dgSecondary = null;
            RefreshDualSelect();
        }

        /// <summary>当前列表对应的角色集合（与界面上看到的一致）。</summary>
        private IReadOnlyList<CharacterDefinition> CurrentDualList()
        {
            if (_dgPrimary != null && _dgSecondary == null && _dgPartnersOf != null)
                return _dgPartnersOf(_dgPrimary);
            return _roster;
        }

        /// <summary>
        /// 点“开始对局”。
        ///
        /// ⚠️ 目前只做**校验与提示**：双将的**对局流程**（创建玩家、
        ///    掷骰先攻、牌桌绑定）还没接。下一步做那一块。
        /// </summary>
        /// <summary>
        /// **8 人双将阵营混战**：人类选一对同阵营双将，AI 补齐 7 对；
        /// 三阵营 3 / 3 / 2，尽量保证 16 名角色不重复。
        /// </summary>
        private void StartDualFactionBattle(CharacterDefinition primary,
                                            CharacterDefinition secondary)
        {
            if (primary == null || secondary == null) return;
            if (!DualGeneralFactory.SameAxis(primary, secondary)) return;

            var rng = new System.Random();
            int humanFaction = FactionOf(primary);

            int weak = rng.Next(3);
            int[] counts = { 3, 3, 3 };
            counts[weak] = 2;

            // 按阵营分池；先把人类选的两名从池里拿走，保证不重复。
            var pools = new List<CharacterDefinition>[3];
            for (int f = 0; f < 3; f++) pools[f] = new List<CharacterDefinition>();
            foreach (CharacterDefinition d in _roster)
            {
                if (ReferenceEquals(d, primary) || ReferenceEquals(d, secondary)) continue;
                pools[FactionOf(d)].Add(d);
            }

            var aiPairs = new List<CharacterDefinition[]>(7);
            for (int f = 0; f < 3; f++)
            {
                Shuffle(pools[f], rng);
                int need = counts[f] - (f == humanFaction ? 1 : 0);
                for (int i = 0; i < need; i++)
                {
                    if (pools[f].Count < 2)
                    {
                        ShowComingSoon("双将阵营混战：可用角色不足，无法补齐 8 人。");
                        return;
                    }

                    aiPairs.Add(new[] { pools[f][0], pools[f][1] });
                    pools[f].RemoveRange(0, 2);
                }
            }

            if (aiPairs.Count != 7)
            {
                ShowComingSoon("双将阵营混战：可用角色不足，无法补齐 8 人。");
                return;
            }

            Shuffle(aiPairs, rng);

            var seated = new List<PlayerState>(8);
            var providers = new List<IChoiceProvider>(8);
            var teams = new List<int>(8);

            PlayerState human = DualGeneralFactory.Create("我", primary, secondary);
            seated.Add(human);
            var humanSink = new SimpleChoiceProvider();
            providers.Add(humanSink);
            teams.Add(humanFaction);
            _sinkByPlayer[human] = humanSink;

            for (int i = 0; i < aiPairs.Count; i++)
            {
                CharacterDefinition[] pair = aiPairs[i];
                PlayerState p = DualGeneralFactory.Create("AI" + (i + 1), pair[0], pair[1]);
                var sink = new SimpleChoiceProvider();
                seated.Add(p);
                providers.Add(sink);
                teams.Add(FactionOf(pair[0]));
                _sinkByPlayer[p] = sink;
            }

            for (int i = 0; i < 8; i++) ApplyEightOpeningHands(seated[i], rng);

            _game = GameManager.CreateEight(seated, providers, teams);
            _game.RoundLimit = GameManager.DefaultRoundLimit;

            _choiceA = (SimpleChoiceProvider)providers[0];
            _choiceB = (SimpleChoiceProvider)providers[1];
            _controller = new BoardController(_game, _choiceA, _choiceB,
                aiForA: null, aiForB: MakeAI());

            for (int i = 2; i < 8; i++)
                _controller.BindPlayer(seated[i], MakeAI(), (SimpleChoiceProvider)providers[i]);

            _controller.AiResolver = new AiChoiceResolver(ProviderFor);
            _controller.SetFirstPlayer(seated[0]);

            _isEightPlayer = true;
            _isTwoOnTwo = false;
            _isDualGeneral = true;
            _isIdentity = false;
            _eightSeated = seated;
            _eightHumanPlayer = human;
            _pendingDefA = primary;
            _pendingDefB = aiPairs[0][0];

            StartPreparedMatch();
        }

        private void OnDualStartClicked()
        {
            if (_dgPrimary == null || _dgSecondary == null) return;

            if (_dualFactionMode)
            {
                StartDualFactionBattle(_dgPrimary, _dgSecondary);
                return;
            }

            // 玩家确认：**只有玩家自己选**，
            // 人机对手**随机搭配**，不给人机选角色。
            if (!DualGeneralFactory.TryRandomPair(out _dgFoePrimary, out _dgFoeSecondary))
            {
                ShowComingSoon("双将对局（无法为人机搭配角色）");
                return;
            }

            _pendingDefA = _dgPrimary;
            _pendingDefB = _dgFoePrimary;
            PrepareDualMatch(_dgPrimary, _dgSecondary, _dgFoePrimary, _dgFoeSecondary);

            // 玩家确认：**骰先攻等与经典模式相似**（不做入门模式）。
            _diceRoll.Begin(_game);
            ShowScreen(GameScreen.DiceRoll);
        }

        /// <summary>
        /// 建立**双将**对局（不调 BeginTurn）。
        ///
        /// 与 <see cref="PrepareMatch"/> 的区别：不能用
        /// <c>CharacterFactory.CreateMatch</c> —— 它只会用**单角色**建玩家。
        /// 双将要自己组 <see cref="PlayerState"/> 再交给 <see cref="GameManager"/> 。
        /// </summary>
        private void PrepareDualMatch(CharacterDefinition myPrimary, CharacterDefinition mySecondary,
                                      CharacterDefinition foePrimary, CharacterDefinition foeSecondary)
        {
            _choiceA = new SimpleChoiceProvider();
            _choiceB = new SimpleChoiceProvider();

            PlayerState a = DualGeneralFactory.Create("P1", myPrimary, mySecondary);
            PlayerState b = DualGeneralFactory.Create("P2", foePrimary, foeSecondary);

            _game = new GameManager(a, b, _choiceA, _choiceB);

            IMoveSelector aiA = null;
            IMoveSelector aiB = PlayerBIsAI ? (IMoveSelector)MakeAI() : null;

            _controller = new BoardController(_game, _choiceA, _choiceB,
                                              aiForA: aiA,
                                              aiForB: aiB);
            _controller.AiResolver = new AiChoiceResolver(ProviderFor);
            _isDualGeneral = true;

            // ⚠️ **必须清掉 2v2 的标志**。
            //
            //    `_isTwoOnTwo` 只在 BuildTwoOnTwoMatch 里被置 true，
            //    **从来没有人复位**。于是"先打过一局 2v2、再玩双将"时，
            //    双将（1v1 的对局）会一路走进 2v2 分支：
            //    界面用环形布局、标题印四人信息。
            //    玩家实测说的正是这个："娱乐模式的双将变成 2v2 了"。
            _isTwoOnTwo = false;
            _twoOnTwoSeated = null;
            _isEightPlayer = false;
            _isIdentity = false;
            _eightHumanPlayer = null;
            _eightSeated = null;
        }

        /// <summary>模式按钮：大号标题 + 副标题，两行文字。</summary>
        private Button MakeModeButton(RectTransform parent, string name, Vector2 position,
            string title, string subtitle, Action onClick)
            => CreateArtModeButton(parent, name, position, title, subtitle, onClick);
        private CodexScreen _codex;

        /// <summary>
        /// 打开图鉴。
        ///
        /// ⚠️ 这里是**唯一允许同时看到 Characters 与 UI 两层**的地方
        ///    （GameBootstrap 是表现层入口）——
        ///    所以由它把 CodexEntry 转成 UI 层自己的
        ///    CodexCardData，而不是让 UI 去引用 Characters。
        /// </summary>
        private void OnCodexClicked()
        {
            BuildCodexIfNeeded();

            // 图鉴入口 -> **只有角色卡**（可阵营筛选）
            _codex.SetCards(BuildCodexCards(), allowFilter: true);
            _codex.SetVisible(true);
            ShowScreen(GameScreen.Codex);
        }

        /// <summary>
        /// **魔法球入口**：直接跳到「伤害类型指引」那一页。
        ///
        /// 复用图鉴的翻页器（不另做一套界面），但**换一套卡片集**：
        /// 魔法球 -&gt; 伤害、状态、元素反应三张指南页（不显示阵营筛选）；
        /// 「图」-&gt; 角色图鉴（可筛选）。
        /// </summary>
        private void OnMagicOrbClicked()
        {
            BuildCodexIfNeeded();

            // 魔法球入口 -> **只有指南页**（伤害类型指引 / 状态一览）。
            //
            // 玩家要求："魔法球和图鉴入口进入是一个东西，把伤害类型指引和
            // 状态一览单独放进魔法球入口"。所以这里换成另一套卡片集，
            // 并把阵营筛选整块隐藏（对指南页做阵营筛选没有意义）。
            _codex.SetCards(BuildGuideCards(), allowFilter: false);
            _codex.SetVisible(true);
            _codex.GoTo(0);
            ShowScreen(GameScreen.Codex);
        }

        private void BuildCodexIfNeeded()
        {
            if (_codex != null) return;

            _codex = new CodexScreen();
            _codex.CloseRequested += () => ShowScreen(GameScreen.MainMenu);

            // ⚠️ 先给一套初始卡片（Build 需要它才能建出界面）。
            //    真正显示哪一套由两个入口各自调 SetCards 决定。
            _codex.Build(_selectRoot, BuildCodexCards());
        }

        /// <summary>
        /// 取一个角色的**一句技能描述**（图鉴里【技能】那条）。
        ///
        /// ⚠️ 这里是表现层入口允许同时看到 Characters 与 UI 两层的地方，
        ///    所以由它把图鉴的短描述取出来交给界面。
        /// </summary>
        private static string SkillHintText(CharacterDefinition def)
        {
            CodexEntry e = CharacterCodex.For(def);
            if (e != null && !string.IsNullOrEmpty(e.ShortSkill)) return e.ShortSkill;

            // 图鉴里没有时回退到长描述
            return def?.Description ?? "";
        }

        /// <summary>把 Characters 层的图鉴资料转成 UI 层的扁平结构。</summary>
        private static List<CodexCardData> BuildCodexCards()
        {
            var list = new List<CodexCardData>();
            CharacterDefinition[] all = CharacterDefinition.BuiltIn;

            for (int i = 0; i < all.Length; i++)
            {
                CharacterDefinition def = all[i];
                if (def == null) continue;

                CodexEntry e = CharacterCodex.For(def);
                if (e == null) continue;

                // 攻防与生命用**运行时实例**（圣武士·王冠之誓的攻防会随碎盾变）
                PlayerState probe = CharacterFactory.CreatePlayer(def.DisplayName, def);

                var card = new CodexCardData
                {
                    Id = def.Id == "blank" ? "fighter_champion" : def.Id,
                    NameZh = def.DisplayName,
                    NameEn = e.EnglishName,
                    ProfessionId = def.Profession?.Id,
                    ProfessionNameZh = def.Profession?.NameZh,
                    ProfessionNameEn = def.Profession?.NameEn,
                    SubclassNameZh = def.SubclassNameZh,
                    SubclassNameEn = def.SubclassNameEn,
                    Alignment = Alignments.NameOf(e.Alignment),
                    MaxHealth = CharacterFactory.MaxHealthOf(def),
                    Attack = probe?.Character?.AttackBonus ?? def.AttackBonus,
                    Defense = probe?.Character?.DefenseBonus ?? def.DefenseBonus,
                    Range = probe != null ? GameManager.BaseRangeOf(probe) : 1,
                    ShortSkill = e.ShortSkill,
                    DetailSkill = e.DetailSkill,
                    PlayGuide = e.PlayGuide,
                    Counter = e.Counter,
                    LawAxis = Alignments.LawAxisOf(e.Alignment),
                    GoodAxis = Alignments.GoodAxisOf(e.Alignment),
                };

                for (int k = 0; k < e.Interactions.Count; k++)
                    card.Interactions.Add(e.Interactions[k]);

                list.Add(card);
            }

            // ⚠️ 指南页**不在这里** —— 玩家要求把它们单独放进魔法球入口
            //    （原来两个入口进的是同一个东西，页码也连着数）。
            //    见 <see cref="BuildGuideCards"/>。
            return list;
        }

        /// <summary>
        /// 魔法球入口的卡片集：伤害类型、状态一览、元素反应。
        ///
        /// 玩家要求："把伤害类型指引和状态一览单独放进魔法球入口"。
        /// 顺序：① 伤害类型指引 ② 状态一览。
        /// </summary>
        private static List<CodexCardData> BuildGuideCards()
        {
            List<GuideSheet> sheets = GuideSheets.All();
            var list = new List<CodexCardData>(sheets.Count);

            for (int i = 0; i < sheets.Count; i++)
                list.Add(ToCard(sheets[i]));

            return list;
        }

        /// <summary>
        /// 把一份 **Core 的速查表**（<see cref="GuideSheet"/>）转成图鉴卡片。
        ///
        /// ⚠️ 内容全部来自 Core —— 界面只负责排版：
        ///    · 没有归属标签（"法师·塑能学派体系"）、没有属性行
        ///    · 没有【技能】/【技能详细】/【操作指导】/【如何针对】四节
        ///    （玩家要求：这两张表是**纯速查**，"不仅法师·塑能学派要用，日后做 pve 冒险也很重要"）
        ///
        /// ⚠️ 数据放在 Core 的另一个好处：**测试项目引用不到 UI**，
        ///    写在这里就没法测；放进 Core 才能被断言。
        /// </summary>
        private static CodexCardData ToCard(GuideSheet sheet)
        {
            var card = new CodexCardData
            {
                NameZh = sheet.TitleZh,
                NameEn = sheet.TitleEn,
                IsGuide = true,
                TwoColumnItems = sheet.TwoColumn,
                GuideNote = sheet.Note,

                // 显式全部留空 —— CodexScreen 靠"内容为空"跳过那些小节与标签
                Alignment = "",
                MaxHealth = 0, Attack = 0, Defense = 0, Range = 0,
                ShortSkill = "",
                DetailSkill = "",
                PlayGuide = "",
                Counter = "",
                LawAxis = null,
                GoodAxis = null,
            };

            for (int i = 0; i < sheet.Lines.Count; i++)
                card.Interactions.Add(sheet.Lines[i]);

            // ⚠️ 分组（"伤害类型"页：魔法伤害 / 物理伤害）——
            //    界面据此渲染"分类标题 + 每条一个代码绘制的图标"。
            if (sheet.HasGroups)
            {
                card.Groups = new List<CodexCardData.CodexGuideGroup>(sheet.Groups.Count);

                for (int g = 0; g < sheet.Groups.Count; g++)
                {
                    GuideGroup src = sheet.Groups[g];
                    var dst = new CodexCardData.CodexGuideGroup { Title = src.Title };

                    for (int i = 0; i < src.Entries.Count; i++)
                    {
                        GuideEntry e = src.Entries[i];
                        dst.Entries.Add(new CodexCardData.CodexGuideEntry
                        {
                            IconKey = e.IconKey,
                            Status = e.Status,
                            ResourceKey = e.ResourceKey,
                            Color = e.Color,
                            Text = e.Text,
                        });
                    }

                    card.Groups.Add(dst);
                }
            }

            return card;
        }
        private void OnOneOnOneClicked() => ShowScreen(GameScreen.OneOnOneMenu);

        private void OnTwoOnTwoClicked() => ShowScreen(GameScreen.TwoOnTwoMenu);

        // ---- 2v2 子菜单 ----
        //
        // 玩家要求：**不改动主菜单结构** —— 仍然从主菜单已有的「2 v 2」进入，
        // 进来之后再分「人机练习」与「真人对决」。
        // 真人对决目前是**占位**（联网对战尚未实现），点了给出明确提示。

        private void BuildTwoOnTwoScreen()
        {
            _screenTwoOnTwo = new ScreenGroup();

            Text title = MakeText(_selectRoot, "TwoOnTwoTitle", new Vector2(0f, 300f),
                                  new Vector2(1000f, 70f), 52, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "2 v 2";
            _screenTwoOnTwo.Add(title.gameObject);

            Button practice = MakeModeButton(_selectRoot, "TtPractice", new Vector2(0f, 80f),
                                             "人机练习", "与 AI 队友组队（单机四人同屏）",
                                             OnTwoOnTwoPracticeClicked);
            Button pvp = MakeModeButton(_selectRoot, "TtPvp", new Vector2(0f, -70f),
                                        "真人对决", "联网对战玩家",
                                        () => ShowComingSoon("真人对决（联网对战）"));

            _screenTwoOnTwo.Add(practice.gameObject);
            _screenTwoOnTwo.Add(pvp.gameObject);

            // 玩家实测：2v2 的返回按钮偏左（原来在 -420），
            // 要与 1v1 子菜单保持一致（居中）。
            Button back = MakeButton(_selectRoot, "TtBack", new Vector2(0f, -280f),
                                     new Vector2(240f, 64f), "返回");
            // 玩家要求：与 1v1 一致用**默认蓝色**。
            // 原来这里把它覆盖成了灰色（0.30, 0.33, 0.40）。
            back.onClick.AddListener(() => ShowScreen(GameScreen.MainMenu));
            _screenTwoOnTwo.Add(back.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenTwoOnTwo.Add(help.gameObject);
        }

        // ---- 1v1 子菜单 ----

        private void BuildOneOnOneScreen()
        {
            _screenOneOnOne = new ScreenGroup();

            Text title = MakeText(_selectRoot, "OneOnOneTitle", new Vector2(0f, 300f),
                                  new Vector2(1000f, 70f), 52, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "1 v 1";
            _screenOneOnOne.Add(title.gameObject);

            Button ai = MakeModeButton(_selectRoot, "ModeAi", new Vector2(0f, 80f),
                                       "人机对战", "与 AI 对战（先选玩法）", OnStartAiClicked);
            Button pvp = MakeModeButton(_selectRoot, "ModePvp", new Vector2(0f, -70f),
                                        "竞技", "联网对战玩家",
                                        () => ShowComingSoon("竞技（联网对战）"));

            _screenOneOnOne.Add(ai.gameObject);
            _screenOneOnOne.Add(pvp.gameObject);

            Button back = MakeButton(_selectRoot, "Back", new Vector2(0f, -280f),
                                     new Vector2(240f, 64f), "返回");
            back.onClick.AddListener(() => ShowScreen(GameScreen.MainMenu));
            _screenOneOnOne.Add(back.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenOneOnOne.Add(help.gameObject);
        }


        /// <summary>
        /// 玩法选择界面：进门后先选「入门 / 经典」，再进角色选择。
        ///
        /// 为什么不让它直接进角色选择：两种玩法差别很直观
        /// （有没有掷骰先攻），玩家应该在选角色**之前**就决定，
        /// 免得选完角色才发现玩法不对又退回来。
        /// </summary>
        private void BuildModeScreen()
        {
            _screenMode = new ScreenGroup();

            Text title = MakeText(_selectRoot, "ModeTitle", new Vector2(0f, 300f),
                                  new Vector2(1000f, 70f), 52, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "选择玩法";
            _screenMode.Add(title.gameObject);

            Button intro = MakeModeButton(_selectRoot, "ModeIntro", new Vector2(0f, 110f),
                "入门", "不掷骰先攻，双方从 1/1 开始",
                () =>
                {
                    Mode = PlayMode.Intro;

                    // 玩家要求：**2v2 的入门没有难度选择** ——
                    // 2v2 涉及队友与四人配合，AI 太"笨"会让整局没法验证，
                    // 所以一律按困难（不调难度 = 完全确定性），直接进角色选择。
                    if (_isTwoOnTwo)
                    {
                        Difficulty = AiDifficulty.Hard;
                        GoToCharacterSelect();
                        return;
                    }

                    ShowScreen(GameScreen.DifficultyMenu);
                });

            Button classic = MakeModeButton(_selectRoot, "ModeClassic", new Vector2(0f, -60f),
                "经典", "掷 1d20 决定初始手值与先手",
                () => { Mode = PlayMode.Classic; GoToCharacterSelect(); });

            _screenMode.Add(intro.gameObject);
            _screenMode.Add(classic.gameObject);

            Button back = MakeButton(_selectRoot, "ModeBack", new Vector2(0f, -270f),
                                     new Vector2(240f, 64f), "返回");
            // 玩法界面对 1v1 与 2v2 是共用的，所以"返回"要按来源回到对应子菜单
            // （否则 2v2 的玩家会被丢回 1v1 菜单）。
            back.onClick.AddListener(() => ShowScreen(_isTwoOnTwo
                ? GameScreen.TwoOnTwoMenu
                : GameScreen.OneOnOneMenu));
            _screenMode.Add(back.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenMode.Add(help.gameObject);
        }

        /// <summary>
        /// 难度选择界面：**只出现在人机对战的入门玩法里**。
        ///
        /// 经典玩法保持不变（没有难度选择）—— 按玩家要求。
        /// </summary>
        private void BuildDifficultyScreen()
        {
            _screenDifficulty = new ScreenGroup();

            Text title = MakeText(_selectRoot, "DiffTitle", new Vector2(0f, 300f),
                                  new Vector2(1000f, 70f), 52, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "选择难度";
            _screenDifficulty.Add(title.gameObject);

            Button easy = MakeModeButton(_selectRoot, "DiffEasy", new Vector2(0f, 140f),
                "简单", "AI 会偶尔失误",
                () => { Difficulty = AiDifficulty.Easy; GoToCharacterSelect(); });

            Button normal = MakeModeButton(_selectRoot, "DiffNormal", new Vector2(0f, -10f),
                "普通", "AI 大方向稳定，细节有变化",
                () => { Difficulty = AiDifficulty.Normal; GoToCharacterSelect(); });

            Button hard = MakeModeButton(_selectRoot, "DiffHard", new Vector2(0f, -160f),
                "困难", "AI 始终选择最优解",
                () => { Difficulty = AiDifficulty.Hard; GoToCharacterSelect(); });

            _screenDifficulty.Add(easy.gameObject);
            _screenDifficulty.Add(normal.gameObject);
            _screenDifficulty.Add(hard.gameObject);

            Button back = MakeButton(_selectRoot, "DiffBack", new Vector2(0f, -300f),
                                     new Vector2(240f, 64f), "返回");
            back.onClick.AddListener(() => ShowScreen(GameScreen.ModeMenu));
            _screenDifficulty.Add(back.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenDifficulty.Add(help.gameObject);
        }

        private void OnStartAiClicked() => ShowScreen(GameScreen.ModeMenu);

        /// <summary>
        /// 选完玩法与难度后进入角色选择。
        ///
        /// 1v1 与 2v2 **共用"玩法 / 难度"这两屏**，所以分流必须发生在这里 ——
        /// 否则 2v2 会走进 1v1 的角色选择（两个角色位、没有座位选择）。
        /// </summary>
        private void GoToCharacterSelect()
        {
            if (_isTwoOnTwo)
            {
                // 经典：座位由先攻决定 -> 不选座位
                if (Mode == PlayMode.Classic)
                {
                    ShowClassicTwoOnTwoCharacterScreen();
                    return;
                }

                // 入门：座位由玩家选 —— 必须**先复位标志**，
                // 否则从经典退回来再进角色选择时，座位选择仍然是隐藏的。
                _classicSeatByInitiative = false;
                RollOpponents();

                // ⚠️ 顺序很重要：**先 ShowScreen 再刷新摘要**。
                //    反过来的话，ShowScreen 里对分组调用 SetActive(true)
                //    会把刚被隐藏的"座位选择"整块重新激活
                //    （实机现象：经典玩法里仍然显示座位选择）。
                ShowScreen(GameScreen.TwoOnTwoCharacter);
                RefreshTwoOnTwoSummary();
                return;
            }

            ShowScreen(GameScreen.CharacterSelect);
        }

        /// <summary>
        /// 2v2 经典玩法：**没有座位选择**（玩家要求）。
        ///
        /// 座位完全由"四人各投 1d20"的先攻结果决定：
        ///   · 大数较大的那一队坐 1 号位与 4 号位（大数 = 1 号、小数 = 4 号）
        ///   · 另一队坐 2 号位（大数）与 3 号位（小数）
        ///   · 大数相同则比小数；都相同则随机选一队
        ///
        /// 玩家在这里只选**自己的角色**（+ 队友角色），座位稍后掷骰得出。
        /// </summary>
        private void ShowClassicTwoOnTwoCharacterScreen()
        {
            _classicSeatByInitiative = true;
            RollOpponents();

            // 同 GoToCharacterSelect：**先切屏再刷新摘要**，
            // 否则 ShowScreen 的 SetActive(true) 会重新显示座位选择。
            ShowScreen(GameScreen.TwoOnTwoCharacter);
            RefreshTwoOnTwoSummary();
        }

        /// <summary>
        /// 抽两名对手的随机角色。
        ///
        /// 玩家要求："在 2v2 人机中可以为自己和人机队友选择角色，
        /// **人机对手随机选择角色**"。
        ///
        /// 每次进入角色选择时抽一次（而不是每帧抽），
        /// 这样玩家看到的对手在点"开始对局"之前是稳定的。
        /// </summary>
        private void RollOpponents()
        {
            if (_roster.Count == 0) return;

            _indexFoe1 = UnityEngine.Random.Range(0, _roster.Count);
            _indexFoe2 = UnityEngine.Random.Range(0, _roster.Count);
        }

        /// <summary>
        /// 2v2 开局：按座位把我 / 队友 / 两名对手放进环里。
        ///
        /// 队伍规则（玩家确认）：**座位 1&4 一队、2&3 一队** ——
        /// 而 <see cref="SeatLayout"/> 保证"队友与我在环上相邻"，
        /// 所以队友一定是我的上家或下家。
        /// </summary>
        private void OnTwoOnTwoStartClicked()
        {
            CharacterDefinition defMe =
                _roster[Mathf.Clamp(_indexA, 0, _roster.Count - 1)];
            CharacterDefinition defMate =
                _roster[Mathf.Clamp(_indexMate, 0, _roster.Count - 1)];
            CharacterDefinition defFoe1 =
                _roster[Mathf.Clamp(_indexFoe1, 0, _roster.Count - 1)];
            CharacterDefinition defFoe2 =
                _roster[Mathf.Clamp(_indexFoe2, 0, _roster.Count - 1)];

            _pendingTwoOnTwo = new TwoOnTwoSetup
            {
                Seat = _seat,
                DefMe = defMe,
                DefMate = defMate,
                DefFoe1 = defFoe1,
                DefFoe2 = defFoe2,
            };

            // 入门：座位由玩家选，直接建局开局。
            //
            // 经典：座位由掷骰决定，所以**不能**先按玩家选的座位建局 ——
            // 那会建出一套用不上的玩家，而且座位与先攻结果不一致。
            // 两条路各自建局，但都由 ApplyToPlayers 写手值。
            if (Mode == PlayMode.Classic)
            {
                BeginTwoOnTwoInitiative();
                return;
            }

            PrepareTwoOnTwoMatch();
            StartPreparedMatch();
        }

        /// <summary>
        /// **2v2 经典**：四人各投 1d20 -> 定座位 -> 依次决定初始手值。
        ///
        /// 座位由掷骰决定，所以这里**先建人、再掷骰、最后按结果落座**，
        /// 而不是先按玩家选的座位建局（那会多建一套用不上的玩家）。
        ///
        /// 建好之后用 <see cref="TwoOnTwoStartController.ApplyToPlayers"/>
        /// 把分配好的初始手值写进四人的手牌，再正常开局。
        /// </summary>
        private void BeginTwoOnTwoInitiative()
        {
            if (_pendingTwoOnTwo == null) return;

            // 我的座位在选择界面里是未知的（经典不选座位），
            // 所以队伍按"建局顺序"给：前两位一队、后两位一队。
            var players = new List<PlayerState>(4);
            var teams = new List<int>(4);

            for (int i = 0; i < 4; i++) teams.Add(i < 2 ? 0 : 1);

            // 角色安排：我 + 队友（同队）+ 两名对手（同队）
            CharacterDefinition[] defs =
            {
                _pendingTwoOnTwo.DefMe, _pendingTwoOnTwo.DefMate,
                _pendingTwoOnTwo.DefFoe1, _pendingTwoOnTwo.DefFoe2,
            };

            for (int i = 0; i < 4; i++)
            {
                players.Add(CharacterFactory.CreatePlayer(defs[i].DisplayName, defs[i]));
                _sinkByPlayer[players[i]] = new SimpleChoiceProvider();
            }

            _twoOnTwoStart = TwoOnTwoStartController.RollAndBegin(
                players, teams, new System.Random());

            // 记下"我是哪一位"（不能按名字反查：对手可能随机到同名角色）
            _twoOnTwoMeIndex = 0;
            _twoOnTwoRoster = players;

            // 交给**先攻界面**逐步决定（AI 的步骤由界面自动代做，
            // 人类的那一步停下来等玩家点候选）。
            _screenTwoOnTwoInitiative.SetHuman(players[_twoOnTwoMeIndex]);
            ShowScreen(GameScreen.TwoOnTwoInitiative);
            _screenTwoOnTwoInitiative.Begin(_twoOnTwoStart);
        }

        /// <summary>
        /// 先攻走完后：按**先攻得出的座位**重排四个人并真正开局。
        ///
        /// 初始手值已由界面在 <c>Finish</c> 里通过
        /// <see cref="TwoOnTwoStartController.ApplyToPlayers"/> 写进手牌
        /// （只写数值、不触发效果）。
        /// </summary>
        private void OnTwoOnTwoInitiativeFinished()
        {
            if (_twoOnTwoStart == null || _twoOnTwoRoster == null) return;

            // 按座位重排：seated[0] = 1 号位
            var seated = new List<PlayerState>(4);
            var seatedProviders = new List<SimpleChoiceProvider>(4);
            for (int seat = 1; seat <= 4; seat++)
            {
                PlayerState p = _twoOnTwoStart.PlayerAtSeat(seat);
                seated.Add(p);

                seatedProviders.Add(_sinkByPlayer.TryGetValue(p, out SimpleChoiceProvider sp)
                    ? sp
                    : new SimpleChoiceProvider());
            }

            // 我在新的座位顺序里的下标
            int myIndex = 0;
            for (int i = 0; i < seated.Count; i++)
                if (ReferenceEquals(seated[i], _twoOnTwoRoster[_twoOnTwoMeIndex])) myIndex = i;

            _choiceA = seatedProviders[0];
            _choiceB = seatedProviders[1];
            _choiceC = seatedProviders[2];
            _choiceD = seatedProviders[3];

            BuildTwoOnTwoMatch(seated, seatedProviders, myIndex,
                               firstToAct: seated[0]);

            StartPreparedMatch();
        }

        /// <summary>
        /// 2v2 人机练习：与 1v1 共用"玩法（入门 / 经典）"流程。
        ///
        /// ⚠️ 但 **2v2 的入门没有难度选择**（玩家要求）——
        /// 进入门时直接按困难并跳到角色选择。
        /// </summary>
        private void OnTwoOnTwoPracticeClicked()
        {
            _isTwoOnTwo = true;
            _isEightPlayer = false;
            _isIdentity = false;
            _eightHumanPlayer = null;
            _eightSeated = null;
            ShowScreen(GameScreen.ModeMenu);
        }

        /// <summary>
        /// 2v2 的建局：按**座位顺序**把四个人放进环里。
        ///
        /// 座位规则（玩家确认）：座位 `1&4` 一队、`2&3` 一队。
        /// 所以先把"我 / 队友 / 对手1 / 对手2"按座位算好站位，再交给
        /// <see cref="GameManager.Create2v2"/>（它负责映射到底层 A/B/C/D 槽位）。
        ///
        /// 我永远是**主视角**（人类），其余三人是 AI —— 与 1v1 的
        /// "A 是人类、B 可选 AI"同一个约定。
        /// </summary>
        private void PrepareTwoOnTwoMatch()
        {
            if (_pendingTwoOnTwo == null) return;

            TwoOnTwoSetup setup = _pendingTwoOnTwo;
            int meSeat = SeatLayout.Normalize(setup.Seat);
            int mateSeat = SeatLayout.TeammateSeat(meSeat);
            int[] foeSeats = SeatLayout.OpponentSeats(meSeat);

            // 按座位 1..4 建四个人（空位先放 null，随后按算出的座位填入）
            var seated = new List<PlayerState>(4);
            var providers = new List<SimpleChoiceProvider>(4);
            for (int i = 0; i < 4; i++)
            {
                seated.Add(null);
                providers.Add(null);
            }

            var defs = new List<CharacterDefinition>(4);
            var names = new List<string>(4);
            for (int i = 0; i < 4; i++) { defs.Add(null); names.Add(null); }

            // 我（人类）
            defs[meSeat - 1] = setup.DefMe;
            names[meSeat - 1] = "我";
            // 队友（AI）
            defs[mateSeat - 1] = setup.DefMate;
            names[mateSeat - 1] = "队友";
            // 两名对手（随机，AI）
            defs[foeSeats[0] - 1] = setup.DefFoe1;
            names[foeSeats[0] - 1] = "对手1";
            defs[foeSeats[1] - 1] = setup.DefFoe2;
            names[foeSeats[1] - 1] = "对手2";

            for (int i = 0; i < 4; i++)
            {
                seated[i] = CharacterFactory.CreatePlayer(names[i], defs[i]);
                providers[i] = new SimpleChoiceProvider();

                // 登记"人类抉择器"，让 AI 代做的答案写回正确的玩家
                _sinkByPlayer[seated[i]] = providers[i];
            }

            _choiceA = providers[0];
            _choiceB = providers[1];
            _choiceC = providers[2];
            _choiceD = providers[3];

            BuildTwoOnTwoMatch(seated, providers, meSeat - 1);

            _pendingDefA = setup.DefMe;
            _pendingDefB = setup.DefFoe1;
        }

        /// <summary>
        /// 用"已经按座位 1..4 排好的四个人"建局并建控制器。
        ///
        /// 入门（座位玩家选）与经典（座位先攻定）**共用这一段** ——
        /// 两条路只是"怎么得到这个座位顺序"不同。
        ///
        /// <paramref name="meIndex"/> 是我在 <paramref name="seated"/> 里的下标，
        /// **只有他是人类**，其余三人都是 AI。
        /// </summary>
        private void BuildTwoOnTwoMatch(List<PlayerState> seated,
                                        List<SimpleChoiceProvider> providers,
                                        int meIndex,
                                        PlayerState firstToAct = null)
        {
            _game = GameManager.Create2v2(seated, providers.ConvertAll(x => (IChoiceProvider)x));

            // 玩家要求：**所有模式的轮次上限统一为 50 轮**。
            // 旧版给 2v2 单独设 100 回合，是因为那时按"每人一次行动"计数；
            // 现在按**轮**计数（一轮 = 所有角色都经过一次行动），
            // 50 轮在 2v2 里已经等于 200 个回合，不需要再放大。
            _game.RoundLimit = GameManager.DefaultRoundLimit;

            // 前两人的 AI 走构造参数（与 1v1 同一路径）；
            // 其余玩家用 BindPlayer 登记 —— 只有"我"是人类（ai 传 null）。
            _controller = new BoardController(
                _game,
                providers[0] as IPlayerChoiceSink, providers[1] as IPlayerChoiceSink,
                aiForA: (meIndex == 0) ? null : (IMoveSelector)MakeAI(),
                aiForB: (meIndex == 1) ? null : (IMoveSelector)MakeAI());

            for (int i = 2; i < 4; i++)
            {
                IMoveSelector ai = (i == meIndex) ? null : (IMoveSelector)MakeAI();
                _controller.BindPlayer(seated[i], ai, providers[i] as IPlayerChoiceSink);
            }

            _controller.AiResolver = new AiChoiceResolver(ProviderFor);

            // 谁先行动：
            //   · 经典：**1 号位**先动（先攻决定了座位，座位 1 就是全场最高点）
            //   · 入门：**我**先动（单机主视角先手，便于立即操作）
            //
            // ⚠️ 曾经无条件设成"我" —— 于是经典里我坐 4 号位却先动，
            //    1/2/3 号位的行动等于被跳过（玩家实测报告）。
            _controller.SetFirstPlayer(firstToAct ?? seated[meIndex]);

            _twoOnTwoSeated = seated;
            _twoOnTwoMeIndex = meIndex;
            _twoOnTwoMySeat = meIndex + 1;
        }

        // ---- 屏幕切换 ----

        /// <summary>
        /// 切换到指定界面。所有分组先全部隐藏，再显示目标分组。
        ///
        /// 同时**保证承载界面的画布是激活的** —— 否则一旦画布被别处关掉，
        /// 切屏就什么都不显示（实机表现为一片纯色，看起来像崩了）。
        /// </summary>
        public void ShowScreen(GameScreen screen)
        {
            _screen = screen;
            ApplyFrontEndScreen(screen);

            // 保险：画布是三个界面共用的，切回来时必须确保它是激活的
            if (_selectRoot != null && !_selectRoot.gameObject.activeSelf)
                _selectRoot.gameObject.SetActive(true);

            if (_modal != null) _modal.Hide();

            // 对局界面由 GameBoardUI 自己管显隐
            bool inMatch = screen == GameScreen.InMatch;
            _board?.SetVisible(inMatch);

            _screenMainMenu?.SetVisible(screen == GameScreen.MainMenu);
            _screenAdventure?.SetVisible(screen == GameScreen.Adventure);
            _screenStoryChapter?.SetVisible(screen == GameScreen.StoryChapterSelect);
            _screenStoryBattles?.SetVisible(screen == GameScreen.StoryBattleSelect);
            _screenAdventureFinale?.SetVisible(screen == GameScreen.AdventureFinaleSelect);
            if (screen == GameScreen.StoryBattleSelect) RefreshStoryProgress();
            _screenCompetition?.SetVisible(screen == GameScreen.Competition);
            _screenCasual?.SetVisible(screen == GameScreen.Casual);
            _screenDualGeneral?.SetVisible(screen == GameScreen.DualGeneral);
            _screenDualGeneralMenu?.SetVisible(screen == GameScreen.DualGeneralMenu);
            _screenFactionBattleMenu?.SetVisible(screen == GameScreen.FactionBattleMenu);
            _screenDualFactionMenu?.SetVisible(screen == GameScreen.DualFactionMenu);
            _screenIdentityBattleMenu?.SetVisible(screen == GameScreen.IdentityBattleMenu);
            _screenIdentitySelect?.SetVisible(screen == GameScreen.IdentitySelect);
            _screenDualSelect?.SetVisible(screen == GameScreen.DualGeneralSelect);
            _screenEightPlayerMenu?.SetVisible(screen == GameScreen.EightPlayerMenu);
            _screenFactionSelect?.SetVisible(screen == GameScreen.FactionSelect);
            _screenOneOnOne?.SetVisible(screen == GameScreen.OneOnOneMenu);
            _screenTwoOnTwo?.SetVisible(screen == GameScreen.TwoOnTwoMenu);
            _screenMode?.SetVisible(screen == GameScreen.ModeMenu);
            _screenDifficulty?.SetVisible(screen == GameScreen.DifficultyMenu);
            _screenCharacter?.SetVisible(screen == GameScreen.CharacterSelect);            _screenTwoOnTwoCharacter?.SetVisible(screen == GameScreen.TwoOnTwoCharacter);
            _screenTwoOnTwoInitiative?.SetVisible(screen == GameScreen.TwoOnTwoInitiative);
            _diceRoll?.SetVisible(screen == GameScreen.DiceRoll);
            ResetProfessionNavigation(screen);

            // ⚠️ 图鉴也必须在这里管住！
            //
            //    我原来只在 OnCodexClicked 里 SetVisible(true)，
            //    但**没有人把它关掉** ——
            //    于是点"返回"切回主菜单后，图鉴还盖在上面
            //    （玩家实测："点返回没有关闭图鉴"）。
            //    与其他屏幕一样，按"当前屏幕是不是它"来决定。
            _codex?.SetVisible(screen == GameScreen.Codex);

            if (screen == GameScreen.MainMenu || screen == GameScreen.OneOnOneMenu
                || screen == GameScreen.TwoOnTwoMenu
                || screen == GameScreen.ModeMenu || screen == GameScreen.DifficultyMenu
                || screen == GameScreen.CharacterSelect
                || screen == GameScreen.TwoOnTwoCharacter || screen == GameScreen.DiceRoll
                || screen == GameScreen.TwoOnTwoInitiative
                || screen == GameScreen.EightPlayerMenu
                || screen == GameScreen.FactionSelect)
            {
                // 让弹窗保持在最上层
                if (_modal?.Root != null) _modal.Root.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// 诊断用：当前界面的显隐快照。
        /// 排查"切屏后一片空白"时用它确认到底是画布被关了、还是分组没显示。
        /// </summary>
        public string DebugScreenInfo()
            => $"screen={_screen} " +
               $"canvas={( _selectRoot != null && _selectRoot.gameObject.activeSelf ? "on" : "OFF")} " +
               $"main={_screenMainMenu?.IsVisible} " +
               $"oneOnOne={_screenOneOnOne?.IsVisible} " +
               $"character={_screenCharacter?.IsVisible}";

        /// <summary>当前所在界面（供测试断言）。</summary>
        public GameScreen CurrentScreen => _screen;

        // ---- 角色选择 ----

        private void BuildCharacterScreen()
        {
            _screenCharacter = new ScreenGroup();

            Text title = MakeText(_selectRoot, "SelectTitle", new Vector2(0f, 470f),
                                  new Vector2(1200f, 70f), 48, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "选择角色";
            _selectTitle = title;
            _screenCharacter.Add(title.gameObject);

            // ---- 玩家 A ----
            _screenCharacter.Add(MakeSectionLabel("玩家角色", new Vector2(0f, 406f)));
            _rosterViewA = BuildRosterRow("RosterA", new Vector2(0f, 327f), initialIndex: _indexA);
            _rosterViewA.SelectionChanged += index =>
            {
                _indexA = index;
                _rosterViewA.ScrollToSelected();
                RefreshDetail(_detailA, _roster[_indexA]);
            };
            _screenCharacter.Add(_rosterViewA.Root.gameObject);
            _detailA = MakeDetailPanel("DetailA", new Vector2(0f, 232f));
            _screenCharacter.Add(_detailA.transform.parent.gameObject);

            // ---- 玩家 B ----
            _screenCharacter.Add(MakeSectionLabel("AI 角色", new Vector2(0f, 102f)));
            _rosterViewB = BuildRosterRow("RosterB", new Vector2(0f, 26f), initialIndex: _indexB);
            _rosterViewB.SelectionChanged += index =>
            {
                _indexB = index;
                _rosterViewB.ScrollToSelected();
                RefreshDetail(_detailB, _roster[_indexB]);
            };
            _screenCharacter.Add(_rosterViewB.Root.gameObject);
            _detailB = MakeDetailPanel("DetailB", new Vector2(0f, -70f));
            _screenCharacter.Add(_detailB.transform.parent.gameObject);

            RefreshDetail(_detailA, _roster[_indexA]);
            RefreshDetail(_detailB, _roster[_indexB]);

            Button start = MakeButton(_selectRoot, "Start", new Vector2(0f, -256f),
                                      new Vector2(340f, 76f), "开始对局");
            start.onClick.AddListener(OnStartClicked);
            _screenCharacter.Add(start.gameObject);

            Button back = MakeButton(_selectRoot, "SelectBack", new Vector2(-420f, -256f),
                                     new Vector2(220f, 76f), "返回");
            back.onClick.AddListener(() => ShowScreen(GameScreen.OneOnOneMenu));
            _screenCharacter.Add(back.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenCharacter.Add(help.gameObject);
        }

        /// <summary>小节标题（"玩家 A 角色"），返回节点以便登记进屏幕分组。</summary>
        private GameObject MakeSectionLabel(string label, Vector2 position)
        {
            Text text = MakeText(_selectRoot, label, position, new Vector2(600, 30),
                                 24, TextAnchor.MiddleCenter, new Color(0.88f, 0.91f, 0.97f));
            UiFont.ApplyBold(text);
            text.text = label;
            return text.gameObject;
        }

        /// <summary>建一条角色卡列表。</summary>
        private CharacterRosterView BuildRosterRow(string name, Vector2 position, int initialIndex)
        {
            var view = new CharacterRosterView();
            // 玩家要求：角色选择界面的技能介绍
            // **全部采用图鉴里【技能】那条短描述**。
            view.Build(_selectRoot, name, position, _roster, UiFont.Regular, initialIndex,
                       SkillHintText);
            view.ScrollToSelected();
            return view;
        }

        /// <summary>详情面板：显示选中角色的完整技能说明。</summary>
        private Text MakeDetailPanel(string name, Vector2 position)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(_selectRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1120f, 74f);
            rect.anchoredPosition = position;

            var img = go.GetComponent<Image>();
            FrontEndArt.Apply(img, "UI/panel_detail");
            img.raycastTarget = false;

            Text text = MakeText(rect, "Text", Vector2.zero, new Vector2(1092f, 62f),
                                 19, TextAnchor.MiddleLeft, new Color(0.86f, 0.90f, 0.96f));
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        /// <summary>刷新详情面板内容。</summary>
        private static void RefreshDetail(Text target, CharacterDefinition def)
        {
            if (target == null || def == null) return;

            // 详情也用图鉴那条短描述（与卡片一致）
            target.text = def.DisplayName + "　" + CharacterRosterView.DescribeStats(def) + "\n" + SkillHintText(def);
            var emblem = target.transform.parent.Find("SelectedEmblem");
            if (emblem != null) emblem.GetComponent<Image>().sprite = BattleArt.Get("Subclasses/" + (def.Id == "blank" ? "fighter_champion" : def.Id));
        }


        /// <summary>构造 Dropdown 需要的下拉模板（Item + 滚动区域）。纯代码搭建的标准套路。</summary>
        private RectTransform MakeDropdownTemplate(RectTransform parent, out Text itemLabel)
        {
            // 注意：Image / ScrollRect 这类 UI 组件会自动带上 RectTransform，
            // 所以创建时**不要**再把 RectTransform 写进组件列表（会报 "already added"）。
            var templateGo = new GameObject("Template", typeof(Image), typeof(ScrollRect));
            templateGo.transform.SetParent(parent, false);

            var templateRect = templateGo.GetComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0f, 0f);
            templateRect.anchorMax = new Vector2(1f, 0f);
            templateRect.pivot = new Vector2(0.5f, 1f);
            templateRect.anchoredPosition = new Vector2(0f, -2f);
            templateRect.sizeDelta = new Vector2(0f, 240f);

            templateGo.GetComponent<Image>().color = new Color(0.16f, 0.19f, 0.26f);

            var viewportGo = new GameObject("Viewport", typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(templateGo.transform, false);
            StretchFull(viewportGo.GetComponent<RectTransform>());
            viewportGo.GetComponent<Image>().color = Color.white;
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 60f);

            var itemGo = new GameObject("Item", typeof(Image), typeof(Toggle));
            itemGo.transform.SetParent(contentGo.transform, false);
            var itemRect = itemGo.GetComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0f, 0.5f);
            itemRect.anchorMax = new Vector2(1f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0f, 60f);

            var itemImg = itemGo.GetComponent<Image>();
            itemImg.color = new Color(0.20f, 0.24f, 0.32f);

            var checkGo = new GameObject("Item Checkmark", typeof(Image));
            checkGo.transform.SetParent(itemGo.transform, false);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(20f, 20f);
            checkRect.anchoredPosition = new Vector2(16f, 0f);

            itemLabel = MakeText(itemRect, "Item Label", Vector2.zero, new Vector2(-40f, 50f),
                                 24, TextAnchor.MiddleCenter, Color.white);
            itemLabel.rectTransform.anchorMin = Vector2.zero;
            itemLabel.rectTransform.anchorMax = Vector2.one;
            itemLabel.rectTransform.offsetMin = new Vector2(20f, 0f);
            itemLabel.rectTransform.offsetMax = new Vector2(-10f, 0f);

            var toggle = itemGo.GetComponent<Toggle>();
            toggle.targetGraphic = itemImg;
            toggle.graphic = checkGo.GetComponent<Image>();

            var scroll = templateGo.GetComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewportGo.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            // Dropdown 会在展开时自己设置 Content 的高度与位置，这里只需要保证初始值合法。
            contentRect.anchoredPosition = Vector2.zero;

            templateGo.SetActive(false);

            return templateRect;
        }

        // ============================================================
        // 流程
        // ============================================================

        private void OnStartClicked()
        {
            // ⚠️ 这里**不能**把 _selectRoot 整个关掉。
            //
            // `_selectRoot` 是**所有界面共用的那个画布**（主菜单 / 1v1 / 角色选择都在它下面）。
            // 曾经这里 `SetActive(false)`，而 `ShowScreen()` 只切分组显隐、不会重新激活画布，
            // 于是"返回标题 / 再来一局"之后整个 UI 都不见了 ——
            // 实机表现是一片纯色（相机背景），玩家以为游戏崩了。
            //
            // 隐藏界面是 `ShowScreen` 的职责（切分组显隐），这里不需要也不应该插手。

            // 从其他模式进来时先清掉双将标志，
            // 否则标题栏会把 1v1 当成双将显示。
            _isDualGeneral = false;

            CharacterDefinition defA = _roster[Mathf.Clamp(_indexA, 0, _roster.Count - 1)];
            CharacterDefinition defB = _roster[Mathf.Clamp(_indexB, 0, _roster.Count - 1)];

            // 经典模式先进"掷骰分配初始手值"，分配完才真正开始对局；
            // 入门模式不掷骰先攻，双方用默认初始手值直接开局。
            _pendingDefA = defA;
            _pendingDefB = defB;
            PrepareMatch(defA, defB);

            if (Mode == PlayMode.Intro)
            {
                StartPreparedMatch();
                return;
            }

            _diceRoll.Begin(_game);
            ShowScreen(GameScreen.DiceRoll);
        }

        /// <summary>
        /// 建立对局对象与控制器（**不调用 BeginTurn**）。
        ///
        /// 拆出来是因为开局要先掷骰分配初始手值：
        /// 分配阶段只改手的数值、不触发任何效果，所以可以安全地
        /// 在 <see cref="BoardController.BeginTurn"/> 之前完成。
        /// </summary>
        private void PrepareMatch(CharacterDefinition defA, CharacterDefinition defB)
        {
            _choiceA = new SimpleChoiceProvider();
            _choiceB = new SimpleChoiceProvider();

            _game = CharacterFactory.CreateMatch(defA, defB, _choiceA, _choiceB);

            // P1：A 永远是人类，B 可选 AI（同屏轮流或人机对战）
            IMoveSelector aiA = null;
            IMoveSelector aiB = PlayerBIsAI ? (IMoveSelector)MakeAI() : null;

            // 注意：sink 必须按玩家分别传（A 一份、B 一份）。
            // 曾经这里只传了 A 的 sink，导致 B 当防守方时选盾询问被静默跳过。
            _controller = new BoardController(_game, _choiceA, _choiceB,
                                              aiForA: aiA,
                                              aiForB: aiB);

            // AI 的"回合内抉择"（选盾 / 锁手）由 UI 注入解析器代做
            _controller.AiResolver = new AiChoiceResolver(ProviderFor);

            // ⚠️ 1v1 路径（含经典 / 入门 / 双将）必须清掉 2v2 标志。
            //    `_isTwoOnTwo` 只在 BuildTwoOnTwoMatch 里被置 true、无人复位，
            //    于是"先打 2v2 再玩 1v1"会残留成 2v2 的界面与标题。
            //    在**建局**这一层统一清，比在每个入口各清一次更可靠。
            _isTwoOnTwo = false;

            // `_twoOnTwoSeated` 同样从不清理 —— 残留会让诊断里
            // 打出一份"2v2 手值"，与当前这局完全不符。
            _twoOnTwoSeated = null;
            _isEightPlayer = false;
            _eightSeated = null;
        }

        /// <summary>掷骰分配完成后真正进入对局。</summary>
        private void OnDiceRollFinished()
        {
            if (_game == null || _controller == null) return;
            if (_pendingDefA == null || _pendingDefB == null) return;

            _controller.SetFirstPlayer(_diceRoll.StartingPlayer);
            if (_adventure?.UsesClassicSetup == true)
            {
                _adventure.BeginTutorial();
                StartPreparedMatch();
                _board.SetTitle(_adventure.DisplayTitle);
                _board.SetBattleBackground(_adventure.BattleBackgroundKey);
                return;
            }

            // 双将：骰先攻完了也走同一条路（与经典模式一致）
            if (_isDualGeneral)
            {
                StartPreparedMatch();
                return;
            }

            // 先手 = 点数**大**的那位（后选但先动，与"点数小先决定"互补）。
            // 必须在一局开始前设好，否则第一回合会按默认（A）走。
            _controller.SetFirstPlayer(_diceRoll.StartingPlayer);

            StartPreparedMatch();
        }

        private void RefreshIdentityLabels()
        {
            if (!_isIdentity || _game == null || !_game.IsIdentityMode
                || _eightSeated == null) return;

            for (int i = 0; i < _eightSeated.Count; i++)
            {
                PlayerState p = _eightSeated[i];
                if (p == null) continue;
                IdentityRole role = _game.IdentityOf(p);
                bool reveal = _game.IsIdentityPublic(p)
                              || ReferenceEquals(p, _eightHumanPlayer);
                string label = p.IsSummon ? "召唤物" : !reveal ? "？"
                    : role == IdentityRole.Lord ? _game.IdentityLordTitle
                    : IdentityRoleInfo.DisplayName(role);
                _board.SetRingIdentity(p, label, true);
                _board.SetRingRelation(p, IdentityPresentation.Relation(_game, _eightHumanPlayer, p));
            }
            IdentityPresentation.Counts(_game, _eightHumanPlayer, out int mates, out int foes);
            _board.SetTitle($"8 人身份局　我（{_eightHumanPlayer.Definition?.DisplayName ?? _eightHumanPlayer.Name}）　存活队友 {mates} 人　对手 {foes} 人");
        }

        /// <summary>
        /// 把已经 PrepareMatch 好的对局推到界面上并开始第一回合。
        /// </summary>
        private void StartPreparedMatch(bool beginTurn = true)
        {
            ShowScreen(GameScreen.InMatch);

            // ⚠️ **武僧·太极宗初始的 5 也进入太极形态**（玩家要求）。
            //
            //    必须在这里统一做一次：`ShieldState.Taichi` 原本只在
            //    "运算产生 Shield 效果"时才被设上，而开局直接摆出来的 5
            //    （掷骰决定的手值 / 直接开局）没经过那一步 ——
            //    于是"有 5 却不能发动太极"。
            //
            //    放在开局这里而不是各建局路径里，是为了让 1v1 与 2v2、
            //    入门与经典**共用同一处**，不会漏掉某条路。
            _game.ApplyInitialTaichiForm();

            // 8 人阵营混战：8 个座位，关系按阵营计算（自己 / 队友 / 对手）。
            if (_isEightPlayer && _eightSeated != null && _eightSeated.Count == 8)
            {
                PlayerState me = _eightHumanPlayer ?? _eightSeated[0];
                int mySeat = _eightSeated.IndexOf(me) + 1;
                var relations = new List<RingRelation>(8);
                for (int i = 0; i < 8; i++)
                    relations.Add(_isIdentity ? IdentityPresentation.Relation(_game, me, _eightSeated[i]) : ReferenceEquals(_eightSeated[i], me)
                        ? RingRelation.Self
                        : _game.IsTeammate(me, _eightSeated[i])
                            ? RingRelation.Teammate : RingRelation.Opponent);

                _board.BindPlayersRing(me, _eightSeated, mySeat, relations);
                string meName = !string.IsNullOrEmpty(me.DisplayNameOverride)
                    ? me.DisplayNameOverride
                    : me.Definition?.DisplayName ?? me.Name;
                string modeName = _isIdentity ? "8 人身份局"
                    : _isDualGeneral ? "双将阵营混战" : "8 人阵营混战";
                _board.SetTitle($"{modeName}　我（{meName}）" +
                                $"　队友 {relations.FindAll(r => r == RingRelation.Teammate).Count} 人" +
                                $"　对手 {relations.FindAll(r => r == RingRelation.Opponent).Count} 人");
                RefreshIdentityLabels();
            }
            else if (_isTwoOnTwo && _twoOnTwoSeated != null)
            {
                PlayerState me = _twoOnTwoSeated[_twoOnTwoMeIndex];
                PlayerState mate = _twoOnTwoSeated[SeatLayout.TeammateSeat(_twoOnTwoMySeat) - 1];
                _board.BindPlayersRing(me, _twoOnTwoSeated, _twoOnTwoMySeat);
                _board.SetTitle($"2 v 2　我（{me.Definition?.DisplayName ?? me.Name}）" +
                                $"　队友（{mate.Definition?.DisplayName ?? mate.Name}）");
            }
            else
            {
                _board.BindPlayers(_game.PlayerA, _game.PlayerB);

                if (_isDualGeneral)
                {
                    // 玩家要求：**战报只显示主将名**，
                    // 但标题栏不是战报—— 两名都列出来才知道对手有什么技能。
                    _board.SetTitle(
                        $"双将：我（{_pendingDefA.DisplayName} + {_dgSecondary?.DisplayName}" +
                        $"）  vs  人机（{_pendingDefB.DisplayName} + {_dgFoeSecondary?.DisplayName}）");
                }
                else
                {
                    _board.SetTitle($"{_pendingDefA.DisplayName}  vs  {_pendingDefB.DisplayName}");
                }
            }

            _board.HideGameOver();
            _board.SetExitButtonVisible(true);
            _board.SetVisible(true);

            if (beginTurn) _controller.BeginTurn();
            RefreshView();
        }

        public void StartMatch(CharacterDefinition defA, CharacterDefinition defB)
        {
            // 兼容旧入口（测试/直连）：跳过掷骰，直接用默认初始手值开局。
            _pendingDefA = defA;
            _pendingDefB = defB;
            PrepareMatch(defA, defB);

            ShowScreen(GameScreen.InMatch);

            _board.BindPlayers(_game.PlayerA, _game.PlayerB);
            _board.SetTitle($"{defA.DisplayName}  vs  {defB.DisplayName}");
            _board.HideGameOver();
            _board.SetExitButtonVisible(true);
            _board.SetVisible(true);

            _controller.BeginTurn();
            RefreshView();
        }

        private IMoveSelector MakeAI()
        {
            if (UseGreedyAI)
            {
                var ai = new GreedyChoiceProvider
                {
                    UseChainBreak = true,   // 连锁目标已按实际队友关系过滤。
                    UseValidatedTuning = true,

                    // 2v2 集火策略（按角色查表，见 FocusPolicies / Tools/focus-report.txt）：
                    // 只对实验证明有效的 8 个角色启用 focusSelf；1v1 里天然无副作用。
                    UseLearnedFocus = true,
                };

                // 难度**只在入门玩法**生效（玩家要求：经典保持不变）。
                // 经典模式走贪心 AI 的默认行为（不抖动 = 始终最优）。
                if (Mode == PlayMode.Intro) ai.ApplyDifficulty(Difficulty);

                return ai;
            }
            return new RandomChoiceProvider(12345);
        }

        /// <summary>重开：回到角色选择（保留已选角色，方便换对手再打）。</summary>
        private void OnRestartClicked()
        {
            if (_adventure != null) { StartAdventureLevel(_adventure.LevelNumber, true); return; }
            CleanupMatch();
            ShowScreen(GameScreen.CharacterSelect);
        }

        /// <summary>对局中的"返回标题"：回到主菜单。</summary>
        private void OnExitClicked()
        {
            ReturnToSelection();
        }

        /// <summary>
        /// 结束进程。**只在主菜单提供** ——
        /// 对局中的按钮一律是"返回标题"，避免误触直接关掉游戏。
        /// </summary>
        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>回到开始界面并清掉当前对局。</summary>
        private void ReturnToSelection()
        {
            CleanupMatch();
            ShowScreen(GameScreen.MainMenu);
        }

        /// <summary>
        /// 丢弃当前对局与表现层状态。
        ///
        /// 单独抽出来是因为**退出**（回主菜单）与**重开**（回角色选择）
        /// 都要做这套清理，只是最后去的界面不同。
        /// </summary>
        private void CleanupMatch()
        {
            _board.HideGameOver();
            _board.HideAdventureGuide();
            _board.SetBattleBackground();
            _board.ShowAdventureObstacles(false);
            _diceRoll?.SetLesson(null, null);
            _board.SetExitButtonVisible(false);
            _board.SetSkipButtonVisible(false);
            _board.SetAcceptButtonVisible(false);
            _board.SetDeclineButtonVisible(false);
            _board.SetVisible(false);

            _controller = null;      // 丢弃当前对局，避免 Update 里继续推进 AI
            _game = null;
            _adventure = null;      // 冒险关卡随对局一起丢弃
            _adventureVictoryShown = false;
            _aiDelayTimer = 0f;

            // ⚠️ **把这一局的"模式判定"一起关掉**（玩家要求：
            //    "每次游戏结束就把该游戏的判定关掉"）。
            //
            //    原来只清了 _controller 与 _game，而 2v2 的两个标志留着：
            //      _isTwoOnTwo      = true
            //      _twoOnTwoSeated  = 上一局的四个人（非空）
            //    后果：退出 2v2、回主菜单、再开一局 1v1（含双将）时，
            //    RefreshView 第一条就命中
            //      `if (_isTwoOnTwo && _twoOnTwoSeated != null && Count == 4)`
            //    于是**整局都按 2v2 渲染**，永远走不到 1v1 的 BindPlayers ——
            //    这正是玩家反复报告的"进了 2v2 之后进 1v1 还是 2v2 界面"。
            //
            //    放在 CleanupMatch 里而不是各个入口，是因为
            //    **退出**与**重开**都经过这里，一处即覆盖所有路径。
            _isTwoOnTwo = false;
            _twoOnTwoSeated = null;
            _isEightPlayer = false;
            _isIdentity = false;
            _eightHumanPlayer = null;
            _eightSeated = null;

            // 双将标志同理：它决定标题栏的写法
            _isDualGeneral = false;

            // 清掉表现层状态：否则震动可能停在偏移位置、闪烁残留、音效还在响
            _animatedTurn = null;
            _pauseUntil = 0f;
            _board.StopAnimations();
        }

        private void OnSkipTurnClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanTurn) return;

            if (_controller.SkipTurn()) RefreshView();
        }

        /// <summary>
        /// 点了「开赌」（游荡者·赌徒）：掷 1d9，点数直接作为本次行动结果。
        ///
        /// 掷出攻击时 <see cref="BoardController.Gamble"/> 会停在"选打谁"，
        /// 这里只是刷新界面让玩家看到可点的目标。
        /// </summary>
        private void OnGambleClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanChooser) return;

            bool moved = _controller.Gamble();

            // 诊断：开赌失败时把原因带出来 ——
            // 玩家实测"赌完之后卡住"，需要知道是没掷、还是掷了没走通。
            Debug.Log($"[FDD开赌] moved={moved} phase={_controller.Phase}" +
                      $" roll={_controller.LastGambleRoll}" +
                      $" selected={_controller.SelectedHand?.Value.ToString() ?? "NULL"}" +
                      $" 诊断={_controller.LastAiMoveDiagnostic}");

            if (moved) RefreshView();
        }

        /// <summary>
        /// 游荡者·盗贼点了「偷窃」：进入"选被偷的手"这一步。
        ///
        /// 玩家给的交互是三步：点自己的手 -> 点「偷窃」-> 点别人的手。
        /// 所以这里只是**推进阶段**，真正完成在
        /// <c>BoardController.ClickHand</c> 的 ChoosingStealTarget 分支。
        /// </summary>
        private void OnStealClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanChooser) return;

            bool moved = _controller.BeginSteal();

            Debug.Log($"[FDD偷窃] moved={moved} phase={_controller.Phase}" +
                      $" selected={_controller.SelectedHand?.Value.ToString() ?? "NULL"}");

            if (moved) RefreshView();
        }

        /// <summary>发动当前的"角色抉择"（战士·剑士的追加攻击、游荡者·风流剑客的强化等）。</summary>
        private void OnAcceptClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanChooser) return;

            // ⚠️⚠️ **必须无条件刷新。**
            //
            //    原来是 `if (_controller.AcceptPendingChoice()) RefreshView();` ——
            //    于是"发动"被拒绝时（例如法师·塑能学派的"放弃攻击"有 **2 个候选**，
            //    而 AcceptPendingChoice 要求**唯一候选**），
            //    界面**一点反应都没有**，玩家看到的就是**卡住**
            //    （玩家实测："法师·塑能学派点击放弃攻击后卡住"）。
            //
            //    界面必须永远反映控制器的真实状态；失败也要给出可读的原因。
            if (_adventure != null && !_adventure.AllowAcceptChoice) return;
            bool ok = _controller.AcceptPendingChoice();

            if (!ok)
            {
                Debug.LogWarning("[FingerDiceDuel]「发动」被拒绝：" +
                                 $"phase={_controller.Phase} " +
                                 $"pending={_controller.PendingEffect} " +
                                 $"acceptLabel='{_controller.PendingAcceptLabel}' " +
                                 "—— 该抉择有多个候选时请直接点手牌；界面已刷新。");
            }

            RefreshView();
        }

        /// <summary>放弃当前的"角色抉择"（游荡者·风流剑客的闪避 / 强化、野蛮人·守望者道途的锁手等）。</summary>
        private void OnDeclineClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanChooser) return;

            if (_adventure != null && !_adventure.AllowDeclineChoice) return;
            // 「放弃攻击」：已经选好参与运算的手，但没有合法攻击目标。
            // 放弃 = 算用掉本回合，保留运算结果，只取消本次攻击伤害。
            if (_controller.CanGiveUpAttack)
            {
                _controller.GiveUpAttack();
                RefreshView();
                return;
            }

            // ⚠️ 同样必须无条件刷新（理由见 OnAcceptClicked）。
            bool ok = _controller.DeclinePendingChoice();
            if (!ok)
            {
                Debug.LogWarning("[FingerDiceDuel]「不发动 / 放弃」被拒绝：" +
                                 $"phase={_controller.Phase} " +
                                 $"pending={_controller.PendingEffect} —— 界面已刷新。");
            }

            RefreshView();
        }

        /// <summary>
        /// **法师·塑能学派**：玩家在伤害类型面板上选了某一种。
        ///
        /// 玩家确认的顺序是"先问放弃 -> 选类型 -> 再选目标"，
        /// 所以这里选完之后流程继续走到"选攻击目标"那一步。
        /// </summary>
        private void OnDamageTypePicked(DamageType type)
        {
            if (_controller == null) return;
            if (type == DamageType.None) return;

            // ⚠️ **先关面板、再无条件刷新。**
            //
            //    我第一版写成"只有 `ChooseDamageType` 返回 true 才关面板 + 刷新"，
            //    于是它一旦返回 false（状态不符 / 找不到 sink），
            //    面板既不消失、界面也不刷新 —— 玩家看到的就是**卡住**
            //    （玩家实测："法师·塑能学派点击放弃攻击后卡住"）。
            //    界面必须**永远**反映控制器的真实状态，不能"失败就什么都不做"。
            _board.SetDamageTypePanelVisible(false);

            bool ok = _controller.ChooseDamageType(type);

            if (!ok)
            {
                Debug.LogWarning($"[FingerDiceDuel] 选择伤害类型（{type}）被拒绝：" +
                                 $"phase={_controller.Phase} " +
                                 $"pending={_controller.PendingEffect} —— 界面已刷新，请再试一次。");
            }

            RefreshView();
        }

        private void OnHandClicked(PlayerState owner, Hand hand)
        {
            if (_controller == null || _board.AdventureModalVisible) return;

            // 关键：用 IsHumanChooser 而不是 IsHumanTurn。
            // 进入"选盾"阶段时依然是攻击方的回合，但该做决定的是**防守方**；
            // 如果这里按"行动方是否人类"放行，那么"AI 攻击、人类防守"时
            // 玩家的选盾点击会被直接丢弃。
            if (!_controller.IsHumanChooser) return;

            // 冒险教学：教学白名单外的点击直接拒绝（点错不推进、提示重试）。
            if (_adventure != null && _adventure.Started
                && !_adventure.AllowClick(hand))
            {
                _board.SetHint(_adventure.RejectionHint ?? "跟着指引走，先完成这一步。",
                               DescribeLastTurn());
                return;
            }

            if (_controller.ClickHand(hand))
            {
                if (VerboseLogging && _controller.LastTurn != null)
                    Debug.Log(BoardController.DescribeTurn(_controller.LastTurn));

                RefreshView();
            }
        }

        /// <summary>
        /// 冒险教学关的可点性过滤：教学白名单外的手在界面上也表现为"不可点"。
        /// 只在"该人类做决定"时收紧；AI 回合与普通对局不受影响。
        /// </summary>
        private bool AdventureAllowsClick(Hand hand)
        {
            if (_adventure == null) return true;
            if (!_adventure.Started || _board.AdventureModalVisible) return false;
            if (!_controller.IsHumanChooser) return true;
            return _adventure.AllowClick(hand);
        }

        /// <summary>当前引导建议点击的手（作为战斗桌的额外高亮；无引导时为 null）。</summary>
        private List<Hand> AdventureHighlights()
        {
            if (_adventure == null || !_adventure.Started) return null;
            Hand suggested = _adventure.SuggestedHand;
            return suggested != null ? new List<Hand> { suggested } : null;
        }

        private void Update()
        {
            if (_controller == null) return;
            if (_controller.Phase == BoardPhase.GameOver) return;
            if (_board.AdventureModalVisible || (_adventure != null && !_adventure.Started)) { _aiDelayTimer = 0f; return; }
            if (IsPausing) return;              // 回合间停顿：暂停 AI 推进

            // ⚠️⚠️ **吟游诗人·勇气学院的抉择没答完之前，AI 一律不许行动。**
            //
            //    玩家实测："1 号吟游诗人·勇气学院受到 2 号伤害，玩家还没选完，
            //    3 号 4 号已经行动完了。"
            //    因为 `CollegeOfValorChoicePending` 不是 `BoardPhase`，AI 的推进只看了
            //    `IsHumanChooser` —— 那时"该谁决定"已经交回 AI，于是它继续打。
            //    这个面板是**半途插进来**的，所以必须单独挡一道。
            if (_controller.CollegeOfValorChoicePending)
            {
                _aiDelayTimer = 0f;
                return;
            }

            // 注意：判断依据是"现在该谁做决定"（IsHumanChooser），不是"轮到谁行动"。
            // 否则"AI 攻击、人类防守"时会停在这里等一个永远不会来的 AI 动作，
            // 而人类的选盾点击又已经放行了 —— 流程就会卡住。
            if (_controller.IsHumanChooser)
            {
                _aiDelayTimer = 0f;      // 人类做决定时重置，保证每次 AI 动作前都等满
                return;
            }

            // 需要 AI 做决定：先等 AiThinkSeconds 秒，让玩家看清刚才发生了什么。
            _aiDelayTimer += Time.unscaledDeltaTime;
            if (_aiDelayTimer < Mathf.Max(0f, AiThinkSeconds)) return;

            _aiDelayTimer = 0f;

            bool acted;
            if (_controller.Phase == BoardPhase.ChoosingAttackerHand)
                acted = _controller.MakeAutomaticMove();   // AI 的回合：出招
            else
                acted = _controller.ResolvePendingAiChoice();  // AI 只需代做一个抉择

            if (acted)
            {
                if (VerboseLogging && _controller.LastTurn != null)
                    Debug.Log(BoardController.DescribeTurn(_controller.LastTurn));

                RefreshView();
            }
        }

        // ============================================================
        // 刷新
        // ============================================================

        private void RefreshSacrificeButton()
        {
            if (!_isIdentity || _game == null || _eightHumanPlayer == null)
            {
                _board.SetSacrificeButtonVisible(false, false);
                return;
            }

            PlayerState lord = null;
            for (int i = 0; i < _game.Players.Count; i++)
            {
                PlayerState p = _game.Players[i];
                if (p != null && _game.IdentityOf(p) == IdentityRole.Lord) { lord = p; break; }
            }

            bool show = lord != null && lord.IsAlive
                        && _eightHumanPlayer.IsAlive
                        && _game.IdentityOf(_eightHumanPlayer) == IdentityRole.Loyalist;
            if (!show) _humanSacrificeArmed = false;
            _board.SetSacrificeButtonVisible(show, _humanSacrificeArmed);
            if (_game != null)
                _game.HumanSacrificeCandidate = _humanSacrificeArmed ? _eightHumanPlayer : null;
        }

        private void OnSacrificeClicked()
        {
            if (!_isIdentity || _game == null || _eightHumanPlayer == null) return;
            if (!_eightHumanPlayer.IsAlive
                || _game.IdentityOf(_eightHumanPlayer) != IdentityRole.Loyalist) return;

            _humanSacrificeArmed = !_humanSacrificeArmed;
            RefreshView();
        }

        private void RefreshView()
        {
            if (_controller == null || _board == null || _game == null) return;
            _adventure?.Tick();
            SyncSeatLayout();

            // ⚠️ **必须先算可点性，再刷新高亮**。
            //
            //    高亮（RefreshSide）现在依赖 `HandCard.IsClickable`，
            //    而这个标记由 SetInteractable 写入。原来 SetInteractable
            //    在 Refresh 之后调用 —— 于是高亮读到的永远是**上一帧**的可点性，
            //    表现成"对手的手明明能点却没有高光"（玩家反复实测到）。
            _board.EffectContext=_game;
            _board.SetInteractable((owner, hand) => _controller.CanAct(hand) && AdventureAllowsClick(hand));

            IReadOnlyList<Hand> choices =
                _controller.Phase == BoardPhase.ChoosingDefenseShield
                    ? (IReadOnlyList<Hand>)_controller.DefenseShieldChoices
                    : _controller.ChoiceCandidates;

            // 2v2：四个人绑在环形位置上；下方那组手牌永远是"我"的（可点），
            // 上方那组跟随**当前行动方**（这样能看见 AI 在打哪只手）。
            if (_isEightPlayer && _eightSeated != null && _eightSeated.Count == 8)
            {
                _board.SetRingActive(_controller.ActivePlayer,
                                     _eightHumanPlayer ?? _eightSeated[0]);

                _board.RefreshRing(_eightSeated, _controller.SelectedHand, choices,
                                   turnCount: _controller.DisplayRound,
                                   turnLimit: _controller.RoundLimit,
                                   showSkipButton: ShowSkipButton(),
                                   skipButtonInteractable: _controller.CanSkipTurn);

                RefreshIdentityLabels();

                if (_controller.Chooser != null)
                    _board.PlaceChoiceButtonsNextTo(_controller.Chooser);
            }
            else if (_isTwoOnTwo && _twoOnTwoSeated != null && _twoOnTwoSeated.Count == 4)
            {
                PlayerState me = _twoOnTwoSeated[Mathf.Clamp(_twoOnTwoMeIndex, 0, 3)];
                _board.SetRingActive(_controller.ActivePlayer, me);

                // 每个人的手牌都显示在自己的环形面板上，
                // 并且都参与"当前阶段候选高亮"（玩家要看清点哪里）
                _board.RefreshRing(_twoOnTwoSeated, _controller.SelectedHand, choices,
                                   turnCount: _controller.DisplayRound,
                                   turnLimit: _controller.RoundLimit,
                                   showSkipButton: ShowSkipButton(),
                                   skipButtonInteractable: _controller.CanSkipTurn);

                // ⚠️ "发动 / 不发动"在 2v2 里要按**当前做抉择那位的手牌**重新定位。
                //    它们的默认位置是"相对画布中心 x=700"（1v1 的屏幕右侧），
                //    在环形布局里会落到左下角的战报上面（玩家实测截图确认）。
                //    1v1 不调用它 —— 那边已验证过的布局不动。
                //    （`IsInChoicePhase` 是私有的，这里用公开的 `Chooser` 判断：
                //      "有抉择归属者"就说明正处在抉择阶段。）
                if (_controller.Chooser != null)
                    _board.PlaceChoiceButtonsNextTo(_controller.Chooser);
            }
            else
            {
                _board.Refresh(_game.PlayerA, _game.PlayerB,
                               _controller.SelectedHand,
                               choices,
                               showSkipButton: ShowSkipButton(),
                               skipButtonInteractable: _controller.CanSkipTurn,
                               turnCount: _controller.DisplayRound,
                               turnLimit: _controller.RoundLimit,
                               extraHighlights: AdventureHighlights());
                if (_controller.Chooser != null)
                    _board.PlaceChoiceButtonsNextTo(_controller.Chooser);
            }

            // 游荡者·赌徒的「开赌」按钮：只在"等开赌"这一步显示
            _board.SetGambleButtonVisible(_controller.CanGamble);

            // 游荡者·盗贼的「偷窃」按钮：只在"点了自己的手、且本回合没偷过"时显示
            _board.SetStealButtonVisible(_controller.CanSteal);
            RefreshSacrificeButton();

            _board.SetLog(_controller.Log);
            _board.SetHint(DescribeHint(), DescribeLastTurn());

            // ⚠️ 把"结算被拦截 / 非法运算"这类异常**打到 Console**。
            //
            // 只写进战报是不够的：玩家遇到问题时能拿到的是 Console 输出，
            // 而这类问题（空运算源）人工很难复现，日志是唯一的线索。
            // 用"内容变了才打"避免每帧刷屏。
            LogControllerAnomaly();

            PlayTurnAnimations();

            // 回合切换过渡：点亮当前行动方的名字 + 一个提示音
            if (_game != null && _controller.Phase != BoardPhase.GameOver)
            {
                _board.ShowActivePlayer(_controller.ActivePlayer);
                _board.PlaySfx(SfxKind.TurnSwitch);
            }

            // 角色抉择的出口：
            //   · "发动"按钮 —— 只有一个候选时（战士·剑士追加、游荡者·风流剑客强化、牧师·生命领域的攻击/回复）
            //   · "不发动 / 放弃攻击"按钮 —— 允许放弃时
            //
            // 「放弃攻击」是**新流程专有**的出口：玩家已选定参与运算的手，
            // 但当前**没有合法攻击目标**（游荡者·刺客潜行，日后还有攻击距离不足等），
            // 此时"放弃攻击"是唯一出路。放弃 = **算用掉本回合**。
            bool giveUp = _controller.IsHumanChooser && _controller.CanGiveUpAttack;
            bool canDecline = giveUp
                              || (_controller.IsHumanChooser && _controller.CanDeclinePendingChoice);

            // ⚠️ 确认目标阶段**不显示**"不发动" —— 那一步只有"点目标的手确认"
            //    与（没有合法目标时的）"放弃攻击"。
            //    留着它会让玩家点出一个无效操作，甚至触发死循环
            //    （玩家的死循环就是这么点出来的）。
            if (_controller.Phase == BoardPhase.ChoosingTargetPlayer && !giveUp)
                canDecline = false;

            _board.SetDeclineButtonVisible(canDecline && (_adventure == null || _adventure.AllowDeclineChoice));
            if (canDecline) _board.SetDeclineButtonLabel(giveUp ? "放弃攻击" : DeclineLabel());

            string acceptLabel = _controller.IsHumanChooser ? _controller.PendingAcceptLabel : null;
            // 没有合法目标时不给"发动"（只让玩家放弃），避免打出一个无效的攻击
            bool canAccept = canDecline && !giveUp && !string.IsNullOrEmpty(acceptLabel);
            _board.SetAcceptButtonVisible(canAccept && (_adventure == null || _adventure.AllowAcceptChoice));
            if (canAccept) _board.SetAcceptButtonLabel(acceptLabel);

            // ⚠️ **法师·塑能学派：轮到"选伤害类型"时弹 10 选 1 面板，而不是普通的发动/不发动。**
            //
            //    玩家确认的顺序：先问"是否放弃攻击" -> 选类型 -> 再选目标。
            //    这一步必须**优先**于发动按钮处理，否则玩家会看到
            //    一个没有意义的"就选它"按钮。
            bool pickingDamageType =
                _controller.IsHumanChooser
                && _controller.PendingEffect == HandEffect.SchoolOfEvocationDamageType;

            _board.SetDamageTypePanelVisible(pickingDamageType);
            if (pickingDamageType)
            {
                // 面板已经替代了这两个按钮
                _board.SetAcceptButtonVisible(false);
                _board.SetDeclineButtonVisible(false);
            }

            _board.SetSkillActionContext(_controller.PendingEffect,
                _controller.IsHumanChooser ? _controller.Chooser : null, _controller.ActivePlayer,
                _controller.Phase == BoardPhase.ChoosingSeatSwapTarget ? _controller.ChoiceCandidates
                    : _controller.PendingRequest?.Candidates, canAccept);
            _board.SetSeatSwapActionVisible(_controller.IsHumanTurn
                && ReferenceEquals(_controller.ActivePlayer,HumanPlayer()) && _controller.CanCastle);

            // 临时诊断：把面板显隐的真实状态打出来 ——
            // "点了放弃却看不到 10 选 1"这类问题只能靠它定位
            // （是没进这个分支、还是面板没显示出来）。
            if (_controller.PendingEffect == HandEffect.SchoolOfEvocationDamageType
                || _board.IsDamageTypePanelVisible)
            {
                Debug.Log($"[FDD伤害类型] phase={_controller.Phase} " +
                          $"pending={_controller.PendingEffect} " +
                          $"isHuman={_controller.IsHumanChooser} " +
                          $"picking={pickingDamageType} " +
                          $"panelVisible={_board.IsDamageTypePanelVisible}");
            }

            _board.SetWizardChoices(_controller,RefreshView, _adventure == null ? (Func<StatusEffect, bool>)null : _adventure.AllowManeuver);

            // —— 冒险教学：导演推进 / 引导旁白 / 胜利要点卡 ——
            if (_adventure != null)
            {
                if (_adventure.Started && !_adventure.IsFinished)
                    _board.ShowAdventureNarration($"{AdventureLevel1.GuideName} · {_adventure.StepNumber}/{_adventure.StepCount}", _adventure.Narration,
                                                  AdventureLevel1.GuideAvatarKey);
                else
                    _board.HideAdventureNarration();

                if (_adventure.IsFinished && !_adventureVictoryShown)
                {
                    _adventureVictoryShown = true;
                    if (_adventure.IsComplete)
                    {
                        PlayerPrefs.SetInt(_adventure.CompletionKey, 1);
                        if (_adventure.LevelNumber == 10) PlayerPrefs.SetInt(AdventureChapter.CompletionKey, 1);
                        PlayerPrefs.Save();
                    }
                    string heading = _adventure.IsComplete ? _adventure.ResultHeading
                        : _game.Winner == null ? "训练平局" : "训练失利";
                    int next = _adventure.LevelNumber + 1;
                    _board.ShowAdventureVictory(heading + " · " + _adventure.DisplayTitle,
                        _adventure.IsComplete ? _adventure.SummaryText : _adventure.FailureText, AdventureLevel1.GuideAvatarKey,
                        OnAdventureReplay, ReturnToAdventure,
                        _adventure.IsComplete && next <= AdventureChapter.BattleCount ? (System.Action)(() => StartAdventureLevel(next)) : null,
                        next <= AdventureChapter.BattleCount ? "继续" + AdventureBattleName(next) : null,
                        _adventure.UsesClassicSetup ? "重新投骰" : "重打本关");
                }
            }

            if (_controller.Phase == BoardPhase.GameOver && _adventure == null)
            {
                _board.ShowGameOver(WinMessage());
                _board.SetSkipButtonVisible(false);
                _board.SetDamageTypePanelVisible(false);
                _board.HideCollegeOfValorChoicePanel();
            }
            else
            {
                // ⚠️ **吟游诗人·勇气学院：本回合受过伤 / 实际回过血 -> 弹四步面板。**
                //
                //    这是第一个"运算之后才问"的技能，所以不接阶段机，
                //    只在这里按需开面板（① 是否发动由玩家在面板里选）。
                //
                //    ⚠️ 只在"从没弹变成该弹"时调 `Show` —— `Show` 会把步骤重置回第 ② 步，
                //       每次刷新都调会把玩家已经点过的进度清掉。
                bool offer = _controller.OfferCollegeOfValorChoiceIfAny();
                if (offer && !IsHumanPlayer(_controller.CollegeOfValorActor))
                {
                    // AI 的吟游诗人·勇气学院自己选完，不弹玩家面板。
                    _controller.AutoResolveAiCollegeOfValorChoice();
                    if (_board.IsCollegeOfValorChoicePanelVisible) _board.HideCollegeOfValorChoicePanel();
                }
                else if (offer && !_board.IsCollegeOfValorChoicePanelVisible)
                    _board.ShowCollegeOfValorChoicePanel(_controller, RefreshView);
                else if (!offer && _board.IsCollegeOfValorChoicePanelVisible)
                    _board.HideCollegeOfValorChoicePanel();
            }

            DiagnoseIfNeeded(canDecline, acceptLabel, canAccept);
        }

        /// <summary>
        /// 把控制器的异常现场打到 Console（内容变化时才打，避免刷屏）。
        ///
        /// 目前看两件事：
        ///   · <c>LastIllegalOperation</c> —— 守门拦截或非法运算的完整现场
        ///   · 最近一条战报里含"被拦截 / 非法"的内容
        /// </summary>
        private string _lastLoggedAnomaly;

        private void LogControllerAnomaly()
        {
            if (_controller == null) return;

            string anomaly = _controller.LastIllegalOperation;
            if (string.IsNullOrEmpty(anomaly)) return;
            if (anomaly == _lastLoggedAnomaly) return;   // 没变就不重复打

            _lastLoggedAnomaly = anomaly;
            Debug.LogWarning("[FDD异常] 结算现场：" + anomaly);
        }


        /// <summary>
        /// 打印一次抉择现场。只在开诊断时输出，正常游玩不会刷屏。
        /// </summary>
        private void DiagnoseIfNeeded(bool canDecline, string acceptLabel, bool canAccept)
        {
            if (!DiagnoseChoices && !VerboseLogging) return;

            var sb = new System.Text.StringBuilder();
            sb.Append("[FDD诊断] ").Append(_controller.DebugPhaseInfo());
            sb.Append($" | canDecline={canDecline} acceptLabel='{acceptLabel}' canAccept={canAccept}");

            // ⚠️ 手值必须按**当前对局**取：2v2 有四人，写死 A/B 会打出错误信息
            //    （曾经这里只打 PlayerA/B，2v2 里等于漏掉两个人）。
            if (_twoOnTwoSeated != null && _twoOnTwoSeated.Count == 4)
            {
                sb.Append(" | 2v2 手值：");
                for (int i = 0; i < 4; i++)
                {
                    PlayerState p = _twoOnTwoSeated[i];
                    sb.Append($"{i + 1}号位({p?.Name}) ")
                      .Append($"L{p?.LeftHand.Value} R{p?.RightHand.Value} ")
                      .Append($"HP{p?.Health} ");
                }
            }
            else
            {
                sb.Append($" | 我方手 L{_game.PlayerA?.LeftHand.Value} R{_game.PlayerA?.RightHand.Value}");
                sb.Append($" | 对手手 L{_game.PlayerB?.LeftHand.Value} R{_game.PlayerB?.RightHand.Value}");

                // ⚠️ 玩家报过"游荡者·赌徒的显示和实际不一样"，两处读的却是同一个对象 ——
                //    所以把**展示层真正用的**手牌对象也打出来（附对象哈希），
                //    这样能确认"显示与诊断读的是不是同一份数据"，
                //    而不是继续在两边各自的读数上打转。
                int aHash = _game.PlayerA?.LeftHand != null ? _game.PlayerA.LeftHand.GetHashCode() : 0;
                int bHash = _game.PlayerB?.LeftHand != null ? _game.PlayerB.LeftHand.GetHashCode() : 0;
                sb.Append($" | [对象] A哈希={aHash} B哈希={bHash}");
            }

            // ⚠️ 关键：把"抉择创建时那次运算"的两只手打出来。
            // 请求没被创建（提示条无文字）时，问题通常就在这里 ——
            // 例如 ActedHand 指向了"另一只手"，于是"另一只手是 2"的条件不成立。
            int actedValue = _controller.PendingActedHand?.Value ?? 0;
            int targetValue = _controller.PendingTargetHand?.Value ?? 0;

            sb.Append($" | 本次运算 {SideLabelOf(_controller.PendingActedHand)}({actedValue})")
              .Append($" + 目标{targetValue} = {Hand.ComputeSum(actedValue, targetValue)}")
              .Append($" 是否攻击={_controller.PendingIsAttack}");

            sb.Append(" | 队列：");
            if (_controller.PendingRequests.Count == 0)
            {
                sb.Append("（无）");
            }
            else
            {
                for (int i = 0; i < _controller.PendingRequests.Count; i++)
                {
                    ChoiceRequest r = _controller.PendingRequests[i];
                    sb.Append($"[{i}]{r.Effect}(v{r.Variant},cand{r.Candidates.Count},")
                      .Append($"accept='{r.AcceptLabel}') ");
                }
            }

            sb.Append(" | 收集：").Append(_controller.LastCollectInfo ?? "(无)");

            // 最近一次结算的关键信息：能区分"这一回合到底结算了什么" ——
            // 玩家报"开赌掷出 8"却又"掷出 5"时，靠它判断是不是发生了两次。
            TurnResult lt = _controller.LastTurn;
            sb.Append($" | 最近结算={(lt == null ? "无" : lt.Actor?.Name)}")
              .Append(lt == null ? "" : $" 赌博={lt.WasGamble} 点数={lt.GambleRoll} 结果={lt.ResultValue}")
              .Append($" | 本次赌点={_controller.PendingGambleRoll}/{_controller.LastGambleRoll}");

            sb.Append(" | AI作答：");
            if (_controller.AiTrace.Count == 0) sb.Append("（无）");
            for (int i = 0; i < _controller.AiTrace.Count; i++)
                sb.Append('[').Append(_controller.AiTrace[i]).Append("] ");

            Debug.Log(sb.ToString());
        }

        private string SideLabelOf(Hand hand)
        {
            if (hand == null) return "（无）";
            if (ReferenceEquals(hand, _game.PlayerA?.LeftHand)) return "我方左";
            if (ReferenceEquals(hand, _game.PlayerA?.RightHand)) return "我方右";
            if (ReferenceEquals(hand, _game.PlayerB?.LeftHand)) return "对方左";
            if (ReferenceEquals(hand, _game.PlayerB?.RightHand)) return "对方右";
            return "未知";
        }

        /// <summary>
        /// 跳过回合按钮依当前人类行动者与控制器规则显示，不依赖座位编号。
        /// 按钮挂在 A 的右手牌右侧，所以 B 的回合不应该显示它。
        /// </summary>
        /// <summary>
        /// 当前是否该显示"跳过回合"按钮。
        ///
        /// ⚠️ 曾经写死 `ActivePlayer == _game.PlayerA` ——
        ///    1v1 里勉强对（人类就是 A），但 **2v2 里完全错**：
        ///    人类可能是 2/3/4 号位，于是按钮永远不显示
        ///    （玩家实测："2v2 中邪术师·至高妖精没有跳过回合按钮"）。
        ///
        /// 正确的判据是"**当前行动方是不是人类**"，与座位无关。
        /// </summary>
        private bool ShowSkipButton()
        {
            bool show = _controller != null
                        && _controller.IsHumanTurn
                        && ReferenceEquals(_controller.ActivePlayer, HumanPlayer())
                        && _controller.CanSkipTurn;

            // 诊断：按钮该显示却没显示时，把四个判据逐个打出来。
            // 这类"界面没画"的问题光看截图分不清是规则判错还是漏传参数。
            if (VerboseLogging && !show && _controller != null
                && _controller.ActivePlayer != null
                && _controller.ActivePlayer.Character is TheArchfeyLogic)
            {
                string line =
                    $"[FDD跳过按钮] 不显示 -> isHumanTurn={_controller.IsHumanTurn}" +
                    $" active={_controller.ActivePlayer?.Name}" +
                    $" human={HumanPlayer()?.Name}" +
                    $" same={ReferenceEquals(_controller.ActivePlayer, HumanPlayer())}" +
                    $" phase={_controller.Phase}" +
                    $" canSkip={_controller.CanSkipTurn}" +
                    $" hp={_controller.ActivePlayer?.Health}/{_controller.ActivePlayer?.MaximumHealth}";

                if (line != _lastSkipDiag) { _lastSkipDiag = line; Debug.Log(line); }
            }

            return show;
        }

        /// <summary>诊断去重用（同样的原因只打一次，避免每帧刷屏）。</summary>
        private string _lastSkipDiag;

        /// <summary>本局里"人类控制的那位玩家"（1v1 是 A；2v2 按我的座位取）。</summary>
        private PlayerState HumanPlayer()
        {
            if (_isEightPlayer && _eightHumanPlayer != null) return _eightHumanPlayer;
            if (_isTwoOnTwo && _twoOnTwoSeated != null && _twoOnTwoSeated.Count == 4)
                return _twoOnTwoSeated[Mathf.Clamp(_twoOnTwoMeIndex, 0, 3)];

            return _game?.PlayerA;
        }

        private string DescribeHint()
        {
            string hint = _controller.Hint;

            if (_controller.Phase == BoardPhase.GameOver) return hint;

            // 兜底：任何情况下都不要让提示条空着，否则玩家不知道能做什么
            if (string.IsNullOrEmpty(hint))
                hint = _controller.IsHumanChooser
                    ? "请选择一张手牌。"
                    : "等待对手行动……";
            else if (!_controller.IsHumanChooser)
                hint = $"{_controller.Chooser?.Name}（AI）正在思考……";

            if (_controller.IsExtraAction)
            {
                int total = _controller.ExtraActionsRemaining + 1;
                hint = $"【额外行动】剩余 {total} 次 · {hint}";
            }

            return hint;
        }

        /// <summary>
        /// 播放最近一次结算的表现（飘字）。
        ///
        /// 只在"出现新的结算结果"时播一次 —— <see cref="RefreshView"/> 在一次结算里
        /// 可能被调用好几次（点手牌、AI 落子、放弃抉择），不去重就会重复飘。
        /// </summary>
        private void PlayTurnAnimations()
        {
            if (_controller == null || _game == null) return;

            TurnResult result = _controller.LastTurn;
            if (ReferenceEquals(result, _animatedTurn)) return;

            _animatedTurn = result;
            if (result == null) return;

            // 受击方优先用结算里记录的**真实挨打者**（8 人局不是 A/B 对偶）；
            // 旧数据没有该字段时退回被加数手主 / 1v1 对偶。
            PlayerState defender = result.Defender
                ?? _game.OwnerOfHand(result.TargetHand);
            if (defender == null)
            {
                defender = ReferenceEquals(result.Actor, _game.PlayerA)
                    ? _game.PlayerB
                    : _game.PlayerA;
            }

            // ⚠️ 带上本次攻击的伤害类型 —— 伤害与"格挡"飘字用它上色，
            //    颜色与攻击类型图标同源（玩家要求）。
            AnimationPlan plan = AnimationDirector.Build(result, defender, result.AttackType);
            _board.PlayAnimations(plan, _game.PlayerA, _game.PlayerB);

            // 回合间停顿：按伤害量动态 0.25~0.5 秒。
            // 伤害是瞬间算完的，不停一下玩家还没看清震屏与飘字就轮到对面了。
            _pauseUntil = Time.unscaledTime + plan.PauseSeconds;
        }

        /// <summary>是否还在"回合间停顿"中。</summary>
        private bool IsPausing => Time.unscaledTime < _pauseUntil;

        /// <summary>放弃按钮的文案（按抉择类型区分）。</summary>
        private string DeclineLabel()
        {
            if (_controller.Phase == BoardPhase.ChoosingSeatSwapTarget) return "取消换位";
            ChoiceRequest? request = _controller.PendingRequest;
            if (request == null) return "不发动";

            switch (request.Value.Effect)
            {
                case HandEffect.SwashbucklerDodge: return "不闪避";
                case HandEffect.SwashbucklerDuelistThrust: return "不强化";
                case HandEffect.PathOfTheWardenLockTarget: return "不锁手";
                case HandEffect.BreakerChainBreak: return "不发动";
                case HandEffect.SwordsmanFollowUp: return "不追加";
                // 游侠·幽域追踪者伏击（与战士·剑士追加攻击同一条通道）
                case HandEffect.GloomStalkerAmbush: return "不伏击";
                // 回复目标是**必选**的（玩家要求：选了回复就必须回复、
                // 运算出 9 必须回复、双 9 两段都必须回复），
                // 所以这里不会走到 —— 按钮根本不会出现。
                // 点自己的手就是回自己、点对方的手就是回对方。
                case HandEffect.LifeDomainAttackOrHeal: return "攻击";
                default: return "不发动";
            }
        }

        /// <summary>
        /// 战斗说明：把最近一次结算的结果也显示在提示条里，
        /// 这样玩家不用去翻下方的战报就知道"刚才发生了什么"。
        /// </summary>
        private string DescribeLastTurn()
        {
            TurnResult last = _controller?.LastTurn;
            if (last == null || last.Actor == null) return "";

            // ⚠️ 只在"**现在还是刚结算那位行动方的回合**"时才显示上一手摘要。
            //
            // 否则会挂着**上一位行动者**的结算 ——
            // 玩家实测看到："轮到圣武士·奉献之誓，摘要却写着 游荡者·风流剑客 左手: 5+1=6"。
            // 那其实是上一回合游荡者·风流剑客打的那一手，与新回合无关，纯属误导。
            //
            // 为什么不比回合号：`BoardController.DisplayTurn` 与
            // `GameManager.TurnCount` 语义差 1（前者是"当前第几回合"、
            // 后者是"已完成几回合"），拿它们相等去比永远不会成立。
            // 直接比行动方既准确又贴合玩家描述的现象。
            if (!ReferenceEquals(_controller.ActivePlayer, last.Actor)) return "";

            return BoardController.DescribeTurn(last);
        }

        private string WinMessage()
        {
            if (_game == null || !_game.IsGameOver) return "";
            if (_game.Winner == null) return "平局！";

            // ⚠️ 玩家要求：**2v2 要把一整队的人全列出来**。
            //    原来只写 `_game.Winner.Name` —— 那是"最终活着的那个玩家"，
            //    队友（哪怕一起打赢了、只是先倒下了）完全没被提到。
            System.Collections.Generic.IReadOnlyList<PlayerState> all = _game.Players;
            if (all == null || all.Count <= 2) return $"{_game.Winner.Name} 获胜！";

            var names = new System.Text.StringBuilder();
            for (int i = 0; i < all.Count; i++)
            {
                PlayerState p = all[i];
                if (p == null) continue;
                if (!_game.IsTeammate(_game.Winner, p) && !ReferenceEquals(p, _game.Winner))
                    continue;

                if (names.Length > 0) names.Append("、");
                names.Append(p.Name);

                // 先倒下的队友标出来，玩家看得出"是谁扛下来的"
                if (!p.IsAlive) names.Append("（已倒下）");
            }

            return $"{names} 的队伍获胜！";
        }

        // ============================================================
        // 小工具
        // ============================================================

        private static Font LoadFont() => UiFont.Get();

        private Text MakeText(RectTransform parent, string name, Vector2 position, Vector2 size,
                              int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>建 2v2 先攻界面并接上"整轮完成"回调。</summary>
        private void BuildInitiativeScreen()
        {
            _screenTwoOnTwoInitiative = new TwoOnTwoInitiativeScreen();
            _screenTwoOnTwoInitiative.Build(_selectRoot, _font);

            // ⚠️ 必须先 Build 再订阅 —— 事件挂在界面实例上。
            _screenTwoOnTwoInitiative.Finished += OnTwoOnTwoInitiativeFinished;
        }

        // ---- 2v2 角色选择 ----
        //
        // 玩家要求：
        //   · 在角色选择界面**选择这次的座位**
        //   · **可以为自己和人机队友选择角色**
        //   · **人机对手随机选择角色**
        //
        // 座位决定四人位置，推导全部来自 Assistant.SeatLayout（已单测覆盖）：
        //   奇数座位 -> 队友是**上家**（界面偏左下）
        //   偶数座位 -> 队友是**下家**（界面偏右下）
        // 所以界面上要**实时显示**"队友在哪一侧"，让玩家选座位时看得见后果。

        private void BuildTwoOnTwoCharacterScreen()
        {
            _screenTwoOnTwoCharacter = new ScreenGroup();

            Text title = MakeText(_selectRoot, "TtCharTitle", new Vector2(0f, 470f),
                                  new Vector2(1200f, 66f), 44, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.90f, 0.62f));
            UiFont.ApplyBold(title);
            title.text = "2 v 2 角色选择";
            _screenTwoOnTwoCharacter.Add(title.gameObject);

            // ---- 座位选择（1..4）----
            //
            // ⚠️ **经典玩法下这一整块都不显示**（玩家要求）：
            //    经典是"四人各投 1d20 定座位"，玩家不该也不能自己挑座位。
            //    入门玩法没有先攻骰，座位才由玩家选。
            _seatPickerRoot = new GameObject("SeatPicker", typeof(RectTransform));
            _seatPickerRoot.transform.SetParent(_selectRoot, false);
            var seatRect = _seatPickerRoot.GetComponent<RectTransform>();
            seatRect.anchorMin = seatRect.anchorMax = new Vector2(0.5f, 0.5f);
            seatRect.pivot = new Vector2(0.5f, 0.5f);
            seatRect.sizeDelta = new Vector2(1400f, 200f);
            seatRect.anchoredPosition = new Vector2(0f, 380f);
            _screenTwoOnTwoCharacter.Add(_seatPickerRoot);

            Text seatLabel = MakeText(seatRect, "SeatLabel", new Vector2(0f, 42f),
                                      new Vector2(600f, 30f), 24, TextAnchor.MiddleCenter,
                                      new Color(0.88f, 0.91f, 0.97f));
            UiFont.ApplyBold(seatLabel);
            seatLabel.text = "我的座位";

            _seatButtons = new Button[SeatLayout.SeatCount];
            for (int i = 0; i < SeatLayout.SeatCount; i++)
            {
                int seat = i + 1;
                float x = -300f + i * 200f;

                Button b = MakeButton(seatRect, "Seat" + seat, new Vector2(x, -10f),
                                      new Vector2(170f, 52f), "座位 " + seat);
                b.onClick.AddListener(() => SelectSeat(seat));
                _seatButtons[i] = b;
            }

            // ---- 实时显示：队友与对手分别在哪一侧 ----
            _seatSummary = MakeText(_selectRoot, "TtSeatSummary", new Vector2(0f, 316f),
                                    new Vector2(1300f, 34f), 22, TextAnchor.MiddleCenter,
                                    new Color(0.90f, 0.93f, 1f, 0.92f));
            _screenTwoOnTwoCharacter.Add(_seatSummary.gameObject);

            // ---- 我的角色 ----
            _screenTwoOnTwoCharacter.Add(MakeSectionLabel("我的角色", new Vector2(0f, 272f)));
            _rosterView2Me = BuildRosterRow("Roster2Me", new Vector2(0f, 208f), initialIndex: _indexA);
            _rosterView2Me.SelectionChanged += index =>
            {
                _indexA = index;
                _rosterView2Me.ScrollToSelected();
                RefreshDetail(_detail2Me, _roster[_indexA]);
            };
            _screenTwoOnTwoCharacter.Add(_rosterView2Me.Root.gameObject);
            _detail2Me = MakeDetailPanel("Detail2Me", new Vector2(0f, 134f));
            _screenTwoOnTwoCharacter.Add(_detail2Me.transform.parent.gameObject);

            // ---- 队友角色 ----
            _screenTwoOnTwoCharacter.Add(MakeSectionLabel("队友角色（人机）", new Vector2(0f, 82f)));
            _rosterView2Mate = BuildRosterRow("Roster2Mate", new Vector2(0f, 18f),
                                              initialIndex: _indexMate);
            _rosterView2Mate.SelectionChanged += index =>
            {
                _indexMate = index;
                _rosterView2Mate.ScrollToSelected();
                RefreshDetail(_detail2Mate, _roster[_indexMate]);
            };
            _screenTwoOnTwoCharacter.Add(_rosterView2Mate.Root.gameObject);
            _detail2Mate = MakeDetailPanel("Detail2Mate", new Vector2(0f, -56f));
            _screenTwoOnTwoCharacter.Add(_detail2Mate.transform.parent.gameObject);

            // ---- 对手（随机，不可选）----
            _foeSummary = MakeText(_selectRoot, "TtFoes", new Vector2(0f, -116f),
                                   new Vector2(1300f, 34f), 22, TextAnchor.MiddleCenter,
                                   new Color(1f, 0.80f, 0.78f, 0.95f));
            _screenTwoOnTwoCharacter.Add(_foeSummary.gameObject);

            RefreshDetail(_detail2Me, _roster[Mathf.Clamp(_indexA, 0, _roster.Count - 1)]);
            RefreshDetail(_detail2Mate, _roster[Mathf.Clamp(_indexMate, 0, _roster.Count - 1)]);

            Button start = MakeButton(_selectRoot, "TtStart", new Vector2(0f, -196f),
                                      new Vector2(340f, 76f), "开始对局");
            start.onClick.AddListener(OnTwoOnTwoStartClicked);
            _screenTwoOnTwoCharacter.Add(start.gameObject);

            Button back = MakeButton(_selectRoot, "TtCharBack", new Vector2(-420f, -196f),
                                     new Vector2(220f, 76f), "返回");
            back.onClick.AddListener(() => ShowScreen(GameScreen.ModeMenu));
            _screenTwoOnTwoCharacter.Add(back.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenTwoOnTwoCharacter.Add(help.gameObject);

            SelectSeat(1);
        }

        /// <summary>选中座位并刷新"队友 / 对手在哪一侧"的说明与按钮高亮。</summary>
        private void SelectSeat(int seat)
        {
            _seat = SeatLayout.Normalize(seat);
            RefreshTwoOnTwoSummary();
        }

        /// <summary>
        /// 刷新 2v2 角色选择的说明文字：
        /// · 队友坐几号位、在我的**上家还是下家**（对应界面左下 / 右下）
        /// · 两名对手坐哪两个座位（对应界面左上 / 右上）
        ///
        /// 高亮当前座位按钮，让"选了哪个"一眼可见。
        /// </summary>
        private void RefreshTwoOnTwoSummary()
        {
            // 经典玩法：没有座位可选，座位稍后由先攻骰决定
            if (_seatPickerRoot != null)
                _seatPickerRoot.SetActive(!_classicSeatByInitiative);

            if (_classicSeatByInitiative)
            {
                if (_seatSummary != null)
                {
                    _seatSummary.text =
                        "经典玩法：开始后四人各投 1d20 决定座位 —— " +
                        "大数较大的那一队去 1 号位与 4 号位（大数 = 1 号、小数 = 4 号），" +
                        "另一队去 2 号位与 3 号位。我坐哪个座位由先攻结果决定。";
                }

                RefreshFoeSummary();
                return;
            }

            int me = _seat;
            int mate = SeatLayout.TeammateSeat(me);
            bool mateIsPrev = SeatLayout.TeammateIsPrevious(me);
            int[] foes = SeatLayout.OpponentSeats(me);

            if (_seatSummary != null)
            {
                _seatSummary.text =
                    $"我 = 座位 {me}（下方正中）　" +
                    $"队友 = 座位 {mate}（我的{(mateIsPrev ? "上家" : "下家")}" +
                    $" → 界面{(mateIsPrev ? "偏左下" : "偏右下")}）　" +
                    $"对手 = 座位 {foes[0]} 与 {foes[1]}（偏左上 / 偏右上）";
            }

            if (_foeSummary != null && _roster.Count > 0)
            {
                CharacterDefinition f1 = _roster[Mathf.Clamp(_indexFoe1, 0, _roster.Count - 1)];
                CharacterDefinition f2 = _roster[Mathf.Clamp(_indexFoe2, 0, _roster.Count - 1)];
                _foeSummary.text = $"对手（随机）：座位 {foes[0]} = {f1.DisplayName}　" +
                                   $"座位 {foes[1]} = {f2.DisplayName}";
            }

            // 高亮当前座位
            HighlightSeat(me);
        }

        /// <summary>经典玩法下的对手摘要（座位未知，所以只说角色）。</summary>
        private void RefreshFoeSummary()
        {
            if (_foeSummary == null || _roster.Count == 0) return;

            CharacterDefinition f1 = _roster[Mathf.Clamp(_indexFoe1, 0, _roster.Count - 1)];
            CharacterDefinition f2 = _roster[Mathf.Clamp(_indexFoe2, 0, _roster.Count - 1)];
            _foeSummary.text = $"对手（随机角色）：{f1.DisplayName}　{f2.DisplayName}";
        }

        /// <summary>高亮当前选中的座位按钮。</summary>
        private void HighlightSeat(int me)
        {
            if (_seatButtons != null)
            {
                for (int i = 0; i < _seatButtons.Length; i++)
                {
                    if (_seatButtons[i] == null) continue;

                    Image img = _seatButtons[i].GetComponent<Image>();
                    if (img == null) continue;

                    FrontEndArt.StyleButton(_seatButtons[i], "tab", i + 1 == me);
                }
            }
        }

        private Button MakeButton(RectTransform parent, string name, Vector2 position,
                                  Vector2 size, string label)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Text text = MakeText(rect, "Label", Vector2.zero, size, 28,
                                 TextAnchor.MiddleCenter, Color.white);
            text.text = label;
            FrontEndArt.StyleButton(go.GetComponent<Button>());

            return go.GetComponent<Button>();
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
