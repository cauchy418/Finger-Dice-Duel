using System;
using System.Collections.Generic;
using FingerDiceDuel.Core;
using FingerDiceDuel.Characters.SpecialLogics;

namespace FingerDiceDuel.Assistant
{
    public sealed partial class GreedyChoiceProvider : IDruidChoicePolicy
    {
        private static readonly HandValueProfile FormHandValues = new HandValueProfile();

        private static HandValueProfile ActiveHandValueProfile(PlayerState owner,GameManager game=null)
            {
            if(owner==null)return FormHandValues;
            if (owner.IsWildShaped || owner.IsPolymorphed) return AiStatePolicy.ApplyHandWeights(FormHandValues,owner,game);
            var profile=HandValueProfiles.For(owner.Definition);
            if(owner.Health>15)return AiStatePolicy.ApplyHandWeights(profile,owner,game);
            if(CharacterBehaviors.FirstImplementer<PathOfTheGiantLogic>(owner)!=null)
                profile=profile.Clone().Adjust(5,38).Adjust(9,35).Adjust(4,-48).Adjust(6,-54).Adjust(8,-60).Adjust(7,10);
            if(CharacterBehaviors.FirstImplementer<PathOfTheBerserkerLogic>(owner)!=null)
                profile=profile.Clone().Adjust(5,25).Adjust(9,35);
            return AiStatePolicy.ApplyHandWeights(profile,owner,game);
        }

        private static int EnemySporeRefillCost(PlayerState actor, PlayerState victim, GameManager game)
        {
            if (victim == null || victim.IsSummon || game.Players.Count <= 2) return 0;
            int cost = 0;
            foreach (var foe in game.Players)
                if (foe != actor && foe != victim && foe.IsAlive && !game.IsKnownAlly(actor, foe)
                    && CharacterBehaviors.FirstImplementer<ISporeDruid>(foe) != null
                    && foe.WildCharges < foe.InitialWildCharges && game.DistanceBetween(foe, victim) == 1)
                    cost += 200;
            return cost;
        }

        private static int DruidScoredResult(LegalMove move,PlayerState actor,GameManager game)
        {
            if(game==null)return AiStatePolicy.PreviewResult(actor,move.ActedHand.Value,move.TargetHand.Value);
            if(!DruidForms.IsDragon(actor)||game.DruidSecondAddend!=null)return game.PreviewOperationResult(actor,move.ActedHand,move.TargetHand);
            int first=actor.HasStatus(StatusEffect.Confusion)&&move.ActedHand.Value+move.TargetHand.Value>=10?1:Hand.ComputeSum(move.ActedHand.Value,move.TargetHand.Value);
            var hands=new List<Hand>();foreach(var next in game.GetLegalMoves(actor))if(ReferenceEquals(next.ActedHand,move.ActedHand))hands.Add(next.TargetHand);
            var second=game.GetDruidPolicy(actor)?.SecondAddend(actor,move.ActedHand,move.TargetHand,hands,game);
            int value=second==null?first:actor.HasStatus(StatusEffect.Confusion)&&first+second.Value>=10?1:Hand.ComputeSum(first,second.Value);
            return GameManager.ModifiedOperationValue(actor,value);
        }

        public int ChooseForm(PlayerState owner,string circle,IReadOnlyList<SkillChoiceOption> options,GameManager game,int wildForm=0)
        {
            if(!StatusRules.CanAct(owner))return 0;
            var enemies = game.OpponentsOf(owner);
            if(circle=="stars")
            {
                int selected = 0;
                int best = int.MinValue;
                foreach (var option in options)
                {
                    if (option.Value == 0) continue;
                    int score = FormValue(owner, game, enemies, wildForm, option.Value);
                    if (score > best) { best = score; selected = option.Value; }
                }
                return selected;
            }

            // Shape costs a normal action. Keep a ready finishing blow and spend
            // scarce charges when the extra pool or a better matchup is useful.
            var moves = game.GetLegalMoves(owner);
            foreach (var move in moves)
                if (IsLethal(move, owner, game, DruidScoredResult(move, owner, game)))
                    return 0;

            bool stars = DruidWildShape.Has<Characters.SpecialLogics.CircleOfTheStarsLogic>(owner.Character);
            int bestForm = 0;
            int threshold = owner.Health + 16 + (owner.WildCharges == 1 ? 5 : 0);
            if (owner.SporeLife > 0) threshold += 14;
            foreach (var option in options)
            {
                int form = option.Value;
                int cost = form >= 5 ? 2 : 1;
                if (form == 0 || owner.WildCharges < cost) continue;
                int score = FormValue(owner, game, enemies, form, stars ? 1 : 0);
                if (stars)
                    for (int star = 2; star <= 3; star++)
                        score = Math.Max(score, FormValue(owner, game, enemies, form, star));
                score -= 7 * (cost - 1);
                if (score > threshold) { threshold = score; bestForm = form; }
            }
            return bestForm;
        }

        private static int FormValue(PlayerState owner, GameManager game, IReadOnlyList<PlayerState> enemies, int form, int star)
        {
            var type = DruidWildShape.TypeOf(form, star);
            int range = DruidWildShape.RangeOf(form, star);
            int attack = DruidWildShape.AttackOf(form, star);
            int bestAttack = -15;
            int pressure = 0;
            foreach (var enemy in enemies)
            {
                if (!enemy.IsAlive) continue;
                int distance = game.DistanceBetween(owner, enemy);
                if (game.CanReach(enemy, owner) && StatusRules.CanAct(enemy)) pressure++;
                if (distance > range || enemy.HasImmunity(type)) continue;
                int damage = AttackContext.ApplyStatBonuses(6 + (form == 5 ? distance : 0), attack,
                    StatusRules.EffectiveDefenseBonus(enemy), ignorePositiveDefense: false);
                damage = StatusRules.ApplyDamageAffinities(enemy, damage, type);
                int value = 4 + damage * 2;
                if (form == 6 && !enemy.HasStatus(StatusEffect.HolyLight)) value += 5;
                if (form == 2 && StatusRules.CanHeal(owner)) value += damage / 2;
                bestAttack = Math.Max(bestAttack, value);
            }
            int score = DruidWildShape.Health[form] * 2 + bestAttack;
            score += DruidWildShape.DefenseOf(form, star) * Math.Min(6, 2 + 2 * pressure);
            if (form == 1 && pressure > 0) score += 3;
            if (form == 3) score -= 5; // 6 loses its attack effect.
            if (star == 3) score += 4;
            if (star == 2)
            {
                var patient = BestChalicePatient(owner, game.Players, game);
                score += 8 + Math.Min(8, patient == null ? 0 : HealingGain(patient, 9));
            }
            return score;
        }

        public bool ExitForm(PlayerState owner,IDruidForm form,GameManager game)
        {
            if (!owner.IsWildShaped || owner.Health > 3 || owner.WildOriginalHealth < owner.Health + 5) return false;
            int originalRange = (owner.DruidOriginal as IAttackRangeProvider)?.BaseAttackRange ?? 1;
            foreach (var enemy in game.OpponentsOf(owner))
            {
                if (!enemy.IsAlive || !game.CanReach(owner, enemy, originalRange)) continue;
                if (!game.CanReach(owner, enemy) || enemy.HasImmunity(DruidWildShape.TypeOf(owner.WildForm, owner.WildStar)))
                    return true;
            }
            // A living buffer still protects the original form on the enemy turn.
            return false;
        }

        public PlayerState ChaliceTarget(PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game)
            => BestChalicePatient(owner, targets, game);

        private static PlayerState BestChalicePatient(PlayerState owner,IReadOnlyList<PlayerState> targets,GameManager game)
        {
            PlayerState best = null;
            int score = 0;
            foreach (var target in targets)
            {
                if (!target.IsAlive || !game.IsKnownAlly(owner,target)) continue;
                int healed = HealingGain(target, 9);
                if (healed <= 0) continue;
                int value = healed * 4 + (target.Health <= 6 ? 8 : 0);
                if (value > score) { best = target; score = value; }
            }
            return best;
        }

        private static int HealingGain(PlayerState player, int amount)
            => StatusRules.CanHeal(player)
                ? Math.Min(player.MaximumHealth - player.Health,
                    amount)
                : 0;

        private static bool WantsSymbiosis(PlayerState owner, GameManager game)
        {
            if (owner.SporeLife > 0 || owner.WildCharges == 0) return false;
            if (owner.Health <= 12 || owner.WildCharges > 1) return true;
            foreach (var enemy in game.OpponentsOf(owner))
                if (enemy.IsAlive && (game.CanReach(enemy, owner)
                    || game.CanReach(owner, enemy) && !enemy.HasImmunity(AttackDamageType.Poison)))
                    return true;
            return false;
        }

        private static int DarknessValue(PlayerState owner, GameManager game)
        {
            if (owner == null || game == null) return 0;
            int value = 0;
            foreach (var player in game.Players)
            {
                if (player == owner || !player.IsAlive || game.DistanceBetween(owner, player) != 1
                    || player.StatusTurnsLeft(StatusEffect.DarknessBlind) >= 2 || player.HasStatus(StatusEffect.HolyLight)
                    || CharacterBehaviors.FirstImplementer<IShadowEyes>(player) != null) continue;
                if (game.IsKnownAlly(owner, player))
                    value -= StatusRules.CanAct(player) ? 20 : 8;
                else
                {
                    if (game.CanReach(player, owner) && !player.HasStatus(StatusEffect.Disabled)) value += 14;
                    if (game.CanReach(owner, player) && !player.HasImmunity(AttackDamageType.Necrotic)) value += 12;
                }
            }
            return value;
        }

        private static int SpecialistSetupValue(PlayerState owner, int result, int other, GameManager game)
        {
            int score = 0;
            if (CharacterBehaviors.DisplayedEffectOf(owner, result) == HandEffectType.None)
            {
                var mark = CharacterBehaviors.FirstImplementer<IHunterMarkSource>(owner);
                var curse = CharacterBehaviors.FirstImplementer<IHexbladeCurse>(owner);
                if (mark != null || curse != null)
                    foreach (var foe in game.OpponentsOf(owner))
                    {
                        if (!foe.IsAlive || !game.CanReach(owner, foe)) continue;
                        if (mark != null && !mark.IsMarked(foe) && game.DistanceBetween(owner, foe) <= 3)
                            score = Math.Max(score, 28 + HunterMarkValue(foe));
                        if (curse != null && !foe.HasStatus(StatusEffect.HolyLight)
                            && (curse.CurseTarget != foe || result > curse.CurseTurns))
                            score = Math.Max(score, 16 + Math.Min(result, 4) * 8);
                    }
            }
            if (CharacterBehaviors.FirstImplementer<IShadowEyes>(owner) != null && result + other == 10)
                score += Math.Min(52, Math.Max(0, DarknessValue(owner, game) * 2));
            if (CharacterBehaviors.FirstImplementer<IDruidCaster>(owner) == null) return score;
            if (result + other == 10 && owner.WildCharges > 0)
                score += owner.Health <= 12 ? 44 : 22;
            if (result == other && CharacterBehaviors.FirstImplementer<ISporeDruid>(owner) != null
                && WantsSymbiosis(owner, game))
                score += owner.Health <= 12 ? 70 : 46;
            if (result == 3 && CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.CircleOfTheLandLogic>(owner) != null)
                score += 30;
            return score;
        }

        private int SpecialistMoveBonus(LegalMove move, PlayerState actor, int result, int other, GameManager game)
        {
            int score = SpecialistSetupValue(actor, result, other, game);
            if (actor.IsPolymorphed) return score;
            if (!actor.IsWildShaped)
            {
                if (!IsAttackEffect(CharacterBehaviors.DisplayedEffectOf(actor, result))) return score;
                var bloodline = CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.DraconicBloodlineLogic>(actor);
                var hunter = CharacterBehaviors.FirstImplementer<IHunterMarkSource>(actor);
                var curse = CharacterBehaviors.FirstImplementer<IHexbladeCurse>(actor);
                bool storm = CharacterBehaviors.FirstImplementer<Characters.SpecialLogics.StormSorceryLogic>(actor) != null;
                if (bloodline == null && hunter == null && curse == null && !storm) return score;
                var target = ResolveIntendedVictim(move, actor, game);
                if (target == null || game.IsKnownAlly(actor, target) || !game.CanReach(actor, target)) return score;
                if (bloodline != null)
                {
                    var status = StatusNames.StatusOf(AttackDamageTypes.ToDamageType(bloodline.Element));
                    if (target.HasImmunity(bloodline.Element)) score -= 8;
                    else if (target.HasStatus(status) && !target.HasStatus(StatusEffect.HolyLight)) score += 14;
                }
                if (hunter?.IsMarked(target) == true)
                    score += 10 + 4 * EstimateAttackDamage(actor, target, result, game);
                if (curse?.CurseTarget == target) score += 24;
                if (storm || bloodline != null) score += (EstimateAttackDamage(actor, target, result, game) - result) * 5;
                return score;
            }
            if (actor.WildForm == 3 && result == 6) return score - 70;
            if (actor.WildStar == 2 && result == 9)
            {
                var patient = BestChalicePatient(actor, game.Players, game);
                if (patient != null)
                {
                    int extra = HealingGain(patient, 9);
                    if (patient == actor) extra = Math.Min(extra, actor.MaximumHealth - actor.Health - HealingGain(actor, 9));
                    score += 8 * extra;
                }
            }
            if (!IsAttackEffect(DruidWildShape.EffectOf(actor.WildForm, result))) return score;
            var victim = ResolveIntendedVictim(move, actor, game);
            if (victim == null || game.IsKnownAlly(actor, victim) || !game.CanReach(actor, victim)) return score - 55;
            int damage = TotalLethalDamage(move, actor, victim, result, game);
            if (damage == 0) return score - 50;
            if (actor.WildForm == 2) score += 8 * HealingGain(actor, damage / 2);
            if (actor.WildForm == 5) score += 8 * game.DistanceBetween(actor, victim);
            if (actor.WildForm == 6 && !victim.HasStatus(StatusEffect.HolyLight)
                && victim.StatusTurnsLeft(StatusEffect.Bleed) < 2) score += 14;
            return score;
        }

        private static int SpecialistCounterplayScore(LegalMove move, PlayerState actor, int result, GameManager game)
        {
            int risk = 0;
            var hand = move.ActedHand;
            foreach (var foe in game.Players)
            {
                if (!foe.IsAlive || foe == actor || game.IsKnownAlly(actor, foe) || !StatusRules.CanAct(foe)) continue;
                if (hand.IsConcealed || hand.IsTargetRestricted || !StatusRules.CanUseHandAsAddend(foe, hand, game)) continue;
                bool wild = foe.IsWildShaped && !foe.IsPolymorphed;
                bool tactician = game.CanChooseTacticianOperation(foe);
                bool champion = CharacterBehaviors.FirstImplementer<ChampionLogic>(foe) != null;
                if (!wild && CharacterBehaviors.FirstImplementer<IDruidCaster>(foe) == null
                    && CharacterBehaviors.FirstImplementer<IShadowEyes>(foe) == null
                    && CharacterBehaviors.FirstImplementer<GloomStalkerLogic>(foe) == null
                    && CharacterBehaviors.FirstImplementer<IHunterMarkSource>(foe) == null
                    && CharacterBehaviors.FirstImplementer<IHexbladeCurse>(foe) == null && !tactician && !champion) continue;
                int before = 0, after = 0;
                foreach (var source in foe.Hands)
                {
                    if (!source.CanBeOperated) continue;
                    int other = foe.GetOtherHand(source).Value;
                    int oldResult = AiStatePolicy.PreviewResult(foe,source.Value,hand.Value);
                    int newResult = AiStatePolicy.PreviewResult(foe,source.Value,result);
                    before = Math.Max(before, SpecialistThreat(foe, actor, oldResult, other, game, wild));
                    after = Math.Max(after, SpecialistThreat(foe, actor, newResult, other, game, wild));
                    if ((tactician || champion && source.Value == 1) && game.CanReach(foe, actor) && StatusRules.CanAttackTarget(foe, actor, game))
                    {
                        before = Math.Max(before, FighterThreat(hand.Value, oldResult));
                        after = Math.Max(after, FighterThreat(result, newResult));
                    }

                    int FighterThreat(int operand, int plus)
                    {
                        int value = IsAttackEffect(DivineScoredEffect(foe, plus, game)) ? EstimateAttackDamage(foe, actor, plus, game, source.Value) : 0;
                        if (!tactician) return value * 5;
                        int minus = AiStatePolicy.PreviewResult(foe,source.Value,operand,true);
                        if (IsAttackEffect(DivineScoredEffect(foe, minus, game)))
                            value = Math.Max(value, EstimateAttackDamage(foe, actor, minus, game, source.Value));
                        return value * 5;
                    }
                }
                risk += after - before;
            }
            return -Clamp(risk, -60, 60);
        }

        private static int SpecialistThreat(PlayerState foe, PlayerState actor, int result, int other, GameManager game, bool wild)
        {
            if (!wild)
            {
                int setup = SpecialistSetupValue(foe, result, other, game);
                var ambush = CharacterBehaviors.FirstImplementer<GloomStalkerLogic>(foe);
                bool ambushing = ambush?.IsAmbushing == true;
                if (ambush != null && !ambushing && result == other && result >= 1 && result <= GloomStalkerLogic.AmbushMaxValue)
                    setup = Math.Max(setup, 32);

                bool marked = CharacterBehaviors.FirstImplementer<IHunterMarkSource>(foe)?.IsMarked(actor) == true;
                if ((marked || ambushing) && IsAttackEffect(DivineScoredEffect(foe, result, game))
                    && game.CanReach(foe, actor) && StatusRules.CanAttackTarget(foe, actor, game))
                    setup = Math.Max(setup, EstimateAttackDamage(foe, actor, result, game) * 5);
                return setup;
            }
            var effect = DruidWildShape.EffectOf(foe.WildForm, result);
            if (effect == HandEffectType.Shield && foe.WildForm == 1 && result == 4) return 24;
            if (effect == HandEffectType.Heal && foe.WildStar == 2)
                return HealingGain(foe, 9) * 4 + 12;
            if (!IsAttackEffect(effect) || !game.CanReach(foe, actor)
                || !StatusRules.CanAttackTarget(foe, actor, game) || foe.HasStatus(StatusEffect.Disabled)) return 0;
            int damage = AttackContext.ApplyStatBonuses(result + (foe.WildForm == 5 ? game.DistanceBetween(foe, actor) : 0),
                StatusRules.EffectiveAttackBonus(foe), StatusRules.EffectiveDefenseBonus(actor), ignorePositiveDefense: false);
            damage = StatusRules.ApplyDamageAffinities(actor, damage, DruidWildShape.TypeOf(foe.WildForm, foe.WildStar));
            return damage * 3 + (damage > 0 && foe.WildForm == 6 && !actor.HasStatus(StatusEffect.HolyLight) ? 12 : 0);
        }

        private static int DragonPreparation(PlayerState owner, Hand source, int value, GameManager game)
        {
            int best = 0;
            foreach (var player in game.Players)
            {
                if (!player.IsAlive || player == owner) continue;
                foreach (var addend in player.Hands)
                {
                    if (addend.IsConcealed || addend.IsTargetRestricted || !StatusRules.CanUseHandAsAddend(owner, addend, game)) continue;
                    int next = StatusRules.ApplyResultModifiers(owner, Hand.ComputeSum(value, addend.Value));
                    var effect = DruidWildShape.EffectOf(owner.WildForm, next);
                    int score = 0;
                    if (IsAttackEffect(effect) && !game.IsKnownAlly(owner, player) && game.CanReach(owner, player)
                        && StatusRules.CanAttackTarget(owner, player, game) && !owner.HasStatus(StatusEffect.Disabled))
                    {
                        var type = DruidWildShape.TypeOf(owner.WildForm, owner.WildStar);
                        int damage = AttackContext.ApplyStatBonuses(next + (owner.WildForm == 5 ? game.DistanceBetween(owner, player) : 0),
                            StatusRules.EffectiveAttackBonus(owner), StatusRules.EffectiveDefenseBonus(player), ignorePositiveDefense: false);
                        score = 4 * StatusRules.ApplyDamageAffinities(player, damage, type);
                    }
                    else if (effect == HandEffectType.Heal) score = 5 * HealingGain(owner, 9);
                    else if (effect == HandEffectType.Shield && !source.IsShield) score = 16;
                    best = Math.Max(best, score);
                }
            }
            return best;
        }
        public Hand SecondAddend(PlayerState owner,Hand acted,Hand first,IReadOnlyList<Hand> candidates,GameManager game)
        {
            Hand best=null;int bestScore=int.MinValue;
            int intermediate=Hand.ComputeSum(acted.Value,first.Value);
            foreach(var hand in candidates)
            {
                int result=StatusRules.ApplyResultModifiers(owner,Hand.ComputeSum(intermediate,hand.Value));
                var effect=CharacterBehaviors.DisplayedEffectOf(owner,result);int score=result;
                if(effect==HandEffectType.Attack4||effect==HandEffectType.Attack6||effect==HandEffectType.Attack8)score+=result*10;
                if(effect==HandEffectType.Heal)score+=Math.Min(9,owner.MaximumHealth-owner.Health)*10;
                if(effect==HandEffectType.Shield&&!acted.IsShield)score+=40;
                if(score>bestScore){bestScore=score;best=hand;}
            }
            return best;
        }
    }
}
