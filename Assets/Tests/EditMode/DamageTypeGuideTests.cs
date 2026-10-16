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
        public void Every_guide_line_starts_with_its_status_glyph()
        {
            // 玩家要求："状态一栏每一个状态的标记也放入" ——
            // 图鉴的指南页要带**与状态图标栏同源**的符号。
            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                string line = StatusNames.GuideLine(t);
                string glyph = StatusNames.GlyphOf(StatusNames.StatusOf(t));

                Assert.IsFalse(string.IsNullOrEmpty(line), $"{t} 的指引文字空了。");
                Assert.IsTrue(line.StartsWith(glyph),
                    $"{t} 的指引应当以它对应状态的符号「{glyph}」开头（现在是「{line}」）。");
                Assert.IsTrue(line.Contains(StatusNames.Of(t)),
                    $"{t} 的指引里应当出现类型名「{StatusNames.Of(t)}」。");
            }
        }

        [Test]
        public void Every_status_line_starts_with_its_glyph()
        {
            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                StatusEffect st = StatusNames.AllStatuses[i];
                string line = StatusNames.StatusLine(st);
                string glyph = StatusNames.GlyphOf(st);

                Assert.IsTrue(line.StartsWith(glyph),
                    $"状态「{StatusNames.Of(st)}」的一行应当以「{glyph}」开头（现在是「{line}」）。");
                Assert.IsTrue(line.Contains(StatusNames.Describe(st)),
                    $"状态「{StatusNames.Of(st)}」的一行应当包含它的效果说明。");
            }
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
