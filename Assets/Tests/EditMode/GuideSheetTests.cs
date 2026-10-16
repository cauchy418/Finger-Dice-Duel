using System.Collections.Generic;
using FingerDiceDuel.Core;

#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif

namespace FingerDiceDuel.Tests
{
    /// <summary>
    /// **速查表（伤害类型 / 状态一览）**测试。
    ///
    /// 玩家要求：
    ///   "删除上方显示的法师体系，删除技能/技能详情/操作指南/如何针对，
    ///    这不仅是法师要用，日后做 pve 冒险也很重要。状态一览同理"
    ///
    /// ⚠️ 这两张表原来写死在 UI 层（`GameBootstrap.BuildDamageTypeCard`），
    ///    而 **UI 是叶子层、测试项目引用不到** —— 根本没法测。
    ///    已挪到 Core 的 <see cref="GuideSheets"/>：
    ///    界面、测试、日后的 PvE 冒险三边共用同一份数据。
    /// </summary>
    [TestFixture]
    public class GuideSheetTests
    {
        [Test]
        public void There_are_two_sheets_in_order()
        {
            List<GuideSheet> sheets = GuideSheets.All();

            Assert.AreEqual(2, sheets.Count);
            Assert.AreEqual("伤害类型", sheets[0].TitleZh, "第 0 页应当是伤害类型。");
            Assert.AreEqual("状态一览", sheets[1].TitleZh, "第 1 页应当是状态一览。");
        }

        [Test]
        public void Sheets_have_no_role_specific_sections()
        {
            List<GuideSheet> sheets = GuideSheets.All();

            for (int i = 0; i < sheets.Count; i++)
            {
                GuideSheet s = sheets[i];

                // 速度表只有"标题 + 行"。这几条其实是**结构性**保证：
                // GuideSheet 根本没有 Alignment / ShortSkill / PlayGuide / Counter
                // 这些字段 —— 想写也写不进去。
                Assert.IsFalse(string.IsNullOrEmpty(s.TitleZh), "标题不能空。");
                Assert.IsFalse(string.IsNullOrEmpty(s.TitleEn), "英文标题不能空。");
                Assert.IsTrue(s.Lines.Count > 0, "清单不能空。");

                for (int k = 0; k < s.Lines.Count; k++)
                    Assert.IsFalse(string.IsNullOrEmpty(s.Lines[k]),
                        $"{s.TitleZh} 第 {k} 行是空的。");
            }
        }

        [Test]
        public void Damage_type_sheet_lists_all_ten()
        {
            GuideSheet s = GuideSheets.DamageTypes();

            Assert.AreEqual(10, s.Lines.Count);
            Assert.IsTrue(s.TwoColumn, "条目短、行数多 -> 应当用两列。");

            foreach (DamageType t in StatusNames.AllTypes)
            {
                bool found = false;
                for (int i = 0; i < s.Lines.Count; i++)
                    if (s.Lines[i].Contains(StatusNames.Of(t))) found = true;

                Assert.IsTrue(found, $"伤害类型表里缺少「{StatusNames.Of(t)}」。");
            }
        }

        [Test]
        public void Status_sheet_lists_all_nine()
        {
            GuideSheet s = GuideSheets.Statuses();

            Assert.AreEqual(9, s.Lines.Count);
            Assert.IsTrue(s.TwoColumn);

            foreach (StatusEffect st in StatusNames.AllStatuses)
            {
                bool found = false;
                for (int i = 0; i < s.Lines.Count; i++)
                    if (s.Lines[i].Contains(StatusNames.Of(st))) found = true;

                Assert.IsTrue(found, $"状态表里缺少「{StatusNames.Of(st)}」。");
            }
        }

        [Test]
        public void Damage_type_sheet_uses_the_shared_guide_lines()
        {
            // 内容**必须**来自 StatusNames（只有一处真相），
            // 不能在速查表里另写一份说明文字。
            GuideSheet s = GuideSheets.DamageTypes();

            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                Assert.AreEqual(StatusNames.GuideLine(StatusNames.AllTypes[i]),
                                s.Lines[i],
                    "伤害类型表的第 " + i + " 行应当就是 Core 的 GuideLine。");
            }
        }

        [Test]
        public void Status_sheet_uses_the_shared_status_lines()
        {
            GuideSheet s = GuideSheets.Statuses();

            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                Assert.AreEqual(StatusNames.StatusLine(StatusNames.AllStatuses[i]),
                                s.Lines[i],
                    "状态表的第 " + i + " 行应当就是 Core 的 StatusLine。");
            }
        }
    }
}
