using System;
using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public interface ILoreRecord
    {
        bool HasRecord(int value);
        int RecordCount { get; }
        int LoreLevel { get; }
    }
    public interface IWholeAttackDefense
    { bool CanReduceAttack(PlayerState owner,int attackValue);void ReduceWholeAttack(AttackContext context,GameManager game); }
    public interface IOperationAddendIllusion
    { bool CanRedirect(PlayerState owner); }
    public sealed class IllusionTouch
    {
        public PlayerState Owner {get;} public PlayerState Actor {get;}
        public Hand Source {get;} public Hand Selected {get;} public Hand Alternate {get;}
        public int SourceValue {get;} public bool Effectless {get;}
        public IllusionTouch(PlayerState owner,PlayerState actor,Hand source,Hand selected,Hand alternate,int sourceValue,bool effectless)
        {Owner=owner;Actor=actor;Source=source;Selected=selected;Alternate=alternate;SourceValue=sourceValue;Effectless=effectless;}
    }
    public interface IIllusionChoicePolicy
    {bool RedirectIllusion(IllusionTouch touch,GameManager game);}
    public interface INewSubclassChoicePolicy
    { Hand ChooseNewSubclassHand(string key,PlayerState owner,IReadOnlyList<Hand> hands,GameManager game); }
    public static class NewSubclassChoices
    {
        public const string LoreGrowth="lore_growth",LoreDefense="lore_defense",IllusionDefense="illusion_defense",StormResistance="storm_resistance",StormSource="storm_source",StormAddend="storm_addend";
        public static List<SkillChoiceOption> YesNo(string yes,string icon,string no="不发动")=>new List<SkillChoiceOption>{
            new SkillChoiceOption(1,yes,icon,"发动本次技能。"),new SkillChoiceOption(0,no,icon,"本次保留。")};
        public static List<SkillChoiceOption> StormResistances()=>new List<SkillChoiceOption>{
            new SkillChoiceOption((int)AttackDamageType.Lightning,"永久闪电抗性","Resistance/resistance_lightning","本局闪电伤害减半。"),
            new SkillChoiceOption((int)AttackDamageType.Thunder,"永久雷鸣抗性","Resistance/resistance_thunder","本局雷鸣伤害减半。")};
        public static bool NormalDamageCanBeReduced(PreTurnChoiceContext context)
            =>context.EffectiveResult<=0||CharacterBehaviors.FirstImplementer<IFixedNormalAttackDamage>(context.Actor)==null;
        public static void OfferHand(string key,string prompt,PlayerState owner,GameManager game,List<Hand> hands,bool decline,Action<Hand> apply)
        {
            if(hands.Count==0||!owner.IsAlive)return;
            var request=new WizardChoice(owner,prompt,hands,decline,apply,false,"不进行额外运算");
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            var policy=game.NewSubclassPolicyFor?.Invoke(owner)??game.GetChoiceProvider(owner) as INewSubclassChoicePolicy;
            var pick=policy?.ChooseNewSubclassHand(key,owner,hands,game);
            if(!request.Choose(pick)&&!decline)request.Choose(hands[0]);
        }
    }
    public partial class GameManager
    {
        public Func<PlayerState,INewSubclassChoicePolicy> NewSubclassPolicyFor {get;set;}
        private sealed class PreparedIllusion
        {
            public PlayerState Actor;
            public Hand Source,Selected,Resolved;
            public int Turn;
            public bool Matches(PlayerState actor,Hand source,Hand selected,int turn)
                =>Turn==turn&&ReferenceEquals(Actor,actor)&&ReferenceEquals(Source,source)
                    &&(ReferenceEquals(Selected,selected)||ReferenceEquals(Resolved,selected));
        }
        private readonly PreparedIllusion[] _illusionPrepared=new PreparedIllusion[2];
        private int _illusionEpoch;
        public Func<PlayerState,IIllusionChoicePolicy> IllusionPolicyFor {get;set;}
        private IllusionTouch CreateIllusionTouch(PlayerState actor,Hand source,Hand selected,int step,bool effectless,int? sourceValue)
        {
            if(actor==null||source==null||selected==null||WillSuppressOperationEffects(actor))return null;
            var owner=OwnerOfHand(selected);
            if(owner==null||!owner.IsAlive||owner.IsPolymorphed||ReferenceEquals(owner,actor)||!IsLegalMove(actor,source,selected,out _))return null;
            bool eligible=false;foreach(var skill in CharacterBehaviors.Implementers<IOperationAddendIllusion>(owner))
                if(skill.CanRedirect(owner)){eligible=true;break;}
            if(!eligible)return null;
            var other=owner.GetOtherHand(selected);if(!IsLegalMove(actor,source,other,out _))return null;
            return new IllusionTouch(owner,actor,source,selected,other,sourceValue??source.Value,
                effectless||step==0&&DruidForms.IsDragon(actor));
        }
        private void CacheIllusion(PlayerState actor,Hand source,Hand selected,Hand resolved,int step)
            =>_illusionPrepared[step]=new PreparedIllusion{Actor=actor,Source=source,Selected=selected,Resolved=resolved,Turn=TurnCount};
        private bool CachedIllusion(PlayerState actor,Hand source,Hand selected,int step,out Hand resolved)
        {
            var cache=_illusionPrepared[step];resolved=cache?.Resolved;
            return cache!=null&&cache.Matches(actor,source,selected,TurnCount);
        }
        private bool ChooseIllusion(IllusionTouch touch)
            =>(IllusionPolicyFor?.Invoke(touch.Owner)??GetChoiceProvider(touch.Owner) as IIllusionChoicePolicy)?.RedirectIllusion(touch,this)==true;
        private Hand ResolveIllusion(IllusionTouch touch,bool redirect)
        {
            bool use=redirect&&touch.Owner.IsAlive&&!touch.Owner.IsPolymorphed
                &&IsLegalMove(touch.Actor,touch.Source,touch.Alternate,out _);
            var actual=use?touch.Alternate:touch.Selected;
            NoteSkill(touch.Owner,"幻术默发",$"{touch.Actor.Name}的{CombatText.HandName(touch.Actor,touch.Source)}{touch.SourceValue}"
                +(use?$"改用{CombatText.HandName(touch.Owner,actual)}{actual.Value}作加数":"保留原加数"));
            return actual;
        }
        // 直接内核调用同步使用策略；控制器先经Request暂停询问人类，再复用同一缓存。
        public Hand PrepareOperationAddend(PlayerState actor,Hand source,Hand selected,int step=0,bool effectless=false,int? sourceValue=null)
        {
            if(CachedIllusion(actor,source,selected,step,out var cached))return cached;
            var touch=CreateIllusionTouch(actor,source,selected,step,effectless,sourceValue);
            var resolved=touch==null?selected:ResolveIllusion(touch,ChooseIllusion(touch));
            CacheIllusion(actor,source,selected,resolved,step);return resolved;
        }
        public bool RequestOperationAddend(PlayerState actor,Hand source,Hand selected,Action<Hand> completed,int step=0,bool effectless=false,int? sourceValue=null)
        {
            if(CachedIllusion(actor,source,selected,step,out var cached)){completed(cached);return false;}
            var touch=CreateIllusionTouch(actor,source,selected,step,effectless,sourceValue);
            if(touch==null){CacheIllusion(actor,source,selected,selected,step);completed(selected);return false;}
            int epoch=_illusionEpoch,turn=TurnCount;
            var prompt=$"【幻术默发】{touch.Actor.Name}：{CombatText.HandName(touch.Actor,source)}{touch.SourceValue} → 你的{CombatText.HandName(touch.Owner,selected)}{selected.Value}。\n"
                +$"改用你的{CombatText.HandName(touch.Owner,touch.Alternate)}{touch.Alternate.Value}作加数？";
            var request=new WizardChoice(touch.Owner,prompt,NewSubclassChoices.YesNo(
                "发动 · 改用"+CombatText.HandName(touch.Owner,touch.Alternate)+touch.Alternate.Value,
                "Subclasses/wizard_school_of_illusion","不发动 · 保留"+CombatText.HandName(touch.Owner,selected)+selected.Value),value=>{
                    if(epoch!=_illusionEpoch||turn!=TurnCount)return;
                    var actual=ResolveIllusion(touch,value==1);CacheIllusion(actor,source,selected,actual,step);completed(actual);
                });
            if(DeferWizardChoice?.Invoke(request)==true)return true;
            request.ChooseOption(ChooseIllusion(touch)?1:0);return false;
        }
        public void ClearPreparedOperationAddend()
        {_illusionEpoch++;_illusionPrepared[0]=null;_illusionPrepared[1]=null;ClearPreparedTrickery();}
        public bool ApplyStormFreeOperation(PlayerState actor,Hand source,Hand target)=>ApplyEffectlessOperation(actor,source,target,"暴风魔法");
    }
}
