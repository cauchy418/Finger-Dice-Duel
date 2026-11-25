using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        // Keep duel heuristics unchanged. A teammate changes both the healing
        // opportunity and the cost of removing an existing friendly blessing.
        private static bool IsTeamClericAi(PlayerState owner,GameManager game)
            =>game!=null&&game.Players.Count>2&&CharacterBehaviors.FirstImplementer<ClericTemplate>(owner)!=null;

        private static int NetClericHealingGain(PlayerState patient,int amount)
        {
            int gain=HealingGain(patient,amount);
            if(gain>0&&patient.HasStatus(StatusEffect.Scorch))
            {
                int backlash=StatusRules.ApplyDamageAffinities(patient,StatusRules.ApplyIncomingDamage(patient,(gain+1)/2),AttackDamageType.Fire);
                gain-=Math.Max(0,backlash-patient.FalseLife);
            }
            return gain;
        }
        private static int TeamClericHealValue(PlayerState owner,PlayerState patient,GameManager game,int amount)
        {
            int heal=NetClericHealingGain(patient,amount);
            int value=heal*8+(heal>0&&patient.Health<=5?40:0);
            bool purifies=CharacterBehaviors.FirstImplementer<LightDomainLogic>(owner)!=null;
            if(purifies)
            {
                foreach(var status in patient.Statuses)
                {
                    if(PlayerState.HolyFireAllows(status))continue;
                    int turns=Math.Min(2,patient.StatusTurnsLeft(status));
                    if(StatusNames.IsNegativeStatus(status))
                        value+=(status==StatusEffect.Frozen||status==StatusEffect.Stunned||status==StatusEffect.Charm?40:18)*turns;
                    else value-=25*turns;
                }
            }
            if(patient.HasStatus(StatusEffect.HolyLight))return value;
            if(CharacterBehaviors.FirstImplementer<KnowledgeDomainLogic>(owner)!=null)value+=15;
            if(CharacterBehaviors.FirstImplementer<WarDomainLogic>(owner)!=null&&patient.StatusTurnsLeft(StatusEffect.WarBlessing)<2)value+=20;
            if(CharacterBehaviors.FirstImplementer<NatureDomainLogic>(owner)!=null&&patient.StatusTurnsLeft(StatusEffect.NatureBlessing)<2)
            {
                foreach(var enemy in game.Players)
                    if(enemy.IsAlive&&!game.IsKnownAlly(owner,enemy)&&game.CanReach(enemy,patient)&&StatusRules.CanAttackTarget(enemy,patient,game))
                    {value+=22;break;}
            }
            if(CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(owner)!=null&&patient.StatusTurnsLeft(StatusEffect.LightBlessing)<2
                &&StatusRules.CanAct(patient))
            {
                foreach(var enemy in game.Players)
                    if(enemy.IsAlive&&!game.IsKnownAlly(owner,enemy)&&game.CanReach(patient,enemy)&&StatusRules.CanAttackTarget(patient,enemy,game))
                    {value+=22;break;}
            }
            return value;
        }
        private static PlayerState BestTeamClericHealTarget(PlayerState owner,GameManager game,int amount=9)
        {
            PlayerState best=owner;int value=int.MinValue;
            foreach(var patient in game.Players)
            {
                if(!patient.IsAlive||!game.IsKnownAlly(owner,patient))continue;
                int candidate=TeamClericHealValue(owner,patient,game,amount);
                if(candidate>value){best=patient;value=candidate;}
            }
            return best;
        }
        private int TeamClericHealingMoveBonus(PlayerState owner,int result,GameManager game)
        {
            if(!IsTeamClericAi(owner,game)||CharacterBehaviors.FirstImplementer<DeathDomainLogic>(owner)!=null
                ||DivineScoredEffect(owner,result,game)!=HandEffectType.Heal)return 0;
            var patient=BestTeamClericHealTarget(owner,game);
            int old=owner.Health>=owner.MaximumHealth?5:owner.Health<=HealPriorityHealth?120:30;
            int heal=NetClericHealingGain(patient,9);
            int desired=heal>0&&patient.Health<=HealPriorityHealth?120:heal>0?30:5;
            // Utility remains valuable at full health, while ordinary healing
            // is ranked using the recipient instead of the caster's health.
            int utility=TeamClericHealValue(owner,patient,game,9)-heal*8-(heal>0&&patient.Health<=5?40:0);
            return desired-old+utility;
        }
    }
}
