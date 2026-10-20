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
    /// **2v2 经典玩法的先攻界面**：四人各投 1d20 → 依次决定初始手值。
    ///
    /// 玩家确认的流程（由 <see cref="TwoOnTwoStartController"/> 提供状态）：
    ///   · 四人各投 1d20，按点数定座位（大数大队去 1&amp;4 号位）
    ///   · 按 **4 → 3 → 2 → 1** 依次决定自己的两次手值
    ///   · **大失败（1）最先**：由**配对的对手**替他决定（完全自由）
    ///   · **大成功（20）最后**：各自决定自己的
    ///   · **后决定的人能看到前面人决定的数字**
    ///
    /// 界面只负责"显示状态 + 把玩家的点击交给控制器"，
    /// 规则与顺序**全在控制器里**（避免两处各写一份而漂移）。
    /// </summary>
    public sealed class TwoOnTwoInitiativeScreen
    {
        private readonly D20DieView[] _dice = new D20DieView[4];
        private MonoBehaviour _runner;
        private Coroutine _rollAnimation;
        private RectTransform _root;
        private Font _font;

        private Text _titleText;
        private Text _seatText;      // 四人点数与座位
        private Text _stepText;      // 当前这一步：谁决定谁
        private Text _decidedText;   // 已决定的数字（信息优势）
        private Text _hintText;

        private Button _rollButton;
        private Button _flipButton;
        private RectTransform _optionsRoot;
        private readonly List<GameObject> _optionButtons = new List<GameObject>();

        private TwoOnTwoStartController _controller;
        private bool _flipped;

        /// <summary>整轮决定完成（调用方据此开局）。</summary>
        public event Action Finished;

        public void Build(RectTransform parent, Font font)
        {
            _font = font;
            _runner = parent.GetComponentInParent<GameBootstrap>();

            var go = new GameObject("TwoOnTwoInitiativeScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_root, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1660f, 980f);
            panelRect.anchoredPosition = Vector2.zero;
            FrontEndArt.Apply(panel.GetComponent<Image>(), "UI/panel_content");

            _titleText = MakeText(panelRect, "Title", new Vector2(0f, 430f),
                                  new Vector2(1440f, 60f), 44, TextAnchor.MiddleCenter,
                                  new Color(0.98f, 0.86f, 0.45f));
            _titleText.text = "2 v 2 掷骰先攻";

            _seatText = MakeText(panelRect, "Seats", new Vector2(0f, 34f),
                                 new Vector2(1500f, 90f), 22, TextAnchor.MiddleCenter,
                                 new Color(0.75f, 0.85f, 1f));

            _stepText = MakeText(panelRect, "Step", new Vector2(0f, -60f),
                                 new Vector2(1500f, 64f), 26, TextAnchor.MiddleCenter,
                                 Color.white);

            _decidedText = MakeText(panelRect, "Decided", new Vector2(0f, -120f),
                                    new Vector2(1500f, 48f), 22, TextAnchor.MiddleCenter,
                                    new Color(0.55f, 0.90f, 0.70f));

            _hintText = MakeText(panelRect, "Hint", new Vector2(0f, -180f),
                                 new Vector2(1500f, 56f), 23, TextAnchor.MiddleCenter,
                                 new Color(0.85f, 0.88f, 0.95f));

            _rollButton = MakeButton(panelRect, "RollButton", new Vector2(0f, -260f),
                                     new Vector2(340f, 68f), "掷骰");
            _rollButton.onClick.AddListener(OnRollClicked);

            var optionScroll = FrontEndArt.Scroll(panelRect, "OptionsViewport", new Vector2(0, -325),
                new Vector2(1400, 200), out _optionsRoot);
            optionScroll.gameObject.SetActive(false);
            _optionsRoot.name = "Options";
            _optionsRoot.anchorMin = _optionsRoot.anchorMax = new Vector2(0.5f, 1);
            _optionsRoot.pivot = new Vector2(0.5f, 1);
            _optionsRoot.anchoredPosition = Vector2.zero;
            _flipButton = MakeButton(panelRect, "FlipButton", new Vector2(0f, -455f),
                                     new Vector2(240f, 52f), "翻转：关", 26);
            _flipButton.onClick.AddListener(OnFlipClicked);

            for (int i = 0; i < 4; i++)
                _dice[i] = D20DieView.Build(panelRect, "InitiativeDie" + i, "先攻骰 " + (i + 1), new Vector2(-570 + i * 380, 230), 210);
            _root.gameObject.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
            if (!visible && _rollAnimation != null && _runner != null)
            {
                _runner.StopCoroutine(_rollAnimation);
                _rollAnimation = null;
            }
        }

        // ============================================================
        // 开始
        // ============================================================

        /// <summary>用"已建好四人"的控制器开始先攻（初始只显示"掷骰"按钮）。</summary>
        public void Begin(TwoOnTwoStartController controller)
        {
            _controller = controller;
            for (int i = 0; i < 4; i++)
            {
                _dice[i].SetName("先攻骰 " + (i + 1));
                _dice[i].SetFace(null);
                _dice[i].Pose(1, 0);
            }
            _flipped = false;

            ClearOptions();
            _rollButton.gameObject.SetActive(true);
            _seatText.text = "";
            _stepText.text = "四人各投 1d20";
            _decidedText.text = "";
            _hintText.text = "点「掷骰」开始（座位由点数决定）";

            _root.gameObject.SetActive(true);
        }

        private void OnRollClicked()
        {
            if (_controller == null || _rollAnimation != null) return;
            _rollButton.gameObject.SetActive(false);
            _rollAnimation = _runner.StartCoroutine(PlayRoll());
        }

        private IEnumerator PlayRoll()
        {
            // 控制器已完成唯一一次掷骰；这里只播放既定结果，不重新取随机数。
            var plans = new DiceRollAnimationPlan[4];
            for (int i = 0; i < 4; i++)
                plans[i] = new DiceRollAnimationPlan(_controller.RollOf(_controller.PlayerAtSeat(i + 1)), new System.Random(61 + i));
            _stepText.text = "正在投掷 1d20 …";
            float time = 0;
            while (time < plans[0].Duration)
            {
                time += Time.deltaTime;
                for (int i = 0; i < 4; i++)
                {
                    _dice[i].SetFace(plans[i].FaceAt(time));
                    _dice[i].Pose(plans[i].ScaleAt(time), Mathf.Sin(time * 30 + i) * 10 * (1 - plans[i].ProgressAt(time)));
                }
                yield return null;
            }
            for (int i = 0; i < 4; i++)
            {
                _dice[i].SetName((i + 1) + "号位 · " + _controller.PlayerAtSeat(i + 1).Name);
                _dice[i].SetFace(plans[i].FinalValue, true);
                _dice[i].Pose(1, 0);
            }
            _seatText.text = _controller.SeatSummary;
            _rollAnimation = null;
            AdvanceUntilHuman();
        }
        private void AdvanceUntilHuman()
        {
            if (_controller == null) return;

            if (_controller.IsFinished)
            {
                Finish();
                return;
            }

            int guard = 0;
            while (!_controller.IsFinished && guard++ < 8)
            {
                if (_controller.IsHumanStep(IsHuman)) break;

                _controller.AutoResolve(IsHuman, TwoOnTwoStartController.DefaultAiSplit,
                                        maxSteps: 1);
            }

            if (_controller.IsFinished)
            {
                Finish();
                return;
            }

            ShowStep();
        }

        /// <summary>把控制器状态画到界面上（当前步骤 + 已决定的数字 + 候选）。</summary>
        private void ShowStep()
        {
            if (_controller == null) return;

            TwoOnTwoInitiative.DecisionStep? step = _controller.CurrentStep;
            if (!step.HasValue) { Finish(); return; }

            int ownerSeat = step.Value.OwnerSeat;
            int deciderSeat = step.Value.DeciderSeat;

            string ownerName = _controller.PlayerAtSeat(ownerSeat)?.Name ?? "?";
            string deciderName = _controller.PlayerAtSeat(deciderSeat)?.Name ?? "?";

            _stepText.text = step.Value.IsCriticalFailure
                ? $"{ownerSeat} 号位（{ownerName}）大失败 —— 由对手 " +
                  $"{deciderSeat} 号位（{deciderName}）替他决定"
                : $"{ownerSeat} 号位（{ownerName}）：投出 " +
                  $"{_controller.RollOf(_controller.PlayerAtSeat(ownerSeat))}，请决定手值";

            // 信息优势：把已决定的数字列出来（玩家要求"后决定者看得到"）
            var sb = new System.Text.StringBuilder();
            for (int seat = 1; seat <= 4; seat++)
            {
                HandSplit? done = _controller.SplitOf(_controller.PlayerAtSeat(seat));
                if (!done.HasValue) continue;

                if (sb.Length > 0) sb.Append("　");
                sb.Append(seat).Append("号位：")
                  .Append(DiceRollText.SplitLabel(done.Value));
            }

            _decidedText.text = sb.Length > 0 ? "已决定　" + sb : "";
            _hintText.text = _controller.Hint;

            BuildOptions();
        }

        private void BuildOptions()
        {
            ClearOptions();

            IReadOnlyList<HandSplit> cands = _controller?.Candidates;
            if (cands == null || cands.Count == 0) return;

            // 大成功去重后仍有 45 种，所以按钮要紧凑（与 1v1 掷骰界面同一规格）
            const float btnW = 200f, btnH = 52f, gapX = 24f, gapY = 14f;
            const int perRow = 6;

            int rows = (cands.Count + perRow - 1) / perRow;
            float totalH = rows * btnH + (rows - 1) * gapY;
            float startY = -btnH * 0.5f;
            _optionsRoot.sizeDelta = new Vector2(1360, Mathf.Max(200, totalH));
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

                // 翻转只改**显示顺序**，不构造新 HandSplit（界面不自己造拆分）
                HandSplit split = cands[i];
                string label = _flipped
                    ? $"{split.Right} / {split.Left}"
                    : DiceRollText.SplitLabel(split);

                Button b = MakeButton(_optionsRoot, "Split" + i, new Vector2(x, y),
                                      new Vector2(btnW, btnH), label, 30);
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(x, y);
                b.onClick.AddListener(() => OnSplitClicked(split));
                _optionButtons.Add(b.gameObject);
            }

            _optionsRoot.parent.gameObject.SetActive(true);
            UpdateFlipButton();
        }

        private void OnSplitClicked(HandSplit split)
        {
            if (_controller == null) return;
            if (!_controller.Submit(split)) return;      // 非法 -> 忽略

            if (_controller.IsFinished) { Finish(); return; }

            AdvanceUntilHuman();
        }

        private void UpdateFlipButton()
        {
            if (_flipButton == null) return;

            bool anyAsymmetric = false;
            IReadOnlyList<HandSplit> cands = _controller?.Candidates;
            if (cands != null)
            {
                for (int i = 0; i < cands.Count; i++)
                    if (cands[i].Left != cands[i].Right) { anyAsymmetric = true; break; }
            }

            _flipButton.gameObject.SetActive(anyAsymmetric);
            Text label = _flipButton.GetComponentInChildren<Text>();
            if (label != null) label.text = _flipped ? "翻转：开" : "翻转：关";
        }

        private void OnFlipClicked()
        {
            _flipped = !_flipped;
            BuildOptions();
        }

        private void Finish()
        {
            ClearOptions();
            _rollButton.gameObject.SetActive(false);
            _flipButton.gameObject.SetActive(false);

            _stepText.text = "先攻完成";
            _hintText.text = _controller?.SeatSummary ?? "";

            // 把分配好的初始手值写进四人的手牌（只写数值，不触发效果）
            _controller?.ApplyToPlayers();

            Finished?.Invoke();
        }

        /// <summary>
        /// "这位玩家是不是人类"。
        ///
        /// 默认只有**主视角**是人类 —— 单机四人同屏里其余三人都是 AI。
        /// 由调用方通过 <see cref="SetHuman"/> 指定。
        /// </summary>
        private PlayerState _human;

        public void SetHuman(PlayerState human) => _human = human;

        private bool IsHuman(PlayerState p)
            => p != null && (_human == null || ReferenceEquals(p, _human));

        private void ClearOptions()
        {
            if (_optionsRoot != null) _optionsRoot.parent.gameObject.SetActive(false);
            for (int i = 0; i < _optionButtons.Count; i++)
                if (_optionButtons[i] != null) UnityEngine.Object.Destroy(_optionButtons[i]);
            _optionButtons.Clear();
        }

        // ============================================================
        // 小工具（与 DiceRollScreen 的写法保持一致）
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
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
