using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private Hand DevotionShockTarget(ChoiceRequest request,PlayerState owner)
        {
            Hand best=null;double score=double.NegativeInfinity;
            foreach(var hand in request.Candidates)
            {
                var target=Game?.OwnerOfHand(hand);if(target==null||!target.IsAlive)continue;
                bool ally=Game.IsKnownAlly(owner,target);
                bool shield=target.LeftHand.IsShield||target.RightHand.IsShield;
                int loss=shield?0:StatusRules.ApplyDamageAffinities(target,StatusRules.ApplyIncomingDamage(target,2),AttackDamageType.Bludgeoning);
                double value=ally?-10000-loss*100-target.FalseLife:(loss>=target.Health?1000:loss*20)+30-target.Health;
                // If the compulsory skill has only allies in range, minimize harm.
                if(ally)value+=shield?500:target.Health*2;
                if(value>score){best=hand;score=value;}
            }
            return best??(request.Candidates.Count>0?request.Candidates[0]:request.DeclineResult);
        }
        private Hand DevotionProtect(ChoiceRequest request,PlayerState owner)
        {
            var target=request.RelatedTarget;var attacker=request.RelatedActor;
            if(Game==null||target==null||attacker==null||!Game.IsKnownAlly(owner,target)||ReferenceEquals(owner,target)
                ||Game.IsKnownAlly(owner,attacker)||!Game.CanReach(owner,target))return request.DeclineResult;
            bool targetShield=target.LeftHand.IsShield||target.RightHand.IsShield;
            bool ownShield=owner.LeftHand.IsShield||owner.RightHand.IsShield;
            var ctx=new AttackContext(attacker,target,attacker.LeftHand,request.ResultValue>0?request.ResultValue:4,Game);
            var type=Game.CurrentRedirectAttack?.AttackType??(attacker.Character as IAttackTypeProvider)?.AttackTypeOf(ctx)??AttackDamageType.Slashing;
            if(target.HasImmunity(type)||targetShield&&type!=AttackDamageType.Force)return request.DeclineResult;
            if(ownShield&&type!=AttackDamageType.Force)return request.Candidates[0];
            int raw=request.ResultValue>0?request.ResultValue:4;
            int incoming=StatusRules.ApplyDamageAffinities(owner,StatusRules.ApplyIncomingDamage(owner,
                AttackContext.ApplyStatBonuses(raw,StatusRules.EffectiveAttackBonus(attacker),StatusRules.EffectiveDefenseBonus(owner),type==AttackDamageType.Force)),type);
            if(Game.CurrentRedirectAttack?.IgnoreStatBonuses==true)incoming=StatusRules.ApplyDamageAffinities(owner,StatusRules.ApplyIncomingDamage(owner,raw),type);
            int original=StatusRules.ApplyDamageAffinities(target,StatusRules.ApplyIncomingDamage(target,
                AttackContext.ApplyStatBonuses(raw,StatusRules.EffectiveAttackBonus(attacker),StatusRules.EffectiveDefenseBonus(target),type==AttackDamageType.Force)),type);
            if(Game.CurrentRedirectAttack?.IgnoreStatBonuses==true)original=StatusRules.ApplyDamageAffinities(target,StatusRules.ApplyIncomingDamage(target,raw),type);
            if(owner.Health+owner.FalseLife<=incoming)return request.DeclineResult;
            return original>=target.Health+target.FalseLife||owner.Health*target.MaximumHealth>target.Health*owner.MaximumHealth&&incoming<=original
                ?request.Candidates[0]:request.DeclineResult;
        }
        private int AvoidFriendlyShock(PlayerState actor,LegalMove move,int result,GameManager game)
        {
            if(CharacterBehaviors.FirstImplementer<IProtectionChoiceProvider>(actor)==null)return 0;
            if(result!=4&&result!=5&&result!=6&&!move.ActedHand.IsShield)return 0;
            bool ally=false;
            foreach(var p in game.Players)
            {
                if(!p.IsAlive||ReferenceEquals(p,actor)||!game.CanReach(actor,p))continue;
                if(!game.IsKnownAlly(actor,p))return 0;ally=true;
            }
            return ally?-1000:0;
        }
    }
}
