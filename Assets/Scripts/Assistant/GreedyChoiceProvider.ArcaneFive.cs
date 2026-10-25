using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider:IArcaneChoicePolicy
    {
        public PlayerState ChooseArcaneTarget(string key,PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game)
        {
            PlayerState best=null;double bestScore=0;
            foreach(var target in targets)
            {
                bool ally=AncientAlly(game,owner,target);double score;
                if(key==ArcaneChoices.Transmute)
                {
                    if(target.HasStatus(StatusEffect.HolyLight))continue;
                    if(ally)score=target.Health<=5&&!target.IsPolymorphed?15+(5-target.Health)*3:-100;
                    else score=target.IsPolymorphed?-10:20+Math.Max(0,StatusRules.EffectiveAttackBonus(target))*5
                        +(target.Health>10?10:target.Health<5?-30:0);
                }
                else
                {
                    if(ally||target.HasStatus(StatusEffect.HolyLight)||!StatusRules.CanAttackTarget(owner,target,game))continue;
                    score=20+Math.Max(0,20-target.Health)*2+(game.CanReach(owner,target)?15:0);
                    var curse=CharacterBehaviors.FirstImplementer<IHexbladeCurse>(owner);
                    if(ReferenceEquals(curse?.CurseTarget,target))score+=curse.CurseTurns<2?8:-15;
                }
                if(score>bestScore){bestScore=score;best=target;}
            }
            return best;
        }
        private static double ArcaneOptionScore(string key,int value,PlayerState owner,PlayerState target,GameManager game)
        {
            if(key==ArcaneChoices.Enchantment)
            {
                if(value==0)return 0;
                if(target==null||!target.IsAlive||AncientAlly(game,owner,target)||target.HasStatus(StatusEffect.HolyLight))return -100;
                if(target.HasStatus((StatusEffect)value))return -10;
                if(value==(int)StatusEffect.Disabled)return 20+Math.Max(0,StatusRules.EffectiveAttackBonus(target))*4;
                return target.HasStatus(StatusEffect.Frozen)?-5:18;
            }
            if(key==ArcaneChoices.KnightCopy)
            {
                // Copy is free and retains the previous enchantment only when declined.
                return value==1?10:0;
            }
            if(value==0)return 0;
            double total=0;int n=0;
            foreach(var enemy in game.OpponentsOf(owner))
                if(enemy.IsAlive&&game.CanReach(owner,enemy))
                {total+=ElementScore(value,owner,enemy,game);n++;}
            return n>0?total/n+10:5;
        }
        private static int ArcaneMoveBonus(PlayerState owner,LegalMove move,int result,GameManager game)
        {
            int bonus=0;
            var song=CharacterBehaviors.FirstImplementer<IBladesong>(owner);
            var effect=CharacterBehaviors.DisplayedEffectOf(owner,result);
            if(song!=null)
            {
                int other=owner.GetOtherHand(move.ActedHand).Value;
                int hp=effect==HandEffectType.Heal?Math.Min(owner.MaximumHealth,owner.Health+9):owner.Health;
                if(!song.Singing&&result+other==hp)bonus+=70;
                if(song.Singing&&(effect==HandEffectType.Attack4||effect==HandEffectType.Attack6||effect==HandEffectType.Attack8
                    ||effect==HandEffectType.Shield||effect==HandEffectType.Heal&&owner.Health<owner.MaximumHealth))bonus+=40;
            }
            if(owner.Definition?.Id==CharacterDefinition.SchoolOfTransmutationId)
            {
                int other=owner.GetOtherHand(move.ActedHand).Value;
                foreach(var target in game.Players)
                    if(target.IsAlive&&!game.IsKnownAlly(owner,target)&&!target.IsPolymorphed&&!target.HasStatus(StatusEffect.HolyLight)
                        &&(target.LeftHand.Value==result&&target.RightHand.Value==other||target.LeftHand.Value==other&&target.RightHand.Value==result))bonus+=40;
            }
            return bonus;
        }
    }
}
