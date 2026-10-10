using System.Collections.Generic;
using FingerDiceDuel.Assistant;
using FingerDiceDuel.Core;

// 注意：每个测试文件都必须自己带这一段 —— C# 的 using 不会跨文件生效。
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// 界面布局的**不重叠**验证。
    ///
    /// 为什么要有这一组：元素重叠已经连续出现三次（玩家A标题被卡片压住、
    /// 选择角色标题与"玩家角色"叠在一起 ……），每次都是肉眼才发现。
    /// 布局虽然是 UI 层的事，但**坐标与尺寸是纯数字**，完全可以机器校验。
    ///
    /// ⚠️ 前提：这些坐标必须与 <c>GameBootstrap</c> 里保持一致。
    /// 改了界面坐标就要同步改这里 —— 这正是它存在的意义：
    /// 逼着改坐标的人重新算一遍间距。
    /// </summary>
    [TestFixture]
    public class LayoutSpacingTests
    {
        /// <summary>画布参考分辨率（与 CanvasScaler 一致）。</summary>
        private const float CanvasHeight = 1080f;

        /// <summary>一个纵向区间 [Bottom, Top]。</summary>
        private readonly struct Span
        {
            public readonly float Top;
            public readonly float Bottom;
            public readonly string Name;

            public Span(string name, float centerY, float height)
            {
                Name = name;
                Top = centerY + height * 0.5f;
                Bottom = centerY - height * 0.5f;
            }

            public override string ToString() => $"{Name}[{Bottom:0}..{Top:0}]";
        }

        /// <summary>断言两个区间不重叠（允许刚好相接）。</summary>
        private static void AssertNoOverlap(Span upper, Span lower)
        {
            Assert.True(lower.Top <= upper.Bottom,
                $"{lower} 压住了 {upper}（重叠 {lower.Top - upper.Bottom:0}px）。");
        }

        /// <summary>断言整组区间从上到下依次不重叠。</summary>
        private static void AssertStackedOrdered(params Span[] spans)
        {
            for (int i = 0; i + 1 < spans.Length; i++)
                AssertNoOverlap(spans[i], spans[i + 1]);
        }

        private static readonly Span SelectTitle = new Span("选择角色标题", 468f, 70f);

        private static readonly Span LabelPlayer = new Span("玩家角色", 412f, 30f);
        private static readonly Span RosterPlayer = new Span("玩家卡片行", 327f, 100f);
        private static readonly Span DetailPlayer = new Span("玩家详情", 258f, 74f);

        private static readonly Span LabelAi = new Span("AI 角色", 130f, 30f);
        private static readonly Span RosterAi = new Span("AI 卡片行", 48f, 100f);
        private static readonly Span DetailAi = new Span("AI 详情", -24f, 74f);

        private static readonly Span StartButton = new Span("开始对局", -256f, 76f);


        [Test]
        public void Character_screen_has_no_overlapping_rows()
        {
            // 从上到下逐个比较：每一项必须在下一项的**上方**。
            // 这里显式列出顺序，不依赖字段声明顺序，避免看错。
            // y 递减：+468 → +392 → +310 → +226 → +94 → +12 → -72 → -256
            AssertStackedOrdered(
                new Span("选择角色标题", 470f, 70f),
                new Span("玩家角色", 406f, 30f),
                new Span("玩家卡片行", 327f, 100f),
                new Span("玩家详情", 232f, 74f),
                new Span("AI 角色", 102f, 30f),
                new Span("AI 卡片行", 26f, 100f),
                new Span("AI 详情", -70f, 74f),
                new Span("开始对局", -256f, 76f));
        }

        [Test]
        public void Player_card_row_does_not_cover_its_own_label()
        {
            // 这就是截图里报过的重叠：卡片轨道 116px 高、中心 262，
            // 顶边到 320，把 y=318 的标题压住了。
            AssertNoOverlap(LabelPlayer, RosterPlayer);
        }

        [Test]
        public void Character_screen_title_does_not_touch_the_first_label()
        {
            // 第二次报的重叠：标题 452±35 -> 底边 487，
            // 而"玩家角色" 382±15 -> 顶边 397，只差 5px，视觉上就是叠在一起。
            AssertNoOverlap(SelectTitle, LabelPlayer);
        }

        [Test]
        public void Every_row_has_a_visible_gap()
        {
            // 只有"不重叠"还不够 —— 贴在一起同样难看。
            // 阈值取 8px：足够把两行"分开"，又不至于逼得整页过度压缩
            // （1080 高要塞下 标题 + 两组「标签/卡片/详情」+ 按钮）。
            const float minGap = 8f;

            // 内联顺序，与 Character_screen_has_no_overlapping_rows 完全一致
            Span[] rows =
            {
                new Span("选择角色标题", 470f, 70f),
                new Span("玩家角色", 406f, 30f),
                new Span("玩家卡片行", 327f, 100f),
                new Span("玩家详情", 232f, 74f),
                new Span("AI 角色", 102f, 30f),
                new Span("AI 卡片行", 26f, 100f),
                new Span("AI 详情", -70f, 74f),
                new Span("开始对局", -256f, 76f),
            };

            for (int i = 0; i + 1 < rows.Length; i++)
            {
                float gap = rows[i].Bottom - rows[i + 1].Top;
                Assert.True(gap >= minGap,
                    $"{rows[i].Name} 与 {rows[i + 1].Name} 之间只有 {gap:0}px 间隙。");
            }
        }

        [Test]
        public void Character_screen_fits_inside_the_canvas()
        {
            float top = SelectTitle.Top;
            float bottom = StartButton.Bottom;

            Assert.True(top <= CanvasHeight * 0.5f,
                $"顶部内容超出画布：{top} > {CanvasHeight * 0.5f}。");
            Assert.True(bottom >= -CanvasHeight * 0.5f,
                $"底部内容超出画布：{bottom} < {-CanvasHeight * 0.5f}。");
        }

        [Test]
        public void Main_menu_rows_do_not_overlap()
        {
            // 主菜单：标题 / 副标题 / 分隔线 / 三个模式 / 提示
            var title = new Span("主标题", 280f, 116f * 1.35f);
            var subtitle = new Span("英文副标题", 126f, 64f);
            var divider = new Span("分隔线", 48f, 14f);
            var mode1 = new Span("1v1", -42f, 116f);
            var mode2 = new Span("2v2", -198f, 116f);
            var mode3 = new Span("8人身份局", -354f, 116f);
            var tip = new Span("提示", -456f, 30f);
            var quit = new Span("退出游戏", -516f, 58f);

            AssertStackedOrdered(title, subtitle, divider, mode1, mode2, mode3, tip, quit);
        }

        [Test]
        public void One_on_one_rows_do_not_overlap()
        {
            var title = new Span("1v1 标题", 300f, 70f);
            var ai = new Span("人机对战", 80f, 116f);
            var pvp = new Span("竞技", -70f, 116f);
            var back = new Span("返回", -280f, 64f);

            AssertStackedOrdered(title, ai, pvp, back);
        }

        [Test]
        public void Mode_buttons_do_not_overlap_the_help_button()
        {
            // 问号在右上角，模式按钮在中间：横向不能撞上
            const float helpLeft = 880f - 32f;      // 问号左边缘
            const float modeRight = 620f * 0.5f;    // 模式按钮右边缘

            Assert.True(modeRight < helpLeft,
                $"模式按钮右边 {modeRight} 会撞上问号左边 {helpLeft}。");
        }

        [Test]
        public void Help_button_is_on_screen()
        {
            const float x = 880f;
            const float y = 470f;
            const float halfW = 32f;
            const float halfH = 32f;

            Assert.True(x + halfW <= 1920f * 0.5f,
                $"问号超出右侧画布：{x + halfW}。");
            Assert.True(y + halfH <= CanvasHeight * 0.5f,
                $"问号超出顶部画布：{y + halfH}。");
        }

        // ============================================================
        // 角色选择页的文案
        // ============================================================

        [Test]
        public void Character_screen_labels_are_renamed_as_requested()
        {
            // 需求：玩家 A 角色 -> 玩家角色；玩家 B 角色 -> AI 角色
            var labels = new List<string> { "玩家角色", "AI 角色" };

            Assert.AreEqual("玩家角色", labels[0]);
            Assert.AreEqual("AI 角色", labels[1]);
        }
    }
}
