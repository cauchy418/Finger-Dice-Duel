using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private static double DivineTargetScore(string key,PlayerState owner,PlayerState target,GameManager game)
        {
            bool ally=game.IsKnownAlly(owner,target);
            if(key==DivineChoices.ConquestAura)
            {
                if(ally||target.HasStatus(StatusEffect.HolyLight)||!OathOfConquestLogic.CanPanic(owner,target))return -100;
                if(target.StatusTurnsLeft(StatusEffect.Panic)>=2&&ReferenceEquals(target.StatusSourceOf(StatusEffect.Panic),owner))return -5;
                bool threatensAlly=false;
                foreach(var friend in game.Players)
                    if(friend.IsAlive&&!ReferenceEquals(friend,owner)&&game.IsKnownAlly(owner,friend)&&game.CanReach(target,friend)
                        &&StatusRules.CanAttackTarget(target,friend,game)){threatensAlly=true;break;}
                return 30+(game.CanReach(target,owner)?20:0)+(threatensAlly?10:0)+Math.Max(0,StatusRules.EffectiveAttackBonus(target))*4;
            }
            if(key==DivineChoices.Blessing)
            {
                if(!ally||target.HasStatus(StatusEffect.HolyLight))return -100;
                int turns=target.StatusTurnsLeft(StatusEffect.WarBlessing);if(turns>=2)return -5;
                int attacks=0;foreach(var hand in target.Hands)if(DivineChoices.AttackDigit(target,hand.Value))attacks++;
                return 25+attacks*8+(ReferenceEquals(owner,target)?5:0)+(target.Health<10?8:0)-turns*8;
            }
            if(ally)return -100;
            double score=25+(game.CanReach(owner,target)?25:0)+Math.Max(0,20-target.Health);
            var vow=CharacterBehaviors.FirstImplementer<IVowOfEnmity>(owner);
            if(ReferenceEquals(vow?.VowTarget,target))score+=8;
            return score;
        }
        private static double DivineOptionScore(string key,int value,PlayerState owner,PlayerState target,GameManager game)
        {
            if(key==DivineChoices.Retarget)
            {
                if(value==0)return 0;
                if(target==null||game.IsKnownAlly(owner,target))return -100;
                var current=CharacterBehaviors.FirstImplementer<IVowOfEnmity>(owner)?.VowTarget;
                if(current==null||!current.IsAlive||game.IsKnownAlly(owner,current))return 50;
                return DivineTargetScore(DivineChoices.Vow,owner,target,game)-DivineTargetScore(DivineChoices.Vow,owner,current,game);
            }
            if(value==0)
            {
                var patient=owner;
                if(IsTeamClericAi(owner,game))patient=BestTeamClericHealTarget(owner,game);
                else if(game.Players.Count>2&&CharacterBehaviors.FirstImplementer<LifeDomainLogic>(owner)!=null)
                    patient=LifeDomainLogic.BestHealTargetFor(owner,game);
                int heal=NetClericHealingGain(patient,9);
                int utility=IsTeamClericAi(owner,game)?TeamClericHealValue(owner,patient,game,9)-heal*8-(heal>0&&patient.Health<=5?40:0):0;
                return heal*8+(heal>0&&patient.Health<=8?40:0)+utility;
            }
            double best=-100;
            foreach(var enemy in game.Players)
            {
                if(!enemy.IsAlive||game.IsKnownAlly(owner,enemy)||!game.CanReach(owner,enemy)||!StatusRules.CanAttackTarget(owner,enemy,game))continue;
                if(FeyCanCancel(enemy,9)||(enemy.Character as IAttackTargetRule)?.IsImmuneToAttack(enemy)==true)continue;
                int damage=EstimateAttackDamage(owner,enemy,9,game);
                double score=Math.Max(0,damage)*8+(damage>=enemy.HealthAcrossForms?100:0);
                if(enemy.LeftHand.IsShield||enemy.RightHand.IsShield)score-=25;
                best=Math.Max(best,score);
            }
            return best;
        }
        private static HandEffectType DivineScoredEffect(PlayerState actor,int value,GameManager game)
        {
            var effect=CharacterBehaviors.DisplayedEffectOf(actor,value);
            if(game!=null&&value==9&&effect==HandEffectType.Heal&&actor.HasStatus(StatusEffect.WarBlessing)
                &&DivineOptionScore(DivineChoices.Nine,1,actor,null,game)>DivineOptionScore(DivineChoices.Nine,0,actor,null,game))return HandEffectType.Attack8;
            return effect;
        }
        private int PaladinMoveBonus(PlayerState actor,LegalMove move,int result,GameManager game)
        {
            int other=actor.GetOtherHand(move.ActedHand).Value,score=0;
            bool devotion=CharacterBehaviors.FirstImplementer<OathOfDevotionLogic>(actor)!=null;
            bool paladin=PaladinGuardianAura.IsPaladin(actor);
            if(paladin)
            {
                bool available=result==5||other==5||devotion&&(result==4||result==6||other==4||other==6);
                if(available)foreach(var ally in game.Players)
                    if(!ReferenceEquals(actor,ally)&&ally.IsAlive&&game.IsKnownAlly(actor,ally)&&game.CanReach(actor,ally))
                    {score+=ally.Health<=8?24:10;break;}
            }
            var conquest=CharacterBehaviors.FirstImplementer<OathOfConquestLogic>(actor);
            if(conquest!=null)
            {
                bool attack=IsAttackEffect(DivineScoredEffect(actor,result,game));
                if(!actor.BeganAttackThisTurn&&!attack)score-=(conquest.AttackStacks+conquest.DefenseStacks)*8;
                if(move.ActedHand.IsShield)
                    foreach(var target in OathOfConquestLogic.AuraCandidates(actor,game))
                        if(DivineTargetScore(DivineChoices.ConquestAura,actor,target,game)>0){score+=20;break;}
            }
            if(IsAttackEffect(DivineScoredEffect(actor,result,game)))
            {
                var victim=ResolveIntendedVictim(move,actor,game,result);
                if(victim!=null&&!game.IsKnownAlly(actor,victim))
                {
                    if(victim.HasAnyShield&&CharacterBehaviors.FirstImplementer<OathOfConquestLogic>(victim)!=null
                        &&OathOfConquestLogic.CanPanic(victim,actor)&&!actor.HasStatus(StatusEffect.HolyLight))score-=16;
                    foreach(var guard in game.Players)
                        if(game.IsKnownAlly(victim,guard)&&!game.IsKnownAlly(actor,guard)
                            &&PaladinGuardianAura.Eligible(guard,actor,victim,game)&&guard.HasAnyShield){score-=12;break;}
                }
            }
            return score;
        }
    }
}
