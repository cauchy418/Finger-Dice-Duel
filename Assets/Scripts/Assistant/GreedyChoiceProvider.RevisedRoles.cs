using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private static double MonkOptionScore(string key,int value,PlayerState owner,PlayerState target,GameManager game)
        {
            if(value==0)return 1;
            if(owner.MonkKi==0)return -100;
            if(key==MonkRules.Harmony)return owner.Health<owner.MaximumHealth&&!owner.HasStatus(StatusEffect.Decay)?30:-10;
            if(key==MonkRules.Drunken)return owner.MonkKi>=2&&!owner.HasStatus(StatusEffect.HolyLight)?15:-10;
            if(key==MonkRules.Defense)return 8+(owner.Health<=10?8:0);
            if(key==MonkRules.Ninja)
            {
                if(target==null)return 0;
                int delta=StatusRules.EffectiveAttackBonus(owner)-StatusRules.EffectiveDefenseBonus(target);
                bool shield=target.LeftHand.IsShield||target.RightHand.IsShield;
                return shield?20:delta>0?10:delta<0?-10:owner.MonkKi>=4?3:-2;
            }
            if(key==MonkRules.Taichi)
            {
                if(target==null)return value==1&&owner.Health<=8?20:-2;
                int danger=Math.Max(1,8+StatusRules.EffectiveAttackBonus(target)-StatusRules.EffectiveDefenseBonus(owner));
                if(value==1)return owner.Health<=danger/2+2?40:owner.MonkKi>=4?4:-2;
                if(value==2)return target.Health<=danger&&owner.Health>danger/2?30:-3;
            }
            return 0;
        }
        private static int RevisedRoleMoveBonus(PlayerState actor,LegalMove move,int result,GameManager game)
        {
            int score=0;int other=actor.GetOtherHand(move.ActedHand).Value;
            if(CharacterBehaviors.FirstImplementer<IMonkResource>(actor)!=null&&result==other&&actor.MonkKi<6)score+=actor.MonkKi<2?40:22;
            var book=CharacterBehaviors.FirstImplementer<IPortentBook>(actor);
            if(book!=null)
            {
                if(result==7&&!book.Portents.ContainsValue(other))score+=35;
                if(book.Portents.Count>0&&(result==7||other==7))score+=25;
                if(book.Portents.Count>0&&move.ActedHand.Value==7&&other!=7&&result!=7)score-=25;
            }
            var enemy=game.OwnerOfHand(move.TargetHand);
            if(enemy!=null&&!game.IsKnownAlly(actor,enemy))
            {
                var enemyBook=CharacterBehaviors.FirstImplementer<IPortentBook>(enemy);
                if(enemyBook!=null&&enemyBook.Portents.Count>0&&(enemy.LeftHand.Value==7||enemy.RightHand.Value==7))score-=10;
                if(MonkRules.DrunkenOf(enemy)!=null&&enemy.MonkKi>0&&result==9)score-=4;
            }
            return score;
        }
    }
    internal static class PortentCollectionExtensions
    {
        public static bool ContainsValue(this IReadOnlyCollection<int> values,int value){foreach(int n in values)if(n==value)return true;return false;}
    }
}
