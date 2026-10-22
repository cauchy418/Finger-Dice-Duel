using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    /// <summary>职业大类的稳定标识、中英名称及徽记。德鲁伊预留，不新增可玩角色。</summary>
    public sealed class CharacterProfession
    {
        public string Id { get; }
        public string NameZh { get; }
        public string NameEn { get; }
        public string IconKey => "Professions/" + Id;
        private CharacterProfession(string id, string zh, string en) { Id=id; NameZh=zh; NameEn=en; }
        public static readonly IReadOnlyList<CharacterProfession> All = new[]
        {
            new CharacterProfession("barbarian", "野蛮人", "Barbarian"),
            new CharacterProfession("bard", "吟游诗人", "Bard"),
            new CharacterProfession("cleric", "牧师", "Cleric"),
            new CharacterProfession("druid", "德鲁伊", "Druid"),
            new CharacterProfession("fighter", "战士", "Fighter"),
            new CharacterProfession("monk", "武僧", "Monk"),
            new CharacterProfession("paladin", "圣武士", "Paladin"),
            new CharacterProfession("ranger", "游侠", "Ranger"),
            new CharacterProfession("rogue", "游荡者", "Rogue"),
            new CharacterProfession("sorcerer", "术士", "Sorcerer"),
            new CharacterProfession("warlock", "邪术师", "Warlock"),
            new CharacterProfession("wizard", "法师", "Wizard"),
        };
        public static CharacterProfession Find(string id)
        {
            foreach (var profession in All) if (profession.Id == id) return profession;
            return null;
        }
    }
}
