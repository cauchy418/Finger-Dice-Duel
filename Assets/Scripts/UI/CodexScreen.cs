using System;
using System.Collections.Generic;
using System.Text;
using FingerDiceDuel.Core;
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
        public string Id;
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

        /// <summary>
        /// **分组**（分类标题 + 条目）。用于"伤害类型"页：
        /// 魔法伤害 / 物理伤害两段，每条前面画一个**代码绘制的图标**。
        ///
        /// ⚠️ 有这个时，界面走分组渲染（不再用 <see cref="Interactions"/> 的纯文字）。
        /// </summary>
        public List<CodexGuideGroup> Groups;
        public string GuideNote;

        /// <summary>分组里的一个分类。</summary>
        public sealed class CodexGuideGroup
        {
            public string Title;
            public List<CodexGuideEntry> Entries = new List<CodexGuideEntry>();
        }

        /// <summary>分组里的一条：图标键 + 颜色 + 文字。</summary>
        public sealed class CodexGuideEntry
        {
            public string IconKey;
            public StatusEffect Status;
            public int Color;
            public string Text;
        }

        /// <summary>
        /// 清单**按两列排版**（左右各一条，成对一行）。
        ///
        /// ⚠️ 为什么需要：指南页的清单是"短条目 + 长说明"，
        ///    单列排下来会**顶到翻页按钮上** —— 玩家截图里「力场」那行与
        ///    「上一张 / 下一张 / 页码」叠在一起了。
        ///    两列既能塞下、也不至于太长。
        /// </summary>
        public bool TwoColumnItems;

        /// <summary>所属"守序 / 中立 / 混乱"。</summary>
        public string LawAxis;

        /// <summary>所属"善良 / 中立 / 邪恶"。</summary>
        public string GoodAxis;

        /// <summary>
        /// 这是**指南页**（不是角色卡）：状态一览 / 伤害类型指引。
        ///
        /// ⚠️ 用途：阵营筛选必须**保留**它 ——
        ///    否则玩家一点"守序"，指南页就消失了，再也找不到。
        ///    比"靠 LawAxis == null 判断"更明确，也不会被将来新增的
        ///    无阵营角色卡误伤。
        /// </summary>
        public bool IsGuide;
    }

    /// <summary>角色总览、徽记详情与独立的伤害/状态速查表。</summary>
    public sealed class CodexScreen
    {
        private RectTransform _root, _overviewRoot, _detailRoot, _guideRoot, _listContent, _bodyContent, _guideContent;
        private ScrollRect _bodyScroll, _guideScroll, _listScroll;
        private Text _heading, _title, _stats, _alignmentLabel, _body, _page, _filterLabel;
        private Image _emblem;
        private Button _filterToggle, _overviewButton, _prevButton, _nextButton;
        private GameObject _filterPanel;
        private readonly List<CodexCardData> _all = new List<CodexCardData>();
        private readonly List<CodexCardData> _filtered = new List<CodexCardData>();
        private readonly Dictionary<string, Button> _lawButtons = new Dictionary<string, Button>();
        private readonly Dictionary<string, Button> _goodButtons = new Dictionary<string, Button>();
        private int _index, _overviewPage;
        public const int OverviewPageSize = 12;
        public int OverviewPageCount => Mathf.Max(1, Mathf.CeilToInt(_filtered.Count / (float)OverviewPageSize));
        public int OverviewPageNumber => _overviewPage + 1;
        private string _lawAxis, _goodAxis;
        private bool _allowFilter = true, _overview = true;
        public event Action CloseRequested;
        public int PageNumber => _filtered.Count == 0 ? 0 : _index + 1;
        public int PageCount => _filtered.Count;
        public string CurrentName => Current == null ? "" : Current.NameZh;
        public string LawFilter => _lawAxis;
        public string GoodFilter => _goodAxis;
        public RectTransform Root => _root;
        public bool IsFilterPanelOpen => _filterPanel != null && _filterPanel.activeSelf;
        public bool IsOverview => _overview;
        private CodexCardData Current => _index >= 0 && _index < _filtered.Count ? _filtered[_index] : null;

        public void Build(RectTransform parent, IReadOnlyList<CodexCardData> cards)
        {
            _root = FrontEndArt.Rect(parent, "CodexScreen", Vector2.zero, Vector2.zero);
            FrontEndArt.Stretch(_root);
            _heading = FrontEndArt.Text(_root, "Heading", "角色图鉴", new Vector2(-620, 444), new Vector2(420, 70), 44, TextAnchor.MiddleLeft, true);
            var close = FrontEndArt.Button(_root, "Close", "返回", new Vector2(760, 440), new Vector2(180, 60));
            close.onClick.AddListener(() => CloseRequested?.Invoke());
            _overviewButton = FrontEndArt.Button(_root, "Overview", "图鉴总览", new Vector2(-200, 440), new Vector2(220, 60));
            _overviewButton.onClick.AddListener(() => { _overview = true; _overviewPage = _index / OverviewPageSize; RebuildNavigation(); Refresh(); });
            var divider = FrontEndArt.Image(_root, "Divider", "UI/divider_horizontal", new Vector2(0, 393), new Vector2(1710, 24));
            _overviewRoot = FrontEndArt.Rect(_root, "OverviewContent", Vector2.zero, Vector2.zero);
            _detailRoot = FrontEndArt.Rect(_root, "CharacterDetail", Vector2.zero, Vector2.zero);
            _guideRoot = FrontEndArt.Rect(_root, "GuideContent", Vector2.zero, Vector2.zero);
            BuildDetail();
            _guideScroll = FrontEndArt.Scroll(_guideRoot, "GuideScroll", new Vector2(0, -30), new Vector2(1660, 730), out _guideContent);
            _filterToggle = FrontEndArt.Button(_root, "FilterToggle", "阵营筛选", new Vector2(760, 359), new Vector2(180, 50), fontSize: 22);
            _filterToggle.onClick.AddListener(ToggleFilterPanel);
            _filterPanel = FrontEndArt.Image(_root, "FilterPanel", "UI/panel_tooltip", new Vector2(565, 220), new Vector2(570, 210)).gameObject;
            _filterPanel.GetComponent<Image>().raycastTarget = true;
            _filterLabel = FrontEndArt.Text(_filterPanel.transform, "FilterLabel", "", new Vector2(0, 64), new Vector2(520, 44), 18);
            BuildAxis(new[] { "守序", "中立", "混乱" }, _lawButtons, 8, true);
            BuildAxis(new[] { "善良", "中立", "邪恶" }, _goodButtons, -53, false);
            var prev = FrontEndArt.Button(_root, "Prev", "上一张", new Vector2(-250, -455), new Vector2(170, 56), fontSize: 22);
            _prevButton = prev;
            var next = FrontEndArt.Button(_root, "Next", "下一张", new Vector2(250, -455), new Vector2(170, 56), fontSize: 22);
            _nextButton = next;
            prev.onClick.AddListener(() => Step(-1));
            next.onClick.AddListener(() => Step(1));
            _page = FrontEndArt.Text(_root, "Page", "", new Vector2(0, -455), new Vector2(280, 48), 22);
            SetCards(cards, true);
        }

        private void BuildDetail()
        {
            FrontEndArt.Image(_detailRoot, "ListPanel", "UI/panel_sidebar", new Vector2(-700, -20), new Vector2(300, 780));
            _listScroll = FrontEndArt.Scroll(_detailRoot, "CharacterList", new Vector2(-700, -20), new Vector2(270, 746), out _listContent);
            _emblem = FrontEndArt.Emblem(_detailRoot, "LargeEmblem", "soldier", new Vector2(-385, 150), new Vector2(300, 300));
            FrontEndArt.Image(_detailRoot, "EmblemDivider", "UI/divider_horizontal", new Vector2(-385, -35), new Vector2(280, 24));
            FrontEndArt.Text(_detailRoot, "EmblemCaption", "命运由你掷下\n传奇在指间诞生", new Vector2(-385, -145), new Vector2(280, 150), 26, title: true);
            FrontEndArt.Image(_detailRoot, "DetailPanel", "UI/panel_detail", new Vector2(310, -20), new Vector2(1070, 780));
            _title = FrontEndArt.Text(_detailRoot, "Title", "", new Vector2(300, 321), new Vector2(950, 58), 34, TextAnchor.MiddleLeft, true);
            _alignmentLabel = FrontEndArt.Text(_detailRoot, "AlignmentLabel", "", new Vector2(300, 265), new Vector2(950, 36), 22, TextAnchor.MiddleLeft);
            _alignmentLabel.color = new Color(0.62f, 0.78f, 0.58f);
            _stats = FrontEndArt.Text(_detailRoot, "Stats", "", new Vector2(300, 215), new Vector2(950, 38), 24, TextAnchor.MiddleLeft);
            _stats.color = FrontEndArt.Gold;
            _bodyScroll = FrontEndArt.Scroll(_detailRoot, "BodyScroll", new Vector2(310, -105), new Vector2(970, 565), out _bodyContent);
            _body = FrontEndArt.Text(_bodyContent, "Body", "", Vector2.zero, new Vector2(930, 560), 22, TextAnchor.UpperLeft);
            _body.rectTransform.anchorMin = _body.rectTransform.anchorMax = new Vector2(0, 1);
            _body.rectTransform.pivot = new Vector2(0, 1);
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.lineSpacing = 1.18f;
        }

        private void BuildAxis(string[] axes, Dictionary<string, Button> buttons, float y, bool law)
        {
            for (int i = 0; i < axes.Length; i++)
            {
                string axis = axes[i];
                var b = FrontEndArt.Button(_filterPanel.transform, "Filter_" + axis, axis,
                    new Vector2((i - 1) * 172, y), new Vector2(156, 48), fontSize: 22);
                b.onClick.AddListener(() => ToggleAxis(axis, law));
                buttons[axis] = b;
            }
        }

        public void SetCards(IReadOnlyList<CodexCardData> cards, bool allowFilter)
        {
            _all.Clear();
            if (cards != null) for (int i = 0; i < cards.Count; i++) if (cards[i] != null) _all.Add(cards[i]);
            _allowFilter = allowFilter;
            _overview = allowFilter;
            _lawAxis = _goodAxis = null;
            _filterPanel.SetActive(false);
            _filterToggle.gameObject.SetActive(allowFilter);
            _overviewButton.gameObject.SetActive(allowFilter);
            ApplyFilter();
        }

        public void ToggleFilterPanel()
        {
            if (_allowFilter) { _filterPanel.SetActive(!_filterPanel.activeSelf); _filterPanel.transform.SetAsLastSibling(); }
        }
        public void ToggleAxis(string axis, bool isLaw)
        {
            if (!_allowFilter) return;
            if (isLaw) _lawAxis = _lawAxis == axis ? null : axis;
            else _goodAxis = _goodAxis == axis ? null : axis;
            ApplyFilter();
        }
        public void ApplyFilter()
        {
            _filtered.Clear();
            foreach (var c in _all)
                if (c.IsGuide || ((_lawAxis == null || c.LawAxis == _lawAxis) && (_goodAxis == null || c.GoodAxis == _goodAxis))) _filtered.Add(c);
            _index = 0;
            _overviewPage = 0;
            RebuildNavigation();
            Refresh();
        }
        public void Step(int delta)
        {
            if (_filtered.Count == 0) return;
            if (_overview)
            {
                _overviewPage = ((_overviewPage + delta) % OverviewPageCount + OverviewPageCount) % OverviewPageCount;
                RebuildNavigation();
                Refresh();
                return;
            }
            _index = (_index + delta) % _filtered.Count;
            if (_index < 0) _index += _filtered.Count;
            _overview = false;
            Refresh();
        }
        public void GoTo(int index)
        {
            if (_filtered.Count == 0) return;
            _index = Mathf.Clamp(index, 0, _filtered.Count - 1);
            _overview = false;
            Refresh();
        }

        private static void Clear(RectTransform root)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                root.GetChild(i).gameObject.SetActive(false);
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);
            }
        }
        private void RebuildNavigation()
        {
            Clear(_overviewRoot);
            Clear(_listContent);
            if (!_allowFilter) return;
            FrontEndArt.Image(_overviewRoot, "GridPanel", "UI/panel_content", new Vector2(-250, -30), new Vector2(1210, 750));
            var parchment = FrontEndArt.Image(_overviewRoot, "Parchment", "Illustrations/codex_parchment", new Vector2(650, -45), new Vector2(420, 690), false);
            FrontEndArt.Image(parchment.transform, "Book", "Illustrations/codex_book_dice", new Vector2(0, 190), new Vector2(340, 225), false);
            var note = FrontEndArt.Text(parchment.transform, "Note", "角色图鉴\n\n收录 " + _filtered.Count + " 名角色\n\n点击徽记查看属性、技能、操作指导与特殊交互。\n\n按阵营筛选，\n寻找你的同伴。", new Vector2(0, -90), new Vector2(305, 340), 25, TextAnchor.UpperLeft, true);
            note.color = new Color(0.23f, 0.15f, 0.08f);
            _listContent.sizeDelta = new Vector2(246, Mathf.Max(746, _filtered.Count * 76 + 10));
            _listContent.anchoredPosition = Vector2.zero;
            for (int i = 0; i < _filtered.Count; i++)
            {
                var c = _filtered[i];
                int index = i;
                int pageIndex = i - _overviewPage * OverviewPageSize;
                if (pageIndex >= 0 && pageIndex < OverviewPageSize)
                {
                    var tile = FrontEndArt.Button(_overviewRoot, "CodexTile" + i, "",
                        new Vector2(-670 + pageIndex % 4 * 280, 208 - pageIndex / 4 * 236),
                        new Vector2(236, 218), "roster_tile");
                    FrontEndArt.RoleTile(tile, c.Id, c.NameZh, 27);
                    tile.onClick.AddListener(() => GoTo(index));
                }
                var row = FrontEndArt.Button(_listContent, "RoleRow" + i, c.NameZh,
                    new Vector2(122, -40 - i * 76), new Vector2(244, 68), "list_row", 20);
                ((RectTransform)row.transform).anchorMin = ((RectTransform)row.transform).anchorMax = new Vector2(0, 1);
                FrontEndArt.Emblem(row.transform, "Emblem", c.Id, new Vector2(-90, 0), new Vector2(54, 54));
                var rowLabel = row.GetComponentInChildren<Text>();
                rowLabel.rectTransform.anchoredPosition = new Vector2(28, 0);
                rowLabel.rectTransform.sizeDelta = new Vector2(170, 56);
                row.onClick.AddListener(() => GoTo(index));
            }
        }

        private void Refresh()
        {
            bool guide = Current != null && Current.IsGuide;
            _overviewRoot.gameObject.SetActive(_overview && !guide);
            _detailRoot.gameObject.SetActive(!_overview && !guide);
            _guideRoot.gameObject.SetActive(guide);
            _heading.text = guide ? "秘法手册" : _overview ? "图鉴总览" : "角色图鉴";
            _page.text = _overview ? OverviewPageNumber + " / " + OverviewPageCount + " · " + _filtered.Count + " 名角色" : PageNumber + " / " + PageCount;
            _prevButton.GetComponentInChildren<Text>().text = _overview ? "上一页" : "上一张";
            _nextButton.GetComponentInChildren<Text>().text = _overview ? "下一页" : "下一张";
            _filterLabel.text = "阵营：" + (_lawAxis ?? "任意") + " · " + (_goodAxis ?? "任意") + "（再点一次取消）";
            foreach (var kv in _lawButtons) FrontEndArt.StyleButton(kv.Value, "tab", kv.Key == _lawAxis);
            foreach (var kv in _goodButtons) FrontEndArt.StyleButton(kv.Value, "tab", kv.Key == _goodAxis);
            for (int i = 0; i < _listContent.childCount; i++)
            {
                var b = _listContent.GetChild(i).GetComponent<Button>();
                if (b != null) FrontEndArt.StyleButton(b, "list_row", b.name == "RoleRow" + _index);
            }
            if (_overview) return;
            var c = Current;
            if (c == null)
            {
                _title.text = "没有符合筛选的角色";
                _body.text = _stats.text = _alignmentLabel.text = "";
                _emblem.gameObject.SetActive(false);
                return;
            }
            if (guide) { RenderGuide(c); return; }
            _title.text = c.NameZh + "  " + c.NameEn;
            _alignmentLabel.text = c.Alignment;
            _stats.text = "生命 " + c.MaxHealth + "　攻 " + Signed(c.Attack) + "　防 " + Signed(c.Defense) + "　距离 " + (c.Range == int.MaxValue ? "无限" : c.Range.ToString());
            _emblem.gameObject.SetActive(true);
            _emblem.sprite = BattleArt.Get("Classes/" + c.Id);
            var sb = new StringBuilder();
            Section(sb, "技能", c.ShortSkill);
            Section(sb, "技能详解", c.DetailSkill);
            Section(sb, "操作指导", c.PlayGuide);
            Section(sb, "如何针对", c.Counter);
            if (c.Interactions != null && c.Interactions.Count > 0) Section(sb, "与其他角色的特殊交互", string.Join("\n", c.Interactions));
            _body.text = sb.ToString();
            float height = Mathf.Max(565, _body.preferredHeight + 20);
            _body.rectTransform.sizeDelta = new Vector2(930, height);
            _bodyContent.sizeDelta = new Vector2(946, height);
            _bodyScroll.StopMovement();
            _bodyContent.anchoredPosition = Vector2.zero;
            _listScroll.verticalNormalizedPosition = Mathf.Clamp01(1 - (_index * 76 - 330) / Mathf.Max(1, _listContent.sizeDelta.y - 746));
        }

        private void RenderGuide(CodexCardData c)
        {
            Clear(_guideContent);
            float y = 0;
            TopText(_guideContent, "GuideTitle", c.NameZh + "   " + c.NameEn, 0, y, 1580, 54, 34, true);
            y += 82;
            if (!string.IsNullOrEmpty(c.GuideNote))
            {
                var note = TopText(_guideContent, "GuideNote", c.GuideNote, 0, y, 1560, 100, 23);
                float noteHeight = Mathf.Max(40, note.preferredHeight);
                note.rectTransform.sizeDelta = new Vector2(1560, noteHeight);
                y += noteHeight + 28;
            }
            if (c.Groups != null && c.Groups.Count > 0)
            {
                foreach (var group in c.Groups)
                {
                    TopText(_guideContent, "GroupTitle", group.Title, 0, y, 1580, 40, 28, true);
                    y += 60;
                    for (int i = 0; i < group.Entries.Count; i += 2)
                    {
                        float rowHeight = 128;
                        for (int col = 0; col < 2 && i + col < group.Entries.Count; col++)
                        {
                            var entry = group.Entries[i + col];
                            float x = col * 810;
                            string key = entry.IconKey;
                            string resource = entry.Status == StatusEffect.None ? "Damage/" + key
                                : "Status/" + BattleArt.StatusKey(entry.Status);
                            var icon = FrontEndArt.Rect(_guideContent, "PaintedIcon_" + key, new Vector2(x + 56, -y - 56), new Vector2(112, 112)).gameObject.AddComponent<Image>();
                            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
                            icon.sprite = BattleArt.Get(resource);
                            icon.preserveAspect = true;
                            icon.raycastTarget = false;
                            var text = TopText(_guideContent, "Entry_" + key, entry.Text, x + 134, y + 6, 640, 112, 23);
                            float h = Mathf.Max(112, text.preferredHeight);
                            text.rectTransform.sizeDelta = new Vector2(640, h);
                            rowHeight = Mathf.Max(rowHeight, h + 28);
                        }
                        y += rowHeight;
                    }
                    y += 30;
                }
            }
            else if (c.Interactions != null)
            {
                var text = TopText(_guideContent, "GuideText", string.Join("\n\n", c.Interactions), 0, y, 1560, 600, 24);
                y += text.preferredHeight + 40;
                text.rectTransform.sizeDelta = new Vector2(1560, y);
            }
            _guideContent.sizeDelta = new Vector2(1636, Mathf.Max(730, y));
            _guideScroll.StopMovement();
            _guideContent.anchoredPosition = Vector2.zero;
        }

        private static Text TopText(Transform parent, string name, string value, float x, float y, float w, float h, int size, bool title = false)
        {
            var text = FrontEndArt.Text(parent, name, value, new Vector2(x, -y), new Vector2(w, h), size, TextAnchor.UpperLeft, title);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = text.rectTransform.pivot = new Vector2(0, 1);
            return text;
        }
        private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();
        private static void Section(StringBuilder sb, string title, string body)
        {
            if (string.IsNullOrEmpty(body)) return;
            sb.AppendLine("<color=#E2C187>" + title + "</color>");
            sb.AppendLine(body);
            sb.AppendLine();
        }
        public void SetVisible(bool visible) { if (_root != null) _root.gameObject.SetActive(visible); }
    }
}
