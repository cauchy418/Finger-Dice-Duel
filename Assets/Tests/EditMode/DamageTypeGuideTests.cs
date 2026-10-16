using System.Collections.Generic;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **法师的伤害类型 / 状态指引数据**测试。
    ///
    /// ⚠️ 这些文字原来是写死在 UI 层（`GameBootstrap.BuildDamageTypeCard`）的，
    ///    而 **UI 是叶子层、测试项目引用不到**（分层铁律），于是没法测。
    ///    已经移到 Core 的 <see cref="StatusNames"/>：
    ///    界面、规则文档、测试三边共用同一份文案（只有一处真相）。
    /// </summary>
    [TestFixture]
    public class DamageTypeGuideTests
    {
        [Test]
        public void There_are_exactly_ten_damage_types()
        {
            Assert.AreEqual(10, StatusNames.AllTypes.Length);
        }

        [Test]
        public void There_are_exactly_nine_statuses()
        {
            Assert.AreEqual(9, StatusNames.AllStatuses.Length,
                "力场不挂状态，所以是 10 种类型 / 9 种状态。");
        }

        [Test]
        public void Type_list_covers_every_enum_value_exactly_once()
        {
            var seen = new List<DamageType>();

            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                Assert.IsTrue(t != DamageType.None, "清单里不该有 None。");
                Assert.IsFalse(seen.Contains(t), $"类型 {t} 重复了。");
                seen.Add(t);
            }

            // 枚举里除 None 之外的每一种都必须被列出
            foreach (DamageType t in System.Enum.GetValues(typeof(DamageType)))
            {
                if (t == DamageType.None) continue;
                Assert.IsTrue(seen.Contains(t), $"清单里缺少 {t}。");
            }
        }

        [Test]
        public void Every_non_force_type_maps_to_a_distinct_status()
        {
            var seen = new List<StatusEffect>();

            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                StatusEffect st = StatusNames.StatusOf(t);

                if (t == DamageType.Force)
                {
                    Assert.AreEqual(StatusEffect.None, st, "力场不挂状态。");
                    continue;
                }

                Assert.IsTrue(st != StatusEffect.None,
                    $"{t} 应当有对应状态。");
                Assert.IsFalse(seen.Contains(st), $"状态 {st} 被两种类型共用。");
                seen.Add(st);
            }

            Assert.AreEqual(9, seen.Count);
        }

        [Test]
        public void Every_guide_line_starts_with_its_coloured_status_glyph()
        {
            // 玩家要求："状态一栏每一个状态的标记也放入"，且"图标要带上颜色"。
            // 所以每一行都以 **<color=#RRGGBB>符号</color>** 开头。
            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                string line = StatusNames.GuideLine(t);
                StatusEffect st = StatusNames.StatusOf(t);

                Assert.IsFalse(string.IsNullOrEmpty(line), $"{t} 的指引文字空了。");
                Assert.IsTrue(line.Contains(StatusNames.Of(t)),
                    $"{t} 的指引里应当出现类型名「{StatusNames.Of(t)}」。");

                if (st == StatusEffect.None)
                {
                    // 力场没有状态，但**玩家要求它也要有颜色**（更深的红色）——
                    // 所以它用单独的颜色入口，而不是不着色。
                    Assert.IsTrue(line.StartsWith(ColorTag(StatusEffect.None)),
                        $"力场那一行应当以深红的符号开头（现在是「{line}」）。");
                    continue;
                }

                Assert.IsTrue(line.StartsWith(ColorTag(st)),
                    $"{t} 的指引应当以带颜色的状态符号开头（现在是「{line}」）。");
            }
        }

        [Test]
        public void Force_line_uses_the_dark_red_and_psychic_uses_a_lighter_pink()
        {
            // 玩家要求："力场采用更深的红色"、"心灵现在红色太深，换成比火焰淡的粉色"
            string force = StatusNames.GuideLine(DamageType.Force);
            Assert.IsTrue(force.Contains(StatusNames.ForceColor.ToString("X6")),
                $"力场那一行应当用深红 0x{StatusNames.ForceColor:X6}。");

            int psychic = StatusNames.StatusColorOf(StatusEffect.Stun);
            int burn = StatusNames.StatusColorOf(StatusEffect.Burn);

            Assert.IsTrue(psychic != burn, "心灵与火焰不能是同一个颜色。");

            // "比火焰淡"：亮度更高、饱和度更低（用简单判据：整体亮度更大）
            int pl = (psychic >> 16 & 0xFF) + (psychic >> 8 & 0xFF) + (psychic & 0xFF);
            int bl = (burn >> 16 & 0xFF) + (burn >> 8 & 0xFF) + (burn & 0xFF);
            Assert.IsTrue(pl > bl,
                $"心灵（0x{psychic:X6}）应当比火焰（0x{burn:X6}）更淡（整体亮度更高）。");
        }

        [Test]
        public void Every_status_line_starts_with_its_coloured_glyph()
        {
            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                StatusEffect st = StatusNames.AllStatuses[i];
                string line = StatusNames.StatusLine(st);

                Assert.IsTrue(line.StartsWith(ColorTag(st)),
                    $"状态「{StatusNames.Of(st)}」的一行应当以带颜色的符号开头（现在是「{line}」）。");
                Assert.IsTrue(line.Contains(StatusNames.Describe(st)),
                    $"状态「{StatusNames.Of(st)}」的一行应当包含它的效果说明。");
            }
        }

        /// <summary>
        /// 拼出期望的颜色标签（与 Core 的写法一致）。
        ///
        /// ⚠️ 这里刻意**不**从 Core 拿现成的字符串，而是自己按格式拼 ——
        ///    否则"格式写错了"也会被测试一并接受（自我实现式断言）。
        /// </summary>
        private static string ColorTag(StatusEffect st)
        {
            int rgb = st == StatusEffect.None
                ? StatusNames.ForceColor          // 力场没有状态，用单独的颜色入口
                : StatusNames.StatusColorOf(st);

            return "<color=#" + rgb.ToString("X6") + ">"
                 + StatusNames.GlyphOf(st) + "</color>";
        }

        [Test]
        public void Every_glyph_is_known_to_render_in_this_project()
        {
            // ⚠️ 血泪教训：闪电换了两次字形都还是空白，最后读
            //    NotoSansCJKsc 的 cmap 才发现主字体里真的没有。
            //
            //    ⚠️ 但**不能只看主字体** —— 玩家实测：☠ / ✖ / ❄ 在主字体里
            //       同样是"没有字形"，可它们**实际能显示**（Unity 会回退到
            //       内置字体）。我把这三个误判成缺字形、换掉之后玩家要求回退。
            //
            //    所以这份清单只放**实测确认渲染失败**的码位：
            var broken = new Dictionary<int, string>
            {
                { 0x26A1, "⚡ 闪电 —— 实测空白" },
                { 0x2607, "☇ 闪电 —— 主字体没有，且回退也没有" },
                { 0x2A1C, "⧜ 闪电 —— 主字体没有，且回退也没有" },
                { 0x2733, "✳ 星芒 —— 实测显示成方框" },
            };

            for (int i = 0; i <= 9; i++)
            {
                var st = (StatusEffect)i;
                string g = StatusNames.GlyphOf(st);
                if (g.Length == 0) continue;

                int cp = g[0];
                string why;
                Assert.IsFalse(broken.TryGetValue(cp, out why),
                    $"状态「{StatusNames.Of(st)}」用的是 {why}"
                    + " —— 请换一个实测能显示的字符。");
            }
        }

        [Test]
        public void Guide_colours_are_distinct_per_status()
        {
            // 颜色是"一眼分辨"的辅助手段，重复了就没意义
            var seen = new List<int>();

            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                int c = StatusNames.StatusColorOf(StatusNames.AllStatuses[i]);

                Assert.IsFalse(seen.Contains(c),
                    $"颜色 0x{c:X6} 被两个状态共用了。");
                seen.Add(c);
            }
        }

        [Test]
        public void Shock_glyph_is_not_a_broken_lightning_char()
        {
            // ⚠️ 玩家实测：状态一览里"感电"那一行前面是**空白** ——
            //    U+26A1(⚡) 与 U+2A1C 主字体都没有、回退也没有。
            //    现在用 ⚠(U+26A0)。这条防止有人"顺手改回去"。
            string g = StatusNames.GlyphOf(StatusEffect.Shock);

            Assert.IsFalse(string.IsNullOrEmpty(g), "感电必须有符号。");

            int cp = g[0];
            Assert.IsTrue(cp != 0x26A1 && cp != 0x2A1C && cp != 0x2607,
                $"不能再用 U+{cp:X4} —— 实测渲染成空白。");
        }

        [Test]
        public void Every_glyph_is_distinct_and_non_empty()
        {
            var seen = new List<string>();

            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                string g = StatusNames.GlyphOf(StatusNames.AllStatuses[i]);

                Assert.IsFalse(string.IsNullOrEmpty(g),
                    $"{StatusNames.Of(StatusNames.AllStatuses[i])} 没有符号。");
                Assert.IsFalse(seen.Contains(g), $"符号「{g}」被两个状态共用了。");
                seen.Add(g);
            }

            // 力场没有状态，但也需要一个占位符号（否则那一行会缺一格）
            Assert.IsFalse(string.IsNullOrEmpty(StatusNames.GlyphOf(StatusEffect.None)));
            Assert.IsFalse(seen.Contains(StatusNames.GlyphOf(StatusEffect.None)),
                "占位符号不该与任何真实状态重复。");
        }

        [Test]
        public void Force_line_says_it_pierces_and_cannot_be_dodged()
        {
            string line = StatusNames.GuideLine(DamageType.Force);

            Assert.IsTrue(line.Contains("不可闪避"), "玩家确认：力场同咒术师，无法被任何方式抵挡/闪避。");
            Assert.IsTrue(line.Contains("不挂状态"), "力场不附带任何持续状态，必须写明。");
        }

        [Test]
        public void Guide_lines_mention_the_players_rules()
        {
            // 抽几条容易被写错的：逐条对照玩家给的原话
            Assert.IsTrue(StatusNames.GuideLine(DamageType.Cold).Contains("减半"),
                "寒冷 = 结果减半");
            Assert.IsTrue(StatusNames.GuideLine(DamageType.Poison).Contains("不可被抵挡"),
                "毒素不可被抵挡");
            Assert.IsTrue(StatusNames.GuideLine(DamageType.Necrotic).Contains("不能回复"),
                "暗蚀 = 不能回血");
            Assert.IsTrue(StatusNames.GuideLine(DamageType.Thunder).Contains("距离 1"),
                "雷鸣 = 只能打距离 1");
            Assert.IsTrue(StatusNames.GuideLine(DamageType.Radiant).Contains("距离 1"),
                "光耀 = 只能选距离 1 的手");
        }
    }
}
