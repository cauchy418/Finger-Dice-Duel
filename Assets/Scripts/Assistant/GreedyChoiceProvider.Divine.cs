using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private static double DivineTargetScore(string key,PlayerState owner,PlayerState target,GameManager game)
        {
            bool ally=game.IsKnownAlly(owner,target);
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
                int heal=owner.HasStatus(StatusEffect.Decay)?0:Math.Min(9,owner.MaximumHealth-owner.Health);
                if(owner.HasStatus(StatusEffect.HolyLight))heal=(heal+1)/2;
                return heal*8+(heal>0&&owner.Health<=8?40:0);
            }
            double best=-100;
            foreach(var enemy in game.Players)
            {
                if(!enemy.IsAlive||game.IsKnownAlly(owner,enemy)||!game.CanReach(owner,enemy)||!StatusRules.CanAttackTarget(owner,enemy,game))continue;
                int damage=StatusRules.EffectiveAttackBonus(owner)-StatusRules.EffectiveDefenseBonus(enemy)+9+ArcaneChoices.BonusAgainst(owner,enemy);
                if(owner.HasStatus(StatusEffect.Disabled))damage=0;
                double score=Math.Max(0,damage)*8+(damage>=enemy.Health?100:0);
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
            return CharacterBehaviors.FirstImplementer<IDruidForm>(actor)!=null?effect:Hand.GetEffectType(value);
        }
    }
}
