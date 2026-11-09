using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private static double PortentOptionScore(int value,PlayerState owner,PlayerState actor,GameManager game)
        {
            if(value<0)return 1;
            if(actor==null||game==null)return -100;
            double difference=PortentOutcomeValue(actor,value,game)-PortentOutcomeValue(actor,game.CurrentPortentResult,game);
            var book=CharacterBehaviors.FirstImplementer<IPortentBook>(owner);
            int growthCost=book?.Portents.Count>0&&book.Portents.Count%3==0?2:0;
            return (ReferenceEquals(owner,actor)||game.IsKnownAlly(owner,actor)?difference:-difference)-1-growthCost;
        }
        private static double PortentOutcomeValue(PlayerState actor,int value,GameManager game)
        {
            var effect=DivineScoredEffect(actor,value,game);
            if(effect==HandEffectType.Heal)
                return StatusRules.CanHeal(actor)?Math.Min(9,actor.MaximumHealth-actor.Health):0;
            if(effect==HandEffectType.Shield)return actor.HasStatus(StatusEffect.Tinnitus)?0:5;
            if(!IsAttackEffect(effect))return 0;
            double best=0;
            foreach(var enemy in game.OpponentsOf(actor))
            {
                if(!enemy.IsAlive||game.IsKnownAlly(actor,enemy)||!game.CanReach(actor,enemy)
                    ||!StatusRules.CanAttackTarget(actor,enemy,game)||FeyCanCancel(enemy,value)
                    ||(enemy.Character as IAttackTargetRule)?.IsImmuneToAttack(enemy)==true)continue;
                int damage=EstimateAttackDamage(actor,enemy,value,game);
                // Portent is scarce: a fully blocked normal hit only breaks a shield.
                bool shield=enemy.LeftHand.IsShield||enemy.RightHand.IsShield;
                best=Math.Max(best,shield?Math.Min(2,damage):damage);
            }
            return best;
        }
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
                if(result==7&&!book.Portents.ContainsValue(other))score+=book.Portents.Count % 3 == 2 ? 60 : 40;
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
