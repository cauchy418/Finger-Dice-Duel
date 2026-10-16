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
    /// 玩家要求（本轮）：
    ///   · "伤害类型和状态还是分成两页" -> 第 1 页「伤害类型」，第 2 页「状态一览」
    ///   · 第 1 页写"雷鸣：附加两回合残响状态"
    ///   · 第 2 页再解释"残响"具体做什么
    ///   · "-> 的部分删除没有意义" -> 文案里不再出现 →
    ///
    /// ⚠️ 数据放在 Core（不是 UI）：**UI 是叶子层、测试项目引用不到**，
    ///    写在那里就没法测。而且它不只服务法师，日后的 PvE 冒险也要用。
    /// </summary>
    [TestFixture]
    public class GuideSheetTests
    {
        [Test]
        public void There_are_two_pages_in_order()
        {
            List<GuideSheet> sheets = GuideSheets.All();

            Assert.AreEqual(2, sheets.Count, "玩家要求分成两页。");
            Assert.AreEqual("伤害类型", sheets[0].TitleZh, "第 1 页应当是伤害类型。");
            Assert.AreEqual("状态一览", sheets[1].TitleZh, "第 2 页应当是状态一览。");
        }

        [Test]
        public void Page_one_lists_ten_types_including_force_last()
        {
            GuideSheet s = GuideSheets.DamageTypes();

            Assert.AreEqual(10, s.Lines.Count, "10 种伤害类型，每种一行。");
            Assert.IsTrue(s.TwoColumn, "条目多 -> 应当用两列。");

            string last = s.Lines[s.Lines.Count - 1];
            Assert.IsTrue(last.Contains(StatusNames.Of(DamageType.Force)),
                "力场应当排在最后一行（它不挂状态，与其余 9 行不同类）。");
            Assert.IsTrue(last.Contains("不挂状态"), "力场的说明要写明它不挂状态。");
        }

        [Test]
        public void Page_two_lists_nine_statuses()
        {
            GuideSheet s = GuideSheets.Statuses();

            Assert.AreEqual(9, s.Lines.Count, "9 种负面状态，每种一行。");
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
        public void Page_one_says_what_status_each_type_applies()
        {
            // 玩家给的范例："雷鸣：附加两回合残响状态"
            GuideSheet s = GuideSheets.DamageTypes();

            for (int i = 0; i < s.Lines.Count; i++)
            {
                string line = s.Lines[i];

                // 力场那一行是"不挂状态"，其余都必须写明"附加两回合XX状态"
                if (line.Contains(StatusNames.Of(DamageType.Force))) continue;

                Assert.IsTrue(line.Contains("附加两回合"),
                    $"「{line}」应当写明「附加两回合」（玩家要求说清挂多久）。");
                Assert.IsTrue(line.Contains("状态"),
                    $"「{line}」应当以「…状态」结尾。");
            }
        }

        [Test]
        public void Guide_lines_do_not_use_the_arrow()
        {
            // 玩家原话："-> 的部分删除没有意义"
            foreach (GuideSheet s in GuideSheets.All())
            {
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    Assert.IsFalse(s.Lines[i].Contains("→"),
                        $"「{s.Lines[i]}」里不该再有箭头。");
                    Assert.IsFalse(s.Lines[i].Contains("->"),
                        $"「{s.Lines[i]}」里不该再有箭头。");
                }
            }
        }

        [Test]
        public void Page_two_explains_the_effects()
        {
            GuideSheet s = GuideSheets.Statuses();

            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                StatusEffect st = StatusNames.AllStatuses[i];
                Assert.IsTrue(s.Lines[i].Contains(StatusNames.Describe(st)),
                    $"「{StatusNames.Of(st)}」那一行应当包含它的效果说明。");
            }
        }

        [Test]
        public void Sheets_share_the_core_texts()
        {
            // 内容**必须**来自 StatusNames（只有一处真相），
            // 不能在速查表里另写一份说明文字。
            GuideSheet d = GuideSheets.DamageTypes();
            GuideSheet st2 = GuideSheets.Statuses();

            for (int i = 0; i < d.Lines.Count; i++)
            {
                bool matched = false;
                foreach (DamageType t in StatusNames.AllTypes)
                    if (d.Lines[i] == StatusNames.GuideLine(t)) matched = true;

                Assert.IsTrue(matched, $"这一行不是 Core 的 GuideLine：「{d.Lines[i]}」。");
            }

            for (int i = 0; i < st2.Lines.Count; i++)
                Assert.AreEqual(StatusNames.StatusLine(StatusNames.AllStatuses[i]),
                                st2.Lines[i]);
        }

        [Test]
        public void Sheet_has_no_role_specific_content()
        {
            // ⚠️ 结构性保证：`GuideSheet` 根本没有
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
    }
}
