using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public static partial class CampaignContent
    {
        private static CampaignSpec ChapterThree(int n)
        {
            var role=n<=22?CharacterDefinition.OathOfTheCrown:n<=24?CharacterDefinition.OathOfDevotion:n==25?CharacterDefinition.OathOfTheAncients:n==26?CharacterDefinition.Oathbreaker:n==27?CharacterDefinition.OathOfConquest:n==28?CharacterDefinition.OathOfVengeance:CharacterDefinition.OathOfDevotion;
            var s=new CampaignSpec(n,role,new[]{1,2,3,5,2,4,5,7});s.Npcs[2].Maximum=24;s.Npcs[1].Shield=true;
            switch(n)
            {
                case 21:
                    s.Op(0,0,3,0,"左1借同袍5＝6，再选甲，7伤；同袍仍5。");
                    s.Op(0,1,2,0,"右2借远处乙2＝4，再选邻近甲，5伤；乙不掉血。");break;
                case 22:
                    s.Hands=new[]{1,2,3,4,3,4,3,1};s.Npcs[2].Maximum=26;
                    s.Op(0,0,3,0,"1＋同袍3＝4，远处乙不可打，选择邻近甲。");
                    s.Op(0,0,3,1,"4＋同袍1＝5，真实合盾，不重置为预置盾。");
                    s.Op(1,0,3,0,"甲3借同袍3＝6，玩家盾挡并碎，攻击与距离1→2。",0);
                    s.Op(0,0,2,0,"破盾5借远处乙3＝8，成长后的距离2可达，伤害10。",2);break;
                case 23:
                    s.Hands=new[]{1,2,2,3,2,4,3,1};s.Npcs[1].Maximum=30;
                    s.Op(0,0,3,0,"1＋同袍3＝4盾，只选择卫护斩人物目标，冲击甲2。");
                    s.Op(0,0,1,0,"完整4盾＋甲2＝6，旧盾破与新盾成分别冲击2，不追加普通攻击。");break;
                case 24:
                    s.Hands=new[]{1,2,3,2,2,4,3,1};s.First=1;s.Npcs[1].Maximum=30;s.Npcs[1].Range=2;s.Npcs[2].Maximum=26;s.Npcs[3].Role=CharacterDefinition.OathOfDevotion;s.Npcs[3].Health=12;s.Opening=l=>l.Player.SetHealthExact(12);
                    s.Op(1,0,0,0,"甲3＋玩家1＝4，攻击同袍；玩家无完整盾不能援护，同袍防+1，12→9。",3);
                    s.Op(3,0,0,0,"同一奉献同袍3＋玩家1＝4合盾，真实卫护斩只能打邻近乙。",2).ShockSeat=2;
                    s.Op(0,0,1,0,"玩家1＋甲4＝5备盾，卫护斩甲2。");
                    s.Op(1,0,0,1,"甲4＋玩家2＝6攻击同袍；发动守护之誓，自己的5盾格挡破碎，再卫护斩甲。",3).Redirect=true;
                    s.Op(3,1,2,1,"同袍1＋乙4＝5再合盾，原4盾仍保留。",2).ShockSeat=2;
                    s.Op(0,0,2,0,"自己的破盾5＋乙2＝7，保留无完整盾，等待同袍援护。");
                    s.Op(1,1,0,1,"甲2＋玩家2＝4；同袍AI真实代挡、自己碎盾再冲击邻近乙，不能冲击玩家。",0);break;
                case 25:
                    s.Hands=new[]{5,2,1,1,2,3,2,2};s.Npcs[1].Maximum=30;s.Npcs[2].Maximum=26;s.Npcs[3].Health=10;s.Opening=l=>{l.Player.SetHealthExact(13);Shield(l.Player);};
                    s.Op(0,0,3,0,"完整5借同袍2＝7，破盾自然之怒选择甲，束缚2；7不造成伤害。").TargetSeat=1;
                    s.Op(1,2,3,1,"甲真实随机一只手不可行动，只用另一只手借2，不是完全冻结。",0);
                    s.Wait(0,"观摩等待束缚计时，保留古贤13体力。");
                    s.Op(1,2,3,1,"再经过甲一个自身回合，束缚自然到期。",0);
                    s.Op(1,0,3,0,"甲调律后算4攻击玩家，3伤，13→10；不灭哨卫选择同袍10→12。",0,4).TargetSeat=3;
                    s.Wait(0,"观摩保留10生命，不能提前自己算9回复。");
                    s.Op(1,0,3,0,"再次4攻击造成3，10→7，再自愈2→9；不额外回复同袍。",0,4);break;
                case 26:
                    s.Hands=new[]{1,2,2,3,2,3,4,5};s.Npcs[1].Maximum=30;s.Npcs[2].Maximum=28;s.Opening=l=>Shield(l.Seats[3],1);
                    s.Op(0,0,3,0,"1借同袍4＝5，恐惧打击攻击甲7；手上5没有合盾。");
                    var ra=s.Wait(1,"甲用正常行动架盾，消耗行动并显示冷却。");ra.Kind=CampaignActionKind.RaiseShield;
                    var rb=s.Wait(2,"乙也用正常行动架盾；同袍原完整盾一直保留。");rb.Kind=CampaignActionKind.RaiseShield;
                    s.Op(0,1,3,1,"右2借同袍盾5＝7，保留左5；借来的盾不破。");
                    s.Op(1,0,3,0,"甲2借同袍4＝6打玩家，憎恨灵光转给乙；乙盾挡破，自己与同袍不掉血。",0).TargetSeat=2;break;
                case 27:ConquestCourse(s);break;
                case 28:VengeanceCourse(s);break;
                case 29:EightCourse(s);break;
                case 30:
                    s.Role=CharacterDefinition.OathOfDevotion;s.Hands=new[]{1,1,1,1,1,1,1,1};
                    s.Npcs[1]=new CampaignNpcSpec("残旗指挥官",TatteredBannersChapter.Commander.Id,36){Attack=1,Defense=1,Range=2,Muster=true};
                    s.Npcs[2]=new CampaignNpcSpec("阵前弩手","campaign_banner_crossbowman",28){Attack=1,Range=2,Type=AttackDamageType.Piercing,Steady=true};
                    s.Npcs[3].Role=CharacterDefinition.OathOfTheAncients;s.Npcs[3].Aid=false;break;
            }
            return s;
        }
        private static void ConquestCourse(CampaignSpec s)
        {
            s.Hands=new[]{1,5,3,2,3,2,3,1};s.Npcs[1].Maximum=32;s.Npcs[2].Maximum=28;
            s.Op(0,0,1,0,"1＋3＝4，开场攻击都是0不恐慌，攻击结束后才成长到1。");
            s.Op(1,0,3,1,"甲3＋同袍1＝4打你，20→16，旧防0结算后防成长1。",0);
            s.Op(0,0,1,1,"4＋2＝6，用已有攻击1造成7；比较本次开始数值，甲恐慌，攻击再成长2。");
            s.Wait(1,"甲恐慌不能打施加者，正常休整观察。");
            s.Op(2,0,3,1,"乙3＋同袍1＝4，旧防1只受3；结束后防2，乙恐慌。",0);
            s.Op(0,0,3,1,"6＋同袍1＝7，未进攻但保留5：攻2→1，防2不减。");
            s.Op(0,1,1,1,"右5＋甲右2＝7，无5且没攻击：攻1→0，防2→1。");
            s.Op(0,0,1,1,"左7＋甲右2＝9，真实回复；防1→0，没有预置或清空成长。");
        }
        private static void VengeanceCourse(CampaignSpec s)
        {
            s.Hands=new[]{1,2,3,3,2,2,1,2};s.Guardian=2;s.Npcs[1].Maximum=30;s.Npcs[1].Health=12;s.Npcs[1].Range=2;s.Npcs[2].Maximum=32;s.Npcs[2].Range=2;s.Npcs[3].Range=2;
            s.Op(0,0,1,0,"初始仇敌选甲，1＋3＝4，对标记者伤害5，甲12→7。");
            s.Wait(1,"甲公开调律右手8，左3暂不变，作为可借加数。").Prepare=(l,p)=>l.Tune(p,p.RightHand,8);
            s.Op(0,1,1,0,"右2＋甲左3＝5，真实合盾。");
            s.Wait(1,"甲在自己的回合将左手调2，等待下一次普通攻击。").Prepare=(l,p)=>l.Tune(p,p.LeftHand,2);
            var incoming=s.Op(2,0,3,1,"乙2＋同袍2＝4攻击你，改标乙；盾碎触发复仇之魂，用右破盾5＋同袍1＝6，只有数字变化。",0);incoming.Option=1;incoming.VowSeat=2;
            var unmarked=s.Op(0,1,1,1,"右6＋甲右8＝4，攻击未标记甲只3伤，甲7→4。");unmarked.VowSeat=2;
            var back=s.Op(1,0,0,0,"甲2＋你的4＝6攻击你；改标回甲，实际受6，20→14。",0);back.Option=1;back.VowSeat=1;
            s.Wait(2,"乙公开调左3，供同袍普通攻击借数。").Prepare=(l,p)=>l.Tune(p,p.LeftHand,3);
            var kill=s.Op(3,0,2,0,"同袍1＋乙3＝4，距2真实击杀甲；仇敌死亡后改标乙，尸体不复活。");kill.VowSeat=2;
            var last=s.Op(0,0,3,1,"甲死亡后环收缩；左4＋同袍2＝6，对新仇敌乙7伤，再自主打赢余敌。",2);last.VowSeat=2;
        }
        private static void EightCourse(CampaignSpec s)
        {
            s.Hands=new[]{2,1,7,2,3,2,2,1,3,2,1,2,1,2,4,1};s.Npcs=new CampaignNpcSpec[8];s.Guardian=4;
            for(int i=1;i<8;i++)s.Npcs[i]=new CampaignNpcSpec(TatteredBannersChapter.EightNames[i],i==4?"campaign_banner_crossbowman":"campaign_banner_warden",i==3||i==7?20:24){Aid=i==3||i==7,Type=i==4?AttackDamageType.Piercing:AttackDamageType.Bludgeoning};
            s.Npcs[1].Maximum=30;s.Npcs[1].Health=10;s.Npcs[1].Range=2;s.Npcs[3].Health=s.Npcs[7].Health=12;s.Npcs[4].Steady=true;s.Npcs[6].Shield=true;
            s.Op(0,0,3,0,"全程奉献之誓。2借远处同袍2＝4盾，卫护斩邻近前锋10→8。");
            s.Op(1,1,7,0,"前锋2＋席8同袍4＝6打同袍；玩家近距援护，以自己盾挡破，再冲击前锋8→6。",7).Redirect=true;
            s.Op(0,1,2,0,"右1＋席3斥候3＝4真实补盾，卫护斩前锋6→4。");
            s.Op(4,0,3,1,"远处弩手3＋席4同袍1＝4攻击同袍。玩家确实有盾，但距离3，不能援护。",3);
            s.Op(0,0,7,0,"自己的破盾4＋席8同袍4＝8，普通攻击击杀前锋；不是冲击击杀。");
            s.Op(0,0,3,1,"中立还存活2人，奖励两次额外行动。一：8＋同袍1＝9，正常回复。");
            s.Op(0,0,4,1,"额外行动二：9＋弩手右2＝1。之后接续席3，队友2／对手4，前锋不复活。");
        }
    }
}
