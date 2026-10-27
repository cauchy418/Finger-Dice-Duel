using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider:IClericDomainPolicy
    {
        public bool SwapTrickery(TrickeryTouch t,GameManager game)
        {
            int old=ClericDigitValue(t.Actor,t.Result,t.Effectless,game),next=ClericDigitValue(t.Actor,t.AddendValue,t.Effectless,game);
            int self=HandValueProfiles.For(t.Owner.Definition).WeightOf(t.Result)-HandValueProfiles.For(t.Owner.Definition).WeightOf(t.AddendValue);
            if(t.Addend.IsShield)self-=45;
            return self+(game.IsKnownAlly(t.Owner,t.Actor)?next-old:old-next)>0;
        }
        private static int ClericDigitValue(PlayerState actor,int value,bool pure,GameManager game)
        {
            int score=HandValueProfiles.For(actor.Definition).WeightOf(value);if(pure)return score;
            var effect=game.OperationEffect(actor,value);
            if(effect==HandEffectType.Heal&&StatusRules.CanHeal(actor))score+=System.Math.Min(9,actor.MaximumHealth-actor.Health)*12;
            if(effect==HandEffectType.Shield)score+=45;
            if(DivineChoices.IsAttack(effect))foreach(var target in game.Players)
                if(target.IsAlive&&!game.IsKnownAlly(actor,target)&&game.CanReach(actor,target)&&StatusRules.CanAttackTarget(actor,target,game))
                {score+=value*10;break;}
            return score;
        }
        public bool CharmTrickery(TrickeryTouch t,GameManager game)=>!game.IsKnownAlly(t.Owner,t.Actor)&&!t.Actor.HasStatus(StatusEffect.HolyLight);
        public Hand TempestHand(PlayerState owner,PlayerState attacker,IReadOnlyList<Hand> hands,GameManager game)
        {
            if(game.IsKnownAlly(owner,attacker)||owner.HasStatus(StatusEffect.Disabled))return null;
            if(attacker.HasImmunity(AttackDamageType.Lightning)&&attacker.HasImmunity(AttackDamageType.Thunder))return null;
            Hand best=null;int score=int.MinValue;
            foreach(var hand in hands){int candidate=0;foreach(var move in game.GetLegalMoves(owner))
                if(ReferenceEquals(move.ActedHand,hand))candidate=System.Math.Max(candidate,StormPreparation(owner,hand,move.TargetHand,game));
                if(candidate>score){score=candidate;best=hand;}}
            return best;
        }
        public Hand TempestAddend(PlayerState owner,Hand source,IReadOnlyList<Hand> targets,GameManager game)
        {Hand best=null;int score=int.MinValue;foreach(var target in targets){int gain=StormPreparation(owner,source,target,game);if(gain>score){score=gain;best=target;}}return best;}
        public AttackDamageType TempestType(PlayerState owner,PlayerState target,GameManager game)
        {
            int lightning=TempestValue(target,AttackDamageType.Lightning),thunder=TempestValue(target,AttackDamageType.Thunder);
            return lightning>=thunder?AttackDamageType.Lightning:AttackDamageType.Thunder;
        }
        private static int TempestValue(PlayerState target,AttackDamageType type)
        {if(target.HasImmunity(type))return -1000;int score=40;
         if(target.HasResistance(type))score-=20;if(target.HasVulnerability(type))score+=20;
         if(StatusRules.HasSonicCrossReaction(target,type))score+=25;
         if(type==AttackDamageType.Thunder&&target.HasStatus(StatusEffect.Resonance))score+=15;
         if(type==AttackDamageType.Lightning&&target.HasStatus(StatusEffect.Wet))score+=15;return score;}
    }
}
