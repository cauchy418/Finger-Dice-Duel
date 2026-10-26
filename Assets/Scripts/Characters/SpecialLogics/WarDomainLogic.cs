using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>回合开始按攻击数字数量，为至多两名不同角色施加战恩。</summary>
    public sealed class WarDomainLogic:CharacterTemplate,ITurnStartHook,ISkillUseCounter
    {
        public int SkillUseCount {get;private set;}
        public override string DisplayName=>"牧师·战争领域";
        public override int DefaultAttackBonus=>-1;
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext context)=>ArcaneChoices.NearFar(context);
        public override void OnMatchReset(PlayerState owner,GameManager game){SkillUseCount=0;}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(game?.CurrentPlayer,owner)||!owner.IsAlive)return;
            int count=0;foreach(var hand in owner.Hands)if(DivineChoices.AttackDigit(owner,hand.Value))count++;
            if(count>0)Offer(owner,game,count,new List<PlayerState>());
        }
        private void Offer(PlayerState owner,GameManager game,int remaining,List<PlayerState> selected)
        {
            if(remaining<=0||!owner.IsAlive||owner.IsPolymorphed)return;
            var targets=new List<PlayerState>();foreach(var p in game.Players)
                if(p.IsAlive&&!p.HasStatus(StatusEffect.HolyLight)&&!selected.Contains(p))targets.Add(p);
            DivineChoices.ChooseTarget(DivineChoices.Blessing,$"战恩 · 选择角色（剩余{remaining}次），或不发动",owner,game,targets,true,target=>{
                if(target==null)return;
                target.ApplyStatus(StatusEffect.WarBlessing,2,owner);selected.Add(target);SkillUseCount++;
                game.NoteSkill(owner,"战恩",target.Name+" 攻防+1，持续2回合；9可改攻击");
                Offer(owner,game,remaining-1,selected);
            });
        }
        public override int ScoreMoveBonus(MoveValueContext context)
            =>(context.ActedValueAfter==4||context.ActedValueAfter==6||context.ActedValueAfter==8)?15:0;
    }
}
