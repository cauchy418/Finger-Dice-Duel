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
                                   string description = null)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("角色 Id 不能为空。", nameof(id));

            Id = id;
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName;
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
        public const string WarriorId = "warrior";
        public const string CurseweaverId = "curseweaver";
        public const string BarbarianId = "barbarian";
        public const string PriestId = "priest";
        public const string RangerId = "ranger";
        public const string WerewolfId = "werewolf";
        public const string SwordsmanId = "swordsman";
        public const string NinjaId = "ninja";
        public const string AssassinId = "assassin";

        public static CharacterDefinition Blank { get; } = new CharacterDefinition(
            BlankId, "白板", SpecialLogicType.None,
            description: "无特殊能力，用于教学与数值基准。");

        public static CharacterDefinition Warrior { get; } = new CharacterDefinition(
            WarriorId, "战士", SpecialLogicType.Warrior,
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
        /// 牧师：9 的回复可指定给任何人（自己或对手）；**4/6/8 可选当攻击或当回复**；
        /// 双手同时为 9 时额外再回复一次 9（同样可选目标）。攻防 -1 / -1。
        /// </summary>
        public static CharacterDefinition Priest { get; } = new CharacterDefinition(
            PriestId, "牧师", SpecialLogicType.Priest,
            attackBonus: -1, defenseBonus: -1,
            description: "回复可以给任何人（自己或对手）：" +
                         "打出 9 时点一只手决定回复给谁（点自己的手=回自己，点对手的手=回对手）；" +
                         "打出 4/6/8 时可自行选择当攻击打还是当回复用" +
                         "（回复量就是该数字，不受攻防加成影响），选了回复后同样点一只手决定给谁；" +
                         "双手同时为 9 时额外再回复一次 9 点（同样可选目标）。攻防 -1 / -1。");

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

        /// <summary>全部内置角色（顺序即为角色选择界面的建议顺序）。</summary>
        public static CharacterDefinition[] BuiltIn { get; } =
        {
            Blank, Warrior, Curseweaver, Barbarian, Priest, Ranger, Werewolf,
            Swordsman, Ninja, Assassin
        };
    }
}
