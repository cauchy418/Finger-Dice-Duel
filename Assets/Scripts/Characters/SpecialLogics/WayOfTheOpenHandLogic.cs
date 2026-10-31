using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class WayOfTheOpenHandLogic : MonkLogic, ITurnStartHook
    {
        private int? _technique;private int _harmonyTurn=-1;
        public override string DisplayName=>"武僧·散打宗";
        public override void OnMatchReset(PlayerState owner,GameManager game){base.OnMatchReset(owner,game);_technique=null;_harmonyTurn=-1;}
        public override void CollectPreTurnChoices(PreTurnChoiceContext context,IList<ChoiceRequest> requests)
        {
            if(!CharacterBehaviors.ActorHas(context.Actor,this))return;_technique=null;if(!context.IsAttack||Ki<1)return;
            requests.Add(new ChoiceRequest(HandEffect.OpenHandTechnique,"散打技巧／渗透劲 · 选择招式或不发动",null,decider:context.Actor,
                options:MartialSkillChoices.OpenHandOptions(Ki),applyOption:v=>_technique=v,optionKey:MartialSkillChoices.OpenHand));
        }
        public override void OnResolveAttack(AttackContext context)
        {
            if(context.IsStatusDamage||context.IsReflected||context.Defender==null)return;
            int choice=_technique??SkillChoicePolicy.Choose(MartialSkillChoices.OpenHand,MartialSkillChoices.OpenHandOptions(Ki),context.Attacker,context.Defender,context.Manager);_technique=null;
            if(choice==-1&&Spend(3))context.ApplyUntimedOnHit(()=>{context.Defender.SetHiddenForce(context.Attacker);context.Manager?.NoteSkill(context.Attacker,"渗透劲",context.Defender.Name+" 被打入暗劲",context.Defender);});
            else if((choice==(int)StatusEffect.KnockedDown||choice==(int)StatusEffect.Stunned)&&Spend(1))
                context.ApplyAttachedStatus(context.Defender,(StatusEffect)choice,2,context.Attacker,context.Manager,"散打技巧");
        }
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(game.CurrentPlayer,owner)||!owner.IsAlive||Ki<=0||Ki!=owner.Health||_harmonyTurn==game.TurnCount)return;
            _harmonyTurn=game.TurnCount;
            MonkRules.Offer(owner,game,MonkRules.Harmony,"混元体 · 移除全部气回复体力，保留正常行动？",MonkRules.YesNo("发动混元体"),v=>{if(v==1)TryHarmonyBody(owner,game);});
        }
        public bool TryHarmonyBody(PlayerState owner,GameManager game)
        {
            if(owner==null||!owner.IsAlive||Ki<=0||Ki!=owner.Health)return false;
            int amount=Ki;int before=owner.Health;owner.TrySpendMonkKi(amount);game?.RecordSkillVisual(owner,"气",owner,new SkillVisualData(value:owner.MonkKi,previous:amount,variant:"spend"));owner.Heal(amount*2);game?.NoteSkill(owner,"混元体",$"移除{amount}气，回复{amount*2}体力",owner,new SkillVisualData(value:owner.Health-before,previous:amount));return true;
        }
    }
}
