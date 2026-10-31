using System.Collections.Generic;
namespace FingerDiceDuel.Core
{
    public enum SecondarySkillStyle { Arrows, Book, Cards, Charm, Circuit, Cleanse, Dice, Dragon, Drain, Drunk, ElementFist, ElementGuard, Enchant, Eye, FalseLife, Growth, Hand, Heal, Ki, Maneuver, Measure, Music, Nightmare, Rage, Repair, Steal, Strike, Sun, Surge, TaichiBoost, Tether, Wind, Convert }
    public sealed class SecondarySkillProfile
    {
        public string Skill {get;} public SecondarySkillStyle Style {get;} public string Sprite {get;} public int Priority {get;} public string Flavor {get;}
        public SecondarySkillProfile(string skill,SecondarySkillStyle style,string sprite,int priority,string flavor)
        {Skill=skill;Style=style;Sprite=sprite;Priority=priority;Flavor=flavor;}
    }
    // Explicit semantic registry. No parsing of combat-log prose, no visual random draws.
    public static class SecondarySkillAnimation
    {
        private static readonly Dictionary<string,SecondarySkillProfile> _profiles=Build();
        public static IEnumerable<SecondarySkillProfile> Profiles=>_profiles.Values;
        public static SecondarySkillProfile Find(string skill)=>skill!=null&&_profiles.TryGetValue(skill,out var profile)?profile:null;
        private static Dictionary<string,SecondarySkillProfile> Build()
        {
            var list=new Dictionary<string,SecondarySkillProfile>();
            list["精通重击"]=new SecondarySkillProfile("精通重击",SecondarySkillStyle.Strike,"weapon_sword",3,"critical");
            list["无畏冲锋"]=new SecondarySkillProfile("无畏冲锋",SecondarySkillStyle.Strike,"weapon_sword",2,"charge");
            list["盾阵重整"]=new SecondarySkillProfile("盾阵重整",SecondarySkillStyle.Repair,"Overlay/shield",2,"");
            list["奥术射击·魔能齐射"]=new SecondarySkillProfile("奥术射击·魔能齐射",SecondarySkillStyle.Arrows,"projectile_arrow",2,"volley");
            list["奥术射击·强力射击"]=new SecondarySkillProfile("奥术射击·强力射击",SecondarySkillStyle.Arrows,"projectile_arrow",2,"power");
            list["奥术射击·穿透箭"]=new SecondarySkillProfile("奥术射击·穿透箭",SecondarySkillStyle.Arrows,"projectile_arrow",2,"piercing");
            list["神圣誓忠"]=new SecondarySkillProfile("神圣誓忠",SecondarySkillStyle.Growth,"Professions/paladin",3,"crown");
            list["生命门徒"]=new SecondarySkillProfile("生命门徒",SecondarySkillStyle.Heal,"Professions/cleric",2,"");
            list["维持生命"]=new SecondarySkillProfile("维持生命",SecondarySkillStyle.Heal,"Professions/cleric",2,"");
            list["净化"]=new SecondarySkillProfile("净化",SecondarySkillStyle.Cleanse,"Professions/cleric",2,"");
            list["思维之盾"]=new SecondarySkillProfile("思维之盾",SecondarySkillStyle.Eye,"Subclasses/warlock_the_great_old_one",2,"");
            list["法术塑形"]=new SecondarySkillProfile("法术塑形",SecondarySkillStyle.Enchant,"fx_force",2,"orb");
            list["狂野魔法浪涌"]=new SecondarySkillProfile("狂野魔法浪涌",SecondarySkillStyle.Surge,"fx_psychic",2,"");
            list["战斗激励"]=new SecondarySkillProfile("战斗激励",SecondarySkillStyle.Music,"Professions/bard",2,"");
            list["快手"]=new SecondarySkillProfile("快手",SecondarySkillStyle.Steal,"Hands/hand_right_2",2,"");
            list["死亡突袭"]=new SecondarySkillProfile("死亡突袭",SecondarySkillStyle.Strike,"weapon_sword",2,"shadow");
            list["法师之手"]=new SecondarySkillProfile("法师之手",SecondarySkillStyle.Hand,"spectral_hand",2,"");
            list["魔术攻击"]=new SecondarySkillProfile("魔术攻击",SecondarySkillStyle.Cards,"Subclasses/rogue_magician",2,"");
            list["太极八卦"]=new SecondarySkillProfile("太极八卦",SecondarySkillStyle.TaichiBoost,"Overlay/taichi",2,"");
            list["借力打力"]=new SecondarySkillProfile("借力打力",SecondarySkillStyle.TaichiBoost,"Overlay/taichi",2,"");
            list["阴阳流转"]=new SecondarySkillProfile("阴阳流转",SecondarySkillStyle.TaichiBoost,"Overlay/taichi",2,"");
            list["守望束缚"]=new SecondarySkillProfile("守望束缚",SecondarySkillStyle.Tether,"Overlay/locked",2,"chain");
            list["狂怒"]=new SecondarySkillProfile("狂怒",SecondarySkillStyle.Rage,"fx_fire",2,"");
            list["狂乱"]=new SecondarySkillProfile("狂乱",SecondarySkillStyle.Rage,"fx_impact",2,"frenzy");
            list["嗜血"]=new SecondarySkillProfile("嗜血",SecondarySkillStyle.Drain,"fx_necrotic",2,"");
            list["巨人之力"]=new SecondarySkillProfile("巨人之力",SecondarySkillStyle.Strike,"weapon_fist",3,"giant");
            list["巨人恢复"]=new SecondarySkillProfile("巨人恢复",SecondarySkillStyle.Heal,"Professions/barbarian",3,"");
            list["野兽形态"]=new SecondarySkillProfile("野兽形态",SecondarySkillStyle.Growth,"Subclasses/barbarian_path_of_the_beast",3,"claw");
            list["命运赌博"]=new SecondarySkillProfile("命运赌博",SecondarySkillStyle.Dice,"fate_die",2,"");
            list["赌徒之运"]=new SecondarySkillProfile("赌徒之运",SecondarySkillStyle.Dice,"fate_die",2,"forced");
            list["战术优势"]=new SecondarySkillProfile("战术优势",SecondarySkillStyle.Measure,"Professions/fighter",3,"");
            list["黎明曙光"]=new SecondarySkillProfile("黎明曙光",SecondarySkillStyle.Sun,"fx_radiant",2,"dawn");
            list["守御闪光"]=new SecondarySkillProfile("守御闪光",SecondarySkillStyle.Sun,"Status/holy_light",2,"brand");
            list["卓越战技"]=new SecondarySkillProfile("卓越战技",SecondarySkillStyle.Maneuver,"weapon_sword",2,"");
            list["龙族先祖"]=new SecondarySkillProfile("龙族先祖",SecondarySkillStyle.Dragon,"ancestral_dragon",2,"");
            list["龙族体魄"]=new SecondarySkillProfile("龙族体魄",SecondarySkillStyle.Surge,"fx_fire",2,"dragon");
            list["四象门徒"]=new SecondarySkillProfile("四象门徒",SecondarySkillStyle.ElementFist,"weapon_fist",2,"");
            list["四象法门"]=new SecondarySkillProfile("四象法门",SecondarySkillStyle.ElementGuard,"Effects/ki",2,"");
            list["大地束缚"]=new SecondarySkillProfile("大地束缚",SecondarySkillStyle.Tether,"Status/restrained",2,"vine");
            list["大地行者"]=new SecondarySkillProfile("大地行者",SecondarySkillStyle.Tether,"Status/restrained",2,"roots");
            list["自然之怒"]=new SecondarySkillProfile("自然之怒",SecondarySkillStyle.Tether,"Status/restrained",2,"vine");
            list["不灭哨卫"]=new SecondarySkillProfile("不灭哨卫",SecondarySkillStyle.Heal,"Professions/druid",2,"");
            list["黑暗赐福"]=new SecondarySkillProfile("黑暗赐福",SecondarySkillStyle.FalseLife,"Effects/false_life",2,"");
            list["直坠噩梦"]=new SecondarySkillProfile("直坠噩梦",SecondarySkillStyle.Nightmare,"Effects/false_life",2,"");
            list["黑暗强运"]=new SecondarySkillProfile("黑暗强运",SecondarySkillStyle.Drain,"Effects/false_life",2,"");
            list["逸闻记录"]=new SecondarySkillProfile("逸闻记录",SecondarySkillStyle.Book,"lore_book",2,"record");
            list["切割语"]=new SecondarySkillProfile("切割语",SecondarySkillStyle.Book,"lore_book",2,"consume");
            list["无与伦比技艺"]=new SecondarySkillProfile("无与伦比技艺",SecondarySkillStyle.Book,"lore_book",2,"growth");
            list["风暴之护"]=new SecondarySkillProfile("风暴之护",SecondarySkillStyle.ElementGuard,"fx_thunder",2,"");
            list["暴风狂怒"]=new SecondarySkillProfile("暴风狂怒",SecondarySkillStyle.Surge,"fx_lightning",2,"");
            list["风暴导向"]=new SecondarySkillProfile("风暴导向",SecondarySkillStyle.Wind,"fx_slash",2,"");
            list["风暴之心"]=new SecondarySkillProfile("风暴之心",SecondarySkillStyle.Wind,"fx_slash",2,"");
            list["奥法武器"]=new SecondarySkillProfile("奥法武器",SecondarySkillStyle.Enchant,"weapon_sword",2,"blade");
            list["法术回响"]=new SecondarySkillProfile("法术回响",SecondarySkillStyle.Enchant,"weapon_sword",2,"echo");
            list["征服威压"]=new SecondarySkillProfile("征服威压",SecondarySkillStyle.Growth,"Professions/paladin",2,"conquest");
            list["恐惧光环"]=new SecondarySkillProfile("恐惧光环",SecondarySkillStyle.Nightmare,"Status/panic",2,"");
            list["催眠凝视"]=new SecondarySkillProfile("催眠凝视",SecondarySkillStyle.Charm,"Status/charm",2,"");
            list["散打技巧"]=new SecondarySkillProfile("散打技巧",SecondarySkillStyle.Maneuver,"weapon_fist",2,"");
            list["混元体"]=new SecondarySkillProfile("混元体",SecondarySkillStyle.Heal,"Effects/ki",2,"");
            list["醉拳技巧"]=new SecondarySkillProfile("醉拳技巧",SecondarySkillStyle.Drunk,"Effects/drunken_fist",2,"");
            list["酣醉若狂"]=new SecondarySkillProfile("酣醉若狂",SecondarySkillStyle.Drunk,"Effects/drunken_fist",2,"upkeep");
            list["气"]=new SecondarySkillProfile("气",SecondarySkillStyle.Ki,"Effects/ki",2,"");
            list["汲能回路"]=new SecondarySkillProfile("汲能回路",SecondarySkillStyle.Circuit,"Adventure/npc_mage_phantom",3,"");
            list["战争祝福"]=new SecondarySkillProfile("战争祝福",SecondarySkillStyle.Convert,"Overlay/attack",2,"nine");
            return list;
        }
    }
}
