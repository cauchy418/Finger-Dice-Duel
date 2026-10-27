using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters;
#if FDD_UNITY_TESTS
using NUnit.Framework;
#endif
namespace FingerDiceDuel.Tests
{
    [TestFixture]
    public class ProfessionContractTests
    {
        // User-approved naming plus pre-import statistics plus the approved later Champion defense adjustment.
        private static readonly string[] Before = {
            "soldier|fighter_champion|战士·勇士|fighter|Champion|1|0|20|1",
            "fighter|fighter_breaker|战士·破阵者|fighter|Breaker|1|0|20|1",
            "arcane_archer|fighter_arcane_archer|战士·魔射手|fighter|Arcane Archer|0|-1|20|3",
            "knight|paladin_oath_of_the_crown|圣武士·王冠之誓|paladin|Oath of the Crown|1|-1|20|1",
            "paladin|paladin_oath_of_devotion|圣武士·奉献之誓|paladin|Oath of Devotion|0|1|20|1",
            "oathbreaker|paladin_oathbreaker|圣武士·破誓者|paladin|Oathbreaker|2|0|20|1",
            "cleric|cleric_life_domain|牧师·生命领域|cleric|Life Domain|-1|-1|20|2147483647",
            "curseweaver|wizard_school_of_conjuration|法师·咒法学派|wizard|School of Conjuration|-1|0|20|2147483647",
            "hexer|warlock_the_archfey|邪术师·至高妖精|warlock|The Archfey|0|-1|20|2147483647",
            "warlock|warlock_the_great_old_one|邪术师·旧日支配者|warlock|The Great Old One|0|-1|20|2147483647",
            "wizard|wizard_school_of_evocation|法师·塑能学派|wizard|School of Evocation|0|-1|20|1",
            "sorcerer|sorcerer_wild_magic|术士·狂野魔法|sorcerer|Wild Magic|0|-1|20|2147483647",
            "bard|bard_college_of_valor|吟游诗人·勇气学院|bard|College of Valor|0|-1|20|3",
            "ranger|rogue_swashbuckler|游荡者·风流剑客|rogue|Swashbuckler|0|-2|20|1",
            "hunter|ranger_gloom_stalker|游侠·幽域追踪者|ranger|Gloom Stalker|0|-1|20|1",
            "thief|rogue_thief|游荡者·盗贼|rogue|Thief|0|-1|20|1",
            "assassin|rogue_assassin|游荡者·刺客|rogue|Assassin|2|-1|20|1",
            "magician|rogue_magician|游荡者·魔术师|rogue|Magician|0|-1|20|2",
            "swordsman|fighter_swordsman|战士·剑士|fighter|Swordsman|2|-1|20|1",
            "monk|monk_way_of_the_tai_chi|武僧·太极宗|monk|Way of the Tai Chi|0|0|20|1",
            "ninja|monk_way_of_the_ninja|武僧·忍宗|monk|Way of the Ninja|1|-1|20|1",
            "barbarian|barbarian_path_of_the_warden|野蛮人·守望者道途|barbarian|Path of the Warden|0|1|20|1",
            "berserker|barbarian_path_of_the_berserker|野蛮人·狂战士道途|barbarian|Path of the Berserker|-1|-2|25|1",
            "giant|barbarian_path_of_the_giant|野蛮人·巨人道途|barbarian|Path of the Giant|2|2|25|1",
            "werewolf|barbarian_path_of_the_beast|野蛮人·野兽道途|barbarian|Path of the Beast|-2|-2|25|1",
            "gambler|rogue_gambler|游荡者·赌徒|rogue|Gambler|0|0|20|1",
            "wanderer|fighter_tactician|战士·战术家|fighter|Tactician|1|-1|20|1",
            "divination|wizard_school_of_divination|法师·预言学派|wizard|School of Divination|0|-1|20|2147483647",
            "necromancy|wizard_school_of_necromancy|法师·死灵学派|wizard|School of Necromancy|0|-1|20|2147483647",
            "abjuration|wizard_school_of_abjuration|法师·防护学派|wizard|School of Abjuration|0|-1|20|2147483647",
            "light|cleric_light_domain|牧师·光明领域|cleric|Light Domain|0|-1|20|2147483647",
            "war|cleric_war_domain|牧师·战争领域|cleric|War Domain|-1|-1|20|2147483647",
            "battle_master|fighter_battle_master|战士·战斗大师|fighter|Battle Master|0|0|20|2",
            "draconic|sorcerer_draconic_bloodline|术士·龙族血脉|sorcerer|Draconic Bloodline|0|-1|20|2147483647",
            "hunter_marksman|ranger_hunter|游侠·猎人|ranger|Hunter|0|-1|20|3",
            "open_hand|monk_way_of_the_open_hand|武僧·散打宗|monk|Way of the Open Hand|0|0|20|1",
            "four_elements|monk_way_of_the_four_elements|武僧·四象宗|monk|Way of the Four Elements|0|0|20|1",
            "moon|druid_circle_of_the_moon|德鲁伊·月亮结社|druid|Circle of the Moon|0|0|20|1",
            "stars|druid_circle_of_the_stars|德鲁伊·星辰结社|druid|Circle of the Stars|0|0|20|3",
            "land|druid_circle_of_the_land|德鲁伊·大地结社|druid|Circle of the Land|0|1|20|1",
            "ancients|paladin_oath_of_the_ancients|圣武士·古贤之誓|paladin|Oath of the Ancients|-1|1|20|1",
            "fiend|warlock_the_fiend|邪术师·妖魔|warlock|The Fiend|0|-1|20|2147483647",
            "lore|bard_college_of_lore|吟游诗人·逸闻学院|bard|College of Lore|0|-1|20|1",
            "storm|sorcerer_storm_sorcery|术士·风暴术法|sorcerer|Storm Sorcery|0|-1|20|2147483647",
            "illusion|wizard_school_of_illusion|法师·幻术学派|wizard|School of Illusion|0|-1|20|2147483647",
        };
        [Test]
        public void All_27_identities_keep_their_approved_stats_and_legacy_aliases()
        {
            Assert.AreEqual(54, CharacterDefinition.BuiltIn.Length);
            var ids = new HashSet<string>();
            foreach (string row in Before)
            {
                string[] v = row.Split('|');
                CharacterDefinition d = CharacterDefinition.Find(v[1]);
                Assert.IsTrue(d != null, v[1]);
                Assert.IsTrue(ids.Add(d.Id), d.Id);
                Assert.AreEqual(v[2], d.DisplayName);
                Assert.AreEqual(v[3], d.Profession.Id);
                Assert.AreEqual(v[4], d.SubclassNameEn);
                Assert.AreEqual(d, CharacterDefinition.Find(v[0]), "Legacy definition: " + v[0]);
                Assert.AreEqual(CharacterCodex.For(d), CharacterCodex.For(v[0]));
                Assert.AreEqual(v[4], CharacterCodex.For(d).EnglishName);
                PlayerState p = CharacterFactory.CreatePlayer(d.DisplayName, d);
                Assert.AreEqual(int.Parse(v[5]), p.Character.AttackBonus, d.Id);
                Assert.AreEqual(int.Parse(v[6]), p.Character.DefenseBonus, d.Id);
                Assert.AreEqual(int.Parse(v[7]), p.MaximumHealth, d.Id);
                Assert.AreEqual(int.Parse(v[8]), GameManager.BaseRangeOf(p), d.Id);
            }
            Assert.AreEqual(CharacterDefinition.LifeDomain, CharacterDefinition.Find("priest"));
        }
        [Test]
        public void Profession_and_alignment_filters_intersect_without_adding_druid_roles()
        {
            Assert.AreEqual(12, CharacterProfession.All.Count);
            string[] professions = { "fighter", "paladin", "cleric", "wizard", "warlock", "sorcerer", "bard", "rogue", "ranger", "monk", "barbarian", "druid" };
            // 2026-10-23 后：战士 6（+战斗大师）、牧师 3（+光明/战争领域）；德鲁伊仍预留。
            int[] counts = { 7, 6, 5, 9, 4, 3, 2, 5, 2, 4, 4, 3 };
            for (int i=0; i<professions.Length; i++)
            {
                Assert.AreEqual(counts[i], CharacterCodex.Filter(null,null,professions[i]).Count, professions[i]);
                foreach (var d in CharacterCodex.Filter("守序","善良",professions[i]))
                {
                    Assert.AreEqual(professions[i],d.Profession.Id);
                    Assert.AreEqual(Alignment.LawfulGood,CharacterCodex.For(d).Alignment);
                }
            }
            Assert.AreEqual(0,CharacterCodex.Filter(null,null,"unknown").Count);
        }
    }
}
