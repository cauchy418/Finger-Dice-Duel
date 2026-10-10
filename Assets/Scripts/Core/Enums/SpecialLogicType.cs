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
        Warrior = 2,

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
        Assassin = 9
    }
}
