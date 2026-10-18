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

        /// <summary>咒术师：攻击无视对方护盾。</summary>
        Curseweaver = 1,

        /// <summary>战士：破坏对方护盾时连锁破坏另一面；可修复己方护盾。</summary>
        Fighter = 2,

        /// <summary>野蛮人：造成伤害后锁定对方一只手一回合。</summary>
        Barbarian = 3,

        /// <summary>牧师：9 的回复可指定给自己或对手；双手同时为 9 时额外再回复一次。</summary>
        Priest = 4,

        /// <summary>游侠：可用"1"闪避攻击；可用"2"强化攻击基础伤害 ×1.5。</summary>
        Ranger = 5,

        /// <summary>狼人：生命上限 +5；运算后双手和为 7 时回复并永久 +1/+1。</summary>
        Werewolf = 6,

        /// <summary>剑客：双 4/6/8 时追加一次无视攻防加成的固定伤害攻击。</summary>
        Swordsman = 7,

        /// <summary>忍者：攻击改为两段伤害减半。</summary>
        Ninja = 8,

        /// <summary>刺客：双手为 7 时潜行（不可被攻击、可跳过一次回合、出手 +7）。</summary>
        Assassin = 9,

        /// <summary>
        /// 圣武士：4/6 改为合盾；合盾与碎盾时对范围内一人造成 2 点真实伤害；
        /// 范围内有人被打时可以把目标改成自己。
        /// </summary>
        Paladin = 10,

        /// <summary>
        /// 妖术师：有人发起攻击时，若攻击数字与自己某只手相同，
        /// 可把那只手 +1 来取消这次攻击（最早结算）。
        /// 体力不满时可跳过回合，把场上一人的体力改成与自己一致。
        /// </summary>
        Hexer = 11,

        /// <summary>
        /// 魔射手：4 改为两段各 4；6 伤害 +2；8 无视盾牌。
        /// </summary>
        ArcaneArcher = 12,

        /// <summary>
        /// 士兵：没有特殊能力，但攻防各 +1，作为"有点强度"的基础角色。
        /// **取代原来的白板**进入角色选择。
        /// </summary>
        Soldier = 13,

        /// <summary>
        /// 赌徒：可以赌博 —— 1d9 的结果直接作为本次行动结果
        /// （4/6/8 攻击、9 回血、5 合盾）。
        /// 上一回合做过正常运算，本回合就必须赌博。
        /// </summary>
        Gambler = 14,

        /// <summary>
        /// 骑士：盾牌破碎后攻击距离永久 +1，且**攻击加成恒等于攻击距离**
        /// （初始都是 1，碎一次后都变 2）。
        /// </summary>
        Knight = 15,

        /// <summary>
        /// 武僧：5 不合盾而进入**太极形态**；被攻击时把 5 变 0，
        /// 受一半伤害并把剩余反弹给攻击方。0 被他人触碰后立即变 1。
        /// </summary>
        Monk = 16,

        /// <summary>
        /// 巨人：4/6/8 不攻击、5 不合盾；打出 5 发起两段攻击
        /// （每段基础伤害 = 另一只手的点数）；打出 9 进行两段回复。
        /// </summary>
        Giant = 17,

        /// <summary>
        /// 盗贼：可以放弃运算改为**偷窃**别人的数字 ——
        /// 双方点数与状态互换，**原先 ≥ 偷来的**才生效，
        /// 效果由**偷来的数字**决定。上回合偷过则本回合不能再偷。
        /// </summary>
        Thief = 18,

        /// <summary>猎人：伏击（暗置 + 抵伤）。</summary>
        Hunter = 19,

        /// <summary>魔术师：自己的手当被加数，2 可攻击。</summary>
        Magician = 20,

        /// <summary>
        /// 法师（Wizard）：运算出攻击数字（4/6/8）时可**放弃这次攻击**，
        /// 给**下一次**攻击指定一种**伤害类型**（10 选 1）；命中后给目标
        /// 挂上对应负面状态（持续目标自己的 2 个回合）。
        /// 力场不挂状态，只按咒术师的方式穿透。
        /// </summary>
        Wizard = 21
    }
}
