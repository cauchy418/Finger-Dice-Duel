using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : IWizardChoicePolicy
    {
        public PlayerState ResistanceTarget(PlayerState owner, GameManager game)
        {
            var best=owner;
            foreach(var p in game.Players)
                if(p.IsAlive && (ReferenceEquals(p,owner)||game.IsKnownAlly(owner,p))
                    && (best==null || p.Health<best.Health)) best=p;
            return best;
        }
        public AttackDamageType ResistanceType(PlayerState owner, PlayerState target, GameManager game)
        {
            var weights=new System.Collections.Generic.Dictionary<AttackDamageType,int>();
            foreach(var enemy in game.OpponentsOf(owner))
            {
                if(!enemy.IsAlive || !StatusRules.CanAct(enemy) || !game.CanReach(enemy,target)) continue;
                var context=new AttackContext(enemy,target,enemy.LeftHand,4,game);
                var type=(enemy.Character as IAttackTypeProvider)?.AttackTypeOf(context)??AttackDamageType.Slashing;
                if(type==AttackDamageType.None) type=AttackDamageType.Slashing;
                if(!AttackDamageTypes.IsMagic(type)) type=AttackDamageType.Bludgeoning;
                int weight=10+System.Math.Max(0,StatusRules.EffectiveAttackBonus(enemy))*2;
                if(target.HasResistance(type)) weight=0;
                weights.TryGetValue(type,out int old);weights[type]=old+weight;
            }
            var best=AttackDamageType.Bludgeoning;int score=-1;
            foreach(var type in ResistanceNames.Choices)
                if(weights.TryGetValue(type,out int value) && value>score) {best=type;score=value;}
            return best;
        }
        public bool ShouldStoreBarrier(PlayerState owner, int points, GameManager game)
        {
            var state=CharacterBehaviors.FirstImplementer<IAbjurationSkill>(owner);
            if(state==null || state.BarrierPoints>=12) return false;
            foreach(var enemy in game.OpponentsOf(owner))
                if(enemy.IsAlive && game.CanReach(owner,enemy)
                    && enemy.Health<=System.Math.Max(0,points+StatusRules.EffectiveAttackBonus(owner)-StatusRules.EffectiveDefenseBonus(enemy))) return false;
            return owner.Health<=10 && state.BarrierPoints<8 || points==4 && state.BarrierPoints<4;
        }
        public PlayerState NecroticTarget(PlayerState owner, GameManager game)
        {
            PlayerState best=null;int bestScore=int.MinValue;
            foreach(var enemy in game.OpponentsOf(owner))
            {
                if(!enemy.IsAlive || !StatusRules.CanAttackTarget(owner,enemy,game)
                    || ((enemy.Character as IAttackTargetRule)?.IsImmuneToAttack(enemy)??false)) continue;
                int score=100-enemy.Health+(enemy.HasStatus(StatusEffect.Decay)?0:20);
                bool shield=enemy.LeftHand.IsShield||enemy.RightHand.IsShield;
                bool block=CharacterBehaviors.FirstImplementer<IAttackBlocker>(enemy)!=null
                    && (enemy.LeftHand.Value==2&&!enemy.LeftHand.IsLocked || enemy.RightHand.Value==2&&!enemy.RightHand.IsLocked);
                if(!shield && !block && enemy.Health<=1) score+=1000;
                if(shield||block) score-=30;
                if(score>bestScore) {best=enemy;bestScore=score;}
            }
            return best;
        }
        public int ExchangeValue(PlayerState owner, int value)
            => HandValueProfiles.For(owner?.Definition).WeightOf(value);
    }
}
