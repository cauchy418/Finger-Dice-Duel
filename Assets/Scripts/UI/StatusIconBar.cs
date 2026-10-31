using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 玩家姓名旁的状态、资源与标记图标栏。
    ///
    /// 负面状态、13类抗性、屏障、气及猎人标记共用；超出固定显示区时可滚动。
    /// 缺图时才回退到既有颜色与符号。
    ///
    /// 为什么不用 Emoji：uGUI 的字体回退不一定带彩色 Emoji，
    /// 缺字会显示成方框（本项目在 ∞ 上踩过同类问题）。
    /// 所以这里用**基础几何字符**，CJK 字体里基本都有。
    ///
    /// ⚠️ 本类**自包含**：自己创建 Image / Text，不依赖 GameBoardUI 的私有工厂
    ///    （那些是 private，而界面层的其余脚本也不该被迫认识它们）。
    /// </summary>
    public sealed class StatusIconBar
    {
        private const float IconSize = 30f;
        private const float IconGap = 4f;

        /// <summary>最大同时显示数量，包含状态、资源与标记。</summary>
        public const int MaxIcons = 72;

        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly RectTransform _content;
        private readonly ScrollRect _scroll;
        private int _columns=9;
        private int _maxRows=2;
        private float _size=42,_gap=4;
        private Vector2 _position;
        private PlayerState _shownPlayer;
        private readonly List<Image> _icons = new List<Image>();
        private readonly List<Text> _glyphs = new List<Text>();
        private readonly List<Text> _turns = new List<Text>();

        /// <summary>当前显示的图标数量（供测试断言）。</summary>
        public int VisibleCount { get; private set; }

        /// <summary>1v1 姓名前放大图标；多人模式仍保留紧凑布局。</summary>
        public void SetBattleLayout(Vector2 position)
        {
            SetGridLayout(position, 42, 4, 9);
        }

        public void SetGridLayout(Vector2 position, float size, float gap, int columns)
        {
            _columns=columns;_size=size;_gap=gap;_position=position;
            _maxRows=columns<=3?6:2;
            _root.anchorMin=_root.anchorMax=new Vector2(0.5f,0.5f);
            _root.pivot=new Vector2(0,1);
            _root.anchoredPosition=position+new Vector2(0,size*0.5f);
            UpdateScrollLayout();
            for (int i = 0; i < _icons.Count; i++)
            {
                _icons[i].rectTransform.sizeDelta = new Vector2(size, size);
                _icons[i].rectTransform.anchoredPosition = new Vector2(i % columns * (size + gap), -size*0.5f-(i / columns) * (size + gap));
                float counterSize=size>=34?19:14;
                _turns[i].rectTransform.anchoredPosition = new Vector2(size/2-counterSize/2-1,-size/2+counterSize/2+1);
                _turns[i].rectTransform.sizeDelta = Vector2.one*counterSize;
                _turns[i].fontSize = size >= 34 ? 14 : 11;
                var counter = _icons[i].transform.Find("Counter") as RectTransform;
                if (counter != null) counter.sizeDelta = Vector2.one * (size >= 34 ? 19 : 14);
            }
        }

        public StatusIconBar(RectTransform parent, Font font, bool anchorTop, string name)
        {
            _font = font;

            var go = new GameObject(name, typeof(Image), typeof(ScrollRect), typeof(RectMask2D));
            go.transform.SetParent(parent, false);

            _root = go.GetComponent<RectTransform>();

            // ⚠️ 玩家要求："异常状态挂得太远了，就挂在名字显示前"，
            //    且"要做到挂 9 个不遮挡名字"。
            //
            //    所以改成**横排一行**、锚在**名字行的左端**：
            //      · 名字文本宽 600、居中 -> 左边界在 -300
            //      · 图标栏右端停在 -310，**向左展开**
            //      · 9 个图标最多 9×26 + 8×3 = 258px -> 左边界到 -568，仍在面板内
            //    这样无论挂几个都不会压住名字（名字本身不动）。
            float nameY = anchorTop ? (0.5f - 18f / 340f) : (0.5f + 18f / 340f);

            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, nameY);
            _root.pivot = new Vector2(1f, 0.5f);       // 右端对齐锚点 -> 往左排
            _root.sizeDelta = new Vector2(MaxIcons * (IconSize + IconGap), IconSize);
            _root.anchoredPosition = new Vector2(-310f, 0f);

            // 背景透明：这一栏只放图标，不要多一块底板
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            go.GetComponent<Image>().raycastTarget = false;

            var content=new GameObject("Content",typeof(RectTransform));content.transform.SetParent(_root,false);
            _content=content.GetComponent<RectTransform>();_content.anchorMin=_content.anchorMax=new Vector2(0,1);
            _content.pivot=new Vector2(0,1);
            _scroll=go.GetComponent<ScrollRect>();_scroll.viewport=_root;_scroll.content=_content;
            _scroll.horizontal=false;_scroll.vertical=true;_scroll.inertia=false;
            _scroll.movementType=ScrollRect.MovementType.Clamped;_scroll.scrollSensitivity=28;
            EnsureIcons();
            SetGridLayout(new Vector2(-310,0),30,4,9);
        }

        /// <summary>按玩家的当前状态刷新这一栏。</summary>
        public void Refresh(PlayerState player, GameManager game=null)
        {
            var list=CombatStatusIcons.For(player,game);
            RefreshSnapshot(player,list);
        }
        public void RefreshSnapshot(PlayerState player,IReadOnlyList<CombatStatusIcon> list)
        {
            int count=System.Math.Min(list.Count,MaxIcons);
            for(int i=0;i<MaxIcons;i++)
            {
                bool show=i<count;_icons[i].gameObject.SetActive(show);if(!show) continue;
                var entry=list[i];var sprite=BattleArt.Get(entry.ResourceKey);
                var motion=_icons[i].GetComponent<CombatStatusIconMotion>();if(motion==null)motion=_icons[i].gameObject.AddComponent<CombatStatusIconMotion>();motion.Bind(entry);
                _icons[i].sprite=sprite;
                _icons[i].color=sprite!=null?Color.white:ColorOf(entry.NegativeStatus);
                _glyphs[i].text=sprite!=null?"":GlyphOf(entry.NegativeStatus);
                _turns[i].text=entry.Counter;
                _icons[i].transform.Find("Counter").gameObject.SetActive(entry.Counter.Length>0);
                BattleTooltip.Set(_icons[i],entry.Description);
            }
            bool changed=!ReferenceEquals(player,_shownPlayer)||VisibleCount!=count;
            _shownPlayer=player;VisibleCount=count;UpdateScrollLayout();
            if(changed) _scroll.verticalNormalizedPosition=1;
        }
        private void UpdateScrollLayout()
        {
            int rows=System.Math.Max(1,(VisibleCount+_columns-1)/_columns);
            _root.sizeDelta=new Vector2(_columns*(_size+_gap)-_gap,System.Math.Min(rows,_maxRows)*(_size+_gap)-_gap);
            if(_content!=null) _content.sizeDelta=new Vector2(_root.sizeDelta.x,rows*(_size+_gap)-_gap);
            _root.GetComponent<Image>().raycastTarget=rows>_maxRows;
        }

        private void HideAll()
        {
            for (int i = 0; i < _icons.Count; i++)
                _icons[i].gameObject.SetActive(false);
        }

        private void EnsureIcons()
        {
            for (int i = 0; i < MaxIcons; i++)
            {
                var go = new GameObject("Status" + i, typeof(Image));
                go.transform.SetParent(_content, false);

                // ⚠️ **横排一行**（原来是竖排一列，所以"挂得太远"）。
                //    pivot 在左端 -> 从锚点往右依次排开，第 0 个最靠左。
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(IconSize, IconSize);
                rect.anchoredPosition = new Vector2(i * (IconSize + IconGap), 0f);

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _icons.Add(img);

                _glyphs.Add(MakeText(rect, "Glyph", Vector2.zero,
                                     new Vector2(IconSize, IconSize), 18,
                                     TextAnchor.MiddleCenter, Color.white));

                var counterGo = new GameObject("Counter", typeof(Image));
                counterGo.transform.SetParent(rect, false);
                var counter = counterGo.GetComponent<Image>();
                counter.rectTransform.anchorMin = counter.rectTransform.anchorMax = new Vector2(1, 0);
                counter.rectTransform.sizeDelta = new Vector2(19, 19);
                counter.rectTransform.pivot=new Vector2(1,0);
                counter.rectTransform.anchoredPosition = new Vector2(-1, 1);
                BattleArt.Apply(counter, "UI/status_counter");
                counter.raycastTarget = false;
                _turns.Add(MakeText(rect, "Turns", new Vector2(IconSize / 2f - 3f,
                                                               -(IconSize / 2f - 3f)),
                                    new Vector2(13f, 13f), 11, TextAnchor.MiddleCenter,
                                    new Color(1f, 1f, 1f, 0.9f)));
            }
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

        // ============================================================
        // 图标外观（换成美术资源时只改这里）
        // ============================================================

        /// <summary>
        /// 状态对应的符号。
        ///
        /// ⚠️ 直接转给 <see cref="StatusNames.GlyphOf"/> —— **只有一处真相**。
        ///    图鉴的指南页也要显示同一批符号，两处各写一份必然漂移。
        /// </summary>
        public static string GlyphOf(StatusEffect s) => StatusNames.GlyphOf(s);

        /// <summary>
        /// 状态对应的主色。
        ///
        /// ⚠️ 数值取自 <see cref="StatusNames.StatusColorOf"/> —— **只有一处真相**。
        ///    图鉴的指南页也用同一份颜色，两处各写一份必然漂移。
        /// </summary>
        public static Color ColorOf(StatusEffect s)
        {
            int rgb = StatusNames.StatusColorOf(s);
            return new Color(((rgb >> 16) & 0xFF) / 255f,
                             ((rgb >> 8) & 0xFF) / 255f,
                             (rgb & 0xFF) / 255f);
        }
    }
}
