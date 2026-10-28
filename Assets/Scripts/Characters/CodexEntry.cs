using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters
{
    /// <summary>
    /// **阵营九宫格**（守序/中立/混乱 × 善良/中立/邪恶）。
    ///
    /// 玩家计划用它给角色分组，并支持按任一轴**筛选**。
    /// </summary>
    public enum Alignment
    {
        LawfulGood = 0,
        NeutralGood = 1,
        ChaoticGood = 2,
        LawfulNeutral = 3,
        TrueNeutral = 4,
        ChaoticNeutral = 5,
        LawfulEvil = 6,
        NeutralEvil = 7,
        ChaoticEvil = 8,
    }

    /// <summary>
    /// **图鉴里一名角色的完整资料**。
    ///
    /// ⚠️ 为什么放在 `Characters` 层、而且**只暴露字符串**：
    ///    图鉴要显示"操作指导""如何针对""与其他角色的特殊交互"，
    ///    这些内容目前只存在于代码注释里。UI 层**不能引用 Characters 层**
    ///    （项目分层铁律），所以由这一层把资料**整理成纯字符串**，
    ///    再由 UI 层原样显示 —— 依赖方向保持不变。
    /// </summary>
    public sealed class CodexEntry
    {
        /// <summary>与 <see cref="CharacterDefinition.Id"/> 对应。</summary>
        public string Id { get; }

        /// <summary>英文名（中文名取 <see cref="CharacterDefinition.DisplayName"/>）。</summary>
        public string EnglishName { get; }

        /// <summary>阵营。</summary>
        public Alignment Alignment { get; }

        /// <summary>**一句话**技能描述（卡片正面用）。</summary>
        public string ShortSkill { get; }

        /// <summary>**详细**技能描述。</summary>
        public string DetailSkill { get; }
        public bool UsesNormalizedSkills { get; }

        /// <summary>操作指导（怎么用这个角色）。</summary>
        public string PlayGuide { get; }

        /// <summary>如何针对这个角色。</summary>
        public string Counter { get; }

        /// <summary>与其他角色的特殊交互（每条一行）。</summary>
        public IReadOnlyList<string> Interactions { get; }

        public CodexEntry(string id, string englishName, Alignment alignment,
                          string shortSkill, string detailSkill,
                          string playGuide, string counter,
                          IReadOnlyList<string> interactions)
        {
            Id = id;
            EnglishName = CharacterIdentity.Find(id)?.SubclassNameEn ?? englishName;
            Alignment = alignment;
            ShortSkill = shortSkill;
            UsesNormalizedSkills = CharacterSkillText.TryGet(id, out var normalized);
            DetailSkill = UsesNormalizedSkills ? normalized : detailSkill;
            PlayGuide = playGuide;
            Counter = counter;
            Interactions = interactions ?? new List<string>();
        }
    }

    /// <summary>阵营的显示文本与筛选。</summary>
    public static class Alignments
    {
        /// <summary>守序 / 中立 / 混乱。</summary>
        public static string LawAxisOf(Alignment a)
        {
            switch (a)
            {
                case Alignment.LawfulGood:
                case Alignment.LawfulNeutral:
                case Alignment.LawfulEvil: return "守序";
                case Alignment.ChaoticGood:
                case Alignment.ChaoticNeutral:
                case Alignment.ChaoticEvil: return "混乱";
                default: return "中立";
            }
        }

        /// <summary>善良 / 中立 / 邪恶。</summary>
        public static string GoodAxisOf(Alignment a)
        {
            switch (a)
            {
                case Alignment.LawfulGood:
                case Alignment.NeutralGood:
                case Alignment.ChaoticGood: return "善良";
                case Alignment.LawfulEvil:
                case Alignment.NeutralEvil:
                case Alignment.ChaoticEvil: return "邪恶";
                default: return "中立";
            }
        }

        /// <summary>
        /// 中文阵营名（例如"守序善良"）。
        ///
        /// ⚠️ "中立中立"要说成**"绝对中立"**（玩家要求）——
        ///    两条轴直接拼接会得到一个读起来很怪的名字，
        ///    而九宫格里只有这一格有专用说法（D&amp;D 也叫 True Neutral）。
        /// </summary>
        public static string NameOf(Alignment a)
            => a == Alignment.TrueNeutral ? "绝对中立" : LawAxisOf(a) + GoodAxisOf(a);
    }
}
