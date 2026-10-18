using UnityEngine;
using UnityEngine.UI;
using FingerDiceDuel.Assistant;

namespace FingerDiceDuel.UI
{
    /// <summary>界面层级。</summary>
    public enum GameScreen
    {
        /// <summary>主菜单（标题 + 模式选择）。</summary>
        MainMenu = 0,

        /// <summary>1v1 子菜单（人机 / 竞技）。</summary>
        OneOnOneMenu = 1,

        /// <summary>1v1 的玩法选择（入门 / 经典）。</summary>
        ModeMenu = 5,

        /// <summary>人机对战（入门）的难度选择。</summary>
        DifficultyMenu = 6,

        /// <summary>2v2 子菜单（人机练习 / 真人对决）。</summary>
        TwoOnTwoMenu = 7,

        /// <summary>2v2 的角色选择（含座位选择与队友选择）。</summary>
        TwoOnTwoCharacter = 8,

        /// <summary>2v2 经典玩法的掷骰先攻（四人依次决定初始手值）。</summary>
        TwoOnTwoInitiative = 9,

        /// <summary>角色选择。</summary>
        CharacterSelect = 2,

        /// <summary>开局掷骰分配初始手值（1d20）。</summary>
        DiceRoll = 4,

        /// <summary>对局中。</summary>
        InMatch = 3,

        /// <summary>图鉴。</summary>
        Codex = 10,

        // ---------- 玩家要求的三大模式（新主菜单层级）----------

        /// <summary>冒险模式（后续开发的 PVE，目前是占位）。</summary>
        Adventure = 11,

        /// <summary>竞技模式（对战：原本 1v1 / 2v2 / 8 人都在这里）。</summary>
        Competition = 12,

        /// <summary>娱乐模式（现在只有双将）。</summary>
        Casual = 13,

        /// <summary>双将模式子菜单（双将对决 / 阵营混战）。</summary>
        DualGeneral = 14,

        /// <summary>双将对决（1v1）：人机 / 真人。</summary>
        DualGeneralMenu = 15,

        /// <summary>阵营混战（8 人）：人机 / 真人。</summary>
        FactionBattleMenu = 16,

        /// <summary>双将的**选将界面**（先选主将，副将候选只剩同阵营）。</summary>
        DualGeneralSelect = 17,

        /// <summary>8 人阵营混战的**单将选角界面**。</summary>
        FactionSelect = 18,

        /// <summary>8 人模式子菜单（阵营混战 / 身份对局）。</summary>
        EightPlayerMenu = 19,

        /// <summary>娱乐 → 双将 → 双将阵营混战的 AI / 真人菜单。</summary>
        DualFactionMenu = 20,

        /// <summary>8 人身份局：人机 / 真人菜单。</summary>
        IdentityBattleMenu = 21,

        /// <summary>8 人身份局选将。</summary>
        IdentitySelect = 22,
    }

    /// <summary>
    /// "厚重金色立体字"标题：纯代码绘制，不需要任何美术资源。
    ///
    /// 做法是**多层描边 + 错位投影 + 金属渐变**：
    ///   · 最底层：深色偏移副本（投影，制造浮起感）
    ///   · 中间层：四周偏移的深色副本（描边，把字从背景里"抠"出来）
    ///   · 最上层：本体，用上下渐变色模拟金属反光
    ///
    /// 为什么分层而不是用 Outline 组件：Unity 的 <c>Outline</c> 只能单向偏移一次，
    /// 做不出均匀描边；多层副本可控且不依赖 shader。
    /// </summary>
    public sealed class TitleArt
    {
        private static readonly Color TopColor = new Color(1f, 0.94f, 0.66f);      // 高光金
        private static readonly Color BottomColor = new Color(0.92f, 0.66f, 0.16f); // 暗金
        private static readonly Color OutlineColor = new Color(0.24f, 0.13f, 0.02f);
        private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.55f);

        /// <summary>
        /// 描边方向（8 向）。
        ///
        /// 用 1.5px 而不是 2~3px：描边太粗会让金色字"发胖"，
        /// 而且它和投影叠在一起时，整体边界会往外扩一大圈，
        /// 看起来就像"标题与投影是两段字"。收细之后边界更贴身。
        /// </summary>
        private static readonly Vector2[] OutlineOffsets =
        {
            new Vector2(-1.5f, 0f), new Vector2(1.5f, 0f),
            new Vector2(0f, 1.5f), new Vector2(0f, -1.5f),
            new Vector2(-1f, 1f), new Vector2(1f, 1f),
            new Vector2(-1f, -1f), new Vector2(1f, -1f),
        };


        /// <summary>标题根节点（可作为分区标题复用）。</summary>
        public RectTransform Root { get; private set; }

        /// <summary>
        /// 创建标题。返回根节点，父级可用它来定位。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">节点名。</param>
        /// <param name="text">标题文字。</param>
        /// <param name="position">相对父节点中心的位置。</param>
        /// <param name="fontSize">字号。</param>
        /// <param name="font">字体。</param>
        public RectTransform Build(RectTransform parent, string name, string text,
                                   Vector2 position, int fontSize, Font font)
        {
            var rootGo = new GameObject(name, typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);

            Root = rootGo.GetComponent<RectTransform>();
            Root.anchorMin = Root.anchorMax = new Vector2(0.5f, 0.5f);
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.sizeDelta = new Vector2(fontSize * text.Length + 120f, fontSize * 1.35f);
            Root.anchoredPosition = position;

            // ① 投影：只露 2px。
            //    曾经用 (6,-7) -> 看成"两层字"；改成 (3,-3) 后仍偏大，
            //    因为卡片/按钮的描边也叠在下面，视觉上更明显。现在收到最小。
            MakeLayer("Shadow", text, fontSize, font, ShadowColor, new Vector2(2f, -2f));

            // ② 描边（8 向深色副本）
            foreach (Vector2 offset in OutlineOffsets)
                MakeLayer("Outline", text, fontSize, font, OutlineColor, offset);

            // ③ 本体：先铺暗金，再在上面压一层"只保留上半部分"的亮金，
            //    看起来就是上方受光的金属字。
            MakeLayer("Base", text, fontSize, font, BottomColor, Vector2.zero);

            // 裁剪容器：只占根节点的上半部分，并裁掉溢出内容
            // 注意：RectMask2D 需要 RectTransform，Unity 会自动加，
            // 显式再声明一次会触发 "RectTransform 重复" 的运行时冲突。
            var topGo = new GameObject("TopHalf", typeof(RectMask2D));
            topGo.transform.SetParent(Root, false);

            var topRect = topGo.GetComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0f, 0.5f);      // 上半
            topRect.anchorMax = new Vector2(1f, 1f);
            topRect.offsetMin = Vector2.zero;
            topRect.offsetMax = Vector2.zero;

            var topMask = topGo.GetComponent<RectMask2D>();
            topMask.padding = Vector4.zero;

            // 高光副本挂在裁剪容器里，位置要抵消容器自身的偏移才能和本体对齐
            Text highlight = MakeLayer(topRect, "Highlight", text, fontSize, font,
                                       TopColor, Vector2.zero);
            highlight.rectTransform.anchoredPosition = new Vector2(0f, -3f);

            return Root;
        }

        private Text MakeLayer(string layerName, string text, int fontSize, Font font,
                               Color color, Vector2 offset)
            => MakeLayer(Root, layerName, text, fontSize, font, color, offset);

        private Text MakeLayer(RectTransform parent, string layerName, string text, int fontSize,
                               Font font, Color color, Vector2 offset)
        {
            var go = new GameObject(layerName, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = offset;

            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = fontSize;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.fontStyle = FontStyle.Bold;

            return t;
        }

        /// <summary>
        /// 副标题：细描边的浅色小字，用来放英文名。
        /// </summary>
        public static Text BuildSubtitle(RectTransform parent, string name, string text,
                                         Vector2 position, int fontSize, Font font)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1400f, fontSize * 2f);
            rect.anchoredPosition = position;

            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = fontSize;
            t.color = new Color(0.86f, 0.80f, 0.58f);
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;

            // 字距靠空格撑开，做出"铭牌"的庄重感
            return t;
        }

        /// <summary>
        /// 标题下方的装饰分隔线：中间亮、两端淡出的两段细线。
        /// </summary>
        public static void BuildDivider(RectTransform parent, Vector2 position, float width)
        {
            MakeLine(parent, "DividerL", position + new Vector2(-width * 0.28f, 0f),
                     new Vector2(width * 0.42f, 3f), new Color(0.95f, 0.78f, 0.35f, 0.9f));
            MakeLine(parent, "DividerR", position + new Vector2(width * 0.28f, 0f),
                     new Vector2(width * 0.42f, 3f), new Color(0.95f, 0.78f, 0.35f, 0.9f));

            MakeLine(parent, "Diamond", position, new Vector2(14f, 14f),
                     new Color(1f, 0.88f, 0.45f, 1f), rotate45: true);
        }

        private static void MakeLine(RectTransform parent, string name, Vector2 position,
                                     Vector2 size, Color color, bool rotate45 = false)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            if (rotate45) rect.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }
    }
}
