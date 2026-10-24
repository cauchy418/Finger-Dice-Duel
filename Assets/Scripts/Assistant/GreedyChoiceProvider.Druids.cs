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
                if(owner.Health<=7)return 1;
                bool near=false;foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&game.DistanceBetween(owner,p)<=1)near=true;
                if(!near)return 3;
                foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&!p.HasResistance(AttackDamageType.Poison)&&StatusRules.EffectiveDefenseBonus(p)>=2)return 4;
                return owner.Health<owner.MaximumHealth?2:4;
            }
            int missing=0;foreach(var p in game.Players)if(p.IsAlive&&game.IsTeammate(owner,p))missing=Math.Max(missing,p.MaximumHealth-p.Health);
            if(missing>=7 && game.GetLegalMoves(owner).Count>0)return 2;
            if(owner.Health<=9)return 3;
            foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&!p.HasResistance(AttackDamageType.Radiant))return 1;
            return 3;
        }
        public bool ExitForm(PlayerState owner,IDruidForm form,GameManager game)
        {
            if(owner.LeftHand.Value!=owner.RightHand.Value)return false;
            if(form.CircleKey=="moon"&&form.FormValue==3)
            {foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&game.DistanceBetween(owner,p)<=1&&owner.LeftHand.Value==6)return true;}
            return false;
        }
        public PlayerState ChaliceTarget(PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game)
        {
            PlayerState best=null;int score=0;foreach(var p in targets)
            {if(!game.IsTeammate(owner,p)||p.HasStatus(StatusEffect.Decay))continue;int need=p.MaximumHealth-p.Health;if(need>score){best=p;score=need;}}
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
