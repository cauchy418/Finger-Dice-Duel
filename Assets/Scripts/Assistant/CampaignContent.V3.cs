using System;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public static partial class CampaignContent
    {
        public static readonly CharacterDefinition HeavyFighter=new CharacterDefinition("story_heavy_fighter","重甲战士",SpecialLogicType.None,2,2,20,"攻+2，防+2，距离1，挥砍伤害。没有特殊技能。","Heavy Fighter");
        public static CampaignSpec BuildV3(int level)
        {
            CampaignChapter.ForLevel(level);
            int legacy=CampaignChapter.LegacyLevel(level);
            CampaignSpec spec=level>=11&&level<=20?GalleryCourse(level):level==29?BladesongCourse():level==23?WardCourse():Build(legacy);
            spec.Level=level;
            if(level==1)
            {
                spec.Role=HeavyFighter;
                spec.Steps[0].Text="借老兵左3算出4，攻击+2令这一击造成6。";
                spec.Steps[1].Text="老兵算4反击；防御+2将4点伤害减为2。";
            }
            ApplyGuideText(spec);
            return spec;
        }
        private static void ApplyGuideText(CampaignSpec spec)
        {
            string[] lines=CampaignGuideText.For(spec.Level);
            // A shared page may cover several legal operations; these are checkpoints,
            // not additional spoken instructions. Explicit mappings keep concepts aligned.
            int[] map=null;
            switch(spec.Level)
            {
                case 1:map=new[]{0,3,4,5,6,7};break;
                case 2:map=new[]{0,1,2,3,4,5,6};break;
                case 9:map=new[]{0,1,2,3,4,5,6};break;
                case 11:map=new[]{0,4,5};break;
                case 12:map=new[]{0,4,4};break;
                case 13:map=new[]{0,2,2,4,4};break;
                case 14:map=new[]{0,0,1,2,3};break;
                case 15:map=new[]{0,1,2,3,3};break;
                case 16:map=new[]{0,1,2,3,4};break;
                case 17:map=new[]{0,2,3,4,5,5};break;
                case 19:map=new[]{0,2,3,3,4,4,4,4,5,5,5,6};break;
                case 21:map=new[]{0,1,2,3,4};break;
                case 22:map=new[]{0,1,2,3,4,5};break;
                case 23:map=new[]{0,1,2,3,4,5,5,6};break;
                case 24:map=new[]{0,1,2,3,4};break;
                case 25:map=new[]{0,2,5,5,6};break;
                case 26:map=new[]{0,1,2,2,2,3,4,4,4,4,5,5};break;
                case 27:map=new[]{0,1,2,3,4};break;
                case 28:map=new[]{0,1,2,2,3,3,4,4,5};break;
                case 29:map=new[]{0,3,3,4,4,5,5};break;
            }
            for(int i=0;i<spec.Steps.Count;i++)
            {
                var step=spec.Steps[i];step.Id=CampaignChapter.LevelId(spec.Level)+"/"+(i+1);
                int index=spec.Level==18?step.GuideIndex:map!=null&&i<map.Length?map[i]:Math.Min(lines.Length-1,i*lines.Length/Math.Max(1,spec.Steps.Count));
                step.GuideIndex=index;
            }
            for(int i=0;i<spec.Steps.Count;i++)
            {
                var step=spec.Steps[i];int end=i+1<spec.Steps.Count?spec.Steps[i+1].GuideIndex:lines.Length;
                step.Text=string.Join("",lines,step.GuideIndex,Math.Max(1,end-step.GuideIndex));
            }
        }
        public static string TrainingSkillsV3(int level,int seat)
        {
            if(level==5||level%10==0)return "";
            if(level>=11&&level<=20)
                return "演练调律：行动前调整自己可用且无完整盾的手值。"+
                    (level>=13&&level<=16?"试验外壳：开场持有一次对应元素免疫；被攻击后消耗。":"")+
                    (level==17?"安魂：示范坟冢之力后为访客回复8，限一次。回廊重赋：实战开始时恢复访客的坟冢之力一次。":"")+
                    (level==18?"刻印：自己可行动时清除已观察的负面，附下一种反应前置，限13次。训练整备：净化自身并回复16，限7次。":"")+
                    (level==19?"换相：自己行动前切换武装。架盾试验：形成5盾并使防御变为+3。":"");
            if(level==23)return "演练调律：行动前调整自己可用且无完整盾的手值。符文换装：切换挥砍／火焰武装。";
            if(level==29)return "演练调律：行动前调整自己可用且无完整盾的手值。";
            return TrainingSkills(CampaignChapter.LegacyLevel(level),seat);
        }
        private static CampaignSpec WardCourse()
        {
            var s=new CampaignSpec(23,CharacterDefinition.SchoolOfAbjuration,new[]{1,3,1,3});s.Npcs[1]=new CampaignNpcSpec("奥法护卫","campaign_arcane_tuner",32){Shield=true};
            var first=Cast(s,0,2,"物理抗性");first.TargetSeat=0;first.Resistance=AttackDamageType.Bludgeoning;
            s.Op(1,1,0,1,"物理减伤",0,6);
            Cast(s,0,4,"奥术守御").StoreWard=true;
            s.Op(1,1,0,0,"守御抵挡",0,6);
            var recast=Cast(s,1,2,"换成火抗");recast.TargetSeat=0;recast.Resistance=AttackDamageType.Fire;
            s.Op(1,1,0,1,"旧物抗已撤销",0,6);
            s.Op(1,1,0,1,"火抗",0,4).Prepare=(l,p)=>l.Equip(p,AttackDamageType.Fire);
            Cast(s,0,9,"回复");return s;
        }
        private static CampaignSpec BladesongCourse()
        {
            var s=new CampaignSpec(29,CharacterDefinition.Bladesinging,new[]{4,6,2,4});s.Npcs[1]=new CampaignNpcSpec("奥法护卫","campaign_arcane_tuner",32){Range=2,Shield=true};
            s.Opening=l=>l.Player.SetHealthExact(14);
            s.Op(0,0,1,1,"进入剑歌").Option=(int)AttackDamageType.Fire;
            s.Wait(1,"留下加数").Prepare=(l,p)=>l.Tune(p,p.RightHand,1);
            s.Op(0,0,1,1,"实际回血维持剑歌");
            s.Wait(1,"观察剑歌维持");
            s.Op(0,1,1,0,"火焰攻击维持剑歌");
            s.Wait(1,"保留加数");
            s.Op(0,1,1,1,"满血9不算有效回复");return s;
        }
    }
}
