using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public static class TatteredBannersChapter
    {
        public const string CompletionKey="FingerDiceDuel.Adventure.Chapter3.Completed";
        public const string Background="Backgrounds/tattered_banners_chapter_03";
        public static readonly CharacterDefinition Guard=new CharacterDefinition("tattered_banners_guard","残旗教官",SpecialLogicType.None,description:"公开教学配置，攻防0，距1，无其他技能。");
        public static readonly CharacterDefinition Commander=new CharacterDefinition("tattered_banners_commander","残旗指挥官",SpecialLogicType.None,1,0,24,"攻+1／防0，距1，挥砍，无其他技能。");
        public static readonly CharacterDefinition Arbalest=new CharacterDefinition("tattered_banners_arbalest","阵前弩手",SpecialLogicType.None,0,0,20,"攻防0，距2，穿刺，无其他技能。");
        public static readonly int[] EightTeams={0,1,2,0,1,2,1,0};
        public static readonly string[] EightNames={"玩家","中立前锋","混乱斥候","守序同袍甲","中立弩手","混乱后卫","中立后卫","守序同袍乙"};
        public static IReadOnlyList<CharacterDefinition> FinaleRoster
        {
            get{var list=new List<CharacterDefinition>();foreach(var role in CharacterDefinition.BuiltIn)
                if(role.Profession?.Id=="paladin"&&CharacterFactory.IsRegistered(role.Logic))list.Add(role);return list;}
        }
        public static string SampleKey(int level,string key)=>"FingerDiceDuel.Adventure.Chapter3.Level"+level+".Sample."+key+".Completed";
        public static string Hint(int level)=>new[]{"借同袍／远敌的数字 · 目标另选","王冠之誓 · 破盾与距离","奉献之誓 · 合盾与两次冲击","奉献之誓 · 援护同袍","古贤之誓 · 束缚与回复","破誓者 · 攻击转移与友伤","征服之誓 · 成长／恐慌／衰减","复仇之誓 · 仇敌与纯运算","公开八人 · 关系／距离／奖惩","六圣武士自选 · 经典2v2"}[level-21];
        private static PlayerState P(GameManager game,int seat)=>game.Players[seat];
        private static void Shield(PlayerState p,int side=0){p.GetHand(side).SetShieldState(ShieldState.Formed);}
        private static void Hp(PlayerState p,int hp)=>p.SetHealthExact(hp);
        private static BannersSample Make(string key,string title,CharacterDefinition role,int[] hands,string intro)=>new BannersSample(key,title,role,hands,intro);

        public static IReadOnlyList<BannersSample> Samples(int level)
        {
            var list=new List<BannersSample>();
            switch(level)
            {
                case 21: Borrow(list);break;
                case 22: Crown(list);break;
                case 23: Devotion(list);break;
                case 24: Protection(list);break;
                case 25: Ancients(list);break;
                case 26: Oathbreaker(list);break;
                case 27: Conquest(list);break;
                case 28: Vengeance(list);break;
                case 29: Eight(list);break;
                case 30:list.Add(Make("finale","共举残旗",CharacterDefinition.OathOfDevotion,new[]{1,1,1,1,1,1,1,1},"真实经典2v2。"));break;
                default:throw new ArgumentOutOfRangeException(nameof(level));
            }
            return list;
        }
        private static void Borrow(List<BannersSample> list)
        {
            var s=Make("borrow","借来的数字",CharacterDefinition.OathOfTheCrown,new[]{1,2,3,5,2,4,5,7},
                "你在一位，同袍四位，教官甲乙二／三位。借同袍或远处敌人的数字，只改变自己的手；之后再选攻击目标。队友也可能被误伤。");
            s.Turn(0,0,3,0,1,"左1＋同袍左5＝6，再选择邻近教官甲攻击；借来的5不变。").RestOthers()
                .Turn(0,1,2,0,1,"右2＋远处教官乙左2＝4，再攻击教官甲。借数距离与攻击距离不同。");
            s.Verify=l=>l.Seats[1].Health==8&&l.Seats[2].Health==20&&l.Seats[3].LeftHand.Value==5&&l.Seats[2].LeftHand.Value==2;
            list.Add(s);
        }
        private static void Crown(List<BannersSample> list)
        {
            var a=Make("range","借得到，不等于打得到",CharacterDefinition.OathOfTheCrown,new[]{1,2,3,4,3,4,3,7},"借数通常不看攻击距离；你的距离1只能攻击相邻角色。选远处目标会被禁用，注意别误伤同袍。");
            a.Turn(0,0,3,0,1,"左1＋同袍左3＝4，查看距离后选择教官甲，造成5伤害。");a.Verify=l=>l.Seats[1].Health==15&&l.Seats[2].Health==20;list.Add(a);
            var b=Make("growth","盾碎之后，越过盾墙",CharacterDefinition.OathOfTheCrown,new[]{5,2,3,4,3,4,3,7},"开场左5为完整盾。自己的盾碎后攻击／距离永久+1；队友盾碎不成长。");
            b.First=1;b.Prepare=g=>Shield(P(g,0));
            b.Turn(1,0,3,0,0,"教官甲3＋同袍3＝6攻击你，盾挡住伤害，观察攻击／距离1→2。").Rest(2).Rest(3)
                .Turn(0,0,2,0,2,"已破盾左5＋远处教官乙左3＝8，距离2可攻击乙，造成10伤害。");
            b.Verify=l=>l.Player.Health==20&&l.Seats[2].Health==10&&((OathOfTheCrownLogic)l.Player.Character).BaseAttackRange==2&&l.Player.Character.AttackBonus==2;list.Add(b);
        }
        private static void Devotion(List<BannersSample> list)
        {
            var s=Make("echoes","盾锋与回响",CharacterDefinition.OathOfDevotion,new[]{1,2,2,3,2,4,3,1},"4／6改为同数字的完整盾，卫护斩冲击邻近角色固定2点。旧盾换新盾触发破盾与合盾两次冲击。");
            s.Turn(0,0,3,0,-1,"左1＋同袍左3＝4合盾，只选卫护斩的目标教官甲；无需再确认普通攻击。").RestOthers()
                .Turn(0,0,1,0,-1,"完整4盾＋教官甲左2＝6，分别为旧盾破碎、新盾形成选择教官甲，两次冲击。");
            s.Verify=l=>l.Player.LeftHand.Value==6&&l.Player.LeftHand.IsShield&&l.Seats[1].Health==14
                &&((OathOfDevotionLogic)l.Player.Character).ShieldFormCount==2;list.Add(s);
        }
        private static void Protection(List<BannersSample> list)
        {
            var a=Make("bodyguard","替同袍承受",CharacterDefinition.OathOfDevotion,new[]{1,2,3,2,2,4,3,1},"同袍12生命，教官甲为距离2弩手，先攻击同袍。保护范围看你到同袍的距离；代挡后用你自己的防御。");
            a.First=1;a.Ranges=new[]{1,2,1,1};a.Prepare=g=>Hp(P(g,3),12);
            a.Turn(1,0,0,0,3,"教官甲3＋你的左1＝4，选择守护之誓代挡：同袍不掉血，你20→17。");
            a.Verify=l=>l.Player.Health==17&&l.Seats[3].Health==12;list.Add(a);
            var b=Make("shieldguard","你的盾接住这一击",CharacterDefinition.OathOfDevotion,new[]{5,2,3,2,2,4,3,3},"重新布置：你的左5为完整盾，同袍12生命。你代挡后由自己的盾格挡，再选择自己的卫护斩目标。");
            b.First=1;b.Ranges=new[]{1,2,1,1};b.Prepare=g=>{Shield(P(g,0));Hp(P(g,3),12);};
            b.Turn(1,0,3,1,3,"教官甲3＋同袍右3＝6攻击同袍。代挡，以盾接住，再冲击距离1的教官甲。");
            b.Verify=l=>l.Player.Health==20&&l.Seats[3].Health==12&&l.Seats[1].Health==18&&!l.Player.LeftHand.IsShield;list.Add(b);
            var c=Make("ally_ai","同袍也会援护你",CharacterDefinition.OathOfDevotion,new[]{1,2,3,2,2,4,5,2},"重新布置：你6生命、无盾，同袍奉献之誓有完整5盾。让同袍AI援护你；同袍离教官甲2，冲击应选择邻近教官乙。");
            c.First=1;c.AllyRole=CharacterDefinition.OathOfDevotion;c.Prepare=g=>{Hp(P(g,0),6);Shield(P(g,3));};
            c.Turn(1,0,0,0,0,"观察同袍的守护之誓与卫护斩，不替AI回答；你仍6生命，同袍仍20，教官乙受到2点冲击。");
            c.Verify=l=>l.Player.Health==6&&l.Seats[3].Health==20&&l.Seats[2].Health==18&&!l.Seats[3].LeftHand.IsShield;list.Add(c);
        }
        private static void Ancients(List<BannersSample> list)
        {
            var a=Make("restrain","束缚仍有一手可动",CharacterDefinition.OathOfTheAncients,new[]{5,2,1,1,2,3,2,2},"左5预置完整盾。主动用盾运算触发自然之怒，给教官甲束缚2回合。每个自己的回合随机禁一手，另一只仍可行动。");
            a.Prepare=g=>Shield(P(g,0));
            a.Turn(0,0,3,0,-1,"左5＋同袍左2＝7，盾碎，选择教官甲附束缚。没有生命伤害，不触发回复。")
                .Turn(1,2,3,1,-1,"观察教官甲实际锁定哪只手，再用另一只借2，得到3或5；回合末束缚2→1。").Rest(2).Rest(3)
                .Turn(0,0,2,0,-1,"左7＋教官乙左2＝9，正常回复；等待第二次束缚行动。")
                .Turn(1,2,3,1,-1,"教官甲再次用合法手借2，得到3或5；束缚1→0，两回合均只锁一手。");
            a.Verify=l=>!l.Seats[1].HasStatus(StatusEffect.Restrained)&&l.Player.Health==20;list.Add(a);
            for(int i=0;i<2;i++)
            {
                int hp=i==0?13:12;var s=Make(i==0?"heal_ally":"heal_self",i==0?"先顾同袍":"低血自愈",CharacterDefinition.OathOfTheAncients,new[]{1,2,3,2,2,4,1,2},
                    "重新布置：你"+hp+"生命，同袍10生命。伤后至少10，选择范围内另一人回复2；低于10则自己回复2，阈值只检查一次。");
                s.First=1;s.Prepare=g=>{Hp(P(g,0),hp);Hp(P(g,3),10);};
                s.Turn(1,0,3,0,0,i==0?"教官甲3＋同袍1＝4，伤害3令你13→10，再选择同袍10→12。":"同样伤害3，你12→9，再自愈2至11；不再追加一次同袍回复。");
                s.Verify=l=>l.Player.Health==(hp==13?10:11)&&l.Seats[3].Health==(hp==13?12:10);list.Add(s);
            }
        }
        private static void Oathbreaker(List<BannersSample> list)
        {
            var a=Make("five_attack","5成为攻击",CharacterDefinition.Oathbreaker,new[]{1,2,3,2,2,4,4,1},"破誓者的5为基础5攻击，不合盾；攻+2，距离1。");
            a.Turn(0,0,3,0,1,"左1＋同袍左4＝5，选择教官甲，造成7伤害。手值5保留但不是盾。");
            a.Verify=l=>l.Seats[1].Health==13&&l.Player.LeftHand.Value==5&&!l.Player.LeftHand.IsShield;list.Add(a);
            var b=Make("transfer","借敌之盾",CharacterDefinition.Oathbreaker,new[]{5,2,2,5,5,1,5,1},"自己有普通5。受到攻击可转移给另一名持完整盾者，不限普通射程；不能选择自己或原攻击者，注意同袍也可能被误选。");
            b.First=1;b.Prepare=g=>{Shield(P(g,1),1);Shield(P(g,2));Shield(P(g,3));Hp(P(g,3),12);};
            b.Turn(1,0,0,1,0,"教官甲2＋你的右2＝4攻击你，发动憎恨灵光，选距离2的教官乙；乙以自己的盾格挡。");
            b.Verify=l=>l.Player.Health==20&&l.Seats[2].Health==20&&!l.Seats[2].LeftHand.IsShield&&l.Seats[3].Health==12&&l.Player.LeftHand.Value==5;list.Add(b);
        }
        private static void Conquest(List<BannersSample> list)
        {
            var a=Make("attack","先交锋，后成长",CharacterDefinition.OathOfConquest,new[]{1,5,3,2,2,4,3,1},"右5为普通数字。征服成长在整次攻击之后；恐慌比较攻击开始时的双方对应加成。首击不回头享受新加成。");
            a.Turn(0,0,1,0,1,"左1＋教官甲左3＝4，造成4伤害。开场攻0相同不恐慌，结算后攻击0→1。").RestOthers()
                .Turn(0,0,1,1,1,"左4＋教官甲右2＝6，用已有攻1造成7伤害；按开场1＞0附恐慌，再成长至攻2。");
            a.Verify=l=>l.Seats[1].Health==9&&l.Player.Character.AttackBonus==2&&l.Seats[1].HasStatus(StatusEffect.Panic);list.Add(a);
            var b=Make("defense","守势成长与消退",CharacterDefinition.OathOfConquest,new[]{5,2,2,3,2,4,2,1},"左5普通数字。受击使用旧防御，攻击结束后成长；自己的回合末不持5，积累防御−1，最低0。");
            b.First=1;
            b.Turn(1,0,0,1,0,"教官甲2＋你的右2＝4，你20→16，防御0→1。").Rest(2).Rest(3)
                .Turn(0,1,3,1,-1,"右2＋同袍右1＝3，保留左5，不攻击；已有防御1保持。")
                .Turn(1,0,3,0,0,"教官甲左4＋同袍左2＝6，你16→11，防御1→2；按开场防御1＞0令甲恐慌。").Rest(2).Rest(3)
                .Turn(0,0,3,0,-1,"左5＋同袍左2＝7，不再持5，回合末防御2→1。").RestOthers()
                .Turn(0,0,3,0,-1,"左7＋同袍左2＝9，回复至20，回合末防御1→0。");
            b.Verify=l=>l.Player.Health==20&&l.Player.Character.DefenseBonus==0;list.Add(b);
            var c=Make("decay","威势也会衰减",CharacterDefinition.OathOfConquest,new[]{1,5,3,2,2,4,2,2},"重新布置已积累攻击2的示例，手1／5。回合内未发起攻击，积累攻击−1，最低0；合盾不算攻击。");
            c.Prepare=g=>{
                var p=P(g,0);var enemy=P(g,1);new AttackContext(p,enemy,p.LeftHand,1,g).Resolve(g.ChoiceProviderB,g);new AttackContext(p,enemy,p.LeftHand,1,g).Resolve(g.ChoiceProviderB,g);
                enemy.SetHealthExact(20);enemy.ClearStatuses();};
            c.Turn(0,0,3,0,-1,"左1＋同袍左2＝3，无攻击，回合末攻击2→1。").RestOthers()
                .Turn(0,0,3,0,-1,"左3＋同袍左2＝5，形成盾但无攻击，攻击1→0。");
            c.Verify=l=>l.Player.Character.AttackBonus==0&&l.Player.LeftHand.IsShield;list.Add(c);
        }
        private static void Vengeance(List<BannersSample> list)
        {
            for(int i=0;i<2;i++)
            {
                bool marked=i==0;var s=Make(marked?"marked":"unmarked",marked?"攻击仇敌":"标记不延长射程",CharacterDefinition.OathOfVengeance,new[]{1,2,3,3,2,2,1,7},"首次自身回合选择仇敌。攻击仇敌每段+1，其他目标−1；远处标记不会扩大攻击范围。");
                s.Turn(0,0,1,0,1,marked?"先标记教官甲，再用左1＋甲左3＝4攻击甲，造成5伤害。":"先标记远处教官乙，再用左1＋甲左3＝4攻击近处甲，仅造成3伤害。");
                s.Verify=l=>l.Seats[1].Health==(marked?15:17);list.Add(s);
            }
            var b=Make("retarget","仇敌破盾与纯运算",CharacterDefinition.OathOfVengeance,new[]{5,2,1,3,2,2,1,1},"完整左5盾，先标记教官乙。来袭时转标教官甲；仇敌碎盾后可选纯运算，只变数字，不攻击或推进回合。");
            b.Prepare=g=>Shield(P(g,0));
            b.Turn(0,1,3,1,-1,"标记乙，再用右2＋同袍右1＝3，保留左盾。")
                .Turn(1,0,0,1,0,"教官甲1＋你的右3＝4，选择转移仇敌标记；盾挡住后发动复仇之魂，点击已破盾左5、同袍左1，变6但不攻击。");
            b.Verify=l=>l.Player.LeftHand.Value==6&&l.Player.RightHand.Value==3&&l.Player.Health==20&&l.Seats[1].Health==20
                &&ReferenceEquals(((IVowOfEnmity)l.Player.Character).VowTarget,l.Seats[1]);list.Add(b);
            var c=Make("death","同袍击杀与距离收缩",CharacterDefinition.OathOfVengeance,new[]{2,1,1,2,3,2,1,2},"教官甲4生命；同袍为距离2弩手。队友击杀仇敌也能重选；死亡移出距离环，原座位关系不变。");
            c.Ranges=new[]{1,1,1,2};c.Prepare=g=>Hp(P(g,1),4);
            c.Turn(0,1,3,0,-1,"先标记甲，右1＋同袍左1＝2，无攻击，等待同袍。").Rest(1).Rest(2)
                .Turn(3,0,2,0,1,"同袍左1＋教官乙左3＝4，跨距离击杀甲；你重新选择乙为仇敌。")
                .Turn(0,0,3,1,2,"甲死亡后你与原三位乙距离1，左2＋同袍右2＝4攻击新仇敌，造成5。");
            c.Verify=l=>!l.Seats[1].IsAlive&&l.Seats[2].Health==15&&l.Game.DistanceBetween(l.Player,l.Seats[2])==1;list.Add(c);
        }
        private static void Eight(List<BannersSample> list)
        {
            var a=Make("eight_borrow","八席中的同袍",CharacterDefinition.OathOfDevotion,new[]{2,1,1,2,1,2,2,2,1,2,1,2,1,2,1,2},"公开阵营3／3／2：你与原四、八位同队，队友2人、对手5人。借远处同袍的数，冲击仍只选邻近敌人。");
            a.Turn(0,0,3,0,-1,"左2＋远处四位同袍左2＝4合盾，卫护斩选二位中立前锋。观察其他七席依次休整。").RestOthers();
            a.Verify=l=>l.Seats[1].Health==18&&l.Seats[3].LeftHand.Value==2;list.Add(a);
            var b=Make("eight_near","近处可以援护",CharacterDefinition.OathOfDevotion,new[]{5,2,2,2,1,2,1,2,1,2,1,2,1,2,4,2},"你的完整5盾；八位同袍12生命。二位前锋距2可攻击八位；你到八位距1，可以援护。");
            b.First=1;b.Ranges=new[]{1,2,1,1,1,1,1,1};b.Prepare=g=>{Shield(P(g,0));Hp(P(g,7),12);};
            b.Turn(1,0,7,0,7,"前锋左2＋八位同袍左4＝6，发动援护以自己的盾挡，再冲击二位前锋。");
            b.Verify=l=>l.Player.Health==20&&l.Seats[7].Health==12&&l.Seats[1].Health==18;list.Add(b);
            var c=Make("eight_far","远处无法援护",CharacterDefinition.OathOfDevotion,new[]{5,2,1,2,1,2,1,2,3,2,1,2,1,2,1,2},"四位同袍12生命，离你距离3。你的盾不能跨距离援护；本次观察实际损失，不算失败。");
            c.First=4;c.Prepare=g=>{Shield(P(g,0));Hp(P(g,3),12);};
            c.Turn(4,0,3,0,3,"五位中立弩手3＋四位同袍1＝4，攻击同袍12→8；超出范围，不出现可发动的守护之誓。");
            c.Verify=l=>l.Seats[3].Health==8&&l.Player.LeftHand.IsShield;list.Add(c);
            var d=Make("rewards","普通击杀的额外行动",CharacterDefinition.OathOfTheCrown,new[]{1,2,7,2,1,2,1,2,1,2,1,2,1,2,1,2},"独立改用王冠之誓。二位中立前锋9生命，无盾；普通攻击击杀后，中立仍有五／七位两人，奖励两次行动，不推进正常轮次。");
            d.Prepare=g=>Hp(P(g,1),9);
            d.Turn(0,0,1,0,1,"左1＋前锋左7＝8，造成9击杀，观察两次额外行动提示。")
                .Turn(0,0,3,0,-1,"额外行动一：左8＋四位同袍左1＝9，满体力实际回复0。")
                .Turn(0,1,4,0,-1,"额外行动二：右2＋五位左1＝3，无攻击。结束后接原三位行动，死者移出距离环。");
            d.Verify=l=>!l.Seats[1].IsAlive&&l.Game.TurnCount==1&&l.Game.RoundCount==0&&!l.Board.IsExtraAction
                &&ReferenceEquals(l.Board.ActivePlayer,l.Seats[2])&&l.Game.DistanceBetween(l.Player,l.Seats[2])==1;list.Add(d);
        }
    }
}
