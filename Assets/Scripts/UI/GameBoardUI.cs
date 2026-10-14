using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 棋盘界面。**所有 UI 都在运行时用代码构建**，不依赖任何预制体，也不需要在编辑器里
    /// 手工摆放控件。这样整个界面都是可以 diff、可以 code review 的 C# 代码，
    /// 也避免了手工编辑场景 YAML 出错。
    ///
    /// 本类只负责"显示"与"把点击转发出去"，不含任何游戏规则 —— 规则在
    /// <see cref="Assistant.BoardController"/> 里，且那部分是无头可测的。
    ///
    /// 后续要换成美术预制体时，只需替换本类的构建部分，对外的事件与方法保持不变。
    /// </summary>
    public sealed class GameBoardUI : MonoBehaviour
    {
        // 参考分辨率
        private const float DesignWidth = 1920f;
        private const float DesignHeight = 1080f;

        private static readonly Color BgColor = new Color(0.09f, 0.10f, 0.13f);
        private static readonly Color PanelColor = new Color(0.14f, 0.15f, 0.19f);
        private static readonly Color TextColor = new Color(0.90f, 0.92f, 0.96f);
        private static readonly Color MutedText = new Color(0.70f, 0.74f, 0.82f);

        private Font _font;

        private RectTransform _canvasRoot;
        private Text _titleText;
        private Text _hintText;
        private Text _logText;
        private Text _statusTextA, _statusTextB;
        private Text _nameTextA, _nameTextB;
        private RectTransform _nameRowA, _nameRowB;
        private FloatingTextLayer _floats;
        private ScreenShaker _shaker;
        private GamePresentation _presentation;
        private SfxPlayer _sfx;
        private Image _hpBarFillA, _hpBarFillB;
        private RectTransform _areaA, _areaB;
        private HandCard[] _cardsA, _cardsB;
        private GameObject _gameOverPanel;
        private Text _gameOverText;
        private Button _skipButton;

        /// <summary>「开赌」按钮（赌徒专用）。</summary>
        private Button _gambleButton;

        /// <summary>盗贼的「偷窃」按钮（按需创建）。</summary>
        private Button _stealButton;
        private Button _exitButton;
        private Button _declineButton;
        private Button _acceptButton;
        private Text _turnText;

        /// <summary>玩家点击了一张手牌（手牌所属玩家、那只手）。</summary>
        public event Action<PlayerState, Hand> HandClicked;

        /// <summary>点击"跳过回合"。</summary>
        public event Action SkipTurnClicked;

        /// <summary>点了「开赌」（赌徒）。</summary>
        public event Action GambleClicked;

        /// <summary>点击"不发动 / 放弃"（角色抉择允许放弃时显示）。</summary>
        public event Action DeclineClicked;

        /// <summary>点击"发动"（角色抉择只有一个候选时显示，位于"不发动"左侧）。</summary>
        public event Action AcceptClicked;

        /// <summary>点击"退出游戏"。</summary>
        public event Action ExitClicked;

        /// <summary>点击"重新开始"。</summary>
        public event Action RestartClicked;

        private sealed class HandCard
        {
            public PlayerState Owner;
            public Hand Hand;
            public Button Button;
            public Image Background;
            public Image Outline;
            public Text NameText;
            public Text ValueText;
            public Text StatusText;

            /// <summary>受击闪烁用的白色覆盖层（默认全透明）。</summary>
            public Image Flash;
        }

        // ============================================================
        // 构建
        // ============================================================

        public void Build()
        {
            _font = LoadFont();

            var canvasGo = new GameObject("GameCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasRoot = canvasGo.GetComponent<RectTransform>();
            Stretch(_canvasRoot);

            CreateBackground();

            // 顶部：标题
            _titleText = CreateText(_canvasRoot, "Title", new Vector2(0, -30), new Vector2(1200, 50),
                                    36, TextAnchor.MiddleCenter, TextColor);
            UiFont.ApplyBold(_titleText);
            _titleText.text = "手指骰子对决";

            // 中部：提示条（说明"现在该做什么"）。
            //
            // 这是玩家唯一能知道"该我做什么"的地方，所以给它一个**有底色的面板**：
            // 纯文字浮在深色背景上很容易被忽略（曾经就被当成"没有提示"）。
            RectTransform hintPanel = CreatePanel(_canvasRoot, "HintPanel",
                                                  new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                                  new Vector2(1600f, 108f),
                                                  new Color(0.13f, 0.17f, 0.26f, 0.95f),
                                                  new Vector2(0f, 56f));

            _hintText = CreateText(hintPanel, "Hint", Vector2.zero, new Vector2(1560f, 96f),
                                   30, TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.45f));
            _hintText.text = "";
            UiFont.ApplyBold(_hintText);

            // 上方 = B（对手）；下方靠上 = A（当前人类玩家）；最下方 = 战斗文字
            BuildPlayerArea(anchorTop: true, out _areaB, out _cardsB,
                            out _nameTextB, out _statusTextB);
            BuildPlayerArea(anchorTop: false, out _areaA, out _cardsA,
                            out _nameTextA, out _statusTextA);

            BuildLogPanel();
            BuildGameOverPanel();
            BuildTopRightButtons();
            BuildTurnLabel();
            BuildFloatLayer();
            BuildRingPanels();

            HideGameOver();
            SetSkipButtonVisible(false);
            SetDeclineButtonVisible(false);
            SetAcceptButtonVisible(false);
        }

        private static Font LoadFont() => UiFont.Get();

        private void CreateBackground()
        {
            var go = new GameObject("Background", typeof(Image));
            go.transform.SetParent(_canvasRoot, false);
            Stretch(go.GetComponent<RectTransform>());

            var img = go.GetComponent<Image>();
            img.color = BgColor;
            img.raycastTarget = false;
        }

        // ============================================================
        // 2v2：四个环形位置的面板
        //
        // 布局由 <see cref="RingLayout"/> 推导（纯函数、已穷举测试）：
        //   · 自己 -> 下方正中
        //   · 两名对手 -> 偏左上 / 偏右上
        //   · 队友 -> 偏左下（他是上家时）或偏右下（他是下家时）
        //
        // 1v1 **完全不受影响**：这些面板默认隐藏，只有 2v2 才显示。
        // ============================================================

        /// <summary>一个环形位置的面板（名字 + 座位 + 血条 + 状态 + 两只手牌）。</summary>
        private sealed class RingPanel
        {
            public RingPosition Position;
            public PlayerState Owner;

            /// <summary>该位置与主视角的关系（我 / 队友 / 对手）。</summary>
            public RingRelation Relation;

            /// <summary>
            /// 该位置的**真实座位号**（1..4），由 <see cref="RingLayout.Build"/> 给出。
            ///
            /// ⚠️ 必须用这个，不能拿"玩家在列表里的下标 +1"当座位号 ——
            ///    我一度那样推算，结果与环形布局的推导不一致
            ///    （实机截图暴露：二三号位标反）。
            /// </summary>
            public int Seat;

            public RectTransform Root;
            public Text NameText;
            public Text SeatText;
            public Text StatusText;
            public Image HpFill;

            /// <summary>这个位置上的两只手牌（左 / 右）。</summary>
            public HandCard[] Cards;
        }

        private readonly List<RingPanel> _ringPanels = new List<RingPanel>(5);

        /// <summary>2v2 时"当前轮到行动"的玩家 —— 底部手牌行显示他的手牌。</summary>
        private PlayerState _ringActive;

        private void BuildRingPanels()
        {
            for (int i = 0; i < 5; i++)
            {
                var position = (RingPosition)i;

                RingLayout.NormalizedOffset(position, out float nx, out float ny);

                // 归一化坐标 -> 设计分辨率下的像素偏移
                var offset = new Vector2(nx * DesignWidth * 0.5f, ny * DesignHeight * 0.5f);

                // 尺寸取自已测试覆盖的规格（单一真相，避免界面与测试各写一个数）
                RingLayout.PanelSize(position, out float pw, out float ph);
                var size = new Vector2(pw, ph);

                RectTransform root = CreatePanel(_canvasRoot, "Ring" + position,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size, PanelColor, offset);

                // 名字 + 座位（玩家要求：每个人整体下方加位次）
                Text name = CreateText(root, "Name", new Vector2(0f, size.y * 0.5f - 30f),
                                       new Vector2(size.x - 20f, 36f), 26,
                                       TextAnchor.MiddleCenter, TextColor);
                UiFont.ApplyBold(name);

                Text seat = CreateText(root, "Seat", new Vector2(0f, size.y * 0.5f - 62f),
                                       new Vector2(size.x - 20f, 28f), 22,
                                       TextAnchor.MiddleCenter, new Color(1f, 0.88f, 0.45f));

                Image hp = BuildHealthBar(root, new Vector2(0f, size.y * 0.5f - 96f));

                Text status = CreateText(root, "Status", new Vector2(0f, size.y * 0.5f - 126f),
                                         new Vector2(size.x - 20f, 26f), 19,
                                         TextAnchor.MiddleCenter, new Color(0.60f, 0.80f, 1f));

                // 两只手牌：并排放在面板下方正中。
                // 260x130 的牌放这儿会超出面板宽度，所以用统一缩放压到 0.62。
                var cards = new HandCard[2];
                float cardScale = 0.62f;
                for (int k = 0; k < 2; k++)
                {
                    float cx = (k == 0 ? -1f : 1f) * 86f;
                    cards[k] = CreateHandCard(root, new Vector2(cx, -size.y * 0.5f + 50f));
                    cards[k].Button.GetComponent<RectTransform>().localScale =
                        new Vector3(cardScale, cardScale, 1f);
                }

                var panel = new RingPanel
                {
                    Position = position,
                    Root = root,
                    NameText = name,
                    SeatText = seat,
                    HpFill = hp,
                    StatusText = status,
                    Cards = cards,
                };

                _ringPanels.Add(panel);

                root.gameObject.SetActive(false);   // 1v1 不显示
            }
        }


        private void BuildPlayerArea(bool anchorTop, out RectTransform area,
                                     out HandCard[] cards, out Text nameText,
                                     out Text statusText)
        {
            float anchorY = anchorTop ? 1f : 0f;
            float offsetY = anchorTop ? -115f : 170f;

            area = CreatePanel(_canvasRoot, anchorTop ? "AreaB" : "AreaA",
                               new Vector2(0.5f, anchorY), new Vector2(0.5f, anchorY),
                               new Vector2(DesignWidth - 60f, 340f), PanelColor,
                               new Vector2(0f, offsetY));

            nameText = CreateText(area, "Name", new Vector2(0, anchorTop ? -18f : 18f),
                                  new Vector2(600, 36), 26, TextAnchor.MiddleCenter, TextColor);
            UiFont.ApplyBold(nameText);
            nameText.text = anchorTop ? "玩家 B" : "玩家 A";

            // 名字行是飘字与（未来的）血条的共同锚点
            if (anchorTop) _nameRowB = nameText.rectTransform;
            else _nameRowA = nameText.rectTransform;

            // 血条**居中**；血量数值挪到名字那一行（见 DescribeNameLine），
            // 这样条和文字不会互相压住。
            Image barFill = BuildHealthBar(area, new Vector2(0f, anchorTop ? -56f : 56f));
            if (anchorTop) _hpBarFillB = barFill;
            else _hpBarFillA = barFill;


            statusText = CreateText(area, "Status", new Vector2(0, anchorTop ? -94f : 94f),
                                    new Vector2(900, 30), 22, TextAnchor.MiddleCenter,
                                    new Color(0.60f, 0.80f, 1f));
            statusText.text = "";

            cards = new HandCard[2];
            for (int i = 0; i < 2; i++)
            {
                // 左手在左、右手在右
                var pos = new Vector2(i == 0 ? -160f : 160f, anchorTop ? -200f : 200f);
                cards[i] = CreateHandCard(area, pos);
            }
        }

        /// <summary>
        /// 创建一根血条，返回**填充图**（调用方保存它，之后只改它的宽度与颜色）。
        ///
        /// ⚠️ 填充必须用**点锚点**（而不是左右拉伸的锚点）：
        /// 拉伸锚点下 <c>sizeDelta.x</c> 表示"相对锚点跨度的偏移量"，
        /// 设成 0 时实际宽度仍等于锚点跨度 —— 那样血条**永远满格**，
        /// 血量归零也还剩一整条（真实 bug）。
        /// 点锚点下 <c>sizeDelta</c> 才是真实尺寸。
        /// </summary>
        private Image BuildHealthBar(RectTransform parent, Vector2 position)
        {
            RectTransform track = CreatePanel(parent, "HpBar",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(HealthBarWidth + 2f * BarPadding,
                            HealthBarHeight + 2f * BarPadding),
                new Color(0.10f, 0.11f, 0.15f, 0.95f), position);

            var fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(track, false);

            var fillRect = fillGo.GetComponent<RectTransform>();
            // 点锚点 + 左中对齐：改 sizeDelta.x 就是改填充宽度
            fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.anchoredPosition = new Vector2(BarPadding, 0f);
            fillRect.sizeDelta = new Vector2(HealthBarWidth, HealthBarHeight);

            var fill = fillGo.GetComponent<Image>();
            fill.color = HealthColor(1f);
            fill.raycastTarget = false;

            return fill;
        }

        /// <summary>血条尺寸。</summary>
        private const float HealthBarWidth = 220f;
        private const float HealthBarHeight = 22f;

        /// <summary>底槽相对填充的内边距。</summary>
        private const float BarPadding = 2f;

        /// <summary>
        /// 血量比例（0..1）。真正的算法在 <see cref="AnimationDirector.HealthRatio"/>
        /// （那边不依赖 UnityEngine，可被无头测试覆盖）。
        /// </summary>
        public static float HealthRatio(int health, int maxHealth)
            => AnimationDirector.HealthRatio(health, maxHealth);

        /// <summary>
        /// 血条颜色：绿（高）→ 黄（中）→ 红（低）。
        /// 两段线性插值，简单可预测。
        /// </summary>
        public static Color HealthColor(float ratio)
        {
            if (ratio < 0f) ratio = 0f;
            if (ratio > 1f) ratio = 1f;

            Color green = new Color(0.30f, 0.78f, 0.36f);
            Color yellow = new Color(0.90f, 0.76f, 0.26f);
            Color red = new Color(0.88f, 0.30f, 0.28f);

            const float low = 0.30f;    // 30% 及以下 = 红
            const float high = 0.60f;   // 60% 及以上 = 绿

            if (ratio >= high) return green;
            if (ratio <= low) return red;

            float t = (ratio - low) / (high - low);   // 0..1
            return t < 0.5f
                ? Color.Lerp(red, yellow, t * 2f)
                : Color.Lerp(yellow, green, (t - 0.5f) * 2f);
        }

        private HandCard CreateHandCard(RectTransform parent, Vector2 position)
        {
            var go = new GameObject("HandCard", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(260, 130);
            rect.anchoredPosition = position;

            var card = new HandCard();
            card.Button = go.GetComponent<Button>();
            card.Background = go.GetComponent<Image>();
            card.Background.color = PanelColor;

            // 描边（放在最底层当边框用）
            var outlineGo = new GameObject("Outline", typeof(Image));
            outlineGo.transform.SetParent(go.transform, false);
            Stretch(outlineGo.GetComponent<RectTransform>());
            outlineGo.transform.SetAsFirstSibling();
            card.Outline = outlineGo.GetComponent<Image>();
            card.Outline.color = HandVisual.OutlineColor(false, false);
            card.Outline.raycastTarget = false;

            var inner = new GameObject("Inner", typeof(Image));
            inner.transform.SetParent(go.transform, false);
            var innerRect = inner.GetComponent<RectTransform>();
            Stretch(innerRect);
            innerRect.offsetMin = new Vector2(3, 3);
            innerRect.offsetMax = new Vector2(-3, -3);
            var innerImg = inner.GetComponent<Image>();
            innerImg.color = PanelColor;
            innerImg.raycastTarget = false;

            card.NameText = CreateText(innerRect, "Side", new Vector2(-70, 0), new Vector2(90, 30),
                                       20, TextAnchor.MiddleCenter, MutedText);
            card.ValueText = CreateText(innerRect, "Value", new Vector2(30, 0), new Vector2(120, 80),
                                        54, TextAnchor.MiddleCenter, Color.white);
            UiFont.ApplyBold(card.ValueText);
            card.StatusText = CreateText(innerRect, "Status", new Vector2(15, -44), new Vector2(220, 26),
                                         18, TextAnchor.MiddleCenter, new Color(0.85f, 0.90f, 1f));

            // 受击闪烁用的白色覆盖层（默认全透明，打中时短暂亮起）
            var flashGo = new GameObject("Flash", typeof(Image));
            flashGo.transform.SetParent(innerRect, false);
            var flashRect = flashGo.GetComponent<RectTransform>();
            Stretch(flashRect);
            var flashImg = flashGo.GetComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;
            card.Flash = flashImg;

            card.Background = innerImg;   // 用内层做底色，外层留给描边

            var captured = card;
            card.Button.onClick.AddListener(() =>
            {
                if (captured.Hand != null && HandClicked != null)
                    HandClicked(captured.Owner, captured.Hand);
            });

            return card;
        }

        private void BuildLogPanel()
        {
            // 战斗文字放在屏幕最下方，和 A 区（玩家自己的手牌）分开，互不遮挡。
            RectTransform panel = CreatePanel(_canvasRoot, "LogPanel",
                                              new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                                              new Vector2(1520, 130), PanelColor,
                                              new Vector2(0f, 78f));

            _logText = CreateText(panel, "LogText", Vector2.zero, new Vector2(1480, 104),
                                  20, TextAnchor.UpperLeft, new Color(0.84f, 0.87f, 0.93f));
            _logText.text = "";
        }

        private void BuildGameOverPanel()
        {
            _gameOverPanel = CreatePanel(_canvasRoot, "GameOver",
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         new Vector2(680, 300),
                                         new Color(0.10f, 0.11f, 0.15f, 0.98f),
                                         new Vector2(0f, 62f)).gameObject;

            _gameOverText = CreateText(_gameOverPanel.GetComponent<RectTransform>(), "Text",
                                       new Vector2(0, 46), new Vector2(620, 90), 42,
                                       TextAnchor.MiddleCenter, Color.white);
            UiFont.ApplyBold(_gameOverText);

            Button restart = CreateButton(_gameOverPanel.GetComponent<RectTransform>(), "Restart",
                                          new Vector2(0, -74), new Vector2(300, 68), "再来一局");
            restart.onClick.AddListener(() => { if (RestartClicked != null) RestartClicked(); });
        }

        // ============================================================
        // 对外接口
        // ============================================================

        /// <summary>
        /// 把两位玩家绑到上下两组手牌上（1v1 用）。
        /// </summary>
        public void BindPlayers(PlayerState a, PlayerState b)
        {
            _nameTextA.text = a.Name;
            _nameTextB.text = b.Name;

            BindSide(_cardsA, a);
            BindSide(_cardsB, b);

            // 名字行也登记给表现层，用于"轮到谁"的高亮过渡
            if (_presentation != null)
            {
                _presentation.RegisterNameRow(a, _nameTextA);
                _presentation.RegisterNameRow(b, _nameTextB);
            }
        }

        /// <summary>
        /// **2v2**：把四个人绑到环形位置的面板上。
        ///
        /// 布局由 <see cref="RingLayout"/> 推导（纯函数、已穷举测试）：
        ///   · 自己 -> 下方正中
        ///   · 两名对手 -> 偏左上 / 偏右上
        ///   · 队友 -> 偏左下（他是上家时）或偏右下（他是下家时）
        ///
        /// 同时把 1v1 的两块大面板**隐藏**，避免两套布局叠在一起。
        ///
        /// 手牌仍复用上下两组：**自己在下方**（可点），
        /// **当前行动方在上方**（看 AI 出牌）。这样不必新增八张牌。
        /// </summary>
        public void BindPlayersRing(PlayerState me, IReadOnlyList<PlayerState> playersBySeat,
                                    int mySeat)
        {
            if (me == null || playersBySeat == null) return;

            // 隐藏 1v1 的两人布局
            if (_areaA != null) _areaA.gameObject.SetActive(false);
            if (_areaB != null) _areaB.gameObject.SetActive(false);

            IReadOnlyList<RingSlot> slots = RingLayout.Build(mySeat);

            for (int i = 0; i < _ringPanels.Count; i++)
            {
                RingPanel panel = _ringPanels[i];
                panel.Owner = null;
                panel.Relation = RingRelation.Self;
                panel.Root.gameObject.SetActive(false);
            }

            for (int i = 0; i < slots.Count; i++)
            {
                RingSlot slot = slots[i];
                if (slot.Seat < 1 || slot.Seat > playersBySeat.Count) continue;

                PlayerState owner = playersBySeat[slot.Seat - 1];

                RingPanel panel = FindPanel(slot.Position);
                if (panel == null) continue;

                panel.Owner = owner;
                panel.Relation = slot.Relation;
                panel.Seat = slot.Seat;
                panel.Root.gameObject.SetActive(true);

                // 这个位置的**两只手**绑到自己的牌上 ——
                // 四个人都能看见手值（这是 2v2 必须的：要判断能出什么）。
                BindSide(panel.Cards, owner);

                // 受击闪烁也要登记，否则 2v2 里打人不会有反馈
                RegisterFlashTargets(owner, panel.Cards);
            }

            // 1v1 的两块大面板**整体隐藏**（它们的名字/血条/状态与环形面板重复）。
            // ⚠️ 但**不能隐藏它们的子物体所在的根** —— 曾经在这里
            //    SetActive(false) 了整个 area，把 A 的手牌一起藏掉了
            //    （手牌是 area 的子物体），表现为"四个人的手都不显示"。
            //    现在手牌只在环形面板上，所以隐藏 area 是安全的；
            //    为了不再踩这个坑，这里只隐藏而不依赖它的子物体。
            if (_areaA != null) _areaA.gameObject.SetActive(false);
            if (_areaB != null) _areaB.gameObject.SetActive(false);
        }

        private RingPanel FindPanel(RingPosition position)
        {
            for (int i = 0; i < _ringPanels.Count; i++)
                if (_ringPanels[i].Position == position) return _ringPanels[i];

            return null;
        }

        /// <summary>
        /// **2v2**：指定"当前回合的行动方"（用于高亮与飘字锚点）。
        ///
        /// 手牌现在挂在四个环形面板上，不再依赖上下两组大面板，
        /// 所以这里只登记表现层的锚点。
        /// </summary>
        public void SetRingActive(PlayerState active, PlayerState me)
        {
            if (active == null) return;

            _ringActive = active;

            if (_presentation == null) return;

            if (me != null) _presentation.RegisterNameRow(me, _nameTextA);

            // 用他自己的面板名字行作为飘字锚点
            RingPanel panel = FindPanelOf(active);
            if (panel != null) _presentation.RegisterNameRow(active, panel.NameText);
        }

        private RingPanel FindPanelOf(PlayerState owner)
        {
            if (owner == null) return null;

            for (int i = 0; i < _ringPanels.Count; i++)
                if (ReferenceEquals(_ringPanels[i].Owner, owner)) return _ringPanels[i];

            return null;
        }

        /// <summary>
        /// **2v2**：刷新四个位置的名字、座位、血量、状态与两只手牌。
        ///
        /// ⚠️ 回合数（<see cref="SetTurn"/>）必须在这里也刷 ——
        ///    2v2 走的是这条路径，**不经过** <see cref="Refresh"/>，
        ///    所以只在那里面调 SetTurn 的话 2v2 永远不显示回合数
        ///    （实机现象："回合依旧没有显示"）。
        /// </summary>
        public void RefreshRing(IReadOnlyList<PlayerState> playersBySeat,
                                Hand selectedHand = null,
                                IReadOnlyList<Hand> highlightAsChoices = null,
                                int turnCount = 0, int turnLimit = 0,
                                bool showSkipButton = false,
                                bool skipButtonInteractable = false)
        {
            if (playersBySeat == null) return;

            if (turnLimit > 0) SetTurn(turnCount, turnLimit);

            // ⚠️ 2v2 必须先**确保按钮被创建**，否则 SetSkipButtonVisible
            //    只是对 null 做判断、什么都不发生。
            //
            //    根因：按钮原本只在 1v1 的 Refresh 路径里由
            //    EnsureSkipButton(handCardRect) 创建（锚点是 A 的右手牌），
            //    而 2v2 走的是本方法 —— 于是 _skipButton 一直是 null，
            //    表现成"判定为 true（日志里没有'不显示'那行）但界面上没有按钮"。
            EnsureSkipButtonForRing(playersBySeat);

            SetSkipButtonVisible(showSkipButton);
            SetSkipButtonInteractable(skipButtonInteractable);

            for (int i = 0; i < _ringPanels.Count; i++)
            {
                RingPanel panel = _ringPanels[i];
                if (panel.Owner == null) continue;

                panel.NameText.text = DescribeNameLine(panel.Owner);
                UpdateHealthBar(panel.HpFill, panel.Owner);
                panel.StatusText.text = HandVisual.ShieldSummary(panel.Owner);

                // 座位号直接用 RingLayout 给的真实座位，**不再二次推算**
                panel.SeatText.text = SeatLabel(panel);

                RefreshSide(panel.Cards, panel.Owner, selectedHand,
                            highlightAsChoices, null);
            }
        }

        /// <summary>
        /// 座位显示文案：**位次 + 关系**（我 / 队友 / 对手）。
        ///
        /// 只写"一号位"不够用 —— 2v2 里玩家最需要一眼看出谁是自己人。
        /// </summary>
        private static string SeatLabel(RingPanel panel)
        {
            if (panel == null || panel.Owner == null) return "";

            string relation = panel.Relation == RingRelation.Self ? "（我）"
                : panel.Relation == RingRelation.Teammate ? "（队友）"
                : "（对手）";

            return $"{SeatName(panel.Seat)}位{relation}";
        }

        /// <summary>座位号 -> 中文位次（一号位..八号位）。</summary>
        private static string SeatName(int seat)
        {
            string[] names = { "零", "一", "二", "三", "四", "五", "六", "七", "八" };
            if (seat < 1 || seat >= names.Length) return seat.ToString();

            return names[seat];
        }

        /// <summary>
        /// 回合切换过渡：点亮当前行动方的名字，其他人恢复常态。
        /// </summary>
        public void ShowActivePlayer(PlayerState active)
        {
            if (_presentation != null) _presentation.ShowActivePlayer(active);
        }

        private void BindSide(HandCard[] cards, PlayerState owner)
        {
            cards[0].Owner = owner;
            cards[0].Hand = owner.LeftHand;
            cards[1].Owner = owner;
            cards[1].Hand = owner.RightHand;

            cards[0].NameText.text = "左手";
            cards[1].NameText.text = "右手";

            RegisterFlashTargets(owner, cards);
        }

        /// <summary>把某位玩家的手牌登记为"可被受击闪烁"的目标。</summary>
        private void RegisterFlashTargets(PlayerState owner, HandCard[] cards)
        {
            if (_presentation == null || owner == null || cards == null) return;

            var flashes = new List<Image>(cards.Length);
            foreach (HandCard card in cards)
                if (card.Flash != null) flashes.Add(card.Flash);

            _presentation.RegisterFlashTargets(owner, flashes);
        }

        public void SetTitle(string text) { if (_titleText != null) _titleText.text = text ?? ""; }

        /// <summary>
        /// 设置提示条内容：第一行是"现在该做什么"，第二行是"刚才发生了什么"。
        /// </summary>
        public void SetHint(string action, string lastResult)
        {
            if (_hintText == null) return;

            string a = action ?? "";
            string b = lastResult ?? "";

            _hintText.text = string.IsNullOrEmpty(b) ? a : a + "\n" + b;
        }

        /// <summary>让手牌所在的容器不可点（例如点 B 区域时 B 自己不该响应）。</summary>
        private void BuildTopRightButtons()
        {
            // 右上角：返回标题。
            // 按钮文案与行为必须一致 —— 原来写"退出游戏"，实际只是回标题，
            // 玩家会以为点了会结束进程（实测反馈："按退出游戏直接关闭游戏了"）。
            _exitButton = CreateButton(_canvasRoot, "ExitButton",
                                       new Vector2(-100f, -40f), new Vector2(190f, 56f), "返回标题",
                                       anchorTopRight: true);
            _exitButton.GetComponent<Image>().color = new Color(0.55f, 0.28f, 0.28f);
            _exitButton.onClick.AddListener(() => { ExitClicked?.Invoke(); });
        }

        /// <summary>
        /// **左上角**：回合数。
        ///
        /// 玩家先要求放右上，实机后要求**改回左上（与 1v1 一致）** ——
        /// 右上角与"返回标题"按钮抢位置，而且 1v1 与 2v2 不一致会让玩家找不到。
        ///
        /// 显示的回合号**从 1 开始**
        /// （玩家操作的第一个回合就是第 1 回合，见 `BoardController.DisplayTurn`）。
        /// </summary>
        private void BuildTurnLabel()
        {
            _turnText = CreateText(_canvasRoot, "TurnCount",
                                   new Vector2(110f, 40f), new Vector2(240f, 48f),
                                   26, TextAnchor.MiddleLeft, TextColor,
                                   anchorTopLeft: true);
            UiFont.ApplyBold(_turnText);
            _turnText.text = "";
        }

        /// <summary>
        /// 创建飘字层。放在最上层（最后创建 = 渲染在最前），避免被面板盖住。
        /// </summary>
        private void BuildFloatLayer()
        {
            var go = new GameObject("FloatLayer", typeof(RectTransform));
            go.transform.SetParent(_canvasRoot, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _floats = go.AddComponent<FloatingTextLayer>();

            // 震动挂在 Canvas 根上（偏移画布本身），与布局无关
            _shaker = _canvasRoot.gameObject.AddComponent<ScreenShaker>();
            _shaker.Bind(_canvasRoot);

            _presentation = _canvasRoot.gameObject.AddComponent<GamePresentation>();

            // 音效：程序化合成，不需要任何音频资源文件
            _sfx = _canvasRoot.gameObject.AddComponent<SfxPlayer>();
            _presentation.Initialize(_floats, _shaker, _sfx);
        }

        /// <summary>
        /// 播放一次结算的表现（目前只有飘字）。
        /// 由 <see cref="GameBootstrap"/> 在每次结算后调用。
        /// </summary>
        public void PlayAnimations(AnimationPlan plan, PlayerState playerA, PlayerState playerB)
        {
            if (plan == null || plan.IsEmpty) return;

            if (_presentation != null) _presentation.Play(plan, playerA, playerB);
        }

        /// <summary>停止所有表现并还原（退出对局 / 重开时调用）。</summary>
        public void StopAnimations()
        {
            if (_presentation != null) _presentation.StopAll();
            if (_sfx != null) _sfx.StopAll();      // 清掉待播音效，避免退出后还在响
        }

        /// <summary>音效总开关。</summary>
        public bool SoundEnabled
        {
            get => _presentation == null || _presentation.SoundEnabled;
            set { if (_presentation != null) _presentation.SoundEnabled = value; }
        }

        /// <summary>播放一个独立音效（例如回合切换）。</summary>
        public void PlaySfx(SfxKind kind)
        {
            if (_presentation != null) _presentation.PlaySfx(kind);
        }

        /// <summary>
        /// 表现层总开关（关掉后不播打击感，便于对比手感）。
        /// </summary>
        public bool ImpactsEnabled
        {
            get => _presentation == null || _presentation.EnableImpacts;
            set { if (_presentation != null) _presentation.EnableImpacts = value; }
        }

        /// <summary>
        /// 某位玩家"名字那一行"的画布本地坐标。
        /// 找不到时退回画布中心，至少不会飘到屏幕外。
        /// </summary>
        public Vector2 FloatAnchorOf(PlayerState player, PlayerState playerA, PlayerState playerB)
        {
            RectTransform row = null;
            if (player != null && ReferenceEquals(player, playerA)) row = _nameRowA;
            else if (player != null && ReferenceEquals(player, playerB)) row = _nameRowB;

            if (row == null || _canvasRoot == null) return Vector2.zero;

            // 名字行的左下角相对画布左下角 = 画布本地坐标
            Vector3 local = _canvasRoot.InverseTransformPoint(row.position);

            return AnchoredFromCanvasLocal(_canvasRoot.rect,
                new Vector2(local.x, local.y));
        }

        /// <summary>
        /// 把"画布本地坐标"（左下角为原点、向上为正）换算成
        /// "以画布中心为原点的 anchoredPosition"。
        ///
        /// 抽成纯函数是为了**可测**：飘字落点对不对不需要看画面就能验证。
        /// </summary>
        public static Vector2 AnchoredFromCanvasLocal(Rect canvasRect, Vector2 canvasLocal)
        {
            // 真正的换算是 AnimationDirector 里的纯浮点版本（那边可以被无头测试覆盖），
            // 这里只负责把 UnityEngine 类型拆开、再拼回去。
            AnimationDirector.CanvasLocalToAnchored(
                canvasRect.width, canvasRect.height,
                canvasLocal.x - canvasRect.xMin, canvasLocal.y - canvasRect.yMin,
                out float x, out float y);

            return new Vector2(x, y);
        }

        /// <summary>更新回合数显示（形如 "14 / 50"）。</summary>
        public void SetTurn(int turnCount, int turnLimit)
        {
            if (_turnText == null) return;
            _turnText.text = $"回合 {turnCount} / {turnLimit}";
        }

        /// <summary>
        /// 2v2 专用的"确保跳过按钮存在"。
        ///
        /// ⚠️ **锚点必须是最右那张手牌**，不能取"第一张"。
        ///    我第一版取 `Cards[0]`（左边那张），按钮就压在**右边那张手牌上**
        ///    （玩家实测截图确认）。
        ///
        /// 优先在"我"那一格（SelfBottom）里找最右的手牌，
        /// 与 1v1 的手感一致：按钮出现在自己手牌的右侧。
        /// </summary>
        private void EnsureSkipButtonForRing(IReadOnlyList<PlayerState> playersBySeat)
        {
            if (_skipButton != null) return;

            HandCard anchor = FindRightmostRingCard();
            if (anchor == null) return;

            EnsureSkipButton(anchor.Button.GetComponent<RectTransform>());
        }

        /// <summary>
        /// 找环形布局里**屏幕最右侧**的那张手牌作为按钮锚点。
        ///
        /// 遍历所有可见面板的所有手牌，取 `anchoredPosition.x` 最大的一张 ——
        /// 这样不依赖"我"在哪一格，按钮永远在整块手牌区的右边，
        /// 任何座位（1~4 号位）都会落在安全区域。
        /// </summary>
        private HandCard FindRightmostRingCard()
        {
            HandCard best = null;
            float bestX = float.NegativeInfinity;

            for (int i = 0; i < _ringPanels.Count; i++)
            {
                RingPanel panel = _ringPanels[i];
                if (panel.Root == null || !panel.Root.gameObject.activeInHierarchy) continue;
                if (panel.Cards == null) continue;

                for (int c = 0; c < panel.Cards.Length; c++)
                {
                    HandCard card = panel.Cards[c];
                    if (card == null || card.Button == null) continue;

                    var rect = card.Button.GetComponent<RectTransform>();
                    if (rect == null) continue;

                    // 优先"我"那一格；同格内取最右
                    bool mine = panel.Position == RingPosition.SelfBottom;
                    float x = rect.anchoredPosition.x + (mine ? 10000f : 0f);

                    if (x > bestX) { bestX = x; best = card; }
                }
            }

            return best;
        }

        /// <summary>
        /// 创建"跳过回合"按钮，并把它放在**手牌区右侧**。
        ///
        /// ⚠️ 两条位置约定（都是玩家实测反馈后改的）：
        ///
        /// 1. **父节点用 `_canvasRoot`**，与"发动 / 不发动"同一坐标系。
        ///    原来挂在手牌容器下（`handCardRect.parent`），坐标系不同，
        ///    同一个数值在 1v1 与 2v2 里落点完全不同。
        ///
        /// 2. **按世界坐标算真实屏幕位置**，不猜偏移量。
        ///    原来用 `handCardRect.anchoredPosition + 260` ——
        ///    那是"锚点在手牌容器里的坐标"，遇上环形布局就压在手牌上了
        ///    （玩家截图确认：按钮盖住右边那张手牌）。
        /// </summary>
        private void EnsureSkipButton(RectTransform handCardRect)
        {
            if (_skipButton != null) return;

            _skipButton = CreateButton(_canvasRoot, "SkipTurnButton",
                                       Vector2.zero, new Vector2(180f, 56f), "跳过回合");
            _skipButton.GetComponent<Image>().color = new Color(0.28f, 0.42f, 0.30f);
            _skipButton.onClick.AddListener(() => { SkipTurnClicked?.Invoke(); });

            PlaceButtonRightOf(handCardRect, _skipButton, 60f);
        }

        /// <summary>
        /// 把一个按钮放到**某只手牌的右侧**（世界坐标换算，父节点须为 canvasRoot）。
        ///
        /// 这样无论手牌在 1v1 的居中布局、还是 2v2 的环形布局里，
        /// 按钮都贴在手牌右边，不会压住它。
        /// </summary>
        private void PlaceButtonRightOf(RectTransform anchorCard, Button button, float gap)
        {
            if (anchorCard == null || button == null || _canvasRoot == null) return;

            var target = button.GetComponent<RectTransform>();
            if (target == null) return;

            // 手牌右边缘的世界坐标 -> canvasRoot 的局部坐标
            var corners = new Vector3[4];
            anchorCard.GetWorldCorners(corners);       // 0=左下 1=左上 2=右上 3=右下
            float rightWorldX = corners[2].x;
            float centerWorldY = (corners[0].y + corners[1].y) * 0.5f;

            var world = new Vector3(rightWorldX + gap, centerWorldY, 0f);
            Vector3 local = _canvasRoot.InverseTransformPoint(world);

            target.anchorMin = target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0f, 0.5f);      // 左边缘对齐，向右伸展
            target.anchoredPosition = new Vector2(local.x, local.y);
        }

        /// <summary>显示/隐藏「开赌」按钮（赌徒专用）。</summary>
        public void SetGambleButtonVisible(bool visible)
        {
            if (!visible)
            {
                if (_gambleButton != null) _gambleButton.gameObject.SetActive(false);
                return;
            }

            EnsureGambleButton();
            if (_gambleButton != null) _gambleButton.gameObject.SetActive(true);
        }

        /// <summary>
        /// 创建「开赌」按钮，放在**手牌区右侧**（与跳过按钮同一套定位规则）。
        ///
        /// 位置由 <see cref="PlaceButtonRightOf"/> 按世界坐标算出，
        /// 所以 1v1 与 2v2 都不会压住手牌。
        /// </summary>
        private void EnsureGambleButton()
        {
            if (_gambleButton != null) return;

            HandCard anchor = FindRightmostRingCard() ?? RightmostSideCard();
            if (anchor == null) return;

            var rect = anchor.Button.GetComponent<RectTransform>();
            if (rect == null) return;

            _gambleButton = CreateButton(_canvasRoot, "GambleButton",
                                         Vector2.zero, new Vector2(160f, 56f), "开赌");
            _gambleButton.GetComponent<Image>().color = new Color(0.62f, 0.42f, 0.16f);
            _gambleButton.onClick.AddListener(() => { GambleClicked?.Invoke(); });

            PlaceButtonRightOf(rect, _gambleButton, 60f);
        }

        /// <summary>1v1 布局里的最右手牌（赌徒按钮的兜底锚点）。</summary>
        private HandCard RightmostSideCard()
        {
            if (_cardsA == null || _cardsA.Length == 0) return null;
            return _cardsA[_cardsA.Length - 1];
        }

        // ============================================================
        // 盗贼：「偷窃」按钮
        // ============================================================

        /// <summary>
        /// 显示 / 隐藏「偷窃」按钮。
        ///
        /// 玩家给的交互：点自己的手 -> 点「偷窃」-> 点别人的手。
        /// 所以按钮只在"盗贼点了自己一只手、且本回合没偷过"时出现。
        /// </summary>
        public void SetStealButtonVisible(bool visible)
        {
            if (!visible)
            {
                if (_stealButton != null) _stealButton.gameObject.SetActive(false);
                return;
            }

            EnsureStealButton();
            if (_stealButton != null) _stealButton.gameObject.SetActive(true);
        }

        /// <summary>「偷窃」按钮被点击。</summary>
        public event Action StealClicked;

        /// <summary>
        /// 创建「偷窃」按钮，放在**手牌区右侧**（与开赌 / 跳过同一套定位规则）。
        ///
        /// 位置由 <see cref="PlaceButtonRightOf"/> 按世界坐标算出，
        /// 所以 1v1 与 2v2 都不会压住手牌。
        /// </summary>
        private void EnsureStealButton()
        {
            if (_stealButton != null) return;

            HandCard anchor = FindRightmostRingCard() ?? RightmostSideCard();
            if (anchor == null) return;

            var rect = anchor.Button.GetComponent<RectTransform>();
            if (rect == null) return;

            _stealButton = CreateButton(_canvasRoot, "StealButton",
                                        Vector2.zero, new Vector2(160f, 56f), "偷窃");
            // 深紫色 —— 与开赌（棕）、跳过（灰）区分开
            _stealButton.GetComponent<Image>().color = new Color(0.42f, 0.20f, 0.52f);
            _stealButton.onClick.AddListener(() => { StealClicked?.Invoke(); });

            PlaceButtonRightOf(rect, _stealButton, 60f);
        }

        /// <summary>显示/隐藏"跳过回合"按钮。</summary>
        public void SetSkipButtonVisible(bool visible)
        {
            if (_skipButton != null) _skipButton.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 把"发动 / 不发动"两个按钮放到**本体手牌的右侧**。
        ///
        /// 1v1 里它们固定在屏幕右侧（`ChoiceButtonX = 700`，相对画布中心），
        /// 那在单人手牌布局下刚好合适；但 2v2 的环形布局里，
        /// 同一个数值会落到**左下角的战报上面**（玩家实测截图确认）。
        ///
        /// 所以 2v2 由调用方在刷新时调用本方法，按**当前做抉择那位的**
        /// 手牌重新定位；1v1 不调用它，保持原样（已验证过的布局不动）。
        /// </summary>
        public void PlaceChoiceButtonsNextTo(PlayerState decider)
        {
            if (decider == null) return;
            if (_acceptButton == null && _declineButton == null) return;

            HandCard anchor = FindRightmostCardOf(decider);
            if (anchor == null) return;

            var rect = anchor.Button.GetComponent<RectTransform>();
            if (rect == null) return;

            // 两个按钮上下叠放：发动在上、不发动在下
            if (_acceptButton != null) PlaceButtonRightOf(rect, _acceptButton, 40f);
            if (_declineButton != null)
            {
                PlaceButtonRightOf(rect, _declineButton, 40f);

                // 往下挪一行，避免与"发动"重叠
                var r = _declineButton.GetComponent<RectTransform>();
                if (r != null)
                    r.anchoredPosition = r.anchoredPosition + new Vector2(0f, -46f);
            }
        }

        /// <summary>找某位玩家在环形布局里的**最右**那张手牌。</summary>
        private HandCard FindRightmostCardOf(PlayerState player)
        {
            HandCard best = null;
            float bestX = float.NegativeInfinity;

            for (int i = 0; i < _ringPanels.Count; i++)
            {
                RingPanel panel = _ringPanels[i];
                if (panel.Root == null || !panel.Root.gameObject.activeInHierarchy) continue;
                if (!ReferenceEquals(panel.Owner, player)) continue;
                if (panel.Cards == null) continue;

                for (int c = 0; c < panel.Cards.Length; c++)
                {
                    HandCard card = panel.Cards[c];
                    if (card == null || card.Button == null) continue;

                    var rect = card.Button.GetComponent<RectTransform>();
                    if (rect == null) continue;

                    if (rect.anchoredPosition.x > bestX)
                    {
                        bestX = rect.anchoredPosition.x;
                        best = card;
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// 显示/隐藏"不发动"按钮。
        ///
        /// 角色的抉择可能允许放弃（游侠的闪避、强化射击、野蛮人的锁手），
        /// 这时必须给玩家一个明确的"放弃"出口，否则他会以为必须选一张手牌。
        /// </summary>
        public void SetDeclineButtonVisible(bool visible)
        {
            EnsureDeclineButton();
            if (_declineButton != null) _declineButton.gameObject.SetActive(visible);
        }

        /// <summary>放弃按钮的文案（不同抉择说法不同）。</summary>
        public void SetDeclineButtonLabel(string label)
        {
            EnsureDeclineButton();
            if (_declineButton == null) return;

            Text text = _declineButton.GetComponentInChildren<Text>();
            if (text != null) text.text = string.IsNullOrEmpty(label) ? "不发动" : label;
        }

        /// <summary>
        /// 显示/隐藏"发动"按钮。
        ///
        /// 只有一个候选的抉择（剑客追加攻击、游侠强化射击）用按钮比"点手牌"直观得多，
        /// 所以这类抉择会在"不发动"左侧显示一个发动按钮。
        /// </summary>
        public void SetAcceptButtonVisible(bool visible)
        {
            EnsureAcceptButton();
            if (_acceptButton != null) _acceptButton.gameObject.SetActive(visible);
        }

        public void SetAcceptButtonLabel(string label)
        {
            EnsureAcceptButton();
            if (_acceptButton == null) return;

            Text text = _acceptButton.GetComponentInChildren<Text>();
            if (text != null) text.text = string.IsNullOrEmpty(label) ? "发动" : label;
        }

        /// <summary>
        /// 角色抉择按钮的水平位置：屏幕右侧（**在右手牌右边**）。
        ///
        /// 为什么放这里：原来"发动"和"不发动"是**左右并排**居中放
        /// （发动在 x=-180 宽 320，不发动在 x=0 宽 220），
        /// 两者边缘相差只有 360px 而总宽 540px —— **必然重叠**（玩家实测截图确认）。
        ///
        /// 现在改成**屏幕右侧、上下两个**，且所有"是否发动"类抉择共用这一处位置。
        /// </summary>
        private const float ChoiceButtonX = 700f;

        /// <summary>上下两个按钮的纵向位置（"发动"在上）。</summary>
        private const float ChoiceAcceptY = -4f;
        private const float ChoiceDeclineY = -40f;

        private const float ChoiceButtonWidth = 300f;
        private const float ChoiceButtonHeight = 32f;

        private void EnsureAcceptButton()
        {
            if (_acceptButton != null) return;

            _acceptButton = CreateButton(_canvasRoot, "AcceptButton",
                                         new Vector2(ChoiceButtonX, ChoiceAcceptY),
                                         new Vector2(ChoiceButtonWidth, ChoiceButtonHeight),
                                         "发动");
            _acceptButton.GetComponent<Image>().color = new Color(0.24f, 0.46f, 0.32f);
            _acceptButton.onClick.AddListener(() => { AcceptClicked?.Invoke(); });
        }

        private void EnsureDeclineButton()
        {
            if (_declineButton != null) return;

            _declineButton = CreateButton(_canvasRoot, "DeclineButton",
                                          new Vector2(ChoiceButtonX, ChoiceDeclineY),
                                          new Vector2(ChoiceButtonWidth, ChoiceButtonHeight),
                                          "不发动");
            _declineButton.GetComponent<Image>().color = new Color(0.42f, 0.30f, 0.30f);
            _declineButton.onClick.AddListener(() => { DeclineClicked?.Invoke(); });
        }

        /// <summary>跳过按钮当前是否可点（显示中且满足条件）。</summary>
        public void SetSkipButtonInteractable(bool interactable)
        {
            if (_skipButton != null) _skipButton.interactable = interactable;
        }

        public void SetExitButtonVisible(bool visible)
        {
            if (_exitButton != null) _exitButton.gameObject.SetActive(visible);
        }

        public void SetLog(IReadOnlyList<string> lines)
        {
            if (_logText == null) return;

            if (lines == null || lines.Count == 0) { _logText.text = ""; return; }

            var sb = new StringBuilder();
            int start = lines.Count > 6 ? lines.Count - 6 : 0;
            for (int i = start; i < lines.Count; i++) sb.AppendLine(lines[i]);
            _logText.text = sb.ToString();
        }

        /// <summary>
        /// 刷新双方手牌、血量、状态。
        /// <paramref name="highlightAsChoices"/> 是当前阶段"正在等待玩家挑选"的那几只候选
        /// （选盾 / 锁手），会被高亮，便于玩家知道该点哪里。
        /// </summary>
        public void Refresh(PlayerState a, PlayerState b, Hand selectedHand,
                            IReadOnlyList<Hand> highlightAsChoices = null,
                            bool showSkipButton = false,
                            bool skipButtonInteractable = false,
                            int turnCount = 0, int turnLimit = 0,
                            IReadOnlyList<Hand> extraHighlights = null)
        {
            RefreshSide(_cardsA, a, selectedHand, highlightAsChoices, extraHighlights);
            RefreshSide(_cardsB, b, selectedHand, highlightAsChoices, extraHighlights);


            UpdateHealthBar(_hpBarFillA, a);
            UpdateHealthBar(_hpBarFillB, b);

            // 名字行同时承担"攻防加值"（狼人会永久成长，必须每次刷新重算）
            if (_nameTextA != null) _nameTextA.text = DescribeNameLine(a);
            if (_nameTextB != null) _nameTextB.text = DescribeNameLine(b);
            _statusTextA.text = HandVisual.ShieldSummary(a);
            _statusTextB.text = HandVisual.ShieldSummary(b);

            if (turnLimit > 0) SetTurn(turnCount, turnLimit);

            // "跳过回合"按钮出现在玩家 A 的右手牌右侧
            EnsureSkipButton(_cardsA[1].Button.GetComponent<RectTransform>());
            SetSkipButtonVisible(showSkipButton);
            SetSkipButtonInteractable(skipButtonInteractable);
        }


        /// <summary>
        /// 名字那一行：名字 + 攻防加值 + 血量数值（整体居中）。
        ///
        /// 例：<c>咒术师    攻 -1    防 0    20/20</c>
        ///
        /// 攻防是**动态**的（狼人会永久成长），所以每次刷新都要重算，
        /// 不能只在开局写一次。负的防御表示"受到的伤害 +2"（见 RULES.md §6）。
        /// </summary>
        private static string DescribeNameLine(PlayerState p)
        {
            int atk = p.Character?.AttackBonus ?? 0;
            int def = p.Character?.DefenseBonus ?? 0;

            return $"{p.Name}    攻 {Signed(atk)}    防 {Signed(def)}    " +
                   $"{p.Health}/{p.MaximumHealth}";
        }

        /// <summary>按当前血量刷新血条（宽度比例 + 颜色）。</summary>
        private static void UpdateHealthBar(Image fill, PlayerState p)
        {
            if (fill == null || p == null) return;

            float ratio = HealthRatio(p.Health, p.MaximumHealth);

            // 填充是左右拉伸的锚点，可用宽度就是底槽减去左右各 2px 内边距
            RectTransform rect = fill.rectTransform;
            rect.sizeDelta = new Vector2(
                AnimationDirector.HealthBarFillWidth(HealthBarWidth, p.Health, p.MaximumHealth),
                rect.sizeDelta.y);

            fill.color = HealthColor(ratio);
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        private static void RefreshSide(HandCard[] cards, PlayerState owner, Hand selectedHand,
                                        IReadOnlyList<Hand> highlightAsChoices,
                                        IReadOnlyList<Hand> extraHighlights)
        {
            foreach (HandCard card in cards)
            {
                Hand hand = card.Hand;
                bool selected = ReferenceEquals(hand, selectedHand);
                bool isChoice = ContainsHand(highlightAsChoices, hand)
                                || ContainsHand(extraHighlights, hand);

                // 只有"当前抉择的候选 / 已选中"才高亮；
                // "可点"只是低调亮边 —— 否则整个回合都会亮着。
                HandVisual.OutlineState outline = HandVisual.OutlineState.Hidden;
                if (selected || isChoice) outline = HandVisual.OutlineState.Highlighted;
                else if (card.Button.interactable) outline = HandVisual.OutlineState.Available;

                card.ValueText.text = hand.Value.ToString();
                card.StatusText.text = HandVisual.StatusText(hand, card.Owner);
                card.Background.color = HandVisual.CardColor(hand, card.Owner);
                card.Outline.color = HandVisual.OutlineColor(outline);
                card.Outline.enabled = outline != HandVisual.OutlineState.Hidden;
            }
        }

        /// <summary>不使用 LINQ：IReadOnlyList 在旧版 profile 上没有 Contains 扩展。</summary>
        private static bool ContainsHand(IReadOnlyList<Hand> list, Hand hand)
        {
            if (list == null || hand == null) return false;

            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], hand)) return true;

            return false;
        }

        /// <summary>设定哪些手牌可点。predicate 返回 true 的手牌才会响应点击。</summary>
        public void SetInteractable(Func<PlayerState, Hand, bool> predicate)
        {
            SetSideInteractable(_cardsA, predicate);
            SetSideInteractable(_cardsB, predicate);

            // ⚠️ 2v2 的手牌挂在四个环形面板上，**必须一起设置** ——
            //    漏掉它们的话，四个人的牌全是默认可点状态
            //    （点了会被 CanAct 拒掉，但描边/手型仍然骗玩家"能点"）。
            for (int i = 0; i < _ringPanels.Count; i++)
                SetSideInteractable(_ringPanels[i].Cards, predicate);
        }

        private static void SetSideInteractable(HandCard[] cards, Func<PlayerState, Hand, bool> predicate)
        {
            foreach (HandCard card in cards)
            {
                bool ok = predicate != null && card.Owner != null && predicate(card.Owner, card.Hand);
                card.Button.interactable = ok;
            }
        }

        public void ShowGameOver(string message)
        {
            if (_gameOverPanel == null) return;
            _gameOverText.text = message ?? "";
            _gameOverPanel.SetActive(true);
        }

        public void HideGameOver()
        {
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (_canvasRoot != null) _canvasRoot.gameObject.SetActive(visible);
        }

        // ============================================================
        // 控件工厂
        // ============================================================

        private RectTransform CreatePanel(RectTransform parent, string name,
                                          Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 size, Color color, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;

            go.GetComponent<Image>().color = color;
            return rect;
        }

        private Text CreateText(RectTransform parent, string name, Vector2 position, Vector2 size,
                                int fontSize, TextAnchor alignment, Color color,
                                bool anchorTopLeft = false, bool anchorTopRight = false)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            if (anchorTopLeft)
            {
                // 左上角锚点；position 是"距左上角的内边距"（x 向右、y 向下）
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(position.x, -Mathf.Abs(position.y));
            }
            else if (anchorTopRight)
            {
                // 右上角锚点；position 是"距右上角的内边距"（x 向左、y 向下）
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-Mathf.Abs(position.x), -Mathf.Abs(position.y));
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
            }

            rect.sizeDelta = size;

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

        private Button CreateButton(RectTransform parent, string name, Vector2 position,
                                    Vector2 size, string label, bool anchorTopRight = false)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            if (anchorTopRight)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
            }
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            go.GetComponent<Image>().color = new Color(0.22f, 0.42f, 0.70f);

            Text text = CreateText(rect, "Label", Vector2.zero, size, 26,
                                   TextAnchor.MiddleCenter, Color.white);
            text.text = label;

            return go.GetComponent<Button>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
