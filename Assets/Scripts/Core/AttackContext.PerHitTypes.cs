using System.Collections.Generic;

namespace FingerDiceDuel.Core
{
    public partial class AttackContext
    {
        public IPerHitAttackType PerHitTypeProvider { get; set; }
        public AttackDamageType PreparedPerHitType { get; set; }
        private bool _independentHitTypes;
        private readonly List<AttackDamageType> _segmentTypes = new List<AttackDamageType>();
        public IReadOnlyList<AttackDamageType> SegmentTypes => _segmentTypes;
        private readonly List<KeyValuePair<AttackDamageType, int>> _typedReflections = new List<KeyValuePair<AttackDamageType, int>>();

        private string _reactionVariant;
        private int ConfigureHitInteraction(int damage)
        {
            _visualReaction = AttackType==AttackDamageType.Cold&&(Defender.HasStatus(StatusEffect.Frost)||Defender.HasStatus(StatusEffect.Wet)||Defender.HasStatus(StatusEffect.Frozen)||Defender.HasStatus(StatusEffect.Burn))
                ||AttackType==AttackDamageType.Fire&&(Defender.HasStatus(StatusEffect.Frost)||Defender.HasStatus(StatusEffect.Wet)||Defender.HasStatus(StatusEffect.Greased))
                ||AttackType==AttackDamageType.Lightning&&Defender.HasStatus(StatusEffect.Wet)||AttackType==AttackDamageType.Thunder&&Defender.HasStatus(StatusEffect.Resonance);
            WasTypeImmune = Defender.HasImmunity(AttackType);
            _sonicCrossReaction = !WasTypeImmune && StatusRules.HasSonicCrossReaction(Defender, AttackType);
            _greaseReaction = !WasTypeImmune && AttackType == AttackDamageType.Fire && Defender.HasStatus(StatusEffect.Greased);
            _acidPoisonReaction = !WasTypeImmune && (AttackType == AttackDamageType.Acid && Defender.HasStatus(StatusEffect.Poison)
                || AttackType == AttackDamageType.Poison && Defender.HasStatus(StatusEffect.Acid));
            _visualReaction|=_sonicCrossReaction||_acidPoisonReaction;
            StatusEffect partner=StatusEffect.None;
            if(AttackType==AttackDamageType.Fire)partner=Defender.HasStatus(StatusEffect.Greased)?StatusEffect.Greased:Defender.HasStatus(StatusEffect.Frost)?StatusEffect.Frost:StatusEffect.Wet;
            else if(AttackType==AttackDamageType.Cold)partner=Defender.HasStatus(StatusEffect.Frozen)?StatusEffect.Frozen:Defender.HasStatus(StatusEffect.Burn)?StatusEffect.Burn:Defender.HasStatus(StatusEffect.Frost)?StatusEffect.Frost:StatusEffect.Wet;
            else if(AttackType==AttackDamageType.Lightning)partner=_sonicCrossReaction?StatusEffect.Resonance:StatusEffect.Wet;
            else if(AttackType==AttackDamageType.Thunder)partner=_sonicCrossReaction?StatusEffect.Shock:StatusEffect.Resonance;
            else if(AttackType==AttackDamageType.Acid)partner=StatusEffect.Poison;
            else if(AttackType==AttackDamageType.Poison)partner=StatusEffect.Acid;
            _reactionVariant=AttackType.ToString().ToLowerInvariant()+"_"+partner.ToString().ToLowerInvariant();
            InteractionStatus = StatusEffect.None;
            InteractionDamageType = DamageType.None;
            if (Manager == null || AttackType == AttackDamageType.None || WasTypeImmune) return damage;
            InteractionDamageType = AttackDamageTypes.ToDamageType(AttackType);
            StatusEffect status, clear;
            damage = StatusRules.ApplyDamageTypeInteraction(Defender, InteractionDamageType, damage, out status, out clear);
            InteractionStatus = status;
            return damage;
        }

        private void ApplyIndependentHitStatus(GameManager game)
        {
            if (_independentHitTypes && _elementHits.Count > 0)
                ApplyAttackerStatus(game, _elementHits[_elementHits.Count - 1]);
        }

        private void ResolveReflection(GameManager manager, int amount, AttackDamageType type)
        {
            if (amount <= 0 || !Attacker.IsAlive || manager == null) return;
            var reflected = new AttackContext(Defender, Attacker, _reflectionSource ?? Defender.LeftHand ?? Defender.RightHand, amount)
            {
                IsSpecialAttack = true, IgnoreStatBonuses = true, IsReflected = true,
                ReflectDepth = ReflectDepth + 1,
                AttackType = type == AttackDamageType.None ? AttackDamageType.Bludgeoning : type,
            };
            reflected.Resolve(manager.GetChoiceProvider(Attacker), manager);
            ReflectedDealt += reflected.AppliedDamage;
            ReflectedAttackType = reflected.AttackType;
        }
    }
}
