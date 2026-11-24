using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        public bool CleanseLightAttack(PlayerState owner,PlayerState target,GameManager game)=>WantsLightCleanse(owner,target,game);
        private static bool WantsLightCleanse(PlayerState owner,PlayerState target,GameManager game)
        {
            if(owner==null||target==null||game?.IsKnownAlly(owner,target)==true)return false;
            int count=0,cost=0;
            foreach(var state in target.Statuses)
            {
                if(!StatusNames.IsNegativeStatus(state))continue;
                count++;
                int value=state==StatusEffect.Frozen||state==StatusEffect.Stunned||state==StatusEffect.Charm?60
                    :state==StatusEffect.Disabled||state==StatusEffect.Tinnitus||state==StatusEffect.Decay?28:10;
                cost+=value*Math.Min(2,target.StatusTurnsLeft(state));
            }
            int bonus=ProjectedAffinityDamage(owner,target,count*2,AttackDamageType.Radiant);
            return bonus*8>cost;
        }
        private static int LightCleanseDamage(PlayerState owner,PlayerState target,GameManager game)
        {
            if(CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(owner)==null||!WantsLightCleanse(owner,target,game))return 0;
            int count=0;foreach(var status in target.Statuses)if(StatusNames.IsNegativeStatus(status))count++;
            return count*2;
        }
        private static int ClericDomainMoveValue(PlayerState actor,PlayerState target,int result,GameManager game)
        {
            if(actor==null||target==null||game==null||!IsAttackEffect(DivineScoredEffect(actor,result,game))
                ||game.IsKnownAlly(actor,target)||!game.CanReach(actor,target)||!StatusRules.CanAttackTarget(actor,target,game))return 0;
            int value=0;
            bool cancelled=FeyCanCancel(target,result);
            if(target.HasStatus(StatusEffect.NatureBlessing)&&!actor.HasStatus(StatusEffect.HolyLight)
                &&actor.StatusTurnsLeft(StatusEffect.Restrained)<2&&!cancelled)value-=24;
            var nature=CharacterBehaviors.FirstImplementer<NatureDomainLogic>(actor);
            if(nature?.StoredElementStatus>StatusEffect.None)
            {
                bool canDeliver=!target.HasStatus(StatusEffect.HolyLight)&&CharacterBehaviors.FirstImplementer<NatureDomainLogic>(target)==null
                    &&!cancelled&&!target.HasAnyShield;
                value+=canDeliver?18:-12;
            }
            bool light=CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(actor)!=null||actor.HasStatus(StatusEffect.LightBlessing);
            if(light&&!cancelled&&!target.HasStatus(StatusEffect.HolyLight)&&target.StatusTurnsLeft(StatusEffect.Blind)<2)value+=12;
            return value;
        }
    }
}
