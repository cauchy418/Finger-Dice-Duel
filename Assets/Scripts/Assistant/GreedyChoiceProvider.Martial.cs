using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        public override int ChooseSkillOption(string key,IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game)
        {
            int best=options[0].Value;double bestScore=double.NegativeInfinity;
            foreach(var option in options)
            {
                double score;
                if(key==DivineChoices.Nine||key==DivineChoices.Retarget)score=DivineOptionScore(key,option.Value,owner,target,game);
                else if(key==ArcaneChoices.Enchantment||key==ArcaneChoices.KnightElement||key==ArcaneChoices.KnightCopy||key==ArcaneChoices.Bladesong)score=ArcaneOptionScore(key,option.Value,owner,target,game);
                else if(key==NewSubclassChoices.StormResistance) score=BloodlineScore((AttackDamageType)option.Value,owner,game);
                else if(key==NewSubclassChoices.LoreGrowth) score=option.Value==1?20:0;
                else if(key==NewSubclassChoices.LoreDefense)
                {bool shield=false;foreach(var h in owner.Hands)if(h.IsShield)shield=true;score=option.Value==0?0:shield&&target!=null&&CharacterFactory.MaxAttackHits(target)==1?-10:20;}
                else if(key==NewSubclassChoices.IllusionDefense)score=option.Value==1?20:0;
                else if(key==AncientRoleChoices.FiendPact) score=FiendPactScore(option.Value,owner,target,game);
                else if(key==MartialSkillChoices.Draconic) score=BloodlineScore((AttackDamageType)option.Value,owner,game);
                else if(key==MartialSkillChoices.OpenHand) score=OpenHandScore(option.Value,owner,target);
                else if(key==MartialSkillChoices.FourElements) score=ElementScore(option.Value,owner,target,game);
                else score=0;
                if(score>bestScore) {bestScore=score;best=option.Value;}
            }
            return best;
        }

        private static double OpenHandScore(int value,PlayerState owner,PlayerState target)
        {
            if(target==null) return value==0?1:0;
            if(value==0) return 2;
            if(target.HasStatus(StatusEffect.HolyLight)) return -10;
            if(value==-1)
                return target.HasResonance || target.Health<=6 ? -2 : 14+Math.Min(12,target.Health/2);
            var status=(StatusEffect)value;
            if(target.HasStatus(status)) return -3;
            if(status==StatusEffect.KnockedDown)
                return target.HasStatus(StatusEffect.Stunned)||target.HasStatus(StatusEffect.Frozen)?0:14+Math.Max(0,StatusRules.EffectiveDefenseBonus(target))*2;
            if(status==StatusEffect.Stunned)
                return target.HasStatus(StatusEffect.Frozen)?0:16+Math.Max(0,StatusRules.EffectiveAttackBonus(target))*2;
            return -10;
        }

        private static double ElementScore(int value,PlayerState owner,PlayerState target,GameManager game)
        {
            if(value==0)
            {
                bool danger=false;
                if(game!=null) foreach(var enemy in game.OpponentsOf(owner))
                    if(enemy.IsAlive && game.CanReach(enemy,owner)) danger=true;
                int ki=CharacterBehaviors.FirstImplementer<IKiResource>(owner)?.Ki??0;
                return danger && owner.Health<=8 && ki<=2 ? 20 : 1;
            }
            if(target==null) return 0;
            var type=(AttackDamageType)value;
            if(target.HasImmunity(type)) return -100;
            double score=ElementDamageScore(type,owner,target);
            if(type==AttackDamageType.Lightning && target.HasStatus(StatusEffect.Wet)) score+=15;
            if(type==AttackDamageType.Cold && (target.HasStatus(StatusEffect.Frost)||target.HasStatus(StatusEffect.Wet))) score+=22;
            if(type==AttackDamageType.Thunder && target.HasStatus(StatusEffect.Resonance)) score+=20;
            if(type==AttackDamageType.Fire && target.HasStatus(StatusEffect.Greased) && !target.HasImmunity(type)) score+=8;
            return score;
        }

        private static double BloodlineScore(AttackDamageType type,PlayerState owner,GameManager game)
        {
            double score=0;if(game==null) return 0;
            foreach(var enemy in game.OpponentsOf(owner))
            {
                if(!enemy.IsAlive) continue;
                score+=ElementDamageScore(type,owner,enemy);
                var attack=new AttackContext(enemy,owner,enemy.LeftHand,6,game);
                var incoming=(enemy.Character as IAttackTypeProvider)?.AttackTypeOf(attack)??AttackDamageType.None;
                if(incoming==type) score+=18+(game.CanReach(enemy,owner)?8:0);
            }
            return score;
        }

        private static double ElementDamageScore(AttackDamageType type,PlayerState owner,PlayerState target)
        {
            int damage=Math.Max(0,6+StatusRules.EffectiveAttackBonus(owner)-StatusRules.EffectiveDefenseBonus(target));
            var magic=AttackDamageTypes.ToDamageType(type);
            damage=StatusRules.ApplyDamageTypeInteraction(target,magic,damage,out var ignored,out var clear);
            damage=StatusRules.ApplyDamageAffinities(target,damage,type);
            if(target.HasImmunity(type)) return 0;
            double score=damage;
            var status=StatusNames.StatusOf(magic);
            if(status!=StatusEffect.None && !target.HasStatus(status) && !target.HasStatus(StatusEffect.HolyLight))
                score+=type==AttackDamageType.Cold?5:type==AttackDamageType.Thunder?4:3;
            if(target.Health<=damage) score+=15;
            if(type==AttackDamageType.Acid && target.HasStatus(StatusEffect.Poison)
                || type==AttackDamageType.Poison && target.HasStatus(StatusEffect.Acid)) score+=2;
            return score;
        }

        public override PlayerState HunterMarkTarget(PlayerState owner,IReadOnlyList<PlayerState> candidates,GameManager game)
        {
            PlayerState best=null;int bestScore=int.MinValue;
            foreach(var target in candidates)
            {
                if(!target.IsAlive || ReferenceEquals(target,owner) || game.IsTeammate(owner,target)) continue;
                bool marked=false;foreach(var source in CharacterBehaviors.Implementers<IHunterMarkSource>(owner)) if(source.IsMarked(target)) marked=true;
                int score=20+(target.MaximumHealth-target.Health)*3+Math.Max(0,StatusRules.EffectiveDefenseBonus(target))*4;
                if(game.CanReach(owner,target)) score+=15;
                score-=game.DistanceBetween(owner,target)*2;
                if(marked) score-=100;
                if(score>bestScore) {bestScore=score;best=target;}
            }
            return best;
        }
    }
}
