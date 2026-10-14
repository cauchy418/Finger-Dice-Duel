using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 图鉴里一名角色的**显示数据**（纯字符串 / 数字，UI 层自己的类型）。
    ///
    /// ⚠️ 为什么 UI 层不直接用 Characters 层的 CodexEntry：
    ///    分层铁律规定 UI 不能引用 Characters。
    ///    所以由 <see cref="GameBootstrap"/>（唯一允许同时看到两边的入口）
    ///    把 CodexEntry 转成这个扁平结构再传进来。
    /// </summary>
    public sealed class CodexCardData
    {
        public string NameZh;
        public string NameEn;
        public string Alignment;
        public int MaxHealth;
        public int Attack;
        public int Defense;
        public int Range;

        /// <summary>一句话技能描述。</summary>
        public string ShortSkill;

        /// <summary>详细技能描述。</summary>
        public string DetailSkill;

        /// <summary>操作指导。</summary>
        public string PlayGuide;

        /// <summary>如何针对。</summary>
        public string Counter;

        /// <summary>与其他角色的特殊交互（每条一行）。</summary>
        public List<string> Interactions = new List<string>();

        /// <summary>所属"守序 / 中立 / 混乱"。</summary>
        public string LawAxis;

        /// <summary>所属"善良 / 中立 / 邪恶"。</summary>
        public string GoodAxis;
    }

    /// <summary>
    /// **角色图鉴**：可翻页卡片 + 阵营筛选。
    ///
    /// 玩家给的版面（自上而下）：
    ///   名称（中英文）→ 生命 / 攻 / 防 / 距离 → 技能（一句）
    ///   → 技能详细 → 操作指导 → 如何针对 → 与其他角色的特殊交互
    ///
    /// 筛选：点守序 / 中立 / 混乱 或 善良 / 中立 / 邪恶，
    /// 两个轴可**依次叠加**（再点一次取消）。
    /// </summary>
    public sealed class CodexScreen
    {
        private RectTransform _root;
        private Font _font;

        private readonly List<CodexCardData> _all = new List<CodexCardData>();
        private readonly List<CodexCardData> _filtered = new List<CodexCardData>();

        private int _index;
        private string _lawAxis;      // null = 不过滤
        private string _goodAxis;

        private Text _title;
        private Text _stats;
        private Text _body;
        private Text _page;
        private Text _filterLabel;
        private Text _alignmentLabel;

        private readonly Dictionary<string, Image> _lawButtons =
            new Dictionary<string, Image>();
        private readonly Dictionary<string, Image> _goodButtons =
            new Dictionary<string, Image>();

        /// <summary>关闭图鉴（返回上一屏）。</summary>
        public event Action CloseRequested;

        public int PageNumber => _filtered.Count == 0 ? 0 : _index + 1;
        public int PageCount => _filtered.Count;
        public string CurrentName => Current == null ? "" : Current.NameZh;
        public string LawFilter => _lawAxis;
        public string GoodFilter => _goodAxis;

        private CodexCardData Current
            => _index >= 0 && _index < _filtered.Count ? _filtered[_index] : null;

        private static readonly string[] LawAxes = { "守序", "中立", "混乱" };
        private static readonly string[] GoodAxes = { "善良", "中立", "邪恶" };

        // 面板是 1520 x 860（与 2v2 先攻界面一致）
        private const float PanelW = 1520f;
        private const float PanelH = 860f;

        public RectTransform Root => _root;

        public void Build(RectTransform parent, IReadOnlyList<CodexCardData> cards)
        {
            _all.Clear();
            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null) _all.Add(cards[i]);
            }

            _font = UiFont.Get();

            var go = new GameObject("CodexScreen", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);

            var panel = new GameObject("Panel", typeof(Image));
            panel.transform.SetParent(_root, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelW, PanelH);
            panelRect.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.11f, 0.13f, 0.17f);

            float halfW = PanelW * 0.5f;
            float halfH = PanelH * 0.5f;

            // ---- 标题（含中英文与阵营）----
            _title = MakeText(panelRect, "Title",
                              new Vector2(0f, halfH - 46f), new Vector2(PanelW - 260f, 52f),
                              36, TextAnchor.MiddleLeft, new Color(0.98f, 0.86f, 0.45f));

            // ---- 关闭 ----
            Button close = MakeButton(panelRect, "Close",
                                      new Vector2(halfW - 90f, halfH - 46f),
                                      new Vector2(140f, 48f), "返回", 22);
            close.onClick.AddListener(() => CloseRequested?.Invoke());

            // ---- 阵营筛选（玩家要求：移到**右侧**，
            //      与名称、介绍放在一起）----
            //
            // ⚠️ 原来它在左侧靠上，与标题同一行区域，
            //    看起来像"属于标题"而不像"属于角色"。
            //    现在整体靠右，与名称/介绍形成一列。
            _alignmentLabel = MakeText(panelRect, "AlignmentLabel",
                                       new Vector2(330f, halfH - 96f),
                                       new Vector2(360f, 34f),
                                       24, TextAnchor.MiddleRight,
                                       new Color(0.62f, 0.82f, 0.60f));

            _filterLabel = MakeText(panelRect, "FilterLabel",
                                    new Vector2(330f, halfH - 138f),
                                    new Vector2(360f, 28f),
                                    17, TextAnchor.MiddleRight,
                                    new Color(0.70f, 0.74f, 0.82f));

            BuildAxisRow(panelRect, LawAxes, _lawButtons, halfH - 186f, true);
            BuildAxisRow(panelRect, GoodAxes, _goodButtons, halfH - 238f, false);

            // ---- 属性行 ----
            _stats = MakeText(panelRect, "Stats",
                              new Vector2(0f, halfH - 300f),
                              new Vector2(PanelW - 120f, 34f),
                              23, TextAnchor.MiddleLeft,
                              new Color(0.95f, 0.85f, 0.55f));

            // ---- 正文（占满下半部分，自动换行）----
            _body = MakeText(panelRect, "Body",
                             new Vector2(0f, -80f),
                             new Vector2(PanelW - 120f, halfH - 30f),
                             18, TextAnchor.UpperLeft,
                             new Color(0.88f, 0.90f, 0.94f));

            // ---- 底部：翻页 ----
            Button prev = MakeButton(panelRect, "Prev",
                                     new Vector2(-halfW + 130f, -halfH + 46f),
                                     new Vector2(150f, 48f), "上一张", 22);
            prev.onClick.AddListener(() => Step(-1));

            Button next = MakeButton(panelRect, "Next",
                                     new Vector2(-halfW + 300f, -halfH + 46f),
                                     new Vector2(150f, 48f), "下一张", 22);
            next.onClick.AddListener(() => Step(1));

            // 页码放在"下一张"的**右边** ——
            // 我第一版把它放在 -halfW+440、宽 200，
            // 而"下一张"的右边缘在 -halfW+375，
            // 所以两者**重叠**（玩家实测："1/18 和下一张重合"）。
            // 现在改成从 -halfW+390 开始，且居左对齐。
            _page = MakeText(panelRect, "Page",
                             new Vector2(-halfW + 500f, -halfH + 46f),
                             new Vector2(200f, 40f),
                             21, TextAnchor.MiddleLeft,
                             new Color(0.75f, 0.78f, 0.85f));

            ApplyFilter();
        }

        private void BuildAxisRow(RectTransform parent, string[] axes,
                                  Dictionary<string, Image> store, float y, bool isLaw)
        {
            const float width = 120f;
            const float gap = 12f;
            float totalW = axes.Length * width + (axes.Length - 1) * gap;

            // 整行靠**右**对齐：最右一个按钮的右边缘对齐 510（与名称/介绍同一右边界）
            const float rightEdge = 510f;
            float x = rightEdge - totalW + width * 0.5f;

            for (int i = 0; i < axes.Length; i++)
            {
                string axis = axes[i];
                Button b = MakeButton(parent, "Filter_" + axis,
                                      new Vector2(x, y), new Vector2(width, 42f),
                                      axis, 21);

                b.onClick.AddListener(() => ToggleAxis(axis, isLaw));
                store[axis] = b.GetComponent<Image>();

                x += width + gap;
            }
        }

        /// <summary>点某个筛选轴：未选中则选中、已选中则取消。</summary>
        public void ToggleAxis(string axis, bool isLaw)
        {
            if (isLaw) _lawAxis = _lawAxis == axis ? null : axis;
            else _goodAxis = _goodAxis == axis ? null : axis;

            ApplyFilter();
        }

        /// <summary>按当前两个轴重新筛选（玩家要求：两轴可依次叠加）。</summary>
        public void ApplyFilter()
        {
            _filtered.Clear();

            for (int i = 0; i < _all.Count; i++)
            {
                CodexCardData c = _all[i];
                if (_lawAxis != null && c.LawAxis != _lawAxis) continue;
                if (_goodAxis != null && c.GoodAxis != _goodAxis) continue;

                _filtered.Add(c);
            }

            _index = 0;
            Refresh();
        }

        /// <summary>翻页（循环）。</summary>
        public void Step(int delta)
        {
            if (_filtered.Count == 0) return;

            _index = (_index + delta) % _filtered.Count;
            if (_index < 0) _index += _filtered.Count;

            Refresh();
        }

        /// <summary>跳到第 index 张（0 起）。</summary>
        public void GoTo(int index)
        {
            if (_filtered.Count == 0) return;

            _index = Mathf.Clamp(index, 0, _filtered.Count - 1);
            Refresh();
        }

        private void Refresh()
        {
            Highlight(_lawButtons, _lawAxis);
            Highlight(_goodButtons, _goodAxis);

            if (_filterLabel != null)
            {
                string law = string.IsNullOrEmpty(_lawAxis) ? "守序/中立/混乱" : _lawAxis;
                string good = string.IsNullOrEmpty(_goodAxis) ? "善良/中立/邪恶" : _goodAxis;
                _filterLabel.text = $"阵营筛选：{law}　·　{good}（再点一次取消）";
            }

            CodexCardData c = Current;

            if (c == null)
            {
                if (_title != null) _title.text = "图鉴：没有符合筛选的角色";
                if (_stats != null) _stats.text = "";
                if (_body != null) _body.text = "";
                if (_page != null) _page.text = "0 / 0";
                return;
            }

            if (_title != null)
                _title.text = $"{c.NameZh}　{c.NameEn}";

            if (_alignmentLabel != null)
                _alignmentLabel.text = c.Alignment;

            if (_stats != null)
                _stats.text = $"生命 {c.MaxHealth}　　攻 {Signed(c.Attack)}　　"
                            + $"防 {Signed(c.Defense)}　　距离 {c.Range}";

            if (_body != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine("【技能】");
                sb.AppendLine(c.ShortSkill);
                sb.AppendLine();
                sb.AppendLine("【技能详细】");
                sb.AppendLine(c.DetailSkill);
                sb.AppendLine();
                sb.AppendLine("【操作指导】");
                sb.AppendLine(c.PlayGuide);
                sb.AppendLine();
                sb.AppendLine("【如何针对】");
                sb.AppendLine(c.Counter);

                if (c.Interactions != null && c.Interactions.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("【与其他角色的特殊交互】");
                    for (int i = 0; i < c.Interactions.Count; i++)
                        sb.AppendLine("· " + c.Interactions[i]);
                }

                _body.text = sb.ToString();
            }

            if (_page != null) _page.text = $"{PageNumber} / {PageCount}";
        }

        private static void Highlight(Dictionary<string, Image> buttons, string active)
        {
            foreach (KeyValuePair<string, Image> kv in buttons)
            {
                if (kv.Value == null) continue;

                kv.Value.color = kv.Key == active
                    ? new Color(0.72f, 0.55f, 0.22f)      // 选中：琥珀色
                    : new Color(0.28f, 0.30f, 0.36f);     // 未选：石板灰
            }
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        // ============================================================
        // 小工具（与 TwoOnTwoInitiativeScreen 的写法保持一致）
        // ============================================================

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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(RectTransform parent, string name, Vector2 position,
                                  Vector2 size, string label, int fontSize)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            go.GetComponent<Image>().color = new Color(0.28f, 0.30f, 0.36f);

            Text text = MakeText(rect, "Label", Vector2.zero, size, fontSize,
                                 TextAnchor.MiddleCenter, Color.white);
            text.text = label;

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
