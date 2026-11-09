using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider
    {
        public override int ChooseSkillOption(string key,IReadOnlyList<SkillChoiceOption> options,PlayerState owner,PlayerState target,GameManager game)
        {
            if(key=="warden_oath")return owner.HasImmunity(AttackDamageType.Force)||!owner.HasAnyShield?1:0;
            if(key=="giant_element_blade")
            {
                var giant=CharacterBehaviors.FirstImplementer<PathOfTheGiantLogic>(owner);
                if(owner.Health<=10||giant?.Enchantment!=AttackDamageType.None)return 0;
                PlayerState enemy=null;foreach(var p in game.OpponentsOf(owner))if(p.IsAlive&&(enemy==null||p.Health<enemy.Health))enemy=p;
                if(enemy==null)return 0;
                int selected=0;double bladeBest=0;foreach(var option in options)if(option.Value!=0){double value=ElementScore(option.Value,owner,enemy,game);if(value>bladeBest){bladeBest=value;selected=option.Value;}}return selected;
            }
            if(key=="druid_symbiosis")return WantsSymbiosis(owner,game)?1:0;
            if(key==ShadowRules.Darkness)return DarknessValue(owner,game)>0?1:0;
            if (key == GamblerLogic.RerollChoice)
            {
                foreach (var option in options)
                    if (option.Value > 0 && WantsGambleReroll(owner, option.Value, game)) return option.Value;
                return 0;
            }
            int best=options[0].Value;double bestScore=double.NegativeInfinity;
            foreach(var option in options)
            {
                double score;
                if(key==DivineChoices.Nine||key==DivineChoices.Retarget)score=DivineOptionScore(key,option.Value,owner,target,game);
                else if(key==ArcaneChoices.TransmutationOperation||key==ArcaneChoices.Enchantment||key==ArcaneChoices.KnightElement||key==ArcaneChoices.KnightCopy||key==ArcaneChoices.Bladesong)score=ArcaneOptionScore(key,option.Value,owner,target,game);
                else if(key==NewSubclassChoices.StormResistance) score=BloodlineScore((AttackDamageType)option.Value,owner,game);
                else if(key==NewSubclassChoices.LoreGrowth) score=option.Value==1?20:0;
                else if(key==NewSubclassChoices.LoreDefense)
                {bool shield=false;foreach(var h in owner.Hands)if(h.IsShield)shield=true;score=option.Value==0?0:shield&&target!=null&&CharacterFactory.MaxAttackHits(target)==1?-10:20;}
                else if(key==NewSubclassChoices.IllusionDefense)score=option.Value==1?20:0;
                else if(key==AncientRoleChoices.FiendPact) score=FiendPactScore(option.Value,owner,target,game);
                else if(key==MartialSkillChoices.Draconic) score=BloodlineScore((AttackDamageType)option.Value,owner,game);
                else if(key==MonkRules.Ninja||key==MonkRules.Taichi||key==MonkRules.Defense||key==MonkRules.Harmony||key==MonkRules.Drunken)score=MonkOptionScore(key,option.Value,owner,target,game);
                else if(key==DivinationRules.Key)score=DivinationRules.AiScore(option.Value,owner,target,game);
                else if(key==MartialSkillChoices.OpenHand) score=OpenHandScore(option.Value,owner,target);
                else if(key==MartialSkillChoices.FourElements) score=FourElementsScore(option.Value,owner,target,game);
                else score=0;
                if(score>bestScore) {bestScore=score;best=option.Value;}
            }
            return best;
        }

        private static double OpenHandScore(int value,PlayerState owner,PlayerState target)
        {
            if(target==null) return value==0?1:0;
            if(value==0) return 2;
            if(target.HasStatus(StatusEffect.HolyLight)&&value!=-1) return -10;
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

        private static double FourElementsScore(int value, PlayerState owner, PlayerState target, GameManager game)
        {
            if (value == 0) return target == null ? 1 : ElementDamageScore(AttackDamageType.Bludgeoning, owner, target);
            int ki = CharacterBehaviors.FirstImplementer<IKiResource>(owner)?.Ki ?? 0;
            if (ki < 2) return -100;
            double cost = ki == 2 ? 4 : 2;
            if (owner.Health <= 8 && ki == 2) cost += 3;
            return ElementScore(value, owner, target, game) - cost;
        }

        private static bool WantsGambleReroll(PlayerState owner, int first, GameManager game)
        {
            if (owner == null || game == null || owner.Health <= GamblerLogic.RerollHealthDie
                || game.WillSuppressOperationEffects(owner)) return false;
            double replacement = 0;
            for (int face = 1; face <= GamblerLogic.GambleDie; face++)
                replacement += GambleOutcomeValue(owner, face, owner.Health - 3, game);
            double current = GambleOutcomeValue(owner, first, owner.Health, game);
            return replacement / GamblerLogic.GambleDie > current + (owner.Health <= 8 ? 4 : 3);
        }

        private static double GambleOutcomeValue(PlayerState owner, int roll, int health, GameManager game)
        {
            int value = StatusRules.ApplyResultModifiers(owner, roll);
            var effect = CharacterBehaviors.DisplayedEffectOf(owner, value);
            if (effect == HandEffectType.Heal)
                return StatusRules.CanHeal(owner) ? Math.Min(owner.MaximumHealth - health, 9) : 0;
            if (effect == HandEffectType.Shield)
                return owner.HasStatus(StatusEffect.Tinnitus) ? 0 : 4;
            if (!IsAttackEffect(effect) || owner.HasStatus(StatusEffect.Disabled)) return 0;
            double best = 0;
            foreach (var foe in game.OpponentsOf(owner))
            {
                if (!foe.IsAlive || !game.CanReach(owner, foe) || !StatusRules.CanAttackTarget(owner, foe, game)) continue;
                int damage = AttackContext.ApplyStatBonuses(value, StatusRules.EffectiveAttackBonus(owner),
                    StatusRules.EffectiveDefenseBonus(foe), ignorePositiveDefense: false);
                damage = StatusRules.ApplyDamageAffinities(foe, damage, AttackDamageType.Slashing);
                bool shielded = foe.LeftHand.IsShield || foe.RightHand.IsShield;
                best = Math.Max(best, shielded ? 2 : damage * 1.25 + (damage >= foe.HealthAcrossForms + foe.SporeLife + foe.FalseLife ? 8 : 0));
            }
            return best;
        }

        private static double BloodlineScore(AttackDamageType type,PlayerState owner,GameManager game)
        {
            double score=0;if(game==null) return 0;
            foreach(var enemy in game.OpponentsOf(owner))
            {
                if(!enemy.IsAlive) continue;
                score+=ElementDamageScore(type,owner,enemy, true);
                var attack=new AttackContext(enemy,owner,enemy.LeftHand,6,game);
                var incoming = CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.DraconicBloodlineLogic>(enemy)?.Element
                    ?? (enemy.Character as IAttackTypeProvider)?.AttackTypeOf(attack) ?? AttackDamageType.None;
                if(incoming==type && !DamageAffinityNames.AttackPiercesAffinities(enemy, type)) score+=18+(game.CanReach(enemy,owner)?8:0);
                else if ((type == AttackDamageType.Lightning || type == AttackDamageType.Thunder)
                    && CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.StormSorceryLogic>(enemy) != null
                    && !DamageAffinityNames.AttackPiercesAffinities(enemy, type))
                    score += game.CanReach(enemy, owner) ? 10 : 4;
            }
            return score;
        }

        private static double ElementDamageScore(AttackDamageType type,PlayerState owner,PlayerState target,bool ignoreAffinity = false)
        {
            int damage=Math.Max(0,6+StatusRules.EffectiveAttackBonus(owner)-StatusRules.EffectiveDefenseBonus(target));
            var magic=AttackDamageTypes.ToDamageType(type);
            bool immune = target.HasImmunity(type);
            if (!immune) damage=StatusRules.ApplyDamageTypeInteraction(target,magic,damage,out _,out _);
            ignoreAffinity |= DamageAffinityNames.AttackPiercesAffinities(owner, type);
            damage=StatusRules.ApplyDamageAffinities(target,damage,type,!ignoreAffinity,ignoreAffinity);
            if(immune) return damage;
            double score=damage;
            var status=StatusNames.StatusOf(magic);
            if(status!=StatusEffect.None && !target.HasStatus(status) && !target.HasStatus(StatusEffect.HolyLight))
                score+=(type==AttackDamageType.Cold?5:type==AttackDamageType.Thunder?4:3)*(target.HasResistance(type)?.5:1);
            if (target.HealthAcrossForms + target.SporeLife + target.FalseLife <= damage) score += 15;
            if(type==AttackDamageType.Acid && target.HasStatus(StatusEffect.Poison)
                || type==AttackDamageType.Poison && target.HasStatus(StatusEffect.Acid)) score+=2;
            return score;
        }

        public override PlayerState HunterVolleyTarget(PlayerState owner, IReadOnlyList<PlayerState> candidates, GameManager game)
        {
            PlayerState best = null; double bestScore = double.MinValue;
            foreach (var target in candidates)
            {
                if (!target.IsAlive || game.IsKnownAlly(owner, target)) continue;
                int score = RangerHunterLogic.ColossusBonus(target) * 3 - target.HealthAcrossForms;
                if (target.LeftHand.IsShield || target.RightHand.IsShield) score -= 8;
                if (target.HasStatus(StatusEffect.Panic)) score += 3;
                if (score > bestScore) { bestScore = score; best = target; }
            }
            return best;
        }

        public override PlayerState HunterMarkTarget(PlayerState owner,IReadOnlyList<PlayerState> candidates,GameManager game)
        {
            PlayerState best=null;int bestScore=0;
            foreach(var target in candidates)
            {
                if(!target.IsAlive || ReferenceEquals(target,owner) || game.IsKnownAlly(owner,target)) continue;
                bool marked=false;foreach(var source in CharacterBehaviors.Implementers<IHunterMarkSource>(owner)) if(source.IsMarked(target)) marked=true;
                int score = 20 + HunterMarkValue(target);
                if(game.CanReach(owner,target)) score+=15;
                score-=game.DistanceBetween(owner,target)*2;
                if(marked) continue;
                if ((target.LeftHand.IsShield || target.RightHand.IsShield) && StatusRules.EffectiveAttackBonus(owner) > 0) score += 6;
                if(score>bestScore) {bestScore=score;best=target;}
            }
            return best;
        }

        private static int HunterMarkValue(PlayerState target)
            => RangerHunterLogic.ColossusBonus(target) * 6
                + Math.Max(0, 20 - target.HealthAcrossForms)
                + Math.Max(0, StatusRules.EffectiveDefenseBonus(target)) * 4;

        private static bool WantsAmbush(PlayerState owner, Hand hidden, GameManager game)
        {
            if (owner == null || game == null) return false;
            Hand exposed = owner.GetOtherHand(hidden);
            foreach (var foe in game.Players)
            {
                if (!foe.IsAlive || foe == owner || game.IsKnownAlly(owner, foe)) continue;
                if (StatusRules.CanAct(foe) && game.CanReach(foe, owner)
                    && StatusRules.CanAttackTarget(foe, owner, game) && !foe.HasStatus(StatusEffect.Disabled)
                    && CharacterBehaviors.FirstImplementer<SchoolOfConjurationLogic>(foe) == null
                    && CanAttackWith(foe, null, exposed, game)) return true;
            }

            if (!exposed.CanBeOperated || owner.HasStatus(StatusEffect.Disabled)) return false;
            foreach (var foe in game.OpponentsOf(owner))
            {
                if (game.IsKnownAlly(owner, foe) || !game.CanReach(owner, foe, GloomStalkerLogic.AmbushRange)
                    || foe.LeftHand.IsShield || foe.RightHand.IsShield) continue;
                foreach (var player in game.Players)
                {
                    if (!player.IsAlive || player == owner) continue;
                    foreach (var addend in player.Hands)
                        if (CanAttackWith(owner, exposed, addend, game)) return true;
                }
                break;
            }
            return false;
        }

        private static bool CanAttackWith(PlayerState attacker, Hand onlySource, Hand addend, GameManager game)
        {
            if (addend.IsConcealed || addend.IsTargetRestricted || !StatusRules.CanUseHandAsAddend(attacker, addend, game))
                return false;
            foreach (var source in attacker.Hands)
            {
                if (!source.CanBeOperated || (onlySource != null && onlySource != source)) continue;
                int result = StatusRules.ApplyResultModifiers(attacker, Hand.ComputeSum(source.Value, addend.Value));
                if (IsAttackEffect(DivineScoredEffect(attacker, result, game))) return true;
            }
            return false;
        }
    }
}
