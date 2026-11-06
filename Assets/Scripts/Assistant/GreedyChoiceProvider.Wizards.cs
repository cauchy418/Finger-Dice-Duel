using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : IWizardChoicePolicy
    {
        public PlayerState ResistanceTarget(PlayerState owner, GameManager game)
        {
            PlayerState best = owner;
            int bestScore = -1;
            foreach (var target in game.Players)
            {
                if (!target.IsAlive || !AncientAlly(game, owner, target)) continue;
                var type = ResistanceType(owner, target, game);
                int score = ResistanceThreat(owner, target, type, game);
                score += (target.MaximumHealth - target.Health) / 2;
                if (score > 0 && target.Health <= 8) score += 14;
                if (score > bestScore) { best = target; bestScore = score; }
            }
            return best;
        }

        public AttackDamageType ResistanceType(PlayerState owner, PlayerState target, GameManager game)
        {
            var best = AttackDamageType.Bludgeoning;
            int bestScore = -1;
            foreach (var type in ResistanceNames.Choices)
            {
                int score = ResistanceThreat(owner, target, type, game);
                if (score > bestScore) { best = type; bestScore = score; }
            }
            return best;
        }

        private static int ResistanceThreat(PlayerState owner, PlayerState target, AttackDamageType group, GameManager game)
        {
            int score = 0;
            foreach (var enemy in game.Players)
            {
                if (!enemy.IsAlive || AncientAlly(game, owner, enemy) || !StatusRules.CanAct(enemy)
                    || !game.CanReach(enemy, target) || !StatusRules.CanAttackTarget(enemy, target, game)) continue;
                var context = new AttackContext(enemy, target, enemy.LeftHand, 6, game);
                var type = CharacterBehaviors.FirstImplementer<IFixedNormalAttackDamage>(enemy)?.NormalAttackType
                    ?? (enemy.Character as IAttackTypeProvider)?.AttackTypeOf(context) ?? AttackDamageType.Slashing;
                if (type == AttackDamageType.None) type = AttackDamageType.Slashing;
                if (DamageAffinityNames.AttackPiercesAffinities(enemy, type) || target.HasPermanentResistance(type)
                    || target.HasPermanentImmunity(type)) continue;
                if (AttackDamageTypes.IsPhysical(type)) type = AttackDamageType.Bludgeoning;
                if (type == group) score += 10 + System.Math.Max(0, StatusRules.EffectiveAttackBonus(enemy)) * 2;
            }
            return score;
        }

        private int AbjurationMoveBonus(PlayerState owner, int result, GameManager game)
        {
            if (result != 2 || CharacterBehaviors.FirstImplementer<IAbjurationSkill>(owner) == null) return 0;
            var target = ResistanceTarget(owner, game);
            var type = ResistanceType(owner, target, game);
            if (target.HasSustainedResistance(type) || ResistanceThreat(owner, target, type, game) == 0) return -44;
            return 25;
        }

        public bool ShouldStoreBarrier(PlayerState owner, int points, GameManager game)
        {
            var state = CharacterBehaviors.FirstImplementer<IAbjurationSkill>(owner);
            if (state == null || state.BarrierPoints >= points) return false;
            int outgoing = 0;
            int incoming = 0;
            foreach (var enemy in game.Players)
            {
                if (!enemy.IsAlive || AncientAlly(game, owner, enemy)) continue;
                if (game.CanReach(owner, enemy) && StatusRules.CanAttackTarget(owner, enemy, game))
                {
                    bool shielded = enemy.LeftHand.IsShield || enemy.RightHand.IsShield;
                    int damage = shielded ? 0 : EstimateAttackDamage(owner, enemy, points, game);
                    if (damage >= enemy.HealthAcrossForms) return false;
                    outgoing = System.Math.Max(outgoing, damage);
                }
                if (!game.CanReach(enemy, owner) || !StatusRules.CanAct(enemy)
                    || !StatusRules.CanAttackTarget(enemy, owner, game)
                    || owner.LeftHand.IsShield || owner.RightHand.IsShield) continue;
                foreach (var move in game.GetLegalMoves(enemy))
                {
                    int result = StatusRules.ApplyResultModifiers(enemy, Hand.ComputeSum(move.ActedHand.Value, move.TargetHand.Value));
                    if (IsAttackEffect(CharacterBehaviors.DisplayedEffectOf(enemy, result)))
                        incoming = System.Math.Max(incoming, EstimateAttackDamage(enemy, owner, result, game, move.ActedHand.Value));
                }
            }
            int prevented = System.Math.Min(incoming, points + state.BarrierPoints) - System.Math.Min(incoming, state.BarrierPoints);
            bool avoidsDeath = incoming - state.BarrierPoints >= owner.HealthAcrossForms
                && incoming - state.BarrierPoints - points < owner.HealthAcrossForms;
            return avoidsDeath || (prevented > 0 && prevented > outgoing && (owner.Health <= 12 || outgoing == 0));
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
