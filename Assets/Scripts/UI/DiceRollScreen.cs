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

        private RectTransform _root;
        private Text _titleText;
        private Text _rollText;
        private Text _orderText;
        private Text _hintText;
        private Button _rollButton;
        private RectTransform _optionsRoot;

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
            panelRect.sizeDelta = new Vector2(1500f, 820f);
            panelRect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.19f);

            _titleText = MakeText(panelRect, "Title", new Vector2(0f, 350f), new Vector2(1400f, 60f),
                                  44, TextAnchor.MiddleCenter, new Color(0.98f, 0.86f, 0.45f));
            _titleText.text = "掷骰决定初始手值";

            _rollText = MakeText(panelRect, "Roll", new Vector2(0f, 250f), new Vector2(1400f, 90f),
                                 64, TextAnchor.MiddleCenter, Color.white);
            _rollText.text = "—";

            _orderText = MakeText(panelRect, "Order", new Vector2(0f, 180f), new Vector2(1400f, 44f),
                                  28, TextAnchor.MiddleCenter, new Color(0.75f, 0.80f, 0.90f));
            _orderText.text = "";

            _hintText = MakeText(panelRect, "Hint", new Vector2(0f, 130f), new Vector2(1400f, 56f),
                                 28, TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 0.95f));
            _hintText.text = "";

            _rollButton = MakeButton(panelRect, "RollButton", new Vector2(0f, 40f),
                                     new Vector2(320f, 70f), "掷骰");
            _rollButton.onClick.AddListener(OnRollClicked);

            // 候选按钮的容器
            var options = new GameObject("Options", typeof(RectTransform));
            options.transform.SetParent(panelRect, false);
            _optionsRoot = options.GetComponent<RectTransform>();
            _optionsRoot.anchorMin = _optionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _optionsRoot.pivot = new Vector2(0.5f, 0.5f);
            _optionsRoot.sizeDelta = new Vector2(1400f, 420f);
            _optionsRoot.anchoredPosition = new Vector2(0f, -170f);

            _root.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        // ============================================================
        // 开始一局新的掷骰
        // ============================================================

        /// <summary>用给定的 GameManager 准备一次掷骰（先不掷，等玩家点按钮）。</summary>
        public void Begin(GameManager game)
        {
            _game = game;
            _controller = new MatchStartController(game);

            // 复位显示
            _rollText.text = "—";
            _orderText.text = "";
            _hintText.text = "双方各投 1d20：点数就是两只手初始点数之和。";
            _rollButton.gameObject.SetActive(true);
            _rollButton.GetComponentInChildren<Text>().text = "掷骰";
            ClearOptions();

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

                string a = planA.FaceAt(t).ToString();
                string b = planB.FaceAt(t).ToString();
                _rollText.text = $"{a}　vs　{b}";

                // 轻微缩放，做出"咔哒"感（纯补间，不需要美术资源）
                float scale = planA.ScaleAt(t);
                _rollText.rectTransform.localScale = new Vector3(scale, scale, 1f);

                yield return null;
            }

            // 定格：写死真实点数（名字也取自控制器，别自己编）
            _rollText.text = DiceRollText.RollLine(_controller.NameA, _controller.RollA,
                                                   _controller.NameB, _controller.RollB);
            _rollText.rectTransform.localScale = Vector3.one;

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

            if (_controller.IsComplete) { Finish(); return; }

            // ⚠️ 提示文案**完全取自控制器**。
            //
            // 界面曾经自己拼（先调 AssignHint 再用 Hint 覆盖），
            // 那份被覆盖的调用是死代码，而且它把两个名字/点数用错 ——
            // 实机出现过「战士投出 19（大失败）」（实际 A 投 17、19 还是小成功）。
            // 现在只认控制器给的文字，并有源码守卫钉住（见 ScreenFlowTests）。
            _hintText.text = _controller.DecisionText;
            if (string.IsNullOrEmpty(_hintText.text)) _hintText.text = _controller.Hint;

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
        /// AI 自动分配：稍等片刻后随机挑一个**合法**拆分。
        ///
        /// 这一步只是先把流程跑通 ——
        /// 真正的 AI 应当按"自己技能看重哪些数 / 对手需要哪些数"来挑（后续再做）。
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

            HandSplit pick = cands[UnityEngine.Random.Range(0, cands.Count)];
            if (!_controller.Submit(pick)) yield break;

            if (_controller.IsComplete) { Finish(); yield break; }

            ShowAssignment();     // 若又轮到 AI，会再次自动分配
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

            const float btnW = 200f, btnH = 64f, gapX = 24f, gapY = 20f;
            const int perRow = 6;

            int rows = (cands.Count + perRow - 1) / perRow;
            float totalH = rows * btnH + (rows - 1) * gapY;
            float startY = totalH * 0.5f - btnH * 0.5f;

            for (int i = 0; i < cands.Count; i++)
            {
                int row = i / perRow;
                int col = i % perRow;

                int inRow = Math.Min(perRow, cands.Count - row * perRow);
                float rowW = inRow * btnW + (inRow - 1) * gapX;
                float startX = -rowW * 0.5f + btnW * 0.5f;

                float x = startX + col * (btnW + gapX);
                float y = startY - row * (btnH + gapY);

                HandSplit split = cands[i];
                Button b = MakeButton(_optionsRoot, "Split" + i, new Vector2(x, y),
                                      new Vector2(btnW, btnH), DiceRollText.SplitLabel(split),
                                      fontSize: 30);
                b.onClick.AddListener(() => OnSplitClicked(split));
                _optionButtons.Add(b.gameObject);
            }

            _optionsRoot.gameObject.SetActive(true);
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

            go.GetComponent<Image>().color = new Color(0.24f, 0.46f, 0.76f);

            Text text = MakeText(rect, "Label", Vector2.zero, size, fontSize,
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
