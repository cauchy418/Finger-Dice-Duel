namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **法师（Wizard）的负面状态**。
    ///
    /// 玩家设计的一套伤害类型体系：法师运算出攻击数字时可以**放弃攻击**，
    /// 为**下一次**攻击指定一种伤害类型；命中后给目标挂上对应状态。
    ///
    /// ## 持续时间（计数式，玩家确认）
    ///
    /// 原文是"直到目标**下下个回合结束**"。对着时间线核对过：
    /// 1v1 里法师在全局第 2 回合命中，目标自己的回合是 3、5，效果到
    /// **全局第 5 回合结束**才移除 —— 也就是**目标完成 2 个自己的回合**。
    ///
    /// 所以实现上**不数"谁的回合"**，而是给每个状态记 <c>TurnsLeft = 2</c>，
    /// 在**目标自己**的回合结束时递减，归零即移除。这样与座位、先手、
    /// 2v2 的轮转顺序都无关。
    ///
    /// ## 数值修正
    ///
    /// | 状态 | 修正 |
    /// |---|---|
    /// | <see cref="Acid"/> 强酸 | 防御加成 **−2** |
    /// | <see cref="Stun"/> 震慑 | 攻击加成 **−2**，且施加者不能成为其合法攻击目标 |
    /// | <see cref="Freeze"/> 冰冻 | 运算结果**减半（向下取整）** |
    /// | <see cref="Blind"/> 目盲 | 只能选**距离 1** 的手当被加数 |
    /// | <see cref="Resonance"/> 残响 | 只能攻击**距离 1** 的角色 |
    /// | <see cref="Decay"/> 腐朽 | 不能回复生命 |
    ///
    /// ## 每回合结算
    ///
    /// | 状态 | 时机 | 伤害 |
    /// |---|---|---|
    /// | <see cref="Poison"/> 中毒 | 目标**回合开始** | 2 点，**不可被抵挡** |
    /// | <see cref="Shock"/> 感电 | 目标**回合开始** | 1 点（自己 + 环上相邻），可被抵挡/闪避/减免 |
    /// | <see cref="Burn"/> 燃烧 | 目标**回合结束** | 3 点，可被盾抵挡 / 游侠闪避 / 太极减免 |
    ///
    /// ⚠️ **这三种"每回合掉血"不会再挂回状态**（玩家明确要求），
    ///    否则会变成无法摆脱的循环。
    ///
    /// <see cref="Force"/> 力场**不附带任何状态** —— 它只是"这一击按咒术师的方式穿透"。
    /// </summary>
    public enum StatusEffect
    {
        None = 0,

        /// <summary>雷鸣：只能攻击与自己**距离 1** 的角色。</summary>
        Resonance = 1,

        /// <summary>强酸：防御加成 **−2**。</summary>
        Acid = 2,

        /// <summary>毒素：回合开始时受 **2** 点毒素伤害，**不可被抵挡**。</summary>
        Poison = 3,

        /// <summary>光耀：计算时只能选择与自己**距离 1** 的手。</summary>
        Blind = 4,

        /// <summary>暗蚀：**不能回复生命值**。</summary>
        Decay = 5,

        /// <summary>火焰：回合结束时受 **3** 点火焰伤害（可被盾 / 闪避 / 太极抵挡）。</summary>
        Burn = 6,

        /// <summary>寒冷：运算后得到的结果**减半（向下取整）**。</summary>
        Freeze = 7,

        /// <summary>闪电：回合开始时自己与相邻玩家各受 **1** 点闪电伤害。</summary>
        Shock = 8,

        /// <summary>心灵：攻击加成 **−2**，且施加者不能成为其合法攻击目标。</summary>
        Stun = 9
    }

    /// <summary>
    /// **法师的伤害类型**（10 种）。
    ///
    /// 与 <see cref="StatusEffect"/> 是**两个概念**：
    ///   · 伤害类型 = "这一击怎么打"（含 <see cref="Force"/>，它不挂状态）；
    ///   · 状态 = "目标身上留下了什么"（9 种）。
    ///
    /// 玩家确认：连打多次时**只有最新的伤害类型保留**（后选的覆盖先选的）。
    /// </summary>
    public enum DamageType
    {
        None = 0,

        /// <summary>雷鸣 -&gt; 残响</summary>
        Thunder = 1,

        /// <summary>强酸 -&gt; 强酸</summary>
        Acid = 2,

        /// <summary>毒素 -&gt; 中毒</summary>
        Poison = 3,

        /// <summary>光耀 -&gt; 目盲</summary>
        Radiant = 4,

        /// <summary>暗蚀 -&gt; 腐朽</summary>
        Necrotic = 5,

        /// <summary>火焰 -&gt; 燃烧</summary>
        Fire = 6,

        /// <summary>寒冷 -&gt; 冰冻</summary>
        Cold = 7,

        /// <summary>闪电 -&gt; 感电</summary>
        Lightning = 8,

        /// <summary>力场：同咒术师，无法被任何方式抵挡/闪避，**不挂状态**。</summary>
        Force = 9,

        /// <summary>心灵 -&gt; 震慑</summary>
        Psychic = 10
    }

    /// <summary>伤害类型 / 状态的中文名与说明（界面与图鉴共用，保证只有一处真相）。</summary>
    public static class StatusNames
    {
        /// <summary>伤害类型的中文名。</summary>
        public static string Of(DamageType type)
        {
            switch (type)
            {
                case DamageType.Thunder: return "雷鸣";
                case DamageType.Acid: return "强酸";
                case DamageType.Poison: return "毒素";
                case DamageType.Radiant: return "光耀";
                case DamageType.Necrotic: return "暗蚀";
                case DamageType.Fire: return "火焰";
                case DamageType.Cold: return "寒冷";
                case DamageType.Lightning: return "闪电";
                case DamageType.Force: return "力场";
                case DamageType.Psychic: return "心灵";
                default: return "无";
            }
        }

        /// <summary>状态的中文名。</summary>
        public static string Of(StatusEffect status)
        {
            switch (status)
            {
                case StatusEffect.Resonance: return "残响";
                case StatusEffect.Acid: return "强酸";
                case StatusEffect.Poison: return "中毒";
                case StatusEffect.Blind: return "目盲";
                case StatusEffect.Decay: return "腐朽";
                case StatusEffect.Burn: return "燃烧";
                case StatusEffect.Freeze: return "冰冻";
                case StatusEffect.Shock: return "感电";
                case StatusEffect.Stun: return "震慑";
                default: return "无";
            }
        }

        /// <summary>状态的**效果一句话**（界面悬浮 / 图鉴用）。</summary>
        public static string Describe(StatusEffect status)
        {
            switch (status)
            {
                case StatusEffect.Resonance:
                    return "只能攻击与自己距离 1 的角色";
                case StatusEffect.Acid:
                    return "防御加成 −2";
                case StatusEffect.Poison:
                    return "回合开始时受 2 点毒素伤害，不可被抵挡";
                case StatusEffect.Blind:
                    return "计算时只能选择与自己距离 1 的手";
                case StatusEffect.Decay:
                    return "不能回复生命值";
                case StatusEffect.Burn:
                    return "回合结束时受 3 点火焰伤害（可被盾 / 闪避 / 太极抵挡）";
                case StatusEffect.Freeze:
                    return "运算后得到的结果减半（向下取整）";
                case StatusEffect.Shock:
                    return "回合开始时自己与相邻玩家各受 1 点闪电伤害";
                case StatusEffect.Stun:
                    return "攻击加成 −2，且施加者不能成为其合法攻击目标";
                default:
                    return "无";
            }
        }

        /// <summary>伤害类型 -&gt; 它挂上的状态（力场没有）。</summary>
        public static StatusEffect StatusOf(DamageType type)
        {
            switch (type)
            {
                case DamageType.Thunder: return StatusEffect.Resonance;
                case DamageType.Acid: return StatusEffect.Acid;
                case DamageType.Poison: return StatusEffect.Poison;
                case DamageType.Radiant: return StatusEffect.Blind;
                case DamageType.Necrotic: return StatusEffect.Decay;
                case DamageType.Fire: return StatusEffect.Burn;
                case DamageType.Cold: return StatusEffect.Freeze;
                case DamageType.Lightning: return StatusEffect.Shock;
                case DamageType.Psychic: return StatusEffect.Stun;
                default: return StatusEffect.None;      // 力场
            }
        }

        /// <summary>
        /// **全部 10 种伤害类型**（固定顺序）。
        ///
        /// ⚠️ 放在 Core 而不是 UI：图鉴的「伤害类型指引」要用它，
        ///    而 UI 是叶子层、**测试项目引用不到** —— 放在这里才能被共用、也才能被测到。
        ///    界面只负责排版。
        /// </summary>
        public static readonly DamageType[] AllTypes =
        {
            DamageType.Thunder, DamageType.Acid, DamageType.Poison, DamageType.Radiant,
            DamageType.Necrotic, DamageType.Fire, DamageType.Cold, DamageType.Lightning,
            DamageType.Force, DamageType.Psychic,
        };

        /// <summary>**全部 9 种状态**（按枚举顺序，便于界面稳定显示）。</summary>
        public static readonly StatusEffect[] AllStatuses =
        {
            StatusEffect.Resonance, StatusEffect.Acid, StatusEffect.Poison,
            StatusEffect.Blind, StatusEffect.Decay, StatusEffect.Burn,
            StatusEffect.Freeze, StatusEffect.Shock, StatusEffect.Stun,
        };

        /// <summary>
        /// 状态的**图标符号**（基础几何字符，CJK 字体里基本都有）。
        ///
        /// ⚠️ 放在 Core 而不是 UI：图鉴的指南页与状态图标栏都要用它，
        ///    两处各写一份必然会漂移（改了一处忘了另一处）。
        ///
        /// ⚠️ 刻意**不用 Emoji** —— 字体回退不一定带彩色 Emoji，
        ///    缺字会显示成方框（本项目在 ∞ 上踩过同类问题）。
        /// </summary>
        public static string GlyphOf(StatusEffect s)
        {
            switch (s)
            {
                case StatusEffect.Resonance: return "\u25ce";   // ◎ 同心 = 残响
                case StatusEffect.Acid: return "\u25bc";        // ▼ 滴落 = 强酸
                case StatusEffect.Poison: return "\u2620";      // ☠ 中毒
                case StatusEffect.Blind: return "\u25c9";       // ◉ 眼睛 = 目盲
                case StatusEffect.Decay: return "\u2716";       // ✖ 断掉的十字 = 不能治疗
                case StatusEffect.Burn: return "\u25b2";        // ▲ 火苗 = 燃烧
                case StatusEffect.Freeze: return "\u2744";      // ❄ 冰冻
                case StatusEffect.Shock: return "\u26a1";       // ⚡ 感电
                case StatusEffect.Stun: return "\u2733";        // ✳ 星芒 = 震慑
                default: return "\u25cb";                       // ○ 占位（无状态）
            }
        }

        /// <summary>
        /// 图鉴「伤害类型指引」里那一行的文字（带图标）：
        /// `◎ 雷鸣 → 残响：只能攻击与自己距离 1 的角色`；
        /// 力场没有状态，写成 `○ 力场：穿盾、无视正向防御、不可闪避（不挂状态）`。
        ///
        /// 放在 Core 是为了让**界面与测试共用同一份文案**（只有一处真相）。
        /// </summary>
        public static string GuideLine(DamageType type)
        {
            StatusEffect st = StatusOf(type);

            if (st == StatusEffect.None)
                return GlyphOf(StatusEffect.None) + " " + Of(type)
                     + "：穿盾、无视正向防御、不可闪避（不挂状态）";

            return GlyphOf(st) + " " + Of(type) + " → " + Of(st) + "：" + Describe(st);
        }

        /// <summary>
        /// 图鉴「状态一览」里那一行的文字（带图标）：
        /// `◎ 残响：只能攻击与自己距离 1 的角色`。
        /// </summary>
        public static string StatusLine(StatusEffect st)
            => GlyphOf(st) + " " + Of(st) + "：" + Describe(st);
    }
}
