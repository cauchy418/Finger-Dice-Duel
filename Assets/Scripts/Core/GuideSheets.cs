using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 一张**速查表**（标题 + 一行行条目）。
    ///
    /// ⚠️ 为什么放在 Core、而不是直接写进 UI 的图鉴卡片：
    ///    UI 是**叶子层，测试项目引用不到** —— 写在那里就没法测。
    ///    而"伤害类型 / 状态一览"这两张表**不只是法师·塑能学派要用**
    ///    （玩家原话："这不仅是法师·塑能学派要用，日后做 pve 冒险也很重要"），
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
        public string Note { get; }

        public GuideSheet(string titleZh, string titleEn,
                          IReadOnlyList<string> lines, bool twoColumn,
                          IReadOnlyList<GuideGroup> groups = null, string note = "")
        {
            TitleZh = titleZh;
            TitleEn = titleEn;
            Lines = lines;
            TwoColumn = twoColumn;
            Groups = groups;
            Note = note;
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
        public StatusEffect Status { get; }
        public string ResourceKey { get; }

        public GuideEntry(string iconKey, int color, string text, StatusEffect status = StatusEffect.None, string resourceKey = null)
        {
            IconKey = iconKey;
            Color = color;
            Text = text;
            Status = status;
            ResourceKey = resourceKey;
        }
    }

    /// <summary>
    /// **伤害类型 / 状态的速查表**（界面与测试共用同一份数据）。
    ///
    /// ⚠️ 这几张表是**纯速查** —— 没有归属标签、没有技能/指南/针对小节。
    ///    原因：它们不只服务法师·塑能学派，日后 PvE 冒险同样要用
    ///    （玩家原话："这不仅是法师·塑能学派要用，日后做 pve 冒险也很重要"）。
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
            => new List<GuideSheet> { DamageTypes(), Statuses(), ElementReactions(), Protections(), CharacterMarks() };

        /// <summary>
        /// **第 1 页「伤害类型」**：10 种类型，各自"附加两回合XX状态"。
        ///
        /// 第 1 页说明伤害附加的状态，第 2 页解释状态，第 3 页单独列出元素反应。
        /// </summary>
        public static GuideSheet Protections()
        {
            var groups = new List<GuideGroup>(); var lines = new List<string>();
            foreach (var affinity in new[] { DamageAffinity.Resistance, DamageAffinity.Vulnerability, DamageAffinity.Immunity })
            {
                var entries = new List<GuideEntry>();
                foreach (var type in ResistanceNames.All)
                {
                    string effect = affinity == DamageAffinity.Resistance ? "伤害减半，向上取整"
                        : affinity == DamageAffinity.Vulnerability ? "伤害×1.5，向上取整"
                        : AttackDamageTypes.IsMagic(type) ? "伤害为0，阻止本击附加状态与元素反应" : "伤害为0，不额外免疫物理技能状态";
                    string text = DamageAffinityNames.Name(affinity, type) + "：" + effect;
                    entries.Add(new GuideEntry(AttackDamageTypes.IconKeyOf(type), AttackDamageTypes.ColorOf(type), text,
                        resourceKey: DamageAffinityNames.Key(affinity, type))); lines.Add(text);
                }
                groups.Add(new GuideGroup(DamageAffinityNames.Name(affinity), entries));
            }
            return new GuideSheet("抗性、易伤与免疫", "Resistance, Vulnerability & Immunity", lines, true, groups,
                "十三种伤害各有抗性、易伤、免疫图标。同类型免疫优先；否则先算易伤×1.5，再算抗性减半，逐步向上取整。一次攻击落地后，全部临时修正一起消耗；盾挡／闪避不消耗，多段整次生效。免疫不清除既有状态。防护学派仍只能赋予原有六项抗性，不新增赋予易伤或免疫的技能。");
        }

        public static GuideSheet CharacterMarks()
        {
            var entries = new List<GuideEntry> {
                new GuideEntry("arcane_barrier", 0x58BFFF, "奥术屏障：法师·防护学派的防护标记。累积攻击数字；在伤害修正后吸收伤害，使用即全部消耗。角标是剩余屏障点。", resourceKey: "Effects/arcane_barrier"),
                new GuideEntry("ki", 0x82D7C9, "气：武僧·散打宗／四象宗的技能资源，上限6点。角标是当前气点，不是剩余回合。", resourceKey: "Effects/ki"),
                new GuideEntry("hunters_mark", 0xCFAC54, "猎人标记：游侠·猎人的专属标记，由对应猎人的攻击消耗。悬停查看来源；多人标记时角标为来源人数，没有回合倒计时。", resourceKey: "Effects/hunters_mark") };
            var lines = new List<string>(); foreach(var entry in entries) lines.Add(entry.Text);
            return new GuideSheet("角色标记", "Character Marks", lines, true,
                new List<GuideGroup> { new GuideGroup("角色专属资源与标记", entries) },
                "屏障、气和猎人标记是角色机制，不计入21种负面状态；各自按对应技能规则获得、消耗和显示。");
        }

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
                                  }, note: "状态在命中后附加；护盾／太极抵挡仍算命中，闪避／取消不算。元素反应见第 3 页。");
        }

        /// <summary>「魔法伤害」这一组：10 种，沿用现有配色。</summary>
        public static GuideGroup MagicGroup()
        {
            List<DamageType> types = MagicTypesOrdered();
            var entries = new List<GuideEntry>(types.Count);

            for (int i = 0; i < types.Count; i++)
            {
                // ⚠️ 用**不带字符符号**的文案 —— 图标改由 PNG 负责
                //    （玩家要求："伤害类型里原来的字符图标可以换掉了"）。
                entries.Add(new GuideEntry(IconKeyOf(types[i]), ColorOf(types[i]),
                                           MagicLinePlain(types[i])));
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
        /// 第 2 页「状态一览」：18 种负面状态各自的效果。
        ///
        /// ⚠️ 玩家要求："状态一览里同步用 png 替换掉字符"——
        ///    所以这一页也走**分组渲染**（含 PNG 图标），不再用字符符号。
        ///    分组模型与第 1 页一致，这样界面只有**一条**渲染路径。
        /// </summary>
        public static GuideSheet Statuses()
        {
            var lines = new List<string>(StatusNames.AllStatuses.Length);
            var entries = new List<GuideEntry>(StatusNames.AllStatuses.Length);

            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
            {
                StatusEffect st = StatusNames.AllStatuses[i];

                // 只用"名称：效果"，不带字符符号（图标由 PNG 负责）
                lines.Add(StatusNames.Of(st) + "：" + StatusNames.Describe(st));

                entries.Add(new GuideEntry(StatusNames.IconKeyOf(st),
                                           StatusNames.StatusColorOf(st),
                                           StatusNames.Of(st) + "：" + StatusNames.Describe(st), st));
            }

            return new GuideSheet("状态一览", "Status Effects", lines, twoColumn: false,
                                  groups: new List<GuideGroup>
                                  {
                                      new GuideGroup("负面状态", entries),
                                  }, note: "共21种负面状态，通常持续目标自己的2个回合；重复施加刷新时间、不叠加强度。覆霜到期附2回合潮湿。油腻运算后触发的倒伏保留两个后续自身回合，跳过或赌博不作油腻判定。");
        }

        /// <summary>
        /// 魔法伤害的一行（**不带字符符号**，图标交给 PNG）。
        ///
        /// 玩家要求："伤害类型里原来的字符图标可以换掉了"。
        /// </summary>
        public static string MagicLinePlain(DamageType t)
        {
            switch (t)
            {
                case DamageType.Psychic:
                    return "心灵：附加两回合状态：恐慌／震慑各 50%";
                default:
                    StatusEffect st = StatusNames.StatusOf(t);
                    return st == StatusEffect.None
                        ? StatusNames.Of(t) + "：穿盾、无视正向防御、不可闪避（不挂状态）"
                        : StatusNames.Of(t) + "：附加两回合" + StatusNames.Of(st) + "状态";
            }
        }

        /// <summary>伤害类型与状态的联动速查行。</summary>
        public static List<string> InteractionLines()
        {
            return new List<string>
            {
                "雷鸣＋残响：清除残响，附加 2 回合眩晕",
                "寒冷＋覆霜／潮湿：清除这两种状态，附加 2 回合冻结",
                "寒冷＋冻结：伤害 ×1.5，保留冻结，不附加覆霜",
                "寒冷＋燃烧：清除燃烧，伤害 ×1.5，不附加覆霜",
                "火焰＋覆霜／潮湿：清除这两种状态，伤害 ×1.5，不附加燃烧",
                "闪电＋潮湿：伤害 ×1.5，保留潮湿，照常附加感电",
                "强酸＋中毒：附加强酸，中毒刷新至2回合",
                "毒素＋强酸：附加中毒，强酸刷新至2回合",
                "火焰＋油腻：清除油腻，伤害×1.5，照常附加燃烧",
            };
        }

        public static GuideGroup InteractionGroup()
        {
            var lines = InteractionLines();
            var types = new[] { DamageType.Thunder, DamageType.Cold, DamageType.Cold,
                DamageType.Cold, DamageType.Fire, DamageType.Lightning, DamageType.Acid, DamageType.Poison, DamageType.Fire };
            var entries = new List<GuideEntry>();
            for (int i = 0; i < lines.Count; i++)
                entries.Add(new GuideEntry(IconKeyOf(types[i]), ColorOf(types[i]), lines[i]));
            return new GuideGroup("元素反应", entries);
        }

        public static GuideSheet ElementReactions()
            => new GuideSheet("元素反应", "Elemental Reactions", InteractionLines(), twoColumn: false,
                groups: new List<GuideGroup> { InteractionGroup() },
                note: "只在命中后触发；护盾／太极抵挡仍算命中，闪避／取消不算。增伤按常规伤害 ×1.5 向上取整。酸毒反应不增伤，刷新已有状态；免疫该魔法伤害时不附加、不刷新、不清除状态。");
    }
}
