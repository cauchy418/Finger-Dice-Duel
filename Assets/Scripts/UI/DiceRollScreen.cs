using System;
using System.Collections;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 开局"掷骰分配初始手值"界面（1d20）。
    ///
    /// 流程：
    /// ```
    /// ① 点「掷骰」-> 两个骰子数字**滚动减速** -> 定格
    /// ② 显示顺序提示（点数小的先分配）
    /// ③ 轮到谁分配 -> 列出**合法拆分**按钮 -> 点一个即完成
    /// ④ 双方都分配完 -> 通知上层开始对局
    /// ```
    ///
    /// ## 为什么要单独一个组件
    ///
    /// `GameBootstrap` 已经很大了；而掷骰界面自带**协程动画**与**动态候选按钮**，
    /// 混进去会让它更难维护。这里只依赖 <see cref="MatchStartController"/> 与
    /// `DiceRollText`（都在 Assistant 层、可无头测试），自己只做布局与播放。
    ///
    /// ⚠️ 候选按钮**只从 controller.Candidates 生成** —— 界面自己绝不拼算拆分，
    /// 否则很容易把不合法的手值给到玩家（玩家明确要求过这一点）。
    /// </summary>
    public sealed class DiceRollScreen : MonoBehaviour
    {
        /// <summary>动画播完后才显示候选按钮（秒）。由时间轴决定，这里只做兜底上限。</summary>
        private const float AnimationSafetyTimeout = 5f;

        /// <summary>轮到 AI 时，等多久再自动分配（让玩家看清"轮到 AI 了"）。</summary>
        private const float AiAssignDelay = 1.1f;

        private D20DieView _dieA, _dieB;
        private RectTransform _root;
        private Text _titleText;
        private Text _rollText;
        private Text _orderText;
        private Text _prevText;
        private Text _hintText;
        private Button _rollButton;
        private Button _flipButton;
        private RectTransform _optionsRoot;

        /// <summary>
        /// 候选按钮是否按"翻转"后的顺序显示（左手/右手对调）。
        ///
        /// 候选本身是**去重**的（只留左手 ≤ 右手，见
        /// <see cref="DiceRollSetup.DistinctSplits"/>），所以 `5/1` 这种摆法
        /// 要靠本开关才看得到 —— 它只改显示顺序，不影响任何规则
        /// （目前左右手作用完全相同）。
        /// </summary>
        private bool _flipped;
        private Text _lessonHint;
        private Button _lessonReturn;
        private Action _onLessonReturn;
        private RectTransform _lessonNarrator;
        private Text _lessonNarration;

        private Font _font;
        private Font _boldFont;

        private MatchStartController _controller;

        /// <summary>对局对象（用来判断"这次该 A 还是 B 决定"）。</summary>
        private GameManager _game;

        /// <summary>AI 自动分配的协程句柄。</summary>
        private Coroutine _autoCoroutine;

        /// <summary>分配全部完成时回调（由 GameBootstrap 接到"开始对局"）。</summary>
        public event Action Finished;

        /// <summary>当前是否显示中。</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>
        /// 开局后**谁先出招** —— 点数**大**的那位（平手则 A）。
        ///
        /// ⚠️ 与"谁先决定初始手值"是两件事：
        /// 点数小的先决定手值（先选但信息少），点数大的先行动（后选但先手）。
        /// 还没掷骰时为 null，调用方应回退到默认先手。
        /// </summary>
        public PlayerState StartingPlayer => _controller?.FirstToAct;

        private readonly List<GameObject> _optionButtons = new List<GameObject>();

        // 骰子动画状态
        private bool _animating;
        private Coroutine _animCoroutine;

        // ============================================================
        // 构建
        // ============================================================

        public void Build(RectTransform parent, Font font, Font boldFont)
        {
            _font = font;
            _boldFont = boldFont;

            var go = new GameObject("DiceRollScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);

            // 卡片底板
            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_root, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1660f, 980f);
            panelRect.anchoredPosition = Vector2.zero;
            FrontEndArt.Apply(panel.GetComponent<Image>(), "UI/panel_content");

            _titleText = MakeText(panelRect, "Title", new Vector2(0f, 430f), new Vector2(1400f, 60f),
                                  44, TextAnchor.MiddleCenter, new Color(0.98f, 0.86f, 0.45f));
            _titleText.text = "掷骰决定初始手值";

            _rollText = MakeText(panelRect, "Roll", new Vector2(0f, 368f), new Vector2(1400f, 42f),
                                 24, TextAnchor.MiddleCenter, FrontEndArt.Gold);
            _rollText.text = "等待投掷 · 1d20";

            _orderText = MakeText(panelRect, "Order", new Vector2(0f, -10f), new Vector2(1400f, 44f),
                                  28, TextAnchor.MiddleCenter, new Color(0.75f, 0.80f, 0.90f));
            _orderText.text = "";

            // 先手方已经选好的手值（信息优势：后决定的人看得到）
            _prevText = MakeText(panelRect, "Previous", new Vector2(0f, -75f), new Vector2(1400f, 46f),
                                 30, TextAnchor.MiddleCenter, new Color(0.55f, 0.90f, 0.70f));
            _prevText.text = "";

            _hintText = MakeText(panelRect, "Hint", new Vector2(0f, -138f), new Vector2(1400f, 56f),
                                 28, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.95f));
            _hintText.text = "";

            _rollButton = MakeButton(panelRect, "RollButton", new Vector2(0f, -260f),
                                     new Vector2(320f, 70f), "掷骰");
            _rollButton.onClick.AddListener(OnRollClicked);

            // 候选按钮的容器
            var optionScroll = FrontEndArt.Scroll(panelRect, "OptionsViewport", new Vector2(0, -310),
                new Vector2(1400, 240), out _optionsRoot);
            optionScroll.gameObject.SetActive(false);
            _optionsRoot.name = "Options";
            _optionsRoot.anchorMin = _optionsRoot.anchorMax = new Vector2(0.5f, 1);
            _optionsRoot.pivot = new Vector2(0.5f, 1);
            _optionsRoot.anchoredPosition = Vector2.zero;
            _flipButton = MakeButton(panelRect, "FlipButton", new Vector2(0f, -455f),
                                     new Vector2(240f, 52f), "翻转：关", fontSize: 26);
            _flipButton.onClick.AddListener(OnFlipClicked);

            _dieA = D20DieView.Build(panelRect, "InitiativeDieA", "玩家", new Vector2(-300, 200), 250);
            _dieB = D20DieView.Build(panelRect, "InitiativeDieB", "对手", new Vector2(300, 200), 250);
            _lessonHint = MakeText(panelRect, "InitiativeLesson", new Vector2(0, 10), new Vector2(1400, 28),
                21, TextAnchor.MiddleCenter, FrontEndArt.Gold);
            _lessonReturn = MakeButton(panelRect, "LessonReturn", new Vector2(-675, 430), new Vector2(160, 52), "返回章节", fontSize: 22);
            _lessonReturn.onClick.AddListener(() => _onLessonReturn?.Invoke());
            _lessonNarrator = FrontEndArt.Rect(panelRect, "VeteranInitiative", new Vector2(-660, 130), new Vector2(280, 400));
            BattleArt.Apply(_lessonNarrator.gameObject.AddComponent<Image>(), "UI/panel_tooltip", true);
            var portrait = FrontEndArt.Rect(_lessonNarrator, "VeteranPortrait", new Vector2(-83, 140), new Vector2(64, 64)).gameObject.AddComponent<Image>();
            BattleArt.Apply(portrait, AdventureLevel1.GuideAvatarKey); portrait.preserveAspect = true; portrait.raycastTarget = false;
            FrontEndArt.Text(_lessonNarrator, "VeteranName", "老兵 · 先攻指引", new Vector2(35, 140), new Vector2(154, 36), 18, TextAnchor.MiddleLeft);
            _lessonNarration = FrontEndArt.Text(_lessonNarrator, "VeteranWords", "", new Vector2(0, -26), new Vector2(236, 248), 18, TextAnchor.UpperLeft);
            _lessonNarration.color = Color.white;
            _lessonNarration.horizontalOverflow = HorizontalWrapMode.Overflow;
            _lessonNarration.verticalOverflow = VerticalWrapMode.Truncate;
            SetLesson(null, null);
            _root.gameObject.SetActive(false);
        }

        public void SetLesson(string title, Action onReturn, string guideName="老兵", string guideAvatar="Adventure/guide_soldier")
        {
            _lessonNarrator.Find("VeteranName").GetComponent<Text>().text=guideName+" · 先攻指引";
            BattleArt.Apply(_lessonNarrator.Find("VeteranPortrait").GetComponent<Image>(),guideAvatar);
            bool enabled = !string.IsNullOrEmpty(title);
            if (_titleText != null) { _titleText.text = enabled ? title : "掷骰决定初始手值"; _titleText.fontSize = enabled ? 32 : 44; }
            if (_lessonHint != null) { _lessonHint.text = enabled ? AdventureLevel5.InitiativeHint : ""; _lessonHint.gameObject.SetActive(enabled); }
            if (_lessonReturn != null) _lessonReturn.gameObject.SetActive(enabled);
            _onLessonReturn = onReturn;
            if (_lessonNarrator != null) _lessonNarrator.gameObject.SetActive(enabled);
            UpdateLessonNarrator();
            if (enabled) _hintText.text = "2～18按总和拆分；1由对手指定，19至少一手9，20自由分配。";
            if (_orderText != null) { _orderText.rectTransform.anchoredPosition = new Vector2(0, enabled ? -45 : -10); _orderText.fontSize = enabled ? 22 : 28; }
            if (_prevText != null) { _prevText.rectTransform.anchoredPosition = new Vector2(0, enabled ? -95 : -75); _prevText.fontSize = enabled ? 24 : 30; }
            if (_hintText != null)
            {
                _hintText.rectTransform.anchoredPosition = new Vector2(0, enabled ? -160 : -138);
                _hintText.rectTransform.sizeDelta = new Vector2(1400, enabled ? 52 : 56);
                _hintText.fontSize = enabled ? 23 : 28;
            }
        }

        private void UpdateLessonNarrator()
        {
            if (_lessonNarrator == null || !_lessonNarrator.gameObject.activeSelf || _lessonNarration == null) return;
            string words;
            if (_controller == null || _controller.RollA == 0)
                words = "「先看清：谁先选，谁先动。」\n双方各投1d20。小点先分配手值，大点先行动。\n同点同时分配，本场玩家先行动。\n起手只分数字，5不是完整盾，9不立即回复。";
            else if (_animating)
                words = "「等骰点落定，再作决定。」\n小点先分配，大点先行动；分配先后与行动先后是两件事。";
            else if (_controller.IsNamingOpponent)
                words = "「大失败，由对手代选。」\n" + _controller.Assignee.Name + "投出1，由" + _controller.Decider.Name + "指定其两手。\n这只改变分配权，先攻仍看骰点。\n本场" + _controller.FirstToAct.Name + "先行动。";
            else
                words = "「骰点已定：你" + _controller.RollA + "，对手" + _controller.RollB + "。」\n本场" + _controller.FirstToAct.Name + "先行动。\n现在由" + (_controller.Decider?.Name ?? "双方") + "分配手值。\n小点先选，大点后选；后选可参考已公开的手值。";
            FrontEndArt.SetNarrationText(_lessonNarration, words, 236f, 19, reflow: true);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
            if (!visible)
            {
                if (_animCoroutine != null) StopCoroutine(_animCoroutine);
                if (_autoCoroutine != null) StopCoroutine(_autoCoroutine);
                _animCoroutine = _autoCoroutine = null;
                _animating = false;
            }
        }

        // ============================================================
        // 开始一局新的掷骰
        // ============================================================

        /// <summary>用给定的 GameManager 准备一次掷骰（先不掷，等玩家点按钮）。</summary>
        public void Begin(GameManager game)
        {
            SetLesson(null, null);
            _game = game;
            _controller = new MatchStartController(game);
            _dieA.SetName(_controller.NameA); _dieB.SetName(_controller.NameB);
            _dieA.SetFace(null); _dieB.SetFace(null);
            _dieA.Pose(1, 0); _dieB.Pose(1, 0);

            // 复位显示
            _rollText.text = "等待投掷 · 1d20";
            _orderText.text = "";
            _prevText.text = "";
            _hintText.text = "双方各投 1d20：点数就是两只手初始点数之和。";
            _rollButton.gameObject.SetActive(true);
            _rollButton.GetComponentInChildren<Text>().text = "掷骰";
            ClearOptions();

            // 每次开局都从"不翻转"开始，避免沿用上一局的显示顺序
            _flipped = false;
            if (_flipButton != null) _flipButton.gameObject.SetActive(false);

            _animating = false;
            if (_animCoroutine != null) { StopCoroutine(_animCoroutine); _animCoroutine = null; }
            if (_autoCoroutine != null) { StopCoroutine(_autoCoroutine); _autoCoroutine = null; }
        }

        private void OnRollClicked()
        {
            if (_controller == null || _animating) return;

            _rollButton.gameObject.SetActive(false);
            _controller.Roll();

            // ⚠️ 顺序提示**必须**取自控制器。
            // 曾经界面自己拼名字（把 decider 的名字两边都用上），
            // 于是"点数小的先分配"指向了错误的人 —— 实测被玩家一眼看出。
            _orderText.text = _controller.OrderText;

            _animCoroutine = StartCoroutine(PlayRollAnimation());
            UpdateLessonNarrator();
        }

        /// <summary>
        /// 掷骰动画：两个骰子数字快速滚动、逐渐减速、定格。
        ///
        /// 时间轴来自 <see cref="DiceRollAnimationPlan"/>（纯数据、已单测），
        /// 这里只负责每帧把 <c>FaceAt</c> 写进 Text。
        /// </summary>
        private IEnumerator PlayRollAnimation()
        {
            _animating = true;

            var planA = new DiceRollAnimationPlan(_controller.RollA, new System.Random(_controller.RollA * 31 + 1));
            var planB = new DiceRollAnimationPlan(_controller.RollB, new System.Random(_controller.RollB * 37 + 7));

            float t = 0f;
            while (t < planA.Duration || t < planB.Duration)
            {
                t += Time.deltaTime;

                _rollText.text = "正在投掷 1d20 …";
                _dieA.SetFace(planA.FaceAt(t)); _dieB.SetFace(planB.FaceAt(t));
                float wobble = Mathf.Sin(t * 32) * 12 * (1 - planA.ProgressAt(t));
                _dieA.Pose(planA.ScaleAt(t), wobble);
                _dieB.Pose(planB.ScaleAt(t), -wobble);

                // 轻微缩放，做出"咔哒"感（纯补间，不需要美术资源）
                float scale = planA.ScaleAt(t);
                _rollText.rectTransform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            // 定格：写死真实点数（名字也取自控制器，别自己编）
            _rollText.text = DiceRollText.RollLine(_controller.NameA, _controller.RollA,
                                                   _controller.NameB, _controller.RollB);
            _rollText.rectTransform.localScale = Vector3.one;
            _dieA.SetFace(_controller.RollA, true); _dieB.SetFace(_controller.RollB, true);
            _dieA.Pose(1, 0); _dieB.Pose(1, 0);

            // 顺序提示：同样取自控制器
            _orderText.text = _controller.OrderText;

            _animating = false;
            _animCoroutine = null;

            ShowAssignment();
        }

        // ============================================================
        // 分配阶段：列出**合法**拆分
        // ============================================================

        private void ShowAssignment()
        {
            if (_controller == null) return;
            UpdateLessonNarrator();

            if (_controller.IsComplete) { Finish(); return; }

            // ⚠️ 提示文案**完全取自控制器**。
            //
            // 界面曾经自己拼（先调 AssignHint 再用 Hint 覆盖），
            // 那份被覆盖的调用是死代码，而且它把两个名字/点数用错 ——
            // 实机出现过「战士·破阵者投出 19（大失败）」（实际 A 投 17、19 还是小成功）。
            // 现在只认控制器给的文字，并有源码守卫钉住（见 ScreenFlowTests）。
            _hintText.text = _controller.DecisionText;
            if (string.IsNullOrEmpty(_hintText.text)) _hintText.text = _controller.Hint;

            // 信息优势：后决定的一方**看得到**先手方已经选好的手值。
            //
            // 点数小的人先决定、看不到对方的分配；点数大的人后决定、看得到，
            // 于是能针对性应对 —— 这就是"后决定算更成功"的体现。
            //
            // 双大失败时不给（那时两人都在替对手指定，看到就变成互相追逐）。
            _prevText.text = "";

            if (_controller.HasInfoAdvantage)
            {
                var prevSplit = _controller.PreviousSplit;
                if (prevSplit.HasValue)
                {
                    _prevText.text =
                        $"对手已选：{DiceRollText.SplitLabel(prevSplit.Value)}　" +
                        $"（{_controller.PreviouslyAssigned?.Name}）";
                }
            }

            BuildOptions();

            // 轮到 AI 就自动分配（否则会停在界面上等人点，而玩家根本不该替 AI 选）
            if (IsAiTurn) StartAutoAssign();
        }

        /// <summary>当前是否轮到 AI（B 由电脑操作，或这次是 B 替 A 指定）。</summary>
        private bool IsAiTurn
        {
            get
            {
                if (_controller?.Decider == null || _game == null) return false;

                // B 方的任何决定都由电脑代做；A 方的决定由玩家做。
                // （大失败时决策者是对手，所以要看 Decider 而不是 Assignee。）
                return ReferenceEquals(_controller.Decider, _game.PlayerB);
            }
        }

        /// <summary>
        /// AI 自动分配：稍等片刻后按**价值表**挑一个合法拆分。
        ///
        /// 选点策略见 <see cref="DiceSplitAdvisor"/> 与 <see cref="HandValueProfiles"/>：
        ///   · 优先最大化 AI 自己角色的价值
        ///   · 两个拆分对它相近时，挑一个"给玩家价值更低"的
        ///   · 若 AI 是后手（看得到玩家已选），再考虑克制那一对
        ///
        /// ⚠️ 候选仍然**只来自 controller.Candidates** —— 界面绝不自己拼拆分。
        /// </summary>
        private void StartAutoAssign()
        {
            if (_autoCoroutine != null) return;
            _autoCoroutine = StartCoroutine(AutoAssignAfterDelay());
        }

        private IEnumerator AutoAssignAfterDelay()
        {
            // 稍等一下，让玩家看清"轮到 AI 了"
            float t = 0f;
            while (t < AiAssignDelay)
            {
                t += Time.deltaTime;
                if (_controller == null || _controller.IsComplete) break;
                yield return null;
            }

            _autoCoroutine = null;
            if (_controller == null || _controller.IsComplete) yield break;

            IReadOnlyList<HandSplit> cands = _controller.Candidates;
            if (cands == null || cands.Count == 0) yield break;

            HandSplit pick = PickAiSplit(cands);
            if (!_controller.Submit(pick)) yield break;

            if (_controller.IsComplete) { Finish(); yield break; }

            ShowAssignment();     // 若又轮到 AI，会再次自动分配
        }

        /// <summary>
        /// 让 AI 选一个拆分。
        ///
        /// **决策者是 AI 时，"被分配者"可能是玩家**（大失败时由对手指定），
        /// 所以角色取值要用 <c>Assignee</c> —— 那才是"这对手值属于谁"。
        /// 而"要克制谁"看的是另一方。
        /// </summary>
        private HandSplit PickAiSplit(IReadOnlyList<HandSplit> cands)
        {
            var assignee = _controller.Assignee;
            var decider = _controller.Decider;

            // assignee = 这对手值最终属于谁（大失败时正好是玩家）
            var myDef = assignee?.Definition;

            // 另一方 = 决策者自己（他自己分配自己时，对手是另一位玩家）
            var other = ReferenceEquals(assignee, _controller.PlayerA)
                ? _controller.PlayerB
                : _controller.PlayerA;

            // 后手的信息优势：先手方已经选好了，AI 可以用来克制
            HandSplit? opponentsPick = _controller.HasInfoAdvantage
                ? _controller.PreviousSplit
                : null;

            var advisor = new DiceSplitAdvisor();
            return advisor.Choose(myDef, other?.Definition, cands, opponentsPick);
        }

        /// <summary>
        /// 生成候选按钮。
        ///
        /// ⚠️ **只遍历 controller.Candidates** —— 界面绝不自己算拆分，
        /// 这样"不合法的选项"从结构上就不可能出现在界面上。
        /// </summary>
        private void BuildOptions()
        {
            ClearOptions();

            IReadOnlyList<HandSplit> cands = _controller.Candidates;
            if (cands == null || cands.Count == 0) return;

            // 大成功去重后仍有 45 种（8 行），所以按钮要紧凑些才放得下
            const float btnW = 200f, btnH = 52f, gapX = 24f, gapY = 14f;
            const int perRow = 6;

            int rows = (cands.Count + perRow - 1) / perRow;
            float totalH = rows * btnH + (rows - 1) * gapY;
            float startY = -btnH * 0.5f;
            _optionsRoot.sizeDelta = new Vector2(1360, Mathf.Max(240, totalH));
            _optionsRoot.anchoredPosition = Vector2.zero;

            for (int i = 0; i < cands.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;

                int inRow = Math.Min(perRow, cands.Count - row * perRow);
                float rowW = inRow * btnW + (inRow - 1) * gapX;
                float startX = -rowW * 0.5f + btnW * 0.5f;

                float x = startX + col * (btnW + gapX);
                float y = startY - row * (btnH + gapY);

                // 翻转开关只影响**显示顺序**：候选本身是去重后的（左手 ≤ 右手）。
                // 所以这里只把标签的两个数字对调显示 ——
                // **不构造新的 HandSplit**（界面绝不自己造拆分，见源码守卫测试）。
                HandSplit split = cands[i];
                string label = _flipped
                    ? $"{split.Right} / {split.Left}"
                    : DiceRollText.SplitLabel(split);

                Button b = MakeButton(_optionsRoot, "Split" + i, new Vector2(x, y),
                                      new Vector2(btnW, btnH), label, fontSize: 30);
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(x, y);
                b.onClick.AddListener(() => OnSplitClicked(split));
                _optionButtons.Add(b.gameObject);
            }

            _optionsRoot.parent.gameObject.SetActive(true);
            _optionsRoot.gameObject.SetActive(true);
            UpdateFlipButton();
        }

        /// <summary>
        /// 更新"翻转"按钮的可见性与文案。
        ///
        /// 只在**存在镜像对被去掉**时才显示 —— 即候选里至少有一个
        /// "左右不相等"的拆分（相同时翻转没有意义）。
        /// </summary>
        private void UpdateFlipButton()
        {
            if (_flipButton == null) return;

            bool anyAsymmetric = false;
            if (_controller != null)
            {
                IReadOnlyList<HandSplit> cands = _controller.Candidates;
                for (int i = 0; i < cands.Count; i++)
                {
                    if (cands[i].Left != cands[i].Right) { anyAsymmetric = true; break; }
                }
            }

            _flipButton.gameObject.SetActive(anyAsymmetric);
            Text label = _flipButton.GetComponentInChildren<Text>();
            if (label != null) label.text = _flipped ? "翻转：开" : "翻转：关";
        }

        private void OnFlipClicked()
        {
            _flipped = !_flipped;
            BuildOptions();     // 重画候选（用翻转后的显示顺序）
        }

        private void OnSplitClicked(HandSplit split)
        {
            if (_controller == null) return;
            if (!_controller.Submit(split)) return;     // 非法 -> 静默忽略（界面上不该发生）

            if (_controller.IsComplete) { Finish(); return; }

            // 轮到下一个 -> 刷新提示与候选
            ShowAssignment();
        }

        private void Finish()
        {
            ClearOptions();
            _rollButton.gameObject.SetActive(false);
            _hintText.text = _controller?.Hint ?? "开局完成";

            Finished?.Invoke();
        }

        private void ClearOptions()
        {
            if (_optionsRoot != null) _optionsRoot.parent.gameObject.SetActive(false);
            for (int i = 0; i < _optionButtons.Count; i++)
                if (_optionButtons[i] != null) Destroy(_optionButtons[i]);
            _optionButtons.Clear();
        }

        // ============================================================
        // 小工具（与 GameBootstrap 的写法保持一致）
        // ============================================================

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

        private Button MakeButton(RectTransform parent, string name, Vector2 position,
                                  Vector2 size, string label, int fontSize = 28)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Text text = MakeText(rect, "Label", Vector2.zero, size, fontSize,
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
