using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private static int MageHandReserveOpportunity(PlayerState foe, PlayerState victim, Hand source, int addend, GameManager game)
        {
            var skill = ArcaneTricksterLogic.For(foe);
            if (skill == null || !skill.CanExchange(foe, source, game) || game.WillSuppressOperationEffects(foe) || foe.HasStatus(StatusEffect.Dizzy)) return 0;
            int result = AiStatePolicy.PreviewResult(foe, skill.MageHandValue, addend);
            var effect = DivineScoredEffect(foe, result, game);
            if (effect == HandEffectType.Heal) return HealingGain(foe, 9) * 3;
            if (effect == HandEffectType.Shield && !foe.HasStatus(StatusEffect.Tinnitus)) return 16;
            if (IsAttackEffect(effect) && game.CanReach(foe, victim) && StatusRules.CanAttackTarget(foe, victim, game))
                return EstimateAttackDamage(foe, victim, result, game, skill.MageHandValue) * 5;
            return 0;
        }

        private static int MageHandAttackDamage(PlayerState attacker, PlayerState victim, int result,
            int sourceValue, bool shielded, GameManager game)
        {
            if (FeyCanCancel(victim, result) || (victim.Character as IAttackTargetRule)?.IsImmuneToAttack(victim) == true)
                return 0;
            if (shielded && CharacterFactory.MaxAttackHits(attacker) == 1
                && CharacterBehaviors.FirstImplementer<BreakerLogic>(attacker) == null) return 0;
            return EstimateAttackDamage(attacker, victim, result, game, sourceValue);
        }

        private double MageHandExchangeScore(int option, PlayerState owner, GameManager game)
        {
            if (option == 0) return 0;
            var skill = ArcaneTricksterLogic.For(owner);
            var hand = option == 1 ? owner.LeftHand : owner.RightHand;
            if (skill == null || !skill.CanExchange(owner, hand, game) || hand.Value == skill.MageHandValue) return -1000;
            // Compare projected digits directly; scoring must not alter the live model.
            double before = MageHandBoardValue(owner, null, 0, game);
            double after = MageHandBoardValue(owner, hand, skill.MageHandValue, game);
            var profile = HandValueProfiles.For(owner.Definition);
            // Stored digits become usable at the next start/end exchange, so discount their value.
            after += (profile.WeightOf(hand.Value) - profile.WeightOf(skill.MageHandValue)) * .08;
            return after - before - .1;
        }
        private double MageHandBoardValue(PlayerState owner, Hand changed, int replacement, GameManager game)
        {
            double best = 0, other = 0;
            var profile = HandValueProfiles.For(owner.Definition);
            double value = 0;
            foreach (var source in owner.Hands)
            {
                int digit = ReferenceEquals(source, changed) ? replacement : source.Value;
                value += profile.WeightOf(digit) * .12;
                bool shield = source.IsShield && (!ReferenceEquals(source, changed) || replacement == source.Value);
                if (shield) value += 24;
                if (!source.CanBeOperated) continue;
                double sourceBest = 0;
                foreach (var player in game.Players)
                {
                    if (!player.IsAlive || ReferenceEquals(player, owner)) continue;
                    foreach (var operand in player.Hands)
                    {
                        if (operand.IsConcealed || !StatusRules.CanUseHandAsAddend(owner, operand, game)) continue;
                        int result = game.PreviewOperationResult(owner, source, operand, digit, operand.Value);
                        var effect = game.OperationEffectsSuppressed(owner, source, operand)
                            ? HandEffectType.None : DivineScoredEffect(owner, result, game);
                        double score = 0;
                        if (effect == HandEffectType.Heal && StatusRules.CanHeal(owner))
                            score = HealingGain(owner, 9) * 5;
                        else if (effect == HandEffectType.Shield && !owner.HasStatus(StatusEffect.Tinnitus)) score = shield ? 4 : 22;
                        else if (IsAttackEffect(effect))
                            foreach (var target in game.Players)
                                if (target.IsAlive && !game.IsKnownAlly(owner, target) && game.CanReach(owner, target)
                                    && StatusRules.CanAttackTarget(owner, target, game) && !FeyCanCancel(target, result))
                                {
                                    int damage = MageHandAttackDamage(owner, target, result, digit, target.HasAnyShield, game);
                                    score = Math.Max(score, damage >= target.HealthAcrossForms ? 1000 + damage : damage * 6);
                                }
                        sourceBest = Math.Max(sourceBest, score);
                    }
                }
                if (sourceBest > best) { other = best; best = sourceBest; }
                else other = Math.Max(other, sourceBest);
            }
            // End exchange hands the board to the opponents. The rogue can exchange again
            // before its next action; an immediate attack/heal opportunity has no value now.
            if (ArcaneTricksterLogic.For(owner)?.ExchangeAtTurnStart != false)
                value += best + other * .3;
            // Body digits are public addends. Deny strong opposing follow-up operations.
            foreach (var foe in game.Players)
            {
                if (!foe.IsAlive || game.IsKnownAlly(owner, foe) || !StatusRules.CanAct(foe)
                    || !game.CanReach(foe, owner) || !StatusRules.CanAttackTarget(foe, owner, game)) continue;
                if (game.WillSuppressOperationEffects(foe) || foe.HasStatus(StatusEffect.Dizzy)) continue;
                bool hasShield = false;
                foreach (var body in owner.Hands)
                    if (body.IsShield && (!ReferenceEquals(body, changed) || replacement == body.Value)) hasShield = true;
                int threat = 0;
                foreach (var source in foe.Hands)
                {
                    if (!source.CanBeOperated) continue;
                    foreach (var operand in owner.Hands)
                    {
                        if (operand.IsConcealed || !StatusRules.CanUseHandAsAddend(foe, operand, game)) continue;
                        int digit = ReferenceEquals(operand, changed) ? replacement : operand.Value;
                        int result = game.PreviewOperationResult(foe, source, operand, source.Value, digit);
                        if (IsAttackEffect(DivineScoredEffect(foe, result, game)))
                        {
                            int damage = MageHandAttackDamage(foe, owner, result, source.Value, hasShield, game);
                            threat = Math.Max(threat, damage >= owner.HealthAcrossForms ? 1000 : damage);
                        }
                    }
                }
                value -= threat * 3;
            }
            return value;
        }
    }
}
