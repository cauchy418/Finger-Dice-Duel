using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private int StateMoveAdjustment(LegalMove move,PlayerState actor,int result,GameManager game,bool effectless=false)
        {
            if(game==null||actor==null)return 0;
            var effect=effectless?HandEffectType.None:DivineScoredEffect(actor,result,game);
            var victim=ResolveIntendedVictim(move,actor,game,result);
            bool canAttack=victim!=null&&victim.IsAlive&&!game.IsKnownAlly(actor,victim)&&game.CanReach(actor,victim)&&StatusRules.CanAttackTarget(actor,victim,game)&&(victim.Character as IAttackTargetRule)?.IsImmuneToAttack(victim)!=true;
            bool lifeHealing=!effectless&&IsAttackEffect(effect)&&WantsLifeHealing(actor,victim,result,game);
            if(lifeHealing)effect=HandEffectType.Heal;
            int damage=canAttack&&IsAttackEffect(effect)?EstimateAttackDamage(actor,victim,result,game,move.ActedHand.Value,move.TargetHand):0;
            int heal=0;
            if(effect==HandEffectType.Heal)
            {
                int healAmount=lifeHealing?result:9;
                var planned=CharacterBehaviors.FirstImplementer<DeathDomainLogic>(actor)!=null?BestDeathHealTarget(actor,game)
                    :CharacterBehaviors.FirstImplementer<LifeDomainLogic>(actor)!=null?LifeDomainLogic.BestHealTargetFor(actor,game,healAmount)
                    :IsTeamClericAi(actor,game)?BestTeamClericHealTarget(actor,game,healAmount)
                    :CharacterBehaviors.FirstImplementer<IHealTargetAdvisor>(actor)?.BestHealTarget(actor,game)??actor;
                victim=planned;
                if(!game.IsKnownAlly(actor,planned)&&DeathEnemyControlValue(actor,planned,game)>0)heal=1;
                foreach(var patient in game.Players)
                {
                    if(!patient.IsAlive||!ReferenceEquals(patient,planned)||!game.IsKnownAlly(actor,patient))continue;
                    bool cleanse=CharacterBehaviors.FirstImplementer<LifeDomainLogic>(actor)!=null;
                    int support=IsClericAi(actor)?ClericHealingSupportValue(actor,patient,game):0;
                    bool recovery=StatusRules.CanHeal(patient)||cleanse;
                    if(!recovery&&support==0)continue;
                    int amount=recovery?Math.Min(healAmount,patient.MaximumHealth-patient.Health):0;
                    if(patient.HasStatus(StatusEffect.Scorch)&&!cleanse)amount-=(amount+1)/2;
                    heal=Math.Max(heal,amount);
                    // A cleric's full-health healing can still cleanse or grant a blessing.
                    if(support>0)heal=Math.Max(heal,1);
                }
            }
            return AiStatePolicy.ScoreMove(new AiStateContext(actor,victim,game,move,result,effect,damage,heal,canAttack));
        }
        private bool WantsLifeHealing(PlayerState actor,PlayerState target,int result,GameManager game)
        {
            if(actor==null||game==null||CharacterBehaviors.FirstImplementer<LifeDomainLogic>(actor)==null)return false;
            if(target?.IsAlive==true&&!game.IsKnownAlly(actor,target)&&!target.HasAnyShield
                &&!target.HasStatus(StatusEffect.FeignedDeath)&&game.CanReach(actor,target)
                &&StatusRules.CanAttackTarget(actor,target,game)&&!FeyCanCancel(target,result)
                &&(target.Character as IAttackTargetRule)?.IsImmuneToAttack(target)!=true)
            {
                int remaining=0;bool grave=false;
                foreach(var enemy in game.Players)if(enemy.IsAlive&&!game.IsKnownAlly(actor,enemy))remaining++;
                foreach(var part in DruidWildShape.Parts(target.PersistentCharacter))
                    if(part is IGraveStrength strength&&!strength.GraveSpent)grave=true;
                // A winning attack ends the danger; the deferred heal choice must
                // agree with the move scorer's immediate-win preference.
                if(remaining==1&&!grave&&EstimateAttackDamage(actor,target,result,game)
                    >=target.HealthAcrossForms+target.SporeLife+target.FalseLife)return false;
            }
            bool danger=actor.Health*100<=actor.MaximumHealth*LifeDomainHealHealthPercent;
            foreach(var ally in game.Players)
                if(ally.IsAlive&&game.IsKnownAlly(actor,ally)&&(ally.Health*100<=ally.MaximumHealth*LifeDomainHealHealthPercent
                    ||ally.HasStatus(StatusEffect.Frozen)||ally.HasStatus(StatusEffect.Disabled)||ally.HasStatus(StatusEffect.Stunned)
                    ||ally.HasStatus(StatusEffect.Charm)||ally.HasStatus(StatusEffect.Decay)||ally.HasStatus(StatusEffect.Tinnitus)||ally.HasStatus(StatusEffect.HolyLight)))danger=true;
            if(!danger&&target!=null&&!actor.HasStatus(StatusEffect.BlessedHealing)&&actor.MaximumHealth-actor.Health<result)
                danger=EstimateAttackDamage(actor,target,result,game)<=1||target.HasAnyShield&&CharacterFactory.MaxAttackHits(actor)==1;
            return danger;
        }
        private static bool IsClericAi(PlayerState actor)=>CharacterBehaviors.FirstImplementer<ClericTemplate>(actor)!=null||CharacterBehaviors.FirstImplementer<LifeDomainLogic>(actor)!=null;
        private static int ClericHealingSupportValue(PlayerState actor,PlayerState patient,GameManager game)
        {
            // Effects can be blocked by holy fire. Knowledge stacks are not a timed blessing.
            if(CharacterBehaviors.FirstImplementer<LifeDomainLogic>(actor)!=null)
            {
                foreach(var status in patient.Statuses)if(StatusNames.IsNegativeStatus(status)||status==StatusEffect.HolyLight)return 1;
                if(patient.MaximumHealth-patient.Health<9&&patient.StatusTurnsLeft(StatusEffect.BlessedHealing)<2)return 1;
            }
            if(patient.HasStatus(StatusEffect.HolyLight))return 0;
            if(CharacterBehaviors.FirstImplementer<KnowledgeDomainLogic>(actor)!=null)return 1;
            if(CharacterBehaviors.FirstImplementer<WarDomainLogic>(actor)!=null&&patient.StatusTurnsLeft(StatusEffect.WarBlessing)<2)return 1;
            if(CharacterBehaviors.FirstImplementer<NatureDomainLogic>(actor)!=null&&patient.StatusTurnsLeft(StatusEffect.NatureBlessing)<2)return 1;
            if(CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(actor)!=null&&patient.StatusTurnsLeft(StatusEffect.LightBlessing)<2)return 1;
            if(CharacterBehaviors.FirstImplementer<DeathDomainLogic>(actor)!=null&&WantsDeathBlessing(actor,patient,game))return 1;
            if(CharacterBehaviors.FirstImplementer<LightDomainLogic>(actor)!=null)
                foreach(var status in patient.Statuses)if(!PlayerState.HolyFireAllows(status))return 1;
            return 0;
        }
    }
}
