using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class StormSorceryLogic : CharacterTemplate,ITurnStartHook,ITurnEndHook,IAttackTypeProvider,IStatusApplyingAttack
    {
        private bool _chosen,_queued,_operated;public int ForcedRoll {get;set;}=-1;public Random Rng {get;set;}=new Random();
        public AttackDamageType Resistance {get;private set;}=AttackDamageType.None;
        public override string DisplayName=>"术士·风暴术法";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override void OnMatchReset(PlayerState owner,GameManager manager){_chosen=false;_queued=false;_operated=false;Resistance=AttackDamageType.None;}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(_chosen||_queued||!owner.IsAlive)return;_queued=true;
            var options=NewSubclassChoices.StormResistances();
            var request=new WizardChoice(owner,"风暴亲和 · 选择本局永久抗性",options,value=>{
                var type=(AttackDamageType)value;if(type!=AttackDamageType.Lightning&&type!=AttackDamageType.Thunder)return;
                if(_chosen)return;Resistance=type;owner.AddPermanentResistance(type);_chosen=true;_queued=false;game.NoteSkill(owner,"风暴亲和",AttackDamageTypes.NameOf(type)+"永久抗性");});
            if(game.DeferWizardChoice?.Invoke(request)==true)return;
            request.ChooseOption(SkillChoicePolicy.Choose(NewSubclassChoices.StormResistance,options,owner,null,game));
        }
        public override void OnResolveAttack(AttackContext context)
        {
            if(context.AttackType!=AttackDamageType.None)return;
            int roll=ForcedRoll>=0?ForcedRoll%100:(Rng??(Rng=new Random())).Next(100);
            context.AttackType=roll<30?AttackDamageType.Lightning:roll<60?AttackDamageType.Thunder:AttackTypeOf(context);
            context.Manager?.NoteSkill(context.Attacker,"风暴术法",AttackDamageTypes.NameOf(context.AttackType));
        }
        public override AttackDamageType AttackTypeOf(AttackContext context)=>(context?.Manager?.DistanceBetween(context.Attacker,context.Defender)??1)<=1?AttackDamageType.Bludgeoning:AttackDamageType.Piercing;
        public StatusEffect StatusToApplyOnHit(AttackContext context)=>StatusNames.StatusOf(AttackDamageTypes.ToDamageType(context.AttackType));
        public override void OnOperationResolved(PlayerState owner,Hand hand,int before,int add,HandEffectType effect,GameManager game,IPreTurnChoiceSink sink)=>_operated=true;
        public void OnTurnEnded(PlayerState owner,GameManager game)
        {
            bool acted=_operated;_operated=false;if(!acted||!owner.IsAlive||!StatusRules.CanAct(owner))return;
            var sources=new List<Hand>();foreach(var move in game.GetLegalMoves(owner))if(!sources.Contains(move.ActedHand))sources.Add(move.ActedHand);
            NewSubclassChoices.OfferHand(NewSubclassChoices.StormSource,"风暴余势 · 选择运算手，或不发动",owner,game,sources,true,source=>{
                if(source==null)return;var targets=new List<Hand>();foreach(var move in game.GetLegalMoves(owner))if(ReferenceEquals(move.ActedHand,source))targets.Add(move.TargetHand);
                NewSubclassChoices.OfferHand(NewSubclassChoices.StormAddend,"风暴余势 · 选择加数（只改变手值）",owner,game,targets,true,target=>{if(target!=null)game.RequestOperationAddend(owner,source,target,h=>game.RequestTrickeryOperation(owner,source,h,()=>game.ApplyStormFreeOperation(owner,source,h),true),effectless:true);});});
        }
    }
}
