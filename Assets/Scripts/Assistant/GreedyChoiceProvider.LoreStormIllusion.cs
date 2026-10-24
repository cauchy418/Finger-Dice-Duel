using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : INewSubclassChoicePolicy
    {
        private Hand _stormPlannedSource;
        private static int StormPreparation(PlayerState owner,Hand source,Hand target,GameManager game)
        {
            int value=StatusRules.ApplyResultModifiers(owner,Hand.ComputeSum(source.Value,target.Value));
            var profile=HandValueProfiles.For(owner.Definition);var other=ReferenceEquals(source,owner.LeftHand)?owner.RightHand:owner.LeftHand;
            int score=profile.WeightOf(value)-profile.WeightOf(source.Value);
            if(source.IsShield)score-=60;
            foreach(var book in CharacterBehaviors.Implementers<ILoreRecord>(owner))if(!book.HasRecord(value))score+=4;
            foreach(var enemy in game.Players)
            {
                if(!enemy.IsAlive||ReferenceEquals(enemy,owner)||game.IsTeammate(owner,enemy))continue;
                foreach(var h in enemy.Hands)
                    if(game.IsLegalMove(owner,source,h,out _))
                    {
                        int next=Hand.ComputeSum(value,h.Value);score+=next==8?5:next==6?4:next==4?3:next==5?2:0;
                    }
            }
            if(CharacterBehaviors.FirstImplementer<IOperationAddendIllusion>(owner)!=null&&value==2)score+=20;
            return score;
        }
        public Hand ChooseNewSubclassHand(string key,PlayerState owner,IReadOnlyList<Hand> hands,GameManager game)
        {
            Hand best=null;int score=0;
            if(key==NewSubclassChoices.StormSource)
            {
                foreach(var move in game.GetLegalMoves(owner))
                {int v=StormPreparation(owner,move.ActedHand,move.TargetHand,game);if(v>score){score=v;best=move.ActedHand;}}
                _stormPlannedSource=best;return best;
            }
            var source=_stormPlannedSource;if(source==null||!owner.Owns(source))return null;
            foreach(var target in hands){int v=StormPreparation(owner,source,target,game);if(v>score){score=v;best=target;}}
            _stormPlannedSource=null;return best;
        }
    }
}
