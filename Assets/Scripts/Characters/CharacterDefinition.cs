using System;

namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 角色数值定义：把角色的可调数值从 C# 代码里抽出来。
    /// 调平衡只需修改本表（或由外部数据构建），不需要改动角色逻辑代码。
    /// 纯数据、不依赖 Unity，可在纯 .NET 下构造与测试。
    /// </summary>
    public sealed class CharacterDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }

        /// <summary>
        /// 英文名（图鉴用）。
        ///
        /// 为什么单独存一个字段：图鉴要显示"名称（中英文）"，
        /// 而 <see cref="DisplayName"/> 是中文、且被战报与界面广泛引用，
        /// 不能改成"中英合写"（会把所有地方都弄脏）。
        /// </summary>
        public string EnglishName { get; }
        public SpecialLogicType Logic { get; }

        /// <summary>造成伤害修正（白板 = 0）。</summary>
        public int AttackBonus { get; }

        /// <summary>受到伤害修正（正数 = 减伤，负数 = 易伤）。</summary>
        public int DefenseBonus { get; }

        /// <summary>最大生命。非正数时回退为 <see cref="PlayerState.DefaultMaxHealth"/>。</summary>
        public int MaxHealth { get; }

        public string Description { get; }

        public CharacterDefinition(string id, string displayName, SpecialLogicType logic,
                                   int attackBonus = 0, int defenseBonus = 0,
                                   int maxHealth = PlayerState.DefaultMaxHealth,
                                   string description = null,
                                    string englishName = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("角色 Id 不能为空。", nameof(id));

            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
            EnglishName = englishName ?? string.Empty;
            Logic = logic;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            MaxHealth = maxHealth < 1 ? PlayerState.DefaultMaxHealth : maxHealth;
            Description = description ?? string.Empty;
        }

        public override string ToString()
            => $"{Id}({DisplayName}) HP={MaxHealth} ATK={AttackBonus:+#;-#;0} DEF={DefenseBonus:+#;-#;0}";

        // ============================================================
        // 内置角色表
        // ============================================================

        public const string BlankId = "blank";
        public const string FighterId = "fighter";
        public const string CurseweaverId = "curseweaver";
        public const string BarbarianId = "barbarian";
        public const string PriestId = "priest";
        public const string RangerId = "ranger";
        public const string WerewolfId = "werewolf";
        public const string SwordsmanId = "swordsman";
        public const string NinjaId = "ninja";
        public const string AssassinId = "assassin";
        public const string PaladinId = "paladin";
        public const string HexerId = "hexer";
        public const string ArcaneArcherId = "arcane_archer";
        public const string SoldierId = "soldier";
        public const string GamblerId = "gambler";
        public const string KnightId = "knight";
        public const string MonkId = "monk";
        public const string GiantId = "giant";
        public const string ThiefId = "thief";
        public const string HunterId = "hunter";
        public const string MagicianId = "magician";
        public const string WizardId = "wizard";
        public const string SorcererId = "sorcerer";
        public const string WarlockId = "warlock";
        public const string BardId = "bard";

        public static CharacterDefinition Blank { get; } = new CharacterDefinition(
            BlankId, "白板", SpecialLogicType.None,
            description: "无特殊能力，用于教学与数值基准。");

        public static CharacterDefinition Fighter { get; } = new CharacterDefinition(
            FighterId, "战士", SpecialLogicType.Fighter,
            attackBonus: 1,
            description: "连锁破盾：己方盾碎时可击碎场上任意一面成型的盾；新合盾时可修复另一只手的碎盾。攻击 +1。");

        public static CharacterDefinition Curseweaver { get; } = new CharacterDefinition(
            CurseweaverId, "咒术师", SpecialLogicType.Curseweaver,
            attackBonus: -1,
            description: "攻击无视对方护盾与正向防御加成。攻击 -1。");

        public static CharacterDefinition Barbarian { get; } = new CharacterDefinition(
            BarbarianId, "野蛮人", SpecialLogicType.Barbarian,
            defenseBonus: 1,
            description: "新合盾时可锁住对手一只手，使其不能作为运算源，直到该盾破碎。防御 +1。");

        /// <summary>
        /// 牧师：攻击可改为回复；回复可以给任何人（自己或对手）；
        /// 双 9 额外回复。攻防 -1 / -1。
        /// </summary>
        public static CharacterDefinition Priest { get; } = new CharacterDefinition(
            PriestId, "牧师", SpecialLogicType.Priest,
            attackBonus: -1, defenseBonus: -1,
            description: "攻击可改为回复；回复可以给任何人（自己或对手）；双 9 额外回复。");

        public static CharacterDefinition Ranger { get; } = new CharacterDefinition(
            RangerId, "游侠", SpecialLogicType.Ranger,
            defenseBonus: -2,
            description: "被攻击时可用一只「1」变成 2 完全闪避（可放弃）；攻击时可用一只「2」变成 3，" +
                         "让基础伤害 ×1.5（4→6、6→9、8→12）。攻防 0 / -2。");

        public static CharacterDefinition Werewolf { get; } = new CharacterDefinition(
            WerewolfId, "狼人", SpecialLogicType.Werewolf,
            attackBonus: -2, defenseBonus: -2,
            maxHealth: PlayerState.DefaultMaxHealth,   // 实际上限 = 20 + WerewolfLogic.HealthBonus(5) = 25
            description: "生命上限 +5（25）。运算后双手数字之和为 7 时，回复生命上限一半的血量，且攻防永久各 +1。" +
                         "攻防 -2 / -2。");

        public static CharacterDefinition Swordsman { get; } = new CharacterDefinition(
            SwordsmanId, "剑客", SpecialLogicType.Swordsman,
            attackBonus: 2, defenseBonus: -1,
            description: "运算出 4/6/8 时，若另一只手与该手数字相同（双 4/双 6/双 8），" +
                         "则在主攻击前先追加一次固定 4/6/8 伤害的攻击，该攻击无视双方攻防加成。" +
                         "攻防 +2 / -1。");

        public static CharacterDefinition Ninja { get; } = new CharacterDefinition(
            NinjaId, "忍者", SpecialLogicType.Ninja,
            attackBonus: 1, defenseBonus: -1,
            description: "攻击改为两段伤害减半（4→两次 2、6→两次 3、8→两次 4），每段各自结算攻防加成。" +
                         "攻防 +1 / -1。");

        public static CharacterDefinition Assassin { get; } = new CharacterDefinition(
            AssassinId, "刺客", SpecialLogicType.Assassin,
            attackBonus: 2, defenseBonus: -1,
            description: "两只手均为 7 时进入潜行：不能被攻击；每次潜行可以跳过一回合；" +
                         "从潜行发起的攻击伤害 +7。攻防 +2 / -1。");

        /// <summary>
        /// 圣武士：4/6 不再攻击，改为合盾；合盾与碎盾时对范围内一名角色
        /// 造成 2 点真实伤害（强制发动、玩家选目标）；
        /// 范围内有人被攻击时可以把目标改成自己。
        /// 攻防 0 / 0（玩家要求从 0 / +1 下调，压一压它在 2v2 的强度）。
        /// </summary>
        public static CharacterDefinition Paladin { get; } = new CharacterDefinition(
            PaladinId, "圣武士", SpecialLogicType.Paladin,
            attackBonus: 0, defenseBonus: 0,
            description: "4 与 6 不再攻击，改为合成盾牌；合出盾、主动碎盾、被打碎盾时，" +
                         "对攻击范围内一名角色造成 2 点真实伤害（强制发动，目标自选）；" +
                         "攻击范围内的角色被攻击时，可以把目标改成自己。攻防 0 / 0。");

        /// <summary>
        /// 妖术师：有人发起攻击时，若攻击数字与自己某只手相同，
        /// 可把那只手 +1（永久）来取消这次攻击 —— 取消最早结算，能取消咒术师。
        /// 剑客追加攻击与圣武士冲击的"攻击数字视为 0"，无法被取消。
        /// 体力不满时可跳过回合，把场上一人的体力改成与自己一致。
        /// 攻防 0 / -1，攻击距离无限。
        /// </summary>
        public static CharacterDefinition Hexer { get; } = new CharacterDefinition(
            HexerId, "妖术师", SpecialLogicType.Hexer,
            attackBonus: 0, defenseBonus: -1,
            description: "有人发起攻击时，若该攻击数字与自己某只手相同，可把那只手 +1（永久）" +
                         "以取消这次攻击（最早结算，可取消咒术师）；追加攻击与冲击不可被取消。" +
                         "体力不满时可跳过回合，把场上一人的体力改成与自己一致。攻防 0 / -1，攻击距离无限。");

        /// <summary>
        /// 魔射手：4 改为两段各 4；6 伤害 +2；8 无视盾牌（仍可被闪避 / 取消）。
        /// 攻防 0 / -1，攻击距离 3。
        /// </summary>
        public static CharacterDefinition ArcaneArcher { get; } = new CharacterDefinition(
            ArcaneArcherId, "魔射手", SpecialLogicType.ArcaneArcher,
            attackBonus: 0, defenseBonus: -1,
            description: "4 改为两段各 4 点伤害；6 的伤害 +2；8 无视盾牌（但仍可被闪避或取消）。" +
                         "攻防 0 / -1，攻击距离 3。");

        /// <summary>
        /// 士兵：攻 +1 / 防 +1，攻击距离 1，没有特殊能力。
        ///
        /// 玩家要求用它取代白板进入角色选择 ——
        /// 白板没有强度，作为可选角色没有意义。
        /// </summary>
        public static CharacterDefinition Soldier { get; } = new CharacterDefinition(
            SoldierId, "士兵", SpecialLogicType.Soldier,
            attackBonus: 1, defenseBonus: 1,
            description: "没有特殊能力，但攻防各 +1、攻击距离 1 —— 扎实的基础角色。");

        /// <summary>
        /// 赌徒：攻 0 / 防 0，攻击距离 1。
        ///
        /// 可以赌博（1d9 的结果直接当作本次行动结果）；
        /// 上一回合做过正常运算的话，本回合必须赌博。
        /// </summary>
        public static CharacterDefinition Gambler { get; } = new CharacterDefinition(
            GamblerId, "赌徒", SpecialLogicType.Gambler,
            attackBonus: 0, defenseBonus: 0,
            description: "运算时可以选择赌博：点自己一只手后开赌，1d9 的结果直接作为本次行动结果" +
                         "（4/6/8 攻击、9 回血、5 合盾）。上一回合做过正常运算的话，本回合必须赌博。" +
                         "攻防 0 / 0，攻击距离 1。");

        /// <summary>
        /// 骑士：攻 0 / 防 −1，攻击距离 1。
        ///
        /// 自己的盾碎后距离永久 +1，且攻击加成恒等于距离。
        /// </summary>
        public static CharacterDefinition Knight { get; } = new CharacterDefinition(
            KnightId, "骑士", SpecialLogicType.Knight,
            attackBonus: 0, defenseBonus: -1,
            description: "自己的盾牌破碎后，攻击距离永久 +1，且攻击加成恒等于攻击距离" +
                         "（初始都是 1；碎一次后都变 2，以此类推）。攻防 0 / −1。");

        /// <summary>
        /// 武僧：攻 0 / 防 0，攻击距离 1。
        ///
        /// 5 不合盾而进入太极形态；被攻击时把 5 变 0、
        /// 受一半伤害并把剩余反弹给攻击方。
        /// </summary>
        public static CharacterDefinition Monk { get; } = new CharacterDefinition(
            MonkId, "武僧", SpecialLogicType.Monk,
            attackBonus: 0, defenseBonus: 0,
            description: "5 不合成盾牌，而是进入太极形态：被攻击时可以把那张 5 变成 0，" +
                         "只受本次攻击应有伤害的一半，并把剩余的一半反弹给攻击方" +
                         "（反弹无视距离、可被盾挡下、可被游侠闪避，但无法被妖术师取消）。" +
                         "对咒术师的攻击无减半也不反弹。自己的 0 被他人当加数触碰后立即变成 1。" +
                         "攻防 0 / 0，攻击距离 1。");

        /// <summary>
        /// 巨人：攻 +2 / 防 +2，攻击距离 1。
        ///
        /// 4/6/8 不攻击、5 不合盾；打出 5 发起两段攻击（伤害 = 另一只手点数）；
        /// 打出 9 进行两段回复。
        /// </summary>
        public static CharacterDefinition Giant { get; } = new CharacterDefinition(
            GiantId, "巨人", SpecialLogicType.Giant,
            attackBonus: 2, defenseBonus: 2,
            description: "4/6/8 无法攻击，5 也无法合成盾牌。打出 5 时发起两段攻击，" +
                         "每段的基础伤害等于另一只手的点数（之后各自再加攻防加成）；" +
                         "打出 9 时进行两段回复，每段回复量等于另一只手的点数。" +
                         "生命上限 +5（25）。攻防 +2 / +2。");

        /// <summary>
        /// 盗贼：攻 0 / 防 −1，攻击距离 1。
        ///
        /// 可以放弃运算改为偷窃别人的数字。
        /// </summary>
        public static CharacterDefinition Thief { get; } = new CharacterDefinition(
            ThiefId, "盗贼", SpecialLogicType.Thief,
            attackBonus: 0, defenseBonus: -1,
            description: "可以放弃运算，改为偷窃他人的数字：先点自己的一只手，" +
                         "再点偷窃按钮，然后点别人的手完成。偷窃即双方点数互换，" +
                         "原先的点数大于等于偷来的点数才生效，" +
                         "效果由偷来的数字决定（偷来 4 即发起攻击、偷来 5 即合成盾牌、" +
                         "偷来 9 即回复）；否则交换照做但没有效果。" +
                         "被偷的人只被改变点数，不触发效果。" +
                         "上回合发起过偷窃则本回合不能再偷。无限距离，可以偷队友。" +
                         "攻 0 / 防 −1。");

        /// <summary>
        /// 猎人：攻 0 / 防 −1，攻击距离 1。
        ///
        /// 运算后若两只手同时是 1/2/3 且数字相同，
        /// 可以（主动选择）把参与运算的那只手暗置，进入伏击。
        /// </summary>
        public static CharacterDefinition Hunter { get; } = new CharacterDefinition(
            HunterId, "猎人", SpecialLogicType.Hunter,
            attackBonus: 0, defenseBonus: -1,
            description: "运算后若两只手同时是 1/2/3 且数字相同，" +
                         "可以把参与运算的那只手暗置（不能发起运算、" +
                         "也不能被当成运算数字，但数值可见），进入伏击状态。" +
                         "伏击时攻击 +1、攻击距离 3；" +
                         "即将受到伤害时若在伏击，则恢复被暗置的手并抵挡此次伤害" +
                         "（触发时机在武僧太极之后）。" +
                         "咒术师可以穿透伏击打出伤害，咒术攻击也不会解除伏击。" +
                         "攻 0 / 防 −1。");

        /// <summary>
        /// 魔术师：攻 0 / 防 −1，攻击距离 2。
        ///
        /// 可以选择自己的手当被加数；运算出 2 可以攻击。
        /// </summary>
        public static CharacterDefinition Magician { get; } = new CharacterDefinition(
            MagicianId, "魔术师", SpecialLogicType.Magician,
            attackBonus: 0, defenseBonus: -1,
            description: "可以选择自己的手当被加数" +
                         "（例：左手 3、右手 5，拿左手 3 碰自己的 5 凑出 8）；" +
                         "两只手任选一个方向，变的是先点的那只手，一只手不能加自己。" +
                         "运算出 2/4/6/8 都是攻击，伤害 = 牌面数字（吃攻防加成）。" +
                         "攻 0 / 防 −1，攻击距离 2。");

        /// <summary>
        /// 法师（Wizard）：攻 0 / 防 −1，距离 1，中立善良。
        ///
        /// 运算出攻击数字（4/6/8）时可放弃这次攻击，
        /// 给下一次攻击指定一种伤害类型（10 选 1）；
        /// 命中后给目标挂上对应负面状态，持续目标自己的 2 个回合。
        /// </summary>
        public static CharacterDefinition Wizard { get; } = new CharacterDefinition(
            WizardId, "法师", SpecialLogicType.Wizard,
            attackBonus: 0, defenseBonus: -1,
            description: "运算出攻击数字（4/6/8）时，可以选择放弃这次攻击，" +
                         "改为给下一次攻击指定一种伤害类型（10 选 1）：" +
                         "雷鸣（残响）、强酸、毒素、光耀（目盲）、暗蚀（腐朽）、" +
                         "火焰（燃烧）、寒冷（冰冻）、闪电（感电）、" +
                         "力场（同咒术师，不挂状态）、心灵（震慑）。" +
                         "命中后目标获得对应负面状态，持续目标自己的 2 个回合；" +
                         "被盾抵挡 / 太极减免仍算命中，被闪避或被取消则作废（要重新放弃一次）。" +
                         "连续放弃时只保留最新的选择。" +
                         "攻 0 / 防 −1，攻击距离 1。");

        /// <summary>
        /// 术士（Sorcerer）：攻 0 / 防 −1，攻击距离无限，混乱善良。
        ///
        /// 每次攻击各 8% 概率改写成 10 种元素之一（合计 80%），余 20% 普攻；
        /// 改写时按该元素挂状态（力场不挂，走穿盾）。
        /// 普攻：距离 1 以内钝击、1 以外穿刺。
        /// </summary>
        public static CharacterDefinition Sorcerer { get; } = new CharacterDefinition(
            SorcererId, "术士", SpecialLogicType.Sorcerer,
            attackBonus: 0, defenseBonus: -1,
            description: "每次攻击各有 8% 概率改写成 10 种元素之一" +
                         "（雷鸣 / 强酸 / 毒素 / 光耀 / 暗蚀 / 火焰 / 寒冷 / 闪电 / 心灵 / 力场），" +
                         "合计 80%；剩下 20% 为普通攻击。" +
                         "改写后按该元素挂对应的负面状态（力场不挂状态，改为穿盾、无视正向防御、不可闪避）。" +
                         "被盾抵挡 / 太极减免仍算命中，被闪避或被取消仍作废。" +
                         "普通攻击对距离 1 以内为钝击伤害、1 以外为穿刺伤害。" +
                         "攻 0 / 防 −1，攻击距离无限。");



        /// <summary>
        /// 邪术师：攻 0 / 防 -1，攻击距离无限，中立邪恶。
        /// 命中（闪避 / 取消除外）令自己与该目标的距离 +2，持续目标自己的 2 个回合。
        /// </summary>
        public static CharacterDefinition Warlock { get; } = new CharacterDefinition(
            WarlockId, "邪术师", SpecialLogicType.Warlock,
            attackBonus: 0, defenseBonus: -1,
            description: "你命中的攻击（除闪避 / 取消外）令你与目标之间的距离 +2，" +
                         "直到目标下下个回合结束 —— 他射程不足时这两个回合都打不到你。" +
                         "距离只影响你与他之间，不改变其他玩家之间的距离；" +
                         "重复命中只刷新时长、不叠加，且不封顶。" +
                         "攻击对距离 1 以内为挥砍伤害、1 以外为穿刺伤害。" +
                         "攻 0 / 防 -1，攻击距离无限。");

        /// <summary>
        /// 吟游诗人：攻 0 / 防 -1，攻击距离 3，守序中立，伤害类型穿刺。
        /// 每次行动结算完，若本回合受过伤害或实际回复过体力即可发动。
        /// </summary>
        public static CharacterDefinition Bard { get; } = new CharacterDefinition(
            BardId, "吟游诗人", SpecialLogicType.Bard,
            attackBonus: 0, defenseBonus: -1,
            description: "每次行动结算完，若本回合受过伤害或回复过体力" +
                         "（实际回 0 不算，状态伤害也算），可发动技能：" +
                         "先选增幅攻击 / 增幅防御 / 削弱攻击 / 削弱防御之一，" +
                         "再选攻击范围内 1~3 名角色（含自己，同一人点两只手只算一次），" +
                         "给他们对应的攻防加成 ±(4-目标数)：1 人 ±3、2 人 ±2、3 人 ±1。" +
                         "持续到你下一次发动技能（旧的立即移除），或你死亡时立即清除。" +
                         "攻 0 / 防 -1，攻击距离 3，伤害类型穿刺。");

        /// <summary>全部内置角色（顺序即为角色选择界面的建议顺序）。</summary>
        public static CharacterDefinition[] BuiltIn { get; } =
        {
            // ⚠️ 白板已被士兵取代（玩家要求）—— 白板不再出现在选择列表里。
            //    它仍作为兜底定义保留（未登记的逻辑类型会退化成它）。
            Soldier, Fighter, Curseweaver, Barbarian, Priest, Ranger, Werewolf,
            Swordsman, Ninja, Assassin, Paladin, Hexer, ArcaneArcher, Gambler, Knight,
            Monk, Giant, Thief, Hunter, Magician, Wizard, Sorcerer, Warlock, Bard
            // ⚠️ Warlock / Bard 已实现但「还没有 AI 策略」——
            //    在轮询赛里会因 AI 无手可下而停滞（实测 8 例「僵局」）。
            //    补齐 AI 后再接入可选列表。
        };
    }
}
