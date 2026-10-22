using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **一次攻击的伤害类型**（玩家要求："现在把所有攻击都标记上攻击类型"）。
    ///
    /// 分成两大类：
    ///   · **物理**：挥砍 / 钝击 / 穿刺（图标为灰色）
    ///   · **魔法**：法师·塑能学派那 10 种（图标沿用各自配色；力场为深红）
    ///
    /// ⚠️ 为什么用一个**统一枚举**而不是"物理一套 + 魔法一套"：
    ///    一次攻击只会有一个类型，而飘字颜色、图标、以及太极反弹的"继承"
    ///    都只需要"一个类型"即可。两套枚举会让这三处都要写两次分支。
    /// </summary>
    public enum AttackDamageType
    {
        /// <summary>未指定（不应出现在界面上；用于兼容旧路径）。</summary>
        None = 0,

        // ---------- 物理 ----------
        /// <summary>挥砍：刀剑类。</summary>
        Slashing = 1,

        /// <summary>钝击：钝器。</summary>
        Bludgeoning = 2,

        /// <summary>穿刺：远程武器或刺剑类。</summary>
        Piercing = 3,

        // ---------- 魔法（数值与法师·塑能学派的 DamageType 对齐，便于互转）----------
        Thunder = 11,
        Acid = 12,
        Poison = 13,
        Radiant = 14,
        Necrotic = 15,
        Fire = 16,
        Cold = 17,
        Lightning = 18,
        Force = 19,
        Psychic = 20,
    }

    /// <summary>
    /// **攻击类型的属性**：名称、颜色、图标键。
    ///
    /// ⚠️ 颜色是**唯一一份**：速查表、攻击类型图标、伤害飘字
    ///    （包括"被盾牌抵挡"那一条）全都从这里取，
    ///    这样"飘字颜色 = 图标颜色"是**结构上保证**的，不会漂移。
    /// </summary>
    public static class AttackDamageTypes
    {
        /// <summary>物理三类的统一颜色（中性灰）。</summary>
        public const int PhysicalColor = 0x9AA0AC;

        /// <summary>是否属于魔法那一类。</summary>
        public static bool IsMagic(AttackDamageType t) => (int)t >= 11;

        /// <summary>中文名。</summary>
        public static string NameOf(AttackDamageType t)
        {
            switch (t)
            {
                case AttackDamageType.Slashing: return "挥砍";
                case AttackDamageType.Bludgeoning: return "钝击";
                case AttackDamageType.Piercing: return "穿刺";
                case AttackDamageType.Thunder: return "雷鸣";
                case AttackDamageType.Acid: return "强酸";
                case AttackDamageType.Poison: return "毒素";
                case AttackDamageType.Radiant: return "光耀";
                case AttackDamageType.Necrotic: return "暗蚀";
                case AttackDamageType.Fire: return "火焰";
                case AttackDamageType.Cold: return "寒冷";
                case AttackDamageType.Lightning: return "闪电";
                case AttackDamageType.Force: return "力场";
                case AttackDamageType.Psychic: return "心灵";
                default: return "无";
            }
        }

        /// <summary>
        /// **颜色**（0xRRGGBB）—— 与图标颜色一致。
        ///
        /// 物理三类统一灰；魔法沿用各自配色（力场深红）。
        /// </summary>
        public static int ColorOf(AttackDamageType t)
        {
            if (!IsMagic(t)) return PhysicalColor;

            // 魔法类型复用法师·塑能学派体系的颜色表（唯一一份）
            return StatusNames.GuideColorOf(ToDamageType(t));
        }

        /// <summary>图标键（交给 <see cref="DamageIcons.Render"/> 画）。</summary>
        public static string IconKeyOf(AttackDamageType t)
        {
            switch (t)
            {
                case AttackDamageType.Slashing: return "slashing";
                case AttackDamageType.Bludgeoning: return "bludgeoning";
                case AttackDamageType.Piercing: return "piercing";
                default:
                    return DamageIcons.KeyOf(ToDamageType(t));
            }
        }

        /// <summary>魔法类型 -> 法师·塑能学派的 <see cref="DamageType"/>（物理返回 None）。</summary>
        public static DamageType ToDamageType(AttackDamageType t)
        {
            switch (t)
            {
                case AttackDamageType.Thunder: return DamageType.Thunder;
                case AttackDamageType.Acid: return DamageType.Acid;
                case AttackDamageType.Poison: return DamageType.Poison;
                case AttackDamageType.Radiant: return DamageType.Radiant;
                case AttackDamageType.Necrotic: return DamageType.Necrotic;
                case AttackDamageType.Fire: return DamageType.Fire;
                case AttackDamageType.Cold: return DamageType.Cold;
                case AttackDamageType.Lightning: return DamageType.Lightning;
                case AttackDamageType.Force: return DamageType.Force;
                case AttackDamageType.Psychic: return DamageType.Psychic;
                default: return DamageType.None;
            }
        }

        /// <summary>法师·塑能学派的 <see cref="DamageType"/> -> 攻击类型。</summary>
        public static AttackDamageType FromDamageType(DamageType t)
        {
            switch (t)
            {
                case DamageType.Thunder: return AttackDamageType.Thunder;
                case DamageType.Acid: return AttackDamageType.Acid;
                case DamageType.Poison: return AttackDamageType.Poison;
                case DamageType.Radiant: return AttackDamageType.Radiant;
                case DamageType.Necrotic: return AttackDamageType.Necrotic;
                case DamageType.Fire: return AttackDamageType.Fire;
                case DamageType.Cold: return AttackDamageType.Cold;
                case DamageType.Lightning: return AttackDamageType.Lightning;
                case DamageType.Force: return AttackDamageType.Force;
                case DamageType.Psychic: return AttackDamageType.Psychic;
                default: return AttackDamageType.None;
            }
        }

        /// <summary>物理伤害类型 -> 攻击类型。</summary>
        public static AttackDamageType FromPhysical(PhysicalDamageType t)
        {
            switch (t)
            {
                case PhysicalDamageType.Slashing: return AttackDamageType.Slashing;
                case PhysicalDamageType.Bludgeoning: return AttackDamageType.Bludgeoning;
                case PhysicalDamageType.Piercing: return AttackDamageType.Piercing;
                default: return AttackDamageType.None;
            }
        }

        /// <summary>全部魔法类型（顺序与法师·塑能学派的 AllTypes 一致）。</summary>
        public static readonly AttackDamageType[] AllMagic =
        {
            AttackDamageType.Thunder, AttackDamageType.Acid, AttackDamageType.Poison,
            AttackDamageType.Radiant, AttackDamageType.Necrotic, AttackDamageType.Fire,
            AttackDamageType.Cold, AttackDamageType.Lightning, AttackDamageType.Force,
            AttackDamageType.Psychic,
        };
    }

    /// <summary>
    /// **角色声明自己的攻击类型**（玩家要求：所有攻击都要有类型）。
    ///
    /// ⚠️ 放在 Core 接口、由 Characters 实现：
    ///    Core 层不能引用 Characters（分层铁律），
    ///    但 `AttackContext.Resolve` 需要问"这次攻击是什么类型"。
    /// </summary>
    public interface IAttackTypeProvider
    {
        /// <summary>
        /// 本次攻击的伤害类型。
        /// </summary>
        /// <param name="context">本次攻击的上下文（含攻方、受方、结果等）。</param>
        AttackDamageType AttackTypeOf(AttackContext context);
    }
}
