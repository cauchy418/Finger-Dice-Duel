using System;
using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Characters.SpecialLogics;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// **吟游诗人的四步抉择面板**（玩家确认的流程）：
    ///   ① 是否发动  ->  ② 增幅攻击 / 增幅防御 / 削弱攻击 / 削弱防御
    ///   -> ③ 目标数 1~3（强度 ±(4−人数)）  ->  ④ 选目标（按角色，可含自己）
    ///
    /// ⚠️ 自包含（自己建 Image / Text / Button），与 <see cref="DamageTypePanel"/> 同一套做法。
    ///
    /// ⚠️ 这是本项目**第一个"运算之后才问"**的技能，所以它不接 `BoardPhase` 阶段机，
    ///    而是只认 `BoardController` 上的三个入口：
    ///    `BardCandidates` / `BardMaxTargetCount` / `ApplyBardChoice` / `DeclineBardChoice`。
    /// </summary>
    public sealed class BardChoicePanel
    {
        /// <summary>四项选择的显示文案（顺序 = <see cref="BardLogic.BuffKind"/>）。</summary>
        public static readonly string[] KindLabels =
        {
            "增幅攻击", "增幅防御", "削弱攻击", "削弱防御",
        };

        private const float PanelW = 460f;
        private const float PanelH = 400f;
        private const float BtnW = 200f;
        private const float BtnH = 46f;
        private const float Gap = 10f;
        private const int MaxCandidates = 8;

        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly Text _title;

        private readonly Button[] _kindButtons = new Button[4];
        private readonly Button[] _countButtons = new Button[3];
        private readonly Button[] _candidateButtons = new Button[MaxCandidates];
        private readonly Text[] _candidateLabels = new Text[MaxCandidates];
        private readonly Button _confirm;
        private readonly Button _decline;

        private enum Step { Kind, Count, Targets }

        private Step _step = Step.Kind;
        private BoardController _board;
        private Action _onDone;
        private BardLogic.BuffKind _kind = BardLogic.BuffKind.BoostAttack;
        private int _wantCount = 1;
        private readonly List<PlayerState> _selected = new List<PlayerState>(3);

        /// <summary>面板当前是否可见。</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>当前处在第几步（供测试断言）。0 = 未显示、2/3/4 = 对应步骤。</summary>
        public int CurrentStep
            => !IsVisible ? 0 : (_step == Step.Kind ? 2 : (_step == Step.Count ? 3 : 4));

        public BardChoicePanel(RectTransform parent, Font font)
        {
            _font = font;

            var go = new GameObject("BardChoicePanel", typeof(Image));
            go.transform.SetParent(parent, false);

            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(PanelW, PanelH);
            // ⚠️ 玩家要求：放在**右侧**，与"发动 / 不发动"按钮同一处。
            //    原来放左侧，正好压住对手的手牌与战场信息。
            _root.anchoredPosition = new Vector2(640f, 0f);
            BattleArt.Apply(go.GetComponent<Image>(), "UI/panel_tooltip", true);

            _title = MakeText(_root, "Title", new Vector2(0f, 165f),
                              new Vector2(PanelW - 24f, 32f), 21,
                              TextAnchor.MiddleCenter, Color.white);
            _title.font = UiFont.Title;
            _title.color = BattleArt.Gold;

            // ---- ② 四项 ----
            for (int i = 0; i < 4; i++)
            {
                BardLogic.BuffKind kind = (BardLogic.BuffKind)i;
                _kindButtons[i] = MakeButton(KindLabels[i], new Vector2(
                    (i % 2 == 0 ? -105f : 105f),
                    110f - (i / 2) * (BtnH + Gap)), () => PickKind(kind));
            }

            // ---- ③ 目标数 ----
            for (int i = 0; i < 3; i++)
            {
                int n = i + 1;
                _countButtons[i] = MakeButton((n) + " 人  ±" + (4 - n),
                    new Vector2((i - 1) * (BtnW * 0.6f + Gap), 40f),
                    () => PickCount(n), BtnW * 0.6f);
            }

            // ---- ④ 候选 ----
            for (int i = 0; i < MaxCandidates; i++)
            {
                int index = i;
                _candidateButtons[i] = MakeButton("?", new Vector2(
                    (i % 2 == 0 ? -105f : 105f),
                    40f - (i / 2) * (BtnH + Gap - 6f)), () => ToggleCandidate(index));
                _candidateButtons[i].name = "BardTarget" + i;
                _candidateLabels[i] = _candidateButtons[i].GetComponentInChildren<Text>();
            }

            _confirm = MakeButton("确认", new Vector2(-105f, -165f), Confirm);
            _decline = MakeButton("不发动", new Vector2(105f, -165f), Decline);

            go.SetActive(false);
        }

        // ============================================================
        // 显示 / 隐藏
        // ============================================================

        /// <summary>
        /// 按当前状态显示面板（① 已经由调用方判断过了）。
        /// 每次显示都从第 ② 步重新开始。
        /// </summary>
        public void Show(BoardController board, Action onDone)
        {
            if (_root == null) return;

            _board = board;
            _onDone = onDone;
            _step = Step.Kind;
            _selected.Clear();
            _wantCount = 1;

            RefreshLabels();
            _root.gameObject.SetActive(true);
        }

        /// <summary>隐藏面板。</summary>
        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        // ============================================================
        // 三步交互
        // ============================================================

        private void PickKind(BardLogic.BuffKind kind)
        {
            _kind = kind;
            _step = Step.Count;
            RefreshLabels();
        }

        private void PickCount(int n)
        {
            int max = _board != null ? _board.BardMaxTargetCount : 1;
            if (n > max) n = max;
            if (n < 1) n = 1;

            _wantCount = n;
            _selected.Clear();
            _step = Step.Targets;
            RefreshLabels();
        }

        private void ToggleCandidate(int index)
        {
            if (_board == null) return;

            IReadOnlyList<PlayerState> cands = _board.BardCandidates;
            if (index < 0 || index >= cands.Count) return;

            PlayerState p = cands[index];

            if (_selected.Contains(p)) { _selected.Remove(p); }
            else
            {
                if (_selected.Count >= _wantCount) return;   // 已经选满
                _selected.Add(p);
            }

            RefreshLabels();
        }

        private void Confirm()
        {
            if (_board == null) return;
            if (_selected.Count != _wantCount) return;       // 还没选满

            _board.ApplyBardChoice(_kind, new List<PlayerState>(_selected));
            Hide();
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        private void Decline()
        {
            _board?.DeclineBardChoice();
            Hide();
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }

        // ============================================================
        // 刷新可见性 / 文案
        // ============================================================

        private void RefreshLabels()
        {
            if (_board == null) return;

            for (int i = 0; i < 4; i++)
                if (_kindButtons[i] != null)
                    _kindButtons[i].gameObject.SetActive(_step == Step.Kind);

            int max = _board.BardMaxTargetCount;
            for (int i = 0; i < 3; i++)
            {
                if (_countButtons[i] == null) continue;
                bool usable = (i + 1) <= max;
                _countButtons[i].gameObject.SetActive(_step == Step.Count && usable);
            }

            IReadOnlyList<PlayerState> cands = _board.BardCandidates;
            for (int i = 0; i < MaxCandidates; i++)
            {
                if (_candidateButtons[i] == null) continue;

                bool usable = _step == Step.Targets && i < cands.Count;
                _candidateButtons[i].gameObject.SetActive(usable);
                if (!usable) continue;

                PlayerState p = cands[i];
                string mark = _selected.Contains(p) ? "√ " : "";
                if (_candidateLabels[i] != null)
                    _candidateLabels[i].text = mark + p.Name;

                // 选满之后，没选中的就不给点了（避免玩家以为还能加）
                _candidateButtons[i].interactable = _selected.Contains(p)
                                                    || _selected.Count < _wantCount;
            }

            _confirm.gameObject.SetActive(_step == Step.Targets);
            _confirm.interactable = _selected.Count == _wantCount;
            _decline.gameObject.SetActive(true);

            switch (_step)
            {
                case Step.Kind:
                    _title.text = "吟游诗人：选择增幅 / 削弱哪一项";
                    break;
                case Step.Count:
                    _title.text = "选择目标数（决定强度）";
                    break;
                default:
                    _title.text = $"选择 {_wantCount} 个目标（已选 {_selected.Count}）"
                                + $"  强度 ±{BoardController.BardStrengthFor(_wantCount)}";
                    break;
            }
        }

        // ============================================================
        // 构件
        // ============================================================

        private Button MakeButton(string label, Vector2 position, Action onClick,
                                  float width = BtnW)
        {
            var go = new GameObject("Bard" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(_root, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, BtnH);
            rect.anchoredPosition = position;

            go.GetComponent<Image>().color = new Color(0.22f, 0.28f, 0.38f);

            Text text = MakeText(rect, "Label", Vector2.zero,
                                 new Vector2(width - 8f, BtnH), 18,
                                 TextAnchor.MiddleCenter, Color.white);
            text.text = label;

            var btn = go.GetComponent<Button>();
            BattleArt.Button(btn);
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        private Text MakeText(RectTransform parent, string name, Vector2 position,
                              Vector2 size, int fontSize, TextAnchor alignment, Color color)
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
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
