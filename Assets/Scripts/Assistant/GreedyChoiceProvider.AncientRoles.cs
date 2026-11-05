using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : IAncientRoleChoicePolicy
    {
        private static bool AncientAlly(GameManager game,PlayerState owner,PlayerState target)
            => ReferenceEquals(owner,target)||ReferenceEquals(owner,target?.SummonOwner)|| game.IsKnownAlly(owner,target);
        public Hand ChooseAncientTarget(string key,PlayerState owner,IReadOnlyList<Hand> candidates,GameManager game)
        {
            Hand best=null;double bestScore=double.NegativeInfinity;
            foreach(var hand in candidates)
            {
                var target=game.OwnerOfHand(hand);if(target==null||!target.IsAlive)continue;
                bool ally=AncientAlly(game,owner,target);double score;
                if(key==AncientRoleChoices.AncientHeal)
                {
                    int need=!StatusRules.CanHeal(target)?0:target.MaximumHealth-target.Health;
                    // 回复为强制效果；无受伤队友时优先满血敌人，避免替敌人有效治疗。
                    score=ally?100+Math.Min(2,need)*30-target.Health:50-Math.Min(2,need)*30;
                }
                else if(key==AncientRoleChoices.LandSeal)
                {
                    if (hand.IsTargetRestricted || hand.IsConcealed) continue;
                    score = 0;
                    foreach(var other in game.Players)
                    {
                        if(!other.IsAlive || !StatusRules.CanAct(other)
                            || !StatusRules.CanUseHandAsAddend(other, hand, game)) continue;
                        int opportunity = 0;
                        foreach(var source in other.Hands)
                        {
                            if(!source.CanBeOperated||ReferenceEquals(source,hand)||ReferenceEquals(other,target)&&!(other.Character is ISelfAddendProvider self&&self.AllowsSelfAddend))continue;
                            int result=StatusRules.ApplyResultModifiers(other,Hand.ComputeSum(source.Value,hand.Value));
                            var effect=CharacterBehaviors.DisplayedEffectOf(other,result);
                            int value=effect==HandEffectType.Heal?HealingGain(other,9)*3
                                :effect==HandEffectType.Shield?8
                                :IsAttackEffect(effect)&&game.CanReach(other,owner)&&!other.HasStatus(StatusEffect.Disabled)?result*2:0;
                            value += SpecialistSetupValue(other, result, other.GetOtherHand(source).Value, game) / 2;
                            opportunity = Math.Max(opportunity, value);
                        }
                        score += AncientAlly(game,owner,other) ? -opportunity : opportunity;
                    }
                }
                else
                {
                    if(ally||target.HasStatus(StatusEffect.HolyLight))continue;
                    score=20+Math.Max(0,StatusRules.EffectiveAttackBonus(target))*3;
                    if(target.HasStatus(StatusEffect.Restrained))score-=15;
                    if(target.LeftHand.IsLocked||target.RightHand.IsLocked)score+=12;
                    if(game.CanReach(target,owner))score+=5;
                }
                if(score>bestScore){bestScore=score;best=hand;}
            }
            return key==AncientRoleChoices.LandSeal && bestScore<=0 ? null : best;
        }
        private static double FiendPactScore(int value,PlayerState owner,PlayerState target,GameManager game)
        {
            if(value==0)return owner.Health<=8?18:5;
            if(owner.FalseLife<=0||target==null||AncientAlly(game,owner,target)||target.HasStatus(StatusEffect.HolyLight))return -100;
            if(owner.FalseLife>=6&&owner.Health<=10)return -5;
            var status=(StatusEffect)value;if(target.HasStatus(status))return -10;
            if(status==StatusEffect.Disabled)return 12+Math.Max(0,StatusRules.EffectiveAttackBonus(target))*2-owner.FalseLife;
            if(status==StatusEffect.Panic)return 10+(game.CanReach(target,owner)?8:0)-owner.FalseLife;
            return -100;
        }
    }
}
