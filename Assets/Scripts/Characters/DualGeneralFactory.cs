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

            ICharacterBehavior dual = CreateBehavior(primary, secondary);

            // ⚠️ 体力上限**取最大**（玩家确认）——
            //    不能相加，否则双将血量翻倍。
            int maxHealth = CharacterFactory.MaxHealthOf(primary);
            int other = CharacterFactory.MaxHealthOf(secondary);
            if (other > maxHealth) maxHealth = other;

            var player = new PlayerState(name, dual, maxHealth);
            player.BindDefinition(primary);
            player.DisplayNameOverride = $"{primary.DisplayName} + {secondary.DisplayName}";
            return player;
        }

        /// <summary>
        /// 只造**行为对象**（不建 <see cref="PlayerState"/>）。
        ///
        /// 用于需要自己组建对局的场合
        /// （双将不能走 <see cref="CharacterFactory.CreateMatch"/>，
        /// 因为它只会用**单角色**建玩家）。
        /// </summary>
        public static ICharacterBehavior CreateBehavior(CharacterDefinition primary,
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
            (a as CharacterTemplate)?.ApplyDefinition(primary);
            (b as CharacterTemplate)?.ApplyDefinition(secondary);

            var dual = new DualGeneralBehavior(a, b);
            dual.SetNames(primary.DisplayName, secondary.DisplayName);
            return dual;
        }

        /// <summary>
        /// **随机搭配一对**同阵营角色（供人机对手用）。
        ///
        /// 玩家确认："人机随机搭配不需要给人机选角色"。
        ///
        /// 做法：先从全部角色里随机一个当**主将**，
        /// 再从**同阵营的副将候选**里随机一个。
        /// 这样天然满足"同阵营、不同角色"，不需要重试。
        ///
        /// 返回 null 表示无法搭配（例如角色表为空）。
        /// </summary>
        public static bool TryRandomPair(out CharacterDefinition primary,
                                         out CharacterDefinition secondary)
        {
            primary = null;
            secondary = null;

            CharacterDefinition[] all = CharacterDefinition.BuiltIn;
            if (all == null || all.Length == 0) return false;

            // 先随机挑一个**有同阵营伙伴**的主将（避开重试）
            var candidates = new List<CharacterDefinition>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (PartnersFor(all[i]).Count > 0) candidates.Add(all[i]);
            }
            if (candidates.Count == 0) return false;

            int rnd = _random.Next(candidates.Count);
            primary = candidates[rnd];

            List<CharacterDefinition> partners = PartnersFor(primary);
            if (partners.Count == 0) return false;

            secondary = partners[_random.Next(partners.Count)];
            return true;
        }

        /// <summary>
        /// 固定种子的随机数（可复现，方便测试与排查）。
        ///
        /// ⚠️ 不用 <c>UnityEngine.Random</c>：本类在 Characters 层，
        ///    **不得引用 UnityEngine**（分层铁律）。
        /// </summary>
        private static readonly System.Random _random = new System.Random();

        /// <summary>两名角色是否同属一个**阵营序列**（守序 / 中立 / 混乱）。
        ///
        /// 同阵营只决定选将资格，不保证技能兼容。多个效果改写器按负责数字分派，真正同时接管同一数字才按主将优先；
        /// 阵营调整后的冲突检查见 Tools/dual-alignment-audit.md。
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
