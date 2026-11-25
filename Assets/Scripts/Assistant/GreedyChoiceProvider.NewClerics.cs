using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        // Healing an enemy is useful only when the two lost actions buy a real recovery
        // window or let teammates fight another enemy. The last enemy cannot be killed
        // during feigned death, so ordinary duels must keep making offensive progress.
        private static int DeathEnemyControlValue(PlayerState healer,PlayerState patient,GameManager game,int healAmount=9)
        {
            if(game==null||patient?.IsAlive!=true||game.IsKnownAlly(healer,patient)
                ||patient.HasStatus(StatusEffect.HolyLight)||patient.HasStatus(StatusEffect.FeignedDeath)
                ||!StatusRules.CanAct(patient)||game.WillSuppressOperationEffects(patient))return 0;
            int enemies=0,pressure=0;
            foreach(var p in game.Players)
            {
                if(p.IsAlive&&!game.IsKnownAlly(healer,p))enemies++;
                if(p.IsAlive&&game.IsKnownAlly(healer,p))pressure=Math.Max(pressure,DeathAttackThreat(patient,p,game));
            }
            int recovery=healer.MaximumHealth-healer.Health;
            if(enemies<=1&&(recovery<9||pressure<healer.HealthAcrossForms
                ||patient.Health<patient.MaximumHealth-4))return 0;
            int heal=HealingGain(patient,healAmount);
            int value=pressure*12-heal*10-(enemies<=1?30:10);
            return Math.Max(0,value);
        }
        private static int DeathAttackThreat(PlayerState enemy,PlayerState patient,GameManager game)
        {
            if(!StatusRules.CanAct(enemy)||enemy.HasStatus(StatusEffect.FeignedDeath)
                ||game.WillSuppressOperationEffects(enemy)||!game.CanReach(enemy,patient)
                ||!StatusRules.CanAttackTarget(enemy,patient,game))return 0;
            int strongest=0;
            foreach(var move in game.GetLegalMoves(enemy))
            {
                int result=game.PreviewOperationResult(enemy,move.ActedHand,move.TargetHand);
                if(!IsAttackEffect(DivineScoredEffect(enemy,result,game))||FeyCanCancel(patient,result))continue;
                if(patient.HasAnyShield&&CharacterFactory.MaxAttackHits(enemy)==1
                    &&CharacterBehaviors.FirstImplementer<IPiercesMitigation>(enemy)==null)continue;
                strongest=Math.Max(strongest,EstimateAttackDamage(enemy,patient,result,game,move.ActedHand.Value,move.TargetHand)*CharacterFactory.MaxAttackHits(enemy));
            }
            return strongest;
        }
        private static PlayerState BestDeathHealTarget(PlayerState owner,GameManager game)
        {
            PlayerState best=CharacterBehaviors.FirstImplementer<IHealTargetAdvisor>(owner)?.BestHealTarget(owner,game)??owner;
            int gain=HealingGain(best,9);
            int bestValue=gain*8+(gain>0&&best.Health<=5?25:0);
            foreach(var patient in game.Players)
            {
                int value=DeathEnemyControlValue(owner,patient,game);
                if(value>bestValue){best=patient;bestValue=value;}
            }
            return best;
        }
        private static int DeathHealingMoveBonus(PlayerState owner,int result,GameManager game)
        {
            if(game==null||CharacterBehaviors.FirstImplementer<DeathDomainLogic>(owner)==null
                ||DivineScoredEffect(owner,result,game)!=HandEffectType.Heal)return 0;
            var patient=BestDeathHealTarget(owner,game);
            if(!game.IsKnownAlly(owner,patient))return DeathEnemyControlValue(owner,patient,game);
            return 0;
        }
        private static int LightBlessingPreparationValue(PlayerState owner,int result,GameManager game)
        {
            if(game==null||game.Players.Count!=2||CharacterBehaviors.FirstImplementer<RadiantLightDomainLogic>(owner)==null
                ||DivineScoredEffect(owner,result,game)!=HandEffectType.Heal||owner.HasStatus(StatusEffect.HolyLight)
                ||owner.HasStatus(StatusEffect.LightBlessing))return 0;
            foreach(var enemy in game.OpponentsOf(owner))
                if(enemy.IsAlive&&game.CanReach(owner,enemy)&&StatusRules.CanAttackTarget(owner,enemy,game)
                    &&!enemy.HasAnyShield&&!enemy.HasStatus(StatusEffect.FeignedDeath))
                {
                    if(enemy.HasImmunity(AttackDamageType.Radiant))return 110;
                    if(enemy.HasResistance(AttackDamageType.Radiant))return 35;
                }
            return 0;
        }
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
            bool grave=false;
            foreach(var part in DruidWildShape.Parts(target.PersistentCharacter))
                if(part is IGraveStrength strength&&!strength.GraveSpent)grave=true;
            // Removing control has no future cost when the guaranteed extra damage
            // alone finishes the target. Shields and death prevention invalidate that.
            if(bonus>0&&!grave&&!target.HasAnyShield&&!target.HasStatus(StatusEffect.FeignedDeath)
                &&bonus>=target.HealthAcrossForms+target.SporeLife+target.FalseLife)return true;
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
