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
                    || foe.MustGamble && foe.Character is GamblerLogic)continue;
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
    }
}
