using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public interface IDruidForm
    {
        string CircleKey { get; }
        int FormValue { get; }
        bool NeedsSecondAddend { get; }
    }
    public interface IDruidChoicePolicy
    {
        int ChooseForm(PlayerState owner,string circle,IReadOnlyList<SkillChoiceOption> options,GameManager game);
        bool ExitForm(PlayerState owner,IDruidForm form,GameManager game);
        PlayerState ChaliceTarget(PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game);
        Hand SecondAddend(PlayerState owner,Hand acted,Hand first,IReadOnlyList<Hand> candidates,GameManager game);
    }
    public static class DruidForms
    {
        public static readonly string[] MoonNames={"基础形态","熊","狼","鹰","蛇","枭熊","黑豹"};
        public static readonly string[] StarNames={"基础形态","射手座","圣杯座","巨龙座"};
        public static readonly string[] MoonKeys={"","moon_bear","moon_wolf","moon_eagle","moon_snake","moon_owlbear","moon_panther"};
        public static readonly string[] StarKeys={"","stars_archer","stars_chalice","stars_dragon"};
        public static readonly string[] MoonDescriptions={"回合开始双手和10，可消耗充能并跳过回合变形；回合末可解除。","12体力，攻−1 防+1 距1，挥砍；4改为合盾。","10体力，攻0 防−1 距1，挥砍；回复实际生命伤害的一半，向下取整。","8体力，攻+1 防−1 距3，穿刺；6不能攻击。","8体力，攻0 防0 距1，毒素伤害。","消耗2充能；16体力，攻0 防+2 距2，挥砍；基础伤害加双方距离。","消耗2充能；10体力，攻+2 防0 距1，挥砍；命中附2回合流血。"};
        public static readonly string[] StarDescriptions={"与荒野形态一并进入及解除；攻防相加、距离取高。","攻+2 防−1 距3；攻击改为光耀。","攻0 防0 距1；荒野体力上限+4；打出9回复后，可额外治疗一名角色9。","攻+1 防+1 距3；回合开始可纯运算，未进行则回合结束必须纯运算。"};
        public static List<SkillChoiceOption> Options(string circle,bool includeBase=true)
        {
            var names=circle=="moon"?MoonNames:StarNames;var keys=circle=="moon"?MoonKeys:StarKeys;var descriptions=circle=="moon"?MoonDescriptions:StarDescriptions;
            var result=new List<SkillChoiceOption>();for(int i=1;i<names.Length;i++)result.Add(new SkillChoiceOption(i,names[i],"Forms/"+keys[i],descriptions[i]));
            if(includeBase) result.Add(new SkillChoiceOption(0,"保持基础形态","Professions/druid",descriptions[0]));return result;
        }
        public static bool IsDragon(PlayerState player)
        {foreach(var form in CharacterBehaviors.Implementers<IDruidForm>(player))if(form.NeedsSecondAddend)return true;return false;}
        public static string Name(string circle,int value)=>(circle=="moon"?MoonNames:StarNames)[value];
        public static void OfferEntry(PlayerState owner,GameManager game,string circle,System.Action<int> apply)
        {
            var options=Options(circle);var request=new WizardChoice(owner,(circle=="moon"?"荒野形态":"星耀形态")+" · 选择形态",options,apply);
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            int pick=game.GetDruidPolicy(owner)?.ChooseForm(owner,circle,options,game)??1;
            if(!request.ChooseOption(pick)) request.ChooseOption(0);
        }
        public static void OfferExit(PlayerState owner,GameManager game,IDruidForm form,System.Action exit)
        {
            if(!owner.IsAlive || form.FormValue==0)return;
            var options=new[]{new SkillChoiceOption(1,"解除"+Name(form.CircleKey,form.FormValue),"Professions/druid","解除荒野及星耀，恢复原体力和技能。"),new SkillChoiceOption(0,"保留形态","Forms/"+(form.CircleKey=="moon"?MoonKeys:StarKeys)[form.FormValue],"保留当前形态。")};
            var request=new WizardChoice(owner,"回合结束 · 是否解除形态",options,value=>{if(value==1)exit();});
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            request.ChooseOption(game.GetDruidPolicy(owner)?.ExitForm(owner,form,game)==true?1:0);
        }
    }
}
