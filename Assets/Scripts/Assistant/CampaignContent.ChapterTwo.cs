using System;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public static partial class CampaignContent
    {
        private static CampaignStep Cast(CampaignSpec s,int side,int result,string text,bool abandon=false,DamageType? element=null)
        {var step=s.Op(0,side,1,0,text,1,result);step.Abandon=abandon;step.Element=element;return step;}
        private static CampaignSpec ChapterTwo(int n)
        {
            var s=new CampaignSpec(n,n==20?CharacterDefinition.SchoolOfEvocation:MageTowerChapter.PlayerRole(n),new[]{1,2,3,2});
            s.Npcs[1].Type=AttackDamageType.Slashing;
            switch(n)
            {
                case 11:
                    s.Hands=new[]{5,2,1,3};s.Npcs[1].Range=2;s.Npcs[1].Shield=true;s.Opening=l=>Shield(l.Player);
                    s.Op(0,0,1,0,"完整5＋1＝6，先选火焰再确定目标；附魔立即用于本击。").Option=(int)AttackDamageType.Fire;
                    s.Op(1,0,0,1,"护卫1＋2＝3；燃烧在它本回合末扣血。",0);
                    s.Op(1,0,0,1,"观摩，让护卫切为光耀并普通4攻击；受击后选记录光耀。",0,4).Prepare=(l,p)=>l.Equip(p,AttackDamageType.Radiant);
                    s.Steps[s.Steps.Count-1].Option=1;
                    s.Op(0,1,1,0,"算出6，使用刚记录的光耀攻击；目盲不是圣火。",1,6);
                    s.Wait(1,"护卫换装火焰，接下来试着应对他的火焰攻击。").Prepare=(l,p)=>l.Equip(p,AttackDamageType.Fire);break;
                case 12:EvocationCourse(s);break;
                case 13:
                    s=new CampaignSpec(n,CharacterDefinition.SchoolOfConjuration,new[]{3,2,1,5,2,8,6,4});s.Npcs[1].Maximum=32;s.Npcs[1].Defense=3;s.Npcs[2].Maximum=24;s.Opening=l=>Shield(l.Seats[1],1);
                    s.Op(0,0,1,0,"3＋1＝4，力场实际3伤，正防+3和完整盾仍在。");
                    s.Op(0,1,3,1,"2＋同袍4＝6，力场5伤，自己的双手自然变4／6。");
                    var castle=s.Wait(0,"王车易位：可先取消预览，再选同袍6／4换位；队友关系保持不变。");castle.Kind=CampaignActionKind.Castle;
                    s.Wait(1,"换位后到甲距离变2；本轮仍按旧名单行动。");s.Wait(2,"本轮继续按换位前的顺序行动。");s.Wait(3,"观察同袍保留自己的轮次，下轮按新座位顺序；留意新的攻击距离。");break;
                case 14:AbjurationCourse(s);break;
                case 15:
                    s.First=1;s.Npcs[1].Attack=1;
                    s.Op(1,0,0,0,"护卫先算4，实际伤你后，对它附加失能。",0,4).Enchant=StatusEffect.Disabled;
                    s.Op(0,0,1,1,"先算3，保留护卫身上的失能。",1,3);
                    s.Op(1,0,0,1,"护卫算6，失能使本次攻击伤害归零。",0,6);
                    s.Op(0,1,1,0,"算8实际伤到护卫，附加魅惑。",1,8).Enchant=StatusEffect.Charm;
                    s.Op(1,1,0,0,"观察护卫魅惑后的随机行动。",0);break;
                case 16:
                    s=new CampaignSpec(n,CharacterDefinition.SchoolOfNecromancy,new[]{2,3,3,8,8,2,1,1}){First=1,Guardian=2};s.Npcs[1].Maximum=24;s.Npcs[1].Health=1;s.Npcs[2].Maximum=32;s.Npcs[2].Range=2;s.Npcs[2].Force=true;s.Opening=l=>l.Player.SetHealthExact(14);
                    s.Op(1,0,0,1,"侍从3＋3＝6，点自己的2抵挡，2变1、生命仍14。",0);
                    var life=s.Op(0,1,3,0,"免费1暗蚀选择侍从击杀；换己右3与尸右8，只换一对。正常8＋同袍1＝9回复。 ");life.DeclineNecromancy=false;life.TargetSeat=1;life.SwapOwn=1;life.SwapDead=1;
                    s.Op(0,0,3,0,"免费暗蚀选择放弃，再正常1＋同袍1＝2，准备抵挡对照。");
                    s.Op(2,0,0,0,"主护卫以6力场来袭，2不能抵挡、不被消耗。",0,6).Prepare=(l,p)=>l.Equip(p,AttackDamageType.Force);
                    s.Wait(2,"观察力场反例后回到挥砍武装；自主时每第三次普通攻击改为力场。").Prepare=(l,p)=>l.Equip(p,AttackDamageType.Slashing);break;
                case 17:DivinationCourse(s);break;
                case 18:
                    s.Hands=new[]{2,4,2,5};s.First=1;s.Npcs[1].Maximum=30;
                    var a=s.Op(1,0,0,1,"护卫左2触碰你右4；发动幻术默发改借左2，算4，后续减伤不发动。",0);a.IllusionTouch=1;a.IllusionDefense=0;
                    s.Wait(0,"观摩保留2／4，让护卫再示范同一意图。");
                    var b=s.Op(1,0,0,1,"护卫调左2，再触碰右4；不发动改算、不减伤，算6受7伤。",0);b.Prepare=(l,p)=>l.Tune(p,p.LeftHand,2);b.IllusionTouch=0;b.IllusionDefense=0;
                    s.Wait(0,"观摩保留2／4；小手已经是2仍可发动幻影实体。");
                    var c=s.Op(1,0,0,1,"护卫调左4，借右4算8；改算不发动、幻影实体发动，9伤减半为5。",0);c.Prepare=(l,p)=>l.Tune(p,p.LeftHand,4);c.IllusionTouch=0;c.IllusionDefense=1;break;
                case 19:TransmutationCourse(s);break;
                case 20:s.Npcs[1]=new CampaignNpcSpec("法师幻影",MageTowerChapter.PhantomId,40){Attack=1,Range=CharacterTemplate.InfiniteAttackRange,Type=AttackDamageType.Acid,Phantom=true};break;
            }
            return s;
        }
        private static void EvocationCourse(CampaignSpec s)
        {
            s.Npcs[1].Maximum=32;s.Npcs[1].Type=AttackDamageType.Fire;
            var types=new[]{DamageType.Fire,DamageType.Cold,DamageType.Cold,DamageType.Fire,DamageType.Lightning,DamageType.Thunder,DamageType.Acid,DamageType.Poison,DamageType.Radiant,DamageType.Necrotic,DamageType.Psychic,DamageType.Force};
            for(int i=0;i<types.Length;i++)
            {
                int index=i;var type=types[i];
                Cast(s,0,4,"存储"+StatusNames.Of(type)+"：按当前数字算4，选择放弃攻击和本步类型；",true,type);
                var prep=s.Op(1,1,0,1,i==0?"护卫6火焰攻击，待用火抗先减伤；负面时长也减1。":"护卫准备一回合，保留你的手值与待用类型。",0,i==0?6:1);
                prep.Prepare=(l,p)=>{
                    if(index==3||index==6||index==8||index==11)l.Refit(p);
                    if(index==3||index==4){p.ApplyStatus(StatusEffect.Wet,2,p);l.Game.NoteSkill(p,"注水铭刻","自身潮湿2回合",p);}
                };
                var hit=Cast(s,0,6,"释放"+StatusNames.Of(type)+"：正常6攻击并消费待用类型；接着观察护卫自己的回合。");
                if(i==2)hit.Verify=l=>l.Seats[1].HasStatus(StatusEffect.Frozen);
                if(i==3)hit.Verify=l=>!l.Seats[1].HasStatus(StatusEffect.Wet)&&!l.Seats[1].HasStatus(StatusEffect.Burn);
                if(i==4)hit.Verify=l=>l.Seats[1].HasStatus(StatusEffect.Shock);
                if(i==2){s.Wait(1,"冻结后第一次跳过。");s.Wait(0,"观摩保留手值，等待冻结自然解除。");s.Wait(1,"冻结第二次跳过后正常解除。");}
                else if(i==1)s.Op(1,1,0,1,"覆霜把原本8运算减为4，仍可行动与攻击。",0,4);
                else if(i==9)s.Op(1,1,0,1,"护卫算9，腐朽阻止回复，留意腐朽如何阻止治疗。",0,9);
                else s.Wait(1,i==4?"观察感电回合开始的邻接波及。":i==7?"观察中毒在回合开始扣体力。":i==10?"按心灵分支查看恐慌／震慑限制。":"观察该状态的实际时点和计时。");
            }
            s.Wait(1,"护卫固定为火焰武装，留意火焰留下的燃烧。").Prepare=(l,p)=>l.Equip(p,AttackDamageType.Fire);
        }
        private static void AbjurationCourse(CampaignSpec s)
        {
            s.Hands=new[]{1,3,1,3};s.Npcs[1].Maximum=32;s.Npcs[1].Shield=true;
            var ward=Cast(s,0,2,"1＋1＝2，选择自己，再选物理整组；抗性不是一击消耗。");ward.TargetSeat=0;ward.Resistance=AttackDamageType.Bludgeoning;
            s.Op(1,1,0,1,"普通6原7伤，物理抗性减为4。",0,6);
            Cast(s,0,4,"再算4，放弃攻击存奥术守御4。").StoreWard=true;
            s.Op(1,1,0,0,"普通6：7→抗性4→屏障0，屏障整池清空、抗性保留。",0,6);
            var re=Cast(s,1,2,"重新算2，将持续火抗给护卫；自己的旧物理抗性撤销。");re.TargetSeat=1;re.Resistance=AttackDamageType.Fire;
            s.Op(1,1,0,1,"普通6不再被自己的旧物抗减半。",0,6);
            Cast(s,0,9,"算9回复，火焰抗性继续保留。");
        }
        private static void DivinationCourse(CampaignSpec s)
        {
            s.Hands=new[]{3,4,1,4};s.Npcs[1].Maximum=30;
            s.Op(0,0,1,1,"3＋4＝7，预兆记录另一手4。");
            s.Op(1,0,0,0,"护卫1＋7＝8，选择预兆4，改为4攻击，受5伤。",0).Portent=4;
            s.Op(0,1,1,1,"正常把另一手算成5，为下一份记录准备。",1,5);
            s.Op(0,0,1,1,"持7先正常算6，随后再回到7；留意另一只手的点数。",1,6);
            s.Op(0,0,1,1,"正常算7，记录另一手5。",1,7);
            s.Op(1,0,0,0,"护卫算8，消耗预兆5改为完整盾。",0,8).Portent=5;
            s.Op(0,1,1,1,"正常使用自己的5算9，破掉原盾，准备记录9。",1,9);
            s.Op(1,0,0,0,"护卫用自己的完整5运算，破盾，为后续攻击准备。",0);
            s.Op(0,0,1,1,"自己的7先算6，不消耗记录。",1,6);
            s.Op(0,0,1,1,"再算7，记录另一手9。",1,7);
            s.Op(1,0,0,0,"护卫算8，选择不发动，保留预兆9并承受伤害。",0,8).Portent=-1;
            var heal=s.Op(0,1,1,0,"自己的9＋护卫8原为7；持另一手7，消耗预兆9替换为9回复。 ");heal.Portent=9;
        }
        private static void TransmutationCourse(CampaignSpec s)
        {
            s.Hands=new[]{8,6,6,4};s.Npcs[1].Maximum=32;s.Npcs[1].Type=AttackDamageType.Fire;
            s.Op(0,0,1,0,"8＋6＝4，和护卫6／4无序匹配；回合末选择变形。 ").TargetSeat=1;
            s.Op(1,0,0,0,"羊6＋4＝1；原火焰技能暂停。",0);
            s.Op(0,1,1,0,"6＋羊1＝7，保留形态观察计时。");
            s.Op(1,1,0,0,"羊4＋4＝8，实际7钝击；第二个自身回合后恢复原形与当前数字。",0);
            s.Wait(0,"观摩保留4／7，让恢复的护卫在自己的回合调律。");
            s.Wait(1,"护卫把双手调为7／1。").Prepare=(l,p)=>{l.Tune(p,p.LeftHand,7);l.Tune(p,p.RightHand,1);};
            s.Op(0,0,1,0,"4＋7＝1，与护卫7／1匹配；再次选择变形。").TargetSeat=1;
            s.Op(1,1,0,0,"羊1＋己1＝2，无伤，仍是完整8血池。",0);
            s.Op(0,0,1,0,"1＋羊7＝8，经防御修正为10：羊承受8，剩余2由原形承受。").Verify=l=>!l.Seats[1].IsPolymorphed;
        }
    }
}
