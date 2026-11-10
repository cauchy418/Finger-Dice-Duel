using System;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : ITacticianOperationPolicy
    {
        public bool UseValidatedTuning { get; set; }
        /// <summary>实验开关：按运算后数字评估所有敌方可借用的新机会；预览不修改战局。</summary>
        public bool UseProjectedCounterplay { get; set; }
        public bool ChooseSubtraction(GameManager game, PlayerState actor, Hand source, Hand operand)
        {
            var move = new LegalMove(actor, source, operand);
            int plus = game.PreviewTacticianResult(actor, source, operand, false);
            int minus = game.PreviewTacticianResult(actor, source, operand, true);
            return ScoreMove(move, actor, game, minus) > ScoreMove(move, actor, game, plus);
        }

        private int ProjectedCounterplayScore(LegalMove move,PlayerState actor,GameManager game,int? operationResult=null)
        {
            if(game==null||actor==null||move.ActedHand==null||DenyRatioPercent<=0)return 0;
            int next=operationResult??DruidScoredResult(move,actor,game),other=actor.GetOtherHand(move.ActedHand).Value;
            int worst=0;
            foreach(var foe in game.Players)
            {
                if(foe==actor||!foe.IsAlive||game.IsKnownAlly(actor,foe)||!StatusRules.CanAct(foe)
                    || game.HasCrowdGambling(foe) || game.MustUseGambling(foe))continue;
                int best=0;
                int operators=game.CanChooseTacticianOperation(foe)?2:1;
                foreach(var hand in foe.Hands)
                {
                    if(!hand.CanBeOperated)continue;
                    foreach(var addendHand in new[]{move.ActedHand,actor.GetOtherHand(move.ActedHand)})
                    for(int op=0;op<operators;op++)
                    {
                        if(!StatusRules.CanUseHandAsAddend(foe,addendHand,game)||addendHand.IsConcealed)continue;
                        int addend=ReferenceEquals(addendHand,move.ActedHand)?next:other;
                        int result=AiStatePolicy.PreviewResult(foe,hand.Value,addend,op!=0);
                        var effect=CharacterBehaviors.DisplayedEffectOf(foe,result);
                        var profile=ActiveHandValueProfile(foe,game);
                        int value=profile.WeightOf(result)/4;
                        switch(effect)
                        {
                            case HandEffectType.Attack4:case HandEffectType.Attack6:case HandEffectType.Attack8:
                                if (game.CanReach(foe, actor) && StatusRules.CanAttackTarget(foe, actor, game))
                                {
                                    int damage = EstimateAttackDamage(foe, actor, result, game, hand.Value);
                                    if (damage > 0) value += Math.Min(IsLowHealth(actor) ? 60 : 32, damage * (IsLowHealth(actor) ? 8 : 4));
                                }
                                break;
                            case HandEffectType.Shield:if(!foe.HasStatus(StatusEffect.Tinnitus))value+=IsLowHealth(foe)?45:15;break;
                            case HandEffectType.Heal:
                                if(StatusRules.CanHeal(foe)&&foe.Health<foe.MaximumHealth)value+=IsLowHealth(foe)?50:15;
                                break;
                        }
                        int remaining=foe.GetOtherHand(hand).Value;
                        foreach(var combo in profile.Combos)if(combo.Matches(result,remaining))value+=Math.Min(40,combo.Bonus/4);
                        best=Math.Max(best,value);
                    }
                }
                worst=Math.Max(worst,best);
            }
            return -worst*DenyRatioPercent/100;
        }
        public int ProjectedCounterplayScoreForTest(LegalMove move,PlayerState actor,GameManager game)
            =>ProjectedCounterplayScore(move,actor,game);

        // Current sampled high-performing opponents: evaluate their actual arithmetic opportunities,
        // including setup at full health. This does not alter any role's rules or sampling weights.
        private static bool IsAuditedCounterplayRole(PlayerState foe)
            => CharacterBehaviors.FirstImplementer<PathOfTheBeastLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<SwordsmanLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<OathOfDevotionLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<TheGreatOldOneLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<PathOfTheBerserkerLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<DraconicBloodlineLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<SchoolOfIllusionLogic>(foe) != null
                || CharacterBehaviors.FirstImplementer<OathOfConquestLogic>(foe) != null;

        private static int AuditedOpportunityDelta(LegalMove move, PlayerState actor, int result, PlayerState foe, GameManager game)
        {
            if (game.WillSuppressOperationEffects(foe)) return 0;
            int before = 0, after = 0;
            foreach (var source in foe.Hands)
            {
                if (!source.CanBeOperated) continue;
                foreach (var operand in actor.Hands)
                {
                    if (operand.IsConcealed || operand.IsTargetRestricted || !StatusRules.CanUseHandAsAddend(foe, operand, game)) continue;
                    int previous = AiStatePolicy.PreviewResult(foe, source.Value, operand.Value);
                    int projected = AiStatePolicy.PreviewResult(foe, source.Value, ReferenceEquals(operand, move.ActedHand) ? result : operand.Value);
                    before = Math.Max(before, AuditedOpportunity(foe, actor, source, previous, game));
                    after = Math.Max(after, AuditedOpportunity(foe, actor, source, projected, game));
                }
            }
            return after - before;
        }

        private static int AuditedOpportunity(PlayerState foe, PlayerState victim, Hand source, int result, GameManager game)
        {
            int other = foe.GetOtherHand(source).Value;
            int setup = (foe.Character as IAiMoveAdvisor)?.ScoreMoveBonus(new MoveValueContext(source, result, victim.LeftHand, other)) ?? 0;
            foreach (var combo in ActiveHandValueProfile(foe, game).Combos)
                if (combo.Matches(result, other)) setup += Math.Min(40, combo.Bonus / 4);
            int value = Math.Min(60, Math.Max(0, setup));
            var effect = DivineScoredEffect(foe, result, game);
            if (effect == HandEffectType.Heal) value += HealingGain(foe, 9) * 3;
            if (effect == HandEffectType.Shield && !foe.HasStatus(StatusEffect.Tinnitus)) value += 16;
            if (!IsAttackEffect(effect) || !game.CanReach(foe, victim) || !StatusRules.CanAttackTarget(foe, victim, game)) return value;
            int damage = EstimateAttackDamage(foe, victim, result, game, source.Value);
            value += damage * 5;
            if (damage > 0)
            {
                if (CharacterBehaviors.FirstImplementer<TheGreatOldOneLogic>(foe) != null) value += 12;
                if (CharacterBehaviors.FirstImplementer<PathOfTheBerserkerLogic>(foe) != null && foe.Health <= 15)
                    value += HealingGain(foe, damage / 2) * 2;
                if (CharacterBehaviors.FirstImplementer<DraconicBloodlineLogic>(foe) != null && !victim.HasStatus(StatusEffect.HolyLight)) value += 8;
                if (CharacterBehaviors.FirstImplementer<OathOfConquestLogic>(foe) != null && other == 5) value += 16;
            }
            return value;
        }
    }
}
