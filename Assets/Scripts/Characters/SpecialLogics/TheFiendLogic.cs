using FingerDiceDuel.Core;

namespace FingerDiceDuel.Characters.SpecialLogics
{
    /// <summary>邪术师·妖魔：治疗溢出转虚假生命，攻击可消耗虚假生命施加失能/恐慌。</summary>
    public sealed class TheFiendLogic : CharacterTemplate,
        IAttackTypeProvider, IHealingOverflow, IOnHitLanded
    {
        public override string DisplayName => "邪术师·妖魔";
        public override int DefaultDefenseBonus => -1;
        public override int BaseAttackRange => InfiniteAttackRange;

        public void OnHealingOverflow(PlayerState owner, int overflow)
        {
            owner?.GainFalseLife(overflow);
        }

        public override void OnResolveAttack(AttackContext context)
        {
            if (context?.Attacker == null || context.Defender == null) return;
            if (context.Attacker.FalseLife <= 0) return;

            context.Attacker.ClearFalseLife();
            context.Defender.ApplyStatus(StatusEffect.Disabled, 2, context.Attacker);
            context.Manager?.NoteSkill(context.Attacker, "妖魔契约", $"{context.Defender.Name} 失能 2 回合");
        }

        public void OnHitLanded(AttackContext context, GameManager manager)
        {
            if (context == null || context.AppliedDamage <= 0 || context.Attacker == null) return;
            if (context.Defender != null && context.Defender.IsAlive
                && context.Defender.Health * 2 <= context.Defender.MaximumHealth)
            {
                context.Attacker.Heal(3);
                manager?.NoteSkill(context.Attacker, "妖魔汲取", "回复 3 点");
            }
        }

        public override AttackDamageType AttackTypeOf(AttackContext context)
        {
            int distance = context?.Manager?.DistanceBetween(context.Attacker, context.Defender) ?? 1;
            return distance <= 1 ? AttackDamageType.Bludgeoning : AttackDamageType.Piercing;
        }
    }
}
