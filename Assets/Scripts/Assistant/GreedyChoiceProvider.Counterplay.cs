using System;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        public bool UseValidatedTuning { get; set; }
        /// <summary>实验开关：按运算后数字评估所有敌方可借用的新机会；预览不修改战局。</summary>
        public bool UseProjectedCounterplay { get; set; }
        private int ProjectedCounterplayScore(LegalMove move,PlayerState actor,GameManager game)
        {
            if(game==null||actor==null||move.ActedHand==null||DenyRatioPercent<=0)return 0;
            int next=DruidScoredResult(move,actor,game),other=actor.GetOtherHand(move.ActedHand).Value;
            int worst=0;
            foreach(var foe in game.Players)
            {
                if(foe==actor||!foe.IsAlive||game.IsKnownAlly(actor,foe)||!StatusRules.CanAct(foe))continue;
                int best=0;
                foreach(var hand in foe.Hands)
                {
                    if(!hand.CanBeOperated)continue;
                    foreach(int addend in new[]{next,other})
                    {
                        int result=StatusRules.ApplyResultModifiers(foe,Hand.ComputeSum(hand.Value,addend));
                        var effect=CharacterBehaviors.DisplayedEffectOf(foe,result);
                        var profile=HandValueProfiles.For(foe.Definition);
                        int value=profile.WeightOf(result)/4;
                        switch(effect)
                        {
                            case HandEffectType.Attack4:case HandEffectType.Attack6:case HandEffectType.Attack8:
                                if(game.CanReach(foe,actor)&&!foe.HasStatus(StatusEffect.Disabled))value+=IsLowHealth(actor)?60:24;
                                break;
                            case HandEffectType.Shield:value+=IsLowHealth(foe)?45:15;break;
                            case HandEffectType.Heal:
                                if(!foe.HasStatus(StatusEffect.Decay)&&foe.Health<foe.MaximumHealth)value+=IsLowHealth(foe)?50:15;
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
