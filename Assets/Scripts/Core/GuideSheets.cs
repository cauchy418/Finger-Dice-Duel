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

        public GuideSheet(string titleZh, string titleEn,
                          IReadOnlyList<string> lines, bool twoColumn)
        {
            TitleZh = titleZh;
            TitleEn = titleEn;
            Lines = lines;
            TwoColumn = twoColumn;
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
            var lines = new List<string>(StatusNames.AllTypes.Length);

            // 有状态的 9 种按 AllTypes 顺序在前，力场放最后（它不挂状态）
            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                if (StatusNames.StatusOf(t) == StatusEffect.None) continue;
                lines.Add(StatusNames.GuideLine(t));
            }

            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
            {
                DamageType t = StatusNames.AllTypes[i];
                if (StatusNames.StatusOf(t) != StatusEffect.None) continue;
                lines.Add(StatusNames.GuideLine(t));
            }

            return new GuideSheet("伤害类型", "Damage Types", lines, twoColumn: true);
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
