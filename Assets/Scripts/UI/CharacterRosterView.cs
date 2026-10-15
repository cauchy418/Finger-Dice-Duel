using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 角色卡的横向滚动列表。
    ///
    /// 为什么用"卡片 + 横向滚动"而不是下拉框：
    /// 下拉框只显示名字，10 个角色的技能玩家完全看不到，
    /// 选角色变成盲选。卡片能一眼展示名字、身材与技能摘要。
    ///
    /// 做成独立组件是为了**两处复用** —— 玩家 A 和玩家 B 各一份，
    /// 布局代码只写一次。
    /// </summary>
    public sealed class CharacterRosterView
    {
        /// <summary>卡片尺寸（含间距）。</summary>
        private const float CardWidth = 172f;
        private const float CardHeight = 96f;
        private const float CardGap = 10f;

        /// <summary>可视区域宽度。</summary>
        private const float ViewWidth = 1120f;
        private const float ViewHeight = 100f;

        private static readonly Color CardNormal = new Color(0.16f, 0.19f, 0.25f);
        private static readonly Color CardSelected = new Color(0.26f, 0.36f, 0.30f);
        private static readonly Color CardOutline = new Color(0.98f, 0.80f, 0.35f);

        private RectTransform _view;          // 可视区域（带遮罩）
        private RectTransform _content;       // 横向排列的容器
        private readonly List<CardWidget> _cards = new List<CardWidget>();

        private int _selectedIndex = -1;

        /// <summary>当前展示的角色（换名单后会被替换）。</summary>
        private readonly List<CharacterDefinition> _roster = new List<CharacterDefinition>();
        private Font _font;

        /// <summary>选中项变化时回调（索引）。</summary>
        public event Action<int> SelectionChanged;

        /// <summary>当前选中索引（-1 表示未选）。</summary>
        public int SelectedIndex => _selectedIndex;

        /// <summary>列表根节点（用于登记进屏幕分组）。</summary>
        public RectTransform Root => _view;

        private sealed class CardWidget
        {
            public GameObject Root;        // 换名单时要销毁它
            public Image Background;
            public Outline Outline;
        }

        /// <summary>
        /// 在 <paramref name="parent"/> 下创建一条角色卡列表。
        /// </summary>
        /// <param name="parent">挂载的父节点。</param>
        /// <param name="name">节点名。</param>
        /// <param name="position">相对父节点中心的位置。</param>
        /// <param name="roster">要展示的角色（顺序即卡片顺序）。</param>
        /// <param name="font">字体。</param>
        /// <param name="initialIndex">初始选中索引。</param>
        /// <summary>
        /// 把角色换成**一句技能描述**的委托。
        ///
        /// ⚠️ 为什么用委托而不直接调图鉴：
        ///    图鉴资料在 <c>FingerDiceDuel.Characters</c> 层，
        ///    而**表现层不能引用它**（分层铁律）。
        ///    所以由入口（GameBootstrap）注入一个取短描述的方法。
        ///    传 null 时回退到原来的"截断长描述"行为。
        /// </summary>
        private System.Func<CharacterDefinition, string> _skillText;

        public void Build(RectTransform parent, string name, Vector2 position,
                          IReadOnlyList<CharacterDefinition> roster, Font font,
                          int initialIndex = 0,
                          System.Func<CharacterDefinition, string> skillText = null)
        {
            _skillText = skillText;
            _font = font;

            // ---- 可视区域：ScrollRect + 遮罩 ----
            var viewGo = new GameObject(name,
                typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            viewGo.transform.SetParent(parent, false);

            _view = viewGo.GetComponent<RectTransform>();
            _view.anchorMin = _view.anchorMax = new Vector2(0.5f, 0.5f);
            _view.pivot = new Vector2(0.5f, 0.5f);
            _view.sizeDelta = new Vector2(ViewWidth, ViewHeight);
            _view.anchoredPosition = position;

            var viewImg = viewGo.GetComponent<Image>();
            viewImg.color = new Color(0.10f, 0.12f, 0.16f, 0.85f);
            // 可视区域本身不做拖动判定，避免挡住卡片点击
            viewImg.raycastTarget = true;

            // ---- 内容容器：横向排列 ----
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_view, false);

            _content = contentGo.GetComponent<RectTransform>();
            // 左端对齐、纵向居中，这样滚动只影响 x
            _content.anchorMin = new Vector2(0f, 0.5f);
            _content.anchorMax = new Vector2(0f, 0.5f);
            _content.pivot = new Vector2(0f, 0.5f);

            float totalWidth = roster.Count * CardWidth + (roster.Count + 1) * CardGap;
            _content.sizeDelta = new Vector2(totalWidth, ViewHeight);
            _content.anchoredPosition = new Vector2(CardGap, 0f);

            var scroll = viewGo.GetComponent<ScrollRect>();
            scroll.content = _content;
            scroll.viewport = _view;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.inertia = false;      // 卡片列表不需要惯性，点选更稳

            // ---- 卡片 ----
            SetRoster(roster, initialIndex);
        }

        /// <summary>
        /// **换一份名单**（双将选角要用：选完主将后，副将候选只剩同阵营的那几个）。
        ///
        /// 会销毁旧卡片、按新名单重建，并把可视区域宽度调成新名单的宽度
        /// （名单变短时不调整会让可滚动范围虚高、滚动条拖到空白处）。
        ///
        /// <paramref name="selectIndex"/> 为 -1 表示不选任何一项。
        /// </summary>
        public void SetRoster(IReadOnlyList<CharacterDefinition> roster, int selectIndex = 0)
        {
            if (_content == null || roster == null) return;

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i]?.Root != null)
                    UnityEngine.Object.Destroy(_cards[i].Root);
            }
            _cards.Clear();
            _roster.Clear();

            for (int i = 0; i < roster.Count; i++) _roster.Add(roster[i]);

            float totalWidth = _roster.Count * CardWidth + (_roster.Count + 1) * CardGap;
            _content.sizeDelta = new Vector2(totalWidth, ViewHeight);
            _content.anchoredPosition = new Vector2(CardGap, 0f);

            for (int i = 0; i < _roster.Count; i++)
                _cards.Add(BuildCard(_roster[i], i, _font));

            _selectedIndex = -1;
            if (selectIndex >= 0 && selectIndex < _cards.Count) Select(selectIndex, notify: false);
        }

        /// <summary>当前展示的角色数量（供测试与诊断）。</summary>
        public int Count => _cards.Count;

        private CardWidget BuildCard(CharacterDefinition def, int index, Font font)
        {
            var go = new GameObject("Card" + index,
                typeof(Image), typeof(Button), typeof(Outline));
            go.transform.SetParent(_content, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rect.anchoredPosition = new Vector2(CardGap + index * (CardWidth + CardGap), 0f);

            var bg = go.GetComponent<Image>();
            bg.color = CardNormal;

            var widget = new CardWidget
            {
                Root = go,
                Background = bg,
                Outline = go.GetComponent<Outline>(),
            };
            widget.Outline.effectColor = CardOutline;
            widget.Outline.effectDistance = new Vector2(3f, -3f);
            widget.Outline.enabled = false;
            // 描边只是视觉，不该拦截点击
            widget.Outline.useGraphicAlpha = false;

            // 名字
            MakeText(rect, "Name", new Vector2(0f, 28f), new Vector2(CardWidth - 16f, 32f),
                     24, TextAnchor.MiddleCenter, Color.white, font, bold: true).text = def.DisplayName;

            // 身材（生命 / 攻 / 防）
            MakeText(rect, "Stats", new Vector2(0f, 2f), new Vector2(CardWidth - 12f, 24f),
                     17, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.92f), font).text =
                DescribeStats(def);

            // 技能摘要
            //
            // ⚠️ 玩家要求：**全部采用图鉴里【技能】那条短描述**。
            //    原来是把长描述截断到 26 字，
            //    截出来的句子常常读不通（而且与图鉴不一致）。
            //    图鉴里那句是**人写的、一句话说完**，更适合卡片。
            string hint = _skillText != null ? _skillText(def) : Shorten(def.Description);
            MakeText(rect, "Hint", new Vector2(0f, -26f), new Vector2(CardWidth - 14f, 40f),
                     14, TextAnchor.UpperCenter, new Color(0.60f, 0.68f, 0.78f), font).text = hint;

            int captured = index;
            go.GetComponent<Button>().onClick.AddListener(() => Select(captured));

            return widget;
        }

        /// <summary>选中某一项。重复选中同一项不会重复触发回调。</summary>
        public void Select(int index, bool notify = true)
        {
            if (index < 0 || index >= _cards.Count) return;

            bool changed = index != _selectedIndex;
            _selectedIndex = index;

            for (int i = 0; i < _cards.Count; i++)
            {
                bool on = i == index;
                _cards[i].Background.color = on ? CardSelected : CardNormal;
                _cards[i].Outline.enabled = on;
            }

            if (notify && changed) SelectionChanged?.Invoke(index);
        }

        /// <summary>把某张卡滚进可视区域（选中时用，避免选中的卡在屏幕外）。</summary>
        public void ScrollToSelected()
        {
            if (_content == null || _selectedIndex < 0) return;

            float x = CardGap + _selectedIndex * (CardWidth + CardGap);
            // 让选中的卡尽量居中
            float target = ViewWidth * 0.5f - x - CardWidth * 0.5f;
            float maxScroll = Mathf.Max(0f, _content.sizeDelta.x - ViewWidth);

            _content.anchoredPosition = new Vector2(
                Mathf.Clamp(target, -maxScroll, 0f), 0f);
        }

        /// <summary>
        /// 卡片上的身材摘要。真正的生成逻辑在 <see cref="CharacterCardText"/>
        /// （那边不依赖 UnityEngine / UI 命名空间，可被无头测试覆盖）。
        /// </summary>
        public static string DescribeStats(CharacterDefinition def)
            => CharacterCardText.DescribeStats(def);

        /// <summary>技能摘要（同样委托给可测层）。</summary>
        public static string Shorten(string description, int maxChars = 26)
            => CharacterCardText.Shorten(description, maxChars);

        private static Text MakeText(RectTransform parent, string name, Vector2 position,
                                     Vector2 size, int fontSize, TextAnchor anchor,
                                     Color color, Font font, bool bold = false)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            if (bold) text.fontStyle = FontStyle.Bold;

            return text;
        }
    }
}
