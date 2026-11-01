using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed class CampaignNpcSpec
    {
        public string Name,Id;
        public int Maximum=28,Health=-1,Attack,Defense,Range=1,FreeAttack;
        public AttackDamageType Type=AttackDamageType.Slashing;
        public bool Shield,Aid,Phantom,Rally,Muster,Steady,Force;
        public CharacterDefinition Role;
        public CampaignNpcSpec(string name,string id="campaign_drillmaster",int health=28){Name=name;Id=id;Maximum=health;}
    }
    public sealed class CampaignSpec
    {
        public int Level,First,Guardian=1;
        public CharacterDefinition Role;
        public int[] Hands;
        public CampaignNpcSpec[] Npcs;
        public Action<CampaignLesson> Opening;
        public readonly List<CampaignStep> Steps=new List<CampaignStep>();
        public CampaignSpec(int level,CharacterDefinition role,int[] hands)
        {Level=level;Role=role;Hands=hands;Npcs=new CampaignNpcSpec[hands.Length/2];for(int i=1;i<Npcs.Length;i++)Npcs[i]=new CampaignNpcSpec(i==3?"同袍":level<11?"哨站教官":level<21?"奥法护卫"+(i==1?"甲":"乙"):"残旗教官"+(i==1?"甲":"乙"),level<11?"campaign_drillmaster":level<21?"campaign_arcane_tuner":"campaign_banner_warden",i==3?20:28);if(Npcs.Length==4)Npcs[3].Aid=true;}
        public CampaignStep Op(int actor,int source,int owner,int addend,string text,int victim=1,int? result=null)
        {var s=new CampaignStep{Id="L"+Level+"-"+(Steps.Count+1),Text=text,Actor=actor,Source=source,AddendOwner=owner,Addend=addend,Victim=victim,Result=result,DeclineNecromancy=true};Steps.Add(s);return s;}
        public CampaignStep Wait(int actor,string text)
        {var s=Op(actor,0,0,0,text);s.Kind=CampaignActionKind.Observe;return s;}
    }
    public static partial class CampaignContent
    {
        public static CampaignSpec Build(int level)
        {
            if(level<1||level>30)throw new ArgumentOutOfRangeException(nameof(level));
            return level<=10?ChapterOne(level):level<=20?ChapterTwo(level):ChapterThree(level);
        }
        public static string NpcEmblem(string id)
        {
            switch(id){case "campaign_drillmaster":return "Adventure/npc_outpost_drillmaster";case "campaign_arcane_tuner":return "Adventure/npc_arcane_guard";case "campaign_banner_warden":return "Adventure/knight_original";case "campaign_banner_crossbowman":return "Subclasses/fighter_arcane_archer";default:return null;}
        }
        public static string FinaleDescription(int chapter)=>chapter==1?"生命40\n攻+2 防+1 距2\n挥砍\n架盾／一次整军回复6":chapter==2?"生命40\n攻+1 防0 距∞\n强酸\n永久火／寒抗性、毒免\n汲能回路3次伤人后回复3":"指挥官36：+1/+1 距2\n一次集结令，己方回复4\n弩手28：+1/0 距2\n穿刺／稳弦\n同袍：古贤20生命";
        internal static void Shield(PlayerState p,int side=0){p.GetHand(side).SetShieldState(ShieldState.Formed);}
        internal static string TrainingSkills(int level,int seat)
        {
            if(level==5||level%10==0)return "";
            string text="演练调律：行动前可调整自己可用且无完整盾的手值。";
            if(seat!=1)return text+(level==16&&seat==2?"符文换装：切换武装的伤害类型。":"");
            if(level==9)text+="训练整备：净化自身负面并回复16，限一次。";
            if(level==12)text+="训练整备：净化自身负面并回复16，限四次。注水铭刻：使自身潮湿2回合。";
            if(level==11||level==12||level==14||level==16)text+="符文换装：切换武装的伤害类型。";
            if(level==14)text+="稳息术：清除你的燃烧并回复8，限两次。试验铭刻：施加一次伤害类型的易伤、抗性或免疫。";
            if(level==19)text+="试验铭刻：使自身获得一次钝击易伤。";
            return text;
        }
    }
    public sealed partial class CampaignLesson
    {
        public static CampaignLesson Create(int level,CharacterDefinition chosen=null)
        {
            var spec=CampaignContent.Build(level);var role=chosen??spec.Role;
            if(level%10==0&&role.Profession?.Id!=(level==10?"fighter":level==20?"wizard":"paladin"))throw new ArgumentException("终战职业不符");
            var seats=new PlayerState[spec.Hands.Length/2];var providers=new IChoiceProvider[seats.Length];
            for(int i=0;i<seats.Length;i++)
            {
                providers[i]=new SimpleChoiceProvider();
                if(i==0)seats[i]=CharacterFactory.CreatePlayer(role.DisplayName,role);
                else
                {
                    var n=spec.Npcs[i];
                    if(n.Role!=null)seats[i]=CharacterFactory.CreatePlayer(n.Name,n.Role);
                    else
                    {
                        var npc=new CampaignNpcLogic(n.Name,n.Attack,n.Defense,n.Range,n.Type){FreeAttackDelta=n.FreeAttack,CanRaiseShield=n.Shield,CanAid=n.Aid,Phantom=n.Phantom,CanRally=n.Rally,CanMuster=n.Muster,Steady=n.Steady,PeriodicForce=n.Force};
                        npc.TrainingNotes=CampaignContent.TrainingSkills(level,i);
                        seats[i]=new PlayerState(n.Name,npc,n.Maximum);
                        seats[i].BindDefinition(new CharacterDefinition(n.Id,n.Name,SpecialLogicType.None,n.Attack,n.Defense,n.Maximum,AttackDamageTypes.NameOf(n.Type)+"伤害。"+npc.AbilitySummary,n.Name));
                    }
                    seats[i].DisplayNameOverride=n.Name;
                }
            }
            var game=seats.Length==8?GameManager.CreateEight(seats,providers,TatteredBannersChapter.EightTeams):seats.Length==4?GameManager.Create2v2(seats,providers):new GameManager(seats[0],seats[1],providers[0],providers[1]);
            for(int i=0;i<seats.Length;i++){seats[i].LeftHand.SetValue(spec.Hands[i*2]);seats[i].RightHand.SetValue(spec.Hands[i*2+1]);if(i>0){seats[i].SetMaxHealth(spec.Npcs[i].Maximum);seats[i].SetHealthExact(spec.Npcs[i].Health<0?spec.Npcs[i].Maximum:spec.Npcs[i].Health);}}
            var board=new BoardController(game,(IPlayerChoiceSink)providers[0],(IPlayerChoiceSink)providers[1]);
            var lesson=new CampaignLesson(level,seats,game,board,spec.Steps,spec.Guardian);spec.Opening?.Invoke(lesson);board.SetFirstPlayer(seats[spec.First]);
            game.TakeSkillLog();return lesson;
        }
    }
}
