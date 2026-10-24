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
    { bool ShouldRedirect(PlayerState owner,GameManager game); }
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
        private PlayerState _illusionActor;
        private Hand _illusionSource,_illusionSelected,_illusionResolved;
        private int _illusionTurn=-1;
        public Hand PrepareOperationAddend(PlayerState actor,Hand source,Hand selected)
        {
            if(actor==null||source==null||selected==null)return selected;
            if(_illusionTurn==TurnCount&&ReferenceEquals(actor,_illusionActor)&&ReferenceEquals(source,_illusionSource)
                &&(ReferenceEquals(selected,_illusionSelected)||ReferenceEquals(selected,_illusionResolved)))return _illusionResolved;
            _illusionActor=actor;_illusionSource=source;_illusionSelected=selected;_illusionResolved=selected;_illusionTurn=TurnCount;
            var owner=OwnerOfHand(selected);
            if(owner==null||ReferenceEquals(owner,actor))return selected;
            var other=ReferenceEquals(selected,owner.LeftHand)?owner.RightHand:owner.LeftHand;
            if(!IsLegalMove(actor,source,other,out _))return selected;
            foreach(var skill in CharacterBehaviors.Implementers<IOperationAddendIllusion>(owner))
                if(skill.ShouldRedirect(owner,this))
                { _illusionResolved=other;NoteSkill(owner,"镜像错位",$"加数改为{CombatText.HandName(owner,other)}{other.Value}");break; }
            return _illusionResolved;
        }
        public void ClearPreparedOperationAddend()
        {_illusionTurn=-1;_illusionActor=null;_illusionSource=null;_illusionSelected=null;_illusionResolved=null;}
        public bool ApplyStormFreeOperation(PlayerState actor,Hand source,Hand target)
        {
            if(!actor.IsAlive||!StatusRules.CanAct(actor)||!IsLegalMove(actor,source,target,out _))return false;
            target=PrepareOperationAddend(actor,source,target);
            int old=source.Value,add=target.Value,value=StatusRules.ApplyResultModifiers(actor,Hand.ComputeSum(old,add));
            source.SetValue(value);
            NoteSkill(actor,"风暴余势",$"{CombatText.HandName(actor,source)}{old}+{add}→{value}（无效果）");
            ClearPreparedOperationAddend();return true;
        }
    }
}
