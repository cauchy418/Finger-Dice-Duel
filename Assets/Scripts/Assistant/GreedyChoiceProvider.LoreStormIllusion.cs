using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : INewSubclassChoicePolicy,IIllusionChoicePolicy
    {
        public bool RedirectIllusion(IllusionTouch touch,GameManager game)
        {
            int original=IllusionOperationValue(touch,touch.Selected,game);
            int alternate=IllusionOperationValue(touch,touch.Alternate,game);
            return game.IsKnownAlly(touch.Owner,touch.Actor)?alternate>original:alternate<original;
        }
        private static int IllusionOperationValue(IllusionTouch touch,Hand addend,GameManager game)
        {
            var actor=touch.Actor;
            int value=StatusRules.ApplyResultModifiers(actor,Hand.ComputeSum(touch.SourceValue,addend.Value));
            var profile=HandValueProfiles.For(actor.Definition);int score=profile.WeightOf(value);
            int other=actor.GetOtherHand(touch.Source).Value;
            foreach(var combo in profile.Combos)if(combo.Matches(value,other))score+=Math.Min(80,combo.Bonus);
            if(touch.Effectless)return score;
            var effect=CharacterBehaviors.DisplayedEffectOf(actor,value);
            if(effect==HandEffectType.Heal)
            {
                if(StatusRules.CanHeal(actor))score+=Math.Min(9,actor.MaximumHealth-actor.Health)*12;
                if(!actor.HasStatus(StatusEffect.WarBlessing))return score;
            }
            if(effect==HandEffectType.Shield&&!actor.HasStatus(StatusEffect.Tinnitus))score+=touch.Source.IsShield?5:45;
            if(DivineChoices.IsAttack(effect)||value==9&&actor.HasStatus(StatusEffect.WarBlessing))
            {
                int best=0;
                foreach(var target in game.Players)
                {
                    if(!target.IsAlive||game.IsKnownAlly(actor,target)||!game.CanReach(actor,target)||!StatusRules.CanAttackTarget(actor,target,game))continue;
                    int damage=actor.HasStatus(StatusEffect.Disabled)?0:Math.Max(0,value+StatusRules.EffectiveAttackBonus(actor)-StatusRules.EffectiveDefenseBonus(target)+ArcaneChoices.BonusAgainst(actor,target));
                    int gain=damage*12+(damage>=target.Health?200:0);
                    if(target.LeftHand.IsShield||target.RightHand.IsShield)gain/=2;
                    best=Math.Max(best,gain);
                }
                score+=best;
            }
            return score;
        }
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
