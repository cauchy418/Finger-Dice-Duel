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
        public CharacterIdentity Identity { get; }
        public CharacterProfession Profession => Identity?.Profession;
        public string SubclassNameZh => Identity?.SubclassNameZh ?? DisplayName;
        public string SubclassNameEn => Identity?.SubclassNameEn ?? EnglishName;

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

            Identity = CharacterIdentity.Find(id);
            Id = Identity?.Id ?? id;
            DisplayName = Identity?.FullNameZh ?? (string.IsNullOrEmpty(displayName) ? id : displayName);
            EnglishName = Identity?.SubclassNameEn ?? englishName ?? string.Empty;
            Logic = logic;
            AttackBonus = attackBonus;
            DefenseBonus = defenseBonus;
            MaxHealth = maxHealth < 1 ? PlayerState.DefaultMaxHealth : maxHealth;
            Description = description ?? string.Empty;
        }

        /// <summary>兼容旧ID的目录查询，不把别名重新加入可选名单。</summary>
        public static CharacterDefinition Find(string id)
        {
            id = CharacterIdentity.NormalizeId(id);
            foreach (var role in BuiltIn) if (role.Id == id) return role;
            return id == BlankId ? Blank : null;
        }

        public override string ToString()
            => $"{Id}({DisplayName}) HP={MaxHealth} ATK={AttackBonus:+#;-#;0} DEF={DefenseBonus:+#;-#;0}";

        // ============================================================
        // 内置角色表
        // ============================================================

        public const string BlankId = "blank";
        public const string BreakerId = "fighter_breaker";
        public const string SchoolOfConjurationId = "wizard_school_of_conjuration";
        public const string PathOfTheWardenId = "barbarian_path_of_the_warden";
        public const string LifeDomainId = "cleric_life_domain";

        /// <summary>兼容旧存档 / 旧资源引用。</summary>
        public const string PriestId = LifeDomainId;
        public const string SwashbucklerId = "rogue_swashbuckler";
        public const string PathOfTheBeastId = "barbarian_path_of_the_beast";
        public const string SwordsmanId = "fighter_swordsman";
        public const string WayOfTheNinjaId = "monk_way_of_the_ninja";
        public const string AssassinId = "rogue_assassin";
        public const string OathOfDevotionId = "paladin_oath_of_devotion";
        public const string TheArchfeyId = "warlock_the_archfey";
        public const string ArcaneArcherId = "fighter_arcane_archer";
        public const string ChampionId = "fighter_champion";
        public const string GamblerId = "rogue_gambler";
        public const string OathOfTheCrownId = "paladin_oath_of_the_crown";
        public const string WayOfTheTaiChiId = "monk_way_of_the_tai_chi";
        public const string PathOfTheGiantId = "barbarian_path_of_the_giant";
        public const string ThiefId = "rogue_thief";
        public const string GloomStalkerId = "ranger_gloom_stalker";
        public const string MagicianId = "rogue_magician";
        public const string SchoolOfEvocationId = "wizard_school_of_evocation";
        public const string WildMagicId = "sorcerer_wild_magic";
        public const string TheGreatOldOneId = "warlock_the_great_old_one";
        public const string CollegeOfValorId = "bard_college_of_valor";
        public const string OathbreakerId = "paladin_oathbreaker";
        public const string PathOfTheBerserkerId = "barbarian_path_of_the_berserker";
        public const string TacticianId = "fighter_tactician";
        public const string SchoolOfDivinationId = "wizard_school_of_divination";
        public const string SchoolOfNecromancyId = "wizard_school_of_necromancy";
        public const string SchoolOfAbjurationId = "wizard_school_of_abjuration";
        public const string DraconicBloodlineId = "sorcerer_draconic_bloodline";
        public const string RangerHunterId = "ranger_hunter";
        public const string WayOfTheOpenHandId = "monk_way_of_the_open_hand";
        public const string WayOfTheFourElementsId = "monk_way_of_the_four_elements";
        public const string CircleOfTheMoonId = "druid_circle_of_the_moon";
        public const string CircleOfTheStarsId = "druid_circle_of_the_stars";
        public const string CircleOfTheLandId = "druid_circle_of_the_land";
        public const string OathOfTheAncientsId = "paladin_oath_of_the_ancients";
        public const string TheFiendId = "warlock_the_fiend";
        public const string LightDomainId = "cleric_light_domain";
        public const string WarDomainId = "cleric_war_domain";
        public const string BattleMasterId = "fighter_battle_master";

        public static CharacterDefinition Blank { get; } = new CharacterDefinition(
            BlankId, "白板", SpecialLogicType.None,
            description: "无特殊能力，用于教学与数值基准。");

        public static CharacterDefinition Breaker { get; } = new CharacterDefinition(
            BreakerId, "战士·破阵者", SpecialLogicType.Breaker,
            attackBonus: 1,
            description: "己方盾碎可连锁破盾，新合盾可修复另一手的碎盾；攻击主动碎盾命中附流血2回合。攻击 +1。");

        public static CharacterDefinition SchoolOfConjuration { get; } = new CharacterDefinition(
            SchoolOfConjurationId, "法师·咒法学派", SpecialLogicType.SchoolOfConjuration,
            attackBonus: -1,
            description: "空间刃无视护盾、正防御与闪避。王车易位：可跳过回合，与双手值相同（不分左右）的角色换位；距离立即更新，下轮按新座位行动。攻击 -1。");

        public static CharacterDefinition PathOfTheWarden { get; } = new CharacterDefinition(
            PathOfTheWardenId, "野蛮人·守望者道途", SpecialLogicType.PathOfTheWarden,
            defenseBonus: 1,
            description: "新合盾时可锁住对手一只手，使其不能作为运算源，直到该盾破碎。防御 +1。");

        /// <summary>
        /// 牧师·生命领域：攻击可改为回复；回复可以给任何人（自己或对手）；
        /// 双 9 额外回复。攻防 -1 / -1。
        /// </summary>
        public static CharacterDefinition LifeDomain { get; } = new CharacterDefinition(
            LifeDomainId, "牧师·生命领域", SpecialLogicType.LifeDomain,
            attackBonus: -1, defenseBonus: -1,
            englishName: "Life Domain",
            description: "攻击可改为回复；回复可给任何人，先清除全部负面状态再回血；双 9 额外回复。");

        /// <summary>兼容旧名。</summary>
        public static CharacterDefinition Priest => LifeDomain;

        public static CharacterDefinition Swashbuckler { get; } = new CharacterDefinition(
            SwashbucklerId, "游荡者·风流剑客", SpecialLogicType.Swashbuckler,
            defenseBonus: -2,
            description: "被攻击时可用一只「1」变成 2 完全闪避（可放弃）；攻击时可用一只「2」变成 3，" +
                         "让基础伤害 ×1.5，命中附流血2回合。攻防 0 / -2。");

        public static CharacterDefinition PathOfTheBeast { get; } = new CharacterDefinition(
            PathOfTheBeastId, "野蛮人·野兽道途", SpecialLogicType.PathOfTheBeast,
            attackBonus: -2, defenseBonus: -2,
            maxHealth: PlayerState.DefaultMaxHealth,   // 实际上限 = 20 + PathOfTheBeastLogic.HealthBonus(5) = 25
            description: "生命上限 +5（25）。运算后双手数字之和为 7 时，回复生命上限一半的血量，且攻防永久各 +1。" +
                         "攻防 -2 / -2。");

        public static CharacterDefinition Swordsman { get; } = new CharacterDefinition(
            SwordsmanId, "战士·剑士", SpecialLogicType.Swordsman,
            attackBonus: 2, defenseBonus: -1,
            description: "运算出 4/6/8 时，若另一只手与该手数字相同（双 4/双 6/双 8），" +
                         "则在主攻击前先追加一次固定 4/6/8 伤害、无视攻防的攻击，命中附流血2回合。" +
                         "攻防 +2 / -1。");

        public static CharacterDefinition WayOfTheNinja { get; } = new CharacterDefinition(
            WayOfTheNinjaId, "武僧·忍宗", SpecialLogicType.WayOfTheNinja,
            attackBonus: 1, defenseBonus: -1,
            description: "攻击改为两段伤害减半（4→两次 2、6→两次 3、8→两次 4），每段各自结算攻防加成。" +
                         "攻防 +1 / -1。");

        public static CharacterDefinition Assassin { get; } = new CharacterDefinition(
            AssassinId, "游荡者·刺客", SpecialLogicType.Assassin,
            attackBonus: 2, defenseBonus: -1,
            description: "两只手均为 7 时进入潜行：不能被攻击；每次潜行可以跳过一回合；" +
                         "从潜行发起的攻击伤害 +7，命中附流血2回合。攻防 +2 / -1。");

        /// <summary>
        /// 圣武士·奉献之誓：4/6 不再攻击，改为合盾；合盾与碎盾时对范围内一名角色
        /// 造成 2 点真实伤害（强制发动、玩家选目标）；
        /// 范围内有人被攻击时可以把目标改成自己。
        /// 攻防 0 / +1。
        /// </summary>
        public static CharacterDefinition OathOfDevotion { get; } = new CharacterDefinition(
            OathOfDevotionId, "圣武士·奉献之誓", SpecialLogicType.OathOfDevotion,
            attackBonus: 0, defenseBonus: 1,
            description: "4 与 6 不再攻击，改为合成盾牌；合出盾、主动碎盾、被打碎盾时，" +
                         "对攻击范围内一名角色造成 2 点真实伤害（强制发动，目标自选）；" +
                         "攻击范围内的角色被攻击时，可以把目标改成自己。攻防 0 / +1。");

        /// <summary>
        /// 邪术师·至高妖精：有人发起攻击时，若攻击数字与自己某只手相同，
        /// 可把那只手 +1（永久）来取消这次攻击 —— 取消最早结算，能取消法师·咒法学派。
        /// 战士·剑士追加攻击与圣武士·奉献之誓冲击的"攻击数字视为 0"，无法被取消。
        /// 体力不满时可跳过回合，把场上一人的体力改成与自己一致。
        /// 攻防 0 / -1，攻击距离无限。
        /// </summary>
        public static CharacterDefinition TheArchfey { get; } = new CharacterDefinition(
            TheArchfeyId, "邪术师·至高妖精", SpecialLogicType.TheArchfey,
            attackBonus: 0, defenseBonus: -1,
            description: "有人发起攻击时，若该攻击数字与自己某只手相同，可把那只手 +1（永久）" +
                         "以取消这次攻击（最早结算，可取消法师·咒法学派）；追加攻击与冲击不可被取消。" +
                         "体力不满时可跳过回合，把场上一人的体力改成与自己一致。攻防 0 / -1，攻击距离∞。");

        /// <summary>
        /// 战士·魔射手：4 改为两段各 4；6 伤害 +2；8 无视盾牌（仍可被闪避 / 取消）。
        /// 攻防 0 / -1，攻击距离 3。
        /// </summary>
        public static CharacterDefinition ArcaneArcher { get; } = new CharacterDefinition(
            ArcaneArcherId, "战士·魔射手", SpecialLogicType.ArcaneArcher,
            attackBonus: 0, defenseBonus: -1,
            description: "4 改为两段各 4 点伤害；6 的伤害 +2；8 无视盾牌（但仍可被闪避或取消）。" +
                         "攻防 0 / -1，攻击距离 3。");

        /// <summary>
        /// 战士·勇士：攻 +1 / 防 0，距离 1；攻击时双手和为 10，基础伤害 ×1.5。
        ///
        /// 玩家要求用它取代白板进入角色选择 ——
        /// 白板没有强度，作为可选角色没有意义。
        /// </summary>
        public static CharacterDefinition Champion { get; } = new CharacterDefinition(
            ChampionId, "战士·勇士", SpecialLogicType.Champion,
            attackBonus: 1, defenseBonus: 0,
            description: "十点重击：运算后双手和为10时，发起攻击的基础伤害×1.5，再计算攻防。攻+1 / 防0，距离1。");

        /// <summary>
        /// 游荡者·赌徒：攻 0 / 防 0，攻击距离 1。
        ///
        /// 可以赌博（1d9 的结果直接当作本次行动结果）；
        /// 上一回合做过正常运算的话，本回合必须赌博。
        /// </summary>
        public static CharacterDefinition Gambler { get; } = new CharacterDefinition(
            GamblerId, "游荡者·赌徒", SpecialLogicType.Gambler,
            attackBonus: 0, defenseBonus: 0,
            description: "运算时可以选择赌博：点自己一只手后开赌，1d9 的结果直接作为本次行动结果" +
                         "（4/6/8 攻击、9 回血、5 合盾）。上一回合做过正常运算的话，本回合必须赌博。" +
                         "攻防 0 / 0，攻击距离 1。");

        /// <summary>
        /// 圣武士·王冠之誓：攻 0 / 防 −1，攻击距离 1。
        ///
        /// 自己的盾碎后距离永久 +1，且攻击加成恒等于距离。
        /// </summary>
        public static CharacterDefinition OathOfTheCrown { get; } = new CharacterDefinition(
            OathOfTheCrownId, "圣武士·王冠之誓", SpecialLogicType.OathOfTheCrown,
            attackBonus: 0, defenseBonus: -1,
            description: "自己的盾牌破碎后，攻击距离永久 +1，且攻击加成恒等于攻击距离" +
                         "（初始都是 1；碎一次后都变 2，以此类推）。攻防 0 / −1。");

        /// <summary>
        /// 武僧·太极宗：攻 0 / 防 0，攻击距离 1。
        ///
        /// 5 不合盾而进入太极形态；被攻击时把 5 变 0、
        /// 受一半伤害并把剩余反弹给攻击方。
        /// </summary>
        public static CharacterDefinition WayOfTheTaiChi { get; } = new CharacterDefinition(
            WayOfTheTaiChiId, "武僧·太极宗", SpecialLogicType.WayOfTheTaiChi,
            attackBonus: 0, defenseBonus: 0,
            description: "5 不合成盾牌，而是进入太极形态：被攻击时可以把那张 5 变成 0，" +
                         "只受本次攻击应有伤害的一半，并把剩余的一半反弹给攻击方" +
                         "（反弹无视距离、可被盾挡下、可被游荡者·风流剑客闪避，但无法被邪术师·至高妖精取消）。" +
                         "对法师·咒法学派的攻击无减半也不反弹。自己的 0 被他人当加数触碰后立即变成 1。" +
                         "攻防 0 / 0，攻击距离 1。");

        /// <summary>
        /// 野蛮人·巨人道途：攻 +2 / 防 +2，攻击距离 1。
        ///
        /// 4/6/8 不攻击、5 不合盾；打出 5 发起两段攻击（伤害 = 另一只手点数）；
        /// 打出 9 进行两段回复。
        /// </summary>
        public static CharacterDefinition PathOfTheGiant { get; } = new CharacterDefinition(
            PathOfTheGiantId, "野蛮人·巨人道途", SpecialLogicType.PathOfTheGiant,
            attackBonus: 2, defenseBonus: 2,
            description: "4/6/8 无法攻击，5 也无法合成盾牌。打出 5 时发起两段攻击，" +
                         "每段的基础伤害等于另一只手的点数（之后各自再加攻防加成）；" +
                         "打出 9 时进行两段回复，每段回复量等于另一只手的点数。" +
                         "生命上限 +5（25）。攻防 +2 / +2。");

        /// <summary>
        /// 游荡者·盗贼：攻 0 / 防 −1，攻击距离 1。
        ///
        /// 可以放弃运算改为偷窃别人的数字。
        /// </summary>
        public static CharacterDefinition Thief { get; } = new CharacterDefinition(
            ThiefId, "游荡者·盗贼", SpecialLogicType.Thief,
            attackBonus: 0, defenseBonus: -1,
            description: "可以放弃运算，改为偷窃他人的数字：先点自己的一只手，" +
                         "再点偷窃按钮，然后点别人的手完成。偷窃即双方点数互换，" +
                         "原先的点数大于等于偷来的点数才生效，" +
                         "效果由偷来的数字决定（偷来 4 即发起攻击、偷来 5 即合成盾牌、" +
                         "偷来 9 即回复）；否则交换照做但没有效果。" +
                         "被偷的人只被改变点数，不触发效果。" +
                         "上回合发起过偷窃则本回合不能再偷。∞距离，可以偷队友。" +
                         "攻 0 / 防 −1。");

        /// <summary>
        /// 游侠·幽域追踪者：攻 0 / 防 −1，攻击距离 1。
        ///
        /// 运算后若两只手同时是 1/2/3 且数字相同，
        /// 可以（主动选择）把参与运算的那只手暗置，进入伏击。
        /// </summary>
        public static CharacterDefinition GloomStalker { get; } = new CharacterDefinition(
            GloomStalkerId, "游侠·幽域追踪者", SpecialLogicType.GloomStalker,
            attackBonus: 0, defenseBonus: -1,
            description: "运算后若两只手同时是 1/2/3 且数字相同，" +
                         "可以把参与运算的那只手暗置（不能发起运算、" +
                         "也不能被当成运算数字，但数值可见），进入伏击状态。" +
                         "伏击时攻击 +1、攻击距离 3；" +
                         "即将受到伤害时若在伏击，则恢复被暗置的手并抵挡此次伤害" +
                         "（触发时机在武僧·太极宗太极之后）。" +
                         "法师·咒法学派可以穿透伏击打出伤害，咒术攻击也不会解除伏击。" +
                         "攻 0 / 防 −1。");

        /// <summary>
        /// 游荡者·魔术师：攻 0 / 防 −1，攻击距离 2。
        ///
        /// 可以选择自己的手当被加数；运算出 2 可以攻击。
        /// </summary>
        public static CharacterDefinition Magician { get; } = new CharacterDefinition(
            MagicianId, "游荡者·魔术师", SpecialLogicType.Magician,
            attackBonus: 0, defenseBonus: -1,
            description: "可以选择自己的手当被加数" +
                         "（例：左手 3、右手 5，拿左手 3 碰自己的 5 凑出 8）；" +
                         "两只手任选一个方向，变的是先点的那只手，一只手不能加自己。" +
                         "运算出 2/4/6/8 都是攻击，伤害 = 牌面数字（吃攻防加成）。" +
                         "攻 0 / 防 −1，攻击距离 2。");

        /// <summary>
        /// 法师·塑能学派（SchoolOfEvocation）：攻 0 / 防 −1，距离 1，中立善良。
        ///
        /// 运算出攻击数字（4/6/8）时可放弃这次攻击，
        /// 给下一次攻击指定一种伤害类型（10 选 1）；
        /// 命中后给目标挂上对应负面状态，持续目标自己的 2 个回合。
        /// </summary>
        public static CharacterDefinition SchoolOfEvocation { get; } = new CharacterDefinition(
            SchoolOfEvocationId, "法师·塑能学派", SpecialLogicType.SchoolOfEvocation,
            attackBonus: 0, defenseBonus: -1,
            description: "运算出攻击数字（4/6/8）时，可以选择放弃这次攻击，" +
                         "改为给下一次攻击指定一种伤害类型（10 选 1）：" +
                         "雷鸣（残响）、强酸、毒素、光耀（目盲）、暗蚀（腐朽）、" +
                         "火焰（燃烧）、寒冷（冰冻）、闪电（感电）、" +
                         "力场（同法师·咒法学派，不挂状态）、心灵（震慑）。" +
                         "命中后目标获得对应负面状态，持续目标自己的 2 个回合；" +
                         "被盾抵挡 / 太极减免仍算命中，被闪避或被取消则作废（要重新放弃一次）。" +
                         "连续放弃时只保留最新的选择。" +
                         "攻 0 / 防 −1，攻击距离 1。");

        /// <summary>
        /// 术士·狂野魔法（WildMagic）：攻 0 / 防 −1，攻击距离无限，混乱善良。
        ///
        /// 每次攻击各 8% 概率改写成 10 种元素之一（合计 80%），余 20% 普攻；
        /// 改写时按该元素挂状态（力场不挂，走穿盾）。
        /// 普攻：距离 1 以内钝击、1 以外穿刺。
        /// </summary>
        public static CharacterDefinition WildMagic { get; } = new CharacterDefinition(
            WildMagicId, "术士·狂野魔法", SpecialLogicType.WildMagic,
            attackBonus: 0, defenseBonus: -1,
            description: "每次攻击各有 8% 概率改写成 10 种元素之一" +
                         "（雷鸣 / 强酸 / 毒素 / 光耀 / 暗蚀 / 火焰 / 寒冷 / 闪电 / 心灵 / 力场），" +
                         "合计 80%；剩下 20% 为普通攻击。" +
                         "改写后按该元素挂对应的负面状态（力场不挂状态，改为穿盾、无视正向防御、不可闪避）。" +
                         "被盾抵挡 / 太极减免仍算命中，被闪避或被取消仍作废。" +
                         "普通攻击对距离 1 以内为钝击伤害、1 以外为穿刺伤害。" +
                         "攻 0 / 防 −1，攻击距离∞。");



        /// <summary>
        /// 邪术师·旧日支配者：攻 0 / 防 -1，攻击距离无限，中立邪恶。
        /// 命中（闪避 / 取消除外）令自己与该目标的距离 +2，持续目标自己的 2 个回合。
        /// </summary>
        public static CharacterDefinition TheGreatOldOne { get; } = new CharacterDefinition(
            TheGreatOldOneId, "邪术师·旧日支配者", SpecialLogicType.TheGreatOldOne,
            attackBonus: 0, defenseBonus: -1,
            description: "你命中的攻击（除闪避 / 取消外）令你与目标之间的距离 +2，" +
                         "直到目标下下个回合结束 —— 他射程不足时这两个回合都打不到你。" +
                         "距离只影响你与他之间，不改变其他玩家之间的距离；" +
                         "重复命中只刷新时长、不叠加，且不封顶。" +
                         "攻击对距离 1 以内为挥砍伤害、1 以外为穿刺伤害。" +
                         "攻 0 / 防 -1，攻击距离∞。");

        /// <summary>
        /// 吟游诗人·勇气学院：攻 0 / 防 -1，攻击距离 3，守序中立，伤害类型穿刺。
        /// 每次行动结算完，若本回合受过伤害或实际回复过体力即可发动。
        /// </summary>
        public static CharacterDefinition CollegeOfValor { get; } = new CharacterDefinition(
            CollegeOfValorId, "吟游诗人·勇气学院", SpecialLogicType.CollegeOfValor,
            attackBonus: 0, defenseBonus: -1,
            description: "每次行动结算完，若本回合受过伤害或回复过体力" +
                         "（实际回 0 不算，状态伤害也算），可发动技能：" +
                         "先选增幅攻击 / 增幅防御 / 削弱攻击 / 削弱防御之一，" +
                         "再选攻击范围内 1~3 名角色（含自己，同一人点两只手只算一次），" +
                         "给他们对应的攻防加成 ±(4-目标数)：1 人 ±3、2 人 ±2、3 人 ±1。" +
                         "持续到你下一次发动技能（旧的立即移除），或你死亡时立即清除。" +
                         "攻 0 / 防 -1，攻击距离 3，伤害类型穿刺。");

        public static CharacterDefinition Oathbreaker { get; } = new CharacterDefinition(
            OathbreakerId, "圣武士·破誓者", SpecialLogicType.Oathbreaker,
            attackBonus: 2, defenseBonus: 0,
            englishName: "Oathbreaker",
            description: "5 不再合盾，改为造成 5 点钝击伤害；攻击距离 1，攻防 +2/0。被攻击时可把目标转移给一名持完整盾牌的非攻击者（玩家自选）。");

        public static CharacterDefinition PathOfTheBerserker { get; } = new CharacterDefinition(
            PathOfTheBerserkerId, "野蛮人·狂战士道途", SpecialLogicType.PathOfTheBerserker,
            attackBonus: -1, defenseBonus: -2,
            englishName: "Path of the Berserker",
            description: "生命上限 +5；9 不再回复，改为造成 9 点挥砍伤害；每少 5 点生命获得 +1/+1；造成伤害后回复伤害一半。攻防 -1/-2，攻击距离 1。");

        public static CharacterDefinition Tactician { get; } = new CharacterDefinition(
            TacticianId, "战士·战术家", SpecialLogicType.Tactician,
            attackBonus: 1, defenseBonus: -1,
            englishName: "Tactician",
            description: "额外获得 +X/+X，X = 场上最高与最低体力差 ÷ 3（向下取整）；挥砍伤害，攻防 +1/-1，攻击距离 1。");

        public static CharacterDefinition SchoolOfDivination { get; } = new CharacterDefinition(
            SchoolOfDivinationId, "法师·预言学派", SpecialLogicType.SchoolOfDivination,
            attackBonus: 0, defenseBonus: -1,
            englishName: "School of Divination",
            description: "攻击距离∞，距离1内钝击、1外穿刺。打出7后至你的下个回合开始：其他角色行动时若手牌有数字等于你打出7时另一只手，该行动直接无效、不触发技能。");

        public static CharacterDefinition SchoolOfNecromancy { get; } = new CharacterDefinition(
            SchoolOfNecromancyId, "法师·死灵学派", SpecialLogicType.SchoolOfNecromancy,
            attackBonus: 0, defenseBonus: -1,
            englishName: "School of Necromancy",
            description: "攻击距离∞，距离1内钝击、1外穿刺。受到攻击时可用2→1完全挡下一段（力场除外）。回合开始有可用1时自动进入免费暗蚀选目标，可放弃；攻击1点暗蚀并附加2回合腐朽，不消耗手值和正常行动。玩家死亡后可与死者交换0~2只手的数字。");

        public static CharacterDefinition SchoolOfAbjuration { get; } = new CharacterDefinition(
            SchoolOfAbjurationId, "法师·防护学派", SpecialLogicType.SchoolOfAbjuration,
            attackBonus: 0, defenseBonus: -1,
            englishName: "School of Abjuration",
            description: "攻击距离∞，距离1内钝击、1外穿刺。打出2选择存活角色，赋予物理整组或雷鸣、强酸、火焰、寒冷、闪电抗性；不能赋予心灵、力场、毒素、光耀、暗蚀抗性。攻击数字可存储奥术屏障，在护盾/抗性后、扣血前吸收，使用即清空。");

        /// <summary>
        /// 牧师·光明领域（2026-10-23）：攻 0 / 防 -1，攻击距离无限，中立善良。
        /// 攻击为光耀伤害、最终伤害恒为 4（无视攻防与数值增减益，防御机制照常），
        /// 命中附带 2 回合圣光。
        /// </summary>
        public static CharacterDefinition LightDomain { get; } = new CharacterDefinition(
            LightDomainId, "牧师·光明领域", SpecialLogicType.LightDomain,
            attackBonus: 0, defenseBonus: -1,
            englishName: "Light Domain",
            description: "攻击为光耀伤害、最终伤害恒为4（4/6/8一律4点，无视攻防与倒伏×1.5等增减益；护盾/闪避/转移/太极照常生效）。命中附带2回合圣光：立即清除目标其他负面状态与全部抗性（勇气战歌与奥术屏障除外），期间阻止新状态与抗性附加；目标回合开始受1点（不可抵挡）、回复量减半（向上取整）。攻防0/-1，攻击距离∞。");

        /// <summary>
        /// 牧师·战争领域（2026-10-23）：攻 0 / 防 -1，攻击距离无限，守序中立。
        /// 受到攻击（被选为目标即算）或发起攻击后 +1/+1（可叠加）；
        /// 自己回合结束时若本回合未发起过攻击，加成清零。
        /// </summary>
        public static CharacterDefinition WarDomain { get; } = new CharacterDefinition(
            WarDomainId, "牧师·战争领域", SpecialLogicType.WarDomain,
            attackBonus: 0, defenseBonus: -1,
            englishName: "War Domain",
            description: "受到攻击（被选为目标即算，闪避/盾挡/零伤害也算）或发起攻击后，攻防各+1、可叠加；自己回合结束时若本回合未发起过攻击，立即清空全部加成。距离1内为钝击伤害、1以外为穿刺伤害。攻防0/-1，攻击距离∞。");

        /// <summary>
        /// 战士·战斗大师（2026-10-23）：攻 0 / 防 0，攻击距离 2，守序中立。
        /// 攻击伤害按数字分型（4 挥砍 / 6 钝击 / 8 穿刺）；
        /// 造成伤害后按另一只手附加招式状态。
        /// </summary>
        public static CharacterDefinition BattleMaster { get; } = new CharacterDefinition(
            BattleMasterId, "战士·战斗大师", SpecialLogicType.BattleMaster,
            attackBonus: 0, defenseBonus: 0,
            englishName: "Battle Master",
            description: "攻击伤害按数字分型：4为挥砍、6为钝击、8为穿刺。攻击实际造成伤害后（>0，盾挡/闪避/取消不触发），若另一只手为4/8，选择给目标附加流血或跛脚2回合；另一只手为6，选择附加震慑或倒伏2回合。攻防0/0，攻击距离2。");

        public static CharacterDefinition DraconicBloodline { get; } = new CharacterDefinition(
            DraconicBloodlineId, "术士·龙族血脉", SpecialLogicType.DraconicBloodline,
            defenseBonus: -1, englishName: "Draconic Bloodline",
            description: "开局选择寒冷/火焰/闪电/毒素/强酸之一；攻击改为该类型，获得该元素永久抗性，该类型状态持续3回合。攻0/防-1，距离∞。");

        public static CharacterDefinition RangerHunter { get; } = new CharacterDefinition(
            RangerHunterId, "游侠·猎人", SpecialLogicType.RangerHunter,
            defenseBonus: -1, englishName: "Hunter",
            description: "距离3穿刺。打出7可在攻击范围内标记一名角色；攻击可额外指定射程内的标记角色，伤害分开计算。攻击标记目标时无视正防御并加已损体力/5，随后消耗标记。");

        public static CharacterDefinition WayOfTheOpenHand { get; } = new CharacterDefinition(
            WayOfTheOpenHandId, "武僧·散打宗", SpecialLogicType.WayOfTheOpenHand,
            englishName: "Way of the Open Hand",
            description: "开局2气、上限6；行动后双手相同得1气。攻击时4气挂共鸣，或2气给目标倒伏/震慑2回合。攻击共鸣目标引爆半伤真实伤害。回合开始气=体力可跳回合发动混元体，移除全部气并回复2倍气点。");

        public static CharacterDefinition WayOfTheFourElements { get; } = new CharacterDefinition(
            WayOfTheFourElementsId, "武僧·四象宗", SpecialLogicType.WayOfTheFourElements,
            englishName: "Way of the Four Elements",
            description: "开局2气、上限6；行动后双手相同得1气。攻击可耗2气改火焰/寒冷/闪电/雷鸣/强酸；受到这些元素攻击时有气则自动耗1气获得该元素抗性。");

        public static CharacterDefinition CircleOfTheMoon { get; } = new CharacterDefinition(
            CircleOfTheMoonId, "德鲁伊·月亮结社", SpecialLogicType.CircleOfTheMoon,
            englishName: "Circle of the Moon",
            description: "回合开始双手和10可进入荒野形态；熊-1/+2且4合盾，狼+2/-1且攻击吸血1/2，鹰+1/-1且射程3/攻击全穿刺/6无法攻击，蛇0/0且攻击改毒素。回合结束双手相同可解除；不能直接切换形态。");

        public static CharacterDefinition CircleOfTheStars { get; } = new CharacterDefinition(
            CircleOfTheStarsId, "德鲁伊·星辰结社", SpecialLogicType.CircleOfTheStars,
            englishName: "Circle of the Stars",
            description: "回合开始双手和10可进入星耀形态；射手+2/-1且攻击光耀，圣杯0/0且9的回复可额外给一名其他角色，巨龙+1/+1且运算先加一只手、结算前再加一只手。回合结束双手相同可解除；不能直接切换形态。");

        public static CharacterDefinition CircleOfTheLand { get; } = new CharacterDefinition(
            CircleOfTheLandId, "德鲁伊·大地结社", SpecialLogicType.CircleOfTheLand,
            defenseBonus: 1, englishName: "Circle of the Land",
            description: "回合结束选一只手，直到下回合不能作为被加数；打出3可令另一名角色束缚2回合。攻0/防+1，距离1钝击。");

        public static CharacterDefinition OathOfTheAncients { get; } = new CharacterDefinition(
            OathOfTheAncientsId, "圣武士·古贤之誓", SpecialLogicType.OathOfTheAncients,
            attackBonus: -1, defenseBonus: 1, englishName: "Oath of the Ancients",
            description: "盾牌破碎时可令攻击范围内一名角色束缚2回合；受到实际伤害后，体力≥10时给攻击范围内另一名角色回复2，否则自己回复2。攻-1/防+1，距离1钝击。");

        public static CharacterDefinition TheFiend { get; } = new CharacterDefinition(
            TheFiendId, "邪术师·妖魔", SpecialLogicType.TheFiend,
            defenseBonus: -1, englishName: "The Fiend",
            description: "治疗溢出转为虚假生命（上限10），伤害优先扣虚假生命；攻击时可移除全部虚假生命令目标失能/恐慌2回合；造成伤害后目标≤半血则回复3。攻0/防-1，无限距离，近钝击远穿刺。");

        /// <summary>全部内置角色（顺序即为角色选择界面的建议顺序）。</summary>
        public static CharacterDefinition[] BuiltIn { get; } =
        {
            // ⚠️ 白板已被战士·勇士取代（玩家要求）—— 白板不再出现在选择列表里。
            //    它仍作为兜底定义保留（未登记的逻辑类型会退化成它）。
            Champion, Breaker, SchoolOfConjuration, PathOfTheWarden, LifeDomain, Swashbuckler, PathOfTheBeast,
            Swordsman, WayOfTheNinja, Assassin, OathOfDevotion, TheArchfey, ArcaneArcher, Gambler, OathOfTheCrown,
            WayOfTheTaiChi, PathOfTheGiant, Thief, GloomStalker, Magician, SchoolOfEvocation, WildMagic, TheGreatOldOne, CollegeOfValor,
            Oathbreaker, PathOfTheBerserker, Tactician,
            SchoolOfDivination, SchoolOfNecromancy, SchoolOfAbjuration,
            LightDomain, WarDomain, BattleMaster,
            DraconicBloodline, RangerHunter, WayOfTheOpenHand, WayOfTheFourElements,
            CircleOfTheMoon, CircleOfTheStars,
            CircleOfTheLand, OathOfTheAncients, TheFiend
            // ⚠️ TheGreatOldOne / CollegeOfValor 已实现但「还没有 AI 策略」——
            //    在轮询赛里会因 AI 无手可下而停滞（实测 8 例「僵局」）。
            //    补齐 AI 后再接入可选列表。
        };
    }
}
