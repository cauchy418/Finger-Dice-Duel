using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class WayOfTheNinjaLogic : MonkLogic, IMultiHitAttack
    {
        private int? _choice;
        public const int HitCount=2;
        public int MaxHits=>Ki>0?2:1;
        public override string DisplayName=>"武僧·忍宗";
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(!CharacterBehaviors.ActorHas(context.Actor,this))return;_choice=null;
            if(!context.IsAttack||Ki<1)return;
            requests.Add(new ChoiceRequest(HandEffect.NinjaTechnique,"影分身 · 是否消耗1气，将攻击拆成两段？",null,decider:context.Actor,
                options:MonkRules.YesNo("影分身 · 1气","Subclasses/monk_way_of_the_ninja"),applyOption:v=>_choice=v,optionKey:MonkRules.Ninja));
        }
        public override void OnResolveAttack(AttackContext context)
        {
            if(context.IsStatusDamage||context.IsReflected||Ki<1)return;
            int choice=_choice??SkillChoicePolicy.Choose(MonkRules.Ninja,MonkRules.YesNo("影分身 · 1气"),context.Attacker,context.Defender,context.Manager);_choice=null;
            if(choice!=1||!Spend(1))return;context.HitCount=2;context.NinjaShadowSplit=true;
            context.Manager?.NoteSkill(context.Attacker,"影分身","消耗1气，初始伤害减半，攻击两段");
        }
        public override void OnMatchReset(PlayerState owner,GameManager game){base.OnMatchReset(owner,game);_choice=null;}
    }
}
