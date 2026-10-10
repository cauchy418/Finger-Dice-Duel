using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FingerDiceDuel.UI
{
    /// <summary>
    /// 一组可以整体显隐的界面元素。
    ///
    /// 为什么需要它：界面从"一个角色选择页"扩展成"主菜单 → 1v1 子菜单 → 角色选择"之后，
    /// 如果还靠零散的 <c>SetActive</c>，很容易漏掉某个元素导致上一层残留。
    /// 用分组后，切屏就是 <c>Show(group)</c> 一句话。
    /// </summary>
    public sealed class ScreenGroup
    {
        private readonly List<GameObject> _members = new List<GameObject>();

        public bool IsVisible { get; private set; }

        /// <summary>登记一个属于本组的节点。</summary>
        public void Add(GameObject go)
        {
            if (go == null) return;
            _members.Add(go);
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
            for (int i = 0; i < _members.Count; i++)
                if (_members[i] != null) _members[i].SetActive(visible);
        }

        /// <summary>把最后一个登记的节点提升到最上层（用于刚创建的弹窗）。</summary>
        public void BringLastToFront()
        {
            if (_members.Count == 0) return;

            GameObject last = _members[_members.Count - 1];
            if (last != null) last.transform.SetAsLastSibling();
        }
    }

    /// <summary>
    /// 简易弹窗：半透明遮罩 + 标题 + 正文 + 一个"知道了"按钮。
    ///
    /// 用于"正在开发，敬请期待"与规则说明 —— 两者结构一样，
    /// 所以做成一个可复用的组件，只换文案。
    /// </summary>
    public sealed class ModalPanel
    {
        private GameObject _root;
        private Text _title;
        private Text _body;
        private ScrollRect _scroll;

        public bool IsVisible => _root != null && _root.activeSelf;

        /// <summary>
        /// 创建弹窗（默认隐藏）。
        /// </summary>
        /// <param name="parent">父节点（应挂在最上层的画布下）。</param>
        /// <param name="font">正文与标题字体。</param>
        /// <param name="boldFont">标题字体。</param>
        /// <param name="size">弹窗尺寸。</param>
        public void Build(RectTransform parent, Font font, Font boldFont,
                          Vector2 size, float bodyFontSize = 22f, float titleFontSize = 34f)
        {
            _root = new GameObject("Modal", typeof(Image));
            _root.transform.SetParent(parent, false);

            var rect = _root.GetComponent<RectTransform>();
            Stretch(rect);

            var blocker = _root.GetComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.72f);      // 吃点击，防止点穿到底层
            blocker.raycastTarget = true;

            var box = new GameObject("Box", typeof(Image));
            box.transform.SetParent(rect, false);

            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = size;
            boxRect.anchoredPosition = Vector2.zero;

            var boxImg = box.GetComponent<Image>();
            boxImg.color = new Color(0.14f, 0.16f, 0.21f, 0.99f);
            boxImg.raycastTarget = true;

            _title = MakeText(boxRect, "Title", new Vector2(0f, size.y * 0.5f - 52f),
                              new Vector2(size.x - 60f, 50f), (int)titleFontSize,
                              TextAnchor.MiddleCenter, new Color(0.98f, 0.86f, 0.48f), boldFont);
            _title.fontStyle = FontStyle.Bold;

            // 正文放在**可滚动**区域里。
            // 规则说明（基础规则 + 10 个角色）远超过一屏，
            // 固定文本框会直接把内容截掉 —— 实测就是这样，所以改成滚动。
            BuildScrollBody(boxRect, size, bodyFontSize, font);

            Button close = MakeButton(boxRect, "Close",
                                      new Vector2(0f, -size.y * 0.5f + 48f),
                                      new Vector2(220f, 56f), "知道了", font);
            close.onClick.AddListener(Hide);

            _root.SetActive(false);
        }

        /// <summary>
        /// 建立可滚动正文区：Viewport（遮罩）+ Content（按文字高度自适应）。
        ///
        /// 用 <see cref="ContentSizeFitter"/> 让 Content 自动长高，
        /// 这样不需要手工算文字占多少像素。
        /// </summary>
        private void BuildScrollBody(RectTransform boxRect, Vector2 size,
                                     float bodyFontSize, Font font)
        {
            float top = size.y * 0.5f - 96f;              // 标题下方
            float bottom = -size.y * 0.5f + 92f;          // 关闭按钮上方
            float height = top - bottom;

            // Viewport：负责裁剪 + 接收拖动。
            //
            // ⚠️ 必须挂一个 **Image**（颜色全透明也可以）。
            // ScrollRect 靠**射线检测**拿拖动事件；只有 RectMask2D 时
            // 这个区域没有任何 Graphic 参与检测，拖动事件就到不了 ScrollRect ——
            // 实测表现就是"规则弹窗点进去拖不动"。
            var viewGo = new GameObject("Viewport",
                typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
            viewGo.transform.SetParent(boxRect, false);

            var viewRect = viewGo.GetComponent<RectTransform>();
            viewRect.anchorMin = viewRect.anchorMax = new Vector2(0.5f, 0.5f);
            viewRect.pivot = new Vector2(0.5f, 0.5f);
            viewRect.sizeDelta = new Vector2(size.x - 80f, height);
            viewRect.anchoredPosition = new Vector2(0f, (top + bottom) * 0.5f);

            // 全透明的拖动热区：看得见的是正文，挡住拖动的是这一层
            var viewImg = viewGo.GetComponent<Image>();
            viewImg.color = new Color(1f, 1f, 1f, 0f);
            viewImg.raycastTarget = true;

            // Content：按文字自适应高度
            var contentGo = new GameObject("Content", typeof(Text), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewRect, false);

            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);      // 从顶部往下排
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(0f, 0f);
            contentRect.offsetMax = new Vector2(0f, 0f);
            contentRect.anchoredPosition = Vector2.zero;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            _body = contentGo.GetComponent<Text>();
            _body.font = font;
            _body.fontSize = (int)bodyFontSize;
            _body.color = new Color(0.88f, 0.91f, 0.96f);
            _body.alignment = TextAnchor.UpperLeft;
            _body.raycastTarget = false;
            _body.horizontalOverflow = HorizontalWrapMode.Wrap;
            _body.verticalOverflow = VerticalWrapMode.Overflow;
            _body.lineSpacing = 1.15f;

            // ScrollRect：竖直滚动，滚轮与拖动都行
            var scroll = viewGo.GetComponent<ScrollRect>();
            _scroll = scroll;
            scroll.content = contentRect;
            scroll.viewport = viewRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.1f;
        }

        /// <summary>显示弹窗。</summary>
        public void Show(string title, string body)
        {
            if (_root == null) return;

            if (_title != null) _title.text = title ?? "";
            if (_body != null) _body.text = body ?? "";

            _root.SetActive(true);
            _root.transform.SetAsLastSibling();     // 保证盖在一切之上

            // 每次都从头开始读：否则重开规则会停在上次滚到的位置
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>把正文滚到顶部（显示后调用；布局要等一帧才算好）。</summary>
        public void ScrollToTop()
        {
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>正文滚动组件（供调用方在下一帧复位）。</summary>
        public ScrollRect Scroll => _scroll;

        public void Hide()
        {
            if (_root != null) _root.SetActive(false);
        }

        /// <summary>弹窗根节点（调用方可能想把它登记进某个 ScreenGroup）。</summary>
        public GameObject Root => _root;

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Text MakeText(RectTransform parent, string name, Vector2 position,
                                     Vector2 size, int fontSize, TextAnchor anchor,
                                     Color color, Font font)
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Button MakeButton(RectTransform parent, string name, Vector2 position,
                                         Vector2 size, string label, Font font)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            go.GetComponent<Image>().color = new Color(0.26f, 0.48f, 0.78f);

            Text text = MakeText(rect, "Label", Vector2.zero, size, 26,
                                 TextAnchor.MiddleCenter, Color.white, font);
            text.text = label;

            return go.GetComponent<Button>();
        }
    }
}
