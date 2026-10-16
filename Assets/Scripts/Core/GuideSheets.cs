using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 一张**速查表**（标题 + 一行行条目）。
    ///
    /// ⚠️ 为什么放在 Core、而不是直接写进 UI 的图鉴卡片：
    ///    UI 是**叶子层，测试项目引用不到** —— 写在那里就没法测。
    ///    而"伤害类型 / 状态一览"这两张表**不只是法师要用**
    ///    （玩家原话："这不仅是法师要用，日后做 pve 冒险也很重要"），
    ///    所以它本来就应该是一份**中立的数据**，由界面去渲染。
    /// </summary>
    public sealed class GuideSheet
    {
        public string TitleZh { get; }
        public string TitleEn { get; }

        /// <summary>一行行条目（已带彩色符号）。</summary>
        public IReadOnlyList<string> Lines { get; }

        /// <summary>是否用两列排版（条目短、行数多时用）。</summary>
        public bool TwoColumn { get; }

        /// <summary>
        /// **本页的分组**（可选）：每组一个标题 + 若干行。
        ///
        /// ⚠️ 玩家要求"在这 10 种伤害前上方分类魔法伤害，再添加物理伤害"——
        ///    所以"伤害类型"这一页要分两段：魔法伤害 / 物理伤害。
        ///    界面按组渲染标题，组内每行前面画**代码绘制的图标**。
        /// </summary>
        public IReadOnlyList<GuideGroup> Groups { get; }

        public GuideSheet(string titleZh, string titleEn,
                          IReadOnlyList<string> lines, bool twoColumn,
                          IReadOnlyList<GuideGroup> groups = null)
        {
            TitleZh = titleZh;
            TitleEn = titleEn;
            Lines = lines;
            TwoColumn = twoColumn;
            Groups = groups;
        }

        /// <summary>本页是否有分组。</summary>
        public bool HasGroups => Groups != null && Groups.Count > 0;
    }

    /// <summary>速查表里的一组（一个分类标题 + 若干条目）。</summary>
    public sealed class GuideGroup
    {
        /// <summary>分类标题（例如"魔法伤害"）。</summary>
        public string Title { get; }

        /// <summary>组内条目（每条自带图标键与颜色，界面用来画图标）。</summary>
        public IReadOnlyList<GuideEntry> Entries { get; }

        public GuideGroup(string title, IReadOnlyList<GuideEntry> entries)
        {
            Title = title;
            Entries = entries;
        }
    }

    /// <summary>速查表里的一条：图标键 + 颜色 + 文字。</summary>
    public sealed class GuideEntry
    {
        /// <summary>图标键（交给 <see cref="DamageIcons.Render"/> 画）。</summary>
        public string IconKey { get; }

        /// <summary>图标颜色（0xRRGGBB）。</summary>
        public int Color { get; }

        /// <summary>条目文字。</summary>
        public string Text { get; }

        public GuideEntry(string iconKey, int color, string text)
        {
            IconKey = iconKey;
            Color = color;
            Text = text;
        }
    }

    /// <summary>
    /// **伤害类型 / 状态的速查表**（界面与测试共用同一份数据）。
    ///
    /// ⚠️ 这几张表是**纯速查** —— 没有归属标签、没有技能/指南/针对小节。
    ///    原因：它们不只服务法师，日后 PvE 冒险同样要用
    ///    （玩家原话："这不仅是法师要用，日后做 pve 冒险也很重要"）。
    /// </summary>
    public static class GuideSheets
    {
        /// <summary>魔法伤害：名称（与 <see cref="DamageType"/> 一一对应）。</summary>
        public static string NameOf(DamageType t) => StatusNames.Of(t);

        /// <summary>物理伤害：名称。</summary>
        public static string NameOf(PhysicalDamageType t)
        {
            switch (t)
            {
                case PhysicalDamageType.Slashing: return "挥砍";
                case PhysicalDamageType.Bludgeoning: return "钝击";
                case PhysicalDamageType.Piercing: return "穿刺";
                default: return "无";
            }
        }

        /// <summary>
        /// 物理伤害的一行文字。图标由界面用 <see cref="DamageIcons"/> 绘制，
        /// 所以这里**只给文字**（与魔法伤害那种"符号 + 文字"分开处理）。
        /// </summary>
        public static string PhysicalLine(PhysicalDamageType t)
        {
            switch (t)
            {
                case PhysicalDamageType.Slashing: return "挥砍：刀剑类造成的伤害";
                case PhysicalDamageType.Bludgeoning: return "钝击：钝器造成的伤害";
                case PhysicalDamageType.Piercing: return "穿刺：远程武器或刺剑类造成的伤害";
                default: return "无";
            }
        }

        /// <summary>
        /// **魔法伤害**这一类的标题行。玩家要求：
        /// "在这 10 种伤害前上方分类魔法伤害"。
        /// </summary>
        public const string MagicCategoryTitle = "魔法伤害";

        /// <summary>**物理伤害**这一类的标题行。玩家要求"再添加物理伤害"。</summary>
        public const string PhysicalCategoryTitle = "物理伤害";

        /// <summary>魔法伤害的 9 种（挂状态的），按 <see cref="StatusNames.AllTypes"/> 顺序。</summary>
        public static List<DamageType> MagicTypesWithStatus()
        {
            var list = new List<DamageType>();
            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                if (StatusNames.StatusOf(t) != StatusEffect.None) list.Add(t);
            }
            return list;
        }

        /// <summary>魔法伤害里不挂状态的（目前只有力场）。</summary>
        public static List<DamageType> MagicTypesWithoutStatus()
        {
            var list = new List<DamageType>();
            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                if (StatusNames.StatusOf(t) == StatusEffect.None) list.Add(t);
            }
            return list;
        }

        /// <summary>全部物理伤害（顺序即显示顺序）。</summary>
        public static readonly PhysicalDamageType[] AllPhysical =
        {
            PhysicalDamageType.Slashing,
            PhysicalDamageType.Bludgeoning,
            PhysicalDamageType.Piercing,
        };

        /// <summary>魔法伤害的颜色（沿用状态色；力场用它自己的深红）。</summary>
        public static int ColorOf(DamageType t) => StatusNames.GuideColorOf(t);

        /// <summary>物理伤害的颜色（统一中性灰）。</summary>
        public static int ColorOf(PhysicalDamageType t) => DamageIcons.PhysicalColor;

        /// <summary>魔法伤害的图标键。</summary>
        public static string IconKeyOf(DamageType t) => DamageIcons.KeyOf(t);

        /// <summary>物理伤害的图标键。</summary>
        public static string IconKeyOf(PhysicalDamageType t) => DamageIcons.KeyOf(t);

        /// <summary>魔法伤害的一行文字。</summary>
        public static string MagicLine(DamageType t) => StatusNames.GuideLine(t);

        /// <summary>魔法伤害的 10 种（含力场，力场排最后）。</summary>
        public static List<DamageType> MagicTypesOrdered()
        {
            var list = MagicTypesWithStatus();
            list.AddRange(MagicTypesWithoutStatus());
            return list;
        }

        /// <summary>魔法伤害的 10 行文字。</summary>
        public static List<string> MagicLines()
        {
            List<DamageType> types = MagicTypesOrdered();
            var lines = new List<string>(types.Count);
            for (int i = 0; i < types.Count; i++) lines.Add(MagicLine(types[i]));
            return lines;
        }

        /// <summary>物理伤害的 3 行文字。</summary>
        public static List<string> PhysicalLines()
        {
            var lines = new List<string>(AllPhysical.Length);
            for (int i = 0; i < AllPhysical.Length; i++)
                lines.Add(PhysicalLine(AllPhysical[i]));
            return lines;
        }

        /// <summary>魔法球入口要显示的全部速查表（顺序即翻页顺序）。</summary>
        public static List<GuideSheet> All()
            => new List<GuideSheet> { DamageTypes(), Statuses() };

        /// <summary>
        /// **第 1 页「伤害类型」**：10 种类型，各自"附加两回合XX状态"。
        ///
        /// ⚠️ 玩家要求分两页：第 1 页只说"这个类型挂什么状态"，
        ///    第 2 页再解释"那个状态具体做什么"。所以这一页**不带状态效果说明**。
        /// </summary>
        public static GuideSheet DamageTypes()
        {
            // 单列（每条前面要画图标 + 中间是文字，两列会挤）
            var lines = new List<string>();
            lines.AddRange(MagicLines());
            lines.AddRange(PhysicalLines());

            return new GuideSheet("伤害类型", "Damage Types", lines, twoColumn: false,
                                  groups: new List<GuideGroup>
                                  {
                                      MagicGroup(),
                                      PhysicalGroup(),
                                  });
        }

        /// <summary>「魔法伤害」这一组：10 种，沿用现有配色。</summary>
        public static GuideGroup MagicGroup()
        {
            List<DamageType> types = MagicTypesOrdered();
            var entries = new List<GuideEntry>(types.Count);

            for (int i = 0; i < types.Count; i++)
            {
                entries.Add(new GuideEntry(IconKeyOf(types[i]), ColorOf(types[i]),
                                           MagicLine(types[i])));
            }

            return new GuideGroup(MagicCategoryTitle, entries);
        }

        /// <summary>「物理伤害」这一组：3 种，统一中性灰。</summary>
        public static GuideGroup PhysicalGroup()
        {
            var entries = new List<GuideEntry>(AllPhysical.Length);

            for (int i = 0; i < AllPhysical.Length; i++)
            {
                entries.Add(new GuideEntry(IconKeyOf(AllPhysical[i]),
                                           ColorOf(AllPhysical[i]),
                                           PhysicalLine(AllPhysical[i])));
            }

            return new GuideGroup(PhysicalCategoryTitle, entries);
        }

        /// <summary>
        /// **第 2 页「状态一览」**：9 种负面状态各自的效果。
        /// </summary>
        public static GuideSheet Statuses()
        {
            var lines = new List<string>(StatusNames.AllStatuses.Length);
            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
                lines.Add(StatusNames.StatusLine(StatusNames.AllStatuses[i]));

            return new GuideSheet("状态一览", "Status Effects", lines, twoColumn: true);
        }
    }
}
