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
        /// <summary>「伤害类型」表：10 种类型各自挂什么状态。</summary>
        public static GuideSheet DamageTypes()
        {
            var lines = new List<string>(StatusNames.AllTypes.Length);
            for (int i = 0; i < StatusNames.AllTypes.Length; i++)
                lines.Add(StatusNames.GuideLine(StatusNames.AllTypes[i]));

            return new GuideSheet("伤害类型", "Damage Types", lines, twoColumn: true);
        }

        /// <summary>「状态一览」表：9 种负面状态各自的效果。</summary>
        public static GuideSheet Statuses()
        {
            var lines = new List<string>(StatusNames.AllStatuses.Length);
            for (int i = 0; i < StatusNames.AllStatuses.Length; i++)
                lines.Add(StatusNames.StatusLine(StatusNames.AllStatuses[i]));

            return new GuideSheet("状态一览", "Status Effects", lines, twoColumn: true);
        }

        /// <summary>魔法球入口要显示的全部速查表（顺序即翻页顺序）。</summary>
        public static List<GuideSheet> All()
            => new List<GuideSheet> { DamageAndStatuses() };

        /// <summary>
        /// **合并表**：一行同时给出"伤害类型 + 它挂上的状态 + 状态效果"。
        ///
        /// ⚠️ 玩家反馈："第一排都显示了两次"。
        ///    原因是原来分成两张表：
        ///      伤害类型表第 1 行 = `◎ 雷鸣 → 残响：只能攻击与自己距离 1 的角色`
        ///      状态表    第 1 行 = `◎ 残响：只能攻击与自己距离 1 的角色`
        ///    信息**本来就是同一组**，翻页还会各出现一次，看起来就是重复。
        ///
        ///    合并成一张之后：
        ///      · 9 种有状态的类型各占一行（类型 + 状态 + 效果，一次说完）
        ///      · 力场单独一行（它不挂状态）
        ///      · 不再有第二张表来重复
        /// </summary>
        public static GuideSheet DamageAndStatuses()
        {
            var lines = new List<string>(StatusNames.AllTypes.Length);

            // 先列"有状态"的（顺序与 AllTypes 一致），力场放最后
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

            return new GuideSheet("伤害类型 · 状态", "Damage & Status", lines,
                                  twoColumn: true);
        }
    }
}
