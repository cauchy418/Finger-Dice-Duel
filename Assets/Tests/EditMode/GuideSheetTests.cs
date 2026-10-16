using System.Collections.Generic;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **速查表（伤害类型 · 状态）**测试。
    ///
    /// 玩家要求：
    ///   "删除上方显示的法师体系，删除技能/技能详情/操作指南/如何针对，
    ///    这不仅是法师要用，日后做 pve 冒险也很重要。状态一览同理"
    /// 以及（合并的起因）：
    ///   "第一排都显示了两次" —— 原来分成"伤害类型"与"状态一览"两张表，
    ///   而它们的信息**本来就是同一组**，翻页各出现一次就显得重复。
    ///   现已合并为**一张表**：一行同时给出 类型 + 状态 + 效果。
    ///
    /// ⚠️ 数据放在 Core（不是 UI）：**UI 是叶子层、测试项目引用不到**，
    ///    写在那里就没法测。而且它不只服务法师，日后的 PvE 冒险也要用。
    /// </summary>
    [TestFixture]
    public class GuideSheetTests
    {
        [Test]
        public void There_is_exactly_one_merged_sheet()
        {
            List<GuideSheet> sheets = GuideSheets.All();

            Assert.AreEqual(1, sheets.Count,
                "伤害类型与状态已合并成一张表 —— 分成两张会让同一组信息翻页各出现一次。");
            Assert.AreEqual("伤害类型 · 状态", sheets[0].TitleZh);
        }

        [Test]
        public void Sheet_has_no_role_specific_content()
        {
            // ⚠️ 这是**结构性**保证：`GuideSheet` 根本没有
            //    Alignment / ShortSkill / PlayGuide / Counter 这些字段 ——
            //    想往里塞角色卡内容也塞不进去。
            foreach (GuideSheet s in GuideSheets.All())
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.TitleZh), "标题不能空。");
                Assert.IsFalse(string.IsNullOrEmpty(s.TitleEn), "英文标题不能空。");
                Assert.IsTrue(s.Lines.Count > 0, "清单不能空。");

                for (int i = 0; i < s.Lines.Count; i++)
                    Assert.IsFalse(string.IsNullOrEmpty(s.Lines[i]),
                        $"{s.TitleZh} 第 {i} 行是空的。");
            }
        }

        [Test]
        public void Merged_sheet_lists_every_damage_type_exactly_once()
        {
            GuideSheet s = GuideSheets.DamageAndStatuses();

            Assert.AreEqual(10, s.Lines.Count, "10 种伤害类型，每种一行。");
            Assert.IsTrue(s.TwoColumn, "条目多 -> 应当用两列。");

            // ⚠️ 每种类型**只能出现一次** —— 这正是"显示两次"那个问题的守门测试
            foreach (DamageType t in StatusNames.AllTypes)
            {
                string name = StatusNames.Of(t);
                int hits = 0;

                for (int i = 0; i < s.Lines.Count; i++)
                    if (s.Lines[i].Contains(name)) hits++;

                Assert.AreEqual(1, hits,
                    $"「{name}」在合并表里出现了 {hits} 次（应当恰好 1 次）。");
            }
        }

        [Test]
        public void Merged_sheet_covers_every_status()
        {
            GuideSheet s = GuideSheets.DamageAndStatuses();

            // 9 种状态各自都要出现在某一行里（由它对应的伤害类型带出来）
            foreach (StatusEffect st in StatusNames.AllStatuses)
            {
                string name = StatusNames.Of(st);
                bool found = false;

                for (int i = 0; i < s.Lines.Count; i++)
                    if (s.Lines[i].Contains(name)) found = true;

                Assert.IsTrue(found, $"合并表里缺少状态「{name}」。");
            }
        }

        [Test]
        public void Merged_sheet_lines_come_from_the_shared_guide_lines()
        {
            // 内容**必须**来自 StatusNames（只有一处真相），
            // 不能在速查表里另写一份说明文字。
            GuideSheet s = GuideSheets.DamageAndStatuses();

            foreach (string line in s.Lines)
            {
                bool matched = false;
                foreach (DamageType t in StatusNames.AllTypes)
                    if (line == StatusNames.GuideLine(t)) matched = true;

                Assert.IsTrue(matched,
                    $"这一行不是 Core 的 GuideLine：「{line}」。");
            }
        }

        [Test]
        public void Force_is_listed_last_and_says_it_has_no_status()
        {
            GuideSheet s = GuideSheets.DamageAndStatuses();
            string last = s.Lines[s.Lines.Count - 1];

            Assert.IsTrue(last.Contains(StatusNames.Of(DamageType.Force)),
                "力场应当排在最后一行（它不挂状态，与其余 9 行不同类）。");
            Assert.IsTrue(last.Contains("不挂状态"), "力场的说明要写明它不挂状态。");
        }
    }
}
