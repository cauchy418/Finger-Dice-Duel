using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class SchoolOfIllusionLogic : CharacterTemplate,IOperationAddendIllusion,IWholeAttackDefense,IAttackTypeProvider
    {
        private bool? _defend;
        public override string DisplayName=>"法师·幻术学派";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override int ScoreMoveBonus(MoveValueContext context)
            =>(context.ActedValueAfter==2||context.OtherHandValue==2?24:0)+(Math.Abs(context.ActedValueAfter-context.OtherHandValue)==2?30:0);
        public override void OnMatchReset(PlayerState owner,GameManager manager)=>_defend=null;
        public bool CanRedirect(PlayerState owner)
            => owner!=null&&!owner.IsPolymorphed&&(owner.LeftHand.Value==2||owner.RightHand.Value==2);
        private static Hand Smaller(PlayerState owner)
            => Math.Abs(owner.LeftHand.Value-owner.RightHand.Value)==2?(owner.LeftHand.Value<owner.RightHand.Value?owner.LeftHand:owner.RightHand):null;
        public bool CanReduceAttack(PlayerState owner,int attackValue)=>owner!=null&&Smaller(owner)!=null;
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(!context.IsAttack||!CharacterBehaviors.ActorHas(context.Defender,this))return;_defend=null;
            if(Smaller(context.Defender)==null||!NewSubclassChoices.NormalDamageCanBeReduced(context))return;
            requests.Add(new ChoiceRequest(HandEffect.SchoolOfIllusionDefense,"镜像折光",null,decider:context.Defender,
                options:NewSubclassChoices.YesNo("小手变2 · 减半","Subclasses/wizard_school_of_illusion"),
                applyOption:v=>_defend=v==1,optionKey:NewSubclassChoices.IllusionDefense));
        }
        public void ReduceWholeAttack(AttackContext context,GameManager game)
        {
            var smaller=Smaller(context.Defender);if(smaller==null||context.IgnoreIncomingModifiers||context.IsStatusDamage)return;
            bool use=_defend==true;if(game?.AnsweredLookupFor==null)use=SkillChoicePolicy.Choose(NewSubclassChoices.IllusionDefense,NewSubclassChoices.YesNo("减伤","Subclasses/wizard_school_of_illusion"),context.Defender,context.Attacker,game)==1;
            _defend=null;if(!use)return;int before=smaller.Value;smaller.SetValue(2);context.HalveWholeAttackDamage();
            game?.NoteSkill(context.Defender,"镜像折光",$"{CombatText.HandName(context.Defender,smaller)}{before}→2，本次伤害减半");
        }
        public override AttackDamageType AttackTypeOf(AttackContext context)=>(context?.Manager?.DistanceBetween(context.Attacker,context.Defender)??1)<=1?AttackDamageType.Bludgeoning:AttackDamageType.Piercing;
    }
}
