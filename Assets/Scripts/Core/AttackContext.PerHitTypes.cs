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

        private int ConfigureHitInteraction(int damage)
        {
            WasTypeImmune = Defender.HasImmunity(AttackType);
            _sonicCrossReaction = !WasTypeImmune && StatusRules.HasSonicCrossReaction(Defender, AttackType);
            _greaseReaction = !WasTypeImmune && AttackType == AttackDamageType.Fire && Defender.HasStatus(StatusEffect.Greased);
            _acidPoisonReaction = !WasTypeImmune && (AttackType == AttackDamageType.Acid && Defender.HasStatus(StatusEffect.Poison)
                || AttackType == AttackDamageType.Poison && Defender.HasStatus(StatusEffect.Acid));
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
