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

        /// <summary>阵营筛选的开关按钮（在返回键下面）。</summary>
        private Button _filterToggle;

        /// <summary>筛选面板（点开才显示）。</summary>
        private GameObject _filterPanel;

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

        /// <summary>
        /// **切换显示的卡片集**。
        ///
        /// 玩家要求："魔法球和图鉴入口进入是一个东西 —— 把伤害类型指引和状态一览
        /// 单独放进魔法球入口"。所以现在两套：
        ///   · 图鉴入口   -> 全部**角色卡**（可阵营筛选）
        ///   · 魔法球入口 -> 只有**指南页**（伤害类型指引 / 状态一览，无筛选意义）
        ///
        /// <paramref name="allowFilter"/> 为 false 时把阵营筛选整块**隐藏** ——
        /// 对指南页做阵营筛选毫无意义，留着只会让人困惑。
        /// </summary>
        public void SetCards(IReadOnlyList<CodexCardData> cards, bool allowFilter)
        {
            _all.Clear();
            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                    if (cards[i] != null) _all.Add(cards[i]);
            }

            _allowFilter = allowFilter;
            if (_filterPanel != null) _filterPanel.SetActive(allowFilter);

            _lawAxis = null;
            _goodAxis = null;
            ApplyFilter();
        }

        private bool _allowFilter = true;

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


            // ---- 标题（含中英文与阵营）----
            _title = MakeText(panelRect, "Title",
                              Vector2.zero, new Vector2(340f, 52f),
                              36, TextAnchor.MiddleLeft, new Color(0.98f, 0.86f, 0.45f));
            // ⚠️ 标题框加宽到 460：英文名有长的（Curseweaver、ArcaneArcher），
            //    框子太窄会被换行成两行（玩家截图里就是这样）。
            Anchor(_title.rectTransform, new Vector2(0f, 1f), 60f, -24f, 460f, 52f);
            _title.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ---- 关闭 ----
            Button close = MakeButton(panelRect, "Close",
                                      Vector2.zero, new Vector2(140f, 48f), "返回", 22);
            Anchor((RectTransform)close.transform, new Vector2(1f, 1f),
                   -60f, -26f, 140f, 48f);
            close.onClick.AddListener(() => CloseRequested?.Invoke());

            // ---- 阵营筛选（玩家要求：移到**右侧**，
            //      与名称、介绍放在一起）----
            //
            // ⚠️ 原来它在左侧靠上，与标题同一行区域，
            //    看起来像"属于标题"而不像"属于角色"。
            //    现在整体靠右，与名称/介绍形成一列。
            // ---- 阵营标签（紧跟名称右侧）----
            // 玩家要求：阵营**移到名字旁边**（而不是右侧单独一行）
            // 玩家要求：阵营放在**英文名后面**。
            // 标题框宽 340、从左边 60 开始，所以阵营从 60+340 = 400 开始。
            _alignmentLabel = MakeText(panelRect, "AlignmentLabel",
                                       Vector2.zero, new Vector2(300f, 44f),
                                       26, TextAnchor.MiddleLeft,
                                       new Color(0.62f, 0.82f, 0.60f));
            Anchor(_alignmentLabel.rectTransform, new Vector2(0f, 1f),
                   526f, -26f, 260f, 44f);
            _alignmentLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ---- 阵营筛选（玩家要求：在返回键下面做一个按钮，
            //      点开后才跳出筛选界面）----
            //
            // ⚠️ 原来是常驻的两行按钮，占掉一大块版面；
            //    现在收起来，只在点开时显示。
            _filterToggle = MakeButton(panelRect, "FilterToggle",
                                       Vector2.zero, new Vector2(140f, 46f),
                                       "阵营筛选", 20);
            // 玩家要求：放在**返回的正下方**（同宽、右对齐）
            Anchor((RectTransform)_filterToggle.transform, new Vector2(1f, 1f),
                   -60f, -84f, 140f, 46f);
            _filterToggle.onClick.AddListener(ToggleFilterPanel);

            // 筛选面板：一个容器，里面放提示 + 两行按钮
            _filterPanel = new GameObject("FilterPanel", typeof(RectTransform));
            _filterPanel.transform.SetParent(panelRect, false);
            RectTransform fp = _filterPanel.GetComponent<RectTransform>();
            fp.anchorMin = fp.anchorMax = new Vector2(0.5f, 0.5f);
            fp.pivot = new Vector2(0.5f, 0.5f);
            fp.sizeDelta = new Vector2(420f, 160f);
            Anchor(fp, new Vector2(1f, 1f), -230f, -142f, 420f, 160f);

            _filterLabel = MakeText(fp, "FilterLabel",
                                    new Vector2(0f, 62f),
                                    new Vector2(400f, 26f),
                                    16, TextAnchor.MiddleCenter,
                                    new Color(0.70f, 0.74f, 0.82f));

            BuildAxisRow(fp, LawAxes, _lawButtons, 18f, true);
            BuildAxisRow(fp, GoodAxes, _goodButtons, -32f, false);

            _filterPanel.SetActive(false);

            // ---- 属性行 ----
            // 玩家实测：属性行与技能描述**重叠**了 ——
            //    因为上一轮我把属性行上移、却把正文一起提了上来。
            //    现在两者都用**锚点定位**，间距是固定的。
            _stats = MakeText(panelRect, "Stats",
                              Vector2.zero, new Vector2(PanelW - 120f, 36f),
                              24, TextAnchor.MiddleLeft,
                              new Color(0.95f, 0.85f, 0.55f));
            Anchor(_stats.rectTransform, new Vector2(0f, 1f),
                   60f, -92f, PanelW - 120f, 36f);

            // ---- 正文（占满下半部分，自动换行）----
            // 正文用**固定矩形 + 左上对齐**，起点在属性行下方 ——
            // 这样"属性行"与"技能"之间的间距是可算的，不会再压在一起。
            _body = MakeText(panelRect, "Body",
                             Vector2.zero, new Vector2(PanelW - 120f, 380f),
                             (int)BodyFontSize, TextAnchor.UpperLeft,
                             new Color(0.88f, 0.90f, 0.94f));

            // 正文用**锚点拉伸**：左上 (60, -136)，右下 (-60, 96)
            // 这样它与属性行之间的间距是固定的，不会再压在一起。
            RectTransform bodyRect = _body.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.pivot = new Vector2(0f, 1f);
            bodyRect.offsetMin = new Vector2(60f, 96f);
            bodyRect.offsetMax = new Vector2(-60f, -136f);

            // ---- 底部：翻页 ----
            Button prev = MakeButton(panelRect, "Prev",
                                     Vector2.zero, new Vector2(150f, 48f), "上一张", 22);
            Anchor((RectTransform)prev.transform, new Vector2(0f, 0f),
                   60f, 34f, 150f, 48f);
            prev.onClick.AddListener(() => Step(-1));

            Button next = MakeButton(panelRect, "Next",
                                     Vector2.zero, new Vector2(150f, 48f), "下一张", 22);
            Anchor((RectTransform)next.transform, new Vector2(0f, 0f),
                   224f, 34f, 150f, 48f);
            next.onClick.AddListener(() => Step(1));

            // 页码放在"下一张"右边（用锚点定位）
            _page = MakeText(panelRect, "Page",
                             Vector2.zero, new Vector2(200f, 40f),
                             21, TextAnchor.MiddleLeft,
                             new Color(0.75f, 0.78f, 0.85f));
            Anchor(_page.rectTransform, new Vector2(0f, 0f),
                   390f, 38f, 200f, 40f);

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

        /// <summary>
        /// 切换筛选面板的显示（玩家要求：点按钮后才跳出筛选界面）。
        /// </summary>
        public void ToggleFilterPanel()
        {
            if (_filterPanel == null) return;

            _filterPanel.SetActive(!_filterPanel.activeSelf);
        }

        /// <summary>筛选面板当前是否打开（测试用）。</summary>
        public bool IsFilterPanelOpen
            => _filterPanel != null && _filterPanel.activeSelf;

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

                // ⚠️ **指南页（状态一览 / 伤害类型）永远保留** ——
                //    它们不是某个角色，不该被"守序 / 善良"这类筛选排除掉。
                //    排除它们会让玩家一旦点了筛选就再也找不到状态说明。
                if (!c.IsGuide)
                {
                    if (_lawAxis != null && c.LawAxis != _lawAxis) continue;
                    if (_goodAxis != null && c.GoodAxis != _goodAxis) continue;
                }

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
                bool noLaw = string.IsNullOrEmpty(_lawAxis);
                bool noGood = string.IsNullOrEmpty(_goodAxis);

                // 玩家要求：面板收起来了，所以提示要说清楚
                // "当前有没有筛选"与"怎么取消"。
                _filterLabel.text = (noLaw && noGood)
                    ? "未筛选（点下面两行选阵营，再点一次取消）"
                    : $"已筛选：{(noLaw ? "任意" : _lawAxis)}　·　"
                      + $"{(noGood ? "任意" : _goodAxis)}（再点一次取消）";
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

            // ⚠️ **内容为空就不显示**（玩家要求；指南页是纯速查表）：
            //    · 归属标签（"法师体系"）
            //    · 属性行（生命 / 攻 / 防 / 距离）
            //    · 【技能】【技能详细】【操作指导】【如何针对】四个小节
            //    这样同一套卡片结构既能放角色卡、也能放中立速查表
            //    （玩家原话："这不仅是法师要用，日后做 pve 冒险也很重要"）。
            if (_alignmentLabel != null)
                _alignmentLabel.text = c.Alignment ?? "";
            if (_alignmentLabel != null)
                _alignmentLabel.gameObject.SetActive(!string.IsNullOrEmpty(c.Alignment));

            bool hasStats = c.MaxHealth > 0 || c.Attack != 0 || c.Defense != 0 || c.Range != 0;

            if (_stats != null)
            {
                _stats.text = hasStats
                    ? $"生命 {c.MaxHealth}　　攻 {Signed(c.Attack)}　　"
                      + $"防 {Signed(c.Defense)}　　距离 {RangeText(c.Range)}"
                    : "";
            }

            if (_body != null)
            {
                var sb = new StringBuilder();

                AppendSection(sb, "【技能】", c.ShortSkill);
                AppendSection(sb, "【技能详细】", c.DetailSkill);
                AppendSection(sb, "【操作指导】", c.PlayGuide);
                AppendSection(sb, "【如何针对】", c.Counter);

                if (c.Interactions != null && c.Interactions.Count > 0)
                {
                    // ⚠️ 有分组 -> 走**分组渲染**（分类标题 + 每条一个图标）
                    if (c.Groups != null && c.Groups.Count > 0)
                    {
                        RenderGroups(c.Groups);
                        if (_page != null) _page.text = $"{PageNumber} / {PageCount}";
                        return;
                    }

                    // ⚠️ 指南页（两列）走**独立的两个 Text 列**，不走正文。
                    if (c.TwoColumnItems)
                    {
                        RenderTwoColumns(c.Interactions);
                        if (_page != null) _page.text = $"{PageNumber} / {PageCount}";
                        return;
                    }
                    // 角色卡：仍然走正文（单列）
                    sb.AppendLine();
                    sb.AppendLine("【与其他角色的特殊交互】");

                    for (int i = 0; i < c.Interactions.Count; i++)
                        sb.AppendLine("· " + c.Interactions[i]);
                }

                _body.text = sb.ToString();
            }

            // 单列卡片：把两列收起来（否则翻到角色卡时两列还留在屏幕上）
            HideColumnTexts();

            if (_page != null) _page.text = $"{PageNumber} / {PageCount}";
        }

        /// <summary>
        /// **两列排版：左右各用一个 Text 组件。**
        ///
        /// ⚠️⚠️ 为什么不用"一个 Text + 补空格对齐"（我原来的做法）：
        ///    补的空格是**离散**的 —— 凑到"左列宽 + 间隙"时实际宽度会略微超出，
        ///    于是那一行**折行**，左列文字在新的一行又出现一次。
        ///    玩家看到的正是这个："雷鸣显示了两次描述、毒素显示了两次描述"。
        ///
        ///    拆成两个 Text 之后，列宽由**布局**决定：
        ///      · 左列一条、右列一条，各占自己的矩形；
        ///      · 不可能把另一列挤到下一行；
        ///      · 对齐由列宽保证，不依赖字体测量精度。
        /// </summary>
        private void RenderTwoColumns(IReadOnlyList<string> interactions)
        {
            EnsureColumnTexts();

            var left = new StringBuilder();
            var right = new StringBuilder();

            for (int i = 0; i < interactions.Count; i += 2)
            {
                left.AppendLine(interactions[i]);
                if (i + 1 < interactions.Count) right.AppendLine(interactions[i + 1]);
            }

            if (_colLeft != null)
            {
                _colLeft.gameObject.SetActive(true);
                _colLeft.text = left.ToString();
            }

            if (_colRight != null)
            {
                _colRight.gameObject.SetActive(true);
                _colRight.text = right.ToString();
            }

            // 单列正文在这一页要让位
            if (_body != null) _body.text = "";
        }

        private Text _colLeft, _colRight;

        // ============================================================
        // 分组渲染（分类标题 + 每条一个代码绘制的图标）
        // ============================================================

        private readonly List<GameObject> _groupRows = new List<GameObject>();

        /// <summary>图标显示尺寸（像素）。</summary>
        private const float IconSize = 26f;

        /// <summary>每一行的行高。</summary>
        private const float RowHeight = 30f;

        /// <summary>
        /// 按分组渲染：每个分类一个标题，组内每条**左边一个图标**、右边文字。
        ///
        /// ⚠️ 图标是**代码画出来的**（<see cref="DamageIcons.Render"/>）——
        ///    因为中文字体里没有刀/锤/弓/闪电/雪花（已逐个查过 cmap）。
        ///    这里把像素喂给 <c>Texture2D</c>，再包成 Sprite 给 Image。
        /// </summary>
        private void RenderGroups(List<CodexCardData.CodexGuideGroup> groups)
        {
            HideColumnTexts();
            if (_body != null) _body.text = "";

            EnsureGroupRoot();
            ClearGroupRows();

            float y = 0f;

            for (int g = 0; g < groups.Count; g++)
            {
                CodexCardData.CodexGuideGroup grp = groups[g];

                // 分类标题（琥珀色，与"选中"一致）
                MakeGroupLabel(grp.Title, y, new Color(0.90f, 0.76f, 0.36f), 20);
                y -= RowHeight + 4f;

                for (int i = 0; i < grp.Entries.Count; i++)
                {
                    CodexCardData.CodexGuideEntry e = grp.Entries[i];

                    // 图标
                    Image icon = MakeGroupIcon(ToSprite(e.IconKey, e.Color), y);
                    if (icon != null) icon.rectTransform.anchoredPosition =
                        new Vector2(IconSize * 0.5f, y - RowHeight * 0.5f + 3f);

                    // 文字（缩进到图标右边）
                    MakeGroupLabel(e.Text, y, new Color(0.88f, 0.90f, 0.94f), 17,
                                   leftPad: IconSize + 10f);

                    y -= RowHeight;
                }

                y -= 6f;      // 组之间留一点空
            }
        }

        private RectTransform _groupRoot;

        private void EnsureGroupRoot()
        {
            if (_groupRoot != null) return;
            if (_root == null) return;

            RectTransform panelRect = _root.childCount > 0
                ? _root.GetChild(0) as RectTransform : _root;

            var go = new GameObject("GuideGroups", typeof(RectTransform));
            go.transform.SetParent(panelRect, false);

            _groupRoot = go.GetComponent<RectTransform>();
            _groupRoot.anchorMin = new Vector2(0f, 0f);
            _groupRoot.anchorMax = new Vector2(1f, 1f);
            _groupRoot.pivot = new Vector2(0f, 1f);
            _groupRoot.offsetMin = new Vector2(60f, 96f);
            _groupRoot.offsetMax = new Vector2(-60f, -136f);
        }

        private void ClearGroupRows()
        {
            for (int i = 0; i < _groupRows.Count; i++)
                if (_groupRows[i] != null) UnityEngine.Object.Destroy(_groupRows[i]);

            _groupRows.Clear();
        }

        private void MakeGroupLabel(string text, float y, Color color, int size,
                                    float leftPad = 0f)
        {
            var go = new GameObject("GuideRow", typeof(Text));
            go.transform.SetParent(_groupRoot, false);
            _groupRows.Add(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(leftPad, 0f);
            rect.offsetMax = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.anchoredPosition = new Vector2(leftPad, y);

            var t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
        }

        private Image MakeGroupIcon(Sprite sprite, float y)
        {
            if (sprite == null) return null;

            var go = new GameObject("GuideIcon", typeof(Image));
            go.transform.SetParent(_groupRoot, false);
            _groupRows.Add(go);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(IconSize, IconSize);
            rect.anchoredPosition = new Vector2(IconSize * 0.5f, y - RowHeight * 0.5f);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        private readonly Dictionary<string, Sprite> _iconCache =
            new Dictionary<string, Sprite>();

        /// <summary>
        /// 把 Core 画好的像素包成 Sprite（带缓存：同一图标只建一次贴图）。
        /// </summary>
        private Sprite ToSprite(string key, int rgb)
        {
            if (string.IsNullOrEmpty(key)) return null;

            string cacheKey = key + "#" + rgb.ToString("X6");
            if (_iconCache.TryGetValue(cacheKey, out Sprite cached)) return cached;

            byte[] px = DamageIcons.Render(key, rgb);

            var tex = new Texture2D(DamageIcons.Size, DamageIcons.Size,
                                    TextureFormat.RGBA32, false, false);
            tex.LoadRawTextureData(px);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();

            var sprite = Sprite.Create(tex,
                                       new Rect(0f, 0f, DamageIcons.Size, DamageIcons.Size),
                                       new Vector2(0.5f, 0.5f));
            _iconCache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>收起两列（翻到单列的角色卡时用）。</summary>
        private void HideColumnTexts()
        {
            if (_colLeft != null) _colLeft.gameObject.SetActive(false);
            if (_colRight != null) _colRight.gameObject.SetActive(false);
        }

        /// <summary>
        /// 建出两列 Text（只建一次，之后复用）。
        ///
        /// 位置与正文同区域（左上锚点、同 60/96 边距），再各分一半宽度。
        /// </summary>
        private void EnsureColumnTexts()
        {
            if (_colLeft != null && _colRight != null) return;
            if (_root == null) return;

            RectTransform panelRect = _root.childCount > 0
                ? _root.GetChild(0) as RectTransform : _root;

            _colLeft = MakeColumnText(panelRect, "ColLeft", 0f, 0.5f);
            _colRight = MakeColumnText(panelRect, "ColRight", 0.5f, 1f);

            _colLeft.gameObject.SetActive(false);
            _colRight.gameObject.SetActive(false);
        }

        private Text MakeColumnText(RectTransform parent, string name,
                                    float anchorMinX, float anchorMaxX)
        {
            Text t = MakeText(parent, name, Vector2.zero, Vector2.zero,
                              (int)BodyFontSize, TextAnchor.UpperLeft,
                              new Color(0.88f, 0.90f, 0.94f));

            RectTransform r = t.rectTransform;
            r.anchorMin = new Vector2(anchorMinX, 0f);
            r.anchorMax = new Vector2(anchorMaxX, 1f);
            r.pivot = new Vector2(0f, 1f);

            // 与正文同样的上下边距；左右各留 60，中间再留半个 ColumnGap
            r.offsetMin = new Vector2(anchorMinX > 0f ? ColumnGap * 0.5f : 60f, 96f);
            r.offsetMax = new Vector2(anchorMaxX < 1f ? -ColumnGap * 0.5f : -60f, -136f);

            return t;
        }

        /// <summary>两列排版时，左右两列之间的最小间隙（像素）。</summary>
        private const float ColumnGap = 28f;

        /// <summary>
        /// 追加一个【小节】：**内容为空就整段不写**（连标题也不写）。
        ///
        /// ⚠️ 玩家要求：指南页（伤害类型 / 状态一览）不要
        ///    【技能】/【技能详细】/【操作指导】/【如何针对】这四节。
        ///    与其在两张卡片上各自留空串、不如由这里统一判空 ——
        ///    这样同一套卡片结构既能放角色卡、也能放中立速查表。
        /// </summary>
        private static void AppendSection(StringBuilder sb, string title, string body)
        {
            if (string.IsNullOrEmpty(body)) return;

            sb.AppendLine(title);
            sb.AppendLine(body);
            sb.AppendLine();
        }

        /// <summary>
        /// 正文**字号**。
        ///
        /// ⚠️ 量两列宽度时必须与实际渲染用**同一个值**，否则对齐是白做的。
        ///    从 18 调到 17 是为了让"左列 46 字 + 长说明"能塞进一行、不换行
        ///    （换行会让右列另起一行，看起来就是"没对齐"）。
        /// </summary>
        private const float BodyFontSize = 17f;

        private static void Highlight(Dictionary<string, Image> buttons, string active)        {
            foreach (KeyValuePair<string, Image> kv in buttons)
            {
                if (kv.Value == null) continue;

                kv.Value.color = kv.Key == active
                    ? new Color(0.72f, 0.55f, 0.22f)      // 选中：琥珀色
                    : new Color(0.28f, 0.30f, 0.36f);     // 未选：石板灰
            }
        }

        private static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

        /// <summary>
        /// 攻击距离的显示文本。
        ///
        /// ⚠️ 无限距离在代码里是 <c>InfiniteAttackRange</c>
        ///    （int.MaxValue = 2147483647），直接输出就是玩家截图里那个样子。
        ///
        /// ⚠⚠ 这个方法我上一轮已经写过一次，但那个脚本静默失败了
        ///    （锚点没匹配上却没报错），所以玩家看到的仍然是数字。
        ///    这次用断言确认替换真的发生了。
        /// </summary>
        private static string RangeText(int range)
            => range >= int.MaxValue ? "∞" : range.ToString();

        public void SetVisible(bool visible)
        {
            if (_root != null) _root.gameObject.SetActive(visible);
        }

        // ============================================================
        // 小工具（与 TwoOnTwoInitiativeScreen 的写法保持一致）
        // ============================================================

        /// <summary>
        /// 把一个控件**贴在父矩形的某个角上**（而不是用绝对坐标）。
        ///
        /// ⚠️ 为什么改成锚点：我先前用的是"父矩形中心 + 绝对偏移"，
        ///    而面板的 pivot 不一定在中心 —— 玩家实测整体偏移到屏幕外，
        ///    就是这个原因。锚点定位不依赖 pivot，稳定得多。
        /// </summary>
        /// <param name="anchor">(0,1) 左上 / (1,1) 右上 / (0,0) 左下 / (1,0) 右下</param>
        private static void Anchor(RectTransform rect, Vector2 anchor,
                                   float offsetX, float offsetY, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(offsetX, offsetY);
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            // ⚠️ **显式打开富文本**（默认虽是 true，但写出来更安全）：
            //    指南页的符号用 `<color=#RRGGBB>` 上色，
            //    一旦这里被关掉，玩家会看到**标签原文**。
            text.supportRichText = true;

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
