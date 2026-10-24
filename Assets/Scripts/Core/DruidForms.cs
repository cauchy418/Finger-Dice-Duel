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
        public static readonly string[] MoonNames={"基础形态","熊","狼","鹰","蛇"};
        public static readonly string[] StarNames={"基础形态","射手","圣杯","巨龙"};
        public static readonly string[] MoonKeys={"","moon_bear","moon_wolf","moon_eagle","moon_snake"};
        public static readonly string[] StarKeys={"","stars_archer","stars_chalice","stars_dragon"};
        public static readonly string[] MoonDescriptions={"双手和10可进入；回合结束双手相同可解除。","攻−1 防+2 距1；4改为合盾。","攻+2 防−1 距1；攻击实际伤害的一半回复自身。","攻+1 防−1 距3；攻击全为穿刺，6不发动攻击。","攻0 防0 距1；攻击为毒素伤害，命中附加中毒。"};
        public static readonly string[] StarDescriptions={"双手和10可进入；回合结束双手相同可解除。","攻+2 防−1 距3；攻击为光耀，命中附加目盲。","攻0 防0 距3；9回复自身后，可额外回复一名其他角色9点。","攻+1 防+1 距3；同一只手连续加两次，第二次后才结算效果；加数可重复。"};
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
            if(!owner.IsAlive || form.FormValue==0 || owner.LeftHand.Value!=owner.RightHand.Value)return;
            var options=new[]{new SkillChoiceOption(1,"解除"+Name(form.CircleKey,form.FormValue),"Professions/druid","恢复本结社的基础形态，不影响另一结社。"),new SkillChoiceOption(0,"保留形态","Forms/"+(form.CircleKey=="moon"?MoonKeys:StarKeys)[form.FormValue],"保留当前形态。")};
            var request=new WizardChoice(owner,"回合结束 · 是否解除形态",options,value=>{if(value==1)exit();});
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            request.ChooseOption(game.GetDruidPolicy(owner)?.ExitForm(owner,form,game)==true?1:0);
        }
    }
}
