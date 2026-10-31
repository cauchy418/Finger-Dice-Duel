using System;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    public sealed class WayOfTheDrunkenMasterLogic : MonkLogic, IDrunkenStance, ITurnStartHook, IHandEffectRewriter, ISelectiveHandEffectRewriter, IHandEffectDisplay
    {
        public override string DisplayName=>"武僧·醉拳宗";
        public bool Drunken {get;private set;}
        public override int ScoreMoveBonus(MoveValueContext context)=>Drunken?(context.ActedValueAfter==context.OtherHandValue?35:10):context.ActedValueAfter==9&&Ki>=2?25:0;
        public Random Rng {get;set;}=new Random();
        public int ForcedOutgoing {get;set;}=-1;public int ForcedIncoming {get;set;}=-1;
        private int _upkeepTurn=-1;
        public void EndDrunken()=>Drunken=false;
        public bool TryEnter(PlayerState owner){if(!owner.IsAlive||owner.HasStatus(StatusEffect.HolyLight))return false;Drunken=true;return true;}
        public int RollOutgoing()=>ForcedOutgoing>=1?Math.Min(10,ForcedOutgoing):Rng.Next(1,11);
        public int RollIncoming()=>ForcedIncoming>=1?Math.Min(5,ForcedIncoming):Rng.Next(1,6);
        public bool HandlesResult(int value)=>Drunken;
        public HandEffectType DisplayedEffectOf(int value)=>Drunken?HandEffectType.Attack4:Hand.GetEffectType(value);
        public HandEffectType RewriteEffect(PlayerState actor,Hand hand,int add,GameManager game){if(Drunken)hand.SetShieldState(ShieldState.None);return DisplayedEffectOf(hand.Value);}
        public override void OnMatchReset(PlayerState owner,GameManager game){base.OnMatchReset(owner,game);Drunken=false;_upkeepTurn=-1;}
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {
            if(!ReferenceEquals(game.CurrentPlayer,owner)||!Drunken||_upkeepTurn==game.TurnCount)return;_upkeepTurn=game.TurnCount;
            if(!Spend(1)){EndDrunken();game.NoteSkill(owner,"酣醉若狂","气不足，解除醉拳",owner,new SkillVisualData(value:Ki,variant:"expire"));}
            else game.NoteSkill(owner,"酣醉若狂","消耗1气，维持醉拳",owner,new SkillVisualData(value:Ki,variant:"upkeep"));
        }
        public override void OnOperationResolved(PlayerState owner,Hand hand,int before,int add,HandEffectType effect,GameManager game,IPreTurnChoiceSink sink)
        {
            if(Drunken||hand?.Value!=9||!owner.IsAlive||owner.HasStatus(StatusEffect.HolyLight))return;
            MonkRules.Offer(owner,game,MonkRules.Drunken,"醉拳技巧 · 本次回复已完成，是否进入醉拳？",MonkRules.YesNo("进入醉拳","Effects/drunken_fist"),v=>{if(v==1&&TryEnter(owner))game.NoteSkill(owner,"醉拳技巧","进入醉拳状态",owner,new SkillVisualData(value:Ki,variant:"enter"));});
        }
    }
}
