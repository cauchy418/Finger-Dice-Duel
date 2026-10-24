using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    /// <summary>33个子职的唯一命名与归属表；旧角色ID在此兼容，数值和技能逻辑保持原样。</summary>
    public sealed class CharacterIdentity
    {
        public string LegacyId { get; }
        public string Id { get; }
        public string ProfessionId { get; }
        public CharacterProfession Profession => CharacterProfession.Find(ProfessionId);
        public string SubclassNameZh { get; }
        public string SubclassNameEn { get; }
        public string FullNameZh => Profession.NameZh + "·" + SubclassNameZh;
        public string FullNameEn => Profession.NameEn + " · " + SubclassNameEn;
        private CharacterIdentity(string legacy, string id, string profession, string zh, string en)
        { LegacyId=legacy; Id=id; ProfessionId=profession; SubclassNameZh=zh; SubclassNameEn=en; }
        public static readonly IReadOnlyList<CharacterIdentity> All = new[]
        {
            new CharacterIdentity("soldier", "fighter_champion", "fighter", "勇士", "Champion"),
            new CharacterIdentity("fighter", "fighter_breaker", "fighter", "破阵者", "Breaker"),
            new CharacterIdentity("arcane_archer", "fighter_arcane_archer", "fighter", "魔射手", "Arcane Archer"),
            new CharacterIdentity("knight", "paladin_oath_of_the_crown", "paladin", "王冠之誓", "Oath of the Crown"),
            new CharacterIdentity("paladin", "paladin_oath_of_devotion", "paladin", "奉献之誓", "Oath of Devotion"),
            new CharacterIdentity("oathbreaker", "paladin_oathbreaker", "paladin", "破誓者", "Oathbreaker"),
            new CharacterIdentity("cleric", "cleric_life_domain", "cleric", "生命领域", "Life Domain"),
            new CharacterIdentity("curseweaver", "wizard_school_of_conjuration", "wizard", "咒法学派", "School of Conjuration"),
            new CharacterIdentity("hexer", "warlock_the_archfey", "warlock", "至高妖精", "The Archfey"),
            new CharacterIdentity("warlock", "warlock_the_great_old_one", "warlock", "旧日支配者", "The Great Old One"),
            new CharacterIdentity("wizard", "wizard_school_of_evocation", "wizard", "塑能学派", "School of Evocation"),
            new CharacterIdentity("sorcerer", "sorcerer_wild_magic", "sorcerer", "狂野魔法", "Wild Magic"),
            new CharacterIdentity("bard", "bard_college_of_valor", "bard", "勇气学院", "College of Valor"),
            new CharacterIdentity("ranger", "rogue_swashbuckler", "rogue", "风流剑客", "Swashbuckler"),
            new CharacterIdentity("hunter", "ranger_gloom_stalker", "ranger", "幽域追踪者", "Gloom Stalker"),
            new CharacterIdentity("thief", "rogue_thief", "rogue", "盗贼", "Thief"),
            new CharacterIdentity("assassin", "rogue_assassin", "rogue", "刺客", "Assassin"),
            new CharacterIdentity("magician", "rogue_magician", "rogue", "魔术师", "Magician"),
            new CharacterIdentity("swordsman", "fighter_swordsman", "fighter", "剑士", "Swordsman"),
            new CharacterIdentity("monk", "monk_way_of_the_tai_chi", "monk", "太极宗", "Way of the Tai Chi"),
            new CharacterIdentity("ninja", "monk_way_of_the_ninja", "monk", "忍宗", "Way of the Ninja"),
            new CharacterIdentity("barbarian", "barbarian_path_of_the_warden", "barbarian", "守望者道途", "Path of the Warden"),
            new CharacterIdentity("berserker", "barbarian_path_of_the_berserker", "barbarian", "狂战士道途", "Path of the Berserker"),
            new CharacterIdentity("giant", "barbarian_path_of_the_giant", "barbarian", "巨人道途", "Path of the Giant"),
            new CharacterIdentity("werewolf", "barbarian_path_of_the_beast", "barbarian", "野兽道途", "Path of the Beast"),
            new CharacterIdentity("gambler", "rogue_gambler", "rogue", "赌徒", "Gambler"),
            new CharacterIdentity("wanderer", "fighter_tactician", "fighter", "战术家", "Tactician"),
            new CharacterIdentity("divination", "wizard_school_of_divination", "wizard", "预言学派", "School of Divination"),
            new CharacterIdentity("necromancy", "wizard_school_of_necromancy", "wizard", "死灵学派", "School of Necromancy"),
            new CharacterIdentity("abjuration", "wizard_school_of_abjuration", "wizard", "防护学派", "School of Abjuration"),
            new CharacterIdentity("light", "cleric_light_domain", "cleric", "光明领域", "Light Domain"),
            new CharacterIdentity("war", "cleric_war_domain", "cleric", "战争领域", "War Domain"),
            new CharacterIdentity("battle_master", "fighter_battle_master", "fighter", "战斗大师", "Battle Master"),
            new CharacterIdentity("draconic", "sorcerer_draconic_bloodline", "sorcerer", "龙族血脉", "Draconic Bloodline"),
            new CharacterIdentity("hunter_marksman", "ranger_hunter", "ranger", "猎人", "Hunter"),
            new CharacterIdentity("open_hand", "monk_way_of_the_open_hand", "monk", "散打宗", "Way of the Open Hand"),
            new CharacterIdentity("four_elements", "monk_way_of_the_four_elements", "monk", "四象宗", "Way of the Four Elements"),
            new CharacterIdentity("moon", "druid_circle_of_the_moon", "druid", "月亮结社", "Circle of the Moon"),
            new CharacterIdentity("stars", "druid_circle_of_the_stars", "druid", "星辰结社", "Circle of the Stars"),
            new CharacterIdentity("land", "druid_circle_of_the_land", "druid", "大地结社", "Circle of the Land"),
            new CharacterIdentity("ancients", "paladin_oath_of_the_ancients", "paladin", "古贤之誓", "Oath of the Ancients"),
            new CharacterIdentity("fiend", "warlock_the_fiend", "warlock", "妖魔", "The Fiend"),
        };
        private static readonly Dictionary<string, CharacterIdentity> Index = BuildIndex();
        private static Dictionary<string, CharacterIdentity> BuildIndex()
        {
            var index = new Dictionary<string, CharacterIdentity>();
            foreach (var role in All) { index.Add(role.Id, role); index.Add(role.LegacyId, role); }
            index.Add("priest", index["cleric"]);
            return index;
        }
        public static CharacterIdentity Find(string id)
            => id != null && Index.TryGetValue(id, out var role) ? role : null;
        public static string NormalizeId(string id) => Find(id)?.Id ?? id;
    }
}
