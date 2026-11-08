using FingerDiceDuel.Core;
using System.Collections.Generic;
namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>牧师共同治疗：所有回复数字可选择其他存活角色。</summary>
    public abstract class ClericTemplate:CharacterTemplate,IPreTurnChoiceProvider,IHealOverride,IHealTargetAdvisor
    {
        public override bool ResultNeedsTarget(PreTurnChoiceContext c)=>c.Effect==HandEffectType.Heal||base.ResultNeedsTarget(c);
        public override void CollectPreTurnChoices(PreTurnChoiceContext c,IList<ChoiceRequest> requests)
        {
            if(c.Effect!=HandEffectType.Heal||CharacterBehaviors.FirstImplementer<LifeDomainLogic>(c.Actor)!=null
                ||!ReferenceEquals(CharacterBehaviors.FirstImplementer<ClericTemplate>(c.Actor),this))return;
            var hands=new List<Hand>();foreach(var p in c.Manager.Players)if(p.IsAlive)hands.Add(p.LeftHand);
            requests.Add(new ChoiceRequest(HandEffect.LifeDomainHealTarget,"回复 · 选择角色（9点体力）",hands,allowDecline:false,decider:c.Actor,resultValue:9));
        }
        public PlayerState ApplyHeal(PlayerState owner,GameManager game,IPreTurnChoiceSink sink)
            =>LifeDomainLogic.ResolveTarget(owner,game,sink,HandEffect.LifeDomainHealTarget);
        public PlayerState BestHealTarget(PlayerState owner,GameManager game)
        {
            PlayerState best=owner;int bestScore=int.MinValue;
            foreach(var p in game.Players)
            {
                if(!p.IsAlive||!game.IsKnownAlly(owner,p))continue;
                int score=StatusRules.CanHeal(p)?System.Math.Min(9,p.MaximumHealth-p.Health)*8:0;
                foreach(var state in p.Statuses)if(!PlayerState.HolyFireAllows(state)&&StatusNames.IsNegativeStatus(state))score+=12;
                if(p.HasStatus(StatusEffect.Scorch))score-=20;
                if(CharacterBehaviors.FirstImplementer<KnowledgeDomainLogic>(owner)!=null&&!p.HasStatus(StatusEffect.HolyLight))
                {score+=12;foreach(var hand in p.Hands)if(hand.Value==4||hand.Value==6||hand.Value==8)score+=5;}
                if(p.Health<=5)score+=25;
                if(score>bestScore){best=p;bestScore=score;}
            }
            return best;
        }
    }
    public sealed class KnowledgeDomainLogic:ClericTemplate
    {
        public override string DisplayName=>"牧师·知识领域";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext c)=>ArcaneChoices.NearFar(c);
        public override int ScoreMoveBonus(MoveValueContext c)
            =>c.ActedHand!=null&&c.TargetHand!=null&&c.ActedHand.Value+c.TargetHand.Value>=10?20:0;
    }
    public sealed class TrickeryDomainLogic:ClericTemplate,ITrickeryDomain
    {
        public override string DisplayName=>"牧师·诡术领域";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext c)=>ArcaneChoices.NearFar(c);
    }
    public sealed class TempestDomainLogic:ClericTemplate,ITempestDomain,ITurnStartHook,IStatusApplyingAttack
    {
        public override string DisplayName=>"牧师·风暴领域";
        public override int DefaultDefenseBonus=>-1;
        public override int BaseAttackRange=>InfiniteAttackRange;
        public override AttackDamageType AttackTypeOf(AttackContext c)=>ArcaneChoices.NearFar(c);
        public StatusEffect StatusToApplyOnHit(AttackContext c)=>StatusNames.StatusOf(AttackDamageTypes.ToDamageType(c.AttackType));
        public void OnTurnStarted(PlayerState owner,GameManager game)
        {if(ReferenceEquals(game.CurrentPlayer,owner))ClericChoices.FreeOperation(owner,game,null,true,"风暴之心");}
    }
}
