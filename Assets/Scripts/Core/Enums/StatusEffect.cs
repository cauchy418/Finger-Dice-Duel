namespace FingerDiceDuel.Core
{
    /// <summary>
    /// **法师·塑能学派（SchoolOfEvocation）的负面状态**。
    ///
    /// 玩家设计的一套伤害类型体系：法师·塑能学派运算出攻击数字时可以**放弃攻击**，
    /// 为**下一次**攻击指定一种伤害类型；命中后给目标挂上对应状态。
    ///
    /// ## 持续时间（计数式，玩家确认）
    ///
    /// 原文是"直到目标**下下个回合结束**"。对着时间线核对过：
    /// 1v1 里法师·塑能学派在全局第 2 回合命中，目标自己的回合是 3、5，效果到
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
    /// | <see cref="Burn"/> 燃烧 | 目标**回合结束** | 3 点，可被盾抵挡 / 游荡者·风流剑客闪避 / 太极减免 |
    ///
    /// ⚠️ **这三种"每回合掉血"不会再挂回状态**（玩家明确要求），
    ///    否则会变成无法摆脱的循环。
    ///
    /// <see cref="Force"/> 力场**不附带任何状态** —— 它只是"这一击按法师·咒法学派的方式穿透"。
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

        /// <summary>寒冷（覆霜）：运算结果**减半（至少 1）**；到期后附加潮湿。</summary>
        Frost = 7,

        /// <summary>兼容旧名：旧版“冰冻”现在叫覆霜。</summary>
        Freeze = Frost,

        /// <summary>闪电：回合开始时自己与相邻玩家各受 **1** 点闪电伤害。</summary>
        Shock = 8,

        /// <summary>心灵（恐慌）：攻击加成 −2，且施加者不能成为其合法攻击目标。</summary>
        Panic = 9,

        /// <summary>兼容旧名：旧版“震慑”现在叫恐慌。</summary>
        Stun = Panic,

        /// <summary>流血：攻防 −1；自己回合开始时失去 1 点体力，不可抵挡。</summary>
        Bleed = 10,

        /// <summary>潮湿：运算结果 −1（至少 1）。</summary>
        Wet = 11,

        /// <summary>冻结：不能行动。</summary>
        Frozen = 12,

        /// <summary>震慑（新）：不能攻击。</summary>
        Stunned = 13,

        /// <summary>跛脚：攻击距离减半。</summary>
        Limp = 14,

        /// <summary>倒伏：受到伤害 ×1.5。</summary>
        KnockedDown = 15,

        /// <summary>魅惑：行动随机化（由 AI 随机合法行动）。</summary>
        Charm = 16,

        /// <summary>失能：攻击伤害改为 0，但保留攻击效果。</summary>
        Disabled = 17,

        /// <summary>眩晕：本次运算结果无效，不触发技能。</summary>
        Dizzy = 18,

        /// <summary>
        /// **圣光（牧师·光明领域，2026-10-23）**：目标回合开始受 1 点伤害（不可抵挡）、
        /// 回复量减半（向上取整）；施加时立即清除目标其他全部状态与全部抗性，
        /// 存在期间阻止新的状态与抗性附加（重复施加圣光 = 刷新时长）。
        /// 剑歌与战恩会被清除；勇气战歌增益与奥术屏障是独立系统，不受圣光影响（玩家口径）。
        /// </summary>
        HolyLight = 19,
        Restrained = 20,
        Greased = 21,
        WarBlessing = 22,
        Tinnitus = 23,
    }

    /// <summary>
    /// **法师·塑能学派的伤害类型**（10 种）。
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

        /// <summary>力场：同法师·咒法学派，无法被任何方式抵挡/闪避，**不挂状态**。</summary>
        Force = 9,

        /// <summary>心灵 -&gt; 震慑</summary>
        Psychic = 10
    }

    /// <summary>伤害类型 / 状态的中文名与说明（界面与图鉴共用，保证只有一处真相）。</summary>
    public static class StatusNames
    {
        /// <summary>绘画图标的唯一资源键；显式处理旧枚举别名，避免 ToString 结果不确定。</summary>
        public static string IconKeyOf(StatusEffect status)
        {
            switch (status)
            {
                case StatusEffect.Resonance: return "resonance";
                case StatusEffect.Acid: return "acid";
                case StatusEffect.Poison: return "poison";
                case StatusEffect.Blind: return "blind";
                case StatusEffect.Decay: return "decay";
                case StatusEffect.Burn: return "burn";
                case StatusEffect.Frost: return "frost";
                case StatusEffect.Shock: return "shock";
                case StatusEffect.Panic: return "panic";
                case StatusEffect.Bleed: return "bleed";
                case StatusEffect.Wet: return "wet";
                case StatusEffect.Frozen: return "frozen";
                case StatusEffect.Stunned: return "stunned";
                case StatusEffect.Limp: return "limp";
                case StatusEffect.KnockedDown: return "knocked_down";
                case StatusEffect.Charm: return "charm";
                case StatusEffect.Disabled: return "disabled";
                case StatusEffect.Dizzy: return "dizzy";
                case StatusEffect.HolyLight: return "holy_light";
                case StatusEffect.Restrained: return "restrained";
                case StatusEffect.Greased: return "greased";
                case StatusEffect.WarBlessing: return "war_blessing";
                case StatusEffect.Tinnitus: return "tinnitus";
                default: return null;
            }
        }

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
                case StatusEffect.Frost: return "覆霜";
                case StatusEffect.Shock: return "感电";
                case StatusEffect.Panic: return "恐慌";
                case StatusEffect.Bleed: return "流血";
                case StatusEffect.Wet: return "潮湿";
                case StatusEffect.Frozen: return "冻结";
                case StatusEffect.Stunned: return "震慑";
                case StatusEffect.Limp: return "跛脚";
                case StatusEffect.KnockedDown: return "倒伏";
                case StatusEffect.Charm: return "魅惑";
                case StatusEffect.Disabled: return "失能";
                case StatusEffect.Dizzy: return "眩晕";
                case StatusEffect.HolyLight: return "圣光";
                case StatusEffect.Restrained: return "束缚";
                case StatusEffect.Greased: return "油腻";
                case StatusEffect.WarBlessing: return "战恩";
                case StatusEffect.Tinnitus: return "耳鸣";
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
                case StatusEffect.Frost:
                    return "运算结果减半（至少为 1）；结束后附加 2 回合潮湿";
                case StatusEffect.Shock:
                    return "回合开始时自己与相邻玩家各受 1 点闪电伤害";
                case StatusEffect.Panic:
                    return "攻击加成 −2，且施加者不能成为其合法攻击目标";
                case StatusEffect.Bleed:
                    return "攻防 −1；自己回合开始时失去 1 点体力，不可抵挡";
                case StatusEffect.Wet:
                    return "运算结果 −1（至少为 1）";
                case StatusEffect.Frozen:
                    return "不能行动";
                case StatusEffect.Stunned:
                    return "不能攻击";
                case StatusEffect.Limp:
                    return "攻击距离减半（至少为 1）";
                case StatusEffect.KnockedDown:
                    return "受到伤害 ×1.5";
                case StatusEffect.Charm:
                    return "行动随机化（由 AI 随机选择合法行动）";
                case StatusEffect.Disabled:
                    return "攻击伤害改为 0，但保留攻击效果";
                case StatusEffect.Dizzy:
                    return "本次运算结果无效，不触发任何技能";
                case StatusEffect.HolyLight:
                    return "回合开始失去1生命（不可抵挡），回复减半；清除并阻止其他状态及临时抗性／易伤／免疫";
                case StatusEffect.Tinnitus:
                    return "受到攻击与持续状态伤害×1.5，不能合盾或回复；免疫雷鸣伤害，持续2个自身回合";
                case StatusEffect.Restrained:
                    return "每个自身回合随机一只手不能主动运算，仍可被借作加数；不解开原有锁手";
                case StatusEffect.WarBlessing:
                    return "攻防+1；算出9可选择放弃回复，改为9点基础伤害攻击；重复施加刷新2回合，不叠加";
                case StatusEffect.Greased:
                    return "每次运算完成后50%概率倒伏2回合；火焰命中清除油腻、伤害×1.5，照常附燃烧";
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
                case DamageType.Cold: return StatusEffect.Frost;
                case DamageType.Lightning: return StatusEffect.Shock;
                case DamageType.Psychic: return StatusEffect.Panic;
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

        /// <summary>与手册的负面效果分类一致；角色状态不参与格挡／抗性减时长。</summary>
        public static bool IsNegativeStatus(StatusEffect status)
            => status != StatusEffect.None && status != StatusEffect.HolyLight && status != StatusEffect.WarBlessing;

        /// <summary>全部22种计时状态（排除兼容别名，顺序固定）。</summary>
        public static readonly StatusEffect[] AllStatuses =
        {
            StatusEffect.Resonance, StatusEffect.Acid, StatusEffect.Poison,
            StatusEffect.Blind, StatusEffect.Decay, StatusEffect.Burn,
            StatusEffect.Frost, StatusEffect.Shock, StatusEffect.Panic,
            StatusEffect.Bleed, StatusEffect.Wet, StatusEffect.Frozen,
            StatusEffect.Stunned, StatusEffect.Limp, StatusEffect.KnockedDown,
            StatusEffect.Charm, StatusEffect.Disabled, StatusEffect.Dizzy,
            StatusEffect.HolyLight,
            StatusEffect.Restrained, StatusEffect.Greased, StatusEffect.WarBlessing, StatusEffect.Tinnitus,
        };

        /// <summary>
        /// 状态对应的**主色**（0xRRGGBB）。
        ///
        /// ⚠️ 放在 Core、且是**唯一一份**：状态图标栏与图鉴指引都用它，
        ///    两处各写一份必然会漂移（改了一处忘了另一处）。
        /// </summary>
        /// <summary>
        /// **力场**的颜色（玩家要求："力场采用更深的红色"）。
        ///
        /// ⚠️ 力场**没有状态**，但界面上仍要有一个颜色 ——
        ///    所以单独给一个入口，不要让它落到中性灰上。
        /// </summary>
        public const int ForceColor = 0x8B1A1A;      // 深红

        /// <summary>
        /// **界面用**的颜色：力场走 <see cref="ForceColor"/>，其余走状态色。
        /// </summary>
        public static int GuideColorOf(DamageType type)
        {
            StatusEffect st = StatusOf(type);
            return st == StatusEffect.None ? ForceColor : StatusColorOf(st);
        }

        /// <summary>
        /// 状态对应的**主色**（0xRRGGBB）。
        ///
        /// ⚠️ 放在 Core、且是**唯一一份**：状态图标栏与图鉴指引都用它，
        ///    两处各写一份必然会漂移（改了一处忘了另一处）。
        ///
        /// 玩家本轮指定的 5 个颜色：
        ///   · 雷鸣 / 残响 -> **紫**
        ///   · 强酸        -> **黄绿**
        ///   · 毒素        -> **绿**
        ///   · 暗蚀 / 腐朽 -> **深绿**
        ///   · 闪电 / 感电 -> **深蓝**
        /// </summary>
        public static int StatusColorOf(StatusEffect s)
        {
            switch (s)
            {
                case StatusEffect.Resonance: return 0x9B4FD0;   // 紫（雷鸣 / 残响）
                case StatusEffect.Acid: return 0xB6CC2E;        // 黄绿
                case StatusEffect.Poison: return 0x3FA34D;      // 绿
                case StatusEffect.Blind: return 0xF2C72E;       // 亮金
                case StatusEffect.Decay: return 0x1E5B2E;       // 深绿（暗蚀 / 腐朽）
                case StatusEffect.Burn: return 0xE8701A;        // 橙红（火焰）
                case StatusEffect.Frost: return 0x4C9EE6;       // 冰蓝
                case StatusEffect.Shock: return 0x1A4FA0;       // 深蓝（闪电 / 感电）
                case StatusEffect.Panic: return 0xF08CBB;       // 淡粉（心灵 / 恐慌）
                case StatusEffect.Bleed: return 0xA83232;       // 暗红
                case StatusEffect.Wet: return 0x2E7FA8;         // 水蓝
                case StatusEffect.Frozen: return 0x7FD8FF;      // 亮冰
                case StatusEffect.Stunned: return 0xD8A6FF;     // 淡紫
                case StatusEffect.Limp: return 0x8C8C8C;        // 灰
                case StatusEffect.KnockedDown: return 0xB8743A;  // 土褐
                case StatusEffect.Charm: return 0xE05AA8;       // 魅紫
                case StatusEffect.Disabled: return 0x6E7A8A;    // 失能灰蓝
                case StatusEffect.Dizzy: return 0xE8A838;       // 眩晕橙黄
                case StatusEffect.WarBlessing: return 0xDBB662;
                case StatusEffect.Tinnitus: return 0xB99DCD;
                case StatusEffect.HolyLight: return 0xFFE194;  // 圣光暖金（浅于目盲的亮金）
                case StatusEffect.Restrained: return 0x638653;
                case StatusEffect.Greased: return 0xB8993C;
                default: return 0x9AA0AC;                       // 中性灰

                // ⚠️ 力场的颜色单独给（它没有状态，走 default 会拿到中性灰）
                //    玩家要求："力场采用更深的红色"。
            }
        }

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
                case StatusEffect.Resonance: return "\u25ce";   // ◎ 同心圆（声波）
                case StatusEffect.Acid: return "\u25bc";        // ▼ 倒三角（滴落）
                case StatusEffect.Poison: return "\u2620";      // ☠ 骷髅
                case StatusEffect.Blind: return "\u25c9";       // ◉ 鱼眼（眼睛）
                case StatusEffect.Decay: return "\u2716";       // ✖ 重叉（斩断治疗）
                case StatusEffect.Burn: return "\u25c6";        // ◆ 实心菱形（热源）
                case StatusEffect.Frost: return "❄";       // ❄ 雪花
                case StatusEffect.Shock: return "⚠";        // ⚠ 警告（电压危险）
                case StatusEffect.Panic: return "✽";        // ✽ 花瓣（恐慌）
                case StatusEffect.Bleed: return "♥";        // ♥ 流血
                case StatusEffect.Wet: return "≈";          // ≈ 水波
                case StatusEffect.Frozen: return "❅";       // ❅ 冻结
                case StatusEffect.Stunned: return "✶";      // ✶ 震慑
                case StatusEffect.Limp: return "↯";         // ↯ 腿伤
                case StatusEffect.KnockedDown: return "↩";  // ↩ 倒地
                case StatusEffect.Charm: return "♡";        // ♡ 魅惑
                case StatusEffect.Disabled: return "⊘";    // ⊘ 失能
                case StatusEffect.Dizzy: return "◌";         // ◌ 眩晕
                case StatusEffect.WarBlessing: return "⚔";
                case StatusEffect.Tinnitus: return "≋";
                case StatusEffect.HolyLight: return "✚";    // ✚ 圣光（重十字）
                case StatusEffect.Restrained: return "♧";
                case StatusEffect.Greased: return "◒";

                // △ 力场（玩家要求："把三角形标记移给力场"）
                case StatusEffect.None: return "\u25b3";

                default: return "\u25cb";                       // ○ 兜底
            }
        }

        /// <summary>
        /// 图鉴「伤害类型指引」里那一行的文字（带图标）：
        /// `◎ 雷鸣：附加两回合残响状态`；
        /// 力场没有状态，写成 `○ 力场：穿盾、无视正向防御、不可闪避（不挂状态）`。
        ///
        /// 放在 Core 是为了让**界面与测试共用同一份文案**（只有一处真相）。
        /// </summary>
        public static string GuideLine(DamageType type)
        {
            StatusEffect st = StatusOf(type);
            return Colorize(GlyphOf(st), GuideColorOf(type)) + " " + GuideSheets.MagicLinePlain(type);
        }

        /// <summary>
        /// 图鉴「状态一览」里那一行的文字（带图标）：
        /// `◎ 残响：只能攻击与自己距离 1 的角色`。
        /// </summary>
        public static string StatusLine(StatusEffect st)
            => Colorize(GlyphOf(st), st) + " " + Of(st) + "：" + Describe(st);

        /// <summary>
        /// **给符号上色**（颜色与状态图标栏一致）。
        ///
        /// 用 uGUI 的富文本标签 —— `Text.supportRichText` 默认就是 true。
        /// 颜色取自 <see cref="StatusColorOf"/>，与图标栏**共用一份**，
        /// 所以图鉴里的符号与战场上的图标必然是同一个颜色。
        ///
        /// ⚠️ 没有状态（力场）时**不着色** —— 否则富文本标签会白白占宽度，
        ///    那一行的对齐也会跟别人不一样。
        /// </summary>
        private static string Colorize(string glyph, StatusEffect st)
        {
            if (st == StatusEffect.None) return glyph;

            return Colorize(glyph, StatusColorOf(st));
        }

        /// <summary>按给定颜色上色（力场用，它没有状态）。</summary>
        private static string Colorize(string glyph, int rgb)
            => "<color=#" + rgb.ToString("X6") + ">" + glyph + "</color>";
    }
}
