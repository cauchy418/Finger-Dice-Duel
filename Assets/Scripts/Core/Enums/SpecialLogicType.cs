namespace FingerDiceDuel.Core
{
    /// <summary>
    /// 角色特殊逻辑标识，用于 CharacterData / CharacterFactory 在编辑器中
    /// 快速映射到对应的 ICharacterBehavior 实现。
    ///
    /// 新增角色时，先在此枚举登记，再由 CharacterFactory 注入具体 Behavior。
    /// </summary>
    public enum SpecialLogicType
    {
        /// <summary>白板：无特殊逻辑。</summary>
        None = 0,

        /// <summary>法师·咒法学派：攻击无视对方护盾。</summary>
        SchoolOfConjuration = 1,

        /// <summary>战士·破阵者：破坏对方护盾时连锁破坏另一面；可修复己方护盾。</summary>
        Breaker = 2,

        /// <summary>野蛮人·守望者道途：造成伤害后锁定对方一只手一回合。</summary>
        PathOfTheWarden = 3,

        /// <summary>牧师·生命领域：9 的回复可指定给自己或对手；双手同时为 9 时额外再回复一次。</summary>
        LifeDomain = 4,

        /// <summary>兼容旧名。</summary>
        Priest = LifeDomain,

        /// <summary>游荡者·风流剑客：可用"1"闪避攻击；可用"2"强化攻击基础伤害 ×1.5。</summary>
        Swashbuckler = 5,

        /// <summary>野蛮人·野兽道途：生命上限 +5；运算后双手和为 7 时回复并永久 +1/+1。</summary>
        PathOfTheBeast = 6,

        /// <summary>战士·剑士：双 4/6/8 时追加一次无视攻防加成的固定伤害攻击。</summary>
        Swordsman = 7,

        /// <summary>武僧·忍宗：攻击改为两段伤害减半。</summary>
        WayOfTheNinja = 8,

        /// <summary>游荡者·刺客：双手为 7 时潜行（不可被攻击、可跳过一次回合、出手 +7）。</summary>
        Assassin = 9,

        /// <summary>
        /// 圣武士·奉献之誓：4/6 改为合盾；合盾与碎盾时对范围内一人造成 2 点真实伤害；
        /// 范围内有人被打时可以把目标改成自己。
        /// </summary>
        OathOfDevotion = 10,

        /// <summary>
        /// 邪术师·至高妖精：有人发起攻击时，若攻击数字与自己某只手相同，
        /// 可把那只手 +1 来取消这次攻击（最早结算）。
        /// 体力不满时可跳过回合，把场上一人的体力改成与自己一致。
        /// </summary>
        TheArchfey = 11,

        /// <summary>
        /// 战士·魔射手：4 改为两段各 4；6 伤害 +2；8 无视盾牌。
        /// </summary>
        ArcaneArcher = 12,

        /// <summary>
        /// 战士·勇士：攻+1 / 防0，运算后双手和10时，发起攻击的基础伤害×1.5。
        /// **取代原来的白板**进入角色选择。
        /// </summary>
        Champion = 13,

        /// <summary>
        /// 游荡者·赌徒：可以赌博 —— 1d9 的结果直接作为本次行动结果
        /// （4/6/8 攻击、9 回血、5 合盾）。
        /// 上一回合做过正常运算，本回合就必须赌博。
        /// </summary>
        Gambler = 14,

        /// <summary>
        /// 圣武士·王冠之誓：盾牌破碎后攻击距离永久 +1，且**攻击加成恒等于攻击距离**
        /// （初始都是 1，碎一次后都变 2）。
        /// </summary>
        OathOfTheCrown = 15,

        /// <summary>
        /// 武僧·太极宗：5 不合盾而进入**太极形态**；被攻击时把 5 变 0，
        /// 受一半伤害并把剩余反弹给攻击方。0 被他人触碰后立即变 1。
        /// </summary>
        WayOfTheTaiChi = 16,

        /// <summary>
        /// 野蛮人·巨人道途：4/6/8 不攻击、5 不合盾；打出 5 发起两段攻击
        /// （每段基础伤害 = 另一只手的点数）；打出 9 进行两段回复。
        /// </summary>
        PathOfTheGiant = 17,

        /// <summary>
        /// 游荡者·盗贼：可以放弃运算改为**偷窃**别人的数字 ——
        /// 双方点数与状态互换，**原先 ≥ 偷来的**才生效，
        /// 效果由**偷来的数字**决定。上回合偷过则本回合不能再偷。
        /// </summary>
        Thief = 18,

        /// <summary>游侠·幽域追踪者：伏击（暗置 + 抵伤）。</summary>
        GloomStalker = 19,

        /// <summary>游荡者·魔术师：自己的手当被加数，2 可攻击。</summary>
        Magician = 20,

        /// <summary>
        /// 法师·塑能学派（SchoolOfEvocation）：运算出攻击数字（4/6/8）时可**放弃这次攻击**，
        /// 给**下一次**攻击指定一种**伤害类型**（10 选 1）；命中后给目标
        /// 挂上对应负面状态（持续目标自己的 2 个回合）。
        /// 力场不挂状态，只按法师·咒法学派的方式穿透。
        /// </summary>
        SchoolOfEvocation = 21,

        /// <summary>
        /// 术士·狂野魔法（WildMagic）：每次攻击**各 8%** 概率改写成 10 种元素之一
        /// （合计 80%），余 20% 普攻。改写成元素时按该元素挂状态，
        /// 力场走穿盾规则。普攻：距离 1 内钝击、1 外穿刺。
        /// </summary>
        WildMagic = 22,

        /// <summary>
        /// 邪术师·旧日支配者（TheGreatOldOne）：命中的攻击令**自己与该目标之间的距离 +2**，
        /// 持续到目标自己的 2 个回合结束（刷新、不叠加、不封顶）。
        /// 距离只影响这一对，不改变其他玩家之间的距离。
        /// </summary>
        TheGreatOldOne = 23,

        /// <summary>
        /// 吟游诗人·勇气学院（CollegeOfValor）：每次行动结算完，若本回合受过伤害或实际回复过体力，
        /// 可发动 —— 选「增幅/削弱 攻击/防御」四项之一，
        /// 再选 1~3 个攻击范围内的角色，给他们的攻防加成 ±(4−X)。
        /// 持续到下次发动或死亡。
        /// </summary>
        CollegeOfValor = 24,

        /// <summary>圣武士·破誓者：5 改为攻击；被攻击时可将目标转移给持完整盾者。</summary>
        Oathbreaker = 25,

        /// <summary>野蛮人·狂战士道途：9 改为攻击；失血越多攻防越高；造成伤害回复一半。</summary>
        PathOfTheBerserker = 26,

        /// <summary>战士·战术家：按场上最高与最低体力差获得动态攻防。</summary>
        Tactician = 27,

        /// <summary>法师·预言学派：7 后的窗口内，符合手值条件的行动无效。</summary>
        SchoolOfDivination = 28,

        /// <summary>法师·死灵学派：2 抵挡、回合开始暗蚀攻击、死亡换牌。</summary>
        SchoolOfNecromancy = 29,

        /// <summary>法师·防护学派：抗性、奥术屏障。</summary>
        SchoolOfAbjuration = 30,

        /// <summary>
        /// 牧师·光明领域：攻击恒为 4 点光耀伤害（无视攻防与增减益），
        /// 命中附带 2 回合圣光（净化并阻止附加、回复减半、回合开始掉 1 点）。
        /// </summary>
        LightDomain = 31,

        /// <summary>
        /// 牧师·战争领域：受到攻击（被选为目标即算）或发起攻击后攻防 +1/+1（可叠加）；
        /// 自己回合结束时若本回合未发起过攻击，加成清零。距离 1 内钝击、外穿刺。
        /// </summary>
        WarDomain = 32,

        /// <summary>
        /// 战士·战斗大师：4=挥砍、6=钝击、8=穿刺；攻击实际造成伤害后，
        /// 按另一只手 4/8 → 流血/跛脚、6 → 眩晕/倒伏 给目标附加 2 回合状态（可选）。
        /// </summary>
        BattleMaster = 33
    }
}
