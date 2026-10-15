using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters
{
    /// <summary>
    /// **双将模式**的创建入口。
    ///
    /// 为什么不放在 <see cref="CharacterFactory"/>（它在 Core 层）：
    ///   **分层铁律** —— Core **不得引用** Characters。
    ///   而 <see cref="DualGeneralBehavior"/> 是 Characters 层的类型。
    ///
    /// 玩家确认的规则：
    ///   · **两名必须是不同角色**
    ///   · **必须同一阵营序列**（守序 / 中立 / 混乱）
    ///   · 攻/防加成**相加**；体力上限与攻击距离**取最大**
    /// </summary>
    public static class DualGeneralFactory
    {
        /// <summary>
        /// 创建一名双将玩家。
        ///
        /// <paramref name="primary"/> 是**主将** —— **战报只显示他的名字**（玩家确认）。
        /// </summary>
        public static PlayerState Create(string name,
                                         CharacterDefinition primary,
                                         CharacterDefinition secondary)
        {
            if (primary == null) throw new ArgumentNullException(nameof(primary));
            if (secondary == null) throw new ArgumentNullException(nameof(secondary));

            if (ReferenceEquals(primary, secondary))
                throw new ArgumentException("双将的两名角色必须不同。", nameof(secondary));

            if (!SameAxis(primary, secondary))
                throw new ArgumentException(
                    $"双将的两名角色必须同属守序、中立、混乱三者之一"
                    + $"（{primary.DisplayName} vs {secondary.DisplayName}）。",
                    nameof(secondary));

            ICharacterBehavior a = CharacterFactory.CreateBehavior(primary);
            ICharacterBehavior b = CharacterFactory.CreateBehavior(secondary);

            // ⚠️ 子角色**不经 CharacterFactory 建玩家**，
            //    所以必须显式注入它们的 Definition ——
            //    否则 DisplayName 会回退成类型名（实测：期望"战士"、实际"Warrior"）。
            (a as CharacterTemplate)?.ApplyDefinition(primary);
            (b as CharacterTemplate)?.ApplyDefinition(secondary);

            var dual = new DualGeneralBehavior(a, b);

            // 战报与 UI 只认主将名（玩家确认）
            dual.SetNames(primary.DisplayName, secondary.DisplayName);

            // ⚠️ 体力上限**取最大**（玩家确认）——
            //    不能相加，否则双将血量翻倍。
            int maxHealth = CharacterFactory.MaxHealthOf(primary);
            int other = CharacterFactory.MaxHealthOf(secondary);
            if (other > maxHealth) maxHealth = other;

            var player = new PlayerState(name, dual, maxHealth);
            player.BindDefinition(primary);
            return player;
        }

        /// <summary>
        /// 两名角色是否同属一个**阵营序列**（守序 / 中立 / 混乱）。
        ///
        /// ⚠️ 这个限制不只是为了风格——它**避开了技能冲突**：
        ///    会互相覆盖的 5 个"接管型"接口
        ///    （改写效果 / 攻击段数 / 回复结算 / 取消攻击 / 跳过回合）
        ///    在三组内**都不重叠**。换成善恶轴就会碰撞（已核对）。
        /// </summary>
        public static bool SameAxis(CharacterDefinition a, CharacterDefinition b)
        {
            if (a == null || b == null) return false;

            CodexEntry ea = CharacterCodex.For(a);
            CodexEntry eb = CharacterCodex.For(b);
            if (ea == null || eb == null) return false;

            return Alignments.LawAxisOf(ea.Alignment) == Alignments.LawAxisOf(eb.Alignment);
        }

        /// <summary>
        /// 列出某个角色**可搭配的副将**（同阵营、不包含自己）。
        ///
        /// 供选角界面用：选完主将后，副将候选只能是这一批。
        /// </summary>
        public static List<CharacterDefinition> PartnersFor(CharacterDefinition primary)
        {
            var list = new List<CharacterDefinition>();
            if (primary == null) return list;

            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || ReferenceEquals(all[i], primary)) continue;
                if (SameAxis(primary, all[i])) list.Add(all[i]);
            }
            return list;
        }
    }
}
