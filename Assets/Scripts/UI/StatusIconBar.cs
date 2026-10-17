using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// **法师状态的图标栏**（显示在玩家面板右侧）。
    ///
    /// 9 种负面状态各有一个小方块：底色 = 伤害类型的主色，
    /// 中间一个符号，右下角小字是**剩余回合数**。
    ///
    /// ⚠️ 为什么用"色块 + 符号"而不是美术图片：
    ///    目前没有图标资源，而这一栏必须先把**信息**传出去
    ///    （玩家要知道自己身上还剩什么、还有几回合）。
    ///    换成 PNG 只需给 <see cref="Image.sprite"/> 赋值，
    ///    其余布局与刷新逻辑都不用动。
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

        /// <summary>最大同时显示的数量（= 状态种类数）。</summary>
        public const int MaxIcons = 9;

        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly List<Image> _icons = new List<Image>();
        private readonly List<Text> _glyphs = new List<Text>();
        private readonly List<Text> _turns = new List<Text>();

        /// <summary>当前显示的图标数量（供测试断言）。</summary>
        public int VisibleCount { get; private set; }

        public StatusIconBar(RectTransform parent, Font font, bool anchorTop, string name)
        {
            _font = font;

            var go = new GameObject(name, typeof(Image));
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

            EnsureIcons();
        }

        /// <summary>按玩家的当前状态刷新这一栏。</summary>
        public void Refresh(PlayerState player)
        {
            IReadOnlyList<StatusEffect> list = player?.Statuses;
            if (list == null) { VisibleCount = 0; HideAll(); return; }

            int count = list.Count;
            if (count > MaxIcons) count = MaxIcons;

            for (int i = 0; i < MaxIcons; i++)
            {
                bool show = i < count;
                _icons[i].gameObject.SetActive(show);
                if (!show) continue;

                StatusEffect s = list[i];
                _icons[i].color = ColorOf(s);
                _glyphs[i].text = GlyphOf(s);
                _turns[i].text = player.StatusTurnsLeft(s).ToString();
            }

            VisibleCount = count;
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
                go.transform.SetParent(_root, false);

                // ⚠️ **横排一行**（原来是竖排一列，所以"挂得太远"）。
                //    pivot 在左端 -> 从锚点往右依次排开，第 0 个最靠左。
                var rect = go.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(IconSize, IconSize);
                rect.anchoredPosition = new Vector2(i * (IconSize + IconGap), 0f);

                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _icons.Add(img);

                _glyphs.Add(MakeText(rect, "Glyph", Vector2.zero,
                                     new Vector2(IconSize, IconSize), 18,
                                     TextAnchor.MiddleCenter, Color.white));

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
