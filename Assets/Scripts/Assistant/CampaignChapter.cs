using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    /// <summary>Stable campaign identities are independent of displayed chapter numbers.</summary>
    public sealed class CampaignChapter
    {
        public readonly int Number;
        public readonly string Id,Name,Guide,Avatar,Background,Profession;
        private CampaignChapter(int n,string id,string name,string guide,string avatar,string background,string profession)
        {Number=n;Id=id;Name=name;Guide=guide;Avatar=avatar;Background=background;Profession=profession;}
        public string Title=>new[]{"","第一章：","第二章：","第三章：","第四章："}[Number]+Name;
        public static readonly CampaignChapter Outpost=new CampaignChapter(1,"outpost","边境哨站","老兵",AdventureLevel1.GuideAvatarKey,"Backgrounds/border_outpost_chapter_01","fighter");
        public static readonly CampaignChapter Gallery=new CampaignChapter(2,"gallery","万象回廊","回廊铭文","Adventure/npc_elemental_echo","Backgrounds/gallery_of_myriad_forms_chapter_02","sorcerer");
        public static readonly CampaignChapter Tower=new CampaignChapter(3,"tower","法师高塔","秘法典籍","Adventure/guide_arcane_codex","Backgrounds/mage_tower_chapter_02","wizard");
        public static readonly CampaignChapter Banners=new CampaignChapter(4,"banners","残旗战场","残旗军士长","Subclasses/"+CharacterDefinition.OathOfTheCrownId,TatteredBannersChapter.Background,"paladin");
        public static readonly CampaignChapter[] All={Outpost,Gallery,Tower,Banners};
        public static CampaignChapter At(int number)
        {if(number<1||number>4)throw new ArgumentOutOfRangeException(nameof(number));return All[number-1];}
        public static CampaignChapter ForLevel(int level)
        {if(level<1||level>40)throw new ArgumentOutOfRangeException(nameof(level));return At((level-1)/10+1);}
        public static string LevelId(int level)=>ForLevel(level).Id+"."+((level-1)%10+1).ToString("00");
        public static string LevelKey(int level)=>"FingerDiceDuel.Adventure.V3.Level."+LevelId(level)+".Completed";
        public string CompletionKey=>"FingerDiceDuel.Adventure.V3.Chapter."+Id+".Completed";
        public IReadOnlyList<CharacterDefinition> FinaleRoster
        {
            get
            {
                var roles=new List<CharacterDefinition>();
                foreach(var role in CharacterDefinition.BuiltIn)if(role.Profession?.Id==Profession)roles.Add(role);
                return roles;
            }
        }
        public static int LegacyLevel(int level)
        {
            if(level<=10)return level;
            if(level<=20||level==29)return 0;
            if(level==21)return 11;
            if(level<=28)return level-9;
            return level-10;
        }
    }
}
