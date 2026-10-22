using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// **法师·塑能学派的伤害类型选择面板**（10 选 1）。
    ///
    /// 玩家确认的交互顺序：
    ///   ① 右侧弹「是否放弃攻击」
    ///   ② 选"放弃" -> 弹这个面板（10 种类型）
    ///   ③ 选完类型 -> 再选攻击目标
    ///
    /// 布局：2 行 × 5 列，每格显示**类型名 + 它会挂上的状态**。
    ///
    /// ⚠️ 自包含（自己建 Image / Text / Button），理由同 <see cref="StatusIconBar"/>。
    /// </summary>
    public sealed class DamageTypePanel
    {
        /// <summary>
        /// 面板里的 10 种类型（顺序固定，便于测试与肌肉记忆）。
        ///
        /// ⚠️ 直接引用 <see cref="StatusNames.AllTypes"/> —— **只有一处真相**。
        ///    原来在这里另抄了一份数组，那种"两处各写一遍"迟早会漂移。
        /// </summary>
        public static DamageType[] AllTypes => StatusNames.AllTypes;

        private const float CellW = 152f;
        private const float CellH = 146f;
        private const float Gap = 8f;
        private const int Columns = 5;

        private readonly RectTransform _root;
        private readonly Font _font;
        private readonly Button[] _buttons = new Button[10];
        private readonly Image[] _fills = new Image[10];

        /// <summary>面板当前是否可见。</summary>
        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        /// <summary>玩家点了哪一种（null = 还没点）。</summary>
        public Action<DamageType> OnPicked { get; set; }

        public DamageTypePanel(RectTransform parent, Font font)
        {
            _font = font;

            var go = new GameObject("DamageTypePanel", typeof(Image));
            go.transform.SetParent(parent, false);

            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(Columns * CellW + (Columns - 1) * Gap + 24f,
                                          (2 * CellH + Gap) + 56f);
            // ⚠️ 玩家要求："选择整体右移"。
            //    原来居中（Vector2.zero），正好压在下方自己的手牌上 ——
            //    玩家要在"手牌"和"类型格"之间来回看，挡着就没法选。
            //    右移 320px 之后与手牌错开（面板宽 ~924，居中时右边界 +462）。
            _root.anchoredPosition = new Vector2(160f, 0f);

            BattleArt.Apply(go.GetComponent<Image>(), "UI/panel_tooltip", true);

            Text title = MakeText(_root, "Title", new Vector2(0f, _root.sizeDelta.y / 2 - 27),
                                  new Vector2(_root.sizeDelta.x, 30f), 22,
                                  TextAnchor.MiddleCenter, Color.white);
            title.text = "【附魔】选择下一次攻击的伤害类型";
            title.font = UiFont.Title;
            title.color = BattleArt.Gold;

            for (int i = 0; i < AllTypes.Length; i++)
            {
                int row = i / Columns;
                int col = i % Columns;

                float x = -(Columns - 1) * 0.5f * (CellW + Gap) + col * (CellW + Gap);
                float y = 55f - row * (CellH + Gap);

                _buttons[i] = MakeCell(AllTypes[i], new Vector2(x, y), i);
            }

            go.SetActive(false);
        }

        private Button MakeCell(DamageType type, Vector2 position, int index)
        {
            var go = new GameObject("Type" + index, typeof(Image), typeof(Button));
            go.transform.SetParent(_root, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(CellW, CellH);
            rect.anchoredPosition = position;

            var fill = go.GetComponent<Image>();
            BattleArt.Apply(fill, "UI/button_normal", true);
            _fills[index] = fill;

            var iconGo = new GameObject("PaintedDamage", typeof(Image));
            iconGo.transform.SetParent(rect, false);
            var icon = iconGo.GetComponent<Image>();
            icon.rectTransform.sizeDelta = new Vector2(76, 76);
            icon.rectTransform.anchoredPosition = new Vector2(0, 23);
            BattleArt.Apply(icon, "Damage/" + BattleArt.DamageKey(type));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            BattleTooltip.Set(icon, GuideSheets.MagicLinePlain(type));
            Text name = MakeText(rect, "Name", new Vector2(0f, -30f),
                                 new Vector2(CellW - 8f, 26f), 20,
                                 TextAnchor.MiddleCenter, Color.white);
            name.text = StatusNames.Of(type);

            // 第二行写"它会挂上的状态"（力场没有状态，写明是穿透）
            name.color = BattleArt.Gold;
            Text sub = MakeText(rect, "Sub", new Vector2(0f, -55f),
                                new Vector2(CellW - 8f, 22f), 14,
                                TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));

            StatusEffect st = StatusNames.StatusOf(type);
            sub.text = st == StatusEffect.None
                ? "穿盾 · 不可闪避"
                : "→ " + StatusNames.Of(st);

            var btn = go.GetComponent<Button>();
            BattleArt.Button(btn);
            DamageType captured = type;
            btn.onClick.AddListener(() => { OnPicked?.Invoke(captured); });

            return btn;
        }

        /// <summary>显示面板。</summary>
        public void Show()
        {
            if (_root != null)
            {
                _root.gameObject.SetActive(true);
                _root.SetAsLastSibling();
            }
        }

        /// <summary>隐藏面板。</summary>
        public void Hide()
        {
            if (_root != null) _root.gameObject.SetActive(false);
        }

        /// <summary>第 <paramref name="index"/> 格是不是可点击（供测试断言）。</summary>
        public bool IsCellInteractable(int index)
            => index >= 0 && index < _buttons.Length && _buttons[index] != null
               && _buttons[index].interactable;

        /// <summary>第 <paramref name="index"/> 格的类型。</summary>
        public static DamageType TypeAt(int index)
            => index >= 0 && index < AllTypes.Length ? AllTypes[index] : DamageType.None;

        /// <summary>
        /// 类型的主色（与状态图标栏 / 速查表**同源**，保证视觉一致）。
        ///
        /// ⚠️ 力场**没有状态**，原来这里硬编码了一个"中性灰蓝"，
        ///    而速查表用的是 `StatusNames.ForceColor`（深红）——
        ///    两处不一致，玩家实测报"力场没有同步成红色"。
        ///    现在统一走 <see cref="StatusNames.GuideColorOf"/>（唯一一份）。
        /// </summary>
        public static Color FillOf(DamageType type)
        {
            int rgb = StatusNames.GuideColorOf(type);
            return new Color(((rgb >> 16) & 0xFF) / 255f,
                             ((rgb >> 8) & 0xFF) / 255f,
                             (rgb & 0xFF) / 255f);
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
