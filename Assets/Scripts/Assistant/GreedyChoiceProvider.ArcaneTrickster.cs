using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        private double MageHandExchangeScore(int option, PlayerState owner, GameManager game)
        {
            if (option == 0) return 0;
            var skill = ArcaneTricksterLogic.For(owner);
            var hand = option == 1 ? owner.LeftHand : owner.RightHand;
            if (skill == null || !skill.CanExchange(owner, hand, game) || hand.Value == skill.MageHandValue) return -1000;
            // Compare projected digits directly; scoring must not alter the live model.
            double before = MageHandBoardValue(owner, null, 0, game);
            double after = MageHandBoardValue(owner, hand, skill.MageHandValue, game);
            if (hand.IsShield && skill.MageHandValue != 5) after -= 24;
            var profile = HandValueProfiles.For(owner.Definition);
            // Stored digits become usable only at a later turn end, so discount their value.
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
                if (!source.CanBeOperated) continue;
                double sourceBest = 0;
                foreach (var player in game.Players)
                {
                    if (!player.IsAlive || ReferenceEquals(player, owner)) continue;
                    foreach (var operand in player.Hands)
                    {
                        if (operand.IsConcealed || !StatusRules.CanUseHandAsAddend(owner, operand, game)) continue;
                        int result = AiStatePolicy.PreviewResult(owner, digit, operand.Value);
                        var effect = DivineScoredEffect(owner, result, game);
                        double score = 0;
                        if (effect == HandEffectType.Heal && StatusRules.CanHeal(owner))
                            score = Math.Min(9, owner.MaximumHealth - owner.Health) * 5;
                        else if (effect == HandEffectType.Shield) score = source.IsShield ? 4 : 22;
                        else if (IsAttackEffect(effect))
                            foreach (var target in game.Players)
                                if (target.IsAlive && !game.IsKnownAlly(owner, target) && game.CanReach(owner, target)
                                    && StatusRules.CanAttackTarget(owner, target, game))
                                    score = Math.Max(score, EstimateAttackDamage(owner, target, result, game, digit) * 6);
                        sourceBest = Math.Max(sourceBest, score);
                    }
                }
                if (sourceBest > best) { other = best; best = sourceBest; }
                else other = Math.Max(other, sourceBest);
            }
            value += best + other * .3;
            // Body digits are public addends. Deny strong opposing follow-up operations.
            foreach (var foe in game.Players)
            {
                if (!foe.IsAlive || game.IsKnownAlly(owner, foe) || !StatusRules.CanAct(foe)
                    || !game.CanReach(foe, owner) || !StatusRules.CanAttackTarget(foe, owner, game)) continue;
                int threat = 0;
                foreach (var source in foe.Hands)
                {
                    if (!source.CanBeOperated) continue;
                    foreach (var operand in owner.Hands)
                    {
                        if (operand.IsConcealed || !StatusRules.CanUseHandAsAddend(foe, operand, game)) continue;
                        int digit = ReferenceEquals(operand, changed) ? replacement : operand.Value;
                        int result = AiStatePolicy.PreviewResult(foe, source.Value, digit);
                        if (IsAttackEffect(DivineScoredEffect(foe, result, game)))
                            threat = Math.Max(threat, EstimateAttackDamage(foe, owner, result, game, source.Value));
                    }
                }
                value -= threat * 3;
            }
            return value;
        }
    }
}
