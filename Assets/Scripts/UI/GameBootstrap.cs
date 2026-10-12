using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
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
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Header("对局设置")]
        [Tooltip("玩家 A 使用的角色（默认白板）")]
        public SpecialLogicType PlayerACharacter = SpecialLogicType.Warrior;

        [Tooltip("玩家 B 使用的角色（默认白板）")]
        public SpecialLogicType PlayerBCharacter = SpecialLogicType.Curseweaver;

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
        public bool VerboseLogging;

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
        private ScreenGroup _screenMainMenu;
        private ScreenGroup _screenOneOnOne;

        /// <summary>2v2 子菜单（人机练习 / 真人对决）。</summary>
        private ScreenGroup _screenTwoOnTwo;

        /// <summary>本次对局是不是 2v2（决定棋盘与角色选择走哪条路）。</summary>
        private bool _isTwoOnTwo;

        /// <summary>2v2 角色选择界面。</summary>
        private ScreenGroup _screenTwoOnTwoCharacter;

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

        /// <summary>2v2 的第三 / 第四位抉择器（C / D 槽位）。</summary>
        private IChoiceProvider _choiceC;
        private IChoiceProvider _choiceD;

        /// <summary>2v2：我选的座位（1..4）。</summary>
        private int _seat = 1;

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
            BuildModeScreen();
            BuildDifficultyScreen();
            BuildCharacterScreen();
            BuildTwoOnTwoCharacterScreen();

            _board = gameObject.AddComponent<GameBoardUI>();
            _board.Build();
            _board.HandClicked += OnHandClicked;
            _board.RestartClicked += OnRestartClicked;
            _board.SkipTurnClicked += OnSkipTurnClicked;
            _board.DeclineClicked += OnDeclineClicked;
            _board.AcceptClicked += OnAcceptClicked;
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

            if (_roster.Count == 0) _roster.Add(CharacterDefinition.Blank);
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

            _selectRoot = canvasGo.GetComponent<RectTransform>();
            StretchFull(_selectRoot);

            // 背景（所有界面共用，不属于任何分组，因此始终显示）
            var bg = new GameObject("Bg", typeof(Image));
            bg.transform.SetParent(_selectRoot, false);
            StretchFull(bg.GetComponent<RectTransform>());
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.09f, 0.10f, 0.13f);
            bgImg.raycastTarget = false;

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
            help.GetComponent<Image>().color = new Color(0.22f, 0.26f, 0.34f);
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

        private void BuildMainMenuScreen()
        {
            _screenMainMenu = new ScreenGroup();

            // 标题：厚重金色立体字（纯代码绘制）
            var art = new TitleArt();
            RectTransform titleRoot = art.Build(_selectRoot, "MainTitle", "手指骰子对决",
                                                new Vector2(0f, 280f), 116, UiFont.Bold);
            _screenMainMenu.Add(titleRoot.gameObject);

            Text subtitle = TitleArt.BuildSubtitle(_selectRoot, "SubTitle",
                                                   "F I N G E R   D I C E   D U E L",
                                                   new Vector2(0f, 126f), 32, UiFont.Regular);
            _screenMainMenu.Add(subtitle.gameObject);

            var dividerRoot = new GameObject("Divider", typeof(RectTransform));
            dividerRoot.transform.SetParent(_selectRoot, false);
            StretchFull(dividerRoot.GetComponent<RectTransform>());
            TitleArt.BuildDivider(dividerRoot.GetComponent<RectTransform>(),
                                  new Vector2(0f, 48f), 1500f);
            _screenMainMenu.Add(dividerRoot);

            // 三个模式
            Button p1 = MakeModeButton(_selectRoot, "Mode1v1", new Vector2(0f, -42f),
                                       "1 v 1", "单挑对决", OnOneOnOneClicked);
            Button p2 = MakeModeButton(_selectRoot, "Mode2v2", new Vector2(0f, -198f),
                                       "2 v 2", "双人组队", OnTwoOnTwoClicked);
            Button p8 = MakeModeButton(_selectRoot, "Mode8p", new Vector2(0f, -354f),
                                       "8 人身份局", "身份阵营对抗",
                                       () => ShowComingSoon("8 人身份局"));

            _screenMainMenu.Add(p1.gameObject);
            _screenMainMenu.Add(p2.gameObject);
            _screenMainMenu.Add(p8.gameObject);

            Button help = MakeHelpButton(_selectRoot);
            _screenMainMenu.Add(help.gameObject);

            // 真正结束进程的入口放在主菜单 —— 对局中那个按钮只负责"返回标题"
            Button quit = MakeButton(_selectRoot, "Quit", new Vector2(0f, -516f),
                                     new Vector2(240f, 58f), "退出游戏");
            quit.GetComponent<Image>().color = new Color(0.34f, 0.24f, 0.26f);
            quit.onClick.AddListener(QuitGame);
            _screenMainMenu.Add(quit.gameObject);

            // 版本/提示
            Text tip = MakeText(_selectRoot, "Tip", new Vector2(0f, -456f), new Vector2(1200f, 30f),
                                20, TextAnchor.MiddleCenter, new Color(0.55f, 0.60f, 0.70f));
            tip.text = "点击右上角 ? 查看游戏规则";
            _screenMainMenu.Add(tip.gameObject);
        }

        /// <summary>模式按钮：大号标题 + 副标题，两行文字。</summary>
        private Button MakeModeButton(RectTransform parent, string name, Vector2 position,
                                      string title, string subtitle, Action onClick)
        {
            Button button = MakeButton(parent, name, position, new Vector2(620f, 116f), title);

            // MakeButton 已经建了一个居中大标签，再加一行副标题
            Text sub = MakeText(button.GetComponent<RectTransform>(), "Sub",
                                new Vector2(0f, -34f), new Vector2(560f, 28f),
                                20, TextAnchor.MiddleCenter, new Color(0.90f, 0.93f, 1f, 0.85f));
            sub.text = subtitle;

            // 主标题上移，给副标题让位
            var label = button.GetComponentInChildren<Text>();
            if (label != null) label.rectTransform.anchoredPosition = new Vector2(0f, 16f);

            button.onClick.AddListener(() => onClick?.Invoke());
            return button;
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

            Button back = MakeButton(_selectRoot, "TtBack", new Vector2(-420f, -256f),
                                     new Vector2(220f, 76f), "返回");
            back.GetComponent<Image>().color = new Color(0.30f, 0.33f, 0.40f);
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
                () => { Mode = PlayMode.Intro; ShowScreen(GameScreen.DifficultyMenu); });

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
                RollOpponents();
                RefreshTwoOnTwoSummary();
                ShowScreen(GameScreen.TwoOnTwoCharacter);
                return;
            }

            ShowScreen(GameScreen.CharacterSelect);
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

            // 建局逻辑本身已经可用（引擎侧完整支持 2v2），
            // 所以先把对局对象真的建出来 —— 这样"建局是否成功"当场可验证，
            // 而不是等到棋盘做完才发现参数不对。
            PrepareTwoOnTwoMatch();

            // ⚠️ 但四人环形棋盘的**表现层尚未完成**（当前棋盘是"左 A / 右 B"两人布局）。
            //
            // 与其把玩家带进一个只显示两个人、其余两人看不见的界面，
            // 不如在这里明确拦住 —— 那是"看起来能玩、实际坏了"的典型陷阱。
            // 棋盘做完后，把下面这行换成 StartPreparedMatch() 即可。
            ShowComingSoon("2v2 四人棋盘（对局已建好，界面开发中）");
        }

        private void OnTwoOnTwoPracticeClicked()
        {
            _isTwoOnTwo = true;
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

            _game = GameManager.Create2v2(seated, providers.ConvertAll(x => (IChoiceProvider)x));

            // 前两人的 AI 走构造参数（与 1v1 同一路径）；
            // 其余玩家用 BindPlayer 登记 —— 只有"我"是人类（ai 传 null）。
            _controller = new BoardController(
                _game,
                providers[0] as IPlayerChoiceSink, providers[1] as IPlayerChoiceSink,
                aiForA: (meSeat == 1) ? null : (IMoveSelector)MakeAI(),
                aiForB: (meSeat == 2) ? null : (IMoveSelector)MakeAI());

            for (int i = 0; i < 4; i++)
            {
                if (i == 0 || i == 1) continue;      // 前两人已在构造里传过

                IMoveSelector ai = (i == meSeat - 1) ? null : (IMoveSelector)MakeAI();
                _controller.BindPlayer(seated[i], ai, providers[i] as IPlayerChoiceSink);
            }

            _controller.AiResolver = new AiChoiceResolver(ProviderFor);

            // 第一回合从**我**开始（单机主视角先手，便于立即操作）
            _controller.SetFirstPlayer(seated[meSeat - 1]);

            _pendingDefA = setup.DefMe;
            _pendingDefB = setup.DefFoe1;
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

            // 保险：画布是三个界面共用的，切回来时必须确保它是激活的
            if (_selectRoot != null && !_selectRoot.gameObject.activeSelf)
                _selectRoot.gameObject.SetActive(true);

            if (_modal != null) _modal.Hide();

            // 对局界面由 GameBoardUI 自己管显隐
            bool inMatch = screen == GameScreen.InMatch;
            _board?.SetVisible(inMatch);

            _screenMainMenu?.SetVisible(screen == GameScreen.MainMenu);
            _screenOneOnOne?.SetVisible(screen == GameScreen.OneOnOneMenu);
            _screenTwoOnTwo?.SetVisible(screen == GameScreen.TwoOnTwoMenu);
            _screenMode?.SetVisible(screen == GameScreen.ModeMenu);
            _screenDifficulty?.SetVisible(screen == GameScreen.DifficultyMenu);
            _screenCharacter?.SetVisible(screen == GameScreen.CharacterSelect);
            _screenTwoOnTwoCharacter?.SetVisible(screen == GameScreen.TwoOnTwoCharacter);
            _diceRoll?.SetVisible(screen == GameScreen.DiceRoll);

            if (screen == GameScreen.MainMenu || screen == GameScreen.OneOnOneMenu
                || screen == GameScreen.TwoOnTwoMenu
                || screen == GameScreen.ModeMenu || screen == GameScreen.DifficultyMenu
                || screen == GameScreen.CharacterSelect
                || screen == GameScreen.TwoOnTwoCharacter || screen == GameScreen.DiceRoll)
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
            back.GetComponent<Image>().color = new Color(0.30f, 0.33f, 0.40f);
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
            view.Build(_selectRoot, name, position, _roster, UiFont.Regular, initialIndex);
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
            img.color = new Color(0.13f, 0.15f, 0.20f, 0.95f);
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

            target.text = $"【{def.DisplayName}】  {def.Description}";
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
        }

        /// <summary>掷骰分配完成后真正进入对局。</summary>
        private void OnDiceRollFinished()
        {
            if (_game == null || _controller == null) return;
            if (_pendingDefA == null || _pendingDefB == null) return;

            // 先手 = 点数**大**的那位（后选但先动，与"点数小先决定"互补）。
            // 必须在一局开始前设好，否则第一回合会按默认（A）走。
            _controller.SetFirstPlayer(_diceRoll.StartingPlayer);

            StartPreparedMatch();
        }

        /// <summary>
        /// 把已经 PrepareMatch 好的对局推到界面上并开始第一回合。
        /// </summary>
        private void StartPreparedMatch()
        {
            ShowScreen(GameScreen.InMatch);

            _board.BindPlayers(_game.PlayerA, _game.PlayerB);
            _board.SetTitle($"{_pendingDefA.DisplayName}  vs  {_pendingDefB.DisplayName}");
            _board.HideGameOver();
            _board.SetExitButtonVisible(true);
            _board.SetVisible(true);

            _controller.BeginTurn();
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
                    UseChainBreak = false,   // P1：AI 暂不发动连锁破盾
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
            _board.SetExitButtonVisible(false);
            _board.SetSkipButtonVisible(false);
            _board.SetAcceptButtonVisible(false);
            _board.SetDeclineButtonVisible(false);
            _board.SetVisible(false);

            _controller = null;      // 丢弃当前对局，避免 Update 里继续推进 AI
            _game = null;
            _aiDelayTimer = 0f;

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

        /// <summary>发动当前的"角色抉择"（剑客的追加攻击、游侠的强化等）。</summary>
        private void OnAcceptClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanChooser) return;

            if (_controller.AcceptPendingChoice()) RefreshView();
        }

        /// <summary>放弃当前的"角色抉择"（游侠的闪避 / 强化、野蛮人的锁手等）。</summary>
        private void OnDeclineClicked()
        {
            if (_controller == null) return;
            if (!_controller.IsHumanChooser) return;

            // 「放弃攻击」：已经选好参与运算的手，但没有合法攻击目标。
            // 放弃 = 算用掉本回合（换对方行动，手值不变）。
            if (_controller.CanGiveUpAttack)
            {
                if (_controller.GiveUpAttack()) RefreshView();
                return;
            }

            if (_controller.DeclinePendingChoice()) RefreshView();
        }

        private void OnHandClicked(PlayerState owner, Hand hand)
        {
            if (_controller == null) return;

            // 关键：用 IsHumanChooser 而不是 IsHumanTurn。
            // 进入"选盾"阶段时依然是攻击方的回合，但该做决定的是**防守方**；
            // 如果这里按"行动方是否人类"放行，那么"AI 攻击、人类防守"时
            // 玩家的选盾点击会被直接丢弃。
            if (!_controller.IsHumanChooser) return;

            if (_controller.ClickHand(hand))
            {
                if (VerboseLogging && _controller.LastTurn != null)
                    Debug.Log(BoardController.DescribeTurn(_controller.LastTurn));

                RefreshView();
            }
        }

        private void Update()
        {
            if (_controller == null) return;
            if (_controller.Phase == BoardPhase.GameOver) return;
            if (IsPausing) return;              // 回合间停顿：暂停 AI 推进

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

        private void RefreshView()
        {
            if (_controller == null || _board == null || _game == null) return;

            IReadOnlyList<Hand> choices =
                _controller.Phase == BoardPhase.ChoosingDefenseShield
                    ? (IReadOnlyList<Hand>)_controller.DefenseShieldChoices
                    : _controller.ChoiceCandidates;

            _board.Refresh(_game.PlayerA, _game.PlayerB,
                           _controller.SelectedHand,
                           choices,
                           showSkipButton: ShowSkipButton(),
                           skipButtonInteractable: _controller.CanSkipTurn,
                           turnCount: _controller.DisplayTurn,
                           turnLimit: _controller.TurnLimit,
                           extraHighlights: null);

            _board.SetInteractable((owner, hand) => _controller.CanAct(hand));

            _board.SetLog(_controller.Log);
            _board.SetHint(DescribeHint(), DescribeLastTurn());

            PlayTurnAnimations();

            // 回合切换过渡：点亮当前行动方的名字 + 一个提示音
            if (_game != null && _controller.Phase != BoardPhase.GameOver)
            {
                _board.ShowActivePlayer(_controller.ActivePlayer);
                _board.PlaySfx(SfxKind.TurnSwitch);
            }

            // 角色抉择的出口：
            //   · "发动"按钮 —— 只有一个候选时（剑客追加、游侠强化、牧师的攻击/回复）
            //   · "不发动 / 放弃攻击"按钮 —— 允许放弃时
            //
            // 「放弃攻击」是**新流程专有**的出口：玩家已选定参与运算的手，
            // 但当前**没有合法攻击目标**（刺客潜行，日后还有攻击距离不足等），
            // 此时"放弃攻击"是唯一出路。放弃 = **算用掉本回合**。
            bool giveUp = _controller.IsHumanChooser && _controller.CanGiveUpAttack;
            bool canDecline = giveUp
                              || (_controller.IsHumanChooser && _controller.CanDeclinePendingChoice);
            _board.SetDeclineButtonVisible(canDecline);
            if (canDecline) _board.SetDeclineButtonLabel(giveUp ? "放弃攻击" : DeclineLabel());

            string acceptLabel = _controller.IsHumanChooser ? _controller.PendingAcceptLabel : null;
            // 没有合法目标时不给"发动"（只让玩家放弃），避免打出一个无效的攻击
            bool canAccept = canDecline && !giveUp && !string.IsNullOrEmpty(acceptLabel);
            _board.SetAcceptButtonVisible(canAccept);
            if (canAccept) _board.SetAcceptButtonLabel(acceptLabel);

            if (_controller.Phase == BoardPhase.GameOver)
            {
                _board.ShowGameOver(WinMessage());
                _board.SetSkipButtonVisible(false);
            }

            DiagnoseIfNeeded(canDecline, acceptLabel, canAccept);
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
            sb.Append($" | 游侠手 L{_game.PlayerA?.LeftHand.Value} R{_game.PlayerA?.RightHand.Value}");
            sb.Append($" | 对手手 L{_game.PlayerB?.LeftHand.Value} R{_game.PlayerB?.RightHand.Value}");

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
        /// "跳过回合"按钮只在**人类玩家 A 回合、且 A 双盾在手、且还没开始选目标**时出现。
        /// 按钮挂在 A 的右手牌右侧，所以 B 的回合不应该显示它。
        /// </summary>
        private bool ShowSkipButton()
            => _controller != null
               && _controller.IsHumanTurn
               && ReferenceEquals(_controller.ActivePlayer, _game.PlayerA)
               && _controller.CanSkipTurn;

        private string DescribeHint()
        {
            string hint = _controller.Hint;

            if (_controller.Phase == BoardPhase.GameOver) return hint;

            // 兜底：任何情况下都不要让提示条空着，否则玩家不知道能做什么
            if (string.IsNullOrEmpty(hint))
                return _controller.IsHumanChooser
                    ? "请选择一张手牌。"
                    : "等待对手行动……";

            if (!_controller.IsHumanChooser)
                return $"{_controller.Chooser?.Name}（AI）正在思考……";

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

            // 受击方 = 行动方的对手
            PlayerState defender = ReferenceEquals(result.Actor, _game.PlayerA)
                ? _game.PlayerB
                : _game.PlayerA;

            AnimationPlan plan = AnimationDirector.Build(result, defender);
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
            ChoiceRequest? request = _controller.PendingRequest;
            if (request == null) return "不发动";

            switch (request.Value.Effect)
            {
                case HandEffect.RangerDodge: return "不闪避";
                case HandEffect.RangerPowerShot: return "不强化";
                case HandEffect.BarbarianLockTarget: return "不锁手";
                case HandEffect.WarriorChainBreak: return "不发动";
                case HandEffect.SwordsmanFollowUp: return "不追加";
                // 回复目标是**必选**的（玩家要求：选了回复就必须回复、
                // 运算出 9 必须回复、双 9 两段都必须回复），
                // 所以这里不会走到 —— 按钮根本不会出现。
                // 点自己的手就是回自己、点对方的手就是回对方。
                case HandEffect.PriestAttackOrHeal: return "攻击";
                default: return "不发动";
            }
        }

        /// <summary>
        /// 战斗说明：把最近一次结算的结果也显示在提示条里，
        /// 这样玩家不用去翻下方的战报就知道"刚才发生了什么"。
        /// </summary>
        private string DescribeLastTurn()
        {
            if (_controller?.LastTurn == null) return "";
            return BoardController.DescribeTurn(_controller.LastTurn);
        }

        private string WinMessage()
        {
            if (_game == null || !_game.IsGameOver) return "";
            if (_game.Winner == null) return "平局！";
            return $"{_game.Winner.Name} 获胜！";
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
            _screenTwoOnTwoCharacter.Add(MakeSectionLabel("我的座位", new Vector2(0f, 418f)));

            _seatButtons = new Button[SeatLayout.SeatCount];
            for (int i = 0; i < SeatLayout.SeatCount; i++)
            {
                int seat = i + 1;
                float x = -300f + i * 200f;

                Button b = MakeButton(_selectRoot, "Seat" + seat, new Vector2(x, 366f),
                                      new Vector2(170f, 52f), "座位 " + seat);
                b.onClick.AddListener(() => SelectSeat(seat));
                _seatButtons[i] = b;
                _screenTwoOnTwoCharacter.Add(b.gameObject);
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
            back.GetComponent<Image>().color = new Color(0.30f, 0.33f, 0.40f);
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
            if (_seatButtons != null)
            {
                for (int i = 0; i < _seatButtons.Length; i++)
                {
                    if (_seatButtons[i] == null) continue;

                    Image img = _seatButtons[i].GetComponent<Image>();
                    if (img == null) continue;

                    img.color = (i + 1 == me)
                        ? new Color(0.85f, 0.70f, 0.30f)
                        : new Color(0.30f, 0.33f, 0.40f);
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

            go.GetComponent<Image>().color = new Color(0.24f, 0.46f, 0.76f);

            Text text = MakeText(rect, "Label", Vector2.zero, size, 28,
                                 TextAnchor.MiddleCenter, Color.white);
            text.text = label;

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
