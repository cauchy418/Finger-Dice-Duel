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
        public void Page_one_has_two_groups_magic_then_physical()
        {
            GuideSheet s = GuideSheets.DamageTypes();

            // ⚠️ 玩家要求："在这 10 种伤害前上方分类魔法伤害，再添加物理伤害"
            Assert.IsTrue(s.HasGroups, "伤害类型页应当分组。");
            Assert.AreEqual(2, s.Groups.Count);
            Assert.AreEqual("魔法伤害", s.Groups[0].Title);
            Assert.AreEqual("物理伤害", s.Groups[1].Title);

            Assert.AreEqual(10, s.Groups[0].Entries.Count, "魔法伤害 10 种。");
            Assert.AreEqual(3, s.Groups[1].Entries.Count, "物理伤害 3 种。");
            Assert.AreEqual(13, s.Lines.Count, "合计 13 行。");
        }

        [Test]
        public void Magic_group_keeps_force_last_and_its_own_colour()
        {
            GuideGroup magic = GuideSheets.MagicGroup();

            string lastKey = magic.Entries[magic.Entries.Count - 1].IconKey;
            Assert.AreEqual(DamageIcons.KeyOf(DamageType.Force), lastKey,
                "力场应当排在魔法组的最后（它不挂状态）。");

            // 魔法伤害沿用现有配色
            for (int i = 0; i < magic.Entries.Count; i++)
            {
                Assert.IsTrue(magic.Entries[i].Color != 0,
                    $"{magic.Entries[i].Text} 没有颜色。");
                Assert.IsFalse(string.IsNullOrEmpty(magic.Entries[i].IconKey));
            }
        }

        [Test]
        public void Physical_group_is_all_grey_and_pierces_correctly()
        {
            GuideGroup phys = GuideSheets.PhysicalGroup();

            Assert.AreEqual(3, phys.Entries.Count);

            // 玩家要求："三个图标颜色均保持现在造成伤害显示的颜色（灰色）"
            for (int i = 0; i < phys.Entries.Count; i++)
            {
                Assert.AreEqual(DamageIcons.PhysicalColor, phys.Entries[i].Color,
                    $"「{phys.Entries[i].Text}」应当是统一的灰色。");
            }

            // 图标键互不重复（刀 / 锤 / 箭）
            var keys = new List<string>();
            for (int i = 0; i < phys.Entries.Count; i++)
            {
                Assert.IsFalse(keys.Contains(phys.Entries[i].IconKey),
                    $"图标键 {phys.Entries[i].IconKey} 重复了。");
                keys.Add(phys.Entries[i].IconKey);
            }

            Assert.IsTrue(keys.Contains("slashing"));
            Assert.IsTrue(keys.Contains("bludgeoning"));
            Assert.IsTrue(keys.Contains("piercing"));
        }

        [Test]
        public void Physical_lines_describe_the_weapon_kind()
        {
            // 玩家给的定义
            Assert.IsTrue(GuideSheets.PhysicalLine(PhysicalDamageType.Slashing).Contains("刀剑"));
            Assert.IsTrue(GuideSheets.PhysicalLine(PhysicalDamageType.Bludgeoning).Contains("钝器"));
            Assert.IsTrue(GuideSheets.PhysicalLine(PhysicalDamageType.Piercing).Contains("远程"));
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
        public void Page_one_says_what_status_each_magic_type_applies()
        {
            // 玩家给的范例："雷鸣：附加两回合残响状态"
            // ⚠️ 只对**魔法伤害**成立 —— 物理伤害那三类不挂状态。
            GuideGroup magic = GuideSheets.MagicGroup();

            for (int i = 0; i < magic.Entries.Count; i++)
            {
                string line = magic.Entries[i].Text;

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
            // 内容**必须**来自 Core（只有一处真相），
            // 不能在速查表里另写一份说明文字。
            GuideGroup magic = GuideSheets.MagicGroup();

            for (int i = 0; i < magic.Entries.Count; i++)
            {
                bool matched = false;
                foreach (DamageType t in StatusNames.AllTypes)
                    if (magic.Entries[i].Text == StatusNames.GuideLine(t)) matched = true;

                Assert.IsTrue(matched,
                    $"这一行不是 Core 的 GuideLine：「{magic.Entries[i].Text}」。");
            }

            GuideGroup phys = GuideSheets.PhysicalGroup();
            for (int i = 0; i < phys.Entries.Count; i++)
                Assert.AreEqual(GuideSheets.PhysicalLine(GuideSheets.AllPhysical[i]),
                                phys.Entries[i].Text);

            GuideSheet st2 = GuideSheets.Statuses();
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
