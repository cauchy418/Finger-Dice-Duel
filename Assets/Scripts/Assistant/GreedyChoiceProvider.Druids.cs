using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : IDruidChoicePolicy
    {
        private static int DruidScoredResult(LegalMove move,PlayerState actor,GameManager game)
        {
            int result=Hand.ComputeSum(move.ActedHand.Value,move.TargetHand.Value);
            if(game!=null&&DruidForms.IsDragon(actor))
            {
                var hands=new List<Hand>();foreach(var next in game.GetLegalMoves(actor))if(ReferenceEquals(next.ActedHand,move.ActedHand))hands.Add(next.TargetHand);
                var second=game.DruidSecondAddend??game.GetDruidPolicy(actor)?.SecondAddend(actor,move.ActedHand,move.TargetHand,hands,game);
                if(second!=null)result=Hand.ComputeSum(result,second.Value);
            }
            return result;
        }
        public int ChooseForm(PlayerState owner,string circle,IReadOnlyList<SkillChoiceOption> options,GameManager game)
        {
            if(!StatusRules.CanAct(owner))return 0;
            if(circle=="moon")
            {
                int pick=0;double best=owner.Health>=16?18:0;
                foreach(var option in options)
                {
                    int f=option.Value;if(f==0)continue;int range=f==3?3:f==5?2:1;
                    int cost=f>=5?2:1;if(cost>owner.WildCharges)continue;
                    double score=(DruidWildShape.Health[f]-owner.Health)*1.2+8-(cost-1)*5;
                    bool reachable=false;foreach(var enemy in game.OpponentsOf(owner))if(enemy.IsAlive&&game.DistanceBetween(owner,enemy)<=range)
                    {reachable=true;var type=f==4?AttackDamageType.Poison:f==3?AttackDamageType.Piercing:AttackDamageType.Slashing;score+=enemy.HasImmunity(type)?-30:enemy.HasResistance(type)?-8:5;}
                    if(!reachable)score-=35;
                    if(f==5)score+=10;if(f==6)score+=8;if(f==1&&(owner.LeftHand.Value==4||owner.RightHand.Value==4))score+=8;
                    if(f==3&&(owner.LeftHand.Value==6||owner.RightHand.Value==6))score-=12;
                    if(score>best){best=score;pick=f;}
                }
                return pick;
            }
            int missing=0;foreach(var p in game.Players)if(p.IsAlive&&game.IsKnownAlly(owner,p))missing=Math.Max(missing,p.MaximumHealth-p.Health);
            if(missing>=7 && game.GetLegalMoves(owner).Count>0)return 2;
            if(owner.Health<=9)return 3;
            foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&!p.HasResistance(AttackDamageType.Radiant))return 1;
            return 3;
        }
        public bool ExitForm(PlayerState owner,IDruidForm form,GameManager game)
        {
            if(owner.IsWildShaped&&owner.Health<=3&&owner.WildOriginalHealth>owner.Health+5)return true;
            if(form.CircleKey=="moon"&&form.FormValue==3)
            {foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&game.DistanceBetween(owner,p)<=1&&owner.LeftHand.Value==6)return true;}
            return false;
        }
        public PlayerState ChaliceTarget(PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game)
        {
            PlayerState best=null;int score=0;foreach(var p in targets)
            {if(!game.IsKnownAlly(owner,p)||!StatusRules.CanHeal(p))continue;int need=p.MaximumHealth-p.Health;if(need>score){best=p;score=need;}}
            return best;
        }
        public Hand SecondAddend(PlayerState owner,Hand acted,Hand first,IReadOnlyList<Hand> candidates,GameManager game)
        {
            Hand best=null;int bestScore=int.MinValue;
            int intermediate=Hand.ComputeSum(acted.Value,first.Value);
            foreach(var hand in candidates)
            {
                int result=StatusRules.ApplyResultModifiers(owner,Hand.ComputeSum(intermediate,hand.Value));
                var effect=CharacterBehaviors.DisplayedEffectOf(owner,result);int score=result;
                if(effect==HandEffectType.Attack4||effect==HandEffectType.Attack6||effect==HandEffectType.Attack8)score+=result*10;
                if(effect==HandEffectType.Heal)score+=Math.Min(9,owner.MaximumHealth-owner.Health)*10;
                if(effect==HandEffectType.Shield&&!acted.IsShield)score+=40;
                if(score>bestScore){bestScore=score;best=hand;}
            }
            return best;
        }
    }
}
